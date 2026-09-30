using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MinistryOfPower.Runtime;
using MinistryOfPower.Simulation;
using MinistryOfPower.UI.Map;

namespace MinistryOfPower.EditorTools
{
    /// <summary>Play-mode capture of MapOcean placeholder under CONUS art.</summary>
    public static class OceanCapture
    {
        private const string ArmKey = "MoP.OceanCaptureArmed";
        public const string OutDir = "Artifacts/map-ocean";
        public const string OutFile = "map-ocean-placeholder.png";

        [MenuItem("Ministry of Power/Capture Map Ocean Shot")]
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
            Debug.Log("Ocean capture armed.");
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
                var go = new GameObject("OceanCaptureHost");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<OceanCaptureHost>();
            }
            else if (change == PlayModeStateChange.ExitingPlayMode)
            {
                SessionState.SetBool(ArmKey, false);
            }
        }
    }

    public sealed class OceanCaptureHost : MonoBehaviour
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
                    var ocean = GameObject.Find("MapOcean");
                    var art = GameObject.Find("MapArt");
                    if (ocean != null && art != null) break;
                }
                yield return null;
            }

            var oceanGo = GameObject.Find("MapOcean");
            var artGo = GameObject.Find("MapArt");
            if (oceanGo == null || artGo == null)
            {
                Debug.LogError("OCEAN_CAPTURE_FAIL: mapOcean=" + (oceanGo != null) + " mapArt=" + (artGo != null));
                EditorApplication.isPlaying = false;
                yield break;
            }

            var cam = Object.FindFirstObjectByType<MapCameraController>();
            cam?.ApplyDefaultFrame(force: true);

            var terminator = Object.FindFirstObjectByType<MapDayTerminator>();
            terminator?.EnsureOverlay();

            Directory.CreateDirectory(OceanCapture.OutDir);
            yield return null;
            yield return new WaitForEndOfFrame();

            string path = Path.Combine(OceanCapture.OutDir, OceanCapture.OutFile);
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            if (shot != null)
            {
                File.WriteAllBytes(path, shot.EncodeToPNG());
                Object.Destroy(shot);
            }
            else
            {
                ScreenCapture.CaptureScreenshot(path);
                yield return new WaitForEndOfFrame();
            }

            var osr = oceanGo.GetComponent<SpriteRenderer>();
            Debug.Log("OCEAN_CAPTURE_OK → " + Path.GetFullPath(path)
                      + " sort=" + (osr != null ? osr.sortingOrder.ToString() : "n/a")
                      + " sprite=" + (osr != null && osr.sprite != null ? osr.sprite.name : "null")
                      + " bounds=" + (osr != null ? osr.bounds.size.ToString("F2") : "n/a"));
            EditorApplication.isPlaying = false;
        }
    }
}
