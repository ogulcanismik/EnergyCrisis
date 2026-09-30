using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using MinistryOfPower.Runtime;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.UI
{
    /// <summary>
    /// Grey-box Paradox shell (UI brief B): top metrics + one speed cluster,
    /// left investment/policy drawer, right cabinet/lobby drawer, bottom 24h dispatch.
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
            Cabinet,
            Pause
        }

        private Text _dateTimeText;
        private Text _tickText;
        private Text _treasuryText;
        private Text _confidenceText;
        private Text _marginText;
        private Text _speedText;
        private Text _panelTitle;
        private Text _panelBody;
        private Text _logText;
        private Text _lobbyText;
        private Text _cabinetSummaryText;
        private Text _seasonBannerText;
        private Text _tipText;
        private GameObject _tipBanner;
        private float _tipTimer;
        private GameObject _leftPanel;
        private GameObject _rightDrawer;
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
        private GameObject _buildActions;
        private GameObject _buildFilterBar;
        private GameObject _dealActions;
        private GameObject _policyActions;
        private GameObject _cabinetActions;
        private GameObject _pauseActions;
        private GameObject _crisisStdActions;
        private GameObject _crisisShedActions;
        private readonly Button[] _queueCancelBtns = new Button[5];
        private readonly Text[] _queueCancelLabels = new Text[5];
        private readonly string[] _queueCancelIds = new string[5];
        private readonly Dictionary<string, GameObject> _buildOrderBtns = new Dictionary<string, GameObject>(8);
        private MenuId _openMenu = MenuId.None;
        private BuildFilter _buildFilter = BuildFilter.All;
        private Season? _lastSeason;
        private float _seasonBannerTimer;
        private RegionId? _openRegion;
        private readonly StringBuilder _sb = new StringBuilder(2048);
        private readonly List<string> _log = new List<string>(48);
        private int _tooltipBudgetKey = int.MinValue;
        private int _tooltipMarginKey = int.MinValue;
        private int _tooltipCatalogKey = int.MinValue;
        private int _panelDirtyDay = int.MinValue;
        private MenuId _panelDirtyMenu = (MenuId)(-1);
        private bool _staticTipsAttached;

        private Action _onPause, _onSlow, _onNormal, _onFast, _onVeryFast, _onStep;
        private Action<string> _onBuild;
        private Action<string> _onCancelBuild;
        private Action<string> _onRetire;
        private Action<CrisisChoice> _onCrisis;
        private Action<CrisisChoice> _onCabinet;
        private Action _onEmergencyImport;
        private Action _onPrivateReserve;
        private Action<int> _onSaveSlot;
        private Action<int> _onLoadSlot;
        private Action _onResign;
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
            Action onPause, Action onSlow, Action onNormal, Action onFast, Action onVeryFast, Action onStep,
            Action<string> onBuild, Action<string> onCancelBuild, Action<string> onRetire,
            Action<CrisisChoice> onCrisis, Action<CrisisChoice> onCabinet,
            Action onEmergencyImport, Action onPrivateReserve,
            Action<int> onSaveSlot, Action<int> onLoadSlot, Action onResign)
        {
            _session = session;
            _onPause = onPause;
            _onSlow = onSlow;
            _onNormal = onNormal;
            _onFast = onFast;
            _onVeryFast = onVeryFast;
            _onStep = onStep;
            _onBuild = onBuild;
            _onCancelBuild = onCancelBuild;
            _onRetire = onRetire;
            _onCrisis = onCrisis;
            _onCabinet = onCabinet;
            _onEmergencyImport = onEmergencyImport;
            _onPrivateReserve = onPrivateReserve;
            _onSaveSlot = onSaveSlot;
            _onLoadSlot = onLoadSlot;
            _onResign = onResign;
            EnsureUi();
            MaybeShowFirstRunHelp();
        }

        public void PushLog(string line)
        {
            _log.Add(line);
            if (_log.Count > 40) _log.RemoveAt(0);
        }

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

            float hour = s.Clock.DayFraction * 24f;
            bool peak = (hour >= 7f && hour <= 10f) || (hour >= 17f && hour <= 21f);
            _dateTimeText.text = $"{s.Clock.FormatDate()}  {s.Clock.FormatTimeOfDay()}  ·  {s.Clock.CurrentSeason}";
            _tickText.text = peak ? "PEAK" : "OFF-PEAK";
            _tickText.color = peak ? UiFactory.Hex("C45A5A") : UiFactory.Hex("6BA36A");
            _speedText.text = s.IsGameOver ? "SACKED" : s.IsVictory ? "VICTORY" : MapSpeedLabel(s.Clock.Speed);
            _strip?.Render(s);
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
            _yearModal.SetActive(true);
            _onPause?.Invoke();
        }

        public void HideYearReport()
        {
            if (_yearModal != null) _yearModal.SetActive(false);
            _onNormal?.Invoke();
        }

        public void ForceOpenMenu(MenuId id)
        {
            EnsureUi();
            _openMenu = id;
            Render();
        }

        public string DebugPanelTitle => _panelTitle != null ? _panelTitle.text : "";
        public string DebugPanelBody => _panelBody != null ? _panelBody.text : "";
        public MenuId DebugOpenMenu => _openMenu;

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
            float hour = s.Clock.DayFraction * 24f;
            bool peak = (hour >= 7f && hour <= 10f) || (hour >= 17f && hour <= 21f);
            _dateTimeText.text = $"{s.Clock.FormatDate()}  {s.Clock.FormatTimeOfDay()}  ·  {s.Clock.CurrentSeason}";
            _tickText.text = peak ? "PEAK" : "OFF-PEAK";
            _tickText.color = peak ? UiFactory.Hex("C45A5A") : UiFactory.Hex("6BA36A");

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

            _speedText.text = s.IsGameOver ? "SACKED" : s.IsVictory ? "VICTORY" : MapSpeedLabel(s.Clock.Speed);

            MaybeAnnounceSeason(s);
            RefreshBuildCatalogTooltips(s);

            _sb.Length = 0;
            _sb.AppendLine("DESK LOG");
            int start = Math.Max(0, _log.Count - 8);
            for (int i = start; i < _log.Count; i++) _sb.AppendLine("· " + _log[i]);
            _logText.text = _sb.ToString();

            RefreshLobbyDrawer(s);
            _strip?.Render(s);

            int day = s.Clock.AbsoluteDay;
            if (_openMenu != _panelDirtyMenu || day != _panelDirtyDay)
            {
                _panelDirtyMenu = _openMenu;
                _panelDirtyDay = day;
                RefreshSidePanel(s);
            }
            else if (_openMenu != MenuId.None)
            {
                // Still refresh open construction queue buttons cheaply.
                if (_openMenu == MenuId.Construction) RefreshQueueCancelButtons(s);
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

        private static string MapSpeedLabel(GameSpeed speed)
        {
            switch (speed)
            {
                case GameSpeed.Paused: return "PAUSE";
                case GameSpeed.Slow: return "1x";
                case GameSpeed.Normal: return "1x";
                case GameSpeed.Fast: return "2x";
                case GameSpeed.VeryFast: return "5x";
                default: return speed.ToString();
            }
        }

        private void RefreshLobbyDrawer(GameSession s)
        {
            if (_lobbyText == null || s?.Cabinet == null) return;
            CabinetState c = s.Cabinet;
            float lobby = s.FossilLobby01 * 100f;
            _lobbyText.text =
                "LOBBY PRESSURE\n" +
                "Fossil lobby   " + Bar(lobby) + "  " + DisplayUnits.LobbyPts(lobby) + "\n" +
                "Climate / green " + Bar(c.ClimateMandatePressure) + "  " + DisplayUnits.LobbyPts(c.ClimateMandatePressure) + "\n" +
                "Industry       " + Bar(c.IndustryPressure) + "  " + DisplayUnits.LobbyPts(c.IndustryPressure) + "\n" +
                "Bills mandate  " + Bar(c.AffordabilityMandate) + "  " + DisplayUnits.LobbyPts(c.AffordabilityMandate) + "\n\n" +
                "Fossil lobby raises retire cost & bruises clean builds.\n" +
                "Climate pressure rises when transition stalls.\n" +
                "Industry wants firm MW and cheap fuel.\n" +
                "Lobby meters are points (0–100), not money.";

            if (_cabinetSummaryText != null)
            {
                _cabinetSummaryText.text =
                    "CABINET\nPM confidence " + DisplayUnits.Points(c.PmConfidence, "0.0") + "\n" +
                    "Mandate: " + c.ActiveMandate + "\n" +
                    "Tariff freeze: " + (c.TariffFreezeActive ? c.TariffFreezeDaysRemaining + "d" : "off") + "\n" +
                    "Emerg. fossil: " + (c.EmergencyFossilActive ? c.EmergencyFossilDaysRemaining + "d" : "off");
            }
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
            bool leftOpen = _openMenu != MenuId.None && _openMenu != MenuId.Cabinet;
            _leftPanel.SetActive(leftOpen);

            if (_buildActions != null) _buildActions.SetActive(_openMenu == MenuId.Construction);
            if (_buildFilterBar != null) _buildFilterBar.SetActive(_openMenu == MenuId.Construction);
            if (_dealActions != null) _dealActions.SetActive(_openMenu == MenuId.Deals);
            if (_policyActions != null) _policyActions.SetActive(_openMenu == MenuId.Policy);
            if (_cabinetActions != null) _cabinetActions.SetActive(true); // always on right drawer
            if (_pauseActions != null) _pauseActions.SetActive(_openMenu == MenuId.Pause);

            // Content texts: left panel for most menus; Cabinet uses same fields for smoke.
            switch (_openMenu)
            {
                case MenuId.Construction:
                    _panelTitle.text = "CONSTRUCTION · " + _buildFilter.ToString().ToUpperInvariant();
                    _panelBody.text = BuildConstructionText(s, _buildFilter);
                    break;
                case MenuId.EnergyMix:
                    _panelTitle.text = "ENERGY MIX";
                    _panelBody.text = BuildMixText(s);
                    break;
                case MenuId.Resources:
                    _panelTitle.text = "RESOURCES";
                    _panelBody.text = BuildResourcesText(s);
                    break;
                case MenuId.Deals:
                    _panelTitle.text = "INVESTMENT / DEALS";
                    _panelBody.text = BuildDealsText(s);
                    break;
                case MenuId.Policy:
                    _panelTitle.text = "ENERGY POLICY";
                    _panelBody.text = BuildPolicyText(s);
                    break;
                case MenuId.Mandate:
                    _panelTitle.text = "MANDATE / VICTORY";
                    _panelBody.text = s.Mandate != null ? s.Mandate.FormatPanel(s) : "—";
                    break;
                case MenuId.Budget:
                    _panelTitle.text = "BUDGET LEDGER";
                    _panelBody.text = s.Ledger != null ? s.Ledger.FormatPanel(s.Budget) : "—";
                    break;
                case MenuId.History:
                    _panelTitle.text = "EVENT HISTORY";
                    _panelBody.text = s.History != null ? s.History.FormatPanel() : "—";
                    break;
                case MenuId.Cabinet:
                    _panelTitle.text = "CABINET";
                    _panelBody.text = BuildCabinetText(s);
                    break;
                case MenuId.Pause:
                    _panelTitle.text = "PAUSE / SAVE";
                    _panelBody.text =
                        "Slot 1 = Quicksave (F5) / autosave every 30 days.\n" +
                        "Resign returns to main menu.\n\n" +
                        (s.Difficulty != null ? s.Difficulty.FormatSummary(s.Scenario.StartingBudget) : "");
                    break;
                default:
                    _panelTitle.text = "";
                    _panelBody.text = "";
                    break;
            }
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
            return "PM Confidence " + DisplayUnits.Points(c.PmConfidence, "0.0") + "\n" +
                   "Climate pressure " + DisplayUnits.Points(c.ClimateMandatePressure, "0.0") + "\n" +
                   "Industry pressure " + DisplayUnits.Points(c.IndustryPressure, "0.0") + "\n" +
                   "Affordability mandate " + DisplayUnits.Points(c.AffordabilityMandate, "0.0") + "\n\n" +
                   "Mandate: " + c.ActiveMandate + "\n" +
                   "Tariff freeze: " + (c.TariffFreezeActive ? c.TariffFreezeDaysRemaining + "d" : "off") + "\n" +
                   "Emergency fossil: " + (c.EmergencyFossilActive ? c.EmergencyFossilDaysRemaining + "d" : "off") + "\n\n" +
                   "Fill Reserve: stock fuels + adequacy.\n" +
                   "RE Subsidy: transition + bills; lobby bruise.\n" +
                   "Don't confuse motion with governance.";
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
            _openMenu = _openMenu == id ? MenuId.None : id;
            _panelDirtyMenu = (MenuId)(-1); // force side-panel rebuild
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
            switch (GameSettings.DefaultSpeed)
            {
                case GameSpeed.Slow: _onSlow?.Invoke(); break;
                case GameSpeed.Fast: _onFast?.Invoke(); break;
                case GameSpeed.VeryFast: _onVeryFast?.Invoke(); break;
                case GameSpeed.Paused: break;
                default: _onNormal?.Invoke(); break;
            }
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

            // ——— TOP BAR: date/tick, metrics, one speed cluster ———
            UiFactory.Panel(canvasGo.transform, "TopBar", new Vector2(0f, 0.905f), new Vector2(1f, 1f), bar);
            UiFactory.Label(canvasGo.transform, "Brand", accent, 16, FontStyle.Bold,
                new Vector2(0.008f, 0.955f), new Vector2(0.16f, 0.995f)).text = "MINISTRY OF POWER";

            _dateTimeText = UiFactory.Label(canvasGo.transform, "DateTime", paper, 14, FontStyle.Bold,
                new Vector2(0.16f, 0.955f), new Vector2(0.40f, 0.995f));
            _tickText = UiFactory.Label(canvasGo.transform, "Tick", accent, 12, FontStyle.Bold,
                new Vector2(0.40f, 0.955f), new Vector2(0.47f, 0.995f), TextAnchor.MiddleCenter);
            _treasuryText = UiFactory.Label(canvasGo.transform, "Treasury", paper, 13, FontStyle.Bold,
                new Vector2(0.16f, 0.91f), new Vector2(0.30f, 0.955f));
            _confidenceText = UiFactory.Label(canvasGo.transform, "Conf", paper, 13, FontStyle.Bold,
                new Vector2(0.30f, 0.91f), new Vector2(0.40f, 0.955f));
            _marginText = UiFactory.Label(canvasGo.transform, "Margin", paper, 13, FontStyle.Bold,
                new Vector2(0.40f, 0.91f), new Vector2(0.56f, 0.955f));

            // Speed: Pause / 1x / 2x / 5x / +Day  (top-right only)
            UiFactory.Panel(canvasGo.transform, "TimeBar", new Vector2(0.56f, 0.905f), new Vector2(1f, 1f), UiFactory.Hex("2B2118"));
            _speedText = UiFactory.Label(canvasGo.transform, "Spd", paper, 13, FontStyle.Bold,
                new Vector2(0.57f, 0.955f), new Vector2(0.66f, 0.995f), TextAnchor.MiddleCenter);
            UiFactory.Button(canvasGo.transform, "P", "❚❚", new Vector2(0.66f, 0.915f), new Vector2(0.72f, 0.99f),
                () => _onPause?.Invoke(), accent, ink, 12, "Pause");
            UiFactory.Button(canvasGo.transform, "S1", "1x", new Vector2(0.72f, 0.915f), new Vector2(0.78f, 0.99f),
                () => _onNormal?.Invoke(), accent, ink, 12, "1× · ~2.8 min/day");
            UiFactory.Button(canvasGo.transform, "S2", "2x", new Vector2(0.78f, 0.915f), new Vector2(0.84f, 0.99f),
                () => _onFast?.Invoke(), accent, ink, 12, "2× · ~84s/day");
            UiFactory.Button(canvasGo.transform, "S5", "5x", new Vector2(0.84f, 0.915f), new Vector2(0.90f, 0.99f),
                () => _onVeryFast?.Invoke(), accent, ink, 12, "5× · ~34s/day");
            UiFactory.Button(canvasGo.transform, "D1", "Skip", new Vector2(0.90f, 0.915f), new Vector2(0.99f, 0.99f),
                () => _onStep?.Invoke(), UiFactory.Hex("8B6914"), paper, 12, "Skip +1 day (not a speed)");

            var seasonImg = UiFactory.Panel(canvasGo.transform, "SeasonBanner", new Vector2(0.28f, 0.84f), new Vector2(0.72f, 0.90f), UiFactory.Hex("2B2118"));
            _seasonBanner = seasonImg.gameObject;
            _seasonBannerText = UiFactory.Label(seasonImg.transform, "ST", accent, 15, FontStyle.Bold,
                new Vector2(0.04f, 0.1f), new Vector2(0.96f, 0.9f), TextAnchor.MiddleCenter);
            _seasonBanner.SetActive(false);

            var tipImg = UiFactory.Panel(canvasGo.transform, "TipBanner", new Vector2(0.22f, 0.76f), new Vector2(0.78f, 0.835f), UiFactory.Hex("3A2E22"));
            _tipBanner = tipImg.gameObject;
            _tipText = UiFactory.Label(tipImg.transform, "TipT", paper, 13, FontStyle.Italic,
                new Vector2(0.03f, 0.1f), new Vector2(0.97f, 0.9f), TextAnchor.MiddleCenter);
            _tipBanner.SetActive(false);

            // ——— LEFT RAIL: construction / policy / investment ———
            UiFactory.Panel(canvasGo.transform, "LeftRail", new Vector2(0f, 0.18f), new Vector2(0.11f, 0.905f), bar);
            float ly = 0.88f;
            MenuBtn(canvasGo.transform, "Construction", MenuId.Construction, ref ly, "Queue, catalog, projections");
            MenuBtn(canvasGo.transform, "Policy", MenuId.Policy, ref ly, "Subsidies, freezes, reserves");
            MenuBtn(canvasGo.transform, "Investment", MenuId.Deals, ref ly, "Imports & private reserve");
            MenuBtn(canvasGo.transform, "Energy Mix", MenuId.EnergyMix, ref ly, "Live plant portfolio");
            MenuBtn(canvasGo.transform, "Resources", MenuId.Resources, ref ly, "Fuel stocks + market");
            MenuBtn(canvasGo.transform, "Mandate", MenuId.Mandate, ref ly, "Victory tracker");
            MenuBtn(canvasGo.transform, "Budget", MenuId.Budget, ref ly, "Quarter & YTD ledger");
            MenuBtn(canvasGo.transform, "History", MenuId.History, ref ly, "Past event cards");
            MenuBtn(canvasGo.transform, "Pause", MenuId.Pause, ref ly, "Save / load / F5");

            // Left content panel
            var side = UiFactory.Panel(canvasGo.transform, "SidePanel", new Vector2(0.11f, 0.18f), new Vector2(0.40f, 0.905f), panel);
            _leftPanel = side.gameObject;
            _panelTitle = UiFactory.Label(side.transform, "PTitle", accent, 18, FontStyle.Bold,
                new Vector2(0.04f, 0.9f), new Vector2(0.88f, 0.98f));
            _panelBody = UiFactory.Label(side.transform, "PBody", paper, 11, FontStyle.Normal,
                new Vector2(0.04f, 0.52f), new Vector2(0.96f, 0.9f), TextAnchor.UpperLeft);

            var filterBar = UiFactory.Panel(side.transform, "BuildFilters", new Vector2(0.02f, 0.455f), new Vector2(0.98f, 0.515f), new Color(0, 0, 0, 0.2f));
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

            var pauseHost = UiFactory.Panel(side.transform, "PauseActions", new Vector2(0f, 0f), new Vector2(1f, 0.28f), new Color(0, 0, 0, 0));
            _pauseActions = pauseHost.gameObject;
            for (int i = 0; i < 5; i++)
            {
                int slot = i;
                float x0 = 0.04f + i * 0.19f;
                string saveLabel = i == 0 ? "QS/Auto" : "Save " + (i + 1);
                string loadLabel = i == 0 ? "Load QS" : "Load " + (i + 1);
                UiFactory.Button(pauseHost.transform, "Save" + i, saveLabel,
                    new Vector2(x0, 0.55f), new Vector2(x0 + 0.17f, 0.95f),
                    () => _onSaveSlot?.Invoke(slot), UiFactory.Hex("3E5C3A"), paper, 10,
                    i == 0 ? "Quicksave / autosave slot" : "Save slot " + (i + 1));
                UiFactory.Button(pauseHost.transform, "Load" + i, loadLabel,
                    new Vector2(x0, 0.28f), new Vector2(x0 + 0.17f, 0.52f),
                    () => _onLoadSlot?.Invoke(slot), UiFactory.Hex("30506B"), paper, 10);
            }

            UiFactory.Button(pauseHost.transform, "Resign", "Resign to Menu",
                new Vector2(0.2f, 0.02f), new Vector2(0.8f, 0.24f),
                () => RequestConfirm("RESIGN?",
                    "Leave the desk and return to the main menu.\nUnsaved progress since last save is lost.",
                    () => _onResign?.Invoke()),
                UiFactory.Hex("6B3030"), paper, 12);

            _buildActions.SetActive(false);
            _dealActions.SetActive(false);
            _policyActions.SetActive(false);
            _pauseActions.SetActive(false);
            _leftPanel.SetActive(false);

            // ——— RIGHT DRAWER: Cabinet + Lobby ———
            var right = UiFactory.Panel(canvasGo.transform, "RightDrawer", new Vector2(0.78f, 0.18f), new Vector2(1f, 0.905f), panel);
            _rightDrawer = right.gameObject;
            UiFactory.Button(right.transform, "CabOpen", "Cabinet detail", new Vector2(0.08f, 0.94f), new Vector2(0.92f, 0.99f),
                () => ToggleMenu(MenuId.Cabinet), UiFactory.Hex("3A2E22"), accent, 11, "Open cabinet briefing");
            _lobbyText = UiFactory.Label(right.transform, "Lobby", paper, 11, FontStyle.Normal,
                new Vector2(0.05f, 0.52f), new Vector2(0.95f, 0.93f), TextAnchor.UpperLeft);
            _cabinetSummaryText = UiFactory.Label(right.transform, "CabSum", accent, 12, FontStyle.Bold,
                new Vector2(0.05f, 0.30f), new Vector2(0.95f, 0.51f), TextAnchor.UpperLeft);

            var cabHost = UiFactory.Panel(right.transform, "CabinetActions", new Vector2(0f, 0f), new Vector2(1f, 0.28f), new Color(0, 0, 0, 0));
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

            // Compact log under right drawer? Keep thin log strip above bottom.
            UiFactory.Panel(canvasGo.transform, "LogPanel", new Vector2(0.42f, 0.18f), new Vector2(0.78f, 0.30f), UiFactory.Hex("1A1510"));
            _logText = UiFactory.Label(canvasGo.transform, "Log", UiFactory.Hex("D2C3A8"), 11, FontStyle.Normal,
                new Vector2(0.425f, 0.185f), new Vector2(0.775f, 0.295f), TextAnchor.UpperLeft);

            // ——— BOTTOM: 24h curve + emergency strip ———
            UiFactory.Panel(canvasGo.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0.18f), bar);
            _strip = SupplyDemandStrip.Create(canvasGo.transform, new Vector2(0.01f, 0.01f), new Vector2(0.72f, 0.17f));

            UiFactory.Label(canvasGo.transform, "EmergLabel", accent, 11, FontStyle.Bold,
                new Vector2(0.73f, 0.14f), new Vector2(0.99f, 0.175f), TextAnchor.MiddleCenter).text = "EMERGENCY";
            UiFactory.Button(canvasGo.transform, "EmShed", "Load Shed*", new Vector2(0.73f, 0.095f), new Vector2(0.99f, 0.135f),
                () => ShowTip("Load-shed choices appear on crisis cards when adequacy threatens. Use crisis modal options."),
                UiFactory.Hex("5C4A3E"), paper, 11, "Load-shed is offered on crisis cards when available.");
            UiFactory.Button(canvasGo.transform, "EmImp", "E-Import", new Vector2(0.73f, 0.05f), new Vector2(0.99f, 0.09f),
                () => _onEmergencyImport?.Invoke(), UiFactory.Hex("30506B"), paper, 11, "Buy emergency import buffer.");
            UiFactory.Button(canvasGo.transform, "EmRes", "Reserve", new Vector2(0.73f, 0.005f), new Vector2(0.99f, 0.045f),
                () => _onPrivateReserve?.Invoke(), UiFactory.Hex("3E5C3A"), paper, 11, "Sign private reserve MW.");

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

            var year = UiFactory.Panel(canvasGo.transform, "YearModal", new Vector2(0.26f, 0.26f), new Vector2(0.74f, 0.74f), UiFactory.Hex("1E1812"));
            _yearModal = year.gameObject;
            _yearTitle = UiFactory.Label(year.transform, "YT", accent, 22, FontStyle.Bold,
                new Vector2(0.06f, 0.78f), new Vector2(0.94f, 0.95f), TextAnchor.MiddleCenter);
            _yearBody = UiFactory.Label(year.transform, "YB", paper, 14, FontStyle.Normal,
                new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.76f), TextAnchor.UpperLeft);
            UiFactory.Button(year.transform, "YOk", "Continue the mandate",
                new Vector2(0.25f, 0.06f), new Vector2(0.75f, 0.18f),
                HideYearReport, UiFactory.Hex("8B6914"), paper, 14);
            _yearModal.SetActive(false);

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
                "Paradox desk shell — one HUD, no stacked leftovers.\n\n" +
                "· Top: date/tick, treasury, confidence, grid margin, Pause/1x/2x/5x.\n" +
                "· Left: Construction / Policy / Investment.\n" +
                "· Right: Lobby pressure meters + Cabinet levers.\n" +
                "· Bottom: 24h load/supply curve + emergency import/reserve.\n" +
                "· Map center: USA grey-box regions, plants, transmission lines.\n" +
                "· Left-click regions/plants · hold right-mouse to pan · scroll to zoom.\n" +
                "· Crisis modals for adequacy trade-offs.";
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

        private void MenuBtn(Transform parent, string label, MenuId id, ref float y, string tip = null)
        {
            float h = 0.055f;
            UiFactory.Button(parent, "M_" + label, label,
                new Vector2(0.01f, y - h), new Vector2(0.105f, y),
                () => ToggleMenu(id), UiFactory.Hex("3A2E22"), UiFactory.Hex("E7DCC8"), 11, tip);
            y -= h + 0.01f;
        }
    }
}
