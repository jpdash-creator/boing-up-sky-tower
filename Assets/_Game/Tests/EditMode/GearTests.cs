using System.Collections.Generic;
using System.IO;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using BoingUpSkyTower.Simulation;
using NUnit.Framework;

namespace BoingUpSkyTower.Tests
{
    /// <summary>Gear, drops, chests, worn boosts and the save migration (docs/17, docs/19): three slots, 180 pieces, no pants, no forge.</summary>
    public class GearTests
    {
        ContentRepository repo;

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            GearCatalog.ArtProbe = d => true;   // these tests cover the rules for every piece; the art filter has its own tests below and in CharacterRigTests
        }

        [TearDown]
        public void TearDown() { GearCatalog.ArtProbe = null; }

        ProgressService NewService() { return new ProgressService(repo, new MemorySaveStore(), 5); }

        // ---- catalog ----

        [Test]
        public void CatalogHas180PiecesInThreeSlotsWithTheRarityCountsPerSlot()
        {
            Assert.AreEqual(3, GearCatalog.SlotCount);
            Assert.AreEqual(3, System.Enum.GetValues(typeof(GearSlot)).Length, "head, body, boots: no pants");
            Assert.AreEqual(180, GearCatalog.All.Count);
            int[] expected = { 20, 16, 12, 8, 4 };
            for (int s = 0; s < 3; s++)
            {
                int inSlot = 0;
                for (int r = 0; r < 5; r++)
                {
                    GearDef[] p = GearCatalog.Pieces((GearSlot)s, (GearRarity)r);
                    Assert.AreEqual(expected[r], p.Length, "slot " + s + " rarity " + r);
                    inSlot += p.Length;
                }
                Assert.AreEqual(60, inSlot);
            }
        }

        [Test]
        public void IdsFollowSlotRarityLetterAndNumberAndArtIdIsTheId()
        {
            var ids = new HashSet<string>();
            var names = new HashSet<string>();
            string letters = "curel";
            foreach (GearDef d in GearCatalog.All)
            {
                Assert.IsTrue(ids.Add(d.Id), d.Id);
                Assert.IsTrue(names.Add(d.Slot + "/" + d.Name), "name " + d.Name);
                Assert.AreEqual("gear_" + GearCatalog.SlotNames[(int)d.Slot].ToLowerInvariant() + "_" + letters[(int)d.Rarity] + d.Number.ToString("D2"), d.Id);
                Assert.AreEqual(d.Id, d.ArtId, "art id = id");
                Assert.IsFalse(d.Id.Contains("pants"));
                Assert.AreSame(d, GearCatalog.Get(d.Id));
            }
            Assert.IsNotNull(GearCatalog.Get("gear_head_c01"));
            Assert.IsNotNull(GearCatalog.Get("gear_boots_l04"));
            Assert.IsNull(GearCatalog.Get("gear_boots_l05"));
            Assert.IsNull(GearCatalog.Get("gear_pants_c01"));
        }

        [Test]
        public void EachSlotBoostsItsPowerUpsInTurnAndRarityPicksTheFixedSize()
        {
            PowerUp[][] want =
            {
                new[] { PowerUp.Shield, PowerUp.SlowTime },
                new[] { PowerUp.Magnet, PowerUp.Flight, PowerUp.WallStick },
                new[] { PowerUp.DoubleJump, PowerUp.Rocket },
            };
            int[] pct = { 10, 20, 40, 75, 100 };
            foreach (GearDef d in GearCatalog.All)
            {
                Assert.AreEqual(want[(int)d.Slot][(d.Number - 1) % want[(int)d.Slot].Length], d.Boost, d.Id);
                Assert.AreEqual(pct[(int)d.Rarity], d.BoostPercent, d.Id);
            }
            // every power-up of the slot appears in every rarity band that is big enough to alternate through all of them
            for (int s = 0; s < 3; s++)
                for (int r = 0; r < 5; r++)
                {
                    var seen = new HashSet<PowerUp>();
                    foreach (GearDef d in GearCatalog.Pieces((GearSlot)s, (GearRarity)r)) seen.Add(d.Boost);
                    Assert.AreEqual(want[s].Length, seen.Count, "slot " + s + " rarity " + r);
                }
        }

