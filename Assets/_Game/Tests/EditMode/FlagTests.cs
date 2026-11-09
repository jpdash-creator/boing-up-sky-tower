using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>Dev flags GAME_JUMP_TEST and SYSTEM_TEST (docs/23) and Capi (region 2 hero) who stays hidden until region 2 is unlocked.</summary>
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
        public void CapiIsHiddenUntilRegion2IsUnlocked()
        {
            var repo = ContentRepository.LoadFromResources();
            var svc = new ProgressService(repo, new MemorySaveStore(), 5);
            Assert.IsTrue(CharacterCatalog.Owns(svc, CharacterCatalog.Momo), "Momo is open by default");
            Assert.IsFalse(CharacterCatalog.Owns(svc, CharacterCatalog.ForestGirl));
            CollectionAssert.AreEqual(new[] { CharacterCatalog.Momo }, CharacterCatalog.Available(svc));

            svc.Data.UnlockedRegions.Add(repo.GetRegionByIndex(2).Id);
            Assert.IsTrue(svc.OwnsCharacter("forestgirl"), "region 2 unlocked");
            CollectionAssert.AreEqual(new[] { CharacterCatalog.Momo }, CharacterCatalog.Available(svc), "still hidden until the reveal pop-up is confirmed");
            svc.MarkHeroRevealed("forestgirl");
            CollectionAssert.AreEqual(new[] { CharacterCatalog.Momo, CharacterCatalog.ForestGirl }, CharacterCatalog.Available(svc));
        }

        [Test]
        public void TheNewRegionHeroesAreCommonAndComeWithTheirRegion()
        {
            Assert.AreEqual(2, CharacterDefs.ForestGirl.Region);
            Assert.AreEqual(3, CharacterDefs.BeachOtter.Region);
            Assert.AreEqual(4, CharacterDefs.CandyGirl.Region);
            Assert.AreEqual(5, CharacterDefs.Monk.Region);
            Assert.AreEqual("Capi", CharacterDefs.ForestGirl.Name);
            Assert.AreEqual("Otto", CharacterDefs.BeachOtter.Name);
            Assert.AreEqual("Bonbon", CharacterDefs.CandyGirl.Name);
            Assert.AreEqual("Bao", CharacterDefs.Monk.Name);
            foreach (CharacterSpec s in new[] { CharacterDefs.ForestGirl, CharacterDefs.BeachOtter, CharacterDefs.CandyGirl, CharacterDefs.Monk })
            {
                Assert.AreEqual(CharacterRarity.Common, s.Rarity);
                Assert.IsTrue(s.NeedsReveal, s.Id);
            }
            var repo = ContentRepository.LoadFromResources();
            var svc = new ProgressService(repo, new MemorySaveStore(), 5);
            Assert.IsFalse(svc.OwnsCharacter("beachotter"));
            Assert.IsFalse(svc.OwnsCharacter("candygirl"));
            Assert.IsFalse(svc.OwnsCharacter("monk"));
            svc.Data.UnlockedRegions.Add(repo.GetRegionByIndex(3).Id);
            Assert.IsTrue(svc.OwnsCharacter("beachotter"), "region 3 unlocked");
            Assert.IsFalse(svc.OwnsCharacter("candygirl"), "region 4 still locked");
            Assert.IsFalse(svc.OwnsCharacter("monk"), "region 5 still locked");
            Assert.IsFalse(CharacterCatalog.Owns(svc, CharacterCatalog.BeachOtter), "hidden until the reveal");
            svc.MarkHeroRevealed("beachotter");
            Assert.IsTrue(CharacterCatalog.Owns(svc, CharacterCatalog.BeachOtter));
            svc.Data.UnlockedRegions.Add(repo.GetRegionByIndex(4).Id);
            Assert.IsTrue(svc.OwnsCharacter("candygirl"), "region 4 unlocked");
            Assert.IsFalse(CharacterCatalog.Owns(svc, CharacterCatalog.CandyGirl), "hidden until the reveal");
            svc.MarkHeroRevealed("candygirl");
            svc.Data.UnlockedRegions.Add(repo.GetRegionByIndex(5).Id);
            svc.MarkHeroRevealed("monk");
            CollectionAssert.AreEqual(new[] { CharacterCatalog.Momo, CharacterCatalog.BeachOtter, CharacterCatalog.CandyGirl, CharacterCatalog.Monk }, CharacterCatalog.Available(svc));
        }

        // ---- hero reveal (docs/21): every region has one Common hero, hidden until its region is unlocked and the one-time pop-up is confirmed ----

        [Test]
        public void MomoIsAlwaysVisibleAndNeverNeedsAReveal()
        {
            var svc = NewService();
            Assert.IsFalse(CharacterDefs.Momo.NeedsReveal);
            Assert.IsTrue(CharacterDefs.ForestGirl.NeedsReveal);
            Assert.IsTrue(svc.IsCharacterVisible("momo"));
            Assert.IsTrue(svc.IsHeroRevealed("momo"));
            CollectionAssert.DoesNotContain(svc.PendingHeroReveals(), "momo");
            Assert.IsFalse(svc.MarkHeroRevealed("momo"), "nothing to record for Momo");
        }

        [Test]
        public void ARegionHeroIsHiddenBeforeItsRegionUnlocks()
        {
            var svc = NewService();
            Assert.IsFalse(svc.IsCharacterVisible("forestgirl"));
            CollectionAssert.IsEmpty(svc.PendingHeroReveals(), "no pop-up while the region is locked");
            Assert.IsFalse(svc.MarkHeroRevealed("forestgirl"), "a locked region's hero cannot be revealed");
            CollectionAssert.AreEqual(new[] { CharacterCatalog.Momo }, CharacterCatalog.Available(svc));
        }

        [Test]
        public void TheRevealIsPendingOnceThenSavedAndNeverShownAgain()
        {
            var repo = ContentRepository.LoadFromResources();
            var store = new MemorySaveStore();
            var svc = new ProgressService(repo, store, 5);
            svc.Data.UnlockedRegions.Add(repo.GetRegionByIndex(2).Id);
            CollectionAssert.AreEqual(new[] { "forestgirl" }, svc.PendingHeroReveals(), "region 2 has Capi");
            Assert.IsFalse(svc.IsCharacterVisible("forestgirl"), "hidden until OK");

            Assert.IsTrue(svc.MarkHeroRevealed("forestgirl"));
            Assert.IsFalse(svc.MarkHeroRevealed("forestgirl"), "recorded once");
            CollectionAssert.IsEmpty(svc.PendingHeroReveals());
            Assert.IsTrue(svc.IsCharacterVisible("forestgirl"));

            // saved immediately: a new service on the same store still has it revealed (no second pop-up)
            var again = new ProgressService(repo, store, 5);
            CollectionAssert.Contains(again.Data.HeroesRevealed, "forestgirl");
            CollectionAssert.IsEmpty(again.PendingHeroReveals());
            Assert.IsTrue(again.IsCharacterVisible("forestgirl"));
        }

        [Test]
        public void AnOldSaveWithAnUnlockedRegionGetsTheRevealOnce()
        {
            var repo = ContentRepository.LoadFromResources();
            var store = new MemorySaveStore();
            var data = new SaveData();
            data.UnlockedRegions.Add(repo.GetRegionByIndex(2).Id);
            data.HeroesRevealed = null;   // a save written before this field existed
            store.Save(data);
            var svc = new ProgressService(repo, store, 5);
            Assert.IsNotNull(svc.Data.HeroesRevealed);
            CollectionAssert.AreEqual(new[] { "forestgirl" }, svc.PendingHeroReveals(), "region 2 has Capi");
        }

        [Test]
        public void ASelectedHiddenHeroFallsBackToMomo()
        {
            var repo = ContentRepository.LoadFromResources();
            try
            {
                GameSession.InitForTests(new MemorySaveStore());
                CharacterCatalog.Selected = CharacterCatalog.ForestGirl;
                Assert.AreSame(CharacterCatalog.Momo, CharacterCatalog.Selected, "region 2 locked: Momo");
                GameSession.Progress.Data.UnlockedRegions.Add(repo.GetRegionByIndex(2).Id);
                Assert.AreSame(CharacterCatalog.Momo, CharacterCatalog.Selected, "unlocked but not revealed: still Momo");
                GameSession.Progress.MarkHeroRevealed("forestgirl");
                Assert.AreSame(CharacterCatalog.ForestGirl, CharacterCatalog.Selected, "revealed: the choice stands");
            }
            finally
            {
                CharacterCatalog.Selected = CharacterCatalog.Momo;
                GameSession.Reset();
            }
        }

        [Test]
        public void AnOldSaveThatOwnsOrSelectedTheDeletedSporelingFallsBackToMomo()
        {
            var repo = ContentRepository.LoadFromResources();
            var store = new MemorySaveStore();
            var data = new SaveData();
            data.Characters.Add("sporeling");
            data.HeroesRevealed.Add("sporeling");
            data.UnlockedRegions.Add(repo.GetRegionByIndex(2).Id);
            store.Save(data);
            var svc = new ProgressService(repo, store, 5);
            CollectionAssert.DoesNotContain(svc.Data.Characters, "sporeling");
            CollectionAssert.DoesNotContain(svc.Data.HeroesRevealed, "sporeling");
            Assert.IsFalse(svc.OwnsCharacter("sporeling"));
            Assert.IsFalse(svc.IsCharacterVisible("sporeling"));
            Assert.IsFalse(svc.MarkHeroRevealed("sporeling"));
            Assert.IsNull(CharacterDefs.Get("sporeling"));
            Assert.AreSame(CharacterCatalog.Momo, CharacterCatalog.Get("sporeling"));
            try
            {
                GameSession.InitForTests(new MemorySaveStore());
                UnityEngine.PlayerPrefs.SetString("settings.character", "sporeling");
                Assert.AreSame(CharacterCatalog.Momo, CharacterCatalog.Selected, "a saved choice of the deleted hero falls back to Momo");
            }
            finally
            {
                CharacterCatalog.Selected = CharacterCatalog.Momo;
                GameSession.Reset();
            }
        }

        [Test]
        public void EveryCharacterSpecHasADefAndHeroesNeverDropFromChests()
        {
            Assert.AreEqual(CharacterDefs.All.Length, CharacterCatalog.All.Length);
            foreach (CharacterSpec s in CharacterDefs.All) Assert.AreEqual(s.Id, CharacterCatalog.Get(s.Id).Id);
            foreach (CharacterSpec s in CharacterDefs.OfRarity(CharacterRarity.Common)) Assert.IsFalse(s.IsRegionHero);
        }

        [Test]
        public void CharacterTestOwnsTheCommonHeroesOnly()
        {
            var svc = NewService();
            Assert.IsFalse(svc.OwnsCharacter("forestgirl"));
            ProgressService.TestAllCharacters = true;
            Assert.IsTrue(svc.OwnsCharacter("forestgirl"));
            Assert.IsTrue(CharacterCatalog.Owns(svc, CharacterCatalog.ForestGirl));
            Assert.IsFalse(svc.OwnsCharacter("forestgirl_u"), "Uncommon heroes are merged or bought, never given by Character Test");
            int commons = 0;
            foreach (CharacterSpec s in CharacterDefs.All) if (s.Rarity == CharacterRarity.Common && s.Region > 0) commons++;
            Assert.AreEqual(commons, CharacterCatalog.Available(svc).Length, "only the Common heroes are owned");
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
