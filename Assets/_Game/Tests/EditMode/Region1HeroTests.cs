using System.Collections.Generic;
using System.Linq;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using BoingUpSkyTower.Simulation;
using NUnit.Framework;

namespace BoingUpSkyTower.Tests
{
    /// <summary>
    /// Region 1 has ONE ACTIVE hero line, Momo, at all five rarity tiers (owner Bug List 6, 2026-11-09). Bumble, Ribbi, Dandy and Pip and their tiers are INACTIVE ALWAYS: their physics rows stay as data
    /// (HeroTable, Active = false) and their art lives in Art/_inactive_heroes, but they never exist at runtime. The data (ids, rarity, merge chain, region), the art that loads, the tier rows (rule, table,
    /// powers), and that chests, bundles and shop draws can give them ONLY from open regions.
    /// </summary>
    public class Region1HeroTests
    {
        static readonly string[] Lines = { "momo" };
        static readonly string[] Inactive = { "bumble", "ribbi", "dandy", "pip" };
        static readonly string[] Suffix = { "", "_u", "_r", "_e", "_l" };
        static readonly CharacterRarity[] Ladder = { CharacterRarity.Common, CharacterRarity.Uncommon, CharacterRarity.Rare, CharacterRarity.Epic, CharacterRarity.Legendary };

        static IEnumerable<string> AllIds()
        {
            foreach (string line in Lines) foreach (string s in Suffix) yield return line + s;
        }

