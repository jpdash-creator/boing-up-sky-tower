using System.Text;
using FluffyClimbers.Economy;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// Cached strings for the Run HUD timer, the live delta and small numbers (doc 46, CUX-1: zero managed allocations per frame). A time text is looked up by its tenth of a
    /// second, so the clock never builds a string while it runs: the table for 0:00.0 to 3:00.0 is filled by <see cref="Warmup"/> (when the HUD is built, never during a run) and the
    /// rest up to 10:00.0 fills on first use and is then reused for the whole session. Beyond that a string is built (a rare, very long endless run). Pure C# (dotnet-testable).
    /// </summary>
    public static class TimedText
    {
        public const int WarmTenths = 1800;      // 3:00.0 (a Time Attack run, a long level)
        public const int MaxCachedTenths = 6000; // 10:00.0
        public const int DeltaCacheTenths = 600; // plus or minus 60.0 s
        public const int MaxCachedInt = 2000;

        static readonly string[] times = new string[MaxCachedTenths + 1];
        static readonly string[] deltas = new string[2 * DeltaCacheTenths + 1];
        static readonly string[] ints = new string[MaxCachedInt + 1];
        static readonly StringBuilder sb = new StringBuilder(24);

        /// <summary>Fills the tables used in the first three minutes. Call once when a HUD is built; safe to call again (nothing is rebuilt).</summary>
        public static void Warmup()
        {
            for (int t = 0; t <= WarmTenths; t++) TimeOfTenths(t);
            for (int d = -DeltaCacheTenths; d <= DeltaCacheTenths; d++) DeltaOfTenths(d);
        }

        /// <summary>"01:12.4" for a clock of <paramref name="ticks"/> simulation ticks (60 per second), rounded to the nearest tenth like <see cref="TimeRecords.FormatTime(int)"/>.</summary>
        public static string Time(int ticks)
        {
            return TimeOfTenths(TimeRecords.TimeTenths(ticks));
        }

        /// <summary>The same text from a whole number of tenths of a second.</summary>
        public static string TimeOfTenths(int tenths)
        {
            if (tenths < 0) tenths = 0;
            if (tenths > MaxCachedTenths) return Build(tenths);
            string s = times[tenths];
            if (s == null) times[tenths] = s = Build(tenths);
            return s;
        }

        static string Build(int tenths)
        {
            sb.Length = 0;
            TimeRecords.FormatTime(sb, tenths * 6);   // 6 ticks = exactly one tenth
            return sb.ToString();
        }

        /// <summary>"+3.2 s" / "-0.4 s" / "0.0 s": elapsed ticks minus the best run's ticks for the same floor (negative = faster).</summary>
        public static string Delta(int deltaTicks)
        {
            int tenths = deltaTicks >= 0 ? (deltaTicks + 3) / 6 : -((-deltaTicks + 3) / 6);   // rounded to the nearest tenth, half away from zero
            return DeltaOfTenths(tenths);
        }

        static string DeltaOfTenths(int tenths)
        {
            if (tenths < -DeltaCacheTenths || tenths > DeltaCacheTenths) return BuildDelta(tenths);
            int i = tenths + DeltaCacheTenths;
            string s = deltas[i];
            if (s == null) deltas[i] = s = BuildDelta(tenths);
            return s;
        }

        static string BuildDelta(int tenths)
        {
            sb.Length = 0;
            TimeRecords.FormatDeltaTime(sb, tenths * 6);
            return sb.ToString();
        }

        /// <summary>A small non-negative number as text, cached (floor counters, the ghost floor).</summary>
        public static string Int(int n)
        {
            if (n < 0) n = 0;
            if (n > MaxCachedInt) return n.ToString();
            string s = ints[n];
            if (s == null) ints[n] = s = n.ToString();
            return s;
        }

        /// <summary>A pace difference as sign and number only: "+0.05", "-0.12", "0.00" (the unit is already on the pill beside it).</summary>
        public static string PaceShort(int deltaHundredths)
        {
            sb.Length = 0;
            if (deltaHundredths > 0) sb.Append('+');
            else if (deltaHundredths < 0) sb.Append('-');
            TimeRecords.AppendHundredths(sb, deltaHundredths < 0 ? -deltaHundredths : deltaHundredths);
            return sb.ToString();
        }
    }
}
