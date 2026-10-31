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
    /// The in-game shop (docs/14, docs/22, docs/23; restyled by docs/26 section 1 to the reference screenshots). Five tabs in the bottom bar:
    /// Bundles | Chests | Power-ups | Coins | Gems, all built inside <see cref="ShopChrome"/> (Momo shopkeeper scene on a wood background, ribbon title, framed cards).
    /// Bundles is built by BundlesScreen; Chests hosts the Treasure Shop page (<see cref="TreasureChestScreen"/>); Power-ups sells the power-ups (a coins and a gems
    /// Buy button each); Coins sells coin packs for gems (real); Gems sells gem packs (TEST purchases in development builds, nothing in release builds because
    /// there is no billing yet). Characters stay on the Heroes screen. Built in code under the safe area. Menu screen only: it has no per-frame code.
    /// The Subscription button of the header lives in ShopScreen.Subscription.cs (AddSubscriptionButton).
    /// </summary>
    public sealed partial class ShopScreen
    {
        /// <summary>What the shop needs from the meta scene: the shared modal popup. The shop never owns a canvas of its own.</summary>
        public sealed class Host
        {
            public Action<string, string, float> OpenPopup;                  // title, body, panel height
            public Func<string, bool, float, Action, Button> PopupButton;    // label, primary (pink) or secondary, y from the panel bottom, action
            public Action ClosePopup;
            /// <summary>A fresh full-screen dim overlay layer for the chest reveal and cards (docs/17); null where the host has none (chests then do nothing).</summary>
            public Func<RectTransform> NewOverlay;
            public Action CloseOverlay;
            /// <summary>Unused (the Chests tab is part of the shop); kept so hosts that still set it compile.</summary>
            public Action OpenTreasureChests;
            /// <summary>Opens the Subscription page (docs/26); the header shows a Subscription button only when the host sets this.</summary>
            public Action OpenSubscription;
        }

        public const int TabBundles = ShopChrome.TabBundles, TabChests = ShopChrome.TabChests, TabPowerups = ShopChrome.TabPowerups, TabCoins = ShopChrome.TabCoins, TabGems = ShopChrome.TabGems;
        /// <summary>Kept for callers that still pass a section: ShowShop(TabGems, CoinsSection) opens the Coins tab.</summary>
        public const int GemsSection = 0, CoinsSection = 1;
        /// <summary>Spending more gems than this asks for a confirmation first (docs/14 FR-SHOP-20).</summary>
        public const int ConfirmGemThreshold = 50;

        public static readonly string[] TabNames = ShopChrome.TabNames;

        /// <summary>Tests set this to audit the development-build layout in the editor (where GameSettings.DevBuild is false).</summary>
        public static bool? TestPurchasesOverride;

        public static bool TestPurchasesEnabled { get { return TestPurchasesOverride ?? GameSettings.SystemTest; } }

        // Layout (reference pixels). Cards are anchored by fractions of the content width.
        const float CardGap = 14f, ColGap = 16f, ButtonH = 126f;
        // Power-up card (gold like the coin and gem cards): a face with the (i), the icon, the name and the count, then a dark strip with the two price buttons.
        const float PowerStripH = 20f + ButtonH + 10f + ButtonH + 12f;
        static float PowerFaceH { get { return 262f + (Mathf.Max(1f, UiKit.TextScale) - 1f) * 200f; } }
        static float PowerCardH { get { return PowerFaceH + PowerStripH; } }

        static readonly Color PromoPriceColor = new Color(0.02f, 0.30f, 0.15f, 1f);          // deep green on the gold card (about 6:1)
        static readonly Color OnBlue = new Color(0.86f, 0.93f, 1f, 1f);                       // secondary text on the blue power-up cards

        readonly RectTransform screen;
        readonly ProgressService progress;
        readonly ShopCatalog catalog;
        readonly ArtCatalog art;
        readonly Host host;
        RectTransform body;          // the content rect of the current page
        RectTransform page;          // its parent (ribbon + content); destroyed on every tab change
        RectTransform bar;
        TreasureChestScreen treasure;

        public int Tab { get; private set; }
        /// <summary>Gems or Coins (legacy view of the tab for callers that think in sections).</summary>
        public int Section { get { return Tab == TabCoins ? CoinsSection : GemsSection; } }
        public Text CoinText { get; private set; }
        public Text GemText { get; private set; }
        public Text StarText { get; private set; }
        /// <summary>The Treasure Shop page while the Chests tab shows (null otherwise). Tests drive purchases through it.</summary>
        public TreasureChestScreen Treasure { get { return Tab == TabChests ? treasure : null; } }

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

        /// <summary>Builds the scene with the wallet and the page for the given tab (a legacy Gems tab with the Coins section opens Coins).</summary>
        public void Build(int tab, int section)
        {
            if (tab == TabGems && section == CoinsSection) tab = TabCoins;
            Tab = Mathf.Clamp(tab, 0, ShopChrome.TabCount - 1);
            Text coin, gem, star;
            ShopChrome.BuildScene(screen, art, progress, out coin, out gem, out star);
            CoinText = coin;
            GemText = gem;
            StarText = star;
            AddSubscriptionButton();   // ShopScreen.Subscription.cs
            RebuildBody();
        }

        public void SelectTab(int tab)
        {
            Tab = Mathf.Clamp(tab, 0, ShopChrome.TabCount - 1);
            RebuildBody();
        }

        /// <summary>Legacy: Gems or Coins.</summary>
        public void SelectSection(int section)
        {
            SelectTab(section == CoinsSection ? TabCoins : TabGems);
        }

        /// <summary>Sets the three wallet pills from the save (the host also refreshes the coin and gem ones on every wallet change).</summary>
        public void RefreshWallet()
        {
            if (CoinText != null) UiKit.SetText(CoinText, progress.Coins.ToString());
            if (GemText != null) UiKit.SetText(GemText, progress.Gems.ToString());
            if (StarText != null) UiKit.SetText(StarText, ShopChrome.TotalStars(progress).ToString());
        }

        void RebuildBody()
        {
            if (page != null)
            {
                page.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(page.gameObject);
            }
            if (bar != null)
            {
                bar.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(bar.gameObject);
            }
            treasure = null;
            body = ShopChrome.BuildBody(screen, ShopChrome.TabNames[Tab], ShopChrome.RibbonColor.Yellow);   // same yellow italic header on every page
            page = (RectTransform)body.parent;
            switch (Tab)
            {
                case TabBundles: BuildBundles(); break;
                case TabChests: BuildChests(); break;
                case TabPowerups: BuildPowerUps(); break;
                case TabCoins: BuildPackList(false); break;
                default: BuildPackList(true); break;
            }
            bar = ShopChrome.BuildTabBar(screen, Tab, SelectTab, null);
            RefreshWallet();
        }

        // ---------------- tab 0: bundles ----------------

        void BuildBundles()
        {
            BundlesScreen.Build(body, progress, art, host, RefreshWallet);   // docs/26 section 3 (written by stream B2)
        }

        // ---------------- tab 1: chests (the Treasure Shop page) ----------------

        void BuildChests()
        {
            var h = new TreasureChestScreen.Host
            {
                NewOverlay = host.NewOverlay,
                CloseOverlay = host.CloseOverlay,
                BuyGemPack = index =>
                {
                    if (host.CloseOverlay != null) host.CloseOverlay();
                    SelectTab(TabGems);
                    TapGemPack(index);
                },
            };
            treasure = new TreasureChestScreen(screen, progress, catalog, art, h);
            treasure.BuildPage(body);
        }

        // ---------------- grid helpers ----------------

        /// <summary>Power-up grid: rows of 3; a last row with fewer cards keeps the 3-column width and is centred.</summary>
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

        /// <summary>A price button stretched across the width of its card, <paramref name="bottom"/> above the card's bottom edge.</summary>
        Button BottomButton(RectTransform card, string name, int role, Sprite icon, string text, string a11y, Action onClick, float bottom, float side)
        {
            Button b = ShopChrome.PriceButton(card, name, role, icon, text, a11y, onClick);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(side, bottom);
            rt.offsetMax = new Vector2(-side, bottom + ButtonH);
            return b;
        }

        // ---------------- tab 2: power-ups (docs/15 section 3, docs/14 Tab 2) ----------------

        string lastBoughtId;

        void BuildPowerUps()
        {
            bool dev = TestPurchasesEnabled;
            RectTransform area = body;
            if (dev)
            {
                Button give = UiKit.AddButton(body, "TestGive", "Test: add 3 of each", Btn(0), Color.white, new Vector2(760f, 126f), TestGrantAll, 44);
                UiKit.Place((RectTransform)give.transform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(760f, 126f));
                give.GetComponent<A11yLabel>().Text = "Test only: add 3 of every power-up";
                area = UiKit.Rect(body, "ListHost");
                area.anchorMin = Vector2.zero;
                area.anchorMax = Vector2.one;
                area.offsetMin = Vector2.zero;
                area.offsetMax = new Vector2(0f, -140f);
            }
            var defs = new System.Collections.Generic.List<PowerUpDef>(progress.PowerUpDefs.Implemented);
            defs.Sort((x, y) => x.CoinPrice != y.CoinPrice ? x.CoinPrice.CompareTo(y.CoinPrice) : y.Order.CompareTo(x.Order));   // cheapest first, Flight last (owner 2026-10-24)
            RectTransform list = ShopChrome.ScrollArea(area, ((defs.Count + 2) / 3) * (PowerCardH + CardGap));
            for (int i = 0; i < defs.Count; i++)
            {
                int row, col, cols;
                float off;
                Cell3(i, defs.Count, out row, out col, out cols, out off);
                BuildPowerUpCard(list, row * (PowerCardH + CardGap), col, cols, off, defs[i]);
            }
        }

        /// <summary>
        /// One power-up card in the coin and gem card colours: a gold face with the (i) at the top left, the icon centred in the face, the name and the owned
        /// count under it, and a dark strip along the bottom with one green coins and one yellow gems button.
        /// </summary>
        void BuildPowerUpCard(RectTransform list, float y, int col, int cols, float offsetCols, PowerUpDef def)
        {
            RectTransform crt = ShopChrome.Card(list, "Panel", ShopChrome.FrameColor.Gold);
            Transform card = crt;
            PlaceCell(crt, y, col, cols, PowerCardH, offsetCols);
            float k = Mathf.Max(1f, UiKit.TextScale) - 1f;
            float nameH = 60f + k * 100f;

            Image strip = ToyKit.Sliced(card, "Fill", Theme.RoundSprite, 26f);
            strip.color = new Color(0.66f, 0.36f, 0.08f, 1f);
            strip.rectTransform.anchorMin = new Vector2(0f, 0f);
            strip.rectTransform.anchorMax = new Vector2(1f, 0f);
            strip.rectTransform.pivot = new Vector2(0.5f, 0f);
            strip.rectTransform.offsetMin = new Vector2(9f, 9f);
            strip.rectTransform.offsetMax = new Vector2(-9f, 9f + PowerStripH);

            // the icon, centred across the face (the (i) sits at the top left, clear of it)
            Image icon = PowerUpUi.AddIcon(card, "Icon", def.Type, new Vector2(0.5f, 1f), new Vector2(0f, -12f), 110f, false);
            icon.rectTransform.pivot = new Vector2(0.5f, 1f);
            icon.raycastTarget = false;
            AddInfoButton(card, def.Name, def.Description + "\nYou can own and use as many as you like.");
            Text name = UiKit.AddText(card, "Name", def.Name, 36, Theme.Ink, TextAnchor.MiddleCenter, true);
            DropOutline(name);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            name.rectTransform.anchorMin = new Vector2(0f, 1f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.pivot = new Vector2(0.5f, 1f);
            name.rectTransform.offsetMin = new Vector2(6f, -(128f + nameH));
            name.rectTransform.offsetMax = new Vector2(-6f, -128f);

            int owned = progress.PowerUpCount(def.Id);
            bool justBought = def.Id == lastBoughtId;
            Text own = UiKit.AddText(card, "Owned", (justBought ? "Bought! " : "x") + owned, 36, justBought ? PromoPriceColor : Theme.Ink, TextAnchor.MiddleCenter, true);
            DropOutline(own);
            own.horizontalOverflow = HorizontalWrapMode.Overflow;
            own.rectTransform.anchorMin = new Vector2(0f, 1f);
            own.rectTransform.anchorMax = new Vector2(1f, 1f);
            own.rectTransform.pivot = new Vector2(0.5f, 1f);
            own.rectTransform.offsetMin = new Vector2(6f, -(130f + nameH * 2f));
            own.rectTransform.offsetMax = new Vector2(-6f, -(130f + nameH));

            BottomButton(crt, "BuyCoins", 1, art != null ? art.CoinIcon : null, Num(def.CoinPrice), "Buy " + def.Name + " for " + Num(def.CoinPrice) + " coins", () => TapBuyPowerUp(def, PowerUpCurrency.Coins), 20f, 22f);
            if (def.GemPrice > 0)
                BottomButton(crt, "BuyGems", 0, art != null ? art.GemIcon : null, Num(def.GemPrice), "Buy " + def.Name + " for " + Num(def.GemPrice) + " gems", () => TapBuyPowerUp(def, PowerUpCurrency.Gems), 20f + ButtonH + 10f, 22f);
        }

        /// <summary>The (i) info button at the top LEFT of a power-up card: a 48 dp target with a small circled i drawn at its top left corner.</summary>
        void AddInfoButton(Transform card, string title, string body)
        {
            Image hit = UiKit.AddImage(card, "Info", null, new Color(0f, 0f, 0f, 0f));
            hit.rectTransform.anchorMin = hit.rectTransform.anchorMax = new Vector2(0f, 1f);
            hit.rectTransform.pivot = new Vector2(0f, 1f);
            hit.rectTransform.anchoredPosition = new Vector2(4f, -4f);
            hit.rectTransform.sizeDelta = new Vector2(UiKit.MinTouchPx, UiKit.MinTouchPx);
            MakeInfo(hit, title, body);
            Image ring = UiKit.AddImage(hit.transform, "Ring", Glyphs.Ring, Theme.Ink);
            ring.raycastTarget = false;
            ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = new Vector2(0f, 1f);
            ring.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            ring.rectTransform.anchoredPosition = new Vector2(34f, -34f);
            ring.rectTransform.sizeDelta = new Vector2(50f, 50f);
            Text i = UiKit.AddText(ring.transform, "I", "i", 28, Theme.Ink, TextAnchor.MiddleCenter, true);
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
                    SelectTab(coins ? TabCoins : TabGems);
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

        // ---------------- tabs 3 and 4: coin and gem packs (3 per row, gold cards) ----------------

        // Set by BuildPackList for the cards it builds.
        float packCardH, packLabelSlot, packListPriceSlot, packNoteSlot, packOffsetCols;
        const float PileTop = 16f, PileH = 170f, StripH = ButtonH + 22f;
        static float TextK { get { return Mathf.Max(1f, UiKit.TextScale) - 1f; } }
        static float AmountH { get { return 58f + TextK * 80f; } }

        void BuildPackList(bool gems)
        {
            progress.FirstPurchaseDoubleActive = GameSettings.FirstPurchaseDouble;   // switch, OFF by default
            int count = gems ? catalog.GemPacks.Count : catalog.CoinPacks.Count;
            bool promo = !gems && progress.LaunchPromoActive;
            int bestCoinIndex = promo ? BestCoinValueIndex() : -1;
            // Slots under the picture only exist when some card of the list needs them, so the cards stay as short as possible.
            bool anyLabel = bestCoinIndex >= 0, anyNote = false;
            if (gems)
                for (int i = 0; i < count; i++)
                {
                    if (!string.IsNullOrEmpty(catalog.GemPacks[i].Badge)) anyLabel = true;
                    if (progress.IsFirstPurchaseAvailable(catalog.GemPacks[i])) anyNote = true;
                }
            else if (promo) anyLabel = true;
            packLabelSlot = anyLabel ? 52f + TextK * 80f : 0f;
            packListPriceSlot = promo ? 48f + TextK * 70f : 0f;
            packNoteSlot = anyNote ? 84f : 0f;
            // picture, label, amount, list price, note, gap, then the price strip (dark band with the green button)
            packCardH = PileTop + PileH + packLabelSlot + AmountH + packListPriceSlot + packNoteSlot + 8f + StripH + 9f;
            int rows = (count + 2) / 3;
            RectTransform list = ShopChrome.ScrollArea(body, rows * (packCardH + CardGap));
            for (int i = 0; i < count; i++)
            {
                int row, col, cols;
                float off;
                Cell3(i, count, out row, out col, out cols, out off);
                packOffsetCols = off;
                float y = row * (packCardH + CardGap);
                float tier = count > 1 ? i / (float)(count - 1) : 0f;
                if (gems) BuildGemCard(list, y, col, cols, i, catalog.GemPacks[i], tier);
                else BuildCoinCard(list, y, col, cols, i, catalog.CoinPacks[i], promo, i == bestCoinIndex, tier);
            }
        }

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

        /// <summary>The real-money price text of a gem pack ("$0.79"; list price, or the promo price while the promo is on).</summary>
        public string PriceText(GemPackDef pack) { return ShopChrome.PriceText(ShopCatalog.GemPackUsd(pack, progress.LaunchPromoActive)); }

        void BuildGemCard(RectTransform list, float y, int col, int cols, int index, GemPackDef pack, float tier)
        {
            string price = PriceText(pack);
            string special = pack.Badge == "popular" ? "Popular" : (pack.Badge == "best_value" ? "Best value" : null);
            bool test = TestPurchasesEnabled;
            bool first = progress.IsFirstPurchaseAvailable(pack);   // switch on, pack 50 to 1,000 gems, not bought before on this account
            string a11y = "Buy " + pack.Gems + " gems for " + price + (first ? ", first purchase: x2 gems" : "") + (test ? ", test purchase" : ", not available yet");
            // The picture is Resources/Art/Ui/gem_pile_<gems>.png (for example gem_pile_550.png); without it a pile of gem icons that grows with the pack is drawn.
            BuildCard(list, y, col, cols, ToyKit.Get("gem_pile_" + pack.Gems), art != null ? art.GemIcon : null, tier, Num(pack.Gems), null, price, false, special, a11y, () => TapGemPack(index), null, first ? FirstPurchaseText : null);
        }

        void BuildCoinCard(RectTransform list, float y, int col, int cols, int index, CoinPackDef pack, bool promo, bool best, float tier)
        {
            bool showPromo = promo && pack.GemsPromo < pack.GemsList;
            int cost = ShopCatalog.CoinPackGems(pack, promo);
            string listPrice = showPromo ? pack.GemsList + " gems" : null;
            string price = cost + " gems";
            string a11y = "Buy " + pack.Coins + " coins for " + cost + " gems";
            // Always a green button with a small gem icon and the gem count; a pack the player cannot afford sends them to the Gems tab on tap (TapCoinPack).
            if (progress.Gems < cost) a11y += ", not enough gems, opens the Gems tab";
            // The picture is Resources/Art/Ui/coin_pile_<coins>.png; without it a pile of coin icons is drawn.
            BuildCard(list, y, col, cols, ToyKit.Get("coin_pile_" + pack.Coins), art != null ? art.CoinIcon : null, tier, Num(pack.Coins), listPrice != null ? pack.GemsList.ToString() : null, cost.ToString(), showPromo, best ? "Best value" : null, a11y, () => TapCoinPack(index), art != null ? art.GemIcon : null);
        }

        /// <summary>Text on a gem pack card that still has its once-only first-purchase double.</summary>
        public const string FirstPurchaseText = "First purchase: x2 gems";

        /// <summary>
        /// One pack card (three per row, SS14): a gold card with the big pile picture (decorative), "Popular" / "Best value" as small green italic text below it,
        /// the amount, the crossed-out launch price when there is one, the first-purchase text, and one green price button on a dark strip. Text is dark ink or
        /// deep green on the gold card (contrast above 6:1).
        /// </summary>
        void BuildCard(RectTransform list, float y, int col, int cols, Sprite pile, Sprite fallbackIcon, float tier, string amount, string listPrice, string price, bool launch, string special, string a11y, Action onBuy, Sprite priceIcon, string note = null)
        {
            RectTransform crt = ShopChrome.Card(list, "Panel", ShopChrome.FrameColor.Gold);
            Transform card = crt;
            PlaceCell(crt, y, col, cols, packCardH, packOffsetCols);

            // dark price strip along the bottom, like the reference
            Image strip = ToyKit.Sliced(card, "Fill", Theme.RoundSprite, 26f);
            strip.color = new Color(0.66f, 0.36f, 0.08f, 1f);
            strip.rectTransform.anchorMin = new Vector2(0f, 0f);
            strip.rectTransform.anchorMax = new Vector2(1f, 0f);
            strip.rectTransform.pivot = new Vector2(0.5f, 0f);
            strip.rectTransform.offsetMin = new Vector2(9f, 9f);
            strip.rectTransform.offsetMax = new Vector2(-9f, 9f + StripH);

            // the pile picture (or the code-drawn pile of icons)
            RectTransform area = UiKit.Rect(card, "PileArea");
            area.anchorMin = area.anchorMax = new Vector2(0.5f, 1f);
            area.pivot = new Vector2(0.5f, 1f);
            area.anchoredPosition = new Vector2(0f, -PileTop);
            area.sizeDelta = new Vector2(300f, PileH);
            if (pile != null)
            {
                Image pic = UiKit.AddImage(area, "Pile", pile, Color.white);
                pic.preserveAspect = true;
                pic.raycastTarget = false;   // decorative: not a touch target
                UiKit.Stretch(pic.rectTransform);
            }
            else ShopChrome.IconPile(area, fallbackIcon, tier);

            // "Popular" / "Best value" (and "Offer" while the launch promo is on): small green italic text below the picture, one line
            string label = special ?? (launch ? "Offer" : null);
            if (label != null)
            {
                Text lt = UiKit.AddText(card, "Label", label, 42, PromoPriceColor, TextAnchor.MiddleCenter, true);
                DropOutline(lt);
                lt.fontStyle = FontStyle.Italic;
                lt.fontSize = Mathf.RoundToInt(UiKit.MinBodyTextPx * UiKit.TextScale);
                lt.horizontalOverflow = HorizontalWrapMode.Overflow;
                lt.verticalOverflow = VerticalWrapMode.Overflow;
                lt.rectTransform.anchorMin = new Vector2(0f, 1f);
                lt.rectTransform.anchorMax = new Vector2(1f, 1f);
                lt.rectTransform.pivot = new Vector2(0.5f, 1f);
                lt.rectTransform.offsetMin = new Vector2(4f, -(PileTop + PileH + packLabelSlot));
                lt.rectTransform.offsetMax = new Vector2(-4f, -(PileTop + PileH));
            }

            float amountTop = PileTop + PileH + packLabelSlot;
            Text amountText = UiKit.AddText(card, "Amount", amount, 46, Theme.Ink, TextAnchor.MiddleCenter, true);
            DropOutline(amountText);
            amountText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Place((RectTransform)amountText.transform, new Vector2(0.5f, 1f), new Vector2(0f, -amountTop), new Vector2(300f, AmountH));
            amountText.rectTransform.pivot = new Vector2(0.5f, 1f);

            float below = amountTop + AmountH;
            if (listPrice != null)
            {
                Text lp = UiKit.AddText(card, "ListPrice", listPrice, 34, Theme.Ink, TextAnchor.MiddleCenter);
                DropOutline(lp);
                lp.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiKit.Place((RectTransform)lp.transform, new Vector2(0.5f, 1f), new Vector2(0f, -below), new Vector2(300f, packListPriceSlot));
                lp.rectTransform.pivot = new Vector2(0.5f, 1f);
                float w = Mathf.Min(lp.preferredWidth, 280f);
                Image sl = UiKit.AddImage(lp.transform, "Strike", null, Theme.Ink);
                UiKit.Place(sl.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(w, 4f));
                sl.rectTransform.pivot = new Vector2(0.5f, 0.5f);
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

            BottomButton(crt, "Buy", 1, priceIcon, price, a11y, onBuy, 9f + 11f, 22f);
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
            string price = PriceText(pack);
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
                SelectTab(TabGems);
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