        static ulong Mix(ref ulong s)
        {
            unchecked
            {
                s += 0x9E3779B97F4A7C15UL;
                ulong z = s;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        [Test]
        public void AllFiveRegion1HeroesExistWithTheRightRarityLineAndRegion()
        {
            Assert.AreEqual(5, AllIds().Count());
            foreach (string line in Lines)
                for (int i = 0; i < 5; i++)
                {
                    string id = line + Suffix[i];
                    CharacterSpec c = CharacterDefs.Get(id);
                    Assert.IsNotNull(c, id);
                    Assert.AreEqual(Ladder[i], c.Rarity, id);
                    Assert.AreEqual(line, c.LineId, id);
                    Assert.AreEqual(i > 0, c.IsMergeTier, id);
                    Assert.AreEqual(1, BundleCards.RegionOf(c, CharacterDefs.All), id + " belongs to Region 1");
                    Assert.AreEqual(i == 0 ? 1 : 0, c.Region, id);
                    Assert.AreEqual(CharacterDefs.PowerCount[i], c.Powers.Length, id + " power count follows the tier");
                    Assert.IsNull(c.Stats.ValidateTier(), id);
                    Assert.IsFalse(string.IsNullOrEmpty(c.Name), id);
                }
            Assert.AreEqual("Momo", CharacterDefs.Get("momo").Name);
            // all ids and names are different, and exactly the 25 active heroes exist
            Assert.AreEqual(25, CharacterDefs.All.Length);
            Assert.AreEqual(CharacterDefs.All.Length, CharacterDefs.All.Select(c => c.Id).Distinct().Count());
            Assert.AreEqual(CharacterDefs.All.Length, CharacterDefs.All.Select(c => c.Name).Distinct().Count());
            foreach (CharacterSpec c in CharacterDefs.All) StringAssert.DoesNotContain("wing", c.Name.ToLowerInvariant(), "no wings on any hero");
        }

        [Test]
        public void TheFourExtraRegion1CommonsAndTheirTiersAreInactiveAlways()
        {
            foreach (string line in Inactive)
                foreach (string s in Suffix)
                {
                    Assert.IsNull(CharacterDefs.Get(line + s), line + s + " is inactive always and never exists at runtime");
                    HeroRow row = HeroTable.Find(line + s);
                    Assert.IsNotNull(row, line + s + " keeps its generated physics row as data");
                    Assert.IsFalse(row.Active, line + s);
                    Assert.IsNotNull(CharacterDefs.GetIncludingInactive(line + s), line + s + " is reachable by the data accessor");
                    Assert.IsFalse(CharacterInventory.Exists(line + s), line + s);
                    Assert.AreSame(CharacterCatalog.Momo, CharacterCatalog.Get(line + s), line + s + " falls back to Momo in the catalog");
                }
        }

        [Test]
        public void EveryRegion1HeroHasItsOwnArtThatLoads()
        {
            foreach (string id in AllIds())
            {
                CharacterDef def = CharacterCatalog.Get(id);
                Assert.AreEqual(id, def.Id);
                Assert.IsFalse(def.UsesPlaceholderArt, id + " has its own Resources/Characters/" + id + "/rig.json");
                CharacterRig rig = def.Rig;
                Assert.GreaterOrEqual(rig.Order.Length, 6, id);
                Assert.AreEqual(1365f, rig.CanvasW, id);
                Assert.AreEqual(1649f, rig.CanvasH, id);
                foreach (string part in rig.Order) Assert.IsNotNull(def.PartSprite(part), id + "/" + part + " loads as a sprite");
                Assert.IsNotNull(def.PartSprite("head"), id);
                Assert.IsNotNull(def.PartSprite(id == "momo" ? "torso" : "body"), id);   // Common Momo keeps his nine original parts
                Assert.IsNotEmpty(PartsAssembler.Build(def, null), id);
            }
        }

        [Test]
        public void TheMergeChainIsCompleteCommonToLegendaryAndLegendaryEndsIt()
        {
            foreach (string line in Lines)
            {
                CharacterSpec cur = CharacterDefs.Get(line);
                for (int i = 1; i < 5; i++)
                {
                    CharacterSpec next = MergeRules.NextTier(cur);
                    Assert.IsNotNull(next, cur.Id + " has a next tier");
                    Assert.AreEqual(line + Suffix[i], next.Id);
                    Assert.AreSame(cur, MergeRules.PreviousTier(next));
                    Assert.AreEqual(3, MergeRules.CopiesNeeded);
                    cur = next;
                }
                Assert.IsNull(MergeRules.NextTier(cur), line + " Legendary does not merge");
            }
        }

        [Test]
        public void MomoIsTheRegion1StarterAndNeedsNoReveal()
        {
            Assert.IsTrue(CharacterDefs.Momo.RegionStarter);
            Assert.IsFalse(CharacterDefs.Momo.NeedsReveal);
            Assert.IsTrue(CharacterDefs.Momo.IsRegionHero);
            Assert.IsTrue(CharacterDefs.ForestGirl.NeedsReveal, "regions 2 to 5 keep their reveal");
            CharacterDefs.OfRarity(CharacterRarity.Common).ForEach(c => Assert.Fail("a Common hero is never in a chest pool list: " + c.Id));
        }

        [Test]
        public void ANewSaveOwnsMomoOnlyAndASaveNamingAnInactiveHeroDropsItWithoutCrashing()
        {
            var svc = new ProgressService(BoingUpSkyTower.Content.ContentRepository.LoadFromResources(), new MemorySaveStore(), 5);
            Assert.IsTrue(svc.OwnsCharacter("momo"));
            foreach (string id in AllIds().Where(i => i != "momo")) Assert.IsFalse(svc.OwnsCharacter(id), id + " is not free");
            foreach (string id in Inactive)
            {
                Assert.AreEqual(0, svc.AddCopies(id, 1), id + ": an inactive hero can never be granted");
                Assert.IsFalse(svc.OwnsCharacter(id), id);
                Assert.IsFalse(svc.IsCharacterVisible(id), id);
            }
            Assert.AreEqual(1, CharacterCatalog.OwnedCards(svc).Count, "one card: Momo");
            CollectionAssert.IsEmpty(svc.PendingHeroReveals());
        }

        [Test]
        public void LoadingASaveThatNamesAnInactiveHeroDropsItAndFallsBackToMomo()
        {
            var store = new MemorySaveStore();
            var old = new SaveData();
            old.Characters.Add("bumble"); old.Characters.Add("momo");
            old.Copies["bumble_u"] = 3; old.Copies["ryu"] = 2; old.Copies["momo"] = 1;
            old.HeroesRevealed.Add("pip");
            store.Save(old);
            var svc = new ProgressService(BoingUpSkyTower.Content.ContentRepository.LoadFromResources(), store, 5);   // must not throw
            CollectionAssert.DoesNotContain(svc.Data.Characters, "bumble");
            CollectionAssert.DoesNotContain(svc.Data.Copies.Keys, "bumble_u");
            CollectionAssert.DoesNotContain(svc.Data.Copies.Keys, "ryu");
            CollectionAssert.DoesNotContain(svc.Data.HeroesRevealed, "pip");
            Assert.IsFalse(svc.OwnsCharacter("bumble"));
            Assert.AreEqual(0, svc.Copies("ryu"));
            Assert.IsTrue(svc.OwnsCharacter("momo"));
            Assert.AreSame(CharacterCatalog.Momo, CharacterCatalog.Get("bumble"), "a hero id that is not active falls back to Momo");
        }

        [Test]
        public void RegionOneTierRowsFollowTheTableAndThePowerPools()
        {
            int rows = 0;
            foreach (string line in Lines)
            {
                CharacterSpec common = CharacterDefs.Get(line);
                for (int i = 1; i < 5; i++)
                {
                    TierRow row = MergeTierDefs.Rows.First(r => r.Id == line + Suffix[i]);
                    HeroRow hr = HeroTable.Find(row.Id);
                    rows++;
                    Assert.AreEqual(Ladder[i], row.Rarity);
                    Assert.IsNull(TierRules.Validate(row), row.Id);
                    Assert.IsNotNull(hr, row.Id + " has an absolute row in the hero table");
                    Assert.IsTrue(hr.Active, row.Id);
                    CharacterSpec spec = CharacterDefs.Get(row.Id);
                    Assert.IsNull(TierRules.ValidatePowers(line, row.Rarity, spec.Powers), row.Id);
                    Assert.AreEqual(CharacterDefs.CooldownSeconds[i], TierRules.RuleFor(row.Rarity).CooldownSeconds);
                    Assert.IsNull(spec.Stats.ValidatePhysics(), row.Id + " plays inside the limits");
                    Assert.AreEqual(hr.LaunchTenths, spec.Stats.LaunchTenths, row.Id);
                    Assert.AreEqual(hr.Speed, spec.Stats.MaxSpeed, row.Id);
                    Assert.AreEqual(hr.Control, spec.Stats.Control, row.Id);
                    Assert.AreEqual(hr.Wall, spec.Stats.WallBoostHundredths, row.Id);
                    Assert.AreEqual(common.Stats.RunBonusCoins + row.RunCoinsAdd, spec.Stats.RunBonusCoins, row.Id);
                    Assert.AreEqual(common.Stats.RunBonusGems + row.RunGemsAdd, spec.Stats.RunBonusGems, row.Id);
                    ShownStats shown = ShownStats.From(spec.Stats, new SimConfig());
                    Assert.AreEqual(shown.Speed, spec.ShownValues().Speed, row.Id + " shows its own absolute speed");
                    Assert.AreEqual(shown.Jump, spec.ShownValues().Jump, row.Id);
                }
            }
            Assert.AreEqual(5 + 19 * 5, MergeTierDefs.Rows.Length, "the 100 tier rows of the inactive lines stay as data: 5 original Uncommon rows + 19 rows for each of Regions 1 to 5");
            Assert.AreEqual(4, rows, "Momo's four tiers above Common are checked");
            Assert.AreEqual(20, CharacterDefs.All.Count(c => c.IsMergeTier), "5 lines x 4 tiers are real characters");
        }

        [Test]
        public void TheRegion1CommonHeroFollowsTheForgivenessIndexRules()
        {
            // docs/44: every Common hero is a generated, measured draw inside the Common band (Jump 0 to 5 percent over 19.0)
            foreach (string line in Lines)
            {
                CharacterStats s = CharacterDefs.Get(line).Stats;
                Assert.IsNull(s.Validate(), line);
                Assert.IsNull(HeroIndex.Validate(0, new HeroPhysics(s.LaunchTenths, s.MaxSpeed, s.Control, s.WallBoostHundredths)), line);
            }
        }

        [Test]
        public void ChestsAndDrawsGiveOnlyActiveHeroesFromOpenRegionsAndReachEveryMomoTier()
        {
            var region1Lines = new HashSet<string>(Lines);
            foreach (ChestKind kind in new[] { ChestKind.Iron, ChestKind.Golden })
            {
                var seen = new HashSet<string>();
                ulong s = 77;
                for (int i = 0; i < 6000; i++)
                {
                    CharacterSpec c = ChestRolls.DrawHero(kind, Mix(ref s), Mix(ref s), CharacterDefs.All, region => false);   // only region 1 is open
                    Assert.IsTrue(region1Lines.Contains(c.LineId), c.Id + " is from a closed region");
                    Assert.IsTrue(c.Rarity < CharacterRarity.Legendary, "a chest never gives Legendary");
                    seen.Add(c.Id);
                }
                CharacterRarity[] tiers = ChestRolls.Tiers(kind);
                foreach (string line in Lines)
                    foreach (CharacterRarity t in tiers)
                        CollectionAssert.Contains(seen, line + Suffix[System.Array.IndexOf(Ladder, t)], kind + " can give " + line + " " + t);
            }
            // bundle hero cards (Uncommon, Rare, Elite)
            var cards = new HashSet<string>();
            ulong b = 5;
            for (int i = 0; i < 6000; i++)
            {
                CharacterSpec c = BundleCards.Draw(Mix(ref b), Mix(ref b), CharacterDefs.All, region => false);
                Assert.IsTrue(region1Lines.Contains(c.LineId), c.Id);
                cards.Add(c.Id);
            }
            foreach (string line in Lines) foreach (int i in new[] { 1, 2, 3 }) CollectionAssert.Contains(cards, line + Suffix[i]);
            // with regions 2 and 3 open the pool grows by their ACTIVE lines only, never by regions 4 and 5 or by an inactive hero
            var wider = new HashSet<string>();
            ulong w = 9;
            for (int i = 0; i < 8000; i++) wider.Add(BundleCards.Draw(Mix(ref w), Mix(ref w), CharacterDefs.All, region => region <= 3).LineId);
            CollectionAssert.AreEquivalent(new[] { "momo", "forestgirl", "beachotter" }, wider);
        }

        [Test]
        public void ShopHeroCardsAreRandomRegion1HeroesOfTheBoughtTier()
        {
            foreach (CharacterRarity r in HeroPurchase.Tiers)
            {
                var got = new HashSet<string>();
                for (ulong roll = 0; roll < 200; roll++)
                {
                    CharacterSpec c = HeroPurchase.Pick(r, CharacterDefs.All, region => false, roll);
                    Assert.IsNotNull(c, r.ToString());
                    Assert.AreEqual(r, c.Rarity);
                    got.Add(c.Id);
                }
                Assert.AreEqual(1, got.Count, r + ": only Momo's tier hero is active in Region 1");
            }
        }

        [Test]
        public void CharacterTestOwnsTheFiveCommonsAndNoTier()
        {
            bool old = ProgressService.TestAllCharacters;
            try
            {
                ProgressService.TestAllCharacters = true;
                var svc = new ProgressService(BoingUpSkyTower.Content.ContentRepository.LoadFromResources(), new MemorySaveStore(), 5);
                foreach (CharacterSpec c in CharacterDefs.All) Assert.AreEqual(c.Rarity == CharacterRarity.Common, svc.OwnsCharacter(c.Id), c.Id);
            }
            finally { ProgressService.TestAllCharacters = old; }
        }
    }
}
