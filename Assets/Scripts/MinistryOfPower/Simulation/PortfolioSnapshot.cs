namespace MinistryOfPower.Simulation
{
    /// <summary>
    /// One-pass fleet aggregates for event weighting and desk exposure scans.
    /// Avoids repeated FuelShare / TotalCapacity walks per spawn.
    /// </summary>
    public readonly struct PortfolioSnapshot
    {
        public readonly float TotalCapacityMw;
        public readonly float OilLinkedShare;
        public readonly float HydroShare;
        public readonly float StorageShare;
        public readonly float FossilShare;
        public readonly float WindShare;
        public readonly float SolarShare;

        public float WeatherShare => WindShare + SolarShare;
        public float PeakerCover => FossilShare + StorageShare;

        public PortfolioSnapshot(
            float totalCapacityMw,
            float oilLinkedShare,
            float hydroShare,
            float storageShare,
            float fossilShare,
            float windShare,
            float solarShare)
        {
            TotalCapacityMw = totalCapacityMw;
            OilLinkedShare = oilLinkedShare;
            HydroShare = hydroShare;
            StorageShare = storageShare;
            FossilShare = fossilShare;
            WindShare = windShare;
            SolarShare = solarShare;
        }

        public float ImportDependence(float importMwBaseline)
        {
            if (importMwBaseline <= 1f) return 0f;
            return importMwBaseline / (TotalCapacityMw + importMwBaseline);
        }
    }
}
