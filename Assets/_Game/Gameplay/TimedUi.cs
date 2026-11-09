using BoingUpSkyTower.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>Small UI helpers shared by the level-start popup, the result screen and the Run HUD of the timed gameplay (doc 46).</summary>
    public static class TimedUi
    {
        public static readonly Color Good = new Color(0.62f, 1f, 0.72f, 1f), Bad = new Color(1f, 0.76f, 0.52f, 1f), Gold = new Color(1f, 0.9f, 0.4f, 1f);

        /// <summary>Height of one line of body text at the current text size (130 percent safe).</summary>
        public static float LineHeight(float factor = 1.3f) { return Mathf.Ceil(UiKit.MinBodyTextPx * UiKit.TextScale * factor); }

        /// <summary>
        /// A dark glass pill in a popup row with an optional icon (a tick or a cross for a delta, so better and worse never rely on colour) and a centred value. Without an icon
        /// the text uses the whole pill. Same look as <see cref="ToyKit.InfoPill"/>.
        /// </summary>
        public static Text StatPill(RectTransform row, Sprite icon, string text, Color color, float width, float height)
        {
            Image pill = Theme.GlassPanel(row, "Pill", height * 0.25f);
            var le = pill.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.minWidth = width;
            le.preferredHeight = height;
            le.flexibleWidth = 0f;
            if (icon != null)
            {
                Image ic = UiKit.AddImage(pill.transform, "Icon", icon, icon == Glyphs.Check || icon == Glyphs.Cross ? color : Color.white);
                var irt = ic.rectTransform;
                irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
                irt.pivot = new Vector2(0f, 0.5f);
                irt.anchoredPosition = new Vector2(height * 0.18f, 0f);
                irt.sizeDelta = new Vector2(height * 0.62f, height * 0.62f);
            }
            Text t = UiKit.AddText(pill.transform, "Value", text, 50, color, TextAnchor.MiddleCenter, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var trt = (RectTransform)t.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(icon != null ? height * 0.9f : 14f, 0f);
            trt.offsetMax = new Vector2(-14f, 0f);
            return t;
        }

        /// <summary>A centred line of text (or up to <paramref name="lines"/> lines) under the top edge of a panel.</summary>
        public static Text Line(Transform panel, string name, string text, float y, float width, int lines, bool italic, Color color)
        {
            Text t = UiKit.AddText(panel, name, text, 42, color, TextAnchor.MiddleCenter, true);
            var o = t.GetComponent<Outline>();
            if (o != null) o.enabled = false;
            if (italic) t.fontStyle = FontStyle.Italic;
            UiKit.Place((RectTransform)t.transform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(width, lines * LineHeight()));
            t.rectTransform.pivot = new Vector2(0.5f, 1f);
            return t;
        }

        /// <summary>Pace of a best run for a popup: "0.83 plat/s", or "-" when the pace is not valid.</summary>
        public static string PaceText(PaceValue p) { return p.IsValid ? TimeRecords.FormatPace(p.Floors, p.Ticks) : "-"; }

        /// <summary>
        /// The "best of this mode" block of a start popup: a label line with the Gameplay mode and, when a record exists, a row with the best Pace and the best Time (survived Time
        /// in endless); otherwise "no record yet". Returns the height used from <paramref name="y"/>.
        /// </summary>
        public static float RecordBlock(Transform panel, float y, int mode, PaceValue pace, int timeTicks, bool endless)
        {
            string modeName = GameSettings.DifficultyNames[Mathf.Clamp(mode, 0, GameSettings.DifficultyNames.Length - 1)];
            if (!pace.IsValid && timeTicks <= 0)
            {
                Line(panel, "RecordNone", modeName + ": no record yet", y, 780f, 1, false, Theme.InkSoft);
                return RecordBlockHeight(false);
            }
            Line(panel, "RecordLabel", endless ? "Best in " + modeName + " (pace, time alive)" : "Best in " + modeName, y, 780f, 2, false, Theme.Ink);
            float labelH = 2f * LineHeight();
            RectTransform row = ToyKit.PillRow(panel, "Record", y + labelH + 4f, 96f);
            Text p = StatPill(row, null, PaceText(pace), Gold, 430f, 96f);
            Text t = StatPill(row, null, timeTicks > 0 ? TimeRecords.FormatTime(timeTicks) : "-", Color.white, 360f, 96f);
            p.transform.parent.name = "RecordPace";
            t.transform.parent.name = "RecordTime";
            return RecordBlockHeight(true);
        }

        /// <summary>Height of <see cref="RecordBlock"/> at the current text size.</summary>
        public static float RecordBlockHeight(bool hasRecord)
        {
            return hasRecord ? 2f * LineHeight() + 4f + 96f + 14f : LineHeight() + 14f;
        }
    }
}