        // ---- art: no art, no drop, no tile ----

        [Test]
        public void APieceWithoutArtNeverDropsAndCannotBeWorn()
        {
            GearCatalog.ArtProbe = d => false;
            for (int i = 0; i < 500; i++) Assert.AreEqual(0, GearDrops.Roll(15, 9000 + i, i).Count, "nothing has art, nothing drops");
            var types = new List<PowerUpDef>(PowerUpCatalog.BuiltIn().Implemented);
            for (int i = 0; i < 200; i++)
            {
                Assert.AreEqual(0, ChestRolls.Roll(ChestKind.Iron, i, types).Gear.Count);
                Assert.AreEqual(0, ChestRolls.Roll(ChestKind.Golden, i, types).Gear.Count);
            }
            var svc = NewService();
            svc.GrantGear("gear_head_c01");
            Assert.IsFalse(svc.Equip("gear_head_c01"), "no art: cannot be worn");
            GearCatalog.ArtProbe = d => true;
            Assert.IsTrue(svc.Equip("gear_head_c01"));
            GearCatalog.ArtProbe = d => false;
            Assert.IsNull(svc.EquippedDef(GearSlot.Head), "a worn piece whose art is gone counts as nothing worn");
            Assert.AreEqual(0, svc.GearBoostPercents()[(int)PowerUp.Shield]);
        }

        [Test]
        public void OnlyPiecesWithArtDropOrAppearInChests()
        {
            GearCatalog.ArtProbe = d => d.Rarity >= GearRarity.Rare && d.Number % 2 == 1;
            int dropped = 0;
            for (int i = 0; i < 3000; i++)
                foreach (string id in GearDrops.Roll(15, 100 + i, i))
                {
                    Assert.IsTrue(GearCatalog.HasArt(GearCatalog.Get(id)), id);
                    dropped++;
                }
            Assert.Greater(dropped, 100);
            var types = new List<PowerUpDef>(PowerUpCatalog.BuiltIn().Implemented);
            for (int i = 0; i < 500; i++)
            {
                foreach (string id in ChestRolls.Roll(ChestKind.Iron, i, types).Gear) Assert.IsTrue(GearCatalog.HasArt(GearCatalog.Get(id)), id);
                foreach (string id in ChestRolls.Roll(ChestKind.Golden, i, types).Gear) Assert.IsTrue(GearCatalog.HasArt(GearCatalog.Get(id)), id);
            }
            Assert.AreEqual(180, GearCatalog.All.Count, "all definitions are kept");
        }

        [Test]
        public void TheShippedPartFilesDecideWhichPiecesHaveArt()
        {
            GearCatalog.ArtProbe = null;   // the real check: the required part pictures exist in Resources/Characters/gear/<id>/
            foreach (GearDef d in GearCatalog.All)
            {
                bool files = true;
                foreach (string part in GearCatalog.RequiredParts(d.Slot))
                    if (UnityEngine.Resources.Load<UnityEngine.Sprite>(GearCatalog.ArtRoot + d.ArtId + "/" + part) == null) files = false;
                Assert.AreEqual(files, GearCatalog.HasArt(d), d.Id);
            }
            CollectionAssert.AreEqual(new[] { "head" }, GearCatalog.RequiredParts(GearSlot.Head));
            CollectionAssert.AreEqual(new[] { "body" }, GearCatalog.RequiredParts(GearSlot.Body));
            CollectionAssert.AreEqual(new[] { "body", "sleeveL", "sleeveR" }, GearCatalog.PartNames(GearSlot.Body));
            CollectionAssert.AreEqual(new[] { "bootL", "bootR" }, GearCatalog.PartNames(GearSlot.Boots));
        }

        // ---- drops ----

