using System.Collections.Generic;
using System.Linq;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using BoingUpSkyTower.Simulation;
using NUnit.Framework;

namespace BoingUpSkyTower.Tests
{
    /// <summary>Bug List 5 F4.2 (docs/40): Region 2 (Mushroom Grove) has Capi, Nib, Spike, Moss and Fenn at all five tiers: data, art, merge chain, tier rows, rewards and draws from open regions only.</summary>
    public class Region2HeroTests
    {
        static readonly string[] Lines = { "forestgirl", "nib", "spike", "moss", "fenn" };
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
        public void AllTwentyFiveRegion2HeroesExistWithTheRightRarityLineAndRegion()
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
            foreach (string n in new[] { "Nib", "Spike", "Moss", "Fenn" }) Assert.IsTrue(CharacterDefs.All.Any(c => c.Name == n), n);
            Assert.IsTrue(CharacterDefs.ForestGirl.RegionStarter);
            Assert.IsTrue(CharacterDefs.ForestGirl.NeedsReveal, "Capi keeps the region reveal");
            foreach (string id in new[] { "nib", "spike", "moss", "fenn" })
            {
                CharacterSpec c = CharacterDefs.Get(id);
                Assert.IsFalse(c.RegionStarter, id);
                Assert.IsFalse(c.NeedsReveal, id);
                Assert.IsTrue(c.IsRegionHero, id);
                Assert.IsNull(c.Stats.Validate(), id);
            }
            Assert.Greater(CharacterDefs.Get("nib").Stats.CoinFind, 0, "Nib finds extra coins");
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
        public void RegionTwoTierRowsFollowTheTableAndThePowerPools()
        {
            var firstPower = new Dictionary<string, PowerUp> { { "nib", PowerUp.Magnet }, { "spike", PowerUp.Shield }, { "moss", PowerUp.WallStick } };
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
                    PowerUp fp;
                    if (firstPower.TryGetValue(line, out fp)) Assert.AreEqual(fp, spec.Powers[0], row.Id);
                    Assert.AreEqual(common.Stats.CoinFind, spec.Stats.CoinFind, row.Id + " keeps the coin find");
                }
            }
        }

        [Test]
        public void Region2CommonsComeFromRegion2GoalsAndAreNotFree()
        {
            var svc = new ProgressService(ContentRepository.LoadFromResources(), new MemorySaveStore(), 5);
            foreach (string id in new[] { "nib", "spike", "moss", "fenn" }) Assert.IsFalse(svc.OwnsCharacter(id), id);
            var map = new Dictionary<string, string> { { "g_2_levels_2", "nib" }, { "g_2_levels_3", "spike" }, { "g_2_levels_4", "moss" }, { "g_2_stars_5", "fenn" } };
            foreach (KeyValuePair<string, string> kv in map)
            {
                RegionGoal g = svc.RewardDefs.FindGoal(kv.Key);
                Assert.IsNotNull(g, kv.Key);
                Assert.AreEqual(kv.Value, g.HeroId, kv.Key);
            }
        }

        [Test]
        public void ChestsBundlesAndShopGiveRegion2HeroesOnlyWhenRegion2IsOpen()
        {
            var r2 = new HashSet<string>(Lines);
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
                if (!open) CollectionAssert.IsEmpty(seenLines.Intersect(r2));
            }
            foreach (CharacterRarity r in HeroPurchase.Tiers)
            {
                var got = new HashSet<string>();
                for (ulong roll = 0; roll < 400; roll++) got.Add(HeroPurchase.Pick(r, CharacterDefs.All, region => region == 2, roll).LineId);
                Assert.IsTrue(Lines.All(got.Contains), r + ": all five Region 2 lines can be bought when Region 2 is open");
            }
        }
    }
}
