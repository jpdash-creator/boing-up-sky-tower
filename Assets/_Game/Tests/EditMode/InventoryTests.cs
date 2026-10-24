using System.Collections.Generic;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace BoingUpSkyTower.Tests
{
    /// <summary>Gear, characters, chests, VIP and the version 2 save (docs/15 sections 4 to 8).</summary>
    public class InventoryTests
    {
        ContentRepository repo;
        MemorySaveStore store;

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            store = new MemorySaveStore();
            ProgressService.TestUnlockAll = false;
        }

        ProgressService NewService() { return new ProgressService(repo, store); }

        // ---- save v2 ----

        [Test]
        public void VersionOneSaveLoadsWithSafeDefaults()
        {
            SaveData old = JsonConvert.DeserializeObject<SaveData>("{\"version\":1,\"coins\":50,\"gems\":3,\"levels\":{},\"unlockedRegions\":[],\"inventory\":{\"powerups\":{\"pw_flight\":2}}}");
            old.Migrate();
            Assert.AreEqual(SaveData.CurrentVersion, old.Version);
            Assert.AreEqual(2, SaveData.CurrentVersion);
            Assert.AreEqual(50, old.Coins);
            Assert.AreEqual(2, old.Inventory.Powerups["pw_flight"]);
            Assert.AreEqual(0, old.Inventory.Gear.Count);
            Assert.AreEqual(0, old.Inventory.Chests.Count);
            Assert.AreEqual("char_momo", old.Characters.Selected);
            Assert.AreEqual(0, old.Vip.Tier);
        }

        [Test]
        public void EveryChangeIsSavedAtOnce()
        {
            ProgressService p = NewService();
            p.AddCoins(5000);
            Assert.AreEqual(StoreBuyResult.Ok, p.TryBuyGear("shirt_plain_tee"));
            Assert.IsTrue(p.EquipGear("char_momo", "shirt_plain_tee"));
            Assert.AreEqual(1, p.GrantChest("coin_common", 1));
            ProgressService again = NewService();
            Assert.IsTrue(again.OwnsGear("shirt_plain_tee"));
            Assert.AreEqual("shirt_plain_tee", again.EquippedGear("char_momo", GearSlot.Shirt));
            Assert.AreEqual(1, again.ChestCount("coin_common"));
            Assert.AreEqual(5000 - 1200, again.Coins);
        }

        [Test]
        public void FileStoreMigratesAnOldFile()
        {
            string path = System.IO.Path.Combine(Application.temporaryCachePath, "inv_old_save.json");
            System.IO.File.WriteAllText(path, "{\"version\":1,\"coins\":7,\"gems\":1,\"levels\":{},\"unlockedRegions\":[]}");
            SaveData d = new FileSaveStore(path).Load();
            Assert.AreEqual(2, d.Version);
            Assert.AreEqual(7, d.Coins);
            Assert.IsNotNull(d.Characters);
            System.IO.File.Delete(path);
        }

        // ---- gear ----

        [Test]
        public void GearCatalogHas32PiecesWithUniqueIds()
        {
            GearCatalog c = GearCatalog.BuiltIn();
            Assert.AreEqual(32, c.All.Count);
            var ids = new HashSet<string>();
            foreach (GearDef g in c.All) Assert.IsTrue(ids.Add(g.Id), g.Id);
            for (int s = 0; s < 4; s++) Assert.AreEqual(8, c.InSlot((GearSlot)s).Count);
            for (int r = 0; r < 4; r++) Assert.AreEqual(8, c.OfRarity((Rarity)r).Count);
        }

        [Test]
        public void BuyingGearIsAtomicAndLegendaryIsNotSold()
        {
            ProgressService p = NewService();
            Assert.AreEqual(StoreBuyResult.NotEnoughCoins, p.TryBuyGear("shirt_plain_tee"));
            Assert.AreEqual(0, p.Coins);
            p.AddCoins(1300);
            Assert.AreEqual(StoreBuyResult.Ok, p.TryBuyGear("shirt_plain_tee"));
            Assert.AreEqual(100, p.Coins);
            Assert.AreEqual(StoreBuyResult.AlreadyOwned, p.TryBuyGear("shirt_plain_tee"));
            Assert.AreEqual(StoreBuyResult.NotForSale, p.TryBuyGear("hat_sun_crown"));
            Assert.AreEqual(StoreBuyResult.Invalid, p.TryBuyGear("hat_nope"));
        }

        [Test]
        public void OneEquippedPiecePerSlotAndPerCharacter()
        {
            ProgressService p = NewService();
            p.AddCoins(10000);
            p.TryBuyGear("hat_straw_hat");
            p.TryBuyGear("hat_knit_beanie");
            Assert.IsFalse(p.EquipGear("char_momo", "hat_sailor_cap"), "not owned");
            Assert.IsTrue(p.EquipGear("char_momo", "hat_straw_hat"));
            Assert.IsTrue(p.EquipGear("char_momo", "hat_knit_beanie"));
            Assert.AreEqual("hat_knit_beanie", p.EquippedGear("char_momo", GearSlot.Hat));
            Assert.IsNull(p.EquippedGear("char_pip", GearSlot.Hat), "each character has its own wardrobe");
            Assert.IsTrue(p.UnequipGear("char_momo", GearSlot.Hat));
            Assert.IsNull(p.EquippedGear("char_momo", GearSlot.Hat));
        }

        [Test]
        public void GearPlaceholderSpritesBuildForEveryPiece()
        {
            foreach (GearDef g in GearCatalog.BuiltIn().All)
            {
                Sprite s = GearArt.PieceSprite(g);
                Assert.IsNotNull(s, g.Id);
                GearArt.RigRect r = GearArt.Rig(g.Slot);
                // pixel aspect equals the rig rectangle on Momo's pixels: drawing it into the rig never stretches it
                float want = (r.W * 1365f) / (r.H * 1649f);
                Assert.AreEqual(want, s.rect.width / s.rect.height, want * 0.03f, g.Id);
            }
        }

        // ---- heroes ----

        [Test]
        public void TwentyHeroesFourPerRegionOneFree()
        {
            HeroCatalog c = HeroCatalog.BuiltIn();
            Assert.AreEqual(20, c.All.Count);
            for (int r = 1; r <= 5; r++)
            {
                List<HeroDef> four = c.OfRegion(r);
                Assert.AreEqual(4, four.Count, "region " + r);
                int free = 0;
                foreach (HeroDef h in four) { if (h.Free) free++; Assert.LessOrEqual(h.JumpPct, 6); Assert.LessOrEqual(h.GemFindPct, 5); Assert.LessOrEqual(h.CoinFindPct, 5); }
                Assert.AreEqual(1, free, "region " + r);
            }
        }

        [Test]
        public void MomoIsOwnedAtTheStartAndOtherRegionsAreLocked()
        {
            ProgressService p = NewService();
            Assert.AreEqual("char_momo", p.SelectedHeroId);
            Assert.AreEqual(HeroState.Owned, p.StateOf(p.HeroDefs.Get("char_momo")));
            Assert.AreEqual(HeroState.ForSale, p.StateOf(p.HeroDefs.Get("char_pip")));
            Assert.AreEqual(HeroState.RegionLocked, p.StateOf(p.HeroDefs.Get("char_spora")));
            Assert.AreEqual(StoreBuyResult.Locked, p.TryBuyHero("char_truffle"));
        }

        [Test]
        public void BuyingAndSelectingAHero()
        {
            ProgressService p = NewService();
            Assert.AreEqual(StoreBuyResult.NotEnoughCoins, p.TryBuyHero("char_pip"));
            Assert.IsFalse(p.SelectHero("char_pip"), "not owned");
            p.AddCoins(4100);
            Assert.AreEqual(StoreBuyResult.Ok, p.TryBuyHero("char_pip"));
            Assert.AreEqual(100, p.Coins);
            Assert.AreEqual(StoreBuyResult.AlreadyOwned, p.TryBuyHero("char_pip"));
            Assert.IsTrue(p.SelectHero("char_pip"));
            Assert.AreEqual("char_pip", NewService().SelectedHeroId);
            // a Legendary costs gems
            Assert.AreEqual(StoreBuyResult.NotEnoughGems, p.TryBuyHero("char_petal"));
            p.AddGems(250);
            Assert.AreEqual(StoreBuyResult.Ok, p.TryBuyHero("char_petal"));
            Assert.AreEqual(0, p.Gems);
        }

        [Test]
        public void FreeCharacterOfARegionOpensWithTheRegion()
        {
            ProgressService p = NewService();
            RegionDef r2 = repo.GetRegionByIndex(2);
            p.AddGems(1000);
            Assert.AreEqual(SkipResult.Ok, p.TrySkipToRegion(r2));
            Assert.AreEqual(HeroState.Owned, p.StateOf(p.HeroDefs.Get("char_spora")));
            Assert.AreEqual(HeroState.ForSale, p.StateOf(p.HeroDefs.Get("char_truffle")));
        }

        // ---- chests ----

        [Test]
        public void ChestOddsSumTo100AndEachChestHasOneCurrency()
        {
            ChestCatalog c = ChestCatalog.BuiltIn();
            Assert.AreEqual(12, c.All.Count);
            foreach (ChestDef d in c.All)
            {
                int sum = 0;
                foreach (int o in d.Odds) sum += o;
                Assert.AreEqual(100, sum, d.Id);
                Assert.AreNotEqual(d.CoinPrice > 0, d.GemPrice > 0, d.Id + " sells for exactly one currency");
            }
        }

        [Test]
        public void ShownOddsAreTheRollerTable()
        {
            ChestDef d = ChestCatalog.BuiltIn().Get("gear_rare");
            Assert.AreEqual("Common 45%, Rare 40%, Epic 14%, Legendary 1%", ChestCatalog.OddsText(d));
            // the roller draws from d.Odds: a big sample lands within 2 points of the shown numbers
            var rng = new ChestRng(99);
            var count = new int[4];
            const int n = 40000;
            for (int i = 0; i < n; i++) count[(int)ChestCatalog.RollRarity(d.Odds, Rarity.Common, rng)]++;
            for (int r = 0; r < 4; r++) Assert.AreEqual(d.Odds[r], 100.0 * count[r] / n, 2.0, "rarity " + r);
        }

        [Test]
        public void PityGuaranteesRareEpicAndLegendaryWithinTheLimits()
        {
            foreach (ChestKind kind in new[] { ChestKind.Coin, ChestKind.Gear, ChestKind.Powerup })
            {
                ChestDef def = ChestCatalog.BuiltIn().Get(kind == ChestKind.Coin ? "coin_common" : (kind == ChestKind.Gear ? "gear_common" : "powerup_common"));
                var pity = new Dictionary<string, int>();
                var rng = new ChestRng(2024);
                int sr = 0, se = 0, sl = 0, mr = 0, me = 0, ml = 0;
                for (int i = 0; i < 100000; i++)
                {
                    ChestResult r = ChestCatalog.Roll(def, pity, null, GearCatalog.BuiltIn(), rng);
                    sr = r.Rarity >= Rarity.Rare ? 0 : sr + 1;
                    se = r.Rarity >= Rarity.Epic ? 0 : se + 1;
                    sl = r.Rarity >= Rarity.Legendary ? 0 : sl + 1;
                    if (sr > mr) mr = sr;
                    if (se > me) me = se;
                    if (sl > ml) ml = sl;
                }
                Assert.LessOrEqual(mr, 9, kind + " rare");
                Assert.LessOrEqual(me, 29, kind + " epic");
                if (kind == ChestKind.Gear) Assert.LessOrEqual(ml, 89, "gear legendary");
            }
        }

        [Test]
        public void BuyAndOpenAChestPaysIntoTheWardrobeAndTheWallet()
        {
            ProgressService p = NewService();
            p.AddCoins(5000);
            p.AddGems(1000);
            Assert.AreEqual(StoreBuyResult.Ok, p.TryBuyChest("gear_common"));
            Assert.AreEqual(5000 - 1500, p.Coins);
            Assert.AreEqual(1, p.ChestCount("gear_common"));
            ChestResult r = p.TryOpenChest("gear_common", 5);
            Assert.IsNotNull(r);
            Assert.AreEqual(0, p.ChestCount("gear_common"));
            Assert.IsTrue(p.OwnsGear(r.GearId));
            Assert.IsNull(p.TryOpenChest("gear_common", 5), "none left");
            // a coin chest pays coins in its range
            p.TryBuyChest("coin_common");
            int before = p.Coins;
            ChestResult c = p.TryOpenChest("coin_common", 77);
            Assert.GreaterOrEqual(c.Coins, 400);
            Assert.LessOrEqual(c.Coins, 900);
            Assert.AreEqual(before + c.Coins, p.Coins);
            // a power-up chest fills the stock
            p.TryBuyChest("powerup_common");
            ChestResult pw = p.TryOpenChest("powerup_common", 11);
            int units = 0;
            foreach (KeyValuePair<string, int> kv in pw.PowerUps) { Assert.AreEqual(kv.Value, p.PowerUpCount(kv.Key)); units += kv.Value; }
            Assert.That(units, Is.InRange(2, 3));
        }

        [Test]
        public void DuplicateGearPaysShardsAndAnAllOwnedRarityPaysCoins()
        {
            ProgressService p = NewService();
            foreach (GearDef g in p.GearDefs.OfRarity(Rarity.Common)) p.GrantGear(g.Id);
            p.GrantChest("gear_common", 5);
            int shardsBefore = p.Data.Inventory.Shards;
            int coinsBefore = p.Coins;
            for (int i = 0; i < 40; i++)
            {
                ChestResult r = p.TryOpenChest("gear_common", (ulong)(1000 + i));
                if (r == null) break;
                if (r.Rarity == Rarity.Common)
                {
                    Assert.IsTrue(r.Duplicate);
                    Assert.AreEqual(0, r.Shards, "everything common is owned: coins instead of shards");
                    Assert.AreEqual(50, r.Coins);
                }
            }
            Assert.GreaterOrEqual(p.Coins, coinsBefore);
            Assert.GreaterOrEqual(p.Data.Inventory.Shards, shardsBefore);
        }

        [Test]
        public void StyleUpgradeNeeds100ShardsAndStopsAtLevel3()
        {
            ProgressService p = NewService();
            p.GrantGear("hat_straw_hat");
            Assert.IsFalse(p.TryUpgradeStyle("hat_straw_hat"));
            p.Data.Inventory.Shards = 350;
            Assert.IsTrue(p.TryUpgradeStyle("hat_straw_hat"));
            Assert.IsTrue(p.TryUpgradeStyle("hat_straw_hat"));
            Assert.IsTrue(p.TryUpgradeStyle("hat_straw_hat"));
            Assert.IsFalse(p.TryUpgradeStyle("hat_straw_hat"), "level 3 is the top");
            Assert.AreEqual(3, p.GearStyleLevel("hat_straw_hat"));
            Assert.AreEqual(50, p.Data.Inventory.Shards);
        }

        // ---- VIP ----

        [Test]
        public void VipTableHasSixRisingTiersInsideTheCaps()
        {
            Assert.AreEqual(6, VipCatalog.All.Count);
            for (int i = 0; i < 6; i++)
            {
                VipTierDef t = VipCatalog.All[i];
                Assert.AreEqual(i + 1, t.Tier);
                Assert.LessOrEqual(t.JumpPct, VipCatalog.JumpCapPct);
                Assert.LessOrEqual(t.GemPct, VipCatalog.GemCapPct);
                Assert.LessOrEqual(t.CoinPct, VipCatalog.CoinCapPct);
                if (i > 0)
                {
                    VipTierDef prev = VipCatalog.All[i - 1];
                    Assert.Greater(t.MonthlyUsd, prev.MonthlyUsd);
                    Assert.Greater(t.JumpPct, prev.JumpPct);
                    Assert.GreaterOrEqual(t.CoinPct, prev.CoinPct);
                }
            }
            // VIP 6 plus the best character stays under the permanent jump cap
            Assert.LessOrEqual(VipCatalog.All[5].JumpPct + 6, VipCatalog.JumpCapPct);
        }

        [Test]
        public void VipTierIsStoredAndAppliesNoBonus()
        {
            ProgressService p = NewService();
            Assert.AreEqual(0, p.VipTier);
            Assert.IsTrue(p.TestSetVipTier(3));
            Assert.AreEqual(3, NewService().VipTier);
            Assert.IsFalse(p.TestSetVipTier(7));
            // nothing in the wallet or the stock changed: the tier is a label until billing and the bonus pipeline exist
            Assert.AreEqual(0, p.Coins);
            Assert.AreEqual(0, p.Gems);
        }
    }
}
