using UnityEngine;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>Player preferences (doc 01 FR-X3). Stored in PlayerPrefs; audio and haptics read these when they exist.</summary>
    public static class GameSettings
    {
        const string SoundKey = "settings.sound";
        const string MusicKey = "settings.music";
        const string HapticsKey = "settings.haptics";
        const string ReducedKey = "settings.reducedmotion";
        const string LeftKey = "settings.lefthanded";
        const string TextKey = "settings.textpercent";
        const string DifficultyKey = "settings.difficulty";
        const string GameJumpKey = "settings.gamejumptest";
        const string SystemTestKey = "settings.systemtest";
        const string PromoKey = "settings.launchpromo";
        const string FirstPurchaseKey = "settings.firstpurchasedouble";

        /// <summary>True in a development build on a device (not in the editor tests): the testing toggles are offered and default to on.</summary>
        public static bool DevBuild { get { return (Debug.isDebugBuild && !Application.isEditor) || AdminAccess.Unlocked; } }

        /// <summary>Tests set this to build the Settings screen with the two dev flag rows (GAME_JUMP_TEST, SYSTEM_TEST) in the editor, where DevBuild is false. It changes only the layout.</summary>
        public static bool ShowDevRowsForTests;

        /// <summary>
        /// Dev flag GAME_JUMP_TEST (development builds only, default OFF): on = every level and region open; off = levels and regions follow real progress
        /// (home, shop, chests and characters stay as they are). Only counts while SYSTEM_TEST is on; with SYSTEM_TEST off it is ignored.
        /// </summary>
        public static bool GameJumpTest
        {
            get { return DevBuild && PlayerPrefs.GetInt(GameJumpKey, 0) == 1; }
            set { PlayerPrefs.SetInt(GameJumpKey, value ? 1 : 0); PlayerPrefs.Save(); ApplyFlags(); }
        }

        /// <summary>
        /// Dev flag SYSTEM_TEST (development builds only, default ON): on = everything open (levels, regions, all characters, test purchases, Autopilot);
        /// off = fully real production behaviour. Release builds are always real.
        /// </summary>
        public static bool SystemTest
        {
            get { return DevBuild && PlayerPrefs.GetInt(SystemTestKey, Debug.isDebugBuild ? 1 : 0) == 1; }   // a release build opened with the admin code starts with it OFF
            set { PlayerPrefs.SetInt(SystemTestKey, value ? 1 : 0); PlayerPrefs.Save(); ApplyFlags(); }
        }

        /// <summary>The effective "every level and region open" state: SYSTEM_TEST and GAME_JUMP_TEST both on (the single value ProgressService.TestUnlockAll is driven from).</summary>
        public static bool TestUnlockAll { get { return EffectiveUnlockAll(DevBuild, SystemTest, GameJumpTest); } }

        /// <summary>The flag matrix as pure functions (tested): levels and regions open only when SYSTEM_TEST and GAME_JUMP_TEST are both on; release builds are always real.</summary>
        public static bool EffectiveUnlockAll(bool devBuild, bool systemTest, bool gameJumpTest) { return devBuild && systemTest && gameJumpTest; }

        /// <summary>All characters owned, test gem purchases and dev tools (Autopilot): only SYSTEM_TEST in a development build.</summary>
        public static bool EffectiveSystemTools(bool devBuild, bool systemTest) { return devBuild && systemTest; }

        /// <summary>Pushes the flags into ProgressService (levels, regions, all characters). Called on boot and when a flag changes.</summary>
        public static void ApplyFlags()
        {
            BoingUpSkyTower.Economy.ProgressService.TestUnlockAll = TestUnlockAll;
            BoingUpSkyTower.Economy.ProgressService.TestAllCharacters = SystemTest;
        }

        /// <summary>
        /// Launch promotion of the shop (docs/14): on = packs cost their promotional price and the list price is shown crossed out.
        /// Default on. Placeholder for the remote config flag shop.launchPromo.enabled (a development build can flip the PlayerPref).
        /// </summary>
        public static bool LaunchPromo
        {
            get { return PlayerPrefs.GetInt(PromoKey, 0) == 1; }   // off for now (owner issue 16.2)
            set { PlayerPrefs.SetInt(PromoKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>
        /// First-purchase double (docs/22 4.1): on = the first purchase of each gem pack of 50 to 1,000 gems gives double gems, once per pack.
        /// Default OFF (owner 2026-10-25); the owner turns it on later (placeholder for a remote config flag, a development build can flip the PlayerPref).
        /// </summary>
        public static bool FirstPurchaseDouble
        {
            get { return PlayerPrefs.GetInt(FirstPurchaseKey, 0) == 1; }
            set { PlayerPrefs.SetInt(FirstPurchaseKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Game difficulty (owner, 2026-10-19): scroll speed grows by this percent every 5 seconds.</summary>
        public static readonly int[] DifficultyStepPercent = { 20, 40, 60, 80, 100 };
        /// <summary>Chance in per mille that a gem spot (one per level row) holds a gem (owner 2026-10-25: halved; tuned values live in SimConfig).</summary>
        public static readonly int[] DifficultyGemPermille = BoingUpSkyTower.Simulation.SimConfig.DifficultyGemPermille;
        public static readonly string[] DifficultyNames = { "Relaxed", "Easy", "Normal", "Hard", "Insane" };

        public static int Difficulty
        {
            get { return Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, 0), 0, DifficultyNames.Length - 1); }
            set { PlayerPrefs.SetInt(DifficultyKey, Mathf.Clamp(value, 0, DifficultyNames.Length - 1)); PlayerPrefs.Save(); }
        }

        /// <summary>Cap of the stepped speed-up in percent (GameSession.ApplyDifficulty uses the same value).</summary>
        public const int SpeedUpCapPercent = 150;

        /// <summary>Highest speed-up the steps can reach on a difficulty: ScrollStepMaxSteps steps of DifficultyStepPercent, limited by the cap.</summary>
        public static int MaxSpeedUpPercent(int level)
        {
            int p = DifficultyStepPercent[level] * new BoingUpSkyTower.Simulation.SimConfig().ScrollStepMaxSteps;
            return p > SpeedUpCapPercent ? SpeedUpCapPercent : p;
        }

        public static string DifficultyLabel(int level)
        {
            return DifficultyNames[level] + "  (up to +" + MaxSpeedUpPercent(level) + "%)";
        }

        public static readonly int[] TextPercents = { 100, 115, 130 };

        public static bool Sound { get { return PlayerPrefs.GetInt(SoundKey, 1) == 1; } set { PlayerPrefs.SetInt(SoundKey, value ? 1 : 0); PlayerPrefs.Save(); AudioManager.Refresh(); } }
        public static bool Music { get { return PlayerPrefs.GetInt(MusicKey, 1) == 1; } set { PlayerPrefs.SetInt(MusicKey, value ? 1 : 0); PlayerPrefs.Save(); AudioManager.Refresh(); } }
        public static bool Haptics { get { return PlayerPrefs.GetInt(HapticsKey, 0) == 1; } set { PlayerPrefs.SetInt(HapticsKey, value ? 1 : 0); PlayerPrefs.Save(); } }

        /// <summary>No screen shake, squash, pulses or parallax scrolling (CUX-6).</summary>
        public static bool ReducedMotion { get { return PlayerPrefs.GetInt(ReducedKey, 0) == 1; } set { PlayerPrefs.SetInt(ReducedKey, value ? 1 : 0); PlayerPrefs.Save(); } }

        /// <summary>Mirrors the run HUD so the pause button and wallet sit under the left thumb (CUX-4).</summary>
        public static bool LeftHanded { get { return PlayerPrefs.GetInt(LeftKey, 0) == 1; } set { PlayerPrefs.SetInt(LeftKey, value ? 1 : 0); PlayerPrefs.Save(); } }

        /// <summary>Accessibility text size: 100, 115 or 130 percent. Applied to every text built afterwards.</summary>
        public static int TextPercent
        {
            get { return PlayerPrefs.GetInt(TextKey, 100); }
            set { PlayerPrefs.SetInt(TextKey, value); PlayerPrefs.Save(); ApplyTextScale(); }
        }

        public static void ApplyTextScale() { UiKit.TextScale = Mathf.Clamp(TextPercent, 100, 130) / 100f; }

        public static void NextTextPercent()
        {
            int i = System.Array.IndexOf(TextPercents, TextPercent);
            TextPercent = TextPercents[(i + 1) % TextPercents.Length];
        }
    }
}
