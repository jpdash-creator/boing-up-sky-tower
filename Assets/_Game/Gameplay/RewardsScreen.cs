using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
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
    /// The OpenArt pictures of the Rewards screen (Resources/Art/Rewards: icon_*.png, panel_*.png and panel_slices.json with the 9-slice borders).
    /// Every lookup may return null while a file is missing: the screen then draws plain shapes (CUX-10).
    /// </summary>
    public static class RewardArt
    {
        static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Sprite> panels = new Dictionary<string, Sprite>();
        static Dictionary<string, Vector4> borders;

        public static Sprite Icon(string name)
        {
            Sprite s;
            if (icons.TryGetValue(name, out s) && s != null) return s;
            s = Resources.Load<Sprite>("Art/Rewards/icon_" + name);
            icons[name] = s;
            return s;
        }

        /// <summary>A panel by short name (card_cream, box_grey, tag_claim ...) with its 9-slice border applied; null when the file is missing.</summary>
        public static Sprite Panel(string name)
        {
            Sprite s;
            if (panels.TryGetValue(name, out s) && s != null) return s;
            s = Resources.Load<Sprite>("Art/Rewards/panel_" + name);
            if (s != null && s.border.sqrMagnitude < 0.5f)
            {
                Vector4 b;
                if (Borders().TryGetValue(name, out b) || Borders().TryGetValue("panel_" + name, out b))
                {
                    Sprite withBorder = Sprite.Create(s.texture, s.rect, new Vector2(0.5f, 0.5f), s.pixelsPerUnit, 0, SpriteMeshType.FullRect, b);
                    withBorder.name = "panel_" + name;   // the layout audit treats panel_ sprites as surfaces, not content
                    s = withBorder;
                }
            }
            panels[name] = s;
            return s;
        }

        // {"card_cream": {"w": 512, "h": 256, "border": [l, b, r, t]}, ...}: read with a small pattern, the Gameplay assembly has no JSON library
        static readonly Regex Entry = new Regex("\"([^\"]+)\"\\s*:\\s*\\{[^}]*?\"border\"\\s*:\\s*\\[\\s*(\\d+)\\s*,\\s*(\\d+)\\s*,\\s*(\\d+)\\s*,\\s*(\\d+)\\s*\\]", RegexOptions.Singleline);

        static Dictionary<string, Vector4> Borders()
        {
            if (borders != null) return borders;
            borders = new Dictionary<string, Vector4>();
            TextAsset ta = Resources.Load<TextAsset>("Art/Rewards/panel_slices");
            if (ta == null) return borders;
            foreach (Match m in Entry.Matches(ta.text))
                borders[m.Groups[1].Value] = new Vector4(int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value), int.Parse(m.Groups[4].Value), int.Parse(m.Groups[5].Value));
            return borders;
        }

        /// <summary>Gives the image the panel art (9-sliced, corners drawn about <paramref name="corner"/> units wide) or, when the file is missing, a plain rounded shape in the fallback colour.</summary>
        public static Image Skin(Image img, string panel, Color fallback, float corner)
        {
            Sprite sp = Panel(panel);
            img.preserveAspect = false;
            if (sp == null)
            {
                img.sprite = UiKit.PillSprite;
                img.color = fallback;
                img.type = Image.Type.Sliced;
                return img;
            }
            img.sprite = sp;
            img.color = Color.white;
            img.type = Image.Type.Sliced;
            float borderUnits = Mathf.Max(sp.border.x, sp.border.y, sp.border.z, sp.border.w) / (sp.pixelsPerUnit / 100f);
            img.pixelsPerUnitMultiplier = Mathf.Max(1f, borderUnits / Mathf.Max(8f, corner));
            return img;
        }
    }

    /// <summary>
    /// The Rewards screen (docs/26 section 4), rebuilt on the OpenArt rewards art: tabs Main | Daily | Weekly at the bottom with Back and Claim All.
    /// Main: region circles, the purple "clear all levels" bonus panel of the chosen region, and the star milestone cards. Daily and Weekly: a scene panel
    /// with the reset timer and the points track (Daily) or the tasks done bar (Weekly), then the task cards (cream, yellow when ready with a CLAIM tag, green when claimed).
    /// Everything scrolls in one list, so short screens never lose content. Menu screen only: no per-frame work except the once-a-second countdown text.
    /// </summary>
    public sealed class RewardsScreen
    {
        /// <summary>What the screen needs from the meta scene: the dim overlay layer for the "Reward!" card and the way back. It never owns a canvas.</summary>
        public sealed class Host
        {
            public Func<RectTransform> NewOverlay;
            public Action CloseOverlay;
            public Action Back;
        }

        const float Side = 24f, RowH = 300f, RowGap = 22f;
        const float WalletTop = 16f, WalletH = 100f;
        const float BackSize = 150f, BottomPad = 16f, TabH = 130f, TabGap = 16f;
        const float TimerH = 110f, TrackH = 420f, WeeklyBoxH = 230f, PurpleH = 300f;
        // Main header stack (top to bottom): circles (selected one is 190 tall, starts at -14), 10 gap, name zone, 24 gap, purple panel.
        // The name zone reserves two lines at the current text scale so a wrapped name can never reach the panel (CUX-3).
        const float CircleBottom = 14f + 190f, NameGap = 10f, PanelGap = 44f;
        static float NameZone { get { return Mathf.Ceil(Mathf.Max(36f, UiKit.MinBodyTextPx) * UiKit.TextScale * 3.9f) + 8f; } }
        static float CircleZone { get { return CircleBottom + NameGap + NameZone + PanelGap; } }
        const float BoxW = 140f, BoxH = 190f, RightCol = 300f;
        static readonly Color Orange = new Color(1f, 0.55f, 0.1f, 1f);

        static readonly Color NavyFallback = new Color(0.10f, 0.13f, 0.30f, 1f);

        readonly RectTransform screen;
        readonly ProgressService progress;
        readonly ContentRepository repo;
        readonly ArtCatalog art;
        readonly Host host;

        RectTransform root, body;
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

        /// <summary>Short number for the small reward boxes: 1,000 becomes 1K, 1,500 becomes 1.5K.</summary>
        static string Compact(int n)
        {
            if (n < 1000) return n.ToString(CultureInfo.InvariantCulture);
            double k = n / 1000.0;
            return k.ToString(k == Math.Floor(k) ? "0" : "0.#", CultureInfo.InvariantCulture) + "K";
        }

        static Text Head(Text t) { t.fontStyle = FontStyle.Italic; return t; }

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

            // the blue back arrow, bottom left (it stays on every tab)
            Button back = ToyKit.GlyphButton(root, "Back", "Back", 2, Glyphs.Left, new Vector2(BackSize, BackSize), () => { if (host != null && host.Back != null) host.Back(); });
            UiKit.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(Side, BottomPad), new Vector2(BackSize, BackSize));

            ticker = root.gameObject.AddComponent<RewardsTicker>();
            ticker.OnTick = Tick;
            Render();
        }

        RewardsTicker ticker;

        /// <summary>Switches to a tab (rebuilds the body).</summary>
        public void SelectTab(RewardTab tab)
        {
            Tab = tab;
            Render();
        }

        /// <summary>Chooses the region shown in the Main tab's bonus panel (tests and the circles use it).</summary>
        public void SelectRegion(int index)
        {
            selectedRegion = Mathf.Clamp(index, 1, GameSession.PlayableRegions);
            if (Tab == RewardTab.Main) Render();
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
            timerText = null;
            timerSeconds = -1;
            lastRemaining = Tab == RewardTab.Main ? 0.0 : Remaining().TotalSeconds;

            BuildList();
            BuildTabs();
            BuildClaimAll();
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
            var sb = new StringBuilder("Resets In ");
            if (left.Days > 0) sb.Append(left.Days).Append("d ").Append(left.Hours.ToString("00")).Append(':').Append(left.Minutes.ToString("00"));   // days left: no seconds, keeps the pill short
            else sb.Append(left.Hours.ToString("00")).Append(':').Append(left.Minutes.ToString("00")).Append(':').Append(left.Seconds.ToString("00"));
            UiKit.SetText(timerText, sb.ToString());
        }

        // ---------------- small builders ----------------

        /// <summary>Puts a panel skin on a rectangle that already exists (so the rectangle can also be a Button and own its children).</summary>
        static Image PlateOn(RectTransform host, string panel, Color fallback, float corner)
        {
            Image img = host.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            return RewardArt.Skin(img, panel, fallback, corner);
        }

        static RectTransform FixedHeight(RectTransform rt, float h)
        {
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = h;
            le.flexibleHeight = 0f;
            return rt;
        }

        static RectTransform FixedSize(RectTransform rt, float w, float h)
        {
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = w;
            le.minHeight = le.preferredHeight = h;
            le.flexibleWidth = le.flexibleHeight = 0f;
            return rt;
        }

        /// <summary>A progress bar: the empty bar art with the fill art over it up to the fraction, and the label on top (white with an outline).</summary>
        RectTransform Bar(RectTransform parent, string label, float fraction)
        {
            RectTransform bar = UiKit.Rect(parent, "Backdrop");
            PlateOn(bar, "bar_empty", new Color(0.26f, 0.28f, 0.36f, 1f), 30f);
            if (fraction > 0.001f)
            {
                RectTransform fill = UiKit.Rect(bar, "Fill");   // a plain orange rounded bar inside the frame (the art's own fill has a trophy cap)
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
                fill.offsetMin = new Vector2(8f, 8f);
                fill.offsetMax = new Vector2(-8f, -8f);
                Image fi = fill.gameObject.AddComponent<Image>();
                fi.sprite = UiKit.PillSprite; fi.type = Image.Type.Sliced; fi.color = Orange; fi.raycastTarget = false;
            }
            Text t = UiKit.AddText(bar, "Count", label, 42, Color.white, TextAnchor.MiddleCenter, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Stretch(t.rectTransform);
            return bar;
        }

        Sprite CoinSprite { get { return art != null ? art.CoinIcon : null; } }
        Sprite GemSprite { get { return art != null ? art.GemIcon : null; } }
        static Sprite BoltSprite { get { return RewardArt.Icon("bolt") ?? ToyKit.Get("icon_bolt"); } }
        Sprite StarSprite { get { return RewardArt.Icon("star1") ?? (art != null ? art.StarFilled : null); } }

        /// <summary>Picture of a task, by its id (the owner's icon map), with the old kit icons as the fallback.</summary>
        static Sprite TaskIcon(string id)
        {
            string name; string fallback;
            switch (id)
            {
                case "d_login": name = "key"; fallback = "icon_daily"; break;
                case "d_play5": name = "hourglass"; fallback = "icon_dial"; break;
                case "d_level1": name = "flag1"; fallback = "icon_flag"; break;
                case "d_level3": name = "flag3"; fallback = "icon_flag"; break;
                case "d_combo3": name = "flame"; fallback = "icon_flame"; break;
                case "d_hard": name = "helmet"; fallback = "icon_bolt"; break;
                case "d_nowall": name = "wall"; fallback = "icon_hand"; break;
                case "d_gemlevel": name = "gem"; fallback = "icon_gem"; break;
                case "d_stars2": name = "star1"; fallback = "icon_star"; break;
                case "d_powerup": name = "bolt"; fallback = "icon_bolt"; break;
                case "w_login7": name = "calendar"; fallback = "icon_daily"; break;
                case "w_play100": case "w_play150": name = "clock"; fallback = "icon_dial"; break;
                case "w_levels15": name = "levels"; fallback = "icon_flag"; break;
                case "w_stars20": name = "medal"; fallback = "icon_star"; break;
                case "w_nowall": name = "wall"; fallback = "icon_hand"; break;
                case "w_stars3": name = "star3"; fallback = "icon_star"; break;
                default:
                    if (id != null && id.StartsWith("w_combos", StringComparison.Ordinal)) { name = "flame"; fallback = "icon_flame"; }
                    else if (id == "s_225") { name = "trophy"; fallback = "icon_star"; }
                    else { name = "star1"; fallback = "icon_star"; }
                    break;
            }
            return RewardArt.Icon(name) ?? ToyKit.Get(fallback);
        }

        /// <summary>
        /// One reward box: the gift box art unstretched on top (grey = not ready, yellow = ready) and the real reward as a small chip under it
        /// (picture and count). Nothing overlaps: the chip row starts under the gift.
        /// </summary>
        void AddBox(RectTransform parent, Sprite icon, string count, bool ready, Color textColor)
        {
            RectTransform box = UiKit.Rect(parent, "Box");
            FixedSize(box, BoxW, BoxH);
            Sprite gift = RewardArt.Panel(ready ? "box_yellow" : "box_grey");
            Image g = UiKit.AddImage(box, "Gift", gift, gift != null ? Color.white : (ready ? new Color(1f, 0.85f, 0.35f, 1f) : new Color(0.55f, 0.57f, 0.64f, 1f)));
            g.preserveAspect = true;
            UiKit.Place(g.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(104f, 104f));

            RectTransform chip = UiKit.Rect(box, "Chip");
            chip.anchorMin = new Vector2(0f, 0f);
            chip.anchorMax = new Vector2(1f, 0f);
            chip.pivot = new Vector2(0.5f, 0f);
            chip.offsetMin = new Vector2(0f, 4f);
            chip.offsetMax = new Vector2(0f, 4f + 76f);
            var hl = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 4f;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            Image ic = UiKit.AddImage(chip, "Icon", icon, Color.white);
            var ile = ic.gameObject.AddComponent<LayoutElement>();
            ile.preferredWidth = ile.preferredHeight = 44f;
            Text t = UiKit.AddText(chip, "Count", count, 42, textColor, TextAnchor.MiddleLeft, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        /// <summary>The reward of an entry as boxes (gems, coins, power-ups), right-aligned in a row.</summary>
        RectTransform AddBoxes(RectTransform card, RewardEntry e, bool ready, Color textColor)
        {
            RectTransform row = UiKit.Rect(card, "Boxes");
            row.anchorMin = row.anchorMax = new Vector2(1f, 0f);
            row.pivot = new Vector2(1f, 0f);
            row.anchoredPosition = new Vector2(-Side, 16f);
            row.sizeDelta = new Vector2(RightCol, BoxH);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 12f;
            hl.childAlignment = TextAnchor.LowerRight;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            if (e.Gems > 0) AddBox(row, GemSprite, Compact(e.Gems), ready, textColor);
            if (e.Coins > 0) AddBox(row, CoinSprite, Compact(e.Coins), ready, textColor);
            if (e.PowerUps > 0) AddBox(row, BoltSprite, "x" + e.PowerUps, ready, textColor);
            return row;
        }

        /// <summary>The green CLAIM tag at the top right of a ready card (a picture only: the whole card is the button).</summary>
        void AddTag(RectTransform card)
        {
            RectTransform tag = UiKit.Rect(card, "Tag");
            UiKit.Place(tag, new Vector2(1f, 1f), new Vector2(-Side, -14f), new Vector2(210f, 76f));
            PlateOn(tag, "tag_claim", new Color(0.25f, 0.72f, 0.28f, 1f), 24f);
            Text t = UiKit.AddText(tag, "Label", "Claim", 42, Color.white, TextAnchor.MiddleCenter, true);
            Head(t).horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Stretch(t.rectTransform);
        }

        void AddTick(RectTransform card)
        {
            Image tick = UiKit.AddImage(card, "Check", RewardArt.Icon("check") ?? Glyphs.Check, Color.white);
            UiKit.Place(tick.rectTransform, new Vector2(1f, 1f), new Vector2(-Side - 70f, -14f), new Vector2(76f, 76f));
        }

        // ---------------- the list ----------------

        void BuildList()
        {
            RectTransform viewport = UiKit.Rect(body, "Viewport");
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(Side, BottomReserve());
            viewport.offsetMax = new Vector2(-Side, -(WalletTop + WalletH + 12f));
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
            vl.spacing = RowGap;
            vl.padding = new RectOffset(0, 0, 4, 72);   // the last card scrolls fully clear of the tab row
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
                foreach (RewardEntry e in defs.StarMilestones) AddCard(content, e, true);
            }
            else if (Tab == RewardTab.Daily)
            {
                BuildSceneHeader(content, true);
                foreach (RewardEntry a in defs.DailyActions) AddCard(content, a, false);
            }
            else
            {
                BuildSceneHeader(content, false);
                foreach (RewardEntry e in defs.WeeklyTasks) AddCard(content, e, true);
            }
        }

        /// <summary>Space under the list: Back and Claim All, then the tab row.</summary>
        static float BottomReserve() { return BottomPad + BackSize + TabGap + TabH + TabGap; }

        // ---------------- Daily and Weekly header: the region scene, the timer and the track ----------------

        void BuildSceneHeader(RectTransform content, bool daily)
        {
            float h = 28f + TimerH + 22f + (daily ? TrackH : WeeklyBoxH) + 16f;
            RectTransform scene = UiKit.Rect(content, "Scene");
            FixedHeight(scene, h);
            scene.gameObject.AddComponent<RectMask2D>();

            // the stage backdrop of the latest unlocked region, cover-fitted and cropped
            int latest = Mathf.Clamp(progress.HighestUnlockedRegionIndex(), 1, GameSession.PlayableRegions);
            RegionArt ra = art != null ? (art.ForRegion(latest) ?? art.ForRegion(1)) : null;
            Sprite bgSprite = ra != null ? ra.BgFar : null;
            Image bg = UiKit.AddImage(scene, "Backdrop", bgSprite, bgSprite != null ? Color.white : new Color(0.25f, 0.42f, 0.7f, 1f));
            bg.preserveAspect = false;
            UiKit.Stretch(bg.rectTransform);
            if (bgSprite != null)
            {
                var arf = bg.gameObject.AddComponent<AspectRatioFitter>();
                arf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                arf.aspectRatio = bgSprite.rect.width / bgSprite.rect.height;
            }

            // the navy timer pill: hourglass and "Resets In hh:mm:ss"
            RectTransform pill = UiKit.Rect(scene, "TimerPill");
            pill.anchorMin = pill.anchorMax = new Vector2(0.5f, 1f);
            pill.pivot = new Vector2(0.5f, 1f);
            pill.anchoredPosition = new Vector2(0f, -28f);
            pill.sizeDelta = new Vector2(880f, TimerH);
            PlateOn(pill, "tab_navy", NavyFallback, 40f);   // the navy tab art makes the dark pill (timer_pill is a coin)
            var hl = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 16f;
            hl.padding = new RectOffset(28, 28, 8, 8);
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            Image ic = UiKit.AddImage(pill, "Icon", RewardArt.Icon("hourglass") ?? ToyKit.Get("icon_dial"), Color.white);
            var ile = ic.gameObject.AddComponent<LayoutElement>();
            ile.preferredWidth = ile.preferredHeight = 84f;
            timerText = UiKit.AddText(pill, "Timer", "", 52, Color.white, TextAnchor.MiddleCenter, true);
            Head(timerText).horizontalOverflow = HorizontalWrapMode.Overflow;

            float top = 28f + TimerH + 22f;
            if (daily) BuildDailyTrack(scene, top);
            else BuildWeeklySummary(scene, top);
        }

        /// <summary>Weekly: the tasks done bar (each task has its own reward in its card).</summary>
        void BuildWeeklySummary(RectTransform scene, float top)
        {
            IReadOnlyList<RewardEntry> tasks = progress.RewardDefs.WeeklyTasks;
            int done = 0;
            foreach (RewardEntry e in tasks) if (progress.RewardProgress(RewardTab.Weekly, e) >= progress.RewardTarget(RewardTab.Weekly, e)) done++;
            RectTransform panel = UiKit.Rect(scene, "SummaryPanel");
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.offsetMin = new Vector2(Side, -(top + WeeklyBoxH));
            panel.offsetMax = new Vector2(-Side, -top);
            Image glass = Theme.GlassPanel(panel, "Glass", 40f);
            UiKit.Stretch(glass.rectTransform);
            Text title = UiKit.AddText(panel, "Title", "Tasks Done", 52, Color.white, TextAnchor.MiddleLeft, true);
            Head(title).horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(32f, -96f);
            trt.offsetMax = new Vector2(-32f, -20f);
            RectTransform bar = Bar(panel, done + "/" + tasks.Count, tasks.Count > 0 ? done / (float)tasks.Count : 0f);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.offsetMin = new Vector2(32f, 28f);
            bar.offsetMax = new Vector2(-32f, 28f + 84f);
        }

        /// <summary>Daily: points n/100 with a reward picture and count at 20, 40, 60, 80 and 100 points; a tick once claimed, a tap claims a ready one.</summary>
        void BuildDailyTrack(RectTransform scene, float top)
        {
            IReadOnlyList<RewardEntry> ms = progress.RewardDefs.DailyMilestones;
            int n = ms.Count;
            if (n == 0) return;
            int value = progress.DailyPoints();
            int max = ms[n - 1].Target;

            RectTransform panelRt = UiKit.Rect(scene, "TrackPanel");
            panelRt.anchorMin = new Vector2(0f, 1f);
            panelRt.anchorMax = new Vector2(1f, 1f);
            panelRt.pivot = new Vector2(0.5f, 1f);
            panelRt.offsetMin = new Vector2(Side, -(top + TrackH));
            panelRt.offsetMax = new Vector2(-Side, -top);
            Image glass = Theme.GlassPanel(panelRt, "Glass", 40f);
            UiKit.Stretch(glass.rectTransform);

            Image star = UiKit.AddImage(panelRt, "Icon", StarSprite, Color.white);
            UiKit.Place(star.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -14f), new Vector2(72f, 72f));
            Text vt = UiKit.AddText(panelRt, "Value", value + " / " + max + " Points", 52, Color.white, TextAnchor.MiddleLeft, true);
            Head(vt).horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform vrt = vt.rectTransform;
            vrt.anchorMin = new Vector2(0f, 1f);
            vrt.anchorMax = new Vector2(1f, 1f);
            vrt.pivot = new Vector2(0f, 1f);
            vrt.offsetMin = new Vector2(112f, -92f);
            vrt.offsetMax = new Vector2(-24f, -14f);

            RectTransform inner = UiKit.Rect(panelRt, "Inner");
            inner.anchorMin = new Vector2(0f, 0f);
            inner.anchorMax = new Vector2(1f, 1f);
            inner.offsetMin = new Vector2(12f, 12f);
            inner.offsetMax = new Vector2(-12f, -104f);

            RectTransform barBg = UiKit.Rect(inner, "Backdrop");
            barBg.anchorMin = new Vector2(0f, 1f);
            barBg.anchorMax = new Vector2(1f, 1f);
            barBg.pivot = new Vector2(0.5f, 1f);
            barBg.offsetMin = new Vector2(0f, -26f);
            barBg.offsetMax = Vector2.zero;
            PlateOn(barBg, "bar_empty", new Color(0f, 0f, 0f, 0.45f), 13f);
            float pos = Mathf.Clamp01(TrackPosition(ms, value));
            if (pos > 0.001f)
            {
                RectTransform fill = UiKit.Rect(barBg, "Fill");
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = new Vector2(pos, 1f);
                fill.offsetMin = new Vector2(3f, 3f);
                fill.offsetMax = new Vector2(-3f, -3f);
                Image fi = fill.gameObject.AddComponent<Image>();
                fi.sprite = UiKit.PillSprite; fi.type = Image.Type.Sliced; fi.color = Orange; fi.raycastTarget = false;
            }
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
            cell.offsetMax = new Vector2(0f, -34f);
            bool claimed = progress.IsRewardClaimed(RewardTab.Daily, e);
            bool reached = progress.RewardProgress(RewardTab.Daily, e) >= progress.RewardTarget(RewardTab.Daily, e);
            if (reached && !claimed)
            {
                // a ready reward is a real button (the cell is 190 units wide, 240 high)
                Image hit = cell.gameObject.AddComponent<Image>();
                hit.color = new Color(1f, 1f, 1f, 0.001f);
                var btn = cell.gameObject.AddComponent<Button>();
                btn.targetGraphic = hit;
                string id = e.Id;
                btn.onClick.AddListener(() => Claim(RewardTab.Daily, id));
                cell.gameObject.AddComponent<A11yLabel>().Text = e.Target + " points: claim " + Describe(e);
            }

            Sprite pic; string count;
            PrimaryReward(e, out pic, out count);
            Sprite ring = RewardArt.Panel("medal_frame");   // the gold ring around the reward picture
            if (ring != null)
            {
                Image frame = UiKit.AddImage(cell, "Frame", ring, reached ? Color.white : new Color(0.72f, 0.74f, 0.82f, 1f));
                UiKit.Place(frame.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(104f, 104f));
            }
            Image icon = UiKit.AddImage(cell, "Icon", pic, reached ? Color.white : new Color(0.72f, 0.74f, 0.82f, 1f));
            UiKit.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(80f, 80f));
            bool extra = (e.Gems > 0 ? 1 : 0) + (e.Coins > 0 ? 1 : 0) + (e.PowerUps > 0 ? 1 : 0) > 1;
            if (extra)
            {
                Image bolt = UiKit.AddImage(icon.transform, "Extra", e.PowerUps > 0 ? BoltSprite : CoinSprite, Color.white);
                UiKit.Place(bolt.rectTransform, new Vector2(1f, 0f), new Vector2(8f, -4f), new Vector2(36f, 36f));
            }
            if (claimed)
            {
                Image tick = UiKit.AddImage(icon.transform, "Claimed", RewardArt.Icon("check") ?? Glyphs.Check, Color.white);
                UiKit.Place(tick.rectTransform, new Vector2(0f, 0f), new Vector2(-6f, -6f), new Vector2(44f, 44f));
            }
            Text c = UiKit.AddText(cell, "Count", count, 42, reached ? Color.white : Theme.OnGlassSoft, TextAnchor.UpperCenter, true);
            c.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform crt = c.rectTransform;
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.offsetMin = new Vector2(0f, -186f);
            crt.offsetMax = new Vector2(0f, -110f);
            Text t = UiKit.AddText(cell, "Target", e.Target.ToString(CultureInfo.InvariantCulture), 42, reached ? Theme.Gold : Theme.OnGlassSoft, TextAnchor.UpperCenter, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform trt = t.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(0f, -266f);
            trt.offsetMax = new Vector2(0f, -190f);
        }

        void PrimaryReward(RewardEntry e, out Sprite pic, out string count)
        {
            if (e.Gems > 0) { pic = GemSprite; count = Compact(e.Gems); }
            else if (e.Coins > 0) { pic = CoinSprite; count = Compact(e.Coins); }
            else { pic = BoltSprite; count = "x" + e.PowerUps; }
        }

        // ---------------- Main header: region circles and the purple bonus panel ----------------

        void BuildMainHeader(RectTransform content)
        {
            RectTransform header = UiKit.Rect(content, "MainHeader");
            FixedHeight(header, CircleZone + PurpleH + 16f);

            int regions = GameSession.PlayableRegions;
            for (int i = 1; i <= regions; i++) BuildCircle(header, repo != null ? repo.TryGetRegionByIndex(i) : null, i, regions);
            BuildPurplePanel(header);
        }

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
            name.fontStyle = FontStyle.Italic;
            // Never break inside a word (1080x2520 showed "Mushroo / m Grove"): break only at the spaces and let a word that is a little
            // wider than its slot spill into the free space beside it. The size is already at the 16 sp floor, so it cannot shrink further.
            name.text = region.Name.Replace(' ', '\n');
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform nrt = name.rectTransform;
            nrt.anchorMin = new Vector2(0f, 1f);
            nrt.anchorMax = new Vector2(1f, 1f);
            nrt.pivot = new Vector2(0.5f, 1f);
            // fixed name zone below the biggest circle: the raised/bigger selected circle cannot push it into the panel
            name.verticalOverflow = VerticalWrapMode.Truncate;
            nrt.offsetMin = new Vector2(0f, -(CircleBottom + NameGap + NameZone));
            nrt.offsetMax = new Vector2(0f, -(CircleBottom + NameGap));
        }

        void BuildPurplePanel(RectTransform header)
        {
            RewardEntry e = null;
            foreach (RewardEntry r in progress.RewardDefs.RegionRewards) if (r.Target == selectedRegion) e = r;
            RegionDef region = repo != null ? repo.TryGetRegionByIndex(selectedRegion) : null;
            if (e == null || region == null) return;
            int value = progress.RewardProgress(RewardTab.Main, e), target = progress.RewardTarget(RewardTab.Main, e);
            bool claimed = progress.IsRewardClaimed(RewardTab.Main, e);
            bool ready = !claimed && value >= target;

            RectTransform panel = UiKit.Rect(header, "PurplePanel");
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.offsetMin = new Vector2(0f, -(CircleZone + PurpleH));
            panel.offsetMax = new Vector2(0f, -CircleZone);
            PlateOn(panel, "panel_purple", new Color(0.62f, 0.42f, 0.82f, 1f), 40f);
            if (ready) MakeClaimable(panel, RewardTab.Main, e, region.Name + " bonus: claim " + Describe(e));

            Text title = UiKit.AddText(panel, "Title", "Clear All " + target + " Levels", 52, Color.white, TextAnchor.MiddleLeft, true);
            Head(title).horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(32f, -100f);
            trt.offsetMax = new Vector2(-(RightCol + 40f), -22f);

            RectTransform bar = Bar(panel, Mathf.Min(value, target) + "/" + target, target > 0 ? Mathf.Clamp01(value / (float)target) : 0f);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(32f, -196f);
            bar.offsetMax = new Vector2(-(RightCol + 40f), -112f);

            if (ready) AddTagAt(panel, new Vector2(32f, -208f));
            else if (claimed) AddTickAt(panel, new Vector2(32f, -208f));
            // the bonus the region pays, as boxes on the right
            RectTransform boxes = AddBoxes(panel, e, ready, Color.white);
            boxes.anchorMin = boxes.anchorMax = new Vector2(1f, 0.5f);
            boxes.pivot = new Vector2(1f, 0.5f);
            boxes.anchoredPosition = new Vector2(-32f, 0f);
        }

        void AddTagAt(RectTransform parent, Vector2 topLeft)
        {
            RectTransform tag = UiKit.Rect(parent, "Tag");
            UiKit.Place(tag, new Vector2(0f, 1f), topLeft, new Vector2(210f, 76f));
            PlateOn(tag, "tag_claim", new Color(0.25f, 0.72f, 0.28f, 1f), 24f);
            Text t = UiKit.AddText(tag, "Label", "Claim", 42, Color.white, TextAnchor.MiddleCenter, true);
            Head(t).horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Stretch(t.rectTransform);
        }

        void AddTickAt(RectTransform parent, Vector2 topLeft)
        {
            Image tick = UiKit.AddImage(parent, "Check", RewardArt.Icon("check") ?? Glyphs.Check, Color.white);
            UiKit.Place(tick.rectTransform, new Vector2(0f, 1f), topLeft, new Vector2(76f, 76f));
        }

        // ---------------- cards ----------------

        /// <summary>Turns a rectangle that has a skin into the claim button for an entry.</summary>
        void MakeClaimable(RectTransform host, RewardTab tab, RewardEntry e, string a11y)
        {
            Image img = host.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;
            var btn = host.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            string id = e.Id;
            btn.onClick.AddListener(() => { AudioManager.Play(Sfx.Tap); Claim(tab, id); });
            host.gameObject.AddComponent<A11yLabel>().Text = a11y;
        }

        string TitleOf(RewardEntry e)
        {
            switch (Tab)
            {
                case RewardTab.Main: return "Collect " + e.Target + " Stars";
                case RewardTab.Daily: return e.Title;
                default: return e.Title;
            }
        }

        /// <summary>
        /// One task card: icon, title, progress bar and the reward boxes. <paramref name="claimable"/> cards (stars, weekly tasks) are claimed by tapping
        /// them when ready (yellow with a CLAIM tag) and turn green with a tick once claimed; Daily action cards only show points (green when done).
        /// </summary>
        void AddCard(RectTransform list, RewardEntry e, bool claimable)
        {
            int value = progress.RewardProgress(Tab, e);
            int target = progress.RewardTarget(Tab, e);
            bool claimed = claimable ? progress.IsRewardClaimed(Tab, e) : value >= target;
            bool ready = claimable && !claimed && value >= target;

            RectTransform row = UiKit.Rect(list, "Row_" + e.Id);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = RowH;
            le.flexibleHeight = 0f;
            string skin = claimed ? "card_green" : (ready ? "card_yellow" : "card_cream");
            Color fb = claimed ? new Color(0.45f, 0.62f, 0.2f, 1f) : (ready ? new Color(1f, 0.9f, 0.2f, 1f) : new Color(0.88f, 0.9f, 0.97f, 1f));
            PlateOn(row, skin, fb, 44f);
            Color ink = claimed ? Color.white : Theme.Ink;

            Image icon = UiKit.AddImage(row, "Icon", TaskIcon(e.Id), Color.white);
            UiKit.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(Side, 0f), new Vector2(120f, 120f));

            Text title = UiKit.AddText(row, "Title", TitleOf(e), 44, ink, TextAnchor.LowerLeft, true);
            if (!claimed) UiKit.OnPanel(title);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            RectTransform trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(168f, -152f);
            trt.offsetMax = new Vector2(-(RightCol + 40f), -14f);

            int shown = Mathf.Min(value, target);
            string label = (e.Stat == RewardStat.PlaySeconds) ? (shown / 60) + "/" + (target / 60) + " min" : Num(shown) + "/" + Num(target);
            RectTransform bar = Bar(row, label, target > 0 ? Mathf.Clamp01(value / (float)target) : 0f);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.offsetMin = new Vector2(168f, 40f);
            bar.offsetMax = new Vector2(-(RightCol + 40f), 40f + 84f);

            if (claimable) AddBoxes(row, e, ready, ink);
            else
            {
                // a Daily action pays points, shown as one box with the star
                RectTransform boxes = UiKit.Rect(row, "Boxes");
                boxes.anchorMin = boxes.anchorMax = new Vector2(1f, 0f);
                boxes.pivot = new Vector2(1f, 0f);
                boxes.anchoredPosition = new Vector2(-Side, 16f);
                boxes.sizeDelta = new Vector2(RightCol, BoxH);
                var hl = boxes.gameObject.AddComponent<HorizontalLayoutGroup>();
                hl.childAlignment = TextAnchor.LowerRight;
                hl.childControlWidth = hl.childControlHeight = true;
                hl.childForceExpandWidth = hl.childForceExpandHeight = false;
                AddBox(boxes, StarSprite, "+" + e.Points, false, ink);
            }
            if (ready) { AddTag(row); MakeClaimable(row, Tab, e, TitleOf(e) + ": claim " + Describe(e)); }
            else if (claimed) AddTick(row);
            else if (claimable) row.gameObject.AddComponent<A11yLabel>().Text = TitleOf(e) + ": " + label;
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

        // ---------------- tabs and Claim All ----------------

        void BuildTabs()
        {
            string[] names = { "Main", "Daily", "Weekly" };
            RewardTab[] tabs = { RewardTab.Main, RewardTab.Daily, RewardTab.Weekly };
            RectTransform bar = UiKit.Rect(body, "Tabs");
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            float y = BottomPad + BackSize + TabGap;
            bar.offsetMin = new Vector2(Side, y);
            bar.offsetMax = new Vector2(-Side, y + TabH);
            for (int i = 0; i < 3; i++)
            {
                RewardTab tab = tabs[i];
                bool on = tab == Tab;
                Sprite sp = RewardArt.Panel(on ? "tab_yellow" : "tab_navy") ?? ToyKit.Button(on ? 0 : 2);
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

        /// <summary>The green Claim All button, bottom right; it only exists while something in the tab is ready.</summary>
        void BuildClaimAll()
        {
            if (!progress.HasClaimable(Tab)) return;
            RewardTab tab = Tab;
            Sprite sp = RewardArt.Panel("button_green") ?? ToyKit.Button(1);
            Button b = UiKit.AddButton(body, "ClaimAll", "", sp, Color.white, new Vector2(430f, 130f), () => ClaimAll(tab), 46);
            b.GetComponent<A11yLabel>().Text = "Claim all ready rewards";
            b.GetComponent<Image>().raycastTarget = true;
            RectTransform rt = (RectTransform)b.transform;
            UiKit.Place(rt, new Vector2(1f, 0f), new Vector2(-Side, BottomPad + (BackSize - 130f) * 0.5f), new Vector2(430f, 130f));
            Text t = UiKit.AddText(rt, "Label", "Claim All", 50, Color.white, TextAnchor.MiddleCenter, true);
            Head(t).horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Stretch(t.rectTransform);
        }

        // ---------------- claiming ----------------

        /// <summary>Claims one reward (a card, a track cell or tests). Shows the "Reward!" card and redraws the tab on success.</summary>
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
    }
}
