using System;

namespace BoingUpSkyTower.Economy
{
    /// <summary>
    /// Pure rules of the hidden ADMIN tools (docs/38-settings-admin.md): who may see the Admin button, how the hidden code is checked, and the
    /// "tap the version label 7 times" counter. No engine types, so it is unit tested without Unity.
    /// </summary>
    public static class AdminGate
    {
        /// <summary>Built-in code when Resources/Services/admin_config.json is missing or empty. Documented in docs/38; change it in the config file before shipping.</summary>
        public const string DefaultCode = "0000-TEST";

        /// <summary>How many taps on the version label open the code box, and the longest pause between two taps.</summary>
        public const int TapsRequired = 7;
        public const double TapWindowSeconds = 2.0;

        /// <summary>The Admin button shows in development builds, and in release builds only after the hidden code was accepted.</summary>
        public static bool Available(bool developmentBuild, bool unlockedByCode) { return developmentBuild || unlockedByCode; }

        /// <summary>True when the typed code equals the configured one (spaces around it and letter case are ignored). An empty configured code never matches.</summary>
        public static bool CodeMatches(string entered, string expected)
        {
            if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(entered)) return false;
            return string.Equals(entered.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Counts quick taps: Tap returns true on the 7th tap in a row (each within 2 s of the previous), then starts over.</summary>
        public sealed class TapCounter
        {
            int count;
            double last = double.NegativeInfinity;

            public int Count { get { return count; } }

            public bool Tap(double nowSeconds)
            {
                if (nowSeconds - last > TapWindowSeconds) count = 0;
                last = nowSeconds;
                count++;
                if (count < TapsRequired) return false;
                count = 0;
                return true;
            }

            public void Reset() { count = 0; last = double.NegativeInfinity; }
        }
    }
}
