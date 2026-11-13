using System;
using System.Collections.Generic;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Leaderboard;
using NUnit.Framework;
using UnityEngine;

namespace FluffyClimbers.Tests
{
    /// <summary>A scripted transport: every call is logged and answered by the handler (no handler = offline).</summary>
    sealed class FakeLbTransport : ILeaderboardTransport
    {
        public readonly List<string> Calls = new List<string>();
        public readonly List<string> Bodies = new List<string>();
        public readonly List<string> Bearers = new List<string>();
        public Func<string, string, string, HttpReply> Handler;

        public void Send(string method, string url, string bearer, string jsonBody, Action<HttpReply> done)
        {
            Calls.Add(method + " " + url);
            Bodies.Add(jsonBody ?? "");
            Bearers.Add(bearer ?? "");
            done(Handler != null ? Handler(method, url, jsonBody ?? "") : new HttpReply { NetworkError = true });
        }

        public void GetTexture(string url, Action<Texture2D> done) { done(null); }

        public int CountOf(string fragment) { int n = 0; foreach (string c in Calls) if (c.Contains(fragment)) n++; return n; }
    }

    /// <summary>
    /// Weekly leaderboard client logic (Planning/LEADERBOARD-DESIGN.md): the India-time week maths, the one-time consent, the persisted upload queue with backoff, the idempotent prize claim, the seed tickets,
    /// the parsers of the service and Firestore answers, and the switched-off default. Everything runs against a stub auth and a fake transport: no network, no Firebase.
    /// </summary>
    public class LeaderboardTests
    {
        ContentRepository repo;
        MemorySaveStore store;
        ProgressService progress;
        FakeLbTransport net;
        StubLeaderboardAuth auth;
        DateTime now;

        static readonly DateTime T0 = new DateTime(2026, 11, 12, 11, 0, 0, DateTimeKind.Utc);   // Thursday 16:30 India time

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            store = new MemorySaveStore();
            progress = new ProgressService(repo, store, 5);
            net = new FakeLbTransport();
            auth = new StubLeaderboardAuth();
            now = T0;
            LeaderboardHub.ResetForTests();
            LeaderboardHub.Clock = () => now;
        }

        [TearDown]
        public void TearDown()
        {
            LeaderboardHub.ResetForTests();
            LeaderboardHub.AccountModeIsGoogle = null;
            AvatarCache.Loader = null;
            AvatarCache.Clear();
        }

        void GoLive(bool consent = true, bool signedIn = true)
        {
            LeaderboardHub.Setup(progress, new LeaderboardConfig { Enabled = true, BaseUrl = "https://lb.test" }, auth, net);
            if (consent) progress.GrantLeaderboardConsent(LeaderboardTime.ToUnix(now));
            if (signedIn) auth.SignIn(null);
        }

        static QueuedRun Run(string level, long crown, int mode = 2, long expires = long.MaxValue / 4)
        {
            return new QueuedRun
            {
                RunId = Guid.NewGuid().ToString("N"), Ticket = "T-" + level, Kind = "level", LevelId = level, Mode = mode, Seed = 77, HeroId = "momo", GameVersion = "0.59.1",
                InputsB64 = "AQID", ReviveAt = new[] { 120 }, Floors = 30, Ticks = 1800, Score = 400, Crown = crown, ExpiresAtUnix = expires
            };
        }

        static HttpReply Json(string body, int status = 200) { return new HttpReply { Status = status, Body = body }; }

        // ---------------- week maths (India time) ----------------

        [Test]
        public void TheWeekRunsFromMondayMidnightIndiaTimeAndTheCountdownIsRight()
        {
            Assert.AreEqual("2026-11-09", LeaderboardTime.WeekKey(T0), "Thursday 11:00 UTC is Thursday 16:30 in India: the week of Monday 9 November");
            Assert.AreEqual(new DateTime(2026, 11, 15, 18, 30, 0, DateTimeKind.Utc), LeaderboardTime.WeekEndUtc(T0), "the week ends Sunday 18:30 UTC = Monday 00:00 India time");
            Assert.AreEqual(new TimeSpan(3, 7, 30, 0), LeaderboardTime.Remaining(T0));
            Assert.AreEqual("3d 07:30", LeaderboardTime.FormatCountdown(LeaderboardTime.Remaining(T0)));

            var lastSecond = new DateTime(2026, 11, 15, 18, 29, 59, DateTimeKind.Utc);
            Assert.AreEqual("2026-11-09", LeaderboardTime.WeekKey(lastSecond));
            Assert.AreEqual(TimeSpan.FromSeconds(1), LeaderboardTime.Remaining(lastSecond));
            Assert.AreEqual("00:00:01", LeaderboardTime.FormatCountdown(LeaderboardTime.Remaining(lastSecond)));

            var rollover = new DateTime(2026, 11, 15, 18, 30, 0, DateTimeKind.Utc);
            Assert.AreEqual("2026-11-16", LeaderboardTime.WeekKey(rollover), "at 18:30 UTC on Sunday the next week starts");
            Assert.AreEqual(TimeSpan.FromDays(7), LeaderboardTime.Remaining(rollover));

            var mondayEarly = new DateTime(2026, 11, 8, 18, 30, 0, DateTimeKind.Utc);   // Monday 00:00 India time exactly
            Assert.AreEqual("2026-11-09", LeaderboardTime.WeekKey(mondayEarly));
            Assert.AreEqual("2026-11-02", LeaderboardTime.WeekKey(mondayEarly.AddSeconds(-1)), "one second earlier is still last week");
            Assert.AreEqual("01:02:03", LeaderboardTime.FormatCountdown(new TimeSpan(1, 2, 3)));
        }

