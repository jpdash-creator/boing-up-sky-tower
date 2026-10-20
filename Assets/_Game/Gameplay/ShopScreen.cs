using System;
using System.Globalization;
using SkyHopper.Content;
using SkyHopper.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace SkyHopper.Gameplay
{
    /// <summary>
    /// The in-game shop (docs/14-shop-requirements.md). Four tabs: Chests and Gears, Power-ups and Characters, VIP and Battle Pass,
    /// Gems and Coins. Tabs 1 to 3 are "Coming soon" panels; tab 4 sells gem packs (TEST purchases in development builds, nothing in
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
        const float TabTop = 250f, TabH = 150f, TabGap = 16f, SideMargin = 24f;
        const float SectionTop = TabTop + TabH * 2f + TabGap + 24f;          // 590
        const float SectionH = 130f;
        const float ListTop = SectionTop + SectionH + 20f;                   // 740
        const float BottomMargin = 24f;
        const float CardH = 436f, CardGap = 20f;

        static readonly Color CardColor = new Color(0.05f, 0.07f, 0.16f, 0.95f);
        static readonly Color ListPriceColor = new Color(0.78f, 0.80f, 0.90f, 1f);
        static readonly Color PromoPriceColor = new Color(0.60f, 1f, 0.70f, 1f);
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

        Sprite Btn(int i) { return art != null ? ArtCatalog.Pick(art.Buttons, i) : null; }

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

            Image panel = UiKit.AddPanel(screen, "WalletPanel", new Color(0.05f, 0.07f, 0.16f, 0.92f));
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
            else BuildComingSoon(Tab);
            // The wallet texts were created by the header and stay valid; the host refreshes them on every wallet change.
            if (CoinText != null) UiKit.SetText(CoinText, progress.Coins.ToString());
            if (GemText != null) UiKit.SetText(GemText, progress.Gems.ToString());
        }

        /// <summary>A button in a two column grid: stretched by anchors so it fits every canvas width. The selected one is drawn 5 percent larger.</summary>
        Button GridButton(string name, string label, int col, float top, float height, bool selected, Action action, string a11y)
        {
            Button b = UiKit.AddButton(body, name, label, Btn(selected ? 1 : 0), Color.white, new Vector2(300f, height), action, 40);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(col * 0.5f, 1f);
            rt.anchorMax = new Vector2(col * 0.5f + 0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(col == 0 ? SideMargin : 8f, -(top + height));
            rt.offsetMax = new Vector2(col == 0 ? -8f : -SideMargin, -top);
            rt.localScale = selected ? new Vector3(1.05f, 1.05f, 1f) : Vector3.one;
            b.GetComponent<A11yLabel>().Text = selected ? a11y + ", selected" : a11y;
            return b;
        }

        void BuildTabBar()
        {
            for (int i = 0; i < TabNames.Length; i++)
            {
                int idx = i;
                GridButton("Tab" + (i + 1), TabNames[i], i % 2, TabTop + (i / 2) * (TabH + TabGap), TabH, i == Tab, () => SelectTab(idx), TabNames[i] + " tab");
            }
        }

        void BuildSectionBar()
        {
            GridButton("SectionGems", "Gems", 0, SectionTop, SectionH, Section == GemsSection, () => SelectSection(GemsSection), "Gems section");
            GridButton("SectionCoins", "Coins", 1, SectionTop, SectionH, Section == CoinsSection, () => SelectSection(CoinsSection), "Coins section");
        }

        void BuildComingSoon(int tab)
        {
            Image panel = UiKit.AddPanel(body, "Panel", CardColor);
            var prt = panel.rectTransform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = new Vector2(SideMargin, BottomMargin);
            prt.offsetMax = new Vector2(-SideMargin, -SectionTop);
            Text head = UiKit.AddText(panel.transform, "Soon", "Coming soon", 84, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.Place((RectTransform)head.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(760f, 140f));
            Text info = UiKit.AddText(panel.transform, "SoonInfo", TabSoonText[Mathf.Clamp(tab, 0, TabSoonText.Length - 1)], 48, new Color(0.88f, 0.90f, 1f, 1f), TextAnchor.MiddleCenter);
            UiKit.Place((RectTransform)info.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(740f, 300f));
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
            list.sizeDelta = new Vector2(0f, count * (CardH + CardGap));

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
                float y = i * (CardH + CardGap);
                if (gems) BuildGemCard(list, y, idx, catalog.GemPacks[i], promo);
                else BuildCoinCard(list, y, idx, catalog.CoinPacks[i], promo, i == bestCoinIndex);
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

        void BuildGemCard(RectTransform list, float y, int index, GemPackDef pack, bool promo)
        {
            bool showPromo = promo && pack.UsdPromo < pack.UsdList;
            string price = ShopCatalog.FormatUsd(ShopCatalog.GemPackUsd(pack, promo));
            string listPrice = showPromo ? ShopCatalog.FormatUsd(pack.UsdList) : null;
            string special = pack.Badge == "popular" ? "Popular" : (pack.Badge == "best_value" ? "Best value" : null);
            bool test = TestPurchasesEnabled;
            string buy = test ? "Buy (test)" : "Not available yet";
            string a11y = "Buy " + pack.Gems + " gems for " + price + (test ? ", test purchase" : ", not available yet");
            BuildCard(list, y, art != null ? art.GemIcon : null, Num(pack.Gems), listPrice, price, showPromo, special, buy, a11y, () => TapGemPack(index));
        }

        void BuildCoinCard(RectTransform list, float y, int index, CoinPackDef pack, bool promo, bool best)
        {
            bool showPromo = promo && pack.GemsPromo < pack.GemsList;
            int cost = ShopCatalog.CoinPackGems(pack, promo);
            string listPrice = showPromo ? pack.GemsList + " gems" : null;
            string price = cost + " gems";
            string a11y = "Buy " + pack.Coins + " coins for " + cost + " gems";
            BuildCard(list, y, art != null ? art.CoinIcon : null, Num(pack.Coins), listPrice, price, showPromo, best ? "Best value" : null, "Buy", a11y, () => TapCoinPack(index));
        }

        /// <summary>
        /// One pack card: row 1 icon, amount and badges; row 2 the crossed-out list price and the price to pay; row 3 the full-width Buy
        /// button (well over 48 dp, in the lower half of the card). Text sits on the dark card backing (contrast far above 4.5:1).
        /// </summary>
        void BuildCard(RectTransform list, float y, Sprite icon, string amount, string listPrice, string price, bool launch, string special, string buyLabel, string a11y, Action onBuy)
        {
            Image card = UiKit.AddPanel(list, "Panel", CardColor);
            RectTransform crt = card.rectTransform;
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.offsetMin = new Vector2(SideMargin, -(y + CardH));
            crt.offsetMax = new Vector2(-SideMargin, -y);

            Image ic = UiKit.AddImage(card.transform, "Icon", icon, Color.white);
            UiKit.Place(ic.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -34f), new Vector2(110f, 110f));

            Text amountText = UiKit.AddText(card.transform, "Amount", amount, 64, Color.white, TextAnchor.MiddleLeft, true);
            UiKit.Place((RectTransform)amountText.transform, new Vector2(0f, 1f), new Vector2(148f, -24f), new Vector2(280f, 144f));

            float badgeY = 24f;
            if (launch)
            {
                AddPill(card.transform, "Launch offer", LaunchBadgeColor, badgeY);
                badgeY += 76f;
            }
            if (special != null) AddPill(card.transform, special, SpecialBadgeColor, badgeY);

            float priceX = 24f;
            if (listPrice != null)
            {
                Text lp = UiKit.AddText(card.transform, "ListPrice", listPrice, 42, ListPriceColor, TextAnchor.MiddleLeft);
                lp.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiKit.Place((RectTransform)lp.transform, new Vector2(0f, 1f), new Vector2(24f, -178f), new Vector2(290f, 90f));
                // The strike-through: a thin line over the list price (the text is also read out as the list price by the accessibility label).
                Image strike = UiKit.AddImage(lp.transform, "Strike", null, ListPriceColor);
                UiKit.Place(strike.rectTransform, new Vector2(0f, 0.5f), new Vector2(-4f, 0f), new Vector2(Mathf.Min(lp.preferredWidth, 290f) + 8f, 5f));
                priceX = 24f + 290f + 16f;
            }
            Text pr = UiKit.AddText(card.transform, "Price", price, 56, launch ? PromoPriceColor : Color.white, TextAnchor.MiddleLeft, true);
            UiKit.Place((RectTransform)pr.transform, new Vector2(0f, 1f), new Vector2(priceX, -178f), new Vector2(330f, 90f));

            Button buy = UiKit.AddButton(card.transform, "Buy", buyLabel, Btn(1), Color.white, new Vector2(300f, 130f), onBuy, 46);
            var brt = (RectTransform)buy.transform;
            brt.anchorMin = new Vector2(0f, 1f);
            brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.offsetMin = new Vector2(24f, -(282f + 130f));
            brt.offsetMax = new Vector2(-24f, -282f);
            buy.GetComponent<A11yLabel>().Text = a11y;
        }

        /// <summary>A rounded badge at the top-right of a card, as wide as its text. Dark text on a light fill (contrast above 10:1).</summary>
        static void AddPill(Transform card, string text, Color fill, float y)
        {
            Image pill = UiKit.AddPanel(card, "NameBacking", fill);
            Text t = UiKit.AddText(pill.transform, "Badge", text, 36, UiKit.ButtonInk, TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            DropOutline(t);
            UiKit.Stretch((RectTransform)t.transform);
            UiKit.Place(pill.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, -y), new Vector2(t.preferredWidth + 40f, 68f));
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
