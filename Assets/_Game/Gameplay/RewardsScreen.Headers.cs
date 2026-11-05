using System;
using System.Collections.Generic;
using System.Globalization;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>Headers of the Rewards tabs: the Main region strip (30 circles, goals of the selected region), the Daily / Weekly scene with its timer, the points track (chests) and the tasks done bar.</summary>
    public sealed partial class RewardsScreen
    {
        // ---- header metrics ----
        static float TimerH { get { return Mathf.Max(96f, LineH(52) + 26f); } }
        const float ChestSize = 108f;
        static float CellH { get { return 4f + ChestSize + 4f + LineH(BodyPx) + 2f + LineH(BodyPx) + 8f; } }
        const float StarRowH = 84f;
        static float TrackPanelH { get { return 12f + StarRowH + 8f + CellH + 12f; } }
        static float SummaryH { get { return Mathf.Max(96f, BarH + 28f); } }

        // ---------------- scene (Daily and Weekly) ----------------

        /// <summary>Cover-fitted picture behind a header, full width (it grows past the list padding to the screen edges) and cropped to the header height.</summary>
        static void SceneBackdrop(RectTransform parent, Sprite sprite, Color fallback, float bleed = Side)
        {
            RectTransform clip = UiKit.Rect(parent, "SceneClip");
            clip.anchorMin = Vector2.zero;
            clip.anchorMax = Vector2.one;
            clip.offsetMin = new Vector2(-bleed, 0f);
            clip.offsetMax = new Vector2(bleed, 0f);
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

        static readonly Color SceneFallback = new Color(0.25f, 0.42f, 0.70f, 1f);

        /// <summary>The scene picture of a header (scene_daily / scene_weekly); else the backdrop of the latest unlocked region; null = the plain sky colour.</summary>
        Sprite SceneSprite(string kind)
        {
            Sprite bgSprite = RewardArt.Scene(kind);
            if (bgSprite == null)
            {
                int latest = Mathf.Clamp(progress.HighestUnlockedRegionIndex(), 1, GameSession.PlayableRegions);
                RegionArt ra = art != null ? (art.ForRegion(latest) ?? art.ForRegion(1)) : null;
                bgSprite = ra != null ? ra.BgFar : null;
            }
            return bgSprite;
        }

        void BuildSceneHeader(RectTransform content, bool daily)
        {
            float timerH = TimerH;
            float panelTop = 22f + timerH + 20f;
            float panelH = TrackPanelH;   // Weekly is as tall as Daily so the scene shows (owner, Bug List 3 follow-up); its panel holds the tasks-done bar
            RectTransform scene = UiKit.Rect(content, "Scene");
            FixedHeight(scene, panelTop + panelH + 22f);

            // scene_daily / scene_weekly art when it exists; else the backdrop of the latest unlocked region; else a plain sky colour
            SceneBackdrop(scene, SceneSprite(daily ? "daily" : "weekly"), SceneFallback);

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

        // ---------------- Main: the pinned region strip ----------------

        /// <summary>Width of one region slot in the strip (five fit across the 1080 reference).</summary>
        public const float StripSlotW = 216f;
        const float SmallCircle = 150f, BigCircle = 190f;
        static readonly Color RingGold = new Color(1f, 0.82f, 0.2f, 1f);

        sealed class StripSlot
        {
            public RectTransform Slot, Btn, MarkRt;
            public Image Mark;
            public string Name;
            public int Levels, Cleared;
            public bool Soon, Open, Done, Dot;
        }

        StripSlot[] stripSlots;
        RectTransform stripRow, ringRt, pointerRt;

        /// <summary>The strip's row: exactly 30 children (Slot1 ... Slot30), built once per screen build and reused (selection only restyles them).</summary>
        public RectTransform StripRow { get { return stripRow; } }

        /// <summary>
        /// The pinned Main header: the scene picture (scene_daily, like Daily), a dark glass strip for the names and ONE horizontally scrolling strip of all 30 regions.
        /// Regions 1-5 show their pin; 6-30 are grey with a lock and "Coming Soon". The selected circle is big, ringed, has a pointer under its name and is centred;
        /// a red dot marks a region with something to claim. The strip snaps to the nearest circle when the finger lifts; a tap selects.
        /// </summary>
        void BuildMainHeader(RectTransform parent)
        {
            float top = WalletTop + WalletH + 12f;
            RectTransform header = UiKit.Rect(parent, "MainHeader");
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.offsetMin = new Vector2(0f, -(top + CircleZone));
            header.offsetMax = new Vector2(0f, -top);
            mainHeader = header;

            RectTransform scene = UiKit.Rect(header, "Scene");
            UiKit.Stretch(scene);
            SceneBackdrop(scene, SceneSprite("daily"), SceneFallback, 0f);
            // a dark glass strip behind the names so the white names read on any scene
            Image names = Plate(header, "Backdrop", null, new Color(0.04f, 0.07f, 0.22f, 0.70f), 6f);
            RectTransform nrt0 = names.rectTransform;
            nrt0.anchorMin = new Vector2(0f, 1f);
            nrt0.anchorMax = new Vector2(1f, 1f);
            nrt0.pivot = new Vector2(0.5f, 1f);
            nrt0.offsetMin = new Vector2(0f, -(CircleBottom + NameGap + NameZone + 6f));
            nrt0.offsetMax = new Vector2(0f, -(CircleBottom + NameGap - 6f));

            int total = RegionGoalTable.TotalRegions;
            RectTransform vp = UiKit.Rect(header, "Strip");
            UiKit.Stretch(vp);
            vp.gameObject.AddComponent<RectMask2D>();
            Image hit = vp.gameObject.AddComponent<Image>();   // the drag also starts on the gaps between circles
            hit.color = new Color(0f, 0f, 0f, 0.001f);
            RectTransform content = UiKit.Rect(vp, "Content");
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            RectTransform row = UiKit.Rect(content, "Row");
            row.anchorMin = new Vector2(0f, 0f);
            row.anchorMax = new Vector2(0f, 1f);
            row.pivot = new Vector2(0f, 0.5f);
            row.sizeDelta = new Vector2(total * StripSlotW, 0f);
            stripRow = row;

            var sr = vp.gameObject.AddComponent<ScrollRect>();
            sr.viewport = vp;
            sr.content = content;
            sr.horizontal = true;
            sr.vertical = false;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 20f;
            var snap = vp.gameObject.AddComponent<RegionStripSnap>();
            snap.Scroll = sr;
            snap.Content = content;
            snap.Row = row;
            snap.Viewport = vp;
            snap.SlotW = StripSlotW;
            snap.Count = total;
            snap.FallbackWidth = screen != null && screen.rect.width > 200f ? screen.rect.width : 1080f;
            stripSnap = snap;

            stripSlots = new StripSlot[total];
            for (int i = 1; i <= total; i++) stripSlots[i - 1] = BuildStripSlot(row, i);

            // one ring and one pointer, moved to the selected slot (the ring is drawn behind the circle)
            Transform sel = stripSlots[selectedRegion - 1].Slot;
            Image ring = UiKit.AddImage(sel, "Ring", Glyphs.Ring, RingGold);
            ring.preserveAspect = true;
            ringRt = ring.rectTransform;
            UiKit.Place(ringRt, new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(BigCircle + 24f, BigCircle + 24f));
            Image pointer = Plate(sel, "Pointer", null, RingGold, 2f);
            pointerRt = pointer.rectTransform;
            UiKit.Place(pointerRt, new Vector2(0.5f, 1f), new Vector2(0f, -(CircleZone - 34f)), new Vector2(24f, 24f));   // a small gold diamond just above the list, inside the header (centre 22 px above its edge)
            pointerRt.localRotation = Quaternion.Euler(0f, 0f, 45f);

            for (int i = 0; i < total; i++) ApplySlot(i);
            ApplyStripSelection();
            snap.CenterOn(selectedRegion - 1, false);
        }

        StripSlot BuildStripSlot(RectTransform row, int index)
        {
            bool soon = progress.IsRegionComingSoon(index);
            RegionDef region = !soon && repo != null ? repo.TryGetRegionByIndex(index) : null;
            bool playable = region != null;
            bool open = playable && progress.IsRegionGoalsOpen(index);
            int cleared = playable ? progress.RegionLevelsCleared(index) : 0;
            bool done = playable && region.Levels.Count > 0 && cleared >= region.Levels.Count;
            var s = new StripSlot { Soon = soon || !playable, Open = open, Done = done, Cleared = cleared, Levels = playable ? region.Levels.Count : 0 };

            RectTransform slot = UiKit.Rect(row, "Slot" + index);
            slot.anchorMin = new Vector2(0f, 0f);
            slot.anchorMax = new Vector2(0f, 1f);
            slot.pivot = new Vector2(0.5f, 0.5f);
            slot.offsetMin = new Vector2((index - 1) * StripSlotW + 2f, 0f);
            slot.offsetMax = new Vector2(index * StripSlotW - 2f, 0f);
            s.Slot = slot;

            Sprite circle = ToyKit.Get("plate_circle") ?? Theme.RoundSprite;
            Sprite sprite = circle;
            Color tint = new Color(0.40f, 0.43f, 0.55f, 1f);   // Coming Soon: grey
            if (playable)
            {
                string slug = region.Id.Length > 11 ? region.Id.Substring(11) : region.Id;
                Sprite pin = Resources.Load<Sprite>("Art/Pins/pin_" + slug);
                sprite = pin != null ? pin : circle;
                tint = pin == null ? new Color(0.35f, 0.55f, 0.85f, 1f) : (open ? Color.white : new Color(0.55f, 0.58f, 0.68f, 1f));
            }
            int idx = index;
            Button b = UiKit.AddButton(slot, "Region" + index, "", sprite, tint, new Vector2(SmallCircle, SmallCircle), () => SelectRegion(idx), 40);
            b.GetComponent<Image>().preserveAspect = true;
            s.Btn = (RectTransform)b.transform;
            s.Btn.anchorMin = s.Btn.anchorMax = new Vector2(0.5f, 1f);
            s.Btn.pivot = new Vector2(0.5f, 1f);

            if (done || !open)
            {
                Sprite st = done ? (RewardArt.Icon("check") ?? Glyphs.Check) : (RewardArt.Icon("lock") ?? ToyKit.Get("icon_lock"));
                s.Mark = UiKit.AddImage(s.Btn, "State", st, Color.white);
                s.MarkRt = s.Mark.rectTransform;
            }
            s.Dot = playable && progress.HasClaimableInRegion(index);
            if (s.Dot) ToyKit.AddBadge(s.Btn);   // red dot: something to claim in this region

            s.Name = s.Soon ? "Coming\nSoon" : region.Name.Replace(' ', '\n');
            Text name = UiKit.AddText(slot, "Name", s.Name, Mathf.RoundToInt(Mathf.Max(36f, UiKit.MinBodyTextPx)), s.Soon ? new Color(0.84f, 0.87f, 0.96f, 1f) : Color.white, TextAnchor.UpperCenter, true);
            Head(name);
            // Never break inside a word (1080x2520 showed "Mushroo / m Grove"): the lines are split by hand at the spaces and a word that is a little
            // wider than its slot spills into the free space beside it. The size is already at the 16 sp floor, so it cannot shrink further.
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform nrt = name.rectTransform;
            nrt.anchorMin = new Vector2(0f, 1f);
            nrt.anchorMax = new Vector2(1f, 1f);
            nrt.pivot = new Vector2(0.5f, 1f);
            nrt.offsetMin = new Vector2(0f, -(CircleBottom + NameGap + NameZone));   // fixed name zone below the biggest circle
            nrt.offsetMax = new Vector2(0f, -(CircleBottom + NameGap));
            return s;
        }

        string SlotLabel(int index, StripSlot s)
        {
            string what;
            if (s.Soon) what = "Region " + index + ", Coming Soon";
            else
            {
                RegionDef r = repo != null ? repo.TryGetRegionByIndex(index) : null;
                string nm = r != null ? r.Name : "Region " + index;
                what = nm + (s.Done ? ", cleared" : (s.Open ? ", " + s.Cleared + " of " + s.Levels + " levels" : ", locked"));
            }
            return what + (s.Dot ? ", something to claim" : "") + (index == selectedRegion ? ", selected" : "");
        }

        /// <summary>Restyles one slot (selected = big and raised, others small).</summary>
        void ApplySlot(int slotIndex)
        {
            StripSlot s = stripSlots[slotIndex];
            bool sel = slotIndex + 1 == selectedRegion;
            float size = sel ? BigCircle : SmallCircle;
            s.Btn.sizeDelta = new Vector2(size, size);
            s.Btn.anchoredPosition = new Vector2(0f, sel ? -14f : -64f);   // the chosen region is bigger and raised
            if (s.MarkRt != null) UiKit.Place(s.MarkRt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.55f, size * 0.55f));
            A11yLabel a = s.Btn.GetComponent<A11yLabel>();
            if (a != null) a.Text = SlotLabel(slotIndex + 1, s);
        }

        /// <summary>The selected region changed: restyle the slots and move the ring and the pointer (no rebuild, no allocation beyond the label strings).</summary>
        void ApplyStripSelection()
        {
            if (stripSlots == null) return;
            for (int i = 0; i < stripSlots.Length; i++) ApplySlot(i);
            Transform slot = stripSlots[selectedRegion - 1].Slot;
            if (ringRt != null) { ringRt.SetParent(slot, false); ringRt.SetAsFirstSibling(); }
            if (pointerRt != null) pointerRt.SetParent(slot, false);
        }

        // ---------------- Main: the goal list of the selected region ----------------

        void BuildMainGoals(RectTransform content)
        {
            if (progress.IsRegionComingSoon(selectedRegion)) { BuildComingSoon(content); return; }
            if (!progress.IsRegionGoalsOpen(selectedRegion)) BuildLockedNote(content);
            IReadOnlyList<RegionGoal> goals = progress.RegionGoals(selectedRegion);
            bool divider = false;
            for (int i = 0; i < goals.Count; i++)
            {
                RegionGoal g = goals[i];
                if (g.IsEndless && !divider) { BuildEndlessDivider(content); divider = true; }   // the endless tiers come last, under their own heading
                AddCard(content, g, true);
            }
        }

        /// <summary>One centred card for a region without goals: a lock and "Coming Soon", nothing to claim.</summary>
        void BuildComingSoon(RectTransform content)
        {
            RectTransform row = UiKit.Rect(content, "ComingSoon");
            float h = Mathf.Max(340f, 70f + 140f + 16f + LineH(60) * 2f + 40f);
            FixedHeight(row, h);
            Image bg = Plate(row, "Panel", "plate_cream", StateTint(CardState.Open), CardCorner);
            UiKit.Stretch(bg.rectTransform);
            Sprite lockSprite = RewardArt.Icon("lock") ?? ToyKit.Get("icon_lock");
            Image icon = UiKit.AddImage(row, "Lock", lockSprite, Color.white);
            UiKit.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(140f, 140f));
            Text t = UiKit.AddText(row, "Label", "Coming Soon", 60, Theme.Ink, TextAnchor.MiddleCenter, true);
            UiKit.OnPanel(t);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            RectTransform trt = t.rectTransform;
            trt.anchorMin = new Vector2(0f, 0f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.offsetMin = new Vector2(30f, 20f);
            trt.offsetMax = new Vector2(-30f, -(40f + 140f + 16f));
            row.gameObject.AddComponent<A11yLabel>().Text = "Region " + selectedRegion + " is coming soon";
        }

        /// <summary>A short note on top of a region that is not open yet (the goals below start counting once it is unlocked).</summary>
        void BuildLockedNote(RectTransform content)
        {
            RectTransform row = UiKit.Rect(content, "LockedNote");
            Image bg = Plate(row, "Panel", "plate_chip", Color.white, RewardCorner);
            UiKit.Stretch(bg.rectTransform);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(24, 24, 12, 12);
            hl.spacing = 16f;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 96f;
            Image ic = UiKit.AddImage(row, "Lock", RewardArt.Icon("lock") ?? ToyKit.Get("icon_lock"), Color.white);
            SetPref(ic.gameObject, 64f, 64f);
            Text t = UiKit.AddText(row, "Text", "Unlock this region to earn", BodyPx, Color.white, TextAnchor.MiddleCenter, true);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            row.gameObject.AddComponent<A11yLabel>().Text = "This region is locked. Unlock it to earn these rewards.";
        }

        /// <summary>The small "Endless" heading (tower icon between two lines) above the endless tiers.</summary>
        void BuildEndlessDivider(RectTransform content)
        {
            RectTransform row = UiKit.Rect(content, "EndlessDivider");
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(8, 8, 6, 6);
            hl.spacing = 16f;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.minHeight = LineH(BodyPx) + 12f;
            MakeLine(row);
            Image ic = UiKit.AddImage(row, "Icon", ToyKit.Get("icon_tower"), Color.white);
            SetPref(ic.gameObject, 56f, 56f);
            Text t = UiKit.AddText(row, "Label", "Endless", BodyPx, Color.white, TextAnchor.MiddleCenter, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            MakeLine(row);
            row.gameObject.AddComponent<A11yLabel>().Text = "Endless goals";
        }

        static void MakeLine(RectTransform row)
        {
            Image line = Plate(row, "Line", null, new Color(0.66f, 0.72f, 0.95f, 0.8f), 3f);
            var le = line.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 20f;
            le.flexibleWidth = 1f;
            le.minHeight = le.preferredHeight = 6f;
            le.flexibleHeight = 0f;
        }
    }

    /// <summary>
    /// Snap and centring of the Main region strip (30 equal slots). The content has half a viewport of padding at both ends, so EVERY slot can be centred: centring slot i
    /// is content x = -i * slot width, whatever the screen width. When the finger lifts the strip glides (frame-rate independent) to the nearest slot, with a little
    /// carry for a flick. The glide is stepped by the screen's ticker, so a resting strip costs nothing; no allocation anywhere.
    /// </summary>
    public sealed class RegionStripSnap : MonoBehaviour, UnityEngine.EventSystems.IBeginDragHandler, UnityEngine.EventSystems.IEndDragHandler
    {
        public ScrollRect Scroll;
        public RectTransform Content, Row, Viewport;
        public float SlotW = 216f;
        public int Count = 30;
        public float FallbackWidth = 1080f;

        bool dirty = true, dragging;
        float target = float.NaN;
        int centered;

        float ViewW { get { float w = Viewport != null ? Viewport.rect.width : 0f; return w > 10f ? w : FallbackWidth; } }

        /// <summary>The slot index (0-based) nearest to the viewport centre right now.</summary>
        public int NearestIndex { get { return Mathf.Clamp(Mathf.RoundToInt(-Content.anchoredPosition.x / SlotW), 0, Count - 1); } }

        /// <summary>Content x that centres a slot.</summary>
        public float XFor(int index) { return -Mathf.Clamp(index, 0, Count - 1) * SlotW; }

        /// <summary>Sizes the content and the row for the current viewport width.</summary>
        public void Fit()
        {
            float vw = ViewW;
            float pad = Mathf.Max(0f, (vw - SlotW) * 0.5f);
            Content.sizeDelta = new Vector2((Count - 1) * SlotW + vw, 0f);
            Row.anchoredPosition = new Vector2(pad, 0f);
        }

        /// <summary>Centres a slot, gliding or at once (reduced motion and the first build jump).</summary>
        public void CenterOn(int index, bool animate)
        {
            centered = Mathf.Clamp(index, 0, Count - 1);
            Fit();
            float x = XFor(centered);
            if (!animate || GameSettings.ReducedMotion)
            {
                if (Scroll != null) Scroll.StopMovement();
                Content.anchoredPosition = new Vector2(x, 0f);
                target = float.NaN;
            }
            else target = x;
        }

        public void OnBeginDrag(UnityEngine.EventSystems.PointerEventData e) { dragging = true; target = float.NaN; }

        public void OnEndDrag(UnityEngine.EventSystems.PointerEventData e)
        {
            dragging = false;
            float v = Scroll != null ? Scroll.velocity.x : 0f;
            int idx = Mathf.Clamp(Mathf.RoundToInt((-Content.anchoredPosition.x - v * 0.16f) / SlotW), 0, Count - 1);
            centered = idx;
            target = XFor(idx);
        }

        void OnRectTransformDimensionsChange() { dirty = true; }

        /// <summary>Called once per frame by the Rewards ticker.</summary>
        public void Step(float dt)
        {
            if (dirty)
            {
                dirty = false;
                Fit();
                if (!dragging && float.IsNaN(target)) Content.anchoredPosition = new Vector2(XFor(centered), 0f);   // a new screen size: keep the centred slot centred
            }
            if (dragging || float.IsNaN(target)) return;
            if (Scroll != null) Scroll.StopMovement();   // the glide replaces the scroll rect's own inertia
            float x = Content.anchoredPosition.x;
            x = Mathf.Lerp(x, target, 1f - Mathf.Exp(-14f * dt));   // time based: the same glide at any frame rate (CUX-1)
            if (Mathf.Abs(x - target) < 0.5f) { x = target; target = float.NaN; }
            Content.anchoredPosition = new Vector2(x, 0f);
        }
    }
}
