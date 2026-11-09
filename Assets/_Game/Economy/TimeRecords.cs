using System;
using System.Collections.Generic;
using System.Text;

namespace FluffyClimbers.Economy
{
    // Timed gameplay (doc 46): Time, Pace, records, medals. PURE C# (no UnityEngine types) so dotnet tests cover it.
    // Time is kept in simulation ticks (60 per second), Pace as the rational floors x 60 / ticks and is only ever compared by
    // cross-multiplication (never by float), so a record decision is exact and the same on every device and on a server.

    public enum MedalLevel
    {
        None = 0,
        Bronze = 1,
        Silver = 2,
        Gold = 3
    }

    /// <summary>What a run record belongs to.</summary>
    public enum RunKind
    {
        /// <summary>A campaign level that was completed (LevelId set).</summary>
        Level = 0,
        /// <summary>An endless run that ended (RegionId set).</summary>
        Endless = 1,
        /// <summary>A 3:00 Time Attack run (RegionId set).</summary>
        TimeAttack = 2
    }

    /// <summary>Pace as the exact ratio Floors / Ticks (floors per tick; x60 for plat/s). Zero floors or zero ticks = no pace (0).</summary>
    public readonly struct PaceValue
    {
        public readonly int Floors;
        public readonly int Ticks;

        public PaceValue(int floors, int ticks)
        {
            Floors = floors;
            Ticks = ticks;
        }

        /// <summary>False when there is no pace to speak of (no floors or no time): such a value never beats anything.</summary>
        public bool IsValid { get { return Floors > 0 && Ticks > 0; } }

        /// <summary>Pace in hundredths of a plat/s, rounded half up (83 = 0.83 plat/s). 0 when not valid.</summary>
        public int Hundredths { get { return TimeRecords.PaceHundredths(Floors, Ticks); } }

        /// <summary>-1, 0, +1: this pace against another one, by cross-multiplication. A not valid pace counts as 0.</summary>
        public int CompareTo(PaceValue other)
        {
            return TimeRecords.ComparePace(Floors, Ticks, other.Floors, other.Ticks);
        }
    }

    /// <summary>Gives the target Pace (the fastest the solvability bot reaches) per level and Gameplay mode, in milli plat/s (830 = 0.830). 0 = no data, no medal.</summary>
    public interface IMedalTargetSource
    {
        int TargetPaceMilli(string levelId, int mode);
    }

    /// <summary>No target data yet (the medal calibration job has not run): every target is 0, so nobody earns a medal.</summary>
    public sealed class NoMedalTargets : IMedalTargetSource
    {
        public int TargetPaceMilli(string levelId, int mode) { return 0; }
    }

    /// <summary>A simple in-memory target table (tests, and the loader of the generated data file later).</summary>
    public sealed class DictionaryMedalTargets : IMedalTargetSource
    {
        readonly Dictionary<string, int> map = new Dictionary<string, int>();

        static string Key(string levelId, int mode) { return levelId + "|" + mode; }

        public void Set(string levelId, int mode, int targetPaceMilli) { map[Key(levelId, mode)] = targetPaceMilli; }

        public int TargetPaceMilli(string levelId, int mode)
        {
            int v;
            return levelId != null && map.TryGetValue(Key(levelId, mode), out v) ? v : 0;
        }
    }

    /// <summary>The one place the medal targets come from. Replace <see cref="Source"/> when the generated target file exists.</summary>
    public static class MedalTargets
    {
        public static IMedalTargetSource Source = new NoMedalTargets();

        /// <summary>Gold target (the bot's fastest Pace) of a level and mode in milli plat/s, 0 when unknown.</summary>
        public static int Lookup(string levelId, int mode)
        {
            IMedalTargetSource s = Source;
            return s == null ? 0 : Math.Max(0, s.TargetPaceMilli(levelId, mode));
        }
    }

