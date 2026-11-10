using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Monetization;
using FluffyClimbers.Simulation;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>Doc 22 point 9: revive prices (5 then 10 gems), one free ad revive with the first revive, charges only after success.</summary>
    public class ReviveTests
    {
        ContentRepository repo;
        ProgressService progress;

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            progress = new ProgressService(repo, new MemorySaveStore(), 5);
            Services.Ads = new StubAdService { Immediate = true };
        }

        [TearDown]
        public void TearDown() { Services.Reset(); }

        RunSession NewDeadSession()
        {
            var s = new RunSession(repo, new SimConfig(), "lvl_001_01");
            Kill(s);
            return s;
        }

        static void Kill(RunSession s)
        {
            s.State.Grounded = false;
            s.State.Y = Fx.FromInt(-200);
            s.State.Invuln = 0;
            s.Step(0);
        }

        [Test]
        public void PricesAreFiveThenTen()
        {
            Assert.AreEqual(5, RunSession.ReviveCostFor(0));
            Assert.AreEqual(10, RunSession.ReviveCostFor(1));
            Assert.AreEqual(10, RunSession.ReviveCostFor(7), "stays 10");
        }

        [Test]
        public void FirstGemReviveCostsFiveAndKeepsTheRunsGems()
        {
            progress.AddGems(20);
            RunSession s = NewDeadSession();
            s.State.Gems = 3;
            Assert.IsTrue(s.State.Dead);
            Assert.IsTrue(s.TryRevive(progress));
            Assert.AreEqual(15, progress.Gems);
            Assert.AreEqual(3, s.State.Gems, "a revive keeps the run's gems");
            Assert.IsFalse(s.State.Dead);
        }

        [Test]
        public void SecondGemReviveCostsTen()
        {
            progress.AddGems(30);
            RunSession s = NewDeadSession();
            Assert.IsTrue(s.TryRevive(progress));
            Kill(s);
            Assert.AreEqual(10, s.NextReviveCost);
            Assert.IsTrue(s.TryRevive(progress));
            Assert.AreEqual(15, progress.Gems);
            Kill(s);
            Assert.IsTrue(s.CanRevive, "gem revives are not capped");
            Assert.AreEqual(10, s.NextReviveCost, "the price stays 10");
            Assert.IsTrue(s.TryRevive(progress));
            Assert.AreEqual(5, progress.Gems);
            Kill(s);
            Assert.IsFalse(s.TryRevive(progress), "5 gems left, 10 needed");
            Assert.AreEqual(5, progress.Gems);
        }

        [Test]
        public void TooFewGemsSpendsNothingAndDoesNotRevive()
        {
            progress.AddGems(4);
            RunSession s = NewDeadSession();
            Assert.AreEqual(1, s.ReviveShortfall(progress));
            Assert.IsFalse(s.TryRevive(progress));
            Assert.AreEqual(4, progress.Gems);
            Assert.IsTrue(s.State.Dead);
            Assert.AreEqual(0, s.State.RevivesUsed);
        }

        [Test]
        public void GemsAreDeductedOnlyAfterTheReviveSucceeds()
        {
            progress.AddGems(20);
            RunSession s = NewDeadSession();
            s.State.Dead = false;                       // nothing to revive: must not charge
            Assert.IsFalse(s.TryRevive(progress));
            Assert.AreEqual(20, progress.Gems);
            s.State.Dead = true;
            Assert.IsTrue(s.TryRevive(progress));
            Assert.AreEqual(15, progress.Gems);
        }

        [Test]
        public void AdReviveIsOfferedWithTheFirstReviveOnlyWhenAnAdIsReady()
        {
            RunSession s = NewDeadSession();
            Assert.IsTrue(s.AdReviveOffered);
            var ads = new StubAdService { Immediate = true, Ready = false };
            Services.Ads = ads;
            Assert.IsFalse(Services.Ads.IsReady(AdPlacements.Revive), "no ad or no network: only the gem option shows");
            ads.Ready = true;
            Assert.IsTrue(Services.Ads.IsReady(AdPlacements.Revive));
        }

        [Test]
        public void WatchedAdRevivesForFreeAndKeepsGems()
        {
            progress.AddGems(7);
            RunSession s = NewDeadSession();
            s.State.Gems = 4;
            bool granted = false;
            Services.Ads.ShowRewarded(AdPlacements.Revive, g => granted = g);
            Assert.IsTrue(granted);
            s.MarkAdWatched();
            Assert.IsTrue(s.TryReviveFree());
            Assert.AreEqual(7, progress.Gems, "the ad revive is free");
            Assert.AreEqual(4, s.State.Gems);
            Assert.IsFalse(s.State.Dead);
            Assert.AreEqual(1, s.State.RevivesUsed);
        }

        [Test]
        public void AfterAnAdReviveTheNextAdIsSixtySecondsAndGemsStayFive()
        {
            progress.AddGems(12);
            RunSession s = NewDeadSession();
            Assert.AreEqual("revive_30", s.Revive.NextAdPlacement);
            s.MarkAdWatched();
            Assert.IsTrue(s.TryReviveFree());
            Kill(s);
            Assert.IsTrue(s.AdReviveOffered);
            Assert.AreEqual("revive_60", s.Revive.NextAdPlacement);
            Assert.AreEqual(5, s.NextReviveCost, "an ad does not make gems dearer");
            s.MarkAdWatched();
            Assert.IsTrue(s.TryReviveFree());
            Kill(s);
            Assert.IsFalse(s.AdReviveOffered, "no ads after two in a run");
            s.MarkAdWatched();
            Assert.IsFalse(s.TryReviveFree(), "a third free revive is refused");
            Assert.IsTrue(s.TryRevive(progress));
            Assert.AreEqual(7, progress.Gems);
        }

        [Test]
        public void AfterAGemReviveTheAdIsStillOfferedAndGemsCostTen()
        {
            progress.AddGems(5);
            RunSession s = NewDeadSession();
            Assert.IsTrue(s.TryRevive(progress));
            Kill(s);
            Assert.IsTrue(s.AdReviveOffered);
            Assert.AreEqual("revive_30", s.Revive.NextAdPlacement, "no ad was watched yet");
            Assert.AreEqual(10, s.NextReviveCost);
        }

        [Test]
        public void ReviveUsesTheReviveAdPlacement()
        {
            string got = null;
            Services.Ads.ShowRewarded(AdPlacements.Revive, g => got = "done");
            Assert.AreEqual("done", got);
            Assert.AreEqual("revive", AdPlacements.Revive);
        }

        [Test]
        public void NothingIsGrantedWithoutAWatchedAd()
        {
            RunSession s = NewDeadSession();
            Assert.IsFalse(s.TryReviveFree());
            bool granted = true;
            Services.Ads = new StubAdService { Ready = false };
            Services.Ads.ShowRewarded(AdPlacements.Revive, g => granted = g);
            Assert.IsFalse(granted, "early close or error grants nothing");
            Assert.IsFalse(s.TryReviveFree());
            Assert.IsTrue(s.State.Dead);
        }

        [Test]
        public void ReviveThatFailedAfterAWatchedAdIsGrantedAgainOnRetryWithoutAnotherAd()
        {
            RunSession s = NewDeadSession();
            s.State.Dead = false;                       // the revive could not happen right now
            s.MarkAdWatched();
            Assert.IsFalse(s.TryReviveFree());
            Assert.IsTrue(s.AdRewardPending, "the reward is kept");
            var ads = new StubAdService { Immediate = true };
            Services.Ads = ads;
            s.State.Dead = true;
            Assert.IsTrue(s.TryReviveFree());
            Assert.AreEqual(0, ads.ShowCount, "no second ad was needed");
            Assert.IsFalse(s.AdRewardPending);
        }

        [Test]
        public void AFailedAdGrantsNothing()
        {
            Services.Ads = new StubAdService { Immediate = true, Ready = false };
            bool granted = true;
            Services.Ads.ShowRewarded(AdPlacements.Revive, g => granted = g);
            Assert.IsFalse(granted, "no fill or an error grants nothing");
        }
    }
}
