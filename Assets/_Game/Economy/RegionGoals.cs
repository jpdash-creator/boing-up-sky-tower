using System.Collections.Generic;
using FluffyClimbers.Simulation;

namespace FluffyClimbers.Economy
{
    /// <summary>The kinds of per-region Main goals (Bug List 5 F1.a, docs/39). Every goal counts ONLY play inside its region.</summary>
    public enum RegionGoalType
    {
        /// <summary>Stars earned in the region (best per level, max 75).</summary>
        Stars = 0,
        /// <summary>Different levels of the region cleared (max 25); the last tier is "clear all levels".</summary>
        Levels,
        /// <summary>Levels of the region with 3 stars (max 25).</summary>
        ThreeStarLevels,
        /// <summary>Jumps made in the region (campaign and endless).</summary>
        Jumps,
        /// <summary>Combo chains (3 landings or more) made in the region.</summary>
        Combos,
        /// <summary>Minutes played inside the region (runs only, not menus).</summary>
        PlayMinutes,
        /// <summary>Coins earned in the region (level payouts and endless payouts).</summary>
        Coins,
        /// <summary>Power-up units used in the region.</summary>
        PowerUps,
        /// <summary>Levels of the region cleared on Hard or Insane.</summary>
        HardClears,
        /// <summary>Clears of the region's no-wall-bounce level (level 10).</summary>
        NoWallClears,
        /// <summary>Best floor reached in the region's Endless tower.</summary>
        EndlessFloors,
        /// <summary>Levels of the region with a Gold medal in at least one Gameplay mode (doc 46 section 8; max 25). Appended last: ids of the older goals never change.</summary>
        GoldMedalLevels
    }

    /// <summary>One goal of one region: an id that never changes, a target, a reward. <see cref="RewardEntry.Stat"/> is the type token.</summary>
    public sealed class RegionGoal : RewardEntry
    {
        public int Region;
        public RegionGoalType Type;
        /// <summary>1-based tier inside its type (1 = easiest).</summary>
        public int Tier;
        /// <summary>True for the endless tiers (they may pay slightly more, see docs/39).</summary>
        public bool IsEndless { get { return Type == RegionGoalType.EndlessFloors; } }
        /// <summary>True for the gold medal tiers (doc 46): same coins as the star tiers; their last tier is one of the eight gem goals.</summary>
        public bool IsMedal { get { return Type == RegionGoalType.GoldMedalLevels; } }
    }

    /// <summary>
    /// THE reward table of the Main tab (docs/39): per type the base targets and the reward of each tier, the per-region scaling of the targets and of the coins.
    /// Retune here only. Regions 1..<see cref="PlayableRegions"/> have goals, the others are Coming Soon and have none.
    /// </summary>
    public static class RegionGoalTable
    {
        /// <summary>The map has 30 regions; the Main tab shows all of them.</summary>
        public const int TotalRegions = 30;
        /// <summary>Regions 1 to 5 are playable and have goals; 6 to 30 are Coming Soon.</summary>
        public const int PlayableRegions = 5;
        /// <summary>Levels per region (Bug List 6 item 4: 25; regions 1 to 5 are the playable ones and have goals, so this is the count the goals use).</summary>
        public const int LevelsPerRegion = 25;
        public const int StarsPerRegion = LevelsPerRegion * 3;
        /// <summary>The level that is the no-wall-bounce challenge in every region.</summary>
        public const int NoWallLevelIndex = 10;

        /// <summary>Targets of jumps, combos, play minutes and coins grow by region: percent of the base target for regions 1..5.</summary>
        public static readonly int[] TargetPercent = { 100, 125, 150, 180, 220 };
        /// <summary>Power-ups used scale gently (they cost the player money): percent of the base target.</summary>
        public static readonly int[] PowerUpTargetPercent = { 100, 100, 125, 150, 175 };
        /// <summary>Coin rewards grow a little with the region (gems and power-ups do not scale). Owner 2026-11-10: the SAME factor as the level payout (region coinFactor 1.00 to 1.12), one table in <see cref="CoinPayoutRules.RegionFactorPercent"/>.</summary>
        public static int[] CoinPercent { get { return CoinPayoutRules.RegionFactorPercent; } }