        static int[] CountHits(int levelIndex, int rolls)
        {
            var hits = new int[5];
            int[] pcts = GearDrops.PercentsFor(levelIndex);
            for (int i = 0; i < rolls; i++)
            {
                var rng = new Rng(GearDrops.SeedOf(1000 + i * 7, i));
                List<string> drops = GearDrops.RollWith(ref rng, pcts);
                foreach (string id in drops) hits[(int)GearCatalog.Get(id).Rarity]++;
            }
            return hits;
        }

        [TestCase(1, new[] { 20, 10, 5, 2, 1 })]
        [TestCase(5, new[] { 40, 20, 10, 5, 2 })]
        [TestCase(10, new[] { 40, 20, 10, 5, 2 })]
        [TestCase(15, new[] { 80, 40, 20, 10, 5 })]
        public void DropOddsPerLevelMatchTheSpecWithFixedSeeds(int levelIndex, int[] percents)
        {
            Assert.AreEqual(percents, GearDrops.PercentsFor(levelIndex));
            const int rolls = 40000;
            int[] hits = CountHits(levelIndex, rolls);
            int[] again = CountHits(levelIndex, rolls);
            Assert.AreEqual(hits, again, "fixed seeds give fixed hits");
            for (int r = 0; r < 5; r++)
            {
                double expected = rolls * percents[r] / 100.0;
                double sigma = System.Math.Sqrt(rolls * (percents[r] / 100.0) * (1 - percents[r] / 100.0));
                Assert.AreEqual(expected, hits[r], 5 * sigma + 1, "rarity " + r + " at level " + levelIndex);
            }
        }

        [Test]
        public void OtherLevelsUseTheNormalOdds()
        {
            foreach (int i in new[] { 1, 2, 3, 4, 6, 7, 8, 9, 11, 12, 13, 14 })
                Assert.AreEqual(GearDrops.NormalPercent, GearDrops.PercentsFor(i), "level " + i);
        }

        [Test]
        public void ADropIsRepeatableAndHitsPickAnyOfTheThreeSlots()
        {
            Assert.AreEqual(GearDrops.Roll(15, 777, 3), GearDrops.Roll(15, 777, 3));
            var slots = new HashSet<GearSlot>();
            var pieces = new HashSet<string>();
            for (int i = 0; i < 300; i++)
                foreach (string id in GearDrops.Roll(15, 4242, i))
                {
                    slots.Add(GearCatalog.Get(id).Slot);
                    pieces.Add(id);
                }
            Assert.AreEqual(3, slots.Count, "head, body and boots all drop");
            Assert.Greater(pieces.Count, 20, "different pieces drop");
            bool differs = false;
            for (int i = 0; i < 30 && !differs; i++) differs = !ListEq(GearDrops.Roll(15, 777, i), GearDrops.Roll(15, 777, i + 1));
            Assert.IsTrue(differs, "the clear count changes the roll");
        }

        static bool ListEq(List<string> a, List<string> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }

        [Test]
        public void ClearingLevelsPutsTheDroppedGearInTheInventoryAndTheResult()
        {
            var svc = NewService();
            int dropped = 0;
            RegionDef r1 = repo.GetRegionByIndex(1);
            for (int rep = 0; rep < 12; rep++)
            {
                foreach (LevelDef l in repo.LevelsOf(r1.Id))
                {
                    RewardResult rr = svc.ApplyRun(l.Id, new RunOutcome { Completed = true, StarMask = 1 });
                    if (rr.GearDrops != null)
                    {
                        dropped += rr.GearDrops.Count;
                        foreach (string id in rr.GearDrops) Assert.IsNotNull(GearCatalog.Get(id));
                    }
                }
            }
            Assert.Greater(dropped, 10, "180 clears with the 20 to 80 percent common odds drop plenty");
            int owned = 0;
            foreach (var kv in svc.Data.Inventory.Gear) owned += kv.Value;
            Assert.AreEqual(dropped, owned);
        }

