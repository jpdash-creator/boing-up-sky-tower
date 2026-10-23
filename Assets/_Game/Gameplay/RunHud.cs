using System;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// Code-built HUD and overlays (intro, pause, failed, results) for the Run scene. Icon-first (docs/16 sections G and I): dark pills with a
    /// painted icon and a number, a slim tower gauge instead of floor and goal sentences, icon banners for combo and speed-up, and round icon
    /// buttons with a one-word caption in the popups.
    /// </summary>
    public sealed class RunHud : MonoBehaviour
    {
        public Action OnPause, OnResume, OnRetry, OnNext, OnMenu, OnRevive;
        /// <summary>A power-up button was pressed (loadout slot 0..2). The run controller latches it into the next tick's input.</summary>
        public Action<int> OnPower;
        /// <summary>The close mark of the current overlay (pause: resume, end screens: back to the level select).</summary>
        Action OnClose;

        ArtCatalog art;
        Canvas canvas;
        Text floorText, goalText, gemText, comboText, timeText, introText, speedText;
        RectTransform comboRoot, speedRoot, introRoot, gaugeMarker;
        float gaugeH;
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
        /// <summary>The power-up buttons above the jump pad (docs/15 FR-PWR-3); empty when the run has no loadout.</summary>
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
        Text Pill(RectTransform root, string name, Sprite icon, Vector2 anchor, Vector2 pos, Vector2 size, Color number)
        {
            Image pill = Theme.GlassPanel(root, name, size.y * 0.25f);
            P(pill.rectTransform, anchor, pos, size);
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
            bestBefore = endless && GameSession.Progress != null ? GameSession.Progress.EndlessBestFloor(session.Region.Id) : 0;

            // Top left: floor (tower icon + number) and goal (flag icon + target). The words live in the level intro only.
            floorText = Pill(root, "FloorPill", ToyKit.Get("icon_tower"), new Vector2(0f, 1f), new Vector2(18f, -16f), new Vector2(300f, 104f), Color.white);
            goalText = Pill(root, "GoalPill", ToyKit.Get("icon_flag"), new Vector2(0f, 1f), new Vector2(18f, -134f), new Vector2(300f, 104f), new Color(1f, 0.95f, 0.6f));
            UiKit.SetText(goalText, endless ? bestBefore.ToString() : session.Spec.TargetRow().ToString());

            // Slim tower gauge under the pills: Momo's height as a marker between the ground and the finish flag.
            gaugeH = 520f;
            Image gauge = Theme.GlassPanel(root, "Gauge", 24f);
            P(gauge.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -256f), new Vector2(64f, gaugeH));
            Image marker = UiKit.AddImage(gauge.transform, "Marker", Theme.RoundSprite, new Color(1f, 0.82f, 0.3f, 1f));
            marker.type = Image.Type.Sliced;
            marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            marker.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            marker.rectTransform.sizeDelta = new Vector2(44f, 44f);
            marker.rectTransform.anchoredPosition = new Vector2(0f, 40f);
            Image mring = UiKit.AddImage(marker.transform, "Ring", Theme.RingSprite, Color.white);
            mring.type = Image.Type.Sliced;
            mring.preserveAspect = false;
            UiKit.Stretch(mring.rectTransform);
            gaugeMarker = marker.rectTransform;
            gauge.gameObject.SetActive(!endless);

            // Top right: gems pill, the 3 stars of the level (lit when collected), then the round pause button.
            starIcons = new Image[3];
            Text gems = Pill(root, "GemPill", art != null ? art.GemIcon : null, new Vector2(1f, 1f), new Vector2(-18f, -16f), new Vector2(300f, 104f), new Color(0.75f, 0.92f, 1f));
            gemText = gems;
            for (int i = 0; i < 3 && !endless; i++)
            {
                starIcons[i] = UiKit.AddImage(root, "Star" + i, art != null ? art.StarFilled : null, new Color(0.35f, 0.35f, 0.4f, 0.9f));
                P(starIcons[i].rectTransform, new Vector2(1f, 1f), new Vector2(-30f - (2 - i) * 96f, -134f), new Vector2(88f, 88f));
            }
            if (endless) { gems.transform.parent.gameObject.SetActive(false); }   // endless has no gems or stars to collect

            Button pause = ToyKit.GlyphButton(root, "Pause", "Pause", 2, Glyphs.Pause, new Vector2(130f, 130f), () => { if (OnPause != null) OnPause(); });
            P((RectTransform)pause.transform, new Vector2(1f, 1f), new Vector2(-30f, endless ? -140f : -240f), new Vector2(130f, 130f));

            // Combo: flame icon and the multiplier; speed up: arrows icon and the percent. Both appear on demand.
            comboRoot = BannerRoot(root, "ComboBanner", "icon_flame", new Color(1f, 0.85f, 0.2f), new Vector2(0f, -150f), out comboText);
            speedRoot = BannerRoot(root, "SpeedBanner", "icon_speed", new Color(1f, 0.7f, 0.4f), new Vector2(0f, -280f), out speedText);
            comboRoot.gameObject.SetActive(false);
            speedRoot.gameObject.SetActive(false);

            timeText = UiKit.AddText(root, "Time", "", 40, Color.white, Align(TextAnchor.UpperLeft));
            P((RectTransform)timeText.transform, new Vector2(0f, 1f), new Vector2(100f, -262f), new Vector2(460f, 60f));

            BuildIntro(root, session);
            Pad = ControlPad.Build(root, art, mirror);
            Powers = new PowerUpBar();
            Powers.OnPress = slot => { if (OnPower != null) OnPower(slot); };
            Powers.Build(root, session, P);
            Powers.SetVisible(false);   // shown when the intro text is gone
            introTimer = 3.2f;
        }

        /// <summary>A centred icon + text banner (combo, speed up) under the top pills. Returns its root; the text is the out parameter.</summary>
        RectTransform BannerRoot(RectTransform root, string name, string icon, Color number, Vector2 pos, out Text text)
        {
            Image pill = Theme.GlassPanel(root, name, 24f);
            UiKit.Place(pill.rectTransform, new Vector2(0.5f, 1f), pos, new Vector2(380f, 112f));
            pill.rectTransform.pivot = new Vector2(0.5f, 1f);
            Image ic = UiKit.AddImage(pill.transform, "Icon", ToyKit.Get(icon), Color.white);
            ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            ic.rectTransform.pivot = new Vector2(0f, 0.5f);
            ic.rectTransform.anchoredPosition = new Vector2(22f, 0f);
            ic.rectTransform.sizeDelta = new Vector2(84f, 84f);
            text = UiKit.AddText(pill.transform, "Value", "", 64, number, TextAnchor.MiddleCenter, true);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
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
            UiKit.Place(back.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 130f), new Vector2(780f, 560f));
            back.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            introRoot = back.rectTransform;
            string introTitle = endless ? "Endless" : "Level " + session.Level.Index;
            Text region = UiKit.AddText(introRoot, "Region", session.Region.Name, 48, new Color(0.85f, 0.93f, 1f), TextAnchor.MiddleCenter, true);
            UiKit.Place((RectTransform)region.transform, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(720f, 70f));
            introText = UiKit.AddText(introRoot, "Level", introTitle, 88, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.Place((RectTransform)introText.transform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(720f, 120f));
            // Goal: flag icon and the target floor.
            Image flag = UiKit.AddImage(introRoot, "GoalIcon", ToyKit.Get("icon_flag"), Color.white);
            UiKit.Place(flag.rectTransform, new Vector2(0.5f, 1f), new Vector2(-110f, -250f), new Vector2(96f, 96f));
            flag.rectTransform.pivot = new Vector2(0.5f, 1f);
            Text goal = UiKit.AddText(introRoot, "Goal", endless ? "" : session.Spec.TargetRow().ToString(), 72, new Color(1f, 0.95f, 0.6f), TextAnchor.MiddleLeft, true);
            UiKit.Place((RectTransform)goal.transform, new Vector2(0.5f, 1f), new Vector2(100f, -246f), new Vector2(260f, 100f));
            goal.rectTransform.pivot = new Vector2(0.5f, 1f);
            flag.gameObject.SetActive(!endless);
            // Controls hint: left, right, jump as small plates with the same glyphs as the real buttons.
            Sprite[] glyphs = { Glyphs.Left, Glyphs.Left, Glyphs.Up };
            for (int i = 0; i < 3; i++)
            {
                Image plate = UiKit.AddImage(introRoot, "Hint" + i, ToyKit.Button(i == 2 ? 1 : 2), Color.white);
                plate.type = Image.Type.Sliced;
                plate.preserveAspect = false;
                plate.pixelsPerUnitMultiplier = 3f;
                UiKit.Place(plate.rectTransform, new Vector2(0.5f, 0f), new Vector2((i - 1) * 150f, 50f), new Vector2(120f, 120f));
                plate.rectTransform.pivot = new Vector2(0.5f, 0f);
                Image g = UiKit.AddImage(plate.transform, "Glyph", glyphs[i], Color.white);
                g.rectTransform.anchorMin = g.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                g.rectTransform.sizeDelta = new Vector2(76f, 76f);
                if (i == 1) g.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            }
        }

        Image[] starIcons;
        int lastRow = -1, lastStarMask = -1, lastGems = -1, lastTimeKey = -1, lastCombo2 = -1, lastBest = -1;

        public void Refresh(RunSession s, float deltaTime)
        {
            SimState st = s.State;
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
                    gaugeMarker.anchoredPosition = new Vector2(0f, 40f + f * (gaugeH - 80f));
                }
            }
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
            if (!GameSettings.ReducedMotion) comboRoot.localScale = Vector3.one * (1f + comboPulse * 0.25f);

            // "Speed up" cue each time the screen speeds up (every ScrollStepSeconds): icon plus percent, a static cue with reduced motion.
            if (st.StartTick >= 0 && !s.Sim.CampaignRamp)
            {
                int step = (st.Tick - st.StartTick) / (s.Cfg.ScrollStepSeconds * SimConfig.TicksPerSecond);
                if (step > lastSpeedStep)
                {
                    lastSpeedStep = step;
                    UiKit.SetText(speedText, "+" + (step * s.Cfg.ScrollStepPercent) + "%");
                    speedRoot.gameObject.SetActive(true);
                    speedCueTimer = 1.6f;
                    AudioManager.Play(Sfx.SpeedUp);
                }
            }
            if (speedCueTimer > 0f)
            {
                speedCueTimer -= deltaTime;
                if (!GameSettings.ReducedMotion) speedRoot.localScale = Vector3.one * (1f + Mathf.Clamp01(speedCueTimer - 1.2f) * 0.4f);
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

        void OpenOverlay(string title, float height = 1000f)
        {
            CloseOverlay();
            overlay = UiKit.Rect(canvas.transform, "Overlay");
            UiKit.Stretch(overlay);
            var dim = overlay.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.62f);
            UiKit.BuildPopupFrame(overlay, title, art, new Vector2(860f, height), () => { if (OnClose != null) OnClose(); });
        }

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
            for (int i = 0; i < acts.Length; i++) total += acts[i].Size;
            float x = -total * 0.5f;
            for (int i = 0; i < acts.Length; i++)
            {
                Act a = acts[i];
                Button b = ToyKit.IconButton(panel, a.A11y, a.A11y, a.Role, a.Icon, new Vector2(a.Size, a.Size), a.Do, 0.6f);
                b.interactable = a.Enabled;
                UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(x + a.Size * 0.5f, 118f), new Vector2(a.Size, a.Size));
                Text cap = UiKit.AddText(panel, a.A11y + "Caption", a.Caption, 42, Theme.Ink, TextAnchor.MiddleCenter, true);
                var o = cap.GetComponent<Outline>();
                if (o != null) o.enabled = false;
                cap.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiKit.Place((RectTransform)cap.transform, new Vector2(0.5f, 0f), new Vector2(x + a.Size * 0.5f, 46f), new Vector2(a.Size + gap, 60f));
                x += a.Size + gap;
            }
        }

        public void ShowPause()
        {
            OpenOverlay("Paused", 640f);
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
        void OpenShop()
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
            };
            GameSession.Progress.LaunchPromoActive = GameSettings.LaunchPromo;
            shop = new ShopScreen(safe, GameSession.Progress, ShopCatalog.FromRepository(GameSession.Repo), art, host);
            shop.Build(ShopScreen.TabPowerups, ShopScreen.GemsSection);
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

        public void ShowFailed(RunSession s, bool canAffordRevive)
        {
            OpenOverlay("You fell!", 800f);
            AudioManager.Play(Sfx.Fail);
            OnClose = () => { if (OnMenu != null) OnMenu(); };
            Transform panel = PanelOf();
            // How far you got: tower = the floor reached, flag = the goal, and a bar between the ground and the flag.
            int target = Mathf.Max(1, s.Spec.TargetRow());
            RectTransform row = ToyKit.PillRow(panel, "Result", 215f, 104f);
            ToyKit.InfoPill(row, ToyKit.Get("icon_tower"), s.State.Row.ToString(), Color.white, 270f, 104f);
            ToyKit.InfoPill(row, ToyKit.Get("icon_flag"), target.ToString(), new Color(1f, 0.95f, 0.6f), 270f, 104f);
            ProgressBar(panel, 345f, s.State.Row / (float)target);
            // Four equal round buttons (owner issue 16.1): retry, revive, shop and home.
            OverlayRow(
                MakeAct("Retry", "Try again", 1, Glyphs.Restart, 150f, () => { if (OnRetry != null) OnRetry(); }),
                MakeAct("Revive " + s.NextReviveCost, "Revive for " + s.NextReviveCost + " gems", 0, ToyKit.Get("icon_gem"), 150f, () => { if (OnRevive != null) OnRevive(); }, canAffordRevive),
                MakeAct("Shop", "Shop", 0, ToyKit.Get("icon_shop"), 150f, OpenShop),
                MakeAct("Home", "Level select", 2, ToyKit.Get("icon_home"), 150f, () => { if (OnMenu != null) OnMenu(); }));
        }

        /// <summary>Endless game over: floor, best (or NEW BEST), score and coins paid; no revive in endless. Try again starts a new tower.</summary>
        public void ShowEndlessFailed(RunSession s, EndlessReward r)
        {
            OpenOverlay("You fell!", 900f);
            OnClose = () => { if (OnMenu != null) OnMenu(); };
            Transform panel = PanelOf();
            RectTransform row1 = ToyKit.PillRow(panel, "Result", 215f, 104f);
            ToyKit.InfoPill(row1, ToyKit.Get("icon_tower"), r.Floors.ToString(), Color.white, 270f, 104f);
            ToyKit.InfoPill(row1, ToyKit.Get("icon_crown"), (r.NewBest ? r.Floors : r.BestBefore).ToString(), new Color(1f, 0.9f, 0.4f), 270f, 104f);
            RectTransform row2 = ToyKit.PillRow(panel, "Result2", 335f, 104f);
            ToyKit.InfoPill(row2, art != null ? art.StarFilled : null, s.State.Score.ToString(), Color.white, 270f, 104f);
            ToyKit.InfoPill(row2, art != null ? art.CoinIcon : null, "+" + r.Coins, new Color(1f, 0.92f, 0.5f), 270f, 104f);
            if (r.NewBest) ToyKit.Ribbon(panel, "New best!", new Vector2(0.5f, 1f), new Vector2(0f, -450f), new Vector2(520f, 184f));
            OverlayRow(
                MakeAct("Retry", "Try again", 1, Glyphs.Restart, 150f, () => { if (OnRetry != null) OnRetry(); }),
                MakeAct("Shop", "Shop", 0, ToyKit.Get("icon_shop"), 150f, OpenShop),
                MakeAct("Home", "Level select", 2, ToyKit.Get("icon_home"), 150f, () => { if (OnMenu != null) OnMenu(); }));
        }

        public void ShowResults(RunSession s, RewardResult reward, bool hasNext)
        {
            OpenOverlay("Level clear!", 980f);
            AudioManager.Play(Sfx.Win);
            OnClose = () => { if (OnMenu != null) OnMenu(); };
            Transform panel = PanelOf();
            int mask = s.BuildOutcome().StarMask;
            for (int i = 0; i < 3; i++)
            {
                bool got = (mask & (1 << i)) != 0;
                Image star = UiKit.AddImage(panel, "Star" + i, art != null ? (got ? art.StarFilled : art.StarEmpty) : null, got ? Color.white : new Color(1f, 1f, 1f, 0.6f));
                UiKit.Place(star.rectTransform, new Vector2(0.5f, 1f), new Vector2((i - 1) * 200f, -190f), new Vector2(180f, 180f));
                star.rectTransform.pivot = new Vector2(0.5f, 1f);
            }
            // What you earned, as icon pills (no sentences): coins and gems, then the best combo.
            RectTransform row1 = ToyKit.PillRow(panel, "Earned", 400f, 104f);
            ToyKit.InfoPill(row1, art != null ? art.CoinIcon : null, "+" + reward.TotalCoins, new Color(1f, 0.92f, 0.5f), 270f, 104f);
            ToyKit.InfoPill(row1, art != null ? art.GemIcon : null, "+" + reward.TotalGems, new Color(0.75f, 0.92f, 1f), 270f, 104f);
            RectTransform row2 = ToyKit.PillRow(panel, "Earned2", 520f, 104f);
            ToyKit.InfoPill(row2, ToyKit.Get("icon_flame"), "x" + s.State.BestCombo, new Color(1f, 0.78f, 0.35f), 270f, 104f);
            if (reward.FirstClear) ToyKit.InfoPill(row2, ToyKit.Get("icon_crown"), "First", new Color(1f, 0.9f, 0.4f), 270f, 104f);
            if (!string.IsNullOrEmpty(reward.UnlockedRegionId)) ToyKit.InfoPill(row2, ToyKit.Get("icon_flag"), "New", new Color(0.7f, 1f, 0.75f), 270f, 104f);
            // Three equal round buttons (owner issue 16.7).
            OverlayRow(
                MakeAct("Next", "Next level", 1, ToyKit.Get("icon_play"), 150f, () => { if (OnNext != null) OnNext(); }, hasNext),
                MakeAct("Retry", "Play again", 0, Glyphs.Restart, 150f, () => { if (OnRetry != null) OnRetry(); }),
                MakeAct("Home", "Level select", 2, ToyKit.Get("icon_home"), 150f, () => { if (OnMenu != null) OnMenu(); }));
        }
    }
}
