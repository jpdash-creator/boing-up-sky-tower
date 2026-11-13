using System;
using System.Collections.Generic;
using FluffyClimbers.Simulation;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// Device side of the weekly leaderboard (Planning/LEADERBOARD-DESIGN.md): consent flag, the offline upload queue and the idempotent grant of a weekly prize.
    /// The server decides scores, ranks and prizes; this only stores what the device must remember and turns prize item counts into inventory.
    /// </summary>
    public sealed partial class ProgressService
    {
        /// <summary>At most this many runs wait for upload (one per slot, so this is plenty).</summary>
        public const int LeaderboardMaxQueue = 12;
        /// <summary>A run with more base64 characters than this is not kept in the queue (the cloud save must stay small); it is only sent right away.</summary>
        public const int LeaderboardMaxRunChars = 120000;
        /// <summary>Total base64 characters of the whole queue.</summary>
        public const int LeaderboardMaxQueueChars = 400000;
        const int MaxGrantedClaims = 60;

        LeaderboardSaveData LbState
        {
            get
            {
                if (Data.Leaderboard == null) Data.Leaderboard = new LeaderboardSaveData();
                if (Data.Leaderboard.Queue == null) Data.Leaderboard.Queue = new List<QueuedRun>();
                if (Data.Leaderboard.GrantedClaims == null) Data.Leaderboard.GrantedClaims = new List<string>();
                if (Data.Leaderboard.DeclinedDay == null) Data.Leaderboard.DeclinedDay = "";
                return Data.Leaderboard;
            }
        }

        // ---------------- consent ----------------

        /// <summary>True when the player accepted the current consent text (an older version asks again).</summary>
        public bool HasLeaderboardConsent
        {
            get { return Data.LeaderboardConsent != null && Data.LeaderboardConsent.V >= LeaderboardConsent.CurrentVersion; }
        }

        /// <summary>Stores the consent flag and saves at once.</summary>
        public void GrantLeaderboardConsent(long unixSeconds)
        {
            Data.LeaderboardConsent = new LeaderboardConsent { V = LeaderboardConsent.CurrentVersion, At = unixSeconds };
            LbState.DeclinedDay = "";
            Commit();
        }

        /// <summary>"Leave leaderboards": clears the flag and the queue (nothing more is sent). Saves at once.</summary>
        public void RevokeLeaderboardConsent()
        {
            Data.LeaderboardConsent = null;
            LbState.Queue.Clear();
            Commit();
        }

        /// <summary>"Not now": remembers the UTC day so the popup is not shown again until tomorrow. Stores no consent.</summary>
        public void DeclineLeaderboardConsent(string dayKey)
        {
            LbState.DeclinedDay = dayKey ?? "";
            Commit();
        }

        /// <summary>The consent popup may be shown: no valid consent, and it was not declined today (<paramref name="dayKey"/> = yyyyMMdd, UTC).</summary>
        public bool ShouldAskLeaderboardConsent(string dayKey)
        {
            if (HasLeaderboardConsent) return false;
            if (string.IsNullOrEmpty(dayKey)) return true;
            return !string.Equals(LbState.DeclinedDay, dayKey, StringComparison.Ordinal);
        }

        // ---------------- display name ----------------

        /// <summary>Longest stored display name (the rules class in the Leaderboard assembly enforces 3 to 16; this is only the safety cap).</summary>
        public const int LeaderboardNameMaxStored = 16;

        /// <summary>The player's own leaderboard name, or "" when none is set (the Google Play Games name is shown then).</summary>
        public string LeaderboardDisplayName { get { return Data.LeaderboardDisplayName ?? ""; } }

        /// <summary>
        /// Stores an ALREADY CHECKED display name (LeaderboardHub.SetDisplayName runs LeaderboardNameRules first; Economy cannot see that assembly). Trims, refuses more than
        /// <see cref="LeaderboardNameMaxStored"/> characters, stores null for an empty name (back to the Play Games name). Returns false when nothing was stored. Saves at once on a change.
        /// </summary>
        public bool SetLeaderboardDisplayName(string checkedName)
        {
            string n = (checkedName ?? "").Trim();
            if (n.Length > LeaderboardNameMaxStored) return false;
            string now = Data.LeaderboardDisplayName ?? "";
            if (string.Equals(now, n, StringComparison.Ordinal)) return true;
            Data.LeaderboardDisplayName = n.Length == 0 ? null : n;
            Commit();
            return true;
        }

        // ---------------- upload queue ----------------

        /// <summary>The queued runs, oldest first (read only; change them through the methods below).</summary>
        public IReadOnlyList<QueuedRun> LeaderboardQueue { get { return LbState.Queue; } }

        /// <summary>
        /// Adds a run to the persisted queue. Refused (false) without consent, for a run that is too large to keep, or when the same slot already holds a run with an equal or higher crown.
        /// A better run of a slot replaces the queued one (a lower crown never changes a slot on the server anyway). The queue is bounded: the oldest runs go first.
        /// </summary>
        public bool EnqueueLeaderboardRun(QueuedRun run)
        {
            if (run == null || !HasLeaderboardConsent) return false;
            if (run.InputsB64 != null && run.InputsB64.Length > LeaderboardMaxRunChars) return false;
            List<QueuedRun> q = LbState.Queue;
            string key = run.SlotKey;
            for (int i = 0; i < q.Count; i++)
            {
                if (q[i].SlotKey != key) continue;
                if (q[i].Crown >= run.Crown) return false;
                q.RemoveAt(i);
                break;
            }
            q.Add(run);
            while (q.Count > LeaderboardMaxQueue) q.RemoveAt(0);
            while (q.Count > 1 && QueueChars(q) > LeaderboardMaxQueueChars) q.RemoveAt(0);
            Commit();
            return true;
        }

        static int QueueChars(List<QueuedRun> q)
        {
            int n = 0;
            for (int i = 0; i < q.Count; i++) n += q[i].InputsB64 != null ? q[i].InputsB64.Length : 0;
            return n;
        }

        /// <summary>The first queued run that is due (<c>NextTryUnix</c> reached and the ticket not expired), or null. Expired runs are dropped on the way.</summary>
        public QueuedRun NextDueLeaderboardRun(long nowUnix)
        {
            List<QueuedRun> q = LbState.Queue;
            bool dropped = false;
            for (int i = q.Count - 1; i >= 0; i--)
                if (q[i].ExpiresAtUnix > 0 && q[i].ExpiresAtUnix <= nowUnix) { q.RemoveAt(i); dropped = true; }
            if (dropped) Commit();
            for (int i = 0; i < q.Count; i++) if (q[i].NextTryUnix <= nowUnix) return q[i];
            return null;
        }

        /// <summary>Removes a run (sent, or refused for good by the server). Saves.</summary>
        public void RemoveLeaderboardRun(string runId)
        {
            List<QueuedRun> q = LbState.Queue;
            for (int i = 0; i < q.Count; i++)
                if (q[i].RunId == runId) { q.RemoveAt(i); Commit(); return; }
        }

        /// <summary>A failed attempt: the next one is later (30 s, 1 min, 2 min ... at most 30 min). After 12 failures the run is dropped. Saves.</summary>
        public void LeaderboardRunFailed(string runId, long nowUnix)
        {
            List<QueuedRun> q = LbState.Queue;
            for (int i = 0; i < q.Count; i++)
            {
                if (q[i].RunId != runId) continue;
                q[i].Tries++;
                if (q[i].Tries >= 12) { q.RemoveAt(i); Commit(); return; }
                q[i].NextTryUnix = nowUnix + BackoffSeconds(q[i].Tries);
                Commit();
                return;
            }
        }

        /// <summary>Seconds to wait after the n-th failure: 30 x 2^(n-1), at most 1800.</summary>
        public static long BackoffSeconds(int failures)
        {
            if (failures <= 0) return 0;
            long s = 30L << Math.Min(failures - 1, 6);
            return Math.Min(s, 1800L);
        }

        // ---------------- weekly prize ----------------

        /// <summary>True when this device already granted the prize of the claim (weekKey_board_uid).</summary>
        public bool LeaderboardPrizeGranted(string claimId)
        {
            return !string.IsNullOrEmpty(claimId) && LbState.GrantedClaims.Contains(claimId);
        }

        /// <summary>
        /// Turns a weekly prize (item counts from the server) into inventory, ONCE per claim id: a repeated call returns AlreadyClaimed and changes nothing.
        /// Chosen on the device from the claim id (repeatable, no Random): the power-up types (one per unit, cycling through the implemented types), the weekly bundle (a middle-tier
        /// bundle of the pool: its coins and power-ups plus its hidden hero card) and the hero card of every chest (the normal chest roll, no gems paid). One save holds everything.
        /// </summary>
        public RewardClaimOutcome GrantLeaderboardPrize(string claimId, int bundles, int goldChests, int ironChests, int powerUps, int coins)
        {
            var outcome = new RewardClaimOutcome { Result = RewardClaimResult.Invalid };
            if (string.IsNullOrEmpty(claimId) || bundles < 0 || goldChests < 0 || ironChests < 0 || powerUps < 0 || coins < 0) return outcome;
            if (bundles + goldChests + ironChests + powerUps + coins == 0) return outcome;
            LeaderboardSaveData lb = LbState;
            if (lb.GrantedClaims.Contains(claimId)) { outcome.Result = RewardClaimResult.AlreadyClaimed; return outcome; }
            var types = new List<PowerUpDef>(PowerUpDefs.Implemented);
            if (types.Count == 0) return outcome;

            ulong h = 1469598103934665603UL;   // FNV-1a of the claim id: the same claim always makes the same choices
            for (int i = 0; i < claimId.Length; i++) { h ^= claimId[i]; h *= 1099511628211UL; }
            int start = (int)(h % (ulong)types.Count);

            outcome.Coins += coins;
            for (int i = 0; i < powerUps; i++) AddPrizePowerUp(outcome, types[(start + i) % types.Count].Id);

            BundleSaveData b = BundleState;
            EnsureBundleSeed(b);
            var cards = new List<CharacterSpec>();
            for (int i = 0; i < goldChests + ironChests; i++)
            {
                ChestKind kind = i < goldChests ? ChestKind.Golden : ChestKind.Iron;
                ChestContents c = ChestRolls.Roll(kind, Data.ChestOpens++, types, CharacterDefs.All, IsBundleRegionOpen, b.Seed);
                foreach (string pid in c.PowerUps) AddPrizePowerUp(outcome, pid);
                outcome.Coins += c.Coins;
                if (c.Hero != null && CharacterInventory.Exists(c.Hero.Id)) cards.Add(c.Hero);
            }
            for (int i = 0; i < bundles; i++)
            {
                BundleDef def = PickPrizeBundle(h + (ulong)i * 7919UL);
                if (def == null) continue;
                outcome.Coins += def.Coins;
                foreach (BundlePowerUp p in def.PowerUps)
                    for (int n = 0; n < p.Count; n++) AddPrizePowerUp(outcome, p.Id);
                if (def.HiddenCard)
                {
                    CharacterSpec card = DrawBundleCard(b);
                    if (card != null && CharacterInventory.Exists(card.Id)) cards.Add(card);
                }
            }
            Data.Coins = SaturatingAdd(Data.Coins, outcome.Coins);
            lb.GrantedClaims.Add(claimId);
            while (lb.GrantedClaims.Count > MaxGrantedClaims) lb.GrantedClaims.RemoveAt(0);
            for (int i = 0; i < cards.Count; i++) outcome.HeroIds.Add(cards[i].Id);
            for (int i = 0; i < cards.Count; i++) AddCopies(cards[i].Id, 1);   // the existing API; its first save also holds everything above
            if (cards.Count == 0) Commit();
            outcome.Result = RewardClaimResult.Ok;
            return outcome;
        }

        void AddPrizePowerUp(RewardClaimOutcome outcome, string id)
        {
            Data.Inventory.Powerups[id] = SaturatingAdd(PowerUpCount(id), 1);   // no stock cap: a prize always gives its power-ups
            outcome.PowerUpIds.Add(id);
        }

        /// <summary>The weekly prize bundle: one of the middle-tier bundles of the pool (the highest tier when there is no middle one), picked by the hash.</summary>
        BundleDef PickPrizeBundle(ulong h)
        {
            var pool = new List<BundleDef>();
            int best = 0;
            foreach (BundleDef d in Bundles.All) if (d != null && d.Tier > best) best = d.Tier;
            int want = best >= 2 ? 2 : best;
            foreach (BundleDef d in Bundles.All) if (d != null && d.Tier == want) pool.Add(d);
            return pool.Count == 0 ? null : pool[(int)(h % (ulong)pool.Count)];
        }
    }
}
