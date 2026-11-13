using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Simulation;
using Xunit;

namespace FluffyClimbers.LeaderboardService.Tests
{
    /// <summary>Slot rules, weekly totals, boards, prizes and claims, through the same API the HTTP layer calls (in-memory store, fake clock).</summary>
    public class ApiTests
    {
        sealed class Rig
        {
            public InMemoryStore Store = new InMemoryStore();
            public DateTime Now = new DateTime(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc);   // a Thursday, week 2026-11-09
            public LeaderboardApi Api;
            public Rig()
            {
                Store.LimitsValue.MinSubmitIntervalMs = 0;
                Api = new LeaderboardApi(Store, Harness.Repo, () => Now, "test-salt");
            }
            public string Week { get { return WeekMath.WeekKey(Now); } }

            public static string Target(RunSubmission s) { return s.Kind == Kinds.Level ? s.LevelId : s.RegionId; }

            /// <summary>The ticket /start-run would have issued for this upload (the test chose the seed itself).</summary>
            public string TicketFor(string uid, RunSubmission s, int minutes = 120) { return Api.Tickets.Issue(uid, s.Kind, Target(s), s.Mode, s.Seed, minutes); }

            /// <summary>Submit with a fresh valid ticket.</summary>
            public Task<ApiResult> Send(string uid, RunSubmission s) { s.Ticket = TicketFor(uid, s); return Api.SubmitRunAsync(uid, s); }
        }

        static dynamic Body(ApiResult r) { return Newtonsoft.Json.Linq.JObject.FromObject(r.Body); }

        static RunSubmission BotRun(string level, int mode, int seed, int fractionOfTrace = 0)
        {
            RunSession s = Harness.NewLevelSession(level, mode, seed);
            int[] trace = Harness.BotTrace(s, s.State);
            if (fractionOfTrace > 0) Harness.Play(s, trace, 0, trace.Length / fractionOfTrace); else Harness.Play(s, trace);
            return Harness.Upload(s, Kinds.Level, seed);
        }

        [Fact]
        public async Task ALowerRunNeverChangesASlotAndABetterOneRaisesTheTotalsByTheDifference()
        {
            var rig = new Rig();
            RunSubmission x = BotRun("lvl_001_02", 2, 100), y = BotRun("lvl_001_02", 2, 101), z = BotRun("lvl_001_02", 2, 102);
            RunSubmission[] byCrown = new[] { x, y, z }.OrderBy(r => r.Claimed.Crown).ToArray();
            Assert.True(byCrown[0].Claimed.Crown < byCrown[2].Claimed.Crown, "three completed runs of different layouts need different crowns");
            RunSubmission half = byCrown[0], full = byCrown[2];   // both COMPLETED runs of the same slot (level + setting)
            long cFull = full.Claimed.Crown, cHalf = half.Claimed.Crown;
            Assert.True(cHalf > 0);

            ApiResult r1 = await rig.Send("alice", half);
            Assert.Equal(200, r1.Status);
            Assert.True((bool)Body(r1).improved);
            Assert.Equal(cHalf, (long)Body(r1).totals.overall);

            ApiResult r2 = await rig.Send("alice", full);   // better: replaces, totals grow by the difference only
            Assert.True((bool)Body(r2).improved);
            Assert.Equal(cFull, (long)Body(r2).totals.overall);
            Assert.Equal(cFull, (long)Body(r2).totals.levels);
            Assert.Equal(0L, (long)Body(r2).totals.endless);

            ApiResult r3 = await rig.Send("alice", half);   // lower: nothing changes
            Assert.False((bool)Body(r3).improved);
            Assert.Equal(cFull, (long)Body(r3).slotCrown);
            ApiResult r4 = await rig.Send("alice", full);   // equal: nothing changes
            Assert.False((bool)Body(r4).improved);

            PlayerWeek w = await rig.Store.GetPlayerWeekAsync(rig.Week, "alice", true);
            Assert.Single(w.Slots);
            Assert.Equal(cFull, w.Slots[0].Crown);
            Assert.Equal("c.lvl_001_02.2", w.Slots[0].SlotId);
            Assert.Equal(1, w.Totals.SlotCount);
            Assert.Equal(cFull, w.Totals.Overall);
        }

