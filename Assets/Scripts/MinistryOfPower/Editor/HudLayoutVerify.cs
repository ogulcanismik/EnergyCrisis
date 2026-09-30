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
            var spdMinus = FindRt(chrome, "SpdMinus");
            var spdPlus = FindRt(chrome, "SpdPlus");
            var spdLabel = FindText(chrome, "Spd");
            var timeBar = FindRt(chrome, "TimeBar");
            var topBar = FindRt(chrome, "TopBar");
            var side = FindGo(chrome, "SidePanel");
            var filterToggle = FindGo(chrome, "FilterToggle");
            var dealBrief = FindGo(chrome, "DealBriefToggle");
            var escBtn = FindGo(chrome, "EscBtn");
            var ordersPanel = FindGo(chrome, "OrdersPanel");
            var ordersToggle = FindGo(chrome, "OrdersToggle");
            var chartMix = FindGo(chrome, "Chart_Mix");
            var chartBudget = FindGo(chrome, "Chart_Budget");
            var chartDrawer = FindGo(chrome, "ChartDrawer");
            var duckBtn = FindGo(chrome, "DuckBtn");
            var duckDrawer = FindGo(chrome, "DuckDrawer");
            var tabRow = FindGo(chrome, "TabRow");
            var constructionRail = FindGo(chrome, "M_Construction");
            var dealsRail = FindGo(chrome, "M_Deals");
            var cabinetRail = FindGo(chrome, "M_Cabinet");
            var subsidiesRail = FindGo(chrome, "M_Subsidies");
            var legacyReports = FindGo(chrome, "M_Reports");
            var legacyFlyout = FindGo(chrome, "ReportsFlyout");
            var legacyLobby = FindGo(chrome, "LobbyInfo");
            var legacyRightCab = FindGo(chrome, "RightDrawer");
            var fatEmergency = FindGo(chrome, "EmergLabel");
            var legacyLeftRail = FindGo(chrome, "LeftRail");
            var deskLog = FindGo(chrome, "LogPanel");
            var deskLogLabel = FindGo(chrome, "Log");
            var legacyBrand = FindGo(chrome, "Brand");
            var legacyPause = FindGo(chrome, "P");
            var legacySkip = FindGo(chrome, "D1");
            var legacyS1 = FindGo(chrome, "S1");
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
            sb.Append("date=").Append(date != null)
                .Append(" tickGone=").Append(tick == null || !tick.gameObject.activeInHierarchy).AppendLine();
            sb.Append("escBtn=").Append(escBtn != null).Append(" ordersPanel=").Append(ordersPanel != null).AppendLine();
            sb.Append("chartMix=").Append(chartMix != null).Append(" chartBudget=").Append(chartBudget != null).AppendLine();
            sb.Append("duckBtn=").Append(duckBtn != null).Append(" duckDrawer=").Append(duckDrawer != null).AppendLine();
            bool chartSquare = false;
            if (chartMix != null)
            {
                var crt = chartMix.GetComponent<RectTransform>();
                if (crt != null)
                {
                    float w = crt.anchorMax.x - crt.anchorMin.x;
                    float h = crt.anchorMax.y - crt.anchorMin.y;
                    chartSquare = w <= 0.05f && h <= 0.05f && Mathf.Abs(w - h) < 0.02f;
                    sb.Append("chartMixW=").Append(w.ToString("0.000"))
                        .Append(" chartMixH=").Append(h.ToString("0.000"))
                        .Append(" chartSquare=").Append(chartSquare).AppendLine();
                }
            }

            sb.Append("tabRow=").Append(tabRow != null).Append(" leftTabs=")
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
                .Append(" fatEmergency=").Append(fatEmergency != null)
                .Append(" legacyLeftRail=").Append(legacyLeftRail != null)
                .Append(" deskLog=").Append(deskLog != null || deskLogLabel != null).AppendLine();
            bool tabsHorizontal = false;
            if (constructionRail != null && subsidiesRail != null)
            {
                var a = constructionRail.GetComponent<RectTransform>();
                var b = subsidiesRail.GetComponent<RectTransform>();
                if (a != null && b != null)
                {
                    float midAy = (a.anchorMin.y + a.anchorMax.y) * 0.5f;
                    float midBy = (b.anchorMin.y + b.anchorMax.y) * 0.5f;
                    tabsHorizontal = Mathf.Abs(midAy - midBy) < 0.02f && a.anchorMin.x < b.anchorMin.x;
                }
            }

            sb.Append("tabsHorizontal=").Append(tabsHorizontal).AppendLine();
            sb.Append("meshChart=").Append(dispatchChart != null)
                .Append(" dispatchActive=").Append(dispatchChart != null && dispatchChart.isActiveAndEnabled)
                .AppendLine();
            sb.Append("secondsPerDay1x=").Append(GameClock.SecondsPerDay1x.ToString("0")).AppendLine();

            bool stackOk = false;
            bool narrowOk = false;
            bool dateVisible = false;
            bool compactTop = false;
            bool hudDateFmt = false;
            sb.Append("spdMinus=").Append(spdMinus != null)
                .Append(" spdPlus=").Append(spdPlus != null)
                .Append(" spdLabel=").Append(spdLabel != null).AppendLine();
            sb.Append("legacyBrand=").Append(legacyBrand != null)
                .Append(" legacyPause=").Append(legacyPause != null)
                .Append(" legacySkip=").Append(legacySkip != null)
                .Append(" legacyS1=").Append(legacyS1 != null).AppendLine();
            if (date != null && spdMinus != null)
            {
                // Single-row top: date and −/+ share the same thin band.
                float dateMidY = (date.rectTransform.anchorMin.y + date.rectTransform.anchorMax.y) * 0.5f;
                float btnMidY = (spdMinus.anchorMin.y + spdMinus.anchorMax.y) * 0.5f;
                stackOk = Mathf.Abs(dateMidY - btnMidY) < 0.03f;
                float dateW = date.rectTransform.anchorMax.x - date.rectTransform.anchorMin.x;
                string dateSample = (date.text ?? "").Replace('\n', ' ').Trim();
                dateVisible = date.gameObject.activeInHierarchy
                              && date.enabled
                              && date.color.a >= 0.9f
                              && dateW >= 0.10f
                              && dateSample.Length >= 8;
                hudDateFmt = System.Text.RegularExpressions.Regex.IsMatch(dateSample, @"^\d{1,2}/[A-Za-z]{3}/\d{4}");
                sb.Append("dateMidY=").Append(dateMidY.ToString("0.000"))
                    .Append(" minusMidY=").Append(btnMidY.ToString("0.000"))
                    .Append(" sameRow=").Append(stackOk).AppendLine();
                sb.Append("dateWidth=").Append(dateW.ToString("0.000"))
                    .Append(" dateVisible=").Append(dateVisible)
                    .Append(" hudDateFmt=").Append(hudDateFmt).AppendLine();
                sb.Append("dateSample=").Append(dateSample).AppendLine();
                sb.Append("spdSample=").Append(spdLabel != null ? spdLabel.text : "").AppendLine();
            }

            if (topBar != null)
            {
                float topH = topBar.anchorMax.y - topBar.anchorMin.y;
                compactTop = topH <= 0.055f && topBar.anchorMin.y >= 0.94f;
                sb.Append("topBarH=").Append(topH.ToString("0.000"))
                    .Append(" compactTop=").Append(compactTop).AppendLine();
            }

            if (timeBar != null)
            {
                float span = timeBar.anchorMax.x - timeBar.anchorMin.x;
                narrowOk = timeBar.anchorMin.x >= 0.65f && span <= 0.35f;
                sb.Append("timeBarMinX=").Append(timeBar.anchorMin.x.ToString("0.000"))
                    .Append(" timeBarSpan=").Append(span.ToString("0.000"))
                    .Append(" timeClusterNarrow=").Append(narrowOk).AppendLine();
            }

            if (spdMinus != null)
            {
                float btnW = spdMinus.anchorMax.x - spdMinus.anchorMin.x;
                float btnH = spdMinus.anchorMax.y - spdMinus.anchorMin.y;
                sb.Append("minusBtnWidth=").Append(btnW.ToString("0.000"))
                    .Append(" minusBtnH=").Append(btnH.ToString("0.000"))
                    .Append(" minusCompact=").Append(btnW <= 0.035f && btnH <= 0.04f).AppendLine();
                narrowOk = narrowOk && btnW <= 0.035f;
            }

            // Verbs → left panel; charts → ChartDrawer (not left rail).
            hud.ForceOpenMenu(ParadoxChromeHud.MenuId.Construction);
            bool constructionOpen = side != null && side.activeInHierarchy
                                    && hud.DebugOpenMenu == ParadoxChromeHud.MenuId.Construction;
            hud.ForceOpenChart(ParadoxChromeHud.MenuId.Budget);
            bool budgetChartDrawer = chartDrawer != null && chartDrawer.activeInHierarchy
                                     && hud.DebugOpenChart == ParadoxChromeHud.MenuId.Budget
                                     && (side == null || !side.activeInHierarchy
                                         || hud.DebugOpenMenu != ParadoxChromeHud.MenuId.Budget);
            hud.ForceOpenMenu(ParadoxChromeHud.MenuId.Cabinet);
            bool cabinetPanel = side != null && side.activeInHierarchy
                                && hud.DebugOpenMenu == ParadoxChromeHud.MenuId.Cabinet;
            hud.ForceOpenMenu(ParadoxChromeHud.MenuId.Construction);
            bool constructionAgain = side != null && side.activeInHierarchy
                                     && hud.DebugOpenMenu == ParadoxChromeHud.MenuId.Construction;
            // Orders thin tab on RIGHT edge (not left).
            bool ordersEdgeTab = false;
            if (ordersToggle != null)
            {
                var ort = ordersToggle.GetComponent<RectTransform>();
                if (ort != null)
                {
                    float w = ort.anchorMax.x - ort.anchorMin.x;
                    float h = ort.anchorMax.y - ort.anchorMin.y;
                    ordersEdgeTab = ort.anchorMax.x >= 0.995f && ort.anchorMin.x >= 0.95f
                                    && w <= 0.04f && h >= 0.20f;
                }
            }

            // Verb trigger chips stay readable; opened SidePanel is the compact footprint.
            bool verbButtonsReadable = false;
            if (constructionRail != null)
            {
                var crt = constructionRail.GetComponent<RectTransform>();
                if (crt != null)
                {
                    float w = crt.anchorMax.x - crt.anchorMin.x;
                    float h = crt.anchorMax.y - crt.anchorMin.y;
                    verbButtonsReadable = w >= 0.07f && h >= 0.028f;
                }
            }

            bool verbPanelCompact = false;
            if (side != null)
            {
                var srt = side.GetComponent<RectTransform>();
                if (srt != null)
                {
                    float w = srt.anchorMax.x - srt.anchorMin.x;
                    float h = srt.anchorMax.y - srt.anchorMin.y;
                    verbPanelCompact = w <= 0.34f && h <= 0.70f && srt.anchorMin.y >= 0.20f;
                }
            }

            sb.Append("constructionOpen=").Append(constructionOpen)
                .Append(" budgetChartDrawer=").Append(budgetChartDrawer)
                .Append(" cabinetPanel=").Append(cabinetPanel)
                .Append(" constructionAgain=").Append(constructionAgain).AppendLine();
            sb.Append("ordersEdgeTab=").Append(ordersEdgeTab)
                .Append(" verbButtonsReadable=").Append(verbButtonsReadable)
                .Append(" verbPanelCompact=").Append(verbPanelCompact)
                .Append(" chartDrawer=").Append(chartDrawer != null).AppendLine();

            bool exclusiveOk = constructionOpen && budgetChartDrawer && cabinetPanel && constructionAgain
                               && ordersEdgeTab && verbButtonsReadable && verbPanelCompact;

            bool chartLive = false;
            int curveLen = session?.DayDemandCurve != null ? session.DayDemandCurve.Length : 0;
            sb.Append("dayCurveLen=").Append(curveLen).AppendLine();
            // 24h strip lives in DuckDrawer — open it for the live check.
            if (duckDrawer != null) duckDrawer.SetActive(true);
            if (dispatchChart != null && session != null)
            {
                var strip = chrome.GetComponentInChildren<SupplyDemandStrip>(true);
                strip?.Render(session);
                chartLive = dispatchChart.gameObject.activeInHierarchy && curveLen >= 24;
            }

            sb.Append("chartLive=").Append(chartLive).AppendLine();
            if (duckDrawer != null) duckDrawer.SetActive(false);

            bool stripNotPermanentBottom = true;
            var bottom = FindGo(chrome, "Bottom");
            if (bottom != null)
            {
                foreach (var t in bottom.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "DayStrip")
                    {
                        stripNotPermanentBottom = false;
                        break;
                    }
                }
            }

            sb.Append("stripNotPermanentBottom=").Append(stripNotPermanentBottom).AppendLine();

            hud.ForceOpenMenu(ParadoxChromeHud.MenuId.None);

            bool tickGone = tick == null || !tick.gameObject.activeInHierarchy;
            bool ok = date != null && tickGone && spdMinus != null && spdPlus != null && spdLabel != null
                      && escBtn != null && ordersPanel != null && ordersToggle != null
                      && chartMix != null && chartBudget != null && chartSquare && chartDrawer != null
                      && duckBtn != null && duckDrawer != null && stripNotPermanentBottom
                      && tabRow != null
                      && constructionRail != null && dealsRail != null
                      && cabinetRail != null && subsidiesRail != null
                      && tabsHorizontal
                      && filterToggle != null && dealBrief != null
                      && stackOk && narrowOk && exclusiveOk && chartLive && dateVisible
                      && compactTop && hudDateFmt
                      && legacyReports == null && legacyFlyout == null
                      && legacyLobby == null && legacyRightCab == null
                      && fatEmergency == null && legacyLeftRail == null
                      && deskLog == null && deskLogLabel == null
                      && legacyBrand == null && legacyPause == null
                      && legacySkip == null && legacyS1 == null
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
