using System.Collections.Generic;
using Newtonsoft.Json;

namespace BoingUpSkyTower.Economy
{
    /// <summary>Best result per level. Stars is the count of earned stars, StarMask the bits (bit 0 = star 1 ...).</summary>
    public sealed class LevelProgress
    {
        [JsonProperty("stars")] public int Stars;
        [JsonProperty("starMask")] public int StarMask;
        [JsonProperty("bestScore")] public int BestScore;
        [JsonProperty("clears")] public int Clears;
    }

    /// <summary>Owned stock (docs/15 section 8.1). Only power-ups.</summary>
    public sealed class InventoryData
    {
        /// <summary>Power-up id (pw_flight ...) to owned count, 1..99. Zero counts are removed.</summary>
        [JsonProperty("powerups")] public Dictionary<string, int> Powerups = new Dictionary<string, int>();
    }

    /// <summary>
    /// Local, offline save (FR-X2 later syncs it with the server: server wins for currencies, max wins for progress).
    /// Plain data so it can be versioned and migrated.
    /// </summary>
    public sealed class SaveData
    {
        /// <summary>Version 3 added the character copies (docs/34); older saves are migrated on load (each owned character starts with 1 copy). Version 2 added the chest counter. Gear fields of older saves are ignored on load (gear was removed 2026-10-24).</summary>
        public const int CurrentVersion = 3;

        [JsonProperty("version")] public int Version = CurrentVersion;
        [JsonProperty("coins")] public int Coins;
        [JsonProperty("gems")] public int Gems;
        [JsonProperty("levels")] public Dictionary<string, LevelProgress> Levels = new Dictionary<string, LevelProgress>();
        /// <summary>Region ids unlocked by progress or by paying the gem skip. Region 1 is always unlocked and is not stored.</summary>
        [JsonProperty("unlockedRegions")] public List<string> UnlockedRegions = new List<string>();
        /// <summary>Endless mode: best floor per region id. Added with endless mode; old saves simply have no entry (no version bump needed).</summary>
        [JsonProperty("endlessBest")] public Dictionary<string, int> EndlessBest = new Dictionary<string, int>();
        /// <summary>Endless coin cap: the UTC day (yyyyMMdd) the counter below belongs to, and how many paying endless runs finished that day.</summary>
        [JsonProperty("endlessDay")] public string EndlessDay = "";
        [JsonProperty("endlessRunsToday")] public int EndlessRunsToday;
        /// <summary>Owned stock. Added with power-ups (docs/15); old saves load with an empty inventory (no version bump needed).</summary>
        [JsonProperty("inventory")] public InventoryData Inventory = new InventoryData();
        /// <summary>Obsolete since 2026-10-31 (no loadout any more): kept in the save for compatibility and ignored.</summary>
        [JsonProperty("loadout")] public List<string> Loadout = new List<string>();
        /// <summary>Characters owned through chests or the shop. Common characters of an unlocked region are owned without an entry.</summary>
        [JsonProperty("characters")] public List<string> Characters = new List<string>();
        /// <summary>Ids of region heroes whose one-time "New hero joins you!" reveal was confirmed (Momo needs none). Additive field, no version bump; old saves load empty, so an already unlocked hero gets its reveal once.</summary>
        [JsonProperty("heroesRevealed")] public List<string> HeroesRevealed = new List<string>();
        /// <summary>Chests opened so far: seeds the next chest roll so openings are repeatable.</summary>
        [JsonProperty("chestOpens")] public int ChestOpens;
        /// <summary>Gem pack ids (gempack_50 ...) that already received the once-only first-purchase double on this account. Additive field, no version bump.</summary>
        [JsonProperty("firstPurchase")] public List<string> FirstPurchase = new List<string>();
        /// <summary>Rewards tracking and claims (docs/26 section 4): day and week counters, claimed ids. Additive field, no version bump.</summary>
        [JsonProperty("rewards")] public RewardsData Rewards = new RewardsData();
        /// <summary>Gem bundles (docs/26 section 3): the saved offer set, the draw counter and the timer offer. Additive field, no version bump; see ProgressService.Bundles.cs.</summary>
        [JsonProperty("bundles")] public BundleSaveData Bundles = new BundleSaveData();
        /// <summary>Subscription state (docs/26 section 2). Additive field, no version bump; old saves load with no subscription.</summary>
        [JsonProperty("subscription")] public SubscriptionData Subscription = new SubscriptionData();
        /// <summary>Copies per character id (docs/34): the tier hero has its own id, so this is a count per (hero, tier). A missing key means not counted yet: an owned character then counts as 1 copy. 0 means all copies were merged away.</summary>
        [JsonProperty("copies")] public Dictionary<string, int> Copies = new Dictionary<string, int>();
        /// <summary>Account (docs/35). Additive fields, no version bump. accountMode: "" (not chosen yet), "guest" or "google". accountId is the Play Games player id of the linked account.</summary>
        [JsonProperty("accountMode")] public string AccountMode = "";
        [JsonProperty("accountId")] public string AccountId = "";
        /// <summary>UTC time (ISO 8601 round trip) of the last successful upload or download of the cloud save.</summary>
        [JsonProperty("lastCloudSyncUtc")] public string LastCloudSyncUtc = "";
        /// <summary>True once the player passed the login screen (Google or guest): it is shown only on the first launch.</summary>
        [JsonProperty("loginChosen")] public bool LoginChosen;
        /// <summary>UTC time (ISO 8601 round trip) of the last change, stamped by ProgressService on every save. Used to compare saves.</summary>
        [JsonProperty("lastPlayedUtc")] public string LastPlayedUtc = "";
        /// <summary>Every real-money purchase (docs/38): product id, kind, transaction id, time. Kept when progress is reset; restore and cloud sync read it. Additive field, no version bump.</summary>
        [JsonProperty("purchases")] public List<PurchaseRecord> Purchases = new List<PurchaseRecord>();
    }
}
