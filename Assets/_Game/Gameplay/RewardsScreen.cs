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
    /// <summary>Calls back once per frame while the Rewards screen lives (the countdown and the midnight / Monday rollover). Removed with the screen.</summary>
    public sealed class RewardsTicker : MonoBehaviour
    {
        public Action OnTick;
        void Update() { if (OnTick != null) OnTick(); }
    }

    /// <summary>
    /// The Rewards screen (docs/26 section 4): tabs Main | Daily | Weekly at the bottom, a milestone track on top (stars on Main, points on Daily),
    /// a countdown to the reset, and a scrolling list of rows (icon, title, progress bar with n/N, claim button with the reward chips).
    /// The track is a display (its cells are narrower than a touch target); every claim is a button of a row, so each touch target is at least 48 dp.
    /// Menu screen only: no per-frame work except the once-a-second countdown text.
    /// </summary>
    public sealed class RewardsScreen
    {
        /// <summary>What the screen needs from the meta scene: the dim overlay layer for the "Reward claimed" card. It never owns a canvas.</summary>
        public sealed class Host
        {
            public Func<RectTransform> NewOverlay;
            public Action CloseOverlay;
        }

        const float Side = 28f, RowH = 290f, RowGap = 24f, ClaimH = 256f, TabH = 140f, TabGap = 16f;
        const float WalletTop = 18f, SummaryTop = 138f, SummaryH = 100f, TrackTop = 258f, TrackH = 392f;

        static readonly Color PanelBlue = new Color(0.30f, 0.34f, 0.64f, 1f);

        readonly RectTransform screen;
        readonly ProgressService progress;
        readonly ContentRepository repo;
        readonly ArtCatalog art;
        readonly Host host;

        RectTransform root, body;
        Text summaryText;
        int summarySeconds = -1;
        double lastRemaining;
        RewardsTicker ticker;

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

        /// <summary>Short number for the narrow track cells: 1,000 becomes 1K, 1,500 becomes 1.5K.</summary>
        static string Compact(int n)
        {
            if (n < 1000) return n.ToString(CultureInfo.InvariantCulture);
            double k = n / 1000.0;
            return k.ToString(k == Math.Floor(k) ? "0" : "0.#", CultureInfo.InvariantCulture) + "K";
        }

        // ---------------- build ----------------

        public void Build(RewardTab tab)
        {
            Tab = tab;
            root = UiKit.Rect(screen, "RewardsRoot");
            UiKit.Stretch(root);

            // wallet (coins, gems) at the top right; the back button sits at the top left (MetaController)
            RectTransform wallet = UiKit.Rect(root, "Wallet");
            wallet.anchorMin = new Vector2(0f, 1f);
            wallet.anchorMax = new Vector2(1f, 1f);
            wallet.pivot = new Vector2(0.5f, 1f);
            wallet.offsetMin = new Vector2(190f, -(WalletTop + 100f));
            wallet.offsetMax = new Vector2(-16f, -WalletTop);
            var whl = wallet.gameObject.AddComponent<HorizontalLayoutGroup>();
            whl.spacing = 10f;
            whl.childControlWidth = whl.childControlHeight = true;
            whl.childForceExpandWidth = whl.childForceExpandHeight = true;
            CoinText = ToyKit.WalletPillFlex(wallet, "CoinsPill", art != null ? art.CoinIcon : null, Color.white, 100f);
            GemText = ToyKit.WalletPillFlex(wallet, "GemsPill", art != null ? art.GemIcon : null, new Color(0.75f, 0.92f, 1f), 100f);
            UiKit.SetText(CoinText, progress.Coins.ToString());
            UiKit.SetText(GemText, progress.Gems.ToString());

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

        void Render()
        {
            if (root == null) return;
            if (body != null)
            {
                body.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(body.gameObject);
            }
            body = UiKit.Rect(root, "Body");
            UiKit.Stretch(body);
            summaryText = null;
            summarySeconds = -1;
            lastRemaining = Tab == RewardTab.Main ? 0.0 : Remaining().TotalSeconds;

            BuildSummary();
            bool track = Tab != RewardTab.Weekly;
            if (track) BuildTrack();
            BuildList(track ? TrackTop + TrackH + 20f : TrackTop);
            BuildTabs();
            Tick();
        }

        TimeSpan Remaining() { return Tab == RewardTab.Weekly ? progress.WeeklyResetIn() : progress.DailyResetIn(); }

        // ---------------- summary pill: stars (Main) or the reset countdown ----------------

        void BuildSummary()
        {
            Image pill = Theme.GlassPanel(body, "SummaryPill", 36f);
            RectTransform prt = pill.rectTransform;
            prt.anchorMin = new Vector2(0f, 1f);
            prt.anchorMax = new Vector2(1f, 1f);
            prt.pivot = new Vector2(0.5f, 1f);
            prt.offsetMin = new Vector2(190f, -(SummaryTop + SummaryH));
            prt.offsetMax = new Vector2(-Side, -SummaryTop);
            var hl = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 14f;
            hl.padding = new RectOffset(24, 24, 6, 6);
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            Sprite icon = Tab == RewardTab.Main ? (art != null ? art.StarFilled : null) : ToyKit.Get("icon_dial");
            Image ic = UiKit.AddImage(pill.transform, "Icon", icon, Color.white);
            var ile = ic.gameObject.AddComponent<LayoutElement>();
            ile.preferredWidth = ile.preferredHeight = 70f;
            summaryText = UiKit.AddText(pill.transform, "Summary", "", 50, Color.white, TextAnchor.MiddleCenter, true);
            summaryText.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (Tab == RewardTab.Main) UiKit.SetText(summaryText, "Stars " + progress.TotalStars() + " / " + MainMax());
        }

        int MainMax()
        {
            IReadOnlyList<RewardEntry> m = progress.RewardDefs.StarMilestones;
            return m.Count > 0 ? m[m.Count - 1].Target : 0;
        }

        /// <summary>Once-a-second countdown and the rollover check (a day or week that ends while the screen is open rebuilds the tab).</summary>
        void Tick()
        {
            if (Tab == RewardTab.Main || summaryText == null) return;
            TimeSpan left = Remaining();
            if (left.TotalSeconds > lastRemaining + 1.0) { Render(); return; }   // the period rolled over: new counters, new claims
            lastRemaining = left.TotalSeconds;
            int secs = (int)left.TotalSeconds;
            if (secs == summarySeconds) return;
            summarySeconds = secs;
            var sb = new StringBuilder("Resets in ");
            if (left.Days > 0) sb.Append(left.Days).Append("d ").Append(left.Hours.ToString("00")).Append(':').Append(left.Minutes.ToString("00"));   // days left: no seconds, keeps the pill short
            else sb.Append(left.Hours.ToString("00")).Append(':').Append(left.Minutes.ToString("00")).Append(':').Append(left.Seconds.ToString("00"));
            UiKit.SetText(summaryText, sb.ToString());
        }

        // ---------------- track ----------------

        IReadOnlyList<RewardEntry> TrackEntries()
        {
            return Tab == RewardTab.Main ? progress.RewardDefs.StarMilestones : progress.RewardDefs.DailyMilestones;
        }

        void BuildTrack()
        {
            IReadOnlyList<RewardEntry> ms = TrackEntries();
            int n = ms.Count;
            if (n == 0) return;
            int value = Tab == RewardTab.Main ? progress.TotalStars() : progress.DailyPoints();
            int max = ms[n - 1].Target;

            Image panel = Theme.GlassPanel(body, "TrackPanel", 40f);
            RectTransform prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0f, 1f);
            prt.anchorMax = new Vector2(1f, 1f);
            prt.pivot = new Vector2(0.5f, 1f);
            prt.offsetMin = new Vector2(Side, -(TrackTop + TrackH));
            prt.offsetMax = new Vector2(-Side, -TrackTop);

            // current value, top left: star icon and "37 / 225" (stars) or "20 / 100 points"
            Image star = UiKit.AddImage(panel.transform, "Icon", art != null ? art.StarFilled : null, Color.white);
            UiKit.Place(star.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -14f), new Vector2(72f, 72f));
            string valueText = value + " / " + max + (Tab == RewardTab.Daily ? " points" : " stars");
            Text vt = UiKit.AddText(panel.transform, "Value", valueText, 52, Color.white, TextAnchor.MiddleLeft, true);
            vt.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform vrt = vt.rectTransform;
            vrt.anchorMin = new Vector2(0f, 1f);
            vrt.anchorMax = new Vector2(1f, 1f);
            vrt.pivot = new Vector2(0f, 1f);
            vrt.offsetMin = new Vector2(112f, -92f);
            vrt.offsetMax = new Vector2(-24f, -14f);

            // the bar behind the cells; the fill reaches the centre of a cell exactly when its target is met
            const float pad = 12f;
            RectTransform inner = UiKit.Rect(panel.transform, "Inner");
            inner.anchorMin = new Vector2(0f, 0f);
            inner.anchorMax = new Vector2(1f, 1f);
            inner.offsetMin = new Vector2(pad, 12f);
            inner.offsetMax = new Vector2(-pad, -104f);

            Image barBg = UiKit.AddPanel(inner, "Backdrop", new Color(0f, 0f, 0f, 0.45f));
            RectTransform brt = barBg.rectTransform;
            brt.anchorMin = new Vector2(0f, 1f);
            brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.offsetMin = new Vector2(0f, -24f);
            brt.offsetMax = Vector2.zero;
            Image fill = UiKit.AddPanel(brt, "Fill", Theme.Gold);
            fill.rectTransform.anchorMin = new Vector2(0f, 0f);
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(TrackPosition(ms, value)), 1f);
            fill.rectTransform.offsetMin = new Vector2(2f, 2f);
            fill.rectTransform.offsetMax = new Vector2(-2f, -2f);

            for (int i = 0; i < n; i++) BuildTrackCell(inner, ms[i], i, n);
        }

        /// <summary>Position (0..1 of the bar) of a value: cell centres are at (k + 0.5) / n, the start is 0.</summary>
        static float TrackPosition(IReadOnlyList<RewardEntry> ms, int value)
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

        void BuildTrackCell(RectTransform inner, RewardEntry e, int index, int n)
        {
            RectTransform cell = UiKit.Rect(inner, "Cell" + e.Target);
            cell.anchorMin = new Vector2(index / (float)n, 0f);
            cell.anchorMax = new Vector2((index + 1) / (float)n, 1f);
            cell.offsetMin = Vector2.zero;
            cell.offsetMax = new Vector2(0f, -32f);
            bool claimed = progress.IsRewardClaimed(Tab, e);
            bool reached = progress.RewardProgress(Tab, e) >= progress.RewardTarget(Tab, e);

            // the picture: gems first, then coins, then power-ups; a second reward kind is a small bolt on the corner
            Sprite pic; string count;
            PrimaryReward(e, out pic, out count);
            Image icon = UiKit.AddImage(cell, "Icon", pic, reached ? Color.white : new Color(0.72f, 0.74f, 0.82f, 1f));
            UiKit.Place(icon.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(76f, 76f));
            bool extra = (e.Gems > 0 ? 1 : 0) + (e.Coins > 0 ? 1 : 0) + (e.PowerUps > 0 ? 1 : 0) > 1;
            if (extra)
            {
                Image bolt = UiKit.AddImage(icon.transform, "Extra", ExtraIcon(e), Color.white);
                UiKit.Place(bolt.rectTransform, new Vector2(1f, 0f), new Vector2(8f, -4f), new Vector2(36f, 36f));
            }
            if (claimed)
            {
                Image tick = UiKit.AddImage(icon.transform, "Claimed", Glyphs.Check, Color.white);
                UiKit.Place(tick.rectTransform, new Vector2(0f, 0f), new Vector2(-6f, -6f), new Vector2(40f, 40f));
            }
            Text c = UiKit.AddText(cell, "Count", count, 42, reached ? Color.white : Theme.OnGlassSoft, TextAnchor.UpperCenter, true);
            c.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform crt = c.rectTransform;
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.offsetMin = new Vector2(0f, -156f);   // 76 px: a 130% number line is about 66 px
            crt.offsetMax = new Vector2(0f, -80f);
            Text t = UiKit.AddText(cell, "Target", e.Target.ToString(CultureInfo.InvariantCulture), 42, reached ? Theme.Gold : Theme.OnGlassSoft, TextAnchor.UpperCenter, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform trt = t.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(0f, -236f);
            trt.offsetMax = new Vector2(0f, -160f);
        }

        void PrimaryReward(RewardEntry e, out Sprite pic, out string count)
        {
            if (e.Gems > 0) { pic = art != null ? art.GemIcon : null; count = Compact(e.Gems); }
            else if (e.Coins > 0) { pic = art != null ? art.CoinIcon : null; count = Compact(e.Coins); }
            else { pic = ToyKit.Get("icon_bolt"); count = "x" + e.PowerUps; }
        }

        Sprite ExtraIcon(RewardEntry e)
        {
            if (e.PowerUps > 0) return ToyKit.Get("icon_bolt");
            return art != null ? art.CoinIcon : null;
        }

        // ---------------- list ----------------

        void BuildList(float top)
        {
            RectTransform viewport = UiKit.Rect(body, "Viewport");
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(Side, TabBarSpace());
            viewport.offsetMax = new Vector2(-Side, -top);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hit = viewport.gameObject.AddComponent<Image>();   // catches the drag on the gaps between rows
            hit.color = new Color(0f, 0f, 0f, 0.001f);

            RectTransform content = UiKit.Rect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = RowGap;
            vl.padding = new RectOffset(0, 0, 4, 40);
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
                foreach (RewardEntry e in defs.StarMilestones) AddClaimRow(content, e);
                foreach (RewardEntry e in defs.RegionRewards) AddClaimRow(content, e);
            }
            else if (Tab == RewardTab.Daily)
            {
                foreach (RewardEntry e in defs.DailyMilestones) AddClaimRow(content, e);
                foreach (RewardEntry a in defs.DailyActions) AddActionRow(content, a);
            }
            else
            {
                foreach (RewardEntry e in defs.WeeklyTasks) AddClaimRow(content, e);
            }
        }

        static float TabBarSpace() { return TabH + TabGap * 2f; }

        string TitleOf(RewardEntry e)
        {
            switch (Tab)
            {
                case RewardTab.Main:
                    if (e.Id.StartsWith("r_", StringComparison.Ordinal))
                    {
                        RegionDef r = repo != null ? repo.TryGetRegionByIndex(e.Target) : null;
                        return (r != null ? r.Name : e.Title.Replace(" cleared", "")) + " cleared";
                    }
                    return "Collect " + e.Target + " stars";
                case RewardTab.Daily:
                    return e.Stat == null ? "Earn " + e.Target + " points" : e.Title;
                default:
                    return e.Title;
            }
        }

        static Sprite IconForStat(string stat)
        {
            string name;
            switch (stat)
            {
                case RewardStat.Login: case RewardStat.LoginDays: name = "icon_daily"; break;
                case RewardStat.PlaySeconds: case RewardStat.PlayMinutes: name = "icon_dial"; break;
                case RewardStat.Levels: name = "icon_flag"; break;
                case RewardStat.Combos: name = "icon_flame"; break;
                case RewardStat.Hard: case RewardStat.PowerUps: name = "icon_bolt"; break;
                case RewardStat.NoWall: name = "icon_hand"; break;
                case RewardStat.GemLevel: case RewardStat.AllGems: case RewardStat.Gems: name = "icon_gem"; break;
                case RewardStat.Stars3: name = "icon_star"; break;
                default: name = "icon_star"; break;
            }
            return ToyKit.Get(name);
        }

        Sprite RowIcon(RewardEntry e)
        {
            if (Tab == RewardTab.Main)
            {
                if (e.Id.StartsWith("r_", StringComparison.Ordinal)) return ToyKit.Get("icon_tower");
                return art != null ? art.StarFilled : null;
            }
            if (e.Stat == null) return art != null ? art.StarFilled : null;
            return IconForStat(e.Stat);
        }

        RectTransform NewRow(RectTransform list, RewardEntry e, out int value, out int target)
        {
            RectTransform row = UiKit.Rect(list, "Row_" + e.Id);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = RowH;
            le.flexibleHeight = 0f;
            Theme.CardBacking(row);

            value = progress.RewardProgress(Tab, e);
            target = progress.RewardTarget(Tab, e);
            int shown = Mathf.Min(value, target);

            Image icon = UiKit.AddImage(row, "Icon", RowIcon(e), Color.white);
            UiKit.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(112f, 112f));

            Text title = UiKit.AddText(row, "Title", TitleOf(e), 42, Theme.Ink, TextAnchor.MiddleLeft, true);
            UiKit.OnPanel(title);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            RectTransform trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 0.47f);   // left edge in pixels (right of the 28 + 112 px icon), so it clears the icon at every row width
            trt.anchorMax = new Vector2(0.68f, 0.99f);
            trt.offsetMin = new Vector2(156f, 0f);
            trt.offsetMax = Vector2.zero;

            // progress bar with n/N inside
            Image bar = UiKit.AddPanel(row, "Backdrop", new Color(0.80f, 0.84f, 0.93f, 1f));   // light track: ink text reads on both the track and the gold fill
            RectTransform brt = bar.rectTransform;
            brt.anchorMin = new Vector2(0f, 0.08f);
            brt.anchorMax = new Vector2(0.68f, 0.40f);
            brt.offsetMin = new Vector2(156f, 0f);
            brt.offsetMax = Vector2.zero;
            Image fill = UiKit.AddPanel(brt, "Fill", Theme.Gold);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(target > 0 ? Mathf.Clamp01(value / (float)target) : 0f, 1f);
            fill.rectTransform.offsetMin = new Vector2(3f, 3f);
            fill.rectTransform.offsetMax = new Vector2(-3f, -3f);
            string label = (e.Stat == RewardStat.PlaySeconds) ? (shown / 60) + "/" + (target / 60) + " min" : Num(shown) + "/" + Num(target);
            Text bt = UiKit.AddText(bar.transform, "Count", label, 42, Theme.Ink, TextAnchor.MiddleCenter, true);
            UiKit.OnPanel(bt);
            bt.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Stretch(bt.rectTransform);
            return row;
        }

        void AddClaimRow(RectTransform list, RewardEntry e)
        {
            int value, target;
            RectTransform row = NewRow(list, e, out value, out target);
            bool claimed = progress.IsRewardClaimed(Tab, e);
            bool ready = !claimed && value >= target;
            RewardTab tab = Tab;
            string id = e.Id;
            string a11y = TitleOf(e) + ": " + (claimed ? "claimed" : ready ? "claim " + Describe(e) : Describe(e) + ", " + Math.Min(value, target) + " of " + target);
            Button b = UiKit.AddButton(row, "Claim_" + id, "", ToyKit.Button(ready ? 1 : (claimed ? 2 : 0)), Color.white, new Vector2(300f, ClaimH), () => Claim(tab, id), 40);
            b.GetComponent<A11yLabel>().Text = a11y;
            b.GetComponent<Image>().raycastTarget = true;
            b.interactable = ready;
            var cb = b.colors;
            cb.disabledColor = Color.white;   // a plate that cannot be claimed yet keeps its look (the label and chips carry the state)
            b.colors = cb;
            RectTransform rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(0.70f, 0.5f);
            rt.anchorMax = new Vector2(0.98f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(0f, -ClaimH * 0.5f);
            rt.offsetMax = new Vector2(0f, ClaimH * 0.5f);
            var vl = b.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 2f;
            vl.padding = new RectOffset(8, 8, 6, 6);
            vl.childAlignment = TextAnchor.MiddleCenter;
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = vl.childForceExpandHeight = false;
            if (claimed) { AddButtonLabel(b.transform, "Claimed"); return; }
            if (ready) AddButtonLabel(b.transform, "CLAIM");
            AddChips(b.transform, e);
        }

        void AddActionRow(RectTransform list, RewardEntry a)
        {
            int value, target;
            RectTransform row = NewRow(list, a, out value, out target);
            bool done = value >= target;
            Image chip = Theme.GlassPanel(row, "PointsChip", 36f);
            RectTransform rt = chip.rectTransform;
            rt.anchorMin = new Vector2(0.70f, 0.5f);
            rt.anchorMax = new Vector2(0.98f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(0f, -60f);
            rt.offsetMax = new Vector2(0f, 60f);
            Text t = UiKit.AddText(chip.transform, "Points", done ? "Done +" + a.Points : "+" + a.Points + " pts", 44, done ? Theme.Gold : Color.white, TextAnchor.MiddleCenter, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Stretch(t.rectTransform);
        }

        static void AddButtonLabel(Transform button, string text)
        {
            Text t = UiKit.AddText(button, "State", text, 46, UiKit.ButtonInk, TextAnchor.MiddleCenter, true);
            UiKit.OnPanel(t);
            t.color = UiKit.ButtonInk;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = 80f;
        }

        /// <summary>One chip row per reward kind inside the claim button: picture and count. Ink on the plate for contrast.</summary>
        void AddChips(Transform button, RewardEntry e)
        {
            if (e.Gems > 0) AddChip(button, art != null ? art.GemIcon : null, Num(e.Gems));
            if (e.Coins > 0) AddChip(button, art != null ? art.CoinIcon : null, Num(e.Coins));
            if (e.PowerUps > 0) AddChip(button, ToyKit.Get("icon_bolt"), "x" + e.PowerUps);
        }

        void AddChip(Transform button, Sprite icon, string count)
        {
            RectTransform chip = UiKit.Rect(button, "Chip");
            var cle = chip.gameObject.AddComponent<LayoutElement>();
            cle.preferredHeight = cle.minHeight = 76f;
            var hl = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 8f;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            Image ic = UiKit.AddImage(chip, "Icon", icon, Color.white);
            var ile = ic.gameObject.AddComponent<LayoutElement>();
            ile.preferredWidth = ile.preferredHeight = 48f;
            Text t = UiKit.AddText(chip, "Count", count, 44, UiKit.ButtonInk, TextAnchor.MiddleLeft, true);
            UiKit.OnPanel(t);
            t.color = UiKit.ButtonInk;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
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

        // ---------------- tabs ----------------

        void BuildTabs()
        {
            string[] names = { "Main", "Daily", "Weekly" };
            RewardTab[] tabs = { RewardTab.Main, RewardTab.Daily, RewardTab.Weekly };
            RectTransform bar = UiKit.Rect(body, "Tabs");
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.offsetMin = new Vector2(Side, TabGap);
            bar.offsetMax = new Vector2(-Side, TabGap + TabH);
            for (int i = 0; i < 3; i++)
            {
                RewardTab tab = tabs[i];
                bool on = tab == Tab;
                Button b = UiKit.AddButton(bar, "Tab" + names[i], names[i], ToyKit.Button(on ? 0 : 2), Color.white, new Vector2(200f, TabH), () => { if (Tab != tab) SelectTab(tab); }, 50);
                b.GetComponent<A11yLabel>().Text = names[i] + " rewards tab" + (on ? ", selected" : "") + (progress.HasClaimable(tab) ? ", something to claim" : "");
                b.GetComponent<Image>().raycastTarget = true;
                RectTransform rt = (RectTransform)b.transform;
                rt.anchorMin = new Vector2(i / 3f, 0f);
                rt.anchorMax = new Vector2((i + 1) / 3f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(i == 0 ? 0f : 8f, 0f);
                rt.offsetMax = new Vector2(i == 2 ? 0f : -8f, 0f);
                if (progress.HasClaimable(tab)) ToyKit.AddBadge(b.transform);   // red dot: something to claim in this tab
            }
        }

        // ---------------- claiming ----------------

        /// <summary>Claims one reward (a row button, or tests). Shows the "Reward claimed" card and redraws the tab on success.</summary>
        public RewardClaimOutcome Claim(RewardTab tab, string id)
        {
            RewardClaimOutcome res = progress.TryClaimReward(tab, id);
            if (res.Result != RewardClaimResult.Ok) return res;
            AudioManager.Play(Sfx.Win);
            Haptics.Pulse();
            UiKit.SetText(CoinText, progress.Coins.ToString());
            UiKit.SetText(GemText, progress.Gems.ToString());
            Render();
            ShowClaimed(res);
            return res;
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
    }
}
