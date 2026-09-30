namespace MinistryOfPower.Simulation
{
    /// <summary>
    /// Temporary day-loop modifiers from shocks. Tick down each day; DayResolver reads them.
    /// </summary>
    public sealed class DayModifiers
    {
        public float OilShockMultiplier = 1f;
        public int OilShockDaysRemaining;

        public float HydroFactorMul = 1f;
        public int DroughtDaysRemaining;

        public float DemandFactorMul = 1f;
        public int HeatwaveDaysRemaining;

        public float ImportMwAvailable = 0f;
        public float ImportMwBaseline = 0f;
        public int ImportDisruptionDaysRemaining;
        /// <summary>Private reserve + emergency purchases — firm MW added to daily supply.</summary>
        public float ExtraFirmMw;

        public void ConfigureBaselineImports(float importMw)
        {
            ImportMwBaseline = importMw;
            if (ImportDisruptionDaysRemaining <= 0)
            {
                ImportMwAvailable = importMw;
            }
        }

        public void TickDay()
        {
            TickOil();
            TickDrought();
            TickHeatwave();
            TickImport();
        }

        public void ApplyOilShock(float multiplierAdd, int days)
        {
            OilShockMultiplier = System.Math.Max(OilShockMultiplier, 1f + multiplierAdd);
            OilShockDaysRemaining = System.Math.Max(OilShockDaysRemaining, days);
        }

        public void ApplyDrought(float hydroMul, int days)
        {
            HydroFactorMul = System.Math.Min(HydroFactorMul, hydroMul);
            DroughtDaysRemaining = System.Math.Max(DroughtDaysRemaining, days);
        }

        public void ApplyHeatwave(float demandMul, int days)
        {
            DemandFactorMul = System.Math.Max(DemandFactorMul, demandMul);
            HeatwaveDaysRemaining = System.Math.Max(HeatwaveDaysRemaining, days);
        }

        public void ApplyImportCut(float remainingFraction, int days)
        {
            ImportMwAvailable = ImportMwBaseline * remainingFraction;
            ImportDisruptionDaysRemaining = System.Math.Max(ImportDisruptionDaysRemaining, days);
        }

        private void TickOil()
        {
            if (OilShockDaysRemaining <= 0)
            {
                OilShockMultiplier = Approach(OilShockMultiplier, 1f, 0.02f);
                return;
            }

            OilShockDaysRemaining--;
            if (OilShockDaysRemaining <= 0)
            {
                OilShockMultiplier = Approach(OilShockMultiplier, 1f, 0.08f);
            }
        }

        private void TickDrought()
        {
            if (DroughtDaysRemaining <= 0)
            {
                HydroFactorMul = Approach(HydroFactorMul, 1f, 0.04f);
                return;
            }

            DroughtDaysRemaining--;
            if (DroughtDaysRemaining <= 0)
            {
                HydroFactorMul = Approach(HydroFactorMul, 1f, 0.1f);
            }
        }

        private void TickHeatwave()
        {
            if (HeatwaveDaysRemaining <= 0)
            {
                DemandFactorMul = Approach(DemandFactorMul, 1f, 0.04f);
                return;
            }

            HeatwaveDaysRemaining--;
            if (HeatwaveDaysRemaining <= 0)
            {
                DemandFactorMul = Approach(DemandFactorMul, 1f, 0.1f);
            }
        }

        private void TickImport()
        {
            if (ImportDisruptionDaysRemaining <= 0)
            {
                ImportMwAvailable = Approach(ImportMwAvailable, ImportMwBaseline, ImportMwBaseline * 0.08f + 1f);
                return;
            }

            ImportDisruptionDaysRemaining--;
            if (ImportDisruptionDaysRemaining <= 0)
            {
                ImportMwAvailable = Approach(ImportMwAvailable, ImportMwBaseline, ImportMwBaseline * 0.25f + 1f);
            }
        }

        private static float Approach(float current, float target, float step)
        {
            if (current < target) return System.Math.Min(current + step, target);
            return System.Math.Max(current - step, target);
        }
    }
}
