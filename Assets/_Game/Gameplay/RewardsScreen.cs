using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// The Rewards screen (docs/26 section 4, "RewardsStyle"), tabs Main | Daily | Weekly in a footer with Back and Claim All.
    /// Main (Bug List 5 F1.a, docs/39): a PINNED scene header (scene_daily, like Daily) with a horizontally scrolling, snapping strip of all 30 region circles (1-5 playable, 6-30 "Coming Soon"),
    /// then the goal cards of the SELECTED region (the same compact cards as Daily and Weekly; the endless tiers come last under an "Endless" divider). Claim All on Main claims the selected region's ready goals.
    /// Daily and Weekly: a scene header with the reset timer and the points track (Daily, chests with thresholds) or the tasks done bar (Weekly), then the task cards.
    /// Cards (compact: 136 px, gap 10): cream (open), yellow with a big green Claim button as the reward plate (ready; the card itself claims too), green with a check (claimed); a dark reward plate at the right.
    /// A claim shows a SMALL ICON CARD (chips with amounts, hero faces, a green OK; closes after 4 s or on a tap), never a sentence.
    /// Files: this one (frame, tabs, claiming, the small reward card), RewardsScreen.Cards.cs (cards, plates, bars, hero faces), RewardsScreen.Headers.cs (Main region strip, scene headers, track).
    /// Menu screen only: no per-frame work except the once-a-second countdown text and the strip's snap glide (only while it moves).
    /// </summary>
    public sealed partial class RewardsScreen
    {
        /// <summary>What the screen needs from the meta scene: the dim overlay layer for the reward card and the way back. It never owns a canvas.</summary>
        public sealed class Host
        {
            public Func<RectTransform> NewOverlay;
            public Action CloseOverlay;
            public Action Back;
        }

        // ---- metrics (canvas units of the 1080 x 1920 reference) ----
        const float Side = 24f;
        // The Back arrow sits top left exactly like on every other screen (MetaController.AddBack: 150 px at 20 / -20) and the wallet row starts to its right at 190 px
        // (MetaController.AddPillRow: 190 .. -16, top 16, 112 tall). The list starts below both.
        public const float BackSize = 150f, BackX = 20f, BackTop = 20f;
        const float WalletTop = 16f, WalletH = 112f, WalletLeft = 190f, WalletRight = 16f;
        const float BottomPad = 16f, ClaimBtnH = 130f, TabH = 130f, TabGap = 14f;
        /// <summary>Top of the list / pinned Main header: under the Back arrow (the taller of Back and the wallet row) plus a gap.</summary>
        public const float HeaderH = BackTop + BackSize + 12f;
        /// <summary>Height of the dark footer bar (Claim All lives in it).</summary>
        public const float FooterH = BottomPad + ClaimBtnH + BottomPad;
        const int BodyPx = 44;

        // Main header stack (top to bottom): circles (selected one is 190 tall, starts at -14), 10 gap, name zone, gap (holds the pointer).
        // The name zone reserves two lines at the current text scale so a wrapped name can never reach the list (CUX-3).
        const float CircleBottom = 14f + 190f, NameGap = 10f, PanelGap = 44f;
        static float NameZone { get { return Mathf.Ceil(Mathf.Max(36f, UiKit.MinBodyTextPx) * UiKit.TextScale * 3.9f) + 8f; } }
        /// <summary>Height of the pinned Main header (the region strip) at the current text size.</summary>
        public static float CircleZone { get { return CircleBottom + NameGap + NameZone + PanelGap; } }

        // ---- colours ----
        static readonly Color FooterColor = new Color(0.10f, 0.13f, 0.32f, 1f);
        static readonly Color TrackDark = new Color(0.12f, 0.14f, 0.29f, 1f);
        static readonly Color BarBlue = new Color(0.20f, 0.60f, 1f, 1f);
        static readonly Color BarGreen = new Color(0.22f, 0.78f, 0.34f, 1f);
        static readonly Color BarGold = new Color(1f, 0.72f, 0.12f, 1f);

        readonly RectTransform screen;
        readonly ProgressService progress;
        readonly ContentRepository repo;
        readonly ArtCatalog art;
        readonly Host host;

        RectTransform root, body;
        RectTransform listViewport, mainHeader;
        RegionStripSnap stripSnap;
        GameObject claimAllGo;
        Button backButton;
        RewardsTicker ticker;
        Text timerText;
        int timerSeconds = -1;
        double lastRemaining;
        int selectedRegion;

        public Text CoinText { get; private set; }
        public Text GemText { get; private set; }
        public RewardTab Tab { get; private set; }
        /// <summary>The region whose goals the Main tab lists (1..30).</summary>
        public int SelectedRegion { get { return selectedRegion; } }
        /// <summary>The scrolling list of the current tab (tests scroll it or count its rows).</summary>
        public RectTransform ListContent { get; private set; }
        /// <summary>The card of the last <see cref="ShowClaimed"/> (null before the first one; tests read its chips).</summary>
        public RectTransform RewardCard { get; private set; }

        public RewardsScreen(RectTransform screen, ProgressService progress, ContentRepository repo, ArtCatalog art, Host host)
        {
            this.screen = screen;
            this.progress = progress;
            this.repo = repo;
            this.art = art;
            this.host = host;
        }

        static string Num(int n) { return n.ToString("N0", CultureInfo.InvariantCulture); }

        /// <summary>Short number for the small reward plates: 1,000 becomes 1K, 1,500 becomes 1.5K.</summary>
        static string Compact(int n)
        {
            if (n < 1000) return n.ToString(CultureInfo.InvariantCulture);
            double k = n / 1000.0;
            return k.ToString(k == Math.Floor(k) ? "0" : "0.#", CultureInfo.InvariantCulture) + "K";
        }

        static Text Head(Text t) { t.fontStyle = FontStyle.Italic; return t; }

        /// <summary>Height of one line of text at <paramref name="px"/> (raised to the 16 sp floor and scaled by the accessibility text size).</summary>
        public static float LineH(int px) { return Mathf.Ceil(Mathf.Round(Mathf.Max(px, UiKit.MinBodyTextPx) * UiKit.TextScale) * 1.22f); }

        /// <summary>Space under the list: the footer bar, then the tab row.</summary>
        public static float BottomReserve() { return FooterH + TabGap + TabH + TabGap; }

        // ---------------- build ----------------

        public void Build(RewardTab tab)
        {
            Tab = tab;
            int cur = progress.CurrentRegion().Index;
            selectedRegion = Mathf.Clamp(cur, 1, RegionGoalTable.PlayableRegions);
            root = UiKit.Rect(screen, "RewardsRoot");
            UiKit.Stretch(root);

            RectTransform wallet = UiKit.Rect(root, "Wallet");
            wallet.anchorMin = new Vector2(0f, 1f);
            wallet.anchorMax = new Vector2(1f, 1f);
            wallet.pivot = new Vector2(0.5f, 1f);
            wallet.offsetMin = new Vector2(WalletLeft, -(WalletTop + WalletH));
            wallet.offsetMax = new Vector2(-WalletRight, -WalletTop);
            var whl = wallet.gameObject.AddComponent<HorizontalLayoutGroup>();
            whl.spacing = 10f;
            whl.childControlWidth = whl.childControlHeight = true;
            whl.childForceExpandWidth = whl.childForceExpandHeight = true;
            CoinText = ToyKit.WalletPillFlex(wallet, "CoinsPill", art != null ? art.CoinIcon : null, Color.white, WalletH);
            GemText = ToyKit.WalletPillFlex(wallet, "GemsPill", art != null ? art.GemIcon : null, new Color(0.75f, 0.92f, 1f), WalletH);
            UiKit.SetText(CoinText, progress.Coins.ToString());
            UiKit.SetText(GemText, progress.Gems.ToString());

            // the blue back arrow, top left: same place and size as MetaController.AddBack on every other screen (it stays on every tab; Render keeps it on top)
            backButton = ToyKit.GlyphButton(root, "Back", "Back", 2, Glyphs.Left, new Vector2(BackSize, BackSize), () => { if (host != null && host.Back != null) host.Back(); });
            UiKit.Place((RectTransform)backButton.transform, new Vector2(0f, 1f), new Vector2(BackX, -BackTop), new Vector2(BackSize, BackSize));

            ticker = root.gameObject.AddComponent<RewardsTicker>();
            ticker.OnTick = Tick;
            Render();
        }

        /// <summary>Switches to a tab (rebuilds the body).</summary>
        public void SelectTab(RewardTab tab)
        {
            Tab = tab;
            completedOpen = false;   // every tab starts with its Completed section collapsed
            Render();
        }

        /// <summary>
        /// Chooses the region whose goals the Main tab lists (1..30; the circles, tests and the admin tools use it). On Main the strip is re-centred and the goal list and the
        /// Claim All button are rebuilt (the list is NOT static: it changes with every region); on another tab only the choice is stored.
        /// </summary>
        public void SelectRegion(int index)
        {
            if (Mathf.Clamp(index, 1, RegionGoalTable.TotalRegions) != selectedRegion) completedOpen = false;
            selectedRegion = Mathf.Clamp(index, 1, RegionGoalTable.TotalRegions);
            if (Tab != RewardTab.Main || body == null) return;
            ApplyStripSelection();
            if (stripSnap != null) stripSnap.CenterOn(selectedRegion - 1, true);
            RebuildGoals();
            BuildClaimAll();
        }

        static void Kill(GameObject go)
        {
            go.name = "Dead";   // a delayed Destroy keeps the old object for a frame: name lookups must not find it
            go.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(go);
            else UnityEngine.Object.DestroyImmediate(go);
        }

        void Render(bool keepScroll = false)
        {
            if (root == null) return;
            float keepY = keepScroll && ListContent != null ? ListContent.anchoredPosition.y : 0f;
            if (body != null) Kill(body.gameObject);
            body = UiKit.Rect(root, "Body");
            UiKit.Stretch(body);
            timerText = null;
            timerSeconds = -1;
            listViewport = null;
            mainHeader = null;
            claimAllGo = null;
            stripSnap = null;
            lastRemaining = Tab == RewardTab.Main ? 0.0 : Remaining().TotalSeconds;

            if (Tab == RewardTab.Main) BuildMainHeader(body);
            BuildList();
            BuildFooter();
            BuildTabs();
            BuildClaimAll();
            if (backButton != null) backButton.transform.SetAsLastSibling();   // above the footer bar
            if (keepScroll && ListContent != null) ListContent.anchoredPosition = new Vector2(0f, keepY);
            Tick();
        }

        TimeSpan Remaining() { return Tab == RewardTab.Weekly ? progress.WeeklyResetIn() : progress.DailyResetIn(); }

        /// <summary>Once-a-second countdown and the rollover check (a day or week that ends while the screen is open rebuilds the tab); on Main it drives the strip's snap glide.</summary>
        void Tick()
        {
            if (Tab == RewardTab.Main)
            {
                if (stripSnap != null) stripSnap.Step(Time.unscaledDeltaTime);
                return;
            }
            if (timerText == null) return;
            TimeSpan left = Remaining();
            if (left.TotalSeconds > lastRemaining + 1.0) { Render(); return; }   // the period rolled over: new counters, new claims
            lastRemaining = left.TotalSeconds;
            int secs = (int)left.TotalSeconds;
            if (secs == timerSeconds) return;
            timerSeconds = secs;
            var sb = new StringBuilder();
            if (left.Days > 0) sb.Append(left.Days).Append("d ").Append(left.Hours.ToString("00")).Append(':').Append(left.Minutes.ToString("00"));   // days left: no seconds, keeps the pill short
            else sb.Append(left.Hours.ToString("00")).Append(':').Append(left.Minutes.ToString("00")).Append(':').Append(left.Seconds.ToString("00"));
            UiKit.SetText(timerText, sb.ToString());
        }

        // ---------------- the list ----------------

        void BuildList()
        {
            RectTransform viewport = UiKit.Rect(body, "Viewport");
            listViewport = viewport;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(0f, BottomReserve());   // full width: the scene headers run edge to edge
            viewport.offsetMax = new Vector2(0f, -(HeaderH + (Tab == RewardTab.Main ? CircleZone : 0f)));
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hit = viewport.gameObject.AddComponent<Image>();   // catches the drag on the gaps between cards
            hit.color = new Color(0f, 0f, 0f, 0.001f);
            if (mainHeader != null) viewport.SetSiblingIndex(mainHeader.GetSiblingIndex() + 1);   // a rebuilt list stays under the footer

            RectTransform content = UiKit.Rect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = CardGap;
            vl.padding = new RectOffset((int)Side, (int)Side, Tab == RewardTab.Main ? 14 : 4, 72);   // the last card scrolls fully clear of the tab row
            vl.childAlignment = TextAnchor.UpperCenter;
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ListContent = content;

            var sr = viewport.gameObject.AddComponent<ScrollRect>();
            sr.viewport = viewport;
            sr.content = content;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 40f;

            RewardCatalog defs = progress.RewardDefs;
            if (Tab == RewardTab.Main)
            {
                BuildMainGoals(content);
            }
            else if (Tab == RewardTab.Daily)
            {
                BuildSceneHeader(content, true);
                AddCardsWithCompleted(content, defs.DailyActions, false);   // a finished daily action moves to "Completed (N)" at once
            }
            else
            {
                BuildSceneHeader(content, false);
                AddCardsWithCompleted(content, defs.WeeklyTasks, true);
            }
        }

        /// <summary>Main: rebuilds only the goal list (a region was chosen); the strip and the footer stay.</summary>
        void RebuildGoals()
        {
            if (listViewport != null) Kill(listViewport.gameObject);
            BuildList();
        }

        // ---------------- footer: tabs, bar, Back and Claim All ----------------

        void BuildFooter()
        {
            Image bar = ToyKit.Sliced(body, "Bg", UiKit.PillSprite, 4f);
            bar.color = FooterColor;
            RectTransform rt = bar.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0f, FooterH);
            Image edge = ToyKit.Sliced(bar.transform, "Backdrop", UiKit.PillSprite, 2f);   // a thin lighter line on the top edge
            edge.color = new Color(0.32f, 0.38f, 0.68f, 1f);
            edge.rectTransform.anchorMin = new Vector2(0f, 1f);
            edge.rectTransform.anchorMax = new Vector2(1f, 1f);
            edge.rectTransform.pivot = new Vector2(0.5f, 1f);
            edge.rectTransform.offsetMin = new Vector2(0f, -5f);
            edge.rectTransform.offsetMax = Vector2.zero;
        }

        void BuildTabs()
        {
            string[] names = { "Main", "Daily", "Weekly" };
            RewardTab[] tabs = { RewardTab.Main, RewardTab.Daily, RewardTab.Weekly };
            RectTransform bar = UiKit.Rect(body, "Tabs");
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            float y = FooterH + TabGap;
            bar.offsetMin = new Vector2(Side, y);
            bar.offsetMax = new Vector2(-Side, y + TabH);
            for (int i = 0; i < 3; i++)
            {
                RewardTab tab = tabs[i];
                bool on = tab == Tab;
                Sprite sp = on ? ToyKit.Button(0) : (ToyKit.Get("plate_chip") ?? ToyKit.Button(2));   // flat dark, the chosen one yellow
                Button b = UiKit.AddButton(bar, "Tab" + names[i], "", sp, Color.white, new Vector2(200f, TabH), () => { if (Tab != tab) SelectTab(tab); }, 50);
                b.GetComponent<A11yLabel>().Text = names[i] + " rewards tab" + (on ? ", selected" : "") + (progress.HasClaimable(tab) ? ", something to claim" : "");
                b.GetComponent<Image>().raycastTarget = true;
                RectTransform rt = (RectTransform)b.transform;
                rt.anchorMin = new Vector2(i / 3f, 0f);
                rt.anchorMax = new Vector2((i + 1) / 3f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(i == 0 ? 0f : 8f, 0f);
                rt.offsetMax = new Vector2(i == 2 ? 0f : -8f, 0f);
                Text t = UiKit.AddText(rt, "Label", names[i], 50, on ? UiKit.ButtonInk : Color.white, TextAnchor.MiddleCenter, true);
                if (on) UiKit.OnPanel(t);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiKit.Stretch(t.rectTransform);
                if (progress.HasClaimable(tab)) ToyKit.AddBadge(b.transform);   // red dot: something to claim in this tab
            }
        }

        /// <summary>
        /// The green Claim All button, bottom right in the footer; it only exists while something in the tab is ready.
        /// On Main it claims the goals that are ready in the SELECTED region (the red dots on the strip circles lead to the other regions).
        /// </summary>
        void BuildClaimAll()
        {
            if (claimAllGo != null) { Kill(claimAllGo); claimAllGo = null; }
            bool any = Tab == RewardTab.Main ? progress.HasClaimableInRegion(selectedRegion) : progress.HasClaimable(Tab);
            if (!any) return;
            RewardTab tab = Tab;
            Button b = UiKit.AddButton(body, "ClaimAll", "Claim All", ToyKit.Button(1), Color.white, new Vector2(440f, 130f), () => ClaimAll(tab), 50);
            b.GetComponent<A11yLabel>().Text = tab == RewardTab.Main ? "Claim all ready rewards of this region" : "Claim all ready rewards";
            b.GetComponent<Image>().raycastTarget = true;
            UiKit.Place((RectTransform)b.transform, new Vector2(1f, 0f), new Vector2(-Side, BottomPad), new Vector2(440f, ClaimBtnH));
            claimAllGo = b.gameObject;
            if (backButton != null) backButton.transform.SetAsLastSibling();
        }

        // ---------------- claiming ----------------

        /// <summary>Claims one reward (a card, a track chest or tests). Shows the small reward card and redraws the tab on success.</summary>
        public RewardClaimOutcome Claim(RewardTab tab, string id)
        {
            RewardClaimOutcome res = progress.TryClaimReward(tab, id);
            if (res.Result == RewardClaimResult.Ok) AfterClaim(res);
            return res;
        }

        /// <summary>Claims everything ready in a tab in one tap (one save). On Main: the ready goals of the selected region only.</summary>
        public RewardClaimOutcome ClaimAll(RewardTab tab)
        {
            RewardClaimOutcome res = tab == RewardTab.Main ? progress.ClaimAllRegion(selectedRegion) : progress.ClaimAll(tab);
            if (res.Result == RewardClaimResult.Ok) AfterClaim(res);
            return res;
        }

        /// <summary>Seconds after one win jingle in which further claims stay quiet (Bug List 8 follow-up): claiming many rewards in a row must not repeat the jingle for each.</summary>
        public const float WinSoundCooldown = 2.5f;
        static float lastWinSoundTime = -100f;

        void AfterClaim(RewardClaimOutcome res)
        {
            float now = Time.unscaledTime;
            if (now - lastWinSoundTime >= WinSoundCooldown) { lastWinSoundTime = now; AudioManager.Play(Sfx.Win); }   // the tap sound of each card still plays
            Haptics.Pulse();
            UiKit.SetText(CoinText, progress.Coins.ToString());
            UiKit.SetText(GemText, progress.Gems.ToString());
            Render(true);
            ShowClaimed(res);
        }

        // ---------------- the small reward card ----------------

        const float ChipCardW = 206f, ChipCardGap = 14f, ChipIcon = 96f, CardPad = 36f;
        /// <summary>Two chips a row: with the card drawn 50 percent bigger four chips would be wider than the 1080 reference (CUX-3), so more chips wrap to a new row.</summary>
        const int ChipsPerRow = 2;
        /// <summary>Owner Bug List 8: the reward (chest) card is 50 percent bigger in width and height. It is applied as the panel's scale so every chip, icon and the OK button grow with it.</summary>
        public const float RewardCardScale = 1.5f;
        /// <summary>Seconds the small reward card stays before it closes itself.</summary>
        public const float RewardCardSeconds = 4f;

        /// <summary>One icon chip of the small reward card: a picture (or a hero face) and a short amount ("40", "2K", "x3"); no sentences.</summary>
        struct ClaimChip
        {
            public Sprite Icon;
            public PowerUp? Power;
            public string HeroId, Amount, Spoken;
        }

        static float ChipH { get { return 14f + ChipIcon + 6f + LineH(BodyPx) + 10f; } }

        /// <summary>The icon chips a claim paid, in order: gems, coins, each power-up type with its count, one face per hero copy.</summary>
        List<ClaimChip> ClaimChips(RewardClaimOutcome res)
        {
            var chips = new List<ClaimChip>(6);
            if (res.Gems > 0) chips.Add(new ClaimChip { Icon = GemSprite, Amount = Compact(res.Gems), Spoken = Num(res.Gems) + " gems" });
            if (res.Coins > 0) chips.Add(new ClaimChip { Icon = CoinSprite, Amount = Compact(res.Coins), Spoken = Num(res.Coins) + " coins" });
            var counts = new Dictionary<string, int>();
            var order = new List<string>();
            foreach (string pid in res.PowerUpIds)
            {
                if (!counts.ContainsKey(pid)) { counts[pid] = 0; order.Add(pid); }
                counts[pid]++;
            }
            foreach (string pid in order)
            {
                PowerUpDef def = progress.PowerUpDefs.Get(pid);
                chips.Add(new ClaimChip { Icon = BoltSprite, Power = def != null ? def.Type : (PowerUp?)null, Amount = "x" + counts[pid], Spoken = counts[pid] + " " + (def != null ? def.Name : "power-up") });
            }
            foreach (string hid in res.HeroIds)
            {
                CharacterSpec spec = CharacterInventory.Get(hid);
                chips.Add(new ClaimChip { HeroId = hid, Amount = "", Spoken = (spec != null ? spec.Name : "hero") + " hero" });
            }
            return chips;
        }

        /// <summary>
        /// The small reward card: a compact centred card with a row of icon chips (gem, coin, power-up with x n, a hero face for every hero copy) and a green check OK button
        /// (at least 48 dp, "OK" for screen readers). No sentences. It closes on OK, on a tap anywhere, or by itself after <see cref="RewardCardSeconds"/>.
        /// </summary>
        public void ShowClaimed(RewardClaimOutcome res)
        {
            if (host == null || host.NewOverlay == null) return;
            RectTransform overlay = host.NewOverlay();
            List<ClaimChip> chips = ClaimChips(res);
            int n = Mathf.Max(1, chips.Count);
            int rows = (n + ChipsPerRow - 1) / ChipsPerRow;
            int perRow = Mathf.Min(n, ChipsPerRow);
            float chipH = ChipH;
            float ok = Mathf.Max(130f, UiKit.MinTouchRef);
            float w = 2f * CardPad + perRow * ChipCardW + (perRow - 1) * ChipCardGap;
            float h = CardPad + rows * chipH + (rows - 1) * ChipCardGap + 26f + ok + 30f;

            RectTransform panel = UiKit.Rect(overlay, "Panel");
            UiKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
            panel.pivot = new Vector2(0.5f, 0.5f);
            // 50 percent bigger, but never beyond the safe area (a Side margin all round) at any aspect or text size
            float availW = screen != null && screen.rect.width > 200f ? screen.rect.width : 1080f;
            float availH = screen != null && screen.rect.height > 200f ? screen.rect.height : 1920f;
            float scale = Mathf.Min(RewardCardScale, (availW - 2f * Side) / w, (availH - 2f * Side) / h);
            panel.localScale = new Vector3(scale, scale, 1f);
            Theme.CardBacking(panel);
            RewardCard = panel;

            var spoken = new List<string>();
            for (int i = 0; i < chips.Count; i++)
            {
                int row = i / ChipsPerRow, inRow = Mathf.Min(ChipsPerRow, chips.Count - row * ChipsPerRow);
                float rowW = inRow * ChipCardW + (inRow - 1) * ChipCardGap;
                float x = -rowW * 0.5f + (i % ChipsPerRow) * (ChipCardW + ChipCardGap) + ChipCardW * 0.5f;
                BuildClaimChip(panel, chips[i], new Vector2(x, -(CardPad + row * (chipH + ChipCardGap))), chipH);
                spoken.Add(chips[i].Spoken);
            }
            panel.gameObject.AddComponent<A11yLabel>().Text = "Reward: " + string.Join(", ", spoken.ToArray());

            ToyKit.PopupAction(panel, "OK", null, Color.white, 30f, host.CloseOverlay);

            // a tap anywhere closes it (the dim overlay is the button), and so does the timer
            Action close = host.CloseOverlay;
            var tap = overlay.gameObject.AddComponent<Button>();
            Image dim = overlay.GetComponent<Image>();
            if (dim != null) { dim.raycastTarget = true; tap.targetGraphic = dim; }
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(() => { if (close != null) close(); });
            var timer = overlay.gameObject.AddComponent<RewardCardTimer>();
            timer.Seconds = RewardCardSeconds;
            timer.OnTimeUp = close;
        }

        void BuildClaimChip(RectTransform panel, ClaimChip c, Vector2 pos, float chipH)
        {
            Image chip = Plate(panel, "Chip", "plate_chip", Color.white, RewardCorner);
            UiKit.Place(chip.rectTransform, new Vector2(0.5f, 1f), pos, new Vector2(ChipCardW, chipH));
            if (c.HeroId != null)
            {
                RectTransform face = HeroFace(chip.transform, "HeroFace", c.HeroId, ChipIcon);
                UiKit.Place(face, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ChipIcon, ChipIcon));
                return;
            }
            if (c.Power.HasValue) PowerUpUi.AddIcon(chip.transform, "Icon", c.Power.Value, new Vector2(0.5f, 1f), new Vector2(0f, -14f), ChipIcon, false);
            else
            {
                Image ic = UiKit.AddImage(chip.transform, "Icon", c.Icon, Color.white);
                UiKit.Place(ic.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(ChipIcon, ChipIcon));
            }
            Text t = UiKit.AddText(chip.transform, "Amount", c.Amount, BodyPx, Color.white, TextAnchor.MiddleCenter, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform trt = t.rectTransform;
            trt.anchorMin = new Vector2(0f, 0f);
            trt.anchorMax = new Vector2(1f, 0f);
            trt.pivot = new Vector2(0.5f, 0f);
            trt.offsetMin = new Vector2(0f, 8f);
            trt.offsetMax = new Vector2(0f, 8f + LineH(BodyPx));
        }

        /// <summary>"20 gems and 1 power-up" for screen readers.</summary>
        static string Describe(RewardEntry e)
        {
            var parts = new List<string>();
            if (e.Gems > 0) parts.Add(Num(e.Gems) + " gems");
            if (e.Coins > 0) parts.Add(Num(e.Coins) + " coins");
            if (e.PowerUps > 0) parts.Add(e.PowerUps + (e.PowerUps == 1 ? " power-up" : " power-ups"));
            if (e.GivesHero) parts.Add("a hero");
            return string.Join(" and ", parts.ToArray());
        }
    }

    /// <summary>Closes the small reward card after a few seconds (unscaled time). Lives on the overlay, so it dies with it; one float subtraction per frame, no allocation.</summary>
    public sealed class RewardCardTimer : MonoBehaviour
    {
        public float Seconds = 4f;
        public Action OnTimeUp;

        void Update()
        {
            Seconds -= Time.unscaledDeltaTime;
            if (Seconds > 0f) return;
            enabled = false;
            if (OnTimeUp != null) OnTimeUp();
        }
    }
}
