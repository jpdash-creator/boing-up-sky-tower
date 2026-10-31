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
    /// The Treasure Shop page (docs/22 point 3, restyled by docs/26 section 1 like reference SS13). It is a PAGE of the shop (the Chests tab of
    /// <see cref="ShopScreen"/>), built inside <see cref="ShopChrome"/>: a red CHEST ribbon and two cards, IRON CHEST (blue) and GOLD CHEST (gold), each with
    /// the chest picture on the left, the title top right, DIRECT coloured text of what is inside (no (i) button) and the buttons Open 1 (green) and
    /// Open 10 (yellow) with the gem price at the bottom right. Opening asks first (every price is above 50 gems), plays a reveal of about 1.2 s that a tap
    /// skips (reduced motion shows the result at once) and ends on a Collect button. Too few gems offers the smallest gem pack that covers the shortfall.
    /// Gems only; power-ups and coins only. Menu screen only: no per-frame code except the reveal animation.
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

        // Compact cards (owner issue list 27): both fit one screen at 100% text on every aspect; at 130% the page scrolls.
        const float CardBaseH = 424f, RowGap = 14f;
        const float BuyBottom = 14f, TextTop = 14f;
        static float TextK { get { return Mathf.Max(1f, UiKit.TextScale) - 1f; } }
        static float BuyH { get { return 140f + TextK * 40f; } }
        static float TitleH { get { return 60f + TextK * 220f; } }
        const float ListRowH = 100f;
        const int MaxListRows = 6;

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

        // ---------------- the direct text of a chest ----------------

        static readonly string[] RarityNames = { "Common", "Uncommon", "Rare", "Epic", "Legendary" };

        /// <summary>Rarity colours (Common grey, Uncommon green, Rare blue, Epic purple, Legendary gold); darker shades on the gold card so the words stay readable.</summary>
        static Color RarityColor(int rarity, bool onGold)
        {
            switch (rarity)
            {
                case 0: return onGold ? new Color(0.30f, 0.30f, 0.34f) : new Color(0.80f, 0.82f, 0.86f);
                case 1: return onGold ? new Color(0.02f, 0.36f, 0.12f) : new Color(0.45f, 0.98f, 0.50f);
                case 2: return onGold ? new Color(0.05f, 0.25f, 0.75f) : new Color(0.55f, 0.78f, 1f);
                case 3: return onGold ? new Color(0.45f, 0.10f, 0.60f) : new Color(0.84f, 0.60f, 1f);
                default: return onGold ? new Color(0.50f, 0.26f, 0.0f) : new Color(1f, 0.86f, 0.30f);
            }
        }

        static string Colored(string text, Color c) { return "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + text + "</color>"; }

        /// <summary>What a chest gives, as plain text (accessibility label and tests): for example "Contains 1 power-up and 400 to 600 coins."</summary>
        public static string DescriptionText(ChestKind kind)
        {
            int pu = ChestRolls.PowerUpCount(kind);
            return "Contains " + pu + (pu == 1 ? " power-up" : " power-ups") + " and " + Num(ChestRolls.MinCoins(kind)) + " to " + Num(ChestRolls.MaxCoins(kind)) + " coins.";
        }

        /// <summary>The same text with coloured words (coins gold, power-ups green, character rarities in their colours) plus the characters line.</summary>
        public static string DescriptionRich(ChestKind kind)
        {
            bool gold = kind == ChestKind.Golden;
            Color coins = gold ? new Color(0.45f, 0.24f, 0.0f) : new Color(1f, 0.86f, 0.30f);
            Color power = gold ? new Color(0.02f, 0.36f, 0.12f) : new Color(0.45f, 0.98f, 0.50f);
            int pu = ChestRolls.PowerUpCount(kind);
            string text = "Contains " + Colored(pu + (pu == 1 ? " power-up" : " power-ups"), power) + " and " + Colored(Num(ChestRolls.MinCoins(kind)) + " to " + Num(ChestRolls.MaxCoins(kind)) + " coins", coins) + ".";
            if (!ChestRolls.CharactersEnabled) return text + "\nCharacters: coming soon";
            int low = (int)ChestRolls.LowestRarity(kind);
            string chars = "";
            for (int i = 0; i < 3 && low + i < RarityNames.Length; i++)
                chars += (i > 0 ? ", " : "") + Colored(RarityNames[low + i], RarityColor(low + i, gold));
            return text + "\nCharacters: " + chars + ", up to " + ChestRolls.MaxCharacters(kind);
        }

        // ---------------- build ----------------

        /// <summary>Builds the two chest cards into the shop's content rect (scrolls when the screen is short or the text is large).</summary>
        public void BuildPage(RectTransform content)
        {
            cardH = CardBaseH + TextK * 700f;
            RectTransform list = ShopChrome.ScrollArea(content, 2f * cardH + RowGap);
            BuildChestCard(list, ChestKind.Iron, 0f);
            BuildChestCard(list, ChestKind.Golden, cardH + RowGap);
        }

        void BuildChestCard(RectTransform list, ChestKind kind, float y)
        {
            bool gold = kind == ChestKind.Golden;
            RectTransform card = ShopChrome.Card(list, kind + "Card", gold ? ShopChrome.FrameColor.Gold : ShopChrome.FrameColor.Blue);
            card.anchorMin = new Vector2(0f, 1f);
            card.anchorMax = new Vector2(1f, 1f);
            card.pivot = new Vector2(0.5f, 1f);
            card.anchoredPosition = new Vector2(0f, -y);
            card.sizeDelta = new Vector2(0f, cardH);

            // left column: the title on top (wraps to two lines at large text), the chest picture under it
            Color titleCol = gold ? new Color(0.33f, 0.14f, 0.02f) : new Color(0.82f, 0.93f, 1f);
            Color baseCol = gold ? new Color(0.20f, 0.08f, 0.02f) : Color.white;
            Text name = UiKit.AddText(card, "Name", ChestName(kind).ToUpperInvariant(), 44, titleCol, TextAnchor.MiddleLeft, true);
            if (gold) name.GetComponent<Outline>().enabled = false;
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform nrt = name.rectTransform;
            nrt.anchorMin = new Vector2(0.03f, 1f);
            nrt.anchorMax = new Vector2(0.35f, 1f);
            nrt.pivot = new Vector2(0.5f, 1f);
            nrt.anchoredPosition = new Vector2(0f, -TextTop);
            nrt.sizeDelta = new Vector2(0f, TitleH);

            Sprite chest = ChestArt(kind);
            Image pic = UiKit.AddImage(card, "Chest", chest, chest != null ? Color.white : new Color(0.7f, 0.5f, 0.3f, 1f));
            pic.preserveAspect = true;
            pic.rectTransform.anchorMin = new Vector2(0.02f, 0f);
            pic.rectTransform.anchorMax = new Vector2(0.36f, 1f);
            pic.rectTransform.offsetMin = new Vector2(0f, 14f);
            pic.rectTransform.offsetMax = new Vector2(0f, -(TextTop + TitleH + 6f));

            // right column: the direct text (what is inside, coloured by what it is) above the two buttons
            Text desc = UiKit.AddText(card, "Contents", DescriptionRich(kind), 42, baseCol, TextAnchor.UpperLeft);
            if (gold) desc.GetComponent<Outline>().enabled = false;
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform drt = desc.rectTransform;
            drt.anchorMin = new Vector2(0.38f, 0f);
            drt.anchorMax = new Vector2(0.97f, 1f);
            drt.offsetMin = new Vector2(0f, BuyBottom + BuyH + 10f);
            drt.offsetMax = new Vector2(0f, -TextTop);
            card.gameObject.AddComponent<A11yLabel>().Text = ChestName(kind) + ". " + DescriptionText(kind) + (ChestRolls.CharactersEnabled ? "" : " Characters: coming soon.");

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

        /// <summary>Pays and opens at once, without the confirmation (the confirmation calls this). Saves once inside TryOpenChests.</summary>
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
            RectTransform panel = UiKit.BuildPopupFrame(overlay, "Open chests?", art, new Vector2(860f, 1180f), host.CloseOverlay);
            int powerUps = ChestRolls.PowerUpCount(kind);
            string body = "Open " + (opens == 1 ? "1 " + ChestName(kind) : "10 " + ChestName(kind) + "s") + " for " + Num(price) + " gems?\n"
                + "Each chest: " + powerUps + (powerUps == 1 ? " power-up and " : " power-ups and ") + Num(ChestRolls.MinCoins(kind)) + " to " + Num(ChestRolls.MaxCoins(kind)) + " coins.\n"
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

        void ShowReveal(ChestKind kind, int opens, List<ChestContents> contents)
        {
            RectTransform overlay = host.NewOverlay();
            int coins = 0;
            var counts = new Dictionary<string, int>();
            foreach (ChestContents c in contents)
            {
                coins += c.Coins;
                foreach (string id in c.PowerUps)
                {
                    int have;
                    counts.TryGetValue(id, out have);
                    counts[id] = have + 1;
                }
            }
            var types = new List<PowerUpDef>();
            foreach (PowerUpDef d in progress.PowerUpDefs.Implemented) if (counts.ContainsKey(d.Id)) types.Add(d);

            const float chestY = 255f, chestH = 170f, coinsY = 445f, listY = 565f;   // the chest starts below the title
            float listH = Mathf.Min(types.Count, MaxListRows) * ListRowH;
            float panelH = listY + listH + 24f + 130f + 40f;
            string title = opens == 1 ? ChestName(kind) : "10 " + ChestName(kind) + "s";
            RectTransform panel = UiKit.BuildPopupFrame(overlay, title, art, new Vector2(900f, panelH), null);
            var anim = overlay.gameObject.AddComponent<ChestRevealAnim>();
            Reveal = anim;

            // the chest, shaking, then bursting open; a tap on it skips the animation
            Sprite chest = ChestArt(kind);
            Image pic = UiKit.AddImage(panel, "Chest", chest, chest != null ? Color.white : new Color(0.7f, 0.5f, 0.3f, 1f));
            UiKit.Place(pic.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -chestY), new Vector2(240f, chestH));
            pic.rectTransform.pivot = new Vector2(0.5f, 1f);
            pic.raycastTarget = true;
            var skip = pic.gameObject.AddComponent<Button>();
            skip.transition = Selectable.Transition.None;
            skip.onClick.AddListener(() => { if (!anim.Done) anim.Skip(); });
            pic.gameObject.AddComponent<A11yLabel>().Text = "Skip the opening animation";
            anim.Chest = pic.rectTransform;
            anim.ChestImage = pic;

            // coins gained, with the word
            RectTransform coinRow = ToyKit.PillRow(panel, "Coins", coinsY, 96f);
            ToyKit.InfoPill(coinRow, art != null ? art.CoinIcon : null, "+" + Num(coins) + " coins", new Color(1f, 0.92f, 0.5f), 600f, 96f);
            coinRow.gameObject.AddComponent<A11yLabel>().Text = Num(coins) + " coins";
            anim.Items.Add(coinRow);

            // power-ups gained by NAME, with a count (a scrolling list when there are many types)
            RectTransform vp = UiKit.Rect(panel, "PowerUps");
            vp.anchorMin = vp.anchorMax = new Vector2(0.5f, 1f);
            vp.pivot = new Vector2(0.5f, 1f);
            vp.sizeDelta = new Vector2(820f, Mathf.Max(10f, listH));
            vp.anchoredPosition = new Vector2(0f, -listY);
            vp.gameObject.AddComponent<RectMask2D>();
            var vhit = vp.gameObject.AddComponent<Image>();
            vhit.color = new Color(0f, 0f, 0f, 0f);
            RectTransform content = UiKit.Rect(vp, "Content");
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(820f, Mathf.Max(10f, types.Count * ListRowH));
            var sr = vp.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.viewport = vp;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            for (int i = 0; i < types.Count; i++)
            {
                PowerUpDef d = types[i];
                int n = counts[d.Id];
                RectTransform r = UiKit.Rect(content, "Row" + i);
                r.anchorMin = new Vector2(0f, 1f);
                r.anchorMax = new Vector2(1f, 1f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(0f, ListRowH - 8f);
                r.anchoredPosition = new Vector2(0f, -(i + 0.5f) * ListRowH);
                Image ic = PowerUpUi.AddIcon(r, "Icon", d.Type, new Vector2(0f, 0.5f), Vector2.zero, 84f, false);
                ic.rectTransform.pivot = new Vector2(0f, 0.5f);
                ic.rectTransform.anchoredPosition = new Vector2(30f, 0f);
                Text nm = UiKit.AddText(r, "Name", d.Name, 46, Theme.Ink, TextAnchor.MiddleLeft, true);
                nm.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiKit.OnPanel(nm);
                RectTransform nrt = nm.rectTransform;
                nrt.anchorMin = Vector2.zero;
                nrt.anchorMax = Vector2.one;
                nrt.offsetMin = new Vector2(138f, 0f);
                nrt.offsetMax = new Vector2(-170f, 0f);
                Text ct = UiKit.AddText(r, "Count", "x" + n, 52, Theme.Ink, TextAnchor.MiddleRight, true);
                ct.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiKit.OnPanel(ct);
                RectTransform crt = ct.rectTransform;
                crt.anchorMin = new Vector2(1f, 0f);
                crt.anchorMax = new Vector2(1f, 1f);
                crt.pivot = new Vector2(1f, 0.5f);
                crt.sizeDelta = new Vector2(150f, 0f);
                crt.anchoredPosition = new Vector2(-24f, 0f);
                r.gameObject.AddComponent<A11yLabel>().Text = n + " " + d.Name;
                anim.Items.Add(r);
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
