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
                    // The parts are drawn by normalised size, so the pixel size may be scaled down (hero parts are capped at 1024 px, APK size): the canvas SHAPE must still match.
                    Assert.AreEqual(1365f / 1649f, (float)d.Sprite.texture.width / d.Sprite.texture.height, 0.005f, c.Id + " " + d.Name);
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
            Assert.AreEqual(25, all.Length, "owner 2026-11-09: only the 25 ACTIVE heroes exist (5 Common heroes + 20 merge tiers); the inactive 100 are data only");
            // order is hero then tier (docs/36): each Common hero is followed by its tiers; Region 1 first
            string[] expected =
            {
                "momo", "momo_u", "momo_r", "momo_e", "momo_l",
                "forestgirl", "forestgirl_u", "forestgirl_r", "forestgirl_e", "forestgirl_l",
                "beachotter", "beachotter_u", "beachotter_r", "beachotter_e", "beachotter_l",
                "candygirl", "candygirl_u", "candygirl_r", "candygirl_e", "candygirl_l",
                "monk", "monk_u", "monk_r", "monk_e", "monk_l"
            };
            Assert.AreEqual(expected.Length, all.Length);
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
        public void ChestsGiveCoinsPowerUpsAndOneHero()
        {
            var types = new System.Collections.Generic.List<PowerUpDef>(new ProgressService(Content.ContentRepository.LoadFromResources(), new MemorySaveStore(), 5).PowerUpDefs.Implemented);
            ChestContents iron = ChestRolls.Roll(ChestKind.Iron, 0, types);
            ChestContents gold = ChestRolls.Roll(ChestKind.Golden, 0, types);
            Assert.AreEqual(1, iron.PowerUps.Count);
            Assert.AreEqual(3, gold.PowerUps.Count);
            Assert.That(iron.Coins, Is.InRange(800, 1200));
            Assert.That(gold.Coins, Is.InRange(3000, 4200));
            Assert.IsNotNull(iron.Hero);
            Assert.IsNotNull(gold.Hero);
        }

        [Test]
        public void AChestHeroIsAnotherCopyAndNoGemsComeBack()
        {
            var svc = new ProgressService(Content.ContentRepository.LoadFromResources(), new MemorySaveStore(), 5);
            Assert.IsTrue(svc.OwnsCharacter("momo"));
            Assert.IsFalse(svc.OwnsCharacter("forestgirl"), "region 2 is still locked");
            svc.Data.Gems = 1000;
            System.Collections.Generic.List<ChestContents> opened;
            Assert.AreEqual(ChestOpenResult.Ok, svc.TryOpenChests(ChestKind.Iron, 1, out opened));
            Assert.IsNotNull(opened[0].Hero);
            Assert.AreEqual(1000 - ChestRolls.Price(ChestKind.Iron, 1), svc.Gems, "no refund");
            Assert.IsFalse(svc.OwnsCharacter("forestgirl"), "a locked region's hero never drops");
            Assert.That(opened[0].Coins, Is.InRange(800, 1200));
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
