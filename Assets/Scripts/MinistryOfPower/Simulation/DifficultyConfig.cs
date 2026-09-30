namespace MinistryOfPower.Simulation
{
    public enum DifficultyId
    {
        Easy = 0,
        Normal = 1,
        Hard = 2
    }

    public sealed class DifficultyConfig
    {
        public DifficultyId Id = DifficultyId.Normal;
        public string DisplayName = "Normal";
        public float BudgetMultiplier = 1f;
        public float IncomeMultiplier = 1f;
        public float EventHarshness = 1f;
        public float EventFrequencyMultiplier = 1f;
        public float LobbyPressureMultiplier = 1f;
        public float ConfidenceDrainMultiplier = 1f;
        public float StartingConfidenceBonus;
        public string Blurb = "Balanced ministry pressure.";

        public static DifficultyConfig Create(DifficultyId id)
        {
            switch (id)
            {
                case DifficultyId.Easy:
                    return new DifficultyConfig
                    {
                        Id = DifficultyId.Easy,
                        DisplayName = "Easy",
                        BudgetMultiplier = 1.65f,
                        IncomeMultiplier = 1.35f,
                        EventHarshness = 0.5f,
                        EventFrequencyMultiplier = 0.48f,
                        LobbyPressureMultiplier = 0.45f,
                        ConfidenceDrainMultiplier = 0.45f,
                        StartingConfidenceBonus = 16f,
                        Blurb = "Fat treasury (+65%), soft/rare shocks — first years are survivable learning space."
                    };
                case DifficultyId.Hard:
                    return new DifficultyConfig
                    {
                        Id = DifficultyId.Hard,
                        DisplayName = "Hard",
                        BudgetMultiplier = 0.62f,
                        IncomeMultiplier = 0.72f,
                        EventHarshness = 1.6f,
                        EventFrequencyMultiplier = 1.55f,
                        LobbyPressureMultiplier = 1.65f,
                        ConfidenceDrainMultiplier = 1.55f,
                        StartingConfidenceBonus = -14f,
                        Blurb = "Thin cash (−38%), loud shocks, vicious lobby — mistakes compound fast."
                    };
                default:
                    return new DifficultyConfig
                    {
                        Id = DifficultyId.Normal,
                        DisplayName = "Normal",
                        Blurb = "Baseline budget, monthly-ish shocks, standard lobby pressure."
                    };
            }
        }

        public string FormatSummary(float baseBudget)
        {
            return $"{DisplayName}: start treasury ~{(baseBudget * BudgetMultiplier):0}, " +
                   $"events ×{EventFrequencyMultiplier:0.00} / harsh ×{EventHarshness:0.00}. {Blurb}";
        }
    }
}
