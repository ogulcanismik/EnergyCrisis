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
    /// <summary>One-shot play capture for right-edge Orders + compact verb panel.</summary>
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

            var ort = FindNamed(hud.transform, "OrdersToggle").GetComponent<RectTransform>();
            Debug.Log("ordersMinX=" + ort.anchorMin.x.ToString("0.000")
                      + " maxX=" + ort.anchorMax.x.ToString("0.000"));

            hud.ForceOpenMenu(ParadoxChromeHud.MenuId.None);
            hud.ForceOpenChart(ParadoxChromeHud.MenuId.None);
            Directory.CreateDirectory(Path.GetFullPath("Artifacts/hud-reshell"));

            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            Write("Artifacts/hud-reshell/hud-layout-v2-orders-edge.png");

            hud.ForceOpenMenu(ParadoxChromeHud.MenuId.Construction);
            yield return null;
            yield return new WaitForSecondsRealtime(0.2f);
            yield return new WaitForEndOfFrame();
            Write("Artifacts/hud-reshell/hud-layout-v2-verb-panel.png");

            var srt = FindNamed(hud.transform, "SidePanel").GetComponent<RectTransform>();
            Debug.Log("side " + srt.anchorMin.ToString("F3") + "-" + srt.anchorMax.ToString("F3"));
            Debug.Log("SHOT_FIX_OK");
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
