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
    /// Paradox-style edge chrome: top bar, bottom bar, side menus, time top-right, subpanels.
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
            Mandate,
            Budget,
            History,
            Cabinet,
            Pause
        }

        private Text _dateTimeText;
        private Text _speedText;
        private Text _dayNightText;
        private Text _metersText;
        private Text _briefText;
        private Text _panelTitle;
        private Text _panelBody;
        private Text _logText;
        private Text _weatherText;
        private Text _difficultyText;
        private Text _seasonBannerText;
        private Text _mandateText;
        private Text _tipText;
        private GameObject _tipBanner;
        private float _tipTimer;
        private GameObject _sidePanel;
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
        private SupplyDemandStrip _strip;
        private GameObject _buildActions;
        private GameObject _buildFilterBar;
        private GameObject _dealActions;
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
            _dateTimeText.text = $"{s.Clock.FormatDate()}  {s.Clock.FormatTimeOfDay()}  ·  {s.Clock.CurrentSeason}";
            _speedText.text = s.IsGameOver ? "SACKED" : s.IsVictory ? "VICTORY" : s.Clock.Speed.ToString();
            _dayNightText.text = s.Clock.IsNight ? "NIGHT" : "DAY";
            _weatherText.text = $"{s.CurrentWeather}  ·  Treasury {s.Budget:0.0}";

            _metersText.text =
                $"Adeq {m.Adequacy:0}   Aff {m.Affordability:0}   Trans {m.Transition:0}   Conf {m.Confidence:0}";
            UiFactory.AttachTooltip(_metersText.gameObject,
                $"Seat meters\nAdequacy {m.Adequacy:0.0} — keep lights on\n" +
                $"Affordability {m.Affordability:0.0} — bill pressure\n" +
                $"Transition {m.Transition:0.0} — clean share progress\n" +
                $"Confidence {m.Confidence:0.0} — sack clock");

            if (_difficultyText != null && s.Difficulty != null)
            {
                _difficultyText.text = s.Difficulty.DisplayName.ToUpperInvariant();
                _difficultyText.color = s.Difficulty.Id == DifficultyId.Hard
                    ? UiFactory.Hex("C45A5A")
                    : s.Difficulty.Id == DifficultyId.Easy
                        ? UiFactory.Hex("6BA36A")
                        : UiFactory.Hex("C4A35A");
                UiFactory.AttachTooltip(_difficultyText.gameObject, s.Difficulty.FormatSummary(s.Scenario.StartingBudget));
            }

            if (_mandateText != null && s.Mandate != null)
            {
                _mandateText.text = s.Mandate.FormatHud(s);
            }

            MaybeAnnounceSeason(s);

            string brief = string.IsNullOrEmpty(s.LastReport.Brief) ? s.Scenario.Description : s.LastReport.Brief;
            if (s.LastReport.DemandMw > 0f)
            {
                brief += $"\n{s.LastReport.SupplyMw:0}/{s.LastReport.DemandMw:0} MW";
            }

            if (s.PrivateReserveMw > 0f || s.EmergencyImportMw > 0f)
            {
                brief += $" · reserve {s.PrivateReserveMw:0} · e-import {s.EmergencyImportMw:0}";
            }

            _briefText.text = brief;

            _sb.Length = 0;
            _sb.AppendLine("DESK LOG");
            int start = Math.Max(0, _log.Count - 10);
            for (int i = start; i < _log.Count; i++) _sb.AppendLine("· " + _log[i]);
            _logText.text = _sb.ToString();

            _strip?.Render(s);
            RefreshSidePanel(s);
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
            if (_sidePanel == null) return;
            if (_openMenu == MenuId.None)
            {
                _sidePanel.SetActive(false);
                return;
            }

            _sidePanel.SetActive(true);
            if (_buildActions != null) _buildActions.SetActive(_openMenu == MenuId.Construction);
            if (_buildFilterBar != null) _buildFilterBar.SetActive(_openMenu == MenuId.Construction);
            if (_dealActions != null) _dealActions.SetActive(_openMenu == MenuId.Deals);
            if (_cabinetActions != null) _cabinetActions.SetActive(_openMenu == MenuId.Cabinet);
            if (_pauseActions != null) _pauseActions.SetActive(_openMenu == MenuId.Pause);

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
                    _panelTitle.text = "IMPORTS & DEALS";
                    _panelBody.text = BuildDealsText(s);
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
            }
        }

        private void RefreshQueueCancelButtons(GameSession s)
        {
            if (_queueCancelBtns[0] == null) return;
            bool show = _openMenu == MenuId.Construction && _sidePanel != null && _sidePanel.activeSelf;
            IReadOnlyList<BuildOrder> orders = s.Builds.Orders;
            int qi = 0;
            for (int i = 0; i < orders.Count && qi < _queueCancelBtns.Length; i++)
            {
                BuildOrder o = orders[i];
                if (o.IsCancelled || o.IsComplete) continue;
                _queueCancelIds[qi] = o.Id;
                _queueCancelBtns[qi].gameObject.SetActive(show);
                string drain = o.PaymentMode == BuildPaymentMode.PerQuarter && o.QuarterlyCost > 0f
                    ? $" · {o.QuarterlyCost:0}/q"
                    : "";
                bool stressed = s.Budget < o.QuarterlyCost * 2f;
                _queueCancelLabels[qi].text = $"X {o.DisplayName} · {o.QuartersRemaining}q{drain}" + (stressed ? " !" : "");
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
            _regionBody.text = RegionCatalog.BuildDetail(s, _openRegion.Value);
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
                $"Fuel {plant.Fuel}  ·  {RegionCatalog.DisplayName(plant.Region)}\n" +
                $"Capacity {plant.CapacityMw:0} MW  ·  avail {plant.Availability:0%}\n" +
                $"Upkeep {plant.QuarterlyUpkeep:0.0}/q (market ×{marketMul:0.00} → {plant.QuarterlyUpkeep * marketMul:0.0})\n" +
                $"Var cost {plant.VariableCostPerMwh:0.0}  ·  oil exp {plant.OilExposure:0%}\n" +
                $"Daily fuel use {plant.DailyFuelUse:0.00}\n\n" +
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
                    sb.Append("  drain ").Append(o.QuarterlyCost.ToString("0")).Append("/q");
                sb.Append('\n');
            }

            if (!any) sb.AppendLine("· Queue empty — order below.");
            float income = s.Scenario.QuarterlyIncome * (s.Difficulty?.IncomeMultiplier ?? 1f);
            if (qBurn > income * 0.85f || (qBurn > 0f && s.Budget < qBurn))
            {
                sb.Append("⚠ Treasury stress: queue drain ").Append(qBurn.ToString("0.0"))
                    .Append("/q vs income ").Append(income.ToString("0.0"))
                    .Append(" (cash ").Append(s.Budget.ToString("0.0")).Append(")\n");
            }

            sb.AppendLine().Append("CATALOG [").Append(filter).Append("] — cost / MW / upkeep / fuel / time\n");
            IReadOnlyList<BuildDefinitionConfig> cat = s.BuildCatalog;
            for (int i = 0; i < cat.Count; i++)
            {
                BuildDefinitionConfig b = cat[i];
                if (!MatchesFuelFilter(b.ResultFuel, filter)) continue;
                string fuel = ResourceStockpile.NeedsStock(b.ResultFuel) ? b.ResultFuel.ToString() : "none";
                sb.Append("· ").Append(b.DisplayName)
                    .Append("  $").Append(b.UpfrontCost.ToString("0"))
                    .Append("  ").Append(b.ResultCapacityMw.ToString("0")).Append("MW")
                    .Append("  upk ").Append(b.QuarterlyUpkeep.ToString("0.0"))
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
                    .Append(p.CapacityMw.ToString("0")).Append(" MW\n");
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
                .Append("\n\nImports ").Append(s.Modifiers.ImportMwAvailable.ToString("0"))
                .Append('/').Append(s.Modifiers.ImportMwBaseline.ToString("0")).Append(" MW")
                .Append("\n\nFuel market: ").Append(s.FuelMarket != null ? s.FuelMarket.FormatLine() : "—")
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
                "Ministerial procurement — not a politician sim.\n\n" +
                $"Interconnector baseline: {s.Modifiers.ImportMwAvailable:0}/{s.Modifiers.ImportMwBaseline:0} MW\n" +
                $"Emergency import buffer: {s.EmergencyImportMw:0} MW ({s.EmergencyImportDaysRemaining}d left)\n" +
                $"  → Buy +80 MW / 10d for ~{importCost:0} treasury.\n\n" +
                $"Private reserve: {s.PrivateReserveMw:0} MW (cap 200) · {s.PrivateReserveQuarterlyCost:0.0}/q\n" +
                $"  → Sign +60 MW for ~{reserveUpfront:0} upfront + 4.5/q\n\n" +
                "Extra firm MW feeds the day resolve.";
        }

        private static string BuildCabinetText(GameSession s)
        {
            CabinetState c = s.Cabinet;
            return $"PM Confidence {c.PmConfidence:0.0}\n" +
                   $"Climate pressure {c.ClimateMandatePressure:0.0}\n" +
                   $"Industry pressure {c.IndustryPressure:0.0}\n" +
                   $"Affordability mandate {c.AffordabilityMandate:0.0}\n\n" +
                   $"Mandate: {c.ActiveMandate}\n" +
                   $"Tariff freeze: {(c.TariffFreezeActive ? c.TariffFreezeDaysRemaining + "d" : "off")}\n" +
                   $"Emergency fossil: {(c.EmergencyFossilActive ? c.EmergencyFossilDaysRemaining + "d" : "off")}\n\n" +
                   "Buttons below. Don't confuse motion with governance.";
        }

        private void ToggleMenu(MenuId id)
        {
            _openMenu = _openMenu == id ? MenuId.None : id;
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

            UiFactory.Panel(canvasGo.transform, "TopBar", new Vector2(0f, 0.92f), new Vector2(1f, 1f), bar);
            UiFactory.Label(canvasGo.transform, "Brand", accent, 20, FontStyle.Bold,
                new Vector2(0.01f, 0.93f), new Vector2(0.18f, 0.99f)).text = "MINISTRY OF POWER";
            _difficultyText = UiFactory.Label(canvasGo.transform, "Diff", accent, 13, FontStyle.Bold,
                new Vector2(0.18f, 0.93f), new Vector2(0.24f, 0.99f), TextAnchor.MiddleCenter);
            _difficultyText.text = "NORMAL";
            _metersText = UiFactory.Label(canvasGo.transform, "Meters", paper, 14, FontStyle.Bold,
                new Vector2(0.24f, 0.955f), new Vector2(0.62f, 0.995f), TextAnchor.MiddleCenter);
            _mandateText = UiFactory.Label(canvasGo.transform, "MandateBar", UiFactory.Hex("D2C3A8"), 12, FontStyle.Normal,
                new Vector2(0.24f, 0.92f), new Vector2(0.62f, 0.955f), TextAnchor.MiddleCenter);
            _mandateText.text = "MANDATE";

            UiFactory.Panel(canvasGo.transform, "TimeBar", new Vector2(0.62f, 0.92f), new Vector2(1f, 1f), UiFactory.Hex("2B2118"));
            _dateTimeText = UiFactory.Label(canvasGo.transform, "DateTime", paper, 15, FontStyle.Bold,
                new Vector2(0.63f, 0.96f), new Vector2(0.88f, 0.995f));
            _dayNightText = UiFactory.Label(canvasGo.transform, "DN", accent, 14, FontStyle.Bold,
                new Vector2(0.88f, 0.96f), new Vector2(0.94f, 0.995f), TextAnchor.MiddleCenter);
            _speedText = UiFactory.Label(canvasGo.transform, "Spd", paper, 14, FontStyle.Bold,
                new Vector2(0.94f, 0.96f), new Vector2(0.995f, 0.995f), TextAnchor.MiddleCenter);
            _weatherText = UiFactory.Label(canvasGo.transform, "Weather", paper, 13, FontStyle.Normal,
                new Vector2(0.63f, 0.925f), new Vector2(0.995f, 0.96f));

            float sy = 0.865f;
            UiFactory.Button(canvasGo.transform, "P", "❚❚", new Vector2(0.70f, sy), new Vector2(0.74f, 0.91f), () => _onPause?.Invoke(), accent, ink, 12, "Pause");
            UiFactory.Button(canvasGo.transform, "S", "▶", new Vector2(0.74f, sy), new Vector2(0.78f, 0.91f), () => _onSlow?.Invoke(), accent, ink, 12, "Slow");
            UiFactory.Button(canvasGo.transform, "N", "▶▶", new Vector2(0.78f, sy), new Vector2(0.82f, 0.91f), () => _onNormal?.Invoke(), accent, ink, 12, "Normal");
            UiFactory.Button(canvasGo.transform, "F", "▶▶▶", new Vector2(0.82f, sy), new Vector2(0.86f, 0.91f), () => _onFast?.Invoke(), accent, ink, 12, "Fast");
            UiFactory.Button(canvasGo.transform, "VF", ">>>>", new Vector2(0.86f, sy), new Vector2(0.91f, 0.91f), () => _onVeryFast?.Invoke(), accent, ink, 12, "Very fast");
            UiFactory.Button(canvasGo.transform, "D1", "+Day", new Vector2(0.91f, sy), new Vector2(0.97f, 0.91f), () => _onStep?.Invoke(), UiFactory.Hex("8B6914"), paper, 12, "Advance one day");

            // Season / winter banner (center top under chrome)
            var seasonImg = UiFactory.Panel(canvasGo.transform, "SeasonBanner", new Vector2(0.28f, 0.86f), new Vector2(0.72f, 0.915f), UiFactory.Hex("2B2118"));
            _seasonBanner = seasonImg.gameObject;
            _seasonBannerText = UiFactory.Label(seasonImg.transform, "ST", accent, 15, FontStyle.Bold,
                new Vector2(0.04f, 0.1f), new Vector2(0.96f, 0.9f), TextAnchor.MiddleCenter);
            _seasonBanner.SetActive(false);

            var tipImg = UiFactory.Panel(canvasGo.transform, "TipBanner", new Vector2(0.2f, 0.78f), new Vector2(0.8f, 0.855f), UiFactory.Hex("3A2E22"));
            _tipBanner = tipImg.gameObject;
            _tipText = UiFactory.Label(tipImg.transform, "TipT", paper, 13, FontStyle.Italic,
                new Vector2(0.03f, 0.1f), new Vector2(0.97f, 0.9f), TextAnchor.MiddleCenter);
            _tipBanner.SetActive(false);

            UiFactory.Panel(canvasGo.transform, "LeftRail", new Vector2(0f, 0.08f), new Vector2(0.12f, 0.92f), bar);
            float ly = 0.86f;
            MenuBtn(canvasGo.transform, "Construction", MenuId.Construction, ref ly, "Queue, catalog, projections");
            MenuBtn(canvasGo.transform, "Energy Mix", MenuId.EnergyMix, ref ly, "Live plant portfolio");
            MenuBtn(canvasGo.transform, "Resources", MenuId.Resources, ref ly, "Fuel stocks + market prices");
            MenuBtn(canvasGo.transform, "Deals", MenuId.Deals, ref ly, "Imports & private reserve");
            MenuBtn(canvasGo.transform, "Mandate", MenuId.Mandate, ref ly, "Victory tracker / streaks");
            MenuBtn(canvasGo.transform, "Budget", MenuId.Budget, ref ly, "Quarter & YTD ledger");
            MenuBtn(canvasGo.transform, "History", MenuId.History, ref ly, "Past event cards");
            MenuBtn(canvasGo.transform, "Cabinet", MenuId.Cabinet, ref ly, "Proactive levers");
            MenuBtn(canvasGo.transform, "Pause", MenuId.Pause, ref ly, "Save / load / F5 quicksave");

            _strip = SupplyDemandStrip.Create(canvasGo.transform, new Vector2(0.42f, 0.085f), new Vector2(0.70f, 0.17f));

            UiFactory.Panel(canvasGo.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0.08f), bar);
            _briefText = UiFactory.Label(canvasGo.transform, "Brief", paper, 14, FontStyle.Italic,
                new Vector2(0.13f, 0.01f), new Vector2(0.68f, 0.07f), TextAnchor.MiddleLeft);
            UiFactory.Button(canvasGo.transform, "QS", "F5 QS", new Vector2(0.68f, 0.015f), new Vector2(0.74f, 0.065f),
                () => _onSaveSlot?.Invoke(0), UiFactory.Hex("3E5C3A"), paper, 11, "Quicksave to slot 1");

            UiFactory.Button(canvasGo.transform, "QB_Solar", "Solar", new Vector2(0.74f, 0.015f), new Vector2(0.79f, 0.065f),
                () => _onBuild?.Invoke("build_solar"), UiFactory.Hex("3E5C3A"), paper, 11, BuildTip("Solar", "Clean daytime MW."));
            UiFactory.Button(canvasGo.transform, "QB_Wind", "Wind", new Vector2(0.79f, 0.015f), new Vector2(0.84f, 0.065f),
                () => _onBuild?.Invoke("build_wind"), UiFactory.Hex("3E5C4A"), paper, 11, BuildTip("Wind", "Variable clean MW."));
            UiFactory.Button(canvasGo.transform, "QB_Gas", "Gas", new Vector2(0.84f, 0.015f), new Vector2(0.89f, 0.065f),
                () => _onBuild?.Invoke("build_gas"), UiFactory.Hex("5C4A3E"), paper, 11, BuildTip("Gas", "Firm fossil MW."));
            UiFactory.Button(canvasGo.transform, "QB_Nuke", "Nuke", new Vector2(0.89f, 0.015f), new Vector2(0.94f, 0.065f),
                () => _onBuild?.Invoke("build_nuclear"), UiFactory.Hex("3A4A5C"), paper, 11, BuildTip("Nuclear", "Long firm clean."));
            UiFactory.Button(canvasGo.transform, "QB_Retire", "Retire", new Vector2(0.94f, 0.015f), new Vector2(0.995f, 0.065f),
                () => RequestConfirm("RETIRE FOSSIL?",
                    "Retire the oldest fossil in the selected region (or any fossil).\nLobby confidence sting applies.",
                    () => _onRetire?.Invoke(null)),
                UiFactory.Hex("6B3030"), paper, 11, "Retire oldest fossil (confirms).");

            var side = UiFactory.Panel(canvasGo.transform, "SidePanel", new Vector2(0.12f, 0.18f), new Vector2(0.42f, 0.86f), UiFactory.Hex("241C14"));
            _sidePanel = side.gameObject;
            _panelTitle = UiFactory.Label(side.transform, "PTitle", accent, 20, FontStyle.Bold,
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

            // Queue cancel rows (above build catalog buttons)
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

            var buildHost = UiFactory.Panel(side.transform, "BuildActions", new Vector2(0f, 0f), new Vector2(1f, 0.14f), new Color(0, 0, 0, 0));
            _buildActions = buildHost.gameObject;
            string[] ids = { "build_coal", "build_gas", "build_oil", "build_solar", "build_wind", "build_hydro", "build_storage", "build_nuclear" };
            string[] labels = { "Coal", "Gas", "Oil", "Solar", "Wind", "Hydro", "Storage", "Nuke" };
            _buildOrderBtns.Clear();
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                float x0 = 0.04f + (i % 4) * 0.24f;
                float x1 = x0 + 0.22f;
                float row = i < 4 ? 0.95f : 0.48f;
                var ordBtn = UiFactory.Button(buildHost.transform, "Ord_" + id, labels[i],
                    new Vector2(x0, row - 0.4f), new Vector2(x1, row),
                    () => _onBuild?.Invoke(id), UiFactory.Hex("3A2E22"), paper, 11);
                _buildOrderBtns[id] = ordBtn.gameObject;
            }

            var dealHost = UiFactory.Panel(side.transform, "DealActions", new Vector2(0f, 0f), new Vector2(1f, 0.22f), new Color(0, 0, 0, 0));
            _dealActions = dealHost.gameObject;
            UiFactory.Button(dealHost.transform, "DealImport", "Buy Emergency Import", new Vector2(0.04f, 0.52f), new Vector2(0.96f, 0.95f),
                () => _onEmergencyImport?.Invoke(), UiFactory.Hex("30506B"), paper, 13);
            UiFactory.Button(dealHost.transform, "DealReserve", "Sign Private Reserve", new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.48f),
                () => _onPrivateReserve?.Invoke(), UiFactory.Hex("3E5C3A"), paper, 13);

            var cabHost = UiFactory.Panel(side.transform, "CabinetActions", new Vector2(0f, 0f), new Vector2(1f, 0.16f), new Color(0, 0, 0, 0));
            _cabinetActions = cabHost.gameObject;
            UiFactory.Button(cabHost.transform, "CabAbs", "Absorb N/A", new Vector2(0.04f, 0.1f), new Vector2(0.32f, 0.9f),
                () => _onCabinet?.Invoke(CrisisChoice.Absorb), UiFactory.Hex("6B3030"), paper, 11);
            UiFactory.Button(cabHost.transform, "CabFos", "Emerg. Fossil", new Vector2(0.34f, 0.1f), new Vector2(0.66f, 0.9f),
                () => _onCabinet?.Invoke(CrisisChoice.EmergencyFossil), UiFactory.Hex("6B4E30"), paper, 11);
            UiFactory.Button(cabHost.transform, "CabTar", "Tariff Freeze", new Vector2(0.68f, 0.1f), new Vector2(0.96f, 0.9f),
                () => _onCabinet?.Invoke(CrisisChoice.TariffFreeze), UiFactory.Hex("30506B"), paper, 11);

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
            _cabinetActions.SetActive(false);
            _pauseActions.SetActive(false);
            _sidePanel.SetActive(false);

            // Region detail panel (map click)
            var reg = UiFactory.Panel(canvasGo.transform, "RegionPanel", new Vector2(0.43f, 0.55f), new Vector2(0.70f, 0.85f), UiFactory.Hex("1A1510EE"));
            _regionPanel = reg.gameObject;
            _regionTitle = UiFactory.Label(reg.transform, "RT", accent, 18, FontStyle.Bold,
                new Vector2(0.05f, 0.82f), new Vector2(0.8f, 0.96f));
            _regionBody = UiFactory.Label(reg.transform, "RB", paper, 12, FontStyle.Normal,
                new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.8f), TextAnchor.UpperLeft);
            UiFactory.Button(reg.transform, "RClose", "X", new Vector2(0.86f, 0.82f), new Vector2(0.96f, 0.96f),
                () => { _openRegion = null; _regionPanel.SetActive(false); }, UiFactory.Hex("6B3030"), paper, 14);
            _regionPanel.SetActive(false);

            var plant = UiFactory.Panel(canvasGo.transform, "PlantPanel", new Vector2(0.43f, 0.28f), new Vector2(0.70f, 0.54f), UiFactory.Hex("1A1510EE"));
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

            UiFactory.Panel(canvasGo.transform, "LogPanel", new Vector2(0.72f, 0.18f), new Vector2(0.995f, 0.86f), UiFactory.Hex("1A1510"));
            _logText = UiFactory.Label(canvasGo.transform, "Log", UiFactory.Hex("D2C3A8"), 12, FontStyle.Normal,
                new Vector2(0.73f, 0.19f), new Vector2(0.99f, 0.85f), TextAnchor.UpperLeft);

            // Event modal
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
                () => _onCrisis?.Invoke(CrisisChoice.LoadShedIndustry), UiFactory.Hex("5C4A3E"), paper, 12,
                "Industry dark — adequacy up; lobby/economy bruise.");
            UiFactory.Button(shed.transform, "ShedSub", "Shed Suburbs", new Vector2(0.34f, 0.52f), new Vector2(0.66f, 0.95f),
                () => _onCrisis?.Invoke(CrisisChoice.LoadShedSuburbs), UiFactory.Hex("6B3030"), paper, 12,
                "Suburbs dark — adequacy up; confidence bleeds.");
            UiFactory.Button(shed.transform, "ShedTr", "Shed Transit", new Vector2(0.68f, 0.52f), new Vector2(0.97f, 0.95f),
                () => _onCrisis?.Invoke(CrisisChoice.LoadShedTransit), UiFactory.Hex("4A3E5C"), paper, 12,
                "Transit dark — adequacy up; economy + confidence hit.");
            UiFactory.Button(shed.transform, "ShedFos", "Emergency Fossil", new Vector2(0.2f, 0.08f), new Vector2(0.5f, 0.45f),
                () => _onCrisis?.Invoke(CrisisChoice.EmergencyFossil), UiFactory.Hex("6B4E30"), paper, 12,
                "Skip the dark — pay fossil instead.");
            UiFactory.Button(shed.transform, "ShedAbs", "Absorb Hit", new Vector2(0.52f, 0.08f), new Vector2(0.8f, 0.45f),
                () => _onCrisis?.Invoke(CrisisChoice.Absorb), UiFactory.Hex("3A2E22"), paper, 12,
                "Own the numbers. No load-shed.");
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
                "You run the ministry desk — not a politician campaign.\n\n" +
                "· Keep Adequacy & Affordability off the crash floor.\n" +
                "· Click map regions to site builds; click plant icons for detail / retire.\n" +
                "· Mandate bar + Mandate menu sparkline (clean% over quarters).\n" +
                "· Construction filters: Fossil / Clean / Storage.\n" +
                "· Budget / History menus; fuel market in Resources.\n" +
                "· Adequacy crises: load-shed who goes dark.\n" +
                "· F5 quicksave · Continue on main menu · tips once (Settings reset).";
            UiFactory.Button(help.transform, "HelpOk", "Got it — open the desk",
                new Vector2(0.25f, 0.05f), new Vector2(0.75f, 0.16f),
                DismissHelp, UiFactory.Hex("8B6914"), paper, 16);
            _helpOverlay.SetActive(false);
        }

        private static string BuildTip(string name, string note) => name + "\n" + note;

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
            float h = 0.048f;
            UiFactory.Button(parent, "M_" + label, label,
                new Vector2(0.01f, y - h), new Vector2(0.11f, y),
                () => ToggleMenu(id), UiFactory.Hex("3A2E22"), UiFactory.Hex("E7DCC8"), 11, tip);
            y -= h + 0.01f;
        }
    }
}
