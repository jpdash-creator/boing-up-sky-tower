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
    public sealed class ShopScreen
    {
        /// <summary>What the shop needs from the meta scene: the shared modal popup. The shop never owns a canvas of its own.</summary>
        public sealed class Host
        {
            public Action<string, string, float> OpenPopup;                  // title, body, panel height
            public Func<string, bool, float, Action, Button> PopupButton;    // label, primary (pink) or secondary, y from the panel bottom, action
            public Action ClosePopup;
        }

        public const int TabChests = 0, TabPowerups = 1, TabVip = 2, TabGems = 3;
        public const int GemsSection = 0, CoinsSection = 1;
        /// <summary>Spending more gems than this asks for a confirmation first (docs/14 FR-SHOP-20).</summary>
        public const int ConfirmGemThreshold = 50;

        public static readonly string[] TabNames = { "Chests and Gears", "Power-ups and Characters", "VIP and Battle Pass", "Gems and Coins" };
        static readonly string[] TabSoonText =
        {
            "Open chests and equip boots, hats and accessories.",
            "Buy power-ups for your runs and unlock new characters.",
            "VIP membership and the season Battle Pass.",
        };

        /// <summary>Tests set this to audit the development-build layout in the editor (where GameSettings.DevBuild is false).</summary>
        public static bool? TestPurchasesOverride;

        public static bool TestPurchasesEnabled { get { return TestPurchasesOverride ?? GameSettings.DevBuild; } }

        // Layout (reference pixels from the top of the safe area).
        const float TabTop = 250f, TabH = 146f, SideMargin = 28f;                  // tab buttons are TabH - 16 = 130 tall (above the 48 dp minimum)
        const float SectionTop = TabTop + TabH + 24f;                        // 420
        const float SectionH = 146f;
        const float ListTop = SectionTop + SectionH + 20f;                   // 586
        const float BottomMargin = 24f + ToyKit.TabBarReserve - 24f;   // room for the persistent tab bar
        const float CardH = 420f, CardGap = 20f, ColGap = 20f, InfoSize = 126f;
        static float ColW { get { return (1080f - 2f * SideMargin - ColGap) * 0.5f; } }

        static readonly Color ListPriceColor = Theme.InkSoft;
        static readonly Color PromoPriceColor = new Color(0.03f, 0.48f, 0.27f, 1f);          // deep green on the light card (about 6:1)
        static readonly string[] TabShort = { "Chests", "Power-ups", "VIP", "Gems" };
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

        void BuildHeader()
        {
            const float rowH = 84f, panelW = 400f;
            Text title = UiKit.AddText(screen, "Header", "Shop", 64, Color.white, TextAnchor.MiddleLeft);
            var trt = (RectTransform)title.transform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0f, 1f);
            trt.offsetMin = new Vector2(190f, -160f);
            trt.offsetMax = new Vector2(-(panelW + 40f), -30f);

            Image panel = Theme.GlassPanel(screen, "WalletPanel");
            UiKit.Place(panel.rectTransform, new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(panelW, rowH * 2f + 30f));
            CoinText = WalletRow(panel.transform, 0, art != null ? art.CoinIcon : null, Color.white);
            GemText = WalletRow(panel.transform, 1, art != null ? art.GemIcon : null, new Color(0.75f, 0.92f, 1f));
        }

        Text WalletRow(Transform parent, int row, Sprite icon, Color numberColor)
        {
            float y = -15f - row * 84f;
            Image ic = UiKit.AddImage(parent, "Icon" + row, icon, Color.white);
            UiKit.Place(ic.rectTransform, new Vector2(0f, 1f), new Vector2(22f, y - 8f), new Vector2(68f, 68f));
            Text t = UiKit.AddText(parent, "Value" + row, "", 48, numberColor, TextAnchor.MiddleRight, true);
            UiKit.Place((RectTransform)t.transform, new Vector2(1f, 1f), new Vector2(-22f, y), new Vector2(250f, 84f));
            return t;
        }

        void RebuildBody()
        {
            if (body != null)
            {
                body.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(body.gameObject);
            }
            body = UiKit.Rect(screen, "ShopBody");
            UiKit.Stretch(body);
            BuildTabBar();
            if (Tab == TabGems)
            {
                BuildSectionBar();
                BuildPackList(Section == GemsSection);
            }
            else if (Tab == TabPowerups) BuildPowerUps();
            else BuildComingSoon(Tab);
            // The wallet texts were created by the header and stay valid; the host refreshes them on every wallet change.
            if (CoinText != null) UiKit.SetText(CoinText, progress.Coins.ToString());
            if (GemText != null) UiKit.SetText(GemText, progress.Gems.ToString());
        }

        void BuildTabBar()
        {
            Button[] tabs = Theme.TabBar(body, TabShort, Tab, SelectTab, TabH, 38);
            for (int i = 0; i < tabs.Length; i++) tabs[i].GetComponent<A11yLabel>().Text = TabNames[i] + " tab" + (i == Tab ? ", selected" : "");
            ((RectTransform)tabs[0].transform.parent).anchoredPosition = new Vector2(0f, -TabTop);
        }

        void BuildSectionBar()
        {
            Button[] sec = Theme.TabBar(body, new[] { "Gems", "Coins" }, Section, SelectSection, SectionH, 40);
            sec[0].GetComponent<A11yLabel>().Text = "Gems section" + (Section == GemsSection ? ", selected" : "");
            sec[1].GetComponent<A11yLabel>().Text = "Coins section" + (Section == CoinsSection ? ", selected" : "");
            sec[0].name = "SectionGems";
            sec[1].name = "SectionCoins";
            ((RectTransform)sec[0].transform.parent).anchoredPosition = new Vector2(0f, -SectionTop);
        }

        void BuildComingSoon(int tab)
        {
            RectTransform host = UiKit.Rect(body, "Panel");
            host.anchorMin = Vector2.zero;
            host.anchorMax = Vector2.one;
            host.offsetMin = new Vector2(SideMargin, BottomMargin + 20f);
            host.offsetMax = new Vector2(-SideMargin, -(SectionTop));
            Theme.CardBacking(host);
            Text head = UiKit.AddText(host, "Soon", "Coming soon", 76, Theme.Ink, TextAnchor.MiddleCenter, true);
            head.GetComponent<Outline>().enabled = false;
            UiKit.Place((RectTransform)head.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(760f, 140f));
            Text info = UiKit.AddText(host, "SoonInfo", TabSoonText[Mathf.Clamp(tab, 0, TabSoonText.Length - 1)], 46, Theme.InkSoft, TextAnchor.MiddleCenter);
            info.GetComponent<Outline>().enabled = false;
            UiKit.Place((RectTransform)info.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(740f, 300f));
        }

        // ---------------- tab 2: power-ups (docs/15 section 3, docs/14 Tab 2) ----------------

        const float PowerCardH = 560f, PowerCardGap = 20f;
        string lastBoughtId;

        void BuildPowerUps()
        {
            bool dev = TestPurchasesEnabled;
            float top = SectionTop + (dev ? 160f : 0f);
            if (dev)
            {
                Button give = UiKit.AddButton(body, "TestGive", "Test: add 3 of each", Btn(0), Color.white, new Vector2(760f, 130f), TestGrantAll, 44);
                UiKit.Place((RectTransform)give.transform, new Vector2(0.5f, 1f), new Vector2(0f, -SectionTop), new Vector2(760f, 130f));
                give.GetComponent<A11yLabel>().Text = "Test only: add 3 of every power-up";
            }
            var defs = progress.PowerUpDefs.Implemented;
            RectTransform vp = UiKit.Rect(body, "ShopViewport");
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(0f, BottomMargin);
            vp.offsetMax = new Vector2(0f, -top);
            vp.gameObject.AddComponent<RectMask2D>();
            var hit = vp.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);

            RectTransform list = UiKit.Rect(vp, "ShopList");
            list.anchorMin = new Vector2(0f, 1f);
            list.anchorMax = new Vector2(1f, 1f);
            list.pivot = new Vector2(0.5f, 1f);
            list.offsetMin = Vector2.zero;
            list.offsetMax = Vector2.zero;
            list.sizeDelta = new Vector2(0f, ((defs.Count + 1) / 2) * (PowerCardH + PowerCardGap));

            var scroll = vp.gameObject.AddComponent<ScrollRect>();
            scroll.content = list;
            scroll.viewport = vp;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 60f;

            for (int i = 0; i < defs.Count; i++) BuildPowerUpCard(list, (i / 2) * (PowerCardH + PowerCardGap), i % 2, defs[i]);
        }

        /// <summary>Anchors a card in the given column of the two-column grid.</summary>
        static void PlaceInColumn(RectTransform crt, float y, int col, float h)
        {
            float x = SideMargin + col * (ColW + ColGap);
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(0f, 1f);
            crt.pivot = new Vector2(0f, 1f);
            crt.anchoredPosition = new Vector2(x, -y);
            crt.sizeDelta = new Vector2(ColW, h);
        }

        /// <summary>The small round info button in the top-right corner of a card; the details open in a popup.</summary>
        void AddInfoButton(Transform card, string title, string body)
        {
            Button b = UiKit.AddButton(card, "Info", "i", Theme.RoundSprite, Theme.SurfaceDim, new Vector2(InfoSize, InfoSize), () => Info(title, body), 54);
            UiKit.Place((RectTransform)b.transform, new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(InfoSize, InfoSize));
            b.GetComponent<A11yLabel>().Text = "More about " + title;
            Image rim = UiKit.AddImage(b.transform, "Rim", Theme.RingSprite, new Color(Theme.InkSoft.r, Theme.InkSoft.g, Theme.InkSoft.b, 0.45f));
            rim.type = Image.Type.Sliced;
            rim.preserveAspect = false;
            UiKit.Stretch(rim.rectTransform);
        }

        /// <summary>One compact power-up card: icon, name, owned count, an info button (description), and one Buy button per currency, 130 px tall each.</summary>
        void BuildPowerUpCard(RectTransform list, float y, int col, PowerUpDef def)
        {
            RectTransform crt = UiKit.Rect(list, "Panel");
            Transform card = crt;
            Theme.CardBacking(crt);
            PlaceInColumn(crt, y, col, PowerCardH);

            PowerUpUi.AddIcon(card, "Icon", def.Type, new Vector2(0f, 1f), new Vector2(16f, -16f), 100f, false);
            AddInfoButton(card, def.Name, def.Description + "\nTakes up to " + def.CarryCap + " into a run.");
            Text name = UiKit.AddText(card, "Name", def.Name, 42, Theme.Ink, TextAnchor.MiddleLeft, true);
            name.GetComponent<Outline>().enabled = false;
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.Place((RectTransform)name.transform, new Vector2(0f, 1f), new Vector2(124f, -16f), new Vector2(ColW - 124f - InfoSize - 16f, 100f));

            int owned = progress.PowerUpCount(def.Id);
            bool justBought = def.Id == lastBoughtId;
            AddPillLeft(card, justBought ? "Bought! Own " + owned : "You own " + owned, justBought ? LaunchBadgeColor : SpecialBadgeColor, 136f);

            Button coins = UiKit.AddButton(card, "BuyCoins", Num(def.CoinPrice) + " coins", Btn(1), Color.white, new Vector2(ColW - 32f, 130f), () => TapBuyPowerUp(def, PowerUpCurrency.Coins), 40);
            var crt2 = (RectTransform)coins.transform;
            crt2.anchorMin = new Vector2(0f, 0f); crt2.anchorMax = new Vector2(1f, 0f); crt2.pivot = new Vector2(0.5f, 0f);
            crt2.offsetMin = new Vector2(16f, 164f); crt2.offsetMax = new Vector2(-16f, 294f);
            coins.GetComponent<A11yLabel>().Text = "Buy " + def.Name + " for " + Num(def.CoinPrice) + " coins";
            Button gems = UiKit.AddButton(card, "BuyGems", Num(def.GemPrice) + " gems", Btn(0), Color.white, new Vector2(ColW - 32f, 130f), () => TapBuyPowerUp(def, PowerUpCurrency.Gems), 40);
            var grt = (RectTransform)gems.transform;
            grt.anchorMin = new Vector2(0f, 0f); grt.anchorMax = new Vector2(1f, 0f); grt.pivot = new Vector2(0.5f, 0f);
            grt.offsetMin = new Vector2(16f, 16f); grt.offsetMax = new Vector2(-16f, 146f);
            gems.GetComponent<A11yLabel>().Text = "Buy " + def.Name + " for " + Num(def.GemPrice) + " gems";
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

        void BuildPackList(bool gems)
        {
            int count = gems ? catalog.GemPacks.Count : catalog.CoinPacks.Count;
            RectTransform vp = UiKit.Rect(body, "ShopViewport");
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(0f, BottomMargin);
            vp.offsetMax = new Vector2(0f, -ListTop);
            vp.gameObject.AddComponent<RectMask2D>();
            var hit = vp.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);   // gives the ScrollRect something to drag on between the cards

            RectTransform list = UiKit.Rect(vp, "ShopList");
            list.anchorMin = new Vector2(0f, 1f);
            list.anchorMax = new Vector2(1f, 1f);
            list.pivot = new Vector2(0.5f, 1f);
            list.offsetMin = Vector2.zero;
            list.offsetMax = Vector2.zero;
            list.sizeDelta = new Vector2(0f, ((count + 1) / 2) * (CardH + CardGap));

            var scroll = vp.gameObject.AddComponent<ScrollRect>();
            scroll.content = list;
            scroll.viewport = vp;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 60f;

            bool promo = progress.LaunchPromoActive;
            int bestCoinIndex = promo ? BestCoinValueIndex() : -1;
            for (int i = 0; i < count; i++)
            {
                int idx = i;
                float y = (i / 2) * (CardH + CardGap);
                int col = i % 2;
                if (gems) BuildGemCard(list, y, col, idx, catalog.GemPacks[i], promo);
                else BuildCoinCard(list, y, col, idx, catalog.CoinPacks[i], promo, i == bestCoinIndex);
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

        void BuildGemCard(RectTransform list, float y, int col, int index, GemPackDef pack, bool promo)
        {
            bool showPromo = promo && pack.UsdPromo < pack.UsdList;
            string price = ShopCatalog.FormatUsd(ShopCatalog.GemPackUsd(pack, promo));
            string listPrice = showPromo ? ShopCatalog.FormatUsd(pack.UsdList) : null;
            string special = pack.Badge == "popular" ? "Popular" : (pack.Badge == "best_value" ? "Best value" : null);
            bool test = TestPurchasesEnabled;
            string buy = test ? "Buy (test)" : "Not available yet";
            string a11y = "Buy " + pack.Gems + " gems for " + price + (test ? ", test purchase" : ", not available yet");
            string more = Num(pack.Gems) + " gems for " + price + "." + (showPromo ? "\nLaunch offer: normally " + listPrice + "." : "") + (test ? "\nThis is a test purchase, no money is charged." : "\nBuying with real money opens with the store release.");
            BuildCard(list, y, col, art != null ? art.GemIcon : null, Num(pack.Gems), listPrice, price, showPromo, special, buy, a11y, "Gem pack", more, () => TapGemPack(index));
        }

        void BuildCoinCard(RectTransform list, float y, int col, int index, CoinPackDef pack, bool promo, bool best)
        {
            bool showPromo = promo && pack.GemsPromo < pack.GemsList;
            int cost = ShopCatalog.CoinPackGems(pack, promo);
            string listPrice = showPromo ? pack.GemsList + " gems" : null;
            string price = cost + " gems";
            string a11y = "Buy " + pack.Coins + " coins for " + cost + " gems";
            string more = Num(pack.Coins) + " coins for " + price + "." + (showPromo ? "\nLaunch offer: normally " + listPrice + "." : "") + "\nPaid with gems from your wallet.";
            BuildCard(list, y, col, art != null ? art.CoinIcon : null, Num(pack.Coins), listPrice, price, showPromo, best ? "Best value" : null, "Buy", a11y, "Coin pack", more, () => TapCoinPack(index));
        }

        /// <summary>
        /// One compact pack card (two per row): row 1 icon, amount and the info button; row 2 one badge; row 3 the crossed-out list price and the
        /// price to pay; row 4 the full-width Buy button (130 px). Text sits on the light card backing (contrast far above 4.5:1).
        /// </summary>
        void BuildCard(RectTransform list, float y, int col, Sprite icon, string amount, string listPrice, string price, bool launch, string special, string buyLabel, string a11y, string infoTitle, string infoBody, Action onBuy)
        {
            RectTransform crt = UiKit.Rect(list, "Panel");
            Transform card = crt;
            Theme.CardBacking(crt);
            PlaceInColumn(crt, y, col, CardH);

            Image ic = UiKit.AddImage(card, "Icon", icon, Color.white);
            UiKit.Place(ic.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(92f, 92f));
            AddInfoButton(card, infoTitle, infoBody);

            Text amountText = UiKit.AddText(card, "Amount", amount, 56, Theme.Ink, TextAnchor.MiddleLeft, true);
            amountText.GetComponent<Outline>().enabled = false;
            UiKit.Place((RectTransform)amountText.transform, new Vector2(0f, 1f), new Vector2(118f, -12f), new Vector2(ColW - 118f - InfoSize - 12f, 100f));

            // One badge only: a special badge wins; otherwise the launch offer (the crossed-out price also shows it).
            if (special != null) AddPillLeft(card, special, SpecialBadgeColor, 124f);
            else if (launch) AddPillLeft(card, "Launch offer", LaunchBadgeColor, 124f);

            float priceX = 16f;
            float listW = 0f;
            if (listPrice != null)
            {
                Text lp = UiKit.AddText(card, "ListPrice", listPrice, 38, ListPriceColor, TextAnchor.MiddleLeft);
                lp.GetComponent<Outline>().enabled = false;
                lp.horizontalOverflow = HorizontalWrapMode.Overflow;
                listW = Mathf.Min(lp.preferredWidth, 180f);
                UiKit.Place((RectTransform)lp.transform, new Vector2(0f, 1f), new Vector2(16f, -188f), new Vector2(listW + 8f, 84f));
                // The strike-through: a thin line over the list price (the text is also read out as the list price by the accessibility label).
                Image strike = UiKit.AddImage(lp.transform, "Strike", null, ListPriceColor);
                UiKit.Place(strike.rectTransform, new Vector2(0f, 0.5f), new Vector2(-4f, 0f), new Vector2(listW + 8f, 5f));
                priceX = 16f + listW + 20f;
            }
            Text pr = UiKit.AddText(card, "Price", price, 48, launch ? PromoPriceColor : Theme.Ink, TextAnchor.MiddleLeft, true);
            pr.GetComponent<Outline>().enabled = false;
            UiKit.Place((RectTransform)pr.transform, new Vector2(0f, 1f), new Vector2(priceX, -188f), new Vector2(ColW - priceX - 8f, 84f));

            Button buy = UiKit.AddButton(card, "Buy", buyLabel, Btn(1), Color.white, new Vector2(ColW - 32f, 130f), onBuy, 42);
            var brt = (RectTransform)buy.transform;
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.offsetMin = new Vector2(16f, 16f);
            brt.offsetMax = new Vector2(-16f, 146f);
            buy.GetComponent<A11yLabel>().Text = a11y;
        }

        /// <summary>A rounded badge at the left of a card row, as wide as its text. Dark text on a light fill (contrast above 10:1).</summary>
        static void AddPillLeft(Transform card, string text, Color fill, float y)
        {
            Image pill = UiKit.AddPanel(card, "NameBacking", fill);
            Text t = UiKit.AddText(pill.transform, "Badge", text, 36, UiKit.ButtonInk, TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            DropOutline(t);
            UiKit.Stretch((RectTransform)t.transform);
            UiKit.Place(pill.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -y), new Vector2(Mathf.Min(t.preferredWidth + 36f, ColW - 32f), 60f));
        }

        // ---------------- purchases ----------------

        void Info(string title, string body)
        {
            host.OpenPopup(title, body, 860f);
            host.PopupButton("OK", false, 60f, host.ClosePopup);
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
            string price = ShopCatalog.FormatUsd(ShopCatalog.GemPackUsd(pack, progress.LaunchPromoActive));
            host.OpenPopup("Confirm test buy", "TEST PURCHASE\n" + Num(pack.Gems) + " gems for " + price + ".\nNo money is charged.", 1180f);
            host.PopupButton("Confirm", true, 230f, () =>
            {
                host.ClosePopup();
                if (progress.GrantTestGemPurchase(pack) == ShopResult.Ok)
                    Info("Test buy complete", "+" + Num(pack.Gems) + " gems added.\nThis was a test purchase.");
            });
            host.PopupButton("Cancel", false, 60f, host.ClosePopup);
        }

        /// <summary>Tap on a coin pack: more than 50 gems asks first; too few gems offers the way to the Gems section.</summary>
        public void TapCoinPack(int index)
        {
            if (index < 0 || index >= catalog.CoinPacks.Count) return;
            CoinPackDef pack = catalog.CoinPacks[index];
            int price = progress.CoinPackPrice(pack);
            if (progress.Gems < price)
            {
                host.OpenPopup("Not enough gems", "You need " + Num(price) + " gems and have " + Num(progress.Gems) + ".\nGet more gems first.", 1180f);
                host.PopupButton("Get more gems", true, 230f, () =>
                {
                    host.ClosePopup();
                    Tab = TabGems;
                    SelectSection(GemsSection);
                });
                host.PopupButton("Close", false, 60f, host.ClosePopup);
                return;
            }
            if (price > ConfirmGemThreshold)
            {
                host.OpenPopup("Confirm purchase", "Buy " + Num(pack.Coins) + " coins for " + Num(price) + " gems?\nYou have " + Num(progress.Gems) + " gems.", 1180f);
                host.PopupButton("Buy for " + Num(price) + " gems", true, 230f, () => CompleteCoinPurchase(pack));
                host.PopupButton("Cancel", false, 60f, host.ClosePopup);
                return;
            }
            CompleteCoinPurchase(pack);
        }

        void CompleteCoinPurchase(CoinPackDef pack)
        {
            host.ClosePopup();
            ShopResult result = progress.TryBuyCoinPack(pack);
            if (result == ShopResult.Ok) Info("Coins purchased", "+" + Num(pack.Coins) + " coins added.");
            else Info("Not enough gems", "You do not have enough gems for this pack.");
        }
    }
}
