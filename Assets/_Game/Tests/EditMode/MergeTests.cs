using System.Collections.Generic;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using BoingUpSkyTower.Simulation;
using NUnit.Framework;

namespace BoingUpSkyTower.Tests
{
    /// <summary>Character merge (docs/34): the pure rules, the tier data, the copies in the save and the migration of old saves.</summary>
    public class MergeTests
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

        ProgressService NewService(ISaveStore store = null)
        {
            return new ProgressService(repo, store ?? new MemorySaveStore(), 5);
        }

        // ---- pure rules ----

        [Test]
        public void ThreeCopiesBecomeOneOfTheNextTier()
        {
            var counts = new Dictionary<string, int> { { "momo", 3 } };
            MergeOutcome o = MergeRules.Apply(counts, "momo", "momo_u");
            Assert.AreEqual(MergeResult.Ok, o.Result);
            Assert.AreEqual(0, counts["momo"]);
            Assert.AreEqual(1, counts["momo_u"]);
            Assert.AreEqual(0, o.FromCopiesLeft);
            Assert.AreEqual(1, o.ToCopies);
        }

        [Test]
        public void ExtraCopiesStayAndTheNextTierStacks()
        {
            var counts = new Dictionary<string, int> { { "momo", 7 }, { "momo_u", 2 } };
            MergeOutcome o = MergeRules.Apply(counts, "momo", "momo_u");
            Assert.AreEqual(MergeResult.Ok, o.Result);
            Assert.AreEqual(4, counts["momo"], "7 - 3");
            Assert.AreEqual(3, counts["momo_u"], "2 + 1");
        }

        [Test]
        public void TooFewCopiesChangeNothing()
        {
            foreach (int n in new[] { 0, 1, 2 })
            {
                var counts = new Dictionary<string, int> { { "momo", n } };
                MergeOutcome o = MergeRules.Apply(counts, "momo", "momo_u");
                Assert.AreEqual(MergeResult.NotEnoughCopies, o.Result, "copies " + n);
                Assert.AreEqual(n, counts["momo"]);
                Assert.IsFalse(counts.ContainsKey("momo_u"));
            }
            Assert.AreEqual(MergeResult.NotEnoughCopies, MergeRules.Check(2, true));
            Assert.AreEqual(MergeResult.Ok, MergeRules.Check(3, true));
            Assert.AreEqual(MergeResult.Ok, MergeRules.Check(99, true));
        }

        [Test]
        public void TheTopOfALineCannotMerge()
        {
            var counts = new Dictionary<string, int> { { "momo_u", 9 } };
            MergeOutcome o = MergeRules.Apply(counts, "momo_u", null);
            Assert.AreEqual(MergeResult.MaxTier, o.Result);
            Assert.AreEqual(9, counts["momo_u"]);
            Assert.AreEqual(MergeResult.MaxTier, MergeRules.Check(50, false));
            Assert.AreEqual(MergeResult.Unknown, MergeRules.Apply(null, "momo", "momo_u").Result);
        }

        [Test]
        public void TheNextTierStaysUnderTheStackCap()
        {
            var counts = new Dictionary<string, int> { { "momo", 3 }, { "momo_u", MergeRules.MaxCopies } };
            MergeRules.Apply(counts, "momo", "momo_u");
            Assert.AreEqual(MergeRules.MaxCopies, counts["momo_u"]);
        }

        // ---- tier data ----

        [Test]
        public void EveryCommonHeroHasItsUncommonTier()
        {
            string[,] expect =
            {
                { "momo", "momo_u", "Cloudguard Momo" },
                { "forestgirl", "forestgirl_u", "Thornveil Capi" },
                { "beachotter", "beachotter_u", "Tidecrest Otto" },
                { "candygirl", "candygirl_u", "Sugarplate Bonbon" },
                { "monk", "monk_u", "Ironbamboo Bao" },
            };
            for (int i = 0; i < expect.GetLength(0); i++)
            {
                CharacterSpec common = CharacterDefs.Get(expect[i, 0]);
                CharacterSpec next = MergeRules.NextTier(common);
                Assert.IsNotNull(next, expect[i, 0]);
                Assert.AreEqual(expect[i, 1], next.Id);
                Assert.AreEqual(expect[i, 2], next.Name);
                Assert.AreEqual(CharacterRarity.Uncommon, next.Rarity);
                Assert.IsTrue(next.IsMergeTier);
                Assert.AreEqual(0, next.Region, "a merge tier belongs to no region");
                Assert.AreEqual(expect[i, 0], next.LineId);
                Assert.AreEqual(expect[i, 1], next.ArtId, "own art slot");
                Assert.AreEqual(expect[i, 0], next.FallbackArtId, "placeholder art is the Common hero");
                Assert.AreSame(next, CharacterDefs.Get(next.Id));
                Assert.IsNull(MergeRules.NextTier(next), "no Rare tier yet");
                Assert.AreSame(common, MergeRules.PreviousTier(next));
            }
        }