        [Test]
        public void ADropIsTheSameForTheSameLevelAndClearCount()
        {
            LevelDef l = repo.GetLevel("lvl_001_15");
            RegionDef region = repo.GetRegion(l.Region);
            var run = new RunOutcome { Completed = true, StarMask = 1 };
            RewardResult a = RewardCalculator.Compute(l, region, run, null);
            RewardResult b = RewardCalculator.Compute(l, region, run, null);
            Assert.IsTrue(ListEq(a.GearDrops, b.GearDrops));
            var failed = RewardCalculator.Compute(l, region, new RunOutcome { Completed = false }, null);
            Assert.IsNull(failed.GearDrops, "a failed run drops nothing");
        }

        // ---- worn gear ----

        [Test]
        public void OnlyOwnedPiecesCanBeWornAndTheyReplaceTheSlot()
        {
            var svc = NewService();
            GearDef a = GearCatalog.Pieces(GearSlot.Boots, GearRarity.Common)[0];
            GearDef b = GearCatalog.Pieces(GearSlot.Boots, GearRarity.Rare)[1];
            Assert.IsFalse(svc.Equip(a.Id));
            svc.GrantGear(a.Id);
            svc.GrantGear(b.Id);
            Assert.IsTrue(svc.Equip(a.Id));
            Assert.AreEqual(a.Id, svc.EquippedId(GearSlot.Boots));
            Assert.IsTrue(svc.Equip(b.Id));
            Assert.AreEqual(b.Id, svc.EquippedId(GearSlot.Boots));
            Assert.IsNull(svc.EquippedId(GearSlot.Head));
            svc.Unequip(GearSlot.Boots);
            Assert.IsNull(svc.EquippedId(GearSlot.Boots));
            Assert.AreEqual(3, svc.Data.Equipped.Count);
        }

        [Test]
        public void WornGearSurvivesSaveAndLoad()
        {
            var store = new MemorySaveStore();
            var svc = new ProgressService(repo, store, 5);
            GearDef h = GearCatalog.Pieces(GearSlot.Head, GearRarity.Legendary)[0];
            svc.GrantGear(h.Id, 2);
            svc.Equip(h.Id);
            var again = new ProgressService(repo, store, 5);
            Assert.AreEqual(h.Id, again.EquippedId(GearSlot.Head));
            Assert.AreEqual(2, again.GearCount(h.Id));
        }

        // ---- boosts ----

        static GearDef FindPiece(GearSlot slot, GearRarity rarity, PowerUp boost)
        {
            foreach (GearDef d in GearCatalog.Pieces(slot, rarity)) if (d.Boost == boost) return d;
            Assert.Fail("no such piece");
            return null;
        }

        [Test]
        public void BoostsAreSeparatePerPowerUpAndTheLargerSourceOfOnePowerUpWins()
        {
            GearDef body = FindPiece(GearSlot.Body, GearRarity.Rare, PowerUp.Magnet);       // 40
            GearDef head = FindPiece(GearSlot.Head, GearRarity.Common, PowerUp.Shield);     // 10
            GearDef boots = FindPiece(GearSlot.Boots, GearRarity.Epic, PowerUp.DoubleJump); // 75
            int[] b = GearBoosts.Compute(new[] { head, body, boots });
            Assert.AreEqual(40, b[(int)PowerUp.Magnet]);
            Assert.AreEqual(10, b[(int)PowerUp.Shield]);
            Assert.AreEqual(75, b[(int)PowerUp.DoubleJump]);
            Assert.AreEqual(0, b[(int)PowerUp.Flight], "nothing stacks across powers");
            GearDef head2 = FindPiece(GearSlot.Head, GearRarity.Legendary, PowerUp.Shield);  // 100
            int[] c = GearBoosts.Compute(new[] { head, head2 });
            Assert.AreEqual(100, c[(int)PowerUp.Shield], "two sources of one power-up: the larger one, no stacking");
        }