    /// <summary>
    /// Everything about Time and Pace that is pure maths or text: tick conversion, formatting, exact pace comparison, the improve-only rule,
    /// medal grading and the delta against a floor-by-time curve.
    /// </summary>
    public static class TimeRecords
    {
        public const int TicksPerSecond = 60;
        /// <summary>Gameplay modes (Relaxed, Easy, Normal, Hard, Insane); the index is GameSettings.Difficulty.</summary>
        public const int ModeCount = 5;
        /// <summary>Pace unit text.</summary>
        public const string PaceUnit = " plat/s";
        /// <summary>The Daily goal pace: a clear at 0.6 plat/s or more (milli plat/s).</summary>
        public const int DailyPaceClearMilli = 600;
        /// <summary>Time Attack length in ticks (3:00).</summary>
        public const int TimeAttackTicks = 180 * TicksPerSecond;
        /// <summary>Endless split stage length in floors (doc 46 section 7).</summary>
        public const int SplitFloors = 100;
        /// <summary>Splits kept per endless record (floor 100 to 3000).</summary>
        public const int MaxSplits = 30;
        /// <summary>The live delta is shown every this many floors.</summary>
        public const int DeltaEveryFloors = 10;

        /// <summary>The medal table (owner can change it): percent of the target Pace for Bronze, Silver, Gold.</summary>
        public static readonly int[] MedalPercents = { 55, 70, 85 };

        public static bool ValidMode(int mode) { return mode >= 0 && mode < ModeCount; }

        public static int ClampMode(int mode) { return mode < 0 ? 0 : (mode >= ModeCount ? ModeCount - 1 : mode); }

        // ---- time ----

        /// <summary>Whole tenths of a second of a tick count, rounded to the nearest tenth (half up), clamped to 99:59.9.</summary>
        public static int TimeTenths(int ticks)
        {
            if (ticks <= 0) return 0;
            long t = ((long)ticks * 10 + TicksPerSecond / 2) / TicksPerSecond;
            return t > 59999 ? 59999 : (int)t;
        }

        /// <summary>"01:12.4" (mm:ss.t, one decimal). Hours roll into minutes up to 99:59.9. Zero or negative ticks read "00:00.0".</summary>
        public static string FormatTime(int ticks)
        {
            var sb = new StringBuilder(8);
            FormatTime(sb, ticks);
            return sb.ToString();
        }

        /// <summary>Appends the same text to a cached StringBuilder (HUD use: no string is made, no int is boxed or converted).</summary>
        public static void FormatTime(StringBuilder sb, int ticks)
        {
            int tenths = TimeTenths(ticks);
            int sec = tenths / 10;
            int minutes = sec / 60, s = sec % 60;
            AppendTwo(sb, minutes);
            sb.Append(':');
            AppendTwo(sb, s);
            sb.Append('.');
            sb.Append((char)('0' + tenths % 10));
        }

        /// <summary>Text of a delta in ticks with a sign: "+3.2 s", "-0.4 s", "0.0 s" (rounded to a tenth).</summary>
        public static string FormatDeltaTime(int deltaTicks)
        {
            var sb = new StringBuilder(10);
            FormatDeltaTime(sb, deltaTicks);
            return sb.ToString();
        }

        public static void FormatDeltaTime(StringBuilder sb, int deltaTicks)
        {
            long a = deltaTicks < 0 ? -(long)deltaTicks : deltaTicks;
            long tenths = (a * 10 + TicksPerSecond / 2) / TicksPerSecond;
            if (tenths > 0) sb.Append(deltaTicks < 0 ? '-' : '+');
            AppendNumber(sb, (int)(tenths / 10));
            sb.Append('.');
            sb.Append((char)('0' + (int)(tenths % 10)));
            sb.Append(" s");
        }

        // ---- pace ----

        /// <summary>Pace in hundredths of a plat/s, rounded half up: floors x 60 x 100 / ticks. 0 for no floors or no time.</summary>
        public static int PaceHundredths(int floors, int ticks)
        {
            if (floors <= 0 || ticks <= 0) return 0;
            long num = (long)floors * TicksPerSecond * 100;
            long v = (2 * num + ticks) / (2L * ticks);
            return v > int.MaxValue ? int.MaxValue : (int)v;
        }

        /// <summary>Pace in thousandths of a plat/s, rounded half up (target data uses this unit). 0 for no floors or no time.</summary>
        public static int PaceMilli(int floors, int ticks)
        {
            if (floors <= 0 || ticks <= 0) return 0;
            long num = (long)floors * TicksPerSecond * 1000;
            long v = (2 * num + ticks) / (2L * ticks);
            return v > int.MaxValue ? int.MaxValue : (int)v;
        }