        [Test]
        public void TheClientWeekMatchesTheServerWeekKeyFormat()
        {
            // the service writes the Monday date in India time ("2026-11-09"); the client builds the board address from the same key
            Assert.AreEqual("https://firestore.googleapis.com/v1/projects/fluffy-climbers-tower-jump/databases/(default)/documents/weeks/2026-11-09/boards/overall", LeaderboardConfig.BoardDocUrl("2026-11-09", "overall"));
        }

        // ---------------- switched off by default ----------------

        [Test]
        public void TheFeatureIsOffWithoutTheRemoteConfigSectionAndWithAPlaceholderUrl()
        {
            Assert.IsFalse(LeaderboardConfig.From(null).IsLive);
            Assert.IsFalse(LeaderboardConfig.From(repo.RemoteConfig).IsLive, "the shipped remote config has no leaderboard section");
            var on = new RemoteConfigDef { Leaderboard = new LeaderboardConfigDef { Enabled = true, BaseUrl = "REPLACE_WITH_CLOUD_RUN_URL" } };
            Assert.IsFalse(LeaderboardConfig.From(on).IsLive, "a placeholder URL counts as off");
            on.Leaderboard.BaseUrl = "https://leaderboard-abc-el.a.run.app/";
            LeaderboardConfig c = LeaderboardConfig.From(on);
            Assert.IsTrue(c.IsLive);
            Assert.AreEqual("https://leaderboard-abc-el.a.run.app", c.BaseUrl, "no trailing slash");
            on.Leaderboard.Enabled = false;
            Assert.IsFalse(LeaderboardConfig.From(on).IsLive, "the switch wins over a good URL");

            LeaderboardHub.Setup(progress, LeaderboardConfig.Off, auth, net);
            auth.SignIn(null);
            progress.GrantLeaderboardConsent(1);
            bool answered = false;
            LeaderboardHub.RequestTicket("level", "lvl_001_01", 2, t => answered = t == null);
            LeaderboardHub.Submit(Run("lvl_001_01", 100));
            Assert.IsTrue(answered);
            Assert.AreEqual(0, net.Calls.Count, "switched off: nothing is ever called");
            Assert.IsFalse(LeaderboardHub.Enabled);
            Assert.AreEqual(LeaderboardScreen.State.ComingSoon, LeaderboardScreen.StateFor(LeaderboardHub.Enabled, true, true, true));
        }

        [Test]
        public void ScreenStatesFollowLiveSignInAndConsent()
        {
            Assert.AreEqual(LeaderboardScreen.State.ComingSoon, LeaderboardScreen.StateFor(false, true, true, true));
            Assert.AreEqual(LeaderboardScreen.State.SignIn, LeaderboardScreen.StateFor(true, false, false, false), "a guest sees the sign-in prompt");
            Assert.AreEqual(LeaderboardScreen.State.SignIn, LeaderboardScreen.StateFor(true, true, false, true), "Google but no Firebase user yet: connect");
            Assert.AreEqual(LeaderboardScreen.State.Consent, LeaderboardScreen.StateFor(true, true, true, false));
            Assert.AreEqual(LeaderboardScreen.State.Ready, LeaderboardScreen.StateFor(true, true, true, true));
        }

        // ---------------- consent ----------------

        [Test]
        public void ConsentIsStoredOnceAskedAgainOnlyTomorrowAndKeptInTheSave()
        {
            Assert.IsFalse(progress.HasLeaderboardConsent);
            Assert.IsTrue(progress.ShouldAskLeaderboardConsent("20261112"));
            progress.DeclineLeaderboardConsent("20261112");
            Assert.IsFalse(progress.HasLeaderboardConsent, "Not now stores no consent");
            Assert.IsFalse(progress.ShouldAskLeaderboardConsent("20261112"), "not asked again the same day");
            Assert.IsTrue(progress.ShouldAskLeaderboardConsent("20261113"), "asked again the next day");

            progress.GrantLeaderboardConsent(1762945200);
            Assert.IsTrue(progress.HasLeaderboardConsent);
            Assert.IsFalse(progress.ShouldAskLeaderboardConsent("20261201"));
            var again = new ProgressService(repo, store, 5);
            Assert.IsTrue(again.HasLeaderboardConsent, "the flag is in the save: leaderboardConsent { v, at }");
            Assert.AreEqual(LeaderboardConsent.CurrentVersion, again.Data.LeaderboardConsent.V);
            Assert.AreEqual(1762945200L, again.Data.LeaderboardConsent.At);

            again.Data.LeaderboardConsent.V = LeaderboardConsent.CurrentVersion - 1;
            Assert.IsFalse(again.HasLeaderboardConsent, "an older consent text asks again");

            progress.RevokeLeaderboardConsent();
            Assert.IsFalse(progress.HasLeaderboardConsent, "Leave leaderboards clears the flag");
        }

