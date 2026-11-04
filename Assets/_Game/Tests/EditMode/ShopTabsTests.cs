using BoingUpSkyTower.Gameplay;
using NUnit.Framework;

namespace BoingUpSkyTower.Tests
{
    /// <summary>
    /// Shop tab bar of Bug List 1 section 4: exactly four tabs, Bundles | Crown (Subscription + Chests) | Character / Power-ups | Gems / Coins; Heroes and
    /// Power-ups are sections of one page, Coins is a section of the Gems page. Category CriticalUX (CUX-3, CUX-4).
    /// </summary>
    [Category("CriticalUX")]
    public class ShopTabsTests
    {
        [Test]
        public void CUX_ShopHasExactlyFourTabsInTheOwnerOrder()
        {
            Assert.AreEqual(4, ShopChrome.TabCount);
            Assert.AreEqual(4, ShopChrome.TabNames.Length);
            Assert.AreEqual(4, ShopChrome.TabTitles.Length);
            Assert.AreEqual(0, ShopChrome.TabBundles);
            Assert.AreEqual(1, ShopChrome.TabVip);
            Assert.AreEqual(2, ShopChrome.TabCharacter);
            Assert.AreEqual(3, ShopChrome.TabGems);
            Assert.AreEqual(ShopChrome.TabNames, ShopScreen.TabNames);
        }

        [Test]
        public void CUX_OldTabIdsAreAliasesOfTheMergedTabs()
        {
            Assert.AreEqual(ShopChrome.TabCharacter, ShopChrome.TabHeroes);
            Assert.AreEqual(ShopChrome.TabCharacter, ShopChrome.TabPowerups);
            Assert.AreEqual(ShopChrome.TabGems, ShopChrome.TabCoins);
            Assert.AreEqual(ShopChrome.TabVip, ShopScreen.TabVip);
            Assert.AreEqual(ShopChrome.TabCharacter, ShopScreen.TabCharacter);
            Assert.AreEqual(ShopChrome.TabCharacter, ShopScreen.TabPowerups);
            Assert.AreEqual(ShopChrome.TabGems, ShopScreen.TabCoins);
        }

        [Test]
        public void CUX_SectionsAreFirstThenSecond()
        {
            Assert.AreEqual(0, ShopScreen.VipSubscriptionSection);   // Subscription first
            Assert.AreEqual(1, ShopScreen.VipChestsSection);         // Chests under it
            Assert.AreEqual(0, ShopScreen.HeroesSection);            // Heroes first
            Assert.AreEqual(1, ShopScreen.PowerupsSection);          // Power-ups under it
            Assert.AreEqual(0, ShopScreen.GemsSection);              // Gems first
            Assert.AreEqual(1, ShopScreen.CoinsSection);             // Coins under it
        }

        [Test]
        public void CUX_NormalEntryIsGemsCoinsAtTheTop()
        {
            Assert.AreEqual(ShopChrome.TabGems, ShopScreen.DefaultTab);   // Bug List 3 D2.b
            Assert.AreEqual(ShopScreen.GemsSection, 0);
        }

        [Test]
        public void CUX_PageBannersHaveNoVipHeader()
        {
            Assert.AreEqual("Subscription", ShopChrome.TabTitles[ShopChrome.TabVip]);
            Assert.AreEqual("Heroes", ShopChrome.TabTitles[ShopChrome.TabCharacter]);
            Assert.AreEqual("Gems", ShopChrome.TabTitles[ShopChrome.TabGems]);
            CollectionAssert.DoesNotContain(ShopChrome.TabTitles, "VIP");
            Assert.AreEqual("Chests", ShopScreen.VipChestsSectionTitle);
            Assert.AreEqual("Power-ups", ShopScreen.PowerupsSectionTitle);
            Assert.AreEqual("Coins", ShopScreen.CoinsSectionTitle);
        }

        /// <summary>Bug List 2 C2.a: the scroll viewport starts below the Momo art, so the (scrolling) first banner never covers it.</summary>
        [Test]
        public void CUX_ShopViewportStartsBelowTheMomoArt()
        {
            Assert.GreaterOrEqual(ShopChrome.ListTop, ShopChrome.SceneHeight);
            Assert.Less(ShopChrome.ListTop, ShopChrome.ContentTop, "no fixed banner room is reserved any more");
            Assert.GreaterOrEqual(ShopChrome.BannerBlockH, ShopChrome.RibbonH);
        }

        [Test]
        public void CUX_GlobalBarHasNoChestsTab()
        {
            CollectionAssert.DoesNotContain(ToyKit.TabLabels, "Chests");
            Assert.AreEqual(ToyKit.TabLabels.Length, ToyKit.TabA11y.Length);
            Assert.AreEqual("Shop", ToyKit.TabLabels[ToyKit.TabShop]);
        }

        [Test]
        public void CUX_MergedTabIconNamesAreStable()
        {
            Assert.AreEqual("icon_character_power", ShopIcons.CharacterPowerResource);
            Assert.AreEqual("icon_gems_coins", ShopIcons.GemsCoinsResource);
        }
    }
}
