using BoingUpSkyTower.Economy;
using UnityEngine;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The hidden ADMIN tools gate (docs/38-settings-admin.md). Development builds always have the Admin button; release builds show it only after the
    /// hidden code (tap the version label 7 times in Settings, then type the code) was accepted. The code is AdminCode in Resources/Services/admin_config.json
    /// (default 0000-TEST). A wrong code does nothing.
    /// </summary>
    public static class AdminAccess
    {
        public const string ResourcePath = "Services/admin_config";
        const string UnlockedKey = "settings.adminunlocked";

        [System.Serializable]
        sealed class ConfigData { public string adminCode; }

        static string code;

        /// <summary>Tests: pretend this is (true) or is not (false) a development build. Null = the real Debug.isDebugBuild.</summary>
        public static bool? DevelopmentBuildOverride;

        /// <summary>The configured admin code (config file, else AdminGate.DefaultCode).</summary>
        public static string Code
        {
            get
            {
                if (code != null) return code;
                code = AdminGate.DefaultCode;
                try
                {
                    TextAsset t = Resources.Load<TextAsset>(ResourcePath);
                    ConfigData d = t != null ? JsonUtility.FromJson<ConfigData>(t.text) : null;
                    if (d != null && !string.IsNullOrWhiteSpace(d.adminCode)) code = d.adminCode.Trim();
                }
                catch (System.Exception e) { Debug.LogWarning("[Admin] admin_config.json unreadable: " + e.Message); }
                return code;
            }
        }

        /// <summary>Tests: replace the configured code (null = reload from the file next time).</summary>
        public static void SetCodeForTests(string value) { code = value; }

        /// <summary>True after the hidden code was accepted on this install (stays until Lock).</summary>
        public static bool Unlocked
        {
            get { return PlayerPrefs.GetInt(UnlockedKey, 0) == 1; }
            private set { PlayerPrefs.SetInt(UnlockedKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        static bool DevelopmentBuild { get { return DevelopmentBuildOverride ?? Debug.isDebugBuild; } }

        /// <summary>True when the Admin button is shown: development build, or the code was accepted.</summary>
        public static bool Available { get { return AdminGate.Available(DevelopmentBuild, Unlocked); } }

        /// <summary>Checks a typed code. Right: admin mode is on and true is returned. Wrong or empty: nothing changes, false.</summary>
        public static bool TryUnlock(string entered)
        {
            if (!AdminGate.CodeMatches(entered, Code)) return false;
            Unlocked = true;
            GameSettings.ApplyFlags();
            return true;
        }

        /// <summary>Turns the code-unlocked admin mode off again (release builds hide the button; the test flags go back to real behaviour).</summary>
        public static void Lock()
        {
            Unlocked = false;
            GameSettings.ApplyFlags();
        }
    }
}
