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
        /// <summary>ISO-ish currency code for player-facing money (v1 greybox: USD).</summary>
        public string CurrencyCode = DisplayUnits.DefaultCurrencyCode;
        public string CurrencyName = DisplayUnits.DefaultCurrencyName;
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

        public string FormatCatalogTooltip()
        {
            string fuel = ResourceStockpile.NeedsStock(ResultFuel) ? ResultFuel.ToString() : "none";
            string pay = PaymentMode == BuildPaymentMode.Upfront
                ? "upfront " + DisplayUnits.Money(UpfrontCost)
                : DisplayUnits.Money(UpfrontCost) + " + " + DisplayUnits.MoneyPerQuarter(QuarterlyCost);
            return DisplayName + "\n" + pay + " · " + DisplayUnits.Capacity(ResultCapacityMw) +
                   " · upkeep " + DisplayUnits.MoneyPerQuarter(QuarterlyUpkeep) + "\n" +
                   "Fuel " + fuel + " · " + DurationQuarters + "q build\n" + Description;
        }
    }
}
