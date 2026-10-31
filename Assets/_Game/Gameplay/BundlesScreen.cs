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
    /// The Bundles page of the shop (docs/26 section 3, reference screenshot SS12): six bundle cards for gems, each with the bag icon, the name, the
    /// contents as framed chips (coin, gem, power-up, character icons with their counts) and a green price button with the gem icon, plus a Refresh
    /// button (100 coins or 5 gems) that draws six other bundles. Built once into the content rect that ShopChrome.BuildBody returns; a refresh
    /// rebuilds the same rect. Menu screen only: no per-frame code, every position is an anchor, an edge offset or a fixed-size chip, so it fits
    /// 9:16, 9:19.5, 9:21 and 3:4, the safe area and 130 percent text. Touch targets are at least 48 dp (price and Refresh buttons are 130 px tall).
    /// The chip and card helpers are public because the timer-offer popup (BundleTimerButton) draws the same contents.
    /// </summary>
    public static class BundlesScreen
    {
        public const float ChipH = 156f, ChipW = 132f, CoinChipW = 176f, ChipGap = 10f;
        /// <summary>Chips per row: 5 chips (one of them the wider coin chip) are 740 px wide, which fits the narrowest supported canvas (9:21).</summary>
        public const int ChipsPerRow = 5;

        const float CardPad = 26f, IconSize = 140f, NameH = 140f, PriceW = 380f, PriceH = ShopChrome.PriceButtonH, StripH = ShopChrome.PriceButtonH + 22f;
        const float CardGap = 22f, RefreshRowH = 140f, RefreshBtnW = 190f;

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
            if (def.Gems > 0) chips.Add(new Chip { Icon = art != null ? art.GemIcon : null, Text = Compact(def.Gems), Width = ChipW });
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
            if (def.Gems > 0) n++;
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
            if (def.Gems > 0) { if (sb.Length > 0) sb.Append(separator); sb.Append(Num(def.Gems)).Append(" gems"); }
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

        /// <summary>Height of a bundle card: name row, chips, then the dark price strip (like the Coins and Gems pages).</summary>
        public static float CardHeight(BundleDef def)
        {
            return CardPad + NameH + 14f + ChipBlockHeight(ChipRows(def)) + 14f + StripH + 9f;
        }

        /// <summary>
        /// One bundle card in the Coins and Gems page look (gold card, dark strip along the bottom with the green price button) placed at the top of
        /// <paramref name="parent"/> from y, full width. Returns the card height. <paramref name="price"/> is the gem price shown.
        /// </summary>
        public static float AddCard(RectTransform parent, BundleDef def, ArtCatalog art, float y, int price, Action onBuy)
        {
            float chipTop = CardPad + NameH + 14f;
            float h = CardHeight(def);

            RectTransform card = ShopChrome.Card(parent, "Bundle_" + def.Id, ShopChrome.FrameColor.Gold);
            card.anchorMin = new Vector2(0f, 1f);
            card.anchorMax = new Vector2(1f, 1f);
            card.pivot = new Vector2(0.5f, 1f);
            card.anchoredPosition = new Vector2(0f, -y);
            card.sizeDelta = new Vector2(0f, h);

            Image strip = ToyKit.Sliced(card, "Fill", Theme.RoundSprite, 26f);
            strip.color = new Color(0.66f, 0.36f, 0.08f, 1f);
            strip.rectTransform.anchorMin = new Vector2(0f, 0f);
            strip.rectTransform.anchorMax = new Vector2(1f, 0f);
            strip.rectTransform.pivot = new Vector2(0.5f, 0f);
            strip.rectTransform.offsetMin = new Vector2(9f, 9f);
            strip.rectTransform.offsetMax = new Vector2(-9f, 9f + StripH);

            float icon = IconSize + (Mathf.Clamp(def.Tier, 1, 3) - 2) * 14f;   // the bag grows with the tier
            Image bag = UiKit.AddImage(card, "Bag", BagIcon(art), Color.white);
            bag.preserveAspect = true;
            UiKit.Place(bag.rectTransform, new Vector2(0f, 1f), new Vector2(CardPad, -CardPad - (IconSize - icon) * 0.5f), new Vector2(icon, icon));

            Text name = UiKit.AddText(card, "Name", def.Name, 52, UiKit.PanelInk, TextAnchor.MiddleLeft, true);
            UiKit.OnPanel(name);
            var nrt = (RectTransform)name.transform;
            nrt.anchorMin = new Vector2(0f, 1f);
            nrt.anchorMax = new Vector2(1f, 1f);
            nrt.pivot = new Vector2(0f, 1f);
            nrt.offsetMin = new Vector2(CardPad + IconSize + 34f, -(CardPad + NameH));
            nrt.offsetMax = new Vector2(-CardPad, -CardPad);

            string a11y = "Buy " + def.Name + " for " + Num(price) + " gems: " + Describe(def, ", ");
            Button buy = ShopChrome.PriceButton(card, "Buy", 1, art != null ? art.GemIcon : null, Num(price), a11y, onBuy, 44);
            var brt = (RectTransform)buy.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(0f, 9f + (StripH - PriceH) * 0.5f);
            brt.sizeDelta = new Vector2(PriceW, PriceH);

            AddChips(card, def, art, CardPad, chipTop);
            return h;
        }

        // ---------------- the page ----------------

        /// <summary>Builds the Bundles page into <paramref name="body"/> (the content rect of ShopChrome.BuildBody). Calling it again rebuilds the page.</summary>
        public static void Build(RectTransform body, ProgressService progress, ArtCatalog art, ShopScreen.Host host, Action refreshWallet)
        {
            ClearNow(body);
            List<BundleDef> offers = progress.CurrentBundleOffers();
            if (offers.Count == 0)
            {
                Text none = UiKit.AddText(body, "NoBundles", "Bundles are not available right now.\nCome back soon.", 48, Color.white, TextAnchor.MiddleCenter);
                UiKit.Stretch((RectTransform)none.transform);
                return;
            }

            float total = RefreshRowH + CardGap;
            var heights = new float[offers.Count];
            for (int i = 0; i < offers.Count; i++)
            {
                heights[i] = CardHeight(offers[i]);
                total += heights[i] + CardGap;
            }
            total += 20f;
            RectTransform list = ShopChrome.ScrollArea(body, total);

            // top row: a caption on the left, the Refresh button on the right
            Text cap = UiKit.AddText(list, "Caption", "Today's bundles", 44, Color.white, TextAnchor.MiddleLeft, true);
            var crt = (RectTransform)cap.transform;
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0f, 1f);
            crt.offsetMin = new Vector2(8f, -RefreshRowH);
            crt.offsetMax = new Vector2(-(2f * RefreshBtnW + 16f + 8f + 16f), 0f);
            for (int k = 0; k < 2; k++)
            {
                bool withCoins = k == 1;
                int cost = withCoins ? ProgressService.BundleRefreshCoins : ProgressService.BundleRefreshGems;
                bool can = withCoins ? progress.Coins >= cost : progress.Gems >= cost;
                string unit = withCoins ? "coins" : "gems";
                Sprite coin = art != null ? art.CoinIcon : null, gem = art != null ? art.GemIcon : null;
                Button rb = ShopChrome.PriceButton(list, withCoins ? "RefreshCoins" : "RefreshGems", withCoins ? 0 : 1, withCoins ? coin : gem,
                    cost.ToString(), "Show 6 other bundles for " + cost + " " + unit, () => TapRefresh(body, progress, art, host, refreshWallet, withCoins, cap), 44);
                if (!can) rb.GetComponent<Image>().color = new Color(0.55f, 0.55f, 0.55f, 1f);   // dimmed: cannot pay now (the tap explains why)
                var rrt = (RectTransform)rb.transform;
                rrt.anchorMin = rrt.anchorMax = new Vector2(1f, 1f);
                rrt.pivot = new Vector2(1f, 1f);
                rrt.anchoredPosition = new Vector2(-8f - (withCoins ? 0f : RefreshBtnW + 16f), -5f);
                rrt.sizeDelta = new Vector2(RefreshBtnW, RefreshRowH - 10f);
            }

            float y = RefreshRowH + CardGap;
            for (int i = 0; i < offers.Count; i++)
            {
                BundleDef def = offers[i];
                AddCard(list, def, art, y, def.PriceGems, () => TapBuy(body, progress, art, host, refreshWallet, def));
                y += heights[i] + CardGap;
            }
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