        [Test]
        public void TheRunConfigIsBoostedByAPercentOfEachPowerUpAndTheSharedConfigIsUntouched()
        {
            var svc = NewService();
            var baseCfg = new SimConfig();
            svc.GrantGear(FindPiece(GearSlot.Boots, GearRarity.Legendary, PowerUp.Rocket).Id);       // +100 percent Rocket
            svc.GrantGear(FindPiece(GearSlot.Head, GearRarity.Rare, PowerUp.Shield).Id);              // +40 percent Shield
            svc.GrantGear(FindPiece(GearSlot.Body, GearRarity.Uncommon, PowerUp.Magnet).Id);          // +20 percent Magnet pull
            foreach (GearDef d in GearCatalog.All) if (svc.OwnsGear(d.Id)) svc.Equip(d.Id);
            SimConfig run = svc.BuildRunConfig(baseCfg);
            Assert.AreEqual(baseCfg.RocketTicks * 2, run.RocketTicks);
            Assert.AreEqual(baseCfg.ShieldTicks * 140 / 100, run.ShieldTicks);
            Assert.AreEqual(baseCfg.MagnetExtraRadius * 120 / 100, run.MagnetExtraRadius);
            Assert.AreEqual(baseCfg.FlightTicks, run.FlightTicks, "an unboosted power-up keeps its numbers");
            Assert.AreEqual(120, baseCfg.RocketTicks, "the shared config is never changed");
            Assert.AreNotSame(baseCfg, run);
        }

        [Test]
        public void EveryBoostPercentOfThePowerUpsChangesItsConfigField()
        {
            // head: Shield, Slow Time; body: Magnet, Flight, Wall Stick; boots: Double Jump, Rocket (docs/19)
            var baseCfg = new SimConfig();
            int[] pct = { 10, 20, 40, 75, 100 };
            for (int r = 0; r < 5; r++)
            {
                var one = new SimConfig();
                var boost = new int[PowerUps.MaxType + 1];
                boost[(int)PowerUp.WallStick] = pct[r];
                boost[(int)PowerUp.DoubleJump] = pct[r];
                GearBoosts.Apply(one, boost);
                Assert.AreEqual((int)((long)baseCfg.WallStickWindowTicks * (100 + pct[r]) / 100), one.WallStickWindowTicks);
                Assert.AreEqual((int)((long)baseCfg.DoubleJumpWindowTicks * (100 + pct[r]) / 100), one.DoubleJumpWindowTicks);
            }
        }

        [Test]
        public void NothingWornGivesTheSameConfigNumbers()
        {
            var svc = NewService();
            SimConfig run = svc.BuildRunConfig(new SimConfig());
            var d = new SimConfig();
            Assert.AreEqual(d.FlightTicks, run.FlightTicks);
            Assert.AreEqual(d.MagnetTicks, run.MagnetTicks);
            Assert.AreEqual(d.MagnetExtraRadius, run.MagnetExtraRadius);
            Assert.AreEqual(d.SlowTimeTicks, run.SlowTimeTicks);
        }

        [Test]
        public void BoostedGearDoesNotChangeTheLevelLayoutOrTheSimulation()
        {
            var svc = NewService();
            foreach (GearSlot s in new[] { GearSlot.Head, GearSlot.Body, GearSlot.Boots })
            {
                GearDef top = GearCatalog.Pieces(s, GearRarity.Legendary)[0];
                svc.GrantGear(top.Id);
                svc.Equip(top.Id);
            }
            foreach (string id in new[] { "lvl_001_01", "lvl_001_15", "lvl_003_07", "lvl_005_15" })
            {
                var plain = new RunSession(repo, new SimConfig(), id);
                var boosted = new RunSession(repo, svc.BuildRunConfig(new SimConfig()), id);
                Assert.AreEqual(plain.Data.ComputeHash(), boosted.Data.ComputeHash(), id + " layout hash");
                // the same inputs without power-ups give the same state
                for (int t = 0; t < 600; t++)
                {
                    int input = (t / 50) % 2 == 0 ? SimInput.Right : SimInput.Left;
                    if (t % 40 == 0) input |= SimInput.Jump;
                    plain.Step(input);
                    boosted.Step(input);
                }
                Assert.AreEqual(plain.State.X.Raw, boosted.State.X.Raw, id);
                Assert.AreEqual(plain.State.Y.Raw, boosted.State.Y.Raw, id);
                Assert.AreEqual(plain.State.Score, boosted.State.Score, id);
            }
        }