        [Fact]
        public async Task SlotsAreLevelTimesSettingAndAllSlotsAddUpIntoLevelsEndlessAndOverall()
        {
            var rig = new Rig();
            RunSubmission a = BotRun("lvl_001_01", 2, 1), b = BotRun("lvl_001_01", 4, 1), c = BotRun("lvl_002_01", 2, 1);   // three different slots
            RunSession e = Harness.NewEndlessSession("region_001_meadow_springs", 3, 55, false);
            Harness.Play(e, Harness.BotTrace(e, e.State, 40));
            RunSubmission end = Harness.Upload(e, Kinds.Endless, 55);
            RunSession t = Harness.NewEndlessSession("region_001_meadow_springs", 3, 56, true);
            Harness.Play(t, Harness.BotTrace(t, t.State, 40));
            RunSubmission ta = Harness.Upload(t, Kinds.TimeAttack, 56);

            long levels = 0, endless = 0;
            foreach (RunSubmission s in new[] { a, b, c }) { Assert.Equal(200, (await rig.Send("bob", s)).Status); levels += s.Claimed.Crown; }
            foreach (RunSubmission s in new[] { end, ta }) { Assert.Equal(200, (await rig.Send("bob", s)).Status); endless += s.Claimed.Crown; }

            PlayerWeek w = await rig.Store.GetPlayerWeekAsync(rig.Week, "bob", true);
            Assert.Equal(5, w.Slots.Count);
            Assert.Equal(levels, w.Totals.Levels);
            Assert.Equal(endless, w.Totals.Endless);
            Assert.Equal(levels + endless, w.Totals.Overall);
            // Endless and Time Attack of one region and setting are different slots
            Assert.Contains(w.Slots, x => x.SlotId == "e.region_001_meadow_springs.3");
            Assert.Contains(w.Slots, x => x.SlotId == "t.region_001_meadow_springs.3");
        }

        [Fact]
        public async Task ANewWeekStartsEmpty()
        {
            var rig = new Rig();
            RunSubmission a = BotRun("lvl_001_01", 2, 1);
            await rig.Send("carol", a);
            string week1 = rig.Week;
            rig.Now = new DateTime(2026, 11, 15, 18, 29, 59, DateTimeKind.Utc);   // Sunday 23:59:59 IST
            Assert.Equal(week1, rig.Week);
            rig.Now = new DateTime(2026, 11, 15, 18, 30, 0, DateTimeKind.Utc);    // Monday 00:00:00 IST
            Assert.NotEqual(week1, rig.Week);
            Assert.Equal("2026-11-16", rig.Week);
            ApiResult r = await rig.Send("carol", a);
            Assert.True((bool)Body(r).improved, "the same level counts again in the new week");
            Assert.Equal(a.Claimed.Crown, (long)Body(r).totals.overall);
        }

        [Fact]
        public void WeekKeysFollowIndiaTime()
        {
            Assert.Equal("2026-11-09", WeekMath.WeekKey(new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc)));      // Monday 05:30 IST
            Assert.Equal("2026-11-02", WeekMath.WeekKey(new DateTime(2026, 11, 8, 18, 29, 59, DateTimeKind.Utc)));   // Sunday 23:59:59 IST
            Assert.Equal("2026-11-09", WeekMath.WeekKey(new DateTime(2026, 11, 8, 18, 30, 0, DateTimeKind.Utc)));    // Monday 00:00:00 IST
            Assert.Equal(new DateTime(2026, 11, 8, 18, 30, 0, DateTimeKind.Utc), WeekMath.WeekStartUtc(new DateTime(2026, 11, 12, 1, 0, 0, DateTimeKind.Utc)));
            DateTime s;
            Assert.True(WeekMath.TryStartOf("2026-11-09", out s));
            Assert.Equal(new DateTime(2026, 11, 8, 18, 30, 0, DateTimeKind.Utc), s);
            Assert.False(WeekMath.TryStartOf("2026-11-10", out s));   // not a Monday
            Assert.False(WeekMath.TryStartOf("garbage", out s));
        }

