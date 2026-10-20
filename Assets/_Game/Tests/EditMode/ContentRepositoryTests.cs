using System;
using System.IO;
using System.Linq;
using System.Text;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace BoingUpSkyTower.Tests
{
    public class ContentRepositoryTests
    {
        static string ProjectRoot { get { return Path.GetDirectoryName(Application.dataPath); } }
        static string ManifestFile { get { return Path.Combine(ProjectRoot, "Assets", "Resources", "Content", "content_manifest.json"); } }

        static ContentRepository _repo;
        static ContentRepository Repo { get { return _repo ?? (_repo = ContentRepository.LoadFromJson(File.ReadAllText(ManifestFile))); } }

        [Test]
        public void ManifestLoadsFromResources()
        {
            ContentRepository r = ContentRepository.LoadFromResources();
            Assert.AreEqual(30, r.Regions.Count);
            Assert.AreEqual(Repo.ContentHash, r.ContentHash);
        }

        [Test]
        public void Has30Regions450LevelsAndModelsPresent()
        {
            Assert.AreEqual(30, Repo.Regions.Count);
            Assert.AreEqual(450, Repo.LevelCount);
            foreach (RegionDef reg in Repo.Regions)
            {
                var lv = Repo.LevelsOf(reg.Id);
                Assert.AreEqual(15, lv.Count, reg.Id);
                for (int i = 0; i < 15; i++) Assert.AreEqual(i + 1, lv[i].Index, reg.Id);
            }
            Assert.IsNotEmpty(Repo.Items);
            Assert.IsNotEmpty(Repo.GemPacks);
            Assert.IsNotEmpty(Repo.Modules);
            Assert.AreEqual(25, Repo.RemoteConfig.Ramp.RampFloors);
            Assert.AreEqual(100, Repo.RemoteConfig.Economy.CoinsPerGem);
        }

        [Test]
        public void Regions1To20AreLiveAnd21To30AreTeaser()
        {
            Assert.AreEqual(20, Repo.LiveRegions.Count);
            for (int i = 1; i <= 30; i++)
                Assert.AreEqual(i <= 20 ? "live" : "teaser", Repo.GetRegionByIndex(i).Status, "region " + i);
            Assert.AreEqual(1, Repo.Regions[0].Index);
            Assert.AreEqual(30, Repo.Regions[29].Index);
        }

        [Test]
        public void Region5Level15IsHighlyComplexWithFormulaDifficulty()
        {
            RegionDef reg = Repo.GetRegionByIndex(5);
            LevelDef l = Repo.GetLevel("lvl_005_15");
            Assert.AreEqual(reg.Id, l.Region);
            Assert.AreEqual(LevelDef.ClassHighlyComplex, l.LevelClass);
            Assert.AreEqual(1.0, l.Difficulty, 1e-9);
            Fx expected = LevelGenerator.EffectiveDifficulty(Fx.FromRatio(1000, 1000), Fx.FromRatio((int)Math.Round(reg.Rules.DifficultyOffset * 10000), 10000));
            Assert.AreEqual(expected, ContentRepository.EffectiveDifficulty(l, reg));
            Assert.AreEqual(Fx.One, ContentRepository.EffectiveDifficulty(l, reg));

            // mid-ladder level: d + offset * (1 - d), checked in plain double within Fx resolution
            LevelDef l5 = Repo.GetLevel("lvl_005_05");
            double d = l5.Difficulty, off = reg.Rules.DifficultyOffset;
            double formula = d + off * (1 - d);
            Fx fx = ContentRepository.EffectiveDifficulty(l5, reg);
            Assert.AreEqual(formula, fx.Raw / 65536.0, 0.002);
        }

        [Test]
        public void UnlockChainRequiresPreviousRegion()
        {
            Assert.IsNull(Repo.GetRegionByIndex(1).Unlock.Requires);
            for (int i = 2; i <= 30; i++)
                Assert.AreEqual(Repo.GetRegionByIndex(i - 1).Id, Repo.GetRegionByIndex(i).Unlock.Requires, "region " + i);
        }

        [Test]
        public void GemSkipValues()
        {
            Assert.AreEqual(100, Repo.GetRegionByIndex(2).Unlock.GemSkip);
            Assert.AreEqual(400, Repo.GetRegionByIndex(20).Unlock.GemSkip);
        }

        [Test]
        public void CoinPayoutScalesWithRegionFactor()
        {
            Assert.AreEqual(900, ContentRepository.CoinPayoutFor(Repo.GetLevel("lvl_001_15"), Repo.GetRegionByIndex(1)));
            Assert.AreEqual(1008, ContentRepository.CoinPayoutFor(Repo.GetLevel("lvl_005_15"), Repo.GetRegionByIndex(5)));
        }

        [Test]
        public void UnknownIdsThrowWithId()
        {
            var e1 = Assert.Throws<ContentException>(() => Repo.GetLevel("lvl_999_99"));
            StringAssert.Contains("lvl_999_99", e1.Message);
            var e2 = Assert.Throws<ContentException>(() => Repo.GetRegion("region_nope"));
            StringAssert.Contains("region_nope", e2.Message);
        }

        [Test]
        public void ResourcesManifestIsInSyncWithSourceContent()
        {
            string rebuilt = ContentManifestMerger.Build(Path.Combine(ProjectRoot, "Content"));
            string onDisk = File.ReadAllText(ManifestFile, Encoding.UTF8);
            Assert.IsTrue(rebuilt == onDisk,
                "content_manifest.json is stale: run Tools/Boing Up: Sky Tower/Build Content Manifest (or BuildFromCommandLine).");
            StringAssert.Contains(Repo.ContentHash, rebuilt);
        }

        [Test]
        public void MalformedLevelThrowsWithLevelId()
        {
            string text = File.ReadAllText(ManifestFile);
            // drop the seed of one level (property is required)
            int at = text.IndexOf("\"id\": \"lvl_005_07\"", StringComparison.Ordinal);
            Assert.GreaterOrEqual(at, 0);
            int seed = text.IndexOf("\"seed\":", at, StringComparison.Ordinal);
            string broken = text.Substring(0, seed) + "\"seedX\":" + text.Substring(seed + "\"seed\":".Length);
            var e = Assert.Throws<ContentException>(() => ContentRepository.LoadFromJson(broken));
            StringAssert.Contains("lvl_005_07", e.Message);
        }

        [Test]
        public void MissingLevelReferenceThrowsWithLevelId()
        {
            string text = File.ReadAllText(ManifestFile);
            int at = text.IndexOf("\"lvl_003_04\"", StringComparison.Ordinal); // first hit is the region's levels list
            Assert.GreaterOrEqual(at, 0);
            string broken = text.Substring(0, at) + "\"lvl_003_04_gone\"" + text.Substring(at + "\"lvl_003_04\"".Length);
            var e = Assert.Throws<ContentException>(() => ContentRepository.LoadFromJson(broken));
            StringAssert.Contains("lvl_003_04", e.Message);
        }

        [Test]
        public void InvalidJsonThrowsContentException()
        {
            Assert.Throws<ContentException>(() => ContentRepository.LoadFromJson("{ not json"));
            Assert.Throws<ContentException>(() => ContentRepository.LoadFromJson(""));
        }
    }
}