        /// <summary>One tier row: base target (before region scaling) and the reward.</summary>
        public struct Tier
        {
            public int Target, Gems, Coins, PowerUps;
            public Tier(int target, int gems, int coins, int powerUps) { Target = target; Gems = gems; Coins = coins; PowerUps = powerUps; }
        }

        // Owner 2026-11-13 (economy review, docs/39): gems ONLY on the eight milestone goals of a region (50 per region, 250 in all): clear all 25 levels 10, 3 stars on 25 levels 10, gold medals on
        // 25 levels 10, 75 stars 8, 6 hard levels 4, Endless floor 500 3, Endless floor 1000 3, no-wall level 3 times 2. Every other goal pays coins (and power-ups) only. All coin rewards are 20 percent
        // below the old table (these are the base values; ScaledCoins applies the region factor). Common hero gifts: only "clear all 25 levels" and "gold medals on 25 levels".
        // Total claim coins per region are about 80 percent of the old table (rebalanced 2026-11-13). Former hero tiers pay 150 to 220 coins and gem-only goals 50 to 100 coins.
        public static readonly Tier[] StarsTiers =
        {
            new Tier(15, 0, 100, 0), new Tier(30, 0, 60, 0), new Tier(45, 0, 200, 0), new Tier(60, 0, 60, 0), new Tier(75, 8, 150, 1)
        };
        /// <summary>The last tier (clear all 25 levels) also gives the region's Common hero (1 copy): there is NO separate "region cleared" bonus any more. Tiers 2 to 4 pay coins instead of a hero (owner 2026-11-13).</summary>
        public static readonly Tier[] LevelsTiers =
        {
            new Tier(5, 0, 60, 0), new Tier(10, 0, 150, 0), new Tier(15, 0, 180, 0), new Tier(20, 0, 220, 0), new Tier(25, 10, 400, 0)
        };
        /// <summary>
        /// Gold medal levels (doc 46 section 8): 5 / 10 / 15 / 20 / 25 levels. Coins about 20 percent below the old table; the last tier (gold medals on 25 levels) pays 10 gems, 1 power-up and one random
        /// Common hero (see <see cref="GivesRandomHero"/>).
        /// </summary>
        public static readonly Tier[] GoldMedalTiers =
        {
            new Tier(5, 0, 100, 0), new Tier(10, 0, 60, 0), new Tier(15, 0, 120, 0), new Tier(20, 0, 60, 0), new Tier(25, 10, 0, 1)
        };
        public static readonly Tier[] ThreeStarTiers = { new Tier(5, 0, 140, 0), new Tier(13, 0, 60, 0), new Tier(25, 10, 180, 0) };
        public static readonly Tier[] JumpsTiers = { new Tier(400, 0, 90, 0), new Tier(1200, 0, 110, 0), new Tier(3000, 0, 50, 0) };
        public static readonly Tier[] CombosTiers = { new Tier(10, 0, 90, 0), new Tier(40, 0, 110, 0), new Tier(100, 0, 50, 0) };
        public static readonly Tier[] PlayMinutesTiers = { new Tier(15, 0, 90, 0), new Tier(45, 0, 110, 0), new Tier(120, 0, 50, 0) };
        public static readonly Tier[] CoinsTiers = { new Tier(1000, 0, 60, 0), new Tier(3500, 0, 80, 0), new Tier(8000, 0, 50, 0) };
        public static readonly Tier[] PowerUpsTiers = { new Tier(3, 0, 90, 0), new Tier(10, 0, 60, 0), new Tier(25, 0, 180, 1) };
        public static readonly Tier[] HardClearsTiers = { new Tier(1, 0, 100, 0), new Tier(3, 0, 70, 0), new Tier(6, 4, 0, 0) };
        public static readonly Tier[] NoWallTiers = { new Tier(1, 0, 70, 0), new Tier(3, 2, 0, 0) };
        /// <summary>
        /// Endless floors (owner list 100 ... 10,000; targets are floors, never scaled). Gems only on floor 500 and floor 1000 (3 each, 6 per region); the coins are the old values minus 20 percent.
        /// </summary>
        public static readonly Tier[] EndlessTiers =
        {
            new Tier(100, 0, 80, 0), new Tier(200, 0, 120, 0), new Tier(500, 3, 200, 0), new Tier(1000, 3, 280, 0), new Tier(1500, 0, 360, 1),
            new Tier(2000, 0, 440, 0), new Tier(3000, 0, 560, 0), new Tier(4000, 0, 680, 0), new Tier(5000, 0, 800, 1), new Tier(7500, 0, 1040, 0),
            new Tier(10000, 0, 1280, 2)
        };

