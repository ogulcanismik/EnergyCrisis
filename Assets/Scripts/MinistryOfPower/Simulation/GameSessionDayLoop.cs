using System;
using System.Collections.Generic;

namespace MinistryOfPower.Simulation
{
    /// <summary>Day/quarter tick, events, and year roll extracted from GameSession.</summary>
    public static class GameSessionDayLoop
    {
        public static DayReport AdvanceDay(GameSession s)
        {
            if (s.IsGameOver || s.AwaitingCrisisDecision) return s.LastReport;

            bool newQuarter = s.Clock.AdvanceDay();
            if (newQuarter) ResolveQuarter(s);

            s.Modifiers.TickDay();
            s.Cabinet.TickDay();
            if (s.EmergencyImportDaysRemaining > 0)
            {
                s.EmergencyImportDaysRemaining--;
                if (s.EmergencyImportDaysRemaining <= 0) s.EmergencyImportMw = 0f;
            }

            ConsumeFuelsAndApplyShortages(s);
            s.Modifiers.ExtraFirmMw = s.PrivateReserveMw + s.EmergencyImportMw;
            if (s.Cabinet != null && s.Cabinet.EmergencyFossilActive)
                s.Modifiers.ExtraFirmMw += GameSession.EmergencyFossilExtraMw;

            DeterministicRng dayRng = new DeterministicRng(s.Scenario.Seed ^ (s.Clock.AbsoluteDay * 397) ^ 0x5F3759DF);
            s.CurrentWeather = WeatherSampler.SampleKind(s.Clock, dayRng);
            s.FuelMarket.TickDay(dayRng, s.Modifiers);
            s.FuelPriceIndex = s.FuelMarket.BlendedIndex;

            if (s.Clock.Year != s.LastYearForLedger)
            {
                FinalizeYearReport(s, s.LastYearForLedger);
                s.Ledger.OnNewYear();
                s.LastYearForLedger = s.Clock.Year;
                s.ResetYearAccumulator();
                s.YearBudgetAnchor = s.Budget;
            }

            DayReport report = DayResolver.Resolve(
                s.Clock, s.Portfolio, s.Scenario.BaseDemandMw,
                s.Scenario.SolarResource, s.Scenario.WindResource,
                s.FuelPriceIndex, s.Modifiers, s.Budget, s.Meters, s.EffectiveLobby, s.CurrentWeather, dayRng);

            if (s.Cabinet != null && s.Cabinet.TariffFreezeActive)
                report.Affordability = SeatMeters.Clamp(report.Affordability + GameSession.TariffFreezeAffordBoost);

            float conf = report.Confidence;
            if (conf < s.Meters.Confidence)
            {
                float drop = s.Meters.Confidence - conf;
                conf = s.Meters.Confidence - drop * s.Difficulty.ConfidenceDrainMultiplier;
            }

            s.Meters = SeatMeters.Create(report.Adequacy, report.Affordability, report.Transition, conf);
            report.Confidence = s.Meters.Confidence;
            s.LastReport = report;
            s.FillDayCurves(report.DemandMw, report.SupplyMw);
            s.Mandate.TickDay(s.Meters);
            s.Cabinet.SyncFromMeters(s.Meters, s.EffectiveLobby);
            s.YearAdeqSum += s.Meters.Adequacy;
            s.YearAffSum += s.Meters.Affordability;
            s.YearSamples++;
            s.InvokeDayResolved(report);

            if (s.Clock.CurrentSeason == Season.Winter)
                s.RaiseTip("first_winter");

            s.DaysSinceMajorEvent++;
            s.DaysSinceMeterCrash++;

            TrySpawnMajorEvent(s, dayRng);
            if (!s.AwaitingCrisisDecision) TrySpawnMeterCrashCrisis(s);

            s.CheckEndStates();
            return report;
        }