        [Test]
        public void NothingIsQueuedSentOrTicketedWithoutConsent()
        {
            GoLive(false);
            net.Handler = (m, u, b) => Json("{}");
            LeaderboardHub.Submit(Run("lvl_001_01", 100));
            bool answered = false;
            LeaderboardHub.RequestTicket("level", "lvl_001_01", 2, t => answered = true);
            Assert.IsTrue(answered);
            Assert.AreEqual(0, net.Calls.Count, "no start-run and no submit-run before the player said yes");
            Assert.AreEqual(0, progress.LeaderboardQueue.Count);
            Assert.IsFalse(LeaderboardHub.CanUpload);
            Assert.IsFalse(LeaderboardHub.WantsTicket);

            LeaderboardHub.AcceptConsent();
            Assert.IsTrue(LeaderboardHub.CanUpload);
            Assert.IsTrue(progress.HasLeaderboardConsent);
        }

        // ---------------- upload queue ----------------

        [Test]
        public void ARunIsUploadedAtOnceWithTheTicketAndRemovedWhenAccepted()
        {
            GoLive();
            net.Handler = (m, u, b) => Json("{\"accepted\":true,\"counted\":true}");
            LeaderboardHub.Submit(Run("lvl_001_03", 321));
            Assert.AreEqual(1, net.CountOf("POST https://lb.test/submit-run"));
            StringAssert.Contains("\"ticket\":\"T-lvl_001_03\"", net.Bodies[0]);
            StringAssert.Contains("\"levelId\":\"lvl_001_03\"", net.Bodies[0]);
            StringAssert.Contains("\"reviveAt\":[120]", net.Bodies[0], "revives travel with the run");
            StringAssert.Contains("\"crown\":321", net.Bodies[0]);
            StringAssert.DoesNotContain("regionId", net.Bodies[0]);
            Assert.AreEqual("stub-token", net.Bearers[0], "the Firebase ID token is the bearer");
            Assert.AreEqual(0, progress.LeaderboardQueue.Count, "an accepted run leaves the queue");
        }

        [Test]
        public void AnOfflineRunStaysInTheSaveAndRetriesWithBackoff()
        {
            GoLive();
            net.Handler = null;   // offline
            LeaderboardHub.Submit(Run("lvl_001_01", 100));
            Assert.AreEqual(1, net.Calls.Count);
            Assert.AreEqual(1, progress.LeaderboardQueue.Count, "kept for later");
            Assert.AreEqual(1, new ProgressService(repo, store, 5).LeaderboardQueue.Count, "the queue is persisted in the save");

            LeaderboardHub.Pump();
            Assert.AreEqual(1, net.Calls.Count, "inside the 30 s backoff nothing is sent");
            now = now.AddSeconds(31);
            LeaderboardHub.Pump();
            Assert.AreEqual(2, net.Calls.Count, "after the backoff it tries again");
            now = now.AddSeconds(40);
            LeaderboardHub.Pump();
            Assert.AreEqual(2, net.Calls.Count, "the second wait is 60 s");
            now = now.AddSeconds(30);
            net.Handler = (m, u, b) => Json("{\"accepted\":true}");
            LeaderboardHub.Pump();
            Assert.AreEqual(3, net.Calls.Count);
            Assert.AreEqual(0, progress.LeaderboardQueue.Count, "sent once the network is back");
        }

        [Test]
        public void BackoffDoublesFromThirtySecondsToThirtyMinutes()
        {
            long[] expect = { 0, 30, 60, 120, 240, 480, 960, 1800, 1800 };
            for (int i = 0; i < expect.Length; i++) Assert.AreEqual(expect[i], ProgressService.BackoffSeconds(i), "failure " + i);
        }

        [Test]
        public void AFinalRefusalDropsTheRunAndATransientOneKeepsIt()
        {
            GoLive();
            foreach (int status in new[] { 400, 403, 409, 410, 422, 426 })
            {
                net.Handler = (m, u, b) => Json("{\"error\":\"x\"}", status);
                LeaderboardHub.Submit(Run("lvl_001_0" + (status % 9 + 1), 500 + status));
                Assert.AreEqual(0, progress.LeaderboardQueue.Count, "HTTP " + status + " is final: never retried");
            }
            foreach (int status in new[] { 401, 429, 500, 503 })
            {
                while (progress.LeaderboardQueue.Count > 0) progress.RemoveLeaderboardRun(progress.LeaderboardQueue[0].RunId);
                net.Handler = (m, u, b) => Json("{\"error\":\"x\"}", status);
                LeaderboardHub.Submit(Run("lvl_002_01", 900));
                Assert.AreEqual(1, progress.LeaderboardQueue.Count, "HTTP " + status + " is retried later");
                Assert.AreEqual(1, progress.LeaderboardQueue[0].Tries);
            }
        }

