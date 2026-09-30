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
        Slow = 1,
        Normal = 2,
        Fast = 3,
        VeryFast = 4
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

        public static float SecondsPerDay(GameSpeed speed)
        {
            switch (speed)
            {
                case GameSpeed.Slow: return 2.0f;
                case GameSpeed.Normal: return 0.85f;
                case GameSpeed.Fast: return 0.28f;
                case GameSpeed.VeryFast: return 0.08f;
                default: return float.MaxValue;
            }
        }
    }
}
