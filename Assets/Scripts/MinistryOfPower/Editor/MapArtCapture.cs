using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MinistryOfPower.Runtime;
using MinistryOfPower.Simulation;
using MinistryOfPower.UI;
using MinistryOfPower.UI.Map;

namespace MinistryOfPower.EditorTools
{
    /// <summary>Play-mode capture of North America map art (no ocean backdrop).</summary>
    public static class MapArtCapture
    {
        private const string ArmKey = "MoP.MapArtCaptureArmed";
        public const string OutDir = "Artifacts/map-art-new";
        public const string OutFile = "map-art-new.png";

        [MenuItem("Ministry of Power/Capture Map Art Shot")]
        public static void CaptureMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += CaptureMenu;
                return;
            }

            GameSettings.HelpSeen = true;
            RunSetup.Ensure().ConfigureNewGame("usa_like", DifficultyId.Normal);
            EditorSceneManager.OpenScene("Assets/Scenes/MinistryDesk.unity");
            SessionState.SetBool(ArmKey, true);
            EditorApplication.isPlaying = true;
            Debug.Log("Map art capture armed.");
        }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlay;
            EditorApplication.playModeStateChanged += OnPlay;
        }

        private static void OnPlay(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(ArmKey, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                var go = new GameObject("MapArtCaptureHost");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<MapArtCaptureHost>();
            }
            else if (change == PlayModeStateChange.ExitingPlayMode)
            {
                SessionState.SetBool(ArmKey, false);
            }
        }
    }

    public sealed class MapArtCaptureHost : MonoBehaviour
    {
        private IEnumerator Start()
        {
            float t0 = Time.realtimeSinceStartup;
            PoliticalMapPresenter map = null;
            while (Time.realtimeSinceStartup - t0 < 25f)
            {
                map = Object.FindFirstObjectByType<PoliticalMapPresenter>();
                if (map != null)
                {
                    map.EnsureBuilt();
                    var art = GameObject.Find("MapArt");
                    var ocean = GameObject.Find("MapOcean");
                    if (art != null && ocean == null) break;
                }
                yield return null;
            }

            var artGo = GameObject.Find("MapArt");
            var oceanGo = GameObject.Find("MapOcean");
            if (artGo == null)
            {
                Debug.LogError("MAP_ART_CAPTURE_FAIL: mapArt missing");
                EditorApplication.isPlaying = false;
                yield break;
            }

            if (oceanGo != null)
                Debug.LogWarning("MAP_ART_CAPTURE: MapOcean still present (expected removed)");

            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("MAP_ART_CAPTURE_FAIL: no Camera.main");
                EditorApplication.isPlaying = false;
                yield break;
            }

            // Frame CONUS; North America art is wider than the old plate.
            var camCtrl = Object.FindFirstObjectByType<MapCameraController>();
            camCtrl?.ApplyDefaultFrame(force: true);
            yield return null;

            Directory.CreateDirectory(MapArtCapture.OutDir);
            string path = Path.Combine(MapArtCapture.OutDir, MapArtCapture.OutFile);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(path));
            yield return new WaitForEndOfFrame();
            yield return null;

            var sr = artGo.GetComponent<SpriteRenderer>();
            string spriteName = sr != null && sr.sprite != null ? sr.sprite.name : "null";
            Bounds b = sr != null ? sr.bounds : default;
            Debug.Log("MAP_ART_CAPTURE_OK path=" + path
                      + " sprite=" + spriteName
                      + " ocean=" + (oceanGo != null)
                      + " boundsXZ=" + b.size.x.ToString("0.00") + "x" + b.size.z.ToString("0.00")
                      + " center=" + artGo.transform.position.ToString("F2"));

            EditorApplication.isPlaying = false;
        }
    }
}
