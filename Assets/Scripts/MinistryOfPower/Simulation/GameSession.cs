using System;
using System.Collections.Generic;

namespace MinistryOfPower.Simulation
{
    public sealed class ScenarioConfig
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public int StartYear = 2026;
        public int Seed = 42;
        public int CampaignYears = MandateTracker.DefaultCampaignYears;
        public float StartingBudget = 100f;
        public float QuarterlyIncome = 12f;
        public float BaseDemandMw = 1000f;
        public float SolarResource = 1f;
        public float WindResource = 1f;
        public float FossilLobbyStrength = 0.5f;
        /// <summary>Extra multiplier on fossil-retire confidence hits.</summary>
        public float LobbyRetireMultiplier = 1f;
        public float ImportCapacityMw = 80f;
        public float StartingAdequacy = 72f;
        public float StartingAffordability = 68f;
        public float StartingTransition = 18f;
        public float StartingConfidence = 70f;
        public string DifferentiationBlurb = "";
        public List<PlantSpawnConfig> StartingPlants = new List<PlantSpawnConfig>();
        public List<EventWeightConfig> EventWeights = new List<EventWeightConfig>();
    }

    public sealed class EventWeightConfig
    {
        public PendingEventKind Kind;
        public float BaseWeight = 1f;
    }

    public sealed class PlantSpawnConfig
    {
        public string DefinitionId;
        public string DisplayName;
        public FuelKind Fuel;
        public float CapacityMw;
        public float Availability = 0.9f;
        public float VariableCostPerMwh = 40f;
        public float OilExposure;
        public float QuarterlyUpkeep;
        public float DailyFuelUse;
        public RegionId Region = RegionId.North;
        public bool RegionExplicit;
    }

    public sealed class BuildDefinitionConfig
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public FuelKind ResultFuel;
        public float ResultCapacityMw;
        public float ResultAvailability = 0.92f;
        public float ResultVariableCost;
        public float ResultOilExposure;
        public BuildPaymentMode PaymentMode = BuildPaymentMode.PerQuarter;
        public float UpfrontCost;
        public float QuarterlyCost;
        public int DurationQuarters = 4;
        public float FossilLobbyConfidencePenalty;
        public float QuarterlyUpkeep;
        public float DailyFuelUse;
    }

    public sealed class GameSession
    {
        public const float MeterCrashAdequacyThreshold = 45f;
        public const float MeterCrashAffordabilityThreshold = 40f;
        public const int MeterCrashCooldownDays = 16;
        public const int MeterCrashStreakRequired = 2;

        public event Action<DayReport> DayResolved;
        public event Action<PendingEvent> EventRaised;
        public event Action<string> LogEmitted;
        public event Action GameOver;
        public event Action<string> TipRaised;
        public event Action<YearReport> YearEnded;

        private readonly List<PlantInstance> _completedScratch = new List<PlantInstance>(4);
        private readonly List<BuildDefinitionConfig> _buildCatalog = new List<BuildDefinitionConfig>();

        public GameClock Clock { get; private set; }
        public PlantPortfolio Portfolio { get; private set; }
        public BuildQueue Builds { get; private set; }
        public SeatMeters Meters { get; private set; }
        public ScenarioConfig Scenario { get; private set; }
        public DifficultyConfig Difficulty { get; private set; }
        public DayModifiers Modifiers { get; private set; }
        public ResourceStockpile Resources { get; private set; }
        public CabinetState Cabinet { get; private set; }
        public FuelMarket FuelMarket { get; private set; }
        public MandateTracker Mandate { get; private set; }
        public BudgetLedger Ledger { get; private set; }
        public EventHistory History { get; private set; }
        public RegionId SelectedRegion { get; private set; } = RegionId.Coast;
        public float Budget { get; private set; }
        public float FuelPriceIndex { get; private set; } = 1f;
        public PendingEvent ActiveEvent { get; private set; }
        public DayReport LastReport { get; private set; }
        public bool IsGameOver { get; private set; }
        public bool IsVictory { get; private set; }
        public bool AwaitingCrisisDecision => ActiveEvent != null && ActiveEvent.AwaitingDecision;
        public IReadOnlyList<BuildDefinitionConfig> BuildCatalog => _buildCatalog;
        public float OilShockMultiplier => Modifiers != null ? Modifiers.OilShockMultiplier : 1f;
        public WeatherKind CurrentWeather { get; private set; } = WeatherKind.Clear;
        public float PrivateReserveMw { get; private set; }
        public float PrivateReserveQuarterlyCost { get; private set; }
        public float EmergencyImportMw { get; private set; }
        public int EmergencyImportDaysRemaining { get; private set; }
        public YearReport LastYearReport { get; private set; }
        /// <summary>24 buckets of relative demand (0..1+) for HUD strip.</summary>
        public float[] DayDemandCurve { get; private set; } = new float[24];
        /// <summary>24 buckets of relative supply (0..1+) for HUD strip.</summary>
        public float[] DaySupplyCurve { get; private set; } = new float[24];

        private int _daysSinceMajorEvent;
        private int _daysSinceMeterCrash;
        private int _meterCrisisStreak;
        private float _effectiveLobby;
        private float _quarterlyIncome;
        private int _lastYearForLedger;
        private float _yearAdeqSum;
        private float _yearAffSum;
        private int _yearSamples;
        private float _yearSpend;
        private float _yearBudgetAnchor;
        private string _yearBiggestEvent = "—";
        private float _yearBiggestSev;
        private int _yearEventCount;

        public void Start(ScenarioConfig scenario, IEnumerable<BuildDefinitionConfig> builds, DifficultyConfig difficulty = null)
        {
            Scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            Difficulty = difficulty ?? DifficultyConfig.Create(DifficultyId.Normal);
            Clock = new GameClock(scenario.StartYear);
            Portfolio = new PlantPortfolio();
            Builds = new BuildQueue();
            Modifiers = new DayModifiers();
            Modifiers.ConfigureBaselineImports(scenario.ImportCapacityMw);
            Resources = ResourceStockpile.CreateDefault();
            Cabinet = new CabinetState();
            FuelMarket = new FuelMarket();
            Mandate = new MandateTracker { CampaignYears = scenario.CampaignYears > 0 ? scenario.CampaignYears : MandateTracker.DefaultCampaignYears };
            Ledger = new BudgetLedger();
            History = new EventHistory();
            SelectedRegion = RegionId.Coast;

            _effectiveLobby = Clamp01(scenario.FossilLobbyStrength * Difficulty.LobbyPressureMultiplier);
            _quarterlyIncome = scenario.QuarterlyIncome * Difficulty.IncomeMultiplier;
            Budget = scenario.StartingBudget * Difficulty.BudgetMultiplier;
            FuelPriceIndex = 1f;
            ActiveEvent = null;
            IsGameOver = false;
            IsVictory = false;
            _daysSinceMajorEvent = 0;
            _daysSinceMeterCrash = MeterCrashCooldownDays;
            _meterCrisisStreak = 0;
            _lastYearForLedger = scenario.StartYear;
            CurrentWeather = WeatherKind.Clear;
            PrivateReserveMw = 0f;
            PrivateReserveQuarterlyCost = 0f;
            EmergencyImportMw = 0f;
            EmergencyImportDaysRemaining = 0;
            LastYearReport = null;
            ResetYearAccumulator();
            _yearBudgetAnchor = Budget;
            FillDayCurves(scenario.BaseDemandMw, scenario.BaseDemandMw * 0.95f);

            _buildCatalog.Clear();
            if (builds != null)
            {
                foreach (BuildDefinitionConfig b in builds)
                {
                    _buildCatalog.Add(b);
                }
            }

            for (int i = 0; i < scenario.StartingPlants.Count; i++)
            {
                PlantSpawnConfig spawn = scenario.StartingPlants[i];
                float upkeep = spawn.QuarterlyUpkeep;
                float fuel = spawn.DailyFuelUse;
                GuessPlantEconomics(spawn.Fuel, spawn.CapacityMw, ref upkeep, ref fuel);
                RegionId region = spawn.RegionExplicit ? spawn.Region : RegionCatalog.DefaultRegionForFuel(spawn.Fuel);
                Portfolio.Add(new PlantInstance(
                    Portfolio.NextPlantId(spawn.DefinitionId ?? "plant"),
                    spawn.DisplayName,
                    spawn.Fuel,
                    spawn.CapacityMw,
                    spawn.Availability,
                    spawn.VariableCostPerMwh,
                    spawn.OilExposure,
                    spawn.DefinitionId ?? spawn.DisplayName,
                    upkeep,
                    fuel,
                    region));
            }

            Meters = SeatMeters.Create(
                scenario.StartingAdequacy,
                scenario.StartingAffordability,
                scenario.StartingTransition,
                SeatMeters.Clamp(scenario.StartingConfidence + Difficulty.StartingConfidenceBonus));

            Cabinet.SyncFromMeters(Meters, _effectiveLobby);
            LastReport = default;
            EmitLog($"Seated: {scenario.DisplayName} · {Difficulty.DisplayName} · treasury {Budget:0}. Don't waste the quiet.");
        }

        public bool TryStartBuild(string buildDefinitionId, out string message)
        {
            BuildDefinitionConfig def = FindBuild(buildDefinitionId);
            if (def == null) { message = "Unknown build."; return false; }
            if (AwaitingCrisisDecision) { message = "Resolve the crisis on your desk first."; return false; }

            float dueNow = def.UpfrontCost;
            if (Budget < dueNow)
            {
                message = $"Need {dueNow:0} budget (have {Budget:0}).";
                return false;
            }

            Budget -= dueNow;
            NoteSpend(dueNow);
            RegionId site = SelectedRegion;
            Builds.Enqueue(new BuildOrder(
                Builds.NextOrderId(),
                def.DisplayName,
                def.Id,
                def.ResultFuel,
                def.ResultCapacityMw,
                def.ResultAvailability,
                def.ResultVariableCost,
                def.ResultOilExposure,
                def.PaymentMode,
                def.UpfrontCost,
                def.QuarterlyCost,
                def.DurationQuarters,
                def.QuarterlyUpkeep,
                def.DailyFuelUse,
                -1,
                site));

            float confidenceHit = def.FossilLobbyConfidencePenalty * _effectiveLobby;
            if (confidenceHit > 0f) ApplyMeterDeltas(0f, 0f, 0f, -confidenceHit);

            message = $"Ordered {def.DisplayName} @ {RegionCatalog.DisplayName(site)} — {def.DurationQuarters}q.";
            EmitLog(message);
            TipRaised?.Invoke("first_build");
            return true;
        }

        public void SetSelectedRegion(RegionId id)
        {
            SelectedRegion = id;
            EmitLog($"Region focus: {RegionCatalog.DisplayName(id)} — new builds site here; retire prefers local fossils.");
        }

        public bool TryCancelBuild(string orderId, out string message)
        {
            if (AwaitingCrisisDecision) { message = "Resolve the crisis first."; return false; }
            if (string.IsNullOrEmpty(orderId))
            {
                // Cancel oldest active
                var orders = Builds.Orders;
                for (int i = 0; i < orders.Count; i++)
                {
                    if (!orders[i].IsCancelled && !orders[i].IsComplete)
                    {
                        orderId = orders[i].Id;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(orderId) || !Builds.TryCancel(orderId))
            {
                message = "No build to cancel.";
                return false;
            }

            message = "Build cancelled. Progress forfeited — treasury already spent the optimism.";
            EmitLog(message);
            return true;
        }

        public bool TryRetirePlant(string plantId, out string message)
        {
            if (AwaitingCrisisDecision) { message = "Resolve the crisis on your desk first."; return false; }

            // Empty id: prefer fossil in selected region, else any fossil.
            if (string.IsNullOrEmpty(plantId))
            {
                PlantInstance pick = null;
                var plants = Portfolio.Plants;
                for (int i = 0; i < plants.Count; i++)
                {
                    if (plants[i].IsRetired || !plants[i].Fuel.IsFossil()) continue;
                    if (plants[i].Region == SelectedRegion) { pick = plants[i]; break; }
                    if (pick == null) pick = plants[i];
                }

                if (pick == null) { message = "No fossil plant to retire."; return false; }
                plantId = pick.Id;
            }

            if (!Portfolio.TryRetire(plantId, out PlantInstance plant))
            {
                message = "Plant not found or already retired.";
                return false;
            }

            float lobbyHit = plant.Fuel.IsFossil()
                ? (4f + _effectiveLobby * 8f) * Difficulty.LobbyPressureMultiplier * Scenario.LobbyRetireMultiplier
                : 1.5f;
            ApplyMeterDeltas(0f, 0f, 0f, -lobbyHit);
            message = $"Retired {plant.DisplayName} ({RegionCatalog.DisplayName(plant.Region)}). Lobby −{lobbyHit:0.0} conf.";
            EmitLog(message);
            return true;
        }

        /// <summary>Buy emergency interconnector power for a short window.</summary>
        public bool TryBuyEmergencyImport(out string message)
        {
            if (AwaitingCrisisDecision) { message = "Resolve the crisis first."; return false; }
            const float mw = 80f;
            float cost = 22f + Difficulty.EventHarshness * 6f;
            if (Budget < cost)
            {
                message = $"Emergency import needs {cost:0} budget.";
                return false;
            }

            Budget -= cost;
            NoteSpend(cost);
            EmergencyImportMw = Math.Max(EmergencyImportMw, mw);
            EmergencyImportDaysRemaining = Math.Max(EmergencyImportDaysRemaining, 10);
            ApplyMeterDeltas(4f, -1f, 0f, 1.5f);
            message = $"Emergency import +{mw:0} MW for {EmergencyImportDaysRemaining}d (−{cost:0} treasury).";
            EmitLog(message);
            return true;
        }

        /// <summary>Sign a private-sector reserve capacity deal (ministerial procurement).</summary>
        public bool TrySignPrivateReserveDeal(out string message)
        {
            if (AwaitingCrisisDecision) { message = "Resolve the crisis first."; return false; }
            if (PrivateReserveMw >= 200f)
            {
                message = "Private reserve already at grey-box cap (200 MW).";
                return false;
            }

            const float mw = 60f;
            float upfront = 18f * Difficulty.BudgetMultiplier;
            float quarterly = 4.5f;
            if (Budget < upfront)
            {
                message = $"Reserve deal needs {upfront:0} upfront.";
                return false;
            }

            Budget -= upfront;
            NoteSpend(upfront);
            PrivateReserveMw += mw;
            PrivateReserveQuarterlyCost += quarterly;
            ApplyMeterDeltas(3f, 0f, -0.5f, 1f);
            message = $"Private reserve +{mw:0} MW (−{upfront:0} now, −{quarterly:0.0}/q).";
            EmitLog(message);
            return true;
        }

        public bool TryCabinetAction(CrisisChoice choice, out string message)
        {
            if (AwaitingCrisisDecision)
            {
                return TryResolveCrisis(choice, out message);
            }

            if (choice == CrisisChoice.StrategicReserve)
            {
                float cost = 14f * Difficulty.BudgetMultiplier;
                if (Budget < cost)
                {
                    message = "Strategic reserve fill needs ~" + cost.ToString("0") + " treasury.";
                    return false;
                }

                Budget -= cost;
                NoteSpend(cost);
                Resources.Add(FuelKind.Coal, 8f);
                Resources.Add(FuelKind.Gas, 8f);
                Resources.Add(FuelKind.Oil, 4f);
                ApplyMeterDeltas(3f, -1f, 0f, 1f);
                message = $"Strategic reserve filled (−{cost:0} treasury). Stocks up; adequacy +3.";
                Cabinet.SyncFromMeters(Meters, _effectiveLobby);
                EmitLog("CABINET: " + message);
                return true;
            }

            if (choice == CrisisChoice.RenewableSubsidy)
            {
                float cost = 18f * Difficulty.BudgetMultiplier;
                if (Budget < cost)
                {
                    message = "Renewable subsidy needs ~" + cost.ToString("0") + " treasury.";
                    return false;
                }

                Budget -= cost;
                NoteSpend(cost);
                ApplyMeterDeltas(0f, 4f, 5f, -2f * Difficulty.LobbyPressureMultiplier);
                message = $"Renewable subsidy (−{cost:0}). Transition +5, bills +4; lobby bruises confidence.";
                Cabinet.SyncFromMeters(Meters, _effectiveLobby);
                EmitLog("CABINET: " + message);
                return true;
            }

            // Proactive policy from Cabinet menu (costs without an active shock card).
            var synthetic = new PendingEvent
            {
                Kind = PendingEventKind.MeterCrash,
                Title = "Cabinet Policy",
                Severity01 = 0.35f * Difficulty.EventHarshness,
                AwaitingDecision = true
            };

            CrisisOutcome outcome = choice == CrisisChoice.Absorb
                ? new CrisisOutcome { Summary = "No action taken.", ConfidenceDelta = -1f }
                : CrisisResolver.ApplyPostpone(choice, synthetic, _effectiveLobby);

            Budget += outcome.BudgetDelta;
            if (outcome.BudgetDelta < 0f) NoteSpend(-outcome.BudgetDelta);
            ApplyMeterDeltas(outcome.AdequacyDelta, outcome.AffordabilityDelta, outcome.TransitionDelta, outcome.ConfidenceDelta);
            CrisisResolver.ApplyOutcomeToModifiers(outcome, Modifiers);

            if (choice == CrisisChoice.TariffFreeze)
            {
                Cabinet.TariffFreezeActive = true;
                Cabinet.TariffFreezeDaysRemaining = Math.Max(Cabinet.TariffFreezeDaysRemaining, 12);
            }
            else if (choice == CrisisChoice.EmergencyFossil)
            {
                Cabinet.EmergencyFossilActive = true;
                Cabinet.EmergencyFossilDaysRemaining = Math.Max(Cabinet.EmergencyFossilDaysRemaining, 10);
            }

            Cabinet.SyncFromMeters(Meters, _effectiveLobby);
            message = outcome.Summary;
            EmitLog("CABINET: " + message);
            return true;
        }

        public void SetSpeed(GameSpeed speed)
        {
            if (Clock == null || IsGameOver) return;
            Clock.SetSpeed(speed);
        }

        public DayReport AdvanceDay()
        {
            if (IsGameOver || AwaitingCrisisDecision) return LastReport;

            bool newQuarter = Clock.AdvanceDay();
            if (newQuarter) ResolveQuarter();

            Modifiers.TickDay();
            Cabinet.TickDay();
            if (EmergencyImportDaysRemaining > 0)
            {
                EmergencyImportDaysRemaining--;
                if (EmergencyImportDaysRemaining <= 0) EmergencyImportMw = 0f;
            }

            ConsumeFuelsAndApplyShortages();
            Modifiers.ExtraFirmMw = PrivateReserveMw + EmergencyImportMw;

            DeterministicRng dayRng = new DeterministicRng(Scenario.Seed ^ (Clock.AbsoluteDay * 397) ^ 0x5F3759DF);
            CurrentWeather = SampleWeatherKind(Clock, dayRng);
            FuelMarket.TickDay(dayRng, Modifiers);
            FuelPriceIndex = FuelMarket.BlendedIndex;

            if (Clock.Year != _lastYearForLedger)
            {
                FinalizeYearReport(_lastYearForLedger);
                Ledger.OnNewYear();
                _lastYearForLedger = Clock.Year;
                ResetYearAccumulator();
                _yearBudgetAnchor = Budget;
            }

            DayReport report = DayResolver.Resolve(
                Clock, Portfolio, Scenario.BaseDemandMw,
                Scenario.SolarResource, Scenario.WindResource,
                FuelPriceIndex, Modifiers, Budget, Meters, _effectiveLobby, dayRng);

            // Difficulty scales confidence drain after resolve
            float conf = report.Confidence;
            if (conf < Meters.Confidence)
            {
                float drop = Meters.Confidence - conf;
                conf = Meters.Confidence - drop * Difficulty.ConfidenceDrainMultiplier;
            }

            Meters = SeatMeters.Create(report.Adequacy, report.Affordability, report.Transition, conf);
            report.Confidence = Meters.Confidence;
            LastReport = report;
            FillDayCurves(report.DemandMw, report.SupplyMw);
            Mandate.TickDay(Meters);
            Cabinet.SyncFromMeters(Meters, _effectiveLobby);
            _yearAdeqSum += Meters.Adequacy;
            _yearAffSum += Meters.Affordability;
            _yearSamples++;
            DayResolved?.Invoke(report);

            if (Clock.CurrentSeason == Season.Winter)
            {
                TipRaised?.Invoke("first_winter");
            }

            _daysSinceMajorEvent++;
            _daysSinceMeterCrash++;

            TrySpawnMajorEvent(dayRng);
            if (!AwaitingCrisisDecision) TrySpawnMeterCrashCrisis();

            CheckEndStates();
            return report;
        }

        public bool TryResolveCrisis(CrisisChoice choice, out string message)
        {
            if (ActiveEvent == null || !ActiveEvent.AwaitingDecision)
            {
                message = "No crisis awaiting decision.";
                return false;
            }

            // Scale severity by difficulty for absorb hits
            float sev = ActiveEvent.Severity01;
            ActiveEvent.Severity01 = Clamp01(sev * Difficulty.EventHarshness);

            CrisisOutcome outcome = choice == CrisisChoice.Absorb
                ? CrisisResolver.ApplyAbsorb(ActiveEvent)
                : CrisisResolver.ApplyPostpone(choice, ActiveEvent, _effectiveLobby);

            ActiveEvent.Severity01 = sev;
            Budget += outcome.BudgetDelta;
            ApplyMeterDeltas(outcome.AdequacyDelta, outcome.AffordabilityDelta, outcome.TransitionDelta, outcome.ConfidenceDelta);
            CrisisResolver.ApplyOutcomeToModifiers(outcome, Modifiers);

            if (choice == CrisisChoice.TariffFreeze)
            {
                Cabinet.TariffFreezeActive = true;
                Cabinet.TariffFreezeDaysRemaining = Math.Max(Cabinet.TariffFreezeDaysRemaining, 10);
            }
            else if (choice == CrisisChoice.EmergencyFossil)
            {
                Cabinet.EmergencyFossilActive = true;
                Cabinet.EmergencyFossilDaysRemaining = Math.Max(Cabinet.EmergencyFossilDaysRemaining, 8);
            }

            if (ActiveEvent.Kind == PendingEventKind.MeterCrash) _daysSinceMeterCrash = 0;

            message = outcome.Summary;
            History.UpdateLatestChoice(ShortChoice(choice));
            EmitLog(message);
            ActiveEvent.AwaitingDecision = false;
            ActiveEvent = null;
            Clock.SetSpeed(GameSpeed.Normal);
            Cabinet.SyncFromMeters(Meters, _effectiveLobby);
            CheckEndStates();
            return true;
        }

        private static string ShortChoice(CrisisChoice choice)
        {
            switch (choice)
            {
                case CrisisChoice.Absorb: return "absorbed";
                case CrisisChoice.EmergencyFossil: return "emergency fossil";
                case CrisisChoice.TariffFreeze: return "tariff freeze";
                case CrisisChoice.LoadShedIndustry: return "shed industry";
                case CrisisChoice.LoadShedSuburbs: return "shed suburbs";
                case CrisisChoice.LoadShedTransit: return "shed transit";
                case CrisisChoice.StrategicReserve: return "strategic reserve";
                case CrisisChoice.RenewableSubsidy: return "renewable subsidy";
                default: return choice.ToString();
            }
        }

        public void NoteEventForYear(PendingEvent evt)
        {
            if (evt == null) return;
            _yearEventCount++;
            if (evt.Severity01 >= _yearBiggestSev)
            {
                _yearBiggestSev = evt.Severity01;
                _yearBiggestEvent = string.IsNullOrEmpty(evt.Title) ? "Event" : evt.Title;
            }
        }

        private void NoteSpend(float amount)
        {
            if (amount > 0f) _yearSpend += amount;
        }

        private void ResetYearAccumulator()
        {
            _yearAdeqSum = 0f;
            _yearAffSum = 0f;
            _yearSamples = 0;
            _yearSpend = 0f;
            _yearBiggestEvent = "—";
            _yearBiggestSev = 0f;
            _yearEventCount = 0;
        }

        private void FinalizeYearReport(int closedYear)
        {
            if (_yearSamples <= 0 && _yearEventCount <= 0) return;
            float adeq = _yearSamples > 0 ? _yearAdeqSum / _yearSamples : Meters.Adequacy;
            float aff = _yearSamples > 0 ? _yearAffSum / _yearSamples : Meters.Affordability;
            float spend = _yearSpend;
            if (spend < 0.01f && _yearBudgetAnchor > Budget)
                spend = _yearBudgetAnchor - Budget;

            LastYearReport = new YearReport
            {
                Year = closedYear,
                AdequacyAvg = adeq,
                AffordAvg = aff,
                CleanPctEnd = Meters.Transition,
                Spend = spend,
                BiggestEventTitle = _yearBiggestEvent,
                BiggestEventSeverity = _yearBiggestSev,
                EventCount = _yearEventCount
            };
            EmitLog("YEAR REPORT: " + closedYear + " closed.");
            YearEnded?.Invoke(LastYearReport);
        }

        public MinistryOfPower.Runtime.GameSaveData CaptureSave(int slot)
        {
            var data = new MinistryOfPower.Runtime.GameSaveData
            {
                Version = MinistryOfPower.Runtime.GameSaveData.CurrentVersion,
                Slot = slot,
                ScenarioId = Scenario.Id,
                ScenarioName = Scenario.DisplayName,
                Difficulty = (int)Difficulty.Id,
                Seed = Scenario.Seed,
                Year = Clock.Year,
                AbsoluteDay = Clock.AbsoluteDay,
                DayIndex = Clock.DayIndex,
                QuarterIndex = Clock.QuarterIndex,
                DayFraction = Clock.DayFraction,
                Speed = (int)Clock.Speed,
                Budget = Budget,
                FuelPriceIndex = FuelPriceIndex,
                Adequacy = Meters.Adequacy,
                Affordability = Meters.Affordability,
                Transition = Meters.Transition,
                Confidence = Meters.Confidence,
                Coal = Resources.Coal,
                Gas = Resources.Gas,
                Oil = Resources.Oil,
                Uranium = Resources.Uranium,
                ImportBaseline = Modifiers.ImportMwBaseline,
                OilShockMul = Modifiers.OilShockMultiplier,
                OilShockDays = Modifiers.OilShockDaysRemaining,
                HydroMul = Modifiers.HydroFactorMul,
                DroughtDays = Modifiers.DroughtDaysRemaining,
                DemandMul = Modifiers.DemandFactorMul,
                HeatwaveDays = Modifiers.HeatwaveDaysRemaining,
                ImportAvailable = Modifiers.ImportMwAvailable,
                ImportDays = Modifiers.ImportDisruptionDaysRemaining,
                PrivateReserveMw = PrivateReserveMw,
                PrivateReserveQuarterlyCost = PrivateReserveQuarterlyCost,
                EmergencyImportMw = EmergencyImportMw,
                EmergencyImportDaysRemaining = EmergencyImportDaysRemaining,
                DaysSinceMajorEvent = _daysSinceMajorEvent,
                DaysSinceMeterCrash = _daysSinceMeterCrash,
                FuelCoal = FuelMarket.Coal,
                FuelGas = FuelMarket.Gas,
                FuelOil = FuelMarket.Oil,
                FuelUranium = FuelMarket.Uranium,
                AdequacyStreak = Mandate.AdequacyGoodStreak,
                AffordStreak = Mandate.AffordabilityGoodStreak,
                BestAdequacyStreak = Mandate.BestAdequacyStreak,
                BestAffordStreak = Mandate.BestAffordabilityStreak,
                SelectedRegion = (int)SelectedRegion
            };

            for (int i = 0; i < _buildCatalog.Count; i++)
            {
                data.BuildCatalogIds.Add(_buildCatalog[i].Id);
            }

            IReadOnlyList<PlantInstance> plants = Portfolio.Plants;
            for (int i = 0; i < plants.Count; i++)
            {
                PlantInstance p = plants[i];
                data.Plants.Add(new MinistryOfPower.Runtime.PlantSave
                {
                    Id = p.Id,
                    DisplayName = p.DisplayName,
                    Fuel = (int)p.Fuel,
                    CapacityMw = p.CapacityMw,
                    Availability = p.Availability,
                    VariableCost = p.VariableCostPerMwh,
                    OilExposure = p.OilExposure,
                    DefinitionId = p.DefinitionId,
                    Retired = p.IsRetired,
                    QuarterlyUpkeep = p.QuarterlyUpkeep,
                    DailyFuelUse = p.DailyFuelUse,
                    Region = (int)p.Region
                });
            }

            IReadOnlyList<BuildOrder> orders = Builds.Orders;
            for (int i = 0; i < orders.Count; i++)
            {
                BuildOrder o = orders[i];
                data.Builds.Add(new MinistryOfPower.Runtime.BuildSave
                {
                    Id = o.Id,
                    DisplayName = o.DisplayName,
                    DefinitionId = o.DefinitionId,
                    Fuel = (int)o.ResultFuel,
                    CapacityMw = o.ResultCapacityMw,
                    Availability = o.ResultAvailability,
                    VariableCost = o.ResultVariableCost,
                    OilExposure = o.ResultOilExposure,
                    PaymentMode = (int)o.PaymentMode,
                    Upfront = o.UpfrontCost,
                    Quarterly = o.QuarterlyCost,
                    TotalQuarters = o.TotalQuarters,
                    QuartersRemaining = o.QuartersRemaining,
                    Upkeep = o.ResultQuarterlyUpkeep,
                    DailyFuelUse = o.ResultDailyFuelUse,
                    Cancelled = o.IsCancelled,
                    Region = (int)o.Region
                });
            }

            return data;
        }

        public void LoadFromSave(MinistryOfPower.Runtime.GameSaveData data, IEnumerable<BuildDefinitionConfig> fallbackCatalog)
        {
            Difficulty = DifficultyConfig.Create((DifficultyId)data.Difficulty);
            ScenarioConfig scenario;
            List<BuildDefinitionConfig> builds;
            MinistryOfPower.Runtime.PrototypeContentFactory.TryCreateById(data.ScenarioId, out scenario, out builds);
            if (fallbackCatalog != null)
            {
                builds = new List<BuildDefinitionConfig>(fallbackCatalog);
            }

            Start(scenario, builds, Difficulty);

            Clock.Restore(data.Year, data.AbsoluteDay, data.DayIndex, data.QuarterIndex, data.DayFraction, (GameSpeed)data.Speed);
            Budget = data.Budget;
            FuelPriceIndex = data.FuelPriceIndex;
            Meters = SeatMeters.Create(data.Adequacy, data.Affordability, data.Transition, data.Confidence);
            Resources.Coal = data.Coal;
            Resources.Gas = data.Gas;
            Resources.Oil = data.Oil;
            Resources.Uranium = data.Uranium;
            Modifiers.ConfigureBaselineImports(data.ImportBaseline);
            Modifiers.OilShockMultiplier = data.OilShockMul;
            Modifiers.OilShockDaysRemaining = data.OilShockDays;
            Modifiers.HydroFactorMul = data.HydroMul;
            Modifiers.DroughtDaysRemaining = data.DroughtDays;
            Modifiers.DemandFactorMul = data.DemandMul;
            Modifiers.HeatwaveDaysRemaining = data.HeatwaveDays;
            Modifiers.ImportMwAvailable = data.ImportAvailable;
            Modifiers.ImportDisruptionDaysRemaining = data.ImportDays;
            PrivateReserveMw = data.PrivateReserveMw;
            PrivateReserveQuarterlyCost = data.PrivateReserveQuarterlyCost;
            EmergencyImportMw = data.EmergencyImportMw;
            EmergencyImportDaysRemaining = data.EmergencyImportDaysRemaining;
            Modifiers.ExtraFirmMw = PrivateReserveMw + EmergencyImportMw;
            _daysSinceMajorEvent = data.DaysSinceMajorEvent;
            _daysSinceMeterCrash = data.DaysSinceMeterCrash;
            if (data.FuelCoal > 0.01f) FuelMarket.Coal = data.FuelCoal;
            if (data.FuelGas > 0.01f) FuelMarket.Gas = data.FuelGas;
            if (data.FuelOil > 0.01f) FuelMarket.Oil = data.FuelOil;
            if (data.FuelUranium > 0.01f) FuelMarket.Uranium = data.FuelUranium;
            FuelPriceIndex = FuelMarket.BlendedIndex;
            Mandate.AdequacyGoodStreak = data.AdequacyStreak;
            Mandate.AffordabilityGoodStreak = data.AffordStreak;
            Mandate.BestAdequacyStreak = data.BestAdequacyStreak;
            Mandate.BestAffordabilityStreak = data.BestAffordStreak;
            SelectedRegion = (RegionId)data.SelectedRegion;

            RebuildPortfolioFromSave(data);
            RebuildBuildsFromSave(data);
            Cabinet.SyncFromMeters(Meters, _effectiveLobby);
            EmitLog($"Loaded save — {Scenario.DisplayName}, day {Clock.AbsoluteDay}.");
        }

        private void RebuildPortfolioFromSave(MinistryOfPower.Runtime.GameSaveData data)
        {
            Portfolio = new PlantPortfolio();
            for (int i = 0; i < data.Plants.Count; i++)
            {
                MinistryOfPower.Runtime.PlantSave p = data.Plants[i];
                RegionId region = p.Region >= 0 && p.Region <= 2
                    ? (RegionId)p.Region
                    : RegionCatalog.DefaultRegionForFuel((FuelKind)p.Fuel);
                var plant = new PlantInstance(
                    p.Id, p.DisplayName, (FuelKind)p.Fuel, p.CapacityMw, p.Availability,
                    p.VariableCost, p.OilExposure, p.DefinitionId, p.QuarterlyUpkeep, p.DailyFuelUse, region);
                if (p.Retired) plant.SetRetired(true);
                Portfolio.Add(plant);
            }
        }

        private void RebuildBuildsFromSave(MinistryOfPower.Runtime.GameSaveData data)
        {
            Builds = new BuildQueue();
            for (int i = 0; i < data.Builds.Count; i++)
            {
                MinistryOfPower.Runtime.BuildSave b = data.Builds[i];
                if (b.Cancelled) continue;
                RegionId region = b.Region >= 0 && b.Region <= 2
                    ? (RegionId)b.Region
                    : RegionCatalog.DefaultRegionForFuel((FuelKind)b.Fuel);
                var order = new BuildOrder(
                    b.Id, b.DisplayName, b.DefinitionId, (FuelKind)b.Fuel, b.CapacityMw, b.Availability,
                    b.VariableCost, b.OilExposure, (BuildPaymentMode)b.PaymentMode, b.Upfront, b.Quarterly,
                    b.TotalQuarters, b.Upkeep, b.DailyFuelUse, b.QuartersRemaining, region);
                Builds.Enqueue(order);
            }
        }

        private void ResolveQuarter()
        {
            Budget += _quarterlyIncome;
            float charges = Builds.TickQuarter(_completedScratch);
            Budget -= charges;
            Budget -= PrivateReserveQuarterlyCost;

            IReadOnlyList<PlantInstance> plants = Portfolio.Plants;
            float upkeep = 0f;
            for (int i = 0; i < plants.Count; i++)
            {
                if (plants[i].IsRetired) continue;
                upkeep += plants[i].QuarterlyUpkeep * FuelMarket.UpkeepMul(plants[i].Fuel);
            }

            Budget -= upkeep;
            Resources.Add(FuelKind.Coal, 18f);
            Resources.Add(FuelKind.Gas, 16f);
            Resources.Add(FuelKind.Oil, 12f);
            Resources.Add(FuelKind.Nuclear, 4f);

            Ledger.RecordQuarter(_quarterlyIncome, upkeep, charges, PrivateReserveQuarterlyCost, 0f);

            for (int i = 0; i < _completedScratch.Count; i++)
            {
                PlantInstance plant = _completedScratch[i];
                string id = Portfolio.NextPlantId(plant.DefinitionId);
                Portfolio.Add(new PlantInstance(
                    id, plant.DisplayName, plant.Fuel, plant.CapacityMw, plant.Availability,
                    plant.VariableCostPerMwh, plant.OilExposure, plant.DefinitionId,
                    plant.QuarterlyUpkeep, plant.DailyFuelUse, plant.Region));
                EmitLog($"{plant.DisplayName} online @ {RegionCatalog.DisplayName(plant.Region)} (+{plant.CapacityMw:0} MW).");
            }

            if (Budget < 0f)
            {
                ApplyMeterDeltas(0f, -2f, 0f, -3f);
                EmitLog("Treasury overdrawn — confidence wobbles.");
            }
        }

        private void ConsumeFuelsAndApplyShortages()
        {
            IReadOnlyList<PlantInstance> plants = Portfolio.Plants;
            bool shortage = false;
            for (int i = 0; i < plants.Count; i++)
            {
                PlantInstance p = plants[i];
                if (p.IsRetired || p.DailyFuelUse <= 0f || !ResourceStockpile.NeedsStock(p.Fuel))
                {
                    continue;
                }

                if (!Resources.TryConsume(p.Fuel, p.DailyFuelUse))
                {
                    shortage = true;
                }
            }

            if (shortage)
            {
                ApplyMeterDeltas(-2.5f, -1f, 0f, -0.8f);
                EmitLog("Fuel stockpile short — plant output constrained.");
            }
        }

        private void TrySpawnMajorEvent(DeterministicRng rng)
        {
            if (ActiveEvent != null || _daysSinceMajorEvent < 22) return;

            float chance = _daysSinceMajorEvent >= 40 ? 0.55f : 0.08f;
            chance *= 0.85f + 0.3f * Difficulty.EventHarshness;
            chance *= Difficulty.EventFrequencyMultiplier;
            if (rng.NextFloat01() > chance) return;

            if (!MajorEventSpawner.TrySpawn(Portfolio, Modifiers, Clock, rng, out PendingEvent evt, Scenario.EventWeights)
                || evt == null)
            {
                return;
            }

            RaiseEvent(evt, autoResolveIfInsured: true);
        }

        private void TrySpawnMeterCrashCrisis()
        {
            bool inBand = Meters.Adequacy < MeterCrashAdequacyThreshold
                          || Meters.Affordability < MeterCrashAffordabilityThreshold
                          || LastReport.CrisisTriggered;
            _meterCrisisStreak = inBand ? _meterCrisisStreak + 1 : 0;
            if (ActiveEvent != null || _daysSinceMeterCrash < MeterCrashCooldownDays) return;
            if (_meterCrisisStreak < MeterCrashStreakRequired) return;

            PendingEvent evt = MajorEventSpawner.BuildMeterCrash(Meters, Clock.CurrentSeason);
            _meterCrisisStreak = 0;
            RaiseEvent(evt, autoResolveIfInsured: false);
        }

        private void RaiseEvent(PendingEvent evt, bool autoResolveIfInsured)
        {
            ActiveEvent = evt;
            _daysSinceMajorEvent = 0;
            NoteEventForYear(evt);
            History.Record(evt, Clock, autoResolveIfInsured && !evt.AwaitingDecision ? "shrugged" : "pending");
            EventRaised?.Invoke(ActiveEvent);
            TipRaised?.Invoke("first_event");

            if (autoResolveIfInsured && !ActiveEvent.AwaitingDecision)
            {
                ApplyInsuredShrug(ActiveEvent);
                EmitLog($"{ActiveEvent.Title} — insured portfolio shrugs it off.");
                ActiveEvent = null;
            }
            else
            {
                Clock.SetSpeed(GameSpeed.Paused);
                EmitLog(ActiveEvent.OffersLoadShed
                ? $"IN-TRAY: {ActiveEvent.Title} — who goes dark, or burn fossil."
                : $"IN-TRAY: {ActiveEvent.Title} — absorb or postpone.");
            }
        }

        private void ApplyInsuredShrug(PendingEvent evt)
        {
            switch (evt.Kind)
            {
                case PendingEventKind.OilPriceShock: Modifiers.ApplyOilShock(evt.Severity01 * 0.15f, 5); break;
                case PendingEventKind.Drought: Modifiers.ApplyDrought(0.75f, 6); break;
                case PendingEventKind.Heatwave: Modifiers.ApplyHeatwave(1.05f, 4); break;
                case PendingEventKind.ImportDisruption: Modifiers.ApplyImportCut(0.7f, 5); break;
                case PendingEventKind.ColdSnap: Modifiers.ApplyHeatwave(1.06f, 4); break;
                case PendingEventKind.StormOutage:
                    Modifiers.ApplyDrought(0.8f, 4);
                    Modifiers.ApplyImportCut(0.75f, 4);
                    break;
            }
        }

        private void CheckEndStates()
        {
            if (Meters.Confidence <= 0.01f)
            {
                IsGameOver = true;
                Clock.SetSpeed(GameSpeed.Paused);
                EmitLog("SACKED — ministerial confidence collapsed.");
                GameOver?.Invoke();
                return;
            }

            if (!IsVictory && Mandate.CheckVictory(Clock, Scenario.StartYear, Meters))
            {
                IsVictory = true;
                Clock.SetSpeed(GameSpeed.Paused);
                EmitLog("MANDATE HONOURED — campaign held: clean mix, bills, lights.");
            }
        }

        private BuildDefinitionConfig FindBuild(string id)
        {
            for (int i = 0; i < _buildCatalog.Count; i++)
            {
                if (_buildCatalog[i].Id == id) return _buildCatalog[i];
            }

            return null;
        }

        private void ApplyMeterDeltas(float a, float aff, float t, float c)
        {
            Meters = SeatMeters.Create(Meters.Adequacy + a, Meters.Affordability + aff, Meters.Transition + t, Meters.Confidence + c);
        }

        private void EmitLog(string message) => LogEmitted?.Invoke(message);

        private static void GuessPlantEconomics(FuelKind fuel, float mw, ref float upkeep, ref float fuelUse)
        {
            if (upkeep > 0f || fuelUse > 0f) return;
            float scale = mw / 200f;
            switch (fuel)
            {
                case FuelKind.Coal: upkeep = 3.5f * scale; fuelUse = 0.4f * scale; break;
                case FuelKind.Gas: upkeep = 3f * scale; fuelUse = 0.3f * scale; break;
                case FuelKind.Oil: upkeep = 2f * scale; fuelUse = 0.5f * scale; break;
                case FuelKind.Nuclear: upkeep = 4f * scale; fuelUse = 0.07f * scale; break;
                default: upkeep = 1f * scale; fuelUse = 0f; break;
            }
        }

        private static WeatherKind SampleWeatherKind(GameClock clock, DeterministicRng rng)
        {
            Season season = clock.CurrentSeason;
            float r = rng.NextFloat01();
            if (season == Season.Winter)
            {
                if (r < 0.35f) return WeatherKind.Storm;
                if (r < 0.6f) return WeatherKind.Overcast;
                return WeatherKind.Clear;
            }

            if (season == Season.Summer)
            {
                if (r < 0.2f) return WeatherKind.HeatHaze;
                if (r < 0.35f) return WeatherKind.Overcast;
                return WeatherKind.Clear;
            }

            if (r < 0.25f) return WeatherKind.Overcast;
            if (r < 0.35f) return WeatherKind.Storm;
            return WeatherKind.Clear;
        }

        private void FillDayCurves(float dayDemand, float daySupply)
        {
            // Duck-curve-ish relative shape for grey-box strip (not live dispatch).
            for (int h = 0; h < 24; h++)
            {
                float t = h / 24f;
                float demandShape = 0.75f
                    + 0.15f * (float)Math.Sin((t - 0.2f) * Math.PI * 2f)
                    + 0.2f * Math.Max(0f, (float)Math.Sin((t - 0.65f) * Math.PI * 2f));
                float solarShape = Math.Max(0f, (float)Math.Sin((t - 0.05f) * Math.PI));
                float supplyShape = 0.7f + 0.25f * solarShape
                    + (CurrentWeather == WeatherKind.Storm ? 0.08f : 0f);

                DayDemandCurve[h] = dayDemand * demandShape;
                DaySupplyCurve[h] = daySupply * supplyShape;
            }
        }

        private static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }
    }

    public enum WeatherKind
    {
        Clear = 0,
        Overcast = 1,
        Storm = 2,
        HeatHaze = 3
    }
}
