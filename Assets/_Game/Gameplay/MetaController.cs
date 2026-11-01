using System;
using System.Collections.Generic;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// Meta scene: main menu, world map, level select (15 levels per region, stars, locks), level info popup,
    /// region gem-skip popup and settings. Built in code from the sliced art (no prefabs yet).
    /// </summary>
    public sealed class MetaController : MonoBehaviour
    {
        // World map (Tools/art_pipeline/build_world_map.py): 10 stitched segments of 3 regions each, bottom to top, in the Resources/Map chunks.
        [System.Serializable] class MapChunkMeta { public string file; public int height; }
        [System.Serializable] class MapSpanMeta { public int bottom; public int height; }
        // The painted world-map pages (owner, 2026-10-24): one tall image per 5 regions, with the 5 marker positions (x across, y up from the bottom, 0..1).
        [System.Serializable] class PaintedMarker { public float x, y; public string tagSide; }   // tagSide "below": the name tag is tried below the icon first
        [System.Serializable] class PaintedPage { public string file; public PaintedMarker[] markers = new PaintedMarker[0]; }
        [System.Serializable] class PaintedPages { public int width = 1080, height = 1922; public PaintedPage[] pages = new PaintedPage[0]; }
        [System.Serializable] class WorldMapMeta
        {
            public int width = 1080, totalHeight = 15764;
            public MapChunkMeta[] chunks = new MapChunkMeta[0];
            public MapSpanMeta[] segmentSpans = new MapSpanMeta[0];
        }

        const float MapTopBar = 330f;                                              // fixed bar (home, title, wallet); the map scrolls below it
        static readonly float[] PinRise = { 0.24f, 0.50f, 0.78f };                 // height of a segment's three regions, bottom to top
        static readonly float[] PinSide = { 0.30f, 0.68f, 0.34f };                 // left / right / left, so the pins zig-zag up the map
        // Hand-placed pins (owner list 6, 2026-10-20): region index -> (x across the map, height inside the region's segment), both 0..1.
        static readonly Dictionary<int, Vector2> PinSpots = new Dictionary<int, Vector2>
        {
            { 1, new Vector2(0.20f, 0.30f) },    // on the green grass of the floating meadow
            { 2, new Vector2(0.11f, 0.56f) },    // at the big purple mushroom on the left
            { 7, new Vector2(0.17f, 0.26f) },    // by the big tree on the left
            { 8, new Vector2(0.40f, 0.46f) },    // on the sand dune hump
            { 9, new Vector2(0.81f, 0.64f) },    // just above the red flag of the pirate ship
            { 10, new Vector2(0.88f, 0.14f) },   // on the blue toy block at the right
        };
        static float lastMapOffset = -1f; static int lastMapCurrent = -1;                                          // scroll position kept while visiting a region's levels
        RectTransform mapContent, mapViewport;
        ScrollRect mapScroll;
        float mapAspect = 1f, mapWidthSeen = -1f, mapHeightSeen = -1f;
        int mapCurrent;

        ContentRepository repo;
        ProgressService progress;
        ArtCatalog art;
        Canvas canvas;
        RectTransform screenRoot; // full-bleed root (backgrounds)
        RectTransform screen;     // safe-area content of the current screen: all text and buttons live under here
        RectTransform popup;
        RectTransform momo;
        Vector2 momoBase;
        Text coinText, gemText;
        string currentRegionId;

        public string CurrentScreen { get; private set; }

        void Start()
        {
            GameSession.EnsureInit();
            GameSettings.ApplyTextScale();
            AudioManager.SetMusic(MusicTrack.Meta);
            repo = GameSession.Repo;
            progress = GameSession.Progress;
            art = GameSession.Art;
            canvas = UiKit.CreateCanvas("MetaUi", 0);
            canvas.transform.SetParent(transform, false);
            progress.Changed += RefreshWallet;
            progress.RecordLogin();   // reward statistics: today's login (saves only the first time each day)

            if (!string.IsNullOrEmpty(GameSession.PendingRegionId))
            {
                string id = GameSession.PendingRegionId;
                GameSession.PendingRegionId = null;
                ShowLevels(repo.GetRegion(id));
            }
            else ShowMenu();
        }

        void OnDestroy()
        {
            if (progress != null) progress.Changed -= RefreshWallet;
        }

        void Update()
        {
            if (progress != null) progress.FeedPlayTime(Time.unscaledDeltaTime, true);   // reward statistics: menu play time
            if (mapContent != null && mapViewport != null && (Mathf.Abs(mapViewport.rect.width - mapWidthSeen) > 0.5f || Mathf.Abs(mapViewport.rect.height - mapHeightSeen) > 0.5f) && mapViewport.rect.width > 2f)
            {
                float norm = mapScroll.verticalNormalizedPosition;
                SizeMap();
                mapScroll.verticalNormalizedPosition = norm;   // the window changed shape: keep looking at the same part of the map
            }
            if (momo != null && !GameSettings.ReducedMotion) momo.anchoredPosition = momoBase + new Vector2(0f, Mathf.Sin(Time.unscaledTime * 2.2f) * 22f);
        }

        /// <summary>
        /// Lays out the level screen inside its rectangle (wallet above, Endless button below): the region title, then the 15 level circles
        /// (3 columns x 5 rows, level 1 at the bottom). The gaps above the title, between title and circles and under the circles are equal, so
        /// the title is centred between the top and the first circle and the circles are centred between the title and the Endless button, on
        /// every screen shape. Circles are 150 units (never under 130, the 48 dp touch minimum) and their stars scale with them.
        /// </summary>
        sealed class LevelGridFit : MonoBehaviour
        {
            public RectTransform Title, TitleExtra;
            public RectTransform[] Nodes;
            public int[] Cols, Rows;
            public bool[] Current;

            void Start() { Apply(); }
            void OnRectTransformDimensionsChange() { Apply(); }

            public void Apply()
            {
                if (Nodes == null) return;
                float h = ((RectTransform)transform).rect.height;
                if (h < 10f) return;
                const float titleH = 100f, gMin = 20f, rgMin = 8f, baseNode = 190f, starExtent = 50f / 190f;   // star rows hang 50 base units under a circle
                float rowFactor = 1f + starExtent;                                  // row height (circle + stars) as a multiple of the circle size
                float nodePx = Mathf.Clamp((h - titleH - 3f * gMin - 4f * rgMin) / (5f * rowFactor), 130f, 150f);
                float rowCell = nodePx * rowFactor;
                float spare = h - titleH - 5f * rowCell - 4f * rgMin;                  // what is left for the three equal gaps and wider row gaps
                float g = Mathf.Clamp(spare / 3f, 6f, 90f);
                float rg = rgMin + Mathf.Max(0f, spare - 3f * g) / 4f;
                float pitch = rowCell + rg;
                float k = nodePx / baseNode;
                if (Title != null)
                {
                    Title.anchorMin = new Vector2(0f, 0f);
                    Title.anchorMax = new Vector2(1f, 0f);
                    Title.pivot = new Vector2(0.5f, 0.5f);
                    Title.sizeDelta = new Vector2(TitleExtra != null ? -48f - 180f : -48f, titleH);
                    Title.anchoredPosition = new Vector2(TitleExtra != null ? -90f : 0f, h - g - titleH * 0.5f);
                    if (TitleExtra != null)
                    {
                        TitleExtra.anchorMin = TitleExtra.anchorMax = new Vector2(1f, 0f);
                        TitleExtra.pivot = new Vector2(1f, 0.5f);
                        TitleExtra.anchoredPosition = new Vector2(-24f, h - g - titleH * 0.5f);
                    }
                }
                for (int i = 0; i < Nodes.Length; i++)
                {
                    RectTransform rt = Nodes[i];
                    if (rt == null) continue;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.2f + 0.3f * Cols[i], 0f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(0f, g + Rows[i] * pitch + nodePx * starExtent + nodePx * 0.5f);
                    rt.localScale = Vector3.one * (k * (Current[i] ? 1.12f : 1f));
                }
            }
        }

        // ---------------- helpers ----------------

        RectTransform NewScreen(string name)
        {
            if (screenRoot != null) Destroy(screenRoot.gameObject);
            ClosePopup();
            momo = null;
            coinText = null;
            gemText = null;
            starText = null;
            levelsText = null;
            regionStarsText = null;
            screenRoot = UiKit.Rect(canvas.transform, name);
            UiKit.Stretch(screenRoot);
            screen = UiKit.SafeArea(screenRoot);
            CurrentScreen = name;
            return screen;
        }

        static void DropOutline(Text t)
        {
            var o = t.GetComponent<Outline>();
            if (o != null) o.enabled = false;
        }

        Sprite Btn(int i) { return ToyKit.Button(i == 1 ? 1 : 0) ?? (art != null ? ArtCatalog.Pick(art.Buttons, i) : null); }

        void SkyBackground() { Theme.SkyBackground(screenRoot); }

        /// <summary>The level screen shows the painted background of ITS region (owner, 2026-10-24), calmed with a blue veil so the path and nodes read.</summary>
        void LevelBackground(RegionArt ra)
        {
            if (ra == null || ra.BgFar == null) { SkyBackground(); return; }
            Background(ra.BgFar, Color.white);
            Image veil = UiKit.AddImage(screenRoot, "BgVeil", null, new Color(0.08f, 0.2f, 0.42f, 0.30f));
            veil.preserveAspect = false;
            UiKit.Stretch(veil.rectTransform);
            veil.transform.SetSiblingIndex(1);
        }

        void Background(Sprite sprite, Color fallback)
        {
            Image bg = UiKit.AddImage(screenRoot, "Bg", sprite, sprite != null ? Color.white : fallback);
            bg.preserveAspect = false;
            UiKit.Stretch(bg.rectTransform);
            bg.transform.SetAsFirstSibling();
            // A cover-fit background: scales to fill any aspect without stretching the art.
            if (sprite != null)
            {
                var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = sprite.rect.width / sprite.rect.height;
            }
        }

        Text starText;

        /// <summary>Stretches a header between the icon button (left) and the wallet panel (right) so the two can never overlap.</summary>
        static void BesideWallet(RectTransform rt, float yFromTop, float height, float rightInset = 310f)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(190f, yFromTop - height);
            rt.offsetMax = new Vector2(-rightInset, yFromTop);
        }

        /// <summary>Wallet: one dark rounded panel at the top-right with three aligned rows (coins, gems, stars): [icon][number].</summary>
        void AddWallet(float panelAlpha = 0.92f)
        {
            const float rowH = 84f, panelW = 270f;
            Image panel = Theme.GlassPanel(screen, "WalletPanel");
            panel.color = new Color(Theme.Glass.r, Theme.Glass.g, Theme.Glass.b, Theme.Glass.a * Mathf.Clamp01(panelAlpha / 0.92f));
            UiKit.Place(panel.rectTransform, new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(panelW, rowH * 3f + 30f));
            coinText = WalletRow(panel.transform, 0, art != null ? art.CoinIcon : null, Color.white);
            gemText = WalletRow(panel.transform, 1, art != null ? art.GemIcon : null, new Color(0.75f, 0.92f, 1f));
            starText = WalletRow(panel.transform, 2, art != null ? art.StarFilled : null, new Color(1f, 0.92f, 0.5f));
            RefreshWallet();
        }

        // Centre of mass of each currency sprite (fraction of its width and height, measured from the art): the icons are shifted by it so
        // coin, gem and star sit on one vertical line and one centre line in every wallet row (owner list 9).
        static Vector2 IconMass(Sprite icon)
        {
            if (icon == null) return new Vector2(0.5f, 0.5f);
            switch (icon.name)
            {
                case "cur_coin_single": return new Vector2(0.594f, 0.617f);
                case "cur_gem_single": return new Vector2(0.446f, 0.478f);
                case "cur_star_filled": return new Vector2(0.494f, 0.542f);
                default: return new Vector2(0.5f, 0.5f);
            }
        }

        /// <summary>One wallet row: icon on the left of its column (centred on its visual mass), value right-aligned. Column 0 is the left one.</summary>
        Text WalletRow(Transform parent, int row, Sprite icon, Color numberColor, int column = 0, float valueWidth = 140f, float x0 = 0f, float colWidth = 270f, int fontSize = 48, float iconX = 22f)
        {
            const float box = 68f;
            float y = -15f - row * 84f;
            Vector2 mass = IconMass(icon);
            Image ic = UiKit.AddImage(parent, "Icon" + column + "_" + row, icon, Color.white);
            UiKit.Place(ic.rectTransform, new Vector2(0f, 1f), new Vector2(x0 + iconX + box * 0.5f + (0.5f - mass.x) * box, y - 8f - box * 0.5f + (mass.y - 0.5f) * box), new Vector2(box, box));
            ic.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Text t = UiKit.AddText(parent, "Value" + column + "_" + row, "", fontSize, numberColor, TextAnchor.MiddleRight, true);
            UiKit.Place((RectTransform)t.transform, new Vector2(0f, 1f), new Vector2(x0 + colWidth - 22f - valueWidth, y), new Vector2(valueWidth, 84f));
            return t;
        }

        Text levelsText, regionStarsText;

        /// <summary>
        /// Currency pills (coins, gems, total stars) in one row to the right of the back button (owner issue 15.4: nothing leaves its pill). Used by
        /// the world map and the level screen.
        /// </summary>
        void AddPillRow()
        {
            RectTransform row = UiKit.Rect(screen, "CurrencyBar");
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(190f, -128f);
            row.offsetMax = new Vector2(-16f, -16f);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 10f;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = true;
            coinText = ToyKit.WalletPillFlex(row, "CoinsPill", art != null ? art.CoinIcon : null, Color.white, 112f);
            gemText = ToyKit.WalletPillFlex(row, "GemsPill", art != null ? art.GemIcon : null, new Color(0.75f, 0.92f, 1f), 112f);
            starText = ToyKit.WalletPillFlex(row, "StarsPill", art != null ? art.StarFilled : null, new Color(1f, 0.92f, 0.5f), 112f);
            RefreshWallet();
        }

        /// <summary>Level screen header: the three currency pills, then two region progress pills (levels cleared and stars of this region).</summary>
        void AddLevelsWallet(RegionDef region)
        {
            AddPillRow();
            RectTransform row = UiKit.Rect(screen, "RegionBar");
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(190f, -236f);
            row.offsetMax = new Vector2(-16f, -140f);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 10f;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = true;
            int cleared = 0;
            foreach (string id in region.Levels) if (progress.IsLevelCleared(id)) cleared++;
            levelsText = ToyKit.WalletPillFlex(row, "LevelsPill", ToyKit.Get("icon_flag"), Color.white, 96f);
            regionStarsText = ToyKit.WalletPillFlex(row, "RegionStarsPill", art != null ? art.StarFilled : null, new Color(1f, 0.92f, 0.5f), 96f);
            UiKit.SetText(levelsText, cleared + "/" + region.Levels.Count);
            UiKit.SetText(regionStarsText, progress.StarsInRegion(region) + "/" + region.Levels.Count * 3);
        }

        void RefreshWallet()
        {
            if (coinText != null) UiKit.SetText(coinText, progress.Coins.ToString());
            if (gemText != null) UiKit.SetText(gemText, progress.Gems.ToString());
            if (starText != null)
            {
                int stars = 0;
                for (int i = 1; i <= GameSession.PlayableRegions; i++)
                {
                    RegionDef r = repo.TryGetRegionByIndex(i);
                    if (r != null) stars += progress.StarsInRegion(r);
                }
                UiKit.SetText(starText, stars.ToString());
            }
        }

        /// <summary>Round icon button at the top-left (home or back arrow): no text, with an accessibility label.</summary>
        void AddBack(string label, Action action)
        {
            Button b = ToyKit.GlyphButton(screen, "Back", label, 2, Glyphs.Left, new Vector2(150f, 150f), action);
            UiKit.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(150f, 150f));
        }

        // ---------------- main menu ----------------

        public void ShowMenu()
        {
            NewScreen("Menu");
            lastMapOffset = -1f;   // the map opens on the current region again after a visit to the menu
            int latest = progress != null ? Mathf.Clamp(progress.HighestUnlockedRegionIndex(), 1, Mathf.Max(1, GameSession.PlayableRegions)) : 1;   // the latest UNLOCKED region (docs/23)
            RegionArt meadow = art != null ? (art.ForRegion(latest) ?? art.ForRegion(1)) : null;
            Background(meadow != null ? meadow.BgFar : null, new Color(0.08f, 0.09f, 0.16f, 1f));
            AddCurrencyBar();
            Sprite logo = ToyKit.Get("logo");
            if (logo != null)
            {
                Image lg = UiKit.AddImage(screen, "Logo", logo, Color.white);
                UiKit.Place(lg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(860f, 352f));
                lg.rectTransform.pivot = new Vector2(0.5f, 1f);
                lg.raycastTarget = false;
            }
            {
                // the SELECTED character, drawn from its parts like the character screen (rebuilt on every visit, so a new choice shows at once)
                RectTransform doll = MomoDoll.Build(screen, "Momo", 540f, CharacterCatalog.Selected, art);
                UiKit.Place(doll, new Vector2(0.5f, 0.40f), Vector2.zero, doll.sizeDelta);
                doll.pivot = new Vector2(0.5f, 0.5f);
                momo = doll;
                momoBase = momo.anchoredPosition;
            }
            AddRewardsButton();
            Text v = UiKit.AddText(screen, "Version", "v" + Application.version, 30, new Color(1f, 1f, 1f, 0.7f));
            UiKit.Place((RectTransform)v.transform, new Vector2(0.5f, 0f), new Vector2(0f, ToyKit.TabBarReserve + 4f), new Vector2(300f, 44f));   // bottom centre, clear of the bundle timer button at the left
            AddTabBar(ToyKit.TabPlay);
            BundleTimerButton.Attach(screen, progress, art, BundleHost(), RefreshWallet);   // timer bundle offer, bottom left (docs/26)
            MaybeShowHeroReveal();
        }

        // ---------------- new hero reveal (docs/21) ----------------

        /// <summary>Shows the one-time "New hero joins you!" pop-up for the first unlocked hero not revealed yet (home and map). The next one follows after OK.</summary>
        void MaybeShowHeroReveal()
        {
            if (progress == null) return;
            System.Collections.Generic.List<string> pending = progress.PendingHeroReveals();
            if (pending.Count == 0) return;
            ShowHeroReveal(CharacterCatalog.Get(pending[0]));
        }

        /// <summary>The reveal card for one hero with its single OK button. OK saves the reveal (so it shows once) and moves on to the next pending hero.</summary>
        public void ShowHeroReveal(CharacterDef who)
        {
            if (who == null) return;
            RectTransform card = HeroRevealCard.Build(NewOverlay(), who, art);
            string id = who.Id;
            HeroRevealCard.AddOk(card, () =>
            {
                if (progress != null) progress.MarkHeroRevealed(id);
                ClosePopup();
                MaybeShowHeroReveal();
            });
        }

        /// <summary>
        /// Rewards (daily gift) button, owner issue 29: bottom right, directly ABOVE the Heroes tab with ToyKit.RewardsButtonGap between them (same on home and the world map).
        /// 140 px (at least 48 dp), red dot when something is claimable. Subscription moved into the bottom bar; Bundles live in the Shop's Bundles tab.
        /// </summary>
        void AddRewardsButton()
        {
            bool claim = progress != null && progress.HasClaimable();
            Button b = ToyKit.IconButton(screen, "Rewards", claim ? "Rewards, something to claim" : "Rewards", 2, ToyKit.Get("icon_daily"), new Vector2(ToyKit.RewardsButtonSize, ToyKit.RewardsButtonSize), ShowRewards, 0.5f);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(ToyKit.RewardsButtonSize, ToyKit.RewardsButtonSize);
            rt.anchoredPosition = new Vector2(-18f, ToyKit.RewardsButtonBottom);   // 18 = bar side margin 12 + half the bar gap 6: right edge lines up with the Heroes button
            if (claim) ToyKit.AddBadge(b.transform);   // red dot: something to claim
        }

        /// <summary>Currency pills (coins, gems, total stars) with the round settings gear at the right (owner issue 14, SS2).</summary>
        void AddCurrencyBar()
        {
            RectTransform row = UiKit.Rect(screen, "CurrencyBar");
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -16f);
            row.sizeDelta = new Vector2(-32f, 128f);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 10f;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = true;
            coinText = ToyKit.WalletPillFlex(row, "CoinsPill", art != null ? art.CoinIcon : null, Color.white, 104f);
            gemText = ToyKit.WalletPillFlex(row, "GemsPill", art != null ? art.GemIcon : null, new Color(0.75f, 0.92f, 1f), 104f);
            starText = ToyKit.WalletPillFlex(row, "StarsPill", art != null ? art.StarFilled : null, new Color(1f, 0.92f, 0.5f), 104f);
            Button gear = ToyKit.IconButton(row, "Settings", "Settings", 2, ToyKit.Get("icon_settings"), new Vector2(130f, 128f), ShowSettings, 0.62f);
            var le = gear.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 130f;
            le.preferredWidth = 130f;
            le.flexibleWidth = 0f;
            RefreshWallet();
        }

        /// <summary>
        /// The persistent bottom tab bar (docs/16 section C). With <paramref name="withPlay"/> false (World Map and Level screen, owner issue 16.3) the
        /// middle PLAY button is left out. The bar holds Shop, Subscription, PLAY, Heroes (owner issue 29; Chests and Bundles are tabs of the Shop; Rewards is the gift button above Heroes).
        /// </summary>
        void AddTabBar(int selected, bool withPlay = true)
        {
            var items = new System.Collections.Generic.List<ToyKit.BarItem>
            {
                new ToyKit.BarItem { Icon = "icon_shop", Caption = "Shop", A11y = "Shop" },
                new ToyKit.BarItem { Icon = "icon_crown", Caption = "Subscription", A11y = "Subscription" },
            };
            if (withPlay) items.Add(new ToyKit.BarItem { Icon = "icon_play", Caption = "PLAY", A11y = "Play", Raised = true });
            items.Add(new ToyKit.BarItem { Icon = "icon_heroes", Caption = "Heroes", A11y = "Characters" });
            int[] tabOf = withPlay ? new[] { ToyKit.TabShop, ToyKit.TabSubscription, ToyKit.TabPlay, ToyKit.TabHeroes } : new[] { ToyKit.TabShop, ToyKit.TabSubscription, ToyKit.TabHeroes };
            ToyKit.BuildIconBar(screen, items.ToArray(), selected < 0 ? -1 : System.Array.IndexOf(tabOf, selected), i => OnTab(tabOf[i]));
        }

        void OnTab(int tab)
        {
            switch (tab)
            {
                case ToyKit.TabShop: if (CurrentScreen != "Shop") ShowShop(); break;
                case ToyKit.TabPlay: ShowMap(); break;
                case ToyKit.TabSubscription: if (CurrentScreen != "Subscription") ShowSubscription(); break;
                case ToyKit.TabChests: if (CurrentScreen != "Shop" || shop == null || shop.Tab != ShopScreen.TabChests) ShowTreasureChests(); break;
                case ToyKit.TabHeroes: if (CurrentScreen != "Characters") ShowCharacters(); break;
            }
        }

        // ---------------- treasure chests ----------------

        Action treasureReturn;
        bool treasureWithPlay = true;

        /// <summary>The Treasure Chest screen while it is showing (null on every other screen). Tests drive purchases through it.</summary>
        public TreasureChestScreen CurrentTreasure { get { return CurrentScreen == "Shop" && shop != null ? shop.Treasure : null; } }

        /// <summary>Where Back goes from the chests: the screen that opened them. The bar keeps PLAY only where the caller had it (home, shop).</summary>
        Action TreasureReturnFor(string from)
        {
            switch (from)
            {
                case "Map": treasureWithPlay = false; return ShowMap;
                case "Levels":
                    {
                        treasureWithPlay = false;
                        RegionDef r = currentRegionId != null ? repo.GetRegion(currentRegionId) : null;
                        if (r == null) return ShowMenu;
                        return () => ShowLevels(r);
                    }
                case "Shop": treasureWithPlay = true; return () => ShowShop();
                default: treasureWithPlay = true; return ShowMenu;
            }
        }

        /// <summary>Treasure Chest screen (docs/22 point 3): opened from the bottom bar Chests tab and the shop's first section; Back returns to the caller.</summary>
        public void ShowTreasureChests()
        {
            // docs/26: the Treasure Shop is the Chests tab of the shop (ShopChrome look); Back goes home like every shop page.
            ShowShop(ShopScreen.TabChests);
        }

        // ---------------- shop ----------------

        ShopScreen shop;

        /// <summary>The shop while its screen is showing (null on every other screen). Tests drive purchases through it.</summary>
        public ShopScreen CurrentShop { get { return CurrentScreen == "Shop" ? shop : null; } }

        /// <summary>Shop (docs/14): four tabs, only Gems and Coins is live. Opens on that tab by default.</summary>
        public void ShowShop(int tab = ShopScreen.TabPowerups, int section = ShopScreen.GemsSection)
        {
            if (CurrentScreen != "Shop" && CurrentScreen != "Subscription") treasureReturn = TreasureReturnFor(CurrentScreen);   // Back returns to the caller (home, map, levels)
            NewScreen("Shop");
            SkyBackground();
            progress.LaunchPromoActive = GameSettings.LaunchPromo;
            var host = new ShopScreen.Host
            {
                OpenPopup = (title, body, height) => OpenPopup(title, body, height),
                PopupButton = (label, primary, y, action) => PopupButton(label, Btn(primary ? 1 : 0), Color.white, y, action),
                ClosePopup = ClosePopup,
                NewOverlay = NewOverlay,
                CloseOverlay = ClosePopup,
                OpenTreasureChests = ShowTreasureChests,
                OpenSubscription = ShowSubscription,
            };
            shop = new ShopScreen(screen, progress, ShopCatalog.FromRepository(repo), art, host);
            shop.Build(tab, section);
            Action shopBack = treasureReturn;
            AddBack("Back", () => { if (shopBack != null) shopBack(); else ShowMenu(); });
            coinText = shop.CoinText;     // RefreshWallet keeps these two current after every purchase
            gemText = shop.GemText;
            RefreshWallet();
        }

        // ---------------- subscription (docs/26 section 2) ----------------

        SubscriptionScreen subscription;
        Action subscriptionReturn;

        /// <summary>The Subscription page while it is showing (null on every other screen). Tests drive purchases through it.</summary>
        public SubscriptionScreen CurrentSubscription { get { return CurrentScreen == "Subscription" ? subscription : null; } }

        /// <summary>Subscription page: opened from the home button and from the shop header; Back returns to the caller.</summary>
        public void ShowSubscription()
        {
            if (CurrentScreen != "Subscription") subscriptionReturn = CurrentScreen == "Shop" ? (Action)(() => ShowShop()) : CurrentScreen == "Map" ? (Action)ShowMap : ShowMenu;
            NewScreen("Subscription");
            Background(null, new Color(0.30f, 0.18f, 0.10f, 1f));   // brown wood look of the shop pages
            var host = new ShopScreen.Host
            {
                OpenPopup = (title, body, height) => OpenPopup(title, body, height),
                PopupButton = (label, primary, y, action) => PopupButton(label, Btn(primary ? 1 : 0), Color.white, y, action),
                ClosePopup = ClosePopup,
                NewOverlay = NewOverlay,
                CloseOverlay = ClosePopup,
            };
            subscription = new SubscriptionScreen(screen, progress, progress.SubscriptionDefs, art, host);
            subscription.Build();
            Action back = subscriptionReturn;
            AddBack("Back", () => { if (back != null) back(); else ShowMenu(); });
            AddTabBar(-1);
            coinText = subscription.CoinText;
            gemText = subscription.GemText;
            starText = subscription.StarText;
            RefreshWallet();
        }

        // ---------------- rewards (docs/26 section 4) ----------------

        RewardsScreen rewards;

        /// <summary>The Rewards screen while it is showing (null on every other screen). Tests drive claims through it.</summary>
        public RewardsScreen CurrentRewards { get { return CurrentScreen == "Rewards" ? rewards : null; } }

        public void ShowRewards() { ShowRewards(RewardTab.Main); }

        /// <summary>Rewards: tabs Main | Daily | Weekly, opened from the home button; Back returns to the home screen.</summary>
        public void ShowRewards(RewardTab tab)
        {
            NewScreen("Rewards");
            Background(null, new Color(0.30f, 0.34f, 0.64f, 1f));   // the blue-purple quest look
            progress.RecordLogin();
            var host = new RewardsScreen.Host { NewOverlay = NewOverlay, CloseOverlay = ClosePopup, Back = ShowMenu };
            rewards = new RewardsScreen(screen, progress, repo, art, host);
            rewards.Build(tab);   // its own Back arrow sits bottom left
            coinText = rewards.CoinText;
            gemText = rewards.GemText;
            RefreshWallet();
        }

        // ---------------- characters ----------------

        CharactersScreen characters;

        public CharactersScreen CurrentCharacters { get { return CurrentScreen == "Characters" ? characters : null; } }

        /// <summary>Characters: the chosen character on the land stage (the current region's backdrop, grass and the dirt oval) and the list of characters below.</summary>
        public void ShowCharacters()
        {
            NewScreen("Characters");
            Stage stage = StageScene.Build(screenRoot, art, progress, StageScene.CharacterLayout());
            characters = new CharactersScreen(screen, progress, art, new CharactersScreen.Host { Stage = stage });
            characters.Build();
            AddBack("Back", ShowMenu);
            AddTabBar(ToyKit.TabHeroes);
            coinText = characters.CoinText;
            gemText = characters.GemText;
            RefreshWallet();
        }

        // ---------------- world map ----------------

        // ---------------- world map: 6 swipe pages of 5 region medallions (docs/16 section D) ----------------

        const int MapPages = 6, RegionsPerPage = 5;

        /// <summary>Snaps a horizontally scrolling set of equal pages to the nearest page when the finger lifts.</summary>
        sealed class PageSnap : MonoBehaviour, IBeginDragHandler, IEndDragHandler
        {
            public ScrollRect Scroll;
            public int Pages = 1;
            public RectTransform Content, Viewport;
            public Image[] Dots;
            bool dragging;
            float target = -1f;

            public int Page { get { return Mathf.Clamp(Mathf.RoundToInt(Scroll.horizontalNormalizedPosition * (Pages - 1)), 0, Pages - 1); } }
            public void OnBeginDrag(PointerEventData e) { dragging = true; target = -1f; }
            public void OnEndDrag(PointerEventData e) { dragging = false; target = Page / (float)Mathf.Max(1, Pages - 1); }
            public void GoTo(int page) { Scroll.horizontalNormalizedPosition = Mathf.Clamp01(page / (float)Mathf.Max(1, Pages - 1)); }

            void Update()
            {
                if (Viewport != null && Content != null)
                {
                    float w = Viewport.rect.width * Pages;
                    if (Mathf.Abs(Content.sizeDelta.x - w) > 0.5f) Content.sizeDelta = new Vector2(w, 0f);
                }
                if (!dragging && target >= 0f)
                {
                    float n = Mathf.MoveTowards(Scroll.horizontalNormalizedPosition, target, Time.unscaledDeltaTime * 3.5f);
                    Scroll.horizontalNormalizedPosition = n;
                    if (Mathf.Approximately(n, target)) target = -1f;
                }
                if (Dots != null)
                {
                    int p = Page;
                    for (int i = 0; i < Dots.Length; i++) Dots[i].color = i == p ? new Color(1f, 0.82f, 0.2f, 1f) : new Color(1f, 1f, 1f, 0.65f);
                }
            }
        }

        static Color PaletteColor(string name)
        {
            switch ((name ?? "").ToLowerInvariant())
            {
                case "green": return new Color(0.42f, 0.78f, 0.34f);
                case "pink": return new Color(1f, 0.55f, 0.72f);
                case "sky blue": return new Color(0.45f, 0.75f, 1f);
                case "purple": return new Color(0.66f, 0.45f, 0.9f);
                case "teal": return new Color(0.25f, 0.78f, 0.75f);
                case "gold": return new Color(1f, 0.8f, 0.3f);
                case "aqua": return new Color(0.35f, 0.82f, 0.95f);
                case "mint": return new Color(0.55f, 0.92f, 0.72f);
                case "jade": return new Color(0.28f, 0.72f, 0.5f);
                case "red": return new Color(0.92f, 0.32f, 0.3f);
                case "orange": return new Color(1f, 0.6f, 0.25f);
                case "white": return new Color(0.92f, 0.96f, 1f);
                case "blue": return new Color(0.3f, 0.55f, 0.95f);
                default: return new Color(0.7f, 0.78f, 0.95f);
            }
        }

        /// <summary>The swipe pages of medallions (docs/16 section D). Not the main map for now: the owner prefers the painted world map.</summary>
        public void ShowMapPages()
        {
            NewScreen("Map");
            currentRegionId = null;
            mapContent = null;
            mapScroll = null;
            SkyBackground();
            AddPillRow();
            AddBack("Menu", ShowMenu);
            AddTabBar(-1, false);

            RectTransform vp = UiKit.Rect(screen, "MapPages");
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(0f, ToyKit.TabBarReserve + 70f);
            vp.offsetMax = new Vector2(0f, -150f);
            vp.gameObject.AddComponent<RectMask2D>();
            var vhit = vp.gameObject.AddComponent<Image>();
            vhit.color = new Color(0f, 0f, 0f, 0f);
            RectTransform content = UiKit.Rect(vp, "PagesContent");
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(1080f * MapPages, 0f);
            var scroll = vp.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = vp;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var snap = vp.gameObject.AddComponent<PageSnap>();
            snap.Scroll = scroll;
            snap.Pages = MapPages;
            snap.Content = content;
            snap.Viewport = vp;

            int current = Mathf.Clamp(progress.HighestUnlockedRegionIndex(), 1, Mathf.Max(1, GameSession.PlayableRegions));
            float[] lanes = { 0.3f, 0.7f, 0.3f, 0.7f, 0.3f };
            for (int p = 0; p < MapPages; p++)
            {
                RectTransform page = UiKit.Rect(content, "Page" + (p + 1));
                page.anchorMin = new Vector2(p / (float)MapPages, 0f);
                page.anchorMax = new Vector2((p + 1) / (float)MapPages, 1f);
                page.offsetMin = page.offsetMax = Vector2.zero;
                ToyKit.Ribbon(page, (p * RegionsPerPage + 1) + " - " + (p * RegionsPerPage + RegionsPerPage), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(460f, 164f));
                float top = 200f;   // below the ribbon
                float[] mx = new float[RegionsPerPage], my = new float[RegionsPerPage];
                for (int k = 0; k < RegionsPerPage; k++) { mx[k] = lanes[k]; my[k] = 0.17f + k * 0.15f; }
                for (int k = 0; k + 1 < RegionsPerPage; k++)
                {
                    for (int d = 1; d <= 9; d++)
                    {
                        float t = d / 10f;
                        float curve = (1f - Mathf.Cos(t * Mathf.PI)) * 0.5f;   // S-curve: the path bends instead of running straight
                        Image dot = UiKit.AddImage(page, "Dot", Theme.RoundSprite, new Color(1f, 1f, 1f, 0.8f));
                        dot.type = Image.Type.Sliced;
                        dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(Mathf.Lerp(mx[k], mx[k + 1], curve), Mathf.Lerp(my[k], my[k + 1], t));
                        dot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                        dot.rectTransform.anchoredPosition = Vector2.zero;
                        dot.rectTransform.sizeDelta = new Vector2(20f, 20f);
                    }
                }
                for (int k = 0; k < RegionsPerPage; k++)
                {
                    int idx = p * RegionsPerPage + k + 1;
                    RegionDef region = repo.TryGetRegionByIndex(idx);
                    if (region == null) continue;
                    AddMedallion(page, region, mx[k], my[k], idx == current);
                }
            }

            // page dots
            var dots = new Image[MapPages];
            RectTransform dotRow = UiKit.Rect(screen, "PageDots");
            UiKit.Place(dotRow, new Vector2(0.5f, 0f), new Vector2(0f, ToyKit.TabBarReserve + 18f), new Vector2(MapPages * 44f, 30f));
            for (int i = 0; i < MapPages; i++)
            {
                dots[i] = UiKit.AddImage(dotRow, "Dot" + i, Theme.RoundSprite, Color.white);
                dots[i].type = Image.Type.Sliced;
                dots[i].rectTransform.anchorMin = dots[i].rectTransform.anchorMax = new Vector2(0f, 0.5f);
                dots[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                dots[i].rectTransform.anchoredPosition = new Vector2(22f + i * 44f, 0f);
                dots[i].rectTransform.sizeDelta = new Vector2(26f, 26f);
            }
            snap.Dots = dots;
            StartCoroutine(OpenMapPage(snap, (current - 1) / RegionsPerPage));
        }

        System.Collections.IEnumerator OpenMapPage(PageSnap snap, int page)
        {
            yield return null;
            if (snap != null) snap.GoTo(page);
            yield return null;
            if (snap != null) snap.GoTo(page);
        }

        /// <summary>One round region medallion: the region colour, its signature picture (or a padlock), the name and the stars earned.</summary>
        void AddMedallion(RectTransform page, RegionDef region, float lane, float frac, bool isCurrent)
        {
            bool playable = progress.IsRegionPlayable(region);
            bool unlocked = playable && progress.IsRegionUnlocked(region);
            const float size = 210f;
            Color col = unlocked ? PaletteColor(region.Theme.Palette != null && region.Theme.Palette.Count > 0 ? region.Theme.Palette[0] : null) : new Color(0.36f, 0.42f, 0.55f);
            RegionDef r = region;
            Button b = UiKit.AddButton(page, "Region" + region.Index, "", Theme.RoundSprite, col, new Vector2(size, size), () => OnRegionTapped(r), 40);
            b.GetComponent<A11yLabel>().Text = region.Name + (unlocked ? "" : (playable ? ", locked" : ", coming soon"));
            var brt = (RectTransform)b.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(lane, frac);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = new Vector2(size, size);
            brt.localScale = Vector3.one * (isCurrent ? 1.12f : 1f);
            Image face = b.GetComponent<Image>();
            face.type = Image.Type.Sliced;
            face.pixelsPerUnitMultiplier = Mathf.Max(0.2f, 44f / (size * 0.5f));
            Image ring = UiKit.AddImage(b.transform, "Ring", Glyphs.Ring, isCurrent ? new Color(1f, 0.82f, 0.2f, 1f) : UiKit.ButtonInk);
            ring.rectTransform.anchorMin = Vector2.zero;
            ring.rectTransform.anchorMax = Vector2.one;
            ring.rectTransform.offsetMin = new Vector2(-8f, -8f);
            ring.rectTransform.offsetMax = new Vector2(8f, 8f);
            RegionArt ra = art != null ? art.ForRegion(region.Index) : null;
            Sprite pic = unlocked && ra != null && ra.Signature != null && ra.Signature.Length > 0 ? ra.Signature[0] : ToyKit.Get(unlocked ? "icon_flag" : "icon_lock");
            Image icon = UiKit.AddImage(b.transform, "Icon", pic, Color.white);
            icon.preserveAspect = true;
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            icon.rectTransform.sizeDelta = new Vector2(size * 0.62f, size * 0.62f);
            Text name = UiKit.AddText(page, "Name" + region.Index, region.Name, 42, Color.white, TextAnchor.MiddleCenter, true);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            var nrt = (RectTransform)name.transform;
            nrt.anchorMin = nrt.anchorMax = new Vector2(lane, frac);
            nrt.pivot = new Vector2(0.5f, 1f);
            nrt.anchoredPosition = new Vector2(0f, -(size * 0.5f + 12f));
            nrt.sizeDelta = new Vector2(400f, 56f);
            if (unlocked)
            {
                int stars = progress.StarsInRegion(region);
                RectTransform pillRow = UiKit.Rect(page, "Stars" + region.Index);
                pillRow.anchorMin = pillRow.anchorMax = new Vector2(lane, frac);
                pillRow.pivot = new Vector2(0.5f, 1f);
                pillRow.anchoredPosition = new Vector2(0f, -(size * 0.5f + 74f));
                pillRow.sizeDelta = new Vector2(230f, 62f);
                Text st = ToyKit.WalletPillFlex(pillRow, "StarsPill", art != null ? art.StarFilled : null, new Color(1f, 0.92f, 0.5f), 62f);
                UiKit.SetText(st, stars + "/" + region.Levels.Count * 3);
                var hl = pillRow.gameObject.AddComponent<HorizontalLayoutGroup>();
                hl.childControlWidth = hl.childControlHeight = true;
                hl.childForceExpandWidth = hl.childForceExpandHeight = true;
            }
        }

        /// <summary>The painted world map (the 10 stitched region paintings with a pin for every region): the main map (owner, 2026-10-24).</summary>
        public void ShowMap()
        {
            BuildMap();
            MaybeShowHeroReveal();
        }

        void BuildMap()
        {
            NewScreen("Map");
            currentRegionId = null;
            mapContent = null;
            Image bgFill = UiKit.AddImage(screenRoot, "Bg", null, new Color(0.10f, 0.14f, 0.25f, 1f));
            UiKit.Stretch(bgFill.rectTransform);
            bgFill.transform.SetAsFirstSibling();

            WorldMapMeta meta = new WorldMapMeta();
            TextAsset metaAsset = Resources.Load<TextAsset>("Map/world_map");
            if (metaAsset != null) meta = JsonUtility.FromJson<WorldMapMeta>(metaAsset.text);
            // Painted pages replace the old stitched segments when they exist (regions 1-5 on page 1, 6-10 on page 2, ...).
            PaintedPages painted = null;
            TextAsset pagesAsset = Resources.Load<TextAsset>("Map/painted_pages");
            if (pagesAsset != null)
            {
                painted = JsonUtility.FromJson<PaintedPages>(pagesAsset.text);
                if (painted == null || painted.pages == null || painted.pages.Length == 0) painted = null;
            }
            if (painted != null)
            {
                meta = new WorldMapMeta { width = painted.width, totalHeight = painted.height * painted.pages.Length, chunks = new MapChunkMeta[painted.pages.Length], segmentSpans = new MapSpanMeta[0] };
                for (int pi = 0; pi < painted.pages.Length; pi++) meta.chunks[pi] = new MapChunkMeta { file = painted.pages[pi].file, height = painted.height };
                bgFill.color = new Color(0.55f, 0.78f, 1f, 1f);
            }
            int total = Mathf.Max(1, meta.totalHeight);

            // Scrolling viewport below the fixed top bar. Its content keeps the art's aspect, so the map is never stretched.
            mapViewport = UiKit.Rect(screenRoot, "MapViewport");   // full-bleed (under the notch and system bars); the HUD sits in the safe area above it
            mapViewport.SetSiblingIndex(screen.GetSiblingIndex());
            UiKit.Stretch(mapViewport);
            mapViewport.offsetMax = Vector2.zero;   // the map fills the width; the top bar floats over it
            mapViewport.offsetMin = new Vector2(0f, ToyKit.TabBarReserve + ToyKit.RewardsStrip);   // the icon tab bar and the Rewards button above Heroes sit under the map
            mapViewport.gameObject.AddComponent<RectMask2D>();
            Image hit = mapViewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            mapContent = UiKit.Rect(mapViewport, "MapContent");
            mapContent.anchorMin = new Vector2(0f, 1f);
            mapContent.anchorMax = new Vector2(1f, 1f);
            mapContent.pivot = new Vector2(0.5f, 1f);
            mapContent.offsetMin = mapContent.offsetMax = Vector2.zero;
            mapContent.gameObject.AddComponent<CanvasGroup>().alpha = 0f;   // shown once the first layout pass has placed it
            mapAspect = (float)meta.width / total;   // content height follows the viewport width (SizeMap), so the art is never stretched
            var scroll = mapViewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = mapContent;
            scroll.viewport = mapViewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 60f;
            mapScroll = scroll;

            float bottomPx = 0f;
            foreach (MapChunkMeta c in meta.chunks)
            {
                Texture2D tex = Resources.Load<Texture2D>("Map/" + c.file);
                var raw = UiKit.Rect(mapContent, c.file).gameObject.AddComponent<RawImage>();
                raw.texture = tex;
                raw.color = tex != null ? Color.white : new Color(0.2f, 0.3f, 0.5f, 1f);
                raw.raycastTarget = false;
                raw.rectTransform.anchorMin = new Vector2(0f, bottomPx / total);
                raw.rectTransform.anchorMax = new Vector2(1f, (bottomPx + c.height) / total);
                raw.rectTransform.offsetMin = raw.rectTransform.offsetMax = Vector2.zero;
                bottomPx += c.height;
            }

            float currentRise = 0f;
            pinTags.Clear();
            tagLayout = painted;
            int current = Mathf.Clamp(progress.HighestUnlockedRegionIndex(), 1, Mathf.Max(1, GameSession.PlayableRegions));
            mapCurrent = current;
            for (int i = 1; i <= 30; i++)
            {
                RegionDef region = repo.TryGetRegionByIndex(i);
                if (region == null) continue;
                if (painted != null)
                {
                    int pg = (i - 1) / 5, mk = (i - 1) % 5;
                    if (pg >= painted.pages.Length || mk >= painted.pages[pg].markers.Length) continue;   // that page is not painted yet
                    PaintedMarker m = painted.pages[pg].markers[mk];
                    float prise = (pg * painted.height + m.y * painted.height) / total;
                    AddRegionPin(region, new Vector2(m.x, prise), 1, 0f, 0f, TagKit.MapTextMaxWidth, m.tagSide == "below" ? 1 : 0);
                    if (i == current) currentRise = prise;
                    continue;
                }
                int seg = (i - 1) / 3, k = (i - 1) % 3;
                MapSpanMeta span = seg < meta.segmentSpans.Length ? meta.segmentSpans[seg] : new MapSpanMeta { bottom = seg * 1538, height = 1538 };
                float inSeg = PinRise[k];
                float side = seg % 2 == 0 ? PinSide[k] : 1f - PinSide[k];
                Vector2 spot;
                if (PinSpots.TryGetValue(i, out spot)) { side = spot.x; inSeg = spot.y; }
                float rise = (span.bottom + span.height * inSeg) / total;
                AddRegionPin(region, new Vector2(side, rise));
                if (i == current) currentRise = rise;
            }

            if (painted != null)
            {
                var replanner = mapContent.gameObject.AddComponent<MapTagReplanner>();
                replanner.Content = mapContent;
                replanner.OnSize = ReplanTags;   // plans the tags against the real content size, again when it changes
            }

            // Soft cloud bands over the joins between the painted pages (owner, 2026-10-24: clouds separate the six sets).
            if (painted != null)
            {
                for (int pj = 1; pj < painted.pages.Length; pj++)
                {
                    float seam = pj * (float)painted.height / total;
                    float[] cx = { 0.12f, 0.34f, 0.56f, 0.78f, 0.95f };
                    for (int c = 0; c < cx.Length; c++)
                    {
                        Image cloud = UiKit.AddImage(mapContent, "SeamCloud", Theme.RoundSprite, new Color(1f, 1f, 1f, 0.92f));
                        cloud.type = Image.Type.Sliced;
                        cloud.preserveAspect = false;
                        cloud.pixelsPerUnitMultiplier = 0.3f;
                        cloud.raycastTarget = false;
                        cloud.rectTransform.anchorMin = cloud.rectTransform.anchorMax = new Vector2(cx[c], seam);
                        cloud.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                        cloud.rectTransform.sizeDelta = new Vector2(380f + (c % 2) * 90f, 150f + (c % 3) * 30f);
                        cloud.rectTransform.anchoredPosition = new Vector2(0f, (c % 2 == 0 ? 18f : -14f));
                    }
                }
            }

            // Fixed overlay: dark top bar with the home button, the title and the wallet.
            Image bar = UiKit.AddImage(screen, "TopBacking", Theme.GradientDownSprite, new Color(0.04f, 0.06f, 0.16f, 0.50f));   // a fade, not a band
            bar.preserveAspect = false;
            bar.rectTransform.anchorMin = new Vector2(0f, 1f);
            bar.rectTransform.anchorMax = new Vector2(1f, 1f);
            bar.rectTransform.pivot = new Vector2(0.5f, 1f);
            bar.rectTransform.offsetMin = new Vector2(0f, -MapTopBar);
            bar.rectTransform.offsetMax = Vector2.zero;
            AddPillRow();
            AddBack("Menu", ShowMenu);
            AddTabBar(-1, false);
            AddRewardsButton();   // above Heroes, same gap as home; the map viewport leaves a strip for it (no pin or tag can sit under it)
            BundleTimerButton.Attach(screen, progress, art, BundleHost(), RefreshWallet);   // timer bundle offer, bottom left (docs/26)

            CenterMap(currentRise);
            StartCoroutine(CenterMapAgain(currentRise));
        }

        System.Collections.IEnumerator CenterMapAgain(float rise)
        {
            yield return null;     // again after the first real layout passes (the canvas size and safe area settle then)
            CenterMap(rise);
            if (mapContent != null) mapContent.GetComponent<CanvasGroup>().alpha = 1f;
            yield return null;
            CenterMap(rise);
        }

        /// <summary>Content height = viewport width / art aspect. Done by hand: it is exact on the frame it runs and the ScrollRect sees it at once.</summary>
        void SizeMap()
        {
            float w = mapViewport.rect.width;
            mapWidthSeen = w;
            mapHeightSeen = mapViewport.rect.height;
            mapContent.sizeDelta = new Vector2(0f, w / mapAspect);
        }

        void CenterMap(float rise)
        {
            if (mapContent == null || mapViewport == null || mapScroll == null) return;
            Canvas.ForceUpdateCanvases();
            if (mapViewport.rect.height < 2f) return;   // not laid out yet; the next pass does it
            SizeMap();
            float contentH = mapContent.rect.height, viewH = mapViewport.rect.height;
            float scrollable = Mathf.Max(1f, contentH - viewH);
            float n = lastMapOffset >= 0f && lastMapCurrent == mapCurrent
                ? lastMapOffset
                : 1f - Mathf.Clamp01(((1f - rise) * contentH - viewH * 0.5f) / scrollable);
            mapScroll.verticalNormalizedPosition = n;     // 0 = bottom of the map, 1 = top (the ScrollRect keeps its own bounds right)
        }

        /// <summary>Where one region's name tag sits: Mode 1 below, 2 above, 4 right, 3 left of the pin, shifted by Dx/Dy map units, text wrapped at MaxW.</summary>
        struct TagSpot { public int Mode; public float Dx, Dy, MaxW, Score; }

        static float TagOverlap(Rect a, Rect b, bool hard)
        {
            float ox = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            float oy = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            if (ox <= 0f || oy <= 0f) return 0f;
            return hard ? ox * oy + 1000f : ox * oy * 0.01f;
        }

        /// <summary>
        /// Candidate search for every name tag (owner 2026-10-25: no tag covers a pin or another tag). For each pin, in order, candidates are tried
        /// from the most natural to the least: text width (single line, then wrapped at 340 and 240 units), then the shift along the pin's side
        /// (0, +-50, +-100 ... units), then below, above, right, left. A candidate is free when, on the REAL map content size (width and height read from
        /// mapContent once it is laid out, re-planned when that size changes), it stays 44 units inside the edges, keeps 8 units from every pin (160 wide, plus the padlock) and from
        /// every tag placed before it. The first free candidate wins; if none is free the one with the least overlap is used (seam clouds only soften the
        /// choice). Text never shrinks.
        /// </summary>
        static Dictionary<int, TagSpot> PlanTags(List<PinTag> pinList, PaintedPages painted, Vector2 mapSize)
        {
            const float gap = 8f, edge = TagKit.EdgeMargin, cloudHalf = 110f;
            var ownPin = new Rect(-80f, -84f, 168f, 164f);                                   // the pin button plus its padlock (8 right, 4 below)
            var otherPin = new Rect(-80f - gap, -84f - gap, 168f + 2f * gap, 164f + 2f * gap);
            float[] scales = { 1f };   // the REAL map size is planned (mapSize), no guessed scales
            float[] maxWs = { TagKit.MapTextMaxWidth, 340f, 240f, 190f };
            float[] offs = { 0f, 50f, -50f, 100f, -100f, 150f, -150f, 220f, -220f, 300f, -300f, 400f, -400f };
            float[] exts = { 0f, 40f, 90f };   // pushes the tag further from its pin when the pin's own side is crowded
            int[] order = { 1, 2, 4, 3 };   // below, above, right, left
            var plan = new Dictionary<int, TagSpot>();
            var keys = new List<int>();
            var centres = new List<Vector2>();   // map pixels of the painted art (y grows upward)
            var labels = new List<string>();
            var prefers = new List<bool>();      // the map data asks for the tag below the icon
            float pw = mapSize.x, ph = mapSize.y / Mathf.Max(1, painted.pages.Length);   // one painted page is ph high; seams sit at multiples of it
            foreach (PinTag pt in pinList)
            {
                keys.Add(pt.Region.Index);
                centres.Add(new Vector2(pt.Anchor.x * mapSize.x, pt.Anchor.y * mapSize.y));   // real pin centres (y grows upward)
                labels.Add(pt.Label);
                prefers.Add(pt.Prefer == 1);
            }
            int n = keys.Count;
            var placedRel = new Rect[n];   // each placed tag, relative to its own pin centre
            for (int k = 0; k < n; k++)
            {
                float bestScore = float.MaxValue;
                var best = new TagSpot { Mode = 1, MaxW = maxWs[0] };
                Rect bestRel = new Rect(0f, 0f, 0f, 0f);
                float widestWord = TagKit.WidestWord(labels[k], 36, false, true);   // a wrap width below this would split a word
                for (int pass = prefers[k] ? 0 : 1; pass < 2; pass++)
                {
                if (pass == 1 && prefers[k] && bestScore < 1000f) break;   // the preferred side was free (clouds only soften): keep it, else fall back to every side
                float prevW = -1f;
                for (int wi = 0; wi < maxWs.Length && bestScore > 0f; wi++)
                {
                    float effW = Mathf.Max(maxWs[wi], widestWord);   // widen instead of splitting a word
                    if (Mathf.Abs(effW - prevW) < 0.5f) continue;   // same effective width as the previous candidate (widths run from wide to narrow)
                    prevW = effW;
                    Vector2 sz = TagKit.BackingSize(labels[k], 36, false, true, effW);
                    for (int ei = 0; ei < exts.Length && bestScore > 0f; ei++)
                    for (int oi = 0; oi < offs.Length && bestScore > 0f; oi++)
                    {
                        for (int oo = 0; oo < order.Length && bestScore > 0f; oo++)
                        {
                            int mode = order[oo];
                            if (pass == 0 && mode != 1) continue;   // the map data asked for "below": only that side in the first pass
                            bool vertical = mode == 1 || mode == 2;   // below / above slide sideways, right / left slide up and down
                            float off = offs[oi], ext = exts[ei];
                            if (!vertical && Mathf.Abs(off) > 220f) continue;
                            float dx = vertical ? off : 0f, dy = vertical ? 0f : off;
                            Rect rel;
                            switch (mode)
                            {
                                case 1: rel = new Rect(-sz.x / 2f + dx, -86f - ext - sz.y, sz.x, sz.y); break;     // the tag's top is 6 below the pin's bottom
                                case 2: rel = new Rect(-sz.x / 2f + dx, 86f + ext, sz.x, sz.y); break;
                                case 4: rel = new Rect(90f + ext, -sz.y / 2f + dy, sz.x, sz.y); break;
                                default: rel = new Rect(-90f - ext - sz.x, -sz.y / 2f + dy, sz.x, sz.y); break;
                            }
                            float score = 0f;
                            for (int si = 0; si < scales.Length; si++)
                            {
                                float s = scales[si];
                                Vector2 c = centres[k] * s;
                                var tag = new Rect(c.x + rel.x, c.y + rel.y, rel.width, rel.height);
                                if (tag.xMin < edge) score += (edge - tag.xMin) * 10f + 1000f;
                                if (tag.xMax > pw * s - edge) score += (tag.xMax - (pw * s - edge)) * 10f + 1000f;
                                for (int j = 0; j < n; j++)
                                {
                                    Vector2 cj = centres[j] * s;
                                    Rect pr = j == k ? ownPin : otherPin;
                                    score += TagOverlap(tag, new Rect(cj.x + pr.x, cj.y + pr.y, pr.width, pr.height), true);
                                    if (j < k)
                                    {
                                        Rect t2 = placedRel[j];
                                        score += TagOverlap(tag, new Rect(cj.x + t2.x - gap, cj.y + t2.y - gap, t2.width + 2f * gap, t2.height + 2f * gap), true);
                                    }
                                }
                                for (int pj = 1; pj < painted.pages.Length; pj++)
                                    score += TagOverlap(tag, new Rect(0f, pj * ph * s - cloudHalf, pw * s, cloudHalf * 2f), false);
                            }
                            if (score < bestScore - 0.001f)
                            {
                                bestScore = score;
                                best = new TagSpot { Mode = mode, Dx = dx + (mode == 4 ? ext : (mode == 3 ? -ext : 0f)), Dy = dy + (mode == 2 ? ext : (mode == 1 ? -ext : 0f)), MaxW = effW, Score = score };
                                bestRel = rel;
                            }
                        }
                    }
                }
                }   // pass
                plan[keys[k]] = best;
                placedRel[k] = bestRel;
            }
            return plan;
        }

        /// <summary>A round pin on the map with a slim dark name tag beside it. Locked and not-yet-built regions show a padlock.</summary>
        void AddRegionPin(RegionDef region, Vector2 anchor, int tagMode = 0, float tagDx = 0f, float tagDy = 0f, float tagMaxW = TagKit.MapTextMaxWidth, int preferMode = 0)   // preferMode 1 = the map data asks for the tag below the icon (tried first by PlanTags). mode: 0 beside the pin, 1 below, 2 above, 3 left, 4 right
        {
            bool playable = progress.IsRegionPlayable(region);
            bool unlocked = progress.IsRegionUnlocked(region);
            int cleared = 0;
            if (playable) foreach (string id in region.Levels) if (progress.IsLevelCleared(id)) cleared++;

            // MapNodes order from the art sheet: unlocked, current, completed, locked, teaser, ring.
            int sprite = !playable ? 4 : (!unlocked ? 3 : (cleared >= region.Levels.Count ? 2 : (cleared > 0 ? 1 : 0)));
            Sprite nodeSprite = art != null ? ArtCatalog.Pick(art.MapNodes, sprite) : null;

            // The painted medallion of this region (docs/16 section D, owner 2026-10-24); the old node art is the fallback.
            string slug = region.Id.Length > 11 ? region.Id.Substring(11) : region.Id;
            Sprite painted = Resources.Load<Sprite>("Art/Pins/pin_" + slug);
            bool current = region.Index == mapCurrent && unlocked && playable;
            float pin = painted != null ? 160f : 110f;
            Button b = UiKit.AddButton(mapContent, "Region" + region.Index, "", painted != null ? painted : nodeSprite, Color.white, new Vector2(pin, pin), () => OnRegionTapped(region), 40);
            b.GetComponent<A11yLabel>().Text = region.Name + (unlocked ? "" : (playable ? ", locked" : ", coming soon"));
            RectTransform rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            b.GetComponent<Image>().preserveAspect = true;
            if (painted != null)
            {
                if (!(playable && unlocked)) b.GetComponent<Image>().color = new Color(0.55f, 0.58f, 0.68f, 1f);   // locked: grey, with a padlock on top
                if (!(playable && unlocked))
                {
                    Image lk = UiKit.AddImage(rt, "Lock", ToyKit.Get("icon_lock"), Color.white);
                    lk.rectTransform.anchorMin = lk.rectTransform.anchorMax = new Vector2(1f, 0f);
                    lk.rectTransform.pivot = new Vector2(1f, 0f);
                    lk.rectTransform.sizeDelta = new Vector2(72f, 72f);
                    lk.rectTransform.anchoredPosition = new Vector2(8f, -4f);
                    lk.raycastTarget = false;
                }
                else if (current)
                {
                    Image ring = UiKit.AddImage(rt, "CurrentRing", Glyphs.Ring, new Color(1f, 0.82f, 0.2f, 1f));
                    ring.rectTransform.anchorMin = Vector2.zero;
                    ring.rectTransform.anchorMax = Vector2.one;
                    ring.rectTransform.offsetMin = new Vector2(-14f, -14f);
                    ring.rectTransform.offsetMax = new Vector2(14f, 14f);
                    ring.raycastTarget = false;
                }
            }

            // The name tag is built from the real map size (see ReplanTags): first a default one below the pin, replaced once the map has its size.
            var pt = new PinTag { Region = region, Pin = rt, Anchor = anchor, Label = region.Name, Open = playable && unlocked, Prefer = preferMode };   // only the real name; locked regions keep the padlock on the pin
            pinTags.Add(pt);
            BuildPinTag(pt, tagMode, tagDx, tagDy, tagMaxW);
        }

        /// <summary>One pin and its name tag (rebuilt when the planner moves it).</summary>
        sealed class PinTag { public RegionDef Region; public RectTransform Pin; public Vector2 Anchor; public string Label; public bool Open; public int Prefer; public GameObject Tag; }

        readonly List<PinTag> pinTags = new List<PinTag>();
        PaintedPages tagLayout;

        /// <summary>Runs the tag planner once the map content has its real size, and again whenever that size changes (aspect, rotation).</summary>
        sealed class MapTagReplanner : MonoBehaviour
        {
            public RectTransform Content;
            public System.Action<Vector2> OnSize;
            Vector2 last = new Vector2(-1f, -1f);

            void LateUpdate()
            {
                if (Content == null || OnSize == null) return;
                Vector2 s = Content.rect.size;
                if (s.x < 2f || s.y < 2f) return;
                if ((s - last).sqrMagnitude < 0.25f) return;
                last = s;
                OnSize(s);
            }
        }

        void ReplanTags(Vector2 size)
        {
            if (tagLayout == null || pinTags.Count == 0) return;
            Dictionary<int, TagSpot> plan = PlanTags(pinTags, tagLayout, size);
            foreach (PinTag pt in pinTags)
            {
                TagSpot sp;
                if (pt.Pin != null && plan.TryGetValue(pt.Region.Index, out sp)) BuildPinTag(pt, sp.Mode, sp.Dx, sp.Dy, sp.MaxW);
            }
        }

        void BuildPinTag(PinTag pt, int tagMode, float tagDx, float tagDy, float tagMaxW)
        {
            if (pt.Tag != null) { pt.Tag.SetActive(false); Destroy(pt.Tag); pt.Tag = null; }
            RectTransform rt = pt.Pin;
            bool open = pt.Open;
            // The backing is measured from the text (TagKit): padding >= 12 dp sides / 6 dp top and bottom, solid, long names wrap between words.
            Text name;
            Image tag = TagKit.Build(rt, "NameBacking", pt.Label, 36, open ? Color.white : new Color(0.88f, 0.88f, 0.95f, 1f),
                open ? new Color(0.05f, 0.08f, 0.22f, 0.95f) : new Color(0.16f, 0.14f, 0.28f, 0.95f), tagMaxW, out name);
            pt.Tag = tag.gameObject;
            Vector2 tagSize = tag.rectTransform.sizeDelta;
            bool tagRight = pt.Anchor.x < 0.5f;
            if (tagMode == 2)
            {
                UiKit.Place(tag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 6f), tagSize);
                tag.rectTransform.pivot = new Vector2(0.5f, 0f);
            }
            else if (tagMode == 3 || tagMode == 4)
            {
                bool right = tagMode == 4;
                UiKit.Place(tag.rectTransform, new Vector2(right ? 1f : 0f, 0.5f), new Vector2(right ? 10f : -10f, 0f), tagSize);
                tag.rectTransform.pivot = new Vector2(right ? 0f : 1f, 0.5f);
            }
            else if (tagMode == 1)
            {
                // painted pages: the name sits under the marker, centred
                UiKit.Place(tag.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -6f), tagSize);
                tag.rectTransform.pivot = new Vector2(0.5f, 1f);
            }
            else
            {
                UiKit.Place(tag.rectTransform, new Vector2(tagRight ? 1f : 0f, 0.5f), new Vector2(tagRight ? 10f : -10f, 0f), tagSize);
                tag.rectTransform.pivot = new Vector2(tagRight ? 0f : 1f, 0.5f);
            }
            tag.rectTransform.anchoredPosition += new Vector2(tagDx, tagDy);   // the planner's free spot (PlanTags)
            TagKit.KeepInside(tag.rectTransform, mapContent);   // safety net: near the left or right edge the tag shifts inward instead of being cut
        }

        public void OnRegionTapped(RegionDef region)
        {
            if (mapScroll != null) { lastMapOffset = mapScroll.verticalNormalizedPosition; lastMapCurrent = mapCurrent; }
            if (!progress.IsRegionPlayable(region)) { ShowMessage("Coming soon", region.Name + " is not open yet."); return; }
            if (progress.IsRegionUnlocked(region)) { ShowLevels(region); return; }
            ShowUnlockPopup(region);
        }

        void ShowUnlockPopup(RegionDef region)
        {
            int price = progress.GemSkipPrice(region);
            RegionDef prev = repo.GetRegionByIndex(region.Index - 1);
            bool isNext = region.Index == progress.HighestUnlockedRegionIndex() + 1;
            string body = "Clear level 15 of " + prev.Name + " to open " + region.Name + ".";
            if (isNext) body += "\nOr skip ahead for " + price + " gems (you have " + progress.Gems + ").";
            OpenPopup(region.Name + " is locked", body);
            if (isNext)
            {
                bool can = progress.Gems >= price;
                Button b = PopupButton("Unlock for " + price + " gems", Btn(1), can ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.8f), 230f, () =>
                {
                    if (progress.TrySkipToRegion(region) == SkipResult.Ok) ShowMap();
                });
                b.interactable = can;
            }
            PopupButton("Close", Btn(0), Color.white, 60f, ClosePopup);
        }

        // ---------------- level select ----------------

        public void ShowLevels(RegionDef region)
        {
            NewScreen("Levels");
            currentRegionId = region.Id;
            RegionArt ra = art != null ? art.ForRegion(region.Index) : null;
            LevelBackground(ra);
            AddLevelsWallet(region);
            AddBack("Map", ShowMap);
            List<LevelDef> levels = repo.LevelsOf(region.Id);
            string currentId = null;
            foreach (LevelDef l in levels) if (progress.IsLevelUnlocked(l) && !progress.IsLevelCleared(l.Id)) { currentId = l.Id; break; }

            // A winding path of 15 round nodes, level 1 at the bottom (docs/16 section C): the nodes follow four lanes (centre, left, centre, right),
            // dotted lines join them, and the path scrolls; it opens on the current level. Title row and the Endless icon stay fixed above it.
            RectTransform titleRow = UiKit.Rect(screen, "TitleRow");
            titleRow.anchorMin = new Vector2(0f, 1f);
            titleRow.anchorMax = new Vector2(1f, 1f);
            titleRow.pivot = new Vector2(0.5f, 1f);
            titleRow.anchoredPosition = new Vector2(0f, -244f);
            titleRow.sizeDelta = new Vector2(-40f, 176f);
            Text hdr = UiKit.AddText(titleRow, "Header", region.Name, 64, Color.white, TextAnchor.MiddleLeft, true);
            var hrt = (RectTransform)hdr.transform;
            hrt.anchorMin = Vector2.zero;
            hrt.anchorMax = Vector2.one;
            hrt.offsetMin = new Vector2(24f, 0f);
            hrt.offsetMax = new Vector2(-170f, 0f);

            Button endlessIcon = ToyKit.IconButton(titleRow, "Endless", "Endless mode", 1, Glyphs.Infinity, new Vector2(136f, 136f), () => OnEndlessTapped(region), 0.72f);
            UiKit.Place((RectTransform)endlessIcon.transform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(136f, 136f));
            endlessIcon.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);

            RectTransform vp = UiKit.Rect(screen, "PathViewport");
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(0f, ToyKit.TabBarReserve + 8f);
            vp.offsetMax = new Vector2(0f, -430f);
            vp.gameObject.AddComponent<RectMask2D>();
            var vhit = vp.gameObject.AddComponent<Image>();
            vhit.color = new Color(0f, 0f, 0f, 0f);
            const float nodeSize = 150f;
            RectTransform content = UiKit.Rect(vp, "PathContent");
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(1f, 0f);
            content.pivot = new Vector2(0.5f, 0f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 1200f);
            var cg = content.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;   // shown once the first layout pass has placed the nodes
            var scroll = vp.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = vp;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 60f;

            var nodes = new RectTransform[levels.Count];
            int lastUnlocked = 0, currentIdx = 0;
            for (int i = 0; i < levels.Count; i++)
            {
                LevelDef l = levels[i];
                bool isCur = l.Id == currentId;
                nodes[i] = AddLevelNode(l, content, isCur);
                nodes[i].anchorMin = nodes[i].anchorMax = new Vector2(0.5f, 0f);
                nodes[i].pivot = new Vector2(0.5f, 0.5f);
                nodes[i].sizeDelta = new Vector2(nodeSize, nodeSize);
                nodes[i].localScale = Vector3.one * (isCur ? 1.15f : 1f);
                if (progress.IsLevelUnlocked(levels[i])) lastUnlocked = i;
                if (isCur) currentIdx = i;
            }
            StartCoroutine(LayoutPath(scroll, content, nodes, nodeSize, lastUnlocked, currentIdx));
            AddTabBar(-1, false);
        }

        /// <summary>
        /// Horizontal position (0..1) of level node <paramref name="i"/> and of the flowing line: one smooth wave, level 1 at the far left, then the
        /// middle, the far right, the middle, the far left again. Neighbours are always at least a node apart sideways, so the path can be tight.
        /// </summary>
        // The level path is a snake (owner's sketch, 2026-10-24): three levels run left to right along a row, the road makes a hairpin turn at the
        // right edge, three levels run right to left, a hairpin at the left edge, and so on up the screen. Level 1 is at the far left of the bottom row.
        const int PathPerRow = 3;
        static readonly float[] PathLanesLtr = { 0.16f, 0.50f, 0.84f };

        /// <summary>Horizontal position (0..1) of level node <paramref name="i"/> on the snake.</summary>
        static float PathX(int i)
        {
            int row = i / PathPerRow, k = i % PathPerRow;
            return (row % 2 == 0) ? PathLanesLtr[k] : PathLanesLtr[PathPerRow - 1 - k];
        }

        /// <summary>Row of node <paramref name="i"/>, counted from the bottom.</summary>
        static int PathRow(int i) { return i / PathPerRow; }

        /// <summary>
        /// The road as a list of smooth points (canvas units) through every node, with a rounded hairpin turn between rows. A uniform Catmull-Rom spline
        /// through the node centres and one apex point per turn: no straight stretches between rows, no dots. Each point carries the index of the
        /// node it leads to (used to fade the part of the road that is still locked).
        /// </summary>
        static void BuildRoad(int count, float w, float yBase, float rowPitch, System.Collections.Generic.List<Vector2> pts, System.Collections.Generic.List<int> toNode)
        {
            var key = new System.Collections.Generic.List<Vector2>();
            var keyNode = new System.Collections.Generic.List<int>();
            // lead-in: the road starts a little before level 1
            key.Add(new Vector2(PathX(0) * w - 70f, yBase)); keyNode.Add(0);
            for (int i = 0; i < count; i++)
            {
                float y = yBase + PathRow(i) * rowPitch;
                key.Add(new Vector2(PathX(i) * w, y)); keyNode.Add(i);
                bool lastOfRow = (i % PathPerRow) == PathPerRow - 1;
                if (lastOfRow && i + 1 < count)
                {
                    bool toRight = PathRow(i) % 2 == 0;
                    float apexX = (toRight ? 0.975f : 0.025f) * w;
                    key.Add(new Vector2(apexX, y + rowPitch * 0.5f)); keyNode.Add(i + 1);
                }
            }
            key.Insert(0, key[0] + (key[0] - key[1]));     // phantom end points for the spline
            keyNode.Insert(0, 0);
            key.Add(key[key.Count - 1] + (key[key.Count - 1] - key[key.Count - 2]));
            keyNode.Add(keyNode[keyNode.Count - 1]);
            for (int k = 1; k + 2 < key.Count; k++)
            {
                Vector2 p0 = key[k - 1], p1 = key[k], p2 = key[k + 1], p3 = key[k + 2];
                int steps = Mathf.Max(28, Mathf.CeilToInt((p2 - p1).magnitude * 1.5f / 1.2f));   // a stamp at least every 1.2 px: one unbroken line
                for (int st = 0; st < steps; st++)
                {
                    float t = st / (float)steps, t2 = t * t, t3 = t2 * t;
                    Vector2 q = 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
                    pts.Add(q);
                    toNode.Add(keyNode[k + 1]);
                }
            }
        }

        /// <summary>Draws the road as ONE thin translucent white line (a hair of navy around it) into a single texture, under the nodes.</summary>
        void BuildPathLine(RectTransform content, float w, float h, System.Collections.Generic.List<Vector2> pts, System.Collections.Generic.List<int> toNode, int lastUnlocked)
        {
            const float sc = 1f;
            int tw = Mathf.Max(8, Mathf.RoundToInt(w * sc)), th = Mathf.Max(8, Mathf.RoundToInt(h * sc));
            var oa = new float[tw * th];   // navy hairline alpha
            var fa = new float[tw * th];   // white fill alpha
            for (int pass = 0; pass < 2; pass++)
            {
                float radius = (pass == 0 ? 8.5f : 5f) * sc;
                float[] dst = pass == 0 ? oa : fa;
                float baseAlpha = pass == 0 ? 0.22f : 0.6f;
                for (int n = 0; n < pts.Count; n++)
                {
                    float sa = baseAlpha * (toNode[n] <= lastUnlocked ? 1f : 0.45f);
                    int cx = Mathf.RoundToInt(pts[n].x * sc), cy = Mathf.RoundToInt(pts[n].y * sc), r = Mathf.CeilToInt(radius) + 1;
                    for (int dy = -r; dy <= r; dy++)
                    {
                        int py = cy + dy;
                        if (py < 0 || py >= th) continue;
                        for (int dx = -r; dx <= r; dx++)
                        {
                            int pxx = cx + dx;
                            if (pxx < 0 || pxx >= tw) continue;
                            float cov = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                            float v = sa * cov;
                            if (v > dst[py * tw + pxx]) dst[py * tw + pxx] = v;   // the strongest stamp wins: overlapping stamps never add up
                        }
                    }
                }
            }
            var px = new Color[tw * th];
            var navy = new Color(0.06f, 0.14f, 0.34f);
            for (int i = 0; i < px.Length; i++)
            {
                float a = Mathf.Max(oa[i], fa[i]);
                if (a <= 0f) { px[i] = Color.clear; continue; }
                Color c = Color.Lerp(navy, Color.white, fa[i] / a);
                c.a = a;
                px[i] = c;
            }
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(px);
            tex.Apply();
            var raw = UiKit.Rect(content, "PathLine").gameObject.AddComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
            raw.rectTransform.anchorMin = new Vector2(0f, 0f);
            raw.rectTransform.anchorMax = new Vector2(1f, 0f);
            raw.rectTransform.pivot = new Vector2(0.5f, 0f);
            raw.rectTransform.anchoredPosition = Vector2.zero;
            raw.rectTransform.sizeDelta = new Vector2(0f, h);
            raw.transform.SetAsFirstSibling();   // under the nodes
        }

        /// <summary>
        /// After the first layout pass: places the 15 nodes on the snake so that TEN levels fit the real height of the view (owner, 2026-10-24),
        /// draws the road, and scrolls so the current level sits in the middle.
        /// </summary>
        System.Collections.IEnumerator LayoutPath(ScrollRect scroll, RectTransform content, RectTransform[] nodes, float nodeSize, int lastUnlocked, int currentIdx)
        {
            yield return null;
            if (scroll == null) yield break;
            RectTransform vpr = (RectTransform)scroll.viewport;
            float vpH = vpr.rect.height, w = vpr.rect.width;
            const float starRoom = 56f, edge = 24f;
            // level 10 is the first node of row 3: rows 0..3 span three row gaps; they plus one node and its stars fill the view
            float rowPitch = Mathf.Clamp((vpH - nodeSize - starRoom - 2f * edge) / 3f, 190f, 600f);
            float yBase = edge + starRoom + nodeSize * 0.5f;
            int rows = (nodes.Length + PathPerRow - 1) / PathPerRow;
            float contentH = yBase * 2f + (rows - 1) * rowPitch;
            content.sizeDelta = new Vector2(0f, contentH);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] == null) continue;
                nodes[i].anchorMin = nodes[i].anchorMax = new Vector2(PathX(i), 0f);
                nodes[i].anchoredPosition = new Vector2(0f, yBase + PathRow(i) * rowPitch);
            }
            var pts = new System.Collections.Generic.List<Vector2>();
            var toNode = new System.Collections.Generic.List<int>();
            BuildRoad(nodes.Length, w, yBase, rowPitch, pts, toNode);
            BuildPathLine(content, w, contentH, pts, toNode, lastUnlocked);
            float y = yBase + PathRow(currentIdx) * rowPitch;
            float span = Mathf.Max(1f, contentH - vpH);
            scroll.verticalNormalizedPosition = Mathf.Clamp01((y - vpH * 0.5f) / span);
            var cg = content.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
        }

        /// <summary>Endless start popup: best floor and START. There is no loadout any more: every owned power-up is available in the run.</summary>
        public void OnEndlessTapped(RegionDef region)
        {
            int best = progress.EndlessBestFloor(region.Id);
            OpenPopup("Endless", "", 900f);
            Transform panel = popup.Find("Panel");
            Destroy(panel.Find("Body").gameObject);
            Transform endTitle = panel.Find("Title");
            if (endTitle != null) { var etr = (RectTransform)endTitle; etr.anchoredPosition = new Vector2(0f, etr.anchoredPosition.y); etr.sizeDelta = new Vector2(540f, etr.sizeDelta.y); }   // centred on the panel (the frame leaves room for the X by shifting it 30 left)
            Image inf = UiKit.AddImage(panel, "EndlessIcon", ToyKit.Button(1), Color.white);
            inf.type = Image.Type.Sliced;
            inf.preserveAspect = false;
            inf.pixelsPerUnitMultiplier = 2.6f;
            UiKit.Place(inf.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(150f, 150f));
            inf.rectTransform.pivot = new Vector2(0.5f, 1f);
            ToyKit.AddGlyph(inf.transform, Glyphs.Infinity, 0.72f);
            Text rname = UiKit.AddText(panel, "Region", region.Name, 52, Theme.Ink, TextAnchor.MiddleCenter, true);
            DropOutline(rname);
            UiKit.Place((RectTransform)rname.transform, new Vector2(0.5f, 1f), new Vector2(0f, -352f), new Vector2(760f, 80f));
            RectTransform bestRow = ToyKit.PillRow(panel, "BestFloor", 425f, 96f);
            ToyKit.InfoPill(bestRow, ToyKit.Get("icon_crown"), best > 0 ? best.ToString() : "-", new Color(1f, 0.9f, 0.4f), 300f, 96f);
            ToyKit.PopupAction(popup.Find("Panel"), "START", Btn(1), Color.white, 60f, () => StartEndless(region), -90f);
            ToyKit.PopupAction(popup.Find("Panel"), "Close", Btn(0), Color.white, 70f, ClosePopup, 100f);
        }

        /// <summary>Height reserved at the bottom of the level screen for the Endless button (button 130 + 24 margin + 16 gap).</summary>
        const float EndlessBarHeight = 170f;

        /// <summary>Opens the endless tower of a region in the Run scene.</summary>
        public void StartEndless(RegionDef region)
        {
            GameSession.SelectEndless(region.Id);
            SceneManager.LoadScene("Run");
        }

        // Sprite order of ui_level_nodes after slicing: 0 plain bubble, 1 gem bubble (garbled text), 2 crown, 3 padlock,
        // 4 = three small pieces (NOT a node), 5 large bubble.
        RectTransform AddLevelNode(LevelDef level, Transform parent, bool isCurrent)
        {
            bool unlocked = progress.IsLevelUnlocked(level);
            bool cleared = progress.IsLevelCleared(level.Id);
            bool boss = level.LevelClass == LevelDef.ClassHighlyComplex;
            bool complex = level.LevelClass == LevelDef.ClassComplex;
            int sprite = !unlocked ? 3 : (boss ? 2 : (cleared ? 5 : 0));
            Color tint = Color.white;
            if (unlocked && !boss && cleared) tint = new Color(1f, 0.93f, 0.55f, 1f);   // completed: warm gold bubble
            else if (isCurrent) tint = new Color(1f, 1f, 0.7f, 1f);                     // current: bright
            Sprite nodeSprite = art != null ? ArtCatalog.Pick(art.LevelNodes, sprite) : null;
            Button b = UiKit.AddButton(parent, "Level" + level.Index, "", nodeSprite, tint, new Vector2(190f, 190f), () => OnLevelTapped(level), 40);
            RectTransform rt = (RectTransform)b.transform;
            b.GetComponent<Image>().preserveAspect = true;
            if (unlocked && !boss)
            {
                Text num = UiKit.AddText(rt, "Num", level.Index.ToString(), 72, UiKit.ButtonInk);
                DropOutline(num);
                UiKit.Stretch((RectTransform)num.transform);
            }
            // Doc 25: the gem icon marks the levels that hold gems to collect on the way (8 and 15); the combo levels (5 and 10) have none.
            bool gemLevel = level.Index == 8 || level.Index == 15;
            if (unlocked && gemLevel && art != null && art.GemIcon != null)
            {
                Image gem = UiKit.AddImage(rt, "GemBadge", art.GemIcon, Color.white);
                gem.rectTransform.anchorMin = gem.rectTransform.anchorMax = new Vector2(1f, 1f);
                gem.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                gem.rectTransform.anchoredPosition = new Vector2(-14f, -14f);
                gem.rectTransform.sizeDelta = new Vector2(64f, 64f);
            }
            LevelProgress p = progress.GetProgress(level.Id);
            int stars = p != null ? p.Stars : 0;
            for (int s = 0; s < 3 && unlocked; s++)
            {
                Sprite star = art != null ? (s < stars ? art.StarFilled : art.StarEmpty) : null;
                Image si = UiKit.AddImage(rt, "S" + s, star, s < stars ? Color.white : new Color(1f, 1f, 1f, 0.45f));
                RectTransform srt = si.rectTransform;
                srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0f);
                srt.pivot = new Vector2(0.5f, 1f);
                srt.anchoredPosition = new Vector2((s - 1) * 49f, -4f);
                srt.sizeDelta = new Vector2(46f, 46f);
            }
            return rt;
        }

        public void OnLevelTapped(LevelDef level)
        {
            if (!progress.IsLevelUnlocked(level)) { ShowMessage("Locked", "Clear level " + (level.Index - 1) + " first."); return; }
            LevelSpec spec = LevelSpecFactory.Build(level, repo.GetRegion(level.Region));
            LevelProgress p = progress.GetProgress(level.Id);
            int starsHave = p != null ? p.Stars : 0;
            OpenPopup("Level " + level.Index, "", 1020f);
            Transform panel = popup.Find("Panel");
            Destroy(panel.Find("Body").gameObject);

            // Everything is centred on the panel: the goal, the three stars, then what a clear pays as icon pills (no sentences).
            Text goal = UiKit.AddText(panel, "Goal", RunSession.DescribeGoal(spec), 46, Theme.Ink, TextAnchor.MiddleCenter, true);
            DropOutline(goal);
            UiKit.Place((RectTransform)goal.transform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(760f, 110f));
            string rule = RunSession.GoalRule(spec);
            if (rule != null)
            {
                // Level rule before PLAY: small italic line under the goal (e.g. "Don't touch the walls.").
                goal.rectTransform.anchoredPosition = new Vector2(0f, -140f);
                goal.rectTransform.sizeDelta = new Vector2(760f, 80f);
                Text ruleText = UiKit.AddText(panel, "Rule", rule, 38, Theme.Ink, TextAnchor.MiddleCenter, true);
                ruleText.fontStyle = FontStyle.Italic;
                DropOutline(ruleText);
                UiKit.Place((RectTransform)ruleText.transform, new Vector2(0.5f, 1f), new Vector2(0f, -215f), new Vector2(760f, 65f));
            }
            for (int i = 0; i < 3; i++)
            {
                bool got = i < starsHave;
                Image star = UiKit.AddImage(panel, "Star" + i, art != null ? (got ? art.StarFilled : art.StarEmpty) : null, got ? Color.white : new Color(1f, 1f, 1f, 0.55f));
                UiKit.Place(star.rectTransform, new Vector2(0.5f, 1f), new Vector2((i - 1) * 170f, -290f), new Vector2(130f, 130f));
                star.rectTransform.pivot = new Vector2(0.5f, 1f);
            }
            RectTransform pays = ToyKit.PillRow(panel, "Pays", 440f, 96f);
            if (starsHave >= 3)
            {
                ToyKit.InfoPill(pays, art != null ? art.CoinIcon : null, "50%", new Color(1f, 0.92f, 0.5f), 250f, 96f);   // 3-star levels: replays pay 50 percent of the coins and no gems
            }
            else
            {
                ToyKit.InfoPill(pays, art != null ? art.CoinIcon : null, "70", new Color(1f, 0.92f, 0.5f), 220f, 96f);
                ToyKit.InfoPill(pays, art != null ? art.StarFilled : null, "+60", Color.white, 220f, 96f);
                ToyKit.InfoPill(pays, ToyKit.Get("icon_flame"), "+5", new Color(1f, 0.78f, 0.35f), 220f, 96f);
            }
            if (p != null && p.BestScore > 0)
            {
                RectTransform best = ToyKit.PillRow(panel, "Best", 550f, 96f);
                ToyKit.InfoPill(best, ToyKit.Get("icon_crown"), p.BestScore.ToString(), new Color(1f, 0.9f, 0.4f), 300f, 96f);
            }
            ToyKit.PopupAction(popup.Find("Panel"), "PLAY", Btn(1), Color.white, 60f, () =>
            {
                GameSession.CurrentLevelId = level.Id;
                GameSession.EndlessMode = false;
                SceneManager.LoadScene("Run");
            }, -90f);
            ToyKit.PopupAction(popup.Find("Panel"), "Close", Btn(0), Color.white, 70f, ClosePopup, 100f);
        }

        // ---------------- settings ----------------

        public void ShowSettings()
        {
            NewScreen("Settings");
            SkyBackground();
            AddBack("Back", ShowMenu);
            ToyKit.Ribbon(screen, "Settings", new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(580f, 206f));

            bool dev = GameSettings.DevBuild;
            const float rowH = 148f;
            int rows = 7 + (dev ? 2 : 0);
            // One cream card holds the icon rows (icon, short label, switch or value button).
            RectTransform card = UiKit.Rect(screen, "Card");
            card.anchorMin = new Vector2(0f, 1f);
            card.anchorMax = new Vector2(1f, 1f);
            card.pivot = new Vector2(0.5f, 1f);
            card.anchoredPosition = new Vector2(0f, -250f);
            card.sizeDelta = new Vector2(-40f, rows * rowH + 40f);
            Theme.CardBacking(card);
            int r = 0;
            AddSwitchRow(card, r++ * rowH, "icon_sound", "Sound", () => GameSettings.Sound, v => GameSettings.Sound = v);
            AddSwitchRow(card, r++ * rowH, "icon_music", "Music", () => GameSettings.Music, v => GameSettings.Music = v);
            AddSwitchRow(card, r++ * rowH, "icon_vibrate", "Haptics", () => GameSettings.Haptics, v => GameSettings.Haptics = v);
            AddSwitchRow(card, r++ * rowH, "icon_motion", "Reduced motion", () => GameSettings.ReducedMotion, v => GameSettings.ReducedMotion = v);
            AddSwitchRow(card, r++ * rowH, "icon_hand", "Left-handed", () => GameSettings.LeftHanded, v => GameSettings.LeftHanded = v);
            AddValueRow(card, r++ * rowH, "icon_size", "Text size", GameSettings.TextPercent + "%", () =>
            {
                GameSettings.NextTextPercent();
                ShowSettings();   // rebuild so the new size applies to every label
            });
            AddValueRow(card, r++ * rowH, "icon_dial", "Difficulty", GameSettings.DifficultyNames[GameSettings.Difficulty], ShowDifficultyList);
            if (dev) AddSwitchRow(card, r++ * rowH, "icon_lock", "GAME_JUMP_TEST", () => GameSettings.GameJumpTest, v => GameSettings.GameJumpTest = v);
            if (dev) AddSwitchRow(card, r++ * rowH, "icon_lock", "SYSTEM_TEST", () => GameSettings.SystemTest, v => GameSettings.SystemTest = v);

            Button reset = UiKit.AddButton(screen, "Reset", "Reset progress", ToyKit.Button(3), Color.white, new Vector2(560f, 130f), ConfirmReset, 46);
            UiKit.Place((RectTransform)reset.transform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(560f, 130f));
        }

        /// <summary>Left part of a settings row: the painted icon and a short label (ink on the cream card).</summary>
        void AddRowLead(RectTransform card, float y, string icon, string label)
        {
            Image ic = UiKit.AddImage(card, label + "Icon", ToyKit.Get(icon), Color.white);
            ic.preserveAspect = true;
            ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0f, 1f);
            ic.rectTransform.pivot = new Vector2(0f, 1f);
            ic.rectTransform.anchoredPosition = new Vector2(28f, -(y + 28f));
            ic.rectTransform.sizeDelta = new Vector2(92f, 92f);
            Text t = UiKit.AddText(card, label, label, 46, Theme.Ink, TextAnchor.MiddleLeft, true);
            DropOutline(t);
            var rt = (RectTransform)t.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(140f, -(y + 28f + 92f));
            rt.offsetMax = new Vector2(-270f, -(y + 28f));
        }

        /// <summary>An on/off row: a kit switch (green with the knob right when on, blue with the knob left when off).</summary>
        void AddSwitchRow(RectTransform card, float y, string icon, string label, Func<bool> get, Action<bool> set)
        {
            AddRowLead(card, y, icon, label);
            Button b = UiKit.AddButton(card, label + "Toggle", "", ToyKit.Button(get() ? 1 : 2), Color.white, new Vector2(200f, 126f), null);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-28f, -(y + 11f));
            rt.sizeDelta = new Vector2(200f, 126f);
            Image knob = UiKit.AddImage(b.transform, "Knob", Theme.RoundSprite, Color.white);
            knob.type = Image.Type.Sliced;
            knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = new Vector2(get() ? 0.74f : 0.26f, 0.5f);
            knob.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            knob.rectTransform.anchoredPosition = Vector2.zero;
            knob.rectTransform.sizeDelta = new Vector2(86f, 86f);
            Image ring = UiKit.AddImage(knob.transform, "Ring", Theme.RingSprite, UiKit.ButtonInk);
            ring.type = Image.Type.Sliced;
            ring.preserveAspect = false;
            UiKit.Stretch(ring.rectTransform);
            b.GetComponent<A11yLabel>().Text = label + (get() ? ", on" : ", off");
            b.onClick.AddListener(() =>
            {
                set(!get());
                bool on = get();
                b.GetComponent<Image>().sprite = ToyKit.Button(on ? 1 : 2);
                knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = new Vector2(on ? 0.74f : 0.26f, 0.5f);
                b.GetComponent<A11yLabel>().Text = label + (on ? ", on" : ", off");
            });
        }

        /// <summary>A row with a yellow value button (text size, difficulty).</summary>
        void AddValueRow(RectTransform card, float y, string icon, string label, string value, Action onClick)
        {
            AddRowLead(card, y, icon, label);
            Button b = UiKit.AddButton(card, label + "Button", value, ToyKit.Button(0), Color.white, new Vector2(240f, 126f), onClick, 44);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-28f, -(y + 11f));
            rt.sizeDelta = new Vector2(240f, 126f);
            b.GetComponent<A11yLabel>().Text = label + ", " + value;
        }

        public void ShowDifficultyList()
        {
            OpenPopup("Difficulty", "How fast the screen speeds up. Harder levels also drop more gems.", 1300f);
            Transform panel = popup.Find("Panel");
            for (int i = 0; i < GameSettings.DifficultyNames.Length; i++)
            {
                int level = i;
                bool current = level == GameSettings.Difficulty;
                string label = GameSettings.DifficultyLabel(level) + "";
                Button opt = UiKit.AddButton(panel, "Diff" + level, label, Btn(current ? 1 : 0), Color.white, new Vector2(700f, 132f), () =>
                {
                    GameSettings.Difficulty = level;
                    GameSession.ApplyDifficulty();
                    ClosePopup();
                    ShowSettings();
                }, 40);
                UiKit.Place((RectTransform)opt.transform, new Vector2(0.5f, 0f), new Vector2(0f, 40f + (GameSettings.DifficultyNames.Length - 1 - i) * 145f), new Vector2(700f, 132f));
            }
        }

        public void ConfirmReset()
        {
            OpenPopup("Reset progress?", "All stars, coins, gems and unlocked regions will be erased on this device.");
            PopupButton("Erase everything", Btn(1), new Color(1f, 0.6f, 0.6f), 230f, () =>
            {
                progress.ResetAll();
                ClosePopup();
                ShowMenu();
            });
            PopupButton("Cancel", Btn(0), Color.white, 60f, ClosePopup);
        }

        // ---------------- popups ----------------

        void OpenPopup(string title, string body, float height = 1180f)
        {
            ClosePopup();
            popup = UiKit.Rect(canvas.transform, "Popup");
            UiKit.Stretch(popup);
            var dim = popup.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.65f);
            RectTransform panel = UiKit.BuildPopupFrame(popup, title, art, new Vector2(860f, height), ClosePopup);
            Text b = UiKit.AddText(panel, "Body", body, 44, Color.white);
            UiKit.OnPanel(b);
            UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0f, -214f), new Vector2(740f, Mathf.Min(520f, height - 214f - 200f)));   // centred in the space between the title and the buttons
            b.alignment = TextAnchor.MiddleCenter;
        }

        Button PopupButton(string label, Sprite sprite, Color tint, float y, Action action)
        {
            return ToyKit.PopupAction(popup.Find("Panel"), label, sprite, tint, y, action);
        }

        void ShowMessage(string title, string body)
        {
            OpenPopup(title, body, 860f);
            PopupButton("OK", Btn(0), Color.white, 60f, ClosePopup);
        }

        /// <summary>A fresh full-screen dim layer for chest overlays (they build their own card on it); ClosePopup removes it.</summary>
        RectTransform NewOverlay()
        {
            ClosePopup();
            popup = UiKit.Rect(canvas.transform, "Popup");
            UiKit.Stretch(popup);
            var dim = popup.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.65f);
            return popup;
        }

        /// <summary>The popup services the bundle timer offer needs (docs/26): the shared modal popup and the full-screen overlay.</summary>
        ShopScreen.Host BundleHost()
        {
            return new ShopScreen.Host
            {
                OpenPopup = (title, body, height) => OpenPopup(title, body, height),
                PopupButton = (label, primary, y, action) => PopupButton(label, Btn(primary ? 1 : 0), Color.white, y, action),
                ClosePopup = ClosePopup,
                NewOverlay = NewOverlay,
                CloseOverlay = ClosePopup,
            };
        }

        void ClosePopup()
        {
            if (popup != null) Destroy(popup.gameObject);
            popup = null;
        }
    }
}
