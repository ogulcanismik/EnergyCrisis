namespace MinistryOfPower.Simulation
{
    /// <summary>Single seasonal weather-kind table shared by HUD clock and day resolve.</summary>
    public static class WeatherSampler
    {
        public static WeatherKind SampleKind(GameClock clock, DeterministicRng rng)
        {
            Season season = clock.CurrentSeason;
            float r = rng.NextFloat01();
            if (season == Season.Winter)
            {
                if (r < 0.35f) return WeatherKind.Storm;
                if (r < 0.6f) return WeatherKind.Overcast;
                return WeatherKind.Clear;
            }

            if (season == Season.Summer)
            {
                if (r < 0.2f) return WeatherKind.HeatHaze;
                if (r < 0.35f) return WeatherKind.Overcast;
                return WeatherKind.Clear;
            }

            if (r < 0.25f) return WeatherKind.Overcast;
            if (r < 0.35f) return WeatherKind.Storm;
            return WeatherKind.Clear;
        }
    }
}
