using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FluffyClimbers.Economy
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
        public const string Hard = "hard";              // levels STARTED on difficulty 3 (Hard) or more (no longer used by a task, see HardClear)
        public const string NoWall = "noWall";          // no-wall-touch level (level 7) cleared
        public const string GemLevel = "gemLevel";      // gem levels (8 and 15) cleared
        public const string Stars2 = "stars2";          // levels cleared with 2 or more stars (works on replays: star pickups are not stripped)
        public const string PowerUps = "powerups";      // power-up units used
        public const string StarsEarned = "starsEarned"; // stars earned in completed levels (sum of the star count of each counted clear)
        public const string Stars3 = "stars3";          // 3-star clears
        public const string Replays = "replays";        // day: replays of already cleared levels that were counted (at most MaxCountedReplaysPerDay)
        public const string LoginMask = "loginMask";    // week: bit 0 = Monday ... bit 6 = Sunday
        public const string HardClear = "hardClear";    // levels CLEARED on difficulty 3 (Hard) or more (Bug List 5 F1.b)
        public const string EndlessFloor = "endlessFloor"; // best endless floor reached (day and week keep the maximum)

        // Timed goals (doc 46 section 8). They are read from the TimedCounters of the save (ProgressService.TimedGoals.cs), which restart with the local day and week.
        public const string PaceClears = "paceClears";          // day: not revived level clears at 0.6 plat/s or more
        public const string Floor100In3 = "floor100in3";        // day: 1 when endless reached floor 100 within 3:00 today
        public const string BestPaceLevels = "bestPaceLevels";  // week: distinct levels with a new best Pace
        public const string GoldMedals = "goldMedals";          // week: gold medals earned
        public const string TimeAttackFloors = "taFloors";      // week: best Time Attack floors

        /// <summary>Hard starts at this difficulty setting (0 Relaxed, 1 Easy, 2 Normal, 3 Hard, 4 Insane).</summary>
        public const int HardDifficulty = 3;

        /// <summary>Owner rule: every first clear counts; replays of cleared levels count for at most this many per day in total.</summary>
        public const int MaxCountedReplaysPerDay = 3;
    }

    /// <summary>
    /// The reward tables (docs/26 section 4, docs/39). The Main tab is a list of GOALS per region built from <see cref="RegionGoalTable"/> (the one place to
    /// retune them); Daily and Weekly come from the tables below. Content/economy/rewards.json is the editable copy of the Daily and Weekly tables (parsed by
    /// FromJson); a unit test keeps it identical to the built-in copy. Region goals are code-only: they are generated from the table, region by region.
    /// </summary>
    public sealed class RewardCatalog
    {
        /// <summary>Removed (Bug List 5 F1.a): the 25..225 star cards are gone. Always empty; kept so older UI code compiles until the Rewards UI is redone.</summary>
        [System.Obsolete("Replaced by RegionGoals(region)")]
        public IReadOnlyList<RewardEntry> StarMilestones { get { return empty; } }
        /// <summary>Removed (Bug List 5 F1.a): "clear all levels" is now the last Levels goal of each region. Always empty.</summary>
        [System.Obsolete("Replaced by RegionGoals(region)")]
        public IReadOnlyList<RewardEntry> RegionRewards { get { return empty; } }
        /// <summary>The eight daily actions (10 + 15 + 20 + 20 + 15 + 20 + 15 + 15 = 130 points).</summary>
        public IReadOnlyList<RewardEntry> DailyActions { get { return dailyActions; } }
        /// <summary>The daily points milestones (Target = points).</summary>
        public IReadOnlyList<RewardEntry> DailyMilestones { get { return dailyMilestones; } }
        public IReadOnlyList<RewardEntry> WeeklyTasks { get { return weekly; } }

        static readonly IReadOnlyList<RewardEntry> empty = new List<RewardEntry>();
        static readonly IReadOnlyList<RegionGoal> noGoals = new List<RegionGoal>();
        readonly List<RewardEntry> dailyActions = new List<RewardEntry>();
        readonly List<RewardEntry> dailyMilestones = new List<RewardEntry>();
        readonly List<RewardEntry> weekly = new List<RewardEntry>();
        readonly List<List<RegionGoal>> regionGoals = new List<List<RegionGoal>>();   // index = region - 1, 30 entries (empty for Coming Soon regions)
        readonly List<RegionGoal> allGoals = new List<RegionGoal>();

        public int DailyMaxPoints
        {
            get
            {
                int n = 0;
                foreach (RewardEntry a in dailyActions) n += a.Points;
                return n;
            }
        }

        /// <summary>The goals of a region in listing order. Regions 6 to 30 (Coming Soon) and invalid indexes have none.</summary>
        public IReadOnlyList<RegionGoal> RegionGoals(int region)
        {
            return region >= 1 && region <= regionGoals.Count ? regionGoals[region - 1] : noGoals;
        }

        /// <summary>Every goal of every playable region (what the Admin switch and Claim All walk).</summary>
        public IReadOnlyList<RegionGoal> AllRegionGoals { get { return allGoals; } }

        /// <summary>True for regions 6 to 30 (and any region without goals): the Main tab shows "Coming Soon".</summary>
        public bool IsComingSoon(int region) { return RegionGoals(region).Count == 0; }

        void BuildRegionGoals()
        {
            regionGoals.Clear();
            allGoals.Clear();
            for (int r = 1; r <= RegionGoalTable.TotalRegions; r++)
            {
                List<RegionGoal> g = RegionGoalTable.Build(r);
                regionGoals.Add(g);
                allGoals.AddRange(g);
            }
        }

        static RewardEntry E(string id, string title, string stat, int target, int points, int gems, int coins, int powerUps, int hero = 0)
        {
            return new RewardEntry { Id = id, Title = title, Stat = stat, Target = target, Points = points, Gems = gems, Coins = coins, PowerUps = powerUps, HeroRegion = hero };
        }

        // daily action: id, title, counter, target, points (6 actions, 100 points)
        static readonly object[][] DailyRows =
        {
            new object[] { "d_login", "Log in today", RewardStat.Login, 1, 10 },
            new object[] { "d_play5", "Play for 5 minutes", RewardStat.PlaySeconds, 300, 15 },
            new object[] { "d_play15", "Play for 15 minutes", RewardStat.PlaySeconds, 900, 20 },
            new object[] { "d_level3", "Complete 3 levels", RewardStat.Levels, 3, 20 },
            new object[] { "d_combo5", "Make 5 combos", RewardStat.Combos, 5, 15 },
            new object[] { "d_hard", "Clear a hard level", RewardStat.HardClear, 1, 20 },
            // timed goals (doc 46): each takes a Daily slot and pays like the existing actions (15 points); a full day is now 130 points, the milestones stay at 20..100
            new object[] { "d_pace2", "2 clears at 0.6 plat/s", RewardStat.PaceClears, 2, 15 },
            new object[] { "d_floor100", "Endless floor 100 in 3:00", RewardStat.Floor100In3, 1, 15 },
        };

        // daily milestone: points, gems, coins, power-ups (a day of 100 points pays 10 gems + 1,500 coins)
        static readonly int[][] DailyMilestoneRows =
        {
            new[] { 20, 0, 300, 0 }, new[] { 40, 3, 0, 0 }, new[] { 60, 0, 500, 0 }, new[] { 80, 3, 0, 0 }, new[] { 100, 4, 700, 0 },
        };

        // weekly task: id, title, counter, target, gems, coins, power-ups, hero (the LAST row, the hardest, gives one random Common hero of an open region)
        static readonly object[][] WeeklyRows =
        {
            new object[] { "w_login5", "Log in on 5 days", RewardStat.LoginDays, 5, 8, 400, 0, 0 },
            new object[] { "w_play100", "Play for 100 minutes", RewardStat.PlayMinutes, 100, 8, 500, 0, 0 },
            new object[] { "w_powerups10", "Use 10 power-ups", RewardStat.PowerUps, 10, 0, 0, 2, 0 },
            new object[] { "w_stars20", "Earn 20 stars", RewardStat.StarsEarned, 20, 6, 800, 0, 0 },
            new object[] { "w_levels20", "Complete 20 levels", RewardStat.Levels, 20, 8, 600, 0, 0 },
            new object[] { "w_nowall2", "Clear the no-wall level 2 times", RewardStat.NoWall, 2, 8, 500, 0, 0 },
            new object[] { "w_endless100", "Reach floor 100 in Endless", RewardStat.EndlessFloor, 100, 8, 500, 0, 0 },
            new object[] { "w_combos150", "Make 150 combos", RewardStat.Combos, 150, 10, 800, 1, 0 },
            // timed goals (doc 46): rewards reuse existing weekly packages; the gems add only 8 (the gem budget of docs/39 holds)
            new object[] { "w_pace3", "Best pace on 3 levels", RewardStat.BestPaceLevels, 3, 8, 600, 0, 0 },
            new object[] { "w_gold5", "Earn 5 gold medals", RewardStat.GoldMedals, 5, 0, 800, 1, 0 },
            new object[] { "w_ta100", "Time Attack: 100 floors", RewardStat.TimeAttackFloors, 100, 0, 500, 0, 0 },
            new object[] { "w_hard5", "Clear 5 hard levels", RewardStat.HardClear, 5, 16, 1000, 0, RewardEntry.RandomOpenRegionHero },
        };

        public static RewardCatalog BuiltIn()
        {
            var c = new RewardCatalog();
            c.BuildRegionGoals();
            foreach (object[] r in DailyRows) c.dailyActions.Add(E((string)r[0], (string)r[1], (string)r[2], (int)r[3], (int)r[4], 0, 0, 0));
            foreach (int[] r in DailyMilestoneRows) c.dailyMilestones.Add(E("dp_" + r[0], r[0] + " points", null, r[0], 0, r[1], r[2], r[3]));
            foreach (object[] r in WeeklyRows) c.weekly.Add(E((string)r[0], (string)r[1], (string)r[2], (int)r[3], 0, (int)r[4], (int)r[5], (int)r[6], (int)r[7]));
            return c;
        }

        /// <summary>
        /// Parses Content/economy/rewards.json: { "dailyActions": [..], "dailyMilestones": [..], "weekly": [..] }.
        /// Every entry has id, title, target and any of gems, coins, powerups, heroRegion; actions have stat and points; weekly tasks have stat.
        /// The region goals are not in the file: they come from <see cref="RegionGoalTable"/>.
        /// </summary>
        public static RewardCatalog FromJson(string json)
        {
            var c = new RewardCatalog();
            c.BuildRegionGoals();
            JObject root = JObject.Parse(json);
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
                    Points = (int?)t["points"] ?? 0, Gems = (int?)t["gems"] ?? 0, Coins = (int?)t["coins"] ?? 0, PowerUps = (int?)t["powerups"] ?? 0,
                    HeroRegion = (int?)t["heroRegion"] ?? 0
                });
            }
        }

        /// <summary>Finds an entry of the tab's tables by id (null when unknown). The Main tab finds region goals.</summary>
        public RewardEntry Find(RewardTab tab, string id)
        {
            if (id == null) return null;
            if (tab == RewardTab.Main) return FindGoal(id);
            if (tab == RewardTab.Daily) return FindIn(dailyMilestones, id);
            return FindIn(weekly, id);
        }

        /// <summary>Finds a region goal by id (g_region_type_tier), null when unknown.</summary>
        public RegionGoal FindGoal(string id)
        {
            if (id == null) return null;
            for (int i = 0; i < allGoals.Count; i++) if (allGoals[i].Id == id) return allGoals[i];
            return null;
        }

        static RewardEntry FindIn(List<RewardEntry> list, string id)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Id == id) return list[i];
            return null;
        }
    }
}
