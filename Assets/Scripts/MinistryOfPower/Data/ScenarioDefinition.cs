using System;
using System.Collections.Generic;
using UnityEngine;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.Data
{
    [Serializable]
    public sealed class ScenarioPlantEntry
    {
        public PlantDefinition Definition;
        public string OverrideName;
        public float CapacityMwOverride;
        [Tooltip("When RegionExplicit, plant is sited in this region instead of DefaultRegionForFuel.")]
        public RegionId Region = RegionId.North;
        public bool RegionExplicit;
    }

    /// <summary>Per-scenario weight override for a deck event kind (ColdSnap/Storm, etc.).</summary>
    [Serializable]
    public sealed class ScenarioEventWeightOverride
    {
        public PendingEventKind Kind;
        [Range(0f, 2f)] public float BaseWeight = 1f;
    }

    [CreateAssetMenu(menuName = "Ministry of Power/Scenario Definition", fileName = "Scenario_")]
    public sealed class ScenarioDefinition : ScriptableObject
    {
        public string Id = "scenario";
        public string DisplayName = "Scenario";
        [TextArea(2, 6)] public string Description;
        [TextArea(1, 3)] public string DifferentiationBlurb;
        public int StartYear = 2026;
        public int Seed = 42;
        public int CampaignYears = MandateTracker.DefaultCampaignYears;
        public float StartingBudget = 100f;
        public float QuarterlyIncome = 12f;
        [Tooltip("Player-facing currency for treasury/build costs (stored as billions of this currency).")]
        public string CurrencyCode = "USD";
        public string CurrencyName = "US dollars";
        public float BaseDemandMw = 1000f;
        public float SolarResource = 1f;
        public float WindResource = 1f;
        [Range(0f, 1f)] public float FossilLobbyStrength = 0.5f;
        [Tooltip("Extra multiplier on fossil-retire confidence hits (factory Federal 1.55 / Sun-Rich 0.55).")]
        public float LobbyRetireMultiplier = 1f;
        [Tooltip("Baseline import / interconnector MW available in the day resolve.")]
        public float ImportCapacityMw = 80f;
        public float StartingAdequacy = 72f;
        public float StartingAffordability = 68f;
        public float StartingTransition = 18f;
        public float StartingConfidence = 70f;
        public List<ScenarioPlantEntry> StartingPlants = new List<ScenarioPlantEntry>();
        public List<BuildDefinition> AvailableBuilds = new List<BuildDefinition>();
        public List<EventDefinition> EventDeck = new List<EventDefinition>();
        [Tooltip("Optional per-kind weight overrides (Federal cold-heavy vs Sun-Rich storm/import-heavy).")]
        public List<ScenarioEventWeightOverride> EventWeightOverrides = new List<ScenarioEventWeightOverride>();

        public ScenarioConfig ToConfig()
        {
            var config = new ScenarioConfig
            {
                Id = Id,
                DisplayName = DisplayName,
                Description = Description,
                DifferentiationBlurb = DifferentiationBlurb ?? "",
                StartYear = StartYear,
                Seed = Seed,
                CampaignYears = CampaignYears > 0 ? CampaignYears : MandateTracker.DefaultCampaignYears,
                StartingBudget = StartingBudget,
                QuarterlyIncome = QuarterlyIncome,
                CurrencyCode = string.IsNullOrEmpty(CurrencyCode) ? DisplayUnits.DefaultCurrencyCode : CurrencyCode,
                CurrencyName = string.IsNullOrEmpty(CurrencyName) ? DisplayUnits.DefaultCurrencyName : CurrencyName,
                BaseDemandMw = BaseDemandMw,
                SolarResource = SolarResource,
                WindResource = WindResource,
                FossilLobbyStrength = FossilLobbyStrength,
                LobbyRetireMultiplier = LobbyRetireMultiplier,
                ImportCapacityMw = ImportCapacityMw,
                StartingAdequacy = StartingAdequacy,
                StartingAffordability = StartingAffordability,
                StartingTransition = StartingTransition,
                StartingConfidence = StartingConfidence
            };

            for (int i = 0; i < EventDeck.Count; i++)
            {
                EventDefinition evt = EventDeck[i];
                if (evt == null) continue;
                float w = evt.BaseWeight > 0f ? evt.BaseWeight : 1f;
                float overridden = FindOverrideWeight(evt.Kind);
                if (overridden > 0f) w = overridden;
                config.EventWeights.Add(new EventWeightConfig
                {
                    Kind = evt.Kind,
                    BaseWeight = w
                });
            }

            for (int i = 0; i < StartingPlants.Count; i++)
            {
                ScenarioPlantEntry entry = StartingPlants[i];
                if (entry?.Definition == null)
                {
                    continue;
                }

                float? cap = entry.CapacityMwOverride > 0f ? entry.CapacityMwOverride : (float?)null;
                config.StartingPlants.Add(entry.Definition.ToSpawn(
                    entry.OverrideName,
                    cap,
                    entry.RegionExplicit ? entry.Region : (RegionId?)null));
            }

            return config;
        }

        public List<BuildDefinitionConfig> ToBuildConfigs()
        {
            var list = new List<BuildDefinitionConfig>(AvailableBuilds.Count);
            for (int i = 0; i < AvailableBuilds.Count; i++)
            {
                if (AvailableBuilds[i] != null)
                {
                    list.Add(AvailableBuilds[i].ToConfig());
                }
            }

            return list;
        }

        private float FindOverrideWeight(PendingEventKind kind)
        {
            if (EventWeightOverrides == null) return 0f;
            for (int i = 0; i < EventWeightOverrides.Count; i++)
            {
                ScenarioEventWeightOverride o = EventWeightOverrides[i];
                if (o != null && o.Kind == kind && o.BaseWeight > 0f)
                    return o.BaseWeight;
            }

            return 0f;
        }
    }
}
