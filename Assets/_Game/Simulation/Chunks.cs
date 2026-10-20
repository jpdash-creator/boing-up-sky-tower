namespace SkyHopper.Simulation
{
    /// <summary>One authored row. X is the platform centre as a share of the screen width in permille (0..1000).</summary>
    public struct ChunkRow
    {
        public int XPermille;
        /// <summary>Platform width in tenths of a world unit.</summary>
        public int WidthTenths;
        public PlatformKind Kind;
        /// <summary>Vertical gap from the previous row, in hundredths of a unit (must stay below 0.9 of the max jump height).</summary>
        public int GapHundredths;
    }

    /// <summary>A hand-authored sequence of rows (doc 08 section 4, D15). Class 3 = complex (L5, L10), 4 = highly complex gauntlet (L15).</summary>
    public sealed class Chunk
    {
        public string Id;
        public int Class;
        public ChunkRow[] Rows;
    }

    /// <summary>
    /// Shared chunk library (static data, no files). The builder places the rows with the chunk entered from the
    /// previous platform: the first row is moved into reach, later rows keep their authored offsets (mirrored
    /// on a seeded coin flip). Authored data obeys the generator reach rule; the tests re-check every chunk.
    /// </summary>
    public static class ChunkLibrary
    {
        private static ChunkRow R(int x, int w, PlatformKind k, int gap)
        {
            ChunkRow r = new ChunkRow();
            r.XPermille = x; r.WidthTenths = w; r.Kind = k; r.GapHundredths = gap;
            return r;
        }

        private static Chunk C(string id, int cls, params ChunkRow[] rows)
        {
            Chunk c = new Chunk();
            c.Id = id; c.Class = cls; c.Rows = rows;
            return c;
        }

        private const PlatformKind N = PlatformKind.Normal;
        private const PlatformKind M = PlatformKind.Moving;
        private const PlatformKind V = PlatformKind.Vanishing;
        private const PlatformKind K = PlatformKind.Crumble;
        private const PlatformKind B = PlatformKind.Bounce;

        public static readonly Chunk[] All = new Chunk[]
        {
            // ---- complex (L5, L10): medium widths 2.4 to 3.6, gaps 2.6 to 3.8 ----
            C("chunk_complex_a", 3, // zigzag
                R(250, 36, N, 300), R(750, 36, N, 300), R(250, 36, N, 300), R(750, 36, N, 300),
                R(250, 36, N, 300), R(750, 36, N, 300), R(500, 36, N, 300)),
            C("chunk_complex_b", 3, // staircase up and back
                R(170, 32, N, 280), R(300, 32, N, 280), R(450, 32, N, 280), R(600, 32, N, 280),
                R(750, 32, N, 280), R(830, 32, N, 280), R(700, 32, N, 280), R(550, 32, N, 280)),
            C("chunk_complex_c", 3, // pillars, taller gaps
                R(300, 26, N, 360), R(700, 26, N, 360), R(300, 26, N, 360), R(700, 26, N, 360),
                R(300, 26, N, 360), R(700, 26, N, 360)),
            C("chunk_complex_d", 3, // bounce garden
                R(200, 32, N, 320), R(500, 34, B, 320), R(800, 32, N, 320), R(500, 34, B, 320),
                R(200, 32, N, 320), R(500, 34, N, 320)),
            C("chunk_complex_e", 3, // drift
                R(300, 34, M, 300), R(700, 34, M, 300), R(500, 34, N, 300), R(300, 34, M, 300),
                R(700, 34, M, 300), R(500, 34, N, 300)),
            C("chunk_complex_f", 3, // crumble run, a solid platform between crumbles
                R(300, 34, K, 280), R(600, 34, N, 280), R(400, 34, K, 280), R(700, 34, N, 280),
                R(300, 34, K, 280), R(500, 34, N, 280)),

            // ---- gauntlet (L15): widths 1.6 to 2.4, gaps 3.6 to 4.6, moving/vanishing/crumble mixes ----
            C("chunk_gauntlet_a", 4, // narrow zigzag with drift and vanish
                R(300, 20, N, 420), R(650, 20, M, 420), R(350, 20, N, 420), R(700, 20, V, 420),
                R(400, 20, N, 420), R(650, 20, M, 420), R(500, 20, N, 420)),
            C("chunk_gauntlet_b", 4, // narrow staircase
                R(200, 18, N, 400), R(350, 18, N, 400), R(500, 18, V, 400), R(650, 18, N, 400),
                R(800, 18, M, 400), R(650, 18, N, 400), R(500, 18, N, 400), R(350, 18, V, 400)),
            C("chunk_gauntlet_c", 4, // wall dance: wide swings
                R(200, 22, N, 400), R(800, 22, N, 400), R(200, 22, N, 400), R(800, 22, N, 400),
                R(200, 22, N, 400), R(800, 22, N, 400)),
            C("chunk_gauntlet_d", 4, // bounce relay
                R(300, 20, N, 420), R(650, 22, B, 420), R(350, 20, N, 420), R(650, 22, B, 420),
                R(350, 20, N, 420), R(500, 22, N, 420)),
            C("chunk_gauntlet_e", 4, // crumble ladder
                R(400, 24, K, 360), R(600, 24, K, 360), R(400, 24, K, 360), R(600, 24, K, 360),
                R(400, 24, N, 360), R(600, 24, K, 360)),
            C("chunk_gauntlet_f", 4, // moving maze
                R(350, 24, M, 400), R(650, 24, M, 400), R(350, 24, M, 400), R(650, 24, M, 400),
                R(500, 24, N, 400), R(350, 24, M, 400))
        };

        /// <summary>Returns the chunk with this id, or null for an unknown id.</summary>
        public static Chunk Get(string id)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].Id == id) return All[i];
            return null;
        }
    }
}
