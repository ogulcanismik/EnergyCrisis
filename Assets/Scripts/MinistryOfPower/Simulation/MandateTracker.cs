using System.Collections.Generic;
using System.Text;

namespace MinistryOfPower.Simulation
{
    /// <summary>Campaign mandate / victory progress (default 20y campaign).</summary>
    public sealed class MandateTracker
    {
        public const int DefaultCampaignYears = 20;
        public const float WinTransition = 70f;
        public const float WinAffordability = 55f;
        public const float WinAdequacy = 60f;
        public const int StreakGoodDays = 1;
        public const int SparkSampleCap = 48;

        public int CampaignYears = DefaultCampaignYears;
        public int AdequacyGoodStreak;
        public int AffordabilityGoodStreak;
        public int BestAdequacyStreak;
        public int BestAffordabilityStreak;

        private readonly List<float> _cleanSamples = new List<float>(SparkSampleCap);
        private int _daysSinceSample;

        public IReadOnlyList<float> CleanSamples => _cleanSamples;

        public void TickDay(SeatMeters m)
        {
            if (m.Adequacy >= 60f)
            {
                AdequacyGoodStreak++;
                if (AdequacyGoodStreak > BestAdequacyStreak) BestAdequacyStreak = AdequacyGoodStreak;
            }
            else AdequacyGoodStreak = 0;

            if (m.Affordability >= 55f)
            {
                AffordabilityGoodStreak++;
                if (AffordabilityGoodStreak > BestAffordabilityStreak) BestAffordabilityStreak = AffordabilityGoodStreak;
            }
            else AffordabilityGoodStreak = 0;

            _daysSinceSample++;
            if (_daysSinceSample >= GameClock.DaysPerYear / 4 || _cleanSamples.Count == 0)
            {
                _daysSinceSample = 0;
                PushSample(m.Transition);
            }
        }

        public void PushSample(float cleanPct)
        {
            _cleanSamples.Add(cleanPct);
            while (_cleanSamples.Count > SparkSampleCap)
                _cleanSamples.RemoveAt(0);
        }

        public int YearsRemaining(GameClock clock, int startYear)
        {
            int end = startYear + CampaignYears;
            int left = end - clock.Year;
            return left < 0 ? 0 : left;
        }

        public float YearsElapsed01(GameClock clock, int startYear)
        {
            float elapsed = clock.Year - startYear + clock.DayIndex / (float)GameClock.DaysPerYear;
            if (elapsed < 0f) elapsed = 0f;
            return Clamp01(elapsed / CampaignYears);
        }

        public bool MeetsWinMeters(SeatMeters m)
        {
            return m.Transition >= WinTransition
                   && m.Affordability >= WinAffordability
                   && m.Adequacy >= WinAdequacy;
        }

        /// <summary>Win when campaign years elapsed and meters held.</summary>
        public bool CheckVictory(GameClock clock, int startYear, SeatMeters m)
        {
            return clock.Year >= startYear + CampaignYears && MeetsWinMeters(m);
        }

        /// <summary>0..1 composite for HUD (transition weight highest).</summary>
        public float Progress01(SeatMeters m, GameClock clock, int startYear)
        {
            float t = Clamp01(m.Transition / WinTransition);
            float aff = Clamp01(m.Affordability / WinAffordability);
            float adeq = Clamp01(m.Adequacy / WinAdequacy);
            float time = YearsElapsed01(clock, startYear);
            return Clamp01(t * 0.45f + aff * 0.2f + adeq * 0.15f + time * 0.2f);
        }

        public string FormatHud(GameSession s)
        {
            var sb = new StringBuilder(160);
            int yrLeft = YearsRemaining(s.Clock, s.Scenario.StartYear);
            sb.Append("MANDATE  ").Append(yrLeft).Append('y')
                .Append("  ·  clean ").Append(s.Meters.Transition.ToString("0")).Append('%')
                .Append('/').Append(WinTransition.ToString("0"))
                .Append("  ·  streak A").Append(AdequacyGoodStreak)
                .Append("/F").Append(AffordabilityGoodStreak)
                .Append("  ·  win ").Append((Progress01(s.Meters, s.Clock, s.Scenario.StartYear) * 100f).ToString("0"))
                .Append('%');
            return sb.ToString();
        }

        public string FormatPanel(GameSession s)
        {
            var sb = new StringBuilder(640);
            int endYear = s.Scenario.StartYear + CampaignYears;
            sb.Append("Campaign ").Append(s.Scenario.StartYear).Append('–').Append(endYear).Append('\n');
            sb.Append("Years left: ").Append(YearsRemaining(s.Clock, s.Scenario.StartYear)).Append('\n');
            sb.Append("Win needs: Transition ≥").Append(WinTransition.ToString("0"))
                .Append(" · Aff ≥").Append(WinAffordability.ToString("0"))
                .Append(" · Adeq ≥").Append(WinAdequacy.ToString("0"))
                .Append(" at year ").Append(endYear).Append('\n');
            sb.Append("Now: T ").Append(s.Meters.Transition.ToString("0.0"))
                .Append("  Aff ").Append(s.Meters.Affordability.ToString("0.0"))
                .Append("  Adeq ").Append(s.Meters.Adequacy.ToString("0.0")).Append('\n');
            sb.Append("Good streaks (days): Adeq ").Append(AdequacyGoodStreak)
                .Append(" (best ").Append(BestAdequacyStreak).Append(')')
                .Append(" · Aff ").Append(AffordabilityGoodStreak)
                .Append(" (best ").Append(BestAffordabilityStreak).Append(")\n");
            sb.Append("Lose: Confidence → 0 (sack). Mandate: ").Append(s.Cabinet.ActiveMandate);
            sb.Append("\n\nClean% sparkline (quarterly):\n").Append(FormatSparkline());
            return sb.ToString();
        }

        public string FormatSparkline()
        {
            if (_cleanSamples.Count == 0) return "· (no samples yet — play a few quarters)";
            const string bars = "▁▂▃▄▅▆▇█";
            var sb = new StringBuilder(_cleanSamples.Count + 24);
            float min = 100f, max = 0f;
            for (int i = 0; i < _cleanSamples.Count; i++)
            {
                float v = _cleanSamples[i];
                if (v < min) min = v;
                if (v > max) max = v;
            }

            float span = max - min;
            if (span < 1f) span = 1f;
            for (int i = 0; i < _cleanSamples.Count; i++)
            {
                int idx = (int)((_cleanSamples[i] - min) / span * (bars.Length - 1));
                if (idx < 0) idx = 0;
                if (idx >= bars.Length) idx = bars.Length - 1;
                sb.Append(bars[idx]);
            }

            sb.Append("  ").Append(_cleanSamples[0].ToString("0")).Append("→")
                .Append(_cleanSamples[_cleanSamples.Count - 1].ToString("0")).Append('%');
            return sb.ToString();
        }

        private static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }
    }
}