        /// <summary>Short stable token of a type, used in goal ids: g_&lt;region&gt;_&lt;token&gt;_&lt;tier&gt;.</summary>
        public static string Token(RegionGoalType t)
        {
            switch (t)
            {
                case RegionGoalType.Stars: return "stars";
                case RegionGoalType.Levels: return "levels";
                case RegionGoalType.ThreeStarLevels: return "stars3";
                case RegionGoalType.Jumps: return "jumps";
                case RegionGoalType.Combos: return "combos";
                case RegionGoalType.PlayMinutes: return "time";
                case RegionGoalType.Coins: return "coins";
                case RegionGoalType.PowerUps: return "powerups";
                case RegionGoalType.HardClears: return "hard";
                case RegionGoalType.NoWallClears: return "nowall";
                case RegionGoalType.GoldMedalLevels: return "gold";
                default: return "endless";
            }
        }

        public static string GoalId(int region, RegionGoalType type, int tier) { return "g_" + region + "_" + Token(type) + "_" + tier; }

        /// <summary>The types in the order the goals are listed.</summary>
        public static readonly RegionGoalType[] Order =
        {
            RegionGoalType.Stars, RegionGoalType.Levels, RegionGoalType.ThreeStarLevels, RegionGoalType.Jumps, RegionGoalType.Combos,
            RegionGoalType.PlayMinutes, RegionGoalType.Coins, RegionGoalType.PowerUps, RegionGoalType.HardClears, RegionGoalType.NoWallClears,
            RegionGoalType.GoldMedalLevels, RegionGoalType.EndlessFloors
        };   // the endless tiers stay last: the Rewards screen puts them under their own heading

        public static Tier[] TiersOf(RegionGoalType t)
        {
            switch (t)
            {
                case RegionGoalType.Stars: return StarsTiers;
                case RegionGoalType.Levels: return LevelsTiers;
                case RegionGoalType.ThreeStarLevels: return ThreeStarTiers;
                case RegionGoalType.Jumps: return JumpsTiers;
                case RegionGoalType.Combos: return CombosTiers;
                case RegionGoalType.PlayMinutes: return PlayMinutesTiers;
                case RegionGoalType.Coins: return CoinsTiers;
                case RegionGoalType.PowerUps: return PowerUpsTiers;
                case RegionGoalType.HardClears: return HardClearsTiers;
                case RegionGoalType.NoWallClears: return NoWallTiers;
                case RegionGoalType.GoldMedalLevels: return GoldMedalTiers;
                default: return EndlessTiers;
            }
        }

        public static bool IsPlayable(int region) { return region >= 1 && region <= PlayableRegions; }

        /// <summary>Rounds a scaled target to a clean number (steps of 1, 5, 10 or 100 by size).</summary>
        static int Nice(long v)
        {
            long step = v >= 1000 ? 100 : v >= 100 ? 10 : v >= 20 ? 5 : 1;
            return (int)((v + step / 2) / step * step);
        }

        static int ScaledTarget(RegionGoalType t, int baseTarget, int region)
        {
            int[] pct;
            switch (t)
            {
                case RegionGoalType.Jumps:
                case RegionGoalType.Combos:
                case RegionGoalType.PlayMinutes:
                case RegionGoalType.Coins: pct = TargetPercent; break;
                case RegionGoalType.PowerUps: pct = PowerUpTargetPercent; break;
                default: return baseTarget;
            }
            return Nice((long)baseTarget * pct[region - 1] / 100);
        }

        /// <summary>Coins of a region: the base reward times the region percent, rounded to 10 coins.</summary>
        public static int ScaledCoins(int coins, int region)
        {
            if (coins <= 0 || !IsPlayable(region)) return 0;
            return (int)(((long)coins * CoinPercent[region - 1] + 500) / 1000 * 10);
        }

        public static string TitleOf(RegionGoalType t, int target)
        {
            switch (t)
            {
                case RegionGoalType.Stars: return "Earn " + target + " stars";
                case RegionGoalType.Levels: return target >= LevelsPerRegion ? "Clear all " + target + " levels" : "Clear " + target + " levels";
                case RegionGoalType.ThreeStarLevels: return "Get 3 stars on " + target + " levels";
                case RegionGoalType.Jumps: return "Make " + target + " jumps";
                case RegionGoalType.Combos: return "Make " + target + " combos";
                case RegionGoalType.PlayMinutes: return "Play " + target + " minutes";
                case RegionGoalType.Coins: return "Earn " + target + " coins";
                case RegionGoalType.PowerUps: return "Use " + target + " power-ups";
                case RegionGoalType.HardClears: return target == 1 ? "Clear a hard level" : "Clear " + target + " hard levels";
                case RegionGoalType.NoWallClears: return target == 1 ? "Clear the no-wall level" : "Clear the no-wall level " + target + " times";
                case RegionGoalType.GoldMedalLevels: return "Gold medals on " + target + " levels";
                default: return "Reach floor " + target + " in Endless";
            }
        }

        /// <summary>
        /// Hero gifts of the Main goals (owner 2026-11-13): only two per region. "Clear all 25 levels" hands over the region's own Common hero (see Build); "gold medals on 25 levels" hands out one random ACTIVE
        /// Common hero of an open region (<see cref="RewardEntry.RandomOpenRegionHero"/>, deterministic from the per-account seed). Nothing in RegionGoals names a hero id.
        /// </summary>
        public static bool GivesRandomHero(RegionGoalType type, int tier)
        {
            return type == RegionGoalType.GoldMedalLevels && tier == 5;
        }

        /// <summary>The goals of a region in listing order (empty for a Coming Soon region or an invalid index).</summary>
        public static List<RegionGoal> Build(int region)
        {
            var list = new List<RegionGoal>(48);
            if (!IsPlayable(region)) return list;
            foreach (RegionGoalType type in Order)
            {
                Tier[] tiers = TiersOf(type);
                for (int i = 0; i < tiers.Length; i++)
                {
                    int target = type == RegionGoalType.EndlessFloors ? tiers[i].Target : ScaledTarget(type, tiers[i].Target, region);
                    bool last = type == RegionGoalType.Levels && i == tiers.Length - 1;
                    list.Add(new RegionGoal
                    {
                        Id = GoalId(region, type, i + 1), Title = TitleOf(type, target), Stat = Token(type), Target = target,
                        Gems = tiers[i].Gems, Coins = ScaledCoins(tiers[i].Coins, region), PowerUps = tiers[i].PowerUps,
                        HeroRegion = last ? region : GivesRandomHero(type, i + 1) ? RewardEntry.RandomOpenRegionHero : 0, Region = region, Type = type, Tier = i + 1
                    });
                }
            }
            return list;
        }
    }
}
