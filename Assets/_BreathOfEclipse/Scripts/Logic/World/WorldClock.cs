using System;

namespace BreathOfEclipse.World
{
    /// <summary>Parts of the day the world reacts to (NPC routines, shops, demons, ambience).</summary>
    public enum DayPhase
    {
        LateNight = 0,
        Dawn = 1,
        Morning = 2,
        Day = 3,
        Afternoon = 4,
        Sunset = 5,
        Night = 6
    }

    /// <summary>
    /// The world's 24-hour clock: day counter + hour (0..24), real-time advance with a time scale, pause, and
    /// explicit jumps (rest / debug). Engine independent so schedules and events can be unit tested.
    /// </summary>
    public sealed class WorldClock
    {
        public const float HoursPerDay = 24f;
        /// <summary>Default pace: one in-game hour every 90 real seconds (a full day ≈ 36 minutes).</summary>
        public const float DefaultSecondsPerHour = 90f;

        public int Day { get; private set; } = 1;
        public float Hour { get; private set; } = 7f;
        public float SecondsPerHour { get; set; } = DefaultSecondsPerHour;
        /// <summary>Debug / rest speed-up (1x, 5x, 20x…).</summary>
        public float Multiplier { get; set; } = 1f;
        public bool Paused { get; set; }

        /// <summary>Hours since day 1, 00:00 — the world's absolute timestamp (events, deadlines, recovery).</summary>
        public double TotalHours => (Day - 1) * (double)HoursPerDay + Hour;
        public DayPhase Phase => PhaseOf(Hour);

        /// <summary>(previous, next) whenever the phase changes.</summary>
        public event Action<DayPhase, DayPhase> PhaseChanged;
        /// <summary>Raised once when the day counter increases.</summary>
        public event Action<int> NewDay;

        public WorldClock() { }

        public WorldClock(int day, float hour) => Set(day, hour);

        public void Set(int day, float hour)
        {
            var before = Phase;
            Day = Math.Max(1, day);
            Hour = Wrap(hour);
            if (Phase != before) PhaseChanged?.Invoke(before, Phase);
        }

        /// <summary>Sets the hour of the current day; going "back" in time moves to the next day instead (debug / rest never rewinds).</summary>
        public void SetHourForward(float hour)
        {
            hour = Wrap(hour);
            double delta = hour - Hour;
            if (delta < 0) delta += HoursPerDay;
            AdvanceHours(delta);
        }

        /// <summary>Advances by real seconds using the pace, multiplier and pause. Returns the hours advanced.</summary>
        public double Tick(float realSeconds)
        {
            if (Paused || realSeconds <= 0f || SecondsPerHour <= 0f) return 0;
            double hours = realSeconds / SecondsPerHour * Math.Max(0f, Multiplier);
            AdvanceHours(hours);
            return hours;
        }

        public void AdvanceHours(double hours)
        {
            if (hours <= 0) return;
            var before = Phase;
            double h = Hour + hours;
            while (h >= HoursPerDay)
            {
                h -= HoursPerDay;
                Day++;
                NewDay?.Invoke(Day);
            }
            Hour = (float)h;
            if (Phase != before) PhaseChanged?.Invoke(before, Phase);
        }

        public static float Wrap(float hour)
        {
            hour %= HoursPerDay;
            if (hour < 0f) hour += HoursPerDay;
            return hour;
        }

        public static DayPhase PhaseOf(float hour)
        {
            hour = Wrap(hour);
            if (hour < 5f) return DayPhase.LateNight;
            if (hour < 7f) return DayPhase.Dawn;
            if (hour < 10f) return DayPhase.Morning;
            if (hour < 14f) return DayPhase.Day;
            if (hour < 17f) return DayPhase.Afternoon;
            if (hour < 19.5f) return DayPhase.Sunset;
            return DayPhase.Night;
        }

        /// <summary>True from the end of dusk to dawn (demons roam, shops shut, lanterns lit).</summary>
        public static bool IsNight(float hour)
        {
            hour = Wrap(hour);
            return hour >= 19.5f || hour < 5.5f;
        }

        /// <summary>0 at night … 1 at full day, smooth through dawn (5:00–7:30) and dusk (17:00–19:30).</summary>
        public static float Daylight(float hour)
        {
            hour = Wrap(hour);
            if (hour < 5f || hour >= 19.5f) return 0f;
            if (hour < 7.5f) return Smooth((hour - 5f) / 2.5f);
            if (hour < 17f) return 1f;
            return 1f - Smooth((hour - 17f) / 2.5f);
        }

        /// <summary>Warm light of sunrise / sunset (0..1, peaks around 6:00 and 18:15).</summary>
        public static float GoldenHour(float hour)
        {
            hour = Wrap(hour);
            float morning = 1f - Math.Min(1f, Math.Abs(hour - 6.2f) / 1.4f);
            float evening = 1f - Math.Min(1f, Math.Abs(hour - 18.2f) / 1.6f);
            return Math.Max(0f, Math.Max(morning, evening));
        }

        /// <summary>Hours from <paramref name="from"/> forward to <paramref name="to"/> (wrapping past midnight).</summary>
        public static float HoursUntil(float from, float to)
        {
            float d = Wrap(to) - Wrap(from);
            return d < 0f ? d + HoursPerDay : d;
        }

        /// <summary>True if <paramref name="hour"/> lies in [start, end) — windows may wrap past midnight (e.g. 20 → 4).</summary>
        public static bool InWindow(float hour, float start, float end)
        {
            hour = Wrap(hour);
            start = Wrap(start);
            end = Wrap(end);
            if (Math.Abs(start - end) < 1e-4f) return true;
            return start < end ? hour >= start && hour < end : hour >= start || hour < end;
        }

        public static string Format(float hour)
        {
            hour = Wrap(hour);
            int h = (int)hour;
            int m = (int)((hour - h) * 60f);
            if (m >= 60)
            {
                m = 0;
                h = (h + 1) % 24;
            }
            return $"{h:00}:{m:00}";
        }

        private static float Smooth(float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return t * t * (3f - 2f * t);
        }
    }
}
