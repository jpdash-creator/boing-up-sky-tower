using System.Collections.Generic;
using System.Linq;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using BoingUpSkyTower.Simulation;
using NUnit.Framework;

namespace BoingUpSkyTower.Tests
{
    /// <summary>Bug List 5 F4.3 (docs/41): Region 3 (Sunny Beach) has Otto, Pinch, Shelly, Bloop and Coco at all five tiers: data, art, merge chain, tier rows, rewards and draws from open regions only.</summary>
    public class Region3HeroTests
    {
        static readonly string[] Lines = { "beachotter", "pinch", "shelly", "bloop", "coco" };
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
        public void AllTwentyFiveRegion3HeroesExistWithTheRightRarityLineAndRegion()
        {
            foreach (string line in Lines)
                for (int i = 0; i < 5; i++)
                {
                    string id = line + Suffix[i];
                    CharacterSpec c = CharacterDefs.Get(id);
                    Assert.IsNotNull(c, id);
                    Assert.AreEqual(Ladder[i], c.Rarity, id);
                    Assert.AreEqual(line, c.LineId, id);
                    Assert.AreEqual(3, BundleCards.RegionOf(c, CharacterDefs.All), id + " belongs to Region 3");
                    Assert.AreEqual(CharacterDefs.PowerCount[i], c.Powers.Length, id);
                    Assert.IsNull(c.Stats.ValidateTier(), id);
                }
            Assert.AreEqual("Otto", CharacterDefs.Get("beachotter").Name);
            foreach (string n in new[] { "Pinch", "Shelly", "Bloop", "Coco" }) Assert.IsTrue(CharacterDefs.All.Any(c => c.Name == n), n);
            Assert.IsTrue(CharacterDefs.BeachOtter.RegionStarter);
            Assert.IsTrue(CharacterDefs.BeachOtter.NeedsReveal, "Otto keeps the region reveal");
            foreach (string id in new[] { "pinch", "shelly", "bloop", "coco" })
            {
                CharacterSpec c = CharacterDefs.Get(id);
                Assert.IsFalse(c.RegionStarter, id);
                Assert.IsFalse(c.NeedsReveal, id);
                Assert.IsTrue(c.IsRegionHero, id);
                Assert.IsNull(c.Stats.Validate(), id);
            }
            Assert.Greater(CharacterDefs.Get("coco").Stats.CoinFind, 0, "Coco finds extra coins");
        }

        [Test]
        public void EveryRegion3HeroHasItsOwnArtThatLoads()
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
        public void TheMergeChainIsCompleteAndRegion3MembersMergeUp()
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
        public void RegionThreeTierRowsFollowTheChainAndThePowerPools()
        {
            var firstPower = new Dictionary<string, PowerUp> { { "beachotter", PowerUp.Magnet }, { "pinch", PowerUp.Shield }, { "shelly", PowerUp.SlowTime }, { "bloop", PowerUp.DoubleJump }, { "coco", PowerUp.Magnet } };
            foreach (string line in Lines)
            {
                CharacterSpec common = CharacterDefs.Get(line);
                ShownStats prev = common.ShownValues();
                for (int i = 1; i < 5; i++)
                {
                    TierRow row = MergeTierDefs.Rows.First(r => r.Id == line + Suffix[i]);
                    Assert.IsNull(TierRules.Validate(row), row.Id);
                    Assert.IsNull(TierRules.ValidateChain(prev, row), row.Id);
                    CharacterSpec spec = CharacterDefs.Get(row.Id);
                    Assert.IsNull(TierRules.ValidatePowers(line, row.Rarity, spec.Powers), row.Id);
                    Assert.IsNull(spec.Stats.ValidatePhysics(), row.Id);
                    PowerUp fp;
                    if (firstPower.TryGetValue(line, out fp)) Assert.AreEqual(fp, spec.Powers[0], row.Id);
                    if (line == "pinch") Assert.Greater(row.WallPct, 0, row.Id + " boosts wall bounce");
                    if (line == "shelly") Assert.Greater(row.ControlPct, 0, row.Id + " boosts control");
                    if (line == "bloop") Assert.Greater(row.JumpPct, 0, row.Id + " boosts jump");
                    Assert.AreEqual(common.Stats.CoinFind, spec.Stats.CoinFind, row.Id + " keeps the coin find");
                    prev = TierRules.Apply(prev, common.ShownValues(), row);
                }
            }
        }

        [Test]
        public void Region3CommonsComeFromRegion3GoalsAndAreNotFree()
        {
            var svc = new ProgressService(ContentRepository.LoadFromResources(), new MemorySaveStore(), 5);
            foreach (string id in new[] { "pinch", "shelly", "bloop", "coco" }) Assert.IsFalse(svc.OwnsCharacter(id), id);
            var map = new Dictionary<string, string> { { "g_3_levels_2", "pinch" }, { "g_3_levels_3", "shelly" }, { "g_3_levels_4", "bloop" }, { "g_3_stars_5", "coco" } };
            foreach (KeyValuePair<string, string> kv in map)
            {
                RegionGoal g = svc.RewardDefs.FindGoal(kv.Key);
                Assert.IsNotNull(g, kv.Key);
                Assert.AreEqual(kv.Value, g.HeroId, kv.Key);
            }
        }

        [Test]
        public void ChestsBundlesAndShopGiveRegion3HeroesOnlyWhenRegion3IsOpen()
        {
            var r3 = new HashSet<string>(Lines);
            for (int pass = 0; pass < 2; pass++)
            {
                bool open = pass == 1;
                var seenLines = new HashSet<string>();
                ulong s = 31;
                for (int i = 0; i < 6000; i++)
                {
                    CharacterSpec c = ChestRolls.DrawHero(i % 2 == 0 ? ChestKind.Iron : ChestKind.Golden, Mix(ref s), Mix(ref s), CharacterDefs.All, region => open && region == 3);
                    seenLines.Add(c.LineId);
                    CharacterSpec b = BundleCards.Draw(Mix(ref s), Mix(ref s), CharacterDefs.All, region => open && region == 3);
                    seenLines.Add(b.LineId);
                }
                foreach (string line in Lines) Assert.AreEqual(open, seenLines.Contains(line), line + " open=" + open);
                if (!open) CollectionAssert.IsEmpty(seenLines.Intersect(r3));
            }
            foreach (CharacterRarity r in HeroPurchase.Tiers)
            {
                var got = new HashSet<string>();
                for (ulong roll = 0; roll < 400; roll++) got.Add(HeroPurchase.Pick(r, CharacterDefs.All, region => region == 3, roll).LineId);
                Assert.IsTrue(Lines.All(got.Contains), r + ": all five Region 3 lines can be bought when Region 3 is open");
            }
        }
    }
}
