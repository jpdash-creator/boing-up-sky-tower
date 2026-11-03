using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace BoingUpSkyTower.Tests
{
    /// <summary>The character parts, the roster and the chest rolls (gear and the inventory were removed 2026-10-24).</summary>
    public class CharacterRigTests
    {
        [Test]
        public void EveryCharacterHasItsPartsOnTheSameCanvas()
        {
            foreach (CharacterDef c in CharacterCatalog.All)
            {
                var draws = PartsAssembler.Build(c, null);
                Assert.AreEqual(c.Rig.Order.Length, draws.Count, c.Id + " is missing a part picture");
                foreach (PartDraw d in draws)
                {
                    Assert.AreEqual(Vector2.zero, d.Min);
                    Assert.AreEqual(Vector2.one, d.Max);
                    Assert.AreEqual(1365, d.Sprite.texture.width, c.Id + " " + d.Name);
                    Assert.AreEqual(1649, d.Sprite.texture.height, c.Id + " " + d.Name);
                }
            }
        }

        [Test]
        public void TheNewRegionHeroesHaveAllSixPartsAndTheirRig()
        {
            foreach (string id in new[] { "forestgirl", "beachotter", "candygirl", "monk" })
            {
                CharacterDef c = CharacterCatalog.Get(id);
                Assert.AreEqual(id, c.Id);
                // Bonbon also has a "back" piece (the candy cane painted behind her); the others have the six body parts only
                string[] expected = id == "candygirl" ? new[] { "back", "legL", "legR", "body", "handL", "handR", "head" } : new[] { "legL", "legR", "body", "handL", "handR", "head" };
                CollectionAssert.AreEqual(expected, c.Rig.Order, id);
                foreach (string part in c.Rig.Order) Assert.IsNotNull(c.PartSprite(part), id + " is missing " + part);
                Assert.AreEqual(1365f, c.Rig.CanvasW);
                Assert.AreEqual(1649f, c.Rig.CanvasH);
            }
        }

        [Test]
        public void TheRosterHasMomoAndTheRegionHeroes()
        {
            var all = CharacterCatalog.All;
            Assert.AreEqual(10, all.Length, "5 Common heroes + their 5 Uncommon merge tiers (docs/34)");
            // order is hero then tier (docs/36): each Common hero is followed by its Uncommon tier
            string[] expected = { "momo", "momo_u", "forestgirl", "forestgirl_u", "beachotter", "beachotter_u", "candygirl", "candygirl_u", "monk", "monk_u" };
            for (int i = 0; i < expected.Length; i++) Assert.AreEqual(expected[i], all[i].Id);
            Assert.AreSame(CharacterCatalog.Momo, CharacterCatalog.Get("nobody"), "an unknown id falls back to Momo");
        }

        [Test]
        public void TheRunPlacesAPartExactlyWhereTheFractionsSay()
        {
            var d = new PartDraw { Min = new Vector2(0f, 0f), Max = new Vector2(1f, 1f) };
            Vector3 pos, scale;
            d.LocalTransform(new Vector2(0.9f, 1f), out pos, out scale);
            Assert.AreEqual(0f, pos.x, 1e-5f);
            Assert.AreEqual(0f, pos.y, 1e-5f);
            Assert.AreEqual(1f, scale.x, 1e-5f);
            Assert.AreEqual(1f, scale.y, 1e-5f);
        }

        [Test]
        public void ChestsGiveCoinsAndPowerUpsOnly()
        {
            var types = new System.Collections.Generic.List<PowerUpDef>(new ProgressService(Content.ContentRepository.LoadFromResources(), new MemorySaveStore(), 5).PowerUpDefs.Implemented);
            ChestContents iron = ChestRolls.Roll(ChestKind.Iron, 0, types);
            ChestContents gold = ChestRolls.Roll(ChestKind.Golden, 0, types);
            Assert.AreEqual(1, iron.PowerUps.Count);
            Assert.AreEqual(3, gold.PowerUps.Count);
            Assert.That(iron.Coins, Is.InRange(400, 600));
            Assert.That(gold.Coins, Is.InRange(1000, 1400));
        }

        [Test]
        public void ChestsDropNoCharactersWhileTheyAreSwitchedOff()
        {
            Assert.IsFalse(ChestRolls.CharactersEnabled);
            var types = new System.Collections.Generic.List<PowerUpDef>(new ProgressService(Content.ContentRepository.LoadFromResources(), new MemorySaveStore(), 5).PowerUpDefs.Implemented);
            for (int i = 0; i < 200; i++)
            {
                Assert.AreEqual(0, ChestRolls.Roll(ChestKind.Iron, i, types).Characters.Count);
                Assert.AreEqual(0, ChestRolls.Roll(ChestKind.Golden, i, types).Characters.Count);
            }
        }

        [Test]
        public void ARegionUnlockGivesItsCommonCharacterAndChestsAddNoCharacter()
        {
            var svc = new ProgressService(Content.ContentRepository.LoadFromResources(), new MemorySaveStore(), 5);
            Assert.IsTrue(svc.OwnsCharacter("momo"));
            Assert.IsFalse(svc.OwnsCharacter("forestgirl"), "region 2 is still locked");
            svc.Data.Gems = 1000;
            System.Collections.Generic.List<ChestContents> opened;
            Assert.AreEqual(ChestOpenResult.Ok, svc.TryOpenChests(ChestKind.Iron, 1, out opened));
            Assert.AreEqual(0, opened[0].Characters.Count, "characters are switched off");
            Assert.IsFalse(svc.OwnsCharacter("forestgirl"));
            Assert.That(opened[0].Coins, Is.InRange(400, 600));
        }

        [Test]
        public void ADuplicateCharacterRefundsAQuarterOfItsGemPrice()
        {
            Assert.AreEqual(100, ChestRolls.DuplicateRefundGems(400));
            Assert.AreEqual(0, ChestRolls.DuplicateRefundGems(0));
        }

        [Test]
        public void AnOldSaveWithGearFieldsStillLoads()
        {
            var old = Newtonsoft.Json.JsonConvert.DeserializeObject<SaveData>("{\"version\":2,\"coins\":5,\"inventory\":{\"powerups\":{},\"gear\":{\"gear_head_c01\":2},\"gearLevels\":{}},\"equipped\":[\"gear_head_c01\",\"\",\"\",\"\"],\"chestOpens\":3}");
            Assert.AreEqual(5, old.Coins);
            Assert.AreEqual(3, old.ChestOpens);
        }
    }
}
