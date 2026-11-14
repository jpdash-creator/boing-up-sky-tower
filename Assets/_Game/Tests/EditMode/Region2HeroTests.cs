using System.Collections.Generic;
using System.Linq;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Simulation;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// Region 2 (Mushroom Grove) has ONE ACTIVE hero line, Capi, at all five tiers (owner Bug List 6, 2026-11-09: the four extra Common lines of this region and their tiers are inactive always):
    /// data, art, merge chain, tier rows and draws from open regions only.
    /// </summary>
    public class Region2HeroTests
    {
        static readonly string[] Lines = { "forestgirl" };
        static readonly string[] Inactive = { "nib", "spike", "moss", "fenn" };
        static readonly string[] Suffix = { "", "_u", "_r", "_e", "_l" };
        static readonly CharacterRarity[] Ladder = { CharacterRarity.Common, CharacterRarity.Uncommon, CharacterRarity.Rare, CharacterRarity.Epic, CharacterRarity.Legendary };

        static IEnumerable<string> AllIds() { foreach (string l in Lines) foreach (string s in Suffix) yield return l + s; }

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
        public void AllFiveRegion2HeroesExistWithTheRightRarityLineAndRegion()
        {
            foreach (string line in Lines)
                for (int i = 0; i < 5; i++)
                {
                    string id = line + Suffix[i];
                    CharacterSpec c = CharacterDefs.Get(id);
                    Assert.IsNotNull(c, id);
                    Assert.AreEqual(Ladder[i], c.Rarity, id);
                    Assert.AreEqual(line, c.LineId, id);
                    Assert.AreEqual(2, BundleCards.RegionOf(c, CharacterDefs.All), id + " belongs to Region 2");
                    Assert.AreEqual(CharacterDefs.PowerCount[i], c.Powers.Length, id);
                    Assert.IsNull(c.Stats.ValidateTier(), id);
                }
            Assert.AreEqual("Capi", CharacterDefs.Get("forestgirl").Name);
            Assert.IsTrue(CharacterDefs.ForestGirl.RegionStarter);
            Assert.IsTrue(CharacterDefs.ForestGirl.NeedsReveal, "Capi keeps the region reveal");
            Assert.IsTrue(CharacterDefs.ForestGirl.IsRegionHero);
            Assert.IsNull(CharacterDefs.ForestGirl.Stats.Validate());
        }

        [Test]
        public void TheExtraCommonHeroesOfRegion2AreInactiveAndNeverExistAtRuntime()
        {
            foreach (string line in Inactive)
                foreach (string s in Suffix)
                {
                    Assert.IsNull(CharacterDefs.Get(line + s), line + s + " is inactive always");
                    Assert.IsNotNull(HeroTable.Find(line + s), line + s + " keeps its physics row as data");
                    Assert.IsFalse(HeroTable.Find(line + s).Active, line + s);
                }
            Assert.AreEqual(1, CharacterDefs.All.Count(c => c.Region == 2), "one Common hero per region");
        }

        [Test]
        public void EveryRegion2HeroHasItsOwnArtThatLoads()
        {
            foreach (string id in AllIds())
            {
                CharacterDef def = CharacterCatalog.Get(id);
                Assert.AreEqual(id, def.Id);
                Assert.IsFalse(def.UsesPlaceholderArt, id);
                CharacterRig rig = def.Rig;
                Assert.GreaterOrEqual(rig.Order.Length, 6, id);
                Assert.AreEqual(1365f, rig.CanvasW, id);
                foreach (string part in rig.Order) Assert.IsNotNull(def.PartSprite(part), id + "/" + part);
                Assert.IsNotNull(def.PartSprite("head"), id);
                Assert.IsNotNull(def.PartSprite("body"), id);
            }
        }

        [Test]
        public void InactiveHeroIdsFallBackToMomoInTheCatalog()
        {
            foreach (string line in Inactive) Assert.AreSame(CharacterCatalog.Momo, CharacterCatalog.Get(line), line + " has no catalog entry and falls back to Momo");
        }

        [Test]
        public void TheMergeChainIsCompleteAndRegion2MembersMergeUp()
        {
            foreach (string line in Lines)
            {
                CharacterSpec cur = CharacterDefs.Get(line);
                for (int i = 1; i < 5; i++)
                {
                    CharacterSpec next = MergeRules.NextTier(cur);
                    Assert.IsNotNull(next, cur.Id);
                    Assert.AreEqual(line + Suffix[i], next.Id);
                    cur = next;
                }
                Assert.IsNull(MergeRules.NextTier(cur), line + " Legendary does not merge");
            }
        }

        [Test]
        public void Region2TierRowsFollowTheTableAndThePowerPools()
        {
            foreach (string line in Lines)
            {
                CharacterSpec common = CharacterDefs.Get(line);
                for (int i = 1; i < 5; i++)
                {
                    TierRow row = MergeTierDefs.Rows.First(r => r.Id == line + Suffix[i]);
                    Assert.IsNull(TierRules.Validate(row), row.Id);
                    Assert.AreEqual(HeroTable.Find(row.Id).LaunchTenths, CharacterDefs.Get(row.Id).Stats.LaunchTenths, row.Id + " plays its hero table row");
                    CharacterSpec spec = CharacterDefs.Get(row.Id);
                    Assert.IsNull(TierRules.ValidatePowers(line, row.Rarity, spec.Powers), row.Id);
                    Assert.IsNull(spec.Stats.ValidatePhysics(), row.Id);
                    Assert.AreEqual(common.Stats.CoinFind, spec.Stats.CoinFind, row.Id + " keeps the coin find");
                }
            }
        }

        [Test]
        public void Region2GoalsNameNoHeroAndDrawsOnlyGiveActiveHeroes()
        {
            var svc = new ProgressService(ContentRepository.LoadFromResources(), new MemorySaveStore(), 5);
            foreach (string id in Inactive) Assert.IsFalse(svc.OwnsCharacter(id), id);
            // owner 2026-11-13: only the gold-on-25-levels goal gives a random ACTIVE Common hero; levels 10/15/20 and 75 stars pay coins
            foreach (string goal in new[] { "g_2_levels_2", "g_2_levels_3", "g_2_levels_4", "g_2_stars_5", "g_2_gold_5" })
            {
                RegionGoal g = svc.RewardDefs.FindGoal(goal);
                Assert.IsNotNull(g, goal);
                Assert.IsTrue(string.IsNullOrEmpty(g.HeroId), goal + " names no hero any more");
                Assert.AreEqual(goal.EndsWith("gold_5") ? RewardEntry.RandomOpenRegionHero : 0, g.HeroRegion, goal);
            }
        }

        [Test]
        public void ChestsBundlesAndShopGiveRegion2HeroesOnlyWhenRegion2IsOpen()
        {
            var own = new HashSet<string>(Lines);
            for (int pass = 0; pass < 2; pass++)
            {
                bool open = pass == 1;
                var seenLines = new HashSet<string>();
                ulong s = 31;
                for (int i = 0; i < 6000; i++)
                {
                    CharacterSpec c = ChestRolls.DrawHero(i % 2 == 0 ? ChestKind.Iron : ChestKind.Golden, Mix(ref s), Mix(ref s), CharacterDefs.All, region => open && region == 2);
                    seenLines.Add(c.LineId);
                    CharacterSpec b = BundleCards.Draw(Mix(ref s), Mix(ref s), CharacterDefs.All, region => open && region == 2);
                    seenLines.Add(b.LineId);
                }
                foreach (string line in Lines) Assert.AreEqual(open, seenLines.Contains(line), line + " open=" + open);
                if (!open) CollectionAssert.IsEmpty(seenLines.Intersect(own));
                CollectionAssert.IsEmpty(seenLines.Intersect(Inactive), "no inactive hero is ever drawn");
            }
            foreach (CharacterRarity r in HeroPurchase.Tiers)
            {
                var got = new HashSet<string>();
                for (ulong roll = 0; roll < 400; roll++) got.Add(HeroPurchase.Pick(r, CharacterDefs.All, region => region == 2, roll).LineId);
                Assert.IsTrue(Lines.All(got.Contains), r + ": the Region 2 line can be bought when Region 2 is open");
                CollectionAssert.IsEmpty(got.Intersect(Inactive));
            }
        }
    }
}
