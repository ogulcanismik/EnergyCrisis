using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MinistryOfPower.Runtime;
using MinistryOfPower.Simulation;
using MinistryOfPower.UI;

namespace MinistryOfPower.EditorTools
{
    /// <summary>Play-mode captures for HUD drawer layout checks.</summary>
    public static class HudFixCapture
    {
        private const string ArmKey = "MoP.HudFixCaptureArmed";

        [MenuItem("Ministry of Power/Capture HUD Fix Shots")]
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
            Debug.Log("HUD fix capture armed.");
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
                var go = new GameObject("HudFixCaptureHost");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<HudFixCaptureHost>();
            }
            else if (change == PlayModeStateChange.ExitingPlayMode)
            {
                SessionState.SetBool(ArmKey, false);
            }
        }
    }

    public sealed class HudFixCaptureHost : MonoBehaviour
    {
        private IEnumerator Start()
        {
            float t0 = Time.realtimeSinceStartup;
            ParadoxChromeHud hud = null;
            MinistryGameRunner runner = null;
            while (Time.realtimeSinceStartup - t0 < 25f)
            {
                runner = Object.FindFirstObjectByType<MinistryGameRunner>();
                hud = Object.FindFirstObjectByType<ParadoxChromeHud>();
                if (runner != null && runner.Session != null && hud != null
                    && FindNamed(hud.transform, "OrdersToggle") != null)
                    break;
                yield return null;
            }

            if (hud == null)
            {
                Debug.LogError("HUD_FIX_CAPTURE_FAIL");
                EditorApplication.isPlaying = false;
                yield break;
            }

            Directory.CreateDirectory(Path.GetFullPath("Artifacts/hud-reshell"));
            hud.ForceOpenMenu(ParadoxChromeHud.MenuId.None);
            hud.ForceOpenChart(ParadoxChromeHud.MenuId.None);

            // Chart drawer — expect horizontally centered.
            hud.ForceOpenChart(ParadoxChromeHud.MenuId.Budget);
            yield return null;
            yield return new WaitForSecondsRealtime(0.2f);
            yield return new WaitForEndOfFrame();
            var chartRt = FindNamed(hud.transform, "ChartDrawer").GetComponent<RectTransform>();
            float mid = (chartRt.anchorMin.x + chartRt.anchorMax.x) * 0.5f;
            Debug.Log("chartMidX=" + mid.ToString("0.000")
                      + " min=" + chartRt.anchorMin.ToString("F3")
                      + " max=" + chartRt.anchorMax.ToString("F3"));
            Write("Artifacts/hud-reshell/hud-chart-drawer-mid.png");

            hud.ForceOpenChart(ParadoxChromeHud.MenuId.None);

            // 24h drawer — expect right-anchored next to DuckBtn.
            var duckBtn = FindNamed(hud.transform, "DuckBtn");
            duckBtn.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            yield return new WaitForSecondsRealtime(0.2f);
            yield return new WaitForEndOfFrame();
            var duckRt = FindNamed(hud.transform, "DuckDrawer").GetComponent<RectTransform>();
            var btnRt = duckBtn.GetComponent<RectTransform>();
            Debug.Log("duckDrawer " + duckRt.anchorMin.ToString("F3") + "-" + duckRt.anchorMax.ToString("F3")
                      + " btnX0=" + btnRt.anchorMin.x.ToString("0.000"));
            Write("Artifacts/hud-reshell/hud-24h-drawer-right.png");

            Debug.Log(Mathf.Abs(mid - 0.5f) < 0.03f && duckRt.anchorMax.x > 0.55f
                ? "SHOT_DRAWER_LAYOUT_OK"
                : "SHOT_DRAWER_LAYOUT_CHECK");
            EditorApplication.isPlaying = false;
        }

        private static GameObject FindNamed(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
            return null;
        }

        private static void Write(string rel)
        {
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.GetFullPath(rel), tex.EncodeToPNG());
            Object.Destroy(tex);
            Debug.Log("Wrote " + Path.GetFullPath(rel));
        }
    }
}
