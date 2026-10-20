using System.Collections.Generic;
using Newtonsoft.Json;

namespace BoingTower.Economy
{
    /// <summary>Best result per level. Stars is the count of earned stars, StarMask the bits (bit 0 = star 1 ...).</summary>
    public sealed class LevelProgress
    {
        [JsonProperty("stars")] public int Stars;
        [JsonProperty("starMask")] public int StarMask;
        [JsonProperty("bestScore")] public int BestScore;
        [JsonProperty("clears")] public int Clears;
    }

    /// <summary>
    /// Local, offline save (FR-X2 later syncs it with the server: server wins for currencies, max wins for progress).
    /// Plain data so it can be versioned and migrated.
    /// </summary>
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

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
    }
}
