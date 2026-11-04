using System;
using System.Collections.Generic;
using System.Globalization;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>Headers of the Rewards tabs: the Main region strip and purple bonus band, the Daily / Weekly scene with its timer, the points track (chests) and the tasks done bar.</summary>
    public sealed partial class RewardsScreen
    {
        // ---- header metrics ----
        const float ChipW = 150f, ChipGap = 12f;
        static float BandH { get { return 34f + LineH(BodyPx) + 14f + 96f + 18f; } }
        static float TimerH { get { return Mathf.Max(96f, LineH(52) + 26f); } }
        const float ChestSize = 108f;
        static float CellH { get { return 4f + ChestSize + 4f + LineH(BodyPx) + 2f + LineH(BodyPx) + 8f; } }
        const float StarRowH = 84f;
        static float TrackPanelH { get { return 12f + StarRowH + 8f + CellH + 12f; } }
        static float SummaryH { get { return Mathf.Max(96f, BarH + 28f); } }

        // ---------------- scene (Daily and Weekly) ----------------

        /// <summary>Cover-fitted picture behind a header, full width (it grows past the list padding to the screen edges) and cropped to the header height.</summary>
        static void SceneBackdrop(RectTransform parent, Sprite sprite, Color fallback)
        {
            RectTransform clip = UiKit.Rect(parent, "SceneClip");
            clip.anchorMin = Vector2.zero;
            clip.anchorMax = Vector2.one;
            clip.offsetMin = new Vector2(-Side, 0f);
            clip.offsetMax = new Vector2(Side, 0f);
            clip.gameObject.AddComponent<RectMask2D>();
            Image bg = UiKit.AddImage(clip, "Backdrop", sprite, sprite != null ? Color.white : fallback);
            bg.preserveAspect = false;
            UiKit.Stretch(bg.rectTransform);
            if (sprite != null)
            {
                var arf = bg.gameObject.AddComponent<AspectRatioFitter>();
                arf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                arf.aspectRatio = sprite.rect.width / sprite.rect.height;
            }
        }

        void BuildSceneHeader(RectTransform content, bool daily)
        {
            float timerH = TimerH;
            float panelTop = 22f + timerH + 20f;
            float panelH = TrackPanelH;   // Weekly is as tall as Daily so the scene shows (owner, Bug List 3 follow-up); its panel holds the tasks-done bar
            RectTransform scene = UiKit.Rect(content, "Scene");
            FixedHeight(scene, panelTop + panelH + 22f);

            // scene_daily / scene_weekly art when it exists; else the backdrop of the latest unlocked region; else a plain sky colour
            Sprite bgSprite = RewardArt.Scene(daily ? "daily" : "weekly");
            if (bgSprite == null)
            {
                int latest = Mathf.Clamp(progress.HighestUnlockedRegionIndex(), 1, GameSession.PlayableRegions);
                RegionArt ra = art != null ? (art.ForRegion(latest) ?? art.ForRegion(1)) : null;
                bgSprite = ra != null ? ra.BgFar : null;
            }
            SceneBackdrop(scene, bgSprite, new Color(0.25f, 0.42f, 0.70f, 1f));

            // the dark timer pill: hourglass and the time left (an icon instead of "Resets in")
            Image pill = Theme.GlassPanel(scene, "TimerPill", timerH * 0.25f);
            UiKit.Place(pill.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(520f, timerH));
            var hl = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 14f;
            hl.padding = new RectOffset(24, 24, 8, 8);
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            Sprite hg = RewardArt.Slot("task_hourglass") ?? RewardArt.Icon("hourglass") ?? ToyKit.Get("icon_dial");
            Image ic = UiKit.AddImage(pill.transform, "Icon", hg, Color.white);
            SetPref(ic.gameObject, 64f, 64f);
            timerText = UiKit.AddText(pill.transform, "Timer", "", 52, Color.white, TextAnchor.MiddleCenter, true);
            timerText.horizontalOverflow = HorizontalWrapMode.Overflow;
            pill.gameObject.AddComponent<A11yLabel>().Text = daily ? "Time until the daily reset" : "Time until the weekly reset";

            RectTransform panel = UiKit.Rect(scene, "TrackPanel");
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.offsetMin = new Vector2(0f, -(panelTop + panelH));
            panel.offsetMax = new Vector2(0f, -panelTop);
            Image glass = Plate(panel, "Backdrop", null, new Color(0.04f, 0.07f, 0.22f, 0.62f), 36f);   // a dark glass strip so the white numbers read on any scene
            UiKit.Stretch(glass.rectTransform);
            if (daily) BuildDailyTrack(panel);
            else BuildWeeklySummary(panel);
        }

        /// <summary>Weekly: the tasks done bar (each task has its own reward in its card).</summary>
        void BuildWeeklySummary(RectTransform panel)
        {
            IReadOnlyList<RewardEntry> tasks = progress.RewardDefs.WeeklyTasks;
            int done = 0;
            foreach (RewardEntry e in tasks) if (progress.RewardProgress(RewardTab.Weekly, e) >= progress.RewardTarget(RewardTab.Weekly, e)) done++;
            Sprite pic = RewardArt.Slot("task_login_scroll") ?? RewardArt.Icon("scroll") ?? ToyKit.Get("icon_flag");
            Image icon = UiKit.AddImage(panel, "Icon", pic, Color.white);
            UiKit.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(76f, 76f));
            RectTransform bar = Bar(panel, done + "/" + tasks.Count, tasks.Count > 0 ? done / (float)tasks.Count : 0f, BarBlue, BarH);
            bar.anchorMin = new Vector2(0f, 0.5f);
            bar.anchorMax = new Vector2(1f, 0.5f);
            bar.pivot = new Vector2(0.5f, 0.5f);
            bar.offsetMin = new Vector2(110f, -BarH * 0.5f);
            bar.offsetMax = new Vector2(-24f, BarH * 0.5f);
        }

        /// <summary>Daily: points n/100 and a row of milestone chests with the thresholds under them (20 40 60 80 100); a tap on a ready chest claims it.</summary>
        void BuildDailyTrack(RectTransform panel)
        {
            IReadOnlyList<RewardEntry> ms = progress.RewardDefs.DailyMilestones;
            int n = ms.Count;
            if (n == 0) return;
            int value = progress.DailyPoints();
            int max = ms[n - 1].Target;

            Image star = UiKit.AddImage(panel, "Icon", StarSprite, Color.white);
            UiKit.Place(star.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -12f), new Vector2(StarRowH, StarRowH));
            Text vt = UiKit.AddText(panel, "Value", value + "/" + max, 52, Color.white, TextAnchor.MiddleLeft, true);
            vt.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform vrt = vt.rectTransform;
            vrt.anchorMin = new Vector2(0f, 1f);
            vrt.anchorMax = new Vector2(1f, 1f);
            vrt.pivot = new Vector2(0f, 1f);
            vrt.offsetMin = new Vector2(18f + StarRowH + 14f, -(12f + StarRowH));
            vrt.offsetMax = new Vector2(-24f, -12f);

            RectTransform cells = UiKit.Rect(panel, "Cells");
            cells.anchorMin = Vector2.zero;
            cells.anchorMax = Vector2.one;
            cells.offsetMin = new Vector2(12f, 12f);
            cells.offsetMax = new Vector2(-12f, -(12f + StarRowH + 8f));

            // the thin track runs behind the chests at their middle
            float mid = 4f + ChestSize * 0.5f;
            RectTransform track = UiKit.Rect(cells, "Track");
            track.anchorMin = new Vector2(0f, 1f);
            track.anchorMax = new Vector2(1f, 1f);
            track.pivot = new Vector2(0.5f, 1f);
            track.offsetMin = new Vector2(0f, -(mid + 13f));
            track.offsetMax = new Vector2(0f, -(mid - 13f));
            Image tb = Plate(track, "Backdrop", null, TrackDark, 13f);
            UiKit.Stretch(tb.rectTransform);
            float pos = Mathf.Clamp01(TrackPosition(ms, value));
            if (pos > 0.001f)
            {
                Image fi = Plate(track, "Fill", null, BarGold, 10f);
                fi.rectTransform.anchorMin = Vector2.zero;
                fi.rectTransform.anchorMax = new Vector2(pos, 1f);
                fi.rectTransform.offsetMin = new Vector2(3f, 3f);
                fi.rectTransform.offsetMax = new Vector2(-3f, -3f);
            }
            for (int i = 0; i < n; i++) BuildTrackCell(cells, ms[i], i, n);
        }

        /// <summary>Position (0..1 of the bar) of a value: cell centres are at (k + 0.5) / n, the start is 0.</summary>
        public static float TrackPosition(IReadOnlyList<RewardEntry> ms, int value)
        {
            int n = ms.Count;
            if (value <= 0) return 0f;
            for (int k = 0; k < n; k++)
            {
                if (value > ms[k].Target) continue;
                int prev = k == 0 ? 0 : ms[k - 1].Target;
                float start = k == 0 ? 0f : (k - 0.5f) / n;
                float end = (k + 0.5f) / n;
                return start + (end - start) * ((value - prev) / (float)Math.Max(1, ms[k].Target - prev));
            }
            return (n - 0.5f) / n;
        }

        void BuildTrackCell(RectTransform cells, RewardEntry e, int index, int n)
        {
            RectTransform cell = UiKit.Rect(cells, "Cell" + e.Target);
            cell.anchorMin = new Vector2(index / (float)n, 0f);
            cell.anchorMax = new Vector2((index + 1) / (float)n, 1f);
            cell.offsetMin = new Vector2(6f, 0f);    // a gap between neighbouring touch targets
            cell.offsetMax = new Vector2(-6f, 0f);
            bool claimed = progress.IsRewardClaimed(RewardTab.Daily, e);
            bool reached = progress.RewardProgress(RewardTab.Daily, e) >= progress.RewardTarget(RewardTab.Daily, e);
            bool ready = reached && !claimed;
            if (ready)
            {
                // a ready chest sits on a yellow plate and is a real button (shape and colour)
                Image back = Plate(cell, "Backdrop", "plate_cream", new Color(1f, 0.88f, 0.18f, 1f), 30f);
                UiKit.Stretch(back.rectTransform);
                MakeClaimable(cell, back, RewardTab.Daily, e, e.Target + " points: claim " + Describe(e));
            }

            Color dim = new Color(0.66f, 0.70f, 0.82f, 1f);
            string kind = RewardArt.ChestKind(index, n);
            Sprite chestSprite = RewardArt.Chest(kind, claimed);
            Image chest = UiKit.AddImage(cell, "Chest", chestSprite ?? ToyKit.Get("icon_chest"), reached ? Color.white : dim);
            UiKit.Place(chest.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(ChestSize, ChestSize));
            if (claimed && chestSprite == null)
            {
                // no open-chest art yet: a check on the chest says "opened"
                Image tick = CheckBadge(chest.transform, "Claimed", 48f);
                UiKit.Place(tick.rectTransform, new Vector2(1f, 0f), new Vector2(6f, -4f), new Vector2(48f, 48f));
            }
            bool extra = (e.Gems > 0 ? 1 : 0) + (e.Coins > 0 ? 1 : 0) + (e.PowerUps > 0 ? 1 : 0) > 1;
            if (extra)
            {
                Image bolt = UiKit.AddImage(chest.transform, "Extra", e.PowerUps > 0 ? BoltSprite : CoinSprite, Color.white);
                UiKit.Place(bolt.rectTransform, new Vector2(0f, 0f), new Vector2(-6f, -4f), new Vector2(40f, 40f));
            }

            float lh = LineH(BodyPx);
            Color numColor = ready ? Theme.Ink : (reached ? Theme.Gold : Theme.OnGlassSoft);
            Text t = UiKit.AddText(cell, "Target", e.Target.ToString(CultureInfo.InvariantCulture), BodyPx, numColor, TextAnchor.UpperCenter, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (ready) UiKit.OnPanel(t);
            RectTransform trt = t.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(0f, -(4f + ChestSize + 4f + lh));
            trt.offsetMax = new Vector2(0f, -(4f + ChestSize + 4f));

            // the reward under the threshold: its picture and amount
            Sprite pic; string count;
            PrimaryReward(e, out pic, out count);
            RectTransform rr = UiKit.Rect(cell, "Reward");
            rr.anchorMin = new Vector2(0f, 1f);
            rr.anchorMax = new Vector2(1f, 1f);
            rr.pivot = new Vector2(0.5f, 1f);
            rr.offsetMin = new Vector2(0f, -(4f + ChestSize + 4f + lh + 2f + lh));
            rr.offsetMax = new Vector2(0f, -(4f + ChestSize + 4f + lh + 2f));
            var hl = rr.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 4f;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            Image ri = UiKit.AddImage(rr, "Icon", pic, Color.white);
            SetPref(ri.gameObject, 40f, 40f);
            Text ct = UiKit.AddText(rr, "Count", count, BodyPx, ready ? Theme.Ink : Color.white, TextAnchor.MiddleCenter, true);
            ct.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (ready) UiKit.OnPanel(ct);
        }

        void PrimaryReward(RewardEntry e, out Sprite pic, out string count)
        {
            if (e.Gems > 0) { pic = GemSprite; count = Compact(e.Gems); }
            else if (e.Coins > 0) { pic = CoinSprite; count = Compact(e.Coins); }
            else { pic = BoltSprite; count = "x" + e.PowerUps; }
        }

        // ---------------- Main header: region circles and the purple bonus band ----------------

        void BuildMainHeader(RectTransform content)
        {
            RectTransform header = UiKit.Rect(content, "MainHeader");
            FixedHeight(header, CircleZone + BandH);

            int regions = GameSession.PlayableRegions;
            for (int i = 1; i <= regions; i++) BuildCircle(header, repo != null ? repo.TryGetRegionByIndex(i) : null, i, regions);
            BuildBand(header);
        }

        // the region circles are kept exactly as they were (owner answer: the strip is okay)
        void BuildCircle(RectTransform header, RegionDef region, int index, int count)
        {
            if (region == null) return;
            bool selected = index == selectedRegion;
            bool unlocked = progress.IsRegionUnlocked(region) && progress.IsRegionPlayable(region);
            int cleared = progress.RegionLevelsCleared(index);
            bool done = cleared >= region.Levels.Count;

            RectTransform slot = UiKit.Rect(header, "Slot" + index);
            slot.anchorMin = new Vector2((index - 1) / (float)count, 1f);
            slot.anchorMax = new Vector2(index / (float)count, 1f);
            slot.pivot = new Vector2(0.5f, 1f);
            slot.offsetMin = new Vector2(2f, -CircleZone);
            slot.offsetMax = new Vector2(-2f, 0f);

            float size = selected ? 190f : 150f;
            string slug = region.Id.Length > 11 ? region.Id.Substring(11) : region.Id;
            Sprite pin = Resources.Load<Sprite>("Art/Pins/pin_" + slug);
            Color tint = unlocked ? Color.white : new Color(0.55f, 0.58f, 0.68f, 1f);
            Button b = UiKit.AddButton(slot, "Region" + index, "", pin != null ? pin : Theme.RoundSprite, pin != null ? tint : new Color(0.35f, 0.55f, 0.85f, 1f), new Vector2(size, size), () => SelectRegion(index), 40);
            b.GetComponent<A11yLabel>().Text = region.Name + (done ? ", cleared" : (unlocked ? ", " + cleared + " of " + region.Levels.Count + " levels" : ", locked")) + (selected ? ", selected" : "");
            b.GetComponent<Image>().preserveAspect = true;
            RectTransform brt = (RectTransform)b.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.anchoredPosition = new Vector2(0f, selected ? -14f : -64f);   // the chosen region is bigger and raised

            if (done || !unlocked)
            {
                Sprite st = done ? (RewardArt.Icon("check") ?? Glyphs.Check) : (RewardArt.Icon("lock") ?? ToyKit.Get("icon_lock"));
                Image mark = UiKit.AddImage(brt, "State", st, Color.white);
                UiKit.Place(mark.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.55f, size * 0.55f));
            }

            Text name = UiKit.AddText(slot, "Name", region.Name, Mathf.RoundToInt(Mathf.Max(36f, UiKit.MinBodyTextPx)), Color.white, TextAnchor.UpperCenter, true);
            Head(name);
            // Never break inside a word (1080x2520 showed "Mushroo / m Grove"): break only at the spaces and let a word that is a little
            // wider than its slot spill into the free space beside it. The size is already at the 16 sp floor, so it cannot shrink further.
            name.text = region.Name.Replace(' ', '\n');
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform nrt = name.rectTransform;
            nrt.anchorMin = new Vector2(0f, 1f);
            nrt.anchorMax = new Vector2(1f, 1f);
            nrt.pivot = new Vector2(0.5f, 1f);
            // fixed name zone below the biggest circle: the raised/bigger selected circle cannot push it into the band
            name.verticalOverflow = VerticalWrapMode.Truncate;
            nrt.offsetMin = new Vector2(0f, -(CircleBottom + NameGap + NameZone));
            nrt.offsetMax = new Vector2(0f, -(CircleBottom + NameGap));
        }

        /// <summary>
        /// The purple bonus band under the circles (full width, with a pointer under the chosen circle): the scroll icon, a thin bar with n/N in it
        /// and the reward chips (gems, coins) at the right. When ready the title becomes a green CLAIM tag and the whole band is the claim button.
        /// </summary>
        void BuildBand(RectTransform header)
        {
            RewardEntry e = null;
            foreach (RewardEntry r in progress.RewardDefs.RegionRewards) if (r.Target == selectedRegion) e = r;
            RegionDef region = repo != null ? repo.TryGetRegionByIndex(selectedRegion) : null;
            if (e == null || region == null) return;
            int value = progress.RewardProgress(RewardTab.Main, e), target = progress.RewardTarget(RewardTab.Main, e);
            bool claimed = progress.IsRewardClaimed(RewardTab.Main, e);
            bool ready = !claimed && value >= target;
            float bandH = BandH;

            RectTransform band = UiKit.Rect(header, "Band");
            band.anchorMin = new Vector2(0f, 1f);
            band.anchorMax = new Vector2(1f, 1f);
            band.pivot = new Vector2(0.5f, 1f);
            band.offsetMin = new Vector2(-Side, -(CircleZone + bandH));   // edge to edge: past the list padding
            band.offsetMax = new Vector2(Side, -CircleZone);
            Image bg = Plate(band, "Bg", null, PurpleBand, 6f);
            UiKit.Stretch(bg.rectTransform);
            Image edge = Plate(band, "Backdrop", null, PurpleEdge, 3f);
            edge.rectTransform.anchorMin = new Vector2(0f, 0f);
            edge.rectTransform.anchorMax = new Vector2(1f, 0f);
            edge.rectTransform.pivot = new Vector2(0.5f, 0f);
            edge.rectTransform.offsetMin = Vector2.zero;
            edge.rectTransform.offsetMax = new Vector2(0f, 6f);
            if (ready) MakeClaimable(band, bg, RewardTab.Main, e, region.Name + " bonus: claim " + Describe(e));
            else band.gameObject.AddComponent<A11yLabel>().Text = region.Name + " bonus: " + Mathf.Min(value, target) + " of " + target + " levels cleared" + (claimed ? ", claimed" : "");

            // pointer under the chosen circle
            RectTransform ph = UiKit.Rect(band, "PointerHost");
            ph.anchorMin = Vector2.zero;
            ph.anchorMax = Vector2.one;
            ph.offsetMin = new Vector2(Side, 0f);
            ph.offsetMax = new Vector2(-Side, 0f);
            Image pointer = Plate(ph, "Pointer", null, PurpleBand, 2f);
            UiKit.Place(pointer.rectTransform, new Vector2((selectedRegion - 0.5f) / GameSession.PlayableRegions, 1f), Vector2.zero, new Vector2(40f, 40f));
            pointer.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            pointer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            RectTransform inner = UiKit.Rect(band, "Inner");
            inner.anchorMin = Vector2.zero;
            inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(Side + 16f, 14f);
            inner.offsetMax = new Vector2(-(Side + 16f), -34f);

            // the reward chips at the right
            List<RItem> items = RewardItems(e, false);
            int nc = Mathf.Max(1, items.Count);
            float chipsW = nc * ChipW + (nc - 1) * ChipGap;
            RectTransform chips = UiKit.Rect(inner, "Chips");
            chips.anchorMin = new Vector2(1f, 0f);
            chips.anchorMax = new Vector2(1f, 1f);
            chips.pivot = new Vector2(1f, 0.5f);
            chips.offsetMin = new Vector2(-chipsW, 0f);
            chips.offsetMax = Vector2.zero;
            var chl = chips.gameObject.AddComponent<HorizontalLayoutGroup>();
            chl.spacing = ChipGap;
            chl.childControlWidth = chl.childControlHeight = true;
            chl.childForceExpandWidth = false;
            chl.childForceExpandHeight = true;
            foreach (RItem it in items)
            {
                var one = new List<RItem>(1) { it };
                RectTransform chip = RewardPlate(chips, "Chip", one);
                var le = chip.gameObject.AddComponent<LayoutElement>();
                le.minWidth = le.preferredWidth = ChipW;
                le.flexibleWidth = 0f;
            }

            RectTransform left = UiKit.Rect(inner, "Left");
            left.anchorMin = Vector2.zero;
            left.anchorMax = Vector2.one;
            left.offsetMin = Vector2.zero;
            left.offsetMax = new Vector2(-(chipsW + 20f), 0f);
            float lh = LineH(BodyPx);
            if (ready)
            {
                Image tag = Plate(left, "Tag", "btn_green", Color.white, 28f);
                UiKit.Place(tag.rectTransform, new Vector2(0f, 1f), Vector2.zero, new Vector2(TagW, lh + 6f));
                Text tt = UiKit.AddText(tag.transform, "Label", "Claim", BodyPx, UiKit.ButtonInk, TextAnchor.MiddleCenter, true);
                UiKit.OnPanel(tt).horizontalOverflow = HorizontalWrapMode.Overflow;
                UiKit.Stretch(tt.rectTransform);
            }
            else
            {
                Text title = UiKit.AddText(left, "Title", "Clear all levels", BodyPx, Color.white, TextAnchor.MiddleLeft, true);
                title.horizontalOverflow = HorizontalWrapMode.Overflow;
                RectTransform trt = title.rectTransform;
                trt.anchorMin = new Vector2(0f, 1f);
                trt.anchorMax = new Vector2(1f, 1f);
                trt.pivot = new Vector2(0f, 1f);
                trt.offsetMin = new Vector2(0f, -lh);
                trt.offsetMax = Vector2.zero;
            }

            RectTransform row2 = UiKit.Rect(left, "ProgressRow");
            row2.anchorMin = new Vector2(0f, 0f);
            row2.anchorMax = new Vector2(1f, 0f);
            row2.pivot = new Vector2(0.5f, 0f);
            row2.offsetMin = Vector2.zero;
            row2.offsetMax = new Vector2(0f, 96f);
            Sprite pic = RewardArt.Slot("task_login_scroll") ?? RewardArt.Icon("scroll") ?? ToyKit.Get("icon_flag");
            Image icon = UiKit.AddImage(row2, "Icon", pic, Color.white);
            UiKit.Place(icon.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(84f, 84f));
            RectTransform bar = Bar(row2, Mathf.Min(value, target) + "/" + target, target > 0 ? Mathf.Clamp01(value / (float)target) : 0f, claimed || ready ? BarGreen : BarBlue, BarH);
            bar.anchorMin = new Vector2(0f, 0.5f);
            bar.anchorMax = new Vector2(1f, 0.5f);
            bar.pivot = new Vector2(0.5f, 0.5f);
            bar.offsetMin = new Vector2(100f, -BarH * 0.5f);
            bar.offsetMax = new Vector2(0f, BarH * 0.5f);
        }
    }
}
