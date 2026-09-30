using System;
using System.Collections.Generic;

namespace MinistryOfPower.Simulation
{
    /// <summary>
    /// Session orchestrator: owns mutable desk state and public Play API.
    /// Player verbs → <see cref="GameSessionActions"/>; day tick → <see cref="GameSessionDayLoop"/>;
    /// persistence → Runtime <c>GameSessionSaveMapper</c>.
    /// </summary>
    public sealed class GameSession
    {
        public const float MeterCrashAdequacyThreshold = 45f;
        public const float MeterCrashAffordabilityThreshold = 40f;
        public const int MeterCrashCooldownDays = 16;
        public const int MeterCrashStreakRequired = 2;
        public const float EmergencyFossilExtraMw = 120f;
        public const float TariffFreezeAffordBoost = 10f;
        public const float FuelShortageDerate = 0.35f;

        public event Action<DayReport> DayResolved;
        public event Action<PendingEvent> EventRaised;
        public event Action<string> LogEmitted;
        public event Action GameOver;
        public event Action<string> TipRaised;
        public event Action<YearReport> YearEnded;

        private readonly List<PlantInstance> _completedScratch = new List<PlantInstance>(4);
        private readonly List<BuildDefinitionConfig> _buildCatalog = new List<BuildDefinitionConfig>();

        public GameClock Clock { get; internal set; }
        public PlantPortfolio Portfolio { get; internal set; }
        public BuildQueue Builds { get; internal set; }
        public SeatMeters Meters { get; internal set; }
        public ScenarioConfig Scenario { get; private set; }
        public DifficultyConfig Difficulty { get; private set; }
        public DayModifiers Modifiers { get; private set; }
        public ResourceStockpile Resources { get; private set; }
        public CabinetState Cabinet { get; private set; }
        public FuelMarket FuelMarket { get; private set; }
        public MandateTracker Mandate { get; private set; }
        public BudgetLedger Ledger { get; private set; }
        public EventHistory History { get; private set; }
        public RegionId SelectedRegion { get; internal set; } = RegionId.Coast;
        public float Budget { get; internal set; }
        public float FuelPriceIndex { get; internal set; } = 1f;
        public PendingEvent ActiveEvent { get; internal set; }
        public DayReport LastReport { get; internal set; }
        public bool IsGameOver { get; internal set; }
        public bool IsVictory { get; internal set; }
        public bool AwaitingCrisisDecision => ActiveEvent != null && ActiveEvent.AwaitingDecision;
        public IReadOnlyList<BuildDefinitionConfig> BuildCatalog => _buildCatalog;
        public float OilShockMultiplier => Modifiers != null ? Modifiers.OilShockMultiplier : 1f;
        public float FossilLobby01 => EffectiveLobby;
        public WeatherKind CurrentWeather { get; internal set; } = WeatherKind.Clear;
        public float PrivateReserveMw { get; internal set; }
        public float PrivateReserveQuarterlyCost { get; internal set; }
        public float EmergencyImportMw { get; internal set; }
        public int EmergencyImportDaysRemaining { get; internal set; }
        public YearReport LastYearReport { get; internal set; }
        public float[] DayDemandCurve { get; private set; } = new float[24];
        public float[] DaySupplyCurve { get; private set; } = new float[24];

        internal int DaysSinceMajorEvent;
        internal int DaysSinceMeterCrash;
        internal int MeterCrisisStreak;
        internal float EffectiveLobby;
        internal float QuarterlyIncome;
        internal int LastYearForLedger;
        internal float YearAdeqSum;
        internal float YearAffSum;
        internal int YearSamples;
        internal float YearSpend;
        internal float YearBudgetAnchor;
        internal string YearBiggestEvent = "—";
        internal float YearBiggestSev;
        internal int YearEventCount;
        internal List<PlantInstance> CompletedScratch => _completedScratch;

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

