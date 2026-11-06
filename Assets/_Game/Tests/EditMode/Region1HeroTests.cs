using System.Collections.Generic;
using System.Linq;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using BoingUpSkyTower.Simulation;
using NUnit.Framework;

namespace BoingUpSkyTower.Tests
{
    /// <summary>
    /// Bug List 5 F4.1 (docs/40): Region 1 has 5 characters x 5 rarity tiers = 25 heroes. The data (ids, rarity, merge chain, region), the art that loads, the tier rows (rule, chain,
    /// identity stat, powers), and that chests, bundles and shop draws can give them ONLY from open regions.
    /// </summary>
    public class Region1HeroTests
    {
        static readonly string[] Lines = { "momo", "bumble", "ribbi", "dandy", "pip" };
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
        public void AllTwentyFiveRegion1HeroesExistWithTheRightRarityLineAndRegion()
        {
            Assert.AreEqual(25, AllIds().Count());
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
            Assert.AreEqual("Bumble", CharacterDefs.Get("bumble").Name);
            Assert.AreEqual("Ribbi", CharacterDefs.Get("ribbi").Name);
            Assert.AreEqual("Dandy", CharacterDefs.Get("dandy").Name);
            Assert.AreEqual("Pip", CharacterDefs.Get("pip").Name);
            // all ids and names are different
            Assert.AreEqual(CharacterDefs.All.Length, CharacterDefs.All.Select(c => c.Id).Distinct().Count());
            Assert.AreEqual(CharacterDefs.All.Length, CharacterDefs.All.Select(c => c.Name).Distinct().Count());
            foreach (CharacterSpec c in CharacterDefs.All) StringAssert.DoesNotContain("wing", c.Name.ToLowerInvariant(), "no wings on any hero");
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
        public void TheFourNewCommonsAreRegion1HeroesButNotStartersAndNeedNoReveal()
        {
            foreach (string id in new[] { "bumble", "ribbi", "dandy", "pip" })
            {
                CharacterSpec c = CharacterDefs.Get(id);
                Assert.IsTrue(c.IsRegionHero, id);
                Assert.IsFalse(c.RegionStarter, id);
                Assert.IsFalse(c.NeedsReveal, id);
                Assert.AreEqual(1, c.Region, id);
            }
            Assert.IsTrue(CharacterDefs.Momo.RegionStarter);
            Assert.IsFalse(CharacterDefs.Momo.NeedsReveal);
            Assert.IsTrue(CharacterDefs.ForestGirl.NeedsReveal, "regions 2 to 5 keep their reveal");
            CharacterDefs.OfRarity(CharacterRarity.Common).ForEach(c => Assert.Fail("a Common hero is never in a chest pool list: " + c.Id));
        }

        [Test]
        public void ANewSaveOwnsMomoOnlyAndACopyMakesTheOthersOwnedAndVisible()
        {
            var svc = new ProgressService(BoingUpSkyTower.Content.ContentRepository.LoadFromResources(), new MemorySaveStore(), 5);
            Assert.IsTrue(svc.OwnsCharacter("momo"));
            foreach (string id in AllIds().Where(i => i != "momo")) Assert.IsFalse(svc.OwnsCharacter(id), id + " is not free");
            foreach (string id in new[] { "bumble", "ribbi", "dandy", "pip" })
            {
                svc.AddCopies(id, 1);
                Assert.IsTrue(svc.OwnsCharacter(id), id);
                Assert.IsTrue(svc.IsCharacterVisible(id), id + ": no reveal pop-up needed");
            }
            Assert.AreEqual(5, CharacterCatalog.OwnedCards(svc).Count, "one card per owned copy");
            CollectionAssert.IsEmpty(svc.PendingHeroReveals());
        }

        [Test]
        public void RegionOneTierRowsFollowTheChainTheIdentityAndThePowerPools()
        {
            var identity = new Dictionary<string, int> { { "ribbi", 0 }, { "bumble", 1 }, { "dandy", 2 }, { "pip", 3 } };   // Jump, Speed, Control, Wall Bounce
            var firstPower = new Dictionary<string, PowerUp> { { "bumble", PowerUp.Flight }, { "ribbi", PowerUp.DoubleJump }, { "dandy", PowerUp.SlowTime }, { "pip", PowerUp.Shield } };
            int rows = 0;
            foreach (string line in Lines)
            {
                CharacterSpec common = CharacterDefs.Get(line);
                ShownStats prev = common.ShownValues();
                for (int i = 1; i < 5; i++)
                {
                    TierRow row = MergeTierDefs.Rows.First(r => r.Id == line + Suffix[i]);
                    rows++;
                    Assert.AreEqual(Ladder[i], row.Rarity);
                    Assert.IsNull(TierRules.Validate(row), row.Id);
                    Assert.IsNull(TierRules.ValidateChain(prev, row), row.Id + " prints exactly its percent over the tier below");
                    CharacterSpec spec = CharacterDefs.Get(row.Id);
                    Assert.IsNull(TierRules.ValidatePowers(line, row.Rarity, spec.Powers), row.Id);
                    Assert.AreEqual(CharacterDefs.CooldownSeconds[i], TierRules.RuleFor(row.Rarity).CooldownSeconds);
                    Assert.IsNull(spec.Stats.ValidatePhysics(), row.Id + " plays inside the limits");
                    Assert.AreEqual(common.Stats.RunBonusCoins + row.RunCoinsAdd, spec.Stats.RunBonusCoins, row.Id);
                    Assert.AreEqual(common.Stats.RunBonusGems + row.RunGemsAdd, spec.Stats.RunBonusGems, row.Id);
                    int id;
                    if (identity.TryGetValue(line, out id))
                    {
                        int[] pct = { row.JumpPct, row.SpeedPct, row.ControlPct, row.WallPct };
                        Assert.Greater(pct[id], 0, row.Id + " boosts the hero's identity stat");
                        Assert.AreEqual(firstPower[line], spec.Powers[0], row.Id + " keeps the identity power first");
                    }
                    prev = TierRules.Apply(prev, common.ShownValues(), row);
                    Assert.AreEqual(prev.Speed, spec.ShownValues().Speed, row.Id + " shown speed is the chain result");
                    Assert.AreEqual(prev.Jump, spec.ShownValues().Jump, row.Id);
                }
            }
            Assert.AreEqual(5 + 19 + 19, MergeTierDefs.Rows.Length, "5 original Uncommon rows + 19 Region 1 rows + 19 Region 2 rows");
            Assert.AreEqual(20, rows, "Momo's Uncommon row is the original; 4 tiers x 5 lines are checked");
        }

        [Test]
        public void TheCommonIdentityStatsAreTheBestOfTheFiveInRegion1()
        {
            Assert.GreaterOrEqual(CharacterDefs.Get("bumble").Stats.MaxSpeed, CharacterDefs.Get("momo").Stats.MaxSpeed + 1, "Bumble is the fast one");
            Assert.GreaterOrEqual(CharacterDefs.Get("ribbi").Stats.JumpLaunch, CharacterDefs.Get("momo").Stats.JumpLaunch + 2, "Ribbi jumps highest");
            Assert.GreaterOrEqual(CharacterDefs.Get("dandy").Stats.Control, CharacterDefs.Get("momo").Stats.Control + 3, "Dandy is the floaty one");
            Assert.GreaterOrEqual(CharacterDefs.Get("pip").Stats.WallBoostHundredths, CharacterDefs.Get("momo").Stats.WallBoostHundredths + 7, "Pip bounces off walls best");
            foreach (string line in Lines) Assert.IsNull(CharacterDefs.Get(line).Stats.Validate(), line);
        }

        [Test]
        public void ChestsAndDrawsGiveRegion1HeroesOnlyFromOpenRegionsAndReachEveryOne()
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
            // with regions 2 and 3 open the pool grows by their Uncommon heroes, never by regions 4 and 5
            var wider = new HashSet<string>();
            ulong w = 9;
            for (int i = 0; i < 8000; i++) wider.Add(BundleCards.Draw(Mix(ref w), Mix(ref w), CharacterDefs.All, region => region <= 3).LineId);
            CollectionAssert.AreEquivalent(new[] { "momo", "bumble", "ribbi", "dandy", "pip", "forestgirl", "nib", "spike", "moss", "fenn", "beachotter" }, wider);
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
                Assert.AreEqual(5, got.Count, r + ": all five Region 1 heroes of the tier can be bought");
            }
        }

        [Test]
        public void CharacterTestOwnsTheThirteenCommonsAndNoTier()
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
