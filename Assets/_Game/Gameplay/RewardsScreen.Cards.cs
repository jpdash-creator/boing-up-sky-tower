using System;
using System.Collections.Generic;
using FluffyClimbers.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>Cards, plates and bars of the Rewards screen (RewardsStyle: cream open, yellow ready with a big green Claim button on its reward plate, green claimed with a check).</summary>
    public sealed partial class RewardsScreen
    {
        enum CardState { Open, Ready, Done }

        // ---- card metrics (compact lists, Bug List 4 E4.c) ----
        // The card is 136 px on the 1080 reference (it was 214) and the gap 10 (it was 49). It cannot be as low as 90: a ready card carries a real Claim button
        // and every button must be at least 48 dp (UiKit.MinTouchPx, about 126 px), so the card is that button plus 5 px above and below.
        const float IconW = 104f, CardPadX = 20f, CardCorner = 48f, RewardCorner = 30f;
        const float CardPadY = 8f, ItemIcon = 48f, ItemGap = 12f, PlatePad = 14f, CheckW = 44f, HeroFaceSize = 72f;
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
            titleH = Mathf.Max(titleH, 2f * Mathf.Ceil(LineH(BodyPx) * 1.0f));   // Bug List 8 follow-up: every card has room for a two-line title, so a wrapped title never looks cut
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
                case "d_login": slot = "task_key"; icon = "key"; kit = "icon_daily"; break;                                   // log in: key
                case "d_play10": case "d_play25": case "d_play60": case "w_play250": slot = "task_hourglass"; icon = "hourglass"; kit = "icon_dial"; break;   // play minutes: hourglass
                case "d_level3": case "d_level6": case "d_level10": slot = "task_flag"; icon = "flag3"; kit = "icon_flag"; break;   // complete levels: flag
                case "d_combo10": case "d_combo30": case "w_combos300": slot = "task_flame"; icon = "flame"; kit = "icon_flame"; break;   // combos: flame
                case "d_stars9": case "w_stars40": icon = "star1"; kit = "icon_star"; break;                                    // earn stars: star
                case "d_endless50": case "d_endless100": case "d_endlessruns": case "w_endless150": case "w_endless200": case "w_endlessruns20": icon = null; kit = "icon_tower"; break;   // Endless: tower
                case "d_powerups": case "w_powerups20": icon = "bolt"; kit = "icon_bolt"; break;                                // use power-ups: bolt
                case "d_medal": case "w_medals8": icon = "medal"; kit = "icon_crown"; break;                                    // earn medals: medal (any colour)
                case "d_coins": slot = "task_coins"; icon = "coinbag"; kit = "icon_coin"; break;                                // earn coins: coin
                case "d_ta": icon = "clock"; kit = "icon_tower"; break;                                                         // Time Attack floors: clock
                case "w_jumps8000": slot = "task_upgrade"; icon = "bolt"; kit = "icon_speed"; break;                            // jumps: up arrow
                case "w_login7": slot = "task_calendar"; icon = "calendar"; kit = "icon_daily"; break;                          // log in every day: calendar
                case "w_levels40": slot = "task_flag"; icon = "levels"; kit = "icon_flag"; break;
                case "w_hard10": slot = "task_boss_skull"; icon = "helmet"; kit = "icon_bolt"; break;
                case "d_hard": slot = "task_boss_skull"; icon = "helmet"; kit = "icon_bolt"; break;                             // hard levels: boss skull
                case "w_login5": slot = "task_calendar"; icon = "calendar"; kit = "icon_daily"; break;                          // log in on days: calendar
                case "w_play100": slot = "task_hourglass"; icon = "hourglass"; kit = "icon_dial"; break;
                case "w_powerups10": icon = "bolt"; kit = "icon_bolt"; break;                                                   // use power-ups: bolt
                case "w_stars20": icon = "star1"; kit = "icon_star"; break;                                                     // earn stars: star
                case "w_levels20": slot = "task_flag"; icon = "levels"; kit = "icon_flag"; break;
                case "w_nowall2": icon = "wall"; kit = "icon_hand"; break;                                                      // no-wall: wall
                case "w_endless100": icon = null; kit = "icon_tower"; break;                                                    // endless floor: tower
                case "w_combos150": slot = "task_flame"; icon = "flame"; kit = "icon_flame"; break;
                case "w_hard5": slot = "task_boss_skull"; icon = "helmet"; kit = "icon_bolt"; break;
                case "d_pace2": case "w_pace3": icon = "clock"; kit = "icon_speed"; break;                                      // pace goals: clock
                case "d_floor100": icon = null; kit = "icon_tower"; break;                                                      // endless floor 100 in 3:00: tower
                case "w_ta100": icon = "clock"; kit = "icon_tower"; break;                                                      // Time Attack floors: clock
                case "w_gold5": icon = "medal"; kit = "icon_crown"; break;                                                      // gold medals: gold medal (TaskIcon draws MedalIcons gold)
                default:
                    if (id != null && id.StartsWith("g_", StringComparison.Ordinal)) GoalSources(id, out slot, out icon, out kit);
                    break;
            }
        }

        /// <summary>Icon of a region goal by the type token inside its id (g_&lt;region&gt;_&lt;token&gt;_&lt;tier&gt;).</summary>
        static void GoalSources(string id, out string slot, out string icon, out string kit)
        {
            string[] parts = id.Split('_');
            string token = parts.Length > 2 ? parts[2] : "";
            slot = null; icon = "star1"; kit = "icon_star";
            switch (token)
            {
                case "stars": break;                                                                              // stars: star
                case "levels": slot = "task_flag"; icon = "flag1"; kit = "icon_flag"; break;                     // levels: flag
                case "stars3": icon = "star3"; kit = "icon_star"; break;                                          // 3-star levels: three stars (icon_star3)
                case "jumps": slot = "task_upgrade"; icon = "bolt"; kit = "icon_speed"; break;                   // jumps: up arrow
                case "combos": slot = "task_flame"; icon = "flame"; kit = "icon_flame"; break;                   // combos: flame
                case "time": slot = "task_hourglass"; icon = "hourglass"; kit = "icon_dial"; break;              // play time: hourglass
                case "coins": slot = "task_coins"; icon = "coinbag"; kit = "icon_coin"; break;                   // coins: coin
                case "powerups": icon = "bolt"; kit = "icon_bolt"; break;                                         // power-ups: bolt
                case "hard": slot = "task_boss_skull"; icon = "helmet"; kit = "icon_bolt"; break;                // hard levels: boss skull
                case "nowall": icon = "wall"; kit = "icon_hand"; break;                                           // no-wall: wall
                case "endless": icon = null; kit = "icon_tower"; break;                                           // endless floors: tower
                case "gold": icon = "medal"; kit = "icon_crown"; break;                                           // gold medal levels: gold medal (TaskIcon draws MedalIcons gold)
            }
        }

        /// <summary>True for the tasks that count gold medals (w_gold5 and the region goals g_*_gold_*): they show the code-drawn gold medal.</summary>
        public static bool IsGoldMedalTask(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (id == "w_gold5" || id == "w_gold10") return true;
            if (!id.StartsWith("g_", StringComparison.Ordinal)) return false;
            string[] parts = id.Split('_');
            return parts.Length > 2 && parts[2] == "gold";
        }

        static Sprite TaskIcon(string id)
        {
            if (IsGoldMedalTask(id)) { Sprite gold = MedalIcons.For(MedalLevel.Gold, true); if (gold != null) return gold; }
            string slot, icon, kit;
            TaskSources(id, out slot, out icon, out kit);
            Sprite s = RewardArt.Slot(slot) ?? RewardArt.Task(id) ?? RewardArt.Icon(icon) ?? ToyKit.Get(kit);
            return s;
        }

        // ---------------- reward plate ----------------

        struct RItem { public Sprite Icon; public string Text; public string HeroId; public bool IsHero; }

        /// <summary>Id of the Common hero of a region (the hero a region goal gives), null when the region has none.</summary>
        static string RegionHeroId(int region)
        {
            foreach (CharacterSpec c in CharacterDefs.All) if (c.RegionStarter && c.Region == region) return c.Id;
            return null;
        }

        List<RItem> RewardItems(RewardEntry e, bool pointsOnly)
        {
            var l = new List<RItem>(4);
            if (pointsOnly) { l.Add(new RItem { Icon = StarSprite, Text = "+" + e.Points }); return l; }
            if (e.Gems > 0) l.Add(new RItem { Icon = GemSprite, Text = Compact(e.Gems) });
            if (e.Coins > 0) l.Add(new RItem { Icon = CoinSprite, Text = Compact(e.Coins) });
            if (e.PowerUps > 0) l.Add(new RItem { Icon = BoltSprite, Text = "x" + e.PowerUps });
            // a hero reward shows the hero's face; the random weekly hero (HeroRegion -1) is not known before the claim and shows the generic heroes icon
            if (e.GivesHero) l.Add(new RItem { IsHero = true, HeroId = !string.IsNullOrEmpty(e.HeroId) ? e.HeroId : e.HeroRegion > 0 ? RegionHeroId(e.HeroRegion) : null, Icon = ToyKit.Get("icon_heroes"), Text = "" });
            // hero-or-coins gift (Daily 200 points, weekly hard-clear tasks): the hero face, then "or" + the coins paid when the 50 percent roll misses
            if (e.IsHeroOrCoins) l.Add(new RItem { Icon = CoinSprite, Text = "or " + Compact(e.HeroOrCoins) });
            return l;
        }

        /// <summary>
        /// A hero face chip: a round cream disc with the top of the hero doll (the same parts the hero reveal draws) cropped by a circle mask.
        /// Without a hero id (the random weekly hero) it shows the generic heroes icon. The caller sets the size (layout element or Place).
        /// </summary>
        RectTransform HeroFace(Transform parent, string name, string heroId, float size)
        {
            RectTransform frame = UiKit.Rect(parent, name);
            frame.sizeDelta = new Vector2(size, size);
            Sprite circle = ToyKit.Get("plate_circle") ?? Theme.RoundSprite;
            Image disc = UiKit.AddImage(frame, "Disc", circle, new Color(0.99f, 0.93f, 0.78f, 1f));
            disc.preserveAspect = false;
            UiKit.Stretch(disc.rectTransform);
            if (!string.IsNullOrEmpty(heroId))
            {
                Image clip = UiKit.AddImage(frame, "Clip", circle, Color.white);
                clip.preserveAspect = false;
                UiKit.Stretch(clip.rectTransform);
                clip.rectTransform.offsetMin = new Vector2(3f, 3f);
                clip.rectTransform.offsetMax = new Vector2(-3f, -3f);
                var mask = clip.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;
                CharacterDef who = CharacterCatalog.Get(heroId);
                RectTransform doll = MomoDoll.Build(clip.transform, "Portrait", size * 2.2f, who, art);
                UiKit.Place(doll, new Vector2(0.5f, 1f), new Vector2(0f, size * 0.12f), doll.sizeDelta);   // the head sits in the top of the doll canvas
                CharacterSpec spec = CharacterInventory.Get(heroId);
                frame.gameObject.AddComponent<A11yLabel>().Text = (spec != null ? spec.Name : "Hero") + " hero";
            }
            else
            {
                Image generic = UiKit.AddImage(frame, "Generic", ToyKit.Get("icon_heroes"), Color.white);
                UiKit.Stretch(generic.rectTransform);
                generic.rectTransform.offsetMin = new Vector2(size * 0.12f, size * 0.12f);
                generic.rectTransform.offsetMax = new Vector2(-size * 0.12f, -size * 0.12f);
                frame.gameObject.AddComponent<A11yLabel>().Text = "Random hero";
            }
            return frame;
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
            for (int i = 0; i < items.Count; i++) w += (items[i].IsHero ? HeroFaceSize : ItemIcon + 6f + AmountW(items[i].Text)) + (i > 0 ? ItemGap : 0f);
            return Mathf.Max(w, claim ? ClaimButtonH : 0f);
        }

        /// <summary>
        /// The reward plate of a card: one row of icon + amount items. Ready cards get the green plate (btn_green) with a check glyph first: it is the Claim button.
        /// </summary>
        Image CardPlate(Transform parent, List<RItem> items, bool claim)
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
                if (it.IsHero)
                {
                    RectTransform face = HeroFace(p.transform, "Hero", it.HeroId, HeroFaceSize);
                    SetPref(face.gameObject, HeroFaceSize, HeroFaceSize);
                    continue;
                }
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

        /// <summary>
        /// The card title: the table title, shortened where the icon already says it (a tower for Endless) so a title stays on one line next to the reward plate and the
        /// compact 136 px card holds ("Reach floor 100 in Endless" becomes "Reach floor 100"). Screen readers get the same short words plus the reward.
        /// </summary>
        public static string ShortTitle(string title)
        {
            string t = title ?? "";
            const string endless = " in Endless", noWall = "Clear the no-wall level ", times = " times", threeStar = "Get 3 stars on ";
            if (t.EndsWith(endless, StringComparison.Ordinal)) t = t.Substring(0, t.Length - endless.Length);
            if (t.StartsWith(noWall, StringComparison.Ordinal) && t.EndsWith(times, StringComparison.Ordinal) && t.Length > noWall.Length + times.Length)
                t = "No-wall level x" + t.Substring(noWall.Length, t.Length - noWall.Length - times.Length);
            if (t.StartsWith(threeStar, StringComparison.Ordinal)) t = "3 stars on " + t.Substring(threeStar.Length);
            const string goldMedals = "Gold medals on ";   // the gold medal icon already says "medals": "Gold on 25 levels" stays on one line next to the plate (owner Bug List 10)
            if (t.StartsWith(goldMedals, StringComparison.Ordinal)) t = "Gold on " + t.Substring(goldMedals.Length);
            return t;
        }

        string TitleOf(RewardEntry e) { return ShortTitle(e.Title); }

        // ---------------- the card ----------------

        /// <summary>
        /// One task card: icon at the left, the title on top, a bar with "n/N" under it and the reward plate at the right (compact, Bug List 4 E4.c).
        /// <paramref name="claimable"/> cards (stars, weekly tasks) are ready when complete and unclaimed: the card turns yellow and its reward plate becomes a BIG GREEN
        /// CLAIM BUTTON (check glyph + the reward, at least 48 dp, "Claim 20 gems" for screen readers) that claims exactly that reward; tapping the rest of the card claims it too
        /// (button and card both claim). Claimed cards turn green with a check badge. Daily action cards only show points (green when done).
        /// Region goals (Main) use the same card; a goal that gives a hero adds the hero's face to the plate. The card height is the compact base or the measured title + bar when that is more.
        /// </summary>
        void AddCard(RectTransform list, RewardEntry e, bool claimable)
        {
            // region goals read the region API (ready needs an open region); daily actions and weekly tasks use the generic one
            RegionGoal goal = e as RegionGoal;
            int value = goal != null ? progress.RegionGoalProgress(goal) : progress.RewardProgress(Tab, e);
            int target = goal != null ? goal.Target : progress.RewardTarget(Tab, e);
            bool claimed, ready;
            if (goal != null) { claimed = progress.IsRegionGoalClaimed(goal); ready = progress.IsRegionGoalReady(goal); }
            else
            {
                claimed = claimable ? progress.IsRewardClaimed(Tab, e) : value >= target;
                ready = claimable && !claimed && value >= target;
            }
            CardState st = claimed ? CardState.Done : (ready ? CardState.Ready : CardState.Open);

            RectTransform row = UiKit.Rect(list, "Row_" + e.Id);
            RectTransform card = UiKit.Rect(row, "Card");
            UiKit.Stretch(card);

            Image bg = Plate(card, "Bg", "plate_cream", StateTint(st), CardCorner);
            UiKit.Stretch(bg.rectTransform);

            List<RItem> items = RewardItems(e, !claimable);
            float plateW = CardPlateWidth(items, ready);
            float midL = CardPadX + IconW + 14f, midR = CardPadX + plateW + 14f;
            float listW = screen != null && screen.rect.width > 200f ? screen.rect.width : 1080f;
            float cardW = listW - 2f * Side;

            Text title = UiKit.AddText(card, "Title", TitleOf(e), BodyPx, Theme.Ink, TextAnchor.MiddleCenter, true);
            UiKit.OnPanel(title);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            float textW = Mathf.Max(1f, cardW - midL - midR - 4f);
            float titleH = Mathf.Max(LineH(BodyPx), MeasureTitle(title, textW));   // same measure for the card height and the title rect
            {
                // Owner follow-up: a title may take TWO lines (the card has room for them). Only a title that needs more than two lines shrinks (down to the 16 sp minimum);
                // one that still wraps beyond two lines at the minimum makes its own card taller, so nothing is ever clipped.
                int minFs = Mathf.CeilToInt(UiKit.MinBodyTextPx);
                while (titleH > 2f * Mathf.Ceil(title.fontSize * 1.3f) && title.fontSize - 2 >= minFs)
                {
                    title.fontSize -= 2;
                    titleH = Mathf.Max(Mathf.Ceil(title.fontSize * 1.22f), MeasureTitle(title, textW));
                }
            }
            float h = CardHeightFor(titleH);
            FixedHeight(row, h);
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
    }
}
