using System;
using FluffyClimbers.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>One medal line of the crown calculation pop-up: the pace (milli plat/s) the tier needs and whether the player already holds it.</summary>
    public struct CrownCalcMedal
    {
        public MedalLevel Tier;
        public int PaceMilli;
        public bool Earned;
    }

    /// <summary>
    /// Crown line and crown calculation pop-up (owner 2026-11-13, Main Bug List 2 item 4).
    /// The level and Endless start pop-ups show ONE compact line without any dark backing: [crown icon, best crown, (i)] | [coin icon, coin percent] (level) or [tower icon, best floors] (Endless).
    /// The crown part is a real button (at least 48 dp, accessibility label) that opens the small calculation pop-up, built here: the formula in words and numbers, the player's best run
    /// in the current Gameplay setting, the setting bonus, the resulting crown and (levels) the pace each medal needs. Numbers use Lilita One, words Nunito Bold, all on the cream panel in ink.
    /// </summary>
    public static class CrownInfoUi
    {
        public const string RowName = "CrownRow", CrownButtonName = "CrownInfo", CrownValueName = "CrownValue", SecondName = "CrownRowSecond", SecondValueName = "SecondValue", CalcName = "CrownCalc", ViewportName = "CalcViewport";

        /// <summary>Width of the content on the 860 wide popup panels (40 px each side).</summary>
        public const float ContentW = 780f;
        const float PanelW = 860f, TitleZone = 192f, BottomPad = 36f;   // the frame title box ends at 182

        /// <summary>Height of the compact crown line: at least 48 dp (the crown part is a button), and tall enough for the number at the current text size.</summary>
        public static float RowHeight()
        {
            return Mathf.Max(Mathf.Max(96f, UiKit.MinTouchRef), Mathf.Ceil(56f * Mathf.Max(1f, UiKit.TextScale) * 1.3f) + 8f);
        }

        /// <summary>
        /// Builds the single crown line <paramref name="y"/> below the top of <paramref name="panel"/>, centred. The crown part (icon, number, (i) ring) is one button calling
        /// <paramref name="onCrownTap"/>; after a separator comes the second group (icon and value, plain). Returns the row.
        /// </summary>
        public static RectTransform BuildRow(Transform panel, float y, string crownText, string crownA11y, Action onCrownTap, Sprite secondIcon, string secondText, string secondA11y)
        {
            float h = RowHeight();
            RectTransform row = UiKit.Rect(panel, RowName);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -y);
            row.sizeDelta = new Vector2(-48f, h);

            float icon = Mathf.Round(h * 0.62f), ring = Mathf.Round(Mathf.Max(52f, h * 0.52f));
            const float pad = 14f, gap = 10f, sepW = 4f, sepGap = 26f;

            // crown part: a light rounded plate (ink at 7 percent: it only marks the tap target) with the crown icon, the number and the (i) ring
            Button btn = UiKit.AddButton(row, CrownButtonName, "", UiKit.PillSprite, new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.07f), new Vector2(300f, h), onCrownTap);
            btn.GetComponent<A11yLabel>().Text = crownA11y;
            RectTransform brt = (RectTransform)btn.transform;
            Image crownIcon = UiKit.AddImage(brt, "CrownIcon", ToyKit.Get("icon_crown"), Color.white);
            Text cv = UiKit.AddText(brt, CrownValueName, crownText, 56, Theme.Ink, TextAnchor.MiddleCenter, true);
            Image ringImg = UiKit.AddImage(brt, "InfoRing", Glyphs.Ring, Theme.Ink);
            Text iText = UiKit.AddText(ringImg.transform, "I", "i", 42, Theme.Ink, TextAnchor.MiddleCenter, true);
            UiKit.OnPanel(cv);
            UiKit.OnPanel(iText);
            UiKit.Stretch((RectTransform)iText.transform);
            iText.horizontalOverflow = HorizontalWrapMode.Overflow;
            iText.verticalOverflow = VerticalWrapMode.Overflow;

            // second group
            RectTransform sec = UiKit.Rect(row, SecondName);
            sec.gameObject.AddComponent<A11yLabel>().Text = secondA11y;
            Image secIcon = UiKit.AddImage(sec, "SecondIcon", secondIcon, Color.white);
            Text sv = UiKit.AddText(sec, SecondValueName, secondText, 56, Theme.Ink, TextAnchor.MiddleCenter, true);
            UiKit.OnPanel(sv);

            Image sep = UiKit.AddImage(row, "Separator", null, new Color(Theme.InkSoft.r, Theme.InkSoft.g, Theme.InkSoft.b, 0.55f));

            cv.horizontalOverflow = sv.horizontalOverflow = HorizontalWrapMode.Overflow;
            cv.verticalOverflow = sv.verticalOverflow = VerticalWrapMode.Truncate;
            int minFs = Mathf.RoundToInt(UiKit.MinBodyTextPx);
            float cvW, svW, crownW, secW, total;
            int guard = 0;
            while (true)
            {
                cvW = Mathf.Ceil(cv.preferredWidth) + 6f;
                svW = Mathf.Ceil(sv.preferredWidth) + 6f;
                crownW = pad + icon + gap + cvW + gap + ring + pad;
                secW = icon + gap + svW;
                total = crownW + sepGap * 2f + sepW + secW;
                if (total <= ContentW || cv.fontSize <= minFs || ++guard > 40) break;
                cv.fontSize = Mathf.Max(minFs, cv.fontSize - 2);   // one line, never wrapped: shrink toward the 16 sp floor
                sv.fontSize = cv.fontSize;
            }

            float x0 = -total * 0.5f;
            Vector2 mid = new Vector2(0.5f, 0.5f), left = new Vector2(0f, 0.5f), right = new Vector2(1f, 0.5f);
            SetRect(brt, mid, left, new Vector2(x0, 0f), new Vector2(crownW, h));
            SetRect(crownIcon.rectTransform, left, left, new Vector2(pad, 0f), new Vector2(icon, icon));
            SetRect(cv.rectTransform, left, left, new Vector2(pad + icon + gap, 0f), new Vector2(cvW, h));
            SetRect(ringImg.rectTransform, right, right, new Vector2(-pad, 0f), new Vector2(ring, ring));
            SetRect(sep.rectTransform, mid, left, new Vector2(x0 + crownW + sepGap, 0f), new Vector2(sepW, h * 0.6f));
            SetRect(sec, mid, left, new Vector2(x0 + crownW + sepGap * 2f + sepW, 0f), new Vector2(secW, h));
            SetRect(secIcon.rectTransform, left, left, Vector2.zero, new Vector2(icon, icon));
            SetRect(sv.rectTransform, left, left, new Vector2(icon + gap, 0f), new Vector2(svW, h));
            return row;
        }

        static void SetRect(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        // ---------------- the calculation pop-up ----------------

        /// <summary>
        /// Fills the frame <paramref name="panel"/> (from <see cref="UiKit.BuildPopupFrame"/>, the X is its only close) with the crown calculation and resizes it to its content (at most the safe height;
        /// a taller content, only possible at the largest text size on a short screen, scrolls inside the panel). <paramref name="medals"/> is null for Endless and for levels without medal targets.
        /// </summary>
        public static void BuildCalc(RectTransform panel, CrownBreakdown b, string modeName, bool endless, CrownCalcMedal[] medals)
        {
            float titleZone = LeaderboardScreen.FitPopupTitle(panel) + 4f;   // the title at full size (one or two lines), then the content
            RectTransform vp = UiKit.Rect(panel, ViewportName);
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(30f, BottomPad);
            vp.offsetMax = new Vector2(-30f, -titleZone);
            vp.gameObject.AddComponent<RectMask2D>();
            RectTransform content = UiKit.Rect(vp, CalcName);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            float total = FillCalc(content, b, modeName, endless, medals);
            content.sizeDelta = new Vector2(0f, total);

            float maxPanel = Mathf.Max(900f, UiKit.SafeSizeRef().y - 140f);
            float panelH = Mathf.Min(titleZone + total + BottomPad, maxPanel);
            panel.sizeDelta = new Vector2(PanelW, panelH);
            float viewH = panelH - titleZone - BottomPad;
            if (total > viewH + 0.5f)
            {
                Image catcher = vp.gameObject.AddComponent<Image>();   // a transparent surface so a drag anywhere in the viewport scrolls
                catcher.color = new Color(0f, 0f, 0f, 0f);
                var sr = vp.gameObject.AddComponent<ScrollRect>();
                sr.viewport = vp;
                sr.content = content;
                sr.horizontal = false;
                sr.vertical = true;
                sr.movementType = ScrollRect.MovementType.Clamped;
                sr.scrollSensitivity = 40f;
            }
        }

        /// <summary>Adds the lines under <paramref name="parent"/> (anchored top centre) and returns their total height. Compact on purpose: it must fit without scrolling at normal text size on every aspect.</summary>
        static float FillCalc(RectTransform parent, CrownBreakdown b, string modeName, bool endless, CrownCalcMedal[] medals)
        {
            float y = 0f, h;
            Color ink = Theme.Ink, soft = Theme.InkSoft;

            Txt(parent, "Formula", "Crown = (floors x " + CrownScore.FloorPoints + " + combo points) x (1 + bonus)", 42, ink, 0.5f, 0f, y, ContentW, false, false, out h);
            y += h + 2f;
            Txt(parent, "ComboRule", "Combo step n adds n x n x " + CrownScore.ComboPoints + " points.", 42, soft, 0.5f, 0f, y, ContentW, false, false, out h);
            y += h + 8f;

            string bonus = "+" + b.BonusPercent + "%", bonusLabel = modeName + " bonus";
            if (!b.HasRun)
            {
                y += Row(parent, "RowFloors", "floors x " + CrownScore.FloorPoints, "?", y, false, null);
                y += Row(parent, "RowCombo", "Combo points", "?", y, false, null);
                y += Row(parent, "RowBonus", bonusLabel, bonus, y, false, null);
                y += Rule(parent, y);
                y += Row(parent, "RowCrown", "Crown", "?", y, true, null);
                Txt(parent, "Hint", endless ? "Play Endless to set your crown." : "Play this level to set your crown.", 42, soft, 0.5f, 0f, y + 2f, ContentW, false, true, out h);
                y += h + 8f;
            }
            else if (b.PartsKnown)
            {
                y += Row(parent, "RowFloors", b.Floors + " floors x " + CrownScore.FloorPoints, b.FloorPoints.ToString(), y, false, null);
                y += Row(parent, "RowCombo", "Combo points" + (b.BestCombo > 0 ? " (chain " + b.BestCombo + ")" : ""), b.ComboPoints.ToString(), y, false, null);
                y += Row(parent, "RowBonus", bonusLabel, bonus, y, false, null);
                y += Rule(parent, y);
                y += Row(parent, "RowCrown", "Crown", b.Crown.ToString(), y, true, null);
                y += 6f;
            }
            else
            {
                y += Row(parent, "RowTotal", "Score (floors + combo)", b.BaseScore > 0 ? b.BaseScore.ToString() : "?", y, false, null);
                y += Row(parent, "RowBonus", bonusLabel, bonus, y, false, null);
                y += Rule(parent, y);
                y += Row(parent, "RowCrown", "Crown", b.Crown.ToString(), y, true, null);
                Txt(parent, "Hint", "Floors and combo were not saved for this older best.", 42, soft, 0.5f, 0f, y + 2f, ContentW, false, true, out h);
                y += h + 8f;
            }

            if (medals != null && medals.Length > 0)
            {
                Txt(parent, "MedalsCaption", "Medals need pace, not crown:", 42, ink, 0.5f, 0f, y, ContentW, false, false, out h);
                y += h + 2f;
                for (int i = 0; i < medals.Length; i++)
                {
                    CrownCalcMedal m = medals[i];
                    string name = MedalIcons.Name(m.Tier);
                    y += Row(parent, name + "Row", name + (m.Earned ? " (earned)" : ""), MedalStrip.PaceLineText(m.PaceMilli), y, false, MedalIcons.For(m.Tier, m.Earned));
                }
            }
            else if (endless)
            {
                Txt(parent, "NoMedals", "Endless has no medals.", 42, soft, 0.5f, 0f, y, ContentW, false, false, out h);
                y += h + 2f;
            }
            Txt(parent, "Tip", "Climb higher, chain bigger combos.", 42, soft, 0.5f, 0f, y + 4f, ContentW, false, true, out h);
            y += h + 4f;
            return y;
        }

        /// <summary>A thin ink line across the content (a sum line); returns the height it takes.</summary>
        static float Rule(RectTransform parent, float y)
        {
            Image r = UiKit.AddImage(parent, "Rule", null, new Color(Theme.InkSoft.r, Theme.InkSoft.g, Theme.InkSoft.b, 0.45f));
            r.rectTransform.anchorMin = new Vector2(0f, 1f);
            r.rectTransform.anchorMax = new Vector2(1f, 1f);
            r.rectTransform.pivot = new Vector2(0.5f, 1f);
            r.rectTransform.anchoredPosition = new Vector2(0f, -(y + 3f));
            r.rectTransform.sizeDelta = new Vector2(0f, 3f);
            return 9f;
        }

        /// <summary>A label on the left (Nunito), a value on the right (Lilita One), an optional icon before the label. Returns the row height (the taller of the two texts plus a gap).</summary>
        static float Row(RectTransform parent, string name, string label, string value, float y, bool strong, Sprite icon)
        {
            float vw = strong ? 300f : 270f, iconW = icon != null ? 76f : 0f, lw = ContentW - vw - 16f - iconW;
            float hl, hv;
            if (icon != null)
            {
                Image ic = UiKit.AddImage(parent, name + "Icon", icon, Color.white);
                SetTop(ic.rectTransform, 10f, y, 60f, 60f);
            }
            Text l = Txt(parent, name + "Label", label, 42, Theme.Ink, 0f, 0f, y, lw, false, false, out hl, TextAnchor.MiddleLeft, iconW);
            Text v = Txt(parent, name + "Value", value, strong ? 60 : 50, Theme.Ink, 1f, 0f, y, vw, true, false, out hv, TextAnchor.MiddleRight, 0f);
            // the value is one line: shrink it toward the 16 sp floor instead of wrapping
            v.horizontalOverflow = HorizontalWrapMode.Overflow;
            float rowH = Mathf.Max(Mathf.Max(hl, hv), icon != null ? 64f : 0f);
            l.rectTransform.sizeDelta = new Vector2(lw, rowH);
            v.rectTransform.sizeDelta = new Vector2(vw, rowH);
            return rowH + 2f;
        }

        static void SetTop(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>
        /// An ink text anchored by its top edge: <paramref name="ax"/> 0 = left edge, 0.5 = centred, 1 = right edge (offset <paramref name="x"/> from that edge). The height is measured at the real width,
        /// so wrapped lines (large text) always get the room they need.
        /// </summary>
        static Text Txt(RectTransform parent, string name, string text, int size, Color color, float ax, float x, float y, float w, bool display, bool italic, out float h, TextAnchor align = TextAnchor.MiddleCenter, float leftOffset = 0f)
        {
            Text t = UiKit.AddText(parent, name, text, size, color, align, display);
            UiKit.OnPanel(t);
            t.color = color;
            if (italic) t.fontStyle = FontStyle.Italic;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(ax, 1f);
            rt.pivot = new Vector2(ax, 1f);
            float lh = TimedUi.LineHeight();
            float edge = ax < 0.01f ? 10f + leftOffset : (ax > 0.99f ? -10f : 0f);   // the content is 800 wide, the texts 780
            rt.anchoredPosition = new Vector2(x + edge, -y);
            rt.sizeDelta = new Vector2(w, lh);
            h = Mathf.Max(lh, Mathf.Ceil(t.preferredHeight));
            rt.sizeDelta = new Vector2(w, h);
            return t;
        }
    }
}