        [Test]
        public void ARunIsDroppedAfterTwelveFailuresAndWhenItsTicketExpired()
        {
            GoLive();
            net.Handler = null;
            LeaderboardHub.Submit(Run("lvl_001_01", 100));
            for (int i = 0; i < 20 && progress.LeaderboardQueue.Count > 0; i++) { now = now.AddHours(1); LeaderboardHub.Pump(); }
            Assert.AreEqual(0, progress.LeaderboardQueue.Count, "never retried for ever");

            long soon = LeaderboardTime.ToUnix(now) + 100;
            Assert.IsTrue(progress.EnqueueLeaderboardRun(Run("lvl_001_02", 100, 2, soon)));
            now = now.AddSeconds(200);
            Assert.IsNull(progress.NextDueLeaderboardRun(LeaderboardTime.ToUnix(now)), "an expired ticket is dropped, not sent");
            Assert.AreEqual(0, progress.LeaderboardQueue.Count);
        }

        [Test]
        public void TheQueueIsBoundedAndKeepsOnlyTheBestRunOfASlot()
        {
            GoLive();
            for (int i = 1; i <= 20; i++) Assert.IsTrue(progress.EnqueueLeaderboardRun(Run("lvl_001_" + i.ToString("D2"), 100 + i)));
            Assert.AreEqual(ProgressService.LeaderboardMaxQueue, progress.LeaderboardQueue.Count, "bounded: the oldest runs go first");
            Assert.AreEqual("lvl_001_20", progress.LeaderboardQueue[progress.LeaderboardQueue.Count - 1].LevelId);

            Assert.IsFalse(progress.EnqueueLeaderboardRun(Run("lvl_001_20", 100)), "a lower crown in the same slot adds nothing");
            Assert.IsTrue(progress.EnqueueLeaderboardRun(Run("lvl_001_20", 9999)), "a higher crown replaces the queued run");
            int same = 0;
            foreach (QueuedRun q in progress.LeaderboardQueue) if (q.LevelId == "lvl_001_20" && q.Mode == 2) same++;
            Assert.AreEqual(1, same);
            Assert.IsTrue(progress.EnqueueLeaderboardRun(Run("lvl_001_20", 5, 3)), "another Gameplay setting is another slot");

            var huge = Run("lvl_002_01", 1);
            huge.InputsB64 = new string('A', ProgressService.LeaderboardMaxRunChars + 1);
            Assert.IsFalse(progress.EnqueueLeaderboardRun(huge), "a very long run is not kept in the save");
        }

        [Test]
        public void ANewIdTokenIsFetchedOnceAfterA401()
        {
            GoLive();
            int calls = 0;
            net.Handler = (m, u, b) => ++calls == 1 ? Json("{\"error\":\"unauthorized\"}", 401) : Json("{\"accepted\":true}");
            LeaderboardHub.Submit(Run("lvl_001_01", 100));
            Assert.AreEqual(2, net.Calls.Count, "the same call is repeated once with a fresh token");
            Assert.AreEqual(1, auth.RefreshCalls);
            Assert.AreEqual(0, progress.LeaderboardQueue.Count);
        }

        // ---------------- seed tickets ----------------

        const string TicketJson = "{\"ticket\":\"abc.def\",\"seed\":123456,\"expiresAt\":\"2026-11-12T13:00:00Z\"}";

        [Test]
        public void ATicketCarriesTheServerSeedAndIsSingleUse()
        {
            GoLive();
            net.Handler = (m, u, b) => u.EndsWith("/start-run") ? Json(TicketJson) : Json("{}");
            StartTicket got = null;
            LeaderboardHub.RequestTicket("level", "lvl_001_05", 2, t => got = t);
            Assert.IsNotNull(got);
            Assert.AreEqual(123456, got.Seed);
            StringAssert.Contains("\"levelId\":\"lvl_001_05\"", net.Bodies[0]);
            StringAssert.Contains("\"mode\":2", net.Bodies[0]);
            Assert.IsTrue(LeaderboardHub.HasTicket("level", "lvl_001_05", 2));
            Assert.IsNull(LeaderboardHub.TryTakeTicket("level", "lvl_001_05", 3), "bound to the Gameplay setting");
            StartTicket taken = LeaderboardHub.TryTakeTicket("level", "lvl_001_05", 2);
            Assert.AreEqual("abc.def", taken.Ticket);
            Assert.IsNull(LeaderboardHub.TryTakeTicket("level", "lvl_001_05", 2), "a ticket is used once");

            LeaderboardHub.RequestTicket("endless", "region_001_x", 0, null);
            StringAssert.Contains("\"regionId\":\"region_001_x\"", net.Bodies[net.Bodies.Count - 1]);
            StringAssert.DoesNotContain("levelId", net.Bodies[net.Bodies.Count - 1]);
        }

