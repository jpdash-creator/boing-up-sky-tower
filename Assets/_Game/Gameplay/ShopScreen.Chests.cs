using System;
using System.Collections.Generic;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The shop's Chests tab: two big chest cards, blue Iron and gold Golden, each with the chest picture, what it holds as icons and numbers (power-ups and coins),
    /// and two buttons: open 1 and open 10 with the gem price. Opening plays a reveal.
    /// Gems only; too few gems offers the gem packs through an icon. Menu screen only: no per-frame code except the reveal animation.
    /// </summary>
    public sealed partial class ShopScreen
    {
        const float ChestCardH = 480f, ChestCardGap = 16f;
        static readonly Color NeedRed = new Color(0.62f, 0.05f, 0.12f, 1f);

        sealed class ChestLook
        {
            public string PlateSprite, ChestSprite, Name;
            public Color Rim;
        }

        static ChestLook Look(ChestKind kind)
        {
            return kind == ChestKind.Iron
                ? new ChestLook { PlateSprite = "plate_sky", ChestSprite = "Art/Chests/chest_iron", Name = "Iron chest", Rim = new Color(0.62f, 0.86f, 1f, 1f) }
                : new ChestLook { PlateSprite = "plate_gold", ChestSprite = "Art/Chests/chest_golden", Name = "Golden chest", Rim = new Color(1f, 0.88f, 0.45f, 1f) };
        }

        static readonly Dictionary<string, Sprite> chestSprites = new Dictionary<string, Sprite>();

        static Sprite ChestSprite(ChestKind kind)
        {
            string path = Look(kind).ChestSprite;
            Sprite s;
            if (chestSprites.TryGetValue(path, out s) && s != null) return s;
            s = Resources.Load<Sprite>(path);
            chestSprites[path] = s;
            return s;
        }

        void BuildChests()
        {
            RectTransform list = MakeList(2 * ChestCardH + ChestCardGap + 8f, ListTop);
            BuildChestCard(list, 0f, ChestKind.Iron);
            BuildChestCard(list, ChestCardH + ChestCardGap, ChestKind.Golden);
        }

        void BuildChestCard(RectTransform list, float y, ChestKind kind)
        {
            ChestLook look = Look(kind);
            RectTransform card = UiKit.Rect(list, "Panel");
            card.anchorMin = new Vector2(0f, 1f);
            card.anchorMax = new Vector2(1f, 1f);
            card.pivot = new Vector2(0.5f, 1f);
            card.offsetMin = new Vector2(SideMargin, -(y + ChestCardH));
            card.offsetMax = new Vector2(-SideMargin, -y);
            Sprite plate = ToyKit.Get(look.PlateSprite);
            Image bg = ToyKit.Sliced(card, "Panel", plate, 70f);
            UiKit.Stretch(bg.rectTransform);
            bg.color = plate != null ? Color.white : look.Rim;

            // the chest picture: tap it for what the chest holds
            Sprite chest = ChestSprite(kind);
            Image pic = UiKit.AddImage(card, "Chest", chest, chest != null ? Color.white : new Color(0.7f, 0.5f, 0.3f, 1f));
            pic.preserveAspect = true;
            pic.rectTransform.anchorMin = pic.rectTransform.anchorMax = new Vector2(0f, 1f);
            pic.rectTransform.pivot = new Vector2(0f, 1f);
            pic.rectTransform.anchoredPosition = new Vector2(40f, -44f);
            pic.rectTransform.sizeDelta = new Vector2(300f, 260f);
            pic.raycastTarget = true;
            var picBtn = pic.gameObject.AddComponent<Button>();
            picBtn.transition = Selectable.Transition.None;
            picBtn.onClick.AddListener(() => OpenChestInfo(kind));
            pic.gameObject.AddComponent<A11yLabel>().Text = "What is in the " + look.Name;
            AddInfoDot(pic.transform);

            // right side: the power-ups and coins it holds
            RectTransform right = UiKit.Rect(card, "Contents");
            right.anchorMin = new Vector2(0f, 1f);
            right.anchorMax = new Vector2(1f, 1f);
            right.pivot = new Vector2(0.5f, 1f);
            right.offsetMin = new Vector2(360f, -330f);
            right.offsetMax = new Vector2(-40f, -34f);
            BuildContentPills(right, kind, 90f, 110f);

            BuildOpenButton(card, kind, 1, 0);
            BuildOpenButton(card, kind, 10, 1);
        }

        /// <summary>Power-ups (bolt, count) and coins (coin, most) the chest holds, as two icon pills.</summary>
        void BuildContentPills(RectTransform parent, ChestKind kind, float y, float h)
        {
            RectTransform row = UiKit.Rect(parent, "Holds");
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -y);
            row.sizeDelta = new Vector2(0f, h);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 12f;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            ToyKit.InfoPill(row, ToyKit.Get("icon_bolt"), "x" + ChestRolls.PowerUpCount(kind), Color.white, 150f, h);
            ToyKit.InfoPill(row, art != null ? art.CoinIcon : null, ChestRolls.MaxCoins(kind).ToString(), new Color(1f, 0.92f, 0.5f), 250f, h);
        }

        /// <summary>One open button: the chest count (x1, x10) and the gem price. Never disabled: too few gems explains itself on tap.</summary>
        void BuildOpenButton(RectTransform card, ChestKind kind, int opens, int slot)
        {
            int price = ChestRolls.Price(kind, opens);
            bool afford = progress.Gems >= price;
            Button b = UiKit.AddButton(card, "Open" + opens, "", ToyKit.Button(opens == 1 ? 1 : 0), Color.white, new Vector2(300f, ButtonH), () => TapOpenChest(kind, opens), 40);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(slot * 0.5f, 0f);
            rt.anchorMax = new Vector2(slot * 0.5f + 0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(slot == 0 ? 36f : 10f, 22f);
            rt.offsetMax = new Vector2(slot == 0 ? -10f : -36f, 22f + ButtonH);
            b.GetComponent<A11yLabel>().Text = "Open " + opens + (opens == 1 ? " chest" : " chests") + " for " + price + " gems";

            Image chestIcon = UiKit.AddImage(b.transform, "Icon", ToyKit.Get("icon_chest"), Color.white);
            SideSlot(chestIcon.rectTransform, 0.04f, 0.2f);
            Text count = UiKit.AddText(b.transform, "Count", "x" + opens, 44, UiKit.ButtonInk, TextAnchor.MiddleLeft, true);
            count.horizontalOverflow = HorizontalWrapMode.Overflow;
            DropOutline(count);
            SideSlot((RectTransform)count.transform, 0.21f, 0.43f);
            Image gem = UiKit.AddImage(b.transform, "Gem", art != null ? art.GemIcon : null, Color.white);
            SideSlot(gem.rectTransform, 0.45f, 0.59f);
            Text cost = UiKit.AddText(b.transform, "Price", price.ToString(), 44, afford ? UiKit.ButtonInk : NeedRed, TextAnchor.MiddleRight, true);
            cost.horizontalOverflow = HorizontalWrapMode.Overflow;
            DropOutline(cost);
            SideSlot((RectTransform)cost.transform, 0.58f, 0.96f);
        }

        static void SideSlot(RectTransform rt, float x0, float x1)
        {
            rt.anchorMin = new Vector2(x0, 0.08f);
            rt.anchorMax = new Vector2(x1, 0.92f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ---------------- opening ----------------

        /// <summary>Tap on an open button: pays gems and reveals, or explains (with the gem packs one tap away) that gems are missing.</summary>
        public void TapOpenChest(ChestKind kind, int opens)
        {
            int price = ChestRolls.Price(kind, opens);
            if (price <= 0 || host.NewOverlay == null) return;
            if (progress.Gems < price) { ShowNeedGems(price); return; }
            List<ChestContents> contents;
            ChestOpenResult res = progress.TryOpenChests(kind, opens, out contents);
            if (res == ChestOpenResult.Ok) ShowReveal(kind, opens, contents);
            else if (res == ChestOpenResult.NotEnoughGems) ShowNeedGems(price);
        }

        /// <summary>The reveal overlay of the last opening (tests skip its animation through it).</summary>
        public ChestRevealAnim Reveal { get; private set; }

        void CloseOverlayAndRefresh()
        {
            Reveal = null;
            host.CloseOverlay();
            RebuildBody();   // the wallet and the price colours follow the new gem count
        }

        void ShowNeedGems(int price)
        {
            RectTransform overlay = host.NewOverlay();
            RectTransform panel = UiKit.BuildPopupFrame(overlay, "", art, new Vector2(860f, 820f), host.CloseOverlay);
            Image gem = UiKit.AddImage(panel, "BigGem", art != null ? art.GemIcon : null, Color.white);
            UiKit.Place(gem.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(230f, 230f));
            gem.rectTransform.pivot = new Vector2(0.5f, 1f);
            RectTransform row = ToyKit.PillRow(panel, "Need", 440f, 110f);
            ToyKit.InfoPill(row, art != null ? art.GemIcon : null, progress.Gems + " / " + price, new Color(1f, 0.82f, 0.82f), 400f, 110f);
            // the gem packs, one tap away
            Button more = ToyKit.IconButton(panel, "GetGems", "Get more gems", 1, art != null ? art.GemIcon : null, new Vector2(170f, 170f), () =>
            {
                host.CloseOverlay();
                Tab = TabGems;
                SelectSection(GemsSection);
            }, 0.62f);
            UiKit.Place((RectTransform)more.transform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(170f, 170f));
        }

        public void OpenChestInfo(ChestKind kind)
        {
            if (host.NewOverlay == null) return;
            RectTransform overlay = host.NewOverlay();
            RectTransform panel = UiKit.BuildPopupFrame(overlay, "", art, new Vector2(860f, 980f), host.CloseOverlay);
            Sprite chest = ChestSprite(kind);
            Image pic = UiKit.AddImage(panel, "Chest", chest, chest != null ? Color.white : new Color(0.7f, 0.5f, 0.3f, 1f));
            UiKit.Place(pic.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(340f, 280f));
            pic.rectTransform.pivot = new Vector2(0.5f, 1f);
            RectTransform box = UiKit.Rect(panel, "Contents");
            box.anchorMin = new Vector2(0f, 1f);
            box.anchorMax = new Vector2(1f, 1f);
            box.pivot = new Vector2(0.5f, 1f);
            box.offsetMin = new Vector2(60f, -800f);
            box.offsetMax = new Vector2(-60f, -450f);
            BuildContentPills(box, kind, 60f, 110f);
            // every power-up type it can give
            RectTransform types = UiKit.Rect(panel, "Types");
            types.anchorMin = new Vector2(0f, 1f);
            types.anchorMax = new Vector2(1f, 1f);
            types.pivot = new Vector2(0.5f, 1f);
            types.offsetMin = new Vector2(40f, -930f);
            types.offsetMax = new Vector2(-40f, -800f);
            var hl = types.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 8f;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            foreach (PowerUpDef d in progress.PowerUpDefs.Implemented)
            {
                Image ic = PowerUpUi.AddIcon(types, "Type" + d.Id, d.Type, new Vector2(0.5f, 0.5f), Vector2.zero, 100f, false);
                var le = ic.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = le.preferredHeight = le.minWidth = le.minHeight = 100f;
            }
        }

        // ---------------- reveal ----------------

        void ShowReveal(ChestKind kind, int opens, List<ChestContents> contents)
        {
            RectTransform overlay = host.NewOverlay();
            // what dropped, counted first so the card is only as tall as its contents (no empty space)
            int coins = 0;
            var units = new Dictionary<string, int>();
            var order = new List<string>();
            foreach (ChestContents c in contents)
            {
                coins += c.Coins;
                foreach (string id in c.PowerUps) { if (!units.ContainsKey(id)) { units[id] = 0; order.Add(id); } units[id]++; }
            }
            int chipRows = (order.Count + 3) / 4;
            float chipsH = order.Count == 0 ? 0f : chipRows * 104f - 8f;
            float y = 410f;                                   // below the chest
            float chipsY = y; y += chipsH + (chipsH > 0f ? 30f : 0f);
            float coinY = y; y += 100f + 30f;
            float doneY = y;                                  // the green tick sits under the last block
            float panelH = doneY + 150f + 50f;
            RectTransform panel = UiKit.BuildPopupFrame(overlay, "", art, new Vector2(900f, panelH), null);
            var anim = overlay.gameObject.AddComponent<ChestRevealAnim>();
            Reveal = anim;

            // the chest, shaking, then bursting open
            Sprite chest = ChestSprite(kind);
            Image pic = UiKit.AddImage(panel, "Chest", chest, chest != null ? Color.white : new Color(0.7f, 0.5f, 0.3f, 1f));
            UiKit.Place(pic.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(340f, 280f));
            pic.rectTransform.pivot = new Vector2(0.5f, 1f);
            anim.Chest = pic.rectTransform;
            anim.ChestImage = pic;

            // power-ups gained: icon and count
            RectTransform chips = UiKit.Rect(panel, "PowerUps");
            chips.anchorMin = chips.anchorMax = new Vector2(0.5f, 1f);
            chips.pivot = new Vector2(0.5f, 1f);
            chips.anchoredPosition = new Vector2(0f, -chipsY);
            chips.sizeDelta = new Vector2(820f, Mathf.Max(10f, chipsH));
            for (int i = 0; i < order.Count; i++)
            {
                PowerUpDef d = progress.PowerUpDefs.Get(order[i]);
                int r = i / 4, c = i % 4;
                RectTransform chip = UiKit.Rect(chips, "Chip" + i);
                chip.anchorMin = chip.anchorMax = new Vector2(0f, 1f);
                chip.pivot = new Vector2(0f, 1f);
                int inRowChips = Mathf.Min(4, order.Count - r * 4);
                chip.anchoredPosition = new Vector2((820f - (inRowChips * 205f - 10f)) * 0.5f + c * 205f, -r * 104f);   // a short row is centred
                chip.sizeDelta = new Vector2(195f, 96f);
                Image ic = PowerUpUi.AddIcon(chip, "Icon", d.Type, new Vector2(0f, 0.5f), Vector2.zero, 90f, false);
                ic.rectTransform.pivot = new Vector2(0f, 0.5f);
                Text n = UiKit.AddText(chip, "Count", "x" + units[order[i]], 46, Theme.Ink, TextAnchor.MiddleLeft, true);
                n.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiKit.OnPanel(n);
                var nrt = (RectTransform)n.transform;
                nrt.anchorMin = Vector2.zero;
                nrt.anchorMax = Vector2.one;
                nrt.offsetMin = new Vector2(102f, 0f);
                nrt.offsetMax = Vector2.zero;
                a11yChip(chip, d.Name, units[order[i]]);
                anim.Items.Add(chip);
            }

            // coins gained
            RectTransform coinRow = ToyKit.PillRow(panel, "Coins", coinY, 100f);
            ToyKit.InfoPill(coinRow, art != null ? art.CoinIcon : null, "+" + coins, new Color(1f, 0.92f, 0.5f), 340f, 100f);
            anim.Items.Add(coinRow);

            // the green tick: first tap skips the animation, the next one closes
            Button done = ToyKit.IconButton(panel, "Collect", "Collect", 1, Glyphs.Check, new Vector2(150f, 150f), () =>
            {
                if (!anim.Done) anim.Skip();
                else CloseOverlayAndRefresh();
            }, 0.56f);
            UiKit.Place((RectTransform)done.transform, new Vector2(0.5f, 1f), new Vector2(0f, -doneY), new Vector2(150f, 150f));
            ((RectTransform)done.transform).pivot = new Vector2(0.5f, 1f);
            anim.Begin(GameSettings.ReducedMotion);
        }

        static void a11yChip(RectTransform chip, string name, int count)
        {
            chip.gameObject.AddComponent<A11yLabel>().Text = count + " " + name;
        }
    }

    /// <summary>
    /// The chest reveal timeline (menu only): the chest shakes harder for 0.9 s, bursts (a quick scale-up and fade), then the pieces pop in one after another.
    /// With reduced motion, or after a tap, it jumps to the end state. Runs only while the reveal overlay exists.
    /// </summary>
    public sealed class ChestRevealAnim : MonoBehaviour
    {
        public RectTransform Chest;
        public Image ChestImage;
        public readonly List<RectTransform> Items = new List<RectTransform>();
        public bool Done { get; private set; }

        const float ShakeEnd = 0.9f, BurstEnd = 1.25f, PopStart = 1.1f, PopStep = 0.09f, PopTime = 0.28f;
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
