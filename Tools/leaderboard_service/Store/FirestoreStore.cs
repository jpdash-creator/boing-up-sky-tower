using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using Newtonsoft.Json;

namespace FluffyClimbers.LeaderboardService
{
    /// <summary>
    /// Production store: Firestore (native mode, region asia-south1 Mumbai), reached with the Cloud Run service account (application default credentials), which bypasses the security rules.
    /// NOT exercised by the local tests (no emulator is installed on the dev machine); the logic it wraps (SlotMath, Prizes, WeekMath) is. Review the transaction bodies when the emulator is available.
    /// </summary>
    public sealed class FirestoreStore : ILeaderboardStore
    {
        readonly FirestoreDb db;

        public FirestoreStore(string projectId) { db = FirestoreDb.Create(projectId); }

        DocumentReference Player(string uid) { return db.Collection("players").Document(uid); }
        DocumentReference Slot(string wk, string uid, string slotId) { return db.Collection("weeks").Document(wk).Collection("slots").Document(uid + "_" + slotId); }
        DocumentReference Totals(string wk, string uid) { return db.Collection("weeks").Document(wk).Collection("totals").Document(uid); }
        CollectionReference TotalsCol(string wk) { return db.Collection("weeks").Document(wk).Collection("totals"); }
        DocumentReference Board(string wk, string board) { return db.Collection("weeks").Document(wk).Collection("boards").Document(board); }
        DocumentReference Claim(string id) { return db.Collection("prizeClaims").Document(id); }

        public async Task<Limits> GetLimitsAsync()
        {
            var limits = new Limits();
            DocumentSnapshot snap = await db.Collection("config").Document("leaderboard").GetSnapshotAsync();
            if (snap.Exists) JsonConvert.PopulateObject(JsonConvert.SerializeObject(snap.ToDictionary()), limits, new JsonSerializerSettings { ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver { NamingStrategy = new Newtonsoft.Json.Serialization.CamelCaseNamingStrategy() } });
            return limits;
        }

        public async Task<PlayerDoc> GetPlayerAsync(string uid)
        {
            DocumentSnapshot s = await Player(uid).GetSnapshotAsync();
            return s.Exists ? s.ConvertTo<PlayerDoc>() : null;
        }

        public Task<ApplyOutcome> ApplyRunAsync(ApplyInput input)
        {
            return db.RunTransactionAsync(async tx =>
            {
                DocumentReference slotRef = Slot(input.WeekKey, input.Uid, input.Replay.SlotId), totRef = Totals(input.WeekKey, input.Uid), plRef = Player(input.Uid);
                // all reads first (Firestore requires reads before writes inside a transaction)
                DocumentSnapshot slotSnap = await tx.GetSnapshotAsync(slotRef);
                DocumentSnapshot totSnap = await tx.GetSnapshotAsync(totRef);
                DocumentSnapshot plSnap = await tx.GetSnapshotAsync(plRef);
                SlotDoc slot = slotSnap.Exists ? slotSnap.ConvertTo<SlotDoc>() : null;
                TotalsDoc tot = totSnap.Exists ? totSnap.ConvertTo<TotalsDoc>() : null;
                PlayerDoc pl = plSnap.Exists ? plSnap.ConvertTo<PlayerDoc>() : null;

                SlotDoc ns; TotalsDoc nt; PlayerDoc np;
                ApplyOutcome o = SlotMath.Apply(input, slot, tot, pl, out ns, out nt, out np);
                if (!o.Accepted) return o;
                // client-owned fields (displayName, avatarUrl, updatedAt) are merged, never overwritten: the player doc is written with MergeFields of the server-owned ones only
                tx.Set(plRef, np, SetOptions.MergeFields("pid", "heroVariants", "lastSubmitAt", "dayKey", "ticksToday", "createdAt"));
                if (ns != null) tx.Set(slotRef, ns);
                if (nt != null) tx.Set(totRef, nt);
                return o;
            });
        }

        public async Task<PlayerWeek> GetPlayerWeekAsync(string weekKey, string uid, bool withSlots)
        {
            var w = new PlayerWeek();
            DocumentSnapshot t = await Totals(weekKey, uid).GetSnapshotAsync();
            if (t.Exists) w.Totals = t.ConvertTo<TotalsDoc>();
            if (withSlots)
            {
                QuerySnapshot q = await db.Collection("weeks").Document(weekKey).Collection("slots").WhereEqualTo("uid", uid).GetSnapshotAsync();
                foreach (DocumentSnapshot d in q.Documents) w.Slots.Add(d.ConvertTo<SlotDoc>());
            }
            return w;
        }

