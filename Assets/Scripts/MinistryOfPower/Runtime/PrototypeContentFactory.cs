using System.Collections.Generic;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.Runtime
{
    /// <summary>
    /// Fallback content: scenarios + full generator catalog for grey-box.
    /// </summary>
    public static class PrototypeContentFactory
    {
        public static void CreateUsaLike(out ScenarioConfig config, out List<BuildDefinitionConfig> builds)
        {
            config = new ScenarioConfig
            {
                Id = "usa_like",
                DisplayName = "Federal High Budget",
                Description = "Fat treasury, fossil-heavy grid, vicious oil lobby. Retire fossils carefully.",
                DifferentiationBlurb =
                    "Treasury +lobby · Solar CF ~1.0 · Imports thin (60 MW) · Retire fossils hurts hard (×1.55 lobby) · Oil-weighted shocks",
                StartYear = 2026,
                Seed = 2026,
                CampaignYears = 20,
                StartingBudget = 210f,
                QuarterlyIncome = 26f,
                BaseDemandMw = 1250f,
                SolarResource = 0.95f,
                WindResource = 1.15f,
                FossilLobbyStrength = 0.95f,
                LobbyRetireMultiplier = 1.55f,
                ImportCapacityMw = 55f,
                StartingAdequacy = 78f,
                StartingAffordability = 74f,
                StartingTransition = 12f,
                StartingConfidence = 76f,
                StartingPlants = new List<PlantSpawnConfig>
                {
                    Spawn("coal", "Midwest Coal Cluster", FuelKind.Coal, 450f, 0.88f, 35f, 0.05f, RegionId.North),
                    Spawn("gas", "Gulf Gas Fleet", FuelKind.Gas, 400f, 0.9f, 48f, 0.7f, RegionId.Coast),
                    Spawn("oil", "Coastal Oil Peak", FuelKind.Oil, 140f, 0.85f, 72f, 1f, RegionId.Coast),
                    Spawn("solar", "Southwest Solar", FuelKind.Solar, 70f, 0.95f, 5f, 0f, RegionId.Desert),
                    Spawn("nuclear", "Legacy Nuke", FuelKind.Nuclear, 200f, 0.92f, 12f, 0f, RegionId.North),
                    Spawn("wind", "Plains Wind", FuelKind.Wind, 100f, 0.9f, 6f, 0f, RegionId.North)
                },
                EventWeights = DefaultDeck(oil: 1.45f, drought: 0.55f, heat: 0.85f, import: 0.4f, cold: 1.35f, storm: 0.9f)
            };
            builds = CreateFullCatalog();
        }

        public static void CreateSunRich(out ScenarioConfig config, out List<BuildDefinitionConfig> builds)
        {
            config = new ScenarioConfig
            {
                Id = "sun_rich",
                DisplayName = "Sun-Rich Low Budget",
                Description = "Tight cash, blistering sun, import-leaning. Soft lobby — retire fossils cheaply.",
                DifferentiationBlurb =
                    "Thin treasury · Solar CF ~1.55 · Imports fat (180 MW) · Soft retire lobby (×0.55) · Drought/heat/import shocks · Easy still playable years 1–2",
                StartYear = 2026,
                Seed = 77,
                CampaignYears = 20,
                StartingBudget = 68f,
                QuarterlyIncome = 10f,
                BaseDemandMw = 760f,
                SolarResource = 1.55f,
                WindResource = 0.85f,
                FossilLobbyStrength = 0.28f,
                LobbyRetireMultiplier = 0.55f,
                ImportCapacityMw = 180f,
                StartingAdequacy = 62f,
                StartingAffordability = 58f,
                StartingTransition = 28f,
                StartingConfidence = 70f,
                StartingPlants = new List<PlantSpawnConfig>
                {
                    Spawn("oil", "Imported Oil Units", FuelKind.Oil, 140f, 0.85f, 80f, 1f, RegionId.Coast),
                    Spawn("gas", "City Gas", FuelKind.Gas, 180f, 0.9f, 52f, 0.65f, RegionId.Coast),
                    Spawn("coal", "Aging Coal", FuelKind.Coal, 150f, 0.88f, 38f, 0.05f, RegionId.North),
                    Spawn("solar", "Desert Solar Parks", FuelKind.Solar, 220f, 0.95f, 4f, 0f, RegionId.Desert),
                    Spawn("hydro", "River Hydro", FuelKind.Hydro, 130f, 0.8f, 8f, 0f, RegionId.North),
                    Spawn("storage", "Coastal Storage", FuelKind.Storage, 60f, 0.98f, 2f, 0f, RegionId.Coast)
                },
                EventWeights = DefaultDeck(oil: 0.75f, drought: 1.35f, heat: 1.25f, import: 1.55f, cold: 1.1f, storm: 1.2f)
            };
            builds = CreateFullCatalog();
        }

        private static List<EventWeightConfig> DefaultDeck(
            float oil, float drought, float heat, float import, float cold = 1f, float storm = 1f)
        {
            return new List<EventWeightConfig>
            {
                new EventWeightConfig { Kind = PendingEventKind.OilPriceShock, BaseWeight = oil },
                new EventWeightConfig { Kind = PendingEventKind.Drought, BaseWeight = drought },
                new EventWeightConfig { Kind = PendingEventKind.Heatwave, BaseWeight = heat },
                new EventWeightConfig { Kind = PendingEventKind.ImportDisruption, BaseWeight = import },
                new EventWeightConfig { Kind = PendingEventKind.ColdSnap, BaseWeight = cold },
                new EventWeightConfig { Kind = PendingEventKind.StormOutage, BaseWeight = storm }
            };
        }

        public static bool TryCreateById(string id, out ScenarioConfig config, out List<BuildDefinitionConfig> builds)
        {
            if (id == "sun_rich")
            {
                CreateSunRich(out config, out builds);
                return true;
            }

            CreateUsaLike(out config, out builds);
            return id == "usa_like" || string.IsNullOrEmpty(id);
        }

        /// <summary>Merge factory catalog into <paramref name="builds"/> by Id (existing wins; no dupes).</summary>
        public static void AppendDefaultBuilds(List<BuildDefinitionConfig> builds)
        {
            if (builds == null) return;
            List<BuildDefinitionConfig> merged = CatalogMerge.Merge(builds, CreateFullCatalog());
            builds.Clear();
            builds.AddRange(merged);
        }

        /// <summary>
        /// Union base/factory catalog with SO preferred builds by Id. Preferred wins; factory fills gaps.
        /// StartNew: <c>builds = PrototypeContentFactory.MergeCatalog(builds, assetBuilds);</c>
        /// </summary>
        public static List<BuildDefinitionConfig> MergeCatalog(
            List<BuildDefinitionConfig> factoryOrBase,
            List<BuildDefinitionConfig> preferredFromSo)
        {
            return CatalogMerge.Merge(preferredFromSo, factoryOrBase);
        }

        public static List<BuildDefinitionConfig> CreateFullCatalog()
        {
            return new List<BuildDefinitionConfig>
            {
                Build("build_coal", "Coal Plant", FuelKind.Coal, 280f, 0.88f, 32f, 0.05f,
                    BuildPaymentMode.PerQuarter, 22f, 9f, 10, 1f, 4f, 0.45f,
                    "Baseload fossil. Cheap power, transition drag."),
                Build("build_gas", "Gas Combined Cycle", FuelKind.Gas, 240f, 0.91f, 42f, 0.65f,
                    BuildPaymentMode.PerQuarter, 20f, 8f, 8, 2f, 3.5f, 0.35f,
                    "Flexible mid-merit. Oil-linked fuel exposure."),
                Build("build_oil", "Oil Peaker", FuelKind.Oil, 100f, 0.85f, 68f, 1f,
                    BuildPaymentMode.Upfront, 16f, 0f, 4, 3f, 2f, 0.55f,
                    "Fast peak insurance. Expensive fuel."),
                Build("build_solar", "Utility Solar Park", FuelKind.Solar, 180f, 0.95f, 4f, 0f,
                    BuildPaymentMode.PerQuarter, 12f, 7f, 6, 3f, 1f, 0f,
                    "Fast clean capacity. Weather-sensitive."),
                Build("build_wind", "Wind Farm", FuelKind.Wind, 160f, 0.9f, 5f, 0f,
                    BuildPaymentMode.PerQuarter, 14f, 7f, 7, 2.5f, 1.2f, 0f,
                    "Clean capacity with wind variance."),
                Build("build_hydro", "Hydro Upgrade", FuelKind.Hydro, 120f, 0.82f, 7f, 0f,
                    BuildPaymentMode.PerQuarter, 18f, 6f, 10, 1.5f, 1.5f, 0f,
                    "Firm renewables; drought risk."),
                Build("build_storage", "Grid Storage", FuelKind.Storage, 120f, 0.98f, 2f, 0f,
                    BuildPaymentMode.Upfront, 28f, 0f, 8, 1.5f, 0.8f, 0f,
                    "Peak insurance; no fuel."),
                Build("build_nuclear", "Nuclear Block", FuelKind.Nuclear, 400f, 0.92f, 15f, 0f,
                    BuildPaymentMode.PerQuarter, 40f, 14f, 24, 6f, 5f, 0.08f,
                    "Long bet (~6y). Uranium upkeep."),
                Build("build_offshore_wind", "Offshore Wind", FuelKind.Wind, 220f, 0.93f, 6f, 0f,
                    BuildPaymentMode.PerQuarter, 26f, 10f, 12, 1.5f, 1.8f, 0f,
                    "High CF coastal wind. Costly, clean, storm-exposed."),
                Build("build_biomass", "Biomass CHP", FuelKind.Biomass, 140f, 0.9f, 28f, 0f,
                    BuildPaymentMode.PerQuarter, 16f, 7f, 8, 1f, 2.4f, 0.25f,
                    "Dispatchable clean-ish heat+power. Feedstock upkeep, no oil link.")
            };
        }

        private static BuildDefinitionConfig Build(
            string id, string name, FuelKind fuel, float mw, float avail, float varCost, float oil,
            BuildPaymentMode mode, float upfront, float quarterly, int quarters, float lobby,
            float upkeep, float dailyFuel, string desc)
        {
            return new BuildDefinitionConfig
            {
                Id = id,
                DisplayName = name,
                ResultFuel = fuel,
                ResultCapacityMw = mw,
                ResultAvailability = avail,
                ResultVariableCost = varCost,
                ResultOilExposure = oil,
                PaymentMode = mode,
                UpfrontCost = upfront,
                QuarterlyCost = quarterly,
                DurationQuarters = quarters,
                FossilLobbyConfidencePenalty = lobby,
                QuarterlyUpkeep = upkeep,
                DailyFuelUse = dailyFuel,
                Description = desc
            };
        }

        private static PlantSpawnConfig Spawn(
            string id, string name, FuelKind fuel, float mw, float avail, float cost, float oil, RegionId region)
        {
            return new PlantSpawnConfig
            {
                DefinitionId = id,
                DisplayName = name,
                Fuel = fuel,
                CapacityMw = mw,
                Availability = avail,
                VariableCostPerMwh = cost,
                OilExposure = oil,
                Region = region,
                RegionExplicit = true
            };
        }
    }
}
