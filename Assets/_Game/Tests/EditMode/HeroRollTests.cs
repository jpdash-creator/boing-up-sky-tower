using System.Collections.Generic;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// Per-player hero stats (owner 2026-11-12, docs/44 section 11): the roll of a tier hero is dealt ONCE when it is first obtained (AddCopies, merge, chest, bundle, shop, grant),
    /// duplicates keep it, Common heroes are never rolled, it is saved additively and old saves still load.
    /// </summary>
    public class HeroRollTests
    {
        ContentRepository repo;
        bool oldChars;

        [SetUp]
        public void SetUp()
        {
            oldChars = ProgressService.TestAllCharacters;
            ProgressService.TestAllCharacters = false;
            repo = ContentRepository.LoadFromResources();
        }

        [TearDown] public void TearDown() { ProgressService.TestAllCharacters = oldChars; }

        ProgressService NewService(MemorySaveStore store = null, int seed = 12345)
        {
            var svc = new ProgressService(repo, store ?? new MemorySaveStore(), 5);
            svc.Data.Bundles = new BundleSaveData { Seed = seed };
            svc.Data.Coins = 1000000;
            svc.Data.Gems = 100000;
            return svc;
        }

        [Test]
        public void EveryTierHeroOfTheFiveLinesIsRolledAndCommonIsNot()
        {
            int tiers = 0;
            foreach (CharacterSpec c in CharacterInventory.All)
            {
                if (c.Rarity == CharacterRarity.Common) { Assert.IsFalse(HeroRolls.IsRollable(c.Id), c.Id + " Common stays fixed"); continue; }
                Assert.IsTrue(HeroRolls.IsRollable(c.Id), c.Id);
                Assert.GreaterOrEqual(HeroRolls.PoolSize(c.Id), 1, c.Id);
                tiers++;
            }
            Assert.AreEqual(20, tiers, "5 lines x 4 upper tiers");
        }

        [Test]
        public void FirstCopyRollsAndDuplicatesKeepTheRoll()
        {
            ProgressService svc = NewService();
            svc.AddCopies("momo_u", 1);
            Assert.IsTrue(svc.Data.HeroRolls.ContainsKey("momo_u"));
            Assert.AreEqual(HeroRolls.Pick(12345, "momo_u", 0), svc.Data.HeroRolls["momo_u"], "variant = f(account seed, hero id, counter)");
            Assert.AreEqual(0, svc.Data.HeroRollNo["momo_u"]);
            Assert.AreEqual(1, svc.Data.HeroRollCount);
            int first = svc.Data.HeroRolls["momo_u"];
            svc.AddCopies("momo_u", 2);
            Assert.AreEqual(first, svc.Data.HeroRolls["momo_u"], "duplicate copies keep the first roll");
            Assert.AreEqual(1, svc.Data.HeroRollCount, "no new roll was made");
            svc.AddCopies("momo_r", 1);
            Assert.AreEqual(2, svc.Data.HeroRollCount);
            Assert.AreEqual(1, svc.Data.HeroRollNo["momo_r"]);
            Assert.AreEqual(HeroRolls.Pick(12345, "momo_r", 1), svc.Data.HeroRolls["momo_r"]);
        }

        [Test]
        public void CommonHeroesAreNeverRolled()
        {
            ProgressService svc = NewService();
            svc.AddCopies("momo", 3);
            svc.AddCopies("forestgirl", 1);
            svc.GrantCharacter("beachotter");
            Assert.AreEqual(0, svc.Data.HeroRolls.Count);
            Assert.AreEqual(0, svc.Data.HeroRollCount);
            Assert.AreSame(CharacterDefs.Momo.Stats, svc.StatsOf(CharacterDefs.Momo), "Momo Common is the fixed 19.0 reference");
        }

        [Test]
        public void MergeRollsTheResultOnceAndKeepsItOnTheNextMerge()
        {
            ProgressService svc = NewService();
            svc.AddCopies("monk", 3);
            Assert.AreEqual(MergeResult.Ok, svc.TryMerge("monk").Result);
            Assert.IsTrue(svc.Data.HeroRolls.ContainsKey("monk_u"));
            Assert.AreEqual(HeroRolls.Pick(12345, "monk_u", 0), svc.Data.HeroRolls["monk_u"]);
            int roll = svc.Data.HeroRolls["monk_u"];
            svc.AddCopies("monk", 3);
            Assert.AreEqual(MergeResult.Ok, svc.TryMerge("monk").Result);
            Assert.AreEqual(2, svc.Copies("monk_u"));
            Assert.AreEqual(roll, svc.Data.HeroRolls["monk_u"]);
            Assert.AreEqual(1, svc.Data.HeroRollCount);
        }

        [Test]
        public void ShopBundleAndChestPathsRollTheHeroTheyGive()
        {
            ProgressService svc = NewService();
            CharacterSpec bought;
            Assert.AreEqual(HeroBuyResult.Ok, svc.TryBuyHero(CharacterRarity.Uncommon, out bought));
            Assert.AreEqual(HeroRolls.IsRollable(bought.Id), svc.Data.HeroRolls.ContainsKey(bought.Id), "shop " + bought.Id);

            List<ChestContents> got;
            Assert.AreEqual(ChestOpenResult.Ok, svc.TryOpenChests(ChestKind.Golden, 10, out got));
            foreach (ChestContents c in got)
                if (c.Hero != null) Assert.AreEqual(HeroRolls.IsRollable(c.Hero.Id), svc.Data.HeroRolls.ContainsKey(c.Hero.Id), "chest " + c.Hero.Id);

            foreach (BundleDef def in svc.CurrentBundleOffers())
            {
                CharacterSpec card;
                if (svc.TryBuyBundle(def, out card) != BundleResult.Ok || card == null) continue;
                Assert.AreEqual(HeroRolls.IsRollable(card.Id), svc.Data.HeroRolls.ContainsKey(card.Id), "bundle " + card.Id);
            }
            // every owned tier hero has exactly one roll, the counter counts the heroes, and each stored variant is the pure function of (seed, id, counter)
            Assert.AreEqual(svc.Data.HeroRolls.Count, svc.Data.HeroRollCount);
            foreach (KeyValuePair<string, int> kv in svc.Data.HeroRolls)
                Assert.AreEqual(HeroRolls.Pick(12345, kv.Key, svc.Data.HeroRollNo[kv.Key]), kv.Value, kv.Key);
        }

        [Test]
        public void TheRollSurvivesTheSave()
        {
            var store = new MemorySaveStore();
            ProgressService a = NewService(store);
            a.AddCopies("momo_e", 1);
            a.AddCopies("momo_l", 1);
            int e = a.Data.HeroRolls["momo_e"], l = a.Data.HeroRolls["momo_l"];
            var b = new ProgressService(repo, store, 5);
            Assert.AreEqual(e, b.Data.HeroRolls["momo_e"]);
            Assert.AreEqual(l, b.Data.HeroRolls["momo_l"]);
            Assert.AreEqual(2, b.Data.HeroRollCount);
            Assert.AreEqual(e, b.HeroVariant("momo_e"));
        }

        [Test]
        public void OldSavesWithoutRollFieldsLoadAndRollAtFirstUse()
        {
            ProgressService a = NewService();
            a.AddCopies("forestgirl_r", 2);
            var o = JObject.Parse(JsonConvert.SerializeObject(a.Data));
            o.Remove("heroRolls"); o.Remove("heroRollNo"); o.Remove("heroRollCount");
            var store = new MemorySaveStore();
            store.Save(JsonConvert.DeserializeObject<SaveData>(o.ToString()));
            var b = new ProgressService(repo, store, 5);
            Assert.IsNotNull(b.Data.HeroRolls);
            Assert.AreEqual(0, b.Data.HeroRolls.Count, "nothing stored yet");
            Assert.AreEqual(2, b.Copies("forestgirl_r"));
            int v = b.HeroVariant("forestgirl_r");   // owned without a stored roll: dealt now, deterministically from the account seed
            Assert.AreEqual(HeroRolls.Pick(b.Data.Bundles.Seed, "forestgirl_r", 0), v);
            Assert.AreEqual(v, b.Data.HeroRolls["forestgirl_r"]);
            Assert.AreEqual(v, new ProgressService(repo, store, 5).HeroVariant("forestgirl_r"), "saved");
        }

        [Test]
        public void ANotOwnedHeroShowsTheTableRowAndStoresNothing()
        {
            ProgressService svc = NewService();
            Assert.AreEqual(0, svc.HeroVariant("momo_l"));
            Assert.AreEqual(0, svc.Data.HeroRolls.Count);
            CharacterSpec s = CharacterDefs.Get("momo_l");
            Assert.AreSame(s.Stats, svc.StatsOf(s));
        }

        [Test]
        public void AStoredVariantOutsideThePoolIsDealtAgain()
        {
            ProgressService a = NewService();
            a.AddCopies("momo_u", 1);
            a.Data.HeroRolls["momo_u"] = 99999;
            var store = new MemorySaveStore();
            store.Save(a.Data);
            var b = new ProgressService(repo, store, 5);
            Assert.IsFalse(b.Data.HeroRolls.ContainsKey("momo_u"));
            Assert.IsTrue(HeroRolls.IsValidVariant("momo_u", b.HeroVariant("momo_u")));
        }

        [Test]
        public void StatsOfAreTheRolledNumbersAndStayLegal()
        {
            ProgressService svc = NewService();
            foreach (CharacterSpec c in CharacterInventory.All)
            {
                if (!HeroRolls.IsRollable(c.Id)) continue;
                svc.AddCopies(c.Id, 1);
                int v = svc.HeroVariant(c.Id);
                HeroRow row = HeroRolls.Row(c.Id, v);
                CharacterStats s = svc.StatsOf(c);
                Assert.AreEqual(row.LaunchTenths, s.LaunchTenths, c.Id);
                Assert.AreEqual(row.Speed, s.MaxSpeed, c.Id);
                Assert.AreEqual(row.Control, s.Control, c.Id);
                Assert.AreEqual(row.Wall, s.WallBoostHundredths, c.Id);
                Assert.AreEqual(c.Stats.RunBonusCoins, s.RunBonusCoins, c.Id + " run bonus stays the hero's own");
                Assert.AreEqual(c.Stats.RunBonusGems, s.RunBonusGems, c.Id);
                Assert.IsNull(s.ValidateTier(), c.Id);
                Assert.IsNull(HeroIndex.Validate(row.Tier, new HeroPhysics(s.LaunchTenths, s.MaxSpeed, s.Control, s.WallBoostHundredths)), c.Id);
                ShownStats shown = svc.ShownOf(c);
                Assert.IsNotNull(shown, c.Id);
                Assert.AreEqual(ShownStats.From(s, new SimConfig()).Jump, shown.Jump, 1e-9, c.Id);
            }
        }

        [Test]
        public void TwoAccountsGetSlightlyDifferentCopiesOfTheSameHero()
        {
            if (HeroRolls.PoolSize("momo_u") < 2) Assert.Inconclusive("no variants generated yet");
            var seen = new HashSet<int>();
            for (int seed = 1; seed <= 200; seed++)
            {
                ProgressService svc = NewService(null, seed);
                svc.AddCopies("momo_u", 1);
                seen.Add(svc.Data.HeroRolls["momo_u"]);
            }
            Assert.Greater(seen.Count, HeroRolls.PoolSize("momo_u") / 2, "accounts spread over the pool");
        }
    }
}
