using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;

namespace FluffyClimbers.LeaderboardService
{
    /// <summary>A transport-free result: HTTP status and a JSON-serialisable body. Program.cs turns it into an HTTP response; the tests call the API directly.</summary>
    public sealed class ApiResult
    {
        public int Status;
        public object Body;
        public static ApiResult Ok(object body) { return new ApiResult { Status = 200, Body = body }; }
        public static ApiResult Fail(int status, string error, string detail = null) { return new ApiResult { Status = status, Body = new { error, detail } }; }
    }

    public sealed class RankInfo { public string Board { get; set; } public long Crowns { get; set; } public int Rank { get; set; } public long EntryLimit { get; set; } public bool ReachedLimit { get; set; } }

    /// <summary>Request handlers. uid always comes from the verified Firebase token, never from the body.</summary>
    public sealed class LeaderboardApi
    {
        readonly ILeaderboardStore store;
        readonly RunReplayer replayer;
        readonly Func<DateTime> now;
        readonly string pidSalt;
        readonly SemaphoreSlim replaySlots = new SemaphoreSlim(Math.Max(1, Environment.ProcessorCount), Math.Max(1, Environment.ProcessorCount));
        readonly ConcurrentDictionary<string, long> lastSeenMs = new ConcurrentDictionary<string, long>();
        Limits limits = new Limits();
        DateTime limitsAt = DateTime.MinValue;

        public readonly TicketService Tickets;

        public LeaderboardApi(ILeaderboardStore store, ContentRepository repo, Func<DateTime> now, string pidSalt)
        {
            this.store = store; this.replayer = new RunReplayer(repo); this.now = now; this.pidSalt = pidSalt ?? "";
            Tickets = new TicketService(this.pidSalt, now);
        }

        public sealed class StartRunRequest { public string Kind { get; set; } public string LevelId { get; set; } public string RegionId { get; set; } public int Mode { get; set; } }

        // ------------------------------------------------------------------ POST /start-run

        /// <summary>Picks the seed on the server and returns it with a signed single-use ticket bound to uid, level or region and setting.</summary>
        public async Task<ApiResult> StartRunAsync(string uid, StartRunRequest req)
        {
            Limits lim = await LimitsAsync();
            string target;
            if (req == null || req.Mode < 0 || req.Mode >= TimeRecords.ModeCount || !replayer.TargetExists(req.Kind, req.LevelId, req.RegionId, lim.MaxRegion, out target)) return ApiResult.Fail(400, "bad_request");
            long ms = now().Ticks / TimeSpan.TicksPerMillisecond;
            long prev;
            bool had = lastStartMs.TryGetValue(uid, out prev);
            lastStartMs[uid] = ms;
            if (had && ms - prev < 500) return ApiResult.Fail(429, "rate_limited");
            if (lastStartMs.Count > 50000) lastStartMs.Clear();
            int seed = TicketService.NewSeed();
            string ticket = Tickets.Issue(uid, req.Kind, target, req.Mode, seed, lim.TicketMinutes);
            return ApiResult.Ok(new { ticket, seed, expiresAt = now().AddMinutes(lim.TicketMinutes) });
        }

        readonly ConcurrentDictionary<string, long> lastStartMs = new ConcurrentDictionary<string, long>();

        async Task<Limits> LimitsAsync()
        {
            if ((now() - limitsAt).TotalSeconds > 60) { limits = await store.GetLimitsAsync(); limitsAt = now(); }
            return limits;
        }

        /// <summary>The public id of a player on boards: HMAC-SHA256(salt, uid), first 8 bytes in hex.</summary>
        public string PidOf(string uid)
        {
            using (var h = new HMACSHA256(Encoding.UTF8.GetBytes(pidSalt)))
            {
                byte[] d = h.ComputeHash(Encoding.UTF8.GetBytes(uid));
                var sb = new StringBuilder(16);
                for (int i = 0; i < 8; i++) sb.Append(d[i].ToString("x2"));
                return sb.ToString();
            }
        }