        [Test]
        public void MergeTiersNeverEnterAChestPool()
        {
            foreach (CharacterRarity r in new[] { CharacterRarity.Common, CharacterRarity.Uncommon, CharacterRarity.Rare, CharacterRarity.Epic, CharacterRarity.Legendary })
                foreach (CharacterSpec s in CharacterDefs.OfRarity(r)) Assert.IsFalse(s.IsMergeTier, s.Id);
        }

        [Test]
        public void UncommonRowsFollowTheOwnerRuleAndPlayWithinTheLimits()
        {
            foreach (TierRow row in MergeTierDefs.Rows)
            {
                Assert.IsNull(TierRules.Validate(row), row.Id);   // exactly 2 stats of 10..25 percent, run coins 50..100 (step 10), run gems 1..5
                CharacterSpec spec = CharacterDefs.Get(row.Id);
                CharacterSpec common = CharacterDefs.Get(row.LineId);
                Assert.IsNull(spec.Stats.ValidateTier(), row.Id);        // effective stats (what the simulation plays)
                CharacterStats a = common.Stats, b = spec.SheetStats;    // shown stats (boost not limited)
                int changed = 0;
                if (b.JumpLaunch != a.JumpLaunch) changed++;
                if (b.MaxSpeed != a.MaxSpeed) changed++;
                if (b.Control != a.Control) changed++;
                if (b.WallBoostHundredths != a.WallBoostHundredths) changed++;
                Assert.AreEqual(2, changed, row.Id + " boosts exactly 2 of the 4 stats");
                Assert.AreEqual(a.CoinFind, b.CoinFind, "no flat finds any more");
                Assert.AreEqual(a.GemFind, b.GemFind);
                Assert.AreEqual(a.RunBonusCoins + row.RunCoinsAdd, spec.Stats.RunBonusCoins, "run bonus is added on top of the Common hero's");
                Assert.AreEqual(a.RunBonusGems + row.RunGemsAdd, spec.Stats.RunBonusGems);
                Assert.LessOrEqual(MergeRules.StatGainLines(a, b).Count, 5, row.Id + " gain lines fit the result card");
                Assert.GreaterOrEqual(spec.Stats.JumpLaunch, a.JumpLaunch);
                Assert.GreaterOrEqual(spec.Stats.MaxSpeed, a.MaxSpeed);
                Assert.GreaterOrEqual(spec.Stats.Control, a.Control);
                Assert.GreaterOrEqual(spec.Stats.WallBoostHundredths, a.WallBoostHundredths);
                Assert.AreEqual(1, spec.Powers.Length, "Uncommon has 1 hero power");
                Assert.IsNull(TierRules.ValidatePowers(row.LineId, row.Rarity, spec.Powers), row.Id);
            }
        }

        [Test]
        public void BoostIsDeterministicAndRoundsToTheNearest()
        {
            var row = new TierRow { Id = "t", Rarity = CharacterRarity.Uncommon, JumpPct = 10, WallPct = 10, RunCoinsAdd = 50, RunGemsAdd = 1 };
            CharacterStats s = MergeTierDefs.Boost(CharacterStats.Default, row);
            Assert.AreEqual(21, s.JumpLaunch, "19 + 10 percent = 20.9");
            Assert.AreEqual(85, s.WallBoostHundredths, "77 + 10 percent = 84.7");
            Assert.AreEqual(CharacterStats.Default.MaxSpeed, s.MaxSpeed);
            Assert.AreEqual(CharacterStats.Default.Control, s.Control);
            Assert.AreEqual(0, s.CoinFind);
            CharacterStats again = MergeTierDefs.Boost(CharacterStats.Default, row);
            Assert.AreEqual(s.JumpLaunch, again.JumpLaunch);
        }

        [Test]
        public void BoostShowsTheFullPercentAndCapKeepsThePhysicsInsideTheLimits()
        {
            var row = new TierRow { Id = "t", Rarity = CharacterRarity.Uncommon, JumpPct = 25, WallPct = 25, RunCoinsAdd = 50, RunGemsAdd = 1 };
            CharacterStats shown = MergeTierDefs.Boost(CharacterDefs.Momo.Stats, row);
            CharacterStats plays = MergeTierDefs.Cap(shown);
            Assert.AreEqual(24, shown.JumpLaunch, "19 + 25 percent");
            Assert.AreEqual(CharacterStats.JumpLaunchMax, plays.JumpLaunch, "plays at the limit");
            Assert.IsNull(plays.ValidateTier());
        }

