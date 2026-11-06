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
                { "bumble", "bumble_u", "Honeycloak Bumble" },
                { "ribbi", "ribbi_u", "Leafcrown Ribbi" },
                { "dandy", "dandy_u", "Seedling Dandy" },
                { "pip", "pip_u", "Redcape Pip" },
                { "nib", "nib_u", "Leafscarf Nib" },
                { "spike", "spike_u", "Logplate Spike" },
                { "moss", "moss_u", "Vinebelt Moss" },
                { "fenn", "fenn_u", "Scoutvest Fenn" },
                { "pinch", "pinch_u", "Shellvest Pinch" },
                { "shelly", "shelly_u", "Coralstrap Shelly" },
                { "bloop", "bloop_u", "Coralband Bloop" },
                { "coco", "coco_u", "Palmwrap Coco" },
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
                bool full = common.Region <= 3;
                Assert.AreEqual(full, MergeRules.NextTier(next) != null, expect[i, 0] + ": Region 1 to 3 lines continue to Rare, the others stop at Uncommon (F4.4 and F4.5)");
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
        public void UncommonRowsFollowTheOwnerRuleOnTheShownStatsAndPlayWithinTheLimits()
        {
            double[] V(ShownStats x) { return new[] { x.Jump, x.Speed, x.Control, x.WallBounce }; }
            foreach (TierRow row in MergeTierDefs.Rows)
            {
                if (row.Rarity != CharacterRarity.Uncommon) continue;   // Rare, Elite and Legendary rows chain over the previous tier: RegionOneTierRowsFollowTheChain in Region1HeroTests
                Assert.IsNull(TierRules.Validate(row), row.Id);   // exactly 2 stats of 10..25 percent, run coins 50..100 (step 10), run gems 1..5
                CharacterSpec spec = CharacterDefs.Get(row.Id);
                CharacterSpec common = CharacterDefs.Get(row.LineId);
                Assert.IsNull(spec.Stats.ValidateTier(), row.Id);        // effective stats (what the simulation plays)
                double[] c = V(common.ShownValues()), u = V(spec.ShownValues());
                int changed = 0;
                for (int i = 0; i < 4; i++)
                {
                    int pct = ShownStats.Percent(c[i], u[i]);
                    if (pct == 0) { Assert.AreEqual(c[i], u[i], row.Id); continue; }
                    changed++;
                    Assert.That(pct, Is.InRange(10, 25), row.Id + " shown percent");
                }
                Assert.AreEqual(2, changed, row.Id + " boosts exactly 2 of the 4 shown stats");
                Assert.AreEqual(common.Stats.RunBonusCoins + row.RunCoinsAdd, spec.Stats.RunBonusCoins, "run bonus is added on top of the Common hero's");
                Assert.AreEqual(common.Stats.RunBonusGems + row.RunGemsAdd, spec.Stats.RunBonusGems);
                Assert.LessOrEqual(MergeRules.ShownGainLines(common.ShownValues(), spec.ShownValues()).Count, 5, row.Id + " gain lines fit the result card");
                Assert.GreaterOrEqual(spec.Stats.JumpLaunch, common.Stats.JumpLaunch);
                Assert.GreaterOrEqual(spec.Stats.MaxSpeed, common.Stats.MaxSpeed);
                Assert.GreaterOrEqual(spec.Stats.Control, common.Stats.Control);
                Assert.AreEqual(1, spec.Powers.Length, "Uncommon has 1 hero power");
                Assert.IsNull(TierRules.ValidatePowers(row.LineId, row.Rarity, spec.Powers), row.Id);
            }
        }

        [Test]
        public void GainTextShowsTheStoredPercentExactly()
        {
            foreach (TierRow row in MergeTierDefs.Rows)
            {
                CharacterSpec below = MergeRules.PreviousTier(CharacterDefs.Get(row.Id));   // the merge text compares with the tier below
                List<string> lines = MergeRules.ShownGainLines(below.ShownValues(), CharacterDefs.Get(row.Id).ShownValues());
                if (row.JumpPct > 0) CollectionAssert.Contains(lines, "Jump +" + row.JumpPct + "%", row.Id);
                if (row.SpeedPct > 0) CollectionAssert.Contains(lines, "Speed +" + row.SpeedPct + "%", row.Id);
                if (row.ControlPct > 0) CollectionAssert.Contains(lines, "Control +" + row.ControlPct + "%", row.Id);
                if (row.WallPct > 0) CollectionAssert.Contains(lines, "Wall Bounce +" + row.WallPct + "%", row.Id);
                // the run bonus lines show the difference to the tier below (each tier's add is its OWN total on top of the Common hero)
                int dCoins = CharacterDefs.Get(row.Id).ShownValues().RunBonusCoins - below.ShownValues().RunBonusCoins;
                int dGems = CharacterDefs.Get(row.Id).ShownValues().RunBonusGems - below.ShownValues().RunBonusGems;
                if (dCoins > 0) CollectionAssert.Contains(lines, "Run coins +" + dCoins, row.Id);
                if (dGems > 0) CollectionAssert.Contains(lines, "Run gems +" + dGems, row.Id);
            }
            Assert.AreEqual(0, MergeRules.ShownGainLines(null, CharacterDefs.Momo.ShownValues()).Count);
        }

        [Test]
        public void HeroSheetShowsTheTierShownNumbersNotTheCappedPhysics()
        {
            foreach (TierRow row in MergeTierDefs.Rows)
            {
                CharacterSpec spec = CharacterDefs.Get(row.Id);
                HeroSheetData d = HeroSheetData.From(spec.Name, spec.Rarity, "x", spec.Stats, spec.Powers, spec.Shown);
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                Assert.AreEqual(spec.Shown.Jump.ToString("0.#", inv), d.Stats[0, 1], row.Id);
                Assert.AreEqual(spec.Shown.Speed.ToString("0.#", inv), d.Stats[1, 1], row.Id);
                Assert.AreEqual(spec.Shown.Control.ToString("0.#", inv), d.Stats[2, 1], row.Id);
                Assert.AreEqual(spec.Shown.WallBounce.ToString("0.#", inv), d.Stats[3, 1], row.Id);
            }
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

            svc.AddCopies("monk_u", 5);   // Region 5 stops at Uncommon for now
            Assert.AreEqual(MergeResult.MaxTier, svc.TryMerge("monk_u").Result);
            Assert.AreEqual(5, svc.Copies("monk_u"));
            svc.AddCopies("momo_l", 5);   // Legendary is the top of a Region 1 line
            Assert.AreEqual(MergeResult.MaxTier, svc.TryMerge("momo_l").Result);
            Assert.AreEqual(5, svc.Copies("momo_l"));
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
        public void CharacterTestOwnsTheCommonHeroesButNeverTheMergeTiers()
        {
            ProgressService.TestAllCharacters = true;
            ProgressService svc = NewService();
            Assert.IsTrue(svc.OwnsCharacter("momo"));
            Assert.IsTrue(svc.OwnsCharacter("candygirl"), "Character Test: every Common hero is owned");
            Assert.AreEqual(1, svc.Copies("candygirl"));
            Assert.IsFalse(svc.OwnsCharacter("momo_u"), "Uncommon stays locked until merged, bought or received");
            Assert.AreEqual(0, svc.Copies("momo_u"));
        }
    }
}
