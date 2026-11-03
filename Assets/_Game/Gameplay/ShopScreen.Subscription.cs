using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The VIP and Heroes tabs of the shop (owner issue 35, item 2; docs/26 section 2). VIP (crown icon) is ONE scrollable page with two labelled sections:
    /// "Subscription" first (the plan cards of <see cref="SubscriptionScreen"/>), then "Chests" (the Iron and Gold chest cards of <see cref="TreasureChestScreen"/>).
    /// Heroes is a placeholder page ("Heroes coming soon"): nothing can be bought there. The old header Subscription (ticket) button is gone.
    /// </summary>
    public sealed partial class ShopScreen
    {
        int vipSection;               // which section the VIP page opens scrolled to
        float vipKeepY = -1f;         // scroll offset to restore after a rebuild of the VIP page (a purchase or a claim); negative = use vipSection
        SubscriptionScreen vipSubscription;

        /// <summary>Page title and section titles of the VIP tab.</summary>
        public const string VipChestsSectionTitle = "Chests";
        public const string HeroesComingSoonText = "Heroes coming soon";

        // ---------------- VIP ----------------

        void BuildVip()
        {
            RectTransform list = ShopChrome.ScrollArea(body, 600f);
            var vg = list.gameObject.AddComponent<VerticalLayoutGroup>();
            vg.spacing = 28f;
            vg.padding = new RectOffset(0, 0, 4, Mathf.RoundToInt(ShopChrome.ScrollEndPad));
            vg.childControlWidth = vg.childControlHeight = true;
            vg.childForceExpandWidth = true;
            vg.childForceExpandHeight = false;
            vg.childAlignment = TextAnchor.UpperCenter;
            var fit = list.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // 1. Subscription (the plan cards; a purchase or claim rebuilds the whole page and keeps the scroll position)
            vipSubscription = new SubscriptionScreen(screen, progress, progress.SubscriptionDefs, art, host);
            RectTransform subSection = vipSubscription.BuildEmbedded(list, RebuildVip);

            // 2. Chests (the Treasure Shop cards)
            RectTransform chestSection = SubscriptionScreen.NewSection(list, VipChestsSectionTitle);
            var h = new TreasureChestScreen.Host
            {
                NewOverlay = host.NewOverlay,
                CloseOverlay = host.CloseOverlay,
                BuyGemPack = index =>
                {
                    if (host.CloseOverlay != null) host.CloseOverlay();
                    SelectTab(TabGems);
                    TapGemPack(index);
                },
            };
            treasure = new TreasureChestScreen(screen, progress, catalog, art, h);
            treasure.BuildCards(chestSection);

            var scroller = list.gameObject.AddComponent<VipScroller>();
            scroller.List = list;
            scroller.Target = vipSection == VipChestsSection ? chestSection : subSection;
            scroller.Y = vipKeepY;
            vipKeepY = -1f;
        }

        /// <summary>Rebuilds the VIP page after a purchase or claim and keeps where the player had scrolled to.</summary>
        void RebuildVip()
        {
            float y = -1f;
            if (page != null)
            {
                RectTransform list = page.Find("Content/ShopViewport/ShopList") as RectTransform;
                if (list != null) y = list.anchoredPosition.y;
            }
            vipKeepY = y;
            RebuildBody();
            RefreshWallet();
        }

        /// <summary>Scrolls the VIP list to a section (or restores an offset) once, on the first frame after the layout has run.</summary>
        sealed class VipScroller : MonoBehaviour
        {
            public RectTransform List, Target;
            public float Y = -1f;

            void LateUpdate()
            {
                enabled = false;
                if (List != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(List);
                    var vp = List.parent as RectTransform;
                    float maxY = Mathf.Max(0f, List.rect.height - (vp != null ? vp.rect.height : 0f));
                    float y = Y;
                    if (y < 0f) y = Target != null ? -(Target.anchoredPosition.y + Target.rect.height * (1f - Target.pivot.y)) : 0f;
                    List.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, 0f, maxY));
                }
                Destroy(this);
            }
        }

        // ---------------- Heroes (placeholder) ----------------

        /// <summary>The Heroes tab: a card with the heroes icon and "Heroes coming soon". No buttons: nothing can be bought here.</summary>
        void BuildHeroes()
        {
            RectTransform card = ShopChrome.Card(body, "HeroesSoon", ShopChrome.FrameColor.Blue);
            card.anchorMin = new Vector2(0.04f, 0.5f);
            card.anchorMax = new Vector2(0.96f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = Vector2.zero;
            float k = Mathf.Max(1f, UiKit.TextScale) - 1f;
            card.sizeDelta = new Vector2(0f, 560f + k * 260f);
            card.gameObject.AddComponent<A11yLabel>().Text = HeroesComingSoonText + ". Nothing to buy here yet.";

            Image icon = UiKit.AddImage(card, "HeroesIcon", ToyKit.Get("icon_heroes"), Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            icon.rectTransform.pivot = new Vector2(0.5f, 1f);
            icon.rectTransform.anchoredPosition = new Vector2(0f, -50f);
            icon.rectTransform.sizeDelta = new Vector2(240f, 240f);

            float titleH = 90f + k * 120f;
            Text title = UiKit.AddText(card, "Title", HeroesComingSoonText, 56, Color.white, TextAnchor.MiddleCenter, true);
            title.fontStyle = FontStyle.Italic;
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Overflow;
            title.rectTransform.anchorMin = new Vector2(0.05f, 1f);
            title.rectTransform.anchorMax = new Vector2(0.95f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -(50f + 240f + 30f));
            title.rectTransform.sizeDelta = new Vector2(0f, titleH);

            Text sub = UiKit.AddText(card, "Info", "New heroes are on their way. Nothing to buy here yet.", 38, new Color(0.82f, 0.93f, 1f), TextAnchor.UpperCenter);
            sub.horizontalOverflow = HorizontalWrapMode.Wrap;
            sub.verticalOverflow = VerticalWrapMode.Overflow;
            sub.rectTransform.anchorMin = new Vector2(0.06f, 1f);
            sub.rectTransform.anchorMax = new Vector2(0.94f, 1f);
            sub.rectTransform.pivot = new Vector2(0.5f, 1f);
            sub.rectTransform.anchoredPosition = new Vector2(0f, -(50f + 240f + 30f + titleH + 10f));
            sub.rectTransform.sizeDelta = new Vector2(0f, 130f + k * 120f);
        }
    }
}