        [Fact]
        public async Task TheBoardRanksByCrownsThenByTheEarlierTotalAndTheTop100IsMaterialised()
        {
            var rig = new Rig();
            RunSubmission big = BotRun("lvl_001_03", 2, 7), small = BotRun("lvl_001_01", 2, 7);
            Assert.True(big.Claimed.Crown > small.Claimed.Crown);
            await rig.Send("p_small", small);
            rig.Now = rig.Now.AddMinutes(1);
            await rig.Send("p_big", big);
            rig.Now = rig.Now.AddMinutes(1);
            await rig.Send("p_big_late", big);   // the same total, reached later
            BoardDoc b = await rig.Store.RebuildBoardAsync(rig.Week, Boards.Overall, new Limits(), rig.Now, false);
            Assert.Equal(new[] { rig.Api.PidOf("p_big"), rig.Api.PidOf("p_big_late"), rig.Api.PidOf("p_small") }, b.Entries.Select(x => x.Pid).ToArray());
            Assert.Equal(new[] { 1, 2, 3 }, b.Entries.Select(x => x.Rank).ToArray());
            Assert.Equal(0, b.Cutoff);   // fewer than 100 entries: everybody can still enter

            ApiResult mine = await rig.Api.MyWeekAsync("p_big_late", false, false);
            Assert.Equal(2, (int)Body(mine).ranks[0].Rank);
            Assert.Equal(1, (int)Body(await rig.Api.MyWeekAsync("p_big", false, false)).ranks[0].Rank);
            Assert.Equal(3, (int)Body(await rig.Api.MyWeekAsync("p_small", false, false)).ranks[0].Rank);
            Assert.Equal(0, (int)Body(await rig.Api.MyWeekAsync("p_small", false, false)).ranks[2].Rank);   // no endless crowns: unranked there
        }

        [Fact]
        public async Task ARunWithABadClaimOrBrokenInputsNeverTouchesTheBoard()
        {
            var rig = new Rig();
            RunSubmission a = BotRun("lvl_001_01", 2, 3);
            a.Claimed.Crown += 1000;
            ApiResult r = await rig.Send("mallory", a);
            Assert.Equal(422, r.Status);
            Assert.Equal("mismatch", (string)Body(r).error);
            Assert.Null((await rig.Store.GetPlayerWeekAsync(rig.Week, "mallory", true)).Totals);

            RunSubmission huge = BotRun("lvl_001_01", 2, 3);
            rig.Store.LimitsValue.MaxCrownPerRun = 10;
            Assert.Equal("implausible", (string)Body(await rig.Send("mallory", huge)).error);
        }

        [Fact]
        public async Task TheDurableRateLimitStopsFloodsAndTheHeroVariantStaysLocked()
        {
            var rig = new Rig();
            rig.Store.LimitsValue.MinSubmitIntervalMs = 5000;
            RunSubmission a = BotRun("lvl_001_01", 2, 3);
            Assert.Equal(200, (await rig.Send("dave", a)).Status);
            ApiResult again = await rig.Send("dave", a);
            Assert.Equal(429, again.Status);
            rig.Now = rig.Now.AddSeconds(6);
            Assert.Equal(200, (await rig.Send("dave", a)).Status);

            // a rolled hero keeps the variant of its first leaderboard run (needs generated variants: HeroVariants.Rows is empty until the hero generator ran)
            rig.Store.LimitsValue.MinSubmitIntervalMs = 0;
            string id = Harness.HeroWithPool(3);
            if (id == null) return;
            RunSession s1 = Harness.NewLevelSession("lvl_001_02", 2, 8, id, 1);
            Harness.Play(s1, Harness.BotTrace(s1, s1.State));
            Assert.Equal(200, (await rig.Send("erin", Harness.Upload(s1, Kinds.Level, 8, 1))).Status);
            RunSession s2 = Harness.NewLevelSession("lvl_001_03", 2, 8, id, 2);
            Harness.Play(s2, Harness.BotTrace(s2, s2.State));
            ApiResult locked = await rig.Send("erin", Harness.Upload(s2, Kinds.Level, 8, 2));
            Assert.Equal("variant_locked", (string)Body(locked).error);
        }

