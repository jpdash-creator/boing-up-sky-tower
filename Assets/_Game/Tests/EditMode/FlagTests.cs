using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using NUnit.Framework;

namespace BoingUpSkyTower.Tests
{
    /// <summary>Dev flags GAME_JUMP_TEST and SYSTEM_TEST (docs/23) and the Sporeling that stays hidden until region 2 is unlocked.</summary>
    public class FlagTests
    {
        bool oldUnlock, oldChars;

        [SetUp]
        public void SetUp()
        {
            oldUnlock = ProgressService.TestUnlockAll;
            oldChars = ProgressService.TestAllCharacters;
            ProgressService.TestUnlockAll = false;
            ProgressService.TestAllCharacters = false;
        }

        [TearDown]
        public void TearDown()
        {
            ProgressService.TestUnlockAll = oldUnlock;
            ProgressService.TestAllCharacters = oldChars;
        }

        static ProgressService NewService()
        {
            return new ProgressService(ContentRepository.LoadFromResources(), new MemorySaveStore(), 5);
        }

        [Test]
        public void ReleaseBuildsAreAlwaysReal()
        {
            foreach (bool sys in new[] { false, true })
                foreach (bool jump in new[] { false, true })
                {
                    Assert.IsFalse(GameSettings.EffectiveUnlockAll(false, sys, jump));
                    Assert.IsFalse(GameSettings.EffectiveSystemTools(false, sys));
                }
        }

        [Test]
        public void FlagMatrixInADevelopmentBuild()
        {
            // (system, jump) = (off, off): production, all real
            Assert.IsFalse(GameSettings.EffectiveUnlockAll(true, false, false));
            Assert.IsFalse(GameSettings.EffectiveSystemTools(true, false));
            // (off, on): GAME_JUMP_TEST is ignored, still production
            Assert.IsFalse(GameSettings.EffectiveUnlockAll(true, false, true));
            Assert.IsFalse(GameSettings.EffectiveSystemTools(true, false));
            // (on, off): real levels and regions, everything else open (characters, test purchases, Autopilot)
            Assert.IsFalse(GameSettings.EffectiveUnlockAll(true, true, false));
            Assert.IsTrue(GameSettings.EffectiveSystemTools(true, true));
            // (on, on): everything open, levels and regions too
            Assert.IsTrue(GameSettings.EffectiveUnlockAll(true, true, true));
            Assert.IsTrue(GameSettings.EffectiveSystemTools(true, true));
        }

        [Test]
        public void TheEditorBehavesLikeAReleaseBuild()
        {
            Assert.IsFalse(GameSettings.DevBuild);
            Assert.IsFalse(GameSettings.SystemTest);
            Assert.IsFalse(GameSettings.GameJumpTest);
            Assert.IsFalse(GameSettings.TestUnlockAll);
        }

        [Test]
        public void TheSporelingIsHiddenUntilRegion2IsUnlocked()
        {
            var repo = ContentRepository.LoadFromResources();
            var svc = new ProgressService(repo, new MemorySaveStore(), 5);
            Assert.IsTrue(CharacterCatalog.Owns(svc, CharacterCatalog.Momo), "Momo is open by default");
            Assert.IsFalse(CharacterCatalog.Owns(svc, CharacterCatalog.Sporeling));
            CollectionAssert.AreEqual(new[] { CharacterCatalog.Momo }, CharacterCatalog.Available(svc));

            svc.Data.UnlockedRegions.Add(repo.GetRegionByIndex(2).Id);
            Assert.IsTrue(svc.OwnsCharacter("sporeling"), "region 2 unlocked");
            CollectionAssert.AreEqual(new[] { CharacterCatalog.Momo, CharacterCatalog.Sporeling }, CharacterCatalog.Available(svc));
        }

        [Test]
        public void SystemTestShowsEveryCharacter()
        {
            var svc = NewService();
            Assert.IsFalse(svc.OwnsCharacter("sporeling"));
            ProgressService.TestAllCharacters = true;
            Assert.IsTrue(svc.OwnsCharacter("sporeling"));
            Assert.IsTrue(CharacterCatalog.Owns(svc, CharacterCatalog.Sporeling));
            Assert.AreEqual(CharacterCatalog.All.Length, CharacterCatalog.Available(svc).Length);
        }

        [Test]
        public void TheTestUnlockStaticStillOpensLevelsAndRegions()
        {
            var repo = ContentRepository.LoadFromResources();
            var svc = new ProgressService(repo, new MemorySaveStore(), 5);
            RegionDef r2 = repo.GetRegionByIndex(2);
            Assert.IsFalse(svc.IsRegionUnlocked(r2));
            ProgressService.TestUnlockAll = true;
            Assert.IsTrue(svc.IsRegionUnlocked(r2));
        }
    }
}
