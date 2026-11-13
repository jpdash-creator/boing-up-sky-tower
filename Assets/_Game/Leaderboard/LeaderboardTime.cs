using System;
using System.Globalization;
using System.Text;

namespace FluffyClimbers.Leaderboard
{
    /// <summary>
    /// Week arithmetic of the weekly boards (mirrors the service WeekMath): a week runs from Monday 00:00 India time (UTC+05:30, no daylight saving) to the next Monday 00:00,
    /// which is Sunday 18:30 UTC. Pure functions, no clock of their own (the caller passes UTC now), so tests can set any instant.
    /// </summary>
    public static class LeaderboardTime
    {
        public static readonly TimeSpan IstOffset = new TimeSpan(5, 30, 0);
        static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>Monday 00:00 IST of the week that contains <paramref name="utcNow"/>, as a UTC instant.</summary>
        public static DateTime WeekStartUtc(DateTime utcNow)
        {
            DateTime ist = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc) + IstOffset;
            int back = ((int)ist.DayOfWeek + 6) % 7;   // Monday = 0
            DateTime mondayIst = ist.Date.AddDays(-back);
            return DateTime.SpecifyKind(mondayIst - IstOffset, DateTimeKind.Utc);
        }

        /// <summary>The next Monday 00:00 India time (the moment the week ends and the boards reset), as a UTC instant.</summary>
        public static DateTime WeekEndUtc(DateTime utcNow) { return WeekStartUtc(utcNow).AddDays(7); }

        /// <summary>Week key = ISO date of that Monday in IST ("2026-11-09").</summary>
        public static string WeekKey(DateTime utcNow)
        {
            return (WeekStartUtc(utcNow) + IstOffset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        /// <summary>Time left until the week ends (never negative).</summary>
        public static TimeSpan Remaining(DateTime utcNow)
        {
            TimeSpan t = WeekEndUtc(utcNow) - DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
            return t < TimeSpan.Zero ? TimeSpan.Zero : t;
        }

        /// <summary>"6d 04:12" while a day or more is left (no seconds, keeps the pill short), else "03:12:09".</summary>
        public static string FormatCountdown(TimeSpan left)
        {
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;
            var sb = new StringBuilder(12);
            if (left.Days > 0) sb.Append(left.Days).Append("d ").Append(left.Hours.ToString("00", CultureInfo.InvariantCulture)).Append(':').Append(left.Minutes.ToString("00", CultureInfo.InvariantCulture));
            else sb.Append(left.Hours.ToString("00", CultureInfo.InvariantCulture)).Append(':').Append(left.Minutes.ToString("00", CultureInfo.InvariantCulture)).Append(':').Append(left.Seconds.ToString("00", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        public static long ToUnix(DateTime utc) { return (long)(DateTime.SpecifyKind(utc, DateTimeKind.Utc) - Epoch).TotalSeconds; }

        public static DateTime FromUnix(long seconds) { return Epoch.AddSeconds(seconds); }

        /// <summary>UTC day key yyyyMMdd (used for "asked once a day").</summary>
        public static string DayKey(DateTime utcNow) { return utcNow.ToUniversalTime().ToString("yyyyMMdd", CultureInfo.InvariantCulture); }

        /// <summary>Parses an ISO 8601 time from the service; false when it is not one.</summary>
        public static bool TryParseUtc(string text, out DateTime utc)
        {
            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc);
        }
    }
}
