using System.Collections.Generic;
using System.Linq;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// Shop Heroes section (Bug List 4 E2): the price table, the random pick from open regions only, no charge when a tier has no hero, exactly one copy, repeatable picks,
    /// the not-enough-coins and failure paths (nothing changes) and the save. Plus the tier plate colours stay readable (CUX-5).
    /// </summary>
    public class HeroPurchaseTests
    {
        ContentRepository repo;
        MemorySaveStore store;

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            store = new MemorySaveStore();
        }

        ProgressService NewService(int coins = 100000, int seed = 12345)
        {
            var svc = new ProgressService(repo, store, 5);
            svc.Data.Coins = coins;
            svc.Data.Gems = 777;   // heroes never touch gems
            svc.Data.Bundles = new BundleSaveData { Seed = seed };
            return svc;
        }

        static string Snapshot(ProgressService svc) { return JsonConvert.SerializeObject(svc.Data); }

        static CharacterSpec Base(string id, int region) { return new CharacterSpec { Id = id, Name = id, Rarity = CharacterRarity.Common, Region = region }; }
        static CharacterSpec Tier(string id, string line, CharacterRarity r) { return new CharacterSpec { Id = id, Name = id, Rarity = r, Line = line, IsMergeTier = true }; }

        // ---- the one price table ----

        [Test]
        public void ThePriceTableIsTheOwnerTable()
        {
            // coins only (owner Bug List Shop 2a); prices of 2026-11-13: Common 2,000 (now sold), Uncommon 5,000, Rare 12,000, Elite 32,000, Legendary 80,000
            Assert.AreEqual(2000, HeroPurchase.CoinPrice(CharacterRarity.Common));
            Assert.AreEqual(5000, HeroPurchase.CoinPrice(CharacterRarity.Uncommon));
            Assert.AreEqual(12000, HeroPurchase.CoinPrice(CharacterRarity.Rare));
            Assert.AreEqual(32000, HeroPurchase.CoinPrice(CharacterRarity.Epic));
            Assert.AreEqual(80000, HeroPurchase.CoinPrice(CharacterRarity.Legendary));
            Assert.AreEqual(HeroPurchase.Tiers.Length, HeroPurchase.Prices.Length);
        }

        [Test]
        public void TheCardsRunCommonToLegendaryAndEpicIsCalledElite()
        {
            CollectionAssert.AreEqual(new[] { CharacterRarity.Common, CharacterRarity.Uncommon, CharacterRarity.Rare, CharacterRarity.Epic, CharacterRarity.Legendary }, HeroPurchase.Tiers);
            CollectionAssert.AreEqual(new[] { "Common", "Uncommon", "Rare", "Elite", "Legendary" }, HeroPurchase.Tiers.Select(HeroPurchase.RarityName).ToArray());
        }

        [Test]
        public void TheInfoLinesComeFromTheTierRules()
        {
            foreach (CharacterRarity r in HeroPurchase.Tiers)
            {
                if (r == CharacterRarity.Common) continue;   // Common has no tier rule (two short lines, checked below)
                TierRule rule = TierRules.RuleFor(r);
                List<string> lines = HeroPurchase.InfoLines(r);
                Assert.AreEqual(3, lines.Count);
                StringAssert.Contains(rule.PowerCount.ToString(), lines[0]);
                StringAssert.Contains(rule.CooldownSeconds + " s", lines[0]);
                StringAssert.Contains(TierRules.JumpBandText(r), lines[1]);
                StringAssert.Contains("Milestone bonus +" + FluffyClimbers.Simulation.CoinPayoutRules.HeroBonusCoins((int)r) + " coins", lines[2]);   // owner 2026-11-13: the hero bonus by rarity replaced the run bonus range
                StringAssert.Contains(FluffyClimbers.Simulation.CoinPayoutRules.HeroBonusGemsByRarity[(int)r] + " gems", lines[2]);
            }
            Assert.AreEqual(2, HeroPurchase.InfoLines(CharacterRarity.Common).Count);
        }

        // ---- the pick ----

        [Test]
        public void ThePickIsOnlyOfTheRightRarityAndOnlyFromOpenRegions()
        {
            var all = new List<CharacterSpec>
            {
                Base("b1", 1), Base("b2", 2), Base("b3", 3),
                Tier("r1", "b1", CharacterRarity.Rare), Tier("r2", "b2", CharacterRarity.Rare), Tier("r3", "b3", CharacterRarity.Rare),
                Tier("u2", "b2", CharacterRarity.Uncommon), Tier("e2", "b2", CharacterRarity.Epic),
            };
            System.Func<int, bool> only2 = region => region == 2;   // region 1 is always open
            var seen = new HashSet<string>();
            for (ulong roll = 0; roll < 200; roll++)
            {
                CharacterSpec c = HeroPurchase.Pick(CharacterRarity.Rare, all, only2, roll);
                Assert.IsNotNull(c);
                Assert.AreEqual(CharacterRarity.Rare, c.Rarity);
                seen.Add(c.Id);
            }
            CollectionAssert.AreEquivalent(new[] { "r1", "r2" }, seen, "region 3 stays closed, both open Rare heroes can come");
            Assert.IsNull(HeroPurchase.Pick(CharacterRarity.Legendary, all, only2, 5), "no hero of that rarity: null");
            Assert.AreEqual("e2", HeroPurchase.Pick(CharacterRarity.Epic, all, only2, 99).Id);
        }

        [Test]
        public void TheRollIsRepeatableAndDiffersBetweenBuysSeedsAndTiers()
        {
            Assert.AreEqual(HeroPurchase.Roll(7, 3, CharacterRarity.Rare), HeroPurchase.Roll(7, 3, CharacterRarity.Rare));
            Assert.AreNotEqual(HeroPurchase.Roll(7, 3, CharacterRarity.Rare), HeroPurchase.Roll(7, 4, CharacterRarity.Rare));
            Assert.AreNotEqual(HeroPurchase.Roll(7, 3, CharacterRarity.Rare), HeroPurchase.Roll(8, 3, CharacterRarity.Rare));
            Assert.AreNotEqual(HeroPurchase.Roll(7, 3, CharacterRarity.Rare), HeroPurchase.Roll(7, 3, CharacterRarity.Epic));
        }

        // ---- buying ----

        [Test]
        public void BuyingPaysTheCoinsAndAddsExactlyOneCopyOfAnOpenUncommonHero()
        {
            ProgressService svc = NewService(10000);
            int before = svc.Copies("momo_u");
            CharacterSpec hero;
            Assert.AreEqual(HeroBuyResult.Ok, svc.TryBuyHero(CharacterRarity.Uncommon, out hero));
            CollectionAssert.Contains(new[] { "momo_u" }, hero.Id, "only region 1 is open: Momo is its only active line, never an inactive hero");
            Assert.AreEqual(10000 - 5000, svc.Coins);
            Assert.AreEqual(777, svc.Gems, "no gem option: gems untouched");
            Assert.AreEqual(before + 1, svc.Copies("momo_u"), "exactly one copy");
            Assert.AreEqual(1, svc.Data.Bundles.HeroBuys);
        }

        [Test]
        public void AllPricesAreChargedForTheirTier()
        {
            foreach (CharacterRarity r in HeroPurchase.Tiers)
            {
                ProgressService svc = NewService(100000);
                if (svc.HeroBuyPool(r).Count == 0) continue;
                CharacterSpec hero;
                Assert.AreEqual(HeroBuyResult.Ok, svc.TryBuyHero(r, out hero));
                Assert.AreEqual(100000 - HeroPurchase.CoinPrice(r), svc.Coins);
                Assert.AreEqual(777, svc.Gems);
            }
        }

        [Test]
        public void TheStateIsSavedImmediately()
        {
            ProgressService svc = NewService(10000);
            CharacterSpec hero;
            svc.TryBuyHero(CharacterRarity.Uncommon, out hero);
            SaveData saved = store.Load();
            Assert.AreEqual(5000, saved.Coins);
            Assert.AreEqual(1, saved.Bundles.HeroBuys);
            int copies;
            Assert.IsTrue(saved.Copies.TryGetValue(hero.Id, out copies));
            Assert.GreaterOrEqual(copies, 1);
        }

        [Test]
        public void OnlyHeroesOfOpenRegionsCanBeBought()
        {
            ProgressService svc = NewService(1000000);
            Assert.AreEqual(1, svc.HeroBuyPool(CharacterRarity.Uncommon).Count, "Region 1: Momo only (the other lines are inactive)");
            foreach (CharacterRarity r in new[] { CharacterRarity.Rare, CharacterRarity.Epic, CharacterRarity.Legendary })
                Assert.AreEqual(1, svc.HeroBuyPool(r).Count, r + ": one active hero in Region 1, none from the closed regions");
            svc.Data.UnlockedRegions.Add(repo.GetRegionByIndex(2).Id);
            svc.Data.UnlockedRegions.Add(repo.GetRegionByIndex(3).Id);
            var allowed = new HashSet<string>(new[] { "momo_u", "forestgirl_u", "beachotter_u" });
            Assert.IsTrue(allowed.SetEquals(svc.HeroBuyPool(CharacterRarity.Uncommon).Select(c => c.Id)));
            var got = new HashSet<string>();
            for (int i = 0; i < 40; i++)
            {
                CharacterSpec hero;
                Assert.AreEqual(HeroBuyResult.Ok, svc.TryBuyHero(CharacterRarity.Uncommon, out hero));
                Assert.IsTrue(allowed.Contains(hero.Id), hero.Id + " is from a closed region");
                got.Add(hero.Id);
            }
            Assert.Greater(got.Count, 1, "40 buys show more than one of the eleven open heroes");
        }

        [Test]
        public void ARarityWithNoOpenHeroChargesNothingAndChangesNothing()
        {
            ProgressService svc = NewService(100000);
            foreach (CharacterRarity r in HeroPurchase.Tiers)
            {
                if (svc.HeroBuyPool(r).Count != 0) continue;   // none today: Region 1 has heroes of every tier (a future empty tier must still charge nothing)
                string before = Snapshot(svc);
                CharacterSpec hero;
                Assert.AreEqual(HeroBuyResult.NoHeroes, svc.TryBuyHero(r, out hero));
                Assert.IsNull(hero);
                Assert.AreEqual(100000, svc.Coins);
                Assert.AreEqual(before, Snapshot(svc), "not a field of the save moved");
            }
        }

        [Test]
        public void TooFewCoinsIsRefusedWithNothingChanged()
        {
            ProgressService svc = NewService(4999);
            string before = Snapshot(svc);
            CharacterSpec hero;
            Assert.AreEqual(HeroBuyResult.NotEnoughCoins, svc.TryBuyHero(CharacterRarity.Uncommon, out hero));
            Assert.IsNull(hero);
            Assert.AreEqual(4999, svc.Coins);
            Assert.AreEqual(777, svc.Gems, "gems cannot pay for a hero");
            Assert.AreEqual(before, Snapshot(svc));
        }

        [Test]
        public void ARarityThatIsNotSoldFailsWithNothingChanged()
        {
            ProgressService svc = NewService(100000);
            string before = Snapshot(svc);
            CharacterSpec hero;
            Assert.AreEqual(HeroBuyResult.Invalid, svc.TryBuyHero((CharacterRarity)99, out hero));
            Assert.AreEqual(before, Snapshot(svc));
        }

        [Test]
        public void ACommonCardCostsTwoThousandCoinsAndGivesAnOpenRegionBaseHero()
        {
            ProgressService svc = NewService(10000);
            CharacterSpec hero;
            Assert.AreEqual(HeroBuyResult.Ok, svc.TryBuyHero(CharacterRarity.Common, out hero));
            Assert.AreEqual(8000, svc.Coins);
            Assert.AreEqual("momo", hero.Id, "only region 1 is open: Momo is the one Common hero");
            Assert.AreEqual(CharacterRarity.Common, hero.Rarity);
            svc.Data.Coins = 1999;
            Assert.AreEqual(HeroBuyResult.NotEnoughCoins, svc.TryBuyHero(CharacterRarity.Common, out hero));
        }

        [Test]
        public void PicksAreRepeatablePerSeedAndDifferByAccount()
        {
            List<string> Sequence(int seed)
            {
                ProgressService svc = new ProgressService(repo, new MemorySaveStore(), 5);
                svc.Data.Coins = 100000000;
                svc.Data.Bundles = new BundleSaveData { Seed = seed };
                svc.Data.UnlockedRegions.Add(repo.GetRegionByIndex(2).Id);
                svc.Data.UnlockedRegions.Add(repo.GetRegionByIndex(3).Id);
                var ids = new List<string>();
                for (int i = 0; i < 25; i++) { CharacterSpec c; svc.TryBuyHero(CharacterRarity.Uncommon, out c); ids.Add(c.Id); }
                return ids;
            }
            CollectionAssert.AreEqual(Sequence(777), Sequence(777), "same seed, same picks");
            CollectionAssert.AreNotEqual(Sequence(777), Sequence(778), "another account draws another sequence");
        }

        [Test]
        public void TheBuyCounterSurvivesASaveRoundTrip()
        {
            ProgressService a = NewService(10000);
            CharacterSpec hero;
            a.TryBuyHero(CharacterRarity.Uncommon, out hero);
            a.TryBuyHero(CharacterRarity.Uncommon, out hero);
            Assert.AreEqual(2, store.Load().Bundles.HeroBuys);
            Assert.AreEqual(12345, store.Load().Bundles.Seed);
        }

        // ---- CUX-5: the tier plates stay readable ----

        [Category("CriticalUX")]
        [Test]
        public void CUX_TierPlateTextIsReadable()
        {
            foreach (CharacterRarity r in HeroPurchase.Tiers)
            {
                Color plate = ShopScreen.HeroPlateColor(r);
                Assert.GreaterOrEqual(UiKit.Contrast(BundlesScreen.RarityInk(r), plate), 4.5f, r + " name on its plate");
                Assert.GreaterOrEqual(UiKit.Contrast(UiKit.PanelInk, plate), 4.5f, r + " ink on its plate");
            }
            var plates = HeroPurchase.Tiers.Select(r => ColorUtility.ToHtmlStringRGB(ShopScreen.HeroPlateColor(r))).Distinct().Count();
            Assert.AreEqual(5, plates, "every tier has its own plate colour");
        }
    }
}
