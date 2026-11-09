using FluffyClimbers.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
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

    /// <summary>
    /// The medal strip of the level-start popup and the result screen (Bug List 8 item 5): a small caption "Medals: beat this pace", then the three medal icons, each with its
    /// word (Bronze / Silver / Gold) and the pace it needs ("0.90 plat/s", or NEW for the tier this run just earned) under it. Everything is centred; the icon shapes still differ
    /// by shape, never only by colour. Builds nothing when the level has no target data.
    /// </summary>
    public static class MedalStrip
    {
        public const string RowName = "Medals", CaptionName = "MedalsCaption", CaptionText = "Medals: beat this pace";
        const float IconSize = 76f, SlotW = 270f, Gap = 4f, LineGap = 2f;

        /// <summary>Font size of every strip label: the 16 sp floor, grown only half as fast as the text scale (the strip is dense; at 130 percent text it is 115 percent).</summary>
        static int FontPx { get { return Mathf.RoundToInt(UiKit.MinBodyTextPx * Mathf.Lerp(1f, Mathf.Max(1f, UiKit.TextScale), 0.5f)); } }

        /// <summary>Vertical slot of one label line: the real line height of the display font at the current size (measured about 1.5 x the size) plus a margin, so two stacked labels never overlap.</summary>
        static float LineH { get { return Mathf.Ceil(FontPx * 1.55f); } }

        /// <summary>Height of the strip at the current text size: caption line, icon, the word and the pace line, each in its own slot.</summary>
        public static float Height { get { return LineH + Gap + IconSize + 2f + LineH + LineGap + LineH + LineGap; } }

        /// <summary>Height of the compact strip of the result popup: the three icons and under each only the word (no caption, no pace targets), at the current text size.</summary>
        public static float CompactHeight { get { return IconSize + 2f + LineH + 2f; } }

        /// <summary>True when medals can be earned here: the calibration job has written a target for this level and mode.</summary>
        public static bool HasTargets(string levelId, int mode) { return MedalTargets.Lookup(levelId, mode) > 0; }

        /// <summary>"0.90 plat/s": the pace a medal line needs, from its milli plat/s value (rounded half up to hundredths).</summary>
        public static string PaceLineText(int paceMilli)
        {
            var c = new System.Text.StringBuilder(16);
            TimeRecords.AppendHundredths(c, (paceMilli + 5) / 10);
            c.Append(TimeRecords.PaceUnit);
            return c.ToString();
        }

        static Text Cell(RectTransform row, string name, string text, Color color, float x, float y, float w, bool fit)
        {
            Text t = UiKit.AddText(row, name, text, 42, color, TextAnchor.MiddleCenter, true);
            var o = t.GetComponent<Outline>();
            if (o != null) o.enabled = false;
            // one line, never wrapped: shrink the size (down to the 16 sp floor) until the text fits its slot width
            t.fontSize = FontPx;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = false;
            if (fit)
            {
                int min = Mathf.RoundToInt(UiKit.MinBodyTextPx);
                float need = t.preferredWidth;
                if (need > w && need > 0f) t.fontSize = Mathf.Max(min, Mathf.FloorToInt(t.fontSize * w / need));
            }
            var trt = (RectTransform)t.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(x, -y);
            trt.sizeDelta = new Vector2(w, LineH);
            return t;
        }

        /// <summary>
        /// Builds the strip <paramref name="y"/> below the top of <paramref name="panel"/> and returns its height (0 and nothing built without targets). A slot is filled when
        /// <paramref name="held"/> reaches it; under it stand the tier word and the Pace it needs (plat/s), or NEW for the tier <paramref name="newlyEarned"/> that this run just earned.
        /// </summary>
        public static float Build(Transform panel, float y, string levelId, int mode, MedalLevel held, MedalLevel newlyEarned, bool compact = false)
        {
            int target = MedalTargets.Lookup(levelId, mode);
            if (target <= 0) return 0f;
            float h = compact ? CompactHeight : Height, lh = LineH;
            RectTransform row = UiKit.Rect(panel, RowName);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -y);
            row.sizeDelta = new Vector2(-48f, h);
            if (!compact) Cell(row, CaptionName, CaptionText, Theme.InkSoft, 0f, 0f, 3f * SlotW, true);
            float iconY = compact ? 0f : lh + Gap, nameY = iconY + IconSize + 2f, paceY = nameY + lh + LineGap;
            var sb = new System.Text.StringBuilder(112);
            sb.Append(compact ? "Medals: " : "Medals, beat this pace: ").Append(held == MedalLevel.None ? "none earned" : MedalIcons.Name(held) + " earned").Append(". ");
            for (int i = 1; i <= 3; i++)
            {
                var tier = (MedalLevel)i;
                bool got = held >= tier;
                float x = (i - 2) * SlotW;
                int line = TimeRecords.MedalLinePaceMilli(tier, target);
                Image icon = UiKit.AddImage(row, MedalIcons.Name(tier) + "Medal", MedalIcons.For(tier, got), Color.white);
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                icon.rectTransform.pivot = new Vector2(0.5f, 1f);
                icon.rectTransform.anchoredPosition = new Vector2(x, -iconY);
                icon.rectTransform.sizeDelta = new Vector2(IconSize, IconSize);
                Color ink = got ? Theme.Ink : Theme.InkSoft;
                if (compact)
                {
                    // result popup: icon plus the word only; the tier this run just earned says NEW in front of its word (the pace targets stay in the level-start popup)
                    string word = newlyEarned == tier ? "NEW " + MedalIcons.Name(tier) : MedalIcons.Name(tier);
                    Cell(row, MedalIcons.Name(tier) + "Name", word, ink, x, nameY, SlotW - 10f, true);
                }
                else
                {
                    string cap = newlyEarned == tier ? "NEW" : PaceLineText(line);
                    Cell(row, MedalIcons.Name(tier) + "Name", MedalIcons.Name(tier), ink, x, nameY, SlotW - 10f, true);
                    Cell(row, MedalIcons.Name(tier) + "Caption", cap, ink, x, paceY, SlotW - 10f, true);
                }
                sb.Append(MedalIcons.Name(tier)).Append(got ? " earned" : " needs " + PaceLineText(line)).Append(i < 3 ? ", " : ".");
            }
            row.gameObject.AddComponent<A11yLabel>().Text = sb.ToString();
            return h;
        }
    }
}
