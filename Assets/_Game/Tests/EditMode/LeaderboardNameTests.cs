using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Leaderboard;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>The player's own leaderboard display name (owner 2026-11-13): the rules class, the additive save field and the profile push of the hub.</summary>
    public class LeaderboardNameTests
    {
        ContentRepository repo;
        MemorySaveStore store;
        ProgressService progress;

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            store = new MemorySaveStore();
            progress = new ProgressService(repo, store, 5);
            LeaderboardHub.ResetForTests();
        }

        [TearDown]
        public void TearDown() { LeaderboardHub.ResetForTests(); }

        // ---------------- rules ----------------

        [Test]
        public void NormalizeTrimsAndCollapsesSpaces()
        {
            Assert.AreEqual("", LeaderboardNameRules.Normalize(null));
            Assert.AreEqual("", LeaderboardNameRules.Normalize("   "));
            Assert.AreEqual("Sky Fox", LeaderboardNameRules.Normalize("  Sky   Fox \t"));
            Assert.AreEqual("Sky_Fox", LeaderboardNameRules.Normalize("Sky_Fox"));
        }

        [TestCase("Sky Fox", LeaderboardNameCheck.Ok)]
        [TestCase("  Sky   Fox  ", LeaderboardNameCheck.Ok)]
        [TestCase("abc", LeaderboardNameCheck.Ok)]
        [TestCase("Momo_99", LeaderboardNameCheck.Ok)]
        [TestCase("Ōtto Müller", LeaderboardNameCheck.Ok)]
        [TestCase("小明同学", LeaderboardNameCheck.Ok)]
        [TestCase("ab", LeaderboardNameCheck.TooShort)]
        [TestCase("", LeaderboardNameCheck.TooShort)]
        [TestCase("  a  ", LeaderboardNameCheck.TooShort)]
        [TestCase("1234567890123456", LeaderboardNameCheck.Ok)]
        [TestCase("12345678901234567", LeaderboardNameCheck.TooLong)]
        [TestCase("Cool-Name", LeaderboardNameCheck.BadCharacters)]
        [TestCase("Cool.Name", LeaderboardNameCheck.BadCharacters)]
        [TestCase("Hi \U0001F600 you", LeaderboardNameCheck.BadCharacters)]
        public void ValidateGivesTheRightVerdict(string name, LeaderboardNameCheck expected)
        {
            Assert.AreEqual(expected, LeaderboardNameRules.Validate(name));
        }

        [TestCase("fuck")]
        [TestCase("FuCk You")]
        [TestCase("f u c k")]
        [TestCase("fuuuck")]
        [TestCase("5h1t")]
        [TestCase("b1tch_boy")]
        [TestCase("sex")]
        [TestCase("Big Ass")]
        [TestCase("n1gger")]
        public void RudeNamesAreBlocked(string name)
        {
            Assert.AreEqual(LeaderboardNameCheck.Blocked, LeaderboardNameRules.Validate(name), name);
        }

        [TestCase("Class")]
        [TestCase("Assassin")]
        [TestCase("Sussex")]
        [TestCase("Dickens")]
        [TestCase("Peacock")]
        [TestCase("Nigeria")]
        [TestCase("Momo Fan 7")]
        public void OrdinaryNamesThatContainShortBadWordsAreNotBlocked(string name)
        {
            Assert.AreEqual(LeaderboardNameCheck.Ok, LeaderboardNameRules.Validate(name), name);
        }

        [Test]
        public void ResolveUsesTheOwnNameElseTheFallback()
        {
            Assert.AreEqual("Sky Fox", LeaderboardNameRules.Resolve(" Sky  Fox", "Play Name"));
            Assert.AreEqual("Play Name", LeaderboardNameRules.Resolve("", "Play Name"));
            Assert.AreEqual("Play Name", LeaderboardNameRules.Resolve(null, "Play Name"));
            Assert.AreEqual("", LeaderboardNameRules.Resolve(null, null));
        }

        [Test]
        public void EveryVerdictExceptOkHasAMessage()
        {
            Assert.AreEqual("", LeaderboardNameRules.Message(LeaderboardNameCheck.Ok));
            foreach (LeaderboardNameCheck c in new[] { LeaderboardNameCheck.TooShort, LeaderboardNameCheck.TooLong, LeaderboardNameCheck.BadCharacters, LeaderboardNameCheck.Blocked })
                Assert.IsNotEmpty(LeaderboardNameRules.Message(c), c.ToString());
        }

        // ---------------- save field ----------------

        [Test]
        public void TheNameIsSavedAdditivelyAndEmptyGoesBackToNone()
        {
            Assert.AreEqual("", progress.LeaderboardDisplayName);
            Assert.IsTrue(progress.SetLeaderboardDisplayName("Sky Fox"));
            Assert.AreEqual("Sky Fox", new ProgressService(repo, store, 5).LeaderboardDisplayName, "saved at once");
            Assert.IsFalse(progress.SetLeaderboardDisplayName(new string('x', 40)), "the safety cap");
            Assert.AreEqual("Sky Fox", progress.LeaderboardDisplayName);
            Assert.IsTrue(progress.SetLeaderboardDisplayName(""));
            Assert.AreEqual("", new ProgressService(repo, store, 5).LeaderboardDisplayName);
            Assert.IsNull(progress.Data.LeaderboardDisplayName, "stored as null, so the key is not written");
        }

        [Test]
        public void TheFieldIsAdditiveAndTravelsInTheCloudSave()
        {
            SaveEnvelope env = SaveSync.TryDeserialize(SaveSync.Serialize(progress.Data));
            Assert.IsNotNull(env);
            Assert.IsNull(env.Data.LeaderboardDisplayName, "an old save without the field loads with no name");
            progress.SetLeaderboardDisplayName("Sky Fox");
            Assert.AreEqual("Sky Fox", SaveSync.TryDeserialize(SaveSync.Serialize(progress.Data)).Data.LeaderboardDisplayName);
        }

        // ---------------- hub ----------------

        void GoLive(FakeLbTransport net, StubLeaderboardAuth auth, bool consent)
        {
            LeaderboardHub.Setup(progress, new LeaderboardConfig { Enabled = true, BaseUrl = "https://lb.test" }, auth, net);
            if (consent) progress.GrantLeaderboardConsent(1);
            net.Handler = (m, u, b) => new HttpReply { Status = 200, Body = "{}" };
        }

        /// <summary>Body of the latest profile write (later calls such as the my-week read have no body of interest).</summary>
        static string LastCommit(FakeLbTransport net)
        {
            for (int i = net.Calls.Count - 1; i >= 0; i--) if (net.Calls[i].Contains("documents:commit")) return net.Bodies[i];
            return "";
        }

        [Test]
        public void TheProfileCarriesTheOwnNameElseThePlayGamesNameAndIsPushedAgainOnAChange()
        {
            var net = new FakeLbTransport();
            var auth = new StubLeaderboardAuth { ProfileValue = new LbProfile { DisplayName = "Play Name", AvatarUrl = "" } };
            GoLive(net, auth, true);
            LeaderboardHub.SignIn(null);
            Assert.AreEqual(1, net.CountOf("documents:commit"));
            StringAssert.Contains("Play Name", LastCommit(net));

            Assert.AreEqual(LeaderboardNameCheck.Ok, LeaderboardHub.SetDisplayName("  Sky   Fox "));
            Assert.AreEqual("Sky Fox", progress.LeaderboardDisplayName);
            Assert.AreEqual(2, net.CountOf("documents:commit"), "the new name is pushed");
            StringAssert.Contains("Sky Fox", LastCommit(net));

            Assert.AreEqual(LeaderboardNameCheck.Ok, LeaderboardHub.SetDisplayName("Sky Fox"));
            Assert.AreEqual(2, net.CountOf("documents:commit"), "an unchanged name is not pushed again");

            Assert.AreEqual(LeaderboardNameCheck.Ok, LeaderboardHub.SetDisplayName(""));
            Assert.AreEqual("", progress.LeaderboardDisplayName);
            Assert.AreEqual(3, net.CountOf("documents:commit"));
            StringAssert.Contains("Play Name", LastCommit(net), "cleared: back to the Play Games name");
        }

        [Test]
        public void ABadNameIsRefusedAndNothingIsStoredOrSent()
        {
            var net = new FakeLbTransport();
            var auth = new StubLeaderboardAuth();
            GoLive(net, auth, true);
            LeaderboardHub.SignIn(null);
            int sent = net.Calls.Count;
            Assert.AreEqual(LeaderboardNameCheck.TooShort, LeaderboardHub.SetDisplayName("ab"));
            Assert.AreEqual(LeaderboardNameCheck.BadCharacters, LeaderboardHub.SetDisplayName("a#b c"));
            Assert.AreEqual(LeaderboardNameCheck.Blocked, LeaderboardHub.SetDisplayName("fuck"));
            Assert.AreEqual("", progress.LeaderboardDisplayName);
            Assert.AreEqual(sent, net.Calls.Count);
        }

        [Test]
        public void WithoutConsentTheNameIsStoredButNothingIsSent()
        {
            var net = new FakeLbTransport();
            var auth = new StubLeaderboardAuth();
            GoLive(net, auth, false);
            auth.SignIn(null);
            Assert.AreEqual(LeaderboardNameCheck.Ok, LeaderboardHub.SetDisplayName("Sky Fox"));
            Assert.AreEqual("Sky Fox", progress.LeaderboardDisplayName);
            Assert.AreEqual(0, net.Calls.Count, "no consent: nothing is sent");
            Assert.AreEqual("Sky Fox", LeaderboardHub.ShownName);
        }
    }
}
