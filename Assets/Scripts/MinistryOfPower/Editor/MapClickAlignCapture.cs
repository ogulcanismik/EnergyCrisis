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
    /// <summary>Play-mode shot of map art + debug state wires for click-alignment review.</summary>
    public static class MapClickAlignCapture
    {
        private const string ArmKey = "MoP.MapClickAlignCapture";
        public const string OutDir = "Artifacts/map-click-align";
        public const string OutFile = "map-click-align.png";

        [MenuItem("Ministry of Power/Capture Map Click Align Shot")]
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
            Debug.Log("Map click-align capture armed.");
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
                var go = new GameObject("MapClickAlignCaptureHost");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<MapClickAlignCaptureHost>();
            }
            else if (change == PlayModeStateChange.ExitingPlayMode)
            {
                SessionState.SetBool(ArmKey, false);
            }
        }
    }

    public sealed class MapClickAlignCaptureHost : MonoBehaviour
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
                    if (GameObject.Find("MapArt") != null) break;
                }
                yield return null;
            }

            var artGo = GameObject.Find("MapArt");
            if (artGo == null)
            {
                Debug.LogError("MAP_CLICK_ALIGN_FAIL: MapArt missing");
                EditorApplication.isPlaying = false;
                yield break;
            }

            // Draw state rings so click regions are visible against the painted plate.
            DrawDebugWires();

            var camCtrl = Object.FindFirstObjectByType<MapCameraController>();
            camCtrl?.ApplyDefaultFrame(force: true);
            yield return null;
            yield return new WaitForEndOfFrame();

            Directory.CreateDirectory(MapClickAlignCapture.OutDir);
            string path = Path.Combine(MapClickAlignCapture.OutDir, MapClickAlignCapture.OutFile);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(path));
            yield return new WaitForEndOfFrame();
            yield return null;

            var sr = artGo.GetComponent<SpriteRenderer>();
            Bounds b = sr != null ? sr.bounds : default;
            Debug.Log("MAP_CLICK_ALIGN_OK path=" + path
                      + " artCenter=" + artGo.transform.position.ToString("F2")
                      + " boundsXZ=" + b.size.x.ToString("0.00") + "x" + b.size.z.ToString("0.00")
                      + " layoutCenter=" + UsaMapLayout.ArtWorldCenter
                      + " layoutSize=" + UsaMapLayout.ArtWorldSize);

            EditorApplication.isPlaying = false;
        }

        private static void DrawDebugWires()
        {
            var root = GameObject.Find("PoliticalMap");
            if (root == null) return;
            Transform existing = root.transform.Find("DebugStateWires");
            if (existing != null) Object.Destroy(existing.gameObject);

            var wireRoot = new GameObject("DebugStateWires");
            wireRoot.transform.SetParent(root.transform, false);

            for (int i = 0; i < UsaMapLayout.States.Length; i++)
            {
                UsaMapLayout.StatePoly poly = UsaMapLayout.States[i];
                if (poly.Code == "AK" || poly.Code == "HI") continue;

                var go = new GameObject("Wire_" + poly.Code);
                go.transform.SetParent(wireRoot.transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.loop = true;
                lr.widthMultiplier = 0.06f;
                lr.numCornerVertices = 2;
                lr.material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Standard"));
                lr.startColor = lr.endColor = new Color(1f, 0.85f, 0.2f, 0.85f);
                Vector3[] pts = PolygonMeshUtil.OutlineWorld(poly.Ring, UsaMapLayout.RegionHeight + 0.12f, closed: true);
                lr.positionCount = pts.Length;
                lr.SetPositions(pts);
            }
        }
    }
}
