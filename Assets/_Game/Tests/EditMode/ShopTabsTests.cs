using BoingUpSkyTower.Gameplay;
using NUnit.Framework;

namespace BoingUpSkyTower.Tests
{
    /// <summary>Shop tab bar of owner issue 35 (item 2): Bundles | VIP | Heroes | Power-ups | Coins | Gems; the old Chests tab lives inside VIP. Category CriticalUX (CUX-3, CUX-4).</summary>
    [Category("CriticalUX")]
    public class ShopTabsTests
    {
        [Test]
        public void CUX_ShopTabsAreInTheOwnerOrder()
        {
            Assert.AreEqual(6, ShopChrome.TabCount);
            Assert.AreEqual(new[] { "Bundles", "VIP", "Heroes", "Power Ups", "Coins", "Gems" }, ShopChrome.TabNames);
            Assert.AreEqual(0, ShopChrome.TabBundles);
            Assert.AreEqual(1, ShopChrome.TabVip);
            Assert.AreEqual(2, ShopChrome.TabHeroes);
            Assert.AreEqual(3, ShopChrome.TabPowerups);
            Assert.AreEqual(4, ShopChrome.TabCoins);
            Assert.AreEqual(5, ShopChrome.TabGems);
            Assert.AreEqual(ShopChrome.TabNames, ShopScreen.TabNames);
        }

        [Test]
        public void CUX_ShopScreenTabIdsMatchTheChrome()
        {
            Assert.AreEqual(ShopChrome.TabVip, ShopScreen.TabVip);
            Assert.AreEqual(ShopChrome.TabHeroes, ShopScreen.TabHeroes);
            Assert.AreEqual(0, ShopScreen.VipSubscriptionSection);   // Subscription first
            Assert.AreEqual(1, ShopScreen.VipChestsSection);         // Chests under it
        }

        [Test]
        public void CUX_VipHeaderReadsVip()
        {
            Assert.AreEqual("VIP", ShopChrome.TabNames[ShopChrome.TabVip]);
            Assert.AreEqual("VIP", SubscriptionScreen.PageTitle);
        }

        [Test]
        public void CUX_GlobalBarHasNoChestsTab()
        {
            CollectionAssert.DoesNotContain(ToyKit.TabLabels, "Chests");
            Assert.AreEqual(ToyKit.TabLabels.Length, ToyKit.TabA11y.Length);
            Assert.AreEqual("VIP", ToyKit.TabLabels[ToyKit.TabSubscription]);
            Assert.AreEqual("Heroes", ToyKit.TabLabels[ToyKit.TabHeroes]);
            Assert.AreEqual("Shop", ToyKit.TabLabels[ToyKit.TabShop]);
        }
    }
}
