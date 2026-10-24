using System.Collections.Generic;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using BoingUpSkyTower.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace BoingUpSkyTower.Tests
{
    /// <summary>The polygon rig, the draw order, the single picture fallback, the roster and the info screen numbers (docs/19).</summary>
    public class CharacterRigTests
    {
        static readonly List<Object> made = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            GearCatalog.ArtProbe = d => true;
            GearParts.ClearCache();
            GearParts.Override = FakeGearParts;
        }

        [TearDown]
        public void TearDown()
        {
            GearCatalog.ArtProbe = null;
            GearParts.Override = null;
            GearParts.ClearCache();
            CharacterCatalog.TestOverride = false;
            foreach (Object o in made) if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        static Sprite Pic(string name, int w = 8, int h = 10)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name };
            made.Add(tex);
            Sprite s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            s.name = name;
            made.Add(s);
            return s;
        }

        static List<GearPartEntry> FakeGearParts(GearDef d)
        {
            var list = new List<GearPartEntry>();
            foreach (string p in GearCatalog.PartNames(d.Slot)) list.Add(new GearPartEntry { Part = p, Sprite = Pic(d.Id + "_" + p) });
            return list;
        }

        static CharacterDef Fake(string id, params string[] missing)
        {
            var c = new CharacterDef { Id = id, Name = id, Production = true };
            var skip = new HashSet<string>(missing);
            c.PartLoader = part => skip.Contains(part) ? null : Pic(id + "_" + part);
            return c;
        }

        static ProgressService NewService()
        {
            return new ProgressService(Content.ContentRepository.LoadFromResources(), new MemorySaveStore(), 5);
        }

        static ProgressService WearingOneOfEach()
        {
            ProgressService svc = NewService();
            foreach (string id in new[] { "gear_head_c01", "gear_body_u01", "gear_boots_r01" })
            {
                svc.GrantGear(id);
                Assert.IsTrue(svc.Equip(id), id);
            }
            return svc;
        }

        static List<string> Names(List<PartDraw> l)
        {
            var n = new List<string>();
            foreach (PartDraw d in l) n.Add(d.Name);
            return n;
        }

        // ---- the rig ----

        [Test]
        public void RigJsonGivesTheCanvasAndThePartOrderAndDefaultsOtherwise()
        {
            CharacterRig def = CharacterRig.FromJson(null);
            Assert.AreEqual(1365f, def.CanvasW);
            Assert.AreEqual(1649f, def.CanvasH);
            CollectionAssert.AreEqual(new[] { "back", "legL", "legR", "body", "handL", "handR", "head" }, def.Order);
            CharacterRig r = CharacterRig.FromJson("{\"id\":\"x\",\"canvas\":[800,1000],\"order\":[\"legL\",\"legR\",\"body\",\"handL\",\"handR\",\"head\"]}");
            Assert.AreEqual(800f, r.CanvasW);
            Assert.AreEqual(1000f, r.CanvasH);
            Assert.AreEqual(6, r.Order.Length);
            Assert.AreEqual(-1, r.IndexOf("back"));
            CharacterRig bad = CharacterRig.FromJson("{\"canvas\":[0,0]}");
            Assert.AreEqual(1365f, bad.CanvasW);
        }

        [Test]
        public void TheDrawOrderIsBackLegsBootsBodyBodyGearHandsSleevesHeadHeadGear()
        {
            CharacterDef who = Fake("full");
            Assert.IsTrue(who.HasParts);
            ProgressService svc = WearingOneOfEach();
            List<PartDraw> draws = PartsAssembler.Build(who, null, slot => svc.EquippedDef(slot));
            CollectionAssert.AreEqual(new[]
            {
                "back", "legL", "legR", "Boots_bootL", "Boots_bootR", "body", "Body_body", "handL", "handR", "Body_sleeveL", "Body_sleeveR", "head", "Head_head",
            }, Names(draws));
            for (int i = 1; i < draws.Count; i++) Assert.Less(draws[i - 1].Z, draws[i].Z);
            foreach (PartDraw d in draws) Assert.AreEqual(d.Name.Contains("_"), d.Gear, d.Name);
        }

        [Test]
        public void WithoutWornGearAndWithoutABackOnlyTheCharacterPartsAreDrawn()
        {
            CharacterDef who = Fake("nobackpack", "back");
            Assert.IsTrue(who.HasParts, "back is optional");
            List<PartDraw> draws = PartsAssembler.Build(who, null, null);
            CollectionAssert.AreEqual(new[] { "legL", "legR", "body", "handL", "handR", "head" }, Names(draws));
            List<PartDraw> none = PartsAssembler.Build(who, null, slot => null);
            Assert.AreEqual(6, none.Count);
        }

        [Test]
        public void BodyGearWithoutSleevesStillDrawsItsBody()
        {
            GearParts.Override = d => new List<GearPartEntry> { new GearPartEntry { Part = GearCatalog.PartNames(d.Slot)[0], Sprite = Pic(d.Id) } };
            CharacterDef who = Fake("full");
            ProgressService svc = NewService();
            svc.GrantGear("gear_body_c02");
            svc.Equip("gear_body_c02");
            List<string> names = Names(PartsAssembler.Build(who, null, slot => svc.EquippedDef(slot)));
            CollectionAssert.Contains(names, "Body_body");
            CollectionAssert.DoesNotContain(names, "Body_sleeveL");
        }

        [Test]
        public void ACharacterWithoutItsPartsIsOnePictureAndGearDrawsNothing()
        {
            CharacterDef partial = Fake("partial", "handL");   // one required part missing
            Assert.IsFalse(partial.HasParts);
            ProgressService svc = WearingOneOfEach();
            Assert.IsNotNull(svc.EquippedDef(GearSlot.Body), "gear is worn");
            var art = ScriptableObject.CreateInstance<ArtCatalog>();
            made.Add(art);
            art.Player = Pic("momo_picture");
            List<PartDraw> draws = PartsAssembler.Build(partial, art, slot => svc.EquippedDef(slot));
            Assert.AreEqual(1, draws.Count, "his single picture, no gear");
            Assert.AreSame(art.Player, draws[0].Sprite);
            Assert.IsFalse(draws[0].Gear);
        }

        [Test]
        public void MissingPictureAndMissingArtNeverCrash()
        {
            CharacterDef none = Fake("none", "legL", "legR", "body", "handL", "handR", "head", "back");
            Assert.IsFalse(none.HasParts);
            Assert.AreEqual(0, PartsAssembler.Build(none, null, null).Count);
            Assert.AreEqual(0, PartsAssembler.Build(none, null, slot => NewService().EquippedDef(slot)).Count);
        }

        [Test]
        public void AFullCanvasPictureOfAnotherSizeIsScaledToTheCanvas()
        {
            var d = new PartDraw { Sprite = Pic("a", 8, 10) };
            Vector2 canvas = new Vector2(0.08f, 0.10f);   // 8 x 10 px at 100 ppu
            Vector3 same = d.LocalScale(canvas);
            Assert.AreEqual(1f, same.x, 0.0001f);
            Assert.AreEqual(1f, same.y, 0.0001f);
            var big = new PartDraw { Sprite = Pic("b", 16, 20) };
            Vector3 half = big.LocalScale(canvas);
            Assert.AreEqual(0.5f, half.x, 0.0001f);
            Assert.AreEqual(0.5f, half.y, 0.0001f);
        }

        [Test]
        public void TheSameGearFitsEveryCharacterBecauseAllPartsAreFullCanvas()
        {
            ProgressService svc = WearingOneOfEach();
            List<PartDraw> a = PartsAssembler.Build(Fake("a"), null, slot => svc.EquippedDef(slot));
            List<PartDraw> b = PartsAssembler.Build(Fake("b"), null, slot => svc.EquippedDef(slot));
            CollectionAssert.AreEqual(Names(a), Names(b));
        }

        // ---- roster ----

        [Test]
        public void ProductionShowsOnlyMomoAndDevelopmentAddsTheTestCharacter()
        {
            CharacterCatalog.TestOverride = false;
            Assert.AreEqual(1, CharacterCatalog.Available().Length);
            Assert.AreSame(CharacterCatalog.Momo, CharacterCatalog.Selected);
            CharacterCatalog.TestOverride = true;
            CharacterDef[] all = CharacterCatalog.Available();
            Assert.AreEqual(2, all.Length);
            Assert.AreSame(CharacterCatalog.Momo, all[0]);
            Assert.AreSame(CharacterCatalog.Sporeling, all[1]);
            Assert.IsFalse(CharacterCatalog.Sporeling.Production);
            Assert.IsTrue(CharacterCatalog.Momo.Production);
            Assert.IsTrue(CharacterCatalog.Momo.Selectable, "Momo always works, with or without his parts");
        }

        [Test]
        public void ATestCharacterWithoutPartsIsListedButLockedAndNeverSelected()
        {
            CharacterCatalog.TestOverride = true;
            var locked = Fake("locked", "head");
            locked.Production = false;
            Assert.IsFalse(locked.Selectable);
            var ready = Fake("ready");
            ready.Production = false;
            Assert.IsTrue(ready.Selectable);
            if (!CharacterCatalog.Sporeling.HasParts)
            {
                CharacterCatalog.Selected = CharacterCatalog.Sporeling;
                Assert.AreSame(CharacterCatalog.Momo, CharacterCatalog.Selected, "no parts: falls back to Momo, nothing breaks");
            }
            CharacterCatalog.Selected = CharacterCatalog.Momo;
        }

        // ---- gear tile pictures ----

        [Test]
        public void APieceWithoutArtHasNoTilePicture()
        {
            GearCatalog.ArtProbe = null;
            GearParts.Override = null;
            GearIcons.Override = null;
            GearDef d = GearCatalog.Get("gear_head_c01");
            if (!GearCatalog.HasArt(d)) Assert.IsNull(GearIcons.Find(d));   // never a stand-in glyph
        }

        // ---- info screen numbers ----

        [Test]
        public void TheInfoRowsShowWhatTheRunConfigGives()
        {
            ProgressService svc = WearingOneOfEach();
            GearDef flight = null;
            foreach (GearDef d in GearCatalog.Pieces(GearSlot.Body, GearRarity.Epic)) if (d.Boost == PowerUp.Flight) { flight = d; break; }
            svc.GrantGear(flight.Id);
            svc.Equip(flight.Id);
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
            InfoRow row = rows.Find(x => x.Type == PowerUp.Flight);
            Assert.AreEqual(baseCfg.FlightTicks / 60f, row.Before, 0.0001f);
            Assert.AreEqual(run.FlightTicks / 60f, row.After, 0.0001f);
            Assert.AreEqual(75, row.Percent);
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

            GearDef piece = null;
            foreach (GearDef d in GearCatalog.Pieces(GearSlot.Body, GearRarity.Legendary)) if (d.Boost == PowerUp.Flight) { piece = d; break; }   // +100 percent
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
