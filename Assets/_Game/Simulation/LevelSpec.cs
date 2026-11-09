using System;

namespace BoingUpSkyTower.Simulation
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

        /// <summary>
        /// True when the region's signature art (RegionArt.Signature) shows the hazards. Only the gong region has hazard art; in every other region
        /// the signature art is a friendly platform (mushroom cap, flower, gum) and a wave hazard drawn with it looks like something to land on.
        /// </summary>
        public static bool HazardsUseSignatureArt(SignatureKind kind)
        {
            return kind == SignatureKind.SwingingGongs;
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

    /// <summary>
    /// The new special platforms of each region (doc 24: region 1 first, regions 2 to 5 add theirs later by adding a case here, nothing else
    /// in the builder or the endless tower changes). A level or endless tower uses the set of its signature unless LevelSpec.NewPlatforms is set.
    /// </summary>
    public static class RegionPlatformSets
    {
        private static readonly PlatformKind[] NoneSet = new PlatformKind[0];
        private static readonly PlatformKind[] Region1Set =
        {
            PlatformKind.WindPetal, PlatformKind.HiveLift, PlatformKind.SnapDaisy, PlatformKind.Dandelion,
            PlatformKind.ThornBush, PlatformKind.Ladybug, PlatformKind.Seesaw
        };

        private static readonly PlatformKind[] Region2Set =
        {
            PlatformKind.SquishCap, PlatformKind.SproutLift, PlatformKind.PuffFloat, PlatformKind.FairyRing,
            PlatformKind.OrbitCap, PlatformKind.DizzySpores, PlatformKind.SporePop
        };
        /// <summary>Generic kinds a region keeps next to its new set (doc 30 section 7; region 2: rotting log Crumble and dew moss Slippery).</summary>
        private static readonly PlatformKind[] Region2Generics = { PlatformKind.Crumble, PlatformKind.Slippery };

        private static readonly PlatformKind[] Region3Set =
        {
            PlatformKind.SurfBoostWave, PlatformKind.SandcastleStep, PlatformKind.ClamShellPad, PlatformKind.SinkingRaft,
            PlatformKind.TurtleShuttle, PlatformKind.KelpTangle, PlatformKind.PufferfishPad
        };
        /// <summary>Region 3 keeps the sand slab Crumble and the sea foam Vanishing, each once; the drifting platform (signature) is listed twice so it stays the most common kind.</summary>
        private static readonly PlatformKind[] Region3Generics = { PlatformKind.Crumble, PlatformKind.Vanishing };

        private static readonly PlatformKind[] Region4Set =
        {
            PlatformKind.CandyBowl, PlatformKind.FlossGlide, PlatformKind.FizzVent, PlatformKind.LicoriceBelt,
            PlatformKind.TaffyStretch, PlatformKind.JellyWobble, PlatformKind.PinwheelGust
        };
        /// <summary>Region 4 keeps the sugar crumble Crumble and the icing Slippery next to the sticky gum signature (doc 30 section 11 question 8).</summary>
        private static readonly PlatformKind[] Region4Generics = { PlatformKind.Crumble, PlatformKind.Slippery };

        private static readonly PlatformKind[] Region5Set =
        {
            PlatformKind.BambooFlick, PlatformKind.LanternLift, PlatformKind.KoiStone, PlatformKind.SwingPlank,
            PlatformKind.PagodaRidge, PlatformKind.MalletSmash, PlatformKind.RollingLog
        };
        /// <summary>Region 5 keeps the paper-screen Vanishing and the rope-bridge Moving (doc 30 section 11 question 8); the swinging gongs are hazard OBJECTS, not a platform kind (the signature adds no platform option).</summary>
        private static readonly PlatformKind[] Region5Generics = { PlatformKind.Moving, PlatformKind.Vanishing };

        /// <summary>True when the signature platform of the region is listed twice among the special options (it stays the most common kind).</summary>
        public static bool SignatureListedTwice(SignatureKind signature)
        {
            return signature == SignatureKind.DriftingPlatforms;
        }

        public static PlatformKind[] For(SignatureKind signature)
        {
            if (signature == SignatureKind.BounceFlowers) return Region1Set;
            if (signature == SignatureKind.BounceCaps) return Region2Set;
            if (signature == SignatureKind.DriftingPlatforms) return Region3Set;
            if (signature == SignatureKind.StickyGum) return Region4Set;
            if (signature == SignatureKind.SwingingGongs) return Region5Set;
            return NoneSet;
        }

        /// <summary>The generic kinds that join the new set of a region (empty for regions without a new set and for region 1).</summary>
        public static PlatformKind[] GenericsFor(SignatureKind signature)
        {
            if (signature == SignatureKind.DriftingPlatforms) return Region3Generics;
            if (signature == SignatureKind.StickyGum) return Region4Generics;
            if (signature == SignatureKind.SwingingGongs) return Region5Generics;
            return signature == SignatureKind.BounceCaps ? Region2Generics : NoneSet;
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
        /// <summary>
        /// Per-attempt seed (0 = none: the base layout of the level, used by tests and the golden hashes). Informational: <see cref="Seed"/> already holds
        /// MixAttemptSeed(base seed, AttemptSeed) when this is set, so the run can be reproduced from (level, AttemptSeed).
        /// </summary>
        public int AttemptSeed;

        /// <summary>
        /// Layout seed of one attempt: attempt 0 keeps the level's own seed (the base layout), any other value gives a different, fully
        /// deterministic layout. The builder keeps every difficulty setting of the level (it only draws from the level's ranges), so a new seed
        /// means a different but equally hard layout.
        /// </summary>
        public static int MixAttemptSeed(int baseSeed, int attemptSeed)
        {
            if (attemptSeed == 0) return baseSeed;
            unchecked
            {
                uint z = (uint)baseSeed * 0x9E3779B1u ^ (uint)attemptSeed * 0x85EBCA6Bu;
                z = (z ^ (z >> 16)) * 0x7FEB352Du;
                z = (z ^ (z >> 15)) * 0x846CA68Bu;
                z ^= z >> 16;
                return (int)z;
            }
        }

        /// <summary>A copy of this (base) spec whose layout seed is mixed with <paramref name="attemptSeed"/> (0 returns an identical copy). Call it on the base spec only.</summary>
        public LevelSpec WithAttemptSeed(int attemptSeed)
        {
            LevelSpec c = (LevelSpec)MemberwiseClone();
            c.Seed = MixAttemptSeed(Seed, attemptSeed);
            c.AttemptSeed = attemptSeed;
            return c;
        }

        /// <summary>Effective difficulty d in 0..1 (region offset already applied, see LevelGenerator.EffectiveDifficulty).</summary>
        public Fx Difficulty;
        /// <summary>
        /// New special platform kinds this level may use (RegionPlatformSets). Null = the set of <see cref="Signature"/>, empty = none.
        /// </summary>
        public PlatformKind[] NewPlatforms;

        public PlatformKind[] ActiveNewPlatforms()
        {
            return NewPlatforms != null ? NewPlatforms : RegionPlatformSets.For(Signature);
        }

        /// <summary>
        /// Difficulty the builder and the scroll use (doc 24, all regions): levels 1 and 2 and endless keep <see cref="Difficulty"/>; from level 3 it never
        /// falls below a floor that rises linearly from SimConfig.RampFloorLevel3Permille (level 3) to RampFloorLevel25Permille (level 25), so every
        /// level from 3 on is clearly harder than the one before, whatever its content value is.
        /// </summary>
        public Fx EffectiveDifficulty(SimConfig cfg)
        {
            if (Endless || LevelIndex < 3) return Difficulty;
            int idx = LevelIndex > LevelsInRegion ? LevelsInRegion : LevelIndex;
            Fx floor = Fx.FromRatio(cfg.RampFloorPermille(idx, LevelsInRegion), 1000);
            return Difficulty > floor ? Difficulty : floor;
        }

        /// <summary>
        /// The effective difficulty of campaign level <paramref name="levelIndex"/> (1..25) in a region with difficulty offset <paramref name="regionOffset"/>,
        /// computed the way the shipped levels get it (content difficulty with the offset applied, then the level 3..25 ramp floor). The endless cycle
        /// uses it for every floor, so a floor at level N has the layout difficulty of the region level N.
        /// </summary>
        public static Fx DifficultyForLevel(SimConfig cfg, int levelIndex, Fx regionOffset, int levelsInRegion = SimConfig.LevelsPerRegion)
        {
            int idx = levelIndex < 1 ? 1 : (levelIndex > levelsInRegion ? levelsInRegion : levelIndex);
            Fx eff = LevelGenerator.EffectiveDifficulty(Fx.FromRatio(cfg.ContentDifficultyPermille(idx, levelsInRegion), 1000), regionOffset);
            if (idx < 3) return eff;
            Fx floor = Fx.FromRatio(cfg.RampFloorPermille(idx, levelsInRegion), 1000);
            return eff > floor ? eff : floor;
        }

        /// <summary>Level number inside the region, 1..25 (regions 6 to 30 still have 15 until they are migrated). Drives the signature template (1-2 none, 3 gentle, 4+ normal).</summary>
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
        /// <summary>Endless only: a Time Attack tower. Its floor f has the difficulty of campaign level 9 + ceil(f / 100), at most the top level (floor 1 = level 10); see EndlessTower.LevelForFloorTimeAttack. Prefix stable and replayable from the seed like every endless tower.</summary>
        public bool TimeAttack;
        /// <summary>Endless only: the region's difficulty offset (0..0.25) and the floor where difficulty reaches 1 (remote config, 400).</summary>
        public Fx RegionOffset;
        public int EndlessDifficultyFloors = 400;
        /// <summary>Levels of the region this spec belongs to: 25 by the Bug List 6 rule (regions 1 to 5), 15 for the regions 6 to 30 until they are migrated (LevelSpecFactory sets it from the region's level count). It decides the top level (gem level, ramp anchors) and the endless cycle.</summary>
        public int LevelsInRegion = SimConfig.LevelsPerRegion;
        public bool IsTopLevel { get { return LevelIndex == LevelsInRegion; } }

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
                    case GoalType.FindHiddenGem:   // Bug List 6 item 4: a hidden gem level has Goal.Value rows (the gem sits half way, the level ends on its row)
                        rows = Goal.Value > 0 ? Goal.Value : DefaultRows;
                        break;
                    case GoalType.ComboChain:
                        // Doc 25 / Bug List 8 item 3: the combo level is level 5: 75 rows in the 25-level regions (1 to 5), 90 in the legacy 15-level regions; other combo specs keep the default
                        rows = LevelIndex == 5 ? (LevelsInRegion >= 25 ? 75 : 90) : DefaultRows;
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
