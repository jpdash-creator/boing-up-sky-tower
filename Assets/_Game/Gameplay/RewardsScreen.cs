using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The Rewards screen (docs/26 section 4, "RewardsStyle"), sleek redesign of Bug List 3 D4: tabs Main | Daily | Weekly in a footer with Back and Claim All.
    /// Main: the region circle strip, a purple bonus band (clear all levels of the chosen region) and the star milestone cards on a numbered rail.
    /// Daily and Weekly: a scene header with the reset timer and the points track (Daily, chests with thresholds) or the tasks done bar (Weekly), then the task cards.
    /// Cards: cream (open), yellow with a green CLAIM tag (ready), green with a check (claimed); a dark reward plate at the right.
    /// Files: this one (frame, tabs, claiming), RewardsScreen.Cards.cs (cards, plates, bars), RewardsScreen.Headers.cs (Main band, scene headers, track).
    /// Everything scrolls in one list, so short screens never lose content. Menu screen only: no per-frame work except the once-a-second countdown text.
    /// </summary>
    public sealed partial class RewardsScreen
    {
        /// <summary>What the screen needs from the meta scene: the dim overlay layer for the "Reward!" card and the way back. It never owns a canvas.</summary>
        public sealed class Host
        {
            public Func<RectTransform> NewOverlay;
            public Action CloseOverlay;
            public Action Back;
        }

        // ---- metrics (canvas units of the 1080 x 1920 reference) ----
        const float Side = 24f;
        const float WalletTop = 16f, WalletH = 100f;
        const float BackSize = 150f, BottomPad = 16f, TabH = 130f, TabGap = 14f;
        /// <summary>Height of the dark footer bar (Back and Claim All live in it).</summary>
        public const float FooterH = BottomPad + BackSize + BottomPad;
        const int BodyPx = 44;

        // Main header stack (top to bottom): circles (selected one is 190 tall, starts at -14), 10 gap, name zone, gap, bonus band.
        // The name zone reserves two lines at the current text scale so a wrapped name can never reach the band (CUX-3).
        const float CircleBottom = 14f + 190f, NameGap = 10f, PanelGap = 44f;
        static float NameZone { get { return Mathf.Ceil(Mathf.Max(36f, UiKit.MinBodyTextPx) * UiKit.TextScale * 3.9f) + 8f; } }
        static float CircleZone { get { return CircleBottom + NameGap + NameZone + PanelGap; } }

        // ---- colours ----
        static readonly Color FooterColor = new Color(0.10f, 0.13f, 0.32f, 1f);
        static readonly Color TrackDark = new Color(0.12f, 0.14f, 0.29f, 1f);
        static readonly Color BarBlue = new Color(0.20f, 0.60f, 1f, 1f);
        static readonly Color BarGreen = new Color(0.22f, 0.78f, 0.34f, 1f);
        static readonly Color BarGold = new Color(1f, 0.72f, 0.12f, 1f);
        static readonly Color PurpleBand = new Color(0.48f, 0.32f, 0.74f, 1f);
        static readonly Color PurpleEdge = new Color(0.32f, 0.20f, 0.54f, 1f);

        readonly RectTransform screen;
        readonly ProgressService progress;
        readonly ContentRepository repo;
        readonly ArtCatalog art;
        readonly Host host;

        RectTransform root, body;
        Button backButton;
        RewardsTicker ticker;
        Text timerText;
        int timerSeconds = -1;
        double lastRemaining;
        int selectedRegion;

        public Text CoinText { get; private set; }
        public Text GemText { get; private set; }
        public RewardTab Tab { get; private set; }
        /// <summary>The scrolling list of the current tab (tests scroll it or count its rows).</summary>
        public RectTransform ListContent { get; private set; }

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
            selectedRegion = Mathf.Clamp(cur, 1, GameSession.PlayableRegions);
            root = UiKit.Rect(screen, "RewardsRoot");
            UiKit.Stretch(root);

            RectTransform wallet = UiKit.Rect(root, "Wallet");
            wallet.anchorMin = new Vector2(0f, 1f);
            wallet.anchorMax = new Vector2(1f, 1f);
            wallet.pivot = new Vector2(0.5f, 1f);
            wallet.offsetMin = new Vector2(Side, -(WalletTop + WalletH));
            wallet.offsetMax = new Vector2(-Side, -WalletTop);
            var whl = wallet.gameObject.AddComponent<HorizontalLayoutGroup>();
            whl.spacing = 10f;
            whl.childControlWidth = whl.childControlHeight = true;
            whl.childForceExpandWidth = whl.childForceExpandHeight = true;
            CoinText = ToyKit.WalletPillFlex(wallet, "CoinsPill", art != null ? art.CoinIcon : null, Color.white, WalletH);
            GemText = ToyKit.WalletPillFlex(wallet, "GemsPill", art != null ? art.GemIcon : null, new Color(0.75f, 0.92f, 1f), WalletH);
            UiKit.SetText(CoinText, progress.Coins.ToString());
            UiKit.SetText(GemText, progress.Gems.ToString());

            // the blue back arrow, bottom left (it stays on every tab; Render keeps it above the footer bar)
            backButton = ToyKit.GlyphButton(root, "Back", "Back", 2, Glyphs.Left, new Vector2(BackSize, BackSize), () => { if (host != null && host.Back != null) host.Back(); });
            UiKit.Place((RectTransform)backButton.transform, new Vector2(0f, 0f), new Vector2(Side, BottomPad), new Vector2(BackSize, BackSize));

            ticker = root.gameObject.AddComponent<RewardsTicker>();
            ticker.OnTick = Tick;
            Render();
        }

        /// <summary>Switches to a tab (rebuilds the body).</summary>
        public void SelectTab(RewardTab tab)
        {
            Tab = tab;
            Render();
        }

        /// <summary>Chooses the region shown in the Main tab's bonus band (tests and the circles use it).</summary>
        public void SelectRegion(int index)
        {
            selectedRegion = Mathf.Clamp(index, 1, GameSession.PlayableRegions);
            if (Tab == RewardTab.Main) Render();
        }

        static void Kill(GameObject go)
        {
            go.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(go);
            else UnityEngine.Object.DestroyImmediate(go);
        }

        void Render()
        {
            if (root == null) return;
            if (body != null) Kill(body.gameObject);
            body = UiKit.Rect(root, "Body");
            UiKit.Stretch(body);
            timerText = null;
            timerSeconds = -1;
            lastRemaining = Tab == RewardTab.Main ? 0.0 : Remaining().TotalSeconds;

            BuildList();
            BuildFooter();
            BuildTabs();
            BuildClaimAll();
            if (backButton != null) backButton.transform.SetAsLastSibling();   // above the footer bar
            Tick();
        }

        TimeSpan Remaining() { return Tab == RewardTab.Weekly ? progress.WeeklyResetIn() : progress.DailyResetIn(); }

        /// <summary>Once-a-second countdown and the rollover check (a day or week that ends while the screen is open rebuilds the tab).</summary>
        void Tick()
        {
            if (Tab == RewardTab.Main || timerText == null) return;
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
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(0f, BottomReserve());   // full width: the scene headers and the band run edge to edge
            viewport.offsetMax = new Vector2(0f, -(WalletTop + WalletH + 12f));
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hit = viewport.gameObject.AddComponent<Image>();   // catches the drag on the gaps between cards
            hit.color = new Color(0f, 0f, 0f, 0.001f);

            RectTransform content = UiKit.Rect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = CardGap;
            vl.padding = new RectOffset((int)Side, (int)Side, 4, 72);   // the last card scrolls fully clear of the tab row
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
                BuildMainHeader(content);
                int n = defs.StarMilestones.Count;
                for (int i = 0; i < n; i++) AddCard(content, defs.StarMilestones[i], true, i, n, true);
            }
            else if (Tab == RewardTab.Daily)
            {
                BuildSceneHeader(content, true);
                foreach (RewardEntry a in defs.DailyActions) AddCard(content, a, false, 0, 0, false);
            }
            else
            {
                BuildSceneHeader(content, false);
                foreach (RewardEntry e in defs.WeeklyTasks) AddCard(content, e, true, 0, 0, false);
            }
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

        /// <summary>The green Claim All button, bottom right in the footer; it only exists while something in the tab is ready.</summary>
        void BuildClaimAll()
        {
            if (!progress.HasClaimable(Tab)) return;
            RewardTab tab = Tab;
            Button b = UiKit.AddButton(body, "ClaimAll", "Claim All", ToyKit.Button(1), Color.white, new Vector2(440f, 130f), () => ClaimAll(tab), 50);
            b.GetComponent<A11yLabel>().Text = "Claim all ready rewards";
            b.GetComponent<Image>().raycastTarget = true;
            UiKit.Place((RectTransform)b.transform, new Vector2(1f, 0f), new Vector2(-Side, BottomPad + (BackSize - 130f) * 0.5f), new Vector2(440f, 130f));
        }

        // ---------------- claiming ----------------

        /// <summary>Claims one reward (a card, a track chest or tests). Shows the "Reward!" card and redraws the tab on success.</summary>
        public RewardClaimOutcome Claim(RewardTab tab, string id)
        {
            RewardClaimOutcome res = progress.TryClaimReward(tab, id);
            if (res.Result == RewardClaimResult.Ok) AfterClaim(res);
            return res;
        }

        /// <summary>Claims everything ready in a tab in one tap (one save).</summary>
        public RewardClaimOutcome ClaimAll(RewardTab tab)
        {
            RewardClaimOutcome res = progress.ClaimAll(tab);
            if (res.Result == RewardClaimResult.Ok) AfterClaim(res);
            return res;
        }

        void AfterClaim(RewardClaimOutcome res)
        {
            AudioManager.Play(Sfx.Win);
            Haptics.Pulse();
            UiKit.SetText(CoinText, progress.Coins.ToString());
            UiKit.SetText(GemText, progress.Gems.ToString());
            Render();
            ShowClaimed(res);
        }

        /// <summary>The "Reward!" card listing what a claim paid (the layout audit shows it with a sample).</summary>
        public void ShowClaimed(RewardClaimOutcome res)
        {
            if (host == null || host.NewOverlay == null) return;
            RectTransform overlay = host.NewOverlay();
            RectTransform panel = UiKit.BuildPopupFrame(overlay, "Reward!", art, new Vector2(860f, 980f), host.CloseOverlay);
            var sb = new StringBuilder();
            if (res.Gems > 0) sb.Append("+").Append(Num(res.Gems)).Append(" gems\n");
            if (res.Coins > 0) sb.Append("+").Append(Num(res.Coins)).Append(" coins\n");
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
                sb.Append("+").Append(counts[pid]).Append(' ').Append(def != null ? def.Name : pid).Append('\n');
            }
            Text t = UiKit.AddText(panel, "Body", sb.ToString().TrimEnd('\n'), 56, Theme.Ink);
            UiKit.OnPanel(t);
            UiKit.Place((RectTransform)t.transform, new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(740f, 520f));
            ToyKit.PopupAction(panel, "OK", null, Color.white, 60f, host.CloseOverlay);
        }

        /// <summary>"20 gems and 1 power-up" for screen readers and the reward card.</summary>
        static string Describe(RewardEntry e)
        {
            var parts = new List<string>();
            if (e.Gems > 0) parts.Add(Num(e.Gems) + " gems");
            if (e.Coins > 0) parts.Add(Num(e.Coins) + " coins");
            if (e.PowerUps > 0) parts.Add(e.PowerUps + (e.PowerUps == 1 ? " power-up" : " power-ups"));
            return string.Join(" and ", parts.ToArray());
        }
    }
}