        // ---- chests ----

        [Test]
        public void ChestPricesMatchTheSpec()
        {
            Assert.AreEqual(100, ChestRolls.Price(ChestKind.Iron, 1));
            Assert.AreEqual(1000, ChestRolls.Price(ChestKind.Iron, 10));
            Assert.AreEqual(300, ChestRolls.Price(ChestKind.Golden, 1));
            Assert.AreEqual(3000, ChestRolls.Price(ChestKind.Golden, 10));
            Assert.AreEqual(0, ChestRolls.Price(ChestKind.Iron, 5));
        }

        [Test]
        public void ChestsAreOpenedWithGemsOnlyAndNotEnoughChangesNothing()
        {
            var svc = NewService();
            svc.AddCoins(5000);
            svc.AddGems(99);
            List<ChestContents> c;
            Assert.AreEqual(ChestOpenResult.NotEnoughGems, svc.TryOpenChests(ChestKind.Iron, 1, out c));
            Assert.IsNull(c);
            Assert.AreEqual(99, svc.Gems);
            Assert.AreEqual(5000, svc.Coins);
            Assert.AreEqual(0, svc.OwnedGearKinds);
            Assert.AreEqual(ChestOpenResult.Invalid, svc.TryOpenChests(ChestKind.Iron, 3, out c));
        }

        [Test]
        public void AnIronChestGivesCommonGearThreePowerUpsAndCoinsFromAllThreeSlots()
        {
            var svc = NewService();
            svc.AddGems(1000);
            List<ChestContents> list;
            Assert.AreEqual(ChestOpenResult.Ok, svc.TryOpenChests(ChestKind.Iron, 10, out list));
            Assert.AreEqual(0, svc.Gems);
            Assert.AreEqual(10, list.Count);
            var slots = new HashSet<GearSlot>();
            int gear = 0;
            foreach (ChestContents c in list)
            {
                Assert.GreaterOrEqual(c.Gear.Count, 1, "Common is 100 percent");
                Assert.LessOrEqual(c.Gear.Count, 3);
                Assert.AreEqual(GearRarity.Common, GearCatalog.Get(c.Gear[0]).Rarity);
                foreach (string g in c.Gear)
                {
                    Assert.LessOrEqual((int)GearCatalog.Get(g).Rarity, (int)GearRarity.Rare);
                    slots.Add(GearCatalog.Get(g).Slot);
                    gear++;
                }
                Assert.AreEqual(3, c.PowerUps.Count);
                Assert.That(c.Coins, Is.InRange(100, 1000));
            }
            Assert.AreEqual(3, slots.Count, "ten chests roll all three slots");
            int owned = 0;
            foreach (var kv in svc.Data.Inventory.Gear) owned += kv.Value;
            Assert.AreEqual(gear, owned);
        }

        [Test]
        public void AGoldenChestStartsAtRareAndGivesSixPowerUps()
        {
            var svc = NewService();
            svc.AddGems(3000);
            List<ChestContents> list;
            Assert.AreEqual(ChestOpenResult.Ok, svc.TryOpenChests(ChestKind.Golden, 10, out list));
            Assert.AreEqual(0, svc.Gems);
            Assert.AreEqual(10, list.Count);
            foreach (ChestContents c in list)
            {
                Assert.GreaterOrEqual(c.Gear.Count, 1, "Rare is 100 percent");
                foreach (string g in c.Gear) Assert.GreaterOrEqual((int)GearCatalog.Get(g).Rarity, (int)GearRarity.Rare);
                Assert.AreEqual(6, c.PowerUps.Count);
                Assert.That(c.Coins, Is.InRange(300, 3000));
            }
        }