        // ------------------------------------------------------------------ POST /submit-run

        public async Task<ApiResult> SubmitRunAsync(string uid, RunSubmission sub)
        {
            Limits lim = await LimitsAsync();
            // cheap in-process throttle first, so a flood never reaches the replay (the durable per-player limit is inside the transaction)
            long ms = now().Ticks / TimeSpan.TicksPerMillisecond;   // the injected clock (tests move it)
            long prev;
            bool had = lastSeenMs.TryGetValue(uid, out prev);
            lastSeenMs[uid] = ms;
            if (had && ms - prev < lim.MinSubmitIntervalMs / 2) return ApiResult.Fail(429, "rate_limited");
            if (lastSeenMs.Count > 50000) lastSeenMs.Clear();

            if (sub == null) return ApiResult.Fail(400, "bad_request");
            // ---- ticket: signature, expiry, binding to uid / level or region / setting / seed (single use is checked after the replay) ----
            TicketInfo tk;
            string terr = Tickets.Parse(sub.Ticket, out tk);
            if (terr != null) return ApiResult.Fail(terr == "ticket_expired" ? 410 : 403, terr);
            string target = sub.Kind == Kinds.Level ? sub.LevelId : sub.RegionId;
            if (tk.Uid != uid || tk.Kind != sub.Kind || tk.Target != target || tk.Mode != sub.Mode || tk.Seed != sub.Seed) return ApiResult.Fail(403, "ticket_mismatch");

            ReplayResult rr;
            await replaySlots.WaitAsync();
            try { rr = replayer.Replay(sub, lim); }
            finally { replaySlots.Release(); }
            if (!rr.Ok) return ApiResult.Fail(rr.Error == "unsupported_version" ? 426 : 422, rr.Error, rr.Detail);
            string mismatch = RunReplayer.CompareClaim(sub, rr);
            if (mismatch != null) return ApiResult.Fail(422, "mismatch", mismatch);

            // single use: the ticket is burned once the run is accepted as a valid replay (a run that does not replay leaves it usable)
            if (!await store.ConsumeTicketAsync(tk.Id, uid, tk.ExpiresAt.AddDays(1))) return ApiResult.Fail(409, "ticket_used");

            // campaign slots count COMPLETED levels only; Endless and Time Attack count the run when it ends
            if (sub.Kind == Kinds.Level && !rr.GoalReached)
                return ApiResult.Ok(new { accepted = true, improved = false, counted = false, reason = "level_not_completed", crown = rr.Crown, floors = rr.Floors });

            DateTime t = now();
            string weekKey = WeekMath.WeekKey(t);
            ApplyOutcome o = await store.ApplyRunAsync(new ApplyInput { Uid = uid, Pid = PidOf(uid), WeekKey = weekKey, Run = sub, Replay = rr, Now = t, Limits = lim });
            if (!o.Accepted) return ApiResult.Fail(o.Error == "rate_limited" ? 429 : 422, o.Error);

            if (o.Improved) await MaybeRebuildAsync(weekKey, o, lim, t);
            return ApiResult.Ok(new
            {
                accepted = true, counted = true, improved = o.Improved, weekKey, slotId = rr.SlotId, crown = rr.Crown, previousCrown = o.PreviousCrown, slotCrown = o.SlotCrown, floors = rr.Floors, ticks = rr.Ticks,
                totals = o.Totals == null ? null : new { overall = o.Totals.Overall, levels = o.Totals.Levels, endless = o.Totals.Endless }
            });
        }

