using BoingUpSkyTower.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// Shop tab 3: the six VIP tiers (docs/15 section 6). UI only: no subscription can be bought and no bonus is applied anywhere. Development builds can set a
    /// local tier from the details popup to check the screens.
    /// </summary>
    public sealed partial class ShopScreen
    {
        const float VipCardH = 520f;

        void BuildVip()
        {
            RectTransform list = MakeList(RowCount(VipCatalog.TierCount) * (VipCardH + CardGap) + 20f, ListTop);
            for (int i = 0; i < VipCatalog.TierCount; i++)
            {
                int row, col, cols;
                Cell(i, VipCatalog.TierCount, out row, out col, out cols);
                BuildVipCard(list, row * (VipCardH + CardGap), col, cols, VipCatalog.All[i]);
            }
        }

        void BuildVipCard(RectTransform list, float y, int col, int cols, VipTierDef tier)
        {
            RectTransform crt = UiKit.Rect(list, "Panel");
            Transform card = crt;
            Theme.CardBacking(crt);
            PlaceCell(crt, y, col, cols, VipCardH);
            bool current = progress.VipTier == tier.Tier;

            Image icon = UiKit.AddImage(card, "Icon", ToyKit.Get("icon_crown"), Color.Lerp(Color.white, GearArt.Fill((Rarity)Mathf.Min(3, (tier.Tier - 1) / 2)), 0.45f));
            icon.preserveAspect = true;
            UiKit.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(130f, 130f));
            icon.rectTransform.pivot = new Vector2(0.5f, 1f);
            icon.raycastTarget = true;
            var ib = icon.gameObject.AddComponent<Button>();
            ib.transition = Selectable.Transition.None;
            ib.onClick.AddListener(() => ShowVipDetails(tier));
            icon.gameObject.AddComponent<A11yLabel>().Text = "Details of " + tier.Name;
            AddInfoDot(icon.transform);
            if (current) AddBadgeTop(card, "Current", SpecialBadgeColor);

            Text name = UiKit.AddText(card, "Name", tier.Name, 46, Theme.Ink, TextAnchor.MiddleCenter, true);
            name.GetComponent<Outline>().enabled = false;
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            name.rectTransform.anchorMin = new Vector2(0f, 1f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.pivot = new Vector2(0.5f, 1f);
            name.rectTransform.offsetMin = new Vector2(4f, -296f);
            name.rectTransform.offsetMax = new Vector2(-4f, -230f);
            Text line = UiKit.AddText(card, "Jump", "Jump +" + tier.JumpPct + "%", 38, Theme.InkSoft, TextAnchor.MiddleCenter);
            DropOutline(line);
            line.horizontalOverflow = HorizontalWrapMode.Overflow;
            line.rectTransform.anchorMin = new Vector2(0f, 1f);
            line.rectTransform.anchorMax = new Vector2(1f, 1f);
            line.rectTransform.pivot = new Vector2(0.5f, 1f);
            line.rectTransform.offsetMin = new Vector2(4f, -362f);
            line.rectTransform.offsetMax = new Vector2(-4f, -300f);

            PriceButton(crt, "Price", current ? 0 : 1, null, "$" + tier.MonthlyUsd.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "/mo", "Details of " + tier.Name + ", " + tier.MonthlyText, () => ShowVipDetails(tier), 14f);
        }

        /// <summary>The tier's bonuses, both prices, the renewal disclosure, and the buttons (Subscribe says it is not open yet).</summary>
        public void ShowVipDetails(VipTierDef tier)
        {
            bool dev = TestPurchasesEnabled;
            bool current = progress.VipTier == tier.Tier;
            host.OpenPopup(tier.Name, "", 1480f);
            var sb = new System.Text.StringBuilder();
            foreach (string l in tier.Lines()) sb.Append(l).Append('\n');
            sb.Append('\n').Append(tier.MonthlyText).Append('\n').Append(tier.WeeklyText).Append("\n\n");
            sb.Append("One tier at a time. A subscription renews automatically until you cancel it; you can cancel any time in your store account. ");
            sb.Append("Subscriptions are not open yet: they arrive with the store release, and nothing is charged now.");
            if (current) sb.Append("\n\nThis is your current tier (test).");
            ScrollingPopupBody(sb.ToString(), 240f, dev ? 560f : 430f);
            host.PopupButton("Subscribe", true, 230f, () =>
            {
                host.ClosePopup();
                Info("Not available yet", "Subscriptions open with the store release. Nothing was charged.");
            });
            if (dev)
            {
                host.PopupButton(current ? "Test: end VIP" : "Test: activate", false, 380f, () =>
                {
                    progress.TestSetVipTier(current ? 0 : tier.Tier);
                    host.ClosePopup();
                    RebuildBody();
                });
            }
            host.PopupButton("Close", false, 60f, host.ClosePopup);
        }
    }
}
