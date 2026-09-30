namespace MinistryOfPower.Simulation
{
    /// <summary>Fuel / resource stockpile for generation upkeep (ministerial stocks, not commodity trading).</summary>
    public sealed class ResourceStockpile
    {
        public float Coal;
        public float Gas;
        public float Oil;
        public float Uranium;

        public static ResourceStockpile CreateDefault()
        {
            return new ResourceStockpile
            {
                Coal = 120f,
                Gas = 100f,
                Oil = 80f,
                Uranium = 40f
            };
        }

        public float Get(FuelKind fuel)
        {
            switch (fuel)
            {
                case FuelKind.Coal: return Coal;
                case FuelKind.Gas: return Gas;
                case FuelKind.Oil: return Oil;
                case FuelKind.Nuclear: return Uranium;
                default: return float.MaxValue;
            }
        }

        public void Add(FuelKind fuel, float amount)
        {
            switch (fuel)
            {
                case FuelKind.Coal: Coal += amount; break;
                case FuelKind.Gas: Gas += amount; break;
                case FuelKind.Oil: Oil += amount; break;
                case FuelKind.Nuclear: Uranium += amount; break;
            }
        }

        public bool TryConsume(FuelKind fuel, float amount)
        {
            if (!NeedsStock(fuel))
            {
                return true;
            }

            float have = Get(fuel);
            if (have < amount)
            {
                return false;
            }

            Add(fuel, -amount);
            return true;
        }

        public static bool NeedsStock(FuelKind fuel)
        {
            return fuel == FuelKind.Coal || fuel == FuelKind.Gas || fuel == FuelKind.Oil || fuel == FuelKind.Nuclear;
        }
    }
}