        [Fact]
        public async Task PrizesAreClaimedOnceAfterTheWeekClosesWithTheFrozenRankAndLimits()
        {
            var rig = new Rig();
            // small entry limits so a handful of bot runs can reach them
            rig.Store.LimitsValue.OverallEntryLimit = 10; rig.Store.LimitsValue.LevelsEntryLimit = 10; rig.Store.LimitsValue.EndlessEntryLimit = 1000000;
            RunSubmission r1 = BotRun("lvl_001_01", 2, 21), r2 = BotRun("lvl_001_02", 2, 21);
            string wk = rig.Week;
            await rig.Send("top", r1); await rig.Send("top", r2);
            rig.Now = rig.Now.AddSeconds(1);
            await rig.Send("second", r2);

            // the week is still open
            ApiResult open = await rig.Api.ClaimPrizeAsync("top", new LeaderboardApi.ClaimRequest { WeekKey = wk, Board = Boards.Overall });
            Assert.Equal(409, open.Status);

            rig.Now = new DateTime(2026, 11, 16, 3, 0, 0, DateTimeKind.Utc);   // the next Monday morning
            ApiResult pend = await rig.Api.MyWeekAsync("top", false, true);
            Assert.Equal(2, ((Newtonsoft.Json.Linq.JArray)Body(pend).pendingPrizes).Count);   // overall and levels

            // the config changes later: the closed week keeps the limits it was frozen with
            rig.Store.LimitsValue.OverallEntryLimit = 999999999;
            ApiResult c1 = await rig.Api.ClaimPrizeAsync("top", new LeaderboardApi.ClaimRequest { WeekKey = wk, Board = Boards.Overall });
            Assert.Equal(200, c1.Status);
            Assert.True((bool)Body(c1).grant);
            Assert.Equal(1, (int)Body(c1).rank);
            // rank 1 of overall: Bundle + Gold chest; with 1.5x limit met (crowns far above 15)
            Assert.Equal(1, (int)Body(c1).prize.Bundles);
            Assert.Equal(1, (int)Body(c1).prize.GoldChests);

            // idempotent: asking again before the ack returns the same prize
            ApiResult c2 = await rig.Api.ClaimPrizeAsync("top", new LeaderboardApi.ClaimRequest { WeekKey = wk, Board = Boards.Overall });
            Assert.True((bool)Body(c2).grant);
            Assert.Equal((string)Body(c1).claimId, (string)Body(c2).claimId);
            // ack, then never again
            ApiResult ack = await rig.Api.ClaimPrizeAsync("top", new LeaderboardApi.ClaimRequest { WeekKey = wk, Board = Boards.Overall, Ack = true });
            Assert.Equal("granted", (string)Body(ack).state);
            ApiResult c3 = await rig.Api.ClaimPrizeAsync("top", new LeaderboardApi.ClaimRequest { WeekKey = wk, Board = Boards.Overall });
            Assert.False((bool)Body(c3).grant);

            // rank 2 gets the second prize, an unranked player nothing, nobody can ack a claim of another player
            ApiResult s = await rig.Api.ClaimPrizeAsync("second", new LeaderboardApi.ClaimRequest { WeekKey = wk, Board = Boards.Overall });
            Assert.Equal(2, (int)Body(s).rank);
            Assert.Equal(1, (int)Body(s).prize.Bundles);
            Assert.Equal(1, (int)Body(s).prize.IronChests);
            Assert.Equal(403, (await rig.Api.ClaimPrizeAsync("nobody", new LeaderboardApi.ClaimRequest { WeekKey = wk, Board = Boards.Overall })).Status);
            Assert.Equal(403, (await rig.Api.ClaimPrizeAsync("top", new LeaderboardApi.ClaimRequest { WeekKey = wk, Board = Boards.Endless })).Status);   // no endless crowns
            Assert.Equal(404, (await rig.Api.ClaimPrizeAsync("second", new LeaderboardApi.ClaimRequest { WeekKey = wk, Board = Boards.Levels, Ack = true })).Status);
            Assert.Equal(400, (await rig.Api.ClaimPrizeAsync("top", new LeaderboardApi.ClaimRequest { WeekKey = "2026-11-10", Board = Boards.Overall })).Status);
        }