            EffectiveLobby = MathUtil.Clamp01(scenario.FossilLobbyStrength * Difficulty.LobbyPressureMultiplier);
            QuarterlyIncome = scenario.QuarterlyIncome * Difficulty.IncomeMultiplier;
            Budget = scenario.StartingBudget * Difficulty.BudgetMultiplier;
            FuelPriceIndex = 1f;
            ActiveEvent = null;
            IsGameOver = false;
            IsVictory = false;
            DaysSinceMajorEvent = 0;
            DaysSinceMeterCrash = MeterCrashCooldownDays;
            MeterCrisisStreak = 0;
            LastYearForLedger = scenario.StartYear;
            CurrentWeather = WeatherKind.Clear;
            PrivateReserveMw = 0f;
            PrivateReserveQuarterlyCost = 0f;
            EmergencyImportMw = 0f;
            EmergencyImportDaysRemaining = 0;
            LastYearReport = null;
            ResetYearAccumulator();
            YearBudgetAnchor = Budget;
            FillDayCurves(scenario.BaseDemandMw, scenario.BaseDemandMw * 0.95f);

            _buildCatalog.Clear();
            if (builds != null)
            {
                foreach (BuildDefinitionConfig b in builds)
                    _buildCatalog.Add(b);
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

            Cabinet.SyncFromMeters(Meters, EffectiveLobby);
            LastReport = default;
            EmitLog($"Seated: {scenario.DisplayName} · {Difficulty.DisplayName} · treasury {Budget:0}. Don't waste the quiet.");
        }

        public bool TryStartBuild(string buildDefinitionId, out string message)
            => GameSessionActions.TryStartBuild(this, buildDefinitionId, out message);

        public void SetSelectedRegion(RegionId id)
            => GameSessionActions.SetSelectedRegion(this, id);

        public bool TryCancelBuild(string orderId, out string message)
            => GameSessionActions.TryCancelBuild(this, orderId, out message);

        public bool TryRetirePlant(string plantId, out string message)
            => GameSessionActions.TryRetirePlant(this, plantId, out message);

        public bool TryBuyEmergencyImport(out string message)
            => GameSessionActions.TryBuyEmergencyImport(this, out message);

        public bool TrySignPrivateReserveDeal(out string message)
            => GameSessionActions.TrySignPrivateReserveDeal(this, out message);

        public bool TryCabinetAction(CrisisChoice choice, out string message)
            => GameSessionActions.TryCabinetAction(this, choice, out message);

        public bool TryResolveCrisis(CrisisChoice choice, out string message)
            => GameSessionActions.TryResolveCrisis(this, choice, out message);

        public void SetSpeed(GameSpeed speed)
        {
            if (Clock == null || IsGameOver) return;
            Clock.SetSpeed(speed);
        }

        public DayReport AdvanceDay() => GameSessionDayLoop.AdvanceDay(this);

        public void NoteEventForYear(PendingEvent evt)
        {
            if (evt == null) return;
            YearEventCount++;
            if (evt.Severity01 >= YearBiggestSev)
            {
                YearBiggestSev = evt.Severity01;
                YearBiggestEvent = string.IsNullOrEmpty(evt.Title) ? "Event" : evt.Title;
            }
        }

        internal void NoteSpend(float amount)
        {
            if (amount > 0f) YearSpend += amount;
        }

        internal void ResetYearAccumulator()
        {
            YearAdeqSum = 0f;
            YearAffSum = 0f;
            YearSamples = 0;
            YearSpend = 0f;
            YearBiggestEvent = "—";
            YearBiggestSev = 0f;
            YearEventCount = 0;
        }

        internal BuildDefinitionConfig FindBuild(string id)
        {
            for (int i = 0; i < _buildCatalog.Count; i++)
            {
                if (_buildCatalog[i].Id == id) return _buildCatalog[i];
            }

            return null;
        }

        internal void ApplyMeterDeltas(float a, float aff, float t, float c)
        {
            Meters = SeatMeters.Create(Meters.Adequacy + a, Meters.Affordability + aff, Meters.Transition + t, Meters.Confidence + c);
        }

        internal void EmitLog(string message) => LogEmitted?.Invoke(message);

        internal void RaiseTip(string tipId) => TipRaised?.Invoke(tipId);

        internal void InvokeDayResolved(DayReport report) => DayResolved?.Invoke(report);

        internal void InvokeEventRaised(PendingEvent evt) => EventRaised?.Invoke(evt);

        internal void InvokeYearEnded(YearReport report) => YearEnded?.Invoke(report);

        internal void CheckEndStates()
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

        internal void FillDayCurves(float dayDemand, float daySupply)
        {
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
    }
}
