namespace MinistryOfPower.Simulation
{
    public enum PendingEventKind
    {
        None = 0,
        OilPriceShock = 1,
        Drought = 2,
        Heatwave = 3,
        ImportDisruption = 4,
        MeterCrash = 5,
        ColdSnap = 6,
        StormOutage = 7
    }

    public sealed class PendingEvent
    {
        public PendingEventKind Kind;
        public string Title;
        public string Body;
        public string ExposureLabel;
        public float Exposure01;
        public float Severity01;
        public bool AwaitingDecision;
        /// <summary>True when adequacy meter-crash offers who-goes-dark choices.</summary>
        public bool OffersLoadShed;
    }

    public enum CrisisChoice
    {
        Absorb = 0,
        EmergencyFossil = 1,
        TariffFreeze = 2,
        LoadShedIndustry = 3,
        LoadShedSuburbs = 4,
        LoadShedTransit = 5
    }

    public struct CrisisOutcome
    {
        public string Summary;
        public float BudgetDelta;
        public float AdequacyDelta;
        public float AffordabilityDelta;
        public float TransitionDelta;
        public float ConfidenceDelta;
        public float OilShockMultiplierAdd;
        public int OilShockDays;
        public float DroughtHydroMul;
        public int DroughtDays;
        public float HeatwaveDemandMul;
        public int HeatwaveDays;
        public float ImportRemainingFraction;
        public int ImportDays;
        public bool ApplyOil;
        public bool ApplyDrought;
        public bool ApplyHeatwave;
        public bool ApplyImport;
    }

    public static class CrisisResolver
    {
        public static bool IsLoadShed(CrisisChoice choice)
        {
            return choice == CrisisChoice.LoadShedIndustry
                   || choice == CrisisChoice.LoadShedSuburbs
                   || choice == CrisisChoice.LoadShedTransit;
        }

        public static CrisisOutcome ApplyAbsorb(PendingEvent evt)
        {
            float hit = 6f + evt.Severity01 * 14f;

            switch (evt.Kind)
            {
                case PendingEventKind.OilPriceShock:
                    return new CrisisOutcome
                    {
                        Summary = $"Absorbed: {evt.Title}. Bills and confidence take the punch.",
                        AdequacyDelta = -hit * 0.35f,
                        AffordabilityDelta = -hit * 0.85f,
                        ConfidenceDelta = -hit,
                        ApplyOil = true,
                        OilShockMultiplierAdd = 0.15f + evt.Severity01 * 0.35f,
                        OilShockDays = 12 + (int)(evt.Severity01 * 18f)
                    };

                case PendingEventKind.Drought:
                    return new CrisisOutcome
                    {
                        Summary = $"Absorbed: {evt.Title}. Reservoirs stay low; adequacy headlines bite.",
                        AdequacyDelta = -hit * 0.9f,
                        AffordabilityDelta = -hit * 0.25f,
                        ConfidenceDelta = -hit * 0.85f,
                        ApplyDrought = true,
                        DroughtHydroMul = 0.35f + (1f - evt.Severity01) * 0.25f,
                        DroughtDays = 14 + (int)(evt.Severity01 * 20f)
                    };

                case PendingEventKind.Heatwave:
                    return new CrisisOutcome
                    {
                        Summary = $"Absorbed: {evt.Title}. Peak demand runs hot; seats sweat.",
                        AdequacyDelta = -hit * 0.8f,
                        AffordabilityDelta = -hit * 0.45f,
                        ConfidenceDelta = -hit,
                        ApplyHeatwave = true,
                        HeatwaveDemandMul = 1.12f + evt.Severity01 * 0.18f,
                        HeatwaveDays = 8 + (int)(evt.Severity01 * 12f)
                    };

                case PendingEventKind.ImportDisruption:
                    return new CrisisOutcome
                    {
                        Summary = $"Absorbed: {evt.Title}. Interconnector shortfall hits the brief.",
                        AdequacyDelta = -hit * 0.85f,
                        AffordabilityDelta = -hit * 0.4f,
                        ConfidenceDelta = -hit * 0.9f,
                        ApplyImport = true,
                        ImportRemainingFraction = 0.15f + (1f - evt.Severity01) * 0.35f,
                        ImportDays = 10 + (int)(evt.Severity01 * 14f)
                    };

                case PendingEventKind.MeterCrash:
                    return new CrisisOutcome
                    {
                        Summary = $"Absorbed: {evt.Title}. You own today's numbers — no one else to blame.",
                        AdequacyDelta = -hit * 0.35f,
                        AffordabilityDelta = -hit * 0.35f,
                        ConfidenceDelta = -hit * 1.1f
                    };

                case PendingEventKind.ColdSnap:
                    return new CrisisOutcome
                    {
                        Summary = $"Absorbed: {evt.Title}. Heating load eats the margin.",
                        AdequacyDelta = -hit * 0.85f,
                        AffordabilityDelta = -hit * 0.55f,
                        ConfidenceDelta = -hit,
                        ApplyHeatwave = true,
                        HeatwaveDemandMul = 1.1f + evt.Severity01 * 0.16f,
                        HeatwaveDays = 7 + (int)(evt.Severity01 * 10f)
                    };

                case PendingEventKind.StormOutage:
                    return new CrisisOutcome
                    {
                        Summary = $"Absorbed: {evt.Title}. Renewables and lines take the weather.",
                        AdequacyDelta = -hit * 0.8f,
                        AffordabilityDelta = -hit * 0.3f,
                        ConfidenceDelta = -hit * 0.95f,
                        ApplyDrought = true,
                        DroughtHydroMul = 0.55f + (1f - evt.Severity01) * 0.2f,
                        DroughtDays = 5 + (int)(evt.Severity01 * 8f),
                        ApplyImport = true,
                        ImportRemainingFraction = 0.45f + (1f - evt.Severity01) * 0.3f,
                        ImportDays = 6 + (int)(evt.Severity01 * 8f)
                    };

                default:
                    return new CrisisOutcome
                    {
                        Summary = $"Absorbed: {evt.Title}.",
                        ConfidenceDelta = -hit
                    };
            }
        }