        public async Task<long> CountAheadAsync(string weekKey, string board, long crowns, DateTime at)
        {
            CollectionReference col = TotalsCol(weekKey);
            string f = board, fAt = board + "At";
            AggregateQuerySnapshot more = await col.WhereGreaterThan(f, crowns).Count().GetSnapshotAsync();
            AggregateQuerySnapshot tie = await col.WhereEqualTo(f, crowns).WhereLessThan(fAt, at).Count().GetSnapshotAsync();
            return (more.Count ?? 0) + (tie.Count ?? 0);
        }

        public async Task<BoardDoc> GetBoardAsync(string weekKey, string board)
        {
            DocumentSnapshot s = await Board(weekKey, board).GetSnapshotAsync();
            return s.Exists ? s.ConvertTo<BoardDoc>() : null;
        }

        public async Task<BoardDoc> RebuildBoardAsync(string weekKey, string board, Limits limits, DateTime now, bool final)
        {
            BoardDoc existing = await GetBoardAsync(weekKey, board);
            if (existing != null && existing.Final) return existing;
            // index: totals (board DESC, boardAt ASC), see firestore.indexes.json
            QuerySnapshot q = await TotalsCol(weekKey).OrderByDescending(board).OrderBy(board + "At").Limit(limits.BoardSize).GetSnapshotAsync();
            var doc = new BoardDoc { Board = board, WeekKey = weekKey, Entries = new List<BoardEntry>(), RebuiltAt = now, Final = final, LimitUsed = limits.EntryLimit(board), TopThreeFactorPercent = limits.TopThreeFactorPercent };
            DateTime expire = DateTime.MinValue;
            long last = 0; int n = 0;
            foreach (DocumentSnapshot d in q.Documents)
            {
                TotalsDoc t = d.ConvertTo<TotalsDoc>();
                long c = t.Get(board);
                if (c <= 0) continue;
                n++;
                doc.Entries.Add(new BoardEntry { Rank = n, Pid = t.Pid, Name = t.Name, Avatar = t.Avatar, Crowns = c });
                last = c;
                if (t.ExpireAt > expire) expire = t.ExpireAt;
            }
            doc.Cutoff = n >= limits.BoardSize ? last : 0;
            doc.ExpireAt = expire == DateTime.MinValue ? now.AddDays(limits.RetentionDays) : expire;
            // a final board is written with a precondition: only if no final board exists (two concurrent finalisations produce one result)
            await db.RunTransactionAsync(async tx =>
            {
                DocumentSnapshot cur = await tx.GetSnapshotAsync(Board(weekKey, board));
                if (cur.Exists && cur.GetValue<bool>("final")) return;
                tx.Set(Board(weekKey, board), doc);
            });
            BoardDoc stored = await GetBoardAsync(weekKey, board);
            return stored ?? doc;
        }

        public Task<ClaimDoc> CreateClaimIfAbsentAsync(ClaimDoc claim)
        {
            return db.RunTransactionAsync(async tx =>
            {
                DocumentReference r = Claim(ClaimIds.Make(claim.WeekKey, claim.Board, claim.Uid));
                DocumentSnapshot s = await tx.GetSnapshotAsync(r);
                if (s.Exists) return s.ConvertTo<ClaimDoc>();
                tx.Create(r, claim);
                return claim;
            });
        }

        public Task<ClaimDoc> AckClaimAsync(string claimId, string uid, DateTime now)
        {
            return db.RunTransactionAsync(async tx =>
            {
                DocumentReference r = Claim(claimId);
                DocumentSnapshot s = await tx.GetSnapshotAsync(r);
                if (!s.Exists) return (ClaimDoc)null;
                ClaimDoc c = s.ConvertTo<ClaimDoc>();
                if (c.Uid != uid) return null;
                if (c.State == "issued") { c.State = "granted"; c.AckedAt = now; tx.Update(r, new Dictionary<string, object> { { "state", "granted" }, { "ackedAt", now } }); }
                return c;
            });
        }

        public Task<bool> ConsumeTicketAsync(string ticketId, string uid, DateTime expireAt)
        {
            return db.RunTransactionAsync(async tx =>
            {
                DocumentReference r = db.Collection("tickets").Document(ticketId);
                DocumentSnapshot s = await tx.GetSnapshotAsync(r);
                if (s.Exists) return false;
                tx.Create(r, new Dictionary<string, object> { { "uid", uid }, { "at", DateTime.UtcNow }, { "expireAt", expireAt } });
                return true;
            });
        }

        public async Task<ClaimDoc> GetClaimAsync(string claimId)
        {
            DocumentSnapshot s = await Claim(claimId).GetSnapshotAsync();
            return s.Exists ? s.ConvertTo<ClaimDoc>() : null;
        }
    }
}
