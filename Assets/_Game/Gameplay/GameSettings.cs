using UnityEngine;

namespace BoingTower.Gameplay
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
        const string TestUnlockKey = "settings.testunlock";
        const string PromoKey = "settings.launchpromo";

        /// <summary>True in a development build on a device (not in the editor tests): the testing toggles are offered and default to on.</summary>
        public static bool DevBuild { get { return Debug.isDebugBuild && !Application.isEditor; } }

        /// <summary>Testing aid: all levels and regions open. Only has an effect in a development build.</summary>
        public static bool TestUnlockAll
        {
            get { return DevBuild && PlayerPrefs.GetInt(TestUnlockKey, 1) == 1; }
            set { PlayerPrefs.SetInt(TestUnlockKey, value ? 1 : 0); PlayerPrefs.Save(); BoingTower.Economy.ProgressService.TestUnlockAll = value && DevBuild; }
        }

        /// <summary>
        /// Launch promotion of the shop (docs/14): on = packs cost their promotional price and the list price is shown crossed out.
        /// Default on. Placeholder for the remote config flag shop.launchPromo.enabled (a development build can flip the PlayerPref).
        /// </summary>
        public static bool LaunchPromo
        {
            get { return PlayerPrefs.GetInt(PromoKey, 1) == 1; }
            set { PlayerPrefs.SetInt(PromoKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Game difficulty (owner, 2026-10-19): scroll speed grows by this percent every 5 seconds.</summary>
        public static readonly int[] DifficultyStepPercent = { 20, 40, 60, 80, 100 };
        /// <summary>Chance in percent that a coin spot is a gem. Default 5, at most 10.</summary>
        public static readonly int[] DifficultyGemPercent = { 5, 5, 7, 9, 10 };
        public static readonly string[] DifficultyNames = { "Relaxed", "Easy", "Normal", "Hard", "Insane" };

        public static int Difficulty
        {
            get { return Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, 0), 0, DifficultyNames.Length - 1); }
            set { PlayerPrefs.SetInt(DifficultyKey, Mathf.Clamp(value, 0, DifficultyNames.Length - 1)); PlayerPrefs.Save(); }
        }

        public static string DifficultyLabel(int level)
        {
            return DifficultyNames[level] + "  (+" + DifficultyStepPercent[level] + "% / 5 s)";
        }

        public static readonly int[] TextPercents = { 100, 115, 130 };

        public static bool Sound { get { return PlayerPrefs.GetInt(SoundKey, 1) == 1; } set { PlayerPrefs.SetInt(SoundKey, value ? 1 : 0); PlayerPrefs.Save(); AudioManager.Refresh(); } }
        public static bool Music { get { return PlayerPrefs.GetInt(MusicKey, 1) == 1; } set { PlayerPrefs.SetInt(MusicKey, value ? 1 : 0); PlayerPrefs.Save(); AudioManager.Refresh(); } }
        public static bool Haptics { get { return PlayerPrefs.GetInt(HapticsKey, 1) == 1; } set { PlayerPrefs.SetInt(HapticsKey, value ? 1 : 0); PlayerPrefs.Save(); } }

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
