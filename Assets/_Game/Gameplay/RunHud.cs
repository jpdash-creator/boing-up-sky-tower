using System;
using FluffyClimbers.Economy;
using FluffyClimbers.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// Run screen layout (CUX-3: the HUD and the pads never cover the player, the next platforms or a hazard). The screen is split into a TOP strip (wallet row, gauge and stars)
    /// reserved as room only (nothing is drawn). There is NO bottom strip and no red line (owner decision 2026-11-02): the play field runs to the real bottom edge of the screen,
    /// which is the danger edge of the simulation, and the control pads float over it. RunView sizes its camera from the top reserve alone. All numbers are reference px
    /// (1080 x 1920 canvas, match 0.5); derived from the aspect alone, so view and HUD agree. Pure functions, no allocation.
    /// </summary>
    public static class RunLayout
    {
        /// <summary>Row 1 (pills and pause) height: at least 96 and the real 48 dp touch size of this device.</summary>
        public static float RowH { get { return Mathf.Max(96f, UiKit.MinTouchRef); } }
        /// <summary>Row 2 (gauge and stars) height.</summary>
        public const float Row2H = 76f;
        /// <summary>Row 3 (timer, best pace, live delta; doc 46 section 5) height.</summary>
        public const float Row3H = 80f;
        public const float ChipH = 84f;
        /// <summary>Space of the bottom strip above the safe-area bottom: 20 + jump pad (230) + 20.</summary>
        public const float BottomPadRef = ControlPad.PadBottom + ControlPad.JumpSize;
        /// <summary>The visible play height (world units, 10 wide) never drops below this: the player can be 10 above the danger line.</summary>
        public const float MinPlayUnits = 11.5f;
        public static readonly Color StripColor = new Color(0.07f, 0.09f, 0.17f, 0f);   // transparent: the strips only reserve room, no header or footer bar is drawn (owner 2026-11-02)

        /// <summary>Test hook: forces the aspect (width / height); 0 = the main camera or the screen.</summary>
        public static float AspectOverride;

        public static float AspectNow()
        {
            if (AspectOverride > 0f) return AspectOverride;
            if (RunView.AspectOverride > 0f) return RunView.AspectOverride;
            Camera c = Camera.main;
            if (c != null && c.aspect > 0.1f) return c.aspect;
            return Mathf.Max(1f, Screen.width) / Mathf.Max(1f, Screen.height);
        }

        /// <summary>Canvas width in reference px for an aspect (CanvasScaler match 0.5: scale = sqrt(w/1080 * h/1920)).</summary>
        public static float CanvasW(float aspect) { return 1440f * Mathf.Sqrt(Mathf.Max(0.3f, aspect)); }

        /// <summary>The strips in reference px: safe-area inset (notch, gesture bar) plus the HUD rows; shrunk (both) when the play field would get lower than <see cref="MinPlayUnits"/> (wide 3:4 screens).</summary>
        public static void StripsRef(float aspect, float worldWidth, out float top, out float bottom)
        {
            float cw = CanvasW(aspect), ch = cw / Mathf.Max(0.3f, aspect);
            Rect sa = UiKit.SafeAreaNormalized();
            top = Mathf.Max(0f, 1f - sa.yMax) * ch + 16f + RowH + 12f + Row2H + 12f + Row3H + 12f;
            bottom = 0f;   // owner decision 2026-11-02: no bottom strip and no red line; the play field runs to the real screen bottom, the pads float over it
            float unitsPerRef = worldWidth / cw;
            float room = ch * unitsPerRef - MinPlayUnits;
            float sum = top * unitsPerRef;
            if (sum > room && sum > 0f)
            {
                float f = Mathf.Max(0f, room) / sum;
                top *= f;
            }
        }

        /// <summary>The strips in world units (the camera uses these).</summary>
        public static void StripsWorld(float aspect, float worldWidth, out float top, out float bottom)
        {
            StripsRef(aspect, worldWidth, out top, out bottom);
            float k = worldWidth / CanvasW(aspect);
            top *= k;
            bottom *= k;
        }
    }

    /// <summary>
    /// Code-built HUD and overlays (intro, pause, failed, results) for the Run scene. Icon-first (docs/16 sections G and I): dark pills with a
    /// painted icon and a number, a slim tower gauge instead of floor and goal sentences, icon banners for combo and speed-up, and round icon
    /// buttons with a one-word caption in the popups.
    /// </summary>
    public sealed partial class RunHud : MonoBehaviour
    {
        public Action OnPause, OnResume, OnRetry, OnNext, OnMenu, OnRevive, OnReviveAd;
        /// <summary>A power-up button was pressed (the PowerUp type id). The run controller latches it into the next tick's input.</summary>
        public Action<int> OnPower;
        /// <summary>The close mark of the current overlay (pause: resume, end screens: back to the level select).</summary>
        Action OnClose;

        ArtCatalog art;
        Canvas canvas;
        Text floorText, goalText, gemText, comboText, timeText, introText, speedText;
        RectTransform comboRoot, speedRoot, introRoot, gaugeMarker;
        float gaugeW;
        RectTransform topStripRt;
        Camera aspectCam;
        float lastAspect, worldWidth;
        RectTransform gaugeTrack;
        float speedCueTimer;
        int lastSpeedStep;
        RectTransform overlay;
        float comboPulse;
        int lastCombo;
        float introTimer;

        public bool OverlayOpen { get { return overlay != null; } }

        bool mirror;
        bool endless;         // endless mode: Floor N / Best N, no goal text and no stars
        int bestBefore;       // best floor of the region when the run started
        public ControlPad Pad { get; private set; }
        /// <summary>The power buttons in two side groups (docs/15 FR-PWR-3; left = Flight, Rocket, Double Jump, right = Magnet, Shield, Slow Time, Wall Stick), bought AND free hero powers merged; empty when the player owns no power-up and the hero has no power.</summary>
        public PowerUpBar Powers { get; private set; }

        /// <summary>Places an element, mirrored horizontally for the left-handed layout.</summary>
        void P(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            if (mirror && !Mathf.Approximately(anchor.x, 0.5f)) { anchor.x = 1f - anchor.x; pos.x = -pos.x; }
            UiKit.Place(rt, anchor, pos, size);
        }

        TextAnchor Align(TextAnchor a)
        {
            if (!mirror) return a;
            switch (a)
            {
                case TextAnchor.UpperLeft: return TextAnchor.UpperRight;
                case TextAnchor.UpperRight: return TextAnchor.UpperLeft;
                case TextAnchor.MiddleLeft: return TextAnchor.MiddleRight;
                case TextAnchor.MiddleRight: return TextAnchor.MiddleLeft;
                default: return a;
            }
        }

        /// <summary>A dark pill with an icon at the left and a number after it, placed with <see cref="P"/>. Returns the number text.</summary>
        Text Pill(RectTransform root, string name, Sprite icon, Vector2 anchor, Vector2 pos, Vector2 size, Color number, bool inLayoutRow = false)
        {
            Image pill = Theme.GlassPanel(root, name, size.y * 0.25f);
            if (!inLayoutRow) P(pill.rectTransform, anchor, pos, size);
            Image ic = UiKit.AddImage(pill.transform, "Icon", icon, Color.white);
            ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(mirror ? 1f : 0f, 0.5f);
            ic.rectTransform.pivot = new Vector2(mirror ? 1f : 0f, 0.5f);
            ic.rectTransform.anchoredPosition = new Vector2((mirror ? -1f : 1f) * size.y * 0.2f, 0f);
            ic.rectTransform.sizeDelta = new Vector2(size.y * 0.72f, size.y * 0.72f);
            Text t = UiKit.AddText(pill.transform, "Value", "", 46, number, mirror ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var trt = (RectTransform)t.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(mirror ? size.y * 0.25f : size.y * 0.95f, 0f);
            trt.offsetMax = new Vector2(mirror ? -size.y * 0.95f : -size.y * 0.25f, 0f);
            return t;
        }

        public void Build(RunSession session, ArtCatalog catalog)
        {
            mirror = GameSettings.LeftHanded;
            art = catalog;
            canvas = UiKit.CreateCanvas("RunHud", 10);
            canvas.transform.SetParent(transform, false);

            RectTransform root = UiKit.SafeArea(canvas.transform);
            endless = session.IsEndless;
            bestBefore = endless && GameSession.Progress != null ? (session.IsTimeAttack ? GameSession.Progress.TimeAttackBestFloors(session.Region.Id, session.Mode) : GameSession.Progress.EndlessBestFloor(session.Region.Id)) : 0;

            // CUX-3 layout: the play field is the screen between two reserved strips (RunLayout). The camera (RunView) shows the world between them, so no platform
            // is ever drawn under the top HUD or under the control pads; the top strip only reserves room. The physical screen bottom IS the danger edge (no bottom strip, no line).
            float aspect = RunLayout.AspectNow();
            float topRef, bottomRef;   // bottomRef is always 0 now
            RunLayout.StripsRef(aspect, session.Cfg.ScreenWidth.F(), out topRef, out bottomRef);
            lastAspect = aspect;
            aspectCam = Camera.main;
            Image topStrip = UiKit.AddImage(canvas.transform, "TopStrip", null, RunLayout.StripColor);
            topStrip.raycastTarget = false;
            topStrip.rectTransform.anchorMin = new Vector2(0f, 1f);
            topStrip.rectTransform.anchorMax = new Vector2(1f, 1f);
            topStrip.rectTransform.pivot = new Vector2(0.5f, 1f);
            topStrip.rectTransform.anchoredPosition = Vector2.zero;
            topStrip.rectTransform.sizeDelta = new Vector2(0f, topRef);
            topStripRt = topStrip.rectTransform;
            topStrip.transform.SetAsFirstSibling();
            worldWidth = session.Cfg.ScreenWidth.F();
            // no bottom strip and no danger line is drawn (owner 2026-11-02): the physical screen bottom is the danger edge

            float rowH = RunLayout.RowH;
            float pauseS = rowH;

            // Row 1: floor, goal, gems share the width left of the round pause button (a layout row: they fit every aspect and 130 percent text).
            RectTransform pillRow = UiKit.Rect(root, "HudPills");
            pillRow.anchorMin = new Vector2(0f, 1f);
            pillRow.anchorMax = new Vector2(1f, 1f);
            pillRow.pivot = new Vector2(0.5f, 1f);
            float padL = 18f, padR = 18f + pauseS + 14f;   // the pause button sits at the right edge (the left-handed layout mirrors it)
            pillRow.offsetMin = new Vector2(mirror ? padR : padL, -(16f + rowH));
            pillRow.offsetMax = new Vector2(mirror ? -padL : -padR, -16f);
            var hl = pillRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 14f;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = true;
            hl.reverseArrangement = mirror;
            floorText = Pill(pillRow, "FloorPill", ToyKit.Get("icon_tower"), Vector2.zero, Vector2.zero, new Vector2(0f, rowH), Color.white, true);
            goalText = Pill(pillRow, "GoalPill", ToyKit.Get("icon_flag"), Vector2.zero, Vector2.zero, new Vector2(0f, rowH), new Color(1f, 0.95f, 0.6f), true);
            UiKit.SetText(goalText, endless ? bestBefore.ToString() : session.Spec.TargetRow().ToString());
            Text gems = Pill(pillRow, "GemPill", art != null ? art.GemIcon : null, Vector2.zero, Vector2.zero, new Vector2(0f, rowH), new Color(0.75f, 0.92f, 1f), true);
            gemText = gems;
            if (endless) { gems.transform.parent.gameObject.SetActive(false); }   // endless has no gems or stars to collect
            Button pause = ToyKit.GlyphButton(root, "Pause", "Pause", 2, Glyphs.Pause, new Vector2(pauseS, pauseS), () => { if (OnPause != null) OnPause(); });
            pause.gameObject.AddComponent<CanvasGroup>().alpha = ControlPad.ButtonAlpha;
            P((RectTransform)pause.transform, new Vector2(1f, 1f), new Vector2(-18f, -16f), new Vector2(pauseS, pauseS));

            // Row 2: the tower gauge as a slim horizontal bar (the hero's height between the ground and the flag) and the 3 level stars at the right end.
            float row2Y = -(16f + rowH + 12f);
            const float starS = 64f, starGap = 6f, trackH = 52f;
            float starsW = endless ? 0f : 3f * starS + 2f * starGap;
            float gaugeRight = 18f + (endless ? 0f : starsW + 20f);
            Image gauge = Theme.GlassPanel(root, "Gauge", trackH * 0.5f);
            RectTransform grt = gauge.rectTransform;
            grt.anchorMin = new Vector2(0f, 1f);
            grt.anchorMax = new Vector2(1f, 1f);
            grt.pivot = new Vector2(0.5f, 1f);
            float gy = row2Y - (RunLayout.Row2H - trackH) * 0.5f;
            grt.offsetMin = new Vector2(mirror ? gaugeRight : 18f, gy - trackH);
            grt.offsetMax = new Vector2(mirror ? -18f : -gaugeRight, gy);
            gaugeTrack = grt;
            gaugeW = 400f;
            Image marker = UiKit.AddImage(gauge.transform, "Marker", Theme.RoundSprite, new Color(1f, 0.82f, 0.3f, 1f));
            marker.type = Image.Type.Sliced;
            marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            marker.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            marker.rectTransform.sizeDelta = new Vector2(40f, 40f);
            marker.rectTransform.anchoredPosition = new Vector2(mirror ? 370f : 30f, 0f);
            Image mring = UiKit.AddImage(marker.transform, "Ring", Theme.RingSprite, Color.white);
            mring.type = Image.Type.Sliced;
            mring.preserveAspect = false;
            UiKit.Stretch(mring.rectTransform);
            gaugeMarker = marker.rectTransform;
            gauge.gameObject.SetActive(!endless);
            starIcons = new Image[3];
            for (int i = 0; i < 3 && !endless; i++)
            {
                starIcons[i] = UiKit.AddImage(root, "Star" + i, art != null ? art.StarFilled : null, new Color(0.35f, 0.35f, 0.4f, 0.9f));
                P(starIcons[i].rectTransform, new Vector2(1f, 1f), new Vector2(-18f - (2 - i) * (starS + starGap), row2Y - (RunLayout.Row2H - starS) * 0.5f), new Vector2(starS, starS));
            }

            // Combo and speed-up are transient banners in the CENTRE of the screen (owner 2026-11-02): horizontally centred, combo just above and speed-up just below the vertical middle.
            // The centre is clear of the top HUD rows, the pads and the power-up buttons (those sit at the sides / bottom); anchors are symmetric, so left-handed mirroring needs no change.
            // The speedrun clock stays between the pads in the bottom strip.
            float leftSpan = ControlPad.MoveSpan + 20f, rightSpan = ControlPad.JumpSpan + 20f;
            if (mirror) { float t = leftSpan; leftSpan = rightSpan; rightSpan = t; }
            comboRoot = BannerRoot(root, "ComboBanner", "icon_flame", new Color(1f, 0.85f, 0.2f), BannerComboY, out comboText);
            speedRoot = BannerRoot(root, "SpeedBanner", "icon_speed", new Color(1f, 0.7f, 0.4f), BannerSpeedY, out speedText);
            comboRoot.gameObject.SetActive(false);
            speedRoot.gameObject.SetActive(false);

            timeText = UiKit.AddText(root, "Time", "", 40, Color.white, TextAnchor.MiddleCenter);
            RectTransform trt0 = (RectTransform)timeText.transform;
            trt0.anchorMin = new Vector2(0f, 0f);
            trt0.anchorMax = new Vector2(1f, 0f);
            trt0.pivot = new Vector2(0.5f, 0f);
            float ty = 20f + 2f * (RunLayout.ChipH + 10f);
            trt0.offsetMin = new Vector2(leftSpan, ty);
            trt0.offsetMax = new Vector2(-rightSpan, ty + 56f);
            timeText.horizontalOverflow = HorizontalWrapMode.Overflow;

            BuildTiming(root, session, row2Y);   // doc 46: timer, best pace and live delta in the third strip row, plus the optional Pace Ghost
            BuildIntro(root, session);
            Pad = ControlPad.Build(root, art, mirror);
            Powers = new PowerUpBar();
            Powers.OnPress = slot => { if (OnPower != null) OnPower(slot); };
            Powers.Build(root, session, P);
            Powers.SetVisible(false);   // shown when the intro text is gone (free hero powers are merged into these buttons, Bug List 6)
            // The three pad buttons are the last children of the HUD: nothing built above (banners, intro, strips, bars) can sit on top of them or swallow a touch (owner 2026-11-07).
            // Pause and result pop-ups are created later, so they still cover the pads on purpose.
            Pad.Left.transform.SetAsLastSibling();
            Pad.Right.transform.SetAsLastSibling();
            Pad.Jump.transform.SetAsLastSibling();
            introTimer = 3.2f;
        }

        /// <summary>Banner size and centre offsets (reference px from the middle of the safe area). Public so tests can assert the centre placement.</summary>
        /// <summary>Offset of the intro popup centre from the middle of the safe area (0 = centred vertically).</summary>
        public const float IntroCenterY = 0f;

        public const float BannerW = 380f, BannerH = 112f, BannerComboY = 70f, BannerSpeedY = -70f;

        /// <summary>An icon + text pill (combo, speed up) in the centre of the screen: anchored to the middle, offset vertically by <paramref name="y"/>. Returns its root; the text is the out parameter.</summary>
        RectTransform BannerRoot(RectTransform root, string name, string icon, Color number, float y, out Text text)
        {
            float h = BannerH;
            Image pill = Theme.GlassPanel(root, name, 24f);
            RectTransform prt = pill.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = new Vector2(0f, y);
            prt.sizeDelta = new Vector2(BannerW, h);
            Image ic = UiKit.AddImage(pill.transform, "Icon", ToyKit.Get(icon), Color.white);
            ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            ic.rectTransform.pivot = new Vector2(0f, 0.5f);
            ic.rectTransform.anchoredPosition = new Vector2(22f, 0f);
            ic.rectTransform.sizeDelta = new Vector2(84f, 84f);
            text = UiKit.AddText(pill.transform, "Value", "", 64, number, TextAnchor.MiddleCenter, true);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.resizeTextForBestFit = true;   // "+100%" must stay inside the pill at 130% text
            text.resizeTextMinSize = 40;
            text.resizeTextMaxSize = 64;
            var trt = (RectTransform)text.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(120f, 0f);
            trt.offsetMax = new Vector2(-24f, 0f);
            return pill.rectTransform;
        }

        /// <summary>The level intro: region and level, the goal as flag + target, and the three controls as icons (no sentences).</summary>
        void BuildIntro(RectTransform root, RunSession session)
        {
            Image back = Theme.GlassPanel(root, "Intro", 40f);
            // Centred vertically in the safe area (Bug List 8 item 2c; it used to sit 130 above the middle): the HUD root IS the safe area, so anchor y 0 is its middle.
            UiKit.Place(back.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, IntroCenterY), new Vector2(780f, 410f));
            back.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            introRoot = back.rectTransform;
            string introTitle = session.IsTimeAttack ? "Time Attack" : endless ? "Endless" : "Level " + session.Level.Index;
            Text region = UiKit.AddText(introRoot, "Region", session.Region.Name, 48, new Color(0.85f, 0.93f, 1f), TextAnchor.MiddleCenter, true);
            UiKit.Place((RectTransform)region.transform, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(720f, 70f));
            introText = UiKit.AddText(introRoot, "Level", introTitle, session.IsTimeAttack ? 72 : 88, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.Place((RectTransform)introText.transform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(720f, 120f));
            // Goal: flag icon and the target floor.
            Image flag = UiKit.AddImage(introRoot, "GoalIcon", ToyKit.Get("icon_flag"), Color.white);
            UiKit.Place(flag.rectTransform, new Vector2(0.5f, 1f), new Vector2(-110f, -250f), new Vector2(96f, 96f));
            flag.rectTransform.pivot = new Vector2(0.5f, 1f);
            Text goal = UiKit.AddText(introRoot, "Goal", endless ? "" : session.Spec.TargetRow().ToString(), 72, new Color(1f, 0.95f, 0.6f), TextAnchor.MiddleLeft, true);
            UiKit.Place((RectTransform)goal.transform, new Vector2(0.5f, 1f), new Vector2(100f, -246f), new Vector2(260f, 100f));
            goal.rectTransform.pivot = new Vector2(0.5f, 1f);
            flag.gameObject.SetActive(!endless);
            string rule = endless ? null : RunSession.GoalRule(session.Spec);
            if (rule != null)
            {
                // Level rule shown before the run starts (e.g. "Don't touch the walls."), small italic, centred under the goal.
                Text ruleText = UiKit.AddText(introRoot, "Rule", rule, 40, new Color(1f, 0.95f, 0.6f), TextAnchor.MiddleCenter, true);
                ruleText.fontStyle = FontStyle.Italic;
                UiKit.Place((RectTransform)ruleText.transform, new Vector2(0.5f, 1f), new Vector2(0f, -360f), new Vector2(720f, 70f));
                back.rectTransform.sizeDelta = new Vector2(780f, 490f);   // room for the rule line under the goal
            }
            // The small control-hint chips (left, right, jump) were removed on purpose (owner 2026-11-02): the intro shows the level number, the target and the rule only.
        }

        Image[] starIcons;
        int lastRow = -1, lastStarMask = -1, lastGems = -1, lastTimeKey = -1, lastCombo2 = -1, lastBest = -1;

        public void Refresh(RunSession s, float deltaTime)
        {
            SimState st = s.State;
            // the screen shape changed (rotation, split screen, tests): size the strips again, exactly as RunView does for its camera (no work otherwise)
            float asp = RunView.AspectOverride > 0f ? RunView.AspectOverride : (aspectCam != null ? aspectCam.aspect : lastAspect);
            if (asp > 0.1f && Mathf.Abs(asp - lastAspect) > 0.0005f)
            {
                lastAspect = asp;
                float tr, br;
                RunLayout.StripsRef(asp, worldWidth, out tr, out br);
                topStripRt.sizeDelta = new Vector2(0f, tr);
            }
            if (Powers != null && Powers.ButtonCount > 0)
            {
                if (introTimer <= 0f && !Powers.Visible) Powers.SetVisible(true);
                Powers.Refresh(in st, s.Cfg);
            }
            // Strings are rebuilt only when a value changes: no per-frame allocations (CUX-1).
            if (st.Row != lastRow)
            {
                lastRow = st.Row;
                UiKit.SetText(floorText, st.Row.ToString());
                if (endless)
                {
                    int best = st.Row > bestBefore ? st.Row : bestBefore;   // the best floor follows the climb once it is beaten
                    if (best != lastBest) { lastBest = best; UiKit.SetText(goalText, best.ToString()); }
                }
                else if (gaugeMarker != null)
                {
                    float f = Mathf.Clamp01(st.Row / (float)Mathf.Max(1, s.Spec.TargetRow()));
                    if (gaugeTrack != null && gaugeTrack.rect.width > 60f) gaugeW = gaugeTrack.rect.width;   // the real width once laid out
                    gaugeMarker.anchoredPosition = new Vector2(30f + (mirror ? 1f - f : f) * (gaugeW - 60f), 0f);
                }
            }
            RefreshTiming(s, st.Row);
            if (st.StarMask != lastStarMask)
            {
                lastStarMask = st.StarMask;
                for (int i = 0; i < 3; i++) if (starIcons[i] != null) starIcons[i].color = (st.StarMask & (1 << i)) != 0 ? Color.white : new Color(0.35f, 0.35f, 0.4f, 0.9f);
            }
            int gems = st.Gems + st.HiddenGems + (endless || GameSession.Progress == null ? 0 : GameSession.Progress.Gems);
            if (gems != lastGems) { lastGems = gems; UiKit.SetText(gemText, gems.ToString()); }
            if (s.Spec.Goal.Type == GoalType.Speedrun)
            {
                int sec = st.Tick / 60;
                if (sec != lastTimeKey) { lastTimeKey = sec; UiKit.SetText(timeText, sec + " / " + s.Spec.Goal.TimeSec + " s"); }
            }

            if (st.Combo != lastCombo)
            {
                if (st.Combo > 1) comboPulse = 1f;
                if (st.Combo > lastCombo && st.Combo > 1) AudioManager.Play(Sfx.Combo);
                lastCombo = st.Combo;
            }
            comboPulse = Mathf.Max(0f, comboPulse - deltaTime * 3f);
            if (st.Combo != lastCombo2)
            {
                lastCombo2 = st.Combo;
                UiKit.SetText(comboText, st.Combo > 1 ? "x" + st.Combo : "");
                comboRoot.gameObject.SetActive(st.Combo > 1);
            }
            if (!GameSettings.ReducedMotion) comboRoot.localScale = Vector3.one * (1f + comboPulse * 0.06f);

            // "Speed up" cue each time the screen speeds up (every ScrollStepSeconds): icon plus percent, a static cue with reduced motion.
            if (st.StartTick >= 0)
            {
                // The percent comes from the simulation itself: (current danger-line speed / starting speed - 1), so screen and game always match.
                int step = s.Sim.ScrollSpeedUpPercent(in st);
                if (step > lastSpeedStep)
                {
                    lastSpeedStep = step;
                    UiKit.SetText(speedText, "+" + step + "%");
                    speedRoot.gameObject.SetActive(true);
                    speedCueTimer = 1.6f;
                    AudioManager.Play(Sfx.SpeedUp);
                }
            }
            if (speedCueTimer > 0f)
            {
                speedCueTimer -= deltaTime;
                if (!GameSettings.ReducedMotion) speedRoot.localScale = Vector3.one * (1f + Mathf.Clamp01(speedCueTimer - 1.2f) * 0.1f);
                if (speedCueTimer <= 0f) speedRoot.gameObject.SetActive(false);
            }

            if (introTimer > 0f)
            {
                introTimer -= deltaTime;
                if (introTimer <= 0f) { introRoot.gameObject.SetActive(false); if (Powers != null) Powers.SetVisible(true); }
            }
        }

        public float IntroRemaining { get { return introTimer; } }

        /// <summary>Ends the level intro immediately (used by tests and could back a tap-to-skip).</summary>
        public void SkipIntro()
        {
            introTimer = 0f;
            if (introRoot != null) introRoot.gameObject.SetActive(false);
        }

        // ---------------- overlays ----------------

        void OpenOverlay(string title, float height = 1000f, bool closeX = true)
        {
            CloseOverlay();
            overlay = UiKit.Rect(canvas.transform, "Overlay");
            UiKit.Stretch(overlay);
            var dim = overlay.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.62f);
            UiKit.BuildPopupFrame(overlay, title, art, new Vector2(860f, height), closeX ? (Action)(() => { if (OnClose != null) OnClose(); }) : null);
        }

        // Bottom action row geometry (shared by every overlay): caption height follows the real text size (130 percent text), buttons are at least 48 dp on this device.
        static float CaptionH { get { return Mathf.Ceil(UiKit.MinBodyTextPx * UiKit.TextScale * 1.3f) + 8f; } }
        const float RowBottom = 24f, RowGap = 12f;
        static float ButtonY { get { return RowBottom + CaptionH + RowGap; } }
        static float ButtonSizeFor(float size) { return Mathf.Max(size, UiKit.MinTouchRef); }
        /// <summary>Height of a popup whose content is only the title and one row of icon buttons: no empty middle.</summary>
        static float TitleAndRowHeight(float buttonSize) { return 182f + 36f + ButtonY + ButtonSizeFor(buttonSize); }

        public void CloseOverlay()
        {
            if (shopRoot != null) CloseShop();
            if (overlay != null) Destroy(overlay.gameObject);
            overlay = null;
        }

        Transform PanelOf() { return overlay.Find("Panel"); }

        struct Act
        {
            public string Caption, A11y;
            public int Role;
            public Sprite Icon;
            public float Size;
            public Action Do;
            public bool Enabled;
        }

        static Act MakeAct(string caption, string a11y, int role, Sprite icon, float size, Action act, bool enabled = true)
        {
            return new Act { Caption = caption, A11y = a11y, Role = role, Icon = icon, Size = size, Do = act, Enabled = enabled };
        }

        /// <summary>Round icon buttons with a one-word caption under each, in a row at the bottom of the popup (the primary one is bigger).</summary>
        void OverlayRow(params Act[] acts)
        {
            Transform panel = PanelOf();
            float gap = acts.Length > 3 ? 28f : 44f;
            float total = gap * (acts.Length - 1);
            for (int i = 0; i < acts.Length; i++) { acts[i].Size = ButtonSizeFor(acts[i].Size); total += acts[i].Size; }
            if (total > 800f && acts.Length > 1)
            {
                float shrunk = Mathf.Max(8f, gap - (total - 800f) / (acts.Length - 1));   // big touch targets on a dense screen: tighten the gaps to stay inside the 860 px panel
                total -= (gap - shrunk) * (acts.Length - 1);
                gap = shrunk;
            }
            float x = -total * 0.5f;
            float capH = CaptionH;
            for (int i = 0; i < acts.Length; i++)
            {
                Act a = acts[i];
                Button b = ToyKit.IconButton(panel, a.A11y, a.A11y, a.Role, a.Icon, new Vector2(a.Size, a.Size), a.Do, 0.6f);
                b.interactable = a.Enabled;
                UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(x + a.Size * 0.5f, ButtonY), new Vector2(a.Size, a.Size));
                Text cap = UiKit.AddText(panel, a.A11y + "Caption", a.Caption, 42, Theme.Ink, TextAnchor.MiddleCenter, true);
                var o = cap.GetComponent<Outline>();
                if (o != null) o.enabled = false;
                cap.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiKit.Place((RectTransform)cap.transform, new Vector2(0.5f, 0f), new Vector2(x + a.Size * 0.5f, RowBottom), new Vector2(a.Size + gap, capH));
                x += a.Size + gap;
            }
        }

        public void ShowPause()
        {
            // Sized to its content (title + one row of icon buttons with captions, no empty middle) and with ONE way to resume: the Resume button
            // (no extra X). Buttons are at least 48 dp on this device.
            OpenOverlay("Paused", TitleAndRowHeight(150f), false);
            OnClose = () => { if (OnResume != null) OnResume(); };
            // Four equal round buttons (owner issue 15.3): resume, restart, shop (opens over the paused run) and home.
            OverlayRow(
                MakeAct("Resume", "Resume", 1, ToyKit.Get("icon_play"), 150f, () => { if (OnResume != null) OnResume(); }),
                MakeAct("Restart", "Restart", 0, Glyphs.Restart, 150f, () => { if (OnRetry != null) OnRetry(); }),
                MakeAct("Shop", "Shop", 0, ToyKit.Get("icon_shop"), 150f, OpenShop),
                MakeAct("Home", "Level select", 2, ToyKit.Get("icon_home"), 150f, () => { if (OnMenu != null) OnMenu(); }));
        }

        // ---------------- shop over the paused run ----------------

        RectTransform shopRoot, shopPopup;
        ShopScreen shop;

        /// <summary>The full Shop on top of the paused run; Back returns to the pause menu and the run continues where it was.</summary>
        void OpenShop() { OpenShopTab(ShopScreen.TabPowerups); }

        void OpenShopTab(int tab)
        {
            if (overlay == null || shopRoot != null || GameSession.Progress == null || GameSession.Repo == null) return;
            overlay.gameObject.SetActive(false);
            shopRoot = UiKit.Rect(canvas.transform, "Overlay");
            UiKit.Stretch(shopRoot);
            Theme.SkyBackground(shopRoot);
            RectTransform safe = UiKit.SafeArea(shopRoot);
            var host = new ShopScreen.Host
            {
                OpenPopup = (title, body, height) =>
                {
                    ClosePopupInShop();
                    shopPopup = UiKit.Rect(shopRoot, "Popup");
                    UiKit.Stretch(shopPopup);
                    var dim = shopPopup.gameObject.AddComponent<Image>();
                    dim.color = new Color(0f, 0f, 0f, 0.65f);
                    RectTransform panel = UiKit.BuildPopupFrame(shopPopup, title, art, new Vector2(860f, height), ClosePopupInShop);
                    Text b = UiKit.AddText(panel, "Body", body, 44, Color.white);
                    UiKit.OnPanel(b);
                    UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(740f, 520f));
                },
                PopupButton = (label, primary, y, action) => ToyKit.PopupAction(shopPopup.Find("Panel"), label, ToyKit.Button(primary ? 1 : 0), Color.white, y, action),
                ClosePopup = ClosePopupInShop,
                NewOverlay = () =>
                {
                    ClosePopupInShop();
                    shopPopup = UiKit.Rect(shopRoot, "Popup");
                    UiKit.Stretch(shopPopup);
                    var dim = shopPopup.gameObject.AddComponent<Image>();
                    dim.color = new Color(0f, 0f, 0f, 0.65f);
                    return shopPopup;
                },
                CloseOverlay = ClosePopupInShop,
            };
            GameSession.Progress.LaunchPromoActive = GameSettings.LaunchPromo;
            shop = new ShopScreen(safe, GameSession.Progress, ShopCatalog.FromRepository(GameSession.Repo), art, host);
            shop.Build(tab, tab == ShopScreen.TabCharacter ? ShopScreen.PowerupsSection : ShopScreen.AutoSection);   // deep link "Get power-ups": the Character page scrolled to its Power-ups banner
            Button back = ToyKit.GlyphButton(safe, "Back", "Back to pause", 2, Glyphs.Left, new Vector2(150f, 150f), CloseShop);
            UiKit.Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(150f, 150f));
            GameSession.Progress.Changed += RefreshShopWallet;
        }

        void RefreshShopWallet()
        {
            if (shop == null || GameSession.Progress == null) return;
            UiKit.SetText(shop.CoinText, GameSession.Progress.Coins.ToString());
            UiKit.SetText(shop.GemText, GameSession.Progress.Gems.ToString());
        }

        void ClosePopupInShop()
        {
            if (shopPopup != null) Destroy(shopPopup.gameObject);
            shopPopup = null;
        }

        void CloseShop()
        {
            if (GameSession.Progress != null) GameSession.Progress.Changed -= RefreshShopWallet;
            ClosePopupInShop();
            if (shopRoot != null) Destroy(shopRoot.gameObject);
            shopRoot = null;
            shop = null;
            if (overlay != null) overlay.gameObject.SetActive(true);
        }

        /// <summary>A thin progress bar (dark track, green fill) at <paramref name="y"/> below the top of the panel; <paramref name="fraction"/> 0..1.</summary>
        void ProgressBar(Transform panel, float y, float fraction)
        {
            Image track = Theme.GlassPanel(panel, "Progress", 20f);
            track.rectTransform.anchorMin = new Vector2(0f, 1f);
            track.rectTransform.anchorMax = new Vector2(1f, 1f);
            track.rectTransform.pivot = new Vector2(0.5f, 1f);
            track.rectTransform.anchoredPosition = new Vector2(0f, -y);
            track.rectTransform.sizeDelta = new Vector2(-140f, 50f);
            Image fill = ToyKit.Sliced(track.transform, "Fill", ToyKit.Button(1), 18f);
            fill.rectTransform.anchorMin = new Vector2(0f, 0f);
            fill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.07f, Mathf.Clamp01(fraction)), 1f);
            fill.rectTransform.offsetMin = new Vector2(5f, 5f);
            fill.rectTransform.offsetMax = new Vector2(-5f, -5f);
        }

        /// <summary>
        /// Run failed. ONE line with [Watch Ad] (always while the run has an ad revive left; dimmed when no ad is loaded) next to [gem icon + 5 or 10]; schedule in <see cref="RevivePolicy"/>.
        /// Retry, Shop and Home stay one tap away (the revive never blocks the retry). Every revive button carries an icon and a text (ad length or price).
        /// </summary>
        public void ShowFailed(RunSession s, int gems, bool adReady)
        {
            // The Watch Ad button is ALWAYS shown while the schedule still has an ad revive (owner 2026-11-10); without a loaded ad it is dimmed and a tap explains why.
            adHint = null;
            bool offerAd = s.AdReviveOffered;
            bool adDim = offerAd && !adReady && !s.AdRewardPending;
            bool offerGems = s.CanRevive;
            float hintH = adDim ? AdHintH + 8f : 0f;   // reserved line above the revive row for the "Ad not ready" message (hidden until the player taps)
            // Compact popup (owner 2026-11-02): title, result pills, progress bar, then ONE line with the ad and gem buttons next to each other, then the action row.
            // The height follows the real content: the action row grows with big touch targets / 130 percent text, the revive line with the touch size.
            float contentBottom = s.GoalLost ? 505f : 395f;   // from the top of the panel: end of the progress bar (or of the reason line)
            float reviveH = ReviveButtonH;
            float actionTop = ButtonY + ButtonSizeFor(150f);   // from the bottom of the panel: top of the action buttons
            float reviveY = actionTop + 24f;
            OpenOverlay(s.GoalLost ? "Wall touched!" : "You fell!", contentBottom + 30f + (offerGems ? reviveH + 24f + hintH : 0f) + actionTop);
            AudioManager.Play(Sfx.Fail);
            OnClose = () => { if (OnMenu != null) OnMenu(); };
            Transform panel = PanelOf();
            // How far you got: tower = the floor reached, flag = the goal, and a bar between the ground and the flag.
            int target = Mathf.Max(1, s.Spec.TargetRow());
            RectTransform row = ToyKit.PillRow(panel, "Result", 215f, 104f);
            ToyKit.InfoPill(row, ToyKit.Get("icon_tower"), s.State.Row.ToString(), Color.white, 270f, 104f);
            ToyKit.InfoPill(row, ToyKit.Get("icon_flag"), target.ToString(), new Color(1f, 0.95f, 0.6f), 270f, 104f);
            ProgressBar(panel, 345f, s.State.Row / (float)target);
            if (s.GoalLost)
            {
                // The reason, crisp and centred under the progress bar.
                Text why = UiKit.AddText(panel, "Reason", "This level fails on any wall touch.", 38, Theme.Ink, TextAnchor.MiddleCenter, true);
                why.fontStyle = FontStyle.Italic;
                UiKit.Place((RectTransform)why.transform, new Vector2(0.5f, 1f), new Vector2(0f, -415f), new Vector2(780f, 90f));
            }
            if (offerGems)
            {
                // ONE line, two equal buttons side by side (ad left, gems right; a lone gem button is centred and wider). Schedule: RevivePolicy.
                float w = offerAd ? ReviveBtnW : ReviveBtnWSolo;
                float step = ReviveBtnW + ReviveGap;
                int cost = s.NextReviveCost;
                if (offerAd)
                {
                    string adLabel = s.AdRewardPending ? "FREE" : s.Revive.AdLabel;   // "Watch Ad": no ad length in the text (owner 2026-11-10)
                    string adA11y = s.AdRewardPending ? "Revive for free, ad watched" : (adDim ? "Watch an ad to revive, not ready yet" : "Watch an ad to revive");
                    Button adBtn = ReviveButton(panel, adLabel, adA11y, ToyKit.Get("icon_play"), 1, -step * 0.5f, reviveY, w, () => { if (OnReviveAd != null) OnReviveAd(); });
                    if (adDim)
                    {
                        // Dimmed, never hidden or disabled: the tap still reaches RunController, which starts an ad load and calls ShowAdNotReady.
                        Image bi = adBtn.GetComponent<Image>();
                        if (bi != null) bi.color = new Color(0.55f, 0.55f, 0.55f, 0.85f);
                        Text bt = adBtn.GetComponentInChildren<Text>();
                        if (bt != null) bt.color = new Color(0.10f, 0.06f, 0.14f, 0.8f);
                        Text hint = UiKit.AddText(panel, "AdHint", AdNotReadyText, 38, Theme.Ink, TextAnchor.MiddleCenter, true);
                        hint.fontStyle = FontStyle.Bold;
                        UiKit.Place((RectTransform)hint.transform, new Vector2(0.5f, 0f), new Vector2(0f, reviveY + reviveH + 8f + AdHintH * 0.5f), new Vector2(780f, AdHintH));
                        hint.gameObject.SetActive(false);
                        adHint = hint;
                    }
                }
                // Too few gems: the button stays enabled and opens the Gems tab of the shop; the revive is taken by tapping again.
                ReviveButton(panel, cost.ToString(), "Revive for " + cost + " gems", ToyKit.Get("icon_gem"), 0, offerAd ? step * 0.5f : 0f, reviveY, w, () =>
                {
                    if (GameSession.Progress != null && GameSession.Progress.Gems < cost) OpenShopTab(ShopScreen.TabGems);
                    else if (OnRevive != null) OnRevive();
                });
            }
            OverlayRow(
                MakeAct("Retry", "Try again", 1, Glyphs.Restart, 150f, () => { if (OnRetry != null) OnRetry(); }),
                MakeAct("Shop", "Shop", 0, ToyKit.Get("icon_shop"), 150f, OpenShop),
                MakeAct("Home", "Level select", 2, ToyKit.Get("icon_home"), 150f, () => { if (OnMenu != null) OnMenu(); }));
        }

        // Revive line geometry (reference px, panel is 860 wide): two buttons of 380 and a 20 gap fill 780; both are far above the 48 dp / 8 dp rules.
        const float ReviveBtnW = 380f, ReviveBtnWSolo = 520f, ReviveGap = 20f;
        static float ReviveButtonH { get { return Mathf.Max(130f, UiKit.MinTouchRef); } }

        public const string AdNotReadyText = "Ad not ready, try again in a moment";
        static float AdHintH { get { return Mathf.Ceil(UiKit.MinBodyTextPx * UiKit.TextScale * 1.25f) * 2f + 8f; } }   // two lines at the current text scale
        Text adHint;

        /// <summary>Shows the "Ad not ready" message on the failed popup (the dimmed Watch Ad button was tapped). No-op when the popup has no dimmed ad button.</summary>
        public bool ShowAdNotReady()
        {
            if (adHint == null || overlay == null) return false;
            adHint.gameObject.SetActive(true);
            return true;
        }

        /// <summary>A revive button: icon at the left, a short label (Watch Ad or gem price) that shrinks to fit at 130 percent text (CUX-4, CUX-5; not colour only).</summary>
        Button ReviveButton(Transform panel, string label, string a11y, Sprite icon, int role, float x, float y, float width, Action act)
        {
            float h = ReviveButtonH;
            Button b = UiKit.AddButton(panel, a11y, label, ToyKit.Button(role == 1 ? 1 : 0), Color.white, new Vector2(width, h), act, 44);
            UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(x, y), new Vector2(width, h));
            b.GetComponent<A11yLabel>().Text = a11y;
            Text t = b.GetComponentInChildren<Text>();
            if (t != null)
            {
                var tr = (RectTransform)t.transform;
                tr.offsetMin = new Vector2(h * 0.78f, tr.offsetMin.y);
                tr.offsetMax = new Vector2(-16f, tr.offsetMax.y);
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow = VerticalWrapMode.Truncate;
                t.resizeTextForBestFit = true;
                t.resizeTextMinSize = 30;
                t.resizeTextMaxSize = 52;
            }
            if (icon != null)
            {
                Image ic = UiKit.AddImage(b.transform, "Icon", icon, Color.white);
                ic.raycastTarget = false;
                RectTransform r = ic.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = new Vector2(h * 0.42f, 0f);
                r.sizeDelta = new Vector2(h * 0.56f, h * 0.56f);
            }
            return b;
        }

        /// <summary>
        /// Endless game over (and the end of a 3:00 Time Attack): floor, best (or NEW BEST), score and coins paid; then (doc 46) the run Time (the time survived, or the full 3:00), the Pace
        /// and the difference to the previous best, and the NEW BEST badge. No revive in endless. Try again starts a new tower.
        /// </summary>
        public void ShowEndlessFailed(RunSession s, EndlessReward r)
        {
            RecordResult rec = LastRecord(true);
            bool ta = s.IsTimeAttack;
            ResultTiming timing = ResultTiming.For(s, rec, true, r.NewBest);
            float contentEnd = 670f + timing.TailHeight;
            float actionTop = ButtonY + ButtonSizeFor(150f);
            OpenOverlay(ta ? (s.TimeUp ? "Time up!" : "You fell!") : "You fell!", contentEnd + 20f + actionTop);
            OnClose = () => { if (OnMenu != null) OnMenu(); };
            Transform panel = PanelOf();
            Transform endTitle = panel.Find("Title");
            if (endTitle != null) { var etr = (RectTransform)endTitle; etr.anchoredPosition = new Vector2(0f, etr.anchoredPosition.y); etr.sizeDelta = new Vector2(540f, etr.sizeDelta.y); }   // centred on the panel (the frame shifts it 30 left to leave room for the X)
            RectTransform row1 = ToyKit.PillRow(panel, "Result", 215f, 104f);
            ToyKit.InfoPill(row1, ToyKit.Get("icon_tower"), r.Floors.ToString(), Color.white, 270f, 104f);
            ToyKit.InfoPill(row1, ToyKit.Get("icon_crown"), (r.NewBest ? r.Floors : r.BestBefore).ToString(), new Color(1f, 0.9f, 0.4f), 270f, 104f);
            RectTransform row2 = ToyKit.PillRow(panel, "Result2", 335f, 104f);
            ToyKit.InfoPill(row2, art != null ? art.StarFilled : null, s.State.Score.ToString(), Color.white, 270f, 104f);
            ToyKit.InfoPill(row2, art != null ? art.CoinIcon : null, "+" + r.Coins, new Color(1f, 0.92f, 0.5f), 270f, 104f);
            timing.Build(panel, 450f, 560f, 670f);
            OverlayRow(
                MakeAct("Retry", "Try again", 1, Glyphs.Restart, 150f, () => { if (OnRetry != null) OnRetry(); }),
                MakeAct("Shop", "Shop", 0, ToyKit.Get("icon_shop"), 150f, OpenShop),
                MakeAct("Home", "Level select", 2, ToyKit.Get("icon_home"), 150f, () => { if (OnMenu != null) OnMenu(); }));
            FitPanelToSafeArea();
        }

        public void ShowResults(RunSession s, RewardResult reward, bool hasNext)
        {
            RecordResult rec = LastRecord(false);
            ResultTiming timing = ResultTiming.For(s, rec, false, false);
            int mode = s.Mode;
            bool medals = MedalStrip.HasTargets(s.Level.Id, mode);
            // Compact result layout (CUX-3/4): nothing is ever scaled down, so the height is budgeted row by row (see FitPanelToSafeArea) and every row is tight:
            // title (own line height, so it never touches the stars), small stars, compact medal strip (icons + word), 2 earned rows, time and pace rows, ribbons, action row.
            const float TitleTop = 36f, StarSize = 84f, PillH = 96f, PillPitch = 102f, SectionGap = 6f;
            float titleFont = Mathf.Round(60f * UiKit.TextScale);   // 60 (not 76): "Level clear!" at 130 percent text must stay inside its line
            float titleH = Mathf.Ceil(titleFont * 1.55f);
            float starY = TitleTop + titleH + SectionGap;
            float yMedals = starY + StarSize + SectionGap;
            float medalsH = medals ? MedalStrip.CompactHeight + SectionGap : 0f;
            float yEarned = yMedals + medalsH;
            float yTime = yEarned + 2f * PillPitch, yTail = yTime + 2f * PillPitch - SectionGap;   // 6 px gaps between the pill rows; the ribbons start right under the pace row
            float actionTopL = ButtonY + ButtonSizeFor(150f);
            float panelH = yTail + timing.TailHeight + 12f + actionTopL;
            OpenOverlay("Level clear!", panelH);
            AudioManager.Play(Sfx.Win);
            OnClose = () => { if (OnMenu != null) OnMenu(); };
            Transform panel = PanelOf();
            Text titleText = panel.Find("Title") != null ? panel.Find("Title").GetComponent<Text>() : null;
            if (titleText != null)
            {
                titleText.fontSize = Mathf.RoundToInt(titleFont);
                var ttr = (RectTransform)titleText.transform;
                ttr.anchoredPosition = new Vector2(0f, -TitleTop);
                ttr.sizeDelta = new Vector2(ttr.sizeDelta.x, titleH);
            }
            int mask = s.BuildOutcome().StarMask;
            for (int i = 0; i < 3; i++)
            {
                bool got = (mask & (1 << i)) != 0;
                Image star = UiKit.AddImage(panel, "Star" + i, art != null ? (got ? art.StarFilled : art.StarEmpty) : null, got ? Color.white : new Color(1f, 1f, 1f, 0.6f));
                UiKit.Place(star.rectTransform, new Vector2(0.5f, 1f), new Vector2((i - 1) * 130f, -starY), new Vector2(StarSize, StarSize));
                star.rectTransform.pivot = new Vector2(0.5f, 1f);
            }
            // What you earned, as icon pills (no sentences): coins and gems, then the best combo.
            if (medals)
            {
                MedalLevel held = GameSession.Progress != null ? GameSession.Progress.Medal(s.Level.Id, mode) : MedalLevel.None;
                MedalStrip.Build(panel, yMedals, s.Level.Id, mode, held, rec != null && rec.MedalImproved ? rec.Medal : MedalLevel.None, true);   // compact: icons + word, NEW on a fresh medal; pace targets live in the level-start popup
            }
            RectTransform row1 = ToyKit.PillRow(panel, "Earned", yEarned, PillH);
            ToyKit.InfoPill(row1, art != null ? art.CoinIcon : null, "+" + reward.TotalCoins, new Color(1f, 0.92f, 0.5f), 270f, PillH);
            ToyKit.InfoPill(row1, art != null ? art.GemIcon : null, "+" + reward.TotalGems, new Color(0.75f, 0.92f, 1f), 270f, PillH);
            RectTransform row2 = ToyKit.PillRow(panel, "Earned2", yEarned + PillPitch, PillH);
            ToyKit.InfoPill(row2, ToyKit.Get("icon_flame"), "x" + s.State.BestCombo, new Color(1f, 0.78f, 0.35f), 270f, PillH);
            if (reward.FirstClear) ToyKit.InfoPill(row2, ToyKit.Get("icon_crown"), "First", new Color(1f, 0.9f, 0.4f), 270f, PillH);
            if (!string.IsNullOrEmpty(reward.UnlockedRegionId)) ToyKit.InfoPill(row2, ToyKit.Get("icon_flag"), "New", new Color(0.7f, 1f, 0.75f), 270f, PillH);
            timing.Build(panel, yTime, yTime + PillPitch, yTail);
            // Three equal round buttons (owner issue 16.7).
            OverlayRow(
                MakeAct("Next", "Next level", 1, ToyKit.Get("icon_play"), 150f, () => { if (OnNext != null) OnNext(); }, hasNext),
                MakeAct("Retry", "Play again", 0, Glyphs.Restart, 150f, () => { if (OnRetry != null) OnRetry(); }),
                MakeAct("Home", "Level select", 2, ToyKit.Get("icon_home"), 150f, () => { if (OnMenu != null) OnMenu(); }));
            FitPanelToSafeArea();
        }

        /// <summary>
        /// Guard for tall popups (CUX-3): centres the overlay panel in the safe area. It NEVER scales the panel: scaling shrank the buttons and the close X below 48 dp. The callers budget the height
        /// from the real line heights instead (compact rows); if a panel is still taller than the safe area the overflow is logged so the layout test and the owner see it.
        /// </summary>
        void FitPanelToSafeArea()
        {
            Transform p = overlay != null ? overlay.Find("Panel") : null;
            if (p == null) return;
            var rt = (RectTransform)p;
            Vector2 canvasSize = UiKit.CanvasSizeRef();
            Rect sa = UiKit.SafeAreaNormalized();
            rt.anchoredPosition = new Vector2(0f, (sa.center.y - 0.5f) * canvasSize.y);   // the panel is anchored at the canvas centre: move it to the centre of the safe area
            rt.localScale = Vector3.one;
            float availH = canvasSize.y * sa.height;
            if (rt.sizeDelta.y > availH) Debug.LogWarning("RunHud popup is taller than the safe area: " + rt.sizeDelta.y + " > " + availH);
        }
    }
}