        [Test]
        public void RunBonusIsAddedOnTopOfTheCommonHeroBonus()
        {
            var withBonus = new CharacterStats { RunBonusCoins = 80, RunBonusGems = 2 };
            var row = new TierRow { Id = "t", Rarity = CharacterRarity.Uncommon, JumpPct = 10, WallPct = 10, RunCoinsAdd = 60, RunGemsAdd = 3 };
            CharacterStats s = MergeTierDefs.Boost(withBonus, row);
            Assert.AreEqual(140, s.RunBonusCoins);
            Assert.AreEqual(5, s.RunBonusGems);
            CollectionAssert.Contains(MergeRules.StatGainLines(withBonus, s), "Run coins +60");
            CollectionAssert.Contains(MergeRules.StatGainLines(withBonus, s), "Run gems +3");
            Assert.AreEqual(60, MergeTierDefs.Boost(CharacterStats.Default, row).RunBonusCoins, "a bare base starts at 0");
        }

        [Test]
        public void StatGainLinesUseTheHeroSheetWords()
        {
            var row = new TierRow { Id = "t", Rarity = CharacterRarity.Uncommon, JumpPct = 10, WallPct = 10, RunCoinsAdd = 50, RunGemsAdd = 1 };
            CharacterStats b = MergeTierDefs.Boost(CharacterStats.Default, row);
            CollectionAssert.AreEqual(new[] { "Jump +22%", "Wall Bounce +10%", "Run coins +50", "Run gems +1" }, MergeRules.StatGainLines(CharacterStats.Default, b));
            Assert.AreEqual(0, MergeRules.StatGainLines(CharacterStats.Default, CharacterStats.Default).Count);
            Assert.AreEqual(0, MergeRules.StatGainLines(null, b).Count);
        }