        public static CrisisOutcome ApplyPostpone(CrisisChoice choice, PendingEvent evt, float fossilLobby)
        {
            if (IsLoadShed(choice))
            {
                return ApplyLoadShed(choice, evt);
            }

            switch (choice)
            {
                case CrisisChoice.EmergencyFossil:
                    return new CrisisOutcome
                    {
                        Summary = "Emergency fossil waiver: lights stay on; transition and budget bleed.",
                        BudgetDelta = -18f - evt.Severity01 * 20f,
                        AdequacyDelta = 12f + evt.Severity01 * 8f,
                        AffordabilityDelta = 2f,
                        TransitionDelta = -4f - fossilLobby * 3f,
                        ConfidenceDelta = 3f - fossilLobby * 1.5f,
                        ApplyOil = evt.Kind == PendingEventKind.OilPriceShock,
                        OilShockMultiplierAdd = 0.05f,
                        OilShockDays = 6,
                        ApplyDrought = evt.Kind == PendingEventKind.Drought
                                       || evt.Kind == PendingEventKind.StormOutage,
                        DroughtHydroMul = evt.Kind == PendingEventKind.StormOutage ? 0.65f : 0.55f,
                        DroughtDays = evt.Kind == PendingEventKind.StormOutage ? 5 : 8,
                        ApplyHeatwave = evt.Kind == PendingEventKind.Heatwave
                                        || evt.Kind == PendingEventKind.ColdSnap,
                        HeatwaveDemandMul = evt.Kind == PendingEventKind.ColdSnap ? 1.09f : 1.08f,
                        HeatwaveDays = 5,
                        ApplyImport = evt.Kind == PendingEventKind.ImportDisruption
                                      || evt.Kind == PendingEventKind.StormOutage,
                        ImportRemainingFraction = 0.55f,
                        ImportDays = 6
                    };

                case CrisisChoice.TariffFreeze:
                    return new CrisisOutcome
                    {
                        Summary = "Tariff freeze: bills calm; treasury funds the gap.",
                        BudgetDelta = -25f - evt.Severity01 * 30f,
                        AdequacyDelta = evt.Kind == PendingEventKind.Heatwave
                                        || evt.Kind == PendingEventKind.Drought
                                        || evt.Kind == PendingEventKind.ColdSnap
                                        || evt.Kind == PendingEventKind.StormOutage
                            ? 2f
                            : 0f,
                        AffordabilityDelta = 14f + evt.Severity01 * 6f,
                        TransitionDelta = -1.5f,
                        ConfidenceDelta = 5f,
                        ApplyOil = evt.Kind == PendingEventKind.OilPriceShock,
                        OilShockMultiplierAdd = evt.Severity01 * 0.1f,
                        OilShockDays = 8,
                        ApplyDrought = evt.Kind == PendingEventKind.Drought
                                       || evt.Kind == PendingEventKind.StormOutage,
                        DroughtHydroMul = 0.5f,
                        DroughtDays = 10,
                        ApplyHeatwave = evt.Kind == PendingEventKind.Heatwave
                                        || evt.Kind == PendingEventKind.ColdSnap,
                        HeatwaveDemandMul = 1.1f,
                        HeatwaveDays = 6,
                        ApplyImport = evt.Kind == PendingEventKind.ImportDisruption
                                      || evt.Kind == PendingEventKind.StormOutage,
                        ImportRemainingFraction = 0.4f,
                        ImportDays = 8
                    };

                default:
                    return ApplyAbsorb(evt);
            }
        }