        /// <summary>
        /// Exact comparison of floors1/ticks1 with floors2/ticks2 (-1, 0, +1) by cross-multiplication in 64 bit. A value with no floors or no time
        /// counts as pace 0, and two zero paces are equal.
        /// </summary>
        public static int ComparePace(int floors1, int ticks1, int floors2, int ticks2)
        {
            bool v1 = floors1 > 0 && ticks1 > 0, v2 = floors2 > 0 && ticks2 > 0;
            if (!v1 && !v2) return 0;
            if (!v1) return -1;
            if (!v2) return 1;
            long left = (long)floors1 * ticks2;
            long right = (long)floors2 * ticks1;
            return left < right ? -1 : (left > right ? 1 : 0);
        }

        /// <summary>
        /// True when the new run beats the old record: a higher Pace, and on an exactly equal Pace the shorter Time. A not valid new pace never
        /// wins; any valid pace beats an empty record (no floors or no time).
        /// </summary>
        public static bool IsBetter(PaceValue newPace, PaceValue oldPace)
        {
            if (!newPace.IsValid) return false;
            int c = newPace.CompareTo(oldPace);
            if (c != 0) return c > 0;
            return newPace.Ticks < oldPace.Ticks;   // equal pace (old is valid here): the shorter Time wins
        }

        /// <summary>"0.83 plat/s" (two decimals, always a dot, no culture involved). 0 floors or 0 time read "0.00 plat/s".</summary>
        public static string FormatPace(int floors, int ticks)
        {
            var sb = new StringBuilder(16);
            FormatPace(sb, floors, ticks);
            return sb.ToString();
        }

        /// <summary>Appends "0.83 plat/s" to a cached StringBuilder (HUD use).</summary>
        public static void FormatPace(StringBuilder sb, int floors, int ticks)
        {
            AppendHundredths(sb, PaceHundredths(floors, ticks));
            sb.Append(PaceUnit);
        }

        /// <summary>Appends a hundredths value as "0.83" (no unit).</summary>
        public static void AppendHundredths(StringBuilder sb, int hundredths)
        {
            if (hundredths < 0) { sb.Append('-'); hundredths = -hundredths; }
            AppendNumber(sb, hundredths / 100);
            sb.Append('.');
            sb.Append((char)('0' + hundredths / 10 % 10));
            sb.Append((char)('0' + hundredths % 10));
        }

        /// <summary>Text of a pace difference in hundredths: "+0.05 plat/s", "-0.12 plat/s", "0.00 plat/s".</summary>
        public static string FormatDeltaPace(int deltaHundredths)
        {
            var sb = new StringBuilder(16);
            if (deltaHundredths > 0) sb.Append('+');
            AppendHundredths(sb, deltaHundredths);
            sb.Append(PaceUnit);
            return sb.ToString();
        }

        /// <summary>Appends a non-negative int without allocating (Mono's StringBuilder.Append(int) can).</summary>
        public static void AppendNumber(StringBuilder sb, int value)
        {
            if (value < 0) { sb.Append('-'); value = value == int.MinValue ? int.MaxValue : -value; }
            if (value >= 10) AppendNumber(sb, value / 10);
            sb.Append((char)('0' + value % 10));
        }

        static void AppendTwo(StringBuilder sb, int v)
        {
            sb.Append((char)('0' + v / 10 % 10));
            sb.Append((char)('0' + v % 10));
        }

        // ---- medals ----

        /// <summary>
        /// The medal a Pace earns against the target (the gold reference Pace, in milli plat/s): Bronze, Silver, Gold at <see cref="MedalPercents"/>
        /// percent of it. A pace exactly on a line earns it. A target of 0 (no data) or a not valid pace earns nothing.
        /// </summary>
        public static MedalLevel MedalFor(PaceValue pace, int targetPaceMilli)
        {
            if (!pace.IsValid || targetPaceMilli <= 0) return MedalLevel.None;
            // pace >= target x percent / 100  <=>  floors x 60 x 1000 x 100 >= target x percent x ticks  (all exact in 64 bit)
            long lhs = (long)pace.Floors * TicksPerSecond * 100000L;
            long tick = pace.Ticks;
            for (int m = (int)MedalLevel.Gold; m >= (int)MedalLevel.Bronze; m--)
            {
                long need = (long)targetPaceMilli * MedalPercents[m - 1] * tick;
                if (lhs >= need) return (MedalLevel)m;
            }
            return MedalLevel.None;
        }

