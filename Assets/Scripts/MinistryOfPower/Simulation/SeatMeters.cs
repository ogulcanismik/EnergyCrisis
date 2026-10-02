namespace MinistryOfPower.Simulation
{
    public struct SeatMeters
    {
        public float Adequacy;
        public float Affordability;
        public float Transition;
        public float Confidence;

        public static SeatMeters Create(float adequacy, float affordability, float transition, float confidence)
        {
            return new SeatMeters
            {
                Adequacy = Clamp(adequacy),
                Affordability = Clamp(affordability),
                Transition = Clamp(transition),
                Confidence = Clamp(confidence)
            };
        }

        public static float Clamp(float value)
        {
            if (value < 0f) return 0f;
            if (value > 100f) return 100f;
            return value;
        }
    }

    public struct DayReport
    {
        public int AbsoluteDay;
        public string DateLabel;
        public float DemandMw;
        public float SupplyMw;
        public float FuelPriceIndex;
        public float SolarFactor;
        public float WindFactor;
        public float HydroFactor;
        public float Adequacy;
        public float Affordability;
        public float Transition;
        public float Confidence;
        public float Budget;
        public bool CrisisTriggered;
        public string Brief;
    }

    public struct WeatherSample
    {
        public float Solar;
        public float Wind;
        public float Hydro;
        public float DemandMultiplier;
    }
}
