using System;
using System.Text;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Monetization;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>Login, account and cloud save rules (docs/35): summaries, bytes, the decision table, the stub account and the sync glue.</summary>
    public class AccountTests
    {
        ContentRepository repo;
        MemorySaveStore store;
        StubAccountService stub;

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            store = new MemorySaveStore();
            stub = new StubAccountService { UseMemoryOnly = true };
            Services.Account = stub;
            AccountSync.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            AccountSync.ResetForTests();
            Services.Reset();
        }

        static SaveData Played(int levels, int gems, int coins, string lastPlayed = "2026-11-01T10:00:00.0000000Z")
        {
            var d = new SaveData { Gems = gems, Coins = coins, LastPlayedUtc = lastPlayed };
            for (int i = 1; i <= levels; i++) d.Levels["lvl_001_" + i.ToString("D2")] = new LevelProgress { Stars = 1, StarMask = 1, Clears = 1, BestScore = 10 };
            return d;
        }

        static SaveEnvelope Env(SaveData d, string saved = "2026-11-02T10:00:00.0000000Z")
        {
            SaveEnvelope e = SaveSync.TryDeserialize(SaveSync.Serialize(d));
            e.SavedUtc = saved;
            return e;
        }

        // ---- summary and bytes ----

        [Test]
        public void SummaryCountsLevelsWalletAndHeroCopies()
        {
            SaveData d = Played(3, 120, 4500);
            d.Copies["momo"] = 2;
            d.Characters.Add("candygirl");           // owned without an explicit count weighs 1
            SaveSummary s = SaveSync.Summarize(d);
            Assert.AreEqual(3, s.LevelsCleared);
            Assert.AreEqual(120, s.Gems);
            Assert.AreEqual(4500, s.Coins);
            Assert.AreEqual(3, s.HeroCopies);
            Assert.IsFalse(s.IsEmpty);
            Assert.IsTrue(SaveSync.Summarize(new SaveData()).IsEmpty);
        }

        [Test]
        public void BytesRoundTripWithHeader()
        {
            SaveData d = Played(4, 10, 20);
            d.AccountMode = SaveSync.ModeGoogle;
            d.AccountId = "p1";
            SaveEnvelope e = SaveSync.TryDeserialize(SaveSync.Serialize(d));
            Assert.IsNotNull(e);
            Assert.AreEqual(4, e.Summary.LevelsCleared);
            Assert.AreEqual(4, e.Data.Levels.Count);
            Assert.AreEqual("p1", e.Data.AccountId);
            Assert.IsTrue(SaveSync.IsReadable(e));
        }

        [Test]
        public void CorruptOrEmptyBytesAreNoSave()
        {
            Assert.IsNull(SaveSync.TryDeserialize(null));
            Assert.IsNull(SaveSync.TryDeserialize(new byte[0]));
            Assert.IsNull(SaveSync.TryDeserialize(Encoding.UTF8.GetBytes("not json {")));
            Assert.IsNull(SaveSync.TryDeserialize(Encoding.UTF8.GetBytes("{\"format\":1}")));
        }

        [Test]
        public void SaveFromANewerAppIsUnreadable()
        {
            SaveData d = Played(2, 0, 0);
            SaveEnvelope e = Env(d);
            e.Data.Version = SaveData.CurrentVersion + 1;
            Assert.IsFalse(SaveSync.IsReadable(e));
            Assert.AreEqual(SyncDecision.Nothing, SaveSync.Decide(Played(5, 1, 1), e, "p1"));
        }

        // ---- decision table ----

        [Test]
        public void FirstLinkWithoutCloudSaveUploadsTheGuestProgress()
        {
            Assert.AreEqual(SyncDecision.UploadLocal, SaveSync.Decide(Played(3, 5, 5), null, "p1"));
            Assert.AreEqual(SyncDecision.Nothing, SaveSync.Decide(new SaveData(), null, "p1"), "nothing to upload from a fresh device");
        }

        [Test]
        public void FreshDeviceTakesTheCloudSaveAndAnEmptyCloudTakesTheDevice()
        {
            Assert.AreEqual(SyncDecision.UseCloud, SaveSync.Decide(new SaveData(), Env(Played(6, 9, 9)), "p1"));
            Assert.AreEqual(SyncDecision.UploadLocal, SaveSync.Decide(Played(2, 1, 1), Env(new SaveData()), "p1"));
        }

        [Test]
        public void SameProgressNeedsNoChoice()
        {
            Assert.AreEqual(SyncDecision.Nothing, SaveSync.Decide(Played(3, 5, 5), Env(Played(3, 5, 5, "2026-10-01T00:00:00Z")), "p1"));
        }

        [Test]
        public void GuestWithProgressMeetingACloudSaveAsksThePlayer()
        {
            SaveData guest = Played(3, 5, 5);
            guest.AccountMode = SaveSync.ModeGuest;
            Assert.AreEqual(SyncDecision.AskPlayer, SaveSync.Decide(guest, Env(Played(8, 50, 500)), "p1"));
        }

        [Test]
        public void AnotherGoogleAccountThanBeforeAsks()
        {
            SaveData d = Played(3, 5, 5);
            d.AccountMode = SaveSync.ModeGoogle;
            d.AccountId = "old";
            d.LastCloudSyncUtc = "2026-11-01T00:00:00Z";
            Assert.AreEqual(SyncDecision.AskPlayer, SaveSync.Decide(d, Env(Played(8, 50, 500)), "new"));
        }

        [Test]
        public void LinkedAccountFollowsTheSideThatChangedSinceTheLastSync()
        {
            SaveData d = Played(3, 5, 5, "2026-11-01T10:00:00Z");
            d.AccountMode = SaveSync.ModeGoogle;
            d.AccountId = "p1";
            d.LastCloudSyncUtc = "2026-11-01T12:00:00Z";
            // only the cloud changed (played on another phone)
            Assert.AreEqual(SyncDecision.UseCloud, SaveSync.Decide(d, Env(Played(9, 5, 5), "2026-11-02T08:00:00Z"), "p1"));
            // only the device changed
            d.LastPlayedUtc = "2026-11-03T08:00:00Z";
            Assert.AreEqual(SyncDecision.UploadLocal, SaveSync.Decide(d, Env(Played(9, 5, 5), "2026-11-01T11:00:00Z"), "p1"));
            // both changed and differ
            Assert.AreEqual(SyncDecision.AskPlayer, SaveSync.Decide(d, Env(Played(9, 5, 5), "2026-11-02T08:00:00Z"), "p1"));
        }

        [Test]
        public void SaveTimeAndTextHelpers()
        {
            DateTime t;
            Assert.IsTrue(SaveSync.TryParseTime(SaveSync.NowText(), out t));
            Assert.IsFalse(SaveSync.TryParseTime("garbage", out t));
            StringAssert.Contains("2026", SaveSync.LastPlayedText(new SaveSummary { LastPlayedUtc = "2026-11-01T10:00:00Z" }));
            Assert.AreEqual("Played: unknown", SaveSync.LastPlayedText(new SaveSummary()));
            StringAssert.Contains("3 levels cleared", LoginScreen.SummaryText(new SaveSummary { LevelsCleared = 3 }));
        }

        // ---- save fields ----

        [Test]
        public void LoginIsAskedOnceAndSurvivesAProgressReset()
        {
            var p = new ProgressService(repo, store);
            Assert.IsTrue(LoginFlow.NeedsLogin(p));
            p.SetAccount(SaveSync.ModeGuest, "");
            Assert.IsFalse(LoginFlow.NeedsLogin(p));
            Assert.AreEqual(SaveSync.ModeGuest, p.Data.AccountMode);
            p.ResetAll();
            Assert.IsFalse(LoginFlow.NeedsLogin(p), "resetting progress keeps the login choice");
            var again = new ProgressService(repo, store);
            Assert.IsFalse(LoginFlow.NeedsLogin(again), "stored in the save file");
        }

        [Test]
        public void EveryChangeStampsThePlayTime()
        {
            var p = new ProgressService(repo, store);
            p.AddCoins(1);
            DateTime t;
            Assert.IsTrue(SaveSync.TryParseTime(p.Data.LastPlayedUtc, out t));
        }

        // ---- stub account ----

        [Test]
        public void StubSignsInAndKeepsACloudBlob()
        {
            SignInResult r = null;
            stub.SignIn(x => r = x);
            Assert.IsTrue(r.Success);
            Assert.AreEqual("Test Player", r.DisplayName);
            byte[] got = new byte[] { 1 };
            stub.LoadCloudSave(b => got = b);
            Assert.IsNull(got);
            Assert.IsFalse(stub.LastLoadFailed);
            bool ok = false;
            stub.SaveCloud(new byte[] { 7, 8 }, x => ok = x);
            Assert.IsTrue(ok);
            stub.LoadCloudSave(b => got = b);
            Assert.AreEqual(new byte[] { 7, 8 }, got);
            stub.SignOut();
            stub.LoadCloudSave(b => got = b);
            Assert.IsTrue(stub.LastLoadFailed, "signed out: not 'no save'");
        }

        [Test]
        public void UnavailableServiceOffersGuestOnly()
        {
            var u = new UnavailableAccountService();
            Assert.IsFalse(u.IsAvailable);
            SignInResult r = null;
            u.SignIn(x => r = x);
            Assert.AreEqual(SignInOutcome.Unavailable, r.Status);
        }

        // ---- sync glue ----

        ProgressService NewHooked()
        {
            var p = new ProgressService(repo, store);
            AccountSync.Hook(p);
            return p;
        }

        [Test]
        public void GuestSigningInWithEmptyCloudUploadsTheProgress()
        {
            ProgressService p = NewHooked();
            p.AddCoins(250);
            bool ok = false;
            AccountSync.SignInAndLink((o, m) => ok = o);
            Assert.IsTrue(ok);
            Assert.AreEqual(SaveSync.ModeGoogle, p.Data.AccountMode);
            Assert.AreEqual("stub-player-1", p.Data.AccountId);
            Assert.IsTrue(AccountSync.UploadPending);
            AccountSync.TickForTests();
            SaveEnvelope cloud = null;
            stub.LoadCloudSave(b => cloud = SaveSync.TryDeserialize(b));
            Assert.IsNotNull(cloud);
            Assert.AreEqual(250, cloud.Data.Coins);
            Assert.IsFalse(AccountSync.UploadPending);
            Assert.AreNotEqual("", p.Data.LastCloudSyncUtc);
        }

        [Test]
        public void FreshDeviceSigningInReceivesTheCloudProgress()
        {
            stub.SignIn(null);
            SaveData remote = Played(7, 40, 900);
            stub.SaveCloud(SaveSync.Serialize(remote), null);
            stub.SignOut();
            ProgressService p = NewHooked();
            bool ok = false;
            AccountSync.SignInAndLink((o, m) => ok = o);
            Assert.IsTrue(ok);
            Assert.AreEqual(900, p.Coins);
            Assert.AreEqual(40, p.Gems);
            Assert.AreEqual(7, SaveSync.Summarize(p.Data).LevelsCleared);
            Assert.AreEqual(SaveSync.ModeGoogle, p.Data.AccountMode);
            Assert.IsTrue(p.Data.LoginChosen);
        }

        [Test]
        public void ChangesAfterSyncAreUploadedOnceAfterTheDebounce()
        {
            ProgressService p = NewHooked();
            AccountSync.SignInAndLink((o, m) => { });
            p.AddGems(5);
            Assert.IsTrue(AccountSync.UploadPending);
            p.AddGems(5);
            AccountSync.TickForTests();                       // inside the debounce window: nothing is sent yet
            Assert.IsTrue(AccountSync.UploadPending);
            AccountSync.FlushNow();
            Assert.IsFalse(AccountSync.UploadPending);
            SaveEnvelope cloud = null;
            stub.LoadCloudSave(b => cloud = SaveSync.TryDeserialize(b));
            Assert.AreEqual(10, cloud.Data.Gems);
        }

        [Test]
        public void FailedUploadIsRetriedLaterNotLost()
        {
            ProgressService p = NewHooked();
            AccountSync.SignInAndLink((o, m) => { });
            AccountSync.TickForTests();
            stub.FailCloud = true;
            p.AddCoins(3);
            AccountSync.FlushNow();
            Assert.IsTrue(AccountSync.UploadPending, "kept for the retry");
        }

        [Test]
        public void SignOutGoesBackToGuestAndStopsUploads()
        {
            ProgressService p = NewHooked();
            AccountSync.SignInAndLink((o, m) => { });
            AccountSync.SignOut();
            Assert.AreEqual(SaveSync.ModeGuest, p.Data.AccountMode);
            Assert.IsFalse(AccountSync.IsSignedIn);
            p.AddCoins(1);
            Assert.IsFalse(AccountSync.UploadPending);
            StringAssert.Contains("Guest", AccountSync.AccountLine);
        }

        [Test]
        public void CancelledSignInChangesNothing()
        {
            ProgressService p = NewHooked();
            stub.CancelSignIn = true;
            bool ok = true;
            string msg = null;
            AccountSync.SignInAndLink((o, m) => { ok = o; msg = m; });
            Assert.IsFalse(ok);
            StringAssert.Contains("cancelled", msg);
            Assert.AreNotEqual(SaveSync.ModeGoogle, p.Data.AccountMode);
        }
    }
}
