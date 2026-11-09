using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Monetization;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>The pure parts of the Account pop-up and the Home bar math (owner issue 35.3, docs/35 section 8).</summary>
    public class AccountPopupTests
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

        ProgressService NewHooked()
        {
            var p = new ProgressService(repo, store);
            AccountSync.Hook(p);
            return p;
        }

        [Test]
        public void StateFollowsLinkAndAvailability()
        {
            Assert.AreEqual(AccountPopup.State.Guest, AccountPopup.StateFor(false, true));
            Assert.AreEqual(AccountPopup.State.Unavailable, AccountPopup.StateFor(false, false));
            Assert.AreEqual(AccountPopup.State.SignedIn, AccountPopup.StateFor(true, true));
            Assert.AreEqual(AccountPopup.State.SignedIn, AccountPopup.StateFor(true, false), "a linked account stays signed in even if the plugin is missing");
        }

        [Test]
        public void ButtonLetterIsGOrTheInitial()
        {
            Assert.AreEqual("G", AccountPopup.ButtonLetter(AccountPopup.State.Guest, "Test Player"));
            Assert.AreEqual("G", AccountPopup.ButtonLetter(AccountPopup.State.Unavailable, ""));
            Assert.AreEqual("T", AccountPopup.ButtonLetter(AccountPopup.State.SignedIn, "Test Player"));
            Assert.AreEqual("A", AccountPopup.ButtonLetter(AccountPopup.State.SignedIn, "  ana"));
            Assert.AreEqual("G", AccountPopup.ButtonLetter(AccountPopup.State.SignedIn, ""), "offline link without a name");
            Assert.AreEqual("", AccountPopup.InitialOf(null));
            Assert.AreEqual("", AccountPopup.InitialOf("..."));
        }

        [Test]
        public void LabelsAndTextsPerState()
        {
            Assert.AreEqual("Account", AccountPopup.ButtonA11y(AccountPopup.State.Guest));
            Assert.AreEqual("Account, signed in", AccountPopup.ButtonA11y(AccountPopup.State.SignedIn));
            Assert.AreEqual("Sign in with Google", AccountPopup.SettingsButtonLabel(AccountPopup.State.Guest));
            Assert.AreEqual("Unlink Google Account", AccountPopup.SettingsButtonLabel(AccountPopup.State.SignedIn));
            Assert.AreEqual("", AccountPopup.SettingsButtonLabel(AccountPopup.State.Unavailable));
            Assert.AreEqual("Google sign-in is not available in this build.", AccountPopup.HintFor(AccountPopup.State.Unavailable));
            Assert.AreEqual("Sign-in cancelled.", AccountPopup.ResultText(false, "Sign-in cancelled."));
            Assert.AreEqual("Signed in.", AccountPopup.ResultText(true, null));
            Assert.AreNotEqual("", AccountPopup.ResultText(false, null));
        }

        [Test]
        public void GuestSignInThenSignOutThroughRunPrimary()
        {
            ProgressService p = NewHooked();
            Assert.AreEqual(AccountPopup.State.Guest, AccountPopup.Current);
            StringAssert.Contains("Guest", AccountSync.AccountLine);
            bool ok = false; string text = null;
            AccountPopup.RunPrimary(AccountPopup.State.Guest, (o, t) => { ok = o; text = t; });
            Assert.IsTrue(ok);
            Assert.AreEqual("Signed in.", text);
            Assert.AreEqual(AccountPopup.State.SignedIn, AccountPopup.Current);
            Assert.AreEqual("Signed in as Test Player", AccountSync.AccountLine);
            AccountPopup.RunPrimary(AccountPopup.State.SignedIn, (o, t) => { ok = o; text = t; });
            Assert.IsTrue(ok);
            Assert.AreEqual(AccountPopup.SignedOutText, text);
            Assert.AreEqual(AccountPopup.State.Guest, AccountPopup.Current);
            Assert.AreEqual(SaveSync.ModeGuest, p.Data.AccountMode);
        }

        [Test]
        public void CancelledSignInReportsAMessageAndStaysGuest()
        {
            NewHooked();
            stub.CancelSignIn = true;
            bool ok = true; string text = null;
            AccountPopup.RunPrimary(AccountPopup.State.Guest, (o, t) => { ok = o; text = t; });
            Assert.IsFalse(ok);
            Assert.AreEqual("Sign-in cancelled.", text);
            Assert.AreEqual(AccountPopup.State.Guest, AccountPopup.Current);
        }

        [Test]
        public void UnavailableBuildOffersNothingToRun()
        {
            Services.Account = new UnavailableAccountService();
            NewHooked();
            Assert.AreEqual(AccountPopup.State.Unavailable, AccountPopup.Current);
            bool ok = true; string text = null;
            AccountPopup.RunPrimary(AccountPopup.State.Unavailable, (o, t) => { ok = o; text = t; });
            Assert.IsFalse(ok);
            Assert.AreEqual(AccountPopup.UnavailableText, text);
        }

        [Test]
        public void SyncNowUploadsWhenSignedIn()
        {
            ProgressService p = NewHooked();
            AccountSync.SignInAndLink((o, m) => { });
            AccountSync.TickForTests();
            p.AddCoins(77);
            bool ok = false; string text = null;
            AccountSync.SyncNow((o, t) => { ok = o; text = t; });
            Assert.IsTrue(ok);
            Assert.AreEqual("Synced.", text);
            SaveEnvelope cloud = null;
            stub.LoadCloudSave(b => cloud = SaveSync.TryDeserialize(b));
            Assert.AreEqual(77, cloud.Data.Coins);
            Assert.IsFalse(AccountSync.UploadPending);
        }

        [Test]
        public void SyncNowFailureKeepsTheUploadForARetry()
        {
            ProgressService p = NewHooked();
            AccountSync.SignInAndLink((o, m) => { });
            AccountSync.TickForTests();
            stub.FailCloud = true;
            p.AddCoins(5);
            bool ok = true; string text = null;
            AccountSync.SyncNow((o, t) => { ok = o; text = t; });
            Assert.IsFalse(ok);
            StringAssert.Contains("tried again", text);
            Assert.IsTrue(AccountSync.UploadPending);
        }

        [Test]
        public void SyncNowAsAGuestSaysSignInFirst()
        {
            NewHooked();
            bool ok = true; string text = null;
            AccountSync.SyncNow((o, t) => { ok = o; text = t; });
            Assert.IsFalse(ok);
            Assert.AreEqual("Sign in first.", text);
        }

        [Test]
        public void SyncNowWhenTheAccountDroppedSignsBackInAndCompares()
        {
            ProgressService p = NewHooked();
            AccountSync.SignInAndLink((o, m) => { });
            AccountSync.TickForTests();
            stub.SignOut();                           // the Google session ended, the save is still linked
            bool ok = false; string text = null;
            AccountSync.SyncNow((o, t) => { ok = o; text = t; });   // the stub signs in again quietly only for a player who signed in before
            Assert.IsNotNull(text, "the callback always runs");
            Assert.IsFalse(ok, "the memory stub cannot sign in quietly after a sign-out");
            StringAssert.Contains("connection", text);
        }

        // ---- Settings > Save Progress (Bug List 1 B2.a) ----

        /// <summary>A save store that counts how often the save was written.</summary>
        sealed class CountingStore : ISaveStore
        {
            public int Saves;
            public SaveData Load() { return new SaveData(); }
            public void Save(SaveData data) { Saves++; }
        }

        [Test]
        public void GuestSaveProgressWritesTheSaveOnThePhoneAndNeverTouchesTheCloud()
        {
            var counting = new CountingStore();
            var p = new ProgressService(repo, counting);
            AccountSync.Hook(p);
            p.AddCoins(5);
            int before = counting.Saves;
            bool ok = false; string text = null;
            AccountPopup.SaveProgress(p, (o, t) => { ok = o; text = t; });
            Assert.IsTrue(ok);
            Assert.AreEqual("Saved on this phone", text);
            Assert.AreEqual(AccountPopup.SavedLocalText, text);
            Assert.AreEqual(before + 1, counting.Saves, "the save is written now");
            SaveEnvelope cloud = null;
            stub.LoadCloudSave(b => cloud = SaveSync.TryDeserialize(b));
            Assert.IsNull(cloud, "a guest save never goes to the cloud");
        }

        [Test]
        public void SignedInSaveProgressPushesTheSaveToGoogle()
        {
            var counting = new CountingStore();
            var p = new ProgressService(repo, counting);
            AccountSync.Hook(p);
            AccountSync.SignInAndLink((o, m) => { });
            AccountSync.TickForTests();
            p.AddCoins(77);
            int before = counting.Saves;
            bool ok = false; string text = null;
            AccountPopup.SaveProgress(p, (o, t) => { ok = o; text = t; });
            Assert.IsTrue(ok);
            Assert.AreEqual("Saved to Google", text);
            Assert.Greater(counting.Saves, before, "the phone copy is written too");
            SaveEnvelope cloud = null;
            stub.LoadCloudSave(b => cloud = SaveSync.TryDeserialize(b));
            Assert.IsNotNull(cloud, "the cloud stub received the save");
            Assert.AreEqual(77, cloud.Data.Coins);
        }

        [Test]
        public void SignedInSaveProgressFailureStillSavesOnThePhone()
        {
            var counting = new CountingStore();
            var p = new ProgressService(repo, counting);
            AccountSync.Hook(p);
            AccountSync.SignInAndLink((o, m) => { });
            AccountSync.TickForTests();
            stub.FailCloud = true;
            p.AddCoins(5);
            int before = counting.Saves;
            bool ok = true; string text = null;
            AccountPopup.SaveProgress(p, (o, t) => { ok = o; text = t; });
            Assert.IsFalse(ok);
            Assert.AreEqual(AccountPopup.SavedLocalOnlyText, text);
            Assert.Greater(counting.Saves, before);
        }

        [Test]
        public void SaveProgressWithoutProgressSaysNothingToSave()
        {
            bool ok = true; string text = null;
            AccountPopup.SaveProgress(null, (o, t) => { ok = o; text = t; });
            Assert.IsFalse(ok);
            Assert.AreEqual(AccountPopup.NothingToSaveText, text);
        }

        [Test]
        public void SaveResultTextsAreShortSoTheyFitOneLineOnTheButton()
        {
            foreach (string s in new[] { AccountPopup.SavedLocalText, AccountPopup.SavedGoogleText, AccountPopup.SavedLocalOnlyText, AccountPopup.NothingToSaveText })
                Assert.LessOrEqual(s.Length, 24, s);
            Assert.AreEqual("Save Progress", AccountPopup.SaveLabel);
        }

        [Test]
        public void WidePlayButtonIsTwoIconWidthsWithTheGapBetween()
        {
            // bar 1000 wide, four slots, gap 12: an icon button is 250 - 12 = 238, PLAY (2 slots) is 500 - 12 = 488 = 238 + 12 + 238
            float icon = ToyKit.BarItemWidth(1000f, 4, 1, 12f);
            float play = ToyKit.BarItemWidth(1000f, 4, 2, 12f);
            Assert.AreEqual(238f, icon, 0.001f);
            Assert.AreEqual(2f * icon + 12f, play, 0.001f);
        }
    }
}
