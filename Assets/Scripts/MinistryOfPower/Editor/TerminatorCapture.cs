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
    /// <summary>Play-mode captures of the map day/night terminator at dawn / noon / dusk / night.</summary>
    public static class TerminatorCapture
    {
        private const string ArmKey = "MoP.TerminatorCaptureArmed";
        public const string OutDir = "Artifacts/map-terminator";

        [MenuItem("Ministry of Power/Capture Map Terminator Shots")]
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
            Debug.Log("Terminator capture armed.");
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
                var go = new GameObject("TerminatorCaptureHost");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<TerminatorCaptureHost>();
            }
            else if (change == PlayModeStateChange.ExitingPlayMode)
            {
                SessionState.SetBool(ArmKey, false);
            }
        }
    }

    public sealed class TerminatorCaptureHost : MonoBehaviour
    {
        private IEnumerator Start()
        {
            float t0 = Time.realtimeSinceStartup;
            MinistryGameRunner runner = null;
            while (Time.realtimeSinceStartup - t0 < 25f)
            {
                runner = Object.FindFirstObjectByType<MinistryGameRunner>();
                if (runner != null && runner.Session?.Clock != null
                    && Object.FindFirstObjectByType<MapDayTerminator>() != null)
                    break;
                yield return null;
            }

            if (runner?.Session?.Clock == null)
            {
                Debug.LogError("TERMINATOR_CAPTURE_FAIL: no session/clock");
                EditorApplication.isPlaying = false;
                yield break;
            }

            Directory.CreateDirectory(TerminatorCapture.OutDir);
            runner.Session.Clock.SetSpeed(GameSpeed.Paused);

            yield return CaptureAt(runner, 0.25f, "terminator-dawn.png");
            yield return CaptureAt(runner, 0.50f, "terminator-midday.png");
            yield return CaptureAt(runner, 0.78f, "terminator-dusk.png");
            yield return CaptureAt(runner, 0.05f, "terminator-night.png");

            Debug.Log("TERMINATOR_CAPTURE_OK → " + Path.GetFullPath(TerminatorCapture.OutDir));
            EditorApplication.isPlaying = false;
        }

        private static IEnumerator CaptureAt(MinistryGameRunner runner, float dayFraction, string fileName)
        {
            GameClock c = runner.Session.Clock;
            c.Restore(c.Year, c.AbsoluteDay, c.DayIndex, c.QuarterIndex, dayFraction, GameSpeed.Paused);

            var terminator = Object.FindFirstObjectByType<MapDayTerminator>();
            terminator?.EnsureOverlay();
            terminator?.ApplyClock(c);

            var hud = Object.FindFirstObjectByType<ParadoxChromeHud>();
            hud?.RefreshTimeChrome();

            // Settle LateUpdate + render.
            yield return null;
            yield return new WaitForEndOfFrame();

            string path = Path.Combine(TerminatorCapture.OutDir, fileName);
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

            Debug.Log("Captured " + path + " @ DayFraction=" + dayFraction.ToString("0.00")
                      + " (" + c.FormatTimeOfDay() + ")");
        }
    }
}
