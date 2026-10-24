using System.Collections.Generic;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using BoingUpSkyTower.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace BoingUpSkyTower.Tests
{
    /// <summary>The character rig, the doll, the real gear icons, the art filter and the info screen numbers (docs/18).</summary>
    public class CharacterRigTests
    {
        static readonly GearSlot[] Slots = { GearSlot.Head, GearSlot.Body, GearSlot.Pants, GearSlot.Boots };

        Texture2D tex;
        Sprite sprite;
        System.Func<GearDef, Sprite> oldLayer;

        [SetUp]
        public void SetUp()
        {
            GearCatalog.ArtProbe = d => true;
            tex = new Texture2D(8, 8);
            sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f));
            oldLayer = MomoDoll.LayerFor;
            MomoDoll.LayerFor = d => sprite;
        }

        [TearDown]
        public void TearDown()
        {
            MomoDoll.LayerFor = oldLayer;
            GearCatalog.ArtProbe = null;
            CharacterCatalog.TestOverride = false;
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(tex);
        }

        static ProgressService WearingAll()
        {
            var svc = new ProgressService(Content.ContentRepository.LoadFromResources(), new MemorySaveStore(), 5);
            foreach (GearSlot s in Slots)
            {
                GearDef d = GearCatalog.Pieces(s, GearRarity.Rare)[0];
                svc.GrantGear(d.Id);
                svc.Equip(d.Id);
            }
            return svc;
        }

        static GearDef Piece(GearSlot slot, GearRarity rarity, PowerUp boost)
        {
            foreach (GearDef d in GearCatalog.Pieces(slot, rarity)) if (d.Boost == boost) return d;
            Assert.Fail("no such piece");
            return null;
        }

        // ---- doll: the same composite as the run ----

        [Test]
        public void EveryWornLayerOfMomoHasExactlyTheBodyRect()
        {
            var root = new GameObject("root", typeof(RectTransform));
            try
            {
                RectTransform doll = MomoDoll.Build(root.transform, "Doll", sprite, WearingAll(), 400f, CharacterCatalog.Momo);
                var body = (RectTransform)doll.Find("Momo");
                int layers = 0;
                for (int i = 0; i < doll.childCount; i++)
                {
                    var rt = (RectTransform)doll.GetChild(i);
                    if (!rt.name.StartsWith("Layer")) continue;
                    layers++;
                    Assert.AreEqual(body.rect.size.x, rt.rect.size.x, 0.01f, rt.name + " width");
                    Assert.AreEqual(body.rect.size.y, rt.rect.size.y, 0.01f, rt.name + " height");
                    Assert.AreEqual(body.rect.position.x, rt.rect.position.x, 0.01f, rt.name + " x");
                    Assert.AreEqual(body.rect.position.y, rt.rect.position.y, 0.01f, rt.name + " y");
                }
                Assert.AreEqual(4, layers);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ASecondBodyWearsTheSameLayersThroughItsRig()
        {
            var root = new GameObject("root", typeof(RectTransform));
            try
            {
                CharacterRig rig = CharacterCatalog.Pip.Rig;
                Assert.AreNotEqual(1f, rig.Head.sx, "Pip is a different body");
                RectTransform doll = MomoDoll.Build(root.transform, "Doll", sprite, WearingAll(), 400f, CharacterCatalog.Pip);
                var body = (RectTransform)doll.Find("Momo");
                var head = (RectTransform)doll.Find("Layer_Head");
                var legs = (RectTransform)doll.Find("Layer_Pants");
                Assert.AreEqual(body.rect.width * rig.Head.sx, head.rect.width, 0.05f);
                Assert.AreEqual(body.rect.height * rig.Head.sy, head.rect.height, 0.05f);
                Assert.AreEqual(body.rect.width * rig.Legs.sx, legs.rect.width, 0.05f);
                Assert.IsTrue(Mathf.Abs(head.rect.height - legs.rect.height) > 0.01f);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void TheRunScalesAWornLayerToThePlayerSpriteBoundsWhateverItsPixelsPerUnit()
        {
            // the body is imported at 50 ppu, the layer (painted on the same canvas) at 100 ppu, the player is scaled and flipped like in the run
            var bt = new Texture2D(100, 200);
            var lt = new Texture2D(400, 800);
            Sprite body = Sprite.Create(bt, new Rect(0, 0, 100, 200), new Vector2(0.5f, 0.5f), 50f);
            Sprite layer = Sprite.Create(lt, new Rect(0, 0, 400, 800), new Vector2(0.25f, 0.6f), 100f);   // off-centre pivot on purpose
            var player = new GameObject("player", typeof(SpriteRenderer));
            var child = new GameObject("worn", typeof(SpriteRenderer));
            try
            {
                player.GetComponent<SpriteRenderer>().sprite = body;
                player.transform.localScale = new Vector3(-0.9f, 1.3f, 1f);
                player.transform.position = new Vector3(3f, 4f, 0f);
                child.transform.SetParent(player.transform, false);
                child.GetComponent<SpriteRenderer>().sprite = layer;
                foreach (GearSlot slot in Slots)
                {
                    CharacterRig.Identity.PlaceSprite(child.transform, layer, body, slot);
                    Bounds a = player.GetComponent<SpriteRenderer>().bounds, b = child.GetComponent<SpriteRenderer>().bounds;
                    Assert.AreEqual(a.size.x, b.size.x, 0.001f, slot + " width");
                    Assert.AreEqual(a.size.y, b.size.y, 0.001f, slot + " height");
                    Assert.AreEqual(a.center.x, b.center.x, 0.001f, slot + " centre x");
                    Assert.AreEqual(a.center.y, b.center.y, 0.001f, slot + " centre y");
                }
                // a rig with other proportions changes the layer the same way as on the doll
                CharacterRig pip = CharacterCatalog.Pip.Rig;
                pip.PlaceSprite(child.transform, layer, body, GearSlot.Head);
                Bounds pa = player.GetComponent<SpriteRenderer>().bounds, pb = child.GetComponent<SpriteRenderer>().bounds;
                Assert.AreEqual(pa.size.x * pip.Head.sx, pb.size.x, 0.001f);
                Assert.AreEqual(pa.size.y * pip.Head.sy, pb.size.y, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(child);
                Object.DestroyImmediate(body);
                Object.DestroyImmediate(layer);
                Object.DestroyImmediate(bt);
                Object.DestroyImmediate(lt);
            }
        }

        [Test]
        public void MomoIsTheIdentityRigAndProductionShowsOnlyMomo()
        {
            foreach (GearSlot s in Slots)
            {
                Vector2 min, max;
                CharacterCatalog.Momo.Rig.NormalizedRect(s, out min, out max);
                Assert.AreEqual(Vector2.zero, min);
                Assert.AreEqual(Vector2.one, max);
            }
            CharacterCatalog.TestOverride = false;
            Assert.AreEqual(1, CharacterCatalog.Available().Length);
            Assert.AreSame(CharacterCatalog.Momo, CharacterCatalog.Selected);
            CharacterCatalog.TestOverride = true;
            Assert.AreEqual(2, CharacterCatalog.Available().Length);
            Assert.IsFalse(CharacterCatalog.Pip.Production);
        }

        // ---- art: pieces without an icon never drop or show ----

        [Test]
        public void OnlyPiecesWithArtDropOrAppearInChests()
        {
            GearCatalog.ArtProbe = d => d.Rarity >= GearRarity.Rare && d.Number % 2 == 1;
            int dropped = 0;
            for (int i = 0; i < 3000; i++)
            {
                foreach (string id in GearDrops.Roll(15, 100 + i, i))
                {
                    Assert.IsTrue(GearCatalog.HasArt(GearCatalog.Get(id)), id);
                    dropped++;
                }
            }
            Assert.Greater(dropped, 100);
            var types = new List<Economy.PowerUpDef>(PowerUpCatalog.BuiltIn().Implemented);
            for (int i = 0; i < 500; i++)
            {
                foreach (string id in ChestRolls.Roll(ChestKind.Iron, i, types).Gear) Assert.IsTrue(GearCatalog.HasArt(GearCatalog.Get(id)), id);
                foreach (string id in ChestRolls.Roll(ChestKind.Golden, i, types).Gear) Assert.IsTrue(GearCatalog.HasArt(GearCatalog.Get(id)), id);
            }
            Assert.AreEqual(240, GearCatalog.All.Count, "all definitions are kept");
        }

        [Test]
        public void TheShippedIconsDecideWhichPiecesHaveArt()
        {
            GearCatalog.ArtProbe = null;   // the real check: an icon in Resources/Art/GearIcons
            int with = 0;
            foreach (GearDef d in GearCatalog.All)
            {
                bool icon = GearIcons.Find(d) != null;
                Assert.AreEqual(icon, GearCatalog.HasArt(d), d.Id);
                if (icon) { with++; Assert.IsNotNull(GearLayers.Find(d), d.Id + " has an icon, so it also has a worn layer"); }
            }
            Assert.Greater(with, 0, "some pieces are painted");
            Assert.Less(with, 240, "not every piece is painted yet");
        }

        // ---- info screen numbers ----

        [Test]
        public void TheInfoRowsShowWhatTheRunConfigGives()
        {
            ProgressService svc = WearingAll();
            svc.GrantGear(Piece(GearSlot.Body, GearRarity.Epic, PowerUp.Flight).Id);
            svc.Equip(Piece(GearSlot.Body, GearRarity.Epic, PowerUp.Flight).Id);
            var baseCfg = new SimConfig();
            SimConfig run = svc.BuildRunConfig(baseCfg);
            List<InfoRow> rows = GearInfo.Rows(svc, baseCfg);
            Assert.AreEqual(7, rows.Count, "all seven power-ups");
            foreach (InfoRow r in rows)
            {
                bool sec;
                Assert.AreEqual(GearInfo.Value(baseCfg, r.Type, out sec), r.Before, 0.0001f, r.Type + " before");
                Assert.AreEqual(GearInfo.Value(run, r.Type, out sec), r.After, 0.0001f, r.Type + " after");
                Assert.AreEqual(svc.GearBoostPercents()[(int)r.Type], r.Percent, r.Type.ToString());
                if (r.Percent > 0) Assert.Greater(r.After, r.Before, r.Type + " is boosted");
                else Assert.AreEqual(r.Before, r.After, r.Type + " unchanged");
                foreach (GearDef d in r.Pieces) Assert.AreEqual(r.Type, d.Boost);
            }
            InfoRow flight = rows.Find(x => x.Type == PowerUp.Flight);
            Assert.AreEqual(baseCfg.FlightTicks / 60f, flight.Before, 0.0001f);
            Assert.AreEqual(run.FlightTicks / 60f, flight.After, 0.0001f);
            Assert.AreEqual(75, flight.Percent);
            Assert.AreEqual("4.0 s", GearInfo.Format(4f, true));
        }

        [Test]
        public void EquippingAFlightPieceRaisesTheFlightOfTheRunSession()
        {
            GameSession.InitForTests(new MemorySaveStore());
            ProgressService pr = GameSession.Progress;
            pr.GrantPowerUp("pw_flight", 2);
            pr.SetLoadout(new List<string> { "pw_flight" });
            SimConfig before = GameSession.BuildRunCfg();
            Assert.AreEqual(GameSession.Cfg.FlightTicks, before.FlightTicks, "nothing worn, nothing changes");

            GearDef piece = Piece(GearSlot.Body, GearRarity.Legendary, PowerUp.Flight);   // +100 percent
            pr.GrantGear(piece.Id);
            Assert.IsTrue(pr.Equip(piece.Id));
            SimConfig after = GameSession.BuildRunCfg();
            Assert.AreEqual(GameSession.Cfg.FlightTicks * 2, after.FlightTicks);

            var plain = new RunSession(GameSession.Repo, before, "lvl_001_01");
            var boosted = new RunSession(GameSession.Repo, GameSession.BuildRunCfg(), "lvl_001_01");
            Assert.AreEqual(after.FlightTicks, boosted.Cfg.FlightTicks, "the session uses the boosted config");
            plain.ApplyLoadout(pr.BuildRunLoadout());
            boosted.ApplyLoadout(pr.BuildRunLoadout());
            plain.Step(SimInput.PowerSlot0);
            boosted.Step(SimInput.PowerSlot0);
            Assert.Greater(plain.State.FlightTicks, 0, "flight started");
            Assert.Greater(boosted.State.FlightTicks, plain.State.FlightTicks + GameSession.Cfg.FlightTicks / 2, "the boosted flight lasts longer");
        }
    }
}
