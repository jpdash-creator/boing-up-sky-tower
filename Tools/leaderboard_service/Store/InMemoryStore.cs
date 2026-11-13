using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace FluffyClimbers.LeaderboardService
{
    /// <summary>Process-local store with the same semantics as the Firestore one (one lock = one transaction). For local runs (LB_STORE=memory) and the tests.</summary>
    public sealed class InMemoryStore : ILeaderboardStore
    {
        readonly object gate = new object();
        public Limits LimitsValue = new Limits();
        readonly Dictionary<string, PlayerDoc> players = new Dictionary<string, PlayerDoc>();
        readonly Dictionary<string, SlotDoc> slots = new Dictionary<string, SlotDoc>();      // weekKey|uid|slotId
        readonly Dictionary<string, TotalsDoc> totals = new Dictionary<string, TotalsDoc>(); // weekKey|uid
        readonly Dictionary<string, BoardDoc> boards = new Dictionary<string, BoardDoc>();   // weekKey|board
        readonly Dictionary<string, ClaimDoc> claims = new Dictionary<string, ClaimDoc>();

        static T Clone<T>(T o) where T : class { return o == null ? null : JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(o)); }

        public Task<Limits> GetLimitsAsync() { return Task.FromResult(LimitsValue); }

        public void SetProfile(string uid, string name, string avatar)
        {
            lock (gate)
            {
                PlayerDoc p;
                if (!players.TryGetValue(uid, out p)) players[uid] = p = new PlayerDoc();
                p.DisplayName = name; p.AvatarUrl = avatar;
            }
        }

        public Task<PlayerDoc> GetPlayerAsync(string uid) { lock (gate) { PlayerDoc p; return Task.FromResult(players.TryGetValue(uid, out p) ? Clone(p) : null); } }

        public Task<ApplyOutcome> ApplyRunAsync(ApplyInput input)
        {
            lock (gate)
            {
                SlotDoc slot; TotalsDoc tot; PlayerDoc pl;
                slots.TryGetValue(input.WeekKey + "|" + input.Uid + "|" + input.Replay.SlotId, out slot);
                totals.TryGetValue(input.WeekKey + "|" + input.Uid, out tot);
                players.TryGetValue(input.Uid, out pl);
                SlotDoc ns; TotalsDoc nt; PlayerDoc np;
                ApplyOutcome o = SlotMath.Apply(input, Clone(slot), Clone(tot), Clone(pl), out ns, out nt, out np);
                if (!o.Accepted) return Task.FromResult(o);
                players[input.Uid] = np;
                if (ns != null) slots[input.WeekKey + "|" + input.Uid + "|" + input.Replay.SlotId] = ns;
                if (nt != null) totals[input.WeekKey + "|" + input.Uid] = nt;
                o.Totals = Clone(o.Totals);
                return Task.FromResult(o);
            }
        }

        public Task<PlayerWeek> GetPlayerWeekAsync(string weekKey, string uid, bool withSlots)
        {
            lock (gate)
            {
                var w = new PlayerWeek();
                TotalsDoc t;
                if (totals.TryGetValue(weekKey + "|" + uid, out t)) w.Totals = Clone(t);
                if (withSlots)
                    foreach (var kv in slots) if (kv.Key.StartsWith(weekKey + "|" + uid + "|", StringComparison.Ordinal)) w.Slots.Add(Clone(kv.Value));
                return Task.FromResult(w);
            }
        }

        IEnumerable<TotalsDoc> WeekTotals(string weekKey) { return totals.Where(kv => kv.Key.StartsWith(weekKey + "|", StringComparison.Ordinal)).Select(kv => kv.Value); }

        public Task<long> CountAheadAsync(string weekKey, string board, long crowns, DateTime at)
        {
            lock (gate)
                return Task.FromResult((long)WeekTotals(weekKey).Count(t => t.Get(board) > crowns || (t.Get(board) == crowns && t.At(board) < at)));
        }

        public Task<BoardDoc> GetBoardAsync(string weekKey, string board)
        {
            lock (gate) { BoardDoc b; return Task.FromResult(boards.TryGetValue(weekKey + "|" + board, out b) ? Clone(b) : null); }
        }

        public Task<BoardDoc> RebuildBoardAsync(string weekKey, string board, Limits limits, DateTime now, bool final)
        {
            lock (gate)
            {
                BoardDoc old;
                if (boards.TryGetValue(weekKey + "|" + board, out old) && old.Final) return Task.FromResult(Clone(old));
                var rows = WeekTotals(weekKey).Where(t => t.Get(board) > 0)
                    .OrderBy(t => t, Comparer<TotalsDoc>.Create((a, b) => SlotMath.Compare(a.Get(board), a.At(board), a.Pid, b.Get(board), b.At(board), b.Pid)))
                    .Take(limits.BoardSize).ToList();
                var doc = new BoardDoc { Board = board, WeekKey = weekKey, Entries = new List<BoardEntry>(), RebuiltAt = now, Final = final, LimitUsed = limits.EntryLimit(board), TopThreeFactorPercent = limits.TopThreeFactorPercent };
                for (int i = 0; i < rows.Count; i++) doc.Entries.Add(new BoardEntry { Rank = i + 1, Pid = rows[i].Pid, Name = rows[i].Name, Avatar = rows[i].Avatar, Crowns = rows[i].Get(board) });
                doc.Cutoff = rows.Count >= limits.BoardSize ? rows[rows.Count - 1].Get(board) : 0;
                boards[weekKey + "|" + board] = doc;
                return Task.FromResult(Clone(doc));
            }
        }

        public Task<ClaimDoc> CreateClaimIfAbsentAsync(ClaimDoc claim)
        {
            lock (gate)
            {
                string id = ClaimIds.Make(claim.WeekKey, claim.Board, claim.Uid);
                ClaimDoc c;
                if (!claims.TryGetValue(id, out c)) claims[id] = c = Clone(claim);
                return Task.FromResult(Clone(c));
            }
        }

        public Task<ClaimDoc> AckClaimAsync(string claimId, string uid, DateTime now)
        {
            lock (gate)
            {
                ClaimDoc c;
                if (!claims.TryGetValue(claimId, out c) || c.Uid != uid) return Task.FromResult<ClaimDoc>(null);
                if (c.State == "issued") { c.State = "granted"; c.AckedAt = now; }
                return Task.FromResult(Clone(c));
            }
        }

        readonly HashSet<string> usedTickets = new HashSet<string>();
        public Task<bool> ConsumeTicketAsync(string ticketId, string uid, DateTime expireAt) { lock (gate) return Task.FromResult(usedTickets.Add(ticketId)); }

        public Task<ClaimDoc> GetClaimAsync(string claimId) { lock (gate) { ClaimDoc c; return Task.FromResult(claims.TryGetValue(claimId, out c) ? Clone(c) : null); } }
    }
}
