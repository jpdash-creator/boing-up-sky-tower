using System;

namespace FluffyClimbers.LeaderboardService
{
    /// <summary>The three weekly boards. All of them measure crowns.</summary>
    public static class Boards
    {
        public const string Overall = "overall", Levels = "levels", Endless = "endless";
        public static readonly string[] All = { Overall, Levels, Endless };
        public static bool IsValid(string b) { return b == Overall || b == Levels || b == Endless; }
    }

    /// <summary>Run kinds as the client sends them.</summary>
    public static class Kinds
    {
        public const string Level = "level", Endless = "endless", TimeAttack = "timeattack";
    }

    /// <summary>All limits and switches, loaded from Firestore config/leaderboard (falling back to config.default.json, then to these values). Nothing here is hardcoded in the Firestore rules.</summary>
    public sealed class Limits
    {
        public int[] SimVersions = { 1 };
        public int MaxRegion = 5;
        public long OverallEntryLimit = 200000, LevelsEntryLimit = 150000, EndlessEntryLimit = 50000;
        public int TopThreeFactorPercent = 150;
        public int PaidRanks = 10;
        public int BoardSize = 100;
        public int MaxTicks = 216000;
        public int MaxInputBytes = 262144;
        public int MaxRevives = 40;
        public int MaxBoughtUnitsPerTypePerRun = 8;
        public long MaxCrownPerRun = 20000000;
        public int MinSubmitIntervalMs = 2000;
        public long MaxTicksPerDay = 4000000;
        public int RetentionDays = 90;
        public int ClaimWindowDays = 28;
        public int BoardRebuildSeconds = 20;
        /// <summary>Lifetime of a run ticket from /start-run (a run may last up to MaxTicks = 1 h).</summary>
        public int TicketMinutes = 120;

        public long EntryLimit(string board)
        {
            return board == Boards.Overall ? OverallEntryLimit : board == Boards.Levels ? LevelsEntryLimit : EndlessEntryLimit;
        }

        public bool SimVersionAllowed(int v)
        {
            for (int i = 0; i < SimVersions.Length; i++) if (SimVersions[i] == v) return true;
            return false;
        }
    }

    /// <summary>Week arithmetic: a week runs from Monday 00:00 India time (UTC+05:30, no daylight saving) to the next Monday 00:00, i.e. Sunday 18:30 UTC. The week key is the ISO date of that Monday in IST.</summary>
    public static class WeekMath
    {
        public static readonly TimeSpan IstOffset = new TimeSpan(5, 30, 0);

        /// <summary>Monday 00:00 IST of the week containing <paramref name="utcNow"/>, as a UTC instant.</summary>
        public static DateTime WeekStartUtc(DateTime utcNow)
        {
            DateTime ist = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc) + IstOffset;
            int back = ((int)ist.DayOfWeek + 6) % 7;   // Monday = 0
            DateTime mondayIst = ist.Date.AddDays(-back);
            return DateTime.SpecifyKind(mondayIst - IstOffset, DateTimeKind.Utc);
        }

        public static string WeekKey(DateTime utcNow)
        {
            DateTime start = WeekStartUtc(utcNow);
            return (start + IstOffset).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static DateTime WeekEndUtc(DateTime utcNow) { return WeekStartUtc(utcNow).AddDays(7); }

        /// <summary>Start (UTC instant) of the week with the given key (Monday 00:00 IST); false when the key is not a Monday date.</summary>
        public static bool TryStartOf(string weekKey, out DateTime startUtc)
        {
            startUtc = default(DateTime);
            DateTime d;
            if (weekKey == null || !DateTime.TryParseExact(weekKey, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out d)) return false;
            if (d.DayOfWeek != DayOfWeek.Monday) return false;
            startUtc = DateTime.SpecifyKind(d - IstOffset, DateTimeKind.Utc);
            return true;
        }
    }

    /// <summary>Slot ids. A slot is the unit whose best crown of the week counts: a campaign level in one Gameplay setting (625 slots), or a region in one setting for Endless / Time Attack (50 slots).</summary>
    public static class SlotIds
    {
        public static string Level(string levelId, int mode) { return "c." + levelId + "." + mode; }
        public static string Endless(string regionId, int mode) { return "e." + regionId + "." + mode; }
        public static string TimeAttack(string regionId, int mode) { return "t." + regionId + "." + mode; }
        public static string BoardOf(string kind) { return kind == Kinds.Level ? Boards.Levels : Boards.Endless; }
    }

    /// <summary>The prize of a finished week (owner decision 2026-11-12). Counts of items; the client turns them into inventory (a bundle with its hidden hero card, chests, power-ups, coins).</summary>
    public sealed class PrizeItems
    {
        public int Bundles { get; set; }
        public int GoldChests { get; set; }
        public int IronChests { get; set; }
        public int PowerUps { get; set; }
        public int Coins { get; set; }
        public bool IsEmpty { get { return Bundles + GoldChests + IronChests + PowerUps + Coins == 0; } }
    }

    public static class Prizes
    {
        /// <summary>Prize of a board and rank (1-based), ignoring eligibility. Ranks outside 1..10 get nothing.</summary>
        public static PrizeItems For(string board, int rank)
        {
            if (rank < 1 || rank > 10) return new PrizeItems();
            if (board == Boards.Overall)
            {
                if (rank == 1) return new PrizeItems { Bundles = 1, GoldChests = 1 };
                if (rank == 2) return new PrizeItems { Bundles = 1, IronChests = 1 };
                if (rank == 3) return new PrizeItems { GoldChests = 1 };
                return new PrizeItems { IronChests = 1, PowerUps = 2 };
            }
            if (rank == 1) return new PrizeItems { GoldChests = 1, PowerUps = 3 };
            if (rank == 2) return new PrizeItems { GoldChests = 1 };
            if (rank == 3) return new PrizeItems { IronChests = 1, PowerUps = 2 };
            return new PrizeItems { Coins = 3000, PowerUps = 1 };
        }

        /// <summary>
        /// Eligibility and prize: rank 1..PaidRanks AND crowns at least the board's entry limit; ranks 1 to 3 also need 1.5x the limit.
        /// A rank 1 to 3 player that passed the entry limit but not 1.5x gets the 4th-10th prize (no shifting of other ranks; owner question Q2).
        /// </summary>
        public static PrizeItems Resolve(Limits limits, string board, int rank, long crowns)
        {
            long limit = limits.EntryLimit(board);
            if (rank < 1 || rank > limits.PaidRanks || crowns < limit) return new PrizeItems();
            if (rank <= 3 && crowns * 100 < limit * limits.TopThreeFactorPercent) return For(board, 4);
            return For(board, rank);
        }
    }
}
