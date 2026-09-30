using UnityEditor;
using UnityEngine;
using MinistryOfPower.Data;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.EditorTools
{
    /// <summary>
    /// One-shot content + scene bootstrap for the spirit prototype.
    /// </summary>
    public static class PrototypeBootstrap
    {
        public const string MenuPath = "Ministry of Power/Bootstrap Prototype Content";

        [MenuItem(MenuPath)]
        public static void Bootstrap()
        {
            EnsureFolders();

            PlantDefinition coal = CreatePlant("Assets/Data/Plants/Plant_Coal.asset", "coal", "Coal Plant", FuelKind.Coal, 200f, 0.88f, 36f, 0.05f);
            PlantDefinition gas = CreatePlant("Assets/Data/Plants/Plant_Gas.asset", "gas", "Gas Plant", FuelKind.Gas, 200f, 0.9f, 48f, 0.7f);
            PlantDefinition oil = CreatePlant("Assets/Data/Plants/Plant_Oil.asset", "oil", "Oil Peaker", FuelKind.Oil, 100f, 0.85f, 72f, 1f);
            PlantDefinition solar = CreatePlant("Assets/Data/Plants/Plant_Solar.asset", "solar", "Solar Park", FuelKind.Solar, 100f, 0.95f, 5f, 0f);
            PlantDefinition wind = CreatePlant("Assets/Data/Plants/Plant_Wind.asset", "wind", "Wind Farm", FuelKind.Wind, 100f, 0.9f, 6f, 0f);
            PlantDefinition nuclear = CreatePlant("Assets/Data/Plants/Plant_Nuclear.asset", "nuclear", "Nuclear Unit", FuelKind.Nuclear, 200f, 0.92f, 14f, 0f);
            PlantDefinition hydro = CreatePlant("Assets/Data/Plants/Plant_Hydro.asset", "hydro", "Hydro", FuelKind.Hydro, 80f, 0.8f, 8f, 0f);
            PlantDefinition storagePlant = CreatePlant("Assets/Data/Plants/Plant_Storage.asset", "storage", "Grid Storage", FuelKind.Storage, 60f, 0.98f, 2f, 0f);
            CreatePlant("Assets/Data/Plants/Plant_Biomass.asset", "biomass", "Biomass CHP", FuelKind.Biomass, 100f, 0.9f, 28f, 0f);

            // Full construction catalog (matches PrototypeContentFactory.CreateFullCatalog).
            BuildDefinition buildCoal = CreateBuild("Assets/Data/Builds/Build_Coal.asset", "build_coal", "Coal Plant",
                FuelKind.Coal, 280f, 0.88f, 32f, 0.05f, BuildPaymentMode.PerQuarter, 22f, 9f, 10, 1f, 4f, 0.45f,
                "Baseload fossil. Cheap power, transition drag.");
            BuildDefinition buildGas = CreateBuild("Assets/Data/Builds/Build_Gas.asset", "build_gas", "Gas Combined Cycle",
                FuelKind.Gas, 240f, 0.91f, 42f, 0.65f, BuildPaymentMode.PerQuarter, 20f, 8f, 8, 2f, 3.5f, 0.35f,
                "Flexible mid-merit. Oil-linked fuel exposure.");
            BuildDefinition buildOil = CreateBuild("Assets/Data/Builds/Build_Oil.asset", "build_oil", "Oil Peaker",
                FuelKind.Oil, 100f, 0.85f, 68f, 1f, BuildPaymentMode.Upfront, 16f, 0f, 4, 3f, 2f, 0.55f,
                "Fast peak insurance. Expensive fuel.");
            BuildDefinition buildSolar = CreateBuild("Assets/Data/Builds/Build_Solar.asset", "build_solar", "Utility Solar Park",
                FuelKind.Solar, 180f, 0.95f, 4f, 0f, BuildPaymentMode.PerQuarter, 12f, 7f, 6, 3f, 1f, 0f,
                "Fast clean capacity. Weather-sensitive.");
            BuildDefinition buildWind = CreateBuild("Assets/Data/Builds/Build_Wind.asset", "build_wind", "Wind Farm",
                FuelKind.Wind, 160f, 0.9f, 5f, 0f, BuildPaymentMode.PerQuarter, 14f, 7f, 7, 2.5f, 1.2f, 0f,
                "Clean capacity with wind variance.");
            BuildDefinition buildHydro = CreateBuild("Assets/Data/Builds/Build_Hydro.asset", "build_hydro", "Hydro Upgrade",
                FuelKind.Hydro, 120f, 0.82f, 7f, 0f, BuildPaymentMode.PerQuarter, 18f, 6f, 10, 1.5f, 1.5f, 0f,
                "Firm renewables; drought risk.");
            BuildDefinition buildStorage = CreateBuild("Assets/Data/Builds/Build_Storage.asset", "build_storage", "Grid Storage",
                FuelKind.Storage, 120f, 0.98f, 2f, 0f, BuildPaymentMode.Upfront, 28f, 0f, 8, 1.5f, 0.8f, 0f,
                "Peak insurance; no fuel.");
            BuildDefinition buildNuke = CreateBuild("Assets/Data/Builds/Build_Nuclear.asset", "build_nuclear", "Nuclear Block",
                FuelKind.Nuclear, 400f, 0.92f, 15f, 0f, BuildPaymentMode.PerQuarter, 40f, 14f, 24, 6f, 5f, 0.08f,
                "Long bet (~6y). Uranium upkeep.");
            BuildDefinition buildOffshore = CreateBuild("Assets/Data/Builds/Build_OffshoreWind.asset", "build_offshore_wind", "Offshore Wind",
                FuelKind.Wind, 220f, 0.93f, 6f, 0f, BuildPaymentMode.PerQuarter, 26f, 10f, 12, 1.5f, 1.8f, 0f,
                "High CF coastal wind. Costly, clean, storm-exposed.");
            BuildDefinition buildBiomass = CreateBuild("Assets/Data/Builds/Build_Biomass.asset", "build_biomass", "Biomass CHP",
                FuelKind.Biomass, 140f, 0.9f, 28f, 0f, BuildPaymentMode.PerQuarter, 16f, 7f, 8, 1f, 2.4f, 0.25f,
                "Dispatchable clean-ish heat+power. Feedstock upkeep, no oil link.");

            var fullBuildCatalog = new System.Collections.Generic.List<BuildDefinition>
            {
                buildCoal, buildGas, buildOil, buildSolar, buildWind,
                buildHydro, buildStorage, buildNuke, buildOffshore, buildBiomass
            };

            EventDefinition oilShock = CreateEvent("Assets/Data/Events/Event_OilPriceShock.asset",
                "oil_shock", "Oil Price Shock",
                "Crude spikes. Damage scales with oil/gas exposure in the portfolio.",
                PendingEventKind.OilPriceShock, 1f, 0.15f);
            EventDefinition drought = CreateEvent("Assets/Data/Events/Event_Drought.asset",
                "drought", "Hydro Drought",
                "Dry reservoirs. Damage scales with hydro share.",
                PendingEventKind.Drought, 0.8f, 0.08f);
            EventDefinition heatwave = CreateEvent("Assets/Data/Events/Event_Heatwave.asset",
                "heatwave", "Heatwave Demand Spike",
                "Peak demand surge. Worse with thin peaker/storage cover.",
                PendingEventKind.Heatwave, 0.9f, 0.25f);
            EventDefinition importCut = CreateEvent("Assets/Data/Events/Event_ImportDisruption.asset",
                "import_cut", "Import Disruption",
                "Interconnector fault. Damage scales with import dependence.",
                PendingEventKind.ImportDisruption, 0.75f, 0.08f);
            EventDefinition coldSnap = CreateEvent("Assets/Data/Events/Event_ColdSnap.asset",
                "cold_snap", "Cold Snap",
                "Heating load spikes. Thin firm cover and gas stocks hurt hardest.",
                PendingEventKind.ColdSnap, 1f, 0.2f);
            EventDefinition storm = CreateEvent("Assets/Data/Events/Event_StormOutage.asset",
                "storm_outage", "Storm Outage",
                "Lines and renewables take weather damage; interconnectors wobble.",
                PendingEventKind.StormOutage, 1f, 0.12f);

            var eventDeck = new System.Collections.Generic.List<EventDefinition>
            {
                oilShock, drought, heatwave, importCut, coldSnap, storm
            };

            ScenarioDefinition usa = CreateOrLoadScenario("Assets/Data/Scenarios/Scenario_FederalHighBudget.asset");
            usa.Id = "usa_like";
            usa.DisplayName = "Federal High Budget";
            usa.Description = "High treasury, fossil-heavy grid, strong oil lobby. Easy money, hard politics.";
            usa.StartYear = 2026;
            usa.Seed = 2026;
            usa.StartingBudget = 180f;
            usa.QuarterlyIncome = 22f;
            usa.BaseDemandMw = 1200f;
            usa.SolarResource = 1f;
            usa.WindResource = 1.05f;
            usa.FossilLobbyStrength = 0.85f;
            usa.LobbyRetireMultiplier = 1.55f;
            usa.ImportCapacityMw = 60f;
            usa.CampaignYears = 20;
            usa.DifferentiationBlurb =
                "Treasury +lobby · Solar CF ~1.0 · Imports thin (60 MW) · Retire fossils hurts hard (×1.55 lobby) · Oil-weighted shocks";
            usa.StartingAdequacy = 74f;
            usa.StartingAffordability = 70f;
            usa.StartingTransition = 16f;
            usa.StartingConfidence = 72f;
            usa.StartingPlants = new System.Collections.Generic.List<ScenarioPlantEntry>
            {
                Entry(coal, "Midwest Coal Cluster", 420f, RegionId.North),
                Entry(gas, "Gulf Gas Fleet", 380f, RegionId.Coast),
                Entry(oil, "Coastal Oil Peak", 120f, RegionId.Coast),
                Entry(solar, "Southwest Solar", 90f, RegionId.Desert),
                Entry(nuclear, "Legacy Nuke", 180f, RegionId.North),
                Entry(wind, "Plains Wind", 110f, RegionId.North)
            };
            usa.AvailableBuilds = new System.Collections.Generic.List<BuildDefinition>(fullBuildCatalog);
            usa.EventDeck = eventDeck;
            // Federal: cold-heavy winters, milder storms than Sun-Rich coasts.
            usa.EventWeightOverrides = new System.Collections.Generic.List<ScenarioEventWeightOverride>
            {
                new ScenarioEventWeightOverride { Kind = PendingEventKind.ColdSnap, BaseWeight = 1.35f },
                new ScenarioEventWeightOverride { Kind = PendingEventKind.StormOutage, BaseWeight = 0.9f },
                new ScenarioEventWeightOverride { Kind = PendingEventKind.OilPriceShock, BaseWeight = 1.45f },
                new ScenarioEventWeightOverride { Kind = PendingEventKind.ImportDisruption, BaseWeight = 0.4f }
            };
            EditorUtility.SetDirty(usa);

            ScenarioDefinition sun = CreateOrLoadScenario("Assets/Data/Scenarios/Scenario_SunRichLowBudget.asset");
            sun.Id = "sun_rich";
            sun.DisplayName = "Sun-Rich Low Budget";
            sun.Description = "Tight cash, excellent sun, fragile adequacy. Leapfrog if you can finance it.";
            sun.StartYear = 2026;
            sun.Seed = 77;
            sun.StartingBudget = 70f;
            sun.QuarterlyIncome = 9f;
            sun.BaseDemandMw = 780f;
            sun.SolarResource = 1.45f;
            sun.WindResource = 0.9f;
            sun.FossilLobbyStrength = 0.35f;
            sun.LobbyRetireMultiplier = 0.55f;
            sun.ImportCapacityMw = 160f;
            sun.CampaignYears = 20;
            sun.DifferentiationBlurb =
                "Thin treasury · Solar CF ~1.45 · Imports fat (160 MW) · Soft retire lobby (×0.55) · Drought/heat/import shocks";
            sun.StartingAdequacy = 62f;
            sun.StartingAffordability = 58f;
            sun.StartingTransition = 22f;
            sun.StartingConfidence = 68f;
            sun.StartingPlants = new System.Collections.Generic.List<ScenarioPlantEntry>
            {
                Entry(oil, "Imported Oil Units", 160f, RegionId.Coast),
                Entry(gas, "City Gas", 200f, RegionId.Coast),
                Entry(coal, "Aging Coal", 180f, RegionId.North),
                Entry(solar, "Desert Solar Parks", 160f, RegionId.Desert),
                Entry(hydro, "River Hydro", 140f, RegionId.North),
                Entry(storagePlant, "Coastal Storage", 60f, RegionId.Coast)
            };
            sun.AvailableBuilds = new System.Collections.Generic.List<BuildDefinition>(fullBuildCatalog);
            sun.EventDeck = eventDeck;
            // Sun-Rich: heat/drought/import/storm; cold still present but not Federal-level.
            sun.EventWeightOverrides = new System.Collections.Generic.List<ScenarioEventWeightOverride>
            {
                new ScenarioEventWeightOverride { Kind = PendingEventKind.ColdSnap, BaseWeight = 1.1f },
                new ScenarioEventWeightOverride { Kind = PendingEventKind.StormOutage, BaseWeight = 1.2f },
                new ScenarioEventWeightOverride { Kind = PendingEventKind.Drought, BaseWeight = 1.35f },
                new ScenarioEventWeightOverride { Kind = PendingEventKind.Heatwave, BaseWeight = 1.25f },
                new ScenarioEventWeightOverride { Kind = PendingEventKind.ImportDisruption, BaseWeight = 1.55f }
            };
            EditorUtility.SetDirty(sun);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Ministry of Power: prototype content bootstrapped.");
        }

        private static void EnsureFolders()
        {
            CreateFolder("Assets", "Data");
            CreateFolder("Assets/Data", "Plants");
            CreateFolder("Assets/Data", "Builds");
            CreateFolder("Assets/Data", "Events");
            CreateFolder("Assets/Data", "Scenarios");
            CreateFolder("Assets", "Scripts");
            CreateFolder("Assets/Scripts", "MinistryOfPower");
        }

        private static void CreateFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static PlantDefinition CreatePlant(
            string path, string id, string display, FuelKind fuel, float mw, float avail, float cost, float oil)
        {
            var asset = LoadOrCreate<PlantDefinition>(path);
            asset.Id = id;
            asset.DisplayName = display;
            asset.Fuel = fuel;
            asset.CapacityMw = mw;
            asset.Availability = avail;
            asset.VariableCostPerMwh = cost;
            asset.OilExposure = oil;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static BuildDefinition CreateBuild(
            string path, string id, string display, FuelKind fuel, float mw, float avail, float cost, float oil,
            BuildPaymentMode mode, float upfront, float quarterly, int quarters, float lobbyPenalty,
            float upkeep, float dailyFuel, string desc)
        {
            var asset = LoadOrCreate<BuildDefinition>(path);
            asset.Id = id;
            asset.DisplayName = display;
            asset.Description = desc;
            asset.ResultFuel = fuel;
            asset.ResultCapacityMw = mw;
            asset.ResultAvailability = avail;
            asset.ResultVariableCost = cost;
            asset.ResultOilExposure = oil;
            asset.PaymentMode = mode;
            asset.UpfrontCost = upfront;
            asset.QuarterlyCost = quarterly;
            asset.DurationQuarters = quarters;
            asset.FossilLobbyConfidencePenalty = lobbyPenalty;
            asset.QuarterlyUpkeep = upkeep;
            asset.DailyFuelUse = dailyFuel;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static EventDefinition CreateEvent(
            string path, string id, string display, string desc, PendingEventKind kind, float weight, float threshold)
        {
            var asset = LoadOrCreate<EventDefinition>(path);
            asset.Id = id;
            asset.DisplayName = display;
            asset.Description = desc;
            asset.Kind = kind;
            asset.BaseWeight = weight;
            asset.CrisisOilShareThreshold = threshold;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static ScenarioDefinition CreateOrLoadScenario(string path)
        {
            return LoadOrCreate<ScenarioDefinition>(path);
        }

        private static ScenarioPlantEntry Entry(PlantDefinition def, string name, float mw, RegionId? region = null)
        {
            return new ScenarioPlantEntry
            {
                Definition = def,
                OverrideName = name,
                CapacityMwOverride = mw,
                Region = region ?? RegionId.North,
                RegionExplicit = region.HasValue
            };
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }

            T created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }
    }
}