        [Test]
        public void AnExpiredOrFailedTicketMeansALocalSeedAndNoUpload()
        {
            GoLive();
            net.Handler = (m, u, b) => Json(TicketJson);
            LeaderboardHub.RequestTicket("level", "lvl_001_05", 2, null);
            now = new DateTime(2026, 11, 12, 12, 59, 30, DateTimeKind.Utc);   // 30 s before it expires: inside the 60 s margin
            Assert.IsNull(LeaderboardHub.TryTakeTicket("level", "lvl_001_05", 2));

            net.Handler = null;   // offline: the call fails, the run just plays with a local seed
            bool failed = false;
            LeaderboardHub.RequestTicket("level", "lvl_001_06", 2, t => failed = t == null);
            Assert.IsTrue(failed);
            Assert.IsFalse(LeaderboardHub.HasTicket("level", "lvl_001_06", 2));
        }

        // ---------------- the run upload built from the fresh record ----------------

        [Test]
        public void TheUploadIsBuiltFromTheFreshRecordAndRevivedRunsCount()
        {
            var rec = new RunRecord
            {
                Kind = RunKind.Level, LevelId = "lvl_001_02", RegionId = "region_001", Floors = 41, Ticks = 2500, LayoutSeed = 1, AttemptSeed = 999, HeroId = "momo", Mode = 4,
                GameVersion = "0.59.1", Revived = true, Inputs = new byte[] { 1, 2, 3, 4 }, ReviveAt = new[] { 300, 900 }
            };
            Assert.IsFalse(rec.Counts, "a revived run does not count for records");
            var ticket = new StartTicket { Ticket = "tk", Seed = 555, ExpiresAtUnix = 4000000000 };
            QueuedRun q = LeaderboardRunGlue.Build(rec, 1000, ticket, 3, 100);
            Assert.IsNotNull(q, "but it is uploaded: revived runs count on the boards");
            Assert.AreEqual("level", q.Kind);
            Assert.AreEqual("lvl_001_02", q.LevelId);
            Assert.IsNull(q.RegionId);
            Assert.AreEqual(555, q.Seed, "the seed of the ticket, not the local one");
            Assert.AreEqual(3, q.HeroVariant);
            Assert.AreEqual(LeaderboardConfig.SimVersion, q.SimVersion);
            Assert.AreEqual(Convert.ToBase64String(rec.Inputs), q.InputsB64);
            CollectionAssert.AreEqual(new[] { 300, 900 }, q.ReviveAt);
            Assert.AreEqual(1500, q.Crown, "crown = score x (1 + 50 percent) on Insane");
            Assert.AreEqual(41, q.Floors);
            Assert.AreEqual(2500, q.Ticks);

            rec.Kind = RunKind.TimeAttack; rec.RegionId = "region_002";
            QueuedRun t = LeaderboardRunGlue.Build(rec, 1000, ticket, 0, 100);
            Assert.AreEqual("timeattack", t.Kind);
            Assert.AreEqual("region_002", t.RegionId);
            Assert.IsNull(t.LevelId);

            rec.Inputs = new byte[0];
            Assert.IsNull(LeaderboardRunGlue.Build(rec, 1000, ticket, 0, 100), "no recorded inputs, nothing to replay");
            Assert.IsNull(LeaderboardRunGlue.Build(new RunRecord { Inputs = new byte[] { 1 } }, 1, null, 0, 0), "no ticket, not uploaded");
        }

        // ---------------- prizes ----------------

        const string ClaimJson = "{\"claimId\":\"2026-11-02_overall_stub-uid\",\"state\":\"issued\",\"grant\":true,\"rank\":3,\"crowns\":300000,\"prize\":{\"bundles\":0,\"goldChests\":0,\"ironChests\":0,\"powerUps\":1,\"coins\":3000}}";

        [Test]
        public void AClaimGrantsOnceEvenWhenTheServerRepeatsItsAnswer()
        {
            GoLive();
            net.Handler = (m, u, b) => u.EndsWith("/claim-prize") ? Json(b.Contains("\"ack\":true") ? "{\"state\":\"granted\",\"grant\":false}" : ClaimJson) : Json("{}");
            var prize = new PendingPrize { WeekKey = "2026-11-02", Board = "overall", Rank = 3, Crowns = 300000, Prize = new PrizeItemsDto { PowerUps = 1, Coins = 3000 } };
            Assert.IsTrue(prize.Claimable);
            int coins = progress.Coins;

            ClaimOutcome first = null;
            LeaderboardHub.ClaimPrize(prize, o => first = o);
            Assert.AreEqual(ClaimStatus.Granted, first.Status);
            Assert.AreEqual(3000, first.Reward.Coins);
            Assert.AreEqual(1, first.Reward.PowerUpIds.Count);
            Assert.AreEqual(coins + 3000, progress.Coins, "the coins are in the wallet");
            Assert.IsTrue(progress.LeaderboardPrizeGranted("2026-11-02_overall_stub-uid"));
            Assert.AreEqual(coins + 3000, new ProgressService(repo, store, 5).Coins, "saved at once");
            Assert.AreEqual(2, net.CountOf("claim-prize"), "the claim, then the ack once the grant is saved");
            Assert.AreEqual("granted", prize.State);
            Assert.IsFalse(prize.Claimable, "the card is gone");

            // the ack was lost: the server still says grant true, the device must not grant again
            prize.State = "issued";
            ClaimOutcome second = null;
            LeaderboardHub.ClaimPrize(prize, o => second = o);
            Assert.AreEqual(ClaimStatus.AlreadyClaimed, second.Status);
            Assert.AreEqual(coins + 3000, progress.Coins, "no second grant");
        }