        /// <summary>Ministerial load-shed: who goes dark. Economy vs confidence tradeoffs.</summary>
        public static CrisisOutcome ApplyLoadShed(CrisisChoice choice, PendingEvent evt)
        {
            float sev = evt.Severity01;
            switch (choice)
            {
                case CrisisChoice.LoadShedIndustry:
                    return new CrisisOutcome
                    {
                        Summary = "Load-shed: industry dark. Margins recover; lobby and transition take the bruise.",
                        AdequacyDelta = 10f + sev * 6f,
                        AffordabilityDelta = -1f,
                        TransitionDelta = -2f,
                        ConfidenceDelta = -3f - sev * 2f,
                        BudgetDelta = -4f
                    };

                case CrisisChoice.LoadShedSuburbs:
                    return new CrisisOutcome
                    {
                        Summary = "Load-shed: suburbs dark. Adequacy climbs; confidence bleeds in the evening news.",
                        AdequacyDelta = 9f + sev * 5f,
                        AffordabilityDelta = 1f,
                        TransitionDelta = 0f,
                        ConfidenceDelta = -9f - sev * 4f
                    };

                case CrisisChoice.LoadShedTransit:
                    return new CrisisOutcome
                    {
                        Summary = "Load-shed: transit dark. Grid eases; economy and riders both notice.",
                        AdequacyDelta = 8f + sev * 4f,
                        AffordabilityDelta = -4f - sev * 2f,
                        TransitionDelta = -0.5f,
                        ConfidenceDelta = -5f - sev * 2f,
                        BudgetDelta = -2f
                    };

                default:
                    return ApplyAbsorb(evt);
            }
        }

        public static void ApplyOutcomeToModifiers(CrisisOutcome outcome, DayModifiers mods)
        {
            if (outcome.ApplyOil)
            {
                mods.ApplyOilShock(outcome.OilShockMultiplierAdd, outcome.OilShockDays);
            }

            if (outcome.ApplyDrought)
            {
                mods.ApplyDrought(outcome.DroughtHydroMul, outcome.DroughtDays);
            }

            if (outcome.ApplyHeatwave)
            {
                mods.ApplyHeatwave(outcome.HeatwaveDemandMul, outcome.HeatwaveDays);
            }

            if (outcome.ApplyImport)
            {
                mods.ApplyImportCut(outcome.ImportRemainingFraction, outcome.ImportDays);
            }
        }
    }
}
