using UnityEngine;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// Code-drawn icons of the timed gameplay (Bug List 8 item 2b, owner choice: code-drawn, 0 credits). <see cref="Stopwatch"/> is the Time Attack play icon: a ring with a
    /// crown button on top and a hand, white with a navy outline like the other glyphs. The sprite is built ONCE on first use (the Meta scene calls <see cref="Warmup"/>), never per frame.
    /// </summary>
    public static class TimedIcons
    {
        const int N = 128;
        static Sprite stopwatch;

        /// <summary>The stopwatch icon (white on a navy rim, 128 px). Never null.</summary>
        public static Sprite Stopwatch { get { return stopwatch != null ? stopwatch : (stopwatch = BuildStopwatch()); } }

        public static void Warmup() { Sprite s = Stopwatch; }

        static float Seg(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy));
            float cx = ax + t * dx - px, cy = ay + t * dy - py;
            return Mathf.Sqrt(cx * cx + cy * cy);
        }

        /// <summary>Signed distance of the stopwatch in 0..1 coordinates (y up, negative inside). Public so tests can sample it.</summary>
        public static float StopwatchShape(float x, float y)
        {
            const float cx = 0.5f, cy = 0.44f;
            float r = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            float d = Mathf.Abs(r - 0.31f) - 0.065f;                         // the case ring
            d = Mathf.Min(d, Seg(x, y, 0.5f, 0.74f, 0.5f, 0.82f) - 0.05f);  // the stem
            d = Mathf.Min(d, Seg(x, y, 0.40f, 0.86f, 0.60f, 0.86f) - 0.05f); // the crown button
            d = Mathf.Min(d, Seg(x, y, cx, cy, cx, cy + 0.19f) - 0.05f);     // the long hand (12 o'clock)
            d = Mathf.Min(d, Seg(x, y, cx, cy, cx + 0.12f, cy - 0.08f) - 0.045f);   // the short hand
            d = Mathf.Min(d, r - 0.075f);                                    // the hub
            d = Mathf.Min(d, Seg(x, y, 0.76f, 0.70f, 0.82f, 0.76f) - 0.04f);   // the side button
            return d;
        }

        static Sprite BuildStopwatch()
        {
            const float outline = 0.045f;
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
                            float d = StopwatchShape((i + 0.25f + sx * 0.5f) / N, (j + 0.25f + sy * 0.5f) / N);
                            if (d <= 0f) fill += 0.25f;
                            else if (d <= outline) rim += 0.25f;
                        }
                    }
                    float a = Mathf.Clamp01(fill + rim);
                    float w = fill / Mathf.Max(0.0001f, a);
                    px[j * N + i] = new Color32((byte)Mathf.RoundToInt(Mathf.Lerp(18f, 255f, w)), (byte)Mathf.RoundToInt(Mathf.Lerp(24f, 255f, w)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(52f, 255f, w)), (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            sp.name = "icon_stopwatch";
            return sp;
        }
    }
}
