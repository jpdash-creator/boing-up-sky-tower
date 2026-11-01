using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The Bundles page of the shop (docs/26 section 3, owner issue list 28): a fixed "Refresh Bundles" header (ribbon, caption, two refresh buttons:
    /// 5 gems or 100 coins, direct) above six slim bundle rows for gems: bag icon, name and compact content chips (coin, power-up, later character;
    /// never gems) on the left, the green gem-price button on the right. The list scrolls under the header when it does not fit. Built once into the content rect that ShopChrome.BuildBody returns; a refresh
    /// rebuilds the same rect. Menu screen only: no per-frame code, every position is an anchor, an edge offset or a fixed-size chip, so it fits
    /// 9:16, 9:19.5, 9:21 and 3:4, the safe area and 130 percent text. Touch targets are at least 48 dp (price and Refresh buttons are 130 px tall).
    /// The chip and card helpers are public because the timer-offer popup (BundleTimerButton) draws the same contents.
    /// </summary>
    public static class BundlesScreen
    {
        public const float ChipH = 156f, ChipW = 132f, CoinChipW = 176f, ChipGap = 10f;
        /// <summary>Chips per row: 5 chips (one of them the wider coin chip) are 740 px wide, which fits the narrowest supported canvas (9:21).</summary>
        public const int ChipsPerRow = 5;

        // Slim row (owner issue list 28): bag + name + compact chips on the left, the green gem-price button on the right.
        const float RowPad = 18f, BagSize = 104f, PriceW = 260f, PriceH = ShopChrome.PriceButtonH, RowGap = 10f;
        const float TextLeft = RowPad + BagSize + 12f;
        /// <summary>Width reserved for the name and the chips: the narrowest canvas (9:21, about 940 px) minus margins, pads, bag and price button.</summary>
        const float PriceReserve = PriceW + RowPad + 12f;   // right edge of the text block: price button, its pad and a gap
        /// <summary>Width of the name and chip block. Set from the real body width at the start of Build (conservative fallback when not laid out yet).</summary>
        static float textAreaW = 380f;
        const float CompactChipH = 72f, CompactGap = 8f, NameGap = 6f, NameFont = 42f;
        const float HeaderRibbonW = 700f, HeaderRibbonH = 120f, RefreshRowH = 134f, RefreshBtnW = 190f, HeaderGap = 10f;

        static readonly Color ChipFrame = new Color(0.42f, 0.24f, 0.10f, 1f);
        static readonly Color ChipFill = new Color(0.99f, 0.95f, 0.84f, 1f);

        // ---------------- shared helpers (also used by the timer popup) ----------------

        /// <summary>The bag icon: the OpenArt bag when it exists, the older bundle icon until then, the gem icon as the last fallback.</summary>
        public static Sprite BagIcon(ArtCatalog art)
        {
            return ToyKit.Get("icon_bundle_bag") ?? ToyKit.Get("icon_bundle") ?? (art != null ? art.GemIcon : null);
        }

        public static string Num(int n) { return n.ToString("N0", CultureInfo.InvariantCulture); }

        /// <summary>Counts for a chip: 2,700 as is, 12,900 as 12.9K (never wider than 5 characters).</summary>
        public static string Compact(int n)
        {
            if (n < 10000) return Num(n);
            int tenths = n / 100;
            return tenths % 10 == 0 ? (tenths / 10) + "K" : (tenths / 10) + "." + (tenths % 10) + "K";
        }

        sealed class Chip
        {
            public Sprite Icon;
            public PowerUp Power;
            public bool IsPower;
            public string Text;
            public float Width;
        }

        static List<Chip> MakeChips(BundleDef def, ArtCatalog art)
        {
            var chips = new List<Chip>(7);
            if (def.Coins > 0) chips.Add(new Chip { Icon = art != null ? art.CoinIcon : null, Text = Compact(def.Coins), Width = CoinChipW });
            // no gem chip: bundles contain only coins and power-ups (and later a character)
            foreach (BundlePowerUp p in def.PowerUps)
            {
                PowerUp type = PowerUps.FromId(p.Id);
                if (type == PowerUp.None) continue;
                chips.Add(new Chip { IsPower = true, Power = type, Text = "x" + p.Count, Width = ChipW });
            }
            if (!string.IsNullOrEmpty(def.CharacterId)) chips.Add(new Chip { Icon = ToyKit.Get("icon_heroes"), Text = "1", Width = ChipW });
            return chips;
        }

        /// <summary>Rows of chips a bundle needs (1 or 2).</summary>
        public static int ChipRows(BundleDef def)
        {
            int n = 0;
            if (def.Coins > 0) n++;
            n += def.PowerUps.Count;
            if (!string.IsNullOrEmpty(def.CharacterId)) n++;
            return Mathf.Max(1, (n + ChipsPerRow - 1) / ChipsPerRow);
        }

        /// <summary>Height of the chip block for the given number of rows.</summary>
        public static float ChipBlockHeight(int rows) { return rows * ChipH + (rows - 1) * ChipGap; }

        /// <summary>
        /// Draws the content chips into <paramref name="parent"/>, top-left anchored from (left, top), in rows of at most 5. Returns the height used.
        /// </summary>
        public static float AddChips(RectTransform parent, BundleDef def, ArtCatalog art, float left, float top)
        {
            List<Chip> chips = MakeChips(def, art);
            float x = 0f;
            int row = 0, inRow = 0;
            foreach (Chip c in chips)
            {
                if (inRow == ChipsPerRow) { row++; inRow = 0; x = 0f; }
                AddChip(parent, c, left + x, top + row * (ChipH + ChipGap));
                x += c.Width + ChipGap;
                inRow++;
            }
            return ChipBlockHeight(Mathf.Max(1, row + 1));
        }

        static void AddChip(RectTransform parent, Chip c, float x, float y)
        {
            RectTransform root = UiKit.Rect(parent, "Chip");
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(x, -y);
            root.sizeDelta = new Vector2(c.Width, ChipH);
            Image frame = ToyKit.Sliced(root, "Frame", Theme.RoundSprite, 22f);
            frame.color = ChipFrame;
            UiKit.Stretch(frame.rectTransform);
            Image fill = ToyKit.Sliced(root, "Fill", Theme.RoundSprite, 18f);
            fill.color = ChipFill;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = new Vector2(5f, 5f);
            fill.rectTransform.offsetMax = new Vector2(-5f, -5f);
            if (c.IsPower)
            {
                PowerUpUi.AddIcon(root, "Icon", c.Power, new Vector2(0.5f, 1f), new Vector2(0f, -10f), 64f, false);
            }
            else
            {
                Image ic = UiKit.AddImage(root, "Icon", c.Icon, Color.white);
                UiKit.Place(ic.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(64f, 64f));
            }
            Text count = UiKit.AddText(root, "Count", c.Text, 42, UiKit.PanelInk, TextAnchor.MiddleCenter, true);
            UiKit.OnPanel(count);
            count.horizontalOverflow = HorizontalWrapMode.Overflow;
            var crt = (RectTransform)count.transform;
            crt.anchorMin = new Vector2(0f, 0f);
            crt.anchorMax = new Vector2(1f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.offsetMin = new Vector2(2f, 4f);
            crt.offsetMax = new Vector2(-2f, 76f);
        }

        /// <summary>Contents in words, one item per line: "12,000 coins", "40 gems", "Magnet x3". Used by the popups and the screen reader label.</summary>
        public static string Describe(BundleDef def, string separator)
        {
            var sb = new StringBuilder(96);
            if (def.Coins > 0) sb.Append(Num(def.Coins)).Append(" coins");
            foreach (BundlePowerUp p in def.PowerUps)
            {
                if (sb.Length > 0) sb.Append(separator);
                sb.Append(PowerUpUi.Name(PowerUps.FromId(p.Id))).Append(" x").Append(p.Count);
            }
            if (!string.IsNullOrEmpty(def.CharacterId))
            {
                CharacterSpec cs = CharacterDefs.Get(def.CharacterId);
                if (sb.Length > 0) sb.Append(separator);
                sb.Append(cs != null ? cs.Name : "A new character");
            }
            return sb.ToString();
        }

        // ---------------- the slim bundle row ----------------

        /// <summary>Width of a compact chip: icon, then the count (grows with the text scale so "12.9K" never overflows the frame).</summary>
        static float CompactChipW(Chip c) { return Mathf.Min(textAreaW, Mathf.Ceil(70f + c.Text.Length * 26f * UiKit.TextScale)); }

        static float NameLineH() { return Mathf.Ceil(Mathf.Max(NameFont, UiKit.MinBodyTextPx) * UiKit.TextScale * 1.2f); }

        /// <summary>1 or 2 name lines, estimated from the text length with a generous glyph width (the layout stays deterministic).</summary>
        static int NameLines(BundleDef def)
        {
            float charW = Mathf.Max(NameFont, UiKit.MinBodyTextPx) * UiKit.TextScale * 0.6f;
            int perLine = Mathf.Max(6, (int)(textAreaW / charW));
            int len = def.Name != null ? def.Name.Length : 0;
            return len > perLine * 2 ? 3 : (len > perLine ? 2 : 1);
        }

        static int CompactRows(List<Chip> chips)
        {
            int rows = 1;
            float x = 0f;
            foreach (Chip c in chips)
            {
                float w = CompactChipW(c);
                if (x > 0f && x + w > textAreaW) { rows++; x = 0f; }
                x += w + CompactGap;
            }
            return rows;
        }

        static float TextBlockHeight(BundleDef def)
        {
            int rows = CompactRows(MakeChips(def, null));
            return NameLines(def) * NameLineH() + NameGap + rows * CompactChipH + (rows - 1) * CompactGap;
        }

        /// <summary>Height of a bundle row: the name and chip block, but never less than the price button (130 px) plus padding.</summary>
        public static float CardHeight(BundleDef def)
        {
            return Mathf.Max(RowPad * 2f + TextBlockHeight(def), RowPad * 2f + PriceH);
        }

        static void AddCompactChip(RectTransform parent, Chip c, float x, float y)
        {
            float w = CompactChipW(c);
            RectTransform root = UiKit.Rect(parent, "Chip");
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(x, -y);
            root.sizeDelta = new Vector2(w, CompactChipH);
            Image frame = ToyKit.Sliced(root, "Frame", Theme.RoundSprite, 22f);
            frame.color = ChipFrame;
            UiKit.Stretch(frame.rectTransform);
            Image fill = ToyKit.Sliced(root, "Fill", Theme.RoundSprite, 18f);
            fill.color = ChipFill;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = new Vector2(4f, 4f);
            fill.rectTransform.offsetMax = new Vector2(-4f, -4f);
            var left = new Vector2(0f, 0.5f);
            if (c.IsPower) PowerUpUi.AddIcon(root, "Icon", c.Power, left, new Vector2(10f, 0f), 52f, false);
            else
            {
                Image ic = UiKit.AddImage(root, "Icon", c.Icon, Color.white);
                UiKit.Place(ic.rectTransform, left, new Vector2(10f, 0f), new Vector2(52f, 52f));
            }
            Text count = UiKit.AddText(root, "Count", c.Text, 42, UiKit.PanelInk, TextAnchor.MiddleCenter, true);
            UiKit.OnPanel(count);
            count.horizontalOverflow = HorizontalWrapMode.Wrap;
            count.resizeTextForBestFit = true;   // a chip capped to the text area shrinks its count (never below 16 sp) instead of overflowing
            count.resizeTextMinSize = Mathf.RoundToInt(UiKit.MinBodyTextPx);
            count.resizeTextMaxSize = count.fontSize;
            var crt = (RectTransform)count.transform;
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = new Vector2(64f, 2f);
            crt.offsetMax = new Vector2(-8f, -2f);
        }

        /// <summary>
        /// One slim bundle row (gold card) placed at the top of <paramref name="parent"/> from y, full width: bag icon, name and compact chips on the left,
        /// the green gem-price button on the right. Returns the row height. <paramref name="price"/> is the gem price shown.
        /// </summary>
        public static float AddCard(RectTransform parent, BundleDef def, ArtCatalog art, float y, int price, Action onBuy)
        {
            float h = CardHeight(def);
            float top = (h - TextBlockHeight(def)) * 0.5f;   // centre the text block when the price button makes the row taller

            RectTransform card = ShopChrome.Card(parent, "Bundle_" + def.Id, ShopChrome.FrameColor.Gold);
            card.anchorMin = new Vector2(0f, 1f);
            card.anchorMax = new Vector2(1f, 1f);
            card.pivot = new Vector2(0.5f, 1f);
            card.anchoredPosition = new Vector2(0f, -y);
            card.sizeDelta = new Vector2(0f, h);

            float icon = BagSize + (Mathf.Clamp(def.Tier, 1, 3) - 2) * 8f;   // the bag grows a little with the tier
            Image bag = UiKit.AddImage(card, "Bag", BagIcon(art), Color.white);
            bag.preserveAspect = true;
            UiKit.Place(bag.rectTransform, new Vector2(0f, 0.5f), new Vector2(RowPad + (BagSize - icon) * 0.5f, 0f), new Vector2(icon, icon));

            int lines = NameLines(def);
            Text name = UiKit.AddText(card, "Name", def.Name, (int)NameFont, UiKit.PanelInk, TextAnchor.MiddleLeft, true);
            UiKit.OnPanel(name);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.resizeTextForBestFit = true;   // shrinks (never below 16 sp) instead of spilling over the price button
            name.resizeTextMinSize = Mathf.RoundToInt(UiKit.MinBodyTextPx);
            name.resizeTextMaxSize = name.fontSize;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            // stretch between the bag and the price button: the right edge is tied to the row, so it can never run under the button
            var nrt = (RectTransform)name.transform;
            nrt.anchorMin = new Vector2(0f, 1f);
            nrt.anchorMax = new Vector2(1f, 1f);
            nrt.pivot = new Vector2(0f, 1f);
            nrt.offsetMin = new Vector2(TextLeft, -(top + lines * NameLineH()));
            nrt.offsetMax = new Vector2(-PriceReserve, -top);

            // compact chips under the name, wrapping inside the text area
            float chipTop = top + lines * NameLineH() + NameGap;
            float x = 0f;
            int row = 0;
            foreach (Chip c in MakeChips(def, art))
            {
                float w = CompactChipW(c);
                if (x > 0f && x + w > textAreaW) { row++; x = 0f; }
                AddCompactChip(card, c, TextLeft + x, chipTop + row * (CompactChipH + CompactGap));
                x += w + CompactGap;
            }

            string a11y = "Buy " + def.Name + " for " + Num(price) + " gems: " + Describe(def, ", ");
            Button buy = ShopChrome.PriceButton(card, "Buy", 1, art != null ? art.GemIcon : null, Num(price), a11y, onBuy, 44);
            UiKit.Place((RectTransform)buy.transform, new Vector2(1f, 0.5f), new Vector2(-RowPad, 0f), new Vector2(PriceW, PriceH));
            return h;
        }

        // ---------------- the page ----------------

        /// <summary>
        /// Builds the Bundles page into <paramref name="body"/> (the content rect of ShopChrome.BuildBody). Calling it again rebuilds the page.
        /// Fixed header on top (a "Refresh Bundles" ribbon, a one-line caption and the two refresh buttons), the slim bundle rows scroll below it.
        /// </summary>
        public static void Build(RectTransform body, ProgressService progress, ArtCatalog art, ShopScreen.Host host, Action refreshWallet)
        {
            ClearNow(body);
            // real row width when the canvas is laid out; the narrowest supported content (about 880 px) otherwise. Never wider than the room
            // between the bag and the price button, so chips wrap to a second row (and the row grows) instead of reaching the button.
            // The rect is often not laid out yet while the page is built, so the width is also computed the way CanvasScaler does it (match 0.5:
            // scale = sqrt(w/1080 * h/1920)) from the screen size and the safe area; the smaller of the two wins.
            float sw = Mathf.Max(1f, Screen.width), sh = Mathf.Max(1f, Screen.height);
            float scale = Mathf.Sqrt(sw / UiKit.Reference.x * sh / UiKit.Reference.y);
            float safeW = Mathf.Clamp01(UiKit.SafeAreaNormalized().width);
            float bodyW = Mathf.Max(300f, sw / scale * safeW - 2f * ShopChrome.SideMargin);
            float rectW = body.rect.width;
            if (rectW >= 300f) bodyW = rectW;   // a laid-out rect is the truth; BundleRelayout rebuilds once if it only appears after this call
            textAreaW = Mathf.Clamp(bodyW - TextLeft - PriceReserve - 12f, 200f, 600f);
            List<BundleDef> offers = progress.CurrentBundleOffers();
            if (offers.Count == 0)
            {
                Text none = UiKit.AddText(body, "NoBundles", "Bundles are not available right now.\nCome back soon.", 48, Color.white, TextAnchor.MiddleCenter);
                UiKit.Stretch((RectTransform)none.transform);
                return;
            }

            // fixed header: ribbon, then caption + the two refresh buttons
            float headH = HeaderRibbonH + HeaderGap + RefreshRowH + HeaderGap;
            RectTransform head = UiKit.Rect(body, "RefreshHeader");
            head.anchorMin = new Vector2(0f, 1f);
            head.anchorMax = new Vector2(1f, 1f);
            head.pivot = new Vector2(0.5f, 1f);
            head.anchoredPosition = Vector2.zero;
            head.sizeDelta = new Vector2(0f, headH);
            // The kit ribbon is too small for this title at this size (the text ended up half on the wood), so the header is the yellow italic
            // text straight on the wood, with a dark outline: about 11:1 against the wood.
            Text title = UiKit.AddText(head, "HeaderTitle", "Refresh Bundles", 52, new Color(1f, 0.85f, 0.25f, 1f), TextAnchor.MiddleCenter, true);
            title.fontStyle = FontStyle.Italic;
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            var trt = (RectTransform)title.transform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(8f, -HeaderRibbonH);
            trt.offsetMax = new Vector2(-8f, 0f);

            float rowTop = HeaderRibbonH + HeaderGap;
            Text cap = UiKit.AddText(head, "Caption", "Draw 6 new bundles", 42, Color.white, TextAnchor.MiddleLeft, true);
            var crt = (RectTransform)cap.transform;
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0f, 1f);
            crt.offsetMin = new Vector2(8f, -(rowTop + RefreshRowH));
            crt.offsetMax = new Vector2(-(2f * RefreshBtnW + 16f + 8f + 16f), -rowTop);
            for (int k = 0; k < 2; k++)
            {
                bool withCoins = k == 1;
                int cost = withCoins ? ProgressService.BundleRefreshCoins : ProgressService.BundleRefreshGems;
                bool can = withCoins ? progress.Coins >= cost : progress.Gems >= cost;
                string unit = withCoins ? "coins" : "gems";
                Sprite coin = art != null ? art.CoinIcon : null, gem = art != null ? art.GemIcon : null;
                Button rb = ShopChrome.PriceButton(head, withCoins ? "RefreshCoins" : "RefreshGems", withCoins ? 0 : 1, withCoins ? coin : gem,
                    cost.ToString(), "Show 6 other bundles for " + cost + " " + unit, () => TapRefresh(body, progress, art, host, refreshWallet, withCoins, cap), 44);
                if (!can) rb.GetComponent<Image>().color = new Color(0.55f, 0.55f, 0.55f, 1f);   // dimmed: cannot pay now (the tap explains why)
                UiKit.Place((RectTransform)rb.transform, new Vector2(1f, 1f), new Vector2(-8f - (withCoins ? 0f : RefreshBtnW + 16f), -(rowTop + (RefreshRowH - PriceH) * 0.5f)),
                    new Vector2(RefreshBtnW, PriceH));
            }

            // scrolling list below the header (the header stays put)
            RectTransform listHost = UiKit.Rect(body, "BundleListHost");
            listHost.anchorMin = Vector2.zero;
            listHost.anchorMax = Vector2.one;
            listHost.offsetMin = Vector2.zero;
            listHost.offsetMax = new Vector2(0f, -headH);
            float total = 6f;
            var heights = new float[offers.Count];
            for (int i = 0; i < offers.Count; i++)
            {
                heights[i] = CardHeight(offers[i]);
                total += heights[i] + RowGap;
            }
            RectTransform list = ShopChrome.ScrollArea(listHost, total);
            float y = 0f;
            for (int i = 0; i < offers.Count; i++)
            {
                BundleDef def = offers[i];
                AddCard(list, def, art, y, def.PriceGems, () => TapBuy(body, progress, art, host, refreshWallet, def));
                y += heights[i] + RowGap;
            }

            // if the body width was not known yet, rebuild once as soon as the layout has run (chips must wrap against the real width)
            var rl = body.GetComponent<BundleRelayout>();
            if (rl == null) rl = body.gameObject.AddComponent<BundleRelayout>();
            rl.Body = body;
            rl.BuiltW = rectW >= 300f ? rectW : -1f;
            rl.Rebuild = () => Build(body, progress, art, host, refreshWallet);
        }

        /// <summary>Removes the old children at once (Destroy alone leaves them alive until the end of the frame, so old and new would overlap for a frame).</summary>
        static void ClearNow(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform t = parent.GetChild(i);
                t.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(t.gameObject);
            }
        }

        // ---------------- buying ----------------

        static void Info(ShopScreen.Host host, string title, string body, float height = 860f)
        {
            host.OpenPopup(title, body, height);
            host.PopupButton("OK", false, 60f, host.ClosePopup);
        }

        static void TapBuy(RectTransform body, ProgressService progress, ArtCatalog art, ShopScreen.Host host, Action refreshWallet, BundleDef def)
        {
            int price = def.PriceGems;
            if (progress.Gems < price)
            {
                host.OpenPopup("Not enough gems", "You need " + Num(price) + " gems and have " + Num(progress.Gems) + ".\nOpen the Gems tab to get more.", 1180f);
                host.PopupButton("Close", false, 60f, host.ClosePopup);
                return;
            }
            if (price > ProgressService.BundleConfirmAboveGems)
            {
                host.OpenPopup("Confirm purchase", "Buy " + def.Name + " for " + Num(price) + " gems?\nYou have " + Num(progress.Gems) + " gems.", 1180f);
                host.PopupButton("Buy for " + Num(price) + " gems", true, 230f, () => CompleteBuy(body, progress, art, host, refreshWallet, def));
                host.PopupButton("Cancel", false, 60f, host.ClosePopup);
                return;
            }
            CompleteBuy(body, progress, art, host, refreshWallet, def);
        }

        static void CompleteBuy(RectTransform body, ProgressService progress, ArtCatalog art, ShopScreen.Host host, Action refreshWallet, BundleDef def)
        {
            host.ClosePopup();
            ShowResult(host, refreshWallet, def, progress.TryBuyBundle(def));
        }

        /// <summary>Feedback after a purchase attempt: a summary of everything received, or the reason it failed. Shared with the timer offer.</summary>
        public static void ShowResult(ShopScreen.Host host, Action refreshWallet, BundleDef def, BundleResult result)
        {
            switch (result)
            {
                case BundleResult.Ok:
                    AudioManager.Play(Sfx.PowerUp);
                    if (refreshWallet != null) refreshWallet();
                    Info(host, "Bundle bought!", def.Name + "\n" + Describe(def, ", "), 1180f);
                    break;
                case BundleResult.NotEnoughGems:
                    Info(host, "Not enough gems", "You do not have enough gems for " + def.Name + ".");
                    break;
                default:
                    Info(host, "Not available", "This bundle cannot be bought right now.");
                    break;
            }
        }

        // ---------------- refresh ----------------

        static void TapRefresh(RectTransform body, ProgressService progress, ArtCatalog art, ShopScreen.Host host, Action refreshWallet, bool withCoins, Text caption)
        {
            BundleResult r = progress.RefreshBundleOffers(withCoins);
            if (r == BundleResult.Ok)
            {
                AudioManager.Play(Sfx.Tap);
                if (refreshWallet != null) refreshWallet();
                Build(body, progress, art, host, refreshWallet);
                return;
            }
            // nothing was spent: a short message in the caption's place
            BundleToast.Show(caption, r == BundleResult.NotEnoughCoins ? "Not enough coins" : (r == BundleResult.NotEnoughGems ? "Not enough gems" : "No bundles right now"));
        }
    }

    /// <summary>Rebuilds the Bundles page when the real body width differs from the width it was built for (menu screens only, one rect compare per frame).</summary>
    public sealed class BundleRelayout : MonoBehaviour
    {
        public RectTransform Body;
        public float BuiltW;
        public Action Rebuild;

        void LateUpdate()
        {
            if (Body == null || Rebuild == null) return;
            float w = Body.rect.width;
            if (w < 300f || Mathf.Abs(w - BuiltW) <= 2f) return;
            BuiltW = w;
            Rebuild();
        }
    }

    /// <summary>Shows a short message in place of a caption for two seconds, then puts the caption back. Menu screens only.</summary>
    public sealed class BundleToast : MonoBehaviour
    {
        Text text;
        string original;
        Color originalColor;
        float left;

        public static void Show(Text caption, string message)
        {
            if (caption == null) return;
            var t = caption.GetComponent<BundleToast>();
            if (t == null)
            {
                t = caption.gameObject.AddComponent<BundleToast>();
                t.text = caption;
                t.original = caption.text;
                t.originalColor = caption.color;
            }
            caption.text = message;
            caption.color = new Color(1f, 0.55f, 0.5f, 1f);
            t.left = 2f;
            AudioManager.Play(Sfx.Fail);
        }

        void Update()
        {
            if (left <= 0f) return;
            left -= Time.unscaledDeltaTime;
            if (left > 0f) return;
            text.text = original;
            text.color = originalColor;
        }
    }
}