        [Fact]
        public void PrizeTablesAndEligibilityFollowTheOwnerDecision()
        {
            PrizeItems p;
            p = Prizes.For(Boards.Overall, 1); Assert.Equal((1, 1, 0, 0, 0), (p.Bundles, p.GoldChests, p.IronChests, p.PowerUps, p.Coins));
            p = Prizes.For(Boards.Overall, 2); Assert.Equal((1, 0, 1, 0, 0), (p.Bundles, p.GoldChests, p.IronChests, p.PowerUps, p.Coins));
            p = Prizes.For(Boards.Overall, 3); Assert.Equal((0, 1, 0, 0, 0), (p.Bundles, p.GoldChests, p.IronChests, p.PowerUps, p.Coins));
            foreach (int r in Enumerable.Range(4, 7)) { p = Prizes.For(Boards.Overall, r); Assert.Equal((0, 0, 1, 2, 0), (p.Bundles, p.GoldChests, p.IronChests, p.PowerUps, p.Coins)); }
            foreach (string b in new[] { Boards.Levels, Boards.Endless })
            {
                p = Prizes.For(b, 1); Assert.Equal((0, 1, 0, 3, 0), (p.Bundles, p.GoldChests, p.IronChests, p.PowerUps, p.Coins));
                p = Prizes.For(b, 2); Assert.Equal((0, 1, 0, 0, 0), (p.Bundles, p.GoldChests, p.IronChests, p.PowerUps, p.Coins));
                p = Prizes.For(b, 3); Assert.Equal((0, 0, 1, 2, 0), (p.Bundles, p.GoldChests, p.IronChests, p.PowerUps, p.Coins));
                foreach (int r in Enumerable.Range(4, 7)) { p = Prizes.For(b, r); Assert.Equal((0, 0, 0, 1, 3000), (p.Bundles, p.GoldChests, p.IronChests, p.PowerUps, p.Coins)); }
            }
            Assert.True(Prizes.For(Boards.Overall, 11).IsEmpty);
            Assert.True(Prizes.For(Boards.Overall, 0).IsEmpty);

            var lim = new Limits();   // 200000 / 150000 / 50000, top three need 1.5x
            Assert.True(Prizes.Resolve(lim, Boards.Overall, 5, 199999).IsEmpty);                       // below the entry limit
            Assert.Equal(2, Prizes.Resolve(lim, Boards.Overall, 5, 200000).PowerUps);                   // limit met, ranks 4-10 need no more
            Assert.Equal(2, Prizes.Resolve(lim, Boards.Overall, 1, 299999).PowerUps);                   // rank 1 without 1.5x falls back to the 4th-10th prize (owner question Q2)
            Assert.Equal(1, Prizes.Resolve(lim, Boards.Overall, 1, 300000).Bundles);                    // 1.5 x 200000
            Assert.Equal(1, Prizes.Resolve(lim, Boards.Endless, 3, 75000).IronChests);                  // 1.5 x 50000
            Assert.True(Prizes.Resolve(lim, Boards.Levels, 2, 149999).IsEmpty);
        }

        // ------------------------------------------------------------------ owner decisions 2026-11-12: Q1 completed-only slots, Q3 run tickets

