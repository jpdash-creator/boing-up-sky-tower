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
    /// <summary>Power-ups economy (docs/15 FR-PWR-1, 9, 10; reworked 2026-10-31): catalog, owned stock WITHOUT any cap, atomic buying, no loadout, save compatibility.</summary>
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
                Assert.AreEqual(d.CoinPrice / 20, d.GemPrice, d.Id + " gem price = coin price / 20 (1 gem = 20 coins, docs/22 4.3)");
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
            // owner durations of 2026-10-31 (seconds per tap): Magnet 30, Shield 60, Wall Stick 30, Slow Time 30, Double Jump 30, Rocket 4 (20 floors), Flight 10
            Assert.AreEqual(30, c.Get(PowerUp.Magnet).DurationSec);
            Assert.AreEqual(60, c.Get(PowerUp.Shield).DurationSec);
            Assert.AreEqual(30, c.Get(PowerUp.WallStick).DurationSec);
            Assert.AreEqual(30, c.Get(PowerUp.SlowTime).DurationSec);
            Assert.AreEqual(30, c.Get(PowerUp.DoubleJump).DurationSec);
            Assert.AreEqual(4, c.Get(PowerUp.Rocket).DurationSec);
            Assert.AreEqual(10, c.Get(PowerUp.Flight).DurationSec);
            foreach (PowerUpDef d in c.Implemented) Assert.AreEqual(1, d.Uses, d.Id + ": one unit per tap");
            Assert.AreEqual(20, cfg.RocketFloors);
            Assert.AreEqual(50, cfg.SlowTimePercent);
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
            Assert.AreEqual(75, PowerUpCatalog.PriceFor(flight, PowerUpCurrency.Gems, 1));
            Assert.AreEqual(338, PowerUpCatalog.PriceFor(flight, PowerUpCurrency.Gems, 5), "75 x 5 x 90 percent, rounded half up");
            Assert.AreEqual(0, PowerUpCatalog.PriceFor(flight, PowerUpCurrency.Gems, 0));
            Assert.AreEqual(0, PowerUpCatalog.PriceFor(null, PowerUpCurrency.Gems, 1));
        }

        // ---- stock ----

        [Test]
        public void ANewOrOldSaveHasAnEmptyInventory()
        {
            var svc = NewService();
            Assert.AreEqual(0, svc.PowerUpCount("pw_flight"));
            Assert.IsTrue(svc.BuildRunStock().All(x => x == 0));

            // a save file from before power-ups has neither key
            var old = JsonConvert.DeserializeObject<SaveData>("{\"version\":1,\"coins\":120,\"gems\":7,\"levels\":{},\"unlockedRegions\":[]}");
            Assert.IsNotNull(old.Inventory);
            Assert.AreEqual(0, old.Inventory.Powerups.Count);
            var nulled = JsonConvert.DeserializeObject<SaveData>("{\"version\":1,\"inventory\":null,\"loadout\":null,\"levels\":{},\"unlockedRegions\":[]}");
            var store2 = new MemorySaveStore();
            store2.Save(nulled);
            var svc2 = new ProgressService(repo, store2);
            Assert.AreEqual(0, svc2.PowerUpCount("pw_rocket"));
        }

        [Test]
        public void AnOldSaveWithALoadoutAndAFullStackOf99IsKeptAndTheLoadoutIsIgnored()
        {
            var data = JsonConvert.DeserializeObject<SaveData>("{\"version\":1,\"inventory\":{\"powerups\":{\"pw_flight\":99}},\"loadout\":[\"pw_flight\",\"\",\"\"],\"levels\":{},\"unlockedRegions\":[]}");
            var s = new MemorySaveStore();
            s.Save(data);
            var svc = new ProgressService(repo, s);
            Assert.AreEqual(99, svc.PowerUpCount("pw_flight"));
            Assert.AreEqual(100, svc.GrantPowerUp("pw_flight", 100), "no cap: the old limit of 99 is gone");
            Assert.AreEqual(199, svc.PowerUpCount("pw_flight"));
        }

        [Test]
        public void GrantHasNoCap_AndSavesAtOnce()
        {
            var svc = NewService();
            Assert.AreEqual(5, svc.GrantPowerUp("pw_flight", 5));
            Assert.AreEqual(5, svc.PowerUpCount("pw_flight"));
            Assert.AreEqual(500, svc.GrantPowerUp("pw_flight", 500), "no stock cap of 99 any more");
            Assert.AreEqual(505, svc.PowerUpCount("pw_flight"));
            Assert.AreEqual(0, svc.GrantPowerUp("pw_unknown", 1));
            Assert.AreEqual(0, svc.GrantPowerUp("pw_rocket", 0));
            Assert.AreEqual(0, svc.GrantPowerUp("pw_rocket", -3));
            Assert.AreEqual(505, NewService().PowerUpCount("pw_flight"), "saved");
        }

        [Test]
        public void CountsSaturateInsteadOfOverflowing()
        {
            var svc = NewService();
            Assert.AreEqual(int.MaxValue - 10, svc.GrantPowerUp("pw_magnet", int.MaxValue - 10));
            Assert.AreEqual(10, svc.GrantPowerUp("pw_magnet", 50), "only the room that is left below int.MaxValue");
            Assert.AreEqual(int.MaxValue, svc.PowerUpCount("pw_magnet"));
            Assert.AreEqual(0, svc.GrantPowerUp("pw_magnet", 1));
            svc.AddCoins(100000);
            Assert.AreEqual(PowerUpBuyResult.Ok, svc.TryBuyPowerUp("pw_magnet", PowerUpCurrency.Coins));
            Assert.AreEqual(int.MaxValue, svc.PowerUpCount("pw_magnet"), "buying on top of a full int saturates");
            Assert.IsTrue(svc.TryConsumePowerUp("pw_magnet", 5));
            Assert.AreEqual(int.MaxValue - 5, svc.PowerUpCount("pw_magnet"));
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
        public void GemsBuyPowerUpsAtFiftyAndSeventyFiveGems()
        {
            var svc = NewService();
            svc.AddGems(300);
            Assert.AreEqual(PowerUpBuyResult.Ok, svc.TryBuyPowerUp("pw_doublejump", PowerUpCurrency.Gems, 2));   // 2 x 50
            Assert.AreEqual(200, svc.Gems);
            Assert.AreEqual(2, svc.PowerUpCount("pw_doublejump"));
            Assert.AreEqual(PowerUpBuyResult.Ok, svc.TryBuyPowerUp("pw_rocket", PowerUpCurrency.Gems));          // 75
            Assert.AreEqual(125, svc.Gems);
            Assert.AreEqual(PowerUpBuyResult.NotEnoughGems, svc.TryBuyPowerUp("pw_flight", PowerUpCurrency.Gems, 2));
            Assert.AreEqual(125, svc.Gems);
            Assert.AreEqual(0, svc.PowerUpCount("pw_flight"));
        }

        [Test]
        public void BuyFailsWithoutChangingAnything()
        {
            var svc = NewService();
            svc.AddCoins(100);
            svc.AddGems(5);
            Assert.AreEqual(PowerUpBuyResult.NotEnoughCoins, svc.TryBuyPowerUp("pw_flight", PowerUpCurrency.Coins));
            Assert.AreEqual(PowerUpBuyResult.NotEnoughGems, svc.TryBuyPowerUp("pw_flight", PowerUpCurrency.Gems));
            Assert.AreEqual(PowerUpBuyResult.Invalid, svc.TryBuyPowerUp("pw_unknown", PowerUpCurrency.Coins));
            Assert.AreEqual(PowerUpBuyResult.Invalid, svc.TryBuyPowerUp("pw_flight", PowerUpCurrency.Coins, 0));
            Assert.AreEqual(PowerUpBuyResult.Invalid, svc.TryBuyPowerUp("pw_springboots", PowerUpCurrency.Coins), "reserved, not implemented");
            Assert.AreEqual(100, svc.Coins);
            Assert.AreEqual(5, svc.Gems);
            Assert.AreEqual(0, svc.PowerUpCount("pw_flight"));
        }

        [Test]
        public void BuyingHasNoStackLimit()
        {
            var svc = NewService();
            svc.AddCoins(10000000);
            svc.GrantPowerUp("pw_wallstick", 99);
            Assert.AreEqual(PowerUpBuyResult.Ok, svc.TryBuyPowerUp("pw_wallstick", PowerUpCurrency.Coins), "owning 99 does not block a purchase");
            Assert.AreEqual(100, svc.PowerUpCount("pw_wallstick"));
            Assert.AreEqual(PowerUpBuyResult.Ok, svc.TryBuyPowerUp("pw_wallstick", PowerUpCurrency.Coins, 150));
            Assert.AreEqual(250, svc.PowerUpCount("pw_wallstick"));
            Assert.AreEqual(PowerUpBuyResult.Ok, svc.TryBuyPowerUp("pw_wallstick", PowerUpCurrency.Coins, 1000));
            Assert.AreEqual(1250, svc.PowerUpCount("pw_wallstick"));
        }

        [Test]
        public void ResetAllClearsTheInventory()
        {
            var svc = NewService();
            svc.GrantPowerUp("pw_shield", 2);
            svc.ResetAll();
            Assert.AreEqual(0, svc.PowerUpCount("pw_shield"));
        }

        [Test]
        public void ChestsAlwaysGiveTheirPowerUps_EvenOnAFullFormerStackOf99()
        {
            var svc = NewService();
            foreach (PowerUpDef d in svc.PowerUpDefs.Implemented) svc.GrantPowerUp(d.Id, 99);
            svc.Data.Gems = 100000;
            System.Collections.Generic.List<ChestContents> got;
            Assert.AreEqual(ChestOpenResult.Ok, svc.TryOpenChests(ChestKind.Golden, 10, out got));
            int given = 0;
            foreach (ChestContents c in got) given += c.PowerUps.Count;
            Assert.AreEqual(30, given);
            int owned = 0;
            foreach (PowerUpDef d in svc.PowerUpDefs.Implemented) owned += svc.PowerUpCount(d.Id);
            Assert.AreEqual(7 * 99 + 30, owned, "nothing was skipped because a stack was full");
        }

        // ---- no loadout: every owned type is in the run ----

        [Test]
        public void ARunStartsWithEveryOwnedImplementedTypeAndTheFullCount()
        {
            var svc = NewService();
            svc.GrantPowerUp("pw_flight", 250);
            svc.GrantPowerUp("pw_doublejump", 2);
            svc.GrantPowerUp("pw_shield", 9);
            svc.GrantPowerUp("pw_magnet", 1);
            svc.GrantPowerUp("pw_slowtime", 1);
            svc.GrantPowerUp("pw_wallstick", 1);
            svc.GrantPowerUp("pw_rocket", 3);
            svc.Data.Inventory.Powerups["pw_springboots"] = 2;   // reserved id with stock: not in the run
            int[] stock = svc.BuildRunStock();
            Assert.AreEqual(250, stock[(int)PowerUp.Flight], "no carry cap");
            Assert.AreEqual(3, stock[(int)PowerUp.Rocket]);
            Assert.AreEqual(2, stock[(int)PowerUp.DoubleJump]);
            Assert.AreEqual(1, stock[(int)PowerUp.WallStick]);
            Assert.AreEqual(9, stock[(int)PowerUp.Shield]);
            Assert.AreEqual(1, stock[(int)PowerUp.Magnet]);
            Assert.AreEqual(1, stock[(int)PowerUp.SlowTime]);
            Assert.AreEqual(0, stock[(int)PowerUp.SpringBoots]);
            Assert.AreEqual(250, svc.PowerUpCount("pw_flight"), "building the run stock does not take anything");
        }

        [Test]
        public void TheRunAcceptsATapOnlyWhileUnitsAreLeft_AndEveryTapStacks()
        {
            var session = new RunSession(repo, new SimConfig(), "lvl_001_01");
            var stock = new int[PowerUps.MaxType + 1];
            stock[(int)PowerUp.Magnet] = 2;
            stock[(int)PowerUp.Shield] = 1;
            session.ApplyStock(stock);
            Assert.IsTrue(session.OwnedAtStart(PowerUp.Magnet));
            Assert.IsFalse(session.OwnedAtStart(PowerUp.Flight));

            session.Step(PowerUps.InputBit(PowerUp.Flight));   // not owned: dropped
            Assert.AreEqual(0, session.State.PwUsed);
            session.Step(PowerUps.InputBit(PowerUp.Magnet));
            Assert.AreEqual(1799, session.State.MagnetTicks);
            Assert.AreEqual(1, session.PowerStock(PowerUp.Magnet));
            session.Step(PowerUps.InputBit(PowerUp.Magnet));   // stacks: 30 s on top of the remaining time
            Assert.AreEqual(1799 + 1800 - 1, session.State.MagnetTicks);
            Assert.AreEqual(0, session.PowerStock(PowerUp.Magnet));
            session.Step(PowerUps.InputBit(PowerUp.Magnet));   // nothing left: dropped
            Assert.AreEqual(2, session.State.PwUsed);
            Assert.AreEqual(1799 + 1800 - 2, session.State.MagnetTicks);
        }

        [Test]
        public void EveryOwnedTypeIsUsableInOneRun_NoLimitOfThree()
        {
            var session = new RunSession(repo, new SimConfig(), "lvl_001_01");
            var stock = new int[PowerUps.MaxType + 1];
            for (int i = 1; i <= PowerUps.TypeCount; i++) stock[i] = 2;
            session.ApplyStock(stock);
            session.Step(SimInput.PowerMask);
            Assert.AreEqual(6, session.State.PwUsed, "Flight, Double Jump, Wall Stick, Shield, Magnet, Slow Time; the Rocket waits for the Flight");
            Assert.AreEqual(2, session.PowerStock(PowerUp.Rocket), "a refused tap costs nothing");
            Assert.AreEqual(1, session.PowerStock(PowerUp.Flight));
        }

        // ---- the run settles used units ----

        [Test]
        public void UsedUnitsLeaveTheStockWhenTheRunSettlesAndUnusedOnesStay()
        {
            var svc = NewService();
            svc.GrantPowerUp("pw_flight", 3);
            svc.GrantPowerUp("pw_shield", 2);
            var session = new RunSession(repo, new SimConfig(), "lvl_001_01");
            session.ApplyStock(svc.BuildRunStock());
            Assert.IsTrue(session.HasPowerUps);

            session.Step(PowerUps.InputBit(PowerUp.Flight));   // Flight on
            Assert.IsTrue(PowerUps.IsActive(in session.State, PowerUp.Flight));
            Assert.AreEqual(3, svc.PowerUpCount("pw_flight"), "nothing is saved per tick");
            Assert.AreEqual(1, session.SettlePowerUps(svc));
            Assert.AreEqual(2, svc.PowerUpCount("pw_flight"));
            Assert.AreEqual(2, svc.PowerUpCount("pw_shield"), "the unused shield stays");
            Assert.AreEqual(0, session.SettlePowerUps(svc), "settling twice takes nothing twice");
        }

        [Test]
        public void ARunWithoutPowerUpsHasNoButtons()
        {
            var session = new RunSession(repo, new SimConfig(), "lvl_001_01");
            session.ApplyStock(NewService().BuildRunStock());
            Assert.IsFalse(session.HasPowerUps);
            session.Step(SimInput.PowerMask);   // pressing with nothing owned does nothing
            Assert.AreEqual(0, session.State.PwUsed);
        }

        [Test]
        public void TheBarPlacesSevenButtonsInTwoColumnsWithGapsAndAboveTheJumpButton()
        {
            Assert.AreEqual(1, PowerUpBar.ColumnsFor(1));
            Assert.AreEqual(1, PowerUpBar.ColumnsFor(3));
            Assert.AreEqual(2, PowerUpBar.ColumnsFor(4));
            Assert.AreEqual(2, PowerUpBar.ColumnsFor(7));
            Assert.GreaterOrEqual(PowerUpBar.ButtonSize, UiKit.MinTouchPx, "at least 48 dp");
            Assert.GreaterOrEqual(PowerUpBar.Gap, UiKit.MinSpacingPx, "at least 8 dp between neighbours");
            Assert.GreaterOrEqual(PowerUpBar.BottomOffset, 40f + 20f + 280f + UiKit.MinSpacingPx, "above the 280 px jump button with a gap");
            for (int i = 0; i < 7; i++)
            {
                Vector2 a = PowerUpBar.PositionFor(i, 7);
                for (int j = i + 1; j < 7; j++)
                {
                    Vector2 b = PowerUpBar.PositionFor(j, 7);
                    bool sepX = Mathf.Abs(a.x - b.x) >= PowerUpBar.ButtonSize + PowerUpBar.Gap - 0.01f;
                    bool sepY = Mathf.Abs(a.y - b.y) >= PowerUpBar.ButtonSize + PowerUpBar.Gap - 0.01f;
                    Assert.IsTrue(sepX || sepY, "buttons " + i + " and " + j);
                }
            }
            Assert.AreEqual("x7", PowerUpBar.FormatCount(7));
            Assert.AreEqual("x999", PowerUpBar.FormatCount(999));
            Assert.AreEqual("x1.2K", PowerUpBar.FormatCount(1234));
            Assert.AreEqual("x3.4M", PowerUpBar.FormatCount(3456789));
        }

        [Test]
        public void RunInputLatchesPowerPressesUntilTheNextTick()
        {
            var input = new RunInputReader();
            input.ScriptedMove = () => 0;
            input.LatchPower(PowerUp.Rocket);
            input.LatchPower(PowerUp.SpringBoots);   // not implemented: no bit
            Assert.AreEqual(PowerUps.InputBit(PowerUp.Rocket), input.ReadTick());
            Assert.AreEqual(0, input.ReadTick(), "consumed");
            input.LatchPower(PowerUp.Flight);
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
