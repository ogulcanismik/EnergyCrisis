namespace MinistryOfPower.Simulation
{
    /// <summary>Per-fuel price indices that drift and react to shocks.</summary>
    public sealed class FuelMarket
    {
        public float Coal = 1f;
        public float Gas = 1f;
        public float Oil = 1f;
        public float Uranium = 1f;

        /// <summary>Blended index for DayResolver marginal cost (oil/gas heavy).</summary>
        public float BlendedIndex => Coal * 0.2f + Gas * 0.35f + Oil * 0.35f + Uranium * 0.1f;

        public void TickDay(DeterministicRng rng, DayModifiers mods)
        {
            Coal = Drift(Coal, rng, 0.008f);
            Gas = Drift(Gas, rng, 0.012f);
            Oil = Drift(Oil, rng, 0.015f);
            Uranium = Drift(Uranium, rng, 0.004f);

            if (mods != null && mods.OilShockDaysRemaining > 0)
            {
                float shock = mods.OilShockMultiplier;
                Oil = Clamp(Oil * (0.7f + shock * 0.35f), 0.4f, 3.5f);
                Gas = Clamp(Gas * (0.85f + shock * 0.2f), 0.4f, 3.2f);
                Coal = Clamp(Coal * (0.95f + shock * 0.08f), 0.4f, 2.5f);
            }
        }

        public float IndexFor(FuelKind fuel)
        {
            switch (fuel)
            {
                case FuelKind.Coal: return Coal;
                case FuelKind.Gas: return Gas;
                case FuelKind.Oil: return Oil;
                case FuelKind.Nuclear: return Uranium;
                default: return 1f;
            }
        }

        /// <summary>Upkeep multiplier from fuel markets for fossil/nuclear plants.</summary>
        public float UpkeepMul(FuelKind fuel)
        {
            if (!ResourceStockpile.NeedsStock(fuel)) return 1f;
            return 0.85f + IndexFor(fuel) * 0.15f;
        }

        public string FormatLine()
        {
            return $"Coal {Coal:0.00}  Gas {Gas:0.00}  Oil {Oil:0.00}  U {Uranium:0.00}  · blend {BlendedIndex:0.00}";
        }

        private static float Drift(float v, DeterministicRng rng, float amp)
        {
            return Clamp(v + rng.NextRange(-amp, amp), 0.45f, 3.2f);
        }

        private static float Clamp(float v, float lo, float hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }
    }
}
