using System.IO;
using SkyHopper.Content;
using SkyHopper.Economy;
using SkyHopper.Simulation;
using UnityEngine;

namespace SkyHopper.Gameplay
{
    /// <summary>What the Run scene hands back to the Meta scene.</summary>
    public sealed class RunResult
    {
        public string LevelId;
        public bool Completed;
        public RewardResult Reward;
        public int Stars;
        public int Score;
        /// <summary>Endless run: the floor reached, the coins paid and whether it is a new best (LevelId is then null, RegionId is set).</summary>
        public bool Endless;
        public string RegionId;
        public int Floors;
        public EndlessReward EndlessReward;
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
            Cfg.ScrollStepCapPercent = 150;   // the stepped speed-up stops at +150 percent: beyond it no player can out-climb the screen (level audit 2026-10-20)
        }

        public static string CurrentLevelId;
        /// <summary>
        /// Endless mode, set by the level select before it loads the Run scene (owner decision 2026-10-20): the Run scene then plays the endless
        /// tower of EndlessRegionId instead of CurrentLevelId. EndlessSeed is the seed of the current tower (stored with the run; 0 = pick a new one).
        /// </summary>
        public static bool EndlessMode;
        public static string EndlessRegionId;
        public static int EndlessSeed;

        /// <summary>Starts an endless run next time the Run scene loads. A new random seed is drawn for every run.</summary>
        public static void SelectEndless(string regionId)
        {
            EndlessMode = true;
            EndlessRegionId = regionId;
            EndlessSeed = 0;
        }

        /// <summary>A fresh endless seed: UTC ticks and the region mixed, never 0.</summary>
        public static int NewEndlessSeed()
        {
            unchecked
            {
                long t = System.DateTime.UtcNow.Ticks;
                int seed = (int)(t ^ (t >> 32)) * 1103515245 + 12345;
                return seed == 0 ? 1 : seed;
            }
        }
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
            EndlessMode = false;
            EndlessRegionId = null;
            EndlessSeed = 0;
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
            EndlessMode = false;
            EndlessRegionId = null;
            EndlessSeed = 0;
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
