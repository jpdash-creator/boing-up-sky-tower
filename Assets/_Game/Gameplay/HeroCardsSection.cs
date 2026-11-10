using System;
using UnityEngine;
using UnityEngine.UI;
using FluffyClimbers.Economy;
using FluffyClimbers.Simulation;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// The Heroes section of the Shop "Character / Power-ups" page (Bug List 4 E2, docs/26 section 8b): a 2 x 2 grid of hero cards, row 1 Uncommon and Rare, row 2 Elite
    /// (code tier Epic) and Legendary. Each card has the same build as a Power-up card (the (i) at the top left, an icon, a brown strip with a green price button) but its plate is a
    /// light tint of the tier colour (<see cref="BundlesScreen.RarityInk"/>), the icon is the bundle's "?" hero card and the price is the coin price of
    /// <see cref="HeroPurchase"/>. The (i) opens the tier popup; the green button asks to confirm, pays and gives ONE random hero of that rarity from the player's open regions
    /// (<see cref="ProgressService.TryBuyHero"/>), then shows the hero reveal. A tier with no hero in an open region charges nothing and says "No Rare heroes yet".
    /// Only the two buttons are tappable, never the card as a whole. Menu screen only: no per-frame code.
    /// </summary>
    public sealed partial class ShopScreen
    {
        const float HeroIconSize = 150f;
        const float HeroStripH = 20f + ButtonH + 12f;
        static readonly Color HeroStripBrown = new Color(0.66f, 0.36f, 0.08f, 1f);   // the Power-up card strip

        /// <summary>The card plate of a tier: a light tint of its colour, dark text on it stays above 4.5:1.</summary>
        public static Color HeroPlateColor(CharacterRarity r) { return Color.Lerp(BundlesScreen.RarityInk(r), Color.white, 0.84f); }

        /// <summary>The card border of a tier: the tier colour, a little lightened.</summary>
        public static Color HeroEdgeColor(CharacterRarity r) { return Color.Lerp(BundlesScreen.RarityInk(r), Color.white, 0.20f); }

        /// <summary>Height of one hero card: (i) row, icon, rarity name (grows with the text size), then the price strip. Measured by content, never clipped.</summary>
        static float HeroCardH { get { return 12f + HeroIconSize + 10f + (60f + TextK * 100f) + 14f + HeroStripH; } }

        /// <summary>Builds the 2 x 2 grid as one layout child of the page list.</summary>
        void BuildHeroCards(RectTransform pageList)
        {
            RectTransform grid = UiKit.Rect(pageList, "HeroGrid");
            var le = grid.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = 2f * HeroCardH + CardGap;
            for (int i = 0; i < HeroPurchase.Tiers.Length; i++)
                BuildHeroCard(grid, (i / 2) * (HeroCardH + CardGap), i % 2, HeroPurchase.Tiers[i]);
        }

        void BuildHeroCard(RectTransform grid, float y, int col, CharacterRarity rarity)
        {
            string rname = HeroPurchase.RarityName(rarity);
            int price = HeroPurchase.CoinPrice(rarity);
            RectTransform crt = ShopChrome.Card(grid, "HeroCard_" + rname, ShopChrome.FrameColor.Gold);
            PlaceCell(crt, y, col, 2, HeroCardH);
            // the tier's own plate: tint the card's border and fill (children 0 and 1 of the shared card builder)
            crt.GetChild(0).GetComponent<Image>().color = HeroEdgeColor(rarity);
            crt.GetChild(1).GetComponent<Image>().color = HeroPlateColor(rarity);
            crt.gameObject.AddComponent<A11yLabel>().Text = rname + " hero card, " + Num(price) + " coins" + (progress.Coins >= price ? "" : ", not enough coins");

            Image strip = ToyKit.Sliced(crt, "Strip", Theme.RoundSprite, 26f);
            strip.color = HeroStripBrown;
            strip.rectTransform.anchorMin = new Vector2(0f, 0f);
            strip.rectTransform.anchorMax = new Vector2(1f, 0f);
            strip.rectTransform.pivot = new Vector2(0.5f, 0f);
            strip.rectTransform.offsetMin = new Vector2(9f, 9f);
            strip.rectTransform.offsetMax = new Vector2(-9f, 9f + HeroStripH);

            BundlesScreen.AddHeroCardIcon(crt, new Vector2(0.5f, 1f), new Vector2(0f, -12f), HeroIconSize);   // the "?" hero card, icon and mark only

            AddInfoButton(crt, rname + " heroes", () => TapHeroInfo(rarity));

            Text name = UiKit.AddText(crt, "Rarity", rname, 44, BundlesScreen.RarityInk(rarity), TextAnchor.MiddleCenter, true);
            DropOutline(name);
            name.fontStyle = FontStyle.Bold;
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            float top = 12f + HeroIconSize + 10f;
            name.rectTransform.anchorMin = new Vector2(0f, 1f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.pivot = new Vector2(0.5f, 1f);
            name.rectTransform.offsetMin = new Vector2(6f, -(top + 60f + TextK * 100f));
            name.rectTransform.offsetMax = new Vector2(-6f, -top);

            bool afford = progress.Coins >= price;
            Button buy = BottomButton(crt, "BuyHero", 1, art != null ? art.CoinIcon : null, Num(price), "Buy a random " + rname + " hero for " + Num(price) + " coins" + (afford ? "" : ", not enough coins"), () => TapHeroBuy(rarity), 20f, 22f);
            buy.name = "BuyHero";
            DimIf(buy, !afford);   // too few coins: dimmed; a tap still explains it and offers the coin packs
        }

        // ---------------- the (i) popup ----------------

        /// <summary>The (i) of a tier: its properties from the tier rules, and how many heroes of this tier the player's open regions offer now.</summary>
        public void TapHeroInfo(CharacterRarity rarity)
        {
            string rname = HeroPurchase.RarityName(rarity);
            int now = progress.HeroBuyPool(rarity).Count;
            var lines = HeroPurchase.InfoLines(rarity);
            string region = "Only heroes from your open regions are offered.";
            string avail = "Offered now: " + now + (now == 1 ? " hero" : " heroes") + ".";
            if (host.NewOverlay == null)
            {
                host.OpenPopup(rname + " heroes", string.Join("\n", lines.ToArray()) + "\n" + region + "\n" + avail, 1180f);
                host.PopupButton("OK", false, 60f, host.ClosePopup);
                return;
            }
            RectTransform overlay = host.NewOverlay();
            const float panelW = 860f, W = 740f, gap = 14f;
            float y = 228f;   // below the popup title box (BuildPopupFrame: 130 px tall at -52, grows with 130 percent text); the bundle info starts at 215
            float iconY = y;
            y += 140f + gap;
            float[] h = new float[lines.Count];
            float linesH = 0f;
            for (int i = 0; i < lines.Count; i++) { h[i] = BundlesScreen.TextHeight(lines[i], 40f, W); linesH += h[i] + gap; }
            float linesY = y;
            y += linesH;
            float regionH = BundlesScreen.TextHeight(region, 40f, W);
            float regionY = y;
            y += regionH + gap;
            float availH = BundlesScreen.TextHeight(avail, 40f, W);
            float availY = y;
            y += availH + gap;
            float buttonReserve = 50f + Mathf.Max(170f, UiKit.MinTouchRef) + gap;
            float panelH = Mathf.Max(1000f, y + buttonReserve);
            RectTransform panel = UiKit.BuildPopupFrame(overlay, rname + " heroes", art, new Vector2(panelW, panelH), host.CloseOverlay);
            panel.name = "HeroTierInfo";
            BundlesScreen.AddHeroCardIcon(panel, new Vector2(0.5f, 1f), new Vector2(0f, -iconY), 140f);
            float cy = linesY;
            for (int i = 0; i < lines.Count; i++)
            {
                HeroInfoText(panel, "Line" + i, lines[i], cy, h[i], W, false, UiKit.PanelInk);
                cy += h[i] + gap;
            }
            HeroInfoText(panel, "Regions", region, regionY, regionH, W, false, UiKit.PanelInk);
            HeroInfoText(panel, "Offered", avail, availY, availH, W, true, BundlesScreen.RarityInk(rarity));
            ToyKit.PopupAction(panel, "OK", ToyKit.Button(1), Color.white, 50f, host.CloseOverlay);
        }

        static Text HeroInfoText(RectTransform panel, string name, string text, float y, float h, float w, bool bold, Color color)
        {
            Text t = UiKit.AddText(panel, name, text, 40, color, TextAnchor.MiddleCenter);
            UiKit.OnPanel(t);
            t.color = color;
            if (bold) t.fontStyle = FontStyle.Bold;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.resizeTextForBestFit = true;   // shrinks (never below 16 sp) rather than leaving its measured box
            t.resizeTextMinSize = Mathf.RoundToInt(UiKit.MinBodyTextPx);
            t.resizeTextMaxSize = t.fontSize;
            BundlesScreen.PlaceTop((RectTransform)t.transform, y, w, h);
            return t;
        }

        // ---------------- buying ----------------

        /// <summary>Tap on the green button of a tier: no hero in an open region says so (nothing charged); too few coins explains it and offers the coin packs; otherwise asks to confirm.</summary>
        public void TapHeroBuy(CharacterRarity rarity)
        {
            string rname = HeroPurchase.RarityName(rarity);
            int price = HeroPurchase.CoinPrice(rarity);
            if (price <= 0) return;
            if (progress.HeroBuyPool(rarity).Count == 0)
            {
                ShowNoHeroes(rarity);
                return;
            }
            if (progress.Coins < price)
            {
                ShowNotEnoughCoins(price);
                return;
            }
            host.OpenPopup("Confirm purchase", "Buy a " + rname + " hero for " + Num(price) + " coins?\nYou get a random hero of this rarity from your open regions.\nYou have " + Num(progress.Coins) + " coins.", 1180f);
            host.PopupButton("Buy for " + Num(price) + " coins", true, 230f, () => CompleteHeroBuy(rarity));
            host.PopupButton("Cancel", false, 60f, host.ClosePopup);
        }

        void CompleteHeroBuy(CharacterRarity rarity)
        {
            host.ClosePopup();
            CharacterSpec hero;
            HeroBuyResult result = progress.TryBuyHero(rarity, out hero);
            switch (result)
            {
                case HeroBuyResult.Ok:
                    AudioManager.Play(Sfx.PowerUp);
                    RebuildKeepScroll();   // the wallet follows
                    ShowHeroReveal(hero, HeroPurchase.CoinPrice(rarity));
                    break;
                case HeroBuyResult.NoHeroes:
                    ShowNoHeroes(rarity);
                    break;
                case HeroBuyResult.NotEnoughCoins:
                    Info("Not enough coins", "You do not have enough coins for a " + HeroPurchase.RarityName(rarity) + " hero.");
                    break;
                default:
                    Info("Not available", "This hero card cannot be bought right now.");
                    break;
            }
        }

        /// <summary>"No Rare heroes yet": the hero card icon, one short line and OK. Nothing is charged.</summary>
        void ShowNoHeroes(CharacterRarity rarity)
        {
            string rname = HeroPurchase.RarityName(rarity);
            string line = "No " + rname + " heroes yet";
            if (host.NewOverlay == null)
            {
                Info("Not yet", line);
                return;
            }
            RectTransform overlay = host.NewOverlay();
            float lineH = BundlesScreen.TextHeight(line, 48f, 740f);
            float buttonReserve = 50f + Mathf.Max(170f, UiKit.MinTouchRef) + 14f;
            float panelH = Mathf.Max(760f, 200f + 170f + 14f + lineH + buttonReserve);
            RectTransform panel = UiKit.BuildPopupFrame(overlay, "Not yet", art, new Vector2(860f, panelH), host.CloseOverlay);
            panel.name = "NoHeroesYet";
            BundlesScreen.AddHeroCardIcon(panel, new Vector2(0.5f, 1f), new Vector2(0f, -200f), 170f);
            Text t = HeroInfoText(panel, "Line", line, 200f + 170f + 14f, lineH, 740f, true, BundlesScreen.RarityInk(rarity));
            t.fontSize = Mathf.RoundToInt(Mathf.Max(48f, UiKit.MinBodyTextPx) * UiKit.TextScale);
            t.resizeTextMaxSize = t.fontSize;
            panel.gameObject.AddComponent<A11yLabel>().Text = line;
            ToyKit.PopupAction(panel, "OK", ToyKit.Button(1), Color.white, 50f, host.CloseOverlay);
        }

        /// <summary>The same reveal as a bundle's hidden card: the hero, "You got: name (rarity)" in the tier colour, OK. The copy is already saved.</summary>
        void ShowHeroReveal(CharacterSpec hero, int paid)
        {
            if (hero == null) return;
            string rarity = BundleCards.RarityName(hero.Rarity);
            string plain = "You got: " + hero.Name + " (" + rarity + ")";
            if (host.NewOverlay == null)
            {
                Info("Hero bought!", plain);
                return;
            }
            const float dollH = 480f, dollTop = 190f, gap = 12f, panelW = 860f;
            float nameY = dollTop + dollH + gap, nameH = BundlesScreen.TextHeight(plain, 50f, 780f);
            float paidY = nameY + nameH + gap, paidH = BundlesScreen.TextHeight("Paid 30,000 coins", 40f, 780f);
            float buttonReserve = 50f + Mathf.Max(170f, UiKit.MinTouchRef) + gap;
            float panelH = Mathf.Max(1300f, paidY + paidH + buttonReserve);
            RectTransform overlay = host.NewOverlay();
            RectTransform panel = UiKit.BuildPopupFrame(overlay, "You got!", art, new Vector2(panelW, panelH), host.CloseOverlay);
            panel.name = "HeroReveal";
            CharacterDef who = CharacterCatalog.Get(hero.Id);
            if (who != null)
            {
                RectTransform doll = MomoDoll.Build(panel, "Hero", dollH, who, art);
                BundlesScreen.PlaceTop(doll, dollTop, doll.sizeDelta.x, dollH);
            }
            string rich = "You got: " + hero.Name + " (<color=#" + ColorUtility.ToHtmlStringRGB(BundlesScreen.RarityInk(hero.Rarity)) + ">" + rarity + "</color>)";
            Text nm = UiKit.AddText(panel, "HeroName", rich, 50, UiKit.PanelInk, TextAnchor.MiddleCenter, true);
            UiKit.OnPanel(nm);
            nm.fontStyle = FontStyle.Bold;
            nm.horizontalOverflow = HorizontalWrapMode.Wrap;
            BundlesScreen.PlaceTop((RectTransform)nm.transform, nameY, 780f, nameH);
            HeroInfoText(panel, "Paid", "Paid " + Num(paid) + " coins", paidY, paidH, 780f, false, UiKit.PanelInk);
            ToyKit.PopupAction(panel, "OK", ToyKit.Button(1), Color.white, 50f, host.CloseOverlay);
        }
    }
}