        [Test]
        public void ChestOddsAre50And25PercentForTheUpperRarities()
        {
            var types = new List<PowerUpDef>(PowerUpCatalog.BuiltIn().Implemented);
            const int n = 20000;
            int unc = 0, rare = 0, epic = 0, leg = 0;
            for (int i = 0; i < n; i++)
            {
                foreach (string id in ChestRolls.Roll(ChestKind.Iron, i, types).Gear)
                {
                    GearRarity r = GearCatalog.Get(id).Rarity;
                    if (r == GearRarity.Uncommon) unc++;
                    if (r == GearRarity.Rare) rare++;
                }
                foreach (string id in ChestRolls.Roll(ChestKind.Golden, i, types).Gear)
                {
                    GearRarity r = GearCatalog.Get(id).Rarity;
                    if (r == GearRarity.Epic) epic++;
                    if (r == GearRarity.Legendary) leg++;
                }
            }
            Assert.AreEqual(n * 0.50, unc, n * 0.03);
            Assert.AreEqual(n * 0.25, rare, n * 0.03);
            Assert.AreEqual(n * 0.50, epic, n * 0.03);
            Assert.AreEqual(n * 0.25, leg, n * 0.03);
        }

        [Test]
        public void ChestPowerUpsCoverAllSevenTypes()
        {
            var types = new List<PowerUpDef>(PowerUpCatalog.BuiltIn().Implemented);
            Assert.AreEqual(7, types.Count);
            var seen = new HashSet<string>();
            for (int i = 0; i < 200; i++) foreach (string id in ChestRolls.Roll(ChestKind.Golden, i, types).PowerUps) seen.Add(id);
            Assert.AreEqual(7, seen.Count);
        }

        [Test]
        public void TheSameSaveOpensTheSameChests()
        {
            var a = NewService();
            var b = NewService();
            a.AddGems(1000);
            b.AddGems(1000);
            List<ChestContents> ca, cb;
            a.TryOpenChests(ChestKind.Iron, 10, out ca);
            b.TryOpenChests(ChestKind.Iron, 10, out cb);
            for (int i = 0; i < 10; i++)
            {
                Assert.IsTrue(ListEq(ca[i].Gear, cb[i].Gear));
                Assert.AreEqual(ca[i].Coins, cb[i].Coins);
            }
            Assert.AreEqual(10, a.Data.ChestOpens);
        }

        // ---- save migration (version 1 and 2 -> 3) ----

