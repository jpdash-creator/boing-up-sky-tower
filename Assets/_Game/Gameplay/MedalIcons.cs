using BoingUpSkyTower.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// Medals (doc 46 section 6): earned medals use the approved OpenArt art (Resources/Art/Ui/medal_bronze|silver|gold.png) when present; the code-drawn medals are the fallback and the hollow versions. The three tiers differ by SHAPE and by the number of pips, not only by colour:
    /// Bronze = a round disc with one pip, Silver = a diamond with two pips, Gold = a five-point star with three pips. A medal that is not earned yet is the same shape as a
    /// pale hollow outline. Sprites are built once (Warmup or first use, never during a run).
    /// </summary>
    public static class MedalIcons
    {
        const int N = 128;
        static readonly Sprite[] earned = new Sprite[4], hollow = new Sprite[4];

        static readonly Color Ink = new Color(0.10f, 0.06f, 0.14f, 1f);
        static readonly Color BronzeFill = new Color(0.85f, 0.52f, 0.26f), SilverFill = new Color(0.84f, 0.88f, 0.94f), GoldFill = new Color(1f, 0.80f, 0.16f);

        public static string Name(MedalLevel m)
        {
            switch (m) { case MedalLevel.Bronze: return "Bronze"; case MedalLevel.Silver: return "Silver"; case MedalLevel.Gold: return "Gold"; default: return "None"; }
        }

        /// <summary>The sprite of a tier: filled when earned, a grey-blue hollow outline otherwise. <see cref="MedalLevel.None"/> gives null.</summary>
        public static Sprite For(MedalLevel m, bool isEarned)
        {
            int i = (int)m;
            if (i < 1 || i > 3) return null;
            Sprite[] set = isEarned ? earned : hollow;
            if (set[i] == null) set[i] = (isEarned ? ToyKit.Get("medal_" + Name(m).ToLowerInvariant()) : null) ?? Build(m, isEarned);   // approved OpenArt art when present, else the code-drawn medal
            return set[i];
        }

        public static void Warmup()
        {
            for (int i = 1; i <= 3; i++) { For((MedalLevel)i, true); For((MedalLevel)i, false); }
        }

        /// <summary>Signed distance of the tier's outline in 0..1 coordinates (negative inside). Public so tests can compare the three silhouettes.</summary>
        public static float Shape(MedalLevel m, float x, float y)
        {
            float dx = x - 0.5f, dy = y - 0.5f;
            switch (m)
            {
                case MedalLevel.Bronze:
                    return Mathf.Sqrt(dx * dx + dy * dy) - 0.38f;
                case MedalLevel.Silver:
                    return Mathf.Abs(dx) + Mathf.Abs(dy) - 0.47f;   // diamond: clearly different from the round bronze disc
                default:
                    return StarDistance(dx, dy);
            }
        }

        static readonly float[] starX = new float[10], starY = new float[10];

        static float StarDistance(float px, float py)
        {
            if (starX[0] == 0f && starY[0] == 0f)
            {
                for (int i = 0; i < 10; i++)
                {
                    float r = (i & 1) == 0 ? 0.46f : 0.20f;
                    float a = (90f + 36f * i) * Mathf.Deg2Rad;
                    starX[i] = Mathf.Cos(a) * r;
                    starY[i] = Mathf.Sin(a) * r;
                }
            }
            float best = 10f;
            bool inside = false;
            for (int i = 0, j = 9; i < 10; j = i++)
            {
                float ax = starX[j], ay = starY[j], bx = starX[i], by = starY[i];
                float ex = bx - ax, ey = by - ay;
                float t = Mathf.Clamp01(((px - ax) * ex + (py - ay) * ey) / (ex * ex + ey * ey));
                float cx = ax + t * ex - px, cy = ay + t * ey - py;
                best = Mathf.Min(best, Mathf.Sqrt(cx * cx + cy * cy));
                if ((ay > py) != (by > py) && px < (bx - ax) * (py - ay) / (by - ay) + ax) inside = !inside;
            }
            return inside ? -best : best;
        }

        static Sprite Build(MedalLevel m, bool isEarned)
        {
            Color fill = m == MedalLevel.Gold ? GoldFill : (m == MedalLevel.Silver ? SilverFill : BronzeFill);
            int pips = (int)m;
            var px = new Color32[N * N];
            const float rim = 0.05f, pipR = 0.05f, pipStep = 0.13f;
            for (int j = 0; j < N; j++)
            {
                for (int i = 0; i < N; i++)
                {
                    float fa = 0f, ra = 0f, pa = 0f;
                    for (int sy = 0; sy < 2; sy++)
                    {
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float x = (i + 0.25f + sx * 0.5f) / N, y = (j + 0.25f + sy * 0.5f) / N;
                            float d = Shape(m, x, y);
                            if (d <= 0f)
                            {
                                fa += 0.25f;
                                for (int p = 0; p < pips; p++)
                                {
                                    float cx = 0.5f + (p - (pips - 1) * 0.5f) * pipStep;
                                    if ((x - cx) * (x - cx) + (y - 0.5f) * (y - 0.5f) <= pipR * pipR) { pa += 0.25f; break; }
                                }
                            }
                            else if (d <= rim) ra += 0.25f;
                        }
                    }
                    float a = Mathf.Clamp01(fa + ra);
                    if (a <= 0f) { px[j * N + i] = new Color32(0, 0, 0, 0); continue; }
                    Color c;
                    if (isEarned)
                    {
                        Color body = Color.Lerp(fill, Ink, Mathf.Clamp01(pa / Mathf.Max(0.0001f, fa)));
                        c = Color.Lerp(Ink, body, fa / a);
                        c.a = a;
                    }
                    else
                    {
                        // hollow: no fill, a dark grey-blue outline and faint pips (still the same silhouette)
                        float edge = Mathf.Clamp01(ra / Mathf.Max(0.0001f, a));
                        c = new Color(0.40f, 0.45f, 0.58f, Mathf.Max(edge, Mathf.Clamp01(pa)) * 0.9f + fa * 0.12f);
                        c.a = Mathf.Clamp01(c.a * a);
                    }
                    px[j * N + i] = c;
                }
            }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            sp.name = "medal_" + Name(m).ToLowerInvariant() + (isEarned ? "" : "_hollow");
            return sp;
        }
    }

    /// <summary>A row of the three medal slots (icon plus the pace each needs, or NEW) for the level-start popup and the result screen. Builds nothing when the level has no target data.</summary>
    public static class MedalStrip
    {
        public const string RowName = "Medals";
        const float IconSize = 84f, SlotW = 250f;

        /// <summary>Height of the strip at the current text size.</summary>
        public static float Height { get { return IconSize + 6f + TimedUi.LineHeight(1.25f); } }

        /// <summary>True when medals can be earned here: the calibration job has written a target for this level and mode.</summary>
        public static bool HasTargets(string levelId, int mode) { return MedalTargets.Lookup(levelId, mode) > 0; }

        /// <summary>
        /// Builds the strip <paramref name="y"/> below the top of <paramref name="panel"/> and returns its height (0 and nothing built without targets). A slot is filled when
        /// <paramref name="held"/> reaches it; under it stands the Pace it needs (plat/s), or NEW for the tier <paramref name="newlyEarned"/> that this run just earned.
        /// </summary>
        public static float Build(Transform panel, float y, string levelId, int mode, MedalLevel held, MedalLevel newlyEarned)
        {
            int target = MedalTargets.Lookup(levelId, mode);
            if (target <= 0) return 0f;
            float h = Height;
            RectTransform row = UiKit.Rect(panel, RowName);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -y);
            row.sizeDelta = new Vector2(-48f, h);
            var sb = new System.Text.StringBuilder(96);
            sb.Append("Medals: ").Append(held == MedalLevel.None ? "none earned" : MedalIcons.Name(held) + " earned").Append(". ");
            for (int i = 1; i <= 3; i++)
            {
                var tier = (MedalLevel)i;
                bool got = held >= tier;
                int line = TimeRecords.MedalLinePaceMilli(tier, target);
                Image icon = UiKit.AddImage(row, MedalIcons.Name(tier) + "Medal", MedalIcons.For(tier, got), Color.white);
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                icon.rectTransform.pivot = new Vector2(0.5f, 1f);
                icon.rectTransform.anchoredPosition = new Vector2((i - 2) * SlotW, 0f);
                icon.rectTransform.sizeDelta = new Vector2(IconSize, IconSize);
                string cap;
                bool isNew = newlyEarned == tier;
                if (isNew) cap = "NEW";
                else
                {
                    var c = new System.Text.StringBuilder(8);
                    TimeRecords.AppendHundredths(c, (line + 5) / 10);
                    cap = c.ToString();
                }
                Text t = UiKit.AddText(row, MedalIcons.Name(tier) + "Caption", cap, 42, got ? Theme.Ink : Theme.InkSoft, TextAnchor.UpperCenter, true);
                var o = t.GetComponent<Outline>();
                if (o != null) o.enabled = false;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                var trt = (RectTransform)t.transform;
                trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
                trt.pivot = new Vector2(0.5f, 1f);
                trt.anchoredPosition = new Vector2((i - 2) * SlotW, -(IconSize + 6f));
                trt.sizeDelta = new Vector2(SlotW - 20f, TimedUi.LineHeight(1.25f));
                sb.Append(MedalIcons.Name(tier)).Append(got ? " earned" : " needs " + cap + " plat per second").Append(i < 3 ? ", " : ".");
            }
            row.gameObject.AddComponent<A11yLabel>().Text = sb.ToString();
            return h;
        }
    }
}
