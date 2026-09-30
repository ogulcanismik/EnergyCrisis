using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using MinistryOfPower.Runtime;
using MinistryOfPower.Simulation;
using MinistryOfPower.UI;
using MinistryOfPower.UI.Charts;

namespace MinistryOfPower.EditorTools
{
    /// <summary>
    /// Play-mode check: compact time cluster, exclusive left menus, live mesh chart.
    /// </summary>
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
            Finish(BuildReport(hud, runner.Session));
        }

        private static string BuildReport(ParadoxChromeHud hud, GameSession session)
        {
            var sb = new StringBuilder(1600);
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
            var timeBar = FindRt(chrome, "TimeBar");
            var side = FindGo(chrome, "SidePanel");
            var filterToggle = FindGo(chrome, "FilterToggle");
            var dealBrief = FindGo(chrome, "DealBriefToggle");
            var escBtn = FindGo(chrome, "EscBtn");
            var ordersPanel = FindGo(chrome, "OrdersPanel");
            var chartMix = FindGo(chrome, "Chart_Mix");
            var chartBudget = FindGo(chrome, "Chart_Budget");
            var constructionRail = FindGo(chrome, "M_Construction");
            var dealsRail = FindGo(chrome, "M_Deals");
            var cabinetRail = FindGo(chrome, "M_Cabinet");
            var subsidiesRail = FindGo(chrome, "M_Subsidies");
            var legacyReports = FindGo(chrome, "M_Reports");
            var legacyFlyout = FindGo(chrome, "ReportsFlyout");
            var legacyLobby = FindGo(chrome, "LobbyInfo");
            var legacyRightCab = FindGo(chrome, "RightDrawer");
            var fatEmergency = FindGo(chrome, "EmergLabel");
            UguiMeshChart dispatchChart = null;
            if (chrome != null)
            {
                foreach (var c in chrome.GetComponentsInChildren<UguiMeshChart>(true))
                {
                    if (c != null && c.gameObject.name == "DispatchChart")
                    {
                        dispatchChart = c;
                        break;
                    }
                }
            }

            sb.Append("hud=").Append(true).AppendLine();
            sb.Append("date=").Append(date != null).Append(" tick=").Append(tick != null).AppendLine();
            sb.Append("escBtn=").Append(escBtn != null).Append(" ordersPanel=").Append(ordersPanel != null).AppendLine();
            sb.Append("chartMix=").Append(chartMix != null).Append(" chartBudget=").Append(chartBudget != null).AppendLine();
            sb.Append("leftTabs=")
                .Append(constructionRail != null).Append('/')
                .Append(dealsRail != null).Append('/')
                .Append(cabinetRail != null).Append('/')
                .Append(subsidiesRail != null).AppendLine();
            sb.Append("filterToggle=").Append(filterToggle != null)
                .Append(" dealBrief=").Append(dealBrief != null).AppendLine();
            sb.Append("legacyReports=").Append(legacyReports != null)
                .Append(" legacyFlyout=").Append(legacyFlyout != null)
                .Append(" legacyLobby=").Append(legacyLobby != null)
                .Append(" legacyRightCab=").Append(legacyRightCab != null)
                .Append(" fatEmergency=").Append(fatEmergency != null).AppendLine();
            sb.Append("meshChart=").Append(dispatchChart != null)
                .Append(" dispatchActive=").Append(dispatchChart != null && dispatchChart.isActiveAndEnabled)
                .AppendLine();
            sb.Append("secondsPerDay1x=").Append(GameClock.SecondsPerDay1x.ToString("0")).AppendLine();

            bool stackOk = false;
            bool narrowOk = false;
            bool dateVisible = false;
            if (date != null && pause != null)
            {
                float dateY = date.rectTransform.anchorMin.y;
                float btnY = pause.anchorMax.y;
                stackOk = dateY >= btnY - 0.001f;
                float dateW = date.rectTransform.anchorMax.x - date.rectTransform.anchorMin.x;
                string dateSample = (date.text ?? "").Replace('\n', ' ').Trim();
                dateVisible = date.gameObject.activeInHierarchy
                              && date.enabled
                              && date.color.a >= 0.9f
                              && dateW >= 0.15f
                              && dateSample.Length >= 8;
                sb.Append("dateAnchorMinY=").Append(dateY.ToString("0.000"))
                    .Append(" pauseAnchorMaxY=").Append(btnY.ToString("0.000"))
                    .Append(" dateAboveSpeeds=").Append(stackOk).AppendLine();
                sb.Append("dateWidth=").Append(dateW.ToString("0.000"))
                    .Append(" dateVisible=").Append(dateVisible).AppendLine();
                sb.Append("dateSample=").Append(dateSample).AppendLine();
                sb.Append("tickSample=").Append(tick != null ? tick.text : "").AppendLine();
            }

            if (timeBar != null)
            {
                float span = timeBar.anchorMax.x - timeBar.anchorMin.x;
                narrowOk = timeBar.anchorMin.x >= 0.65f && span <= 0.35f;
                sb.Append("timeBarMinX=").Append(timeBar.anchorMin.x.ToString("0.000"))
                    .Append(" timeBarSpan=").Append(span.ToString("0.000"))
                    .Append(" timeClusterNarrow=").Append(narrowOk).AppendLine();
            }

            if (pause != null && s1 != null)
            {
                float btnW = pause.anchorMax.x - pause.anchorMin.x;
                sb.Append("pauseBtnWidth=").Append(btnW.ToString("0.000"))
                    .Append(" pauseNarrow=").Append(btnW <= 0.08f).AppendLine();
                narrowOk = narrowOk && btnW <= 0.08f;
            }

            // Exclusive menus: Construction, then Budget chart drawer, then Construction again.
            hud.ForceOpenMenu(ParadoxChromeHud.MenuId.Construction);
            bool constructionOpen = side != null && side.activeInHierarchy
                                    && hud.DebugOpenMenu == ParadoxChromeHud.MenuId.Construction;
            hud.ForceOpenMenu(ParadoxChromeHud.MenuId.Budget);
            bool budgetPanel = side != null && side.activeInHierarchy
                               && hud.DebugOpenMenu == ParadoxChromeHud.MenuId.Budget;
            hud.ForceOpenMenu(ParadoxChromeHud.MenuId.Cabinet);
            bool cabinetPanel = side != null && side.activeInHierarchy
                                && hud.DebugOpenMenu == ParadoxChromeHud.MenuId.Cabinet;
            hud.ForceOpenMenu(ParadoxChromeHud.MenuId.Construction);
            bool constructionAgain = side != null && side.activeInHierarchy
                                     && hud.DebugOpenMenu == ParadoxChromeHud.MenuId.Construction;
            sb.Append("constructionOpen=").Append(constructionOpen)
                .Append(" budgetPanel=").Append(budgetPanel)
                .Append(" cabinetPanel=").Append(cabinetPanel)
                .Append(" constructionAgain=").Append(constructionAgain).AppendLine();

            bool exclusiveOk = constructionOpen && budgetPanel && cabinetPanel && constructionAgain;

            bool chartLive = false;
            int curveLen = session?.DayDemandCurve != null ? session.DayDemandCurve.Length : 0;
            sb.Append("dayCurveLen=").Append(curveLen).AppendLine();
            if (dispatchChart != null && session != null)
            {
                var strip = chrome.GetComponentInChildren<SupplyDemandStrip>(true);
                strip?.Render(session);
                chartLive = dispatchChart.isActiveAndEnabled && curveLen >= 24;
            }

            sb.Append("chartLive=").Append(chartLive).AppendLine();

            hud.ForceOpenMenu(ParadoxChromeHud.MenuId.None);

            bool ok = date != null && tick != null && pause != null && s1 != null
                      && escBtn != null && ordersPanel != null
                      && chartMix != null && chartBudget != null
                      && constructionRail != null && dealsRail != null
                      && cabinetRail != null && subsidiesRail != null
                      && filterToggle != null && dealBrief != null
                      && stackOk && narrowOk && exclusiveOk && chartLive && dateVisible
                      && legacyReports == null && legacyFlyout == null
                      && legacyLobby == null && legacyRightCab == null
                      && fatEmergency == null
                      && Mathf.Approximately(GameClock.SecondsPerDay1x, 168f);
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
