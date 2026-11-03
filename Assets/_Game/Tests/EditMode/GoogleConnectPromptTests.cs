using System;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using NUnit.Framework;

namespace BoingUpSkyTower.Tests
{
    /// <summary>Bug List 1 B1.c: when the "connect Google in Settings" pop-up may show (guest, after a completed level, every 3 calendar days, once per session).</summary>
    public class GoogleConnectPromptTests
    {
        static readonly DateTime Now = new DateTime(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc);

        static SaveData Played()
        {
            var d = new SaveData();
            d.Levels["lvl_001_01"] = new LevelProgress { Clears = 1 };
            return d;
        }

        [Test]
        public void NeverBeforeAFirstCompletedLevel()
        {
            Assert.IsFalse(GoogleConnectPrompt.ShouldShow(new SaveData(), AccountPopup.State.Guest, false, Now));
            var tried = new SaveData();
            tried.Levels["lvl_001_01"] = new LevelProgress { Clears = 0 };
            Assert.IsFalse(GoogleConnectPrompt.ShouldShow(tried, AccountPopup.State.Guest, false, Now), "played but not completed");
            Assert.IsTrue(GoogleConnectPrompt.ShouldShow(Played(), AccountPopup.State.Guest, false, Now));
        }

        [Test]
        public void OnlyForGuests()
        {
            Assert.IsFalse(GoogleConnectPrompt.ShouldShow(Played(), AccountPopup.State.SignedIn, false, Now));
            Assert.IsFalse(GoogleConnectPrompt.ShouldShow(Played(), AccountPopup.State.Unavailable, false, Now), "a build without sign-in cannot connect");
        }

        [Test]
        public void OncePerSession()
        {
            Assert.IsFalse(GoogleConnectPrompt.ShouldShow(Played(), AccountPopup.State.Guest, true, Now));
        }

        [Test]
        public void AtMostOnceEveryThreeCalendarDays()
        {
            SaveData d = Played();
            d.LastGooglePromptUtc = new DateTime(2026, 11, 8, 23, 59, 0, DateTimeKind.Utc).ToString("o");
            Assert.IsFalse(GoogleConnectPrompt.ShouldShow(d, AccountPopup.State.Guest, false, new DateTime(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc)), "2 calendar days later");
            Assert.IsFalse(GoogleConnectPrompt.ShouldShow(d, AccountPopup.State.Guest, false, new DateTime(2026, 11, 8, 23, 59, 30, DateTimeKind.Utc)), "same day");
            Assert.IsTrue(GoogleConnectPrompt.ShouldShow(d, AccountPopup.State.Guest, false, new DateTime(2026, 11, 11, 0, 1, 0, DateTimeKind.Utc)), "3 calendar days later (a new date counts, not 72 hours)");
            Assert.AreEqual(3, GoogleConnectPrompt.DaysSinceLastPrompt(d.LastGooglePromptUtc, new DateTime(2026, 11, 11, 0, 1, 0, DateTimeKind.Utc)));
        }

        [Test]
        public void UnreadableOrEmptyStampCountsAsNeverShown()
        {
            SaveData d = Played();
            d.LastGooglePromptUtc = "";
            Assert.IsTrue(GoogleConnectPrompt.ShouldShow(d, AccountPopup.State.Guest, false, Now));
            d.LastGooglePromptUtc = "not a date";
            Assert.IsTrue(GoogleConnectPrompt.ShouldShow(d, AccountPopup.State.Guest, false, Now));
        }

        [Test]
        public void ClockSetBackDoesNotLockThePromptAway()
        {
            SaveData d = Played();
            d.LastGooglePromptUtc = Now.AddDays(30).ToString("o");
            Assert.IsTrue(GoogleConnectPrompt.ShouldShow(d, AccountPopup.State.Guest, false, Now));
        }

        [Test]
        public void ShowingStoresTheStampAndKeepsOldSavesLoadable()
        {
            var store = new MemorySaveStore();
            var p = new ProgressService(BoingUpSkyTower.Content.ContentRepository.LoadFromResources(), store);
            Assert.AreEqual("", p.Data.LastGooglePromptUtc);
            p.MarkGooglePromptShown(Now);
            Assert.AreEqual(0, GoogleConnectPrompt.DaysSinceLastPrompt(p.Data.LastGooglePromptUtc, Now));
            Assert.AreEqual(SaveData.CurrentVersion, p.Data.Version, "additive field: no version break");
            Assert.IsFalse(GoogleConnectPrompt.ShouldShow(p.Data, AccountPopup.State.Guest, false, Now.AddDays(2)));
        }
    }
}
