using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The multi-section pages of the shop (owner issue 35; Bug List 1 section 4; docs/26 section 8). Crown is ONE scrollable page: the fixed yellow banner
    /// "Subscription" (no VIP header), the plan cards of <see cref="SubscriptionScreen"/>, a yellow "Chests" banner, then the chest cards of
    /// <see cref="TreasureChestScreen"/>. Character / Power-ups is one scrollable page: "Heroes" banner, the 2 x 2 hero cards (HeroCardsSection.cs),
    /// the "Power-ups" banner and the power-up cards. The Gems / Coins page lives in ShopScreen.cs.
    /// </summary>
    public sealed partial class ShopScreen
    {
        int pageSection;              // which section of the current page it opens scrolled to (0 = first, 1 = second: Chests, Power-ups, Coins)
        float keepY = -1f;            // scroll offset to restore after a rebuild of the page (a purchase or a claim); negative = use pageSection
        SubscriptionScreen vipSubscription;

        /// <summary>Banner titles of the later sections.</summary>
        public const string VipChestsSectionTitle = "Chests";
        public const string PowerupsSectionTitle = "Power-ups";
        public const string CoinsSectionTitle = "Coins";

        // ---------------- shared scrolling page ----------------

        /// <summary>One scrolling list that stacks the page's sections (banners, cards) with a 28 px gap; the page's first banner is its first item, so it scrolls away with the list.</summary>
        RectTransform NewSectionedList()
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
            ShopChrome.SectionBanner(list, ShopChrome.TabTitles[Tab]);   // the page's first banner (Subscription, Heroes, Gems) scrolls with the list at position 0
            return list;
        }

        /// <summary>Scrolls the list to <paramref name="target"/> (null = the top) on the first frame, or restores the offset kept over a rebuild.</summary>
        void AttachScroller(RectTransform list, RectTransform target)
        {
            var scroller = list.gameObject.AddComponent<SectionScroller>();
            scroller.List = list;
            scroller.Target = target;
            scroller.Y = keepY;
            keepY = -1f;
        }

        // ---------------- Crown: Subscription, then Chests ----------------

        void BuildVip()
        {
            RectTransform list = NewSectionedList();

            // 1. Subscription (the page banner above is its title; a purchase or claim rebuilds the page and keeps the scroll position)
            vipSubscription = new SubscriptionScreen(screen, progress, progress.SubscriptionDefs, art, host);
            vipSubscription.BuildEmbedded(list, RebuildKeepScroll);

            // 2. Chests: a second yellow banner, then the Treasure Shop cards
            RectTransform chestBanner = ShopChrome.SectionBanner(list, VipChestsSectionTitle);
            RectTransform chestSection = SubscriptionScreen.NewSection(list, VipChestsSectionTitle, false);
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

            AttachScroller(list, pageSection == VipChestsSection ? chestBanner : null);
        }

        /// <summary>Rebuilds the current page after a purchase or claim and keeps where the player had scrolled to.</summary>
        void RebuildKeepScroll()
        {
            float y = -1f;
            if (page != null)
            {
                RectTransform list = page.Find("Content/ShopViewport/ShopList") as RectTransform;
                if (list != null) y = list.anchoredPosition.y;
            }
            keepY = y;
            RebuildBody();
            RefreshWallet();
        }

        /// <summary>Scrolls a list to a section (or restores an offset) once, on the first frame after the layout has run.</summary>
        sealed class SectionScroller : MonoBehaviour
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

        // ---------------- Character / Power-ups ----------------

        /// <summary>
        /// The merged Character / Power-ups page, one scrolling list: the page banner "Heroes", the 2 x 2 hero cards, the "Power-ups" banner and
        /// the power-up cards. ShowShop(TabPowerups) opens it scrolled to the Power-ups banner.
        /// </summary>
        void BuildCharacter()
        {
            RectTransform list = NewSectionedList();
            BuildHeroCards(list);   // HeroCardsSection.cs: the 2 x 2 hero cards (Bug List 4 E2)
            RectTransform powerBanner = ShopChrome.SectionBanner(list, PowerupsSectionTitle);
            BuildPowerUps(list);
            AttachScroller(list, pageSection == PowerupsSection ? powerBanner : null);
        }
    }
}
