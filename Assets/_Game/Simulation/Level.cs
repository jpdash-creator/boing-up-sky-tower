namespace SkyHopper.Simulation
{
    public enum PickupKind
    {
        Coin = 0,
        Gem = 1,
        /// <summary>The goal item of FindHiddenGem levels.</summary>
        HiddenGem = 2,
        /// <summary>One of the 3 collectible stars of a level (Tag = star index 0..2). Economy v2 (owner, 2026-10-20).</summary>
        Star = 3
    }

    /// <summary>A collectable. Collected once when the player centre (X, feet Y + PlayerCenterY) is within Radius.</summary>
    public struct Pickup
    {
        public int Id;
        public PickupKind Kind;
        public Fx X, Y;
        public Fx Radius;
        /// <summary>Star: star index 0..2. Gem: rarity 0 common, 1 rare, 2 epic, 3 legendary.</summary>
        public int Tag;
    }

    public enum HazardKind
    {
        /// <summary>Swinging gong: knocks the player back, never kills (region 5 signature).</summary>
        Gong = 0
    }

    /// <summary>
    /// A hazard on a deterministic triangle-wave path. X/Y is the anchor (centre of the swing). The
    /// position at a tick is computed by Sim.HazardPosition (integer math only).
    /// </summary>
    public struct Hazard
    {
        public int Id;
        public HazardKind Kind;
        public Fx X, Y;
        public Fx Radius;
        /// <summary>Full swing period in ticks (even number).</summary>
        public int Period;
        public int Phase;
        /// <summary>Horizontal half-swing in world units.</summary>
        public Fx Amplitude;
    }

    /// <summary>
    /// Everything the Sim needs about one level. Platforms and Pickups are sorted by Y ascending,
    /// Hazards by anchor Y ascending. Ids equal the array index.
    /// </summary>
    public sealed class LevelData
    {
        public Platform[] Platforms = new Platform[0];
        public Hazard[] Hazards = new Hazard[0];
        public Pickup[] Pickups = new Pickup[0];
        /// <summary>Number of platform rows above the start platform (top row index).</summary>
        public int Rows;

        // Hints for bots and UI (not used by the Sim). -1 = not present.
        public int HiddenGemRow = -1;
        public Fx HiddenGemX;
        /// <summary>True when the hidden gem floats over its own side platform (false = builder fallback: gem over the main platform of that row).</summary>
        public bool HiddenGemOnSidePlatform;
        public int LadderStartRow = -1;
        public int LadderEndRow = -1;
        public int WaveStartRow = -1;
        public int WaveEndRow = -1;

        /// <summary>Wraps a plain platform array (no hazards, no pickups).</summary>
        public static LevelData FromPlatforms(Platform[] platforms)
        {
            LevelData d = new LevelData();
            d.Platforms = platforms;
            d.Rows = platforms.Length > 0 ? platforms[platforms.Length - 1].Row : 0;
            return d;
        }

        /// <summary>Deterministic integer hash of the full level content (determinism tests, golden levels, server checks).</summary>
        public long ComputeHash()
        {
            long h = 1469598103934665603L;
            for (int i = 0; i < Platforms.Length; i++)
            {
                Platform p = Platforms[i];
                h = Mix(h, p.Row); h = Mix(h, (int)p.Kind); h = Mix(h, p.X.Raw); h = Mix(h, p.Y.Raw);
                h = Mix(h, p.Width.Raw); h = Mix(h, p.Amplitude.Raw); h = Mix(h, p.Period); h = Mix(h, p.Phase);
            }
            for (int i = 0; i < Hazards.Length; i++)
            {
                Hazard z = Hazards[i];
                h = Mix(h, (int)z.Kind); h = Mix(h, z.X.Raw); h = Mix(h, z.Y.Raw); h = Mix(h, z.Radius.Raw);
                h = Mix(h, z.Period); h = Mix(h, z.Phase); h = Mix(h, z.Amplitude.Raw);
            }
            for (int i = 0; i < Pickups.Length; i++)
            {
                Pickup k = Pickups[i];
                h = Mix(h, (int)k.Kind); h = Mix(h, k.X.Raw); h = Mix(h, k.Y.Raw); h = Mix(h, k.Radius.Raw); h = Mix(h, k.Tag);
            }
            return Mix(h, Rows);
        }

        private static long Mix(long h, int v)
        {
            unchecked { return (h ^ (uint)v) * 1099511628211L; }
        }
    }
}
