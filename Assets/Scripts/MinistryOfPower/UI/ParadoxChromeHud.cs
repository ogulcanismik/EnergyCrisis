using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using MinistryOfPower.Data;
using MinistryOfPower.Runtime;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.UI
{
    /// <summary>
    /// Wireframe HUD shell: top vitals + time/speeds + ESC, verb tabs with compact
    /// left content panels, right-edge Orders tab, bottom chart chips → drawers,
    /// right 24h duck drawer. Map lenses stubbed.
    /// </summary>
    public sealed class ParadoxChromeHud : MonoBehaviour
    {
        public enum MenuId
        {
            None,
            Construction,
            EnergyMix,
            Resources,
            Deals,
            Policy,
            Mandate,
            Budget,
            History,
            Cabinet
        }

        private Text _dateTimeText;
        private Text _tickText;
        private Text _treasuryText;
        private Text _confidenceText;
        private Text _marginText;
        private Text _speedText;
        private Text _panelTitle;
        private Text _panelBody;
        private Text _ordersBody;
        private Text _ordersToggleLabel;
        private Text _seasonBannerText;
        private Text _tipText;
        private Text _filterToggleLabel;
        private Text _dealBriefToggleLabel;
        private GameObject _tipBanner;
        private float _tipTimer;
        private GameObject _leftPanel;
        private GameObject _filterToggleGo;
        private GameObject _dealBriefToggleGo;
        private GameObject _ordersPanel;
        private GameObject _eventModal;
        private Text _eventTitle;
        private Text _eventBody;
        private GameObject _endBanner;
        private Text _endText;
        private GameObject _helpOverlay;
        private GameObject _seasonBanner;
        private enum BuildFilter { All, Fossil, Clean, Storage }

        private GameObject _regionPanel;
        private Text _regionTitle;
        private Text _regionBody;
        private GameObject _plantPanel;
        private Text _plantTitle;
        private Text _plantBody;
        private string _openPlantId;
        private GameObject _confirmModal;
        private Text _confirmTitle;
        private Text _confirmBody;
        private Action _confirmYes;
        private GameObject _yearModal;
        private Text _yearTitle;
        private Text _yearBody;
        private SupplyDemandStrip _strip;
        private GameObject _duckDrawer;
        private Text _duckToggleLabel;
        private bool _duckOpen;
        private GameObject _chartDrawer;
        private Text _chartTitle;
        private Text _chartBody;
        private MenuId _chartMenu = MenuId.None;
        private MenuId _panelDirtyChart = (MenuId)(-1);
        private ReportChartsHost _reportCharts;
        private GameObject _buildActions;
        private GameObject _buildFilterBar;
        private GameObject _dealActions;
        private GameObject _policyActions;
        private GameObject _cabinetActions;
        private GameObject _crisisStdActions;
        private GameObject _crisisShedActions;
        private readonly Button[] _queueCancelBtns = new Button[5];
        private readonly Text[] _queueCancelLabels = new Text[5];
        private readonly string[] _queueCancelIds = new string[5];
        private readonly Dictionary<string, GameObject> _buildOrderBtns = new Dictionary<string, GameObject>(8);
        private MenuId _openMenu = MenuId.None;
        private BuildFilter _buildFilter = BuildFilter.All;
        private bool _buildFiltersOpen;
        private bool _dealBriefOpen;
        private bool _ordersOpen;
        private Season? _lastSeason;
        private float _seasonBannerTimer;
        private RegionId? _openRegion;
        private readonly StringBuilder _sb = new StringBuilder(2048);
        private int _tooltipBudgetKey = int.MinValue;
        private int _tooltipMarginKey = int.MinValue;
        private int _tooltipCatalogKey = int.MinValue;
        private int _panelDirtyDay = int.MinValue;
        private MenuId _panelDirtyMenu = (MenuId)(-1);
        private bool _staticTipsAttached;

        private Action<GameSpeed> _onSetSpeed;
        private Action<string> _onBuild;
        private Action<string> _onCancelBuild;
        private Action<string> _onRetire;
        private Action<CrisisChoice> _onCrisis;
        private Action<CrisisChoice> _onCabinet;
        private Action _onEmergencyImport;
        private Action _onPrivateReserve;
        private Action _onResign;
        private Action _onSystemMenu;

        // Layout knobs from GameTuning (applied before EnsureUi builds chrome).
        private float _timeControlScale = 1f;
        private float _timeSpeedSlotWidth = 0.056f;
        private int _timeControlFontSize = 10;
        private float _leftRailWidth = 0.11f;
        private float _leftDrawerMaxX = 0.40f;
        private float _rightDrawerMinX = 0.78f;
        private float _reportChartHeight = 0.40f;
        private float _chromeBottom = 0.072f;
        private int _hudBodyFontSize = 11;
        private int _hudTitleFontSize = 18;

        /// <summary>Push designer knobs before chrome is built. No-op after EnsureUi has run.</summary>
        public void ApplyTuning(GameTuning tuning)
        {
            if (tuning == null) return;
            _timeControlScale = tuning.timeControlScale;
            _timeSpeedSlotWidth = tuning.timeSpeedSlotWidth;
            _timeControlFontSize = tuning.timeControlFontSize;
            _leftRailWidth = tuning.leftRailWidth;
            _leftDrawerMaxX = tuning.leftDrawerMaxX;
            _rightDrawerMinX = tuning.rightDrawerMinX;
            _reportChartHeight = tuning.reportChartHeight;
            _chromeBottom = tuning.chromeBottomHeight;
            _hudBodyFontSize = tuning.hudBodyFontSize;
            _hudTitleFontSize = tuning.hudTitleFontSize;
            _reportCharts?.ApplyChartHeight(_reportChartHeight);
        }
        private Func<GameSession> _session;

        private void OnEnable()
        {
            RegionMarker.RegionSelected += OnRegionSelected;
            PlantMarker.PlantSelected += OnPlantSelected;
        }

        private void OnDisable()
        {
            RegionMarker.RegionSelected -= OnRegionSelected;
            PlantMarker.PlantSelected -= OnPlantSelected;
        }

        public void Bind(
            Func<GameSession> session,
            Action<GameSpeed> onSetSpeed,
            Action<string> onBuild, Action<string> onCancelBuild, Action<string> onRetire,
            Action<CrisisChoice> onCrisis, Action<CrisisChoice> onCabinet,
            Action onEmergencyImport, Action onPrivateReserve,
            Action onResign, Action onSystemMenu = null)
        {
            _session = session;
            _onSetSpeed = onSetSpeed;
            _onBuild = onBuild;
            _onCancelBuild = onCancelBuild;
            _onRetire = onRetire;
            _onCrisis = onCrisis;
            _onCabinet = onCabinet;
            _onEmergencyImport = onEmergencyImport;
            _onPrivateReserve = onPrivateReserve;
            _onResign = onResign;
            _onSystemMenu = onSystemMenu;
            EnsureUi();
            MaybeShowFirstRunHelp();
        }

        /// <summary>Kept for runner/session log hooks; desk log UI was removed.</summary>
        public void PushLog(string line) { }

        public void InvalidateTooltipCache()
        {
            _tooltipBudgetKey = int.MinValue;
            _tooltipMarginKey = int.MinValue;
            _tooltipCatalogKey = int.MinValue;
            _panelDirtyDay = int.MinValue;
        }

        /// <summary>Lightweight top-bar time/peak refresh for hourly playhead ticks.</summary>
        public void RefreshTimeChrome()
        {
            EnsureUi();
            GameSession s = _session?.Invoke();
            if (s?.Clock == null || _dateTimeText == null) return;

            ApplyTimeCluster(s);
            if (_speedText != null)
            {
                _speedText.text = s.IsGameOver ? "SACKED" : s.IsVictory ? "WIN" : GameClock.FormatPlaySpeed(s.Clock.Speed);
            }
            if (_duckOpen) _strip?.Render(s);
        }

        public void ShowEvent(PendingEvent evt)
        {
            EnsureUi();
            _eventModal.SetActive(true);
            _eventTitle.text = evt.Title;
            string shedHint = evt.OffersLoadShed
                ? "\n\nLoad-shed: Industry (economy bruise) · Suburbs (confidence hit) · Transit (both)."
                : "";
            _eventBody.text =
                $"{evt.Body}{shedHint}\n\n{evt.ExposureLabel}: {evt.Exposure01 * 100f:0}%\nSeverity: {evt.Severity01 * 100f:0}%";

            bool shed = evt.OffersLoadShed;
            if (_crisisStdActions != null) _crisisStdActions.SetActive(!shed);
            if (_crisisShedActions != null) _crisisShedActions.SetActive(shed);
        }

        public void HideEvent()
        {
            if (_eventModal != null) _eventModal.SetActive(false);
        }

        public void FlashQuicksave()
        {
            EnsureUi();
            ShowSeasonBanner("QUICKSAVE · slot 1", false);
        }

        public void ShowYearReport(YearReport report)
        {
            EnsureUi();
            if (_yearModal == null || report == null) return;
            _yearTitle.text = "YEAR " + report.Year + " REPORT";
            _yearBody.text = report.FormatModal();
            _reportCharts?.ShowYear(report);
            _yearModal.SetActive(true);
            _onSetSpeed?.Invoke(GameSpeed.Paused);
        }

        public void HideYearReport()
        {
            if (_yearModal != null) _yearModal.SetActive(false);
            _reportCharts?.HideYear();
            _onSetSpeed?.Invoke(GameSpeed.Normal);
        }

        public void ForceOpenMenu(MenuId id)
        {
            EnsureUi();
            if (IsChartMenu(id))
            {
                ForceOpenChart(id);
                return;
            }

            _openMenu = IsVerbMenu(id) ? id : MenuId.None;
            if (_openMenu != MenuId.Construction) _buildFiltersOpen = false;
            if (_openMenu != MenuId.Deals) _dealBriefOpen = false;
            if (_openMenu != MenuId.None)
            {
                _ordersOpen = false;
                if (_ordersPanel != null) _ordersPanel.SetActive(false);
            }

            Render();
        }

        public void ForceOpenChart(MenuId id)
        {
            EnsureUi();
            _chartMenu = IsChartMenu(id) ? id : MenuId.None;
            if (_chartMenu != MenuId.None)
            {
                _duckOpen = false;
                if (_duckDrawer != null) _duckDrawer.SetActive(false);
                _ordersOpen = false;
                if (_ordersPanel != null) _ordersPanel.SetActive(false);
            }

            _panelDirtyChart = (MenuId)(-1);
            Render();
        }

        public string DebugPanelTitle => _panelTitle != null ? _panelTitle.text : "";
        public string DebugPanelBody => _panelBody != null ? _panelBody.text : "";
        public MenuId DebugOpenMenu => _openMenu;
        public MenuId DebugOpenChart => _chartMenu;
        public bool DebugOrdersOpen => _ordersOpen;
        public bool DebugDuckOpen => _duckOpen;

        private static bool IsVerbMenu(MenuId id) =>
            id == MenuId.Construction || id == MenuId.Deals || id == MenuId.Policy || id == MenuId.Cabinet;

        private static bool IsChartMenu(MenuId id) =>
            id == MenuId.EnergyMix || id == MenuId.Resources || id == MenuId.Mandate
            || id == MenuId.Budget || id == MenuId.History;

        public string DebugBuildTooltip(string buildId)
        {
            if (string.IsNullOrEmpty(buildId) || !_buildOrderBtns.TryGetValue(buildId, out GameObject go) || go == null)
                return "";
            var tip = go.GetComponent<HoverTooltip>();
            return tip != null ? tip.Tip : "";
        }

        private void OnRegionSelected(RegionId id)
        {
            _openRegion = id;
            GameSession s = _session?.Invoke();
            s?.SetSelectedRegion(id);
            EnsureUi();
            if (_regionPanel != null) _regionPanel.SetActive(true);
            RefreshRegionPanel(s);
            Render();
        }

        private void OnPlantSelected(string plantId)
        {
            _openPlantId = plantId;
            EnsureUi();
            if (_plantPanel != null) _plantPanel.SetActive(true);
            RefreshPlantPanel(_session?.Invoke());
            Render();
        }

        private void RequestConfirm(string title, string body, Action onYes)
        {
            EnsureUi();
            _confirmTitle.text = title;
            _confirmBody.text = body;
            _confirmYes = onYes;
            _confirmModal.SetActive(true);
        }

        private void CloseConfirm(bool accepted)
        {
            if (_confirmModal != null) _confirmModal.SetActive(false);
            Action yes = _confirmYes;
            _confirmYes = null;
            if (accepted) yes?.Invoke();
        }

        public void ShowTip(string tip)
        {
            EnsureUi();
            if (_tipBanner == null || _tipText == null || string.IsNullOrEmpty(tip)) return;
            _tipText.text = tip;
            _tipBanner.SetActive(true);
            _tipTimer = 8f;
        }

        private void Update()
        {
            if (_seasonBanner != null && _seasonBanner.activeSelf)
            {
                _seasonBannerTimer -= Time.unscaledDeltaTime;
                if (_seasonBannerTimer <= 0f) _seasonBanner.SetActive(false);
            }

            if (_tipBanner != null && _tipBanner.activeSelf)
            {
                _tipTimer -= Time.unscaledDeltaTime;
                if (_tipTimer <= 0f) _tipBanner.SetActive(false);
            }
        }

        public void Render()
        {
            EnsureUi();
            GameSession s = _session?.Invoke();
            if (s?.Clock == null) return;

            SeatMeters m = s.Meters;
            ApplyTimeCluster(s);

            _treasuryText.text = "Treasury  " + DisplayUnits.Money(s.Budget);
            int budgetKey = (int)(s.Budget * 10f) ^ (s.Difficulty != null ? (int)s.Difficulty.Id : 0);
            if (budgetKey != _tooltipBudgetKey)
            {
                _tooltipBudgetKey = budgetKey;
                string tip = DisplayUnits.TreasuryExplain();
                if (s.Difficulty != null)
                    tip += "\n" + s.Difficulty.FormatSummary(s.Scenario.StartingBudget);
                UiFactory.AttachTooltip(_treasuryText.gameObject, tip);
            }

            // Political capital maps to seat Confidence until a distinct PC meter exists.
            _confidenceText.text = DisplayUnits.ConfidenceHud(m.Confidence);
            if (!_staticTipsAttached)
            {
                UiFactory.AttachTooltip(_confidenceText.gameObject, DisplayUnits.ConfidenceTip());
                _staticTipsAttached = true;
            }

            float margin = s.LastReport.DemandMw > 0f
                ? s.LastReport.SupplyMw - s.LastReport.DemandMw
                : 0f;
            _marginText.text = "Margin  " + DisplayUnits.Capacity(margin, signed: true);
            _marginText.color = margin < 0f ? UiFactory.Hex("C45A5A") : margin < 40f ? UiFactory.Hex("C4A35A") : UiFactory.Hex("6BA36A");
            int marginKey = (int)margin ^ (s.Clock.AbsoluteDay * 17) ^ ((int)m.Adequacy << 8);
            if (marginKey != _tooltipMarginKey)
            {
                _tooltipMarginKey = marginKey;
                UiFactory.AttachTooltip(_marginText.gameObject,
                    "Global grid margin\n" + DisplayUnits.CapacityPair(s.LastReport.SupplyMw, s.LastReport.DemandMw) + "\n" +
                    "Adeq " + m.Adequacy.ToString("0") + " · Aff " + m.Affordability.ToString("0") +
                    " · Trans " + m.Transition.ToString("0") + " pts");
            }

            if (_speedText != null)
            {
                _speedText.text = s.IsGameOver ? "SACKED" : s.IsVictory ? "WIN" : GameClock.FormatPlaySpeed(s.Clock.Speed);
            }

            MaybeAnnounceSeason(s);
            RefreshBuildCatalogTooltips(s);
            RefreshOrdersSummary(s);
            if (_duckDrawer != null) _duckDrawer.SetActive(_duckOpen);
            if (_duckToggleLabel != null)
                _duckToggleLabel.text = _duckOpen ? "24h▴" : "24h";
            if (_duckOpen) _strip?.Render(s);
            if (_ordersPanel != null) _ordersPanel.SetActive(_ordersOpen);

            int day = s.Clock.AbsoluteDay;
            bool panelDirty = _openMenu != _panelDirtyMenu || day != _panelDirtyDay;
            bool chartDirty = _chartMenu != _panelDirtyChart || day != _panelDirtyDay;
            if (panelDirty)
            {
                _panelDirtyMenu = _openMenu;
                _panelDirtyDay = day;
                RefreshSidePanel(s);
            }
            else if (_openMenu == MenuId.Construction)
            {
                RefreshQueueCancelButtons(s);
            }

            if (chartDirty)
            {
                _panelDirtyChart = _chartMenu;
                _panelDirtyDay = day;
                RefreshChartDrawer(s);
            }

            RefreshQueueCancelButtons(s);
            RefreshRegionPanel(s);
            RefreshPlantPanel(s);
            RefreshBuildFilterUi();

            if (_endBanner != null)
            {
                bool show = s.IsGameOver || s.IsVictory;
                _endBanner.SetActive(show);
                if (show)
                {
                    _endText.text = s.IsVictory ? "MANDATE HONOURED" : "SACKED — Confidence collapsed";
                }
            }
        }

        private void ApplyTimeCluster(GameSession s)
        {
            if (_dateTimeText == null) return;
            // Compact: 15/Jan/2026 · 14:00 — no season / day-night / peak chrome.
            _dateTimeText.text = s.Clock.FormatHudDateTime();
            if (_dateTimeText.color.a < 0.9f)
                _dateTimeText.color = UiFactory.Hex("E7DCC8");
            if (_tickText != null)
                _tickText.gameObject.SetActive(false);
        }

        private void NudgePlaySpeed(int delta)
        {
            GameSession s = _session?.Invoke();
            if (s?.Clock == null) return;
            GameSpeed next = GameClock.NudgePlaySpeed(s.Clock.Speed, delta);
            _onSetSpeed?.Invoke(next);
        }

        private void RefreshOrdersSummary(GameSession s)
        {
            if (_ordersPanel == null || _ordersBody == null || s == null) return;
            _ordersPanel.SetActive(_ordersOpen);
            if (!_ordersOpen) return;

            _sb.Length = 0;
            _sb.AppendLine("CONSTRUCTIONS");
            bool anyBuild = false;
            IReadOnlyList<BuildOrder> orders = s.Builds.Orders;
            for (int i = 0; i < orders.Count; i++)
            {
                BuildOrder o = orders[i];
                if (o.IsCancelled || o.IsComplete) continue;
                anyBuild = true;
                _sb.Append("· ").Append(o.DisplayName)
                    .Append("  ").Append(o.QuartersRemaining).Append("q left");
                if (o.PaymentMode == BuildPaymentMode.PerQuarter && o.QuarterlyCost > 0f)
                    _sb.Append("  ").Append(DisplayUnits.MoneyPerQuarter(o.QuarterlyCost));
                _sb.Append('\n');
            }

            if (!anyBuild) _sb.AppendLine("· None in progress");

            _sb.AppendLine().AppendLine("DEADLINES / TIMED");
            bool anyDeadline = false;
            if (s.EmergencyImportDaysRemaining > 0)
            {
                anyDeadline = true;
                _sb.Append("· Emerg. import  ").Append(s.EmergencyImportDaysRemaining).Append("d  ")
                    .Append(DisplayUnits.Capacity(s.EmergencyImportMw)).Append('\n');
            }

            CabinetState c = s.Cabinet;
            if (c != null && c.TariffFreezeActive && c.TariffFreezeDaysRemaining > 0)
            {
                anyDeadline = true;
                _sb.Append("· Tariff freeze  ").Append(c.TariffFreezeDaysRemaining).Append("d\n");
            }

            if (c != null && c.EmergencyFossilActive && c.EmergencyFossilDaysRemaining > 0)
            {
                anyDeadline = true;
                _sb.Append("· Emerg. fossil  ").Append(c.EmergencyFossilDaysRemaining).Append("d\n");
            }

            DayModifiers mods = s.Modifiers;
            if (mods != null)
            {
                if (mods.OilShockDaysRemaining > 0)
                {
                    anyDeadline = true;
                    _sb.Append("· Oil shock  ").Append(mods.OilShockDaysRemaining).Append("d\n");
                }

                if (mods.HeatwaveDaysRemaining > 0)
                {
                    anyDeadline = true;
                    _sb.Append("· Heatwave  ").Append(mods.HeatwaveDaysRemaining).Append("d\n");
                }

                if (mods.DroughtDaysRemaining > 0)
                {
                    anyDeadline = true;
                    _sb.Append("· Drought  ").Append(mods.DroughtDaysRemaining).Append("d\n");
                }

                if (mods.ImportDisruptionDaysRemaining > 0)
                {
                    anyDeadline = true;
                    _sb.Append("· Import cut  ").Append(mods.ImportDisruptionDaysRemaining).Append("d\n");
                }
            }

            if (!anyDeadline) _sb.AppendLine("· No timed deals active");
            _ordersBody.text = _sb.ToString();
        }

        private static string Bar(float value01to100)
        {
            int filled = Mathf.Clamp(Mathf.RoundToInt(value01to100 / 10f), 0, 10);
            _staticBar.Length = 0;
            _staticBar.Append('[');
            for (int i = 0; i < 10; i++) _staticBar.Append(i < filled ? '■' : '·');
            _staticBar.Append(']');
            return _staticBar.ToString();
        }

        private static readonly StringBuilder _staticBar = new StringBuilder(16);

        private void MaybeAnnounceSeason(GameSession s)
        {
            Season cur = s.Clock.CurrentSeason;
            if (_lastSeason == null)
            {
                _lastSeason = cur;
                return;
            }

            if (_lastSeason.Value == cur) return;
            _lastSeason = cur;

            bool winterWarn = cur == Season.Winter && IsWinterThin(s);
            string msg = cur == Season.Winter
                ? (winterWarn
                    ? "WINTER — firm/storage thin. Peak risk elevated."
                    : "WINTER — heating season. Watch fuel and imports.")
                : cur == Season.Summer
                    ? "SUMMER — AC peaks ahead. Heat cards get louder."
                    : cur == Season.Autumn
                        ? "AUTUMN — build runway before the cold."
                        : "SPRING — quiet window. Fund the long bet.";
            ShowSeasonBanner(msg, winterWarn);
            PushLog(msg);
        }

        private static bool IsWinterThin(GameSession s)
        {
            float storage = s.Portfolio.FuelShare(FuelKind.Storage);
            float fossil = s.Portfolio.FossilShare();
            float firmish = fossil + storage + (s.PrivateReserveMw + s.EmergencyImportMw) /
                            Math.Max(1f, s.Portfolio.TotalCapacityMw());
            return storage < 0.05f || firmish < 0.45f;
        }

        private void ShowSeasonBanner(string msg, bool warn)
        {
            if (_seasonBanner == null || _seasonBannerText == null) return;
            _seasonBannerText.text = msg;
            _seasonBanner.GetComponent<Image>().color = warn ? UiFactory.Hex("5C2A1A") : UiFactory.Hex("2B2118");
            _seasonBanner.SetActive(true);
            _seasonBannerTimer = 5.5f;
        }

        private void RefreshSidePanel(GameSession s)
        {
            if (_leftPanel == null) return;
            bool leftOpen = IsVerbMenu(_openMenu);
            _leftPanel.SetActive(leftOpen);

            if (_buildActions != null) _buildActions.SetActive(_openMenu == MenuId.Construction);
            if (_buildFilterBar != null)
                _buildFilterBar.SetActive(_openMenu == MenuId.Construction && _buildFiltersOpen);
            if (_filterToggleGo != null) _filterToggleGo.SetActive(_openMenu == MenuId.Construction);
            if (_filterToggleLabel != null)
                _filterToggleLabel.text = _buildFiltersOpen ? "Filters ▴" : "Filters ▾";

            if (_dealActions != null) _dealActions.SetActive(_openMenu == MenuId.Deals);
            if (_dealBriefToggleGo != null) _dealBriefToggleGo.SetActive(_openMenu == MenuId.Deals);
            if (_dealBriefToggleLabel != null)
                _dealBriefToggleLabel.text = _dealBriefOpen ? "Deal briefing ▴" : "Deal briefing ▾";
            if (_policyActions != null) _policyActions.SetActive(_openMenu == MenuId.Policy);
            if (_cabinetActions != null) _cabinetActions.SetActive(_openMenu == MenuId.Cabinet);

            SyncLeftDrawerLayout();

            switch (_openMenu)
            {
                case MenuId.Construction:
                    _panelTitle.text = "CONSTRUCTION · " + _buildFilter.ToString().ToUpperInvariant();
                    _panelBody.text = BuildConstructionText(s, _buildFilter);
                    break;
                case MenuId.Deals:
                    _panelTitle.text = "DEALS";
                    _panelBody.text = _dealBriefOpen
                        ? BuildDealsText(s)
                        : "Imports & private reserve deals.\nOpen Deal briefing for costs & buffers.";
                    break;
                case MenuId.Policy:
                    _panelTitle.text = "SUBSIDIES / PRICING";
                    _panelBody.text = BuildPolicyText(s);
                    break;
                case MenuId.Cabinet:
                    _panelTitle.text = "CABINET";
                    _panelBody.text = BuildCabinetText(s);
                    break;
                default:
                    _panelTitle.text = "";
                    _panelBody.text = "";
                    break;
            }
        }

        private void RefreshChartDrawer(GameSession s)
        {
            if (_chartDrawer == null) return;
            bool open = IsChartMenu(_chartMenu);
            _chartDrawer.SetActive(open);
            if (!open)
            {
                _reportCharts?.HidePanelCharts();
                return;
            }

            LayoutChartBodyForCharts(_chartMenu == MenuId.Mandate || _chartMenu == MenuId.Budget);
            _reportCharts?.ShowForMenu(_chartMenu, s);

            switch (_chartMenu)
            {
                case MenuId.EnergyMix:
                    _chartTitle.text = "ENERGY MIX";
                    _chartBody.text = BuildMixText(s);
                    break;
                case MenuId.Resources:
                    _chartTitle.text = "RESOURCES";
                    _chartBody.text = BuildResourcesText(s);
                    break;
                case MenuId.Mandate:
                    _chartTitle.text = "MANDATE / VICTORY";
                    _chartBody.text = s.Mandate != null ? FormatMandateSummary(s) : "—";
                    break;
                case MenuId.Budget:
                    _chartTitle.text = "BUDGET LEDGER";
                    _chartBody.text = s.Ledger != null ? FormatBudgetSummary(s) : "—";
                    break;
                case MenuId.History:
                    _chartTitle.text = "EVENT HISTORY";
                    _chartBody.text = s.History != null ? s.History.FormatPanel() : "—";
                    break;
                default:
                    _chartTitle.text = "";
                    _chartBody.text = "";
                    break;
            }
        }

        private void LayoutChartBodyForCharts(bool chartsVisible)
        {
            if (_chartBody == null) return;
            var rt = _chartBody.rectTransform;
            if (chartsVisible)
            {
                rt.anchorMin = new Vector2(0.04f, 0.50f);
                rt.anchorMax = new Vector2(0.96f, 0.90f);
            }
            else
            {
                rt.anchorMin = new Vector2(0.04f, 0.08f);
                rt.anchorMax = new Vector2(0.96f, 0.90f);
            }
        }

        private static string FormatMandateSummary(GameSession s)
        {
            // Chart carries the sparkline; keep a short victory brief above it.
            var sb = new StringBuilder(320);
            int endYear = s.Scenario.StartYear + s.Mandate.CampaignYears;
            sb.Append("Campaign ").Append(s.Scenario.StartYear).Append('–').Append(endYear);
            sb.Append(" · ").Append(s.Mandate.YearsRemaining(s.Clock, s.Scenario.StartYear)).Append("y left\n");
            sb.Append("Win: T≥").Append(MandateTracker.WinTransition.ToString("0"))
                .Append(" Aff≥").Append(MandateTracker.WinAffordability.ToString("0"))
                .Append(" Adeq≥").Append(MandateTracker.WinAdequacy.ToString("0")).Append('\n');
            sb.Append("Now: T ").Append(s.Meters.Transition.ToString("0.0"))
                .Append("  Aff ").Append(s.Meters.Affordability.ToString("0.0"))
                .Append("  Adeq ").Append(s.Meters.Adequacy.ToString("0.0")).Append('\n');
            sb.Append("Streaks A").Append(s.Mandate.AdequacyGoodStreak)
                .Append("/F").Append(s.Mandate.AffordabilityGoodStreak)
                .Append(" · ").Append(s.Cabinet.ActiveMandate);
            return sb.ToString();
        }

        private static string FormatBudgetSummary(GameSession s)
        {
            BudgetLedger led = s.Ledger;
            var sb = new StringBuilder(280);
            sb.Append("Treasury ").Append(DisplayUnits.Money(s.Budget)).Append('\n');
            sb.Append("YTD in ").Append(DisplayUnits.Money(led.YearIncome))
                .Append(" · out ").Append(DisplayUnits.Money(led.YearExpense)).Append('\n');
            sb.Append("Last net ").Append(DisplayUnits.Money(led.LastNet, signed: true)).Append('\n');
            sb.Append('(').Append(DisplayUnits.TreasuryExplain()).Append(')');
            return sb.ToString();
        }

        private void RefreshQueueCancelButtons(GameSession s)
        {
            if (_queueCancelBtns[0] == null) return;
            bool show = _openMenu == MenuId.Construction && _leftPanel != null && _leftPanel.activeSelf;
            IReadOnlyList<BuildOrder> orders = s.Builds.Orders;
            int qi = 0;
            for (int i = 0; i < orders.Count && qi < _queueCancelBtns.Length; i++)
            {
                BuildOrder o = orders[i];
                if (o.IsCancelled || o.IsComplete) continue;
                _queueCancelIds[qi] = o.Id;
                _queueCancelBtns[qi].gameObject.SetActive(show);
                string drain = o.PaymentMode == BuildPaymentMode.PerQuarter && o.QuarterlyCost > 0f
                    ? " · " + DisplayUnits.MoneyPerQuarter(o.QuarterlyCost)
                    : "";
                bool stressed = s.Budget < o.QuarterlyCost * 2f;
                _queueCancelLabels[qi].text = "X " + o.DisplayName + " · " + o.QuartersRemaining + "q" + drain + (stressed ? " !" : "");
                _queueCancelLabels[qi].color = stressed ? UiFactory.Hex("C45A5A") : UiFactory.Hex("E7DCC8");
                qi++;
            }

            for (int i = qi; i < _queueCancelBtns.Length; i++)
            {
                _queueCancelIds[i] = null;
                _queueCancelBtns[i].gameObject.SetActive(false);
            }
        }

        private void RefreshRegionPanel(GameSession s)
        {
            if (_regionPanel == null || !_regionPanel.activeSelf || _openRegion == null || s == null) return;
            _regionTitle.text = RegionCatalog.DisplayName(_openRegion.Value).ToUpperInvariant();
            string stateLine = "";
            if (!string.IsNullOrEmpty(RegionMarker.SelectedStateCode))
            {
                var st = MinistryOfPower.UI.Map.UsaMapLayout.Find(RegionMarker.SelectedStateCode);
                stateLine = "State: " + st.FullName + " (" + st.Code + ")\n\n";
            }

            _regionBody.text = stateLine + RegionCatalog.BuildDetail(s, _openRegion.Value);
        }

        private void RefreshPlantPanel(GameSession s)
        {
            if (_plantPanel == null || !_plantPanel.activeSelf || string.IsNullOrEmpty(_openPlantId) || s == null)
                return;

            PlantInstance plant = null;
            IReadOnlyList<PlantInstance> plants = s.Portfolio.Plants;
            for (int i = 0; i < plants.Count; i++)
            {
                if (plants[i].Id == _openPlantId) { plant = plants[i]; break; }
            }

            if (plant == null || plant.IsRetired)
            {
                _plantPanel.SetActive(false);
                _openPlantId = null;
                return;
            }

            _plantTitle.text = plant.DisplayName.ToUpperInvariant();
            float marketMul = s.FuelMarket != null ? s.FuelMarket.UpkeepMul(plant.Fuel) : 1f;
            _plantBody.text =
                "Fuel " + plant.Fuel + "  ·  " + RegionCatalog.DisplayName(plant.Region) + "\n" +
                "Capacity " + DisplayUnits.Capacity(plant.CapacityMw) + "  ·  avail " + plant.Availability.ToString("0%") + "\n" +
                "Upkeep " + DisplayUnits.MoneyPerQuarter(plant.QuarterlyUpkeep) +
                " (market ×" + marketMul.ToString("0.00") + " → " +
                DisplayUnits.MoneyPerQuarter(plant.QuarterlyUpkeep * marketMul) + ")\n" +
                "Var cost " + DisplayUnits.PricePerMwh(plant.VariableCostPerMwh) +
                "  ·  oil exp " + plant.OilExposure.ToString("0%") + "\n" +
                "Daily fuel use " + plant.DailyFuelUse.ToString("0.00") + "\n\n" +
                "Retire removes this unit (fossil lobby sting if fossil).";
        }

        private void RefreshBuildFilterUi()
        {
            if (_buildOrderBtns.Count == 0) return;
            bool show = _openMenu == MenuId.Construction;
            foreach (KeyValuePair<string, GameObject> kv in _buildOrderBtns)
            {
                bool match = show && MatchesBuildFilter(kv.Key, _buildFilter);
                if (kv.Value != null) kv.Value.SetActive(match);
            }
        }

        private static bool MatchesBuildFilter(string buildId, BuildFilter filter)
        {
            if (filter == BuildFilter.All) return true;
            switch (buildId)
            {
                case "build_coal":
                case "build_gas":
                case "build_oil":
                    return filter == BuildFilter.Fossil;
                case "build_storage":
                    return filter == BuildFilter.Storage || filter == BuildFilter.Clean;
                case "build_solar":
                case "build_wind":
                case "build_hydro":
                case "build_nuclear":
                case "build_offshore_wind":
                case "build_biomass":
                    return filter == BuildFilter.Clean;
                default:
                    return true;
            }
        }

        private static bool MatchesFuelFilter(FuelKind fuel, BuildFilter filter)
        {
            if (filter == BuildFilter.All) return true;
            if (filter == BuildFilter.Fossil) return fuel.IsFossil();
            if (filter == BuildFilter.Storage) return fuel == FuelKind.Storage;
            if (filter == BuildFilter.Clean) return fuel.IsClean();
            return true;
        }

        private static string BuildConstructionText(GameSession s, BuildFilter filter)
        {
            var sb = new StringBuilder(1400);
            sb.AppendLine("BUILD QUEUE");
            float qBurn = 0f;
            bool any = false;
            IReadOnlyList<BuildOrder> orders = s.Builds.Orders;
            for (int i = 0; i < orders.Count; i++)
            {
                BuildOrder o = orders[i];
                if (o.IsCancelled || o.IsComplete) continue;
                any = true;
                if (o.PaymentMode == BuildPaymentMode.PerQuarter) qBurn += o.QuarterlyCost;
                bool stressed = s.Budget < o.QuarterlyCost * 2f;
                sb.Append(stressed ? "! " : "· ")
                    .Append(o.DisplayName)
                    .Append("  ETA ").Append(o.QuartersRemaining).Append('q');
                if (o.PaymentMode == BuildPaymentMode.PerQuarter && o.QuarterlyCost > 0f)
                    sb.Append("  drain ").Append(DisplayUnits.MoneyPerQuarter(o.QuarterlyCost));
                sb.Append('\n');
            }

            if (!any) sb.AppendLine("· Queue empty — order below.");
            float income = s.Scenario.QuarterlyIncome * (s.Difficulty?.IncomeMultiplier ?? 1f);
            if (qBurn > income * 0.85f || (qBurn > 0f && s.Budget < qBurn))
            {
                sb.Append("⚠ Treasury stress: queue drain ").Append(DisplayUnits.MoneyPerQuarter(qBurn))
                    .Append(" vs income ").Append(DisplayUnits.MoneyPerQuarter(income))
                    .Append(" (cash ").Append(DisplayUnits.Money(s.Budget)).Append(")\n");
            }

            sb.AppendLine().Append("CATALOG [").Append(filter).Append("] — cost / MW / upkeep / fuel / time\n");
            IReadOnlyList<BuildDefinitionConfig> cat = s.BuildCatalog;
            for (int i = 0; i < cat.Count; i++)
            {
                BuildDefinitionConfig b = cat[i];
                if (!MatchesFuelFilter(b.ResultFuel, filter)) continue;
                string fuel = ResourceStockpile.NeedsStock(b.ResultFuel) ? b.ResultFuel.ToString() : "none";
                sb.Append("· ").Append(b.DisplayName)
                    .Append("  ").Append(DisplayUnits.Money(b.UpfrontCost))
                    .Append("  ").Append(DisplayUnits.Capacity(b.ResultCapacityMw))
                    .Append("  upk ").Append(DisplayUnits.MoneyPerQuarter(b.QuarterlyUpkeep))
                    .Append("  ").Append(fuel)
                    .Append("  ").Append(b.DurationQuarters).Append("q\n");
            }

            sb.AppendLine().Append(DeskProjections.Build(s, s.Builds.Orders, income));
            return sb.ToString();
        }

        private static string BuildMixText(GameSession s)
        {
            var sb = new StringBuilder(512);
            IReadOnlyList<PlantInstance> plants = s.Portfolio.Plants;
            for (int i = 0; i < plants.Count; i++)
            {
                PlantInstance p = plants[i];
                if (p.IsRetired) continue;
                sb.Append(p.DisplayName).Append("  ").Append(p.Fuel).Append("  ")
                    .Append(DisplayUnits.Capacity(p.CapacityMw)).Append('\n');
            }

            sb.Append("\nClean ").Append((s.Portfolio.TransitionProgress01() * 100f).ToString("0"))
                .Append("%  Fossil ").Append((s.Portfolio.FossilShare() * 100f).ToString("0")).Append('%');
            return sb.ToString();
        }

        private static string BuildResourcesText(GameSession s)
        {
            ResourceStockpile r = s.Resources;
            var sb = new StringBuilder(768);
            sb.Append("Coal ").Append(r.Coal.ToString("0.0"))
                .Append("\nGas ").Append(r.Gas.ToString("0.0"))
                .Append("\nOil ").Append(r.Oil.ToString("0.0"))
                .Append("\nUranium ").Append(r.Uranium.ToString("0.0"))
                .Append("\n\nImports ").Append(DisplayUnits.Capacity(s.Modifiers.ImportMwAvailable))
                .Append('/').Append(DisplayUnits.Capacity(s.Modifiers.ImportMwBaseline))
                .Append("\n\nFuel market (index): ").Append(s.FuelMarket != null ? s.FuelMarket.FormatLine() : "—")
                .Append("\nOil shock mul ").Append(s.OilShockMultiplier.ToString("0.00"))
                .Append("\n\n").Append(DeskProjections.Build(s, s.Builds.Orders,
                    s.Scenario.QuarterlyIncome * (s.Difficulty?.IncomeMultiplier ?? 1f)));
            return sb.ToString();
        }

        private static string BuildDealsText(GameSession s)
        {
            float importCost = 22f + (s.Difficulty?.EventHarshness ?? 1f) * 6f;
            float reserveUpfront = 18f * (s.Difficulty?.BudgetMultiplier ?? 1f);
            return
                "Infrastructure investment & procurement.\n\n" +
                "Interconnector baseline: " + DisplayUnits.Capacity(s.Modifiers.ImportMwAvailable) + "/" +
                DisplayUnits.Capacity(s.Modifiers.ImportMwBaseline) + "\n" +
                "Emergency import buffer: " + DisplayUnits.Capacity(s.EmergencyImportMw) +
                " (" + s.EmergencyImportDaysRemaining + "d left)\n" +
                "  → Buy +" + DisplayUnits.Capacity(80f) + " / 10d for ~" + DisplayUnits.Money(importCost) + ".\n\n" +
                "Private reserve: " + DisplayUnits.Capacity(s.PrivateReserveMw) + " (cap " +
                DisplayUnits.Capacity(200f) + ") · " + DisplayUnits.MoneyPerQuarter(s.PrivateReserveQuarterlyCost) + "\n" +
                "  → Sign +" + DisplayUnits.Capacity(60f) + " for ~" + DisplayUnits.Money(reserveUpfront) +
                " upfront + " + DisplayUnits.MoneyPerQuarter(4.5f) + "\n\n" +
                "Extra firm MW feeds the day resolve.";
        }

        private static string BuildPolicyText(GameSession s)
        {
            CabinetState c = s.Cabinet;
            return
                "Ministerial energy policy — not a voter bloc sim.\n\n" +
                "RE Subsidy: pay treasury for transition + bill relief; lobby bruises confidence.\n" +
                "Tariff Freeze: protect affordability; treasury pays the gap.\n" +
                "Fill Reserve: stock fuels + adequacy bump.\n" +
                "Emergency Fossil: adequacy now; transition and lobby cost.\n\n" +
                $"Active: {(c.TariffFreezeActive ? "tariff freeze " + c.TariffFreezeDaysRemaining + "d" : "no freeze")}" +
                $" · {(c.EmergencyFossilActive ? "emerg fossil " + c.EmergencyFossilDaysRemaining + "d" : "no emerg fossil")}\n\n" +
                "Cause→effect (lightweight):\n" +
                "· Clean builds ↑ → climate pressure ↓, fossil lobby sting on order\n" +
                "· Retire fossil → lobby confidence hit\n" +
                "· Subsidy → transition ↑, lobby bruise";
        }

        private static string BuildCabinetText(GameSession s)
        {
            CabinetState c = s.Cabinet;
            float lobby = s.FossilLobby01 * 100f;
            return
                "PM Confidence " + DisplayUnits.Points(c.PmConfidence, "0.0") + "\n" +
                "Mandate: " + c.ActiveMandate + "\n" +
                "Tariff freeze: " + (c.TariffFreezeActive ? c.TariffFreezeDaysRemaining + "d" : "off") + "\n" +
                "Emergency fossil: " + (c.EmergencyFossilActive ? c.EmergencyFossilDaysRemaining + "d" : "off") + "\n\n" +
                "LOBBY\n" +
                "Fossil  " + Bar(lobby) + "  " + DisplayUnits.LobbyPts(lobby) + "\n" +
                "Climate " + Bar(c.ClimateMandatePressure) + "  " + DisplayUnits.LobbyPts(c.ClimateMandatePressure) + "\n" +
                "Indust. " + Bar(c.IndustryPressure) + "  " + DisplayUnits.LobbyPts(c.IndustryPressure) + "\n" +
                "Bills   " + Bar(c.AffordabilityMandate) + "  " + DisplayUnits.LobbyPts(c.AffordabilityMandate) + "\n\n" +
                "Fossil lobby raises retire cost & bruises clean builds.\n" +
                "Climate pressure rises when transition stalls.\n" +
                "Industry wants firm MW and cheap fuel.";
        }

        private void RefreshBuildCatalogTooltips(GameSession s)
        {
            if (s?.BuildCatalog == null || _buildOrderBtns.Count == 0) return;
            IReadOnlyList<BuildDefinitionConfig> cat = s.BuildCatalog;
            int key = cat.Count * 397;
            for (int i = 0; i < cat.Count; i++)
            {
                if (cat[i] != null) key ^= cat[i].Id != null ? cat[i].Id.GetHashCode() : i;
            }

            if (key == _tooltipCatalogKey) return;
            _tooltipCatalogKey = key;

            for (int i = 0; i < cat.Count; i++)
            {
                BuildDefinitionConfig b = cat[i];
                if (!_buildOrderBtns.TryGetValue(b.Id, out GameObject go) || go == null) continue;
                UiFactory.AttachTooltip(go, b.FormatCatalogTooltip());
            }
        }

        private void ToggleMenu(MenuId id)
        {
            if (!IsVerbMenu(id)) return;
            // Radio/tab: one verb surface at a time. Re-click closes.
            bool closing = _openMenu == id;
            _openMenu = closing ? MenuId.None : id;

            if (_openMenu != MenuId.Construction) _buildFiltersOpen = false;
            if (_openMenu != MenuId.Deals) _dealBriefOpen = false;
            if (_openMenu != MenuId.None)
            {
                _ordersOpen = false;
                if (_ordersPanel != null) _ordersPanel.SetActive(false);
            }

            _panelDirtyMenu = (MenuId)(-1); // force side-panel rebuild
            Render();
        }

        private void ToggleChartDrawer(MenuId id)
        {
            if (!IsChartMenu(id)) return;
            bool closing = _chartMenu == id;
            _chartMenu = closing ? MenuId.None : id;
            if (_chartMenu != MenuId.None)
            {
                _duckOpen = false;
                if (_duckDrawer != null) _duckDrawer.SetActive(false);
                _ordersOpen = false;
                if (_ordersPanel != null) _ordersPanel.SetActive(false);
            }

            _panelDirtyChart = (MenuId)(-1);
            Render();
        }

        private void SyncLeftDrawerLayout()
        {
            if (_leftPanel == null) return;
            var rt = _leftPanel.GetComponent<RectTransform>();
            if (rt == null) return;
            // Compact verb content panel (triggers stay full-size under top bar).
            const float tabY0 = 0.915f;
            const float panelMaxX = 0.30f;
            const float panelMinY = 0.30f;
            rt.anchorMin = new Vector2(0f, panelMinY);
            rt.anchorMax = new Vector2(panelMaxX, tabY0);
        }

        private void ToggleOrders()
        {
            _ordersOpen = !_ordersOpen;
            if (_ordersOpen)
            {
                _openMenu = MenuId.None;
                _chartMenu = MenuId.None;
                _duckOpen = false;
                if (_duckDrawer != null) _duckDrawer.SetActive(false);
                if (_chartDrawer != null) _chartDrawer.SetActive(false);
                _panelDirtyMenu = (MenuId)(-1);
                _panelDirtyChart = (MenuId)(-1);
            }

            if (_ordersPanel != null) _ordersPanel.SetActive(_ordersOpen);
            Render();
        }

        private void ToggleDuck()
        {
            _duckOpen = !_duckOpen;
            if (_duckOpen)
            {
                _chartMenu = MenuId.None;
                if (_chartDrawer != null) _chartDrawer.SetActive(false);
                _panelDirtyChart = (MenuId)(-1);
            }

            if (_duckDrawer != null) _duckDrawer.SetActive(_duckOpen);
            if (_duckToggleLabel != null)
                _duckToggleLabel.text = _duckOpen ? "24h▴" : "24h";
            if (_duckOpen)
            {
                GameSession s = _session?.Invoke();
                if (s != null) _strip?.Render(s);
            }
        }

        private void ToggleBuildFilters()
        {
            _buildFiltersOpen = !_buildFiltersOpen;
            _panelDirtyMenu = (MenuId)(-1);
            Render();
        }

        private void ToggleDealBrief()
        {
            _dealBriefOpen = !_dealBriefOpen;
            _panelDirtyMenu = (MenuId)(-1);
            Render();
        }

        private void MaybeShowFirstRunHelp()
        {
            if (GameSettings.HelpSeen || _helpOverlay == null) return;
            _helpOverlay.SetActive(true);
        }

        private void DismissHelp()
        {
            GameSettings.HelpSeen = true;
            if (_helpOverlay != null) _helpOverlay.SetActive(false);
            GameSpeed start = GameSettings.DefaultSpeed;
            if (start == GameSpeed.Paused) return;
            if (GameClock.PlayMultiplier(start) <= 0) start = GameSpeed.Normal;
            _onSetSpeed?.Invoke(start);
        }

        private void EnsureUi()
        {
            if (_dateTimeText != null) return;

            UiFactory.EnsureEventSystem(transform);
            var canvasGo = new GameObject("ParadoxChrome", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            TooltipService.Ensure(canvasGo.transform);

            Color bar = UiFactory.Hex("1E1812");
            Color accent = UiFactory.Hex("C4A35A");
            Color paper = UiFactory.Hex("E7DCC8");
            Color ink = UiFactory.Hex("1A140F");
            Color panel = UiFactory.Hex("241C14");

            float scale = Mathf.Clamp(_timeControlScale, 0.6f, 1.4f);
            float timeLeft = Mathf.Clamp(1f - 0.32f * scale, 0.65f, 0.76f);

            // Thin single-row top: vitals left · date/hour + − Nx + right.
            const float topY0 = 0.955f;
            const float topY1 = 1f;
            UiFactory.Panel(canvasGo.transform, "TopBar", new Vector2(0f, topY0), new Vector2(1f, topY1), bar);

            int vitalsFont = Mathf.Max(10, _hudBodyFontSize);
            _treasuryText = UiFactory.Label(canvasGo.transform, "Treasury", paper, vitalsFont, FontStyle.Bold,
                new Vector2(0.008f, topY0), new Vector2(0.17f, topY1));
            _confidenceText = UiFactory.Label(canvasGo.transform, "Conf", paper, vitalsFont, FontStyle.Bold,
                new Vector2(0.17f, topY0), new Vector2(0.28f, topY1));
            _marginText = UiFactory.Label(canvasGo.transform, "Margin", paper, vitalsFont, FontStyle.Bold,
                new Vector2(0.28f, topY0), new Vector2(Mathf.Max(0.46f, timeLeft - 0.01f), topY1));

            UiFactory.Panel(canvasGo.transform, "TimeBar", new Vector2(timeLeft, topY0), new Vector2(1f, topY1), UiFactory.Hex("2B2118"));
            // Compact − / Nx / + chips — shorter than the bar so they read as text-height.
            float chipHPad = 0.012f;
            float speedY0 = topY0 + chipHPad;
            float speedY1 = topY1 - chipHPad;
            float chipW = 0.016f;
            float labelW = 0.028f;
            const float speedGap = 0.002f;
            float clusterW = chipW * 2f + labelW + speedGap * 2f;
            float speedX0 = 0.995f - clusterW;
            int spdFont = Mathf.Max(8, _timeControlFontSize - 1);
            int dateFont = Mathf.Max(10, _timeControlFontSize);

            _dateTimeText = UiFactory.Label(canvasGo.transform, "DateTime", paper, dateFont, FontStyle.Bold,
                new Vector2(timeLeft + 0.006f, topY0), new Vector2(speedX0 - 0.006f, topY1), TextAnchor.MiddleLeft);
            _dateTimeText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _dateTimeText.verticalOverflow = VerticalWrapMode.Overflow;
            _dateTimeText.raycastTarget = false;
            _tickText = null; // peak / season / day-night removed from time chrome

            UiFactory.Button(canvasGo.transform, "SpdMinus", "−",
                new Vector2(speedX0, speedY0), new Vector2(speedX0 + chipW, speedY1),
                () => NudgePlaySpeed(-1), accent, ink, spdFont, "Slower (min 1×)");
            _speedText = UiFactory.Label(canvasGo.transform, "Spd", paper, spdFont, FontStyle.Bold,
                new Vector2(speedX0 + chipW + speedGap, speedY0),
                new Vector2(speedX0 + chipW + speedGap + labelW, speedY1), TextAnchor.MiddleCenter);
            _speedText.raycastTarget = false;
            _speedText.text = "1x";
            UiFactory.Button(canvasGo.transform, "SpdPlus", "+",
                new Vector2(speedX0 + chipW + speedGap + labelW + speedGap, speedY0),
                new Vector2(0.995f, speedY1),
                () => NudgePlaySpeed(+1), accent, ink, spdFont, "Faster (max 5×)");

            var seasonImg = UiFactory.Panel(canvasGo.transform, "SeasonBanner", new Vector2(0.28f, 0.86f), new Vector2(0.72f, 0.91f), UiFactory.Hex("2B2118"));
            _seasonBanner = seasonImg.gameObject;
            _seasonBannerText = UiFactory.Label(seasonImg.transform, "ST", accent, 14, FontStyle.Bold,
                new Vector2(0.04f, 0.1f), new Vector2(0.96f, 0.9f), TextAnchor.MiddleCenter);
            _seasonBanner.SetActive(false);

            var tipImg = UiFactory.Panel(canvasGo.transform, "TipBanner", new Vector2(0.22f, 0.80f), new Vector2(0.78f, 0.855f), UiFactory.Hex("3A2E22"));
            _tipBanner = tipImg.gameObject;
            _tipText = UiFactory.Label(tipImg.transform, "TipT", paper, 12, FontStyle.Italic,
                new Vector2(0.03f, 0.1f), new Vector2(0.97f, 0.9f), TextAnchor.MiddleCenter);
            _tipBanner.SetActive(false);

            // ——— VERB TABS under vitals (readable chips; compact content panel below) ———
            float bot = Mathf.Clamp(_chromeBottom, 0.045f, 0.058f);
            const float ordersTabW = 0.018f;
            float ordersX0 = 1f - ordersTabW;
            const float tabY0 = 0.915f;
            const float tabY1 = 0.955f;
            // Readable trigger chips (restore prior footprint); panel itself is compact.
            const float verbPanelMaxX = 0.30f;
            const float verbPanelMinY = 0.30f;
            float tabRowMax = Mathf.Max(verbPanelMaxX, 0.40f);
            UiFactory.Panel(canvasGo.transform, "TabRow", new Vector2(0f, tabY0), new Vector2(tabRowMax, tabY1), bar);
            float tx = 0.008f;
            float tabW = (tabRowMax - 0.016f) / 4f;
            TabChip(canvasGo.transform, "Construction", MenuId.Construction, ref tx, tabW, tabY0, tabY1,
                "Queue, catalog, projections");
            TabChip(canvasGo.transform, "Deals", MenuId.Deals, ref tx, tabW, tabY0, tabY1,
                "Imports & private reserve");
            TabChip(canvasGo.transform, "Cabinet", MenuId.Cabinet, ref tx, tabW, tabY0, tabY1,
                "PM confidence, lobby, levers");
            TabChip(canvasGo.transform, "Subsidies", MenuId.Policy, ref tx, tabW, tabY0, tabY1,
                "Subsidies, freezes, reserves");

            // Compact left content panel (smaller than full rail — not the trigger buttons).
            var side = UiFactory.Panel(canvasGo.transform, "SidePanel",
                new Vector2(0f, verbPanelMinY), new Vector2(verbPanelMaxX, tabY0), panel);
            _leftPanel = side.gameObject;
            _panelTitle = UiFactory.Label(side.transform, "PTitle", accent, _hudTitleFontSize, FontStyle.Bold,
                new Vector2(0.04f, 0.9f), new Vector2(0.78f, 0.98f));
            _panelBody = UiFactory.Label(side.transform, "PBody", paper, _hudBodyFontSize, FontStyle.Normal,
                new Vector2(0.04f, 0.52f), new Vector2(0.96f, 0.9f), TextAnchor.UpperLeft);

            var filterToggle = UiFactory.Button(side.transform, "FilterToggle", "Filters ▾",
                new Vector2(0.04f, 0.455f), new Vector2(0.36f, 0.51f),
                ToggleBuildFilters, UiFactory.Hex("3A2E22"), accent, 11, "Catalog filters");
            _filterToggleGo = filterToggle.gameObject;
            _filterToggleLabel = filterToggle.GetComponentInChildren<Text>();
            _filterToggleGo.SetActive(false);

            var dealBriefToggle = UiFactory.Button(side.transform, "DealBriefToggle", "Deal briefing ▾",
                new Vector2(0.04f, 0.455f), new Vector2(0.55f, 0.51f),
                ToggleDealBrief, UiFactory.Hex("3A2E22"), accent, 11, "Costs & buffer details");
            _dealBriefToggleGo = dealBriefToggle.gameObject;
            _dealBriefToggleLabel = dealBriefToggle.GetComponentInChildren<Text>();
            _dealBriefToggleGo.SetActive(false);

            var filterBar = UiFactory.Panel(side.transform, "BuildFilters", new Vector2(0.38f, 0.455f), new Vector2(0.98f, 0.515f), new Color(0, 0, 0, 0.2f));
            _buildFilterBar = filterBar.gameObject;
            float fx = 0.02f;
            AddBuildFilterBtn(filterBar.transform, "All", BuildFilter.All, ref fx);
            AddBuildFilterBtn(filterBar.transform, "Fossil", BuildFilter.Fossil, ref fx);
            AddBuildFilterBtn(filterBar.transform, "Clean", BuildFilter.Clean, ref fx);
            AddBuildFilterBtn(filterBar.transform, "Store", BuildFilter.Storage, ref fx);
            _buildFilterBar.SetActive(false);

            UiFactory.Button(side.transform, "HelpAgain", "?", new Vector2(0.9f, 0.92f), new Vector2(0.98f, 0.99f),
                () =>
                {
                    GameSettings.HelpSeen = false;
                    if (_helpOverlay != null) _helpOverlay.SetActive(true);
                }, UiFactory.Hex("3A2E22"), accent, 14, "Help brief");

            for (int i = 0; i < _queueCancelBtns.Length; i++)
            {
                int idx = i;
                float y1 = 0.40f - i * 0.055f;
                float y0 = y1 - 0.05f;
                var btn = UiFactory.Button(side.transform, "QCxl" + i, "—", new Vector2(0.04f, y0), new Vector2(0.96f, y1),
                    () =>
                    {
                        if (!string.IsNullOrEmpty(_queueCancelIds[idx]))
                            _onCancelBuild?.Invoke(_queueCancelIds[idx]);
                    }, UiFactory.Hex("4A2820"), paper, 10, "Cancel this build — progress forfeited.");
                _queueCancelBtns[i] = btn;
                _queueCancelLabels[i] = btn.GetComponentInChildren<Text>();
                btn.gameObject.SetActive(false);
            }

            var buildHost = UiFactory.Panel(side.transform, "BuildActions", new Vector2(0f, 0f), new Vector2(1f, 0.2f), new Color(0, 0, 0, 0));
            _buildActions = buildHost.gameObject;
            string[] ids =
            {
                "build_coal", "build_gas", "build_oil", "build_solar", "build_wind",
                "build_hydro", "build_storage", "build_nuclear", "build_offshore_wind", "build_biomass"
            };
            string[] labels = { "Coal", "Gas", "Oil", "Solar", "Wind", "Hydro", "Storage", "Nuke", "OffWind", "Bio" };
            _buildOrderBtns.Clear();
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                int col = i % 5;
                int rowIdx = i / 5;
                float x0 = 0.02f + col * 0.196f;
                float x1 = x0 + 0.18f;
                float y1 = 0.95f - rowIdx * 0.48f;
                float y0 = y1 - 0.42f;
                var ordBtn = UiFactory.Button(buildHost.transform, "Ord_" + id, labels[i],
                    new Vector2(x0, y0), new Vector2(x1, y1),
                    () => _onBuild?.Invoke(id), UiFactory.Hex("3A2E22"), paper, 10);
                _buildOrderBtns[id] = ordBtn.gameObject;
            }

            var dealHost = UiFactory.Panel(side.transform, "DealActions", new Vector2(0f, 0f), new Vector2(1f, 0.22f), new Color(0, 0, 0, 0));
            _dealActions = dealHost.gameObject;
            UiFactory.Button(dealHost.transform, "DealImport", "Buy Emergency Import", new Vector2(0.04f, 0.52f), new Vector2(0.96f, 0.95f),
                () => _onEmergencyImport?.Invoke(), UiFactory.Hex("30506B"), paper, 13);
            UiFactory.Button(dealHost.transform, "DealReserve", "Sign Private Reserve", new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.48f),
                () => _onPrivateReserve?.Invoke(), UiFactory.Hex("3E5C3A"), paper, 13);

            var polHost = UiFactory.Panel(side.transform, "PolicyActions", new Vector2(0f, 0f), new Vector2(1f, 0.28f), new Color(0, 0, 0, 0));
            _policyActions = polHost.gameObject;
            UiFactory.Button(polHost.transform, "PolSub", "RE Subsidy", new Vector2(0.04f, 0.52f), new Vector2(0.48f, 0.95f),
                () => _onCabinet?.Invoke(CrisisChoice.RenewableSubsidy), UiFactory.Hex("2E4A3A"), paper, 12,
                "Pay for transition + bill relief; lobby bruises confidence.");
            UiFactory.Button(polHost.transform, "PolTar", "Tariff Freeze", new Vector2(0.52f, 0.52f), new Vector2(0.96f, 0.95f),
                () => _onCabinet?.Invoke(CrisisChoice.TariffFreeze), UiFactory.Hex("30506B"), paper, 12,
                "Protect bills; treasury pays.");
            UiFactory.Button(polHost.transform, "PolRes", "Fill Reserve", new Vector2(0.04f, 0.05f), new Vector2(0.48f, 0.45f),
                () => _onCabinet?.Invoke(CrisisChoice.StrategicReserve), UiFactory.Hex("3E5C3A"), paper, 12,
                "Spend treasury to top up fuel stocks.");
            UiFactory.Button(polHost.transform, "PolFos", "Emerg. Fossil", new Vector2(0.52f, 0.05f), new Vector2(0.96f, 0.45f),
                () => _onCabinet?.Invoke(CrisisChoice.EmergencyFossil), UiFactory.Hex("6B4E30"), paper, 12,
                "Buy adequacy with treasury and transition.");

            var cabHost = UiFactory.Panel(side.transform, "CabinetActions", new Vector2(0f, 0f), new Vector2(1f, 0.28f), new Color(0, 0, 0, 0));
            _cabinetActions = cabHost.gameObject;
            UiFactory.Button(cabHost.transform, "CabFos", "Emerg. Fossil", new Vector2(0.04f, 0.52f), new Vector2(0.48f, 0.95f),
                () => _onCabinet?.Invoke(CrisisChoice.EmergencyFossil), UiFactory.Hex("6B4E30"), paper, 11,
                "Buy adequacy with treasury and transition.");
            UiFactory.Button(cabHost.transform, "CabTar", "Tariff Freeze", new Vector2(0.52f, 0.52f), new Vector2(0.96f, 0.95f),
                () => _onCabinet?.Invoke(CrisisChoice.TariffFreeze), UiFactory.Hex("30506B"), paper, 11,
                "Protect bills; treasury pays.");
            UiFactory.Button(cabHost.transform, "CabRes", "Fill Reserve", new Vector2(0.04f, 0.05f), new Vector2(0.48f, 0.45f),
                () => _onCabinet?.Invoke(CrisisChoice.StrategicReserve), UiFactory.Hex("3E5C3A"), paper, 11,
                "Spend treasury to top up coal/gas/oil stocks.");
            UiFactory.Button(cabHost.transform, "CabSub", "RE Subsidy", new Vector2(0.52f, 0.05f), new Vector2(0.96f, 0.45f),
                () => _onCabinet?.Invoke(CrisisChoice.RenewableSubsidy), UiFactory.Hex("2E4A3A"), paper, 11,
                "Pay for transition + bill relief; lobby bruises confidence.");

            _buildActions.SetActive(false);
            _dealActions.SetActive(false);
            _policyActions.SetActive(false);
            _cabinetActions.SetActive(false);
            _leftPanel.SetActive(false);

            // ——— TOP-RIGHT: ESC (above right-edge Orders tab) ———
            UiFactory.Button(canvasGo.transform, "EscBtn", "ESC",
                new Vector2(ordersX0 - 0.042f, tabY0), new Vector2(ordersX0 - 0.004f, tabY1),
                () => _onSystemMenu?.Invoke(), UiFactory.Hex("6B3030"), paper, 11,
                "System menu — save / load / settings / resign");

            // ——— RIGHT-EDGE Orders tab (Paradox-style) + slide-in from right ———
            var ordersTab = UiFactory.Button(canvasGo.transform, "OrdersToggle", "ORD",
                new Vector2(ordersX0, 0.38f), new Vector2(1f, 0.72f),
                ToggleOrders, UiFactory.Hex("3A2E22"), accent, 9, "Build queue + timed deadlines");
            _ordersToggleLabel = ordersTab.GetComponentInChildren<Text>();
            if (_ordersToggleLabel != null)
            {
                _ordersToggleLabel.text = "ORDERS";
                _ordersToggleLabel.rectTransform.localEulerAngles = new Vector3(0f, 0f, -90f);
                _ordersToggleLabel.fontSize = 10;
            }

            var orders = UiFactory.Panel(canvasGo.transform, "OrdersPanel",
                new Vector2(ordersX0 - 0.20f, verbPanelMinY), new Vector2(ordersX0, tabY0), panel);
            _ordersPanel = orders.gameObject;
            _ordersBody = UiFactory.Label(orders.transform, "OrdersBody", paper, _hudBodyFontSize, FontStyle.Normal,
                new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.96f), TextAnchor.UpperLeft);
            _ordersPanel.SetActive(false);
            _ordersOpen = false;

            // ——— RIGHT: 24h duck chip above lenses, left of Orders edge tab ———
            const float duckChip = 0.042f;
            float duckY0 = bot + 0.012f;
            float duckY1 = duckY0 + duckChip;
            float duckX1 = ordersX0 - 0.004f;
            float duckX0 = duckX1 - duckChip;

            var duckBtn = UiFactory.Button(canvasGo.transform, "DuckBtn", "24h",
                new Vector2(duckX0, duckY0), new Vector2(duckX1, duckY1),
                ToggleDuck, UiFactory.Hex("3A2E22"), accent, 9, "24h load / supply (daily duck)");
            _duckToggleLabel = duckBtn.GetComponentInChildren<Text>();

            // 24h drawer expands left/up from the DuckBtn (right-anchored to that control).
            const float duckPanelW = 0.38f;
            const float duckPanelH = 0.34f;
            float duckDrawerX1 = duckX0 - 0.003f;
            float duckDrawerX0 = duckDrawerX1 - duckPanelW;
            float duckDrawerY0 = duckY0;
            float duckDrawerY1 = duckY0 + duckPanelH;
            var duck = UiFactory.Panel(canvasGo.transform, "DuckDrawer",
                new Vector2(duckDrawerX0, duckDrawerY0), new Vector2(duckDrawerX1, duckDrawerY1), panel);
            _duckDrawer = duck.gameObject;
            UiFactory.Label(duck.transform, "DuckTitle", accent, 12, FontStyle.Bold,
                new Vector2(0.03f, 0.90f), new Vector2(0.90f, 0.98f)).text = "24h DISPATCH";
            UiFactory.Button(duck.transform, "DuckClose", "X",
                new Vector2(0.90f, 0.90f), new Vector2(0.98f, 0.98f),
                () => { if (_duckOpen) ToggleDuck(); }, UiFactory.Hex("6B3030"), paper, 11);
            _strip = SupplyDemandStrip.Create(duck.transform,
                new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.88f));
            _duckDrawer.SetActive(false);
            _duckOpen = false;
            // Keep the 24h chip above the expanded panel for toggle-close.
            duckBtn.transform.SetAsLastSibling();

            // Chart report drawer — horizontally centered, lower/mid band.
            const float chartPanelW = 0.42f;
            float chartX0 = (1f - chartPanelW) * 0.5f;
            float chartX1 = chartX0 + chartPanelW;
            float chartDrawerY0 = bot + 0.04f;
            const float chartDrawerY1 = 0.44f;
            var chart = UiFactory.Panel(canvasGo.transform, "ChartDrawer",
                new Vector2(chartX0, chartDrawerY0), new Vector2(chartX1, chartDrawerY1), panel);
            _chartDrawer = chart.gameObject;
            _chartTitle = UiFactory.Label(chart.transform, "ChartTitle", accent, 12, FontStyle.Bold,
                new Vector2(0.03f, 0.90f), new Vector2(0.90f, 0.98f));
            UiFactory.Button(chart.transform, "ChartClose", "X",
                new Vector2(0.90f, 0.90f), new Vector2(0.98f, 0.98f),
                () => { if (_chartMenu != MenuId.None) ToggleChartDrawer(_chartMenu); },
                UiFactory.Hex("6B3030"), paper, 11);
            _chartBody = UiFactory.Label(chart.transform, "ChartBody", paper, _hudBodyFontSize, FontStyle.Normal,
                new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.90f), TextAnchor.UpperLeft);
            _reportCharts = ReportChartsHost.Create(chart.transform);
            _reportCharts.ApplyChartHeight(_reportChartHeight);
            _chartDrawer.SetActive(false);
            _chartMenu = MenuId.None;

            // ——— BOTTOM: square chart chips (left) + lens stubs (right of duck, left of Orders) ———
            UiFactory.Panel(canvasGo.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, bot), bar);
            float sq = Mathf.Min(bot - 0.008f, 0.040f);
            float chartY0 = (bot - sq) * 0.5f;
            float chartY1 = chartY0 + sq;
            float cx = 0.008f;
            ChartBtn(canvasGo.transform, "Mix", MenuId.EnergyMix, ref cx, sq, chartY0, chartY1, "Energy mix");
            ChartBtn(canvasGo.transform, "Fuel", MenuId.Resources, ref cx, sq, chartY0, chartY1, "Fuel stocks");
            ChartBtn(canvasGo.transform, "Mandate", MenuId.Mandate, ref cx, sq, chartY0, chartY1, "Mandate tracker");
            ChartBtn(canvasGo.transform, "Budget", MenuId.Budget, ref cx, sq, chartY0, chartY1, "Budget ledger");
            ChartBtn(canvasGo.transform, "History", MenuId.History, ref cx, sq, chartY0, chartY1, "Event history");

            float lx = duckX0 - 0.004f - sq;
            var lensLog = UiFactory.Button(canvasGo.transform, "LensLogistics", "Log",
                new Vector2(lx, chartY0), new Vector2(lx + sq - 0.004f, chartY1),
                () => ShowTip("Map lenses deferred — logistics paths not implemented yet."),
                UiFactory.Hex("3A2E22"), UiFactory.Hex("8A8070"), 8, "Placeholder — map lenses later");
            lensLog.interactable = false;
            lx -= sq;
            var lensWind = UiFactory.Button(canvasGo.transform, "LensWind", "Wind",
                new Vector2(lx, chartY0), new Vector2(lx + sq - 0.004f, chartY1),
                () => ShowTip("Map lenses deferred — wind overlay not implemented yet."),
                UiFactory.Hex("3A2E22"), UiFactory.Hex("8A8070"), 8, "Placeholder — map lenses later");
            lensWind.interactable = false;

            // Region / plant / confirm / year / event / end / help (unchanged spirit)
            var reg = UiFactory.Panel(canvasGo.transform, "RegionPanel", new Vector2(0.42f, 0.55f), new Vector2(0.70f, 0.84f), UiFactory.Hex("1A1510EE"));
            _regionPanel = reg.gameObject;
            _regionTitle = UiFactory.Label(reg.transform, "RT", accent, 18, FontStyle.Bold,
                new Vector2(0.05f, 0.82f), new Vector2(0.8f, 0.96f));
            _regionBody = UiFactory.Label(reg.transform, "RB", paper, 12, FontStyle.Normal,
                new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.8f), TextAnchor.UpperLeft);
            UiFactory.Button(reg.transform, "RClose", "X", new Vector2(0.86f, 0.82f), new Vector2(0.96f, 0.96f),
                () => { _openRegion = null; _regionPanel.SetActive(false); }, UiFactory.Hex("6B3030"), paper, 14);
            _regionPanel.SetActive(false);

            var plant = UiFactory.Panel(canvasGo.transform, "PlantPanel", new Vector2(0.42f, 0.32f), new Vector2(0.70f, 0.54f), UiFactory.Hex("1A1510EE"));
            _plantPanel = plant.gameObject;
            _plantTitle = UiFactory.Label(plant.transform, "PT", accent, 16, FontStyle.Bold,
                new Vector2(0.05f, 0.78f), new Vector2(0.78f, 0.95f));
            _plantBody = UiFactory.Label(plant.transform, "PB", paper, 12, FontStyle.Normal,
                new Vector2(0.05f, 0.28f), new Vector2(0.95f, 0.76f), TextAnchor.UpperLeft);
            UiFactory.Button(plant.transform, "PClose", "X", new Vector2(0.86f, 0.78f), new Vector2(0.96f, 0.95f),
                () => { _openPlantId = null; _plantPanel.SetActive(false); }, UiFactory.Hex("6B3030"), paper, 14);
            UiFactory.Button(plant.transform, "PRetire", "Retire this plant",
                new Vector2(0.1f, 0.05f), new Vector2(0.9f, 0.24f),
                () =>
                {
                    string id = _openPlantId;
                    RequestConfirm("RETIRE PLANT?",
                        "Permanently retire this unit. Fossil retirements sting lobby confidence.",
                        () =>
                        {
                            _onRetire?.Invoke(id);
                            _openPlantId = null;
                            if (_plantPanel != null) _plantPanel.SetActive(false);
                        });
                }, UiFactory.Hex("6B3030"), paper, 12);
            _plantPanel.SetActive(false);

            var confirm = UiFactory.Panel(canvasGo.transform, "ConfirmModal", new Vector2(0.28f, 0.32f), new Vector2(0.72f, 0.68f), UiFactory.Hex("2B2118"));
            _confirmModal = confirm.gameObject;
            _confirmTitle = UiFactory.Label(confirm.transform, "CT", accent, 20, FontStyle.Bold,
                new Vector2(0.06f, 0.72f), new Vector2(0.94f, 0.92f), TextAnchor.MiddleCenter);
            _confirmBody = UiFactory.Label(confirm.transform, "CB", paper, 14, FontStyle.Normal,
                new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.7f), TextAnchor.UpperLeft);
            UiFactory.Button(confirm.transform, "CYes", "Confirm", new Vector2(0.08f, 0.08f), new Vector2(0.48f, 0.26f),
                () => CloseConfirm(true), UiFactory.Hex("6B3030"), paper, 14);
            UiFactory.Button(confirm.transform, "CNo", "Cancel", new Vector2(0.52f, 0.08f), new Vector2(0.92f, 0.26f),
                () => CloseConfirm(false), UiFactory.Hex("3A2E22"), paper, 14);
            _confirmModal.SetActive(false);

            var year = UiFactory.Panel(canvasGo.transform, "YearModal", new Vector2(0.26f, 0.22f), new Vector2(0.74f, 0.78f), UiFactory.Hex("1E1812"));
            _yearModal = year.gameObject;
            _yearTitle = UiFactory.Label(year.transform, "YT", accent, 22, FontStyle.Bold,
                new Vector2(0.06f, 0.88f), new Vector2(0.94f, 0.97f), TextAnchor.MiddleCenter);
            _yearBody = UiFactory.Label(year.transform, "YB", paper, 13, FontStyle.Normal,
                new Vector2(0.08f, 0.50f), new Vector2(0.92f, 0.86f), TextAnchor.UpperLeft);
            UiFactory.Button(year.transform, "YOk", "Continue the mandate",
                new Vector2(0.25f, 0.04f), new Vector2(0.75f, 0.14f),
                HideYearReport, UiFactory.Hex("8B6914"), paper, 14);
            _yearModal.SetActive(false);
            _reportCharts?.AttachYearChart(year.transform);

            var modal = UiFactory.Panel(canvasGo.transform, "EventModal", new Vector2(0.22f, 0.22f), new Vector2(0.78f, 0.78f), UiFactory.Hex("241C14"));
            _eventModal = modal.gameObject;
            _eventTitle = UiFactory.Label(modal.transform, "ET", accent, 22, FontStyle.Bold,
                new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.95f));
            _eventBody = UiFactory.Label(modal.transform, "EB", paper, 14, FontStyle.Normal,
                new Vector2(0.05f, 0.32f), new Vector2(0.95f, 0.8f), TextAnchor.UpperLeft);

            var std = UiFactory.Panel(modal.transform, "CrisisStd", new Vector2(0f, 0f), new Vector2(1f, 0.28f), new Color(0, 0, 0, 0));
            _crisisStdActions = std.gameObject;
            UiFactory.Button(std.transform, "Abs", "Absorb", new Vector2(0.06f, 0.2f), new Vector2(0.34f, 0.85f),
                () => _onCrisis?.Invoke(CrisisChoice.Absorb), UiFactory.Hex("6B3030"), paper, 13,
                "Take the hit on meters / confidence.");
            UiFactory.Button(std.transform, "Fos", "Emergency Fossil", new Vector2(0.36f, 0.2f), new Vector2(0.64f, 0.85f),
                () => _onCrisis?.Invoke(CrisisChoice.EmergencyFossil), UiFactory.Hex("6B4E30"), paper, 13,
                "Buy adequacy with treasury and transition.");
            UiFactory.Button(std.transform, "Tar", "Tariff Freeze", new Vector2(0.66f, 0.2f), new Vector2(0.94f, 0.85f),
                () => _onCrisis?.Invoke(CrisisChoice.TariffFreeze), UiFactory.Hex("30506B"), paper, 13,
                "Protect bills; treasury pays.");

            var shed = UiFactory.Panel(modal.transform, "CrisisShed", new Vector2(0f, 0f), new Vector2(1f, 0.28f), new Color(0, 0, 0, 0));
            _crisisShedActions = shed.gameObject;
            UiFactory.Button(shed.transform, "ShedInd", "Shed Industry", new Vector2(0.03f, 0.52f), new Vector2(0.32f, 0.95f),
                () => _onCrisis?.Invoke(CrisisChoice.LoadShedIndustry), UiFactory.Hex("5C4A3E"), paper, 12);
            UiFactory.Button(shed.transform, "ShedSub", "Shed Suburbs", new Vector2(0.34f, 0.52f), new Vector2(0.66f, 0.95f),
                () => _onCrisis?.Invoke(CrisisChoice.LoadShedSuburbs), UiFactory.Hex("6B3030"), paper, 12);
            UiFactory.Button(shed.transform, "ShedTr", "Shed Transit", new Vector2(0.68f, 0.52f), new Vector2(0.97f, 0.95f),
                () => _onCrisis?.Invoke(CrisisChoice.LoadShedTransit), UiFactory.Hex("4A3E5C"), paper, 12);
            UiFactory.Button(shed.transform, "ShedFos", "Emergency Fossil", new Vector2(0.2f, 0.08f), new Vector2(0.5f, 0.45f),
                () => _onCrisis?.Invoke(CrisisChoice.EmergencyFossil), UiFactory.Hex("6B4E30"), paper, 12);
            UiFactory.Button(shed.transform, "ShedAbs", "Absorb Hit", new Vector2(0.52f, 0.08f), new Vector2(0.8f, 0.45f),
                () => _onCrisis?.Invoke(CrisisChoice.Absorb), UiFactory.Hex("3A2E22"), paper, 12);
            _crisisShedActions.SetActive(false);
            _eventModal.SetActive(false);

            var end = UiFactory.Panel(canvasGo.transform, "End", new Vector2(0.3f, 0.38f), new Vector2(0.7f, 0.62f), UiFactory.Hex("4A1010"));
            _endBanner = end.gameObject;
            _endText = UiFactory.Label(end.transform, "EndT", paper, 24, FontStyle.Bold,
                new Vector2(0.05f, 0.45f), new Vector2(0.95f, 0.9f), TextAnchor.MiddleCenter);
            UiFactory.Button(end.transform, "EndMenu", "Return to Main Menu",
                new Vector2(0.2f, 0.1f), new Vector2(0.8f, 0.35f),
                () => _onResign?.Invoke(), UiFactory.Hex("3A2E22"), paper, 16);
            _endBanner.SetActive(false);

            var help = UiFactory.Panel(canvasGo.transform, "HelpBrief", new Vector2(0.22f, 0.18f), new Vector2(0.78f, 0.82f), UiFactory.Hex("1E1812"));
            _helpOverlay = help.gameObject;
            UiFactory.Label(help.transform, "HT", accent, 22, FontStyle.Bold,
                new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.96f), TextAnchor.MiddleCenter).text = "MINISTER'S BRIEF";
            UiFactory.Label(help.transform, "HB", paper, 13, FontStyle.Normal,
                new Vector2(0.06f, 0.2f), new Vector2(0.94f, 0.84f), TextAnchor.UpperLeft).text =
                "Wireframe desk shell — map first, exclusive top-left tabs.\n\n" +
                "· Top-left: treasury / conf / margin.\n" +
                "· Under vitals: Construction / Deals / Cabinet / Subsidies chips (one at a time).\n" +
                "· Top-right: 15/Jan/2026 · 14:00 and − / speed / + (1×…5×).\n" +
                "· Right: ESC system menu · Orders summary (builds + deadlines).\n" +
                "· Bottom-left: square chart chips open report drawers.\n" +
                "· Right 24h chip: opens daily duck / load-vs-supply drawer.\n" +
                "· Bottom-right: map lenses placeholder (later).\n" +
                "· ESC / ESC button: pause — Save / Load / Settings / Resign.\n" +
                "· F5: Quick Save · map: LMB select · RMB pan · scroll zoom.";
            UiFactory.Button(help.transform, "HelpOk", "Got it — open the desk",
                new Vector2(0.25f, 0.05f), new Vector2(0.75f, 0.16f),
                DismissHelp, UiFactory.Hex("8B6914"), paper, 16);
            _helpOverlay.SetActive(false);
        }

        private void AddBuildFilterBtn(Transform parent, string label, BuildFilter filter, ref float x)
        {
            float w = 0.24f;
            UiFactory.Button(parent, "BF_" + label, label,
                new Vector2(x, 0.1f), new Vector2(x + w - 0.02f, 0.9f),
                () =>
                {
                    _buildFilter = filter;
                    Render();
                }, UiFactory.Hex("3A2E22"), UiFactory.Hex("E7DCC8"), 11);
            x += w;
        }

        private void TabChip(Transform parent, string label, MenuId id, ref float x, float w, float y0, float y1, string tip)
        {
            float pad = 0.004f;
            UiFactory.Button(parent, "M_" + label, label,
                new Vector2(x + pad, y0 + 0.006f), new Vector2(x + w - pad, y1 - 0.006f),
                () => ToggleMenu(id), UiFactory.Hex("3A2E22"), UiFactory.Hex("E7DCC8"), 10, tip);
            x += w;
        }

        private void ChartBtn(Transform parent, string label, MenuId id, ref float x, float side, float y0, float y1, string tip)
        {
            // Square icon-chip → exclusive chart drawer (not left-rail takeover).
            UiFactory.Button(parent, "Chart_" + label, label,
                new Vector2(x, y0), new Vector2(x + side - 0.004f, y1),
                () => ToggleChartDrawer(id), UiFactory.Hex("3A2E22"), UiFactory.Hex("E7DCC8"), 8, tip);
            x += side;
        }
    }
}
