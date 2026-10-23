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
        static Sprite left, up, pause, cross, check, restart;

        public static Sprite Left { get { return left != null ? left : (left = Build(ChevronShape(-1f, 0f))); } }
        public static Sprite Up { get { return up != null ? up : (up = Build(ChevronShape(0f, 1f))); } }
        public static Sprite Pause { get { return pause != null ? pause : (pause = Build(BarsShape())); } }
        public static Sprite Cross { get { return cross != null ? cross : (cross = Build(CrossShape())); } }
        public static Sprite Restart { get { return restart != null ? restart : (restart = Build(RestartShape())); } }
        public static Sprite Check { get { return check != null ? check : (check = Build(TickShape())); } }

        public static void Warmup() { Sprite a = Left, b = Up, c = Pause, d = Cross, e = Check, f = Restart; }

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

        static Sdf TickShape()
        {
            return (x, y) => Mathf.Min(Seg(x, y, 0.24f, 0.5f, 0.42f, 0.3f), Seg(x, y, 0.42f, 0.3f, 0.78f, 0.7f)) - 0.1f;
        }

        static Sprite Build(Sdf shape)
        {
            const float outline = 0.055f;      // navy rim width
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
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