        /// <summary>Refreshes a board's materialised top 100 when the new total can be in it (below the 100th entry it cannot) and the last rebuild is older than BoardRebuildSeconds.</summary>
        async Task MaybeRebuildAsync(string weekKey, ApplyOutcome o, Limits lim, DateTime t)
        {
            foreach (string board in o.GrownBoards)
            {
                BoardDoc b = await store.GetBoardAsync(weekKey, board);
                long mine = o.Totals.Get(board);
                bool canEnter = b == null || b.Entries == null || b.Entries.Count < lim.BoardSize || mine >= b.Cutoff;
                bool stale = b == null || (t - b.RebuiltAt).TotalSeconds >= lim.BoardRebuildSeconds;
                if (canEnter && stale) await store.RebuildBoardAsync(weekKey, board, lim, t, false);
            }
        }

        // ------------------------------------------------------------------ GET /my-week

        public async Task<ApiResult> MyWeekAsync(string uid, bool withSlots, bool withPrizes)
        {
            Limits lim = await LimitsAsync();
            DateTime t = now();
            string weekKey = WeekMath.WeekKey(t);
            PlayerWeek w = await store.GetPlayerWeekAsync(weekKey, uid, withSlots);
            var ranks = new List<RankInfo>();
            foreach (string board in Boards.All)
            {
                long crowns = w.Totals == null ? 0 : w.Totals.Get(board);
                int rank = 0;
                if (crowns > 0) rank = (int)(1 + await store.CountAheadAsync(weekKey, board, crowns, w.Totals.At(board)));
                ranks.Add(new RankInfo { Board = board, Crowns = crowns, Rank = rank, EntryLimit = lim.EntryLimit(board), ReachedLimit = crowns >= lim.EntryLimit(board) });
            }
            List<object> pending = withPrizes ? await PendingPrizesAsync(uid, lim, t) : null;
            return ApiResult.Ok(new
            {
                weekKey, pid = PidOf(uid), weekEndsAt = WeekMath.WeekEndUtc(t), serverTime = t,
                totals = w.Totals == null ? new { overall = 0L, levels = 0L, endless = 0L } : new { overall = w.Totals.Overall, levels = w.Totals.Levels, endless = w.Totals.Endless },
                ranks,
                slots = withSlots ? w.Slots.ConvertAll(s => (object)new { s.SlotId, s.Crown, s.Floors, s.HeroId, s.Mode }) : null,
                pendingPrizes = pending
            });
        }

        // ------------------------------------------------------------------ prizes

        /// <summary>Closed weeks inside the claim window where the player has a paid rank: the board is frozen (final) on first look.</summary>
        async Task<List<object>> PendingPrizesAsync(string uid, Limits lim, DateTime t)
        {
            var result = new List<object>();
            string pid = PidOf(uid);
            DateTime start = WeekMath.WeekStartUtc(t);
            int weeks = (lim.ClaimWindowDays + 6) / 7;
            for (int back = 1; back <= weeks; back++)
            {
                string wk = WeekMath.WeekKey(start.AddDays(-7 * back));
                foreach (string board in Boards.All)
                {
                    BoardDoc b = await store.RebuildBoardAsync(wk, board, lim, t, true);   // no-op when already final
                    BoardEntry e = b.Entries == null ? null : b.Entries.Find(x => x.Pid == pid);
                    if (e == null) continue;
                    PrizeItems p = ResolveFrozen(b, e);
                    if (p.IsEmpty) continue;
                    ClaimDoc c = await store.GetClaimAsync(ClaimIds.Make(wk, board, uid));
                    result.Add(new { weekKey = wk, board, rank = e.Rank, crowns = e.Crowns, prize = p, state = c == null ? "open" : c.State });
                }
            }
            return result;
        }

        /// <summary>Prize with the limits that were frozen into the board when it closed (a later config change never changes a past week).</summary>
        static PrizeItems ResolveFrozen(BoardDoc b, BoardEntry e)
        {
            var frozen = new Limits { TopThreeFactorPercent = b.TopThreeFactorPercent == 0 ? 150 : b.TopThreeFactorPercent };
            if (b.Board == Boards.Overall) frozen.OverallEntryLimit = b.LimitUsed;
            else if (b.Board == Boards.Levels) frozen.LevelsEntryLimit = b.LimitUsed;
            else frozen.EndlessEntryLimit = b.LimitUsed;
            return Prizes.Resolve(frozen, b.Board, e.Rank, e.Crowns);
        }