        [Fact]
        public async Task CampaignSlotsCountCompletedLevelsOnly()
        {
            var rig = new Rig();
            RunSubmission partial = BotRun("lvl_001_02", 2, 100, 2);   // half of the bot line: alive, goal not reached
            ApiResult r = await rig.Send("pat", partial);
            Assert.Equal(200, r.Status);
            Assert.False((bool)Body(r).counted);
            Assert.Equal("level_not_completed", (string)Body(r).reason);
            Assert.Null((await rig.Store.GetPlayerWeekAsync(rig.Week, "pat", true)).Totals);

            RunSubmission done = BotRun("lvl_001_02", 2, 100);
            Assert.True((bool)Body(await rig.Send("pat", done)).counted);
            Assert.Equal(done.Claimed.Crown, (await rig.Store.GetPlayerWeekAsync(rig.Week, "pat", false)).Totals.Levels);

            // Endless and Time Attack count the run when it ends, goal or not
            RunSession e = Harness.NewEndlessSession("region_001_meadow_springs", 2, 77, false);
            Harness.Play(e, Harness.BotTrace(e, e.State, 40));
            RunSubmission end = Harness.Upload(e, Kinds.Endless, 77);
            ApiResult re = await rig.Send("pat", end);
            Assert.True((bool)Body(re).counted);
            Assert.True((bool)Body(re).improved);
        }

        [Fact]
        public async Task ARunNeedsAValidSingleUseTicketBoundToUidTargetSettingAndSeed()
        {
            var rig = new Rig();
            RunSubmission a = BotRun("lvl_001_01", 2, 500);
            ApiResult none = await rig.Api.SubmitRunAsync("tina", a);   // no ticket
            Assert.Equal(403, none.Status);
            Assert.Equal("bad_ticket", (string)Body(none).error);

            // start-run: server chosen seed, ticket bound to the request
            ApiResult st = await rig.Api.StartRunAsync("tina", new LeaderboardApi.StartRunRequest { Kind = Kinds.Level, LevelId = "lvl_001_01", Mode = 2 });
            Assert.Equal(200, st.Status);
            int serverSeed = (int)Body(st).seed;
            Assert.True(serverSeed > 0);
            string ticket = (string)Body(st).ticket;
            Assert.Equal(400, (await rig.Api.StartRunAsync("tina", new LeaderboardApi.StartRunRequest { Kind = Kinds.Level, LevelId = "lvl_006_01", Mode = 2 })).Status);
            Assert.Equal(400, (await rig.Api.StartRunAsync("tina", new LeaderboardApi.StartRunRequest { Kind = Kinds.Level, LevelId = "lvl_001_01", Mode = 9 })).Status);

            RunSubmission real = BotRun("lvl_001_01", 2, serverSeed);   // played with the server's seed
            real.Ticket = ticket;
            // wrong uid
            ApiResult wrongUid = await rig.Api.SubmitRunAsync("mallory", real);
            Assert.Equal("ticket_mismatch", (string)Body(wrongUid).error);
            // wrong level, wrong setting, wrong seed (the client picked its own)
            RunSubmission wl = BotRun("lvl_001_02", 2, serverSeed); wl.Ticket = ticket;
            Assert.Equal("ticket_mismatch", (string)Body(await rig.Api.SubmitRunAsync("tina", wl)).error);
            RunSubmission wm = BotRun("lvl_001_01", 3, serverSeed); wm.Ticket = ticket;
            Assert.Equal("ticket_mismatch", (string)Body(await rig.Api.SubmitRunAsync("tina", wm)).error);
            RunSubmission ws = BotRun("lvl_001_01", 2, serverSeed + 1); ws.Ticket = ticket;
            Assert.Equal("ticket_mismatch", (string)Body(await rig.Api.SubmitRunAsync("tina", ws)).error);
            // a forged / edited ticket
            RunSubmission forged = BotRun("lvl_001_01", 2, serverSeed); forged.Ticket = ticket.Substring(0, ticket.Length - 2) + "AA";
            Assert.Equal("bad_ticket", (string)Body(await rig.Api.SubmitRunAsync("tina", forged)).error);
            Assert.Equal("bad_ticket", (string)Body(await rig.Api.SubmitRunAsync("tina", new RunSubmission { Ticket = "x.y", Claimed = new ClaimedResult() })).error);

            // none of the failures burned the ticket: the right run passes
            ApiResult ok = await rig.Api.SubmitRunAsync("tina", real);
            Assert.Equal(200, ok.Status);
            Assert.True((bool)Body(ok).improved);
            // single use
            ApiResult again = await rig.Api.SubmitRunAsync("tina", real);
            Assert.Equal(409, again.Status);
            Assert.Equal("ticket_used", (string)Body(again).error);
        }