        private static void ResolveQuarter(GameSession s)
        {
            s.Budget += s.QuarterlyIncome;
            float charges = s.Builds.TickQuarter(s.CompletedScratch);
            s.Budget -= charges;
            s.Budget -= s.PrivateReserveQuarterlyCost;

            IReadOnlyList<PlantInstance> plants = s.Portfolio.Plants;
            float upkeep = 0f;
            for (int i = 0; i < plants.Count; i++)
            {
                if (plants[i].IsRetired) continue;
                upkeep += plants[i].QuarterlyUpkeep * s.FuelMarket.UpkeepMul(plants[i].Fuel);
            }

            s.Budget -= upkeep;
            s.Resources.Add(FuelKind.Coal, 18f);
            s.Resources.Add(FuelKind.Gas, 16f);
            s.Resources.Add(FuelKind.Oil, 12f);
            s.Resources.Add(FuelKind.Nuclear, 4f);

            s.Ledger.RecordQuarter(s.QuarterlyIncome, upkeep, charges, s.PrivateReserveQuarterlyCost, 0f);

            for (int i = 0; i < s.CompletedScratch.Count; i++)
            {
                PlantInstance plant = s.CompletedScratch[i];
                string id = s.Portfolio.NextPlantId(plant.DefinitionId);
                s.Portfolio.Add(new PlantInstance(
                    id, plant.DisplayName, plant.Fuel, plant.CapacityMw, plant.Availability,
                    plant.VariableCostPerMwh, plant.OilExposure, plant.DefinitionId,
                    plant.QuarterlyUpkeep, plant.DailyFuelUse, plant.Region));
                s.EmitLog($"{plant.DisplayName} online @ {RegionCatalog.DisplayName(plant.Region)} (+{plant.CapacityMw:0} MW).");
            }

            if (s.Budget < 0f)
            {
                s.ApplyMeterDeltas(0f, -2f, 0f, -3f);
                s.EmitLog("Treasury overdrawn — confidence wobbles.");
            }
        }

        private static void ConsumeFuelsAndApplyShortages(GameSession s)
        {
            s.Portfolio.ClearAllFuelDerates();
            IReadOnlyList<PlantInstance> plants = s.Portfolio.Plants;
            bool shortage = false;
            for (int i = 0; i < plants.Count; i++)
            {
                PlantInstance p = plants[i];
                if (p.IsRetired || p.DailyFuelUse <= 0f || !ResourceStockpile.NeedsStock(p.Fuel))
                    continue;

                if (!s.Resources.TryConsume(p.Fuel, p.DailyFuelUse))
                {
                    p.SetFuelDerate(GameSession.FuelShortageDerate);
                    shortage = true;
                }
            }

            if (shortage)
            {
                s.ApplyMeterDeltas(-2.5f, -1f, 0f, -0.8f);
                s.EmitLog("Fuel stockpile short — plant output constrained.");
            }
        }

        private static void TrySpawnMajorEvent(GameSession s, DeterministicRng rng)
        {
            if (s.ActiveEvent != null || s.DaysSinceMajorEvent < 22) return;

            float chance = s.DaysSinceMajorEvent >= 40 ? 0.55f : 0.08f;
            chance *= 0.85f + 0.3f * s.Difficulty.EventHarshness;
            chance *= s.Difficulty.EventFrequencyMultiplier;
            if (rng.NextFloat01() > chance) return;

            if (!MajorEventSpawner.TrySpawn(s.Portfolio, s.Modifiers, s.Clock, rng, out PendingEvent evt, s.Scenario.EventWeights)
                || evt == null)
            {
                return;
            }

            RaiseEvent(s, evt, autoResolveIfInsured: true);
        }

