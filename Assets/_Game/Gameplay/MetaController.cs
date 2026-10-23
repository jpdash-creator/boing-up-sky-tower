using System;
using System.Collections.Generic;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Simulation;
using UnityEngine;
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
        /// <summary>The loadout picker of the open level or endless popup (tests open its chooser).</summary>
        public LoadoutPicker CurrentPicker { get; private set; }

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
            RegionArt meadow = art != null ? art.ForRegion(1) : null;
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
            if (art != null && art.Player != null)
            {
                Image m = UiKit.AddImage(screen, "Momo", art.Player, Color.white);
                UiKit.Place(m.rectTransform, new Vector2(0.5f, 0.40f), Vector2.zero, new Vector2(440f, 540f));
                m.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                momo = m.rectTransform;
                momoBase = momo.anchoredPosition;
            }
            Text v = UiKit.AddText(screen, "Version", "v" + Application.version, 30, new Color(1f, 1f, 1f, 0.7f));
            UiKit.Place((RectTransform)v.transform, new Vector2(0.5f, 0f), new Vector2(0f, ToyKit.TabBarReserve + 4f), new Vector2(500f, 44f));
            AddTabBar(ToyKit.TabPlay);
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
        /// middle PLAY button is left out. Gear, Characters and Daily are not built yet and say so.
        /// </summary>
        void AddTabBar(int selected, bool withPlay = true)
        {
            var items = new System.Collections.Generic.List<ToyKit.BarItem>
            {
                new ToyKit.BarItem { Icon = "icon_shop", Caption = "Shop", A11y = "Shop" },
                new ToyKit.BarItem { Icon = "icon_gear", Caption = "Gear", A11y = "Gear" },
            };
            if (withPlay) items.Add(new ToyKit.BarItem { Icon = "icon_play", Caption = "PLAY", A11y = "Play", Raised = true });
            items.Add(new ToyKit.BarItem { Icon = "icon_heroes", Caption = "Heroes", A11y = "Characters" });
            items.Add(new ToyKit.BarItem { Icon = "icon_daily", Caption = "Daily", A11y = "Daily rewards" });
            int[] tabOf = withPlay ? new[] { ToyKit.TabShop, ToyKit.TabGear, ToyKit.TabPlay, ToyKit.TabHeroes, ToyKit.TabDaily } : new[] { ToyKit.TabShop, ToyKit.TabGear, ToyKit.TabHeroes, ToyKit.TabDaily };
            ToyKit.BuildIconBar(screen, items.ToArray(), selected < 0 ? -1 : System.Array.IndexOf(tabOf, selected), i => OnTab(tabOf[i]));
        }

        void OnTab(int tab)
        {
            switch (tab)
            {
                case ToyKit.TabShop: if (CurrentScreen != "Shop") ShowShop(); break;
                case ToyKit.TabPlay: ShowMap(); break;
                case ToyKit.TabGear: ShowMessage("Gear", "Hats, shirts and boots for Momo are coming soon."); break;
                case ToyKit.TabHeroes: ShowMessage("Characters", "New characters are coming soon."); break;
                default: ShowMessage("Daily rewards", "Daily rewards are coming soon."); break;
            }
        }

        // ---------------- shop ----------------

        ShopScreen shop;

        /// <summary>The shop while its screen is showing (null on every other screen). Tests drive purchases through it.</summary>
        public ShopScreen CurrentShop { get { return CurrentScreen == "Shop" ? shop : null; } }

        /// <summary>Shop (docs/14): four tabs, only Gems and Coins is live. Opens on that tab by default.</summary>
        public void ShowShop(int tab = ShopScreen.TabGems, int section = ShopScreen.GemsSection)
        {
            NewScreen("Shop");
            SkyBackground();
            progress.LaunchPromoActive = GameSettings.LaunchPromo;
            var host = new ShopScreen.Host
            {
                OpenPopup = (title, body, height) => OpenPopup(title, body, height),
                PopupButton = (label, primary, y, action) => PopupButton(label, Btn(primary ? 1 : 0), Color.white, y, action),
                ClosePopup = ClosePopup,
            };
            shop = new ShopScreen(screen, progress, ShopCatalog.FromRepository(repo), art, host);
            shop.Build(tab, section);
            AddBack("Back", ShowMenu);
            coinText = shop.CoinText;     // RefreshWallet keeps these two current after every purchase
            gemText = shop.GemText;
            RefreshWallet();
        }

        // ---------------- world map ----------------

        public void ShowMap()
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
            int total = Mathf.Max(1, meta.totalHeight);

            // Scrolling viewport below the fixed top bar. Its content keeps the art's aspect, so the map is never stretched.
            mapViewport = UiKit.Rect(screenRoot, "MapViewport");   // full-bleed (under the notch and system bars); the HUD sits in the safe area above it
            mapViewport.SetSiblingIndex(screen.GetSiblingIndex());
            UiKit.Stretch(mapViewport);
            mapViewport.offsetMax = Vector2.zero;   // the map fills the whole screen; the top bar floats over it
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
            int current = Mathf.Clamp(progress.HighestUnlockedRegionIndex(), 1, Mathf.Max(1, GameSession.PlayableRegions));
            mapCurrent = current;
            for (int i = 1; i <= 30; i++)
            {
                RegionDef region = repo.TryGetRegionByIndex(i);
                if (region == null) continue;
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

        /// <summary>A round pin on the map with a slim dark name tag beside it. Locked and not-yet-built regions show a padlock.</summary>
        void AddRegionPin(RegionDef region, Vector2 anchor)
        {
            bool playable = progress.IsRegionPlayable(region);
            bool unlocked = progress.IsRegionUnlocked(region);
            int cleared = 0;
            if (playable) foreach (string id in region.Levels) if (progress.IsLevelCleared(id)) cleared++;

            // MapNodes order from the art sheet: unlocked, current, completed, locked, teaser, ring.
            int sprite = !playable ? 4 : (!unlocked ? 3 : (cleared >= region.Levels.Count ? 2 : (cleared > 0 ? 1 : 0)));
            Sprite nodeSprite = art != null ? ArtCatalog.Pick(art.MapNodes, sprite) : null;

            const float pin = 110f;
            Button b = UiKit.AddButton(mapContent, "Region" + region.Index, "", nodeSprite, Color.white, new Vector2(pin, pin), () => OnRegionTapped(region), 40);
            b.GetComponent<A11yLabel>().Text = region.Name;
            RectTransform rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            b.GetComponent<Image>().preserveAspect = true;

            bool open = playable && unlocked;
            Image tag = Theme.GlassPanel(rt, "NameBacking", 30f);
            tag.color = open ? new Color(0.05f, 0.08f, 0.22f, 0.78f) : new Color(0.16f, 0.14f, 0.28f, 0.78f);
            string label = playable ? region.Name : "Coming soon";
            Text name = UiKit.AddText(tag.transform, "Name", label, 36, open ? Color.white : new Color(0.88f, 0.88f, 0.95f, 1f), TextAnchor.MiddleCenter);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            float w = name.preferredWidth + 40f;
            bool tagRight = anchor.x < 0.5f;
            UiKit.Place(tag.rectTransform, new Vector2(tagRight ? 1f : 0f, 0.5f), new Vector2(tagRight ? 10f : -10f, 0f), new Vector2(w, 60f));
            tag.rectTransform.pivot = new Vector2(tagRight ? 0f : 1f, 0.5f);
            UiKit.Stretch((RectTransform)name.transform);
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
            SkyBackground();
            AddLevelsWallet(region);
            AddBack("Map", ShowMap);
            List<LevelDef> levels = repo.LevelsOf(region.Id);
            string currentId = null;
            foreach (LevelDef l in levels) if (progress.IsLevelUnlocked(l) && !progress.IsLevelCleared(l.Id)) { currentId = l.Id; break; }

            // 3 columns x 5 rows, level 1 at the bottom (the tower climbs upward). The grid fills the space between the wallet widget and the
            // bottom edge, whatever the screen shape: rows are spread and the circles shrink a little when the screen is short (LevelGridFit).
            RectTransform grid = UiKit.Rect(screen, "LevelGrid");
            UiKit.Stretch(grid);
            grid.offsetMax = new Vector2(0f, -(MapTopBar - 20f));
            grid.offsetMin = new Vector2(0f, ToyKit.TabBarReserve + 8f);   // the icon tab bar sits under the grid, never over a circle or its stars
            var fit = grid.gameObject.AddComponent<LevelGridFit>();
            Text hdr = UiKit.AddText(grid, "Header", region.Name, 76, Color.white, TextAnchor.MiddleCenter, true);
            fit.Title = (RectTransform)hdr.transform;
            Button endlessIcon = ToyKit.IconButton(grid, "Endless", "Endless mode", 1, Glyphs.Infinity, new Vector2(136f, 136f), () => OnEndlessTapped(region), 0.72f);
            fit.TitleExtra = (RectTransform)endlessIcon.transform;
            fit.Nodes = new RectTransform[levels.Count];
            fit.Cols = new int[levels.Count];
            fit.Rows = new int[levels.Count];
            fit.Current = new bool[levels.Count];
            for (int i = 0; i < levels.Count; i++)
            {
                LevelDef l = levels[i];
                int row = i / 3, col = i % 3;
                fit.Nodes[i] = AddLevelNode(l, grid, l.Id == currentId);
                fit.Cols[i] = col;
                fit.Rows[i] = row;
                fit.Current[i] = l.Id == currentId;
            }
            fit.Apply();

            AddTabBar(-1, false);
        }

        /// <summary>Endless start popup (docs/15 FR-PWR-2): best floor, the power-up loadout picker and START. The loadout is optional.</summary>
        public void OnEndlessTapped(RegionDef region)
        {
            int best = progress.EndlessBestFloor(region.Id);
            OpenPopup("Endless", "", 1300f);
            Transform panel = popup.Find("Panel");
            Destroy(panel.Find("Body").gameObject);
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
            var picker = CurrentPicker = new LoadoutPicker(progress, art, OpenShopPowerUps);
            picker.Build(popup, panel, 570f);
            ToyKit.PopupAction(popup.Find("Panel"), "START", Btn(1), Color.white, 60f, () => StartEndless(region), -90f);
            ToyKit.PopupAction(popup.Find("Panel"), "Close", Btn(0), Color.white, 70f, ClosePopup, 100f);
        }

        /// <summary>Leaves the current popup for the shop's Power-ups tab (the loadout picker's way to buy more).</summary>
        void OpenShopPowerUps()
        {
            ClosePopup();
            ShowShop(ShopScreen.TabPowerups);
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
            if (unlocked && complex && art != null && art.GemIcon != null)
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
            OpenPopup("Level " + level.Index, "", 1500f);   // taller than the default: the loadout picker (docs/15 9.4) sits above PLAY
            Transform panel = popup.Find("Panel");
            Destroy(panel.Find("Body").gameObject);

            // Everything is centred on the panel: the goal, the three stars, then what a clear pays as icon pills (no sentences).
            Text goal = UiKit.AddText(panel, "Goal", RunSession.DescribeGoal(spec), 46, Theme.Ink, TextAnchor.MiddleCenter, true);
            DropOutline(goal);
            UiKit.Place((RectTransform)goal.transform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(760f, 110f));
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
                ToyKit.InfoPill(pays, art != null ? art.CoinIcon : null, "50%", new Color(1f, 0.92f, 0.5f), 250f, 96f);   // replays pay half the coins and no gems
            }
            else
            {
                ToyKit.InfoPill(pays, art != null ? art.CoinIcon : null, "30", new Color(1f, 0.92f, 0.5f), 220f, 96f);
                ToyKit.InfoPill(pays, art != null ? art.StarFilled : null, "+40", Color.white, 220f, 96f);
                ToyKit.InfoPill(pays, ToyKit.Get("icon_flame"), "+5", new Color(1f, 0.78f, 0.35f), 220f, 96f);
            }
            if (p != null && p.BestScore > 0)
            {
                RectTransform best = ToyKit.PillRow(panel, "Best", 550f, 96f);
                ToyKit.InfoPill(best, ToyKit.Get("icon_crown"), p.BestScore.ToString(), new Color(1f, 0.9f, 0.4f), 300f, 96f);
            }
            CurrentPicker = new LoadoutPicker(progress, art, OpenShopPowerUps);
            CurrentPicker.Build(popup, panel, 790f);
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
            int rows = 7 + (dev ? 1 : 0);
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
            if (dev) AddSwitchRow(card, r++ * rowH, "icon_lock", "All levels open", () => GameSettings.TestUnlockAll, v => GameSettings.TestUnlockAll = v);

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
                string label = GameSettings.DifficultyLabel(level) + (GameSettings.DifficultyGemPercent[level] > 5 ? "  gems " + GameSettings.DifficultyGemPercent[level] + "%" : "");
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
            UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(740f, 520f));
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

        void ClosePopup()
        {
            if (popup != null) Destroy(popup.gameObject);
            popup = null;
        }
    }
}
