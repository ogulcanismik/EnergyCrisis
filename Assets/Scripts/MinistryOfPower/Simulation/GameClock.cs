namespace MinistryOfPower.Simulation
{
    public enum Season
    {
        Winter = 0,
        Spring = 1,
        Summer = 2,
        Autumn = 3
    }

    public enum GameSpeed
    {
        Paused = 0,
        Slow = 1,       // legacy alias of 1×
        Normal = 2,     // 1×
        Fast = 3,       // 2×
        VeryFast = 4,   // 5× (legacy ordinal kept for saves)
        Fast3 = 5,      // 3×
        Fast4 = 6       // 4×
    }

    /// <summary>
    /// Campaign clock with sub-day fraction for day-night lighting. Day → quarter → year.
    /// </summary>
    public sealed class GameClock
    {
        public const int DaysPerQuarter = 90;
        public const int QuartersPerYear = 4;
        public const int DaysPerYear = DaysPerQuarter * QuartersPerYear;
        public const int DaysPerMonthApprox = 30;
        /// <summary>Real seconds for one in-game day at 1× (HUD Slow/Normal). ~2.8 min; 7s per hour tick.</summary>
        public const float SecondsPerDay1x = 168f;
        public const float HoursPerDay = 24f;

        /// <summary>Active 1× day length (from <see cref="Data.GameTuning"/> or default const).</summary>
        private static float s_secondsPerDay1x = SecondsPerDay1x;
        private static float s_speedMultiplierFast = 2f;
        private static float s_speedMultiplierVeryFast = 5f;

        /// <summary>Effective 1× seconds/day after tuning apply (defaults to <see cref="SecondsPerDay1x"/>).</summary>
        public static float ActiveSecondsPerDay1x => s_secondsPerDay1x > 0f ? s_secondsPerDay1x : SecondsPerDay1x;

        /// <summary>Push pace knobs from GameTuning / GameManager. Pass &lt;=0 to reset to defaults.</summary>
        public static void ApplyPaceTuning(float secondsPerDay1x, float speedMultiplierFast, float speedMultiplierVeryFast)
        {
            s_secondsPerDay1x = secondsPerDay1x > 0f ? secondsPerDay1x : SecondsPerDay1x;
            s_speedMultiplierFast = speedMultiplierFast >= 1f ? speedMultiplierFast : 2f;
            s_speedMultiplierVeryFast = speedMultiplierVeryFast >= 1f ? speedMultiplierVeryFast : 5f;
        }

        public int DayIndex { get; private set; }
        public int AbsoluteDay { get; private set; }
        public int QuarterIndex { get; private set; }
        public int Year { get; private set; }
        /// <summary>0 = midnight start, 0.25 dawn, 0.5 noon, 0.75 dusk, wraps at 1 → new day.</summary>
        public float DayFraction { get; private set; }
        public GameSpeed Speed { get; private set; } = GameSpeed.Paused;
        public bool IsPaused => Speed == GameSpeed.Paused;

        public GameClock(int startYear = 2026)
        {
            Year = startYear;
            AbsoluteDay = 0;
            DayIndex = 0;
            QuarterIndex = 0;
            DayFraction = 0.35f; // morning start
        }

        public void SetSpeed(GameSpeed speed) => Speed = speed;

        public void TogglePause() => Speed = IsPaused ? GameSpeed.Normal : GameSpeed.Paused;

        public int Month
        {
            get
            {
                int dayOfYear = QuarterIndex * DaysPerQuarter + DayIndex;
                return (dayOfYear / DaysPerMonthApprox) % 12 + 1;
            }
        }

        public int DayOfMonth
        {
            get
            {
                int dayOfYear = QuarterIndex * DaysPerQuarter + DayIndex;
                return dayOfYear % DaysPerMonthApprox + 1;
            }
        }

        public Season CurrentSeason
        {
            get
            {
                int m = Month;
                if (m == 12 || m <= 2) return Season.Winter;
                if (m <= 5) return Season.Spring;
                if (m <= 8) return Season.Summer;
                return Season.Autumn;
            }
        }

        public bool IsNight => DayFraction < 0.22f || DayFraction > 0.78f;
        public bool IsDaytime => !IsNight;

        /// <summary>Advances sub-day time. Returns true if a calendar day rolled.</summary>
        public bool AdvanceFraction(float deltaFraction)
        {
            if (deltaFraction <= 0f || IsPaused)
            {
                return false;
            }

            DayFraction += deltaFraction;
            bool rolled = false;
            while (DayFraction >= 1f)
            {
                DayFraction -= 1f;
                AdvanceDayInternal();
                rolled = true;
            }

            return rolled;
        }

        /// <summary>Advances one full in-game day (keeps DayFraction).</summary>
        public bool AdvanceDay()
        {
            AdvanceDayInternal();
            return DayIndex == 0; // quarter rolled when DayIndex reset
        }

        private void AdvanceDayInternal()
        {
            AbsoluteDay++;
            DayIndex++;
            if (DayIndex >= DaysPerQuarter)
            {
                DayIndex = 0;
                QuarterIndex++;
                if (QuarterIndex >= QuartersPerYear)
                {
                    QuarterIndex = 0;
                    Year++;
                }
            }
        }

        public bool DidQuarterRollOnLastDayAdvance => DayIndex == 0 && AbsoluteDay > 0;

        public string FormatDate()
        {
            return $"{Year:D4}-{Month:D2}-{DayOfMonth:D2}";
        }

        public string FormatTimeOfDay()
        {
            int minutes = (int)(DayFraction * 24f * 60f);
            int h = minutes / 60;
            int m = minutes % 60;
            return $"{h:D2}:{m:D2}";
        }

        public void Restore(int year, int absoluteDay, int dayIndex, int quarterIndex, float dayFraction, GameSpeed speed)
        {
            Year = year;
            AbsoluteDay = absoluteDay;
            DayIndex = dayIndex;
            QuarterIndex = quarterIndex;
            DayFraction = dayFraction;
            Speed = speed;
        }

        /// <summary>
        /// Real-time length of one in-game day.
        /// Pause → never ticks; play speeds 1×–5× divide <see cref="ActiveSecondsPerDay1x"/>.
        /// </summary>
        public static float SecondsPerDay(GameSpeed speed)
        {
            float baseDay = ActiveSecondsPerDay1x;
            int mul = PlayMultiplier(speed);
            if (mul <= 0) return float.MaxValue;
            // 2× / 5× still honor GameTuning knobs; 3× / 4× interpolate on the 1× day length.
            if (speed == GameSpeed.Fast) return baseDay / s_speedMultiplierFast;
            if (speed == GameSpeed.VeryFast) return baseDay / s_speedMultiplierVeryFast;
            return baseDay / mul;
        }

        public static float SecondsPerHour(GameSpeed speed) => SecondsPerDay(speed) / HoursPerDay;

        /// <summary>Playable HUD steps: 1×, 2×, 3×, 4×, 5× (excludes pause).</summary>
        public static readonly GameSpeed[] PlaySpeedSteps =
        {
            GameSpeed.Normal,
            GameSpeed.Fast,
            GameSpeed.Fast3,
            GameSpeed.Fast4,
            GameSpeed.VeryFast
        };

        /// <summary>1–5 for play speeds; 0 when paused / unknown.</summary>
        public static int PlayMultiplier(GameSpeed speed)
        {
            switch (speed)
            {
                case GameSpeed.Slow:
                case GameSpeed.Normal: return 1;
                case GameSpeed.Fast: return 2;
                case GameSpeed.Fast3: return 3;
                case GameSpeed.Fast4: return 4;
                case GameSpeed.VeryFast: return 5;
                default: return 0;
            }
        }

        public static string FormatPlaySpeed(GameSpeed speed)
        {
            int mul = PlayMultiplier(speed);
            return mul > 0 ? mul + "x" : "PAUSE";
        }

        /// <summary>Clamp-nudge along 1×…5×. From pause, + goes to 1×; − stays paused.</summary>
        public static GameSpeed NudgePlaySpeed(GameSpeed current, int delta)
        {
            if (delta == 0) return current;
            if (current == GameSpeed.Paused)
                return delta > 0 ? GameSpeed.Normal : GameSpeed.Paused;

            int idx = 0;
            for (int i = 0; i < PlaySpeedSteps.Length; i++)
            {
                if (PlaySpeedSteps[i] == current ||
                    (current == GameSpeed.Slow && PlaySpeedSteps[i] == GameSpeed.Normal))
                {
                    idx = i;
                    break;
                }
            }

            int next = idx + (delta > 0 ? 1 : -1);
            if (next < 0) next = 0;
            if (next >= PlaySpeedSteps.Length) next = PlaySpeedSteps.Length - 1;
            return PlaySpeedSteps[next];
        }
    }
}