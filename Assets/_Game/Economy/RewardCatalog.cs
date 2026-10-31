using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace BoingUpSkyTower.Economy
{
    /// <summary>The three tabs of the Rewards screen (docs/26 section 4).</summary>
    public enum RewardTab
    {
        Main = 0,
        Daily = 1,
        Weekly = 2
    }

    /// <summary>Counter names of the reward tracking (per local day and per week, see ProgressService.Rewards.cs).</summary>
    public static class RewardStat
    {
        public const string Login = "login";            // day: 0/1
        public const string LoginDays = "loginDays";    // week: days of this week with a login (derived from a bit mask)
        public const string PlaySeconds = "playSec";
        public const string PlayMinutes = "playMin";    // derived: PlaySeconds / 60
        public const string Levels = "levels";          // levels completed (first clears and replays)
        public const string Combos = "combos";          // combo chains that reached 3 or more landings
        public const string BestCombo = "bestCombo";    // day: best chain; the week keeps the max too
        public const string Hard = "hard";              // levels played on difficulty 3 (Hard) or more
        public const string NoWall = "noWall";          // no-wall-touch level (level 7) cleared
        public const string GemLevel = "gemLevel";      // gem levels (8 and 15) cleared
        public const string AllGems = "allGems";        // levels cleared with every gem pickup of the level collected
        public const string PowerUps = "powerups";      // power-up units used
        public const string Gems = "gems";              // gems collected in levels
        public const string Stars3 = "stars3";          // 3-star clears
        public const string Replays = "replays";        // day: replays of already cleared levels that were counted (at most MaxCountedReplaysPerDay)
        public const string LoginMask = "loginMask";    // week: bit 0 = Monday ... bit 6 = Sunday

        /// <summary>Hard starts at this difficulty setting (0 Relaxed, 1 Easy, 2 Normal, 3 Hard, 4 Insane).</summary>
        public const int HardDifficulty = 3;

        /// <summary>Owner rule: every first clear counts; replays of cleared levels count for at most this many per day in total.</summary>
        public const int MaxCountedReplaysPerDay = 3;
    }

    /// <summary>What a claim pays: gems, coins and a number of RANDOM power-up units (any of the implemented types, repeats allowed).</summary>
    public struct RewardGrant
    {
        public int Gems, Coins, PowerUps;
        public bool IsEmpty { get { return Gems <= 0 && Coins <= 0 && PowerUps <= 0; } }
    }

    /// <summary>One reward row: a star milestone, a region reward, a daily points milestone, a daily action or a weekly task.</summary>
    public sealed class RewardEntry
    {
        public string Id;
        public string Title;
        /// <summary>The counter this entry reads (RewardStat), or null for star, region and points milestones.</summary>
        public string Stat;
        /// <summary>Stars, region index, points or the counter value that completes the entry.</summary>
        public int Target;
        /// <summary>Daily actions only: the points the action is worth.</summary>
        public int Points;
        public int Gems, Coins, PowerUps;

        public RewardGrant Grant { get { return new RewardGrant { Gems = Gems, Coins = Coins, PowerUps = PowerUps }; } }
    }

    /// <summary>
    /// The reward tables (docs/26 section 4). The runtime uses the built-in copy below; a unit test keeps it identical to
    /// Content/economy/rewards.json, the editable source (parsed by FromJson for tools and remote overrides).
    /// </summary>
    public sealed class RewardCatalog
    {
        public IReadOnlyList<RewardEntry> StarMilestones { get { return stars; } }
        /// <summary>One entry per region (Target = region index), paid when all 15 levels of the region are cleared.</summary>
        public IReadOnlyList<RewardEntry> RegionRewards { get { return regions; } }
        /// <summary>The ten daily actions, worth 10 points each.</summary>
        public IReadOnlyList<RewardEntry> DailyActions { get { return dailyActions; } }
        /// <summary>The daily points milestones (Target = points).</summary>
        public IReadOnlyList<RewardEntry> DailyMilestones { get { return dailyMilestones; } }
        public IReadOnlyList<RewardEntry> WeeklyTasks { get { return weekly; } }

        readonly List<RewardEntry> stars = new List<RewardEntry>();
        readonly List<RewardEntry> regions = new List<RewardEntry>();
        readonly List<RewardEntry> dailyActions = new List<RewardEntry>();
        readonly List<RewardEntry> dailyMilestones = new List<RewardEntry>();
        readonly List<RewardEntry> weekly = new List<RewardEntry>();

        public int DailyMaxPoints
        {
            get
            {
                int n = 0;
                foreach (RewardEntry a in dailyActions) n += a.Points;
                return n;
            }
        }

        static RewardEntry E(string id, string title, string stat, int target, int points, int gems, int coins, int powerUps)
        {
            return new RewardEntry { Id = id, Title = title, Stat = stat, Target = target, Points = points, Gems = gems, Coins = coins, PowerUps = powerUps };
        }

        // star milestone: stars, gems, coins, power-ups
        static readonly int[][] StarRows =
        {
            new[] { 25, 20, 0, 0 }, new[] { 50, 0, 1000, 0 }, new[] { 75, 30, 0, 0 }, new[] { 100, 0, 1500, 0 }, new[] { 125, 40, 0, 1 },
            new[] { 150, 0, 2000, 0 }, new[] { 175, 60, 0, 2 }, new[] { 200, 80, 0, 3 }, new[] { 225, 100, 0, 5 },
        };

        static readonly string[] RegionNames = { "Meadow Springs", "Mushroom Grove", "Sunny Beach", "Candy Canyon", "Bamboo Temple" };

        // daily action: id, title, counter, target (points are 10 each)
        static readonly object[][] DailyRows =
        {
            new object[] { "d_login", "Log in today", RewardStat.Login, 1 },
            new object[] { "d_play5", "Play for 5 minutes", RewardStat.PlaySeconds, 300 },
            new object[] { "d_level1", "Complete a level", RewardStat.Levels, 1 },
            new object[] { "d_level3", "Complete 3 levels", RewardStat.Levels, 3 },
            new object[] { "d_combo3", "Do a combo of 3 or more", RewardStat.Combos, 1 },
            new object[] { "d_hard", "Play a hard level", RewardStat.Hard, 1 },
            new object[] { "d_nowall", "Clear the no-wall level", RewardStat.NoWall, 1 },
            new object[] { "d_gemlevel", "Clear a gem level", RewardStat.GemLevel, 1 },
            new object[] { "d_allgems", "Collect all gems in a level", RewardStat.AllGems, 1 },
            new object[] { "d_powerup", "Use a power-up", RewardStat.PowerUps, 1 },
        };

        // daily milestone: points, gems, coins, power-ups
        static readonly int[][] DailyMilestoneRows =
        {
            new[] { 20, 10, 0, 0 }, new[] { 40, 0, 500, 0 }, new[] { 60, 15, 0, 0 }, new[] { 80, 0, 1000, 0 }, new[] { 100, 20, 0, 1 },
        };

        // weekly task: id, title, counter, target, gems, coins, power-ups
        static readonly object[][] WeeklyRows =
        {
            new object[] { "w_login7", "Log in every day of the week", RewardStat.LoginDays, 7, 30, 0, 0 },
            new object[] { "w_play100", "Play for 100 minutes", RewardStat.PlayMinutes, 100, 0, 500, 0 },
            new object[] { "w_play150", "Play for 150 minutes", RewardStat.PlayMinutes, 150, 20, 0, 0 },
            new object[] { "w_combos5", "Do 5 combos", RewardStat.Combos, 5, 0, 300, 0 },
            new object[] { "w_combos10", "Do 10 combos", RewardStat.Combos, 10, 10, 0, 0 },
            new object[] { "w_combos25", "Do 25 combos", RewardStat.Combos, 25, 20, 0, 0 },
            new object[] { "w_combos50", "Do 50 combos", RewardStat.Combos, 50, 0, 0, 1 },
            new object[] { "w_levels15", "Clear 15 levels", RewardStat.Levels, 15, 40, 0, 0 },
            new object[] { "w_gems30", "Collect 30 gems in levels", RewardStat.Gems, 30, 0, 1000, 0 },
            new object[] { "w_nowall", "Clear the no-wall level", RewardStat.NoWall, 1, 15, 0, 0 },
            new object[] { "w_stars3", "Earn 3 stars on 5 levels", RewardStat.Stars3, 5, 25, 0, 0 },
        };

        public static RewardCatalog BuiltIn()
        {
            var c = new RewardCatalog();
            foreach (int[] r in StarRows) c.stars.Add(E("s_" + r[0], r[0] + " stars", null, r[0], 0, r[1], r[2], r[3]));
            for (int i = 0; i < RegionNames.Length; i++) c.regions.Add(E("r_" + (i + 1), RegionNames[i] + " cleared", null, i + 1, 0, 50, 2000, 0));
            foreach (object[] r in DailyRows) c.dailyActions.Add(E((string)r[0], (string)r[1], (string)r[2], (int)r[3], 10, 0, 0, 0));
            foreach (int[] r in DailyMilestoneRows) c.dailyMilestones.Add(E("dp_" + r[0], r[0] + " points", null, r[0], 0, r[1], r[2], r[3]));
            foreach (object[] r in WeeklyRows) c.weekly.Add(E((string)r[0], (string)r[1], (string)r[2], (int)r[3], 0, (int)r[4], (int)r[5], (int)r[6]));
            return c;
        }

        /// <summary>
        /// Parses Content/economy/rewards.json: { "stars": [..], "regions": [..], "dailyActions": [..], "dailyMilestones": [..], "weekly": [..] }.
        /// Every entry has id, title, target and any of gems, coins, powerups; actions have stat and points; weekly tasks have stat.
        /// </summary>
        public static RewardCatalog FromJson(string json)
        {
            var c = new RewardCatalog();
            JObject root = JObject.Parse(json);
            Read(root, "stars", c.stars);
            Read(root, "regions", c.regions);
            Read(root, "dailyActions", c.dailyActions);
            Read(root, "dailyMilestones", c.dailyMilestones);
            Read(root, "weekly", c.weekly);
            return c;
        }

        static void Read(JObject root, string key, List<RewardEntry> into)
        {
            JArray arr = root[key] as JArray;
            if (arr == null) return;
            foreach (JToken t in arr)
            {
                into.Add(new RewardEntry
                {
                    Id = (string)t["id"], Title = (string)t["title"], Stat = (string)t["stat"], Target = (int?)t["target"] ?? 0,
                    Points = (int?)t["points"] ?? 0, Gems = (int?)t["gems"] ?? 0, Coins = (int?)t["coins"] ?? 0, PowerUps = (int?)t["powerups"] ?? 0
                });
            }
        }

        /// <summary>Finds an entry of the tab's tables by id (null when unknown).</summary>
        public RewardEntry Find(RewardTab tab, string id)
        {
            if (id == null) return null;
            if (tab == RewardTab.Main) return FindIn(stars, id) ?? FindIn(regions, id);
            if (tab == RewardTab.Daily) return FindIn(dailyMilestones, id);
            return FindIn(weekly, id);
        }

        static RewardEntry FindIn(List<RewardEntry> list, string id)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Id == id) return list[i];
            return null;
        }
    }
}
