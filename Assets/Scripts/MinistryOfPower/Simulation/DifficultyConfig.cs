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
                        BudgetMultiplier = 1.45f,
                        IncomeMultiplier = 1.25f,
                        EventHarshness = 0.65f,
                        EventFrequencyMultiplier = 0.65f,
                        LobbyPressureMultiplier = 0.6f,
                        ConfidenceDrainMultiplier = 0.65f,
                        StartingConfidenceBonus = 10f,
                        Blurb = "Fat treasury (+45%), rarer/softer shocks, gentler lobby & confidence drain."
                    };
                case DifficultyId.Hard:
                    return new DifficultyConfig
                    {
                        Id = DifficultyId.Hard,
                        DisplayName = "Hard",
                        BudgetMultiplier = 0.7f,
                        IncomeMultiplier = 0.8f,
                        EventHarshness = 1.45f,
                        EventFrequencyMultiplier = 1.45f,
                        LobbyPressureMultiplier = 1.5f,
                        ConfidenceDrainMultiplier = 1.45f,
                        StartingConfidenceBonus = -10f,
                        Blurb = "Thin treasury (−30%), frequent harsh shocks, angry lobby, fast sack clock."
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