        [Fact]
        public async Task AnExpiredTicketIsRefused()
        {
            var rig = new Rig();
            RunSubmission a = BotRun("lvl_001_01", 2, 501);
            a.Ticket = rig.TicketFor("ed", a, 120);
            rig.Now = rig.Now.AddMinutes(121);
            ApiResult r = await rig.Api.SubmitRunAsync("ed", a);
            Assert.Equal(410, r.Status);
            Assert.Equal("ticket_expired", (string)Body(r).error);
            // still valid just inside the window
            RunSubmission b = BotRun("lvl_001_01", 2, 502);
            b.Ticket = rig.TicketFor("ed", b, 120);
            rig.Now = rig.Now.AddMinutes(119);
            Assert.Equal(200, (await rig.Api.SubmitRunAsync("ed", b)).Status);
        }

        [Fact]
        public async Task EndlessTicketsAreBoundToTheRegionAndKind()
        {
            var rig = new Rig();
            ApiResult st = await rig.Api.StartRunAsync("eli", new LeaderboardApi.StartRunRequest { Kind = Kinds.Endless, RegionId = "region_001_meadow_springs", Mode = 1 });
            int seed = (int)Body(st).seed;
            RunSession e = Harness.NewEndlessSession("region_001_meadow_springs", 1, seed, false);
            Harness.Play(e, Harness.BotTrace(e, e.State, 40));
            RunSubmission asTa = Harness.Upload(e, Kinds.TimeAttack, seed);   // same ticket used as a Time Attack run
            asTa.Ticket = (string)Body(st).ticket;
            Assert.Equal("ticket_mismatch", (string)Body(await rig.Api.SubmitRunAsync("eli", asTa)).error);
            RunSubmission asEndless = Harness.Upload(e, Kinds.Endless, seed);
            asEndless.Ticket = (string)Body(st).ticket;
            Assert.Equal(200, (await rig.Api.SubmitRunAsync("eli", asEndless)).Status);
        }

        [Fact]
        public async Task ATopThreePlayerBetweenTheLimitAndOnePointFiveTimesGetsTheFourthToTenthPrize()
        {
            var rig = new Rig();
            RunSubmission r = BotRun("lvl_001_03", 2, 21);
            long x = r.Claimed.Crown;
            rig.Store.LimitsValue.OverallEntryLimit = x;        // crowns == limit: eligible, but below 1.5 x limit
            rig.Store.LimitsValue.LevelsEntryLimit = x;
            rig.Store.LimitsValue.EndlessEntryLimit = 1000000;
            string wk = rig.Week;
            await rig.Send("first", r);
            rig.Now = new DateTime(2026, 11, 16, 3, 0, 0, DateTimeKind.Utc);
            ApiResult c = await rig.Api.ClaimPrizeAsync("first", new LeaderboardApi.ClaimRequest { WeekKey = wk, Board = Boards.Overall });
            Assert.Equal(1, (int)Body(c).rank);
            Assert.Equal(0, (int)Body(c).prize.Bundles);
            Assert.Equal(0, (int)Body(c).prize.GoldChests);
            Assert.Equal(1, (int)Body(c).prize.IronChests);
            Assert.Equal(2, (int)Body(c).prize.PowerUps);
            ApiResult lv = await rig.Api.ClaimPrizeAsync("first", new LeaderboardApi.ClaimRequest { WeekKey = wk, Board = Boards.Levels });
            Assert.Equal(3000, (int)Body(lv).prize.Coins);   // Levels 4th-10th prize: 3000 coins + 1 power-up
        }
    }
}
