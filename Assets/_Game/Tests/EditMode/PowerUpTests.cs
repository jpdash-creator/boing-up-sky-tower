using System.Collections.Generic;
using System.IO;
using System.Linq;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using BoingUpSkyTower.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace BoingUpSkyTower.Tests
{
    /// <summary>Power-ups economy (docs/15 FR-PWR-1, 2, 9, 10): catalog, owned stock, atomic buying, loadout validation, save compatibility.</summary>
    public class PowerUpEconomyTests
    {
        static string ProjectRoot { get { return Path.GetDirectoryName(Application.dataPath); } }

        ContentRepository repo;
        MemorySaveStore store;

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            store = new MemorySaveStore();
        }

        ProgressService NewService() { return new ProgressService(repo, store); }

        // ---- the table ----

        [Test]
        public void BuiltInCatalogMatchesTheJsonFile()
        {
            string json = File.ReadAllText(Path.Combine(ProjectRoot, "Content", "economy", "powerups.json"));
            PowerUpCatalog fromJson = PowerUpCatalog.FromJson(json);
            PowerUpCatalog builtIn = PowerUpCatalog.BuiltIn();
            Assert.AreEqual(11, fromJson.All.Count);
            Assert.AreEqual(fromJson.All.Count, builtIn.All.Count);
            for (int i = 0; i < builtIn.All.Count; i++)
            {
                PowerUpDef a = builtIn.All[i], b = fromJson.All[i];
                Assert.AreEqual(a.Id, b.Id);
                Assert.AreEqual(a.Type, b.Type);
                Assert.AreEqual(a.Name, b.Name, a.Id);
                Assert.AreEqual(a.Description, b.Description, a.Id);
                Assert.AreEqual(a.Trigger, b.Trigger, a.Id);
                Assert.AreEqual(a.DurationSec, b.DurationSec, a.Id);
                Assert.AreEqual(a.Uses, b.Uses, a.Id);
                Assert.AreEqual(a.CarryCap, b.CarryCap, a.Id);
                Assert.AreEqual(a.MaxStack, b.MaxStack, a.Id);
                Assert.AreEqual(a.CoinPrice, b.CoinPrice, a.Id);
                Assert.AreEqual(a.GemPrice, b.GemPrice, a.Id);
                Assert.AreEqual(a.UnlockRegion, b.UnlockRegion, a.Id);
                Assert.AreEqual(a.Implemented, b.Implemented, a.Id);
            }
        }

        [Test]
        public void SevenPowerUpsAreSoldAndTheirNumbersMatchTheSimulation()
        {
            PowerUpCatalog c = PowerUpCatalog.BuiltIn();
            Assert.AreEqual(7, c.Implemented.Count);
            SimConfig cfg = new SimConfig();
            foreach (PowerUpDef d in c.Implemented)
            {
                Assert.IsTrue(PowerUps.IsImplemented(d.Type), d.Id);
                Assert.Greater(d.CoinPrice, 0, d.Id);
                Assert.AreEqual(0, d.GemPrice, d.Id + " is bought with coins only");
                Assert.GreaterOrEqual(d.CarryCap, 1, d.Id);
                Assert.AreEqual(PowerUps.IdOf(d.Type), d.Id);
                int ticks = PowerUps.TotalTicks(cfg, d.Type);
                Assert.AreEqual(d.DurationSec * SimConfig.TicksPerSecond, ticks, d.Id + " duration in the catalog and in SimConfig");
            }
            Assert.AreEqual(1500, c.Get(PowerUp.Flight).CoinPrice);
            Assert.AreEqual(1500, c.Get(PowerUp.Rocket).CoinPrice);
            Assert.AreEqual(1500, c.Get(PowerUp.SlowTime).CoinPrice);
            Assert.AreEqual(1000, c.Get(PowerUp.DoubleJump).CoinPrice);
            Assert.AreEqual(1000, c.Get(PowerUp.WallStick).CoinPrice);
            Assert.AreEqual(1000, c.Get(PowerUp.Shield).CoinPrice);
            Assert.AreEqual(1000, c.Get(PowerUp.Magnet).CoinPrice);
            Assert.AreEqual(2, c.Get(PowerUp.DoubleJump).Uses);
            Assert.AreEqual(cfg.DoubleJumpCount, c.Get(PowerUp.DoubleJump).Uses, "units arm the same number of air jumps");
            foreach (PowerUpDef d in c.All.Where(x => !x.Implemented)) Assert.IsFalse(c.Implemented.Contains(d), d.Id);
        }

        [Test]
        public void BundlePricesFollowTheDiscountRules()
        {
            PowerUpDef flight = PowerUpCatalog.BuiltIn().Get("pw_flight");
            Assert.AreEqual(1500, PowerUpCatalog.PriceFor(flight, PowerUpCurrency.Coins, 1));
            Assert.AreEqual(1500 * 4 * 100 / 100, PowerUpCatalog.PriceFor(flight, PowerUpCurrency.Coins, 4));
            Assert.AreEqual(6750, PowerUpCatalog.PriceFor(flight, PowerUpCurrency.Coins, 5));    // -10 percent
            Assert.AreEqual(12000, PowerUpCatalog.PriceFor(flight, PowerUpCurrency.Coins, 10));   // -20 percent
            Assert.AreEqual(0, PowerUpCatalog.PriceFor(flight, PowerUpCurrency.Gems, 5), "no gem price");
            Assert.AreEqual(0, PowerUpCatalog.PriceFor(flight, PowerUpCurrency.Gems, 0));
            Assert.AreEqual(0, PowerUpCatalog.PriceFor(null, PowerUpCurrency.Gems, 1));
        }

        // ---- stock ----

        [Test]
        public void ANewOrOldSaveHasAnEmptyInventoryAndLoadout()
        {
            var svc = NewService();
            Assert.AreEqual(0, svc.PowerUpCount("pw_flight"));
            Assert.AreEqual(3, svc.GetLoadout().Length);
            Assert.IsTrue(svc.GetLoadout().All(x => x == null));

            // a save file from before power-ups has neither key
            var old = JsonConvert.DeserializeObject<SaveData>("{\"version\":1,\"coins\":120,\"gems\":7,\"levels\":{},\"unlockedRegions\":[]}");
            Assert.IsNotNull(old.Inventory);
            Assert.AreEqual(0, old.Inventory.Powerups.Count);
            var nulled = JsonConvert.DeserializeObject<SaveData>("{\"version\":1,\"inventory\":null,\"loadout\":null,\"levels\":{},\"unlockedRegions\":[]}");
            var store2 = new MemorySaveStore();
            store2.Save(nulled);
            var svc2 = new ProgressService(repo, store2);
            Assert.AreEqual(0, svc2.PowerUpCount("pw_rocket"));
            Assert.AreEqual(3, svc2.GetLoadout().Length);
        }

        [Test]
        public void GrantAddsUpToTheStackCapAndSavesAtOnce()
        {
            var svc = NewService();
            Assert.AreEqual(5, svc.GrantPowerUp("pw_flight", 5));
            Assert.AreEqual(5, svc.PowerUpCount("pw_flight"));
            Assert.AreEqual(94, svc.GrantPowerUp("pw_flight", 500));   // only the room that is left
            Assert.AreEqual(PowerUpCatalog.StackCap, svc.PowerUpCount("pw_flight"));
            Assert.AreEqual(0, svc.GrantPowerUp("pw_flight", 1));
            Assert.AreEqual(0, svc.GrantPowerUp("pw_unknown", 1));
            Assert.AreEqual(0, svc.GrantPowerUp("pw_rocket", 0));
            Assert.AreEqual(0, svc.GrantPowerUp("pw_rocket", -3));
            Assert.AreEqual(99, NewService().PowerUpCount("pw_flight"), "saved");
        }

        [Test]
        public void ConsumeTakesUnitsOrChangesNothing()
        {
            var svc = NewService();
            svc.GrantPowerUp("pw_magnet", 3);
            Assert.IsFalse(svc.TryConsumePowerUp("pw_magnet", 4));
            Assert.AreEqual(3, svc.PowerUpCount("pw_magnet"));
            Assert.IsTrue(svc.TryConsumePowerUp("pw_magnet", 2));
            Assert.AreEqual(1, NewService().PowerUpCount("pw_magnet"));
            Assert.IsTrue(svc.TryConsumePowerUp("pw_magnet"));
            Assert.AreEqual(0, svc.PowerUpCount("pw_magnet"));
            Assert.IsFalse(svc.Data.Inventory.Powerups.ContainsKey("pw_magnet"), "zero counts are removed");
            Assert.IsFalse(svc.TryConsumePowerUp("pw_magnet"));
            Assert.IsFalse(svc.TryConsumePowerUp("pw_magnet", 0));
        }

        // ---- buying ----

        [Test]
        public void BuyWithCoinsDebitsAndAddsInOneSave()
        {
            var svc = NewService();
            svc.AddCoins(3000);
            Assert.AreEqual(PowerUpBuyResult.Ok, svc.TryBuyPowerUp("pw_flight", PowerUpCurrency.Coins));
            Assert.AreEqual(1500, svc.Coins);
            Assert.AreEqual(1, svc.PowerUpCount("pw_flight"));
            var again = NewService();
            Assert.AreEqual(1500, again.Coins);
            Assert.AreEqual(1, again.PowerUpCount("pw_flight"));
        }

        [Test]
        public void GemsCannotBuyPowerUps()
        {
            var svc = NewService();
            svc.AddGems(300);
            Assert.AreEqual(PowerUpBuyResult.Invalid, svc.TryBuyPowerUp("pw_doublejump", PowerUpCurrency.Gems, 2));
            Assert.AreEqual(300, svc.Gems);
            Assert.AreEqual(0, svc.PowerUpCount("pw_doublejump"));
        }

        [Test]
        public void BuyFailsWithoutChangingAnything()
        {
            var svc = NewService();
            svc.AddCoins(100);
            svc.AddGems(5);
            Assert.AreEqual(PowerUpBuyResult.NotEnoughCoins, svc.TryBuyPowerUp("pw_flight", PowerUpCurrency.Coins));
            Assert.AreEqual(PowerUpBuyResult.Invalid, svc.TryBuyPowerUp("pw_flight", PowerUpCurrency.Gems));
            Assert.AreEqual(PowerUpBuyResult.Invalid, svc.TryBuyPowerUp("pw_unknown", PowerUpCurrency.Coins));
            Assert.AreEqual(PowerUpBuyResult.Invalid, svc.TryBuyPowerUp("pw_flight", PowerUpCurrency.Coins, 0));
            Assert.AreEqual(PowerUpBuyResult.Invalid, svc.TryBuyPowerUp("pw_springboots", PowerUpCurrency.Coins), "reserved, not implemented");
            Assert.AreEqual(100, svc.Coins);
            Assert.AreEqual(5, svc.Gems);
            Assert.AreEqual(0, svc.PowerUpCount("pw_flight"));
        }

        [Test]
        public void BuyStopsAtTheStackCap()
        {
            var svc = NewService();
            svc.AddCoins(100000);
            svc.GrantPowerUp("pw_wallstick", 99);
            Assert.AreEqual(PowerUpBuyResult.StackFull, svc.TryBuyPowerUp("pw_wallstick", PowerUpCurrency.Coins));
            Assert.AreEqual(100000, svc.Coins);
            svc.TryConsumePowerUp("pw_wallstick", 5);
            Assert.AreEqual(PowerUpBuyResult.StackFull, svc.TryBuyPowerUp("pw_wallstick", PowerUpCurrency.Coins, 6));
            Assert.AreEqual(PowerUpBuyResult.Ok, svc.TryBuyPowerUp("pw_wallstick", PowerUpCurrency.Coins, 5));
            Assert.AreEqual(99, svc.PowerUpCount("pw_wallstick"));
        }

        [Test]
        public void ResetAllClearsTheInventoryAndLoadout()
        {
            var svc = NewService();
            svc.GrantPowerUp("pw_shield", 2);
            svc.SetLoadoutSlot(0, "pw_shield");
            svc.ResetAll();
            Assert.AreEqual(0, svc.PowerUpCount("pw_shield"));
            Assert.IsTrue(svc.GetLoadout().All(x => x == null));
            Assert.IsTrue(svc.SetLoadoutSlot(1, null), "the loadout list is padded again after a reset");
        }

        // ---- loadout ----

        [Test]
        public void LoadoutAcceptsOnlyOwnedImplementedPowerUps()
        {
            var svc = NewService();
            Assert.IsFalse(svc.SetLoadoutSlot(0, "pw_flight"), "not owned");
            svc.GrantPowerUp("pw_flight", 1);
            Assert.IsTrue(svc.SetLoadoutSlot(0, "pw_flight"));
            Assert.IsFalse(svc.SetLoadoutSlot(3, "pw_flight"), "slot 4 does not exist");
            Assert.IsFalse(svc.SetLoadoutSlot(-1, "pw_flight"));
            Assert.IsFalse(svc.SetLoadoutSlot(1, "pw_unknown"));
            svc.Data.Inventory.Powerups["pw_springboots"] = 2;   // reserved id with stock: still not a candidate
            Assert.IsFalse(svc.SetLoadoutSlot(1, "pw_springboots"));
            CollectionAssert.AreEqual(new string[] { "pw_flight", null, null }, svc.GetLoadout());
        }

        [Test]
        public void ALoadoutHoldsAtMostThreeAndOneTypePerSlot()
        {
            var svc = NewService();
            foreach (string id in new[] { "pw_flight", "pw_rocket", "pw_shield", "pw_magnet" }) svc.GrantPowerUp(id, 1);
            Assert.IsTrue(svc.SetLoadout(new List<string> { "pw_flight", "pw_rocket", "pw_shield" }));
            Assert.IsFalse(svc.SetLoadout(new List<string> { "pw_flight", "pw_rocket", "pw_shield", "pw_magnet" }), "a fourth slot");
            Assert.IsFalse(svc.SetLoadout(new List<string> { "pw_flight", "pw_flight" }), "twice the same type");
            CollectionAssert.AreEqual(new[] { "pw_flight", "pw_rocket", "pw_shield" }, svc.GetLoadout(), "a rejected call changes nothing");

            Assert.IsTrue(svc.SetLoadoutSlot(2, "pw_flight"), "an id already in another slot moves");
            CollectionAssert.AreEqual(new[] { null, "pw_rocket", "pw_flight" }, svc.GetLoadout());
            Assert.IsTrue(svc.SetLoadoutSlot(1, ""));
            CollectionAssert.AreEqual(new[] { null, null, "pw_flight" }, svc.GetLoadout());
        }

        [Test]
        public void TheLoadoutIsRememberedAndEmptiedWhenTheStockRunsOut()
        {
            var svc = NewService();
            svc.GrantPowerUp("pw_rocket", 2);
            svc.GrantPowerUp("pw_slowtime", 1);
            svc.SetLoadout(new List<string> { "pw_rocket", "pw_slowtime" });
            CollectionAssert.AreEqual(new[] { "pw_rocket", "pw_slowtime", null }, NewService().GetLoadout(), "saved between sessions");
            svc.TryConsumePowerUp("pw_slowtime");
            CollectionAssert.AreEqual(new[] { "pw_rocket", null, null }, svc.GetLoadout());
            svc.Data.Inventory.Powerups.Remove("pw_rocket");   // a hand-edited save: the stored loadout is read as validated
            CollectionAssert.AreEqual(new string[] { null, null, null }, svc.GetLoadout());
        }

        [Test]
        public void ARunCarriesAtMostTheCarryCap()
        {
            var svc = NewService();
            svc.GrantPowerUp("pw_flight", 5);        // carry cap 1
            svc.GrantPowerUp("pw_doublejump", 2);    // carry cap 3, only 2 owned
            svc.GrantPowerUp("pw_shield", 9);        // carry cap 2
            svc.SetLoadout(new List<string> { "pw_flight", "pw_doublejump", "pw_shield" });
            LoadoutSlot[] slots = svc.BuildRunLoadout();
            Assert.AreEqual(PowerUp.Flight, slots[0].Type); Assert.AreEqual(1, slots[0].Units);
            Assert.AreEqual(PowerUp.DoubleJump, slots[1].Type); Assert.AreEqual(2, slots[1].Units);
            Assert.AreEqual(PowerUp.Shield, slots[2].Type); Assert.AreEqual(2, slots[2].Units);
            Assert.AreEqual(5, svc.PowerUpCount("pw_flight"), "building the run loadout does not take anything from the stock");
        }

        [Test]
        public void AnEmptyLoadoutBuildsEmptySlots()
        {
            LoadoutSlot[] slots = NewService().BuildRunLoadout();
            Assert.AreEqual(3, slots.Length);
            foreach (LoadoutSlot s in slots) { Assert.AreEqual(PowerUp.None, s.Type); Assert.AreEqual(0, s.Units); }
        }

        // ---- the run settles used units ----

        [Test]
        public void UsedUnitsLeaveTheStockWhenTheRunSettlesAndUnusedOnesStay()
        {
            var svc = NewService();
            svc.GrantPowerUp("pw_flight", 3);
            svc.GrantPowerUp("pw_shield", 2);
            svc.SetLoadout(new List<string> { "pw_flight", "pw_shield" });
            var session = new RunSession(repo, new SimConfig(), "lvl_001_01");
            session.ApplyLoadout(svc.BuildRunLoadout());
            Assert.IsTrue(session.HasLoadout);

            session.Step(SimInput.PowerSlot0);   // Flight on
            Assert.IsTrue(PowerUps.IsActive(in session.State, PowerUp.Flight));
            Assert.AreEqual(3, svc.PowerUpCount("pw_flight"), "nothing is saved per tick");
            Assert.AreEqual(1, session.SettlePowerUps(svc));
            Assert.AreEqual(2, svc.PowerUpCount("pw_flight"));
            Assert.AreEqual(2, svc.PowerUpCount("pw_shield"), "the unused shield stays");
            Assert.AreEqual(0, session.SettlePowerUps(svc), "settling twice takes nothing twice");
        }

        [Test]
        public void ARunWithoutALoadoutHasNoPowerUpButtons()
        {
            var session = new RunSession(repo, new SimConfig(), "lvl_001_01");
            session.ApplyLoadout(NewService().BuildRunLoadout());
            Assert.IsFalse(session.HasLoadout);
            session.Step(SimInput.PowerMask);   // pressing nothing-equipped does nothing
            Assert.AreEqual(0, session.State.PwUsed);
        }

        [Test]
        public void RunInputLatchesPowerPressesUntilTheNextTick()
        {
            var input = new RunInputReader();
            input.ScriptedMove = () => 0;
            input.LatchPower(1);
            input.LatchPower(7);   // out of range, ignored
            Assert.AreEqual(SimInput.PowerSlot1, input.ReadTick());
            Assert.AreEqual(0, input.ReadTick(), "consumed");
            input.LatchPower(0);
            input.Clear();
            Assert.AreEqual(0, input.ReadTick(), "Clear drops a pending press");
        }

        [Test]
        public void PowerUpIdsInTheSaveRoundTripThroughTheSimulationIds()
        {
            foreach (PowerUpDef d in PowerUpCatalog.BuiltIn().All) Assert.AreEqual(d.Type, PowerUps.FromId(d.Id), d.Id);
        }
    }
}
