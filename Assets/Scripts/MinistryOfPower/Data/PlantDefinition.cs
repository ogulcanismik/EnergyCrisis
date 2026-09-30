using UnityEngine;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.Data
{
    [CreateAssetMenu(menuName = "Ministry of Power/Plant Definition", fileName = "Plant_")]
    public sealed class PlantDefinition : ScriptableObject
    {
        public string Id = "plant";
        public string DisplayName = "Plant";
        public FuelKind Fuel = FuelKind.Coal;
        public float CapacityMw = 100f;
        [Range(0f, 1f)] public float Availability = 0.9f;
        public float VariableCostPerMwh = 40f;
        [Range(0f, 1f)] public float OilExposure;

        public PlantSpawnConfig ToSpawn(string overrideName = null, float? capacityOverride = null)
        {
            return new PlantSpawnConfig
            {
                DefinitionId = Id,
                DisplayName = string.IsNullOrEmpty(overrideName) ? DisplayName : overrideName,
                Fuel = Fuel,
                CapacityMw = capacityOverride ?? CapacityMw,
                Availability = Availability,
                VariableCostPerMwh = VariableCostPerMwh,
                OilExposure = OilExposure
            };
        }
    }
}
