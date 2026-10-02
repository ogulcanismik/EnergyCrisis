namespace MinistryOfPower.Simulation
{
    public sealed class CabinetState
    {
        public float PmConfidence;
        public float ClimateMandatePressure;
        public float IndustryPressure;
        public float AffordabilityMandate;
        public string ActiveMandate = "Keep the lights on and bills tolerable.";
        public bool TariffFreezeActive;
        public int TariffFreezeDaysRemaining;
        public bool EmergencyFossilActive;
        public int EmergencyFossilDaysRemaining;

        public static CabinetState FromMeters(SeatMeters meters, float fossilLobby)
        {
            return new CabinetState
            {
                PmConfidence = meters.Confidence,
                ClimateMandatePressure = SeatMeters.Clamp(100f - meters.Transition),
                IndustryPressure = SeatMeters.Clamp(40f + fossilLobby * 40f),
                AffordabilityMandate = SeatMeters.Clamp(100f - meters.Affordability),
                ActiveMandate = meters.Transition < 30f
                    ? "PM wants a visible transition milestone this year."
                    : "Hold the seat: adequacy first, then affordability."
            };
        }

        public void SyncFromMeters(SeatMeters meters, float fossilLobby)
        {
            PmConfidence = meters.Confidence;
            ClimateMandatePressure = SeatMeters.Clamp(100f - meters.Transition);
            IndustryPressure = SeatMeters.Clamp(40f + fossilLobby * 40f);
            AffordabilityMandate = SeatMeters.Clamp(100f - meters.Affordability);
            ActiveMandate = meters.Transition < 30f
                ? "PM wants a visible transition milestone this year."
                : "Hold the seat: adequacy first, then affordability.";
        }

        public void TickDay()
        {
            if (TariffFreezeDaysRemaining > 0)
            {
                TariffFreezeDaysRemaining--;
                if (TariffFreezeDaysRemaining <= 0) TariffFreezeActive = false;
            }

            if (EmergencyFossilDaysRemaining > 0)
            {
                EmergencyFossilDaysRemaining--;
                if (EmergencyFossilDaysRemaining <= 0) EmergencyFossilActive = false;
            }
        }
    }
}
