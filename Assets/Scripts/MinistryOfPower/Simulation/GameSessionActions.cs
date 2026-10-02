using System;

namespace MinistryOfPower.Simulation
{
    /// <summary>Player verbs (builds, retire, deals, cabinet) extracted from GameSession.</summary>
    public static class GameSessionActions
    {
        public static bool TryStartBuild(GameSession s, string buildDefinitionId, out string message)
        {
            BuildDefinitionConfig def = s.FindBuild(buildDefinitionId);
            if (def == null) { message = "Unknown build."; return false; }
            if (s.AwaitingCrisisDecision) { message = "Resolve the crisis on your desk first."; return false; }

            float dueNow = def.UpfrontCost;
            if (s.Budget < dueNow)
            {
                message = "Need " + DisplayUnits.Money(dueNow) + " (have " + DisplayUnits.Money(s.Budget) + ").";
                return false;
            }

            s.Budget -= dueNow;
            s.NoteSpend(dueNow);
            RegionId site = s.SelectedRegion;
            s.Builds.Enqueue(new BuildOrder(
                s.Builds.NextOrderId(),
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

            float confidenceHit = def.FossilLobbyConfidencePenalty * s.EffectiveLobby;
            if (confidenceHit > 0f) s.ApplyMeterDeltas(0f, 0f, 0f, -confidenceHit);

            message = $"Ordered {def.DisplayName} @ {RegionCatalog.DisplayName(site)} — {def.DurationQuarters}q.";
            s.EmitLog(message);
            s.RaiseTip("first_build");
            return true;
        }

        public static void SetSelectedRegion(GameSession s, RegionId id)
        {
            s.SelectedRegion = id;
            s.EmitLog($"Region focus: {RegionCatalog.DisplayName(id)} — new builds site here; retire prefers local fossils.");
        }

        public static bool TryCancelBuild(GameSession s, string orderId, out string message)
        {
            if (s.AwaitingCrisisDecision) { message = "Resolve the crisis first."; return false; }
            if (string.IsNullOrEmpty(orderId))
            {
                var orders = s.Builds.Orders;
                for (int i = 0; i < orders.Count; i++)
                {
                    if (!orders[i].IsCancelled && !orders[i].IsComplete)
                    {
                        orderId = orders[i].Id;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(orderId) || !s.Builds.TryCancel(orderId))
            {
                message = "No build to cancel.";
                return false;
            }

            message = "Build cancelled. Progress forfeited — treasury already spent the optimism.";
            s.EmitLog(message);
            return true;
        }

        public static bool TryRetirePlant(GameSession s, string plantId, out string message)
        {
            if (s.AwaitingCrisisDecision) { message = "Resolve the crisis on your desk first."; return false; }

            if (string.IsNullOrEmpty(plantId))
            {
                PlantInstance pick = null;
                var plants = s.Portfolio.Plants;
                for (int i = 0; i < plants.Count; i++)
                {
                    if (plants[i].IsRetired || !plants[i].Fuel.IsFossil()) continue;
                    if (plants[i].Region == s.SelectedRegion) { pick = plants[i]; break; }
                    if (pick == null) pick = plants[i];
                }

                if (pick == null) { message = "No fossil plant to retire."; return false; }
                plantId = pick.Id;
            }

            if (!s.Portfolio.TryRetire(plantId, out PlantInstance plant))
            {
                message = "Plant not found or already retired.";
                return false;
            }

            float lobbyHit = plant.Fuel.IsFossil()
                ? (4f + s.EffectiveLobby * 8f) * s.Difficulty.LobbyPressureMultiplier * s.Scenario.LobbyRetireMultiplier
                : 1.5f;
            s.ApplyMeterDeltas(0f, 0f, 0f, -lobbyHit);
            message = "Retired " + plant.DisplayName + " (" + RegionCatalog.DisplayName(plant.Region) +
                      "). Lobby −" + lobbyHit.ToString("0.0") + " conf pts.";
            s.EmitLog(message);
            return true;
        }

        public static bool TryBuyEmergencyImport(GameSession s, out string message)
        {
            if (s.AwaitingCrisisDecision) { message = "Resolve the crisis first."; return false; }
            const float mw = 80f;
            float cost = 22f + s.Difficulty.EventHarshness * 6f;
            if (s.Budget < cost)
            {
                message = "Emergency import needs " + DisplayUnits.Money(cost) + ".";
                return false;
            }

            s.Budget -= cost;
            s.NoteSpend(cost);
            s.EmergencyImportMw = Math.Max(s.EmergencyImportMw, mw);
            s.EmergencyImportDaysRemaining = Math.Max(s.EmergencyImportDaysRemaining, 10);
            s.ApplyMeterDeltas(4f, -1f, 0f, 1.5f);
            message = "Emergency import +" + DisplayUnits.Capacity(mw) + " for " + s.EmergencyImportDaysRemaining +
                      "d (−" + DisplayUnits.Money(cost) + ").";
            s.EmitLog(message);
            return true;
        }

        public static bool TrySignPrivateReserveDeal(GameSession s, out string message)
        {
            if (s.AwaitingCrisisDecision) { message = "Resolve the crisis first."; return false; }
            if (s.PrivateReserveMw >= 200f)
            {
                message = "Private reserve already at grey-box cap (" + DisplayUnits.Capacity(200f) + ").";
                return false;
            }

            const float mw = 60f;
            float upfront = 18f * s.Difficulty.BudgetMultiplier;
            float quarterly = 4.5f;
            if (s.Budget < upfront)
            {
                message = $"Reserve deal needs {upfront:0} upfront.";
                return false;
            }

            s.Budget -= upfront;
            s.NoteSpend(upfront);
            s.PrivateReserveMw += mw;
            s.PrivateReserveQuarterlyCost += quarterly;
            s.ApplyMeterDeltas(3f, 0f, -0.5f, 1f);
            message = "Private reserve +" + DisplayUnits.Capacity(mw) + " (−" + DisplayUnits.Money(upfront) +
                      " now, −" + DisplayUnits.MoneyPerQuarter(quarterly) + ").";
            s.EmitLog(message);
            return true;
        }

        public static bool TryCabinetAction(GameSession s, CrisisChoice choice, out string message)
        {
            if (s.AwaitingCrisisDecision)
                return TryResolveCrisis(s, choice, out message);

            if (choice == CrisisChoice.StrategicReserve)
            {
                float cost = 14f * s.Difficulty.BudgetMultiplier;
                if (s.Budget < cost)
                {
                    message = "Strategic reserve fill needs ~" + DisplayUnits.Money(cost) + ".";
                    return false;
                }

                s.Budget -= cost;
                s.NoteSpend(cost);
                s.Resources.Add(FuelKind.Coal, 8f);
                s.Resources.Add(FuelKind.Gas, 8f);
                s.Resources.Add(FuelKind.Oil, 4f);
                s.ApplyMeterDeltas(3f, -1f, 0f, 1f);
                message = $"Strategic reserve filled (−{cost:0} treasury). Stocks up; adequacy +3.";
                s.Cabinet.SyncFromMeters(s.Meters, s.EffectiveLobby);
                s.EmitLog("CABINET: " + message);
                return true;
            }

            if (choice == CrisisChoice.RenewableSubsidy)
            {
                float cost = 18f * s.Difficulty.BudgetMultiplier;
                if (s.Budget < cost)
                {
                    message = "Renewable subsidy needs ~" + DisplayUnits.Money(cost) + ".";
                    return false;
                }

                s.Budget -= cost;
                s.NoteSpend(cost);
                s.ApplyMeterDeltas(0f, 4f, 5f, -2f * s.Difficulty.LobbyPressureMultiplier);
                message = "Renewable subsidy (−" + DisplayUnits.Money(cost) +
                          "). Transition +5, bills +4; lobby bruises confidence.";
                s.Cabinet.SyncFromMeters(s.Meters, s.EffectiveLobby);
                s.EmitLog("CABINET: " + message);
                return true;
            }

            var synthetic = new PendingEvent
            {
                Kind = PendingEventKind.MeterCrash,
                Title = "Cabinet Policy",
                Severity01 = 0.35f * s.Difficulty.EventHarshness,
                AwaitingDecision = true
            };

            CrisisOutcome outcome = choice == CrisisChoice.Absorb
                ? new CrisisOutcome { Summary = "No action taken.", ConfidenceDelta = -1f }
                : CrisisResolver.ApplyPostpone(choice, synthetic, s.EffectiveLobby);

            s.Budget += outcome.BudgetDelta;
            if (outcome.BudgetDelta < 0f) s.NoteSpend(-outcome.BudgetDelta);
            s.ApplyMeterDeltas(outcome.AdequacyDelta, outcome.AffordabilityDelta, outcome.TransitionDelta, outcome.ConfidenceDelta);
            CrisisResolver.ApplyOutcomeToModifiers(outcome, s.Modifiers);

            if (choice == CrisisChoice.TariffFreeze)
            {
                s.Cabinet.TariffFreezeActive = true;
                s.Cabinet.TariffFreezeDaysRemaining = Math.Max(s.Cabinet.TariffFreezeDaysRemaining, 12);
            }
            else if (choice == CrisisChoice.EmergencyFossil)
            {
                s.Cabinet.EmergencyFossilActive = true;
                s.Cabinet.EmergencyFossilDaysRemaining = Math.Max(s.Cabinet.EmergencyFossilDaysRemaining, 10);
            }

            s.Cabinet.SyncFromMeters(s.Meters, s.EffectiveLobby);
            message = outcome.Summary;
            s.EmitLog("CABINET: " + message);
            return true;
        }

        public static bool TryResolveCrisis(GameSession s, CrisisChoice choice, out string message)
        {
            if (s.ActiveEvent == null || !s.ActiveEvent.AwaitingDecision)
            {
                message = "No crisis awaiting decision.";
                return false;
            }

            float sev = s.ActiveEvent.Severity01;
            s.ActiveEvent.Severity01 = MathUtil.Clamp01(sev * s.Difficulty.EventHarshness);

            CrisisOutcome outcome = choice == CrisisChoice.Absorb
                ? CrisisResolver.ApplyAbsorb(s.ActiveEvent)
                : CrisisResolver.ApplyPostpone(choice, s.ActiveEvent, s.EffectiveLobby);

            s.ActiveEvent.Severity01 = sev;
            s.Budget += outcome.BudgetDelta;
            s.ApplyMeterDeltas(outcome.AdequacyDelta, outcome.AffordabilityDelta, outcome.TransitionDelta, outcome.ConfidenceDelta);
            CrisisResolver.ApplyOutcomeToModifiers(outcome, s.Modifiers);

            if (choice == CrisisChoice.TariffFreeze)
            {
                s.Cabinet.TariffFreezeActive = true;
                s.Cabinet.TariffFreezeDaysRemaining = Math.Max(s.Cabinet.TariffFreezeDaysRemaining, 10);
            }
            else if (choice == CrisisChoice.EmergencyFossil)
            {
                s.Cabinet.EmergencyFossilActive = true;
                s.Cabinet.EmergencyFossilDaysRemaining = Math.Max(s.Cabinet.EmergencyFossilDaysRemaining, 8);
            }

            if (s.ActiveEvent.Kind == PendingEventKind.MeterCrash) s.DaysSinceMeterCrash = 0;

            message = outcome.Summary;
            s.History.UpdateLatestChoice(ShortChoice(choice));
            s.EmitLog(message);
            s.ActiveEvent.AwaitingDecision = false;
            s.ActiveEvent = null;
            s.Clock.SetSpeed(GameSpeed.Normal);
            s.Cabinet.SyncFromMeters(s.Meters, s.EffectiveLobby);
            s.CheckEndStates();
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
    }
}
