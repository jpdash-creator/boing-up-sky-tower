using System.Collections.Generic;
using Newtonsoft.Json;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// The one-time leaderboard consent (Planning/LEADERBOARD-DESIGN.md section 5.7). Saved as <c>leaderboardConsent: { "v": 1, "at": unix seconds }</c>.
    /// No run is queued or sent, no profile written and no start-run called while it is missing or older than <see cref="CurrentVersion"/>.
    /// </summary>
    public sealed class LeaderboardConsent
    {
        /// <summary>Bump when the data shown or sent changes: the popup then asks again.</summary>
        public const int CurrentVersion = 1;
        [JsonProperty("v")] public int V;
        [JsonProperty("at")] public long At;
    }

    /// <summary>One finished run waiting for POST /submit-run (offline queue). Plain data so it survives an app restart; the ticket is single use and expires, see <see cref="ExpiresAtUnix"/>.</summary>
    public sealed class QueuedRun
    {
        [JsonProperty("runId")] public string RunId = "";
        [JsonProperty("ticket")] public string Ticket = "";
        /// <summary>"level", "endless" or "timeattack" (the service kinds).</summary>
        [JsonProperty("kind")] public string Kind = "level";
        [JsonProperty("levelId")] public string LevelId;
        [JsonProperty("regionId")] public string RegionId;
        [JsonProperty("mode")] public int Mode;
        [JsonProperty("seed")] public int Seed;
        [JsonProperty("heroId")] public string HeroId = "";
        [JsonProperty("heroVariant")] public int HeroVariant;
        [JsonProperty("simVersion")] public int SimVersion = 1;
        [JsonProperty("gameVersion")] public string GameVersion = "";
        [JsonProperty("inputsB64")] public string InputsB64 = "";
        [JsonProperty("reviveAt")] public int[] ReviveAt = new int[0];
        [JsonProperty("floors")] public int Floors;
        [JsonProperty("ticks")] public int Ticks;
        [JsonProperty("score")] public int Score;
        [JsonProperty("crown")] public long Crown;
        /// <summary>When the ticket stops being accepted (unix seconds). A run past it is dropped instead of sent.</summary>
        [JsonProperty("expiresAt")] public long ExpiresAtUnix;
        [JsonProperty("queuedAt")] public long QueuedAtUnix;
        /// <summary>Failed attempts so far; drives the backoff.</summary>
        [JsonProperty("tries")] public int Tries;
        /// <summary>Earliest time of the next attempt (unix seconds, 0 = now).</summary>
        [JsonProperty("nextTry")] public long NextTryUnix;

        /// <summary>The slot this run competes in (kind + target + setting): only the best queued run of a slot is kept.</summary>
        public string SlotKey { get { return Kind + "|" + (Kind == "level" ? LevelId : RegionId) + "|" + Mode; } }
    }

    /// <summary>Device-side leaderboard state (additive save field <c>leaderboard</c>, no version bump).</summary>
    public sealed class LeaderboardSaveData
    {
        /// <summary>Runs not uploaded yet, oldest first. Bounded by <see cref="ProgressService.LeaderboardMaxQueue"/>.</summary>
        [JsonProperty("queue")] public List<QueuedRun> Queue = new List<QueuedRun>();
        /// <summary>Claim ids (weekKey_board_uid) whose prize this device already granted: a repeated server answer is never granted twice.</summary>
        [JsonProperty("granted")] public List<string> GrantedClaims = new List<string>();
        /// <summary>UTC day (yyyyMMdd) the consent popup was last declined: it asks again at most once a day.</summary>
        [JsonProperty("declinedDay")] public string DeclinedDay = "";
    }
}
