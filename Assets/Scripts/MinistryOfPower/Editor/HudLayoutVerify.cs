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
    /// <summary>Play-mode check: date/hour stacked above speeds; Reports submenu present.</summary>
    public static class HudLayoutVerify
    {
        private const string ArmKey = "MoP.HudLayoutArmed";
        private const string OutName = "hud_layout_verify.txt";
        private static double _enteredAt;

        [MenuItem("Ministry of Power/Verify HUD Layout")]
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
            Debug.Log("HUD layout verify armed — entering Play Mode.");
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
            var hud = Object.FindFirstObjectByType<ParadoxChromeHud>();
            bool ready = runner != null && runner.Session != null && hud != null;
            if (!ready)
            {
                if (EditorApplication.timeSinceStartup - _enteredAt > 20)
                {
                    Finish("=== HUD LAYOUT VERIFY ===\nFAIL session/hud not ready\nHUD_LAYOUT_OK=False\n");
                }

                return;
            }

            if (EditorApplication.timeSinceStartup - _enteredAt < 1.0) return;
            Finish(BuildReport(hud));
        }

        private static string BuildReport(ParadoxChromeHud hud)
        {
            var sb = new StringBuilder(1024);
            sb.AppendLine("=== HUD LAYOUT VERIFY ===");
            Transform chrome = null;
            foreach (Transform t in hud.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "ParadoxChrome")
                {
                    chrome = t;
                    break;
                }
            }

            Text date = FindText(chrome, "DateTime");
            Text tick = FindText(chrome, "Tick");
            var pause = FindRt(chrome, "P");
            var s1 = FindRt(chrome, "S1");
            var reports = FindRt(chrome, "M_Reports");
            var flyout = FindGo(chrome, "ReportsFlyout");
            var lobbyInfo = FindGo(chrome, "LobbyInfo");
            var filterToggle = FindGo(chrome, "FilterToggle");
            var dealBrief = FindGo(chrome, "DealBriefToggle");
            var mandateRail = FindGo(chrome, "M_Mandate");
            var budgetRail = FindGo(chrome, "M_Budget");

            sb.Append("hud=").Append(true).AppendLine();
            sb.Append("date=").Append(date != null).Append(" tick=").Append(tick != null).AppendLine();
            sb.Append("reportsBtn=").Append(reports != null).Append(" flyout=").Append(flyout != null).AppendLine();
            sb.Append("lobbyInfo=").Append(lobbyInfo != null)
                .Append(" filterToggle=").Append(filterToggle != null)
                .Append(" dealBrief=").Append(dealBrief != null).AppendLine();
            sb.Append("legacyMandateRail=").Append(mandateRail != null)
                .Append(" legacyBudgetRail=").Append(budgetRail != null).AppendLine();

            bool stackOk = false;
            if (date != null && pause != null)
            {
                float dateY = date.rectTransform.anchorMin.y;
                float btnY = pause.anchorMax.y;
                stackOk = dateY >= btnY - 0.001f;
                sb.Append("dateAnchorMinY=").Append(dateY.ToString("0.000"))
                    .Append(" pauseAnchorMaxY=").Append(btnY.ToString("0.000"))
                    .Append(" dateAboveSpeeds=").Append(stackOk).AppendLine();
                sb.Append("dateSample=").Append((date.text ?? "").Replace('\n', ' ')).AppendLine();
                sb.Append("tickSample=").Append(tick != null ? tick.text : "").AppendLine();
            }

            hud.ForceOpenMenu(ParadoxChromeHud.MenuId.Budget);
            bool flyActive = flyout != null && flyout.activeInHierarchy;
            sb.Append("reportsOpenOnBudget=").Append(flyActive).AppendLine();
            hud.ForceOpenMenu(ParadoxChromeHud.MenuId.None);

            bool ok = date != null && tick != null && pause != null && s1 != null
                      && reports != null && flyout != null && lobbyInfo != null
                      && filterToggle != null && dealBrief != null
                      && stackOk && mandateRail == null && budgetRail == null
                      && flyActive;
            sb.AppendLine(ok ? "HUD_LAYOUT_OK=True" : "HUD_LAYOUT_OK=False");
            return sb.ToString();
        }

        private static void Finish(string body)
        {
            EditorApplication.update -= OnTick;
            SessionState.SetBool(ArmKey, false);
            string path = Path.Combine(Application.persistentDataPath, OutName);
            File.WriteAllText(path, body, Encoding.UTF8);
            Debug.Log(body);
            Debug.Log("HUD layout verify written: " + path);
            EditorApplication.isPlaying = false;
        }

        private static Text FindText(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<Text>(true))
                if (t.gameObject.name == name) return t;
            return null;
        }

        private static RectTransform FindRt(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t as RectTransform ?? t.GetComponent<RectTransform>();
            return null;
        }

        private static GameObject FindGo(Transform root, string name)
        {
            var rt = FindRt(root, name);
            return rt != null ? rt.gameObject : null;
        }
    }
}
