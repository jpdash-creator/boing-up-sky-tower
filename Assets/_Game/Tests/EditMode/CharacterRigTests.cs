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
            foreach (CharacterDef c in CharacterCatalog.Available())
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
        public void TheRosterHasMomoAndTheSporeling()
        {
            var all = CharacterCatalog.Available();
            Assert.AreEqual(2, all.Length);
            Assert.AreEqual("momo", all[0].Id);
            Assert.AreEqual("sporeling", all[1].Id);
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
            Assert.AreEqual(3, iron.PowerUps.Count);
            Assert.AreEqual(6, gold.PowerUps.Count);
            Assert.That(iron.Coins, Is.InRange(100, 1000));
            Assert.That(gold.Coins, Is.InRange(300, 3000));
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
