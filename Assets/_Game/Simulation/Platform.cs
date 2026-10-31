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
        Seesaw = 14
    }

    /// <summary>Static helpers that classify platform kinds (used by the builder, the endless tower, the bot and the view).</summary>
    public static class PlatformKinds
    {
        /// <summary>Hazard-type new platforms: they cost the player something when misused (never a death).</summary>
        public static bool IsHazardType(PlatformKind k)
        {
            return k == PlatformKind.ThornBush || k == PlatformKind.SnapDaisy;
        }

        /// <summary>Platforms that launch the player (the landing speed on the next platform is not chosen by the player).</summary>
        public static bool IsLaunch(PlatformKind k)
        {
            return k == PlatformKind.Bounce || k == PlatformKind.BounceCap || k == PlatformKind.Dandelion;
        }

        /// <summary>True for the seven region 1 platforms added by doc 24.</summary>
        public static bool IsRegion1New(PlatformKind k)
        {
            return k >= PlatformKind.WindPetal && k <= PlatformKind.Seesaw;
        }

        /// <summary>Platforms whose surface or position depends on the clock or on the player (the bot keys its states by time for them).</summary>
        public static bool IsTimeDependent(PlatformKind k)
        {
            return k == PlatformKind.Moving || k == PlatformKind.Vanishing || k == PlatformKind.WindPetal || k == PlatformKind.HiveLift
                || k == PlatformKind.SnapDaisy || k == PlatformKind.Ladybug;
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
