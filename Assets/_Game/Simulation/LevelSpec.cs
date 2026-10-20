using System;

namespace BoingTower.Simulation
{
    public enum GoalType
    {
        ReachFloor = 0,
        CollectCoins = 1,
        ComboChain = 2,
        NoWallBounce = 3,
        FindHiddenGem = 4,
        Speedrun = 5,
        SurviveWave = 6,
        BossChase = 7
    }

    public enum SignatureKind
    {
        None = 0,
        BounceFlowers = 1,
        BounceCaps = 2,
        DriftingPlatforms = 3,
        StickyGum = 4,
        SwingingGongs = 5
    }

    /// <summary>Id mapping helpers for content JSON (the Simulation assembly has no JSON dependency).</summary>
    public static class SignatureKinds
    {
        /// <summary>Maps a module id from Content/config/modules.json to a signature. Unknown or null ids give None.</summary>
        public static SignatureKind FromModuleId(string id)
        {
            switch (id)
            {
                case "mod_bounce_flowers": return SignatureKind.BounceFlowers;
                case "mod_bounce_caps": return SignatureKind.BounceCaps;
                case "mod_drifting_platforms": return SignatureKind.DriftingPlatforms;
                case "mod_sticky_gum": return SignatureKind.StickyGum;
                case "mod_swinging_gongs": return SignatureKind.SwingingGongs;
                default: return SignatureKind.None;
            }
        }

        /// <summary>The platform kind a signature adds to the spine (None for gongs, which are hazards).</summary>
        public static PlatformKind PlatformFor(SignatureKind kind)
        {
            switch (kind)
            {
                case SignatureKind.BounceFlowers: return PlatformKind.Bounce;
                case SignatureKind.BounceCaps: return PlatformKind.BounceCap;
                case SignatureKind.DriftingPlatforms: return PlatformKind.Moving;
                case SignatureKind.StickyGum: return PlatformKind.Sticky;
                default: return PlatformKind.Normal;
            }
        }
    }

    public static class GoalTypes
    {
        /// <summary>Maps the content JSON goal type string (reachFloor, collectCoins, ...) to GoalType; unknown gives ReachFloor.</summary>
        public static GoalType FromId(string id)
        {
            if (id == null) return GoalType.ReachFloor;
            switch (id.ToLowerInvariant())
            {
                case "collectcoins": return GoalType.CollectCoins;
                case "combochain": return GoalType.ComboChain;
                case "nowallbounce": return GoalType.NoWallBounce;
                case "findhiddengem": return GoalType.FindHiddenGem;
                case "speedrun": return GoalType.Speedrun;
                case "survivewave": return GoalType.SurviveWave;
                case "bosschase": return GoalType.BossChase;
                default: return GoalType.ReachFloor;
            }
        }
    }

    public sealed class GoalSpec
    {
        public GoalType Type;
        /// <summary>Target floor for ReachFloor, NoWallBounce, Speedrun, BossChase; floors per jump for ComboChain.</summary>
        public int Value;
        /// <summary>CollectCoins: coins to collect. ComboChain: how many such jumps.</summary>
        public int Count;
        /// <summary>Speedrun: time limit in seconds.</summary>
        public int TimeSec;
    }

    public sealed class StarSpec
    {
        public const int KindClear = 0;
        public const int KindCoins = 1;
        public const int KindNoRevive = 2;
        public const int KindTime = 3;

        /// <summary>0 clear, 1 coins (Value = coin target), 2 noRevive, 3 time (Value = seconds).</summary>
        public int Kind;
        public int Value;

        public static int KindFromId(string id)
        {
            if (id == null) return KindClear;
            switch (id.ToLowerInvariant())
            {
                case "coins": return KindCoins;
                case "norevive": return KindNoRevive;
                case "time": return KindTime;
                default: return KindClear;
            }
        }
    }

    /// <summary>Everything the builder needs for one campaign level. Pure data, no JSON.</summary>
    public sealed class LevelSpec
    {
        public const int MinRows = 30;
        public const int DefaultRows = 60;

        public int Seed;
        /// <summary>Effective difficulty d in 0..1 (region offset already applied, see LevelGenerator.EffectiveDifficulty).</summary>
        public Fx Difficulty;
        /// <summary>Level number inside the region, 1..15. Drives the signature template (1-2 none, 3 gentle, 4+ normal).</summary>
        public int LevelIndex = 1;
        /// <summary>0 simple, 1 medium, 2 hard, 3 complex, 4 highly complex.</summary>
        public int Class;
        public GoalSpec Goal = new GoalSpec();
        public StarSpec[] Stars = new StarSpec[0];
        public SignatureKind Signature;
        /// <summary>Campaign scroll ramp (BossChase): scroll speed grows with the row, clamped to SimConfig.CampaignRampCapPercent.</summary>
        public bool Ramp;
        /// <summary>Authored chunk ids (chunk_complex_a ...) spliced into the level, empty for none.</summary>
        public string[] FixedChunks = new string[0];
        /// <summary>Explicit platform rows above the start; 0 = derive with RowsFor().</summary>
        public int Rows;
        /// <summary>Endless mode (EndlessTower): no goal, the Goal field is a never-reached placeholder. Campaign specs leave this false.</summary>
        public bool Endless;
        /// <summary>Endless only: the region's difficulty offset (0..0.25) and the floor where difficulty reaches 1 (remote config, 400).</summary>
        public Fx RegionOffset;
        public int EndlessDifficultyFloors = 400;

        /// <summary>
        /// Number of platform rows (top row index). Rule: an explicit Rows wins; ReachFloor, Speedrun and BossChase
        /// use Goal.Value; every other goal uses 60 (DefaultRows). The result is never below 30 (MinRows).
        /// </summary>
        public int RowsFor()
        {
            int rows = Rows;
            if (rows <= 0)
            {
                switch (Goal.Type)
                {
                    case GoalType.ReachFloor:
                    case GoalType.Speedrun:
                    case GoalType.BossChase:
                    case GoalType.NoWallBounce:
                        rows = Goal.Value > 0 ? Goal.Value : DefaultRows;
                        break;
                    default:
                        rows = DefaultRows;
                        break;
                }
            }
            return rows < MinRows ? MinRows : rows;
        }

        /// <summary>Target row of the goal for the row-based goals (the top row for the others).</summary>
        public int TargetRow()
        {
            switch (Goal.Type)
            {
                case GoalType.ReachFloor:
                case GoalType.Speedrun:
                case GoalType.BossChase:
                case GoalType.NoWallBounce:
                    return Goal.Value > 0 ? Goal.Value : RowsFor();
                default:
                    return RowsFor();
            }
        }
    }
}
