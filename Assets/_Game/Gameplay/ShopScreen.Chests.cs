using System;
using System.Collections.Generic;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>Shop tab 1: chests (docs/15 section 4). A chip row picks the kind, one row of four cards shows its tiers, the odds panel below discloses every number.</summary>
    public sealed partial class ShopScreen
    {
        ChestKind chestKind = ChestKind.Coin;

        public ChestKind ChestKindShown { get { return chestKind; } }

        const float ChipH = 126f, ChestCardH = 520f;

        static readonly string[] KindChip = { "Coins", "Gear", "Power-ups" };

        /// <summary>Picks the chest kind shown and rebuilds the tab (tests and the chips use it).</summary>
        public void SelectChestKind(ChestKind kind)
        {
            chestKind = kind;
            RebuildBody();
        }

        void BuildChests()
        {
            bool dev = TestPurchasesEnabled;
            float top = ListTop + (dev ? 140f : 0f);
            if (dev)
            {
                Button give = UiKit.AddButton(body, "TestGive", "Test: add 1 of each chest", Btn(0), Color.white, new Vector2(760f, 126f), TestGrantChests, 44);
                UiKit.Place((RectTransform)give.transform, new Vector2(0.5f, 1f), new Vector2(0f, -ListTop), new Vector2(760f, 126f));
                give.GetComponent<A11yLabel>().Text = "Test only: add one of every chest";
            }

            float oddsH = OddsPanelHeight();
            float cardsY = ChipH + 24f;
            float oddsY = cardsY + ChestCardH + 24f;
            RectTransform list = MakeList(oddsY + oddsH + 24f, top);

            // Kind chips
            RectTransform row = UiKit.Rect(list, "Kinds");
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(SideMargin, -ChipH);
            row.offsetMax = new Vector2(-SideMargin, 0f);
            for (int k = 0; k < 3; k++)
            {
                var kind = (ChestKind)k;
                bool on = kind == chestKind;
                Button b = UiKit.AddButton(row, "Kind" + KindChip[k], KindChip[k], ToyKit.Button(on ? 0 : 2), Color.white, new Vector2(200f, ChipH), () => SelectChestKind(kind), 44);
                b.GetComponent<A11yLabel>().Text = KindChip[k] + " chests" + (on ? ", shown" : "");
                var rt = (RectTransform)b.transform;
                rt.anchorMin = new Vector2(k / 3f, 0f);
                rt.anchorMax = new Vector2((k + 1) / 3f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(6f, 0f);
                rt.offsetMax = new Vector2(-6f, 0f);
                Text label = b.transform.Find("Label").GetComponent<Text>();
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                if (on)
                {
                    Image tick = UiKit.AddImage(b.transform, "Tick", Glyphs.Check, Color.white);
                    tick.rectTransform.anchorMin = tick.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                    tick.rectTransform.pivot = new Vector2(0f, 0.5f);
                    tick.rectTransform.anchoredPosition = new Vector2(10f, 0f);
                    tick.rectTransform.sizeDelta = new Vector2(48f, 48f);
                    label.rectTransform.offsetMin = new Vector2(52f, 0f);
                }
            }

            // One row of four tiers (the shop grid rule: 4 in the first row).
            for (int t = 0; t < 4; t++)
            {
                ChestDef def = progress.ChestDefs.Get(ChestId(chestKind, (Rarity)t));
                int c, cols, rr;
                Cell(t, 4, out rr, out c, out cols);
                BuildChestCard(list, cardsY, c, cols, def);
            }

            BuildOddsPanel(list, oddsY, oddsH);
        }

        static string ChestId(ChestKind kind, Rarity tier)
        {
            string k = kind == ChestKind.Coin ? "coin" : (kind == ChestKind.Gear ? "gear" : "powerup");
            return k + "_" + RarityInfo.Name(tier).ToLowerInvariant();
        }

        static Color ChestTint(Rarity tier)
        {
            return Color.Lerp(Color.white, GearArt.Fill(tier), 0.55f);
        }

        void BuildChestCard(RectTransform list, float y, int col, int cols, ChestDef def)
        {
            RectTransform crt = UiKit.Rect(list, "Panel");
            Transform card = crt;
            Theme.CardBacking(crt);
            PlaceCell(crt, y, col, cols, ChestCardH);

            Image icon = UiKit.AddImage(card, "Icon", ToyKit.Get("icon_chest"), ChestTint(def.Tier));
            icon.preserveAspect = true;
            UiKit.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(130f, 130f));
            icon.rectTransform.pivot = new Vector2(0.5f, 1f);
            icon.raycastTarget = true;
            var ib = icon.gameObject.AddComponent<Button>();
            ib.transition = Selectable.Transition.None;
            ib.onClick.AddListener(() => ShowChestDetails(def));
            icon.gameObject.AddComponent<A11yLabel>().Text = "Details of " + def.Name;
            AddInfoDot(icon.transform);
            // The shape cue for the tier: one to three ink bars beside the chest (none for Common), besides the colour and the word.
            for (int m = 0; m < RarityInfo.Stripes(def.Tier); m++)
            {
                Image mark = UiKit.AddImage(card, "Mark" + m, null, Theme.Ink);
                mark.rectTransform.anchorMin = mark.rectTransform.anchorMax = new Vector2(0f, 1f);
                mark.rectTransform.pivot = new Vector2(0f, 1f);
                mark.rectTransform.anchoredPosition = new Vector2(14f + m * 20f, -60f);
                mark.rectTransform.sizeDelta = new Vector2(12f, 60f);
            }

            Text name = UiKit.AddText(card, "Name", RarityInfo.Name(def.Tier), 38, Theme.Ink, TextAnchor.MiddleCenter, true);
            name.GetComponent<Outline>().enabled = false;
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            name.rectTransform.anchorMin = new Vector2(0f, 1f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.pivot = new Vector2(0.5f, 1f);
            name.rectTransform.offsetMin = new Vector2(4f, -232f);
            name.rectTransform.offsetMax = new Vector2(-4f, -176f);

            int owned = progress.ChestCount(def.Id);
            if (owned > 0)
            {
                Button open = UiKit.AddButton(card, "Open", "Open x" + owned, ToyKit.Button(0), Color.white, new Vector2(200f, ChipH), () => TapOpenChest(def), 40);
                open.GetComponent<A11yLabel>().Text = "Open " + def.Name + ", " + owned + " owned";
                var rt = (RectTransform)open.transform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(14f, -(240f + ChipH));
                rt.offsetMax = new Vector2(-14f, -240f);
                open.transform.Find("Label").GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            else
            {
                Text none = UiKit.AddText(card, "Owned", "Own 0", 38, Theme.InkSoft, TextAnchor.MiddleCenter, true);
                DropOutline(none);
                none.horizontalOverflow = HorizontalWrapMode.Overflow;
                none.rectTransform.anchorMin = new Vector2(0f, 1f);
                none.rectTransform.anchorMax = new Vector2(1f, 1f);
                none.rectTransform.pivot = new Vector2(0.5f, 1f);
                none.rectTransform.offsetMin = new Vector2(4f, -300f);
                none.rectTransform.offsetMax = new Vector2(-4f, -244f);
            }

            bool coins = def.CoinPrice > 0;
            int price = coins ? def.CoinPrice : def.GemPrice;
            PriceButton(crt, "Buy", 1, art != null ? (coins ? art.CoinIcon : art.GemIcon) : null, Num(price), "Buy " + def.Name + " for " + Num(price) + (coins ? " coins" : " gems"), () => TapBuyChest(def), 14f);
        }

        // ---------------- odds panel (store compliance: every number is on the screen before a purchase) ----------------

        static int Lines(string text, int fontSize, float width)
        {
            int perLine = Mathf.Max(8, Mathf.FloorToInt(width / (fontSize * 0.50f)));
            int lines = 0;
            foreach (string part in text.Split('\n')) lines += Mathf.Max(1, Mathf.CeilToInt(part.Length / (float)perLine));
            return lines;
        }

        string OddsText()
        {
            var sb = new System.Text.StringBuilder();
            for (int t = 0; t < 4; t++)
            {
                ChestDef d = progress.ChestDefs.Get(ChestId(chestKind, (Rarity)t));
                if (t > 0) sb.Append('\n');
                sb.Append(RarityInfo.Name(d.Tier)).Append(": ").Append(ChestCatalog.OddsText(d));
            }
            return sb.ToString();
        }

        string PityText()
        {
            string k = chestKind == ChestKind.Coin ? "coin" : (chestKind == ChestKind.Gear ? "gear" : "powerup");
            string s = "A Rare or better result is guaranteed within " + ChestCatalog.PityRare + " chests (now " + progress.PityCount(chestKind, "rare") + "). Epic or better within " + ChestCatalog.PityEpic + " (now " + progress.PityCount(chestKind, "epic") + ").";
            if (chestKind == ChestKind.Gear) s += " Legendary within " + ChestCatalog.PityLegendary + " (now " + progress.PityCount(chestKind, "legendary") + ").";
            return s;
        }

        static string PaysText(ChestKind kind)
        {
            switch (kind)
            {
                case ChestKind.Coin: return "A Coin Chest pays coins: from 400 at Common up to 25,000 at Legendary.";
                case ChestKind.Gear: return "A Gear Chest pays one piece of gear. A duplicate turns into Style Shards.";
                default: return "A Power-up Chest pays power-ups: 2 to 12 units, more types in higher tiers.";
            }
        }

        float OddsPanelHeight()
        {
            float w = 1024f - 2f * SideMargin - 48f;
            int fs = Mathf.RoundToInt(Mathf.Max(42f, UiKit.MinBodyTextPx) * UiKit.TextScale);
            int lines = 1 + Lines(PaysText(chestKind), fs, w) + Lines(OddsText(), fs, w) + Lines(PityText(), fs, w);
            return lines * fs * 1.3f * 1.12f + 120f;
        }

        void BuildOddsPanel(RectTransform list, float y, float h)
        {
            RectTransform crt = UiKit.Rect(list, "Panel");
            Theme.CardBacking(crt);
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.offsetMin = new Vector2(SideMargin, -(y + h));
            crt.offsetMax = new Vector2(-SideMargin, -y);
            foreach (Transform t in crt) t.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var vl = crt.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(24, 24, 18, 18);
            vl.spacing = 10f;
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            FlowLine(crt, "OddsHead", "Odds", 46, Theme.Ink, true);
            FlowLine(crt, "Pays", PaysText(chestKind), 42, Theme.Ink, false);
            FlowLine(crt, "Odds", OddsText(), 42, Theme.Ink, false);
            FlowLine(crt, "Pity", PityText(), 42, Theme.InkSoft, false);
        }

        static Text FlowLine(Transform parent, string name, string text, int size, Color color, bool display)
        {
            Text t = UiKit.AddText(parent, name, text, size, color, TextAnchor.UpperLeft, display);
            DropOutline(t);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            return t;
        }

        // ---------------- details popup with a scrolling body ----------------

        /// <summary>A scrolling text body inside the open popup (130 percent text and long lists never overlap the buttons below).</summary>
        void ScrollingPopupBody(string text, float fromTop, float fromBottom)
        {
            RectTransform panel = host.PopupPanel != null ? host.PopupPanel() : null;
            if (panel == null) return;
            Transform old = panel.Find("Body");
            if (old != null) UnityEngine.Object.Destroy(old.gameObject);
            RectTransform vp = UiKit.Rect(panel, "BodyViewport");
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(60f, fromBottom);
            vp.offsetMax = new Vector2(-60f, -fromTop);
            vp.gameObject.AddComponent<RectMask2D>();
            vp.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            RectTransform content = UiKit.Rect(vp, "BodyContent");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            Text t = UiKit.AddText(content, "Text", text, 44, Theme.Ink, TextAnchor.UpperLeft);
            UiKit.OnPanel(t);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            var sr = vp.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.viewport = vp;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 60f;
        }

        /// <summary>Details of one chest: what it pays, the exact odds and the pity counter, with Buy and (when owned) Open.</summary>
        public void ShowChestDetails(ChestDef def)
        {
            bool coins = def.CoinPrice > 0;
            int price = coins ? def.CoinPrice : def.GemPrice;
            int owned = progress.ChestCount(def.Id);
            host.OpenPopup(def.Name, "", 1480f);
            string text = "You own " + owned + ".\n" + PaysDetail(def) + "\nOdds: " + ChestCatalog.OddsText(def) + ".\n" + PityDetail(def.Kind) + "\nPrice: " + Num(price) + (coins ? " coins" : " gems") + ".";
            ScrollingPopupBody(text, 240f, 560f);
            host.PopupButton("Buy for " + Num(price), true, 230f, () => { host.ClosePopup(); TapBuyChest(def); });
            if (owned > 0) host.PopupButton("Open one", false, 380f, () => { host.ClosePopup(); TapOpenChest(def); });
            host.PopupButton("Close", false, 60f, host.ClosePopup);
        }

        static string PaysDetail(ChestDef def)
        {
            switch (def.Kind)
            {
                case ChestKind.Coin: return "Pays " + Num(def.CoinMin) + " to " + Num(def.CoinMax) + " coins.";
                case ChestKind.Gear: return "Pays one piece of gear. A duplicate pays Style Shards.";
                default: return "Pays " + def.UnitsMin + " to " + def.UnitsMax + " power-ups of up to " + def.Types + " kinds.";
            }
        }

        string PityDetail(ChestKind kind)
        {
            string s = "Guarantee: Rare or better within " + ChestCatalog.PityRare + " chests (now " + progress.PityCount(kind, "rare") + "), Epic or better within " + ChestCatalog.PityEpic + " (now " + progress.PityCount(kind, "epic") + ")";
            if (kind == ChestKind.Gear) s += ", Legendary within " + ChestCatalog.PityLegendary + " (now " + progress.PityCount(kind, "legendary") + ")";
            return s + ".";
        }

        // ---------------- buy and open ----------------

        string lastOpenedText;

        /// <summary>Tap on a chest price: too few coins or gems explains it and points to the right place; a gem spend above 50 asks first.</summary>
        public void TapBuyChest(ChestDef def)
        {
            bool coins = def.CoinPrice > 0;
            int price = coins ? def.CoinPrice : def.GemPrice;
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
                host.OpenPopup("Confirm purchase", "Buy a " + def.Name + " for " + Num(price) + " gems?\nOdds: " + ChestCatalog.OddsText(def) + ".\nYou have " + Num(progress.Gems) + " gems.", 1280f);
                host.PopupButton("Buy for " + Num(price) + " gems", true, 230f, () => CompleteChestPurchase(def));
                host.PopupButton("Cancel", false, 60f, host.ClosePopup);
                return;
            }
            CompleteChestPurchase(def);
        }

        void CompleteChestPurchase(ChestDef def)
        {
            host.ClosePopup();
            StoreBuyResult r = progress.TryBuyChest(def.Id);
            switch (r)
            {
                case StoreBuyResult.Ok:
                    AudioManager.Play(Sfx.PowerUp);
                    RebuildBody();   // the card now offers Open
                    break;
                case StoreBuyResult.AlreadyOwned:
                    Info("Stock is full", "You already hold as many " + def.Name + "s as fit (99). Open some first.");
                    break;
                case StoreBuyResult.NotEnoughCoins: Info("Not enough coins", "You do not have enough coins for a " + def.Name + "."); break;
                case StoreBuyResult.NotEnoughGems: Info("Not enough gems", "You do not have enough gems for a " + def.Name + "."); break;
                default: Info("Not available", def.Name + " cannot be bought right now."); break;
            }
        }

        /// <summary>Opens one owned chest (seed from the service) and shows what it paid.</summary>
        public ChestResult TapOpenChest(ChestDef def)
        {
            return OpenChestWithSeed(def, progress.NextChestSeed());
        }

        /// <summary>Same with a fixed seed (tests).</summary>
        public ChestResult OpenChestWithSeed(ChestDef def, ulong seed)
        {
            ChestResult res = progress.TryOpenChest(def.Id, seed);
            if (res == null)
            {
                Info("No chest", "You do not own a " + def.Name + ".");
                return null;
            }
            AudioManager.Play(res.Rarity >= Rarity.Epic ? Sfx.PowerUp : Sfx.Tap);
            lastOpenedText = DescribeResult(res);
            RebuildBody();
            host.OpenPopup(res.Chest.Name, lastOpenedText, 1180f);
            if (progress.ChestCount(def.Id) > 0) host.PopupButton("Open another", true, 230f, () => { host.ClosePopup(); TapOpenChest(def); });
            host.PopupButton("OK", false, 60f, host.ClosePopup);
            return res;
        }

        public string LastOpenedText { get { return lastOpenedText; } }

        string DescribeResult(ChestResult res)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(RarityInfo.Name(res.Rarity)).Append(" result").Append(res.PityHit ? " (guarantee)" : "").Append('\n');
            if (res.Chest.Kind == ChestKind.Coin) sb.Append('+').Append(Num(res.Coins)).Append(" coins");
            else if (res.Chest.Kind == ChestKind.Gear)
            {
                GearDef g = progress.GearDefs.Get(res.GearId);
                string what = g != null ? g.Name + " (" + RarityInfo.Name(g.Rarity) + " " + RarityInfo.SlotName(g.Slot).ToLowerInvariant() + ")" : "a gear piece";
                if (!res.Duplicate) sb.Append("New: ").Append(what).Append(".\nFind it in Gear.");
                else if (res.Shards > 0) sb.Append("Duplicate: ").Append(what).Append(".\n+").Append(res.Shards).Append(" Style Shards");
                else sb.Append("Duplicate: ").Append(what).Append(".\n+").Append(Num(res.Coins)).Append(" coins instead");
            }
            else
            {
                for (int i = 0; i < res.PowerUps.Count; i++)
                {
                    PowerUpDef d = progress.PowerUpDefs.Get(res.PowerUps[i].Key);
                    if (i > 0) sb.Append('\n');
                    sb.Append(d != null ? d.Name : res.PowerUps[i].Key).Append(" x").Append(res.PowerUps[i].Value);
                }
            }
            return sb.ToString();
        }

        void TestGrantChests()
        {
            if (!TestPurchasesEnabled) return;
            foreach (ChestDef d in progress.ChestDefs.All) progress.GrantChest(d.Id, 1);
            RebuildBody();
        }
    }
}
