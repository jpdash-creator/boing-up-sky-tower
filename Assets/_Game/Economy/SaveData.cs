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
        /// <summary>Version 2 added the chest counter. Gear fields of older saves are ignored on load (gear was removed 2026-10-24).</summary>
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
        /// <summary>Characters owned through chests or the shop. Common characters of an unlocked region are owned without an entry.</summary>
        [JsonProperty("characters")] public List<string> Characters = new List<string>();
        /// <summary>Chests opened so far: seeds the next chest roll so openings are repeatable.</summary>
        [JsonProperty("chestOpens")] public int ChestOpens;
    }
}
