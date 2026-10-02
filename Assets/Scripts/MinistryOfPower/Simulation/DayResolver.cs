namespace MinistryOfPower.Simulation
{
    public static class DayResolver
    {
        public static WeatherSample SampleWeather(int absoluteDay, float solarResource, float windResource, DeterministicRng rng)
        {
            float season = (absoluteDay % GameClock.DaysPerYear) / (float)GameClock.DaysPerYear;
            float summerBias = 0.85f + 0.3f * SinApprox(season * 6.28318f);

            float solar = Clamp(
                (0.55f + 0.35f * summerBias + rng.NextRange(-0.12f, 0.12f)) * solarResource,
                0.15f,
                1.35f);

            float wind = Clamp(
                (0.65f + rng.NextRange(-0.25f, 0.25f)) * windResource,
                0.2f,
                1.3f);

            float hydro = Clamp(0.75f + rng.NextRange(-0.15f, 0.15f), 0.4f, 1.1f);
            float demandMul = Clamp(0.92f + 0.12f * (1f - summerBias) + rng.NextRange(-0.05f, 0.08f), 0.85f, 1.2f);

            return new WeatherSample
            {
                Solar = solar,
                Wind = wind,
                Hydro = hydro,
                DemandMultiplier = demandMul
            };
        }

        /// <param name="visualWeather">Single day weather kind sampled once by GameSession (HUD + resolve share it).</param>
        public static DayReport Resolve(
            GameClock clock,
            PlantPortfolio portfolio,
            float baseDemandMw,
            float solarResource,
            float windResource,
            float fuelPriceIndex,
            DayModifiers modifiers,
            float budget,
            SeatMeters previous,
            float fossilLobbyStrength,
            WeatherKind visualWeather,
            DeterministicRng rng)
        {
            WeatherSample weather = SampleWeather(clock.AbsoluteDay, solarResource, windResource, rng);
            ApplyVisualWeatherToSample(ref weather, visualWeather);

            float hydroFactor = weather.Hydro * (modifiers != null ? modifiers.HydroFactorMul : 1f);
            float demandMul = weather.DemandMultiplier * (modifiers != null ? modifiers.DemandFactorMul : 1f);
            // Oil shock lives in FuelMarket indices only — do not multiply blend again (S1).
            float importMw = modifiers != null ? modifiers.ImportMwAvailable : 0f;
            float extraFirm = modifiers != null ? modifiers.ExtraFirmMw : 0f;

            float demand = baseDemandMw * demandMul;
            float supply = portfolio.SumEffectiveSupply(weather.Solar, weather.Wind, hydroFactor) + importMw + extraFirm;

            float adequacyRaw = demand <= 0.01f ? 100f : (supply / demand) * 100f;
            float adequacy = SeatMeters.Clamp(Lerp(previous.Adequacy, adequacyRaw, 0.55f));

            // Merit-order edge cost; oil exposure multiplier = 1 (market already embeds shock).
            float marginal = portfolio.EstimateMarginalCost(
                fuelPriceIndex, 1f, demand, weather.Solar, weather.Wind, hydroFactor);
            float affordRaw = SeatMeters.Clamp(130f - marginal);
            float affordability = SeatMeters.Clamp(Lerp(previous.Affordability, affordRaw, 0.4f));

            float transition = SeatMeters.Clamp(portfolio.TransitionProgress01() * 100f);

            float confidence = previous.Confidence;
            confidence += ConfidenceDelta(adequacy, affordability, transition, fossilLobbyStrength);
            confidence = SeatMeters.Clamp(confidence);

            bool crisis = adequacy < 45f || affordability < 40f;
            string brief = BuildBrief(adequacy, affordability, transition, supply, demand, crisis, modifiers);

            return new DayReport
            {
                AbsoluteDay = clock.AbsoluteDay,
                DateLabel = clock.FormatDate(),
                DemandMw = demand,
                SupplyMw = supply,
                FuelPriceIndex = fuelPriceIndex,
                SolarFactor = weather.Solar,
                WindFactor = weather.Wind,
                HydroFactor = hydroFactor,
                Adequacy = adequacy,
                Affordability = affordability,
                Transition = transition,
                Confidence = confidence,
                Budget = budget,
                CrisisTriggered = crisis,
                Brief = brief
            };
        }

        private static float ConfidenceDelta(float adequacy, float affordability, float transition, float fossilLobby)
        {
            float delta = 0.02f;

            if (adequacy < 55f) delta -= (55f - adequacy) * 0.12f;
            else if (adequacy > 80f) delta += 0.06f;

            if (affordability < 50f) delta -= (50f - affordability) * 0.1f;
            else if (affordability > 75f) delta += 0.05f;

            if (transition < 25f) delta -= 0.15f;
            else if (transition > 60f) delta += 0.08f;

            if (adequacy < 40f) delta -= 1.2f;
            if (affordability < 35f) delta -= 1.0f;

            if (fossilLobby > 0.6f && transition < 35f)
            {
                delta -= 0.08f * fossilLobby;
            }

            return delta;
        }

        private static string BuildBrief(
            float adequacy,
            float affordability,
            float transition,
            float supply,
            float demand,
            bool crisis,
            DayModifiers modifiers)
        {
            if (crisis)
            {
                if (adequacy < affordability)
                {
                    return $"Adequacy alarm: {supply:0}/{demand:0} MW. Pick who goes dark — or pay for fossil.";
                }

                return "Bill shock. Absorb, freeze tariffs, or burn emergency fuel. Pick your bruise.";
            }

            if (modifiers != null)
            {
                if (modifiers.DroughtDaysRemaining > 0)
                {
                    return "Drought still open. Hydro soft. Don't pretend the reservoirs refilled overnight.";
                }

                if (modifiers.HeatwaveDaysRemaining > 0)
                {
                    return "Heatwave lingering. Evening peaks will try to sack you.";
                }

                if (modifiers.ImportDisruptionDaysRemaining > 0)
                {
                    return "Cable constrained. Domestic margin is the whole brief.";
                }

                if (modifiers.OilShockDaysRemaining > 0)
                {
                    return "Fuel still expensive. Oil-linked units are punching the treasury.";
                }
            }

            if (adequacy < 55f)
            {
                return $"Thin adequacy ({adequacy:0}). Order firm MW or prepare a load-shed.";
            }

            if (affordability < 55f)
            {
                return $"Bill pressure ({affordability:0}). Quiet day is not a free day.";
            }

            if (transition < 30f)
            {
                return "Still fossil-heavy. Quiet desk — fund the long bet before the next card.";
            }

            return "Desk quiet. Portfolio holding. Don't get sentimental.";
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static float SinApprox(float x) => (float)System.Math.Sin(x);

        private static void ApplyVisualWeatherToSample(ref WeatherSample weather, WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Overcast:
                    weather.Solar *= 0.55f;
                    weather.Wind *= 0.9f;
                    break;
                case WeatherKind.Storm:
                    weather.Solar *= 0.35f;
                    weather.Wind *= 1.25f;
                    weather.Hydro *= 1.05f;
                    weather.DemandMultiplier *= 1.04f;
                    break;
                case WeatherKind.HeatHaze:
                    weather.Solar *= 1.1f;
                    weather.DemandMultiplier *= 1.12f;
                    weather.Hydro *= 0.9f;
                    break;
            }
        }
    }
}