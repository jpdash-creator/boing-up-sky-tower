using System.Collections.Generic;

namespace BoingUpSkyTower.Economy
{
    /// <summary>The kinds of per-region Main goals (Bug List 5 F1.a, docs/39). Every goal counts ONLY play inside its region.</summary>
    public enum RegionGoalType
    {
        /// <summary>Stars earned in the region (best per level, max 45).</summary>
        Stars = 0,
        /// <summary>Different levels of the region cleared (max 15); the last tier is "clear all levels".</summary>
        Levels,
        /// <summary>Levels of the region with 3 stars (max 15).</summary>
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
        /// <summary>Clears of the region's no-wall-bounce level (level 7).</summary>
        NoWallClears,
        /// <summary>Best floor reached in the region's Endless tower.</summary>
        EndlessFloors
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
        public const int LevelsPerRegion = 15;
        public const int StarsPerRegion = LevelsPerRegion * 3;
        /// <summary>The level that is the no-wall-bounce challenge in every region.</summary>
        public const int NoWallLevelIndex = 7;

        /// <summary>Targets of jumps, combos, play minutes and coins grow by region: percent of the base target for regions 1..5.</summary>
        public static readonly int[] TargetPercent = { 100, 125, 150, 180, 220 };
        /// <summary>Power-ups used scale gently (they cost the player money): percent of the base target.</summary>
        public static readonly int[] PowerUpTargetPercent = { 100, 100, 125, 150, 175 };
        /// <summary>Coin rewards grow a little with the region: percent of the base coin reward for regions 1..5 (gems and power-ups do not scale).</summary>
        public static readonly int[] CoinPercent = { 100, 108, 116, 124, 132 };

        /// <summary>One tier row: base target (before region scaling) and the reward.</summary>
        public struct Tier
        {
            public int Target, Gems, Coins, PowerUps;
            public Tier(int target, int gems, int coins, int powerUps) { Target = target; Gems = gems; Coins = coins; PowerUps = powerUps; }
        }

        public static readonly Tier[] StarsTiers =
        {
            new Tier(9, 0, 200, 0), new Tier(18, 3, 0, 0), new Tier(27, 0, 400, 0), new Tier(36, 4, 0, 0), new Tier(45, 6, 0, 1)
        };
        /// <summary>The last tier (clear all 15 levels) also gives the region's Common hero (1 copy): there is NO separate "region cleared" bonus any more.</summary>
        public static readonly Tier[] LevelsTiers =
        {
            new Tier(3, 0, 150, 0), new Tier(6, 2, 0, 0), new Tier(9, 0, 300, 0), new Tier(12, 4, 0, 0), new Tier(15, 8, 500, 0)
        };
        public static readonly Tier[] ThreeStarTiers = { new Tier(3, 0, 200, 0), new Tier(8, 4, 0, 0), new Tier(15, 8, 300, 0) };
        public static readonly Tier[] JumpsTiers = { new Tier(400, 0, 150, 0), new Tier(1200, 2, 200, 0), new Tier(3000, 4, 0, 0) };
        public static readonly Tier[] CombosTiers = { new Tier(10, 0, 150, 0), new Tier(40, 2, 200, 0), new Tier(100, 4, 0, 0) };
        public static readonly Tier[] PlayMinutesTiers = { new Tier(15, 0, 150, 0), new Tier(45, 2, 200, 0), new Tier(120, 4, 0, 0) };
        public static readonly Tier[] CoinsTiers = { new Tier(1000, 0, 100, 0), new Tier(3500, 1, 150, 0), new Tier(8000, 3, 0, 0) };
        public static readonly Tier[] PowerUpsTiers = { new Tier(3, 0, 150, 0), new Tier(10, 2, 0, 0), new Tier(25, 0, 300, 1) };
        public static readonly Tier[] HardClearsTiers = { new Tier(1, 0, 200, 0), new Tier(3, 2, 0, 0), new Tier(6, 4, 0, 0) };
        public static readonly Tier[] NoWallTiers = { new Tier(1, 2, 100, 0), new Tier(3, 3, 0, 0) };
        /// <summary>
        /// Endless floors (owner list 100 ... 10,000; targets are floors, never scaled). Difficult achievements, so they pay slightly more than the tight
        /// gem policy would for the same effort.
        /// </summary>
        public static readonly Tier[] EndlessTiers =
        {
            new Tier(100, 1, 100, 0), new Tier(200, 1, 150, 0), new Tier(500, 2, 250, 0), new Tier(1000, 2, 350, 0), new Tier(1500, 2, 450, 1),
            new Tier(2000, 2, 550, 0), new Tier(3000, 2, 700, 0), new Tier(4000, 2, 850, 0), new Tier(5000, 2, 1000, 1), new Tier(7500, 3, 1300, 0),
            new Tier(10000, 4, 1600, 2)
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
                default: return "endless";
            }
        }

        public static string GoalId(int region, RegionGoalType type, int tier) { return "g_" + region + "_" + Token(type) + "_" + tier; }

        /// <summary>The types in the order the goals are listed.</summary>
        public static readonly RegionGoalType[] Order =
        {
            RegionGoalType.Stars, RegionGoalType.Levels, RegionGoalType.ThreeStarLevels, RegionGoalType.Jumps, RegionGoalType.Combos,
            RegionGoalType.PlayMinutes, RegionGoalType.Coins, RegionGoalType.PowerUps, RegionGoalType.HardClears, RegionGoalType.NoWallClears,
            RegionGoalType.EndlessFloors
        };

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
                default: return "Reach floor " + target + " in Endless";
            }
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
                        HeroRegion = last ? region : 0, Region = region, Type = type, Tier = i + 1
                    });
                }
            }
            return list;
        }
    }
}
