using FluffyClimbers.Gameplay;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>Owner list 2026-11-02 item 4: the per-run revive schedule (ads 30 s, 1 m, none; gems 5 then 10).</summary>
    public class RevivePolicyTests
    {
        [Test]
        public void FreshRunOffersA30sAdOrFiveGems()
        {
            var p = new RevivePolicy();
            Assert.AreEqual(2, p.AdsLeft);
            Assert.IsTrue(p.AdAvailable);
            Assert.AreEqual(30, p.NextAdSeconds);
            Assert.AreEqual("AD 30s", p.AdLabel);
            Assert.AreEqual("revive_30", p.NextAdPlacement);
            Assert.AreEqual(5, p.NextGemCost);
        }

        [Test]
        public void AfterOneAdTheNextAdIsOneMinuteAndGemsStayFive()
        {
            var p = new RevivePolicy();
            p.RecordAd();
            Assert.AreEqual(1, p.AdsLeft);
            Assert.AreEqual(60, p.NextAdSeconds);
            Assert.AreEqual("AD 1m", p.AdLabel);
            Assert.AreEqual("revive_60", p.NextAdPlacement);
            Assert.AreEqual(5, p.NextGemCost);
        }

        [Test]
        public void AfterTwoAdsThereAreNoMoreAds()
        {
            var p = new RevivePolicy();
            p.RecordAd();
            p.RecordAd();
            Assert.AreEqual(0, p.AdsLeft);
            Assert.IsFalse(p.AdAvailable);
            Assert.AreEqual(0, p.NextAdSeconds);
            Assert.AreEqual("", p.AdLabel);
            Assert.IsNull(p.NextAdPlacement);
            p.RecordAd();
            Assert.AreEqual(0, p.AdsLeft, "never below zero");
            Assert.AreEqual(5, p.NextGemCost, "gems unaffected by ads");
        }

        [Test]
        public void GemRevivesCostFiveThenTenAndStayTen()
        {
            var p = new RevivePolicy();
            Assert.AreEqual(5, p.NextGemCost);
            p.RecordGem();
            Assert.AreEqual(10, p.NextGemCost);
            p.RecordGem();
            p.RecordGem();
            Assert.AreEqual(10, p.NextGemCost);
            Assert.AreEqual(2, p.AdsLeft, "gems do not use up ads");
            Assert.AreEqual(30, p.NextAdSeconds);
        }

        [Test]
        public void MixedSequenceAdGemAd()
        {
            var p = new RevivePolicy();
            p.RecordAd();
            p.RecordGem();
            Assert.AreEqual(60, p.NextAdSeconds);
            Assert.AreEqual(10, p.NextGemCost);
            p.RecordAd();
            Assert.IsFalse(p.AdAvailable);
            Assert.AreEqual(10, p.NextGemCost);
        }

        [Test]
        public void EveryRunStartsFresh()
        {
            var a = new RevivePolicy();
            a.RecordAd();
            a.RecordGem();
            var b = new RevivePolicy();
            Assert.AreEqual(30, b.NextAdSeconds);
            Assert.AreEqual(5, b.NextGemCost);
        }
    }
}
