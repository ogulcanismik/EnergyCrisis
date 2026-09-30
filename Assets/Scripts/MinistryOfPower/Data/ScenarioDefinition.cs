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
    }

    [CreateAssetMenu(menuName = "Ministry of Power/Scenario Definition", fileName = "Scenario_")]
    public sealed class ScenarioDefinition : ScriptableObject
    {
        public string Id = "scenario";
        public string DisplayName = "Scenario";
        [TextArea(2, 6)] public string Description;
        public int StartYear = 2026;
        public int Seed = 42;
        public float StartingBudget = 100f;
        public float QuarterlyIncome = 12f;
        public float BaseDemandMw = 1000f;
        public float SolarResource = 1f;
        public float WindResource = 1f;
        [Range(0f, 1f)] public float FossilLobbyStrength = 0.5f;
        [Tooltip("Baseline import / interconnector MW available in the day resolve.")]
        public float ImportCapacityMw = 80f;
        public float StartingAdequacy = 72f;
        public float StartingAffordability = 68f;
        public float StartingTransition = 18f;
        public float StartingConfidence = 70f;
        public List<ScenarioPlantEntry> StartingPlants = new List<ScenarioPlantEntry>();
        public List<BuildDefinition> AvailableBuilds = new List<BuildDefinition>();
        public List<EventDefinition> EventDeck = new List<EventDefinition>();

        public ScenarioConfig ToConfig()
        {
            var config = new ScenarioConfig
            {
                Id = Id,
                DisplayName = DisplayName,
                Description = Description,
                StartYear = StartYear,
                Seed = Seed,
                StartingBudget = StartingBudget,
                QuarterlyIncome = QuarterlyIncome,
                BaseDemandMw = BaseDemandMw,
                SolarResource = SolarResource,
                WindResource = WindResource,
                FossilLobbyStrength = FossilLobbyStrength,
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
                config.EventWeights.Add(new EventWeightConfig
                {
                    Kind = evt.Kind,
                    BaseWeight = evt.BaseWeight > 0f ? evt.BaseWeight : 1f
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
                config.StartingPlants.Add(entry.Definition.ToSpawn(entry.OverrideName, cap));
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
    }
}
