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

    /// <summary>Owned stock (docs/15 section 8.1): power-ups, chests, gear and Style Shards.</summary>
    public sealed class InventoryData
    {
        /// <summary>Power-up id (pw_flight ...) to owned count, 1..99. Zero counts are removed.</summary>
        [JsonProperty("powerups")] public Dictionary<string, int> Powerups = new Dictionary<string, int>();
        /// <summary>Unopened chests by chest id (coin_common ...) to count; zero counts are removed.</summary>
        [JsonProperty("chests")] public Dictionary<string, int> Chests = new Dictionary<string, int>();
        /// <summary>Owned gear pieces by id to style level (0 to 3). Presence means owned.</summary>
        [JsonProperty("gear")] public Dictionary<string, int> Gear = new Dictionary<string, int>();
        /// <summary>Style Shards from duplicate gear.</summary>
        [JsonProperty("shards")] public int Shards;
    }

    /// <summary>Characters (docs/15 section 7): bought ones, the selected one and the gear each one wears.</summary>
    public sealed class CharacterData
    {
        /// <summary>Bought character ids. Free characters count as owned once their region is open and are not listed here.</summary>
        [JsonProperty("owned")] public List<string> Owned = new List<string>();
        [JsonProperty("selected")] public string Selected = "char_momo";
        /// <summary>Character id to (slot name to gear id), slot names hat, shirt, pants, boots.</summary>
        [JsonProperty("equipped")] public Dictionary<string, Dictionary<string, string>> Equipped = new Dictionary<string, Dictionary<string, string>>();
    }

    /// <summary>VIP state (UI only for now, docs/15 section 6). Tier 0 = none.</summary>
    public sealed class VipData
    {
        [JsonProperty("tier")] public int Tier;
        [JsonProperty("expiresUtc")] public string ExpiresUtc = "";
        /// <summary>"none", "test" (development builds) or later the store.</summary>
        [JsonProperty("source")] public string Source = "none";
    }

    /// <summary>
    /// Local, offline save (FR-X2 later syncs it with the server: server wins for currencies, max wins for progress).
    /// Plain data so it can be versioned and migrated.
    /// </summary>
    public sealed class SaveData
    {
        public const int CurrentVersion = 2;

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
        /// <summary>Last power-up loadout: up to 3 power-up ids (empty string = empty slot), remembered between runs.</summary>
        [JsonProperty("loadout")] public List<string> Loadout = new List<string>();
        /// <summary>Version 2: characters, chest pity counters (key "kind.tier", for example "gear.rare") and VIP.</summary>
        [JsonProperty("characters")] public CharacterData Characters = new CharacterData();
        [JsonProperty("chestPity")] public Dictionary<string, int> ChestPity = new Dictionary<string, int>();
        [JsonProperty("vip")] public VipData Vip = new VipData();
        /// <summary>Chests opened so far (feeds the roll seed of the development builds).</summary>
        [JsonProperty("chestsOpened")] public int ChestsOpened;

        /// <summary>
        /// Brings any older save to the current shape: every added field gets a safe default (an old save has none of them) and the version is raised.
        /// Called by every store after loading; never throws.
        /// </summary>
        public void Migrate()
        {
            if (Inventory == null) Inventory = new InventoryData();
            if (Inventory.Powerups == null) Inventory.Powerups = new Dictionary<string, int>();
            if (Inventory.Chests == null) Inventory.Chests = new Dictionary<string, int>();
            if (Inventory.Gear == null) Inventory.Gear = new Dictionary<string, int>();
            if (Inventory.Shards < 0) Inventory.Shards = 0;
            if (Loadout == null) Loadout = new List<string>();
            if (Characters == null) Characters = new CharacterData();
            if (Characters.Owned == null) Characters.Owned = new List<string>();
            if (string.IsNullOrEmpty(Characters.Selected)) Characters.Selected = "char_momo";
            if (Characters.Equipped == null) Characters.Equipped = new Dictionary<string, Dictionary<string, string>>();
            if (ChestPity == null) ChestPity = new Dictionary<string, int>();
            if (Vip == null) Vip = new VipData();
            if (Vip.Tier < 0 || Vip.Tier > 6) Vip.Tier = 0;
            if (Version < CurrentVersion) Version = CurrentVersion;
        }
    }
}