        [Test]
        public void AClaimThatCannotReachTheServerChangesNothing()
        {
            GoLive();
            net.Handler = null;
            var prize = new PendingPrize { WeekKey = "2026-11-02", Board = "levels", Rank = 1, Prize = new PrizeItemsDto { GoldChests = 1, PowerUps = 3 } };
            int coins = progress.Coins;
            ClaimOutcome o = null;
            LeaderboardHub.ClaimPrize(prize, x => o = x);
            Assert.AreEqual(ClaimStatus.Offline, o.Status);
            Assert.AreEqual(coins, progress.Coins);
            Assert.IsTrue(prize.Claimable, "still claimable later");

            net.Handler = (m, u, b) => Json("{\"error\":\"not_eligible\"}", 403);
            LeaderboardHub.ClaimPrize(prize, x => o = x);
            Assert.AreEqual(ClaimStatus.NotEligible, o.Status);
        }

        [Test]
        public void ThePrizeItemsBecomeInventoryOnceAndTheGrantIsRepeatable()
        {
            int coins = progress.Coins;
            int stockBefore = 0;
            foreach (KeyValuePair<string, int> kv0 in progress.Data.Inventory.Powerups) stockBefore += kv0.Value;
            RewardClaimOutcome a = progress.GrantLeaderboardPrize("2026-11-02_overall_u1", 1, 1, 1, 2, 500);
            Assert.AreEqual(RewardClaimResult.Ok, a.Result);
            Assert.GreaterOrEqual(a.Coins, 500, "the coins of the prize and of the chests and the bundle");
            Assert.AreEqual(a.Coins, progress.Coins - coins);
            Assert.GreaterOrEqual(a.PowerUpIds.Count, 2 + ChestRolls.IronPowerUps + ChestRolls.GoldenPowerUps, "2 power-ups, plus the ones inside the two chests");
            int stock = 0;
            foreach (KeyValuePair<string, int> kv in progress.Data.Inventory.Powerups) stock += kv.Value;
            Assert.AreEqual(a.PowerUpIds.Count, stock - stockBefore, "every granted power-up is in the stock");

            int coinsAfter = progress.Coins;
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, progress.GrantLeaderboardPrize("2026-11-02_overall_u1", 1, 1, 1, 2, 500).Result);
            Assert.AreEqual(coinsAfter, progress.Coins);
            Assert.AreEqual(RewardClaimResult.Invalid, progress.GrantLeaderboardPrize("x", 0, 0, 0, 0, 0).Result, "an empty prize grants nothing");
            Assert.AreEqual(RewardClaimResult.Invalid, progress.GrantLeaderboardPrize("", 0, 0, 0, 1, 0).Result);
            Assert.AreEqual(RewardClaimResult.Invalid, progress.GrantLeaderboardPrize("y", 0, 0, 0, -1, 0).Result);
        }

        [Test]
        public void PrizeTablesMatchTheOwnerDecision()
        {
            Assert.AreEqual("1 Bundle, 1 Gold chest", LbPrizeTable.For("overall", 1).Describe());
            Assert.AreEqual("1 Bundle, 1 Iron chest", LbPrizeTable.For("overall", 2).Describe());
            Assert.AreEqual("1 Gold chest", LbPrizeTable.For("overall", 3).Describe());
            Assert.AreEqual("1 Iron chest, 2 power-ups", LbPrizeTable.For("overall", 7).Describe());
            Assert.AreEqual("1 Gold chest, 3 power-ups", LbPrizeTable.For("levels", 1).Describe());
            Assert.AreEqual("1 Gold chest", LbPrizeTable.For("endless", 2).Describe());
            Assert.AreEqual("1 Iron chest, 2 power-ups", LbPrizeTable.For("levels", 3).Describe());
            Assert.AreEqual("1 power-up, 3,000 coins", LbPrizeTable.For("endless", 10).Describe());
            Assert.IsTrue(LbPrizeTable.For("overall", 11).IsEmpty);
            Assert.AreEqual(4, LbPrizeTable.Rows("overall").GetLength(0));
        }

        // ---------------- parsers ----------------

        [Test]
        public void ABoardDocumentFromFirestoreBecomesSortedRows()
        {
            const string json = "{\"name\":\"projects/p/databases/(default)/documents/weeks/2026-11-09/boards/overall\",\"fields\":{\"final\":{\"booleanValue\":false},\"entries\":{\"arrayValue\":{\"values\":["
                + "{\"mapValue\":{\"fields\":{\"rank\":{\"integerValue\":\"2\"},\"pid\":{\"stringValue\":\"b2\"},\"name\":{\"stringValue\":\"Otto\"},\"avatar\":{\"stringValue\":\"https://x/y.png\"},\"crowns\":{\"integerValue\":\"2500\"}}}},"
                + "{\"mapValue\":{\"fields\":{\"rank\":{\"integerValue\":\"1\"},\"pid\":{\"stringValue\":\"a1\"},\"name\":{\"stringValue\":\"Momo\"},\"avatar\":{\"stringValue\":\"\"},\"crowns\":{\"integerValue\":\"9000000000\"}}}}]}}}}";
            BoardData b = LbJson.ParseFirestoreBoard(json, "2026-11-09", "overall");
            Assert.AreEqual(2, b.Entries.Count);
            Assert.AreEqual(1, b.Entries[0].Rank, "sorted by rank");
            Assert.AreEqual("Momo", b.Entries[0].Name);
            Assert.AreEqual(9000000000L, b.Entries[0].Crowns, "crowns are 64 bit");
            Assert.AreEqual("https://x/y.png", b.Entries[1].Avatar);
            Assert.AreEqual("b2", b.Entries[1].Pid);
            Assert.AreEqual(0, LbJson.ParseFirestoreBoard("{\"fields\":{\"entries\":{\"arrayValue\":{}}}}", "w", "levels").Entries.Count, "an empty board");
            Assert.AreEqual(0, LbJson.ParseFirestoreBoard("garbage", "w", "levels").Entries.Count);
            Assert.AreEqual(0, LbJson.ParseFirestoreBoard(null, "w", "levels").Entries.Count);
        }

        [Test]
        public void MyWeekGivesTheRankLimitProgressAndTheClaimablePrizes()
        {
            const string json = "{\"weekKey\":\"2026-11-09\",\"pid\":\"p37\",\"weekEndsAt\":\"2026-11-15T18:30:00Z\",\"serverTime\":\"2026-11-12T11:00:00Z\",\"totals\":{\"overall\":1234567,\"levels\":1000000,\"endless\":234567},"
                + "\"ranks\":[{\"board\":\"overall\",\"crowns\":1234567,\"rank\":37,\"entryLimit\":200000,\"reachedLimit\":true},{\"board\":\"levels\",\"crowns\":1000000,\"rank\":0,\"entryLimit\":150000,\"reachedLimit\":true},{\"board\":\"endless\",\"crowns\":10,\"rank\":9,\"entryLimit\":50000,\"reachedLimit\":false}],"
                + "\"pendingPrizes\":[{\"weekKey\":\"2026-11-02\",\"board\":\"levels\",\"rank\":2,\"crowns\":400000,\"prize\":{\"bundles\":0,\"goldChests\":1,\"ironChests\":0,\"powerUps\":0,\"coins\":0},\"state\":\"open\"},"
                + "{\"weekKey\":\"2026-11-02\",\"board\":\"overall\",\"rank\":9,\"crowns\":300000,\"prize\":{\"ironChests\":1,\"powerUps\":2},\"state\":\"granted\"}]}";
            MyWeekData d = LbJson.ParseMyWeek(json);
            Assert.AreEqual("2026-11-09", d.WeekKey);
            Assert.AreEqual("p37", d.Pid);
            Assert.AreEqual(LeaderboardTime.ToUnix(new DateTime(2026, 11, 15, 18, 30, 0, DateTimeKind.Utc)), d.WeekEndsAtUnix);
            Assert.AreEqual(37, d.RankOf("overall").Rank);
            Assert.AreEqual(200000L, d.RankOf("overall").EntryLimit);
            Assert.IsTrue(d.RankOf("overall").ReachedLimit);
            Assert.IsFalse(d.RankOf("endless").ReachedLimit);
            Assert.AreEqual(1234567L, d.Totals[0]);
            Assert.IsTrue(d.PrizesKnown);
            Assert.AreEqual(2, d.Pending.Count);
            Assert.IsTrue(d.Pending[0].Claimable);
            Assert.AreEqual(1, d.Pending[0].Prize.GoldChests);
            Assert.IsFalse(d.Pending[1].Claimable, "already granted");
            Assert.IsNull(LbJson.ParseMyWeek("{\"error\":\"x\"}"));
            Assert.IsFalse(LbJson.ParseMyWeek("{\"weekKey\":\"w\"}").PrizesKnown, "no prize list in the answer");
        }

        [Test]
        public void TheProfileCommitFollowsTheSecurityRules()
        {
            string longName = new string('N', 40);
            string body = LbJson.BuildProfileCommit("fluffy-climbers-tower-jump", "uid1", longName, "http://insecure/x.png");
            StringAssert.Contains("players/uid1", body);
            StringAssert.Contains("REQUEST_TIME", body, "updatedAt must equal the request time");
            StringAssert.Contains("\"fieldPaths\":[\"displayName\",\"avatarUrl\"]", body);
            StringAssert.Contains("\"avatarUrl\":{\"stringValue\":\"\"}", body, "an http URL is not allowed, an empty one is");
            Assert.AreEqual(32, LbJson.SafeName(longName).Length);
            Assert.AreEqual("Player", LbJson.SafeName("   "));
            Assert.AreEqual("https://x/a.png", LbJson.SafeAvatar("https://x/a.png"));
            Assert.AreEqual("", LbJson.SafeAvatar(new string('a', 600)));
        }

        [Test]
        public void ProfileIsWrittenOnceAfterSignInAndAgainOnlyWhenItChanges()
        {
            progress.GrantLeaderboardConsent(1);
            LeaderboardHub.Setup(progress, new LeaderboardConfig { Enabled = true, BaseUrl = "https://lb.test" }, auth, net);
            net.Handler = (m, u, b) => Json("{}");
            auth.ProfileValue = new LbProfile { DisplayName = "Momo Fan", AvatarUrl = "https://x/m.png" };
            LeaderboardHub.SignIn(null);
            Assert.AreEqual(1, net.CountOf("documents:commit"), "the profile is written after the sign-in");
            LeaderboardHub.SyncProfile();
            Assert.AreEqual(1, net.CountOf("documents:commit"), "not again while unchanged");
            auth.ProfileValue = new LbProfile { DisplayName = "Momo Fan 2", AvatarUrl = "https://x/m.png" };
            LeaderboardHub.SyncProfile();
            Assert.AreEqual(2, net.CountOf("documents:commit"));
        }

        // ---------------- boards through the hub ----------------

        [Test]
        public void ABoardIsReadFromFirestoreCachedAndAnOfflineReadIsReportedNotThrown()
        {
            GoLive();
            string wk = LeaderboardTime.WeekKey(now);
            net.Handler = (m, u, b) => u.Contains("/boards/overall") ? Json("{\"fields\":{\"entries\":{\"arrayValue\":{\"values\":[{\"mapValue\":{\"fields\":{\"rank\":{\"integerValue\":\"1\"},\"name\":{\"stringValue\":\"A\"},\"crowns\":{\"integerValue\":\"5\"}}}}]}}}}")
                : Json("", 404);
            BoardData got = null;
            LeaderboardHub.LoadBoard("overall", d => got = d);
            Assert.AreEqual(1, got.Entries.Count);
            Assert.AreEqual(wk, got.WeekKey);
            StringAssert.Contains("/weeks/" + wk + "/boards/overall", net.Calls[0]);
            Assert.AreSame(got, LeaderboardHub.CachedBoard("overall"));
            LeaderboardHub.LoadBoard("overall", d => got = d);
            Assert.AreEqual(1, net.Calls.Count, "cached for a few seconds: one document read per view");

            BoardData empty = null;
            LeaderboardHub.LoadBoard("levels", d => empty = d);
            Assert.IsNotNull(empty);
            Assert.AreEqual(0, empty.Entries.Count, "no document yet = an empty board");

            net.Handler = null;
            BoardData offline = new BoardData();
            LeaderboardHub.LoadBoard("endless", d => offline = d);
            Assert.IsNull(offline, "an offline read gives null, never an exception");
        }

        // ---------------- avatars ----------------

        [Test]
        public void AvatarsAreDownloadedOnceAndServedFromTheCache()
        {
            int loads = 0;
            AvatarCache.Loader = (url, done) => { loads++; done(new Texture2D(8, 4)); };
            Sprite got = null;
            AvatarCache.Request("https://x/a.png", s => got = s);
            Assert.IsNotNull(got);
            Assert.AreEqual(4f, got.rect.width, "a centred square crop: never stretched");
            Assert.AreEqual(4f, got.rect.height);
            Sprite again = null;
            AvatarCache.Request("https://x/a.png", s => again = s);
            Assert.AreSame(got, again);
            Assert.AreEqual(1, loads);

            AvatarCache.Loader = (url, done) => { loads++; done(null); };
            bool called = false;
            AvatarCache.Request("https://x/bad.png", s => called = true);
            Assert.IsFalse(called, "a failed download shows the neutral badge");
            AvatarCache.Request("https://x/bad.png", s => called = true);
            Assert.AreEqual(2, loads, "a failure is remembered for a minute: no retry loop");
            Assert.IsNull(AvatarCache.Peek(""));
        }

        [Test]
        public void AdminTestSwitchForcesTheConfigOnWithTheDeployedUrl()
        {
            Assert.IsFalse(LeaderboardConfig.From(null).IsLive, "shipped default is OFF");
            LeaderboardConfig forced = LeaderboardConfig.From(null, true);
            Assert.IsTrue(forced.IsLive);
            Assert.AreEqual(LeaderboardConfig.DefaultBaseUrl, forced.BaseUrl);
            var cfgOff = new RemoteConfigDef { Leaderboard = new LeaderboardConfigDef { Enabled = false, BaseUrl = "https://lb.example.test/" } };
            Assert.IsFalse(LeaderboardConfig.From(cfgOff).IsLive);
            Assert.AreEqual("https://lb.example.test", LeaderboardConfig.From(cfgOff, true).BaseUrl, "a usable shipped URL is kept");
        }

        [Test]
        public void TestSwitchNeverCountsInAReleaseOrWithoutSystemTest()
        {
            GameSettings.LeaderboardTest = true;
            try { Assert.IsFalse(GameSettings.LeaderboardTest, "editor tests are not a development build on a device: the switch reads off"); }
            finally { GameSettings.LeaderboardTest = false; }
        }
    }
}
