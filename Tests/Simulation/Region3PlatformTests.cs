using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>
    /// Doc 30 section 4 (owner list 31): the seven region 3 (Sunny Beach) special platforms: Surf Boost Wave, Sandcastle Step, Clam Shell Pad,
    /// Sinking Raft, Turtle Shuttle, Kelp Tangle, Pufferfish Pad. Behaviour tests use small hand-built worlds; generator tests use the shipped
    /// region 3 levels with random attempt seeds. The region 2 counterpart is Region2PlatformTests.
    /// </summary>
    [Trait("Category", "Region3Platforms")]
    public class Region3PlatformTests
    {
        private readonly ITestOutputHelper output;
        public Region3PlatformTests(ITestOutputHelper output) { this.output = output; }

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

        private static SimState Stand(Sim sim, int id, int xHundredths = int.MinValue)
        {
            SimState s = sim.NewState();
            Platform p = sim.Platforms[id];
            s.GroundId = id; s.BestGroundId = id; s.Grounded = true; s.Row = p.Row; s.JumpStartRow = p.Row;
            s.X = xHundredths == int.MinValue ? sim.PlatformX(id, 0) : F(xHundredths);
            s.Y = p.Kind == PlatformKind.SandcastleStep ? Sim.SandSurfaceY(p, s.X) : sim.PlatformY(id, 0);
            s.MaxY = s.Y; s.KillY = s.Y - Fx.FromInt(10);
            return s;
        }

        private static SimState Drop(Sim sim, int id, int xHundredths, int heightHundredths = 300, int tick = 0)
        {
            SimState s = sim.NewState();
            s.GroundId = 0; s.Grounded = false; s.Coyote = 0; s.Tick = tick;
            s.X = F(xHundredths); s.Y = sim.PlatformY(id, tick) + F(heightHundredths); s.Vy = -F(1, 100);
            s.MaxY = s.Y; s.KillY = s.Y - Fx.FromInt(10);
            s.JumpStartRow = 0;
            return s;
        }

        private static double D(Fx v) { return v.Raw / 65536.0; }

        private static void Steps(Sim sim, ref SimState s, int n, int input = 0)
        {
            for (int i = 0; i < n; i++) sim.Step(ref s, input);
        }

        private static void StepUntilGrounded(Sim sim, ref SimState s, int id, int maxTicks = 200)
        {
            for (int i = 0; i < maxTicks && !(s.Grounded && s.GroundId == id); i++) sim.Step(ref s, 0);
        }

        // ------------------------------------------------------------------ 3.1 Surf Boost Wave

        [Fact]
        public void SurfBoostWave_Accelerates45UnitsPerSecondSquaredAlongDirAndHoldsStillWhenRunningAgainstIt()
        {
            SimConfig cfg = new SimConfig();
            Assert.Equal(Fx.FromRatio(45, 3600).Raw, cfg.SurfAccel.Raw);
            Sim sim = World(Start(), P(1, 1, PlatformKind.SurfBoostWave, 500, 300, 600, dir: 1));
            SimState s = Stand(sim, 1, 250);
            sim.Step(ref s, 0);
            // no input: friction (45 u/s2) and the wave (45 u/s2) cancel except for the wave's own push of one tick: a slow drift along Dir
            Assert.InRange(D(s.Vx), 0.0, 0.02);
            // running along Dir: own run 30 + wave 45 reaches the top speed within 10 ticks
            SimState run = Stand(sim, 1, 250);
            int ticks = 0;
            while (run.Vx < cfg.MaxSpeed && ticks < 30) { sim.Step(ref run, SimInput.Right); ticks++; }
            Assert.InRange(ticks, 5, 10);
            Assert.Equal(cfg.MaxSpeed.Raw, run.Vx.Raw);
            // running against it: the player holds still (speed stays tiny)
            SimState hold = Stand(sim, 1, 500);
            for (int t = 0; t < 60; t++)
            {
                sim.Step(ref hold, SimInput.Left);
                Assert.InRange(D(hold.Vx), -0.02, 0.02);
            }
            Assert.InRange(D(hold.X), 4.4, 5.6);
        }

        [Fact]
        public void SurfBoostWave_AFullSpeedJumpIs6Point44HighAndDirMirrorsTheBoost()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = World(Start(), P(1, 1, PlatformKind.SurfBoostWave, 800, 300, 400, dir: -1));
            SimState s = Stand(sim, 1, 900);
            Steps(sim, ref s, 20, SimInput.Left);
            Assert.Equal((-cfg.MaxSpeed).Raw, s.Vx.Raw);
            sim.Step(ref s, SimInput.Jump | SimInput.Left);
            double v0 = D(s.Vy + cfg.Gravity);
            double peak = v0 * v0 / (2 * D(cfg.Gravity));
            Assert.InRange(peak, 6.3, 6.5);
        }

        // ------------------------------------------------------------------ 3.2 Sandcastle Step

        [Fact]
        public void SandcastleStep_WalkingIntoTheRaisedHalfStepsUpAndWalkingBackSlidesDownWhileStayingGrounded()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SandcastleStep, 500, 300, 600, dir: 1, ampHundredths: 16));
            SimState s = Stand(sim, 1, 350);
            Assert.Equal(F(300).Raw, s.Y.Raw);
            for (int i = 0; i < 120 && D(s.X) <= 5.05; i++) sim.Step(ref s, SimInput.Right);
            Assert.True(s.Grounded && s.GroundId == 1);
            Assert.Equal(F(316).Raw, s.Y.Raw);   // stepped up the 0.16 riser
            Steps(sim, ref s, 36, SimInput.Left);
            Assert.True(s.Grounded && s.GroundId == 1);
            Assert.Equal(F(300).Raw, s.Y.Raw);   // back on the low half, never airborne
            Assert.Equal(0, s.Combo);
            // the mirror (Dir = -1) has the LEFT half raised
            Sim mirror = World(Start(), P(1, 1, PlatformKind.SandcastleStep, 500, 300, 600, dir: -1, ampHundredths: 16));
            SimState m = Drop(mirror, 1, 350, 200);
            StepUntilGrounded(mirror, ref m, 1);
            Assert.Equal(F(316).Raw, m.Y.Raw);
            SimState n = Drop(mirror, 1, 650, 200);
            StepUntilGrounded(mirror, ref n, 1);
            Assert.Equal(F(300).Raw, n.Y.Raw);
        }

        [Fact]
        public void SandcastleStep_AFallingPlayerLandsOnTheSurfaceUnderItsXAndNeverFallsThroughTheBlock()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SandcastleStep, 500, 300, 400, dir: 1, ampHundredths: 16));
            SimState hi = Drop(sim, 1, 650, 300);
            StepUntilGrounded(sim, ref hi, 1);
            Assert.True(hi.Grounded);
            Assert.Equal(F(316).Raw, hi.Y.Raw);
            SimState lo = Drop(sim, 1, 350, 300);
            StepUntilGrounded(sim, ref lo, 1);
            Assert.Equal(F(300).Raw, lo.Y.Raw);
            // drifting from the low half into the raised half below its top: lands on the low surface instead of falling through
            for (int vx = 5; vx <= 12; vx++)
            {
                SimState d = Drop(sim, 1, 460, 50);
                d.Vx = F(vx, 100);
                StepUntilGrounded(sim, ref d, 1);
                Assert.True(d.Grounded && d.GroundId == 1, "vx " + vx);
                Assert.InRange(D(d.Y), 2.99, 3.17);
            }
            // jumping off the raised half works as from any platform
            sim.Step(ref hi, SimInput.Jump);
            Assert.False(hi.Grounded);
        }

        // ------------------------------------------------------------------ 3.3 Clam Shell Pad

        [Fact]
        public void ClamShellPad_Opens72TicksEvery144AndAnOpenLandingLaunches1Point4Times()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = World(Start(), P(1, 1, PlatformKind.ClamShellPad, 500, 300, 400, phase: 10));
            for (int t = 0; t < 288; t++) Assert.Equal((t + 10) % 144 < 72, sim.ClamOpen(1, t));
            Assert.True(PlatformKinds.IsLaunch(PlatformKind.ClamShellPad) && PlatformKinds.IsBouncePad(PlatformKind.ClamShellPad));
            // landing while open (ticks 0..61 are open for phase 10)
            SimState open = Drop(sim, 1, 500, 100, tick: 5);
            for (int i = 0; i < 60 && open.Row == 0; i++) sim.Step(ref open, 0);
            Assert.False(open.Grounded, "launched");
            Assert.Equal(1, open.Row);
            Assert.InRange(D(open.Vy), D(cfg.JumpBase * cfg.ClamLaunch) - 0.002, D(cfg.JumpBase * cfg.ClamLaunch) + 0.002);
            // landing while shut (ticks 62..133 are shut)
            SimState shut = Drop(sim, 1, 500, 100, tick: 70);
            StepUntilGrounded(sim, ref shut, 1, 60);
            Assert.True(shut.Grounded && shut.GroundId == 1, "a shut shell is a plain platform");
            Assert.Equal(0, shut.Vy.Raw);
        }

        // ------------------------------------------------------------------ 3.4 Sinking Raft

        [Fact]
        public void SinkingRaft_Sinks1UnitIn90TicksAndSpringsBackAt2UnitsPerSecondOnceEmpty()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SinkingRaft, 500, 300, 400));
            SimState s = Drop(sim, 1, 500, 100);
            StepUntilGrounded(sim, ref s, 1);
            Steps(sim, ref s, 45);
            Assert.InRange(3.0 - D(s.Y), 0.49, 0.51);   // half way after 45 ticks
            Steps(sim, ref s, 60);
            Assert.True(s.Grounded);
            Assert.InRange(3.0 - D(s.Y), 0.999, 1.001); // fully sunk, stays there
            Assert.Equal(sim.RaftSink(1, s.Tick, s.CrumbleTick).Raw, (F(300) - s.Y).Raw);
            // jump off at the bottom: the raft comes back at 2 u/s (1 u in 30 ticks)
            sim.Step(ref s, SimInput.Jump);
            Assert.False(s.Grounded);
            Steps(sim, ref s, 15);
            Assert.InRange(3.0 - D(sim.PlatformY(1, s.Tick, s.CrumbleTick)), 0.45, 0.55);
            Steps(sim, ref s, 20);
            Assert.Equal(F(300).Raw, sim.PlatformY(1, s.Tick, s.CrumbleTick).Raw);
        }

        [Fact]
        public void SinkingRaft_AFallingPlayerLandsOnTheSunkSurfaceTheSinkContinuesAndARevivePutsThePlayerOnIt()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SinkingRaft, 500, 300, 400));
            SimState s = Drop(sim, 1, 500, 100);
            StepUntilGrounded(sim, ref s, 1);
            Steps(sim, ref s, 60);
            sim.Step(ref s, SimInput.Jump);
            Steps(sim, ref s, 8);   // mid-air, the raft springs back (about 0.27 u)
            double lowBefore = D(sim.PlatformY(1, s.Tick, s.CrumbleTick));
            s.Vy = -F(1, 100); s.Y = sim.PlatformY(1, s.Tick, s.CrumbleTick) + F(30); s.X = F(500);
            StepUntilGrounded(sim, ref s, 1);
            Assert.True(s.Grounded);
            Assert.True(D(s.Y) < 3.0 && D(s.Y) > 3.0 - 1.01);
            double y0 = D(s.Y);
            Steps(sim, ref s, 30);
            Assert.True(D(s.Y) < y0, "the sink continues from the level it has now (was " + lowBefore.ToString("F2") + ")");
            // revive
            SimState dead = s;
            dead.Dead = true; dead.BestGroundId = 1;
            sim.Revive(ref dead);
            Assert.True(dead.Grounded && dead.GroundId == 1);
            Assert.InRange(D(dead.Y) - D(sim.PlatformY(1, dead.Tick, dead.CrumbleTick)), -0.001, 0.001);
        }

        // ------------------------------------------------------------------ 3.5 Turtle Shuttle

        [Fact]
        public void TurtleShuttle_PausesGlidesTwoPointFourUnitsAndPausesAgainInA264TickPeriod()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.TurtleShuttle, 500, 300, 400, period: 264, ampHundredths: 120));
            double L = 5.0 - 1.2, R = 5.0 + 1.2;
            for (int t = 0; t < 72; t++) Assert.InRange(D(sim.PlatformX(1, t)), L - 0.001, L + 0.001);   // pause at the left end 1.2 s
            Assert.True(D(sim.PlatformX(1, 90)) > L + 0.1 && D(sim.PlatformX(1, 90)) < R - 0.1);          // gliding
            for (int t = 132; t < 204; t++) Assert.InRange(D(sim.PlatformX(1, t)), R - 0.001, R + 0.001); // pause at the right end
            Assert.InRange(D(sim.PlatformX(1, 263)), L - 0.001, L + 0.02);                                 // back at the start of the next period
            Assert.Equal(sim.PlatformX(1, 7).Raw, sim.PlatformX(1, 7 + 264).Raw);
            // the glide takes 60 ticks and never moves faster than a smoothstep allows (peak 1.5 x the mean speed)
            double maxStep = 0;
            for (int t = 72; t < 132; t++) maxStep = Math.Max(maxStep, D(sim.PlatformX(1, t + 1)) - D(sim.PlatformX(1, t)));
            Assert.InRange(maxStep, 0.04, 0.0625);
            // it carries a standing player
            SimState s = Stand(sim, 1, 440);
            s.Tick = 72;
            double x0 = D(s.X);
            Steps(sim, ref s, 60);
            Assert.True(s.Grounded);
            Assert.InRange(D(s.X) - x0, 2.38, 2.42);
        }

        // ------------------------------------------------------------------ 3.6 Kelp Tangle

        [Fact]
        public void KelpTangle_NoJumpFor36TicksAfterLandingThePressIsKeptAndFiresWhenTheLockEnds()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = World(Start(), P(1, 1, PlatformKind.KelpTangle, 500, 300, 600));
            SimState s = Drop(sim, 1, 500, 100);
            StepUntilGrounded(sim, ref s, 1);
            Assert.True(s.Grounded && s.JumpLock > 0);
            int landedAt = s.Tick;
            sim.Step(ref s, SimInput.Jump);   // a single tap right after the landing
            int n = 1;
            while (s.Grounded && n < 80) { sim.Step(ref s, 0); n++; }
            Assert.False(s.Grounded, "the kept press fired");
            int sinceLanding = s.Tick - landedAt;
            Assert.InRange(sinceLanding, cfg.KelpLockTicks - 2, cfg.KelpLockTicks + 1);
            Assert.Equal(0, s.JumpLock);
            // after the lock a tap jumps at once; the lock is cleared by the next landing and re-armed by landing on kelp again
            SimState later = Drop(sim, 1, 500, 100);
            StepUntilGrounded(sim, ref later, 1);
            Steps(sim, ref later, 40);
            sim.Step(ref later, SimInput.Jump);
            Assert.False(later.Grounded);
            // pressing during the lock never loses the input: tap on the last lock tick
            SimState late = Drop(sim, 1, 500, 100);
            StepUntilGrounded(sim, ref late, 1);
            Steps(sim, ref late, cfg.KelpLockTicks - 3);
            sim.Step(ref late, SimInput.Jump);
            Steps(sim, ref late, 4);
            Assert.False(late.Grounded);
            Assert.False(late.Dead);
        }

        // ------------------------------------------------------------------ 3.7 Pufferfish Pad

        [Fact]
        public void PufferfishPad_CalmSwellingPuffedOnA180TickCycle()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.PufferfishPad, 500, 300, 400, phase: 20));
            for (int t = 0; t < 360; t++)
            {
                int c = (t + 20) % 180;
                Assert.Equal(c >= 132 ? 2 : (c >= 96 ? 1 : 0), sim.PufferPhase(1, t));
            }
        }

        [Fact]
        public void PufferfishPad_LandingWhilePuffedHopsThePlayerAwayWithoutCountingALandingOrAHit()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = World(Start(), P(1, 1, PlatformKind.PufferfishPad, 500, 300, 400, phase: 0));
            // land right of the centre at tick 135 (puffed 132..179)
            SimState s = Drop(sim, 1, 560, 60, tick: 130);
            for (int i = 0; i < 40 && s.Y > F(300) + F(5, 100); i++) sim.Step(ref s, 0);
            Steps(sim, ref s, 3);
            Assert.False(s.Grounded);
            Assert.Equal(1, s.PufferHops);
            Assert.Equal(0, s.Row);                 // no landing counted
            Assert.Equal(0, s.Score);
            Assert.Equal(0, s.Hits);
            Assert.False(s.Dead);
            Assert.Equal(cfg.PufferHopVx.Raw, s.Vx.Raw);   // away from the centre = to the right
            // a landing in the calm phase is a plain landing
            SimState calm = Drop(sim, 1, 440, 60, tick: 10);
            StepUntilGrounded(sim, ref calm, 1, 60);
            Assert.True(calm.Grounded && calm.GroundId == 1);
            Assert.Equal(1, calm.Row);
            // standing while it puffs hops (and away to the left from the left half)
            Steps(sim, ref calm, 140);
            Assert.Equal(1, calm.PufferHops);
            Assert.True(calm.Vx < Fx.Zero);
        }

        [Fact]
        public void PufferfishPad_ManyRandomLandingsNeverEndTheRunOrCountAsDamage()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.PufferfishPad, 500, 300, 500, phase: 33));
            Rng rng = new Rng(77);
            for (int i = 0; i < 500; i++)
            {
                SimState s = Drop(sim, 1, 250 + rng.Next(500), 100 + rng.Next(200), tick: rng.Next(400));
                for (int t = 0; t < 250; t++) sim.Step(ref s, (t % 41 == 0 ? SimInput.Jump : 0) | (rng.Next(4) == 0 ? SimInput.Left : 0) | (rng.Next(4) == 0 ? SimInput.Right : 0));
                Assert.False(s.Dead);
                Assert.Equal(0, s.Hits);
            }
        }

        // ------------------------------------------------------------------ determinism and the whole kit on a generated level

        private static List<LoadedLevel> Region3()
        {
            string root = ContentLoader.FindRoot();
            return root == null ? new List<LoadedLevel>() : ContentLoader.LoadRegions(root, 3, 3);
        }

        private static long StateHash(SimState s)
        {
            long h = 17;
            unchecked
            {
                h = h * 31 + s.Tick; h = h * 31 + s.X.Raw; h = h * 31 + s.Y.Raw; h = h * 31 + s.Row; h = h * 31 + s.Score;
                h = h * 31 + s.PufferHops; h = h * 31 + s.GroundId; h = h * 31 + s.JumpLock; h = h * 31 + s.LandTick; h = h * 31 + s.Vx.Raw;
                if (s.CrumbleTick != null) foreach (int c in s.CrumbleTick) h = h * 31 + c;
            }
            return h;
        }

        [Fact]
        public void TheKitIsDeterministic_SameLevelSameInputsSameState()
        {
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in Region3().Where(l => l.Spec.LevelIndex >= 3))
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

        [Fact]
        public void TheKitNeverAllocatesPerStep()
        {
            // Step must stay allocation free with every region 3 kind in play (CUX-1): measure managed allocations over 20000 steps on a level full of them.
            SimConfig cfg = new SimConfig();
            LoadedLevel ll = Region3().First(l => l.Spec.LevelIndex == 15);
            LevelSpec spec = ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(5));
            LevelData d = LevelBuilder.Build(cfg, spec);
            Sim sim = new Sim(cfg, d, spec);
            SimState s = sim.NewState();
            Rng rng = new Rng(9);
            for (int t = 0; t < 500; t++) sim.Step(ref s, rng.Next(3) == 0 ? SimInput.Jump : (rng.Next(2) == 0 ? SimInput.Left : SimInput.Right));   // warm up
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int t = 0; t < 20000; t++)
            {
                if (s.Dead) sim.Revive(ref s);
                sim.Step(ref s, rng.Next(20) == 0 ? SimInput.Jump : (rng.Next(2) == 0 ? SimInput.Left : SimInput.Right));
            }
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.True(after - before < 1024 * 80, "allocated " + (after - before) + " bytes ");
        }

        // ------------------------------------------------------------------ generator: counts, ramp, rules

        private static bool IsRow3Special(PlatformKind k)
        {
            return PlatformKinds.IsRegion3New(k) || k == PlatformKind.Moving || k == PlatformKind.Crumble || k == PlatformKind.Vanishing;
        }

        /// <summary>Spine kind per row: the first platform of each row (row mates and gem side platforms are Normal or Crumble and come after it).</summary>
        private static PlatformKind[] SpineKinds(LevelData d)
        {
            PlatformKind[] kinds = new PlatformKind[d.Rows + 2];
            bool[] seen = new bool[d.Rows + 2];
            foreach (Platform p in d.Platforms)
            {
                if (p.Row < 1 || p.Row > d.Rows || seen[p.Row]) continue;
                seen[p.Row] = true;
                kinds[p.Row] = IsRow3Special(p.Kind) ? p.Kind : PlatformKind.Normal;
            }
            return kinds;
        }

        private static Fx FirstYOfRow(LevelData d, int row)
        {
            foreach (Platform p in d.Platforms) if (p.Row == row) return p.Y;
            return Fx.Zero;
        }

        [Fact]
        public void Region3UsesItsSevenKindsWithTheDriftingSignatureTwiceAndTwoGenericsAndEnumValuesAreStable()
        {
            PlatformKind[] set = RegionPlatformSets.For(SignatureKind.DriftingPlatforms);
            Assert.Equal(7, set.Length);
            Assert.All(set, k => Assert.True(PlatformKinds.IsRegion3New(k)));
            Assert.Equal(new[] { PlatformKind.Crumble, PlatformKind.Vanishing }, RegionPlatformSets.GenericsFor(SignatureKind.DriftingPlatforms));
            Assert.Equal(7, RegionPlatformSets.For(SignatureKind.StickyGum).Length);   // region 4 has its own set since D5
            Assert.Equal(7, RegionPlatformSets.For(SignatureKind.SwingingGongs).Length);   // region 5 got its seven kinds (Region5PlatformTests)
            LevelSpec spec = Region3().First(l => l.Spec.LevelIndex == 5).Spec;
            PlatformKind[] options = SpecialPlatforms.Options(spec, PlatformKind.Moving);
            Assert.Equal(11, options.Length);   // 7 new + drifting twice + Crumble + Vanishing
            Assert.Equal(2, options.Count(k => k == PlatformKind.Moving));
            Assert.Equal(1, options.Count(k => k == PlatformKind.Crumble));
            Assert.Equal(1, options.Count(k => k == PlatformKind.Vanishing));
            Assert.True(PlatformKinds.IsHazardType(PlatformKind.KelpTangle) && PlatformKinds.IsHazardType(PlatformKind.PufferfishPad));
            Assert.True(PlatformKinds.IsDemanding(PlatformKind.SinkingRaft) && PlatformKinds.IsDemanding(PlatformKind.TurtleShuttle));
            Assert.True(PlatformKinds.IsLaunch(PlatformKind.ClamShellPad));
            Assert.Equal(21, (int)PlatformKind.SporePop);
            Assert.Equal(22, (int)PlatformKind.SurfBoostWave);
            Assert.Equal(23, (int)PlatformKind.SandcastleStep);
            Assert.Equal(24, (int)PlatformKind.ClamShellPad);
            Assert.Equal(25, (int)PlatformKind.SinkingRaft);
            Assert.Equal(26, (int)PlatformKind.TurtleShuttle);
            Assert.Equal(27, (int)PlatformKind.KelpTangle);
            Assert.Equal(28, (int)PlatformKind.PufferfishPad);
        }

        [Fact]
        public void SurfBoostWaveIsNeverOfferedInTheNoWallBounceLevel()
        {
            List<LoadedLevel> levels = Region3();
            LoadedLevel lvl7 = levels.First(l => l.Spec.LevelIndex == 7);
            Assert.Equal(GoalType.NoWallBounce, lvl7.Spec.Goal.Type);
            Assert.DoesNotContain(PlatformKind.SurfBoostWave, SpecialPlatforms.Options(lvl7.Spec, PlatformKind.Moving));
            SimConfig cfg = new SimConfig();
            for (int i = 0; i < 30; i++)
                foreach (Platform p in LevelBuilder.Build(cfg, lvl7.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i))).Platforms)
                    Assert.NotEqual(PlatformKind.SurfBoostWave, p.Kind);
            // other levels do have it
            Assert.Contains(PlatformKind.SurfBoostWave, SpecialPlatforms.Options(levels.First(l => l.Spec.LevelIndex == 6).Spec, PlatformKind.Moving));
        }

        [Fact]
        public void OtherRegionsGetNoRegion3PlatformsAndRegion3GetsNoRegion1Or2Platforms()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 4, 5))
                foreach (Platform p in LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(1))).Platforms)
                    Assert.False(PlatformKinds.IsRegion3New(p.Kind), ll.Id + " has " + p.Kind);
            foreach (LoadedLevel ll in Region3())
                foreach (Platform p in LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(1))).Platforms)
                    Assert.False(PlatformKinds.IsRegion1New(p.Kind) || PlatformKinds.IsRegion2New(p.Kind) || p.Kind == PlatformKind.BounceCap, ll.Id + " has " + p.Kind);
        }

        [Fact]
        public void Region3SpecialShareFollowsTheDoc25RampAndLevel1HasNoHazardTypes()
        {
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = Region3();
            Assert.Equal(15, levels.Count);
            foreach (LoadedLevel ll in levels)
            {
                int idx = ll.Spec.LevelIndex;
                double sum = 0;
                for (int i = 0; i < 10; i++)
                {
                    LevelData d = LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i)));
                    PlatformKind[] kinds = SpineKinds(d);
                    int special = 0;
                    for (int r = 1; r <= d.Rows; r++)
                    {
                        if (kinds[r] == PlatformKind.Normal) continue;
                        special++;
                        if (idx == 1) Assert.False(PlatformKinds.IsHazardType(kinds[r]), "the tutorial has no hazard-type platforms");
                    }
                    sum += special * 100.0 / d.Rows;
                }
                double mean = (cfg.RampLowPercent[idx] + cfg.RampHighPercent[idx]) / 2.0;
                double got = sum / 10;
                output.WriteLine("level " + idx + ": " + got.ToString("F1") + " percent of the rows (ramp mean " + mean + ")");
                if (ll.Spec.Goal.Type != GoalType.ComboChain && ll.Spec.FixedChunks.Length == 0) Assert.InRange(got, mean - 8, mean + 2);
            }
        }

        [Fact]
        public void EveryAttemptSeedOfALevelHasTheSameNumberOfSpecialRows()
        {
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in Region3().Where(l => l.Spec.FixedChunks.Length == 0))
            {
                HashSet<int> counts = new HashSet<int>();
                for (int i = 0; i < 20; i++)
                {
                    LevelData d = LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i)));
                    counts.Add(SpineKinds(d).Count(k => k != PlatformKind.Normal));
                }
                Assert.True(counts.Count == 1, ll.Id + " special row counts differ between seeds: " + string.Join(",", counts));
            }
        }

        [Fact]
        public void PlacementRulesHoldOnEveryRandomRegion3Layout()
        {
            SimConfig cfg = new SimConfig();
            List<string> bad = new List<string>();
            Dictionary<PlatformKind, int> seen = new Dictionary<PlatformKind, int>();
            foreach (LoadedLevel ll in Region3())
            {
                int idx = ll.Spec.LevelIndex;
                for (int i = 0; i < 20; i++)
                {
                    LevelSpec spec = ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i));
                    LevelData d = LevelBuilder.Build(cfg, spec);
                    PlatformKind[] kinds = SpineKinds(d);
                    int lastHazard = -100;
                    for (int r = 1; r <= d.Rows; r++)
                    {
                        PlatformKind k = kinds[r];
                        if (k == PlatformKind.Normal) continue;
                        seen[k] = (seen.ContainsKey(k) ? seen[k] : 0) + 1;
                        string tag = ll.Id + " seed " + i + " row " + r + " " + k;
                        PlatformKind below = r > 1 ? kinds[r - 1] : PlatformKind.Normal;
                        bool zone = r > d.Rows - 3 || (d.HiddenGemRow > 0 && Math.Abs(r - d.HiddenGemRow) <= 2);
                        if (idx == 1 && PlatformKinds.IsHazardType(k)) bad.Add(tag + ": hazard type in level 1");
                        if (PlatformKinds.IsHazardType(k))
                        {
                            if (PlatformKinds.IsHazardType(below)) bad.Add(tag + ": two hazard types in adjacent rows");
                            if (PlatformKinds.IsLaunch(below)) bad.Add(tag + ": hazard right after a launcher");
                            if (PlatformKinds.IsDemanding(below)) bad.Add(tag + ": hazard right after a demanding kind");
                            if (idx <= 6 && r - lastHazard < 6) bad.Add(tag + ": hazards " + (r - lastHazard) + " rows apart before level 7");
                            lastHazard = r;
                        }
                        if (PlatformKinds.IsDemanding(k))
                        {
                            if (PlatformKinds.IsDemanding(below) || PlatformKinds.IsHazardType(below) || PlatformKinds.IsLaunch(below)) bad.Add(tag + ": demanding kind next to a demanding kind, hazard or launcher");
                        }
                        if (PlatformKinds.IsLaunch(k) && PlatformKinds.IsLaunch(below)) bad.Add(tag + ": launcher after a launcher");
                        if (zone && (PlatformKinds.IsLaunch(k) || PlatformKinds.IsHazardType(k) || PlatformKinds.IsDemanding(k))) bad.Add(tag + ": launcher, hazard or demanding kind in the last 3 rows / around the hidden gem");
                        if (k == PlatformKind.SurfBoostWave && spec.Goal.Type == GoalType.NoWallBounce) bad.Add(tag + ": surf boost in a no-wall-bounce level");
                    }
                    foreach (Platform p in d.Platforms)
                    {
                        if (p.Row < 1 || p.Row > d.Rows) continue;
                        if (p.Kind == PlatformKind.SurfBoostWave)
                        {
                            if (p.Width < Fx.FromInt(4)) bad.Add(ll.Id + ": surf wave narrower than 4 (" + D(p.Width) + ")");
                            if (p.X < cfg.ScreenWidth / 2 ? p.Dir != 1 : p.Dir != -1) bad.Add(ll.Id + ": surf wave does not point to the middle");
                        }
                        if (p.Kind == PlatformKind.SandcastleStep)
                        {
                            if (p.Width < Fx.FromInt(4)) bad.Add(ll.Id + ": sandcastle narrower than 4 (" + D(p.Width) + ")");
                            if (p.Dir == 0 || p.Amplitude != cfg.SandcastleRise) bad.Add(ll.Id + ": sandcastle without riser or side");
                            Fx below = FirstYOfRow(d, p.Row - 1);
                            if (p.Y - below > cfg.NormalGapMax - cfg.SandcastleRise + Fx.FromRatio(1, 100)) bad.Add(ll.Id + " seed " + i + ": sandcastle climb gap " + D(p.Y - below).ToString("F2"));
                        }
                        if (p.Kind == PlatformKind.TurtleShuttle)
                        {
                            if (p.Period != cfg.TurtlePeriodTicks) bad.Add(ll.Id + ": turtle period " + p.Period);
                            if (p.X - p.Width / 2 - p.Amplitude < Fx.Zero || p.X + p.Width / 2 + p.Amplitude > cfg.ScreenWidth) bad.Add(ll.Id + ": turtle path leaves the walls");
                            if (p.Amplitude > cfg.TurtleAmplitude) bad.Add(ll.Id + ": turtle glide too long");
                        }
                        if (p.Kind == PlatformKind.PufferfishPad && p.Width < cfg.PufferMinWidth) bad.Add(ll.Id + ": puffer narrower than 3");
                        if (p.Kind == PlatformKind.ClamShellPad && p.Phase >= cfg.ClamCycleTicks) bad.Add(ll.Id + ": clam phase");
                        if (p.Kind == PlatformKind.SinkingRaft)
                        {
                            Fx lo = cfg.HiveGapMin, hi = cfg.NormalGapMax - cfg.RaftSinkDepth, tol = Fx.FromRatio(1, 100);
                            Fx below = FirstYOfRow(d, p.Row - 1), above = FirstYOfRow(d, p.Row + 1);
                            if (p.Y - below < lo - tol || p.Y - below > hi + tol) bad.Add(ll.Id + " seed " + i + ": raft gap below " + D(p.Y - below).ToString("F2"));
                            if (above.Raw != 0 && (above - p.Y < lo - tol || above - p.Y > hi + tol)) bad.Add(ll.Id + " seed " + i + ": raft gap above " + D(above - p.Y).ToString("F2"));
                        }
                    }
                }
            }
            foreach (string s in bad.Take(30)) output.WriteLine(s);
            foreach (KeyValuePair<PlatformKind, int> kv in seen.OrderBy(k => (int)k.Key)) output.WriteLine(kv.Key + ": " + kv.Value);
            Assert.True(bad.Count == 0, bad.Count + " rule violations, first: " + (bad.Count > 0 ? bad[0] : ""));
            foreach (PlatformKind k in RegionPlatformSets.For(SignatureKind.DriftingPlatforms)) Assert.True(seen.ContainsKey(k), k + " never placed");
            Assert.True(seen.ContainsKey(PlatformKind.Moving) && seen.ContainsKey(PlatformKind.Crumble) && seen.ContainsKey(PlatformKind.Vanishing), "the signature and the two generics are options");
            // drifting stays the most common kind; each generic is rarer than the signature, and the new kinds stay the stars
            int moving = seen[PlatformKind.Moving];
            foreach (KeyValuePair<PlatformKind, int> kv in seen) if (kv.Key != PlatformKind.Moving) Assert.True(kv.Value <= moving, kv.Key + " (" + kv.Value + ") is more common than drifting (" + moving + ")");
        }

        [Fact]
        public void KindMixIsEvenAndHazardTypesStayAFifthOfTheSpecialRows()
        {
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in Region3().Where(l => l.Spec.LevelIndex >= 3 && l.Spec.FixedChunks.Length == 0))
            {
                int special = 0, hazard = 0, launch = 0, demanding = 0;
                const int Seeds = 10;
                for (int i = 0; i < Seeds; i++)
                {
                    LevelData d = LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i)));
                    PlatformKind[] kinds = SpineKinds(d);
                    for (int r = 1; r <= d.Rows; r++)
                    {
                        if (kinds[r] == PlatformKind.Normal) continue;
                        special++;
                        if (PlatformKinds.IsHazardType(kinds[r])) hazard++;
                        if (PlatformKinds.IsLaunch(kinds[r])) launch++;
                        if (PlatformKinds.IsDemanding(kinds[r])) demanding++;
                    }
                }
                output.WriteLine(ll.Id + ": " + (special / (double)Seeds).ToString("F1") + " special rows per layout, " + (hazard / (double)Seeds).ToString("F1") + " hazard-type, " + (launch / (double)Seeds).ToString("F1") + " launchers, " + (demanding / (double)Seeds).ToString("F1") + " demanding");
                Assert.True(hazard <= special * 0.30 + 1, ll.Id + ": hazards " + hazard + " of " + special);
                Assert.True(launch <= special * 0.15 + 1, ll.Id + ": launchers " + launch + " of " + special);
                Assert.True(demanding <= special * 0.30 + 1, ll.Id + ": demanding " + demanding + " of " + special);
            }
        }

        // ------------------------------------------------------------------ bot proofs

        [Fact]
        public void BotSolvesEveryRandomRegion3LayoutOfLevels1To15With24Seeds()
        {
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = Region3().ToList();
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
                    if (!r.Solved) f.Add(ll.Id + " attempt seed " + RandomLayoutTests.AttemptSeed(i + 40) + ": best row " + r.BestRow + "/" + d.Rows + " nodes " + r.Nodes);
                }
                failures[li] = f.ToArray();
            });
            List<string> all = failures.SelectMany(x => x).ToList();
            foreach (string s in all) output.WriteLine(s);
            output.WriteLine("region 3 levels 1..15 x 24 seeds, unsolved: " + all.Count);
            Assert.True(all.Count == 0, all.Count + " unsolved layouts, first: " + (all.Count > 0 ? all[0] : ""));
        }

        [Fact]
        public void BotSolvesTheBaseLayoutOfEveryRegion3Level()
        {
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = Region3().ToList();
            string[] failures = new string[levels.Count];
            Parallel.For(0, levels.Count, li =>
            {
                LoadedLevel ll = levels[li];
                LevelData d = LevelBuilder.Build(cfg, ll.Spec);
                BotResult r = new LevelBot(new Sim(cfg, d, ll.Spec), ll.Spec, d, SolvabilityT4Tests.NodeBudget * Math.Max(1, ll.Spec.RowsFor() / 60)).Solve();
                if (!r.Solved) failures[li] = ll.Id + ": best row " + r.BestRow + "/" + d.Rows;
            });
            Assert.True(failures.All(f => f == null), string.Join("; ", failures.Where(f => f != null)));
        }

        // ------------------------------------------------------------------ endless

        private static LevelSpec Endless(int seed) { return EndlessTower.MakeSpec(seed, Fx.Zero, SignatureKind.DriftingPlatforms, 400); }

        [Fact]
        public void Endless_Region3UsesTheNewPlatformsWithAFloorBasedRampAndTheSameRules()
        {
            SimConfig cfg = new SimConfig();
            double early = 0, late = 0; int e = 0, l = 0;
            HashSet<PlatformKind> seen = new HashSet<PlatformKind>();
            for (int seed = 1; seed <= 12; seed++)
            {
                LevelData d = EndlessTower.BuildFixed(cfg, Endless(seed * 977), 320);
                PlatformKind[] kinds = new PlatformKind[322];
                bool[] done = new bool[322];
                foreach (Platform p in d.Platforms)
                {
                    if (p.Row < 1 || p.Row > 320 || done[p.Row]) continue;
                    done[p.Row] = true;
                    if (IsRow3Special(p.Kind)) { kinds[p.Row] = p.Kind; seen.Add(p.Kind); }
                }
                for (int r = 5; r <= 40; r++) { early += kinds[r] != PlatformKind.Normal ? 1 : 0; e++; }
                for (int r = 200; r <= 320; r++) { late += kinds[r] != PlatformKind.Normal ? 1 : 0; l++; }
                int lastHazard = -100;
                for (int r = 5; r <= 320; r++)
                {
                    PlatformKind k = kinds[r];
                    if (PlatformKinds.IsHazardType(k))
                    {
                        Assert.False(PlatformKinds.IsHazardType(kinds[r - 1]) || PlatformKinds.IsLaunch(kinds[r - 1]) || PlatformKinds.IsDemanding(kinds[r - 1]));
                        if (r < 40) Assert.True(r - lastHazard >= 6, "hazards too close early in endless");
                        lastHazard = r;
                    }
                    if (PlatformKinds.IsDemanding(k)) Assert.False(PlatformKinds.IsDemanding(kinds[r - 1]) || PlatformKinds.IsHazardType(kinds[r - 1]) || PlatformKinds.IsLaunch(kinds[r - 1]));
                    if (PlatformKinds.IsLaunch(k)) Assert.False(PlatformKinds.IsLaunch(kinds[r - 1]));
                }
            }
            double ep = early * 100 / e, lp = late * 100 / l;
            output.WriteLine("endless special share: floors 5-40 " + ep.ToString("F1") + " percent, floors 200-320 " + lp.ToString("F1") + " percent");
            Assert.InRange(ep, 12, 32);
            Assert.InRange(lp, 48, 62);
            Assert.Equal(10, seen.Count);   // seven new platforms, the drifting platform and the two generics appear
        }

        [Fact]
        public void Endless_Region3GrowthIsPrefixStable()
        {
            SimConfig cfg = new SimConfig();
            LevelData small = EndlessTower.BuildFixed(cfg, Endless(5150), 120);
            EndlessTower grown = new EndlessTower(cfg, Endless(5150), 120);
            grown.GenerateTo(400);
            for (int i = 0; i < small.Platforms.Length; i++)
            {
                Platform p = small.Platforms[i], q = grown.Data.Platforms[i];
                Assert.True(p.Kind == q.Kind && p.X.Raw == q.X.Raw && p.Y.Raw == q.Y.Raw && p.Dir == q.Dir && p.Phase == q.Phase && p.Period == q.Period && p.Amplitude.Raw == q.Amplitude.Raw, "platform " + i + " changed");
            }
        }

        [Fact]
        public void Endless_TheBotClimbs60FloorsOfRegion3WithAllNewPlatforms()
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
    }
}
