using System;
using System.Collections.Generic;
using System.Globalization;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The Treasure Shop page (docs/22 point 3, docs/26 section 1; reworked by Bug List 5 F3 to match the Bundles: coins + power-ups + exactly one hero card, a "Worth x.." tag and an "i" popup).
    /// It is the "Chests" section of the shop's VIP page, built inside <see cref="ShopChrome"/>: two cards, IRON CHEST (blue) and GOLD CHEST (gold). Each card has a top row with the 48 dp "i"
    /// button, the chest name and the italic "Worth x1.8" tag (top right, no star, the name's font); under it the chest picture on the left and, on the right, icon chips (coins range,
    /// power-ups, the hero card as the "?" hero icon) above the buttons Open 1 (green) and Open 10 (yellow) with the gem price. Opening asks first (every price is above 50 gems), plays a
    /// reveal of about 1.2 s that a tap skips (reduced motion shows the result at once) and ends on a Collect button. The reveal is compact and icon-first: small hero rows, icon chips.
    /// Too few gems offers the smallest gem pack that covers the shortfall. Gems only. Menu screen only: no per-frame code except the reveal animation.
    /// </summary>
    public sealed class TreasureChestScreen
    {
        /// <summary>What the page needs from its host: the dim overlay layer and the way to the gem pack purchase. It never owns a canvas.</summary>
        public sealed class Host
        {
            public Func<RectTransform> NewOverlay;
            public Action CloseOverlay;
            /// <summary>Opens the shop's gem purchase flow for the pack at this index of the shop catalog.</summary>
            public Action<int> BuyGemPack;
        }

        // Cards: the top row (info + name + worth) is 48 dp or the name line, the right column holds two chip rows above the open buttons. At 130 percent the page scrolls.
        const float CardBaseH = 470f, RowGap = 14f;
        const float BuyBottom = 14f, TextTop = 14f;
        const float ChipH = 72f, ChipGap = 8f, WorthW = 330f;
        static float TextK { get { return Mathf.Max(1f, UiKit.TextScale) - 1f; } }
        static float BuyH { get { return 140f + TextK * 40f; } }
        static float TitleH { get { return 60f + TextK * 220f; } }
        /// <summary>Height of the card's top row: the 48 dp "i" or the (possibly wrapped) name, whichever is taller.</summary>
        static float TopRowH { get { return Mathf.Max(UiKit.MinTouchPx, TitleH); } }
        const float HeroRowMin = 130f;
        const int MaxHeroRows = 4;

        static readonly Color ChipFrame = new Color(0.42f, 0.24f, 0.10f, 1f);
        static readonly Color ChipFill = new Color(0.99f, 0.95f, 0.84f, 1f);

        readonly RectTransform screen;
        readonly ProgressService progress;
        readonly ShopCatalog catalog;
        readonly ArtCatalog art;
        readonly Host host;
        float cardH = CardBaseH;

        /// <summary>The reveal of the last opening (tests skip its animation through it); null when none is showing.</summary>
        public ChestRevealAnim Reveal { get; private set; }

        public TreasureChestScreen(RectTransform screen, ProgressService progress, ShopCatalog catalog, ArtCatalog art, Host host)
        {
            this.screen = screen;
            this.progress = progress;
            this.catalog = catalog;
            this.art = art;
            this.host = host;
        }

        static string Num(int n) { return n.ToString("N0", CultureInfo.InvariantCulture); }

        public static string ChestName(ChestKind kind) { return kind == ChestKind.Iron ? "Iron Chest" : "Gold Chest"; }

        static readonly Dictionary<ChestKind, Sprite> chestSprites = new Dictionary<ChestKind, Sprite>();

        /// <summary>The chest picture (Resources/Art/Chests); null when the file is missing (callers draw a plain fallback colour).</summary>
        public static Sprite ChestArt(ChestKind kind)
        {
            Sprite s;
            if (chestSprites.TryGetValue(kind, out s) && s != null) return s;
            s = Resources.Load<Sprite>(kind == ChestKind.Iron ? "Art/Chests/chest_iron" : "Art/Chests/chest_golden");
            chestSprites[kind] = s;
            return s;
        }

        // ---------------- the text and chips of a chest ----------------

        /// <summary>The coin range as a chip shows it: "800-1,200".</summary>
        public static string CoinRangeText(ChestKind kind)
        {
            return Num(ChestRolls.MinCoins(kind)) + "-" + Num(ChestRolls.MaxCoins(kind));
        }

        /// <summary>What a chest gives, as plain text (accessibility label and tests): "800 to 1,200 coins, 1 power-up, 1 hero card".</summary>
        public static string DescriptionText(ChestKind kind)
        {
            int pu = ChestRolls.PowerUpCount(kind);
            return Num(ChestRolls.MinCoins(kind)) + " to " + Num(ChestRolls.MaxCoins(kind)) + " coins, " + pu + (pu == 1 ? " power-up" : " power-ups") + ", 1 hero card";
        }

        /// <summary>The worth tag of a chest, for example "Worth x1.8" (see <see cref="ChestRolls.Worth"/>).</summary>
        public static string WorthText(ChestKind kind) { return ChestRolls.WorthLabel(kind); }

        /// <summary>
        /// One content chip (cream plate in a brown frame, like the bundle chips): an icon and a short text, or only the "?" hero card icon when <paramref name="hero"/>.
        /// Placed from the top-left of <paramref name="parent"/> at (x, y); <paramref name="stretch"/> makes it span the parent from x to the right edge.
        /// </summary>
        public static RectTransform AddChip(RectTransform parent, string name, float x, float y, float w, float h, Sprite icon, bool hero, string text, bool stretch = false)
        {
            RectTransform root = UiKit.Rect(parent, name);
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(stretch ? 1f : 0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(x, -y);
            root.sizeDelta = new Vector2(stretch ? -x : w, h);
            Image frame = ToyKit.Sliced(root, "Frame", Theme.RoundSprite, 22f);
            frame.color = ChipFrame;
            UiKit.Stretch(frame.rectTransform);
            Image fill = ToyKit.Sliced(root, "Fill", Theme.RoundSprite, 18f);
            fill.color = ChipFill;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = new Vector2(4f, 4f);
            fill.rectTransform.offsetMax = new Vector2(-4f, -4f);
            if (hero)
            {
                BundlesScreen.AddHeroCardIcon(root, new Vector2(0.5f, 0.5f), Vector2.zero, Mathf.Min(h - 12f, 60f));   // icon and "?" only
                return root;
            }
            float ic = Mathf.Min(h - 20f, 52f);
            Image img = UiKit.AddImage(root, "Icon", icon, Color.white);
            UiKit.Place(img.rectTransform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(ic, ic));
            Text count = UiKit.AddText(root, "Count", text, 42, UiKit.PanelInk, TextAnchor.MiddleCenter, true);
            UiKit.OnPanel(count);
            count.horizontalOverflow = HorizontalWrapMode.Wrap;
            count.resizeTextForBestFit = true;   // shrinks (never below 16 sp) instead of leaving the chip
            count.resizeTextMinSize = Mathf.RoundToInt(UiKit.MinBodyTextPx);
            count.resizeTextMaxSize = count.fontSize;
            var crt = (RectTransform)count.transform;
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = new Vector2(ic + 16f, 2f);
            crt.offsetMax = new Vector2(-8f, -2f);
            return root;
        }

        /// <summary>
        /// The three content chips of a chest in two rows: the coin range (full width of the column, or centred and at most 340 wide in a popup), then the power-up count (generic bolt icon,
        /// the type is random) and the hero card ("?" icon).
        /// </summary>
        static void AddContentChips(RectTransform parent, ChestKind kind, float puChipW, float chipH, float gap, bool centered, float width)
        {
            Sprite coin = GameSession.Art != null ? GameSession.Art.CoinIcon : null;
            Sprite bolt = ToyKit.Get("icon_bolt");
            float coinW = centered ? Mathf.Min(width, 340f) : 0f;
            float row2W = puChipW + gap + 100f;
            float x1 = 0f, x2 = 0f;
            if (centered)
            {
                x1 = (width - coinW) * 0.5f;
                x2 = (width - row2W) * 0.5f;
            }
            AddChip(parent, "CoinsChip", x1, 0f, coinW, chipH, coin, false, CoinRangeText(kind), !centered);
            AddChip(parent, "PowerUpsChip", x2, chipH + gap, puChipW, chipH, bolt, false, "x" + ChestRolls.PowerUpCount(kind));
            AddChip(parent, "HeroChip", x2 + puChipW + gap, chipH + gap, 100f, chipH, null, true, "");
        }

        /// <summary>The "i" button: a 48 dp touch target with a circled i drawn in the given colour (the chest cards are blue or gold, so the ink is chosen per card).</summary>
        static Button AddInfoButton(Transform parent, string name, Vector2 anchor, Vector2 pos, string a11y, Color ink, Action onTap)
        {
            Image hit = UiKit.AddImage(parent, name, null, new Color(0f, 0f, 0f, 0f));
            hit.raycastTarget = true;
            UiKit.Place(hit.rectTransform, anchor, pos, new Vector2(UiKit.MinTouchPx, UiKit.MinTouchPx));
            var b = hit.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { AudioManager.Play(Sfx.Tap); if (onTap != null) onTap(); });
            hit.gameObject.AddComponent<A11yLabel>().Text = a11y;
            Image ring = UiKit.AddImage(hit.transform, "Ring", Glyphs.Ring, ink);
            ring.raycastTarget = false;
            UiKit.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
            Text i = UiKit.AddText(ring.transform, "I", "i", 32, ink, TextAnchor.MiddleCenter, true);
            i.raycastTarget = false;
            i.GetComponent<Outline>().enabled = false;
            UiKit.Stretch((RectTransform)i.transform);
            return b;
        }

        // ---------------- build ----------------

        /// <summary>Builds the two chest cards into the shop's content rect (scrolls when the screen is short or the text is large).</summary>
        public void BuildPage(RectTransform content)
        {
            cardH = CardBaseH + TextK * 700f;
            RectTransform list = ShopChrome.ScrollArea(content, 2f * cardH + RowGap);
            BuildChestCard(list, ChestKind.Iron, 0f, false);
            BuildChestCard(list, ChestKind.Golden, cardH + RowGap, false);
        }

        /// <summary>
        /// The two chest cards as LAYOUT children of <paramref name="section"/> (the "Chests" section of the VIP page: a vertical layout group under the
        /// section header). No scroll area of its own: the VIP page scrolls.
        /// </summary>
        public void BuildCards(RectTransform section)
        {
            cardH = CardBaseH + TextK * 700f;
            BuildChestCard(section, ChestKind.Iron, 0f, true);
            BuildChestCard(section, ChestKind.Golden, 0f, true);
        }

        void BuildChestCard(RectTransform list, ChestKind kind, float y, bool inLayout)
        {
            bool gold = kind == ChestKind.Golden;
            RectTransform card = ShopChrome.Card(list, kind + "Card", gold ? ShopChrome.FrameColor.Gold : ShopChrome.FrameColor.Blue);
            if (inLayout)
            {
                var le = card.gameObject.AddComponent<LayoutElement>();
                le.minHeight = cardH;
                le.preferredHeight = cardH;
            }
            else
            {
                card.anchorMin = new Vector2(0f, 1f);
                card.anchorMax = new Vector2(1f, 1f);
                card.pivot = new Vector2(0.5f, 1f);
                card.anchoredPosition = new Vector2(0f, -y);
                card.sizeDelta = new Vector2(0f, cardH);
            }

            float rowH = TopRowH, rowBottom = TextTop + rowH + 6f;
            Color titleCol = gold ? new Color(0.33f, 0.14f, 0.02f) : new Color(0.82f, 0.93f, 1f);
            Color baseCol = gold ? new Color(0.20f, 0.08f, 0.02f) : Color.white;

            // top row: the "i" (top left), the name, and the italic "Worth x1.8" (top right, no star, the name's font)
            AddInfoButton(card, "ChestInfo", new Vector2(0f, 1f), new Vector2(6f, -TextTop), "About the " + ChestName(kind), baseCol, () => ShowInfo(kind));
            Text name = UiKit.AddText(card, "Name", ChestName(kind).ToUpperInvariant(), 44, titleCol, TextAnchor.MiddleLeft, true);
            if (gold) name.GetComponent<Outline>().enabled = false;
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform nrt = name.rectTransform;
            nrt.anchorMin = new Vector2(0f, 1f);
            nrt.anchorMax = new Vector2(1f, 1f);
            nrt.pivot = new Vector2(0.5f, 1f);
            nrt.offsetMin = new Vector2(UiKit.MinTouchPx + 14f, -(TextTop + rowH));
            nrt.offsetMax = new Vector2(-(WorthW + 22f), -TextTop);

            float worthH = BundlesScreen.WorthTextH(44f);
            Text worth = BundlesScreen.AddWorthText(card, WorthText(kind), new Vector2(1f, 1f), new Vector2(-14f, -(TextTop + Mathf.Max(0f, (rowH - worthH) * 0.5f))), WorthW, 44f);
            worth.color = titleCol;
            var wo = worth.GetComponent<Outline>();
            if (wo != null) wo.enabled = !gold;

            Sprite chest = ChestArt(kind);
            Image pic = UiKit.AddImage(card, "Chest", chest, chest != null ? Color.white : new Color(0.7f, 0.5f, 0.3f, 1f));
            pic.preserveAspect = true;
            pic.rectTransform.anchorMin = new Vector2(0.02f, 0f);
            pic.rectTransform.anchorMax = new Vector2(0.36f, 1f);
            pic.rectTransform.offsetMin = new Vector2(0f, 14f);
            pic.rectTransform.offsetMax = new Vector2(0f, -rowBottom);

            // right column: icon chips (coins range; power-ups; the hero card as the "?" icon) above the two buttons
            RectTransform contents = UiKit.Rect(card, "Contents");
            contents.anchorMin = new Vector2(0.38f, 0f);
            contents.anchorMax = new Vector2(0.97f, 1f);
            contents.offsetMin = new Vector2(0f, BuyBottom + BuyH + 10f);
            contents.offsetMax = new Vector2(0f, -rowBottom);
            AddContentChips(contents, kind, 180f, ChipH, ChipGap, false, 0f);
            card.gameObject.AddComponent<A11yLabel>().Text = ChestName(kind) + ". " + DescriptionText(kind) + ". " + WorthText(kind);

            BuildBuyButton(card, kind, 1, 0.38f, 0.675f, 0f, -6f);
            BuildBuyButton(card, kind, 10, 0.675f, 0.97f, 6f, 0f);
        }

        /// <summary>One open button: "Open 1" or "Open 10" on the top line, the gem icon and price below. Never disabled: too few gems explains itself on tap.</summary>
        void BuildBuyButton(RectTransform card, ChestKind kind, int opens, float x0, float x1, float padMin, float padMax)
        {
            int price = ChestRolls.Price(kind, opens);
            string chests = opens == 1 ? "1 " + ChestName(kind) : "10 " + ChestName(kind) + "s";
            Button b = UiKit.AddButton(card, "Open" + opens, "", ToyKit.Button(opens == 1 ? 1 : 0), Color.white, new Vector2(280f, BuyH), () => TapBuy(kind, opens), 40);
            b.GetComponent<A11yLabel>().Text = "Open " + chests + " for " + Num(price) + " gems";
            b.GetComponent<Image>().raycastTarget = true;
            var rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(x0, 0f);
            rt.anchorMax = new Vector2(x1, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(padMin, BuyBottom);
            rt.offsetMax = new Vector2(padMax, BuyBottom + BuyH);

            Text label = UiKit.AddText(b.transform, "Count", opens == 1 ? "Open 1" : "Open 10", 40, UiKit.ButtonInk, TextAnchor.MiddleCenter, true);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.GetComponent<Outline>().enabled = false;
            RectTransform lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0.04f, 0.52f);
            lrt.anchorMax = new Vector2(0.96f, 0.96f);
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;

            RectTransform line = UiKit.Rect(b.transform, "Price");
            line.anchorMin = new Vector2(0.04f, 0.04f);
            line.anchorMax = new Vector2(0.96f, 0.52f);
            line.offsetMin = line.offsetMax = Vector2.zero;
            var hl = line.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 6f;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            Image gem = UiKit.AddImage(line, "Gem", art != null ? art.GemIcon : null, Color.white);
            var gle = gem.gameObject.AddComponent<LayoutElement>();
            gle.preferredWidth = gle.preferredHeight = 52f;
            Text cost = UiKit.AddText(line, "Cost", Num(price), 44, UiKit.ButtonInk, TextAnchor.MiddleCenter, true);
            cost.horizontalOverflow = HorizontalWrapMode.Overflow;
            cost.GetComponent<Outline>().enabled = false;
        }

        // ---------------- the info popup ("i") ----------------

        /// <summary>
        /// The "i" popup of one chest: icon chips (coins, power-ups, the "?" hero card), a short hero line ("Only heroes of your open regions") with the chance per rarity (Iron 70/20/10,
        /// Gold 50/40/10) and "Worth x1.8 for 100 gems". Icons first, short text, all at least 16 sp.
        /// </summary>
        public void ShowInfo(ChestKind kind)
        {
            if (host.NewOverlay == null) return;
            RectTransform overlay = host.NewOverlay();
            int regions = progress.BundleOpenRegionCount();
            const float panelW = 860f, W = 780f, gap = 14f, chipsY = 215f, colW = 250f, colGap = 15f, popupChipH = 96f;
            string heroText = "Only heroes of your open regions (" + regions + ")";
            string worthLine = WorthText(kind) + " for " + Num(ChestRolls.Price(kind, 1)) + " gems";
            float chipsH = 2f * popupChipH + gap;
            float heroY = chipsY + chipsH + gap * 2f, heroH = Mathf.Max(80f, BundlesScreen.TextHeight(heroText, 40f, W - 100f));
            float chanceY = heroY + heroH + gap, chanceH = BundlesScreen.TextHeight("Uncommon\n70%", 40f, colW);
            float worthY = chanceY + chanceH + gap, worthH = BundlesScreen.WorthPillH();
            string foot = "Same hero twice? You keep both.";
            float footY = worthY + worthH + gap, footH = BundlesScreen.TextHeight(foot, 40f, W);
            float buttonReserve = 50f + Mathf.Max(170f, UiKit.MinTouchRef) + gap;
            float panelH = Mathf.Max(1100f, footY + footH + buttonReserve);
            RectTransform panel = UiKit.BuildPopupFrame(overlay, ChestName(kind), art, new Vector2(panelW, panelH), host.CloseOverlay);
            panel.name = "ChestInfoPopup";

            RectTransform chips = UiKit.Rect(panel, "Chips");
            BundlesScreen.PlaceTop(chips, chipsY, W, chipsH);
            AddContentChips(chips, kind, 180f, popupChipH, gap, true, W);

            RectTransform hero = UiKit.Rect(panel, "HeroLine");
            BundlesScreen.PlaceTop(hero, heroY, W, heroH);
            BundlesScreen.AddHeroCardIcon(hero, new Vector2(0f, 0.5f), new Vector2(4f, 0f), 72f);
            Text ht = UiKit.AddText(hero, "HeroText", heroText, 40, UiKit.PanelInk, TextAnchor.MiddleLeft);
            UiKit.OnPanel(ht);
            ht.horizontalOverflow = HorizontalWrapMode.Wrap;
            ht.resizeTextForBestFit = true;
            ht.resizeTextMinSize = Mathf.RoundToInt(UiKit.MinBodyTextPx);
            ht.resizeTextMaxSize = ht.fontSize;
            var hrt = (RectTransform)ht.transform;
            hrt.anchorMin = Vector2.zero;
            hrt.anchorMax = Vector2.one;
            hrt.offsetMin = new Vector2(92f, 0f);
            hrt.offsetMax = Vector2.zero;

            CharacterRarity[] tiers = ChestRolls.Tiers(kind);
            float x0 = (panelW - (3f * colW + 2f * colGap)) * 0.5f;
            for (int i = 0; i < tiers.Length; i++)
            {
                string label = BundleCards.RarityName(tiers[i]) + "\n" + ChestRolls.ChancePercent(kind, i) + "%";
                Text ct = UiKit.AddText(panel, "Chance_" + BundleCards.RarityName(tiers[i]), label, 40, BundlesScreen.RarityInk(tiers[i]), TextAnchor.MiddleCenter, true);
                UiKit.OnPanel(ct);
                ct.color = BundlesScreen.RarityInk(tiers[i]);
                ct.fontStyle = FontStyle.Bold;
                ct.horizontalOverflow = HorizontalWrapMode.Wrap;
                ct.resizeTextForBestFit = true;
                ct.resizeTextMinSize = Mathf.RoundToInt(UiKit.MinBodyTextPx);
                ct.resizeTextMaxSize = ct.fontSize;
                var crt = (RectTransform)ct.transform;
                crt.anchorMin = crt.anchorMax = new Vector2(0f, 1f);
                crt.pivot = new Vector2(0f, 1f);
                crt.anchoredPosition = new Vector2(x0 + i * (colW + colGap), -chanceY);
                crt.sizeDelta = new Vector2(colW, chanceH);
            }

            BundlesScreen.AddWorthPill(panel, worthLine, new Vector2(0.5f, 1f), new Vector2(0f, -worthY), W);
            Text ft = UiKit.AddText(panel, "Note", foot, 40, UiKit.PanelInk, TextAnchor.MiddleCenter);
            UiKit.OnPanel(ft);
            ft.fontStyle = FontStyle.Italic;
            ft.horizontalOverflow = HorizontalWrapMode.Wrap;
            ft.resizeTextForBestFit = true;
            ft.resizeTextMinSize = Mathf.RoundToInt(UiKit.MinBodyTextPx);
            ft.resizeTextMaxSize = ft.fontSize;
            BundlesScreen.PlaceTop((RectTransform)ft.transform, footY, W, footH);
            panel.gameObject.AddComponent<A11yLabel>().Text = ChestName(kind) + ". " + DescriptionText(kind) + ". " + heroText + ". " + worthLine;
            ToyKit.PopupAction(panel, "OK", ToyKit.Button(1), Color.white, 50f, host.CloseOverlay);
        }

        // ---------------- buying ----------------

        /// <summary>Tap on Buy or Buy 10: too few gems offers the gem packs; otherwise a confirmation (every price is above 50 gems) comes first.</summary>
        public void TapBuy(ChestKind kind, int opens)
        {
            int price = ChestRolls.Price(kind, opens);
            if (price <= 0 || host.NewOverlay == null) return;
            if (progress.Gems < price) { ShowNeedGems(price); return; }
            if (price > ShopScreen.ConfirmGemThreshold) { ShowConfirm(kind, opens, price); return; }
            OpenNow(kind, opens);
        }

        /// <summary>Pays and opens at once, without the confirmation (the confirmation calls this). Saves inside TryOpenChests.</summary>
        public void OpenNow(ChestKind kind, int opens)
        {
            int price = ChestRolls.Price(kind, opens);
            if (price <= 0 || host.NewOverlay == null) return;
            if (progress.Gems < price) { ShowNeedGems(price); return; }
            List<ChestContents> contents;
            ChestOpenResult res = progress.TryOpenChests(kind, opens, out contents);
            if (res == ChestOpenResult.Ok) ShowReveal(kind, opens, contents);
            else if (res == ChestOpenResult.NotEnoughGems) ShowNeedGems(price);
        }

        void ShowConfirm(ChestKind kind, int opens, int price)
        {
            RectTransform overlay = host.NewOverlay();
            RectTransform panel = UiKit.BuildPopupFrame(overlay, "Open chests?", art, new Vector2(860f, 1100f), host.CloseOverlay);
            string body = "Open " + (opens == 1 ? "1 " + ChestName(kind) : "10 " + ChestName(kind) + "s") + " for " + Num(price) + " gems?\n"
                + WorthText(kind) + "\n" + "Includes 1 hero card (?)" + "\n"
                + "You have " + Num(progress.Gems) + " gems.";
            Text t = UiKit.AddText(panel, "Body", body, 44, Theme.Ink);
            UiKit.OnPanel(t);
            UiKit.Place((RectTransform)t.transform, new Vector2(0.5f, 1f), new Vector2(0f, -214f), new Vector2(740f, 520f));
            Button go = UiKit.AddButton(panel, "ConfirmOpen", "Open for " + Num(price) + " gems", ToyKit.Button(1), Color.white, new Vector2(760f, 130f), () => { host.CloseOverlay(); OpenNow(kind, opens); }, 44);
            UiKit.Place((RectTransform)go.transform, new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(760f, 130f));
            ToyKit.PopupAction(panel, "Cancel", null, Color.white, 60f, host.CloseOverlay);
        }

        /// <summary>Smallest gem pack that covers the shortfall; the largest one when none does; -1 without packs.</summary>
        public int PackIndexFor(int shortfall)
        {
            int best = -1, largest = -1;
            for (int i = 0; i < catalog.GemPacks.Count; i++)
            {
                int g = catalog.GemPacks[i].Gems;
                if (largest < 0 || g > catalog.GemPacks[largest].Gems) largest = i;
                if (g >= shortfall && (best < 0 || g < catalog.GemPacks[best].Gems)) best = i;
            }
            return best >= 0 ? best : largest;
        }

        void ShowNeedGems(int price)
        {
            RectTransform overlay = host.NewOverlay();
            RectTransform panel = UiKit.BuildPopupFrame(overlay, "Need more gems", art, new Vector2(860f, 1180f), host.CloseOverlay);
            int shortfall = Mathf.Max(1, price - progress.Gems);
            Text t = UiKit.AddText(panel, "Body", "You need " + Num(price) + " gems and have " + Num(progress.Gems) + ".\nYou are " + Num(shortfall) + " short.", 44, Theme.Ink);
            UiKit.OnPanel(t);
            UiKit.Place((RectTransform)t.transform, new Vector2(0.5f, 1f), new Vector2(0f, -214f), new Vector2(740f, 520f));
            int idx = PackIndexFor(shortfall);
            if (idx >= 0)
            {
                GemPackDef pack = catalog.GemPacks[idx];
                string label = "Get " + Num(pack.Gems) + " gems (" + ShopChrome.PriceText(ShopCatalog.GemPackUsd(pack, progress.LaunchPromoActive)) + ")";
                Button buy = UiKit.AddButton(panel, "GetGems", label, ToyKit.Button(1), Color.white, new Vector2(760f, 130f), () => { if (host.BuyGemPack != null) host.BuyGemPack(idx); }, 44);
                UiKit.Place((RectTransform)buy.transform, new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(760f, 130f));
            }
            ToyKit.PopupAction(panel, "Close", null, Color.white, 60f, host.CloseOverlay);
        }

        // ---------------- reveal ----------------

        void CloseReveal()
        {
            Reveal = null;
            host.CloseOverlay();
        }

        /// <summary>
        /// The compact, icon-first reveal: a small chest (tap skips the animation), icon chips for the coins and each power-up with its count, then one small row per hero won
        /// (portrait, name, rarity word in its colour, "x2" when the same hero came more than once; a scrolling list after four rows) and Collect. No sentences.
        /// </summary>
        void ShowReveal(ChestKind kind, int opens, List<ChestContents> contents)
        {
            RectTransform overlay = host.NewOverlay();
            int coins = 0;
            var counts = new Dictionary<string, int>();
            var heroes = new List<CharacterSpec>();
            var heroCount = new Dictionary<string, int>();
            foreach (ChestContents c in contents)
            {
                coins += c.Coins;
                foreach (string id in c.PowerUps)
                {
                    int have;
                    counts.TryGetValue(id, out have);
                    counts[id] = have + 1;
                }
                if (c.Hero != null)
                {
                    int n;
                    heroCount.TryGetValue(c.Hero.Id, out n);
                    if (n == 0) heroes.Add(c.Hero);
                    heroCount[c.Hero.Id] = n + 1;
                }
            }
            var types = new List<PowerUpDef>();
            foreach (PowerUpDef d in progress.PowerUpDefs.Implemented) if (counts.ContainsKey(d.Id)) types.Add(d);

            const float chestY = 250f, chestH = 150f, chipH = 96f, chipGap = 10f, listW = 820f, coinW = 300f, puW = 150f;
            float heroRowH = Mathf.Max(HeroRowMin, Mathf.Ceil(2f * 46f * Mathf.Max(1f, UiKit.TextScale) * 1.2f) + 24f);
            // chips: the coin chip, then one chip per power-up type, wrapped into rows of the list width and centred
            var widths = new List<float>(types.Count + 1) { coinW };
            foreach (PowerUpDef d in types) widths.Add(puW);
            var rowOf = new int[widths.Count];
            var rowW = new List<float>();
            float cur = 0f;
            int rows = 1;
            for (int i = 0; i < widths.Count; i++)
            {
                if (cur > 0f && cur + chipGap + widths[i] > listW) { rowW.Add(cur); rows++; cur = 0f; }
                cur += (cur > 0f ? chipGap : 0f) + widths[i];
                rowOf[i] = rows - 1;
            }
            rowW.Add(cur);
            float chipsY = chestY + chestH + 14f, chipsH = rows * chipH + (rows - 1) * chipGap;
            float heroesY = chipsY + chipsH + 16f;
            float listH = Mathf.Min(heroes.Count, MaxHeroRows) * heroRowH;
            float panelH = heroesY + listH + (listH > 0f ? 20f : 0f) + 130f + 40f;
            string title = opens == 1 ? ChestName(kind) : "10 " + ChestName(kind) + "s";
            RectTransform panel = UiKit.BuildPopupFrame(overlay, title, art, new Vector2(900f, panelH), null);
            panel.name = "ChestReveal";
            var anim = overlay.gameObject.AddComponent<ChestRevealAnim>();
            Reveal = anim;

            // the chest, shaking, then bursting open; a tap on it skips the animation
            Sprite chest = ChestArt(kind);
            Image pic = UiKit.AddImage(panel, "Chest", chest, chest != null ? Color.white : new Color(0.7f, 0.5f, 0.3f, 1f));
            UiKit.Place(pic.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -chestY), new Vector2(210f, chestH));
            pic.rectTransform.pivot = new Vector2(0.5f, 1f);
            pic.raycastTarget = true;
            var skip = pic.gameObject.AddComponent<Button>();
            skip.transition = Selectable.Transition.None;
            skip.onClick.AddListener(() => { if (!anim.Done) anim.Skip(); });
            pic.gameObject.AddComponent<A11yLabel>().Text = "Skip the opening animation";
            anim.Chest = pic.rectTransform;
            anim.ChestImage = pic;

            // coin and power-up chips (icon and count only)
            RectTransform chipArea = UiKit.Rect(panel, "Chips");
            BundlesScreen.PlaceTop(chipArea, chipsY, listW, chipsH);
            var xs = new float[rows];
            for (int r = 0; r < rows; r++) xs[r] = (listW - rowW[r]) * 0.5f;
            for (int i = 0; i < widths.Count; i++)
            {
                int r = rowOf[i];
                RectTransform chip;
                if (i == 0)
                {
                    chip = AddChip(chipArea, "CoinsChip", xs[r], r * (chipH + chipGap), coinW, chipH, art != null ? art.CoinIcon : null, false, "+" + BundlesScreen.Compact(coins));
                    chip.gameObject.AddComponent<A11yLabel>().Text = Num(coins) + " coins";
                }
                else
                {
                    PowerUpDef d = types[i - 1];
                    chip = AddPowerChip(chipArea, xs[r], r * (chipH + chipGap), puW, chipH, d, counts[d.Id]);
                    chip.gameObject.AddComponent<A11yLabel>().Text = counts[d.Id] + " " + d.Name;
                }
                xs[r] += widths[i] + chipGap;
                anim.Items.Add(chip);
            }

            // heroes won: one small row each (a scrolling list after four)
            if (heroes.Count > 0)
            {
                RectTransform vp = UiKit.Rect(panel, "Heroes");
                BundlesScreen.PlaceTop(vp, heroesY, listW, listH);
                vp.gameObject.AddComponent<RectMask2D>();
                var vhit = vp.gameObject.AddComponent<Image>();
                vhit.color = new Color(0f, 0f, 0f, 0f);
                RectTransform content = UiKit.Rect(vp, "Content");
                content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                content.anchoredPosition = Vector2.zero;
                content.sizeDelta = new Vector2(listW, heroes.Count * heroRowH);
                var sr = vp.gameObject.AddComponent<ScrollRect>();
                sr.content = content;
                sr.viewport = vp;
                sr.horizontal = false;
                sr.vertical = true;
                sr.movementType = ScrollRect.MovementType.Clamped;
                for (int i = 0; i < heroes.Count; i++)
                {
                    RectTransform row = AddHeroRow(content, i, heroRowH, heroes[i], heroCount[heroes[i].Id]);
                    anim.Items.Add(row);
                }
            }

            // Collect: the first tap during the animation only skips it
            Button done = UiKit.AddButton(panel, "Collect", opens == 1 ? "Collect" : "Collect all", ToyKit.Button(1), Color.white, new Vector2(560f, 130f), () =>
            {
                if (!anim.Done) anim.Skip();
                else CloseReveal();
            }, 46);
            UiKit.Place((RectTransform)done.transform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(560f, 130f));
            anim.Begin(GameSettings.ReducedMotion);
        }

        /// <summary>A power-up chip: its round icon and "x3".</summary>
        static RectTransform AddPowerChip(RectTransform parent, float x, float y, float w, float h, PowerUpDef d, int count)
        {
            RectTransform chip = AddChip(parent, "PowerChip_" + d.Id, x, y, w, h, null, false, "x" + count);
            Transform old = chip.Find("Icon");
            if (old != null)
            {
                old.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(old.gameObject);
            }
            PowerUpUi.AddIcon(chip, "PowerIcon", d.Type, new Vector2(0f, 0.5f), new Vector2(10f, 0f), Mathf.Min(h - 20f, 52f), false);
            return chip;
        }

        /// <summary>One hero row of the reveal: the portrait, the name with the rarity word in its colour, and "x2" when the same hero came more than once.</summary>
        RectTransform AddHeroRow(RectTransform content, int index, float rowH, CharacterSpec spec, int count)
        {
            RectTransform r = UiKit.Rect(content, "Hero" + index);
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(0f, rowH - 8f);
            r.anchoredPosition = new Vector2(0f, -(index + 0.5f) * rowH);
            Image frame = ToyKit.Sliced(r, "Frame", Theme.RoundSprite, 22f);
            frame.color = ChipFrame;
            UiKit.Stretch(frame.rectTransform);
            Image fill = ToyKit.Sliced(r, "Fill", Theme.RoundSprite, 18f);
            fill.color = ChipFill;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = new Vector2(4f, 4f);
            fill.rectTransform.offsetMax = new Vector2(-4f, -4f);
            float textLeft = 24f;
            CharacterDef who = CharacterCatalog.Get(spec.Id);
            float dollH = rowH - 24f;
            if (who != null)
            {
                RectTransform doll = MomoDoll.Build(r, "Portrait", dollH, who, art);
                UiKit.Place(doll, new Vector2(0f, 0.5f), new Vector2(14f, 0f), doll.sizeDelta);
                textLeft = 14f + doll.sizeDelta.x + 14f;
            }
            string rarity = BundleCards.RarityName(spec.Rarity);
            string rich = spec.Name + "\n<color=#" + ColorUtility.ToHtmlStringRGB(BundlesScreen.RarityInk(spec.Rarity)) + ">" + rarity + "</color>";
            Text nm = UiKit.AddText(r, "Name", rich, 46, UiKit.PanelInk, TextAnchor.MiddleLeft, true);
            UiKit.OnPanel(nm);
            nm.fontStyle = FontStyle.Bold;
            nm.horizontalOverflow = HorizontalWrapMode.Wrap;
            nm.resizeTextForBestFit = true;
            nm.resizeTextMinSize = Mathf.RoundToInt(UiKit.MinBodyTextPx);
            nm.resizeTextMaxSize = nm.fontSize;
            RectTransform nrt = nm.rectTransform;
            nrt.anchorMin = Vector2.zero;
            nrt.anchorMax = Vector2.one;
            nrt.offsetMin = new Vector2(textLeft, 6f);
            nrt.offsetMax = new Vector2(count > 1 ? -150f : -20f, -6f);
            if (count > 1)
            {
                Text ct = UiKit.AddText(r, "Count", "x" + count, 52, UiKit.PanelInk, TextAnchor.MiddleRight, true);
                UiKit.OnPanel(ct);
                ct.horizontalOverflow = HorizontalWrapMode.Overflow;
                RectTransform crt = ct.rectTransform;
                crt.anchorMin = new Vector2(1f, 0f);
                crt.anchorMax = new Vector2(1f, 1f);
                crt.pivot = new Vector2(1f, 0.5f);
                crt.sizeDelta = new Vector2(130f, 0f);
                crt.anchoredPosition = new Vector2(-20f, 0f);
            }
            r.gameObject.AddComponent<A11yLabel>().Text = spec.Name + ", " + rarity + (count > 1 ? ", " + count + " cards" : "");
            return r;
        }
    }

    /// <summary>
    /// The chest reveal timeline (menu only, about 1.2 s): the chest shakes harder for 0.6 s, bursts (a quick scale-down and fade, once, no flashing), then the
    /// pieces pop in one after another. With reduced motion, or after a tap, it jumps to the end state. Runs only while the reveal overlay exists.
    /// </summary>
    public sealed class ChestRevealAnim : MonoBehaviour
    {
        public RectTransform Chest;
        public Image ChestImage;
        public readonly List<RectTransform> Items = new List<RectTransform>();
        public bool Done { get; private set; }

        const float ShakeEnd = 0.6f, BurstEnd = 0.85f, PopStart = 0.7f, PopStep = 0.08f, PopTime = 0.25f;
        float t;
        bool running;

        public void Begin(bool reducedMotion)
        {
            if (reducedMotion) { Skip(); return; }
            t = 0f;
            running = true;
            foreach (RectTransform r in Items) r.localScale = Vector3.zero;
        }

        public void Skip()
        {
            running = false;
            Done = true;
            if (Chest != null) { Chest.localRotation = Quaternion.identity; Chest.localScale = Vector3.one * 0.85f; }
            if (ChestImage != null) ChestImage.color = new Color(1f, 1f, 1f, 0.45f);
            foreach (RectTransform r in Items) if (r != null) r.localScale = Vector3.one;
        }

        void Update()
        {
            if (!running) return;
            t += Time.unscaledDeltaTime;
            if (Chest != null)
            {
                if (t < ShakeEnd)
                {
                    float k = t / ShakeEnd;
                    Chest.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 46f) * (3f + 12f * k));
                    Chest.localScale = Vector3.one * (1f + 0.08f * k);
                }
                else
                {
                    float k = Mathf.Clamp01((t - ShakeEnd) / (BurstEnd - ShakeEnd));
                    Chest.localRotation = Quaternion.identity;
                    Chest.localScale = Vector3.one * Mathf.Lerp(1.08f, 0.85f, k);
                    if (ChestImage != null) ChestImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0.45f, k));
                }
            }
            for (int i = 0; i < Items.Count; i++)
            {
                RectTransform r = Items[i];
                if (r == null) continue;
                float k = Mathf.Clamp01((t - PopStart - i * PopStep) / PopTime);
                float s = k <= 0f ? 0f : 1f + 2.70158f * Mathf.Pow(k - 1f, 3f) + 1.70158f * Mathf.Pow(k - 1f, 2f);   // ease out with a small overshoot
                r.localScale = Vector3.one * s;
            }
            if (t > PopStart + Items.Count * PopStep + PopTime) Skip();
        }
    }
}
