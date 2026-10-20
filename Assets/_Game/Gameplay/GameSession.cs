using System.IO;
using BoingTower.Content;
using BoingTower.Economy;
using BoingTower.Simulation;
using UnityEngine;

namespace BoingTower.Gameplay
{
    /// <summary>What the Run scene hands back to the Meta scene.</summary>
    public sealed class RunResult
    {
        public string LevelId;
        public bool Completed;
        public RewardResult Reward;
        public int Stars;
        public int Score;
    }

    /// <summary>
    /// Static service shared by Boot, Meta and Run (kept across scene loads because it is plain static state).
    /// EnsureInit() is idempotent, so any scene can be played directly from the editor.
    /// </summary>
    public static class GameSession
    {
        /// <summary>Regions playable in this build (launch scope: five regions).</summary>
        public const int PlayableRegions = 5;

        public static bool Initialized { get; private set; }
        public static ContentRepository Repo { get; private set; }
        public static ProgressService Progress { get; private set; }
        public static ArtCatalog Art { get; private set; }
        public static readonly SimConfig Cfg = new SimConfig();

        /// <summary>Copies the player's difficulty setting into the simulation config (scroll growth per 5 s and gem chance).</summary>
        public static void ApplyDifficulty()
        {
            int d = GameSettings.Difficulty;
            Cfg.ScrollStepPercent = GameSettings.DifficultyStepPercent[d];
            Cfg.GemInsteadOfCoinPercent = GameSettings.DifficultyGemPercent[d];
            Cfg.ScrollStepCapPercent = 400;
        }

        public static string CurrentLevelId;
        /// <summary>Meta opens the level select of this region when it loads (set by the Run scene on exit).</summary>
        public static string PendingRegionId;
        public static RunResult LastResult;

        public static void EnsureInit()
        {
            if (Initialized) return;
            PerformanceSettings.Apply();
            ApplyDifficulty();
            ProgressService.TestUnlockAll = GameSettings.TestUnlockAll;
            GameSettings.ApplyTextScale();
            Repo = ContentRepository.LoadFromResources();
            Progress = new ProgressService(Repo, new FileSaveStore(Path.Combine(Application.persistentDataPath, "save.json")), PlayableRegions);
            Art = Resources.Load<ArtCatalog>("ArtCatalog");
            Initialized = true;
        }

        /// <summary>For tests: install a store (for example in memory) instead of the file save.</summary>
        public static void InitForTests(ISaveStore store, ArtCatalog art = null)
        {
            Repo = ContentRepository.LoadFromResources();
            Progress = new ProgressService(Repo, store, PlayableRegions);
            Art = art ?? Resources.Load<ArtCatalog>("ArtCatalog");
            CurrentLevelId = null;
            PendingRegionId = null;
            LastResult = null;
            Initialized = true;
        }

        public static void Reset()
        {
            Initialized = false;
            Repo = null;
            Progress = null;
            Art = null;
            CurrentLevelId = null;
            PendingRegionId = null;
            LastResult = null;
        }

        /// <summary>The next level after this one in play order, or null if it is the last playable one.</summary>
        public static LevelDef NextLevel(LevelDef level)
        {
            RegionDef region = Repo.GetRegion(level.Region);
            if (level.Index < region.Levels.Count) return Repo.GetLevel(region.Levels[level.Index]);
            RegionDef next = Repo.TryGetRegionByIndex(region.Index + 1);
            if (next == null || !Progress.CanEnterRegion(next)) return null;
            return Repo.GetLevel(next.Levels[0]);
        }
    }
}
