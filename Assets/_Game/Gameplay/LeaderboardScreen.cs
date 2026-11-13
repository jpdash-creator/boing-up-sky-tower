using System;
using System.Collections.Generic;
using System.Globalization;
using FluffyClimbers.Economy;
using FluffyClimbers.Leaderboard;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// The weekly crown leaderboard screen (Planning/LEADERBOARD-DESIGN.md, owner decisions 2026-11-12; redone 2026-11-13 with the painted kit <c>lb_*</c>, Bug List 1).
    /// Over the full-screen picture <c>leaderboard_bg</c>: a top row (Back, the week timer pill, the round Prizes button), then the painted frame with the ribbon title, three tabs (Overall, Regions, Endless),
    /// the top 100 as pooled painted row plates (rank 1 gold, 2 silver, 3 bronze, 4 to 10 green, 11 and lower grey; medal or badge with the rank number, round avatar, name, big crown and crowns) and, when the player
    /// is not among the 100, a pinned own card ("#348") under the list. A player inside the top 100 sees the list opened around their own row (glow outline and a YOU tag); ranks 1 to 10 open at the top.
    /// A guest or signed-out player sees a sign-in card, a player without consent the consent card, a switched-off feature a "coming soon" card (inside the frame).
    /// Layout is measured (HeroSheetScreen pattern): a fitter rebuilds it when the screen shape changes. Only about a screenful of row objects exists; scrolling re-binds them (cached strings, no allocation);
    /// the only per-frame work is one compare of the remaining whole seconds for the timer. No sprite is scaled non-uniformly: the stadium plates slice only their rounded caps (scaled with the row height), the frame
    /// is a ribbon piece at uniform scale over a body whose flat middle stretches vertically.
    /// </summary>
    public sealed class LeaderboardScreen
    {
        public const string ScreenName = "Leaderboard";

        /// <summary>What the screen needs from the meta scene.</summary>
        public sealed class Host
        {
            public Func<RectTransform> NewOverlay;
            public Action CloseOverlay;
            /// <summary>Opens the consent popup (the meta scene shows it and rebuilds the screen after the answer).</summary>
            public Action ShowConsent;
            /// <summary>Google Play Games sign-in (the account flow) followed by the Firebase sign-in; done(true) when the player can take part.</summary>
            public Action<Action<bool>> SignIn;
        }

        public enum State { ComingSoon, SignIn, Consent, Ready }

        // ---- metrics (canvas units of the 1080 x 1920 reference) ----
        const float Side = 20f, TopY = 20f, TopRowH = 150f, BottomPad = 12f;
        const float FrameImgW = 794f, FrameTopImgH = 300f;
        /// <summary>Tab height: at least the 48 dp touch minimum on this screen density.</summary>
        static float TabH { get { return Mathf.Max(120f, UiKit.MinTouchRef); } }
        const float RowH = 100f, RowGap = 8f, PadTop = 30f, PadSide = 6f, TabGap = 8f;
        const float CapX = 26f, RankBoxW = 80f, PinnedRankW = 150f, AvatarSize = 70f, AvatarGap = 6f, YouTagW = 110f, YouTagH = 52f;
        const float CrownTextW = 190f, CrownIconW = 66f, CrownIconH = 55f, RightPad = 30f;
        const float NameRightReserve = RightPad + CrownTextW + 4f + CrownIconW + 8f;

        static readonly Color Ink = UiKit.PanelInk;
        static readonly Color GlowColor = new Color(1f, 0.93f, 0.35f, 1f), GlowSoft = new Color(1f, 0.93f, 0.35f, 0.55f);
        static readonly string[] PlateNames = { "lb_row_gold", "lb_row_silver", "lb_row_bronze", "lb_row_green", "lb_row_grey", "lb_row_you" };
        static readonly string[] BadgeNames = { "lb_medal_gold", "lb_medal_silver", "lb_medal_bronze", "lb_badge_green", "lb_badge_grey" };

        /// <summary>One pooled row: the same shape serves a list row and the pinned own card.</summary>
        sealed class Row
        {
            public GameObject Go;
            public RectTransform Rt, NameRt, TagRt;
            public Image Back, Medal, Photo, Glow, GlowWide;
            public Text Rank, Name, Crowns, Letter, Tag;
            public GameObject TagGo;
            public bool Pinned, IsMe;
            public int PlateIdx = -1;
            public string Url = "";
        }

        readonly RectTransform screen;
        readonly ProgressService progress;
        readonly ArtCatalog art;
        readonly Host host;

        RectTransform root, body, content, viewport;
        ScrollRect scroll;
        Text countdown;
        int board, firstShown = -1, poolSize, positionedBoard = -1, ownIdx = -1;
        float width, height, lastW = -1f, lastH = -1f, lastScrollY, vpH, bodyW, bodyH, frameScale = 1f;
        bool loading, loadFailed, signingIn, prizesOpen, consentAsked, pinnedShown;
        readonly List<Row> pool = new List<Row>();
        Row ownRow;
        BoardData data;
        string[] rankText = new string[0], crownText = new string[0], initialText = new string[0], nameText = new string[0];
        string pinnedRankText = "";
        Action hubHandler;

        public RectTransform Root { get { return root; } }
        /// <summary>0 Overall, 1 Regions, 2 Endless.</summary>
        public int Board { get { return board; } }
        public bool PrizesOpen { get { return prizesOpen; } }
        public int PooledRows { get { return pool.Count; } }
        public bool IsLoading { get { return loading; } }
        public bool LoadFailed { get { return loadFailed; } }
        public State Current { get { return ComputeState(); } }
        /// <summary>The rows of the board on screen (null before the first answer).</summary>
        public BoardData Data { get { return data; } }
        /// <summary>The pinned own card is on screen (the player is not among the loaded rows).</summary>
        public bool PinnedShown { get { return pinnedShown; } }
        /// <summary>Text of the pinned card's rank ("#348", "--"), empty when no card is shown.</summary>
        public string PinnedRankText { get { return pinnedShown ? pinnedRankText : ""; } }
        /// <summary>Index of the player's own row in the loaded board (-1 when not in it).</summary>
        public int OwnIndex { get { return ownIdx; } }
        /// <summary>The list scroll offset in canvas units (0 = top).</summary>
        public float ScrollOffset { get { return content != null ? content.anchoredPosition.y : 0f; } }

        public LeaderboardScreen(RectTransform screen, ProgressService progress, ArtCatalog art, Host host)
        {
            this.screen = screen; this.progress = progress; this.art = art; this.host = host;
        }

        public static State StateFor(bool live, bool googleSignedIn, bool firebaseReady, bool consent)
        {
            if (!live) return State.ComingSoon;
            if (!googleSignedIn || !firebaseReady) return State.SignIn;
            if (!consent) return State.Consent;
            return State.Ready;
        }

        State ComputeState()
        {
            return StateFor(LeaderboardHub.Enabled, LeaderboardHub.IsGoogleSignedIn, LeaderboardHub.IsSignedIn, LeaderboardHub.HasConsent);
        }

        // ---------------- build ----------------

        public void Build(int boardIndex)
        {
            board = Mathf.Clamp(boardIndex, 0, 2);
            root = UiKit.Rect(screen, "LeaderboardRoot");
            UiKit.Stretch(root);
            AvatarCache.Trim();
            data = LeaderboardHub.CachedBoard(LbBoards.All[board]);
            hubHandler = OnHubChanged;
            LeaderboardHub.Changed += hubHandler;
            var tick = root.gameObject.AddComponent<LeaderboardTicker>();
            tick.OnSecond = OnSecond;
            var fit = root.gameObject.AddComponent<LeaderboardFitter>();
            fit.OnSize = (w, h) => { if (Mathf.Abs(w - lastW) > 0.5f || Mathf.Abs(h - lastH) > 0.5f) Layout(w, h); };
            Layout(screen.rect.width > 1f ? screen.rect.width : 1080f, screen.rect.height > 1f ? screen.rect.height : 1920f);
            if (ComputeState() == State.Ready) LoadData(false);
            else if (ComputeState() == State.Consent && LeaderboardHub.ShouldAskConsent && !consentAsked) { consentAsked = true; if (host.ShowConsent != null) host.ShowConsent(); }
        }

        bool Alive { get { return root != null; } }

        void OnHubChanged()
        {
            if (!Alive) { LeaderboardHub.Changed -= hubHandler; return; }
            data = LeaderboardHub.CachedBoard(LbBoards.All[board]) ?? data;
            Layout(width, height);
        }

        void Layout(float w, float h)
        {
            width = w; height = h; lastW = w; lastH = h;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform c = root.GetChild(i);
                c.gameObject.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(c.gameObject); else UnityEngine.Object.DestroyImmediate(c.gameObject);
            }
            pool.Clear(); ownRow = null; content = null; viewport = null; scroll = null; countdown = null; firstShown = -1; pinnedShown = false;
            State st = ComputeState();
            if (st == State.Ready) BuildTopRow();
            BuildFrame(st);
        }

        // ---------------- uniform scaling helpers ----------------

        /// <summary>Stadium plates (rows, tabs, pills) slice only left and right: the rounded caps are drawn at the same scale as the height, only the flat middle stretches.</summary>
        static void CapsFollowHeight(Image img, float rectH)
        {
            img.type = Image.Type.Sliced;
            img.preserveAspect = false;
            Sprite sp = img.sprite;
            if (sp == null) return;
            img.pixelsPerUnitMultiplier = Mathf.Max(0.2f, sp.rect.height / Mathf.Max(1f, rectH));
        }

        static Image Painted(Transform parent, string name, string sprite, bool preserve = true)
        {
            Image img = UiKit.AddImage(parent, name, ToyKit.Get(sprite), Color.white);
            img.preserveAspect = preserve;
            return img;
        }

        // ---------------- top row: week timer and Prizes ----------------

        void BuildTopRow()
        {
            bool lefty = GameSettings.LeftHanded;
            float rowCentre = TopY + TopRowH * 0.5f;
            float prizeSize = Mathf.Max(128f, UiKit.MinTouchRef);
            float pillH = Mathf.Max(100f, RewardsScreen.LineH(52) + 20f);
            float pillW = 320f + (UiKit.TextScale - 1f) * 170f;

            Button prizes = UiKit.AddButton(root, "PrizesButton", "", ToyKit.Get("lb_plate_round"), Color.white, new Vector2(prizeSize, prizeSize), OpenPrizes, 44);
            prizes.GetComponent<A11yLabel>().Text = "Prizes";   // icon only, no text
            var brt = (RectTransform)prizes.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(lefty ? 0f : 1f, 1f);
            brt.pivot = new Vector2(lefty ? 0f : 1f, 0.5f);
            brt.anchoredPosition = new Vector2(lefty ? 190f : -Side, -rowCentre);
            brt.sizeDelta = new Vector2(prizeSize, prizeSize);
            Image pimg = prizes.GetComponent<Image>();
            pimg.type = Image.Type.Simple; pimg.preserveAspect = true;
            Image gift = Painted(prizes.transform, "Gift", "lb_icon_gift");
            gift.rectTransform.anchorMin = new Vector2(0.2f, 0.2f); gift.rectTransform.anchorMax = new Vector2(0.8f, 0.8f);
            gift.rectTransform.offsetMin = gift.rectTransform.offsetMax = Vector2.zero;

            RectTransform pill = UiKit.Rect(root, "TimerPill");
            pill.anchorMin = pill.anchorMax = new Vector2(lefty ? 1f : 0f, 1f);
            pill.pivot = new Vector2(lefty ? 1f : 0f, 0.5f);
            pill.anchoredPosition = new Vector2(lefty ? -Side : 190f, -rowCentre);
            pill.sizeDelta = new Vector2(pillW, pillH);
            Image pbg = UiKit.AddImage(pill, "Plate", ToyKit.Get("lb_pill_timer"), Color.white);
            CapsFollowHeight(pbg, pillH);
            UiKit.Stretch(pbg.rectTransform);
            float iconS = pillH * 0.66f;
            Image clock = Painted(pill, "Clock", "lb_icon_clock");
            UiKit.Place(clock.rectTransform, new Vector2(0f, 0.5f), new Vector2(pillH * 0.34f, 0f), new Vector2(iconS, iconS));
            countdown = Label(pill, "Countdown", "", 52, Color.white, TextAnchor.MiddleCenter, true, true);
            var crt = countdown.rectTransform;
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
            crt.offsetMin = new Vector2(pillH * 0.34f + iconS + 10f, 6f); crt.offsetMax = new Vector2(-pillH * 0.36f, -6f);
            RefreshCountdown();
        }

        // ---------------- the frame ----------------

        void BuildFrame(State st)
        {
            float frameW = Mathf.Min(width - 2f * Side, 940f);
            frameScale = frameW / FrameImgW;
            float topH = FrameTopImgH * frameScale;
            float frameTop = TopY + TopRowH;
            float frameH = Mathf.Max(300f, height - frameTop - BottomPad);
            RectTransform frame = UiKit.Rect(root, "Frame");
            UiKit.Place(frame, new Vector2(0.5f, 1f), new Vector2(0f, -frameTop), new Vector2(frameW, frameH));

            Image bodyImg = UiKit.AddImage(frame, "FrameBody", ToyKit.Get("lb_frame_body"), Color.white);
            bodyImg.type = Image.Type.Sliced; bodyImg.preserveAspect = false;
            bodyImg.pixelsPerUnitMultiplier = 1f / frameScale;
            bodyImg.rectTransform.anchorMin = Vector2.zero; bodyImg.rectTransform.anchorMax = Vector2.one;
            bodyImg.rectTransform.offsetMin = Vector2.zero; bodyImg.rectTransform.offsetMax = new Vector2(0f, -topH);
            Image topImg = Painted(frame, "FrameTop", "lb_frame_top");
            UiKit.Place(topImg.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(frameW, topH));

            Text t = Label(frame, "Title", "Leaderboard", 64, Ink, TextAnchor.MiddleCenter, true, true);
            var trt = t.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 1f); trt.pivot = new Vector2(0f, 1f);
            trt.anchoredPosition = new Vector2(165f * frameScale, -34f * frameScale);
            trt.sizeDelta = new Vector2(464f * frameScale, 116f * frameScale);

            float insetX = 56f * frameScale + 4f, insetB = 56f * frameScale + 8f;
            bodyW = frameW - 2f * insetX;
            bodyH = frameH - topH - insetB - 4f;
            body = UiKit.Rect(frame, "Body");
            body.anchorMin = Vector2.zero; body.anchorMax = Vector2.one;
            body.offsetMin = new Vector2(insetX, insetB); body.offsetMax = new Vector2(-insetX, -(topH + 4f));

            switch (st)
            {
                case State.ComingSoon:
                    StateCard("Weekly leaderboards", "The weekly crown leaderboards are coming soon. Keep climbing: your crowns will count.", null, null);
                    return;
                case State.SignIn:
                    StateCard("Play for the crown", "Sign in with Google Play Games to join the weekly leaderboards and win prizes. Guests do not appear on the boards.", signingIn ? "Signing in..." : "Sign in", signingIn ? (Action)null : OnSignInTapped);
                    return;
                case State.Consent:
                    StateCard("Join the weekly leaderboards", "Choose to appear on the boards. Your name and profile picture are shown to other players.", "Join leaderboards", () => { if (host.ShowConsent != null) host.ShowConsent(); });
                    return;
            }
            BuildTabs();
            ResolveOwn();
            BuildList(TabH + 10f);
            BuildPinned();
        }

        void OnSignInTapped()
        {
            if (signingIn || host.SignIn == null) return;
            signingIn = true;
            Layout(width, height);
            host.SignIn(ok =>
            {
                signingIn = false;
                if (!Alive) return;
                Layout(width, height);
                if (ComputeState() == State.Ready) LoadData(true);
                else if (ComputeState() == State.Consent && LeaderboardHub.ShouldAskConsent && !consentAsked) { consentAsked = true; if (host.ShowConsent != null) host.ShowConsent(); }
            });
        }

        // ---- a centred card for the states without a board ----

        void StateCard(string title, string text, string button, Action action)
        {
            float cardW = Mathf.Min(760f, bodyW - 16f);
            float innerW = cardW - 64f;
            RectTransform card = UiKit.Rect(body, "Card");
            UiKit.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(cardW, 600f));
            card.pivot = new Vector2(0.5f, 0.5f);
            Theme.CardBacking(card);
            float y = 36f;
            Text t = Label(card, "CardTitle", title, 60, Ink, TextAnchor.UpperCenter, true, false);
            float th = RewardsScreen.LineH(60) * 2f;
            UiKit.Place((RectTransform)t.transform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(innerW, th));
            t.resizeTextForBestFit = true; t.resizeTextMinSize = Mathf.RoundToInt(UiKit.MinBodyTextPx); t.resizeTextMaxSize = t.fontSize;
            y += th + 16f;
            Text b = UiKit.AddText(card, "CardText", text, 44, Ink, TextAnchor.UpperCenter);
            UiKit.OnPanel(b);
            b.horizontalOverflow = HorizontalWrapMode.Wrap; b.verticalOverflow = VerticalWrapMode.Overflow;
            UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(innerW, 100f));
            float bh = Mathf.Ceil(b.preferredHeight) + 8f;
            ((RectTransform)b.transform).sizeDelta = new Vector2(innerW, bh);
            y += bh + 28f;
            if (button != null)
            {
                float btnH = Mathf.Max(130f, UiKit.MinTouchRef);
                float btnW = Mathf.Min(620f, innerW);
                Button btn = UiKit.AddButton(card, "CardButton", button, ToyKit.Button(action != null ? 1 : 0), Color.white, new Vector2(btnW, btnH), action, 46);
                btn.interactable = action != null;
                UiKit.Place((RectTransform)btn.transform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(btnW, btnH));
                y += btnH + 36f;
            }
            else y += 8f;
            card.sizeDelta = new Vector2(cardW, y);
        }

        // ---- tabs ----

        void BuildTabs()
        {
            float th = TabH;
            RectTransform bar = UiKit.Rect(body, "Tabs");
            bar.anchorMin = new Vector2(0f, 1f); bar.anchorMax = new Vector2(1f, 1f); bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(0f, -th); bar.offsetMax = new Vector2(0f, 0f);
            float tw = (bodyW - 2f * TabGap) / 3f;
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                bool on = i == board;
                Button b = UiKit.AddButton(bar, "Tab" + LbBoards.Titles[i], LbBoards.Titles[i], ToyKit.Get(on ? "lb_tab_on" : "lb_tab_off"), Color.white, new Vector2(tw, th), () => SelectBoard(idx), 46);
                b.GetComponent<A11yLabel>().Text = LbBoards.Titles[i] + " board" + (on ? ", selected" : "");
                var rt = (RectTransform)b.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(i * (tw + TabGap), 0f);
                rt.sizeDelta = new Vector2(tw, th);
                CapsFollowHeight(b.GetComponent<Image>(), th);
                Text lt = b.GetComponentInChildren<Text>();
                if (lt != null)
                {
                    lt.color = on ? Color.white : Ink;
                    var o = lt.GetComponent<Outline>();
                    if (o != null) o.enabled = on;
                    lt.resizeTextForBestFit = true; lt.resizeTextMinSize = Mathf.RoundToInt(UiKit.MinBodyTextPx); lt.resizeTextMaxSize = lt.fontSize;
                    lt.horizontalOverflow = HorizontalWrapMode.Wrap; lt.verticalOverflow = VerticalWrapMode.Truncate;
                    var lr = lt.rectTransform;
                    lr.offsetMin = new Vector2(12f, 4f); lr.offsetMax = new Vector2(-12f, -4f);
                }
            }
        }

        /// <summary>Shows another board (tab tap).</summary>
        public void SelectBoard(int index)
        {
            index = Mathf.Clamp(index, 0, 2);
            if (index == board) return;
            board = index;
            loadFailed = false;
            positionedBoard = -1;
            data = LeaderboardHub.CachedBoard(LbBoards.All[board]);
            Layout(width, height);
            LoadData(false);
        }

        // ---------------- own standing ----------------

        string MyPid { get { return LeaderboardHub.MyWeek != null ? (LeaderboardHub.MyWeek.Pid ?? "") : ""; } }

        BoardRank MyRank { get { return LeaderboardHub.MyWeek != null ? LeaderboardHub.MyWeek.RankOf(LbBoards.All[board]) : null; } }

        /// <summary>Finds the own row in the loaded board and decides whether the pinned card is needed (the player is not among the loaded rows but has a rank or crowns).</summary>
        void ResolveOwn()
        {
            ownIdx = -1;
            string mine = MyPid;
            if (data != null && mine.Length > 0)
                for (int i = 0; i < data.Entries.Count; i++) if (data.Entries[i].Pid == mine) { ownIdx = i; break; }
            BoardRank r = MyRank;
            pinnedShown = ownIdx < 0 && r != null && (r.Rank > 0 || r.Crowns > 0);
            pinnedRankText = r != null && r.Rank > 0 ? "#" + r.Rank.ToString("N0", CultureInfo.InvariantCulture) : "--";
        }

        // ---------------- the pooled list ----------------

        void BuildList(float topOffset)
        {
            float bottomReserve = pinnedShown ? RowH + 12f : 0f;
            vpH = Mathf.Max(0f, bodyH - topOffset - bottomReserve);
            viewport = UiKit.Rect(body, "Viewport");
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(0f, bottomReserve);
            viewport.offsetMax = new Vector2(0f, -topOffset);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0.001f);

            content = UiKit.Rect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            int n = data != null ? data.Entries.Count : 0;
            float contentH = PadTop + n * (RowH + RowGap) + 8f;
            content.sizeDelta = new Vector2(0f, contentH);
            PrepareStrings();

            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = GameSettings.ReducedMotion ? ScrollRect.MovementType.Clamped : ScrollRect.MovementType.Elastic;
            scroll.inertia = !GameSettings.ReducedMotion;
            scroll.scrollSensitivity = 40f;

            // opening position: the own row centred (ranks 1 to 10 stay at the top); an earlier position of this board is kept across rebuilds
            float y = 0f;
            if (positionedBoard == board) y = lastScrollY;
            else if (n > 0)
            {
                if (ownIdx >= 10) y = PadTop + ownIdx * (RowH + RowGap) + RowH * 0.5f - vpH * 0.5f;
                positionedBoard = board;
            }
            y = Mathf.Clamp(y, 0f, Mathf.Max(0f, contentH - vpH));
            content.anchoredPosition = new Vector2(0f, y);
            lastScrollY = y;
            scroll.onValueChanged.AddListener(OnScrolled);

            poolSize = Mathf.Min(Mathf.Max(n, 0), Mathf.CeilToInt(vpH / (RowH + RowGap)) + 3);
            for (int i = 0; i < poolSize; i++) pool.Add(MakeRow(content, "Row" + i, false));

            if (n == 0) BuildListMessage();
            BindPool(true);
        }

        void BuildListMessage()
        {
            string msg = loadFailed ? "Can't reach the leaderboard. Check your connection and try again." : loading ? "Loading the board..." : "No scores yet this week. Be the first to climb!";
            Text t = UiKit.AddText(viewport, "Message", msg, 48, Ink, TextAnchor.MiddleCenter);
            UiKit.OnPanel(t);
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            var rt = (RectTransform)t.transform;
            rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(1f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(24f, -RewardsScreen.LineH(48) * 2f); rt.offsetMax = new Vector2(-24f, RewardsScreen.LineH(48) * 2f);
            if (loadFailed)
            {
                float btnH = Mathf.Max(130f, UiKit.MinTouchRef);
                Button retry = UiKit.AddButton(viewport, "RetryButton", "Retry", ToyKit.Button(1), Color.white, new Vector2(420f, btnH), () => { loadFailed = false; loading = true; Layout(width, height); LoadData(true); }, 46);
                UiKit.Place((RectTransform)retry.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -RewardsScreen.LineH(48) * 2f - btnH * 0.6f), new Vector2(420f, btnH));
            }
        }

        float RowW { get { return bodyW - 2f * PadSide; } }

        void PrepareStrings()
        {
            int n = data != null ? data.Entries.Count : 0;
            rankText = new string[n]; crownText = new string[n]; initialText = new string[n]; nameText = new string[n];
            for (int i = 0; i < n; i++)
            {
                BoardRow r = data.Entries[i];
                rankText[i] = RankString(r.Rank);
                crownText[i] = CrownString(r.Crowns);
                initialText[i] = InitialOf(r.Name);
                nameText[i] = FitName(r.Name, i == ownIdx, false);
            }
        }

        static string RankString(int rank)
        {
            if (rank <= 0) return "-";
            if (rank > 999) return "999+";
            return rank.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Crowns with thousands separators; from ten million on "12.3M" / "1.2B" so the number never wraps.</summary>
        public static string CrownString(long v)
        {
            if (v < 0) v = 0;
            if (v < 10000000L) return v.ToString("N0", CultureInfo.InvariantCulture);
            if (v < 1000000000L) return (v / 1000000.0).ToString("0.#", CultureInfo.InvariantCulture) + "M";
            return (v / 1000000000.0).ToString("0.#", CultureInfo.InvariantCulture) + "B";
        }

        float NameX(bool pinned, bool tag)
        {
            float x = pinned ? CapX + PinnedRankW + AvatarGap + AvatarSize + 10f : CapX + RankBoxW + AvatarGap + AvatarSize + 10f;
            return tag ? x + YouTagW + 6f : x;
        }

        /// <summary>A name that fits one line of the name slot at the current text size (cut with three dots): the layout audit counts the full-size text, so a long name must not run into the next slot.</summary>
        string FitName(string name, bool tag, bool pinned)
        {
            string n = string.IsNullOrEmpty(name) ? "Player" : name.Trim();
            if (n.Length == 0) n = "Player";
            float boxW = Mathf.Max(60f, RowW - NameX(pinned, tag) - NameRightReserve);
            float fontPx = Mathf.RoundToInt(Mathf.Max(42f, UiKit.MinBodyTextPx) * UiKit.TextScale);
            int perLine = Mathf.Max(5, Mathf.FloorToInt(boxW / (fontPx * 0.52f)));
            int max = perLine - 1;
            if (n.Length <= max) return n;
            int cut = Mathf.Max(1, max - 3);
            if (char.IsHighSurrogate(n[cut - 1])) cut--;
            return n.Substring(0, Mathf.Max(1, cut)).TrimEnd() + "...";
        }

        static string InitialOf(string name)
        {
            string n = (name ?? "").Trim();
            return n.Length == 0 ? "?" : n.Substring(0, 1).ToUpperInvariant();
        }

        void OnScrolled(Vector2 v)
        {
            if (content != null) lastScrollY = content.anchoredPosition.y;
            BindPool(false);
        }

        void BindPool(bool force)
        {
            if (content == null || data == null) return;
            const float pitch = RowH + RowGap;
            int first = Mathf.Max(0, Mathf.FloorToInt((content.anchoredPosition.y - PadTop) / pitch) - 1);
            int maxFirst = Mathf.Max(0, data.Entries.Count - pool.Count);
            if (first > maxFirst) first = maxFirst;
            if (!force && first == firstShown) return;
            firstShown = first;
            for (int k = 0; k < pool.Count; k++)
            {
                int idx = first + k;
                Row r = pool[k];
                if (idx >= data.Entries.Count) { r.Go.SetActive(false); continue; }
                BoardRow e = data.Entries[idx];
                r.Go.SetActive(true);
                float y = -(PadTop + idx * pitch);
                r.Rt.offsetMin = new Vector2(PadSide, y - RowH);
                r.Rt.offsetMax = new Vector2(-PadSide, y);
                bool isMe = idx == ownIdx;
                Bind(r, e.Rank, rankText[idx], nameText[idx], crownText[idx], e.Avatar, initialText[idx], isMe);
                if (isMe) r.Rt.SetAsLastSibling();   // the glow outline is drawn over the neighbouring rows
            }
        }

        // ---------------- pinned own card ----------------

        void BuildPinned()
        {
            if (!pinnedShown) return;
            ownRow = MakeRow(body, "OwnCard", true);
            ownRow.Rt.anchorMin = new Vector2(0f, 0f); ownRow.Rt.anchorMax = new Vector2(1f, 0f); ownRow.Rt.pivot = new Vector2(0.5f, 0f);
            ownRow.Rt.offsetMin = new Vector2(PadSide, 4f); ownRow.Rt.offsetMax = new Vector2(-PadSide, 4f + RowH);
            BoardRank rank = MyRank;
            LbProfile p = LeaderboardHub.Profile;
            string name = string.IsNullOrEmpty(p.DisplayName) ? "You" : p.DisplayName;
            Bind(ownRow, 0, pinnedRankText, FitName(name, false, true), CrownString(rank != null ? rank.Crowns : 0L), p.AvatarUrl, InitialOf(name), true);
        }

        // ---------------- one row ----------------

        Row MakeRow(RectTransform parent, string name, bool pinned)
        {
            var r = new Row { Pinned = pinned };
            r.Rt = UiKit.Rect(parent, name);
            r.Rt.anchorMin = new Vector2(0f, 1f); r.Rt.anchorMax = new Vector2(1f, 1f); r.Rt.pivot = new Vector2(0.5f, 1f);
            r.Go = r.Rt.gameObject;

            // glow outline of the own row (a soft wide halo and a bright thin ring), only active on the own row
            r.GlowWide = AddGlow(r.Rt, "Backdrop", 14f, GlowSoft);
            r.Glow = AddGlow(r.Rt, "Backdrop", 7f, GlowColor);

            r.Back = UiKit.AddImage(r.Rt, "Plate", ToyKit.Get(pinned ? "lb_row_you" : "lb_row_grey"), Color.white);
            r.Back.raycastTarget = false;
            CapsFollowHeight(r.Back, RowH);
            UiKit.Stretch(r.Back.rectTransform);

            float rankW = pinned ? PinnedRankW : RankBoxW;
            if (!pinned)
            {
                r.Medal = UiKit.AddImage(r.Rt, "Medal", null, Color.white);
                r.Medal.preserveAspect = true;
                r.Medal.color = Color.white;
                Anchor(r.Medal.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(CapX + rankW * 0.5f, 0f), new Vector2(rankW, 92f));
            }
            r.Rank = Label(r.Rt, "RankText", "", pinned ? 48 : 44, Ink, TextAnchor.MiddleCenter, true, true);
            OneLine(r.Rank);
            r.Rank.resizeTextForBestFit = true;
            r.Rank.resizeTextMinSize = Mathf.RoundToInt(UiKit.MinBodyTextPx); r.Rank.resizeTextMaxSize = r.Rank.fontSize;
            r.Rank.horizontalOverflow = HorizontalWrapMode.Wrap; r.Rank.verticalOverflow = VerticalWrapMode.Truncate;
            Anchor(r.Rank.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(CapX + rankW * 0.5f, 0f), pinned ? new Vector2(PinnedRankW, 64f) : new Vector2(54f, 50f));

            float avX = CapX + rankW + AvatarGap;
            Image av = UiKit.AddImage(r.Rt, "Avatar", Theme.RoundSprite, new Color(0.62f, 0.70f, 0.90f, 1f));
            av.type = Image.Type.Sliced; av.preserveAspect = false;
            Anchor(av.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(avX, 0f), new Vector2(AvatarSize, AvatarSize));
            av.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            r.Letter = Label(av.transform, "Letter", "?", 48, Ink, TextAnchor.MiddleCenter, true, true);
            UiKit.Stretch(r.Letter.rectTransform);
            r.Photo = UiKit.AddImage(av.transform, "Photo", null, Color.white);
            r.Photo.preserveAspect = true;
            r.Photo.color = Color.white;
            UiKit.Stretch(r.Photo.rectTransform);
            r.Photo.gameObject.SetActive(false);

            if (!pinned)
            {
                RectTransform tag = UiKit.Rect(r.Rt, "YouTag");
                Anchor(tag, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(NameX(false, false) - 4f, 0f), new Vector2(YouTagW, YouTagH));
                Image tbg = UiKit.AddImage(tag, "Plate", ToyKit.Get("lb_pill_score"), Color.white);
                CapsFollowHeight(tbg, YouTagH);
                UiKit.Stretch(tbg.rectTransform);
                r.Tag = Label(tag, "TagText", "YOU", 42, Ink, TextAnchor.MiddleCenter, true, true);
                UiKit.Stretch(r.Tag.rectTransform);
                r.Tag.rectTransform.offsetMin = new Vector2(4f, 2f); r.Tag.rectTransform.offsetMax = new Vector2(-4f, -2f);
                r.Tag.horizontalOverflow = HorizontalWrapMode.Overflow;
                r.TagRt = tag; r.TagGo = tag.gameObject;
                r.TagGo.SetActive(false);
            }

            r.Name = Label(r.Rt, "NameText", "", 42, Ink, TextAnchor.MiddleLeft, false, true);
            OneLine(r.Name);
            r.NameRt = r.Name.rectTransform;
            r.NameRt.anchorMin = new Vector2(0f, 0f); r.NameRt.anchorMax = new Vector2(1f, 1f);
            r.NameRt.offsetMin = new Vector2(NameX(pinned, false), 6f); r.NameRt.offsetMax = new Vector2(-NameRightReserve, -6f);

            Image ci = Painted(r.Rt, "CrownIcon", "lb_icon_crown");
            Anchor(ci.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-(RightPad + CrownTextW + 4f), 0f), new Vector2(CrownIconW, CrownIconH));

            r.Crowns = Label(r.Rt, "CrownsText", "", 52, Ink, TextAnchor.MiddleRight, true, true);
            OneLine(r.Crowns);
            r.Crowns.resizeTextForBestFit = true; r.Crowns.resizeTextMinSize = Mathf.RoundToInt(UiKit.MinBodyTextPx); r.Crowns.resizeTextMaxSize = r.Crowns.fontSize;
            r.Crowns.horizontalOverflow = HorizontalWrapMode.Wrap; r.Crowns.verticalOverflow = VerticalWrapMode.Truncate;
            Anchor(r.Crowns.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-RightPad, 0f), new Vector2(CrownTextW, RowH - 20f));
            return r;
        }

        static Image AddGlow(RectTransform row, string name, float grow, Color color)
        {
            Image g = ToyKit.Sliced(row, name, UiKit.PillSprite, RowH * 0.5f + grow);
            g.color = color; g.raycastTarget = false;
            g.rectTransform.anchorMin = Vector2.zero; g.rectTransform.anchorMax = Vector2.one;
            g.rectTransform.offsetMin = new Vector2(-grow, -grow); g.rectTransform.offsetMax = new Vector2(grow, grow);
            g.gameObject.SetActive(false);
            return g;
        }

        /// <summary>Grows the popup title box to the full-size text height (the layout audit counts the full-size text) and returns the y where the content may start.</summary>
        public static float FitPopupTitle(RectTransform panel)
        {
            Transform tt = panel.Find("Title");
            Text t = tt != null ? tt.GetComponent<Text>() : null;
            if (t == null) return 190f;
            t.resizeTextForBestFit = false;
            t.fontSize = Mathf.RoundToInt(Mathf.Max(54f, UiKit.MinBodyTextPx) * UiKit.TextScale);   // fits one line for short titles, two for long ones
            float h = Mathf.Max(120f, Mathf.Ceil(t.preferredHeight) + 28f);
            var rt = t.rectTransform;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);
            return 52f + h + 12f;
        }

        /// <summary>Forces one line (no wrapping, no best fit): the full-size text height then equals one line and always fits the row.</summary>
        static void OneLine(Text t)
        {
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.resizeTextForBestFit = false;
        }

        static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        void Bind(Row r, int rank, string rankStr, string name, string crowns, string avatarUrl, string initial, bool me)
        {
            int plate = r.Pinned ? 5 : rank == 1 ? 0 : rank == 2 ? 1 : rank == 3 ? 2 : rank >= 4 && rank <= 10 ? 3 : 4;
            if (r.PlateIdx != plate)
            {
                r.PlateIdx = plate;
                Sprite ps = ToyKit.Get(PlateNames[plate]);
                r.Back.sprite = ps != null ? ps : UiKit.PillSprite;
                CapsFollowHeight(r.Back, RowH);
                bool light = plate != 2 && plate != 5;   // bronze and the blue own card get white text with the outline
                Color ink = light ? Ink : Color.white;
                StyleText(r.Rank, ink, !light);
                StyleText(r.Name, ink, !light);
                StyleText(r.Crowns, ink, !light);
                StyleText(r.Letter, Ink, false);
                if (!r.Pinned)
                {
                    Sprite medal = ToyKit.Get(BadgeNames[plate]);
                    r.Medal.sprite = medal;
                    r.Medal.gameObject.SetActive(medal != null);
                    bool isMedal = plate <= 2;
                    r.Rank.rectTransform.anchoredPosition = new Vector2(CapX + RankBoxW * 0.5f, isMedal ? -15f : 0f);
                    r.Rank.rectTransform.sizeDelta = isMedal ? new Vector2(46f, 46f) : new Vector2(60f, 50f);
                }
            }
            if (!r.Pinned && r.IsMe != me)
            {
                r.IsMe = me;
                r.Glow.gameObject.SetActive(me);
                r.GlowWide.gameObject.SetActive(me);
                r.TagGo.SetActive(me);
                r.NameRt.offsetMin = new Vector2(NameX(false, me), 6f);
            }
            UiKit.SetText(r.Rank, rankStr);
            UiKit.SetText(r.Name, name);
            UiKit.SetText(r.Crowns, crowns);
            UiKit.SetText(r.Letter, initial);
            string url = avatarUrl ?? "";
            if (r.Url != url)
            {
                r.Url = url;
                r.Photo.gameObject.SetActive(false);
                if (!r.Letter.gameObject.activeSelf) r.Letter.gameObject.SetActive(true);
            }
            if (url.Length > 0 && !r.Photo.gameObject.activeSelf)
            {
                Sprite have = AvatarCache.Peek(url);
                if (have != null) ShowPhoto(r, have);
                else
                {
                    Row row = r;
                    AvatarCache.Request(url, sp => { if (row.Photo != null && row.Url == url) ShowPhoto(row, sp); });
                }
            }
        }

        static void StyleText(Text t, Color c, bool outline)
        {
            if (t.color != c) t.color = c;
            Outline o = t.GetComponent<Outline>();
            if (o != null && o.enabled != outline) o.enabled = outline;
        }

        static void ShowPhoto(Row r, Sprite sp)
        {
            r.Photo.sprite = sp;
            r.Photo.gameObject.SetActive(true);
            r.Letter.gameObject.SetActive(false);
        }

        // ---------------- text helper ----------------

        /// <summary>A text on a plate or pill: no outline when dark ink on a light plate, shrinks to fit (never below 16 sp), never wraps a number mid-word.</summary>
        static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor anchor, bool display, bool singleLine)
        {
            Text t = UiKit.AddText(parent, name, text, size, color, anchor, display);
            if (color.r < 0.5f) { var o = t.GetComponent<Outline>(); if (o != null) o.enabled = false; }   // dark ink on light plates needs no outline
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.RoundToInt(UiKit.MinBodyTextPx);
            t.resizeTextMaxSize = t.fontSize;
            return t;
        }

        // ---------------- data ----------------

        void LoadData(bool force)
        {
            string id = LbBoards.All[board];
            loading = LeaderboardHub.CachedBoard(id) == null;
            LeaderboardHub.RefreshMyWeek(false, null, force);
            LeaderboardHub.LoadBoard(id, b =>
            {
                loading = false;
                if (!Alive) return;
                if (b == null && LeaderboardHub.CachedBoard(id) == null && !loadFailed) { loadFailed = true; Layout(width, height); }
            }, force);
        }

        // ---------------- timer ----------------

        int lastRemaining = -1;

        void OnSecond()
        {
            int secs = (int)LeaderboardHub.WeekRemaining.TotalSeconds;
            if (lastRemaining >= 0 && secs > lastRemaining + 5)
            {
                // a new week started while the screen was open: read the fresh (empty) boards
                lastRemaining = secs;
                if (ComputeState() == State.Ready) { data = null; positionedBoard = -1; Layout(width, height); LoadData(true); }
                return;
            }
            lastRemaining = secs;
            RefreshCountdown();
        }

        /// <summary>"2d 4h", "5h 12m", "9m 30s": the short time left of the week.</summary>
        public static string ShortCountdown(TimeSpan left)
        {
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;
            if (left.Days > 0) return left.Days.ToString(CultureInfo.InvariantCulture) + "d " + left.Hours.ToString(CultureInfo.InvariantCulture) + "h";
            if (left.Hours > 0) return left.Hours.ToString(CultureInfo.InvariantCulture) + "h " + left.Minutes.ToString(CultureInfo.InvariantCulture) + "m";
            return left.Minutes.ToString(CultureInfo.InvariantCulture) + "m " + left.Seconds.ToString(CultureInfo.InvariantCulture) + "s";
        }

        void RefreshCountdown()
        {
            if (countdown == null) return;
            UiKit.SetText(countdown, ShortCountdown(LeaderboardHub.WeekRemaining));
        }

        // ---------------- prizes popup ----------------

        /// <summary>Opens the prize table of the selected board (a modal popup with its own close X). It is laid out against the real overlay and safe area and rebuilt when they change; a body taller than the safe area scrolls.</summary>
        public void OpenPrizes()
        {
            if (host.NewOverlay == null) return;
            RectTransform overlay = host.NewOverlay();
            prizesOpen = true;
            BuildPrizes(overlay);
            float lw = overlay.rect.width, lh = overlay.rect.height;
            var fit = overlay.gameObject.AddComponent<LeaderboardFitter>();
            fit.OnSize = (w, h) =>
            {
                if (Mathf.Abs(w - lw) <= 0.5f && Mathf.Abs(h - lh) <= 0.5f) return;
                lw = w; lh = h;
                BuildPrizes(overlay);
            };
        }

        void BuildPrizes(RectTransform overlay)
        {
            for (int i = overlay.childCount - 1; i >= 0; i--)
            {
                Transform c = overlay.GetChild(i);
                c.gameObject.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(c.gameObject); else UnityEngine.Object.DestroyImmediate(c.gameObject);
            }
            string id = LbBoards.All[board];
            float ow = overlay.rect.width > 1f ? overlay.rect.width : 1080f, oh = overlay.rect.height > 1f ? overlay.rect.height : 1920f;
            Rect sa = UiKit.SafeAreaNormalized();
            float availH = oh * sa.height - 48f, availW = ow * sa.width - 2f * Side;
            float W = Mathf.Min(900f, Mathf.Max(520f, availW));
            const float pad = 40f, iconW = 84f, labelW = 190f, rowGap = 12f;
            float innerW = W - 2f * pad;
            RectTransform panel = UiKit.BuildPopupFrame(overlay, LbBoards.TitleOf(id) + " prizes", art, new Vector2(W, 900f), () => { prizesOpen = false; if (host.CloseOverlay != null) host.CloseOverlay(); });
            panel.anchoredPosition = new Vector2((sa.center.x - 0.5f) * ow, (sa.center.y - 0.5f) * oh);   // centred in the safe area
            float top = FitPopupTitle(panel);

            RectTransform vp = UiKit.Rect(panel, "Viewport");
            vp.anchorMin = Vector2.zero; vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(pad, pad); vp.offsetMax = new Vector2(-pad, -top);
            vp.gameObject.AddComponent<RectMask2D>();
            RectTransform pc = UiKit.Rect(vp, "Content");
            pc.anchorMin = new Vector2(0f, 1f); pc.anchorMax = new Vector2(1f, 1f); pc.pivot = new Vector2(0.5f, 1f);
            pc.anchoredPosition = Vector2.zero;

            BoardRank rank = MyRankFor(id);
            long limit = rank != null ? rank.EntryLimit : 0;
            string note = limit > 0
                ? "Top 10 are paid at the end of the week. Reach " + limit.ToString("N0", CultureInfo.InvariantCulture) + " crowns to qualify; ranks 1 to 3 need " + (limit * 3 / 2).ToString("N0", CultureInfo.InvariantCulture) + "."
                : "Top 10 are paid at the end of the week once you reach the entry limit of the board.";
            float y = 0f;
            float slack = RewardsScreen.LineH(44);
            Text nt = UiKit.AddText(pc, "PrizeNote", note, 44, Ink, TextAnchor.UpperCenter);
            UiKit.OnPanel(nt);
            nt.horizontalOverflow = HorizontalWrapMode.Wrap; nt.verticalOverflow = VerticalWrapMode.Overflow;
            UiKit.Place((RectTransform)nt.transform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(innerW, 100f));
            float nh = Mathf.Ceil(nt.preferredHeight) + slack;
            ((RectTransform)nt.transform).sizeDelta = new Vector2(innerW, nh);
            y += nh + 12f;

            if (limit > 0) y += BuildLimitBar(pc, rank, innerW, y) + 20f;

            string[,] rows = LbPrizeTable.Rows(id);
            float pw = innerW - iconW - labelW - 24f;
            for (int i = 0; i < 4; i++)
            {
                Text lt = UiKit.AddText(pc, "PrizeRank" + i, rows[i, 0], 46, Ink, TextAnchor.UpperLeft, true);
                UiKit.OnPanel(lt);
                lt.horizontalOverflow = HorizontalWrapMode.Wrap; lt.verticalOverflow = VerticalWrapMode.Overflow;
                Text pt = UiKit.AddText(pc, "PrizeItems" + i, rows[i, 1], 44, Ink, TextAnchor.UpperLeft);
                UiKit.OnPanel(pt);
                pt.horizontalOverflow = HorizontalWrapMode.Wrap; pt.verticalOverflow = VerticalWrapMode.Overflow;
                UiKit.Place((RectTransform)lt.transform, new Vector2(0.5f, 1f), new Vector2(-innerW * 0.5f + iconW + 12f + labelW * 0.5f, -y), new Vector2(labelW, 100f));
                UiKit.Place((RectTransform)pt.transform, new Vector2(0.5f, 1f), new Vector2(innerW * 0.5f - pw * 0.5f, -y), new Vector2(pw, 100f));
                float rh = Mathf.Max(Mathf.Ceil(lt.preferredHeight), Mathf.Ceil(pt.preferredHeight) + slack, slack * 2f, 92f) + 8f;
                ((RectTransform)lt.transform).sizeDelta = new Vector2(labelW, rh);
                ((RectTransform)pt.transform).sizeDelta = new Vector2(pw, rh);
                Image ic = Painted(pc, "PrizeBadge" + i, BadgeNames[i == 3 ? 3 : i]);
                UiKit.Place(ic.rectTransform, new Vector2(0.5f, 1f), new Vector2(-innerW * 0.5f + iconW * 0.5f, -y - rh * 0.5f + 46f), new Vector2(iconW, 92f));
                y += rh + rowGap;
            }
            pc.sizeDelta = new Vector2(0f, y);
            float panelH = Mathf.Min(top + y + pad, Mathf.Max(400f, availH));
            panel.sizeDelta = new Vector2(W, panelH);
            var sr = vp.gameObject.AddComponent<ScrollRect>();
            sr.viewport = vp; sr.content = pc;
            sr.horizontal = false; sr.vertical = true;
            sr.movementType = GameSettings.ReducedMotion ? ScrollRect.MovementType.Clamped : ScrollRect.MovementType.Elastic;
            sr.inertia = !GameSettings.ReducedMotion;
        }

        static BoardRank MyRankFor(string id)
        {
            return LeaderboardHub.MyWeek != null ? LeaderboardHub.MyWeek.RankOf(id) : null;
        }

        /// <summary>The entry-limit progress bar (crowns of the week against the limit) inside the Prizes popup; returns its height.</summary>
        static float BuildLimitBar(RectTransform parent, BoardRank rank, float innerW, float y)
        {
            float labelH = RewardsScreen.LineH(40);
            Text lab = UiKit.AddText(parent, "LimitLabel", "Your crowns this week", 40, Ink, TextAnchor.UpperLeft);
            UiKit.OnPanel(lab);
            lab.horizontalOverflow = HorizontalWrapMode.Wrap; lab.verticalOverflow = VerticalWrapMode.Overflow;
            UiKit.Place((RectTransform)lab.transform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(innerW, labelH));
            const float barH = 72f;
            RectTransform bar = UiKit.Rect(parent, "LimitBar");
            UiKit.Place(bar, new Vector2(0.5f, 1f), new Vector2(0f, -(y + labelH + 6f)), new Vector2(innerW, barH));
            Image track = ToyKit.Sliced(bar, "Backdrop", UiKit.PillSprite, barH * 0.5f);
            track.color = new Color(0.80f, 0.79f, 0.93f, 1f); track.raycastTarget = false;
            UiKit.Stretch(track.rectTransform);
            long crowns = rank.Crowns, limit = rank.EntryLimit;
            float frac = limit > 0 ? Mathf.Clamp01((float)((double)crowns / limit)) : 0f;
            if (frac > 0.001f)
            {
                Image fill = ToyKit.Sliced(bar, "Fill", UiKit.PillSprite, (barH - 12f) * 0.5f);
                fill.color = rank.ReachedLimit ? new Color(0.22f, 0.70f, 0.34f, 1f) : new Color(0.20f, 0.56f, 0.96f, 1f); fill.raycastTarget = false;
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = new Vector2(Mathf.Max(frac, 0.1f), 1f);
                fill.rectTransform.offsetMin = new Vector2(6f, 6f); fill.rectTransform.offsetMax = new Vector2(-6f, -6f);
            }
            string count = crowns.ToString("N0", CultureInfo.InvariantCulture) + " / " + limit.ToString("N0", CultureInfo.InvariantCulture);
            Text ct = Label(bar, "Count", count, 44, Color.white, TextAnchor.MiddleCenter, true, true);
            UiKit.Stretch(ct.rectTransform);
            return labelH + 6f + barH;
        }

        /// <summary>Closes the prize popup (tests).</summary>
        public void ClosePrizes()
        {
            prizesOpen = false;
            if (host.CloseOverlay != null) host.CloseOverlay();
        }

        // ---------------- helpers for the scene ----------------

        /// <summary>Tests: scrolls the list to the normalised position (1 = top, 0 = bottom).</summary>
        public void ScrollTo(float normalized)
        {
            if (scroll != null) scroll.verticalNormalizedPosition = Mathf.Clamp01(normalized);
            if (content != null) lastScrollY = content.anchoredPosition.y;
            BindPool(false);
        }

        /// <summary>Called when the screen is left (the meta scene destroys the root): drops the hub subscription.</summary>
        public void Dispose()
        {
            LeaderboardHub.Changed -= hubHandler;
        }
    }

    /// <summary>Rebuilds the screen when its rectangle changes (another aspect, a notch, a rotation). OnRectTransformDimensionsChange only reads the rectangle: no per-frame work.</summary>
    public sealed class LeaderboardFitter : MonoBehaviour
    {
        public Action<float, float> OnSize;

        void OnRectTransformDimensionsChange()
        {
            if (OnSize == null || !isActiveAndEnabled) return;
            var rt = (RectTransform)transform;
            if (rt.rect.width > 1f && rt.rect.height > 1f) OnSize(rt.rect.width, rt.rect.height);
        }
    }

    /// <summary>Calls <see cref="OnSecond"/> when the remaining whole seconds of the week change (once a second). Per frame: one DateTime read and an int compare.</summary>
    public sealed class LeaderboardTicker : MonoBehaviour
    {
        public Action OnSecond;
        int last = -1;

        void Update()
        {
            if (OnSecond == null) return;
            int s = (int)LeaderboardHub.WeekRemaining.TotalSeconds;
            if (s == last) return;
            last = s;
            OnSecond();
        }
    }
}
