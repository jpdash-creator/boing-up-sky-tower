using System;
using System.Globalization;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The in-game shop (docs/14-shop-requirements.md). Four tabs: Chests and Gears, Power-ups and Characters, VIP and Battle Pass,
    /// Gems and Coins. Tabs 1 and 3 are "Coming soon" panels; tab 2 sells the power-ups (docs/15: cards with a coins and a gems Buy button,
    /// the characters come later); tab 4 sells gem packs (TEST purchases in development builds, nothing in
    /// release builds because there is no billing yet) and coin packs (paid with gems, real). Built in code under the safe area.
    /// Menu screen only: it has no per-frame code.
    /// </summary>
    public sealed partial class ShopScreen
    {
        /// <summary>What the shop needs from the meta scene: the shared modal popup. The shop never owns a canvas of its own.</summary>
        public sealed class Host
        {
            public Action<string, string, float> OpenPopup;                  // title, body, panel height
            public Func<string, bool, float, Action, Button> PopupButton;    // label, primary (pink) or secondary, y from the panel bottom, action
            public Action ClosePopup;
            /// <summary>A fresh full-screen dim overlay layer for the chest reveal and info cards (docs/17); null where the host has none (chests then do nothing).</summary>
            public Func<RectTransform> NewOverlay;
            public Action CloseOverlay;
            /// <summary>Unused since docs/23 (the shop has no chest section); kept so hosts that still set it compile.</summary>
            public Action OpenTreasureChests;
        }

        public const int TabPowerups = 0, TabCharacters = 1, TabGems = 2;
        public const int GemsSection = 0, CoinsSection = 1;
        /// <summary>Spending more gems than this asks for a confirmation first (docs/14 FR-SHOP-20).</summary>
        public const int ConfirmGemThreshold = 50;

        public static readonly string[] TabNames = { "Power-ups", "Characters", "Coins and Gems" };
        static readonly string[] TabSoonText =
        {
            "Buy power-ups for your runs.",
            "Buy new characters with gems.",
        };

        /// <summary>Tests set this to audit the development-build layout in the editor (where GameSettings.DevBuild is false).</summary>
        public static bool? TestPurchasesOverride;

        public static bool TestPurchasesEnabled { get { return TestPurchasesOverride ?? GameSettings.SystemTest; } }

        // Layout (reference pixels from the top of the safe area). Columns are anchored by fractions of the width.
        const float SideMargin = 28f, ColGap = 16f;
        const float HeaderH = 520f;                                         // painted shop scene
        const float ListTop = HeaderH + 130f;                               // below the ribbon title
        const float BottomMargin = ToyKit.TabBarReserve + 4f;               // room for the icon tab bar
        // Pack cards (docs/23): 3 per row, their height is computed from what they show (BuildPackList); power-up cards hold the (i) button on top, a coins and a gems button.
        const float CardGap = 14f, PowerCardH = 710f, ButtonH = 126f;   // compact cards (owner 2026-10-24): 4 in the first row, 3 in the next, full width

        static readonly Color ListPriceColor = Theme.InkSoft;
        static readonly Color PromoPriceColor = new Color(0.03f, 0.48f, 0.27f, 1f);          // deep green on the light card (about 6:1)
        static readonly string[] TabShort = { "Power-ups", "Characters", "Gems" };
        static readonly Color LaunchBadgeColor = new Color(1f, 0.82f, 0.25f, 1f);
        static readonly Color SpecialBadgeColor = new Color(0.62f, 0.90f, 1f, 1f);

        readonly RectTransform screen;
        readonly ProgressService progress;
        readonly ShopCatalog catalog;
        readonly ArtCatalog art;
        readonly Host host;
        RectTransform body;

        public int Tab { get; private set; }
        public int Section { get; private set; }
        public Text CoinText { get; private set; }
        public Text GemText { get; private set; }

        public ShopScreen(RectTransform screen, ProgressService progress, ShopCatalog catalog, ArtCatalog art, Host host)
        {
            this.screen = screen;
            this.progress = progress;
            this.catalog = catalog;
            this.art = art;
            this.host = host;
        }

        Sprite Btn(int i) { return ToyKit.Button(i == 1 ? 1 : 0) ?? (art != null ? ArtCatalog.Pick(art.Buttons, i) : null); }

        static string Num(int n) { return n.ToString("N0", CultureInfo.InvariantCulture); }

        static void DropOutline(Text t)
        {
            var o = t.GetComponent<Outline>();
            if (o != null) o.enabled = false;
        }

        // ---------------- build ----------------

        /// <summary>Builds the header (title and wallet) and the body for the given tab and section.</summary>
        public void Build(int tab, int section)
        {
            Tab = Mathf.Clamp(tab, 0, TabNames.Length - 1);
            Section = Mathf.Clamp(section, 0, 1);
            BuildHeader();
            RebuildBody();
        }

        public void SelectTab(int tab)
        {
            Tab = Mathf.Clamp(tab, 0, TabNames.Length - 1);
            RebuildBody();
        }

        public void SelectSection(int section)
        {
            Section = Mathf.Clamp(section, 0, 1);
            RebuildBody();
        }

        // ---------------- layout (SS2/SS3: scene header, ribbon title, three icon columns, icon tab bar) ----------------

        void BuildHeader()
        {
            // The painted Momo shopkeeper scene across the top (cover-fit, never stretched), the wallet pills over it, the ribbon below.
            RectTransform scene = UiKit.Rect(screen, "Scene");
            scene.anchorMin = new Vector2(0f, 1f);
            scene.anchorMax = new Vector2(1f, 1f);
            scene.pivot = new Vector2(0.5f, 1f);
            scene.anchoredPosition = Vector2.zero;
            scene.sizeDelta = new Vector2(0f, HeaderH);
            scene.gameObject.AddComponent<RectMask2D>();
            Sprite sc = ToyKit.Get("shop_scene");
            Image si = UiKit.AddImage(scene, "SceneArt", sc, sc != null ? Color.white : Theme.Sky);
            si.preserveAspect = false;
            UiKit.Stretch(si.rectTransform);
            if (sc != null)
            {
                var fit = si.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = sc.rect.width / sc.rect.height;
            }

            // Wallet pills, top right, left to right: coins, gems, stars. The back button sits at the top left.
            RectTransform row = UiKit.Rect(screen, "Wallet");
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(190f, -118f);
            row.offsetMax = new Vector2(-16f, -18f);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 10f;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = true;
            CoinText = ToyKit.WalletPillFlex(row, "CoinsPill", art != null ? art.CoinIcon : null, Color.white, 100f);
            GemText = ToyKit.WalletPillFlex(row, "GemsPill", art != null ? art.GemIcon : null, new Color(0.75f, 0.92f, 1f), 100f);
            StarText = ToyKit.WalletPillFlex(row, "StarsPill", art != null ? art.StarFilled : null, new Color(1f, 0.92f, 0.5f), 100f);
        }

        public Text StarText { get; private set; }

        static readonly string[] SectionTitles = { "Power-ups", "Characters", "Coins", "Gems" };

        /// <summary>Index of the bottom tab for the current tab and section (Power-ups, Characters, Coins, Gems).</summary>
        int BarIndex { get { return Tab == TabGems ? (Section == CoinsSection ? 2 : 3) : Tab; } }

        void SelectBar(int i)
        {
            if (i <= TabCharacters) { Tab = i; }
            else { Tab = TabGems; Section = i == 2 ? CoinsSection : GemsSection; }
            RebuildBody();
        }

        void RebuildBody()
        {
            if (body != null)
            {
                body.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(body.gameObject);
            }
            if (bar != null)
            {
                bar.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(bar.gameObject);
            }
            body = UiKit.Rect(screen, "ShopBody");
            UiKit.Stretch(body);
            ToyKit.Ribbon(body, SectionTitles[BarIndex], new Vector2(0.5f, 1f), new Vector2(0f, -(HeaderH - 70f)), new Vector2(580f, 206f));
            if (Tab == TabGems) BuildPackList(Section == GemsSection);
            else if (Tab == TabPowerups) BuildPowerUps();
            else BuildComingSoon(Tab);
            bar = ToyKit.BuildIconBar(screen, new[]
            {
                new ToyKit.BarItem { Icon = "icon_bolt", Caption = "Power", A11y = "Power-ups" },
                new ToyKit.BarItem { Icon = "icon_heroes", Caption = "Heroes", A11y = "Characters" },
                new ToyKit.BarItem { Icon = "icon_coin", Caption = "Coins", A11y = "Coins" },
                new ToyKit.BarItem { Icon = "icon_gem", Caption = "Gems", A11y = "Gems" },
            }, BarIndex, SelectBar);
            // The wallet texts were created by the header and stay valid; the host refreshes them on every wallet change.
            if (CoinText != null) UiKit.SetText(CoinText, progress.Coins.ToString());
            if (GemText != null) UiKit.SetText(GemText, progress.Gems.ToString());
            if (StarText != null) UiKit.SetText(StarText, TotalStars().ToString());
        }

        int TotalStars()
        {
            int stars = 0;
            for (int i = 1; i <= GameSession.PlayableRegions; i++)
            {
                RegionDef r = GameSession.Repo != null ? GameSession.Repo.TryGetRegionByIndex(i) : null;
                if (r != null) stars += progress.StarsInRegion(r);
            }
            return stars;
        }

        RectTransform bar;

        void BuildComingSoon(int tab)
        {
            RectTransform host = UiKit.Rect(body, "Panel");
            host.anchorMin = Vector2.zero;
            host.anchorMax = Vector2.one;
            host.offsetMin = new Vector2(SideMargin, BottomMargin);
            host.offsetMax = new Vector2(-SideMargin, -ListTop);
            Theme.CardBacking(host);
            Image icon = UiKit.AddImage(host, "Icon", ToyKit.Get("icon_heroes"), Color.white);
            UiKit.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(220f, 220f));
            icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Text head = UiKit.AddText(host, "Soon", "Coming soon", 76, Theme.Ink, TextAnchor.MiddleCenter, true);
            head.GetComponent<Outline>().enabled = false;
            UiKit.Place((RectTransform)head.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(760f, 140f));
            Text info = UiKit.AddText(host, "SoonInfo", TabSoonText[Mathf.Clamp(tab, 0, TabSoonText.Length - 1)], 46, Theme.InkSoft, TextAnchor.MiddleCenter);
            info.GetComponent<Outline>().enabled = false;
            UiKit.Place((RectTransform)info.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -250f), new Vector2(740f, 240f));
        }

        /// <summary>A scrolling area under the ribbon; returns the content rect (top-anchored, full width).</summary>
        RectTransform MakeList(float contentHeight, float top)
        {
            RectTransform vp = UiKit.Rect(body, "ShopViewport");
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(0f, BottomMargin);
            vp.offsetMax = new Vector2(0f, -top);
            vp.gameObject.AddComponent<RectMask2D>();
            var hit = vp.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);   // gives the ScrollRect something to drag on between the cards
            RectTransform list = UiKit.Rect(vp, "ShopList");
            list.anchorMin = new Vector2(0f, 1f);
            list.anchorMax = new Vector2(1f, 1f);
            list.pivot = new Vector2(0.5f, 1f);
            list.offsetMin = Vector2.zero;
            list.offsetMax = Vector2.zero;
            list.sizeDelta = new Vector2(0f, contentHeight);
            var scroll = vp.gameObject.AddComponent<ScrollRect>();
            scroll.content = list;
            scroll.viewport = vp;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 60f;
            return list;
        }

        /// <summary>Grid rule for the pack lists: the first row holds 4 cards, every later row up to 3; a row always spans the full width.</summary>
        static void Cell(int i, int n, out int row, out int col, out int cols)
        {
            if (i < 4) { row = 0; col = i; cols = Mathf.Min(4, n); return; }
            int k = i - 4;
            row = 1 + k / 3;
            col = k % 3;
            int left = n - 4 - (row - 1) * 3;
            cols = Mathf.Min(3, left);
        }

        static int RowCount(int n) { return n <= 4 ? 1 : 1 + (n - 4 + 2) / 3; }

        /// <summary>Power-up grid (owner 2026-10-24): rows of 3; a last row with fewer cards keeps the 3-column width and is centred.</summary>
        static void Cell3(int i, int n, out int row, out int col, out int cols, out float offsetCols)
        {
            row = i / 3;
            col = i % 3;
            cols = 3;
            int inRow = Mathf.Min(3, n - row * 3);
            offsetCols = (3 - inRow) * 0.5f;
        }

        /// <summary>Anchors a card in column col of cols (fractions of the width, so it fits every aspect).</summary>
        static void PlaceCell(RectTransform crt, float y, int col, int cols, float h, float offsetCols = 0f)
        {
            crt.anchorMin = new Vector2((col + offsetCols) / (float)cols, 1f);
            crt.anchorMax = new Vector2((col + offsetCols + 1) / (float)cols, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = new Vector2(0f, -y);
            crt.sizeDelta = new Vector2(-ColGap, h);
        }

        /// <summary>A kit button with a currency icon at the left and the price after it.</summary>
        Button PriceButton(RectTransform card, string name, int role, Sprite icon, string price, string a11y, Action onClick, float bottom)
        {
            Button b = UiKit.AddButton(card, name, price, ToyKit.Button(role), Color.white, new Vector2(200f, ButtonH), onClick, 40);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(14f, bottom);
            rt.offsetMax = new Vector2(-14f, bottom + ButtonH);
            b.GetComponent<A11yLabel>().Text = a11y;
            Text label = b.transform.Find("Label").GetComponent<Text>();
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (icon != null)
            {
                Image ic = UiKit.AddImage(b.transform, "Icon", icon, Color.white);
                ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                ic.rectTransform.pivot = new Vector2(0f, 0.5f);
                ic.rectTransform.anchoredPosition = new Vector2(14f, 0f);
                ic.rectTransform.sizeDelta = new Vector2(52f, 52f);
                var lrt = label.rectTransform;
                lrt.offsetMin = new Vector2(62f, 0f);
                lrt.offsetMax = new Vector2(-8f, 0f);
            }
            return b;
        }

        // ---------------- tab 2: power-ups (docs/15 section 3, docs/14 Tab 2) ----------------

        string lastBoughtId;

        void BuildPowerUps()
        {
            bool dev = TestPurchasesEnabled;
            float top = ListTop + (dev ? 140f : 0f);
            if (dev)
            {
                Button give = UiKit.AddButton(body, "TestGive", "Test: add 3 of each", Btn(0), Color.white, new Vector2(760f, 126f), TestGrantAll, 44);
                UiKit.Place((RectTransform)give.transform, new Vector2(0.5f, 1f), new Vector2(0f, -ListTop), new Vector2(760f, 126f));
                give.GetComponent<A11yLabel>().Text = "Test only: add 3 of every power-up";
            }
            var defs = new System.Collections.Generic.List<PowerUpDef>(progress.PowerUpDefs.Implemented);
            defs.Sort((x, y) => x.CoinPrice != y.CoinPrice ? x.CoinPrice.CompareTo(y.CoinPrice) : y.Order.CompareTo(x.Order));   // cheapest first, Flight last (owner 2026-10-24)
            RectTransform list = MakeList(((defs.Count + 2) / 3) * (PowerCardH + CardGap), top);
            for (int i = 0; i < defs.Count; i++)
            {
                int row, col, cols;
                float off;
                Cell3(i, defs.Count, out row, out col, out cols, out off);
                BuildPowerUpCard(list, row * (PowerCardH + CardGap), col, cols, off, defs[i]);
            }
        }

        /// <summary>One power-up card: icon (tap for the description), name, owned count and one price button per currency.</summary>
        void BuildPowerUpCard(RectTransform list, float y, int col, int cols, float offsetCols, PowerUpDef def)
        {
            RectTransform crt = UiKit.Rect(list, "Panel");
            Transform card = crt;
            Theme.CardBacking(crt);
            PlaceCell(crt, y, col, cols, PowerCardH, offsetCols);

            // The (i) button sits at the top right of the card (a full 48 dp target); the icon starts below it so they never overlap.
            Image icon = PowerUpUi.AddIcon(card, "Icon", def.Type, new Vector2(0.5f, 1f), new Vector2(0f, -(UiKit.MinTouchPx + 6f)), 120f, false);
            icon.rectTransform.pivot = new Vector2(0.5f, 1f);
            icon.raycastTarget = false;
            AddInfoButton(card, def.Name, def.Description + "\nTakes up to " + def.CarryCap + " into a run.");
            Text name = UiKit.AddText(card, "Name", def.Name, 36, Theme.Ink, TextAnchor.MiddleCenter, true);
            name.GetComponent<Outline>().enabled = false;
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            name.rectTransform.anchorMin = new Vector2(0f, 1f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.pivot = new Vector2(0.5f, 1f);
            name.rectTransform.offsetMin = new Vector2(6f, -(UiKit.MinTouchPx + 6f + 120f + 74f));
            name.rectTransform.offsetMax = new Vector2(-6f, -(UiKit.MinTouchPx + 6f + 120f + 2f));

            int owned = progress.PowerUpCount(def.Id);
            bool justBought = def.Id == lastBoughtId;
            Text own = UiKit.AddText(card, "Owned", (justBought ? "Bought! " : "x") + owned, 36, justBought ? PromoPriceColor : Theme.InkSoft, TextAnchor.MiddleCenter, true);
            DropOutline(own);
            UiKit.Place((RectTransform)own.transform, new Vector2(0.5f, 1f), new Vector2(0f, -(UiKit.MinTouchPx + 6f + 120f + 84f)), new Vector2(300f, 72f));
            own.rectTransform.pivot = new Vector2(0.5f, 1f);

            PriceButton(crt, "BuyCoins", 1, art != null ? art.CoinIcon : null, Num(def.CoinPrice), "Buy " + def.Name + " for " + Num(def.CoinPrice) + " coins", () => TapBuyPowerUp(def, PowerUpCurrency.Coins), 14f);
            if (def.GemPrice > 0)
                PriceButton(crt, "BuyGems", 0, art != null ? art.GemIcon : null, Num(def.GemPrice), "Buy " + def.Name + " for " + Num(def.GemPrice) + " gems", () => TapBuyPowerUp(def, PowerUpCurrency.Gems), 14f + ButtonH + 10f);
        }

        /// <summary>The (i) info button at the top right of a power-up card: a transparent 48 dp target with a small circled i drawn in it.</summary>
        void AddInfoButton(Transform card, string title, string body)
        {
            Image hit = UiKit.AddImage(card, "Info", null, new Color(0f, 0f, 0f, 0f));
            hit.rectTransform.anchorMin = hit.rectTransform.anchorMax = new Vector2(1f, 1f);
            hit.rectTransform.pivot = new Vector2(1f, 1f);
            hit.rectTransform.anchoredPosition = new Vector2(-4f, -4f);
            hit.rectTransform.sizeDelta = new Vector2(UiKit.MinTouchPx, UiKit.MinTouchPx);
            MakeInfo(hit, title, body);
            Image ring = UiKit.AddImage(hit.transform, "Ring", Glyphs.Ring, Theme.InkSoft);
            ring.raycastTarget = false;
            ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ring.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            ring.rectTransform.anchoredPosition = Vector2.zero;
            ring.rectTransform.sizeDelta = new Vector2(56f, 56f);
            Text i = UiKit.AddText(ring.transform, "I", "i", 28, Theme.InkSoft, TextAnchor.MiddleCenter, true);
            i.raycastTarget = false;
            i.GetComponent<Outline>().enabled = false;
            UiKit.Stretch((RectTransform)i.transform);
        }

        /// <summary>Makes a card image the tap target for its details popup.</summary>
        void MakeInfo(Image img, string title, string body)
        {
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => Info(title, body));
            img.gameObject.AddComponent<A11yLabel>().Text = "More about " + title;
        }

        void TestGrantAll()
        {
            if (!TestPurchasesEnabled) return;
            foreach (PowerUpDef d in progress.PowerUpDefs.Implemented) progress.GrantPowerUp(d.Id, 3);
            lastBoughtId = null;
            RebuildBody();
        }

        /// <summary>Tap on a Buy button: too few coins or gems explains it and points to the right place; a gem spend above 50 asks first.</summary>
        public void TapBuyPowerUp(PowerUpDef def, PowerUpCurrency currency)
        {
            if (def == null) return;
            int price = PowerUpCatalog.PriceFor(def, currency, 1);
            bool coins = currency == PowerUpCurrency.Coins;
            string unit = coins ? "coins" : "gems";
            int have = coins ? progress.Coins : progress.Gems;
            if (have < price)
            {
                host.OpenPopup("Not enough " + unit, "You need " + Num(price) + " " + unit + " and have " + Num(have) + ".\n" + (coins ? "Earn coins in levels or buy them with gems." : "Get more gems first."), 1180f);
                host.PopupButton(coins ? "Get coins" : "Get gems", true, 230f, () =>
                {
                    host.ClosePopup();
                    Tab = TabGems;
                    SelectSection(coins ? CoinsSection : GemsSection);
                });
                host.PopupButton("Close", false, 60f, host.ClosePopup);
                return;
            }
            if (!coins && price > ConfirmGemThreshold)
            {
                host.OpenPopup("Confirm purchase", "Buy " + def.Name + " for " + Num(price) + " gems?\nYou have " + Num(progress.Gems) + " gems.", 1180f);
                host.PopupButton("Buy for " + Num(price) + " gems", true, 230f, () => CompletePowerUpPurchase(def, currency));
                host.PopupButton("Cancel", false, 60f, host.ClosePopup);
                return;
            }
            CompletePowerUpPurchase(def, currency);
        }

        void CompletePowerUpPurchase(PowerUpDef def, PowerUpCurrency currency)
        {
            host.ClosePopup();
            PowerUpBuyResult result = progress.TryBuyPowerUp(def.Id, currency, 1);
            switch (result)
            {
                case PowerUpBuyResult.Ok:
                    lastBoughtId = def.Id;
                    AudioManager.Play(Sfx.PowerUp);
                    RebuildBody();   // the card shows the new count and a "Bought!" mark; the wallet follows through the host
                    break;
                case PowerUpBuyResult.StackFull:
                    Info("Stock is full", "You already own as many " + def.Name + " as the stock can hold (" + PowerUpCatalog.StackCap + ").");
                    break;
                case PowerUpBuyResult.NotEnoughCoins:
                    Info("Not enough coins", "You do not have enough coins for " + def.Name + ".");
                    break;
                case PowerUpBuyResult.NotEnoughGems:
                    Info("Not enough gems", "You do not have enough gems for " + def.Name + ".");
                    break;
                default:
                    Info("Not available", def.Name + " cannot be bought right now.");
                    break;
            }
        }

        // ---------------- pack lists ----------------

        // Set by BuildPackList for the cards it builds.
        float packCardH, packLabelSlot, packListPriceSlot, packNoteSlot;

        void BuildPackList(bool gems)
        {
            progress.FirstPurchaseDoubleActive = GameSettings.FirstPurchaseDouble;   // switch, OFF by default
            int count = gems ? catalog.GemPacks.Count : catalog.CoinPacks.Count;
            bool promo = progress.LaunchPromoActive;
            int bestCoinIndex = promo ? BestCoinValueIndex() : -1;
            // Slots under the icon only exist when some card of the list needs them, so the cards stay as short as possible.
            bool anyLabel = promo, anyNote = false;
            if (gems)
                for (int i = 0; i < count; i++)
                {
                    if (!string.IsNullOrEmpty(catalog.GemPacks[i].Badge)) anyLabel = true;
                    if (progress.IsFirstPurchaseAvailable(catalog.GemPacks[i])) anyNote = true;
                }
            else if (bestCoinIndex >= 0) anyLabel = true;
            packLabelSlot = anyLabel ? 56f : 0f;
            packListPriceSlot = promo ? 48f : 0f;
            packNoteSlot = anyNote ? 84f : 0f;
            // icon 20..170, label, amount 60, list price, note, gap, then the 126 tall button 14 above the bottom
            packCardH = 170f + packLabelSlot + 60f + packListPriceSlot + packNoteSlot + 10f + ButtonH + 14f;
            int rows = (count + 2) / 3;
            RectTransform list = MakeList(rows * (packCardH + CardGap), ListTop);
            for (int i = 0; i < count; i++)
            {
                int idx = i;
                int row, col, cols;
                float off;
                Cell3(i, count, out row, out col, out cols, out off);
                packOffsetCols = off;
                float y = row * (packCardH + CardGap);
                if (gems) BuildGemCard(list, y, col, cols, idx, catalog.GemPacks[i], promo);
                else BuildCoinCard(list, y, col, cols, idx, catalog.CoinPacks[i], promo, i == bestCoinIndex);
            }
        }

        float packOffsetCols;

        /// <summary>Index of the coin pack with the most coins per gem at the promotional prices, or -1 when several tie.</summary>
        int BestCoinValueIndex()
        {
            int best = -1;
            double bestRate = 0.0;
            for (int i = 0; i < catalog.CoinPacks.Count; i++)
            {
                CoinPackDef p = catalog.CoinPacks[i];
                double rate = p.GemsPromo > 0 ? (double)p.Coins / p.GemsPromo : 0.0;
                if (rate > bestRate) { bestRate = rate; best = i; }
            }
            return best >= 0 && CountRate(bestRate) == 1 ? best : -1;
        }

        int CountRate(double rate)
        {
            int n = 0;
            for (int i = 0; i < catalog.CoinPacks.Count; i++)
            {
                CoinPackDef p = catalog.CoinPacks[i];
                if (p.GemsPromo > 0 && Math.Abs((double)p.Coins / p.GemsPromo - rate) <= 1e-9) n++;
            }
            return n;
        }

        void BuildGemCard(RectTransform list, float y, int col, int cols, int index, GemPackDef pack, bool promo)
        {
            bool showPromo = promo && pack.UsdPromo < pack.UsdList;
            string price = ShopCatalog.FormatUsd(ShopCatalog.GemPackUsd(pack, promo));
            string listPrice = showPromo ? ShopCatalog.FormatUsd(pack.UsdList) : null;
            string special = pack.Badge == "popular" ? "Popular" : (pack.Badge == "best_value" ? "Best value" : null);
            bool test = TestPurchasesEnabled;
            string buy = test ? "Buy (test)" : "Not available yet";
            bool first = progress.IsFirstPurchaseAvailable(pack);   // switch on, pack 50 to 1,000 gems, not bought before on this account
            string a11y = "Buy " + pack.Gems + " gems for " + price + (first ? ", first purchase: x2 gems" : "") + (test ? ", test purchase" : ", not available yet");
            string more = Num(pack.Gems) + " gems for " + price + "." + (pack.InrList > 0 ? "\nIn India: Rs " + Num(pack.InrList) + "." : "") + (showPromo ? "\nLaunch offer: normally " + listPrice + "." : "") + (first ? "\nFirst purchase: x2 gems, you get " + Num(progress.GemsFromPack(pack)) + "." : "") + (test ? "\nThis is a test purchase, no money is charged." : "\nBuying with real money opens with the store release.");
            BuildCard(list, y, col, cols, art != null ? art.GemIcon : null, Num(pack.Gems), listPrice, price, showPromo, special, buy, a11y, "Gem pack", more, () => TapGemPack(index), null, first ? FirstPurchaseText : null);
        }

        void BuildCoinCard(RectTransform list, float y, int col, int cols, int index, CoinPackDef pack, bool promo, bool best)
        {
            bool showPromo = promo && pack.GemsPromo < pack.GemsList;
            int cost = ShopCatalog.CoinPackGems(pack, promo);
            string listPrice = showPromo ? pack.GemsList + " gems" : null;
            string price = cost + " gems";
            string a11y = "Buy " + pack.Coins + " coins for " + cost + " gems";
            string more = Num(pack.Coins) + " coins for " + price + "." + (showPromo ? "\nLaunch offer: normally " + listPrice + "." : "") + "\nPaid with gems from your wallet.";
            // Always a green button with a small gem icon and the gem count; a pack the player cannot afford sends them to the Gems tab on tap (TapCoinPack).
            if (progress.Gems < cost) a11y += ", not enough gems, opens the Gems tab";
            BuildCard(list, y, col, cols, art != null ? art.CoinIcon : null, Num(pack.Coins), listPrice != null ? pack.GemsList.ToString() : null, cost.ToString(), showPromo, best ? "Best value" : null, "Buy", a11y, "Coin pack", more, () => TapCoinPack(index), art != null ? art.GemIcon : null);
        }

        /// <summary>Text on a gem pack card that still has its once-only first-purchase double.</summary>
        public const string FirstPurchaseText = "First purchase: x2 gems";

        /// <summary>
        /// One pack card (three per row, docs/23): the big painted icon (decorative), "Popular" / "Best value" as small green italic text below it,
        /// the amount, the crossed-out launch price when there is one, the first-purchase text, and one price button. Text is ink or deep green on the
        /// cream card (contrast above 6:1).
        /// </summary>
        void BuildCard(RectTransform list, float y, int col, int cols, Sprite icon, string amount, string listPrice, string price, bool launch, string special, string buyLabel, string a11y, string infoTitle, string infoBody, Action onBuy, Sprite priceIcon, string note = null, string buttonText = null)
        {
            RectTransform crt = UiKit.Rect(list, "Panel");
            Transform card = crt;
            Theme.CardBacking(crt);
            PlaceCell(crt, y, col, cols, packCardH, packOffsetCols);

            Image ic = UiKit.AddImage(card, "Icon", icon, Color.white);
            ic.preserveAspect = true;
            UiKit.Place(ic.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(150f, 150f));
            ic.rectTransform.pivot = new Vector2(0.5f, 1f);
            ic.raycastTarget = false;   // decorative: not a touch target

            // "Popular" / "Best value" (and "Offer" while the launch promo is on): small green italic text below the icon, one line (a word is never split), 16 sp.
            string label = special ?? (launch ? "Offer" : null);
            if (label != null)
            {
                Text lt = UiKit.AddText(card, "Label", label, 42, PromoPriceColor, TextAnchor.MiddleCenter, true);
                DropOutline(lt);
                lt.fontStyle = FontStyle.Italic;
                lt.fontSize = Mathf.RoundToInt(UiKit.MinBodyTextPx);
                lt.horizontalOverflow = HorizontalWrapMode.Overflow;
                lt.verticalOverflow = VerticalWrapMode.Overflow;
                lt.rectTransform.anchorMin = new Vector2(0f, 1f);
                lt.rectTransform.anchorMax = new Vector2(1f, 1f);
                lt.rectTransform.pivot = new Vector2(0.5f, 1f);
                lt.rectTransform.offsetMin = new Vector2(4f, -(170f + 56f));
                lt.rectTransform.offsetMax = new Vector2(-4f, -170f);
            }

            float amountTop = 170f + packLabelSlot;
            Text amountText = UiKit.AddText(card, "Amount", amount, 46, Theme.Ink, TextAnchor.MiddleCenter, true);
            amountText.GetComponent<Outline>().enabled = false;
            UiKit.Place((RectTransform)amountText.transform, new Vector2(0.5f, 1f), new Vector2(0f, -amountTop), new Vector2(300f, 60f));
            amountText.rectTransform.pivot = new Vector2(0.5f, 1f);

            float below = amountTop + 60f;
            if (listPrice != null)
            {
                Text lp = UiKit.AddText(card, "ListPrice", listPrice, 34, ListPriceColor, TextAnchor.MiddleCenter);
                lp.GetComponent<Outline>().enabled = false;
                lp.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiKit.Place((RectTransform)lp.transform, new Vector2(0.5f, 1f), new Vector2(0f, -below), new Vector2(300f, 44f));
                lp.rectTransform.pivot = new Vector2(0.5f, 1f);
                float w = Mathf.Min(lp.preferredWidth, 280f);
                Image strike = UiKit.AddImage(lp.transform, "Strike", null, ListPriceColor);
                UiKit.Place(strike.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(w, 4f));
                strike.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }
            below += packListPriceSlot;

            // First-purchase text ("First purchase: x2 gems") under the amount on a gem pack that still has its double.
            if (note != null)
            {
                Text nt = UiKit.AddText(card, "Note", note, 30, PromoPriceColor, TextAnchor.MiddleCenter, true);
                DropOutline(nt);
                nt.horizontalOverflow = HorizontalWrapMode.Wrap;
                nt.verticalOverflow = VerticalWrapMode.Truncate;
                nt.resizeTextForBestFit = true;
                nt.resizeTextMinSize = 18;
                nt.resizeTextMaxSize = 30;
                nt.rectTransform.anchorMin = new Vector2(0f, 1f);
                nt.rectTransform.anchorMax = new Vector2(1f, 1f);
                nt.rectTransform.pivot = new Vector2(0.5f, 1f);
                nt.rectTransform.offsetMin = new Vector2(8f, -(below + packNoteSlot - 6f));
                nt.rectTransform.offsetMax = new Vector2(-8f, -below);
            }

            PriceButton(crt, "Buy", 1, priceIcon, buttonText ?? price, a11y, onBuy, 14f);
        }

        /// <summary>
        /// A short badge centred near the top of a card (smaller font: the 16 sp floor, not scaled up), moved slightly lower so the card's upper border
        /// stays visible. The backing is measured from the text (TagKit). Dark text on a light fill (contrast above 10:1). Returns the badge's bottom edge,
        /// in pixels below the top of the card.
        /// </summary>
        static float AddBadgeTop(Transform card, string text, Color fill, int cols)
        {
            const float top = 20f, padX = 8f * UiKit.PxPerDp;
            // One line always (never wrapped, so no word is split): the backing is as wide as the measured text plus padding. The 16 sp text is
            // short ("Popular", "Best value", "Offer") and fits every card.
            Text t;
            Image pill = TagKit.Build(card, "NameBacking", text, 24, UiKit.ButtonInk, fill, 4000f, out t, false, false, padX);
            DropOutline(t);
            Vector2 size = pill.rectTransform.sizeDelta;
            UiKit.Place(pill.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -top), size);
            pill.rectTransform.pivot = new Vector2(0.5f, 1f);
            return top + size.y;
        }

        // ---------------- purchases ----------------

        void Info(string title, string body)
        {
            host.OpenPopup(title, body, 720f);   // small info card (owner 2026-10-24)
            host.PopupButton("OK", false, 40f, host.ClosePopup);
        }

        /// <summary>Tap on a gem pack. Development builds: confirm, then a TEST purchase grants the gems at once. Release builds: no billing yet.</summary>
        public void TapGemPack(int index)
        {
            if (index < 0 || index >= catalog.GemPacks.Count) return;
            GemPackDef pack = catalog.GemPacks[index];
            if (!TestPurchasesEnabled)
            {
                Info("Not available yet", "Buying gems with real money is not open yet. It arrives with the store release.");
                return;
            }
            progress.FirstPurchaseDoubleActive = GameSettings.FirstPurchaseDouble;
            string price = ShopCatalog.FormatUsd(ShopCatalog.GemPackUsd(pack, progress.LaunchPromoActive));
            int credit = progress.GemsFromPack(pack);
            host.OpenPopup("Confirm test buy", "TEST PURCHASE\n" + Num(pack.Gems) + " gems for " + price + ".\n" + (credit > pack.Gems ? FirstPurchaseText + ": you get " + Num(credit) + " gems.\n" : "") + "No money is charged.", 1180f);
            host.PopupButton("Confirm", true, 230f, () =>
            {
                host.ClosePopup();
                int added = progress.GemsFromPack(pack);
                if (progress.GrantTestGemPurchase(pack) == ShopResult.Ok)
                {
                    RebuildBody();   // the first-purchase text goes away and the coin cards re-check what the player can afford
                    Info("Test buy complete", "+" + Num(added) + " gems added.\nThis was a test purchase.");
                }
            });
            host.PopupButton("Cancel", false, 60f, host.ClosePopup);
        }

        /// <summary>
        /// Tap on a coin pack (docs/23): enough gems opens a confirmation with the gem cost; too few gems goes straight to the Gems tab, no popup.
        /// </summary>
        public void TapCoinPack(int index)
        {
            if (index < 0 || index >= catalog.CoinPacks.Count) return;
            CoinPackDef pack = catalog.CoinPacks[index];
            int price = progress.CoinPackPrice(pack);
            if (progress.Gems < price)
            {
                Tab = TabGems;
                SelectSection(GemsSection);
                return;
            }
            host.OpenPopup("Confirm purchase", "Buy " + Num(pack.Coins) + " coins for " + Num(price) + " gems?\nYou have " + Num(progress.Gems) + " gems.", 1180f);
            host.PopupButton("Buy for " + Num(price) + " gems", true, 230f, () => CompleteCoinPurchase(pack));
            host.PopupButton("Cancel", false, 60f, host.ClosePopup);
        }

        void CompleteCoinPurchase(CoinPackDef pack)
        {
            host.ClosePopup();
            ShopResult result = progress.TryBuyCoinPack(pack);
            if (result == ShopResult.Ok)
            {
                RebuildBody();   // cards the player can no longer afford now show their gem shortfall
                Info("Coins purchased", "+" + Num(pack.Coins) + " coins added.");
            }
            else Info("Not enough gems", "You do not have enough gems for this pack.");
        }
    }
}
