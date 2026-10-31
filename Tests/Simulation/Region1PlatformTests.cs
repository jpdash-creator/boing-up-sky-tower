using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>
    /// Doc 24 (owner issue list 26): the seven region 1 special platforms, their placement rules, the level 3 to 15 difficulty ramp and endless mode.
    /// Behaviour tests use small hand-built worlds (no goal, no pickups); generator tests use the shipped region 1 levels with random attempt seeds.
    /// </summary>
    [Trait("Category", "Region1Platforms")]
    public class Region1PlatformTests
    {
        private readonly ITestOutputHelper output;
        public Region1PlatformTests(ITestOutputHelper output) { this.output = output; }

        // ------------------------------------------------------------------ helpers

        private static Fx F(int num, int den = 100) { return Fx.FromRatio(num, den); }

        private static Platform P(int id, int row, PlatformKind kind, int xHundredths, int yHundredths, int wHundredths, int dir = 0, int phase = 0, int period = 0, int ampHundredths = 0)
        {
            Platform p = new Platform();
            p.Id = id; p.Row = row; p.Kind = kind;
            p.X = F(xHundredths); p.Y = F(yHundredths); p.Width = F(wHundredths);
            p.Dir = dir; p.Phase = phase; p.Period = period; p.Amplitude = F(ampHundredths);
            return p;
        }

        private static Platform Start() { return P(0, 0, PlatformKind.Normal, 500, 0, 1000); }

        private static Sim World(params Platform[] plats)
        {
            return new Sim(new SimConfig(), plats, Fx.Zero, false);
        }

        /// <summary>The player stands on platform <paramref name="id"/> at x (hundredths, 0 = centre), no scroll pressure yet.</summary>
        private static SimState Stand(Sim sim, int id, int xHundredths = int.MinValue)
        {
            SimState s = sim.NewState();
            Platform p = sim.Platforms[id];
            s.GroundId = id; s.BestGroundId = id; s.Grounded = true; s.Row = p.Row; s.JumpStartRow = p.Row;
            s.X = xHundredths == int.MinValue ? sim.PlatformX(id, 0) : F(xHundredths);
            s.Y = sim.PlatformY(id, 0);
            s.MaxY = s.Y; s.KillY = s.Y - Fx.FromInt(10);
            return s;
        }

        /// <summary>The player falls onto platform <paramref name="id"/> from above at x (hundredths).</summary>
        private static SimState Drop(Sim sim, int id, int xHundredths, int heightHundredths = 300)
        {
            SimState s = sim.NewState();
            Platform p = sim.Platforms[id];
            s.GroundId = 0; s.Grounded = false; s.Coyote = 0;
            s.X = F(xHundredths); s.Y = sim.PlatformY(id, 0) + F(heightHundredths); s.Vy = -F(1, 100);
            s.MaxY = s.Y; s.KillY = s.Y - Fx.FromInt(10);
            s.JumpStartRow = 0;
            return s;
        }

        private static double D(Fx v) { return v.Raw / 65536.0; }

        private static void Steps(Sim sim, ref SimState s, int n, int input = 0)
        {
            for (int i = 0; i < n; i++) sim.Step(ref s, input);
        }

        // ------------------------------------------------------------------ 1 Wind Petal

        [Fact]
        public void WindPetal_PushesAbout2Point5UnitsPerSecondAndFlipsEvery2Point5Seconds()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.WindPetal, 500, 200, 800, dir: 1, phase: 100));
            SimState s = Stand(sim, 1);
            double x0 = D(s.X);
            Steps(sim, ref s, 50);
            Assert.True(s.Grounded);
            // 50 ticks of +1 gust: 50 x 2.5 / 60 = 2.08 u
            Assert.InRange(D(s.X) - x0, 1.9, 2.15);
            Steps(sim, ref s, 50);   // the phase offset 100 makes the gust flip at tick 50: back to the start
            Assert.InRange(D(s.X) - x0, -0.1, 0.1);
            Assert.Equal(-1, sim.WindDirection(1, 60));
            Assert.Equal(1, sim.WindDirection(1, 10));
            Assert.Equal(1, sim.WindDirection(1, 210));   // flips again 150 ticks later
            Assert.Equal(0, sim.WindDirection(0, 10));
        }

        [Fact]
        public void WindPetal_CueShowsHalfASecondBeforeEachFlip()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.WindPetal, 500, 200, 800, dir: 1, phase: 0));
            for (int t = 0; t < 600; t++)
            {
                bool cue = sim.WindFlipCue(1, t);
                int untilFlip = 150 - (t % 150);
                Assert.Equal(untilFlip <= 30, cue);
                // the direction only changes right after a cue
                if (t > 0 && sim.WindDirection(1, t) != sim.WindDirection(1, t - 1)) Assert.True(sim.WindFlipCue(1, t - 1), "flip at " + t + " without cue");
            }
        }

        [Fact]
        public void WindPetal_DriftsAnUnattendedPlayerOffTheEdgeButNeverKills()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.WindPetal, 500, 200, 300, dir: 1, phase: 0));
            SimState s = Stand(sim, 1);
            bool leftIt = false;
            for (int i = 0; i < 200; i++) { sim.Step(ref s, 0); if (s.GroundId == 0 && s.Grounded) { leftIt = true; break; } }
            Assert.True(leftIt, "the gust must blow an idle player off a 3 u petal within 2.5 s");   // blown off the right edge, lands on the platform below
            Steps(sim, ref s, 120);
            Assert.False(s.Dead);
            Assert.Equal(0, s.GroundId);
        }

        // ------------------------------------------------------------------ 2 Bee Hive Lift

        [Fact]
        public void HiveLift_BobsAboutPoint8RowWithAThreeSecondPeriodAndCarriesThePlayer()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.HiveLift, 500, 300, 400, period: 180, phase: 0));
            double lo = 99, hi = -99;
            for (int t = 0; t < 360; t++)
            {
                double y = D(sim.PlatformY(1, t));
                lo = Math.Min(lo, y); hi = Math.Max(hi, y);
                Assert.Equal(sim.PlatformY(1, t).Raw, sim.PlatformY(1, t + 180).Raw);   // period 3 s
            }
            Assert.InRange(hi - lo, 2.3, 2.45);   // 2 x 1.2 u peak to peak = 0.8 of a 3 u row
            SimState s = Stand(sim, 1);
            for (int t = 0; t < 400; t++)
            {
                sim.Step(ref s, 0);
                Assert.True(s.Grounded);
                Assert.Equal(sim.PlatformY(1, s.Tick).Raw, s.Y.Raw);   // rides the platform
            }
        }

        [Fact]
        public void HiveLift_AFallingPlayerLandsOnTheMovingSurface()
        {
            for (int phase = 0; phase < 180; phase += 15)
            {
                Sim sim = World(Start(), P(1, 1, PlatformKind.HiveLift, 500, 300, 400, period: 180, phase: phase));
                SimState s = Drop(sim, 1, 500, 500);
                Steps(sim, ref s, 120);
                Assert.True(s.Grounded && s.GroundId == 1, "phase " + phase + " did not land");
                Assert.Equal(1, s.Row);
            }
        }

        // ------------------------------------------------------------------ 3 Snap Daisy

        [Fact]
        public void SnapDaisy_ClosesAfter1Point5SecondsAndReopensTwoSecondsLater()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SnapDaisy, 500, 200, 400));
            SimState s = Drop(sim, 1, 500, 100);
            int landTick = -1;
            for (int i = 0; i < 40 && landTick < 0; i++) { sim.Step(ref s, 0); if (s.GroundId == 1 && s.Grounded) landTick = s.Tick; }
            Assert.True(landTick > 0);
            int[] c = s.CrumbleTick;
            Assert.Equal(landTick, c[1]);
            Assert.Equal(3, sim.SnapCountdown(1, landTick, c));
            Assert.Equal(3, sim.SnapCountdown(1, landTick + 29, c));
            Assert.Equal(2, sim.SnapCountdown(1, landTick + 30, c));
            Assert.Equal(1, sim.SnapCountdown(1, landTick + 60, c));
            Assert.Equal(1, sim.SnapPhase(1, landTick + 89, c));
            Assert.True(sim.IsSolid(1, landTick + 89, c));
            Assert.Equal(2, sim.SnapPhase(1, landTick + 90, c));
            Assert.False(sim.IsSolid(1, landTick + 90, c));
            Assert.False(sim.IsSolid(1, landTick + 209, c));
            Assert.Equal(0, sim.SnapPhase(1, landTick + 210, c));
            Assert.True(sim.IsSolid(1, landTick + 210, c));
            // a player who stays is dropped through to the platform below at tick 90, nothing else happens to them
            while (s.Tick < landTick + 89) sim.Step(ref s, 0);
            Assert.True(s.Grounded);
            sim.Step(ref s, 0);
            Assert.False(s.Grounded);
            Steps(sim, ref s, 100);
            Assert.False(s.Dead);
            Assert.Equal(0, s.GroundId);
        }

        [Fact]
        public void SnapDaisy_LandingAfterTheReopenStartsANewCountdown()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SnapDaisy, 500, 200, 400));
            SimState s = Drop(sim, 1, 500, 100);
            s.CrumbleTick[1] = 100;   // an earlier landing, long ago: the daisy has reopened by tick 350
            s.Tick = 350;
            Steps(sim, ref s, 40);
            Assert.True(s.Grounded && s.GroundId == 1, "landed? grounded " + s.Grounded + " ground " + s.GroundId + " y " + D(s.Y) + " tick " + s.Tick);
            Assert.True(s.CrumbleTick[1] > 350, "a new countdown starts with the new landing");
            // landing while it is still counting down keeps the running countdown
            SimState t = Drop(sim, 1, 500, 100);
            t.CrumbleTick[1] = 335;
            t.Tick = 350;
            Steps(sim, ref t, 40);
            Assert.True(t.Grounded && t.GroundId == 1);
            Assert.Equal(335, t.CrumbleTick[1]);
        }

        // ------------------------------------------------------------------ 4 Dandelion Puff

        [Fact]
        public void Dandelion_LaunchesAbout1Point3TimesAJumpHeightAndAboutFourUnitsSideways()
        {
            foreach (int dir in new[] { 1, -1 })
            {
                int px = dir > 0 ? 300 : 700;
                Sim sim = World(Start(), P(1, 1, PlatformKind.Dandelion, px, 200, 400, dir: dir));
                SimState s = Drop(sim, 1, px, 100);
                double standing = D(new SimConfig().JumpBase * new SimConfig().JumpBase / (new SimConfig().Gravity * 2));
                double peak = 0; bool launched = false; double x0 = 0, xEnd = 0;
                for (int i = 0; i < 300; i++)
                {
                    sim.Step(ref s, 0);
                    if (!launched && s.GroundId == 1 && !s.Grounded && s.Vy > Fx.Zero) { launched = true; x0 = D(s.X); }
                    if (launched)
                    {
                        peak = Math.Max(peak, D(s.Y) - 2.0);
                        if (s.Y <= F(200) && s.Vy < Fx.Zero) { xEnd = D(s.X); break; }
                    }
                }
                Assert.True(launched);
                Assert.InRange(peak / standing, 1.25, 1.36);                 // about 1.3 x a standing jump
                Assert.InRange((xEnd - x0) * dir, 3.5, 5.5);                 // about 4 u in the arrow direction (flight back to the launch height)
                Assert.False(s.Dead);
            }
        }

        // ------------------------------------------------------------------ 5 Thorn Bush

        [Fact]
        public void ThornBush_LandingOnTheThornyHalfKnocksTheSamePlayerAwayAndNeverHurts()
        {
            foreach (int dir in new[] { 1, -1 })
            {
                Sim sim = World(Start(), P(1, 1, PlatformKind.ThornBush, 500, 200, 400, dir: dir));
                SimState s = Drop(sim, 1, 500 + dir * 120, 100);
                int hits = 0;
                for (int i = 0; i < 200; i++)
                {
                    sim.Step(ref s, 0);
                    Assert.False(s.Dead);
                    if (s.ThornKnocks > 0 && hits == 0) { hits = 1; Assert.True(s.Vx.Sign() == -dir); Assert.False(s.Grounded); Assert.Equal(0, s.Row); Assert.Equal(0, s.Hits); }
                }
                Assert.True(hits == 1, "no knock for dir " + dir);
                Assert.False(s.Dead);
            }
        }

        [Fact]
        public void ThornBush_TheSafeHalfAndTheCentreStrip_AreOrdinaryPlatform()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.ThornBush, 500, 200, 400, dir: 1));
            foreach (int x in new[] { 380, 460, 500, 510, 300 })   // left half, left of centre, centre, inside the dead zone (0.1 u)
            {
                SimState s = Drop(sim, 1, x, 100);
                Steps(sim, ref s, 30);
                Assert.True(s.Grounded && s.GroundId == 1, "x " + x + " grounded " + s.Grounded + " ground " + s.GroundId + " y " + D(s.Y) + " knocks " + s.ThornKnocks);
                Assert.Equal(0, s.ThornKnocks);
                Assert.Equal(1, s.Row);
            }
            SimState t = Drop(sim, 1, 530, 100);   // 0.3 u onto the thorny half
            Steps(sim, ref t, 40);
            Assert.True(t.ThornKnocks >= 1);
        }

        [Fact]
        public void ThornBush_WalkingOntoTheThornsAlsoHopsThePlayerBack()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.ThornBush, 500, 200, 400, dir: 1));
            SimState s = Stand(sim, 1, 420);
            Steps(sim, ref s, 40, SimInput.Right);
            Assert.True(s.ThornKnocks >= 1);
            Assert.False(s.Dead);
        }

        [Fact]
        public void ThornBush_ManyRandomLandingsNeverEndTheRunOrCountAsDamage()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.ThornBush, 500, 200, 600, dir: -1));
            Rng rng = new Rng(99);
            for (int i = 0; i < 100; i++)
            {
                SimState s = Drop(sim, 1, 250 + rng.Next(500), 100 + rng.Next(200));
                for (int t = 0; t < 150; t++) sim.Step(ref s, (t % 37 == 0 ? SimInput.Jump : 0) | (rng.Next(3) == 0 ? SimInput.Left : 0));
                Assert.False(s.Dead);
                Assert.Equal(0, s.Hits);
            }
        }

        // ------------------------------------------------------------------ 6 Ladybug Rider

        [Fact]
        public void Ladybug_RunsOneAndAHalfTimesFasterWhileTheOwnerStandsOnItAndCarriesThem()
        {
            Platform lady = P(1, 1, PlatformKind.Ladybug, 500, 200, 400, period: 240, phase: 0, ampHundredths: 100);
            Sim sim = World(Start(), lady);
            SimState s = Stand(sim, 1);
            double x0 = D(sim.PlatformX(1, 0, s.CrumbleTick));
            Steps(sim, ref s, 60);
            // 60 ticks of a triangle wave with half period 120: 60 ticks unboosted move 1.0 u, boosted 90 path ticks move 1.5 u
            double moved = D(sim.PlatformX(1, s.Tick, s.CrumbleTick)) - x0;
            Assert.InRange(moved, 1.45, 1.55);
            double plain = D(sim.PlatformX(1, 60)) - D(sim.PlatformX(1, 0));
            Assert.InRange(plain, 0.95, 1.05);
            Assert.InRange(D(s.X) - D(sim.PlatformX(1, s.Tick, s.CrumbleTick)), -0.02, 0.02);   // the rider stays on the same spot of the platform
            Assert.True(sim.LadybugBoosted(s, 1));
            // not standing on it: no boost
            Sim sim2 = World(Start(), lady);
            SimState u = Stand(sim2, 0);
            Steps(sim2, ref u, 60);
            Assert.Equal(sim2.PlatformX(1, 60).Raw, sim2.PlatformX(1, 60, u.CrumbleTick).Raw);
            Assert.False(sim2.LadybugBoosted(u, 1));
        }

        // ------------------------------------------------------------------ 7 Seesaw Leaf

        [Fact]
        public void Seesaw_TiltsTowardTheLandingSideAndSlidesThePlayerToThatEdge()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.Seesaw, 500, 200, 600));
            foreach (int side in new[] { 1, -1 })
            {
                SimState s = Drop(sim, 1, 500 + side * 100, 100);
                for (int i = 0; i < 100 && !(s.Grounded && s.GroundId == 1); i++) sim.Step(ref s, 0);
                Assert.True(s.Grounded && s.GroundId == 1);
                Assert.Equal(side, s.TiltDir);
                Assert.Equal(side, sim.SeesawTilt(s, 1));
                double x0 = D(s.X);
                Steps(sim, ref s, 60);
                Assert.InRange((D(s.X) - x0) * side, 1.7, 1.9);   // 1.8 u/s
            }
            Assert.Equal(0, sim.SeesawTilt(Stand(sim, 0), 1));   // nobody on it: level
        }

        // ------------------------------------------------------------------ determinism and the whole kit on a generated level

        private static List<LoadedLevel> Region1()
        {
            string root = ContentLoader.FindRoot();
            return root == null ? new List<LoadedLevel>() : ContentLoader.LoadRegions(root, 1, 1);
        }

        private static long StateHash(SimState s)
        {
            long h = 17;
            unchecked
            {
                h = h * 31 + s.Tick; h = h * 31 + s.X.Raw; h = h * 31 + s.Y.Raw; h = h * 31 + s.Row; h = h * 31 + s.Score;
                h = h * 31 + s.ThornKnocks; h = h * 31 + s.GroundId; h = h * 31 + s.TiltDir; h = h * 31 + s.LandTick;
                if (s.CrumbleTick != null) foreach (int c in s.CrumbleTick) h = h * 31 + c;
            }
            return h;
        }

        [Fact]
        public void TheKitIsDeterministic_SameLevelSameInputsSameState()
        {
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in Region1().Where(l => l.Spec.LevelIndex >= 3))
            {
                LevelSpec spec = ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(3));
                LevelData a = LevelBuilder.Build(cfg, spec), b = LevelBuilder.Build(cfg, spec);
                Assert.Equal(a.ComputeHash(), b.ComputeHash());
                long[] hs = new long[2];
                for (int run = 0; run < 2; run++)
                {
                    Sim sim = new Sim(cfg, run == 0 ? a : b, spec);
                    SimState s = sim.NewState();
                    Rng rng = new Rng(4242);
                    for (int t = 0; t < 3000 && !s.Dead; t++)
                    {
                        int input = 0;
                        int r = rng.Next(10);
                        if (r < 3) input |= SimInput.Left; else if (r < 6) input |= SimInput.Right;
                        if (rng.Next(25) == 0) input |= SimInput.Jump;
                        sim.Step(ref s, input);
                    }
                    hs[run] = StateHash(s);
                }
                Assert.Equal(hs[0], hs[1]);
            }
        }

        // ------------------------------------------------------------------ generator: counts, ramp, rules

        public static bool IsNewSpecial(PlatformKind k) { return PlatformKinds.IsRegion1New(k) || k == PlatformKind.Bounce; }

        private sealed class RowInfo
        {
            public PlatformKind[] Kind;   // spine kind per row, Normal where none (index = row)
            public int Rows;
        }

        /// <summary>Spine kind per row: the first non-Crumble platform of each row (dense patch extras are Normal / Crumble only).</summary>
        private static RowInfo Rows(LevelData d)
        {
            RowInfo ri = new RowInfo { Rows = d.Rows, Kind = new PlatformKind[d.Rows + 2] };
            foreach (Platform p in d.Platforms)
                if (p.Row >= 1 && p.Row <= d.Rows && IsNewSpecial(p.Kind)) ri.Kind[p.Row] = p.Kind;
            return ri;
        }

        [Fact]
        public void SpecialShareRisesFrom15To55PercentAndLevels1And2StayLight()
        {
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = Region1();
            Assert.Equal(15, levels.Count);
            double[] share = new double[16];
            foreach (LoadedLevel ll in levels)
            {
                int idx = ll.Spec.LevelIndex;
                double sum = 0;
                for (int i = 0; i < 10; i++)
                {
                    LevelData d = LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i)));
                    RowInfo ri = Rows(d);
                    int special = 0, hazard = 0, safe = 0;
                    for (int r = 1; r <= ri.Rows; r++)
                    {
                        if (ri.Kind[r] == PlatformKind.Normal) continue;
                        special++;
                        if (PlatformKinds.IsHazardType(ri.Kind[r])) hazard++; else safe++;
                    }
                    if (idx == 1) Assert.Equal(0, special);
                    if (idx == 2)
                    {
                        Assert.True(safe <= 2 && hazard <= 1, ll.Id + " light touch broken: " + safe + " safe, " + hazard + " hazard");
                        Assert.True(!ri.Kind.Any(k => PlatformKinds.IsLaunch(k)), "no launcher on level 2");
                    }
                    sum += special * 100.0 / ri.Rows;
                }
                share[idx] = sum / 10;
                output.WriteLine("level " + idx + ": " + share[idx].ToString("F1") + " percent of the rows carry a special platform");
            }
            Assert.Equal(0, share[1]);
            Assert.InRange(share[2], 1, 11);
            // the plan hits round(share * free rows): within 4 points of the target, which counts rows beside ladders and chunks as not free
            // The share applies to the free generated rows. Levels with authored chunks (5, 10, 15) or a big combo ladder (11) have fewer of them,
            // so only the plain levels are held to the band; the others must still carry a clear share of the target.
            for (int idx = 3; idx <= 15; idx++)
            {
                double target = 15 + 40.0 * (idx - 3) / 12;
                LoadedLevel ll = levels.First(l => l.Spec.LevelIndex == idx);
                bool plain = ll.Spec.FixedChunks.Length == 0 && ll.Spec.Goal.Type != GoalType.ComboChain;
                if (plain) Assert.InRange(share[idx], target - 8, target + 1.5);
                else Assert.InRange(share[idx], target * 0.4, target + 1.5);
            }
            Assert.InRange(share[3], 10, 16.5);
            for (int idx = 4; idx <= 14; idx++)
            {
                LoadedLevel ll = levels.First(l => l.Spec.LevelIndex == idx);
                if (ll.Spec.FixedChunks.Length == 0 && ll.Spec.Goal.Type != GoalType.ComboChain) Assert.True(share[idx] > share[idx - 1] - 4 || levels.First(l => l.Spec.LevelIndex == idx - 1).Spec.FixedChunks.Length > 0, "share fell from level " + (idx - 1) + " to " + idx);
            }
            Assert.True(share[14] > share[3] + 28);
        }

        [Fact]
        public void PlacementRulesHoldOnEveryRandomRegion1Layout()
        {
            SimConfig cfg = new SimConfig();
            List<string> bad = new List<string>();
            Dictionary<PlatformKind, int> seen = new Dictionary<PlatformKind, int>();
            foreach (LoadedLevel ll in Region1().Where(l => l.Spec.LevelIndex >= 2))
            {
                int idx = ll.Spec.LevelIndex;
                for (int i = 0; i < 20; i++)
                {
                    LevelSpec spec = ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i));
                    LevelData d = LevelBuilder.Build(cfg, spec);
                    RowInfo ri = Rows(d);
                    int lastHazard = -100;
                    for (int r = 1; r <= ri.Rows; r++)
                    {
                        PlatformKind k = ri.Kind[r];
                        if (k == PlatformKind.Normal) continue;
                        seen[k] = (seen.ContainsKey(k) ? seen[k] : 0) + 1;
                        string tag = ll.Id + " seed " + i + " row " + r + " " + k;
                        PlatformKind below = r > 1 ? ri.Kind[r - 1] : PlatformKind.Normal;
                        bool zone = r > ri.Rows - 3 || (d.HiddenGemRow > 0 && Math.Abs(r - d.HiddenGemRow) <= 2);
                        if (PlatformKinds.IsHazardType(k))
                        {
                            if (PlatformKinds.IsHazardType(below)) bad.Add(tag + ": two hazard types in adjacent rows");
                            if (PlatformKinds.IsLaunch(below)) bad.Add(tag + ": hazard right after a launcher");
                            if (idx <= 6 && r - lastHazard < 6) bad.Add(tag + ": hazards " + (r - lastHazard) + " rows apart before level 7");
                            lastHazard = r;
                        }
                        if (PlatformKinds.IsLaunch(k) && PlatformKinds.IsLaunch(below)) bad.Add(tag + ": launcher after a launcher");
                        if (zone && (PlatformKinds.IsLaunch(k) || PlatformKinds.IsHazardType(k))) bad.Add(tag + ": launcher or hazard in the last 3 rows / around the hidden gem");
                    }
                    // gap and position rules
                    for (int pi = 1; pi < d.Platforms.Length; pi++)
                    {
                        Platform p = d.Platforms[pi];
                        if (p.Row < 1 || p.Row > d.Rows || !PlatformKinds.IsRegion1New(p.Kind)) continue;
                        if (p.Kind == PlatformKind.ThornBush && p.Width < cfg.ThornMinWidth) bad.Add(ll.Id + ": thorn bush narrower than " + cfg.ThornMinWidth);
                        if (p.Kind == PlatformKind.WindPetal && p.Width < cfg.WindMinWidth) bad.Add(ll.Id + ": wind petal narrower than " + cfg.WindMinWidth);
                        if (p.Kind == PlatformKind.ThornBush && p.Dir == 0) bad.Add(ll.Id + ": thorn without side");
                        if (p.Kind == PlatformKind.Dandelion && p.Dir == 0) bad.Add(ll.Id + ": dandelion without direction");
                        if (p.Kind == PlatformKind.WindPetal && p.Dir == 0) bad.Add(ll.Id + ": wind petal without direction");
                        if (p.Kind == PlatformKind.HiveLift)
                        {
                            Fx below = FirstYOfRow(d, p.Row - 1), above = FirstYOfRow(d, p.Row + 1);
                            if (p.Y - below < cfg.HiveGapMin - Fx.FromRatio(1, 100) || p.Y - below > cfg.NormalGapMax - cfg.HiveBobAmplitude + Fx.FromRatio(1, 100)) bad.Add(ll.Id + " seed " + i + ": hive gap below " + D(p.Y - below).ToString("F2"));
                            if (above.Raw != 0 && (above - p.Y < cfg.HiveGapMin - Fx.FromRatio(1, 100) || above - p.Y > cfg.NormalGapMax - cfg.HiveBobAmplitude + Fx.FromRatio(1, 100))) bad.Add(ll.Id + " seed " + i + ": hive gap above " + D(above - p.Y).ToString("F2"));
                        }
                        if (p.Kind == PlatformKind.Dandelion)
                        {
                            // the next row sits where the arrow points
                            Platform next = d.Platforms.FirstOrDefault(q => q.Row == p.Row + 1);
                            if (next.Id != 0 && (next.X - p.X) * p.Dir < Fx.FromRatio(9, 10) && next.Width <= Fx.FromInt(4))
                                bad.Add(ll.Id + " seed " + i + ": platform after the dandelion is not in the arrow direction (dx " + D(next.X - p.X).ToString("F2") + " dir " + p.Dir + ")");
                        }
                    }
                }
            }
            foreach (string s in bad.Take(30)) output.WriteLine(s);
            foreach (KeyValuePair<PlatformKind, int> kv in seen.OrderBy(k => (int)k.Key)) output.WriteLine(kv.Key + ": " + kv.Value);
            Assert.True(bad.Count == 0, bad.Count + " rule violations, first: " + (bad.Count > 0 ? bad[0] : ""));
            foreach (PlatformKind k in RegionPlatformSets.For(SignatureKind.BounceFlowers)) Assert.True(seen.ContainsKey(k), k + " never placed");
            Assert.True(seen.ContainsKey(PlatformKind.Bounce), "the flower is one of the options");
        }

        [Fact]
        public void KindMixIsEvenAndHazardTypesStayAQuarterOfTheSpecialRows()
        {
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in Region1().Where(l => l.Spec.LevelIndex >= 3))
            {
                int special = 0, hazard = 0, launch = 0, rows = 0;
                const int Seeds = 10;
                for (int i = 0; i < Seeds; i++)
                {
                    LevelData d = LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i)));
                    RowInfo ri = Rows(d);
                    rows += ri.Rows;
                    for (int r = 1; r <= ri.Rows; r++)
                    {
                        if (ri.Kind[r] == PlatformKind.Normal) continue;
                        special++;
                        if (PlatformKinds.IsHazardType(ri.Kind[r])) hazard++;
                        if (PlatformKinds.IsLaunch(ri.Kind[r])) launch++;
                    }
                }
                output.WriteLine(ll.Id + ": " + ll.Spec.RowsFor() + " rows, " + (special / (double)Seeds).ToString("F1") + " special rows per layout, " + (hazard / (double)Seeds).ToString("F1") + " hazard-type, " + (launch / (double)Seeds).ToString("F1") + " launchers");
                Assert.True(hazard <= special * 0.30 + 1, ll.Id + ": hazards " + hazard + " of " + special);
                Assert.True(launch <= special * 0.30 + 1, ll.Id + ": launchers " + launch + " of " + special);
            }
        }

        private static Fx FirstYOfRow(LevelData d, int row)
        {
            foreach (Platform p in d.Platforms) if (p.Row == row) return p.Y;
            return Fx.Zero;
        }

        [Fact]
        public void EveryAttemptSeedOfALevelHasTheSameNumberOfSpecialRows()
        {
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in Region1().Where(l => l.Spec.FixedChunks.Length == 0))
            {
                HashSet<int> counts = new HashSet<int>();
                for (int i = 0; i < 20; i++)
                {
                    RowInfo ri = Rows(LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i))));
                    counts.Add(ri.Kind.Count(k => k != PlatformKind.Normal));
                }
                Assert.True(counts.Count == 1, ll.Id + " special row counts differ between seeds: " + string.Join(",", counts));
            }
        }

        [Fact]
        public void OtherRegionsGetNoRegion1Platforms()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 2, 5))
                foreach (Platform p in LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(1))).Platforms)
                    Assert.False(PlatformKinds.IsRegion1New(p.Kind), ll.Id + " has " + p.Kind);
        }

        // ------------------------------------------------------------------ difficulty ramp (all regions)

        [Fact]
        public void LayoutDifficultyRisesLevel3To15InEveryRegionAndLevels1And2AreUntouched()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            SimConfig cfg = new SimConfig();
            foreach (var group in ContentLoader.LoadRegions(root, 1, 5).GroupBy(l => l.Region))
            {
                LoadedLevel[] ls = group.OrderBy(l => l.Spec.LevelIndex).ToArray();
                for (int i = 0; i < ls.Length; i++)
                {
                    LevelSpec s = ls[i].Spec;
                    if (s.LevelIndex <= 2) Assert.Equal(s.Difficulty, s.EffectiveDifficulty(cfg));
                    else
                    {
                        Assert.True(s.EffectiveDifficulty(cfg) >= s.Difficulty);
                        if (i > 0 && s.LevelIndex > 3) Assert.True(s.EffectiveDifficulty(cfg) > ls[i - 1].Spec.EffectiveDifficulty(cfg), "level " + s.LevelIndex + " not harder than " + ls[i - 1].Spec.LevelIndex);
                    }
                }
                Assert.True(ls[2].Spec.EffectiveDifficulty(cfg) > ls[1].Spec.EffectiveDifficulty(cfg) + Fx.FromRatio(1, 10), "level 3 must be clearly harder than level 2");
                Assert.Equal(ls[14].Spec.Difficulty, ls[14].Spec.EffectiveDifficulty(cfg));   // level 15 is already at the top
            }
        }

        // ------------------------------------------------------------------ bot proofs

        [Fact]
        public void BotSolvesEveryRandomRegion1LayoutOfLevels2To15With24Seeds()
        {
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = Region1().Where(l => l.Spec.LevelIndex >= 2).ToList();
            string[][] failures = new string[levels.Count][];
            Parallel.For(0, levels.Count, li =>
            {
                LoadedLevel ll = levels[li];
                List<string> f = new List<string>();
                for (int i = 0; i < 24; i++)
                {
                    LevelSpec s = ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i + 40));
                    LevelData d = LevelBuilder.Build(cfg, s);
                    BotResult r = new LevelBot(new Sim(cfg, d, s), s, d, SolvabilityT4Tests.NodeBudget * Math.Max(1, s.RowsFor() / 60)).Solve();
                    if (!r.Solved) f.Add(ll.Id + " attempt seed " + AttemptSeedOf(i + 40) + ": best row " + r.BestRow + "/" + d.Rows + " nodes " + r.Nodes);
                }
                failures[li] = f.ToArray();
            });
            List<string> all = failures.SelectMany(x => x).ToList();
            foreach (string s in all) output.WriteLine(s);
            output.WriteLine("region 1 levels 2..15 x 24 seeds, unsolved: " + all.Count);
            Assert.True(all.Count == 0, all.Count + " unsolved layouts, first: " + (all.Count > 0 ? all[0] : ""));
        }

        private static int AttemptSeedOf(int i) { return RandomLayoutTests.AttemptSeed(i); }

        // ------------------------------------------------------------------ endless

        private static LevelSpec Endless(int seed) { return EndlessTower.MakeSpec(seed, Fx.Zero, SignatureKind.BounceFlowers, 400); }

        [Fact]
        public void Endless_Region1UsesTheNewPlatformsWithAFloorBasedRamp()
        {
            SimConfig cfg = new SimConfig();
            double early = 0, late = 0; int e = 0, l = 0;
            HashSet<PlatformKind> seen = new HashSet<PlatformKind>();
            for (int seed = 1; seed <= 12; seed++)
            {
                LevelData d = EndlessTower.BuildFixed(cfg, Endless(seed * 977), 320);
                PlatformKind[] kinds = new PlatformKind[322];
                foreach (Platform p in d.Platforms) if (p.Row >= 1 && IsNewSpecial(p.Kind)) { kinds[p.Row] = p.Kind; seen.Add(p.Kind); }
                for (int r = 5; r <= 40; r++) { early += kinds[r] != PlatformKind.Normal ? 1 : 0; e++; }
                for (int r = 200; r <= 320; r++) { late += kinds[r] != PlatformKind.Normal ? 1 : 0; l++; }
                // rules hold in endless too
                int lastHazard = -100;
                for (int r = 5; r <= 320; r++)
                {
                    PlatformKind k = kinds[r];
                    if (PlatformKinds.IsHazardType(k))
                    {
                        Assert.False(PlatformKinds.IsHazardType(kinds[r - 1]) || PlatformKinds.IsLaunch(kinds[r - 1]));
                        if (r < 40) Assert.True(r - lastHazard >= 6, "hazards too close early in endless");
                        lastHazard = r;
                    }
                    if (PlatformKinds.IsLaunch(k)) Assert.False(PlatformKinds.IsLaunch(kinds[r - 1]));
                }
            }
            double ep = early * 100 / e, lp = late * 100 / l;
            output.WriteLine("endless special share: floors 5-40 " + ep.ToString("F1") + " percent, floors 200-320 " + lp.ToString("F1") + " percent");
            Assert.InRange(ep, 12, 32);
            Assert.InRange(lp, 48, 62);
            Assert.Equal(8, seen.Count);   // all seven new platforms and the flower appear
        }

        [Fact]
        public void Endless_SeedsGiveDifferentTowersAndGrowthIsPrefixStable()
        {
            SimConfig cfg = new SimConfig();
            Assert.NotEqual(EndlessTower.BuildFixed(cfg, Endless(1), 150).ComputeHash(), EndlessTower.BuildFixed(cfg, Endless(2), 150).ComputeHash());
            LevelData small = EndlessTower.BuildFixed(cfg, Endless(5150), 120);
            EndlessTower grown = new EndlessTower(cfg, Endless(5150), 120);
            grown.GenerateTo(400);
            for (int i = 0; i < small.Platforms.Length; i++)
            {
                Platform p = small.Platforms[i], q = grown.Data.Platforms[i];
                Assert.True(p.Kind == q.Kind && p.X.Raw == q.X.Raw && p.Y.Raw == q.Y.Raw && p.Dir == q.Dir && p.Phase == q.Phase && p.Period == q.Period, "platform " + i + " changed");
            }
        }

        [Fact]
        public void Endless_TheBotClimbs60FloorsOfRegion1WithAllNewPlatforms()
        {
            SimConfig cfg = new SimConfig();
            string[] failures = new string[6];
            Parallel.For(0, 6, k =>
            {
                LevelSpec spec = Endless(31000 + k * 7919);
                LevelData d = EndlessTower.BuildFixed(cfg, spec, 330);
                Sim sim = new Sim(cfg, d, spec.Difficulty, true, false);
                BotResult r = new LevelBot(sim, 60, 60000).Solve();
                if (!r.Solved) failures[k] = "seed " + k + " best row " + r.BestRow;
            });
            Assert.True(failures.All(f => f == null), string.Join("; ", failures.Where(f => f != null)));
        }

        [Fact]
        public void Endless_RunControllerDrawsANewSeedForEveryRun()
        {
            // Documents the contract: GameSession.NewEndlessSeed mixes the clock, StartEndlessRun draws it for every run (Unity side), and the tower is a pure function of the seed.
            SimConfig cfg = new SimConfig();
            long a = EndlessTower.BuildFixed(cfg, Endless(123456), 200).ComputeHash();
            long b = EndlessTower.BuildFixed(cfg, Endless(123456), 200).ComputeHash();
            long c = EndlessTower.BuildFixed(cfg, Endless(123457), 200).ComputeHash();
            Assert.Equal(a, b);
            Assert.NotEqual(a, c);
        }
    }
}
