using UnityEngine;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.Data
{
    [CreateAssetMenu(menuName = "Ministry of Power/Build Definition", fileName = "Build_")]
    public sealed class BuildDefinition : ScriptableObject
    {
        public string Id = "build";
        public string DisplayName = "Build";
        [TextArea(2, 4)] public string Description;
        public FuelKind ResultFuel = FuelKind.Solar;
        public float ResultCapacityMw = 200f;
        [Range(0f, 1f)] public float ResultAvailability = 0.92f;
        public float ResultVariableCost = 8f;
        [Range(0f, 1f)] public float ResultOilExposure;
        public BuildPaymentMode PaymentMode = BuildPaymentMode.PerQuarter;
        public float UpfrontCost = 10f;
        public float QuarterlyCost = 8f;
        public int DurationQuarters = 6;
        public float QuarterlyUpkeep = 1f;
        public float DailyFuelUse;
        [Tooltip("Extra confidence cost scaled by scenario fossil lobby strength.")]
        public float FossilLobbyConfidencePenalty = 2f;

        public BuildDefinitionConfig ToConfig()
        {
            return new BuildDefinitionConfig
            {
                Id = Id,
                DisplayName = DisplayName,
                Description = Description,
                ResultFuel = ResultFuel,
                ResultCapacityMw = ResultCapacityMw,
                ResultAvailability = ResultAvailability,
                ResultVariableCost = ResultVariableCost,
                ResultOilExposure = ResultOilExposure,
                PaymentMode = PaymentMode,
                UpfrontCost = UpfrontCost,
                QuarterlyCost = QuarterlyCost,
                DurationQuarters = DurationQuarters,
                FossilLobbyConfidencePenalty = FossilLobbyConfidencePenalty,
                QuarterlyUpkeep = QuarterlyUpkeep,
                DailyFuelUse = DailyFuelUse
            };
        }
    }
}
