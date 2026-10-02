namespace MinistryOfPower.Simulation
{
    public enum BuildPaymentMode
    {
        Upfront = 0,
        PerQuarter = 1
    }

    public sealed class BuildOrder
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string DefinitionId { get; }
        public FuelKind ResultFuel { get; }
        public float ResultCapacityMw { get; }
        public float ResultAvailability { get; }
        public float ResultVariableCost { get; }
        public float ResultOilExposure { get; }
        public float ResultQuarterlyUpkeep { get; }
        public float ResultDailyFuelUse { get; }
        public BuildPaymentMode PaymentMode { get; }
        public float UpfrontCost { get; }
        public float QuarterlyCost { get; }
        public int TotalQuarters { get; }
        public int QuartersRemaining { get; private set; }
        public RegionId Region { get; }
        public bool IsComplete => QuartersRemaining <= 0;
        public bool IsCancelled { get; private set; }

        public BuildOrder(
            string id,
            string displayName,
            string definitionId,
            FuelKind resultFuel,
            float resultCapacityMw,
            float resultAvailability,
            float resultVariableCost,
            float resultOilExposure,
            BuildPaymentMode paymentMode,
            float upfrontCost,
            float quarterlyCost,
            int totalQuarters,
            float resultQuarterlyUpkeep = 0f,
            float resultDailyFuelUse = 0f,
            int quartersRemainingOverride = -1,
            RegionId region = RegionId.North)
        {
            Id = id;
            DisplayName = displayName;
            DefinitionId = definitionId;
            ResultFuel = resultFuel;
            ResultCapacityMw = resultCapacityMw;
            ResultAvailability = resultAvailability;
            ResultVariableCost = resultVariableCost;
            ResultOilExposure = resultOilExposure;
            ResultQuarterlyUpkeep = resultQuarterlyUpkeep;
            ResultDailyFuelUse = resultDailyFuelUse;
            PaymentMode = paymentMode;
            UpfrontCost = upfrontCost;
            QuarterlyCost = quarterlyCost;
            TotalQuarters = totalQuarters;
            QuartersRemaining = quartersRemainingOverride >= 0 ? quartersRemainingOverride : totalQuarters;
            Region = region;
        }

        public float TickQuarter()
        {
            if (IsCancelled || IsComplete) return 0f;
            float charge = PaymentMode == BuildPaymentMode.PerQuarter ? QuarterlyCost : 0f;
            QuartersRemaining--;
            return charge;
        }

        public void Cancel()
        {
            IsCancelled = true;
            QuartersRemaining = 0;
        }

        public PlantInstance ToPlant(string plantId)
        {
            return new PlantInstance(
                plantId,
                DisplayName,
                ResultFuel,
                ResultCapacityMw,
                ResultAvailability,
                ResultVariableCost,
                ResultOilExposure,
                DefinitionId,
                ResultQuarterlyUpkeep,
                ResultDailyFuelUse,
                Region);
        }
    }
}
