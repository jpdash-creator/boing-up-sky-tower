using System;
using System.Collections.Generic;
using System.IO;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Reads SOLVABILITY_LADDER.csv (the recorded result of the tiered solvability ladder, owner rule 2026-11-12: Relaxed, Easy and Normal are solvable by a Common or
    /// Uncommon bot, Hard and Insane by tier 3 at the latest). The Momo-only audits (Region2AuditTests, StackRuleTests) defer to it for the few levels where the ladder
    /// recorded that Momo alone is not the winner (the strongest Uncommon hero solved it) or that the search was slow (45 s or more, or a longer rerun): a Momo-only
    /// failure there is the documented expectation, not a regression.
    /// </summary>
    public static class LadderKnown
    {
        static HashSet<string> known;

        static void Load()
        {
            if (known != null) return;
            known = new HashSet<string>();
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            string path = Path.Combine(root, "Tests", "Simulation", "SOLVABILITY_LADDER.csv");
            if (!File.Exists(path)) return;
            foreach (string line in File.ReadAllLines(path))
            {
                string[] f = line.Split(',');
                if (f.Length < 9 || f[0] == "region") continue;
                double win;
                bool slow = double.TryParse(f[5], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out win) && win >= 45.0;
                if (f[4].Contains("cookie_u") || f[4].Contains("rerun") || slow) known.Add(f[1] + "|" + f[2]);
            }
        }

        /// <summary>True when the ladder recorded this level and setting as needing the Uncommon hero or a long search.</summary>
        public static bool Expected(string levelId, string settingName)
        {
            Load();
            return known.Contains(levelId + "|" + settingName);
        }

        public static string SettingName(int mode)
        {
            return mode == 0 ? "Relaxed" : mode == 1 ? "Easy" : mode == 2 ? "Normal" : mode == 3 ? "Hard" : "Insane";
        }
    }
}
