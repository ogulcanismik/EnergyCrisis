namespace MinistryOfPower.Simulation
{
    public sealed class PlantInstance
    {
        public string Id { get; }
        public string DisplayName { get; }
        public FuelKind Fuel { get; }
        public float CapacityMw { get; private set; }
        public float Availability { get; private set; }
        public float VariableCostPerMwh { get; private set; }
        public float OilExposure { get; private set; }
        public float QuarterlyUpkeep { get; private set; }
        public float DailyFuelUse { get; private set; }
        public bool IsRetired { get; private set; }
        /// <summary>1 = full output; &lt;1 when fuel stockpile could not cover daily use.</summary>
        public float FuelDerate { get; private set; } = 1f;
        public string DefinitionId { get; }
        public RegionId Region { get; private set; }

        public PlantInstance(
            string id,
            string displayName,
            FuelKind fuel,
            float capacityMw,
            float availability,
            float variableCostPerMwh,
            float oilExposure,
            string definitionId,
            float quarterlyUpkeep = 0f,
            float dailyFuelUse = 0f,
            RegionId region = RegionId.North)
        {
            Id = id;
            DisplayName = displayName;
            Fuel = fuel;
            CapacityMw = capacityMw;
            Availability = MathUtil.Clamp01(availability);
            VariableCostPerMwh = variableCostPerMwh;
            OilExposure = MathUtil.Clamp01(oilExposure);
            DefinitionId = definitionId;
            QuarterlyUpkeep = quarterlyUpkeep;
            DailyFuelUse = dailyFuelUse;
            IsRetired = false;
            FuelDerate = 1f;
            Region = region;
        }

        public void SetRegion(RegionId region) => Region = region;

        public void SetFuelDerate(float derate) => FuelDerate = MathUtil.Clamp01(derate);

        public void ClearFuelDerate() => FuelDerate = 1f;

        public float EffectiveCapacityMw(float weatherFactor)
        {
            if (IsRetired) return 0f;
            float weather = Fuel.IsWeatherSensitive() ? weatherFactor : 1f;
            return CapacityMw * Availability * weather * FuelDerate;
        }

        public void Retire() => IsRetired = true;

        public void SetRetired(bool retired) => IsRetired = retired;
    }
}