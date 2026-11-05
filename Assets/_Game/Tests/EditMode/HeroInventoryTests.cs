using System.Collections.Generic;
using System.Reflection;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using NUnit.Framework;

namespace BoingUpSkyTower.Tests
{
    /// <summary>Character inventory (hidden master list) versus hero inventory (the player's heroes with copies), docs/36.</summary>
    public class HeroInventoryTests
    {
        bool oldChars;
        ContentRepository repo;

        [SetUp]
        public void SetUp()
        {
            oldChars = ProgressService.TestAllCharacters;
            ProgressService.TestAllCharacters = false;
            repo = ContentRepository.LoadFromResources();
        }

        [TearDown]
        public void TearDown() { ProgressService.TestAllCharacters = oldChars; }

        ProgressService NewService(ISaveStore store = null) { return new ProgressService(repo, store ?? new MemorySaveStore(), 5); }

        [Test]
        public void CharacterInventoryHoldsEveryCharacterAndHeroInventoryOnlyTheOwnedOnes()
        {
            ProgressService svc = NewService();
            Assert.AreEqual(CharacterDefs.All.Length, CharacterInventory.Count);
            List<CharacterSpec> owned = svc.HeroInventory.Owned();
            Assert.Less(owned.Count, CharacterInventory.Count, "a new player owns far fewer heroes than exist");
            Assert.AreEqual(1, owned.Count, "a new guest owns Momo only");
            Assert.AreEqual("momo", owned[0].Id);
            Assert.AreEqual(CharacterInventory.Count, CharacterCatalog.All.Length);
            Assert.Less(CharacterCatalog.Available(svc).Length, CharacterCatalog.All.Length, "the list players see is not the master list");
        }

        [Test]
        public void CharacterInventoryIsReadOnlyAndOrderedByHeroThenTier()
        {
            CharacterSpec[] a = CharacterInventory.All;
            a[0] = null;
            Assert.IsNotNull(CharacterInventory.All[0], "All returns a copy");
            CharacterSpec[] all = CharacterInventory.All;
            for (int i = 1; i < all.Length; i++)
            {
                if (all[i].LineId == all[i - 1].LineId) Assert.Greater((int)all[i].Rarity, (int)all[i - 1].Rarity, "tiers of one hero rise");
                Assert.IsFalse(all[i].IsMergeTier && all[i].LineId != all[i - 1].LineId, "a tier follows its own hero");
            }
            Assert.AreEqual("momo", all[0].Id);
            Assert.AreEqual("momo_u", all[1].Id);
            Assert.AreEqual(0, CharacterInventory.TierOf("momo"));
            Assert.AreEqual(1, CharacterInventory.TierOf("momo_u"));
        }

        [Test]
        public void CharacterInventoryExposesNoPlayerState()
        {
            foreach (PropertyInfo p in typeof(CharacterInventory).GetProperties(BindingFlags.Public | BindingFlags.Static))
                Assert.IsFalse(typeof(ProgressService).IsAssignableFrom(p.PropertyType) || typeof(HeroInventory).IsAssignableFrom(p.PropertyType), p.Name);
            foreach (MethodInfo m in typeof(CharacterInventory).GetMethods(BindingFlags.Public | BindingFlags.Static))
                foreach (ParameterInfo pa in m.GetParameters())
                    Assert.IsFalse(typeof(ProgressService).IsAssignableFrom(pa.ParameterType), m.Name + " must not take a player's save");
        }

        [Test]
        public void ApiWorksAndWrappersAgree()
        {
            ProgressService svc = NewService();
            HeroInventory h = svc.HeroInventory;
            Assert.AreEqual(1, h.Copies("momo"));
            Assert.IsTrue(h.Owns("momo"));
            Assert.IsFalse(h.Owns("momo_u"));
            Assert.AreEqual(0, h.Copies("momo_u"));
            Assert.AreEqual(2, h.AddCopies("momo_u", 2));
            Assert.AreEqual(2, svc.Copies("momo_u"));
            Assert.AreEqual(5, svc.AddCopies("momo_u", 3));
            Assert.AreEqual(5, h.Copies("momo_u"));
            Assert.AreEqual(5, h.Copies("momo_u"));
            Assert.AreEqual(0, h.AddCopies("ghost", 3), "unknown id changes nothing");
            Assert.AreEqual(5, h.AddCopies("momo_u", 0), "a count below 1 changes nothing");
        }

        [Test]
        public void AddingCopiesOfANewCharacterAddsANewCard()
        {
            ProgressService svc = NewService();
            int before = CharacterCatalog.Available(svc).Length;
            Assert.AreEqual(1, svc.HeroInventory.Owned().Count);
            svc.AddCopies("beachotter", 1);   // region 3 hero, region still locked: owned but its reveal is pending
            Assert.IsTrue(svc.HeroInventory.Owns("beachotter"));
            svc.AddCopies("momo_u", 1);
            Assert.AreEqual(before + 1, CharacterCatalog.Available(svc).Length, "a tier hero shows at once");
            List<CharacterSpec> owned = svc.HeroInventory.Owned();
            Assert.AreEqual("momo", owned[0].Id);
            Assert.AreEqual("momo_u", owned[1].Id, "ordered by hero then tier");
        }

        [Test]
        public void OwnedListIsOneCardPerCopyWithNoCounts()
        {
            ProgressService svc = NewService();
            Assert.AreEqual(1, CharacterCatalog.OwnedCards(svc).Count, "a new player: one Momo card");
            svc.AddCopies("momo", 1);   // a second Common Momo
            List<HeroCard> cards = CharacterCatalog.OwnedCards(svc);
            Assert.AreEqual(2, cards.Count, "another copy of the same hero = another card");
            Assert.AreEqual("momo", cards[0].Id);
            Assert.AreEqual("momo", cards[1].Id);
            Assert.IsTrue(cards[0].IsFirstOfType);
            Assert.IsFalse(cards[1].IsFirstOfType);
            svc.AddCopies("momo_u", 2);
            Assert.AreEqual(4, CharacterCatalog.OwnedCards(svc).Count);
            Assert.IsNull(typeof(HeroInventory).GetMethod("BadgeCount"), "no count badge any more");
        }

        [Test]
        public void CharacterTestOwnedListHasOnlyCommonHeroes()
        {
            ProgressService.TestAllCharacters = true;
            ProgressService svc = NewService();
            List<HeroCard> cards = CharacterCatalog.OwnedCards(svc);
            Assert.AreEqual(5, cards.Count, "the five Common heroes, one card each");
            foreach (HeroCard c in cards) Assert.AreEqual(CharacterRarity.Common, c.Spec.Rarity, c.Id);
        }

        [Test]
        public void MergeConsumesExactlyThreeCopiesAndTheNewTierJoinsTheOwnedList()
        {
            ProgressService svc = NewService();
            svc.AddCopies("momo", 3);   // 4 copies = 4 cards
            Assert.AreEqual(4, CharacterCatalog.OwnedCards(svc).Count);
            Assert.AreEqual(MergeResult.Ok, svc.TryMerge("momo").Result);
            List<HeroCard> cards = CharacterCatalog.OwnedCards(svc);
            Assert.AreEqual(2, cards.Count, "1 Momo copy left plus the new Uncommon");
            Assert.AreEqual("momo", cards[0].Id);
            Assert.AreEqual("momo_u", cards[1].Id);
            CollectionAssert.Contains(CharacterCatalog.Available(svc), CharacterCatalog.Get("momo_u"));
        }

        [Test]
        public void OldSaveLoadsIntoTheHeroInventory()
        {
            var store = new MemorySaveStore();
            var old = new SaveData { Version = 2 };
            old.Characters.Add("beachotter");
            store.Save(old);
            ProgressService svc = NewService(store);
            Assert.AreEqual(1, svc.HeroInventory.Copies("momo"));
            Assert.AreEqual(1, svc.HeroInventory.Copies("beachotter"));
            Assert.AreEqual(0, svc.HeroInventory.Copies("candygirl"));
            Assert.AreEqual(SaveData.CurrentVersion, svc.Data.Version);
        }

        [Test]
        public void HeroInventoryIsPerSaveAndSurvivesReload()
        {
            var store = new MemorySaveStore();
            ProgressService a = NewService(store);
            a.AddCopies("momo_u", 4);
            ProgressService b = NewService(store);
            Assert.AreEqual(4, b.HeroInventory.Copies("momo_u"));
            ProgressService other = NewService();
            Assert.AreEqual(0, other.HeroInventory.Copies("momo_u"), "another profile has its own inventory");
        }
    }
}