        [Test]
        public void ThePowerCountsAndNamesFollowTheOwnerLadder()
        {
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, CharacterDefs.PowerCount);
            Assert.AreEqual("Elite", CharacterDefs.RarityNames[(int)CharacterRarity.Epic]);
        }

        // ---- save: copies, merging, migration ----

        [Test]
        public void OwnedCharactersCountAsOneCopyAndTheRestAsNone()
        {
            ProgressService svc = NewService();
            Assert.AreEqual(1, svc.Copies("momo"));
            Assert.AreEqual(0, svc.Copies("forestgirl"), "region 2 locked");
            Assert.AreEqual(0, svc.Copies("momo_u"));
            Assert.AreEqual(0, svc.Copies("nobody"));
            svc.Data.UnlockedRegions.Add(repo.GetRegionByIndex(2).Id);
            Assert.AreEqual(1, svc.Copies("forestgirl"), "an unlocked region hero is one copy");
        }

        [Test]
        public void MergingConsumesThreeAndAddsTheNextTierAndSaves()
        {
            var store = new MemorySaveStore();
            ProgressService svc = NewService(store);
            Assert.AreEqual(3, svc.AddCopies("momo", 2));
            Assert.IsTrue(svc.CanMerge("momo"));
            MergeOutcome o = svc.TryMerge("momo");
            Assert.AreEqual(MergeResult.Ok, o.Result);
            Assert.AreEqual("momo_u", o.ToId);
            Assert.AreEqual(0, svc.Copies("momo"));
            Assert.AreEqual(1, svc.Copies("momo_u"));
            Assert.IsTrue(svc.OwnsCharacter("momo_u"));
            Assert.IsTrue(svc.IsCharacterVisible("momo_u"));
            Assert.IsFalse(svc.OwnsCharacter("momo"), "the lower tier stays owned only while copies are left");
            Assert.IsFalse(svc.CanMerge("momo"));

            ProgressService again = NewService(store);   // saved at once: a fresh service reads the same counts
            Assert.AreEqual(0, again.Copies("momo"));
            Assert.AreEqual(1, again.Copies("momo_u"));
            Assert.IsTrue(again.OwnsCharacter("momo_u"));
        }

        [Test]
        public void ExtraCopiesKeepTheLowerTierOwned()
        {
            ProgressService svc = NewService();
            svc.AddCopies("momo", 3);   // 4 copies
            Assert.AreEqual(MergeResult.Ok, svc.TryMerge("momo").Result);
            Assert.AreEqual(1, svc.Copies("momo"));
            Assert.IsTrue(svc.OwnsCharacter("momo"));
            Assert.IsTrue(svc.OwnsCharacter("momo_u"));
        }

        [Test]
        public void NotEnoughCopiesAndTopTierChangeNothing()
        {
            ProgressService svc = NewService();
            svc.AddCopies("momo", 1);   // 2 copies
            MergeOutcome o = svc.TryMerge("momo");
            Assert.AreEqual(MergeResult.NotEnoughCopies, o.Result);
            Assert.AreEqual(2, svc.Copies("momo"));
            Assert.AreEqual(0, svc.Copies("momo_u"));

            svc.AddCopies("momo_u", 5);
            Assert.AreEqual(MergeResult.MaxTier, svc.TryMerge("momo_u").Result);
            Assert.AreEqual(5, svc.Copies("momo_u"));
            Assert.AreEqual(MergeResult.Unknown, svc.TryMerge("nobody").Result);
            Assert.AreEqual(0, svc.AddCopies("nobody", 3));
            Assert.AreEqual(2, svc.AddCopies("momo", 0), "adding nothing changes nothing");
        }

        [Test]
        public void MergedTierHeroIsListedAndSelectable()
        {
            ProgressService svc = NewService();
            CharacterDef u = CharacterCatalog.Get("momo_u");
            Assert.AreEqual("momo_u", u.Id);
            Assert.AreEqual(CharacterRarity.Uncommon, u.Rarity);
            Assert.IsFalse(CharacterCatalog.Owns(svc, u), "not before the merge");
            svc.AddCopies("momo", 2);
            svc.TryMerge("momo");
            Assert.IsTrue(CharacterCatalog.Owns(svc, u));
            CollectionAssert.Contains(CharacterCatalog.Available(svc), u);
            Assert.IsNotNull(u.Info.Stats);
        }

        [Test]
        public void OldSavesMigrateToOneCopyPerOwnedCharacter()
        {
            var store = new MemorySaveStore();
            var old = new SaveData { Version = 2 };
            old.UnlockedRegions.Add(repo.GetRegionByIndex(2).Id);
            old.Characters.Add("beachotter");   // came from a chest or the shop
            store.Save(old);

            ProgressService svc = NewService(store);
            Assert.AreEqual(SaveData.CurrentVersion, svc.Data.Version);
            Assert.AreEqual(1, svc.Data.Copies["momo"]);
            Assert.AreEqual(1, svc.Data.Copies["forestgirl"]);
            Assert.AreEqual(1, svc.Data.Copies["beachotter"]);
            Assert.IsFalse(svc.Data.Copies.ContainsKey("candygirl"), "not owned: no entry");
            Assert.IsFalse(svc.Data.Copies.ContainsKey("momo_u"));
            Assert.IsTrue(svc.OwnsCharacter("forestgirl"));
            Assert.IsTrue(svc.OwnsCharacter("beachotter"));
            Assert.IsFalse(svc.OwnsCharacter("candygirl"));
        }

        [Test]
        public void CopiesFromAnyOtherVersionAreCleaned()
        {
            var store = new MemorySaveStore();
            var data = new SaveData();
            data.Copies["ghost"] = 5;
            data.Copies["momo"] = -3;
            data.Copies["momo_u"] = 2;
            store.Save(data);
            ProgressService svc = NewService(store);
            Assert.IsFalse(svc.Data.Copies.ContainsKey("ghost"));
            Assert.IsFalse(svc.Data.Copies.ContainsKey("momo"), "a negative count is dropped, so Momo counts as one owned copy again");
            Assert.AreEqual(1, svc.Copies("momo"));
            Assert.AreEqual(2, svc.Copies("momo_u"));
        }

        [Test]
        public void ResettingTheSaveResetsTheCopies()
        {
            ProgressService svc = NewService();
            svc.AddCopies("momo", 5);
            svc.TryMerge("momo");
            svc.ResetAll();
            Assert.AreEqual(1, svc.Copies("momo"));
            Assert.AreEqual(0, svc.Copies("momo_u"));
        }

        [Test]
        public void SystemTestOwnsEveryHeroWithoutCopies()
        {
            ProgressService.TestAllCharacters = true;
            ProgressService svc = NewService();
            Assert.IsTrue(svc.OwnsCharacter("momo_u"), "SYSTEM_TEST shows every hero");
            Assert.AreEqual(0, svc.Copies("momo_u"), "but it has no copies to merge");
        }
    }
}