        /// <summary>The medal of a Pace on a level and mode through <see cref="MedalTargets"/>.</summary>
        public static MedalLevel MedalFor(PaceValue pace, string levelId, int mode)
        {
            return MedalFor(pace, MedalTargets.Lookup(levelId, mode));
        }

        /// <summary>The Pace (milli plat/s) a medal needs on a target: target x percent / 100 rounded half up. 0 for None or no target.</summary>
        public static int MedalLinePaceMilli(MedalLevel medal, int targetPaceMilli)
        {
            if (medal == MedalLevel.None || targetPaceMilli <= 0) return 0;
            return (int)(((long)targetPaceMilli * MedalPercents[(int)medal - 1] + 50) / 100);
        }

        /// <summary>True when the pace is at least the Daily goal pace (0.6 plat/s), exactly.</summary>
        public static bool IsDailyPaceClear(int floors, int ticks)
        {
            return floors > 0 && ticks > 0 && (long)floors * TicksPerSecond * 1000L >= (long)DailyPaceClearMilli * ticks;
        }

        // ---- the floor-by-time curve (curve[i] = floors reached at the end of second i + 1 of the clock) ----

        /// <summary>
        /// Ticks the curve needed to reach <paramref name="floor"/> (straight line inside a second, integer maths), or -1 when the curve never
        /// got that high. Floor 0 or less is 0.
        /// </summary>
        public static int TicksToReach(int[] curve, int floor)
        {
            if (floor <= 0) return 0;
            if (curve == null) return -1;
            int prev = 0;
            for (int i = 0; i < curve.Length; i++)
            {
                int f = curve[i];
                if (f >= floor)
                {
                    int span = f - prev;
                    int within = span <= 0 ? 0 : (int)(((long)(floor - prev) * TicksPerSecond + span - 1) / span);   // first tick at which the line has reached the floor
                    return i * TicksPerSecond + within;
                }
                if (f > prev) prev = f;
            }
            return -1;
        }

        /// <summary>Floors in thousandths the curve had reached at a tick of the clock (Pace Ghost height); beyond the curve it holds its last value.</summary>
        public static int FloorsMilliAtTick(int[] curve, int ticks)
        {
            if (curve == null || curve.Length == 0 || ticks <= 0) return 0;
            int sec = ticks / TicksPerSecond, rem = ticks % TicksPerSecond;
            if (sec >= curve.Length) return curve[curve.Length - 1] * 1000;
            int a = sec == 0 ? 0 : curve[sec - 1], b = curve[sec];
            return (int)(a * 1000L + (long)(b - a) * 1000 * rem / TicksPerSecond);
        }

        /// <summary>
        /// Live delta against the best run: elapsed ticks now minus the ticks the best curve needed for the same floor. Negative = faster, positive =
        /// slower. Returns false (delta 0) when the best curve never reached that floor.
        /// </summary>
        public static bool TryDeltaTicks(int[] bestCurve, int floor, int elapsedTicks, out int deltaTicks)
        {
            int best = TicksToReach(bestCurve, floor);
            if (best < 0) { deltaTicks = 0; return false; }
            deltaTicks = elapsedTicks - best;
            return true;
        }

        // ---- endless splits (every 100 floors) ----

        /// <summary>The split stage a floor count has completed: floor 100 = stage 1, 250 = stage 2, 99 = 0. Stage i is the time to reach floor i x 100.</summary>
        public static int SplitStageOf(int floors) { return floors <= 0 ? 0 : floors / SplitFloors; }

        /// <summary>The floor a split stage ends at (stage 1 = 100, stage 3 = 300).</summary>
        public static int SplitStageFloor(int stage) { return stage <= 0 ? 0 : stage * SplitFloors; }

        /// <summary>
        /// Live split delta: ticks now minus the best split of the stage (negative = faster). Stage 1 = floor 100. False (delta 0) when the best
        /// record has no split for that stage yet. Use it when a run crosses floor 100, 200 ...
        /// </summary>
        public static bool TrySplitDelta(int[] bestSplits, int stage, int elapsedTicks, out int deltaTicks)
        {
            if (bestSplits == null || stage < 1 || stage > bestSplits.Length || bestSplits[stage - 1] <= 0) { deltaTicks = 0; return false; }
            deltaTicks = elapsedTicks - bestSplits[stage - 1];
            return true;
        }

