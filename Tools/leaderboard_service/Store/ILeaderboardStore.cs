using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FluffyClimbers.LeaderboardService
{
    public sealed class PlayerWeek
    {
        public TotalsDoc Totals;
        public List<SlotDoc> Slots = new List<SlotDoc>();
    }

    /// <summary>Persistence of the leaderboard. Two implementations: <see cref="FirestoreStore"/> (production, Mumbai) and <see cref="InMemoryStore"/> (local runs and tests). Every method that writes is atomic per call (a Firestore transaction).</summary>
    public interface ILeaderboardStore
    {
        /// <summary>Limits from config/leaderboard merged over the defaults (cached by the caller).</summary>
        Task<Limits> GetLimitsAsync();

        /// <summary>The player's profile (display name, avatar) as the client wrote it; null when none.</summary>
        Task<PlayerDoc> GetPlayerAsync(string uid);

        /// <summary>Atomically: read slot, totals, player; apply <see cref="SlotMath.Apply"/>; write what changed.</summary>
        Task<ApplyOutcome> ApplyRunAsync(ApplyInput input);

        Task<PlayerWeek> GetPlayerWeekAsync(string weekKey, string uid, bool withSlots);

        /// <summary>Number of players strictly ahead of (crowns, at) on a board: more crowns, or equal crowns reached earlier.</summary>
        Task<long> CountAheadAsync(string weekKey, string board, long crowns, DateTime at);

        Task<BoardDoc> GetBoardAsync(string weekKey, string board);

        /// <summary>Builds the top <c>BoardSize</c> from the totals and writes the board document (not when it is already final).</summary>
        Task<BoardDoc> RebuildBoardAsync(string weekKey, string board, Limits limits, DateTime now, bool final);

        /// <summary>Creates the claim if none exists and returns the stored one (the existing claim when called twice: idempotent). All in one transaction.</summary>
        Task<ClaimDoc> CreateClaimIfAbsentAsync(ClaimDoc claim);

        /// <summary>The player confirms the prize was saved on the device: state issued becomes granted. Returns the claim, null when none.</summary>
        Task<ClaimDoc> AckClaimAsync(string claimId, string uid, DateTime now);

        Task<ClaimDoc> GetClaimAsync(string claimId);

        /// <summary>Marks a run ticket as used (create-if-absent in one transaction). False when it was already used. The record expires with the ticket (TTL on tickets.expireAt).</summary>
        Task<bool> ConsumeTicketAsync(string ticketId, string uid, DateTime expireAt);
    }

    public static class ClaimIds
    {
        public static string Make(string weekKey, string board, string uid) { return weekKey + "_" + board + "_" + uid; }
    }
}
