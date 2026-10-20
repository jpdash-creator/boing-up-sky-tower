using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace SkyHopper.Simulation.Tests
{
    /// <summary>
    /// Pins the exact LevelData hash of all 75 levels of regions 1-5 in Content/golden/level_hashes_r1-5.txt.
    /// The Unity EditMode test (LevelSpecFactoryTests) builds the same levels through ContentRepository + LevelSpecFactory
    /// and must produce the same hashes: this is the cross-toolchain determinism contract (dotnet vs Unity Mono).
    /// Regenerate deliberately with the environment variable UPDATE_GOLDEN=1 after an intended generator change.
    /// </summary>
    public class GoldenHashTests
    {
        [Fact]
        public void LevelHashesMatchGoldenFile()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            string golden = Path.Combine(root, "Content", "golden", "level_hashes_r1-5.txt");

            SimConfig cfg = new SimConfig();
            List<string> lines = new List<string>();
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 1, 5))
            {
                LevelData data = LevelBuilder.Build(cfg, ll.Spec);
                lines.Add(ll.Id + " " + data.ComputeHash().ToString());
            }
            Assert.Equal(75, lines.Count);

            string actual = string.Join("\n", lines) + "\n";
            if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1" || !File.Exists(golden))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(golden));
                File.WriteAllText(golden, actual);
                return;
            }
            string expected = File.ReadAllText(golden).Replace("\r\n", "\n");
            Assert.Equal(expected, actual);
        }
    }
}
