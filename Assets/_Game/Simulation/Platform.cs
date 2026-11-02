namespace BoingUpSkyTower.Simulation
{
    public enum PlatformKind
    {
        Normal = 0,
        Moving = 1,
        Crumble = 2,
        Bounce = 3,
        Vanishing = 4,
        Sticky = 5,
        Slippery = 6,
        /// <summary>Region 2 signature: launches higher than Bounce (SimConfig.BounceCapMultiplier).</summary>
        BounceCap = 7,

        // ---- Region 1 special platforms (doc 24); values are stable, the art catalog looks them up by kind ----
        /// <summary>Wind Petal: while standing on it a gust pushes sideways (SimConfig.WindPush); the direction flips every WindFlipTicks, announced WindCueTicks before.</summary>
        WindPetal = 8,
        /// <summary>Bee Hive Lift: bobs up and down (SimConfig.HiveBobAmplitude, HivePeriodTicks).</summary>
        HiveLift = 9,
        /// <summary>Snap Daisy (hazard type): closes SnapCloseTicks after landing, reopens SnapReopenTicks later.</summary>
        SnapDaisy = 10,
        /// <summary>Dandelion Puff (launch type): launches diagonally in Platform.Dir.</summary>
        Dandelion = 11,
        /// <summary>Thorn Bush (hazard type): the half on side Platform.Dir is thorny; landing or stepping on it knocks the player away, never kills.</summary>
        ThornBush = 12,
        /// <summary>Ladybug Rider: short horizontal path, runs faster while the player stands on it.</summary>
        Ladybug = 13,
        /// <summary>Seesaw Leaf: tilts toward the landing side and slides the player toward that edge.</summary>
        Seesaw = 14,

        // ---- Region 2 (Mushroom Grove) special platforms (doc 30 section 3); values are stable, the art catalog looks them up by kind ----
        /// <summary>Squish Cap (helpful): sinks while stood on (SimConfig.SquishDepth over SquishTicks); a jump from a squashed cap launches up to x1.45 higher.</summary>
        SquishCap = 15,
        /// <summary>Sprout Lift (helpful): rises SimConfig.SproutRise after the landing while stood on, sinks back once empty.</summary>
        SproutLift = 16,
        /// <summary>Puff Float (helpful): after a jump from it the fall is slowed for PuffFloatTicks.</summary>
        PuffFloat = 17,
        /// <summary>Fairy Ring (demanding): a wide cap with a hole in the middle (Platform.Amplitude = half width of the hole); stepping into the hole drops the player.</summary>
        FairyRing = 18,
        /// <summary>Orbit Cap (demanding): travels a small ellipse (Platform.Amplitude = sideways reach, SimConfig.OrbitAmplitudeY up and down), period Platform.Period.</summary>
        OrbitCap = 19,
        /// <summary>Dizzy Spores (hazard type): landing locks left/right input for SimConfig.DizzyLockTicks (controls are never inverted; jumping stays possible).</summary>
        DizzySpores = 20,
        /// <summary>Spore Pop (hazard type): swells SporePopTicks after the landing, then flings the player up and toward Platform.Dir if still on it; flat for SporePopFlatTicks.</summary>
        SporePop = 21,

        // ---- Region 3 (Sunny Beach) special platforms (doc 30 section 4); values are stable, the art catalog looks them up by kind ----
        /// <summary>Surf Boost Wave (helpful): while stood on, accelerates the player along Platform.Dir (toward the middle of the screen) at SimConfig.SurfAccel.</summary>
        SurfBoostWave = 22,
        /// <summary>Sandcastle Step (helpful): two levels; the half on side Platform.Dir is SimConfig.SandcastleRise (Platform.Amplitude) higher, the other half is at Platform.Y.</summary>
        SandcastleStep = 23,
        /// <summary>Clam Shell Pad (launch type): opens and shuts on a global clock (ClamCycleTicks, open for ClamOpenTicks); a landing while open launches x1.4.</summary>
        ClamShellPad = 24,
        /// <summary>Sinking Raft (demanding): sinks RaftSinkDepth over RaftSinkTicks while stood on and springs back once empty.</summary>
        SinkingRaft = 25,
        /// <summary>Turtle Shuttle (demanding): swims stop-and-go (TurtlePeriodTicks): pause, glide 2 x Platform.Amplitude, pause, glide back.</summary>
        TurtleShuttle = 26,
        /// <summary>Kelp Tangle (hazard type): after a landing no jump for KelpLockTicks; the jump input is kept and fires when the lock ends.</summary>
        KelpTangle = 27,
        /// <summary>Pufferfish Pad (hazard type): calm, swelling, puffed on a global cycle (PufferCycleTicks); landing or standing while puffed hops the player away, never a hit.</summary>
        PufferfishPad = 28
    }

    /// <summary>Static helpers that classify platform kinds (used by the builder, the endless tower, the bot and the view).</summary>
    public static class PlatformKinds
    {
        /// <summary>Hazard-type new platforms: they cost the player something when misused (never a death).</summary>
        public static bool IsHazardType(PlatformKind k)
        {
            return k == PlatformKind.ThornBush || k == PlatformKind.SnapDaisy || k == PlatformKind.DizzySpores || k == PlatformKind.SporePop
                || k == PlatformKind.KelpTangle || k == PlatformKind.PufferfishPad;
        }

        /// <summary>Platforms that launch the player (the landing speed on the next platform is not chosen by the player).</summary>
        public static bool IsLaunch(PlatformKind k)
        {
            return k == PlatformKind.Bounce || k == PlatformKind.BounceCap || k == PlatformKind.Dandelion || k == PlatformKind.ClamShellPad;
        }

        /// <summary>Pads whose launch speed is set by the previous flight and that the generator follows with a close next row (BounceNextGapMax, BounceNextDxMax): Bounce, BounceCap, Clam Shell Pad.</summary>
        public static bool IsBouncePad(PlatformKind k)
        {
            return k == PlatformKind.Bounce || k == PlatformKind.BounceCap || k == PlatformKind.ClamShellPad;
        }

        /// <summary>True for the seven region 1 platforms added by doc 24.</summary>
        public static bool IsRegion1New(PlatformKind k)
        {
            return k >= PlatformKind.WindPetal && k <= PlatformKind.Seesaw;
        }

        /// <summary>True for the seven region 2 platforms added by doc 30.</summary>
        public static bool IsRegion2New(PlatformKind k)
        {
            return k >= PlatformKind.SquishCap && k <= PlatformKind.SporePop;
        }

        /// <summary>True for the seven region 3 platforms added by doc 30.</summary>
        public static bool IsRegion3New(PlatformKind k)
        {
            return k >= PlatformKind.SurfBoostWave && k <= PlatformKind.PufferfishPad;
        }

        /// <summary>True for every region special platform (regions 1 to 3): they carry Params, painted pieces and the view's motion layer.</summary>
        public static bool IsRegionNew(PlatformKind k)
        {
            return k >= PlatformKind.WindPetal && k <= PlatformKind.PufferfishPad;
        }

        /// <summary>
        /// Hard kinds that depend on position or timing (doc 30 section 8 item 3; Fairy Ring, Orbit Cap, Sinking Raft, Turtle Shuttle) never sit in adjacent rows,
        /// next to a hazard or in the last rows (SpecialPlatforms.Allowed).
        /// </summary>
        public static bool IsDemanding(PlatformKind k)
        {
            return k == PlatformKind.FairyRing || k == PlatformKind.OrbitCap || k == PlatformKind.SinkingRaft || k == PlatformKind.TurtleShuttle;
        }

        /// <summary>Platforms whose surface or position depends on the clock or on the player (the bot keys its states by time for them).</summary>
        public static bool IsTimeDependent(PlatformKind k)
        {
            return k == PlatformKind.Moving || k == PlatformKind.Vanishing || k == PlatformKind.WindPetal || k == PlatformKind.HiveLift
                || k == PlatformKind.SnapDaisy || k == PlatformKind.Ladybug || k == PlatformKind.OrbitCap || k == PlatformKind.SporePop
                || k == PlatformKind.SproutLift || k == PlatformKind.ClamShellPad || k == PlatformKind.TurtleShuttle || k == PlatformKind.PufferfishPad;
        }
    }

    /// <summary>A one-way platform. Y is the top surface; X is the centre (base centre for moving platforms).</summary>
    public struct Platform
    {
        public int Id;
        public int Row;
        public PlatformKind Kind;
        public Fx X;
        public Fx Y;
        public Fx Width;
        /// <summary>Moving and Ladybug platforms: horizontal amplitude and triangle-wave period in ticks.</summary>
        public Fx Amplitude;
        public int Period;
        public int Phase;
        /// <summary>
        /// Direction (-1 / +1, 0 = unused): Wind Petal first gust direction, Dandelion launch direction, Thorn Bush side of the thorny half.
        /// </summary>
        public int Dir;
    }
}