        // ---- Time Attack (3:00) ----

        /// <summary>Ticks left of the 3:00 countdown for a run clock (never below 0). A clock of 0 (before the first jump) has the full length left.</summary>
        public static int TimeAttackTicksLeft(int runTicks)
        {
            int left = TimeAttackTicks - (runTicks < 0 ? 0 : runTicks);
            return left < 0 ? 0 : left;
        }

        /// <summary>True once the 3:00 are used up: the Time Attack run ends on the tick the clock reaches 10800.</summary>
        public static bool TimeAttackExpired(int runTicks) { return runTicks >= TimeAttackTicks; }

        // ---- rates ----

        /// <summary>
        /// Coins (or gems) per minute x 100 for an amount over a clock: the live value for a HUD and the stored capture value.
        /// 0 for no amount or no time. Exact integer maths, rounded half up. Same as <see cref="PerMinuteX100"/>.
        /// </summary>
        public static int RatePerMinute(int amount, int ticks) { return PerMinuteX100(amount, ticks); }

        /// <summary>Appends a per minute value stored x 100 as "12.50/min" (two decimals, a dot, no culture).</summary>
        public static void FormatPerMinute(StringBuilder sb, int x100)
        {
            AppendHundredths(sb, x100);
            sb.Append("/min");
        }

        public static string FormatPerMinute(int x100)
        {
            var sb = new StringBuilder(14);
            FormatPerMinute(sb, x100);
            return sb.ToString();
        }

        /// <summary>Per minute rate in hundredths (coins or gems per minute x 100), 0 for no time. Used for the timed captures.</summary>
        public static int PerMinuteX100(int amount, int ticks)
        {
            if (amount <= 0 || ticks <= 0) return 0;
            long v = ((long)amount * TicksPerSecond * 60 * 100 * 2 + ticks) / (2L * ticks);
            return v > int.MaxValue ? int.MaxValue : (int)v;
        }
    }

    /// <summary>
    /// One finished run as the records see it. Filled by RunSession.BuildRunRecord; applied by ProgressService.RecordRun.
    /// </summary>
    public sealed class RunRecord
    {
        public RunKind Kind;
        /// <summary>Campaign level id (Kind = Level), else null.</summary>
        public string LevelId;
        /// <summary>Region id (Endless and TimeAttack), else null.</summary>
        public string RegionId;
        /// <summary>Floors reached (the platform row counter; it never decreases).</summary>
        public int Floors;
        /// <summary>Clock in simulation ticks: first jump to the end (goal completed or run over). Pauses are not in it.</summary>
        public int Ticks;
        /// <summary>Layout seed: LevelSpec.Seed for a level, the tower seed for endless.</summary>
        public int LayoutSeed;
        /// <summary>The attempt seed a level layout was built from (0 in endless).</summary>
        public int AttemptSeed;
        public string HeroId = "";
        /// <summary>Gameplay mode index 0..4 (GameSettings.Difficulty).</summary>
        public int Mode;
        public string GameVersion = "";
        /// <summary>The run used a revive: it never sets a best Time or Pace and earns no medal.</summary>
        public bool Revived;
        /// <summary>Run-length encoded per-tick inputs (<see cref="InputCodec"/>), about 1 to 3 KB. Empty when not recorded.</summary>
        public byte[] Inputs = new byte[0];
        /// <summary>Floors reached at the end of each second of the clock.</summary>
        public int[] Curve = new int[0];
        /// <summary>Endless: ticks to reach floor 100, 200, ... (split i = floor (i + 1) x 100); only the stages the run reached.</summary>
        public int[] Splits = new int[0];
        /// <summary>Endless captures (set by the caller after the payout): coins and gems collected in the run, for the per-minute records.</summary>
        public int Coins, Gems;

        public PaceValue Pace { get { return new PaceValue(Floors, Ticks); } }
        public int PaceHundredths { get { return TimeRecords.PaceHundredths(Floors, Ticks); } }
        public int PaceMilli { get { return TimeRecords.PaceMilli(Floors, Ticks); } }
        /// <summary>The numbers are usable for a record: floors and time are positive and no revive was used.</summary>
        public bool Counts { get { return !Revived && Floors > 0 && Ticks > 0; } }
    }

