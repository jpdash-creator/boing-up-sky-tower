using System;
using BoingTower.Economy;
using BoingTower.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BoingTower.Gameplay
{
    /// <summary>Code-built HUD and overlays (intro, pause, failed, results) for the Run scene.</summary>
    public sealed class RunHud : MonoBehaviour
    {
        public Action OnPause, OnResume, OnRetry, OnNext, OnMenu, OnRevive;
        /// <summary>The close mark of the current overlay (pause: resume, end screens: back to the level select).</summary>
        Action OnClose;

        ArtCatalog art;
        Canvas canvas;
        Text floorText, goalText, gemText, comboText, timeText, introText, speedText;
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

        public void Build(RunSession session, ArtCatalog catalog)
        {
            mirror = GameSettings.LeftHanded;
            art = catalog;
            canvas = UiKit.CreateCanvas("RunHud", 10);
            canvas.transform.SetParent(transform, false);

            // Dark backing strip: text and icons stay readable over any background art (CUX-5).
            Image strip = UiKit.AddImage(canvas.transform, "TopBacking", null, new Color(0.05f, 0.07f, 0.14f, 0.72f));
            P(strip.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 300f));
            strip.rectTransform.anchorMin = new Vector2(0f, 1f);
            strip.rectTransform.anchorMax = new Vector2(1f, 1f);
            strip.rectTransform.sizeDelta = new Vector2(0f, 300f);

            RectTransform root = UiKit.SafeArea(canvas.transform);

            floorText = UiKit.AddText(root, "Floor", "", 46, Color.white, Align(TextAnchor.UpperLeft), true);
            P((RectTransform)floorText.transform, new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(420f, 60f));

            endless = session.IsEndless;
            bestBefore = endless && GameSession.Progress != null ? GameSession.Progress.EndlessBestFloor(session.Region.Id) : 0;
            goalText = UiKit.AddText(root, "Goal", endless ? "Best " + bestBefore : RunSession.DescribeGoal(session.Spec), endless ? 40 : 34, new Color(1f, 0.95f, 0.6f), Align(TextAnchor.UpperLeft));
            P((RectTransform)goalText.transform, new Vector2(0f, 1f), new Vector2(30f, -112f), new Vector2(700f, 90f));

            // Top right: the 3 stars of the level (filled when collected) and the gems taken so far.
            starIcons = new Image[3];
            for (int i = 0; i < 3 && !endless; i++)
            {
                starIcons[i] = UiKit.AddImage(root, "Star" + i, art != null ? art.StarFilled : null, new Color(1f, 1f, 1f, 0.35f));
                P(starIcons[i].rectTransform, new Vector2(1f, 1f), new Vector2(-30f - (2 - i) * 70f, -26f), new Vector2(60f, 60f));
            }
            Image gemIcon = UiKit.AddImage(root, "GemIcon", art != null ? art.GemIcon : null, Color.white);
            P(gemIcon.rectTransform, new Vector2(1f, 1f), new Vector2(-190f, -96f), new Vector2(56f, 56f));
            gemText = UiKit.AddText(root, "Gems", "0", 46, new Color(0.7f, 0.9f, 1f), Align(TextAnchor.MiddleLeft), true);
            P((RectTransform)gemText.transform, new Vector2(1f, 1f), new Vector2(-30f, -96f), new Vector2(150f, 56f));
            if (endless) { gemIcon.gameObject.SetActive(false); gemText.gameObject.SetActive(false); }   // endless has no gems or stars to collect

            Button pause = UiKit.AddButton(root, "Pause", "II", art != null ? ArtCatalog.Pick(art.RoundButtons, 0) : null, new Color(1f, 1f, 1f, 0.95f), new Vector2(130f, 130f), () => { if (OnPause != null) OnPause(); }, 48);
            P((RectTransform)pause.transform, new Vector2(1f, 1f), new Vector2(-30f, -165f), new Vector2(130f, 130f));

            comboText = UiKit.AddText(root, "Combo", "", 72, new Color(1f, 0.85f, 0.2f));
            P((RectTransform)comboText.transform, new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(700f, 100f));

            speedText = UiKit.AddText(root, "SpeedUp", "", 60, new Color(1f, 0.55f, 0.3f), TextAnchor.MiddleCenter, true);
            P((RectTransform)speedText.transform, new Vector2(0.5f, 1f), new Vector2(0f, -400f), new Vector2(760f, 90f));
            speedText.gameObject.SetActive(false);

            timeText = UiKit.AddText(root, "Time", "", 40, Color.white, Align(TextAnchor.UpperLeft));
            P((RectTransform)timeText.transform, new Vector2(0f, 1f), new Vector2(30f, -215f), new Vector2(500f, 60f));

            string introTitle = endless ? "Endless" : "Level " + session.Level.Index;
            introText = UiKit.AddText(root, "Intro", session.Region.Name + "\n" + introTitle + "\n" + RunSession.DescribeGoal(session.Spec) + "\n\nHold < > to move, tap JUMP", 64, Color.white);
            P((RectTransform)introText.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(820f, 460f));
            Pad = ControlPad.Build(root, art, mirror);
            introTimer = 3.2f;
        }

        Image[] starIcons;
        int lastRow = -1, lastStarMask = -1, lastGems = -1, lastTimeKey = -1, lastCombo2 = -1, lastBest = -1;

        public void Refresh(RunSession s, float deltaTime)
        {
            SimState st = s.State;
            // Strings are rebuilt only when a value changes: no per-frame allocations (CUX-1).
            if (st.Row != lastRow)
            {
                lastRow = st.Row;
                UiKit.SetText(floorText, endless ? "Floor " + st.Row : "Floor " + st.Row + " / " + s.Spec.TargetRow());
                if (endless)
                {
                    int best = st.Row > bestBefore ? st.Row : bestBefore;   // the best floor follows the climb once it is beaten
                    if (best != lastBest) { lastBest = best; UiKit.SetText(goalText, "Best " + best); }
                }
            }
            if (st.StarMask != lastStarMask)
            {
                lastStarMask = st.StarMask;
                for (int i = 0; i < 3; i++) if (starIcons[i] != null) starIcons[i].color = (st.StarMask & (1 << i)) != 0 ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }
            int gems = st.Gems + st.HiddenGems;
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
            if (st.Combo != lastCombo2) { lastCombo2 = st.Combo; UiKit.SetText(comboText, st.Combo > 1 ? "COMBO x" + st.Combo : ""); }
            if (!GameSettings.ReducedMotion) comboText.transform.localScale = Vector3.one * (1f + comboPulse * 0.35f);

            // "Speed up" cue each time the screen speeds up (every ScrollStepSeconds): text plus a short pulse, a static cue with reduced motion.
            if (st.StartTick >= 0 && !s.Sim.CampaignRamp)
            {
                int step = (st.Tick - st.StartTick) / (s.Cfg.ScrollStepSeconds * SimConfig.TicksPerSecond);
                if (step > lastSpeedStep)
                {
                    lastSpeedStep = step;
                    UiKit.SetText(speedText, "SPEED UP  +" + (step * s.Cfg.ScrollStepPercent) + "%");
                    speedText.gameObject.SetActive(true);
                    speedCueTimer = 1.6f;
                    AudioManager.Play(Sfx.SpeedUp);
                }
            }
            if (speedCueTimer > 0f)
            {
                speedCueTimer -= deltaTime;
                if (!GameSettings.ReducedMotion) speedText.transform.localScale = Vector3.one * (1f + Mathf.Clamp01(speedCueTimer - 1.2f) * 0.6f);
                if (speedCueTimer <= 0f) speedText.gameObject.SetActive(false);
            }

            if (introTimer > 0f)
            {
                introTimer -= deltaTime;
                if (introTimer <= 0f) introText.gameObject.SetActive(false);
            }
        }

        public float IntroRemaining { get { return introTimer; } }

        /// <summary>Ends the level intro immediately (used by tests and could back a tap-to-skip).</summary>
        public void SkipIntro()
        {
            introTimer = 0f;
            if (introText != null) introText.gameObject.SetActive(false);
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
            if (overlay != null) Destroy(overlay.gameObject);
            overlay = null;
        }

        Transform PanelOf() { return overlay.Find("Panel"); }

        Button OverlayButton(string label, Sprite sprite, Color tint, float y, Action action)
        {
            Button b = UiKit.AddButton(PanelOf(), label, label, sprite, tint, new Vector2(560f, 130f), action, 50);
            UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(0f, y), new Vector2(560f, 130f));
            return b;
        }

        public void ShowPause()
        {
            OpenOverlay("Paused", 820f);
            OnClose = () => { if (OnResume != null) OnResume(); };
            Sprite b = art != null ? ArtCatalog.Pick(art.Buttons, 0) : null;
            OverlayButton("Resume", b, Color.white, 400f, () => { if (OnResume != null) OnResume(); });
            OverlayButton("Restart", b, new Color(1f, 0.85f, 0.9f), 240f, () => { if (OnRetry != null) OnRetry(); });
            OverlayButton("Level select", b, new Color(0.9f, 0.95f, 1f), 80f, () => { if (OnMenu != null) OnMenu(); });
        }

        public void ShowFailed(RunSession s, bool canAffordRevive)
        {
            OpenOverlay("You fell!", 860f);
            AudioManager.Play(Sfx.Fail);
            OnClose = () => { if (OnMenu != null) OnMenu(); };
            Text sub = UiKit.AddText(PanelOf(), "Sub", "Floor " + s.State.Row + " of " + s.Spec.TargetRow(), 52, Color.white);
            UiKit.OnPanel(sub);
            UiKit.Place((RectTransform)sub.transform, new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(760f, 80f));
            Sprite b = art != null ? ArtCatalog.Pick(art.Buttons, 1) : null;
            Button revive = OverlayButton("Revive  (" + s.NextReviveCost + " gems)", b, canAffordRevive ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.8f), 400f, () => { if (OnRevive != null) OnRevive(); });
            revive.interactable = canAffordRevive;
            Sprite b2 = art != null ? ArtCatalog.Pick(art.Buttons, 0) : null;
            OverlayButton("Try again", b2, Color.white, 240f, () => { if (OnRetry != null) OnRetry(); });
            OverlayButton("Level select", b2, new Color(0.9f, 0.95f, 1f), 80f, () => { if (OnMenu != null) OnMenu(); });
        }

        /// <summary>Endless game over: floor, best (or NEW BEST), score and coins paid; no revive in endless. Try again starts a new tower.</summary>
        public void ShowEndlessFailed(RunSession s, EndlessReward r)
        {
            OpenOverlay("You fell!", 1080f);
            OnClose = () => { if (OnMenu != null) OnMenu(); };
            Text sub = UiKit.AddText(PanelOf(), "Sub", "Floor " + r.Floors, 64, Color.white);
            UiKit.OnPanel(sub);
            UiKit.Place((RectTransform)sub.transform, new Vector2(0.5f, 1f), new Vector2(0f, -215f), new Vector2(760f, 100f));
            string best = r.NewBest ? "NEW BEST!" : "Best " + r.BestBefore;
            string coins = "Coins +" + r.Coins;
            if (r.OverCap) coins += "  (daily limit: half rate)";
            Text info = UiKit.AddText(PanelOf(), "Info", best + "\nScore " + s.State.Score + "\n" + coins, 42, Color.white);
            UiKit.OnPanel(info);
            UiKit.Place((RectTransform)info.transform, new Vector2(0.5f, 1f), new Vector2(0f, -335f), new Vector2(780f, 280f));
            Sprite b2 = art != null ? ArtCatalog.Pick(art.Buttons, 0) : null;
            Sprite b1 = art != null ? ArtCatalog.Pick(art.Buttons, 1) : null;
            OverlayButton("Try again", b1, Color.white, 240f, () => { if (OnRetry != null) OnRetry(); });
            OverlayButton("Level select", b2, new Color(0.9f, 0.95f, 1f), 80f, () => { if (OnMenu != null) OnMenu(); });
        }

        public void ShowResults(RunSession s, RewardResult reward, bool hasNext)
        {
            OpenOverlay("Level clear!", 1180f);
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
            string line = "Coins +" + reward.TotalCoins + "     Gems +" + reward.TotalGems + "\nStars " + reward.Stars + " / 3     Best combo x" + s.State.BestCombo;
            if (reward.TotalCoins > 0) line += "\n(clear + " + reward.Stars + " stars + combos " + reward.ComboCoins + ")";
            if (reward.FirstClear) line += "\nFirst clear!";
            if (!string.IsNullOrEmpty(reward.UnlockedRegionId)) line += "\nNew region unlocked!";
            Text info = UiKit.AddText(panel, "Info", line, 40, Color.white);
            UiKit.OnPanel(info);
            UiKit.Place((RectTransform)info.transform, new Vector2(0.5f, 1f), new Vector2(0f, -400f), new Vector2(780f, 210f));
            Sprite b = art != null ? ArtCatalog.Pick(art.Buttons, 1) : null;
            Sprite b2 = art != null ? ArtCatalog.Pick(art.Buttons, 0) : null;
            Button next = OverlayButton("Next level", b, hasNext ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.8f), 400f, () => { if (OnNext != null) OnNext(); });
            next.interactable = hasNext;
            OverlayButton("Play again", b2, Color.white, 240f, () => { if (OnRetry != null) OnRetry(); });
            OverlayButton("Level select", b2, new Color(0.9f, 0.95f, 1f), 80f, () => { if (OnMenu != null) OnMenu(); });
        }
    }
}
