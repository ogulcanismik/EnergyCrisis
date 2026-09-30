using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using MinistryOfPower.Runtime;
using MinistryOfPower.Simulation;
using MinistryOfPower.UI;

namespace MinistryOfPower.EditorTools
{
    /// <summary>
    /// Play-mode check: ESC pause overlay, slot modals, no left-rail Pause save UI.
    /// </summary>
    public static class PauseMenuVerify
    {
        private const string ArmKey = "MoP.PauseMenuArmed";
        private const string OutName = "pause_menu_verify.txt";
        private static double _enteredAt;

        [MenuItem("Ministry of Power/Verify Pause Menu")]
        public static void VerifyMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += VerifyMenu;
                return;
            }

            GameSettings.HelpSeen = true;
            RunSetup.Ensure().ConfigureNewGame("usa_like", DifficultyId.Normal);
            EditorSceneManager.OpenScene("Assets/Scenes/MinistryDesk.unity");
            SessionState.SetBool(ArmKey, true);
            EditorApplication.isPlaying = true;
            Debug.Log("Pause menu verify armed — entering Play Mode.");
        }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlayState;
            EditorApplication.playModeStateChanged += OnPlayState;
            if (SessionState.GetBool(ArmKey, false) && EditorApplication.isPlaying)
            {
                _enteredAt = EditorApplication.timeSinceStartup;
                EditorApplication.update -= OnTick;
                EditorApplication.update += OnTick;
            }
        }

        private static void OnPlayState(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(ArmKey, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                _enteredAt = EditorApplication.timeSinceStartup;
                EditorApplication.update -= OnTick;
                EditorApplication.update += OnTick;
            }
            else if (change == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= OnTick;
            }
        }

        private static void OnTick()
        {
            if (!SessionState.GetBool(ArmKey, false) || !EditorApplication.isPlaying) return;

            var runner = Object.FindFirstObjectByType<MinistryGameRunner>();
            var pause = Object.FindFirstObjectByType<PauseMenuOverlay>();
            bool ready = runner != null && runner.Session?.Clock != null && pause != null;
            if (!ready)
            {
                if (EditorApplication.timeSinceStartup - _enteredAt > 20)
                    Finish("=== PAUSE MENU VERIFY ===\nFAIL runner/pause not ready\nPAUSE_MENU_OK=False\n");
                return;
            }

            if (EditorApplication.timeSinceStartup - _enteredAt < 1.0) return;
            Finish(BuildReport(runner, pause));
        }

        private static string BuildReport(MinistryGameRunner runner, PauseMenuOverlay pause)
        {
            var sb = new StringBuilder(1200);
            sb.AppendLine("=== PAUSE MENU VERIFY ===");

            if (pause.IsOpen) pause.Close(true);
            runner.Session.SetSpeed(GameSpeed.Normal);
            GameSpeed before = runner.Session.Clock.Speed;

            pause.Open();
            bool openOk = pause.IsOpen && runner.Session.Clock.IsPaused;
            sb.Append("open=").Append(pause.IsOpen)
                .Append(" clockPaused=").Append(runner.Session.Clock.IsPaused)
                .Append(" speedBefore=").Append(before)
                .AppendLine();

            var canvas = GameObject.Find("PauseMenuCanvas");
            sb.Append("pauseCanvas=").Append(canvas != null).AppendLine();

            bool saveInvoked = InvokeNamedButton(canvas, "Save");
            var slot = Object.FindFirstObjectByType<SaveSlotModal>();
            bool saveModal = slot != null && slot.IsOpen;
            sb.Append("saveInvoked=").Append(saveInvoked)
                .Append(" saveModalOpen=").Append(saveModal).AppendLine();
            if (saveModal) slot.Close();

            bool loadInvoked = InvokeNamedButton(canvas, "Load");
            slot = Object.FindFirstObjectByType<SaveSlotModal>();
            bool loadModal = slot != null && slot.IsOpen;
            sb.Append("loadInvoked=").Append(loadInvoked)
                .Append(" loadModalOpen=").Append(loadModal).AppendLine();
            if (loadModal) slot.Close();

            // Persist a slot via pause save path, then confirm label formatting.
            int probeSlot = 2;
            bool saved = SaveGameSystem.TrySave(
                probeSlot,
                GameSessionSaveMapper.Capture(runner.Session, probeSlot),
                out string saveMsg);
            sb.Append("probeSave=").Append(saved).Append(" msg=").Append(saveMsg).AppendLine();
            string label = SaveSlotModal.FormatSlotLabel(SaveGameSystem.PeekSlot(probeSlot));
            sb.Append("slotLabelSample=").Append(label.Replace("\n", " | ")).AppendLine();

            pause.Close(true);
            bool resumeOk = !pause.IsOpen && !runner.Session.Clock.IsPaused;
            sb.Append("resumeClosed=").Append(!pause.IsOpen)
                .Append(" clockRunning=").Append(!runner.Session.Clock.IsPaused)
                .Append(" speedAfter=").Append(runner.Session.Clock.Speed)
                .AppendLine();

            var leftPause = GameObject.Find("M_Pause");
            var pauseActions = GameObject.Find("PauseActions");
            sb.Append("leftRailPauseBtn=").Append(leftPause != null)
                .Append(" pauseActionsHost=").Append(pauseActions != null)
                .Append(" (expect both false)").AppendLine();

            bool ok = openOk && canvas != null && saveInvoked && saveModal
                      && loadInvoked && loadModal && resumeOk
                      && leftPause == null && pauseActions == null && saved;
            sb.Append("PAUSE_MENU_OK=").Append(ok).AppendLine();
            return sb.ToString();
        }

        private static bool InvokeNamedButton(GameObject root, string name)
        {
            if (root == null) return false;
            var buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].gameObject.name == name)
                {
                    buttons[i].onClick.Invoke();
                    return true;
                }
            }

            return false;
        }

        private static void Finish(string report)
        {
            SessionState.SetBool(ArmKey, false);
            EditorApplication.update -= OnTick;
            string path = Path.Combine(Application.dataPath, "..", OutName);
            File.WriteAllText(path, report);
            Debug.Log(report);
            EditorApplication.isPlaying = false;
        }
    }
}