    /// <summary>Run-length coding of the per-tick input values (SimInput bit sets, up to 17 bits). Format: repeated (value varint, run varint).</summary>
    public static class InputCodec
    {
        /// <summary>Codes the first <paramref name="count"/> inputs.</summary>
        public static byte[] Encode(int[] inputs, int count)
        {
            if (inputs == null || count <= 0) return new byte[0];
            var rec = new InputRecorder(64);
            for (int i = 0; i < count; i++) rec.Add(inputs[i]);
            return rec.ToBytes();
        }

        internal static byte[] Pack(int[] values, int[] runs, int count)
        {
            var buf = new List<byte>(count * 3);
            for (int i = 0; i < count; i++)
            {
                WriteVarint(buf, (uint)values[i]);
                WriteVarint(buf, (uint)runs[i]);
            }
            return buf.ToArray();
        }

        static void WriteVarint(List<byte> buf, uint v)
        {
            while (v >= 0x80) { buf.Add((byte)(v | 0x80)); v >>= 7; }
            buf.Add((byte)v);
        }

        /// <summary>Total ticks the data describes (sum of the runs), or -1 when the data is damaged.</summary>
        public static int DecodedLength(byte[] data)
        {
            if (data == null) return 0;
            int pos = 0;
            long total = 0;
            while (pos < data.Length)
            {
                uint value, run;
                if (!ReadVarint(data, ref pos, out value) || !ReadVarint(data, ref pos, out run)) return -1;
                total += run;
                if (total > int.MaxValue) return -1;
            }
            return (int)total;
        }

        /// <summary>Decodes to one value per tick. Damaged data returns null.</summary>
        public static int[] Decode(byte[] data)
        {
            int len = DecodedLength(data);
            if (len < 0) return null;
            var result = new int[len];
            int pos = 0, at = 0;
            while (pos < (data == null ? 0 : data.Length))
            {
                uint value, run;
                ReadVarint(data, ref pos, out value);
                ReadVarint(data, ref pos, out run);
                for (uint k = 0; k < run; k++) result[at++] = (int)value;
            }
            return result;
        }

        static bool ReadVarint(byte[] data, ref int pos, out uint v)
        {
            v = 0;
            int shift = 0;
            while (pos < data.Length)
            {
                byte b = data[pos++];
                v |= (uint)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return true;
                shift += 7;
                if (shift > 28) return false;
            }
            return false;
        }
    }

    /// <summary>
    /// Records the inputs of a run tick by tick with no per-tick allocation: it extends the current run, and only a CHANGE of the input
    /// stores a new (value, length) pair in preallocated arrays that double rarely. <see cref="ToBytes"/> packs it once at the end.
    /// </summary>
    public sealed class InputRecorder
    {
        int[] values;
        int[] runs;
        int pairs;
        int total;

        public InputRecorder(int capacityPairs = 512)
        {
            values = new int[Math.Max(16, capacityPairs)];
            runs = new int[values.Length];
        }

        /// <summary>Ticks recorded.</summary>
        public int Ticks { get { return total; } }

        public void Clear() { pairs = 0; total = 0; }

        public void Add(int input)
        {
            total++;
            if (pairs > 0 && values[pairs - 1] == input) { runs[pairs - 1]++; return; }
            if (pairs == values.Length)
            {
                Array.Resize(ref values, pairs * 2);
                Array.Resize(ref runs, pairs * 2);
            }
            values[pairs] = input;
            runs[pairs] = 1;
            pairs++;
        }

        public byte[] ToBytes()
        {
            if (pairs == 0) return new byte[0];
            return InputCodec.Pack(values, runs, pairs);
        }
    }

    /// <summary>Growing int list for the floor-by-time curve and the endless splits (preallocated, doubles rarely, no per-tick allocation).</summary>
    public sealed class GrowingInts
    {
        int[] items = new int[64];
        public int Count { get; private set; }

        public void Clear() { Count = 0; }

        public void Add(int v)
        {
            if (Count == items.Length) Array.Resize(ref items, Count * 2);
            items[Count++] = v;
        }

        public int[] ToArray()
        {
            var a = new int[Count];
            Array.Copy(items, a, Count);
            return a;
        }
    }
}