        private static void TrySpawnMeterCrashCrisis(GameSession s)
        {
            bool inBand = s.Meters.Adequacy < GameSession.MeterCrashAdequacyThreshold
                          || s.Meters.Affordability < GameSession.MeterCrashAffordabilityThreshold
                          || s.LastReport.CrisisTriggered;
            s.MeterCrisisStreak = inBand ? s.MeterCrisisStreak + 1 : 0;
            if (s.ActiveEvent != null || s.DaysSinceMeterCrash < GameSession.MeterCrashCooldownDays) return;
            if (s.MeterCrisisStreak < GameSession.MeterCrashStreakRequired) return;

            PendingEvent evt = MajorEventSpawner.BuildMeterCrash(s.Meters, s.Clock.CurrentSeason);
            s.MeterCrisisStreak = 0;
            RaiseEvent(s, evt, autoResolveIfInsured: false);
        }

        private static void RaiseEvent(GameSession s, PendingEvent evt, bool autoResolveIfInsured)
        {
            s.ActiveEvent = evt;
            s.DaysSinceMajorEvent = 0;
            s.NoteEventForYear(evt);
            s.History.Record(evt, s.Clock, autoResolveIfInsured && !evt.AwaitingDecision ? "shrugged" : "pending");
            s.InvokeEventRaised(s.ActiveEvent);
            s.RaiseTip("first_event");

            if (autoResolveIfInsured && !s.ActiveEvent.AwaitingDecision)
            {
                ApplyInsuredShrug(s, s.ActiveEvent);
                s.EmitLog($"{s.ActiveEvent.Title} — insured portfolio shrugs it off.");
                s.ActiveEvent = null;
            }
            else
            {
                s.Clock.SetSpeed(GameSpeed.Paused);
                s.EmitLog(s.ActiveEvent.OffersLoadShed
                    ? $"IN-TRAY: {s.ActiveEvent.Title} — who goes dark, or burn fossil."
                    : $"IN-TRAY: {s.ActiveEvent.Title} — absorb or postpone.");
            }
        }

        private static void ApplyInsuredShrug(GameSession s, PendingEvent evt)
        {
            switch (evt.Kind)
            {
                case PendingEventKind.OilPriceShock: s.Modifiers.ApplyOilShock(evt.Severity01 * 0.15f, 5); break;
                case PendingEventKind.Drought: s.Modifiers.ApplyDrought(0.75f, 6); break;
                case PendingEventKind.Heatwave: s.Modifiers.ApplyHeatwave(1.05f, 4); break;
                case PendingEventKind.ImportDisruption: s.Modifiers.ApplyImportCut(0.7f, 5); break;
                case PendingEventKind.ColdSnap: s.Modifiers.ApplyHeatwave(1.06f, 4); break;
                case PendingEventKind.StormOutage:
                    s.Modifiers.ApplyDrought(0.8f, 4);
                    s.Modifiers.ApplyImportCut(0.75f, 4);
                    break;
            }
        }

        public static void FinalizeYearReport(GameSession s, int closedYear)
        {
            if (s.YearSamples <= 0 && s.YearEventCount <= 0) return;
            float adeq = s.YearSamples > 0 ? s.YearAdeqSum / s.YearSamples : s.Meters.Adequacy;
            float aff = s.YearSamples > 0 ? s.YearAffSum / s.YearSamples : s.Meters.Affordability;
            float spend = s.YearSpend;
            if (spend < 0.01f && s.YearBudgetAnchor > s.Budget)
                spend = s.YearBudgetAnchor - s.Budget;

            s.LastYearReport = new YearReport
            {
                Year = closedYear,
                AdequacyAvg = adeq,
                AffordAvg = aff,
                CleanPctEnd = s.Meters.Transition,
                Spend = spend,
                NetTreasuryDelta = s.Budget - s.YearBudgetAnchor,
                BiggestEventTitle = s.YearBiggestEvent,
                BiggestEventSeverity = s.YearBiggestSev,
                EventCount = s.YearEventCount
            };
            s.EmitLog("YEAR REPORT: " + closedYear + " closed.");
            s.InvokeYearEnded(s.LastYearReport);
        }
    }
}