        public sealed class ClaimRequest { public string WeekKey { get; set; } public string Board { get; set; } public bool Ack { get; set; } }

        /// <summary>
        /// POST /claim-prize. First call (Ack false): verifies the closed week, the player's frozen rank and the entry limits, creates prizeClaims/{weekKey}_{board}_{uid} in one transaction and returns the prize
        /// (grant true). Repeating the call while the claim is still "issued" returns the same prize again (the app may have crashed before saving it). With Ack true the app confirms it saved the prize and the claim
        /// becomes "granted"; from then on every call returns grant false. One claim per (week, board, player), ever.
        /// </summary>
        public async Task<ApiResult> ClaimPrizeAsync(string uid, ClaimRequest req)
        {
            Limits lim = await LimitsAsync();
            DateTime t = now();
            DateTime start;
            if (req == null || !Boards.IsValid(req.Board) || !WeekMath.TryStartOf(req.WeekKey, out start)) return ApiResult.Fail(400, "bad_request");
            string claimId = ClaimIds.Make(req.WeekKey, req.Board, uid);

            if (req.Ack)
            {
                ClaimDoc a = await store.AckClaimAsync(claimId, uid, t);
                return a == null ? ApiResult.Fail(404, "no_claim") : ApiResult.Ok(new { state = a.State, grant = false });
            }
            if (start.AddDays(7) > t) return ApiResult.Fail(409, "week_open");
            if (start.AddDays(7 + lim.ClaimWindowDays) < t) return ApiResult.Fail(410, "claim_window_closed");

            BoardDoc b = await store.RebuildBoardAsync(req.WeekKey, req.Board, lim, t, true);   // freezes the board on the first look
            string pid = PidOf(uid);
            BoardEntry e = b.Entries == null ? null : b.Entries.Find(x => x.Pid == pid);
            if (e == null) return ApiResult.Fail(403, "not_ranked");
            PrizeItems p = ResolveFrozen(b, e);
            if (p.IsEmpty) return ApiResult.Fail(403, "not_eligible", "rank " + e.Rank + ", crowns " + e.Crowns);

            ClaimDoc claim = await store.CreateClaimIfAbsentAsync(new ClaimDoc
            {
                Uid = uid, WeekKey = req.WeekKey, Board = req.Board, Rank = e.Rank, Crowns = e.Crowns, State = "issued", CreatedAt = t,
                Bundles = p.Bundles, GoldChests = p.GoldChests, IronChests = p.IronChests, PowerUps = p.PowerUps, Coins = p.Coins,
                ExpireAt = start.AddDays(7).AddDays(lim.RetentionDays)
            });
            return ApiResult.Ok(new { claimId, state = claim.State, grant = claim.State == "issued", rank = claim.Rank, crowns = claim.Crowns, prize = claim.Items });
        }

        // ------------------------------------------------------------------ admin (Cloud Scheduler, X-Admin-Key)

        /// <summary>Rebuilds the current week's three boards (safety net behind the on-write rebuild) and freezes last week's.</summary>
        public async Task<ApiResult> AdminTickAsync()
        {
            Limits lim = await LimitsAsync();
            DateTime t = now();
            string cur = WeekMath.WeekKey(t), prev = WeekMath.WeekKey(WeekMath.WeekStartUtc(t).AddDays(-7));
            foreach (string board in Boards.All)
            {
                await store.RebuildBoardAsync(cur, board, lim, t, false);
                await store.RebuildBoardAsync(prev, board, lim, t, true);
            }
            return ApiResult.Ok(new { rebuilt = cur, finalized = prev });
        }
    }
}