        static string TempDir()
        {
            string dir = Path.Combine(UnityEngine.Application.dataPath, "..", "Temp", "GearTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        [Test]
        public void AVersionOneSaveLoadsWithEmptyGearAndKeepsItsProgress()
        {
            string dir = TempDir();
            try
            {
                string path = Path.Combine(dir, "save.json");
                File.WriteAllText(path, "{\"version\":1,\"coins\":321,\"gems\":45,\"levels\":{\"lvl_001_01\":{\"stars\":2,\"starMask\":3,\"bestScore\":9,\"clears\":1}},\"unlockedRegions\":[],\"inventory\":{\"powerups\":{\"pw_flight\":2}},\"loadout\":[\"pw_flight\",\"\",\"\"]}");
                var svc = new ProgressService(repo, new FileSaveStore(path), 5);
                Assert.AreEqual(321, svc.Coins);
                Assert.AreEqual(45, svc.Gems);
                Assert.AreEqual(2, svc.PowerUpCount("pw_flight"));
                Assert.AreEqual(2, svc.GetProgress("lvl_001_01").Stars);
                Assert.AreEqual(0, svc.OwnedGearKinds);
                Assert.IsNull(svc.EquippedId(GearSlot.Head));
                Assert.AreEqual(3, svc.Data.Equipped.Count);
                Assert.AreEqual(SaveData.CurrentVersion, svc.Data.Version);
                svc.GrantGear(GearCatalog.Pieces(GearSlot.Head, GearRarity.Common)[0].Id);
                var again = new ProgressService(repo, new FileSaveStore(path), 5);
                Assert.AreEqual(1, again.OwnedGearKinds);
                Assert.AreEqual(321, again.Coins);
                StringAssert.Contains("\"version\": 3", File.ReadAllText(path));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void AVersionTwoSaveWithPantsForgeLevelsAndRemovedIdsIsMigrated()
        {
            string dir = TempDir();
            try
            {
                string path = Path.Combine(dir, "save.json");
                // the old format: four worn slots (head, body, pants, boots), forge levels, pants pieces and the dev test pieces
                File.WriteAllText(path, "{\"version\":2,\"coins\":77,\"gems\":8,\"inventory\":{\"powerups\":{\"pw_flight\":1},"
                    + "\"gear\":{\"gear_head_c01\":3,\"gear_body_u02\":1,\"gear_pants_c01\":2,\"gear_pants_l04\":1,\"gear_boots_e03\":1,\"gear_head_test\":1,\"gear_boots_c99\":4,\"gear_head_c02\":0},"
                    + "\"gearLevels\":{\"gear_head_c01\":3}},"
                    + "\"equipped\":[\"gear_head_c01\",\"gear_body_u02\",\"gear_pants_c01\",\"gear_boots_e03\"],\"chestOpens\":5}");
                var svc = new ProgressService(repo, new FileSaveStore(path), 5);   // must not throw
                Assert.AreEqual(77, svc.Coins);
                Assert.AreEqual(1, svc.PowerUpCount("pw_flight"));
                Assert.AreEqual(5, svc.Data.ChestOpens);
                Assert.AreEqual(3, svc.OwnedGearKinds, "head_c01, body_u02 and boots_e03 are still pieces; pants, test and unknown ids are dropped");
                Assert.AreEqual(3, svc.GearCount("gear_head_c01"), "owned copies are kept");
                Assert.IsFalse(svc.OwnsGear("gear_pants_c01"));
                Assert.IsFalse(svc.OwnsGear("gear_head_test"));
                Assert.IsFalse(svc.OwnsGear("gear_boots_c99"));
                CollectionAssert.AreEqual(new[] { "gear_head_c01", "gear_body_u02", "gear_boots_e03" }, svc.Data.Equipped, "the pants entry is gone, boots moved up to slot 3");
                Assert.AreEqual(SaveData.CurrentVersion, svc.Data.Version);
                svc.GrantGear("gear_head_c05");   // forces a save
                string text = File.ReadAllText(path);
                StringAssert.Contains("\"version\": 3", text);
                StringAssert.DoesNotContain("gearLevels", text);
                StringAssert.DoesNotContain("pants", text);
                var again = new ProgressService(repo, new FileSaveStore(path), 5);
                CollectionAssert.AreEqual(new[] { "gear_head_c01", "gear_body_u02", "gear_boots_e03" }, again.Data.Equipped);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void BadGearEntriesInASaveAreRepaired()
        {
            var store = new MemorySaveStore();
            var data = new SaveData();
            data.Inventory.Gear["gear_nope_x99"] = 3;
            data.Inventory.Gear["gear_head_c01"] = 0;
            data.Inventory.Gear["gear_head_c02"] = 2;
            data.Equipped = new List<string> { "gear_head_c02", "gear_head_c02", "gear_zzz", "", "extra" };   // wrong slot, unknown, too long
            store.Save(data);
            var svc = new ProgressService(repo, store, 5);
            Assert.AreEqual(1, svc.OwnedGearKinds);
            Assert.AreEqual(3, svc.Data.Equipped.Count);
            Assert.AreEqual("gear_head_c02", svc.EquippedId(GearSlot.Head));
            Assert.IsNull(svc.EquippedId(GearSlot.Body), "a head piece cannot sit in the body slot");
            Assert.IsNull(svc.EquippedId(GearSlot.Boots));
        }

        [Test]
        public void ResetAllClearsGearToo()
        {
            var svc = NewService();
            svc.GrantGear("gear_boots_c01");
            svc.Equip("gear_boots_c01");
            svc.ResetAll();
            Assert.AreEqual(0, svc.OwnedGearKinds);
            Assert.IsNull(svc.EquippedId(GearSlot.Boots));
            Assert.AreEqual(3, svc.Data.Equipped.Count);
        }
    }
}
