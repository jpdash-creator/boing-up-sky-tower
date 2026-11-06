using System.Collections.Generic;
using System.Linq;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using BoingUpSkyTower.Simulation;
using NUnit.Framework;

namespace BoingUpSkyTower.Tests
{
    /// <summary>Bug List 5 F4.5 (docs/43): Region 5 (Bamboo Temple) has Bao, Sprout, Tora, Rusu and Ryu at all five tiers: data, art, merge chain, tier rows, rewards and draws from open regions only.</summary>
    public class Region5HeroTests
    {
        static readonly string[] Lines = { "monk", "sprout", "tora", "rusu", "ryu" };
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
        public void AllTwentyFiveRegion5HeroesExistWithTheRightRarityLineAndRegion()
        {
            foreach (string line in Lines)
                for (int i = 0; i < 5; i++)
                {
                    string id = line + Suffix[i];
                    CharacterSpec c = CharacterDefs.Get(id);
                    Assert.IsNotNull(c, id);
                    Assert.AreEqual(Ladder[i], c.Rarity, id);
                    Assert.AreEqual(line, c.LineId, id);
                    Assert.AreEqual(5, BundleCards.RegionOf(c, CharacterDefs.All), id + " belongs to Region 5");
                    Assert.AreEqual(CharacterDefs.PowerCount[i], c.Powers.Length, id);
                    Assert.IsNull(c.Stats.ValidateTier(), id);
                }
            Assert.AreEqual("Bao", CharacterDefs.Get("monk").Name);
            foreach (string n in new[] { "Sprout", "Tora", "Rusu", "Ryu" }) Assert.IsTrue(CharacterDefs.All.Any(c => c.Name == n), n);
            Assert.IsTrue(CharacterDefs.Monk.RegionStarter);
            Assert.IsTrue(CharacterDefs.Monk.NeedsReveal, "Bao keeps the region reveal");
            foreach (string id in new[] { "sprout", "tora", "rusu", "ryu" })
            {
                CharacterSpec c = CharacterDefs.Get(id);
                Assert.IsFalse(c.RegionStarter, id);
                Assert.IsFalse(c.NeedsReveal, id);
                Assert.IsTrue(c.IsRegionHero, id);
                Assert.IsNull(c.Stats.Validate(), id);
            }
            Assert.Greater(CharacterDefs.Get("rusu").Stats.CoinFind, 0, "Rusu finds extra coins");
        }

        [Test]
        public void EveryRegion5HeroHasItsOwnArtThatLoads()
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
        public void TheMergeChainIsCompleteAndRegion5MembersMergeUp()
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
        public void RegionFiveTierRowsFollowTheChainAndThePowerPools()
        {
            var firstPower = new Dictionary<string, PowerUp> { { "monk", PowerUp.WallStick }, { "sprout", PowerUp.Shield }, { "tora", PowerUp.DoubleJump }, { "rusu", PowerUp.Magnet }, { "ryu", PowerUp.Rocket } };
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
                    if (line == "sprout") Assert.Greater(row.WallPct, 0, row.Id + " boosts wall bounce");
                    if (line == "tora") Assert.Greater(row.JumpPct, 0, row.Id + " boosts jump");
                    if (line == "ryu") Assert.Greater(row.SpeedPct, 0, row.Id + " boosts speed");
                    Assert.AreEqual(common.Stats.CoinFind, spec.Stats.CoinFind, row.Id + " keeps the coin find");
                    prev = TierRules.Apply(prev, common.ShownValues(), row);
                }
            }
        }

        [Test]
        public void Region5CommonsComeFromRegion5GoalsAndAreNotFree()
        {
            var svc = new ProgressService(ContentRepository.LoadFromResources(), new MemorySaveStore(), 5);
            foreach (string id in new[] { "sprout", "tora", "rusu", "ryu" }) Assert.IsFalse(svc.OwnsCharacter(id), id);
            var map = new Dictionary<string, string> { { "g_5_levels_2", "sprout" }, { "g_5_levels_3", "tora" }, { "g_5_levels_4", "rusu" }, { "g_5_stars_5", "ryu" } };
            foreach (KeyValuePair<string, string> kv in map)
            {
                RegionGoal g = svc.RewardDefs.FindGoal(kv.Key);
                Assert.IsNotNull(g, kv.Key);
                Assert.AreEqual(kv.Value, g.HeroId, kv.Key);
            }
        }

        [Test]
        public void ChestsBundlesAndShopGiveRegion5HeroesOnlyWhenRegion5IsOpen()
        {
            var rN = new HashSet<string>(Lines);
            for (int pass = 0; pass < 2; pass++)
            {
                bool open = pass == 1;
                var seenLines = new HashSet<string>();
                ulong s = 31;
                for (int i = 0; i < 6000; i++)
                {
                    CharacterSpec c = ChestRolls.DrawHero(i % 2 == 0 ? ChestKind.Iron : ChestKind.Golden, Mix(ref s), Mix(ref s), CharacterDefs.All, region => open && region == 5);
                    seenLines.Add(c.LineId);
                    CharacterSpec b = BundleCards.Draw(Mix(ref s), Mix(ref s), CharacterDefs.All, region => open && region == 5);
                    seenLines.Add(b.LineId);
                }
                foreach (string line in Lines) Assert.AreEqual(open, seenLines.Contains(line), line + " open=" + open);
                if (!open) CollectionAssert.IsEmpty(seenLines.Intersect(rN));
            }
            foreach (CharacterRarity r in HeroPurchase.Tiers)
            {
                var got = new HashSet<string>();
                for (ulong roll = 0; roll < 400; roll++) got.Add(HeroPurchase.Pick(r, CharacterDefs.All, region => region == 5, roll).LineId);
                Assert.IsTrue(Lines.All(got.Contains), r + ": all five Region 5 lines can be bought when Region 5 is open");
            }
        }
    }
}
