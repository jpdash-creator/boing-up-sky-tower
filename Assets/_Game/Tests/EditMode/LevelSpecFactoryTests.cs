using System.Collections.Generic;
using System.IO;
using SkyHopper.Content;
using SkyHopper.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace SkyHopper.Tests
{
    // End-to-end content check inside Unity: ContentRepository -> LevelSpecFactory -> LevelBuilder.
    // The hashes must equal the dotnet-generated golden file (Content/golden/level_hashes_r1-5.txt), proving both toolchains build bit-identical levels.
    public class LevelSpecFactoryTests
    {
        static string GoldenPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Content", "golden", "level_hashes_r1-5.txt"));
        }

        [Test]
        public void AllRegions1To5LevelsMatchTheDotnetGoldenHashes()
        {
            var repo = ContentRepository.LoadFromResources();
            var cfg = new SimConfig();
            var golden = new Dictionary<string, string>();
            foreach (var line in File.ReadAllLines(GoldenPath()))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(' ');
                golden[parts[0]] = parts[1];
            }
            Assert.AreEqual(75, golden.Count);

            int checkedLevels = 0;
            for (int r = 1; r <= 5; r++)
            {
                var region = repo.GetRegionByIndex(r);
                foreach (var level in repo.LevelsOf(region.Id))
                {
                    var spec = LevelSpecFactory.Build(level, region);
                    var data = LevelBuilder.Build(cfg, spec);
                    Assert.AreEqual(golden[level.Id], data.ComputeHash().ToString(), "hash mismatch for " + level.Id);
                    checkedLevels++;
                }
            }
            Assert.AreEqual(75, checkedLevels);
        }

        [Test]
        public void SpecMappingKeepsGoalStarsSignatureAndOverrides()
        {
            var repo = ContentRepository.LoadFromResources();
            var l15 = LevelSpecFactory.Build(repo, "lvl_005_15");
            Assert.AreEqual(GoalType.BossChase, l15.Goal.Type);
            Assert.AreEqual(250, l15.Goal.Value);
            Assert.AreEqual(4, l15.Class);
            Assert.IsTrue(l15.Ramp);
            CollectionAssert.AreEqual(new[] { "chunk_gauntlet_a" }, l15.FixedChunks);
            Assert.AreEqual(SignatureKind.SwingingGongs, l15.Signature);
            Assert.AreEqual(3, l15.Stars.Length);
            Assert.AreEqual(StarSpec.KindClear, l15.Stars[0].Kind);
            Assert.AreEqual(StarSpec.KindCoins, l15.Stars[1].Kind);
            Assert.AreEqual(StarSpec.KindNoRevive, l15.Stars[2].Kind);

            var l1 = LevelSpecFactory.Build(repo, "lvl_001_01");
            Assert.AreEqual(SignatureKind.BounceFlowers, l1.Signature);
            Assert.AreEqual(GoalType.ReachFloor, l1.Goal.Type);
            Assert.AreEqual(0, l1.Class);
            Assert.IsFalse(l1.Ramp);
        }

        [Test]
        public void BuiltLevelIsPlayableFromTheStartPlatform()
        {
            var repo = ContentRepository.LoadFromResources();
            var cfg = new SimConfig();
            var spec = LevelSpecFactory.Build(repo, "lvl_001_01");
            var data = LevelBuilder.Build(cfg, spec);
            var sim = new Sim(cfg, data, spec);
            var s = sim.NewState();
            sim.Step(ref s, SimInput.Jump);
            Assert.IsFalse(s.Grounded);
            Assert.IsFalse(s.Dead);
        }

        [Test]
        public void UnknownLevelClassThrowsWithTheName()
        {
            Assert.Throws<ContentException>(() => LevelSpecFactory.ClassFromName("legendary"));
        }
    }
}
