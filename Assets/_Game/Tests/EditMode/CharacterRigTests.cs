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

        [SetUp]
        public void SetUp()
        {
            GearCatalog.ArtProbe = d => true;
        }

        [TearDown]
        public void TearDown()
        {
            GearCatalog.ArtProbe = null;
            CharacterCatalog.TestOverride = false;
        }

        static ProgressService WearingAllTestPieces()
        {
            var svc = new ProgressService(Content.ContentRepository.LoadFromResources(), new MemorySaveStore(), 5);
            foreach (GearDef t in GearCatalog.TestPieces)
            {
                svc.GrantGear(t.Id);
                Assert.IsTrue(svc.Equip(t.Id));
            }
            return svc;
        }

        static GearDef Piece(GearSlot slot, GearRarity rarity, PowerUp boost)
        {
            foreach (GearDef d in GearCatalog.Pieces(slot, rarity)) if (d.Boost == boost) return d;
            Assert.Fail("no such piece");
            return null;
        }

        static List<PartDraw> Draws(CharacterDef who, ProgressService svc)
        {
            return PartsAssembler.Build(who, null, slot => svc.EquippedDef(slot));
        }

        static int IndexOf(List<PartDraw> l, string name)
        {
            for (int i = 0; i < l.Count; i++) if (l[i].Name == name) return i;
            Assert.Fail("no part " + name);
            return -1;
        }

        // ---- parts: the character and the gear stacked back to front ----

        [Test]
        public void EveryCharacterHasItsNineParts()
        {
            CharacterCatalog.TestOverride = true;
            foreach (CharacterDef c in CharacterCatalog.Available())
            {
                Assert.AreEqual(9, c.Rig.Order.Length, c.Id);
                foreach (string part in c.Rig.Order) Assert.IsNotNull(c.PartSprite(part), c.Id + "/" + part);
            }
            Assert.AreEqual(4, CharacterCatalog.Available().Length);
        }

        [Test]
        public void GearPartsSitRightAboveTheCharacterPartTheyCover()
        {
            ProgressService svc = WearingAllTestPieces();
            List<PartDraw> l = Draws(CharacterCatalog.Momo, svc);
            Assert.AreEqual(9 + 2 + 3 + 2 + 1, l.Count, "nine character parts plus the parts of the four pieces");
            Assert.Less(IndexOf(l, "torso"), IndexOf(l, "Pants_legL"), "the pants band goes over the torso");
            Assert.Less(IndexOf(l, "Pants_legL"), IndexOf(l, "Body_torso"));
            Assert.Less(IndexOf(l, "torso"), IndexOf(l, "armL"));
            Assert.AreEqual(IndexOf(l, "footL") + 1, IndexOf(l, "Boots_footL"), "boots right above the foot");
            Assert.AreEqual(IndexOf(l, "armL") + 1, IndexOf(l, "Body_armL"), "the sleeve right above the arm");
            Assert.AreEqual(IndexOf(l, "head") + 1, IndexOf(l, "Head_head"), "the hat right above the head");
            Assert.AreEqual(l.Count - 1, IndexOf(l, "Head_head"), "the hat is the last thing drawn");
            Assert.Less(IndexOf(l, "back"), IndexOf(l, "legL"));
        }

        [Test]
        public void OnMomoEveryPartIsDrawnOnTheWholeCanvasUntouched()
        {
            // Momo is the reference: no fit, so every character and gear part is drawn on exactly the canvas rect
            foreach (PartDraw d in Draws(CharacterCatalog.Momo, WearingAllTestPieces()))
            {
                Assert.AreEqual(Vector2.zero, d.Min, d.Name);
                Assert.AreEqual(Vector2.one, d.Max, d.Name);
            }
        }

        [Test]
        public void TheDollIsTheSameListThePartsAssemblerGives()
        {
            var root = new GameObject("root", typeof(RectTransform));
            try
            {
                CharacterCatalog.TestOverride = true;
                foreach (CharacterDef who in CharacterCatalog.Available())
                {
                    ProgressService svc = WearingAllTestPieces();
                    RectTransform doll = MomoDoll.Build(root.transform, "Doll", svc, 400f, who, null);
                    List<PartDraw> l = Draws(who, svc);
                    Assert.AreEqual(l.Count, doll.childCount, who.Id);
                    for (int i = 0; i < l.Count; i++)
                    {
                        var rt = (RectTransform)doll.GetChild(i);
                        Assert.AreEqual(l[i].Min, rt.anchorMin, who.Id + " " + l[i].Name);
                        Assert.AreEqual(l[i].Max, rt.anchorMax, who.Id + " " + l[i].Name);
                        Assert.IsTrue(rt.name.EndsWith(l[i].Name), rt.name);
                    }
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void TheSameGearIsFittedToEachBodyThroughItsRig()
        {
            ProgressService svc = WearingAllTestPieces();
            foreach (CharacterDef c in new[] { CharacterCatalog.Sporeling, CharacterCatalog.Moth, CharacterCatalog.Rootling })
            {
                SlotFit head = c.Rig.Fits.head;
                Assert.Greater(head.scale, 0f, c.Id + " has a head fit");
                List<PartDraw> l = Draws(c, svc);
                PartDraw hat = l[IndexOf(l, "Head_head")];
                Assert.AreEqual(head.scale, hat.Max.x - hat.Min.x, 0.0005f, c.Id + " hat width fraction is the fit scale");
                Assert.AreEqual(head.scale, hat.Max.y - hat.Min.y, 0.0005f, c.Id + " hat height fraction is the fit scale");
                SlotFit body = c.Rig.Fits.body;
                PartDraw sleeve = l[IndexOf(l, "Body_armL")];
                Assert.AreEqual(body.scale * body.parts.armL.scale, sleeve.Max.x - sleeve.Min.x, 0.0005f, c.Id + " the sleeve has its own fit on top of the jacket fit");
                PartDraw torso = l[IndexOf(l, "Body_torso")];
                Assert.AreEqual(body.scale, torso.Max.x - torso.Min.x, 0.0005f);
                // the character parts themselves are never moved
                PartDraw leg = l[IndexOf(l, "legL")];
                Assert.AreEqual(Vector2.one, leg.Max);
            }
        }

        [Test]
        public void TheRunPlacesAPartExactlyWhereTheFractionsSay()
        {
            // the run draws a part as a child of the player transform (unit = the canvas): its world bounds must be the rect the rig gives
            var tex = new Texture2D(8, 8);
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 0.5f);   // 16 x 16 units, centre pivot
            var player = new GameObject("player");
            var child = new GameObject("part", typeof(SpriteRenderer));
            try
            {
                child.GetComponent<SpriteRenderer>().sprite = sp;
                child.transform.SetParent(player.transform, false);
                player.transform.localScale = new Vector3(-0.5f, 0.25f, 1f);   // scaled and facing left like in the run
                player.transform.position = new Vector3(3f, 4f, 0f);
                Vector2 canvas = sp.bounds.size;
                var d = new PartDraw { Sprite = sp, Min = new Vector2(0.1f, 0.2f), Max = new Vector2(0.6f, 0.9f) };
                Vector3 pos, scale;
                d.LocalTransform(canvas, out pos, out scale);
                child.transform.localScale = scale;
                child.transform.localPosition = pos;
                Bounds b = child.GetComponent<SpriteRenderer>().bounds;
                Vector3 pc = player.transform.position;
                float cw = canvas.x * 0.5f, ch = canvas.y * 0.25f;
                Assert.AreEqual(0.5f * cw, b.size.x, 0.001f);
                Assert.AreEqual(0.7f * ch, b.size.y, 0.001f);
                Assert.AreEqual(pc.y + ((0.2f + 0.9f) * 0.5f - 0.5f) * ch, b.center.y, 0.001f);
                Assert.AreEqual(pc.x - ((0.1f + 0.6f) * 0.5f - 0.5f) * cw, b.center.x, 0.001f, "mirrored with the player");
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(child);
                Object.DestroyImmediate(sp);
                Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void ProductionShowsOnlyMomo()
        {
            CharacterCatalog.TestOverride = false;
            Assert.AreEqual(1, CharacterCatalog.Available().Length);
            Assert.AreSame(CharacterCatalog.Momo, CharacterCatalog.Selected);
            CharacterCatalog.TestOverride = true;
            Assert.AreEqual(4, CharacterCatalog.Available().Length);
            Assert.IsFalse(CharacterCatalog.Sporeling.Production);
            Assert.IsTrue(CharacterCatalog.Momo.Production);
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
                if (icon) { with++; Assert.IsNotNull(GearParts.Find(d), d.Id + " has an icon, so it also has worn art"); }
            }
            Assert.Greater(with, 0, "some pieces are painted");
            Assert.Less(with, 240, "not every piece is painted yet");
        }

        [Test]
        public void TheFourMannequinTestPiecesAreOutsideTheCatalogAndHaveRealArt()
        {
            GearCatalog.ArtProbe = null;
            Assert.AreEqual(240, GearCatalog.All.Count);
            Assert.AreEqual(4, GearCatalog.TestPieces.Count);
            foreach (GearDef t in GearCatalog.TestPieces)
            {
                Assert.AreSame(t, GearCatalog.Get(t.Id));
                Assert.IsTrue(GearCatalog.IsTestPiece(t));
                Assert.IsFalse(new List<GearDef>(GearCatalog.All).Contains(t));
                Assert.IsTrue(GearCatalog.HasArt(t), t.Id + " icon");
                Assert.IsNotNull(GearParts.Find(t), t.Id + " parts");
                foreach (GearDef d in GearCatalog.Pieces(t.Slot, t.Rarity)) Assert.AreNotEqual(t.Id, d.Id);
            }
        }

        // ---- info screen numbers ----

        [Test]
        public void TheInfoRowsShowWhatTheRunConfigGives()
        {
            ProgressService svc = WearingAllTestPieces();
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
