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

            BuildDefinition buildSolar = CreateBuild("Assets/Data/Builds/Build_Solar.asset", "build_solar", "Utility Solar Park",
                FuelKind.Solar, 180f, 0.95f, 4f, 0f, BuildPaymentMode.PerQuarter, 12f, 7f, 6, 3f);
            BuildDefinition buildNuke = CreateBuild("Assets/Data/Builds/Build_Nuclear.asset", "build_nuclear", "Nuclear Block",
                FuelKind.Nuclear, 400f, 0.92f, 15f, 0f, BuildPaymentMode.PerQuarter, 40f, 14f, 24, 6f);
            BuildDefinition buildStorage = CreateBuild("Assets/Data/Builds/Build_Storage.asset", "build_storage", "Grid Storage",
                FuelKind.Storage, 120f, 0.98f, 2f, 0f, BuildPaymentMode.Upfront, 28f, 0f, 8, 1.5f);

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

            var eventDeck = new System.Collections.Generic.List<EventDefinition> { oilShock, drought, heatwave, importCut };

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
            usa.ImportCapacityMw = 60f;
            usa.StartingAdequacy = 74f;
            usa.StartingAffordability = 70f;
            usa.StartingTransition = 16f;
            usa.StartingConfidence = 72f;
            usa.StartingPlants = new System.Collections.Generic.List<ScenarioPlantEntry>
            {
                Entry(coal, "Midwest Coal Cluster", 420f),
                Entry(gas, "Gulf Gas Fleet", 380f),
                Entry(oil, "Coastal Oil Peak", 120f),
                Entry(solar, "Southwest Solar", 90f),
                Entry(nuclear, "Legacy Nuke", 180f),
                Entry(wind, "Plains Wind", 110f)
            };
            usa.AvailableBuilds = new System.Collections.Generic.List<BuildDefinition> { buildSolar, buildNuke, buildStorage };
            usa.EventDeck = eventDeck;
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
            sun.ImportCapacityMw = 160f;
            sun.StartingAdequacy = 62f;
            sun.StartingAffordability = 58f;
            sun.StartingTransition = 22f;
            sun.StartingConfidence = 68f;
            sun.StartingPlants = new System.Collections.Generic.List<ScenarioPlantEntry>
            {
                Entry(oil, "Imported Oil Units", 160f),
                Entry(gas, "City Gas", 200f),
                Entry(coal, "Aging Coal", 180f),
                Entry(solar, "Desert Solar Parks", 160f),
                Entry(hydro, "River Hydro", 140f)
            };
            sun.AvailableBuilds = new System.Collections.Generic.List<BuildDefinition> { buildSolar, buildNuke, buildStorage };
            sun.EventDeck = eventDeck;
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
            BuildPaymentMode mode, float upfront, float quarterly, int quarters, float lobbyPenalty)
        {
            var asset = LoadOrCreate<BuildDefinition>(path);
            asset.Id = id;
            asset.DisplayName = display;
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

        private static ScenarioPlantEntry Entry(PlantDefinition def, string name, float mw)
        {
            return new ScenarioPlantEntry
            {
                Definition = def,
                OverrideName = name,
                CapacityMwOverride = mw
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
