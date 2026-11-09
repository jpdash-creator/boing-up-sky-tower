using System;
using UnityEngine;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The icon of each Gameplay setting button on the level screen (Relaxed, Easy, Normal, Hard, Insane; Bug List 6 item 6). ONE hook: <see cref="For"/>.
    /// It returns the approved painted icon when the file <c>Assets/Resources/Art/Ui/gameplay_&lt;mode&gt;.png</c> exists (imported as a Sprite), otherwise a
    /// clean code-drawn glyph (a speed dial whose needle and number of pips grow with the mode), so dropping the approved art later needs no code change.
    /// File names to drop: gameplay_relaxed, gameplay_easy, gameplay_normal, gameplay_hard, gameplay_insane (see <see cref="ResourceName"/>).
    /// </summary>
    public static class GameplayIcons
    {
        /// <summary>Lower-case mode keys, same order as <see cref="GameSettings.DifficultyNames"/>.</summary>
        public static readonly string[] Keys = { "relaxed", "easy", "normal", "hard", "insane" };

        const int N = 128;
        static readonly Sprite[] fallback = new Sprite[5];

        /// <summary>Resources name (under Resources/Art/Ui) of the approved art of a mode, for example "gameplay_hard".</summary>
        public static string ResourceName(int mode)
        {
            return "gameplay_" + Keys[Mathf.Clamp(mode, 0, Keys.Length - 1)];
        }

        /// <summary>True when the approved painted icon of the mode exists (the fallback glyph is used otherwise).</summary>
        public static bool HasPaintedArt(int mode)
        {
            return ToyKit.Get(ResourceName(mode)) != null;
        }

        /// <summary>The icon sprite of a mode (0 Relaxed .. 4 Insane): the painted art when present, else the generated glyph. Never null.</summary>
        public static Sprite For(int mode)
        {
            mode = Mathf.Clamp(mode, 0, Keys.Length - 1);
            Sprite painted = ToyKit.Get(ResourceName(mode));
            if (painted != null) return painted;
            if (fallback[mode] == null) fallback[mode] = BuildGlyph(mode);
            return fallback[mode];
        }

        static float Seg(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy));
            float cx = ax + t * dx - px, cy = ay + t * dy - py;
            return Mathf.Sqrt(cx * cx + cy * cy);
        }

        /// <summary>The speed dial: a half-circle arc, a needle that swings from left (Relaxed) to right (Insane), a hub and one pip per step under it.</summary>
        static Sprite BuildGlyph(int mode)
        {
            const float cx = 0.5f, cy = 0.4f, arcR = 0.36f;
            float angle = (165f - 37.5f * mode) * Mathf.Deg2Rad;
            float nx = cx + 0.27f * Mathf.Cos(angle), ny = cy + 0.27f * Mathf.Sin(angle);
            int pips = mode + 1;
            Func<float, float, float> shape = (x, y) =>
            {
                float d = 1f;
                float r = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (y >= cy - 0.02f) d = Mathf.Min(d, Mathf.Abs(r - arcR) - 0.065f);                       // the dial arc
                d = Mathf.Min(d, Seg(x, y, cx, cy, nx, ny) - 0.07f);                                         // the needle
                d = Mathf.Min(d, r - 0.11f);                                                                  // the hub
                for (int i = 0; i < pips; i++)                                                                // one pip per step
                {
                    float px = 0.5f + (i - (pips - 1) * 0.5f) * 0.15f;
                    d = Mathf.Min(d, Mathf.Sqrt((x - px) * (x - px) + (y - 0.13f) * (y - 0.13f)) - 0.05f);
                }
                return d;
            };
            const float outline = 0.05f;
            var px32 = new Color32[N * N];
            for (int j = 0; j < N; j++)
            {
                for (int i = 0; i < N; i++)
                {
                    float fill = 0f, rim = 0f;
                    for (int sy = 0; sy < 2; sy++)
                    {
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float d = shape((i + 0.25f + sx * 0.5f) / N, (j + 0.25f + sy * 0.5f) / N);
                            if (d <= 0f) fill += 0.25f;
                            else if (d <= outline) rim += 0.25f;
                        }
                    }
                    float a = Mathf.Clamp01(fill + rim);
                    float w = fill / Mathf.Max(0.0001f, a);   // white fill over a dark navy rim, like the other code-drawn glyphs
                    px32[j * N + i] = new Color32((byte)Mathf.RoundToInt(Mathf.Lerp(18f, 255f, w)), (byte)Mathf.RoundToInt(Mathf.Lerp(24f, 255f, w)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(52f, 255f, w)), (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px32);
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            sp.name = "gameplay_glyph_" + Keys[mode];
            return sp;
        }
    }
}
