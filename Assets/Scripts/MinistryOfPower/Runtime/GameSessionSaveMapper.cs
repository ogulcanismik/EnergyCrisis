using System;
using System.Collections.Generic;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.Runtime
{
    /// <summary>
    /// Maps <see cref="GameSession"/> ↔ <see cref="GameSaveData"/> so Simulation
    /// does not reference Runtime persistence types.
    /// </summary>
    public static class GameSessionSaveMapper
    {
        public static GameSaveData Capture(GameSession session, int slot)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            var data = new GameSaveData
            {
                Version = GameSaveData.CurrentVersion,
                Slot = slot,
                ScenarioId = session.Scenario.Id,
                ScenarioName = session.Scenario.DisplayName,
                Difficulty = (int)session.Difficulty.Id,
                Seed = session.Scenario.Seed,
                Year = session.Clock.Year,
                AbsoluteDay = session.Clock.AbsoluteDay,
                DayIndex = session.Clock.DayIndex,
                QuarterIndex = session.Clock.QuarterIndex,
                DayFraction = session.Clock.DayFraction,
                Speed = (int)session.Clock.Speed,
                Budget = session.Budget,
                FuelPriceIndex = session.FuelPriceIndex,
                Adequacy = session.Meters.Adequacy,
                Affordability = session.Meters.Affordability,
                Transition = session.Meters.Transition,
                Confidence = session.Meters.Confidence,
                Coal = session.Resources.Coal,
                Gas = session.Resources.Gas,
                Oil = session.Resources.Oil,
                Uranium = session.Resources.Uranium,
                ImportBaseline = session.Modifiers.ImportMwBaseline,
                OilShockMul = session.Modifiers.OilShockMultiplier,
                OilShockDays = session.Modifiers.OilShockDaysRemaining,
                HydroMul = session.Modifiers.HydroFactorMul,
                DroughtDays = session.Modifiers.DroughtDaysRemaining,
                DemandMul = session.Modifiers.DemandFactorMul,
                HeatwaveDays = session.Modifiers.HeatwaveDaysRemaining,
                ImportAvailable = session.Modifiers.ImportMwAvailable,
                ImportDays = session.Modifiers.ImportDisruptionDaysRemaining,
                PrivateReserveMw = session.PrivateReserveMw,
                PrivateReserveQuarterlyCost = session.PrivateReserveQuarterlyCost,
                EmergencyImportMw = session.EmergencyImportMw,
                EmergencyImportDaysRemaining = session.EmergencyImportDaysRemaining,
                DaysSinceMajorEvent = session.DaysSinceMajorEvent,
                DaysSinceMeterCrash = session.DaysSinceMeterCrash,
                FuelCoal = session.FuelMarket.Coal,
                FuelGas = session.FuelMarket.Gas,
                FuelOil = session.FuelMarket.Oil,
                FuelUranium = session.FuelMarket.Uranium,
                AdequacyStreak = session.Mandate.AdequacyGoodStreak,
                AffordStreak = session.Mandate.AffordabilityGoodStreak,
                BestAdequacyStreak = session.Mandate.BestAdequacyStreak,
                BestAffordStreak = session.Mandate.BestAffordabilityStreak,
                SelectedRegion = (int)session.SelectedRegion,
                MeterCrisisStreak = session.MeterCrisisStreak,
                TariffFreezeActive = session.Cabinet.TariffFreezeActive,
                TariffFreezeDays = session.Cabinet.TariffFreezeDaysRemaining,
                EmergencyFossilActive = session.Cabinet.EmergencyFossilActive,
                EmergencyFossilDays = session.Cabinet.EmergencyFossilDaysRemaining,
                CurrentWeather = (int)session.CurrentWeather,
                IsGameOver = session.IsGameOver,
                IsVictory = session.IsVictory,
                LastYearForLedger = session.LastYearForLedger,
                YearAdeqSum = session.YearAdeqSum,
                YearAffSum = session.YearAffSum,
                YearSamples = session.YearSamples,
                YearSpend = session.YearSpend,
                YearBudgetAnchor = session.YearBudgetAnchor,
                YearBiggestEvent = session.YearBiggestEvent,
                YearBiggestSev = session.YearBiggestSev,
                YearEventCount = session.YearEventCount,
                MandateDaysSinceSample = session.Mandate.DaysSinceSample
            };

            IReadOnlyList<BuildDefinitionConfig> catalog = session.BuildCatalog;
            for (int i = 0; i < catalog.Count; i++)
                data.BuildCatalogIds.Add(catalog[i].Id);

            IReadOnlyList<float> spark = session.Mandate.CleanSamples;
            for (int i = 0; i < spark.Count; i++)
                data.MandateSparkSamples.Add(spark[i]);

            IReadOnlyList<EventHistoryEntry> history = session.History.Entries;
            for (int i = 0; i < history.Count; i++)
            {
                EventHistoryEntry e = history[i];
                data.EventHistory.Add(new EventHistorySave
                {
                    Title = e.Title,
                    SeasonTag = e.SeasonTag,
                    DateLabel = e.DateLabel,
                    ChoiceSummary = e.ChoiceSummary
                });
            }

            PendingEvent active = session.ActiveEvent;
            if (active != null)
            {
                data.HasActiveEvent = true;
                data.ActiveEventKind = (int)active.Kind;
                data.ActiveEventTitle = active.Title;
                data.ActiveEventBody = active.Body;
                data.ActiveEventExposureLabel = active.ExposureLabel;
                data.ActiveEventExposure01 = active.Exposure01;
                data.ActiveEventSeverity01 = active.Severity01;
                data.ActiveEventAwaiting = active.AwaitingDecision;
                data.ActiveEventOffersLoadShed = active.OffersLoadShed;
            }

            IReadOnlyList<PlantInstance> plants = session.Portfolio.Plants;
            for (int i = 0; i < plants.Count; i++)
            {
                PlantInstance p = plants[i];
                data.Plants.Add(new PlantSave
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

            IReadOnlyList<BuildOrder> orders = session.Builds.Orders;
            for (int i = 0; i < orders.Count; i++)
            {
                BuildOrder o = orders[i];
                data.Builds.Add(new BuildSave
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

        public static void Apply(GameSession session, GameSaveData data, IEnumerable<BuildDefinitionConfig> fallbackCatalog)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (data == null) throw new ArgumentNullException(nameof(data));

            DifficultyConfig difficulty = DifficultyConfig.Create((DifficultyId)data.Difficulty);
            PrototypeContentFactory.TryCreateById(data.ScenarioId, out ScenarioConfig scenario, out List<BuildDefinitionConfig> builds);
            if (fallbackCatalog != null)
                builds = new List<BuildDefinitionConfig>(fallbackCatalog);

            builds = ResolveCatalog(builds, data.BuildCatalogIds);
            session.Start(scenario, builds, difficulty);

            session.Clock.Restore(data.Year, data.AbsoluteDay, data.DayIndex, data.QuarterIndex, data.DayFraction, (GameSpeed)data.Speed);
            session.Budget = data.Budget;
            session.FuelPriceIndex = data.FuelPriceIndex;
            session.Meters = SeatMeters.Create(data.Adequacy, data.Affordability, data.Transition, data.Confidence);
            session.Resources.Coal = data.Coal;
            session.Resources.Gas = data.Gas;
            session.Resources.Oil = data.Oil;
            session.Resources.Uranium = data.Uranium;
            session.Modifiers.ConfigureBaselineImports(data.ImportBaseline);
            session.Modifiers.OilShockMultiplier = data.OilShockMul;
            session.Modifiers.OilShockDaysRemaining = data.OilShockDays;
            session.Modifiers.HydroFactorMul = data.HydroMul;
            session.Modifiers.DroughtDaysRemaining = data.DroughtDays;
            session.Modifiers.DemandFactorMul = data.DemandMul;
            session.Modifiers.HeatwaveDaysRemaining = data.HeatwaveDays;
            session.Modifiers.ImportMwAvailable = data.ImportAvailable;
            session.Modifiers.ImportDisruptionDaysRemaining = data.ImportDays;
            session.PrivateReserveMw = data.PrivateReserveMw;
            session.PrivateReserveQuarterlyCost = data.PrivateReserveQuarterlyCost;
            session.EmergencyImportMw = data.EmergencyImportMw;
            session.EmergencyImportDaysRemaining = data.EmergencyImportDaysRemaining;

            session.DaysSinceMajorEvent = data.DaysSinceMajorEvent;
            session.DaysSinceMeterCrash = data.DaysSinceMeterCrash;
            session.MeterCrisisStreak = data.MeterCrisisStreak;
            if (data.FuelCoal > 0.01f) session.FuelMarket.Coal = data.FuelCoal;
            if (data.FuelGas > 0.01f) session.FuelMarket.Gas = data.FuelGas;
            if (data.FuelOil > 0.01f) session.FuelMarket.Oil = data.FuelOil;
            if (data.FuelUranium > 0.01f) session.FuelMarket.Uranium = data.FuelUranium;
            session.FuelPriceIndex = session.FuelMarket.BlendedIndex;
            session.Mandate.AdequacyGoodStreak = data.AdequacyStreak;
            session.Mandate.AffordabilityGoodStreak = data.AffordStreak;
            session.Mandate.BestAdequacyStreak = data.BestAdequacyStreak;
            session.Mandate.BestAffordabilityStreak = data.BestAffordStreak;
            session.Mandate.RestoreSpark(data.MandateSparkSamples, data.MandateDaysSinceSample);
            session.SelectedRegion = (RegionId)data.SelectedRegion;
            session.CurrentWeather = (WeatherKind)data.CurrentWeather;

            session.Cabinet.TariffFreezeActive = data.TariffFreezeActive;
            session.Cabinet.TariffFreezeDaysRemaining = data.TariffFreezeDays;
            session.Cabinet.EmergencyFossilActive = data.EmergencyFossilActive;
            session.Cabinet.EmergencyFossilDaysRemaining = data.EmergencyFossilDays;
            session.Modifiers.ExtraFirmMw = session.PrivateReserveMw + session.EmergencyImportMw;
            if (session.Cabinet.EmergencyFossilActive)
                session.Modifiers.ExtraFirmMw += GameSession.EmergencyFossilExtraMw;

            session.LastYearForLedger = data.LastYearForLedger > 0 ? data.LastYearForLedger : data.Year;
            session.YearAdeqSum = data.YearAdeqSum;
            session.YearAffSum = data.YearAffSum;
            session.YearSamples = data.YearSamples;
            session.YearSpend = data.YearSpend;
            session.YearBudgetAnchor = data.YearBudgetAnchor > 0.01f ? data.YearBudgetAnchor : data.Budget;
            session.YearBiggestEvent = string.IsNullOrEmpty(data.YearBiggestEvent) ? "—" : data.YearBiggestEvent;
            session.YearBiggestSev = data.YearBiggestSev;
            session.YearEventCount = data.YearEventCount;

            RebuildPortfolio(session, data);
            RebuildBuilds(session, data);
            RestoreHistory(session, data);
            RestoreActiveEvent(session, data);

            session.IsGameOver = data.IsGameOver;
            session.IsVictory = data.IsVictory;
            if (session.IsGameOver || session.IsVictory)
                session.Clock.SetSpeed(GameSpeed.Paused);

            session.Cabinet.SyncFromMeters(session.Meters, session.EffectiveLobby);
            session.EmitLog($"Loaded save — {session.Scenario.DisplayName}, day {session.Clock.AbsoluteDay}.");
        }

        /// <summary>
        /// Prefer saved catalog id order when entries resolve against the available catalog;
        /// otherwise keep the full fallback list (v1/v2 or empty id list).
        /// </summary>
        public static List<BuildDefinitionConfig> ResolveCatalog(
            List<BuildDefinitionConfig> available,
            List<string> savedIds)
        {
            if (available == null) available = new List<BuildDefinitionConfig>();
            if (savedIds == null || savedIds.Count == 0) return available;

            var byId = new Dictionary<string, BuildDefinitionConfig>(available.Count);
            for (int i = 0; i < available.Count; i++)
            {
                BuildDefinitionConfig b = available[i];
                if (b == null || string.IsNullOrEmpty(b.Id)) continue;
                byId[b.Id] = b;
            }

            var ordered = new List<BuildDefinitionConfig>(savedIds.Count);
            for (int i = 0; i < savedIds.Count; i++)
            {
                if (byId.TryGetValue(savedIds[i], out BuildDefinitionConfig hit))
                    ordered.Add(hit);
            }

            // If ids were stale / empty after resolve, keep full catalog rather than wipe Construction.
            return ordered.Count > 0 ? ordered : available;
        }

        private static void RebuildPortfolio(GameSession session, GameSaveData data)
        {
            session.Portfolio = new PlantPortfolio();
            for (int i = 0; i < data.Plants.Count; i++)
            {
                PlantSave p = data.Plants[i];
                RegionId region = p.Region >= 0 && p.Region <= 2
                    ? (RegionId)p.Region
                    : RegionCatalog.DefaultRegionForFuel((FuelKind)p.Fuel);
                var plant = new PlantInstance(
                    p.Id, p.DisplayName, (FuelKind)p.Fuel, p.CapacityMw, p.Availability,
                    p.VariableCost, p.OilExposure, p.DefinitionId, p.QuarterlyUpkeep, p.DailyFuelUse, region);
                if (p.Retired) plant.SetRetired(true);
                session.Portfolio.Add(plant);
            }
        }

        private static void RebuildBuilds(GameSession session, GameSaveData data)
        {
            session.Builds = new BuildQueue();
            for (int i = 0; i < data.Builds.Count; i++)
            {
                BuildSave b = data.Builds[i];
                if (b.Cancelled) continue;
                RegionId region = b.Region >= 0 && b.Region <= 2
                    ? (RegionId)b.Region
                    : RegionCatalog.DefaultRegionForFuel((FuelKind)b.Fuel);
                var order = new BuildOrder(
                    b.Id, b.DisplayName, b.DefinitionId, (FuelKind)b.Fuel, b.CapacityMw, b.Availability,
                    b.VariableCost, b.OilExposure, (BuildPaymentMode)b.PaymentMode, b.Upfront, b.Quarterly,
                    b.TotalQuarters, b.Upkeep, b.DailyFuelUse, b.QuartersRemaining, region);
                session.Builds.Enqueue(order);
            }
        }

        private static void RestoreHistory(GameSession session, GameSaveData data)
        {
            if (data.EventHistory == null || data.EventHistory.Count == 0)
            {
                session.History.Clear();
                return;
            }

            var entries = new List<EventHistoryEntry>(data.EventHistory.Count);
            for (int i = 0; i < data.EventHistory.Count; i++)
            {
                EventHistorySave e = data.EventHistory[i];
                if (e == null) continue;
                entries.Add(new EventHistoryEntry
                {
                    Title = e.Title,
                    SeasonTag = e.SeasonTag,
                    DateLabel = e.DateLabel,
                    ChoiceSummary = e.ChoiceSummary
                });
            }

            session.History.Restore(entries);
        }

        private static void RestoreActiveEvent(GameSession session, GameSaveData data)
        {
            if (!data.HasActiveEvent)
            {
                session.ActiveEvent = null;
                return;
            }

            session.ActiveEvent = new PendingEvent
            {
                Kind = (PendingEventKind)data.ActiveEventKind,
                Title = data.ActiveEventTitle,
                Body = data.ActiveEventBody,
                ExposureLabel = data.ActiveEventExposureLabel,
                Exposure01 = data.ActiveEventExposure01,
                Severity01 = data.ActiveEventSeverity01,
                AwaitingDecision = data.ActiveEventAwaiting,
                OffersLoadShed = data.ActiveEventOffersLoadShed
            };
            if (session.ActiveEvent.AwaitingDecision)
                session.Clock.SetSpeed(GameSpeed.Paused);
        }
    }
}
