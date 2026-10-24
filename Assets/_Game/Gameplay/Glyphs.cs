using System;
using UnityEngine;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The control glyphs (chevrons, pause bars, cross) drawn in code as chunky white shapes with a navy outline, so every control reads as an icon
    /// instead of a word (docs/16 section G) and stays crisp at any size. Built once in Warmup (never during a run, CUX-1).
    /// </summary>
    public static class Glyphs
    {
        const int N = 256;
        static Sprite left, up, pause, cross, check, restart, ring, infinity, helmet, shirt, pants, boot, anvil;

        public static Sprite Left { get { return left != null ? left : (left = Build(ChevronShape(-1f, 0f), "glyph_left")); } }
        public static Sprite Up { get { return up != null ? up : (up = Build(ChevronShape(0f, 1f), "glyph_up")); } }
        public static Sprite Pause { get { return pause != null ? pause : (pause = Build(BarsShape(), "glyph_pause")); } }
        public static Sprite Cross { get { return cross != null ? cross : (cross = Build(CrossShape(), "glyph_cross")); } }
        public static Sprite Restart { get { return restart != null ? restart : (restart = Build(RestartShape(), "glyph_restart")); } }
        /// <summary>A plain circle outline (the power-up timer ring, drawn with a radial fill).</summary>
        public static Sprite Ring { get { return ring != null ? ring : (ring = Build(RingShape(), "glyph_ring", 0.016f)); } }
        /// <summary>The endless-mode symbol: a sideways figure eight.</summary>
        public static Sprite Infinity { get { return infinity != null ? infinity : (infinity = Build(InfinityShape(), "glyph_infinity")); } }
        public static Sprite Check { get { return check != null ? check : (check = Build(TickShape(), "glyph_check")); } }

        // Slot pictures for the placeholder gear tiles (docs/17): head, body, pants, boots, plus the forge anvil.
        public static Sprite Helmet { get { return helmet != null ? helmet : (helmet = Build(HelmetShape(), "glyph_helmet", 0.05f)); } }
        public static Sprite Shirt { get { return shirt != null ? shirt : (shirt = Build(ShirtShape(), "glyph_shirt", 0.05f)); } }
        public static Sprite Pants { get { return pants != null ? pants : (pants = Build(PantsShape(), "glyph_pants", 0.05f)); } }
        public static Sprite Boot { get { return boot != null ? boot : (boot = Build(BootShape(), "glyph_boot", 0.05f)); } }
        public static Sprite Anvil { get { return anvil != null ? anvil : (anvil = Build(AnvilShape(), "glyph_anvil", 0.05f)); } }

        /// <summary>The slot picture for a gear slot index 0..3 (head, body, pants, boots).</summary>
        public static Sprite ForSlot(int slot)
        {
            switch (slot)
            {
                case 0: return Helmet;
                case 1: return Shirt;
                case 2: return Pants;
                default: return Boot;
            }
        }

        public static void Warmup() { Sprite hh = Helmet, ss = Shirt, pp = Pants, bb = Boot, aa = Anvil; WarmupBasic(); }

        static void WarmupBasic() { Sprite a = Left, b = Up, c = Pause, d = Cross, e = Check, f = Restart, g = Ring, h = Infinity; }

        /// <summary>A shape = distance to the nearest stroke segment, minus the stroke radius (negative inside).</summary>
        delegate float Sdf(float x, float y);

        static float Seg(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy));
            float cx = ax + t * dx - px, cy = ay + t * dy - py;
            return Mathf.Sqrt(cx * cx + cy * cy);
        }

        // Coordinates are 0..1 with y up; the stroke radius is 0.11.
        static Sdf ChevronShape(float dirX, float dirY)
        {
            // Points toward (dirX, dirY): left chevron "<", up chevron "^".
            return (x, y) =>
            {
                float r = 0.11f;
                if (dirY > 0.5f) return Mathf.Min(Seg(x, y, 0.22f, 0.36f, 0.5f, 0.66f), Seg(x, y, 0.5f, 0.66f, 0.78f, 0.36f)) - r;
                return Mathf.Min(Seg(x, y, 0.62f, 0.2f, 0.36f, 0.5f), Seg(x, y, 0.36f, 0.5f, 0.62f, 0.8f)) - r;
            };
        }

        static float Box(float x, float y, float cx, float cy, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(x - cx) - (hx - r), qy = Mathf.Abs(y - cy) - (hy - r);
            return Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static Sdf HelmetShape()
        {
            return (x, y) =>
            {
                float dome = Mathf.Max(Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.40f) * (y - 0.40f)) - 0.32f, 0.40f - y);
                return Mathf.Min(dome, Box(x, y, 0.5f, 0.33f, 0.40f, 0.075f, 0.06f));
            };
        }

        static Sdf ShirtShape()
        {
            return (x, y) =>
            {
                float d = Box(x, y, 0.5f, 0.45f, 0.22f, 0.30f, 0.05f);
                d = Mathf.Min(d, Seg(x, y, 0.32f, 0.66f, 0.13f, 0.46f) - 0.085f);
                d = Mathf.Min(d, Seg(x, y, 0.68f, 0.66f, 0.87f, 0.46f) - 0.085f);
                return d;
            };
        }

        static Sdf PantsShape()
        {
            return (x, y) =>
            {
                float d = Box(x, y, 0.5f, 0.70f, 0.26f, 0.10f, 0.04f);
                d = Mathf.Min(d, Box(x, y, 0.35f, 0.38f, 0.10f, 0.32f, 0.04f));
                d = Mathf.Min(d, Box(x, y, 0.65f, 0.38f, 0.10f, 0.32f, 0.04f));
                return d;
            };
        }

        static Sdf BootShape()
        {
            return (x, y) =>
            {
                float d = Box(x, y, 0.40f, 0.60f, 0.14f, 0.26f, 0.05f);
                d = Mathf.Min(d, Box(x, y, 0.50f, 0.28f, 0.34f, 0.12f, 0.08f));
                return d;
            };
        }

        static Sdf AnvilShape()
        {
            return (x, y) =>
            {
                float d = Box(x, y, 0.5f, 0.66f, 0.34f, 0.10f, 0.04f);
                d = Mathf.Min(d, Box(x, y, 0.5f, 0.50f, 0.12f, 0.10f, 0.02f));
                d = Mathf.Min(d, Box(x, y, 0.5f, 0.28f, 0.26f, 0.10f, 0.04f));
                d = Mathf.Min(d, Seg(x, y, 0.16f, 0.66f, 0.06f, 0.62f) - 0.05f);
                return d;
            };
        }

        static Sdf BarsShape()
        {
            return (x, y) => Mathf.Min(Seg(x, y, 0.35f, 0.26f, 0.35f, 0.74f), Seg(x, y, 0.65f, 0.26f, 0.65f, 0.74f)) - 0.095f;
        }

        static Sdf CrossShape()
        {
            return (x, y) => Mathf.Min(Seg(x, y, 0.28f, 0.28f, 0.72f, 0.72f), Seg(x, y, 0.28f, 0.72f, 0.72f, 0.28f)) - 0.095f;
        }

        /// <summary>A circular arrow: an arc of about 280 degrees with an arrowhead at its end.</summary>
        static Sdf RestartShape()
        {
            const int steps = 28;
            var px = new float[steps + 1];
            var py = new float[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float a = (70f + 280f * i / steps) * Mathf.Deg2Rad;
                px[i] = 0.5f + 0.27f * Mathf.Cos(a);
                py[i] = 0.5f + 0.27f * Mathf.Sin(a);
            }
            // Arrowhead at the arc end (tangent points counter-clockwise).
            float ea = 350f * Mathf.Deg2Rad;
            float tx = -Mathf.Sin(ea), ty = Mathf.Cos(ea), nx = Mathf.Cos(ea), ny = Mathf.Sin(ea);
            float hx = px[steps] + tx * 0.09f, hy = py[steps] + ty * 0.09f;
            float w1x = px[steps] - nx * 0.13f, w1y = py[steps] - ny * 0.13f, w2x = px[steps] + nx * 0.13f, w2y = py[steps] + ny * 0.13f;
            return (x, y) =>
            {
                float d = 1f;
                for (int i = 0; i < steps; i++) d = Mathf.Min(d, Seg(x, y, px[i], py[i], px[i + 1], py[i + 1]));
                d = Mathf.Min(d, Seg(x, y, hx, hy, w1x, w1y));
                d = Mathf.Min(d, Seg(x, y, hx, hy, w2x, w2y));
                return d - 0.085f;
            };
        }

        static Sdf RingShape()
        {
            return (x, y) => Mathf.Abs(Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f)) - 0.45f) - 0.022f;
        }

        static Sdf InfinityShape()
        {
            const int steps = 48;
            var px = new float[steps + 1];
            var py = new float[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float t = i * 2f * Mathf.PI / steps;
                float d = 1f + Mathf.Sin(t) * Mathf.Sin(t);
                px[i] = 0.5f + 0.4f * Mathf.Cos(t) / d * 1.0f;
                py[i] = 0.5f + 0.34f * Mathf.Sin(t) * Mathf.Cos(t) / d * 1.1f;
            }
            return (x, y) =>
            {
                float dmin = 1f;
                for (int i = 0; i < steps; i++) dmin = Mathf.Min(dmin, Seg(x, y, px[i], py[i], px[i + 1], py[i + 1]));
                return dmin - 0.062f;
            };
        }

        static Sdf TickShape()
        {
            return (x, y) => Mathf.Min(Seg(x, y, 0.24f, 0.5f, 0.42f, 0.3f), Seg(x, y, 0.42f, 0.3f, 0.78f, 0.7f)) - 0.1f;
        }

        static Sprite Build(Sdf shape, string name, float outline = 0.055f)
        {
            // outline = navy rim width
            var px = new Color32[N * N];
            for (int j = 0; j < N; j++)
            {
                for (int i = 0; i < N; i++)
                {
                    float fill = 0f, rim = 0f;
                    for (int sy = 0; sy < 2; sy++)
                    {
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float x = (i + 0.25f + sx * 0.5f) / N, y = (j + 0.25f + sy * 0.5f) / N;
                            float d = shape(x, y);
                            if (d <= 0f) fill += 0.25f;
                            else if (d <= outline) rim += 0.25f;
                        }
                    }
                    // White fill over a dark navy rim.
                    float a = Mathf.Clamp01(fill + rim);
                    float w = fill / Mathf.Max(0.0001f, a);
                    byte r = (byte)Mathf.RoundToInt(Mathf.Lerp(18f, 255f, w));
                    byte g = (byte)Mathf.RoundToInt(Mathf.Lerp(24f, 255f, w));
                    byte b = (byte)Mathf.RoundToInt(Mathf.Lerp(52f, 255f, w));
                    px[j * N + i] = new Color32(r, g, b, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            sp.name = name;
            return sp;
        }
    }
}
