using System;
using System.Collections.Generic;
using BoingUpSkyTower.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>Cards, plates and bars of the Rewards screen (RewardsStyle: cream open, yellow ready with a big green Claim button on its reward plate, green claimed with a check).</summary>
    public sealed partial class RewardsScreen
    {
        enum CardState { Open, Ready, Done }

        // ---- card metrics (compact lists, Bug List 4 E4.c) ----
        // The card is 136 px on the 1080 reference (it was 214) and the gap 10 (it was 49). It cannot be as low as 90: a ready card carries a real Claim button
        // and every button must be at least 48 dp (UiKit.MinTouchPx, about 126 px), so the card is that button plus 5 px above and below.
        const float IconW = 104f, CardPadX = 20f, CardCorner = 48f, RewardCorner = 30f, RailW = 90f, TagW = 190f;
        const float CardPadY = 8f, ItemIcon = 48f, ItemGap = 12f, PlatePad = 14f, CheckW = 44f, RailBadge = 80f;
        /// <summary>Space between cards.</summary>
        public const float CardGap = 10f;
        /// <summary>Base height of a card; it grows (never shrinks) with the text size and a wrapped title.</summary>
        public const float CompactCardH = 136f;
        /// <summary>Space between the title and the progress bar in the card's text column (at least 6 px).</summary>
        public const float TitleBarGap = 6f;
        /// <summary>Height of the green Claim button on a ready card: the 48 dp touch minimum.</summary>
        public static float ClaimButtonH { get { return Mathf.Ceil(UiKit.MinTouchPx); } }
        static float BarH { get { return LineH(BodyPx) + 14f; } }
        static float CardBarH { get { return LineH(BodyPx) + 4f; } }
        static float PlateRowH { get { return Mathf.Max(ItemIcon, LineH(BodyPx)) + 16f; } }

        /// <summary>Height of a card whose title needs <paramref name="titleH"/>: the compact base, or the text (title over the bar) when that is more, or the plate row.</summary>
        public static float CardHeightFor(float titleH)
        {
            float byText = CardPadY + titleH + TitleBarGap + CardBarH + CardPadY;
            float byPlate = PlateRowH + 12f;
            return Mathf.Max(CompactCardH, byText, byPlate, ClaimButtonH + 10f);
        }

        /// <summary>Height of a card with a one-line title at the current text size (a longer title makes its own card taller).</summary>
        public static float CardHeight { get { return CardHeightFor(LineH(BodyPx)); } }

        static Color StateTint(CardState s)
        {
            switch (s)
            {
                case CardState.Done: return new Color(0.55f, 0.86f, 0.38f, 1f);
                case CardState.Ready: return new Color(1f, 0.88f, 0.18f, 1f);
                default: return new Color(0.86f, 0.88f, 0.96f, 1f);
            }
        }

        // ---------------- small builders ----------------

        /// <summary>A 9-sliced plate from the UI kit (plate_cream, plate_chip ...) tinted; a plain rounded shape when the kit file is missing.</summary>
        static Image Plate(Transform parent, string name, string kitSprite, Color tint, float corner)
        {
            Sprite sp = kitSprite != null ? ToyKit.Get(kitSprite) : null;
            Image img = ToyKit.Sliced(parent, name, sp != null ? sp : UiKit.PillSprite, corner);
            img.color = tint;
            img.raycastTarget = false;
            return img;
        }

        static RectTransform FixedHeight(RectTransform rt, float h)
        {
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = h;
            le.flexibleHeight = 0f;
            return rt;
        }

        static void SetPref(GameObject go, float w, float h)
        {
            var le = go.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = w;
            le.minHeight = le.preferredHeight = h;
            le.flexibleWidth = le.flexibleHeight = 0f;
        }

        /// <summary>A thin dark progress bar with the fill and the "n/N" label inside (white with an outline). Place the returned rectangle.</summary>
        RectTransform Bar(RectTransform parent, string label, float fraction, Color fill, float h)
        {
            RectTransform bar = UiKit.Rect(parent, "Bar");
            Image track = Plate(bar, "Backdrop", null, TrackDark, h * 0.5f);
            UiKit.Stretch(track.rectTransform);
            if (fraction > 0.001f)
            {
                float f = Mathf.Max(Mathf.Clamp01(fraction), 0.1f);   // never thinner than the rounded ends
                Image fi = Plate(bar, "Fill", null, fill, (h - 10f) * 0.5f);
                fi.rectTransform.anchorMin = Vector2.zero;
                fi.rectTransform.anchorMax = new Vector2(f, 1f);
                fi.rectTransform.offsetMin = new Vector2(5f, 5f);
                fi.rectTransform.offsetMax = new Vector2(-5f, -5f);
            }
            Text t = UiKit.AddText(bar, "Count", label, BodyPx, Color.white, TextAnchor.MiddleCenter, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Stretch(t.rectTransform);
            return bar;
        }

        Sprite CoinSprite { get { return art != null ? art.CoinIcon : null; } }
        Sprite GemSprite { get { return art != null ? art.GemIcon : null; } }
        static Sprite BoltSprite { get { return RewardArt.Icon("bolt") ?? ToyKit.Get("icon_bolt"); } }
        Sprite StarSprite { get { return RewardArt.Icon("star1") ?? (art != null ? art.StarFilled : null); } }

        // ---------------- task pictures ----------------

        /// <summary>
        /// The picture of a task id: the new named slot (task_*.png) when the file is there, else the older OpenArt icon, else a UI kit icon.
        /// slot = the Resources/Art/Rewards file (null = no new art for this task), icon = the older icon_* name, kit = the Art/Ui fallback.
        /// </summary>
        public static void TaskSources(string id, out string slot, out string icon, out string kit)
        {
            slot = null; icon = "star1"; kit = "icon_star";
            switch (id)
            {
                case "d_login": slot = "task_key"; icon = "key"; kit = "icon_daily"; break;
                case "d_play5": slot = "task_hourglass"; icon = "hourglass"; kit = "icon_dial"; break;
                case "d_level1": case "d_level3": slot = "task_flag"; icon = id == "d_level1" ? "flag1" : "flag3"; kit = "icon_flag"; break;
                case "d_combo3": slot = "task_flame"; icon = "flame"; kit = "icon_flame"; break;
                case "d_hard": slot = "task_boss_skull"; icon = "helmet"; kit = "icon_bolt"; break;
                case "d_nowall": icon = "wall"; kit = "icon_hand"; break;
                case "d_gemlevel": slot = "task_gem"; icon = "gem"; kit = "icon_gem"; break;
                case "d_stars2": icon = "star1"; kit = "icon_star"; break;
                case "d_powerup": slot = "task_upgrade"; icon = "bolt"; kit = "icon_bolt"; break;
                case "w_login7": slot = "task_calendar"; icon = "calendar"; kit = "icon_daily"; break;
                case "w_play100": case "w_play150": slot = "task_hourglass"; icon = "clock"; kit = "icon_dial"; break;
                case "w_levels15": slot = "task_flag"; icon = "levels"; kit = "icon_flag"; break;
                case "w_stars20": icon = "medal"; kit = "icon_star"; break;
                case "w_nowall": icon = "wall"; kit = "icon_hand"; break;
                case "w_stars3": icon = "star3"; kit = "icon_star"; break;
                default:
                    if (id != null && id.StartsWith("w_combos", StringComparison.Ordinal)) { slot = "task_flame"; icon = "flame"; kit = "icon_flame"; }
                    else if (id == "s_225") { icon = "trophy"; kit = "icon_star"; }
                    break;
            }
        }

        static Sprite TaskIcon(string id)
        {
            string slot, icon, kit;
            TaskSources(id, out slot, out icon, out kit);
            Sprite s = RewardArt.Slot(slot) ?? RewardArt.Task(id) ?? RewardArt.Icon(icon) ?? ToyKit.Get(kit);
            return s;
        }

        // ---------------- reward plate ----------------

        struct RItem { public Sprite Icon; public string Text; }

        List<RItem> RewardItems(RewardEntry e, bool pointsOnly)
        {
            var l = new List<RItem>(3);
            if (pointsOnly) { l.Add(new RItem { Icon = StarSprite, Text = "+" + e.Points }); return l; }
            if (e.Gems > 0) l.Add(new RItem { Icon = GemSprite, Text = Compact(e.Gems) });
            if (e.Coins > 0) l.Add(new RItem { Icon = CoinSprite, Text = Compact(e.Coins) });
            if (e.PowerUps > 0) l.Add(new RItem { Icon = BoltSprite, Text = "x" + e.PowerUps });
            return l;
        }

        /// <summary>Icons and amounts inside a dark plate: one reward is a big icon over its amount, two or more are stacked rows. No overlap by construction (layout groups).</summary>
        static void FillRewardPlate(RectTransform plate, List<RItem> items)
        {
            if (items.Count == 0) return;
            var vl = plate.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(6, 6, 10, 10);
            vl.spacing = 4f;
            vl.childAlignment = TextAnchor.MiddleCenter;
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = vl.childForceExpandHeight = false;
            if (items.Count == 1)
            {
                Image ic = UiKit.AddImage(plate, "Icon", items[0].Icon, Color.white);
                SetPref(ic.gameObject, 76f, 76f);
                Text t = UiKit.AddText(plate, "Amount", items[0].Text, BodyPx, Color.white, TextAnchor.MiddleCenter, true);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                return;
            }
            foreach (RItem it in items)
            {
                RectTransform row = UiKit.Rect(plate, "Item");
                var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                hl.spacing = 4f;
                hl.childAlignment = TextAnchor.MiddleCenter;
                hl.childControlWidth = hl.childControlHeight = true;
                hl.childForceExpandWidth = hl.childForceExpandHeight = false;
                Image ic = UiKit.AddImage(row, "Icon", it.Icon, Color.white);
                SetPref(ic.gameObject, 56f, 56f);
                Text t = UiKit.AddText(row, "Amount", it.Text, BodyPx, Color.white, TextAnchor.MiddleCenter, true);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
        }

        /// <summary>A dark reward plate (chip) with the items; <paramref name="plate"/> is returned so the caller can anchor it.</summary>
        RectTransform RewardPlate(Transform parent, string name, List<RItem> items)
        {
            Image p = Plate(parent, name, "plate_chip", Color.white, RewardCorner);
            FillRewardPlate(p.rectTransform, items);
            return p.rectTransform;
        }

        static float AmountW(string text)
        {
            float fs = Mathf.Round(Mathf.Max(BodyPx, UiKit.MinBodyTextPx) * UiKit.TextScale);
            return Mathf.Ceil(fs * 0.62f * (text != null ? text.Length : 1));
        }

        /// <summary>Width of a card's reward plate (a row of icon + amount items; a ready card adds the check glyph and is at least a 48 dp touch target).</summary>
        static float CardPlateWidth(List<RItem> items, bool claim)
        {
            float w = 2f * PlatePad + (claim ? CheckW + ItemGap : 0f);
            for (int i = 0; i < items.Count; i++) w += ItemIcon + 6f + AmountW(items[i].Text) + (i > 0 ? ItemGap : 0f);
            return Mathf.Max(w, claim ? ClaimButtonH : 0f);
        }

        /// <summary>
        /// The reward plate of a card: one row of icon + amount items. Ready cards get the green plate (btn_green) with a check glyph first: it is the Claim button.
        /// </summary>
        static Image CardPlate(Transform parent, List<RItem> items, bool claim)
        {
            Image p = Plate(parent, "RewardPlate", claim ? "btn_green" : "plate_chip", Color.white, RewardCorner);
            var hl = p.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset((int)PlatePad, (int)PlatePad, 6, 6);
            hl.spacing = ItemGap;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            if (claim)
            {
                Image tick = UiKit.AddImage(p.transform, "Check", RewardArt.Icon("check") ?? Glyphs.Check, Color.white);
                SetPref(tick.gameObject, CheckW, CheckW);
            }
            foreach (RItem it in items)
            {
                RectTransform row = UiKit.Rect(p.transform, "Item");
                var rl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                rl.spacing = 6f;
                rl.childAlignment = TextAnchor.MiddleCenter;
                rl.childControlWidth = rl.childControlHeight = true;
                rl.childForceExpandWidth = rl.childForceExpandHeight = false;
                Image ic = UiKit.AddImage(row, "Icon", it.Icon, Color.white);
                SetPref(ic.gameObject, ItemIcon, ItemIcon);
                Text t = UiKit.AddText(row, "Amount", it.Text, BodyPx, Color.white, TextAnchor.MiddleCenter, true);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                if (claim) UiKit.OnPanel(t).color = UiKit.ButtonInk;
            }
            return p;
        }

        // ---------------- claim tag, check, claimable ----------------

        /// <summary>A round green check badge (shape and colour both say "done").</summary>
        static Image CheckBadge(Transform parent, string name, float size)
        {
            Sprite circle = ToyKit.Get("plate_circle") ?? Theme.RoundSprite;
            Image badge = UiKit.AddImage(parent, name, circle, new Color(0.30f, 0.80f, 0.36f, 1f));
            Image mark = UiKit.AddImage(badge.transform, "Mark", RewardArt.Icon("check") ?? Glyphs.Check, Color.white);
            UiKit.Stretch(mark.rectTransform);
            mark.rectTransform.offsetMin = new Vector2(size * 0.16f, size * 0.16f);
            mark.rectTransform.offsetMax = new Vector2(-size * 0.16f, -size * 0.16f);
            return badge;
        }

        /// <summary>Turns a rectangle into the claim button for an entry; <paramref name="face"/> is the image that takes the touch and shows the press tint.</summary>
        void MakeClaimable(RectTransform host, Image face, RewardTab tab, RewardEntry e, string a11y)
        {
            if (face != null) face.raycastTarget = true;
            var btn = host.gameObject.AddComponent<Button>();
            btn.targetGraphic = face;
            string id = e.Id;
            btn.onClick.AddListener(() => { AudioManager.Play(Sfx.Tap); Claim(tab, id); });
            host.gameObject.AddComponent<A11yLabel>().Text = a11y;
        }

        /// <summary>Height the title needs when wrapped at <paramref name="width"/> canvas units (extents are scaled by pixelsPerUnit like Text.preferredHeight does).</summary>
        static float MeasureTitle(Text t, float width)
        {
            float ppu = Mathf.Max(0.01f, t.pixelsPerUnit);
            float m = t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text, t.GetGenerationSettings(new Vector2(width * ppu, 0f))) / ppu;
            return Mathf.Ceil(m);
        }

        string TitleOf(RewardEntry e)
        {
            return Tab == RewardTab.Main ? "Collect " + e.Target + " Stars" : e.Title;
        }

        // ---------------- the card ----------------

        /// <summary>
        /// One task card: icon at the left, the title on top, a bar with "n/N" under it and the reward plate at the right (compact, Bug List 4 E4.c).
        /// <paramref name="claimable"/> cards (stars, weekly tasks) are ready when complete and unclaimed: the card turns yellow and its reward plate becomes a BIG GREEN
        /// CLAIM BUTTON (check glyph + the reward, at least 48 dp, "Claim 20 gems" for screen readers) that claims exactly that reward; tapping the rest of the card claims it too
        /// (button and card both claim). Claimed cards turn green with a check badge. Daily action cards only show points (green when done).
        /// <paramref name="rail"/> (Main) adds the numbered rail with its line at the left. The card height is the compact base or the measured title + bar when that is more.
        /// </summary>
        void AddCard(RectTransform list, RewardEntry e, bool claimable, int index, int count, bool rail)
        {
            int value = progress.RewardProgress(Tab, e);
            int target = progress.RewardTarget(Tab, e);
            bool claimed = claimable ? progress.IsRewardClaimed(Tab, e) : value >= target;
            bool ready = claimable && !claimed && value >= target;
            CardState st = claimed ? CardState.Done : (ready ? CardState.Ready : CardState.Open);

            RectTransform row = UiKit.Rect(list, "Row_" + e.Id);
            RectTransform card = UiKit.Rect(row, "Card");
            UiKit.Stretch(card);
            if (rail) card.offsetMin = new Vector2(RailW + 12f, 0f);

            Image bg = Plate(card, "Bg", "plate_cream", StateTint(st), CardCorner);
            UiKit.Stretch(bg.rectTransform);

            List<RItem> items = RewardItems(e, !claimable);
            float plateW = CardPlateWidth(items, ready);
            float midL = CardPadX + IconW + 14f, midR = CardPadX + plateW + 14f;
            float listW = screen != null && screen.rect.width > 200f ? screen.rect.width : 1080f;
            float cardW = listW - 2f * Side - (rail ? RailW + 12f : 0f);

            Text title = UiKit.AddText(card, "Title", TitleOf(e), BodyPx, Theme.Ink, TextAnchor.MiddleCenter, true);
            UiKit.OnPanel(title);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            float textW = Mathf.Max(1f, cardW - midL - midR - 4f);
            float titleH = Mathf.Max(LineH(BodyPx), MeasureTitle(title, textW));   // same measure for the card height and the title rect
            if (UiKit.TextScale > 1.01f)
            {
                // 130 percent text: shrink the title down to the 16 sp minimum before the card grows.
                float budget = CompactCardH - 2f * CardPadY - TitleBarGap - CardBarH;
                int minFs = Mathf.CeilToInt(UiKit.MinBodyTextPx);
                while (titleH > budget && title.fontSize - 2 >= minFs)
                {
                    title.fontSize -= 2;
                    titleH = Mathf.Max(Mathf.Ceil(title.fontSize * 1.22f), MeasureTitle(title, textW));
                }
            }
            float h = CardHeightFor(titleH);
            FixedHeight(row, h);
            if (rail) BuildRail(row, index, count, st);
            // Text column = title over the bar, one stack from the top (they cannot overlap); extra card height is split above and below.
            float stack = titleH + TitleBarGap + CardBarH;
            float top = Mathf.Max(CardPadY, (h - stack) * 0.5f);
            RectTransform trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(midL, -(top + titleH));
            trt.offsetMax = new Vector2(-midR, -top);

            Image icon = UiKit.AddImage(card, "Icon", TaskIcon(e.Id), Color.white);
            UiKit.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(CardPadX, 0f), new Vector2(IconW, IconW));
            if (claimed)
            {
                Image tick = CheckBadge(card, "Check", 48f);
                UiKit.Place(tick.rectTransform, new Vector2(0f, 0.5f), new Vector2(CardPadX + IconW - 40f, -IconW * 0.5f + 20f), new Vector2(48f, 48f));
            }

            int shown = Mathf.Min(value, target);
            string label = (e.Stat == RewardStat.PlaySeconds) ? (shown / 60) + "/" + (target / 60) : Num(shown) + "/" + Num(target);
            Color fill = claimed || ready ? BarGreen : BarBlue;
            RectTransform bar = Bar(card, label, target > 0 ? Mathf.Clamp01(value / (float)target) : 0f, fill, CardBarH);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(midL, -(top + titleH + TitleBarGap + CardBarH));
            bar.offsetMax = new Vector2(-midR, -(top + titleH + TitleBarGap));

            Image plate = CardPlate(card, items, ready);
            float plateH = ready ? ClaimButtonH : Mathf.Max(PlateRowH, h - 28f);
            UiKit.Place(plate.rectTransform, new Vector2(1f, 0.5f), new Vector2(-CardPadX, 0f), new Vector2(plateW, plateH));

            if (ready)
            {
                MakeClaimable(card, bg, Tab, e, TitleOf(e) + ": claim " + Describe(e));                       // the rest of the card claims too
                MakeClaimable(plate.rectTransform, plate, Tab, e, "Claim " + Describe(e));                    // the green button claims exactly this reward
            }
            else if (claimable) card.gameObject.AddComponent<A11yLabel>().Text = TitleOf(e) + ": " + label + (claimed ? ", claimed" : "");
        }

        /// <summary>The numbered rail of the Main list: a dark round badge with the milestone number (a check once claimed) on a line that runs through the cards.</summary>
        void BuildRail(RectTransform row, int index, int count, CardState st)
        {
            RectTransform rail = UiKit.Rect(row, "Rail");
            rail.anchorMin = new Vector2(0f, 0f);
            rail.anchorMax = new Vector2(0f, 1f);
            rail.pivot = new Vector2(0f, 0.5f);
            rail.offsetMin = Vector2.zero;
            rail.offsetMax = new Vector2(RailW, 0f);

            bool first = index == 0, last = index == count - 1;
            Image line = Plate(rail, "Backdrop", null, st == CardState.Done ? BarGreen : new Color(0.42f, 0.48f, 0.80f, 1f), 7f);
            RectTransform lrt = line.rectTransform;
            lrt.anchorMin = new Vector2(0.5f, last ? 0.5f : 0f);
            lrt.anchorMax = new Vector2(0.5f, first ? 0.5f : 1f);
            lrt.offsetMin = new Vector2(-7f, last ? 0f : -CardGap);
            lrt.offsetMax = new Vector2(7f, first ? 0f : CardGap);

            Sprite circle = ToyKit.Get("plate_circle") ?? Theme.RoundSprite;
            Image badge = UiKit.AddImage(rail, "Badge", circle, Color.white);
            badge.preserveAspect = true;
            UiKit.Place(badge.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(RailBadge, RailBadge));
            if (st == CardState.Done)
            {
                Image mark = UiKit.AddImage(badge.transform, "Mark", RewardArt.Icon("check") ?? Glyphs.Check, new Color(0.45f, 0.95f, 0.5f, 1f));
                UiKit.Stretch(mark.rectTransform);
                mark.rectTransform.offsetMin = new Vector2(14f, 14f);
                mark.rectTransform.offsetMax = new Vector2(-14f, -14f);
            }
            else
            {
                Text n = UiKit.AddText(badge.transform, "Number", (index + 1).ToString(), BodyPx, st == CardState.Ready ? Theme.Gold : Color.white, TextAnchor.MiddleCenter, true);
                n.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiKit.Stretch(n.rectTransform);
            }
        }
    }
}
