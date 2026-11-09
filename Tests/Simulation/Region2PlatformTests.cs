using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>
    /// Doc 30 section 3 (owner list 30): the seven region 2 (Mushroom Grove) special platforms: Squish Cap, Sprout Lift, Puff Float, Fairy Ring,
    /// Orbit Cap, Dizzy Spores, Spore Pop. Behaviour tests use small hand-built worlds; generator tests use the shipped region 2 levels with random
    /// attempt seeds. The region 1 counterpart is Region1PlatformTests.
    /// </summary>
    [Trait("Category", "Region2Platforms")]
    public class Region2PlatformTests
    {
        private readonly ITestOutputHelper output;
        public Region2PlatformTests(ITestOutputHelper output) { this.output = output; }

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
            s.Y = sim.PlatformY(id, 0);
            s.MaxY = s.Y; s.KillY = s.Y - Fx.FromInt(10);
            return s;
        }

        private static SimState Drop(Sim sim, int id, int xHundredths, int heightHundredths = 300)
        {
            SimState s = sim.NewState();
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

        private static void StepUntilGrounded(Sim sim, ref SimState s, int id, int maxTicks = 200)
        {
            for (int i = 0; i < maxTicks && !(s.Grounded && s.GroundId == id); i++) sim.Step(ref s, 0);
        }

        // ------------------------------------------------------------------ integer sine table

        [Fact]
        public void SinCosTable_MatchesTheRealSineWithin0Point003AndIsPeriodic()
        {
            foreach (int period in new[] { 252, 100, 64, 7 })
                for (int t = 0; t < period; t++)
                {
                    double a = 2 * Math.PI * t / period;
                    Assert.InRange(Math.Abs(D(SpecialPlatforms.Sin(t, period)) - Math.Sin(a)), 0, 0.003);
                    Assert.InRange(Math.Abs(D(SpecialPlatforms.Cos(t, period)) - Math.Cos(a)), 0, 0.003);
                }
            Assert.Equal(0, SpecialPlatforms.Sin(0, 252).Raw);
            Assert.Equal(Fx.One.Raw, SpecialPlatforms.Cos(0, 252).Raw);
            Assert.Equal(Fx.One.Raw, SpecialPlatforms.Sin(63, 252).Raw);
        }

        // ------------------------------------------------------------------ 2.1 Squish Cap

        [Fact]
        public void SquishCap_SinksPoint35UnitsInPoint8SecondsAndTheChargedJumpIs1Point45Times()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SquishCap, 500, 300, 600));
            SimState s = Stand(sim, 1);
            Steps(sim, ref s, 24);
            Assert.InRange(3.0 - D(s.Y), 0.16, 0.19);   // half way
            Steps(sim, ref s, 60);
            Assert.True(s.Grounded);
            Assert.InRange(3.0 - D(s.Y), 0.345, 0.351); // fully sunk
            Assert.Equal(sim.SquishSink(s, 1).Raw, (Fx.FromInt(3) - s.Y).Raw);

            SimConfig cfg = new SimConfig();
            SimState quick = Stand(sim, 1), charged = Stand(sim, 1);
            Steps(sim, ref charged, 60);
            sim.Step(ref quick, SimInput.Jump);
            sim.Step(ref charged, SimInput.Jump);
            double a = D(quick.Vy + cfg.Gravity), b = D(charged.Vy + cfg.Gravity);   // launch speeds (gravity of the jump tick added back)
            Assert.InRange(b / a, 1.44, 1.46);
            Assert.InRange(a, D(cfg.JumpBase) - 0.0001, D(cfg.JumpBase) + 0.0001);   // jumping at once is a plain jump, never worse
        }

        [Fact]
        public void SquishCap_AJumpFromAWalkedOffCapIsNotCharged()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SquishCap, 500, 300, 200));
            SimState s = Stand(sim, 1, 590);
            Steps(sim, ref s, 80);   // stood long enough to be squashed
            Assert.True(s.Grounded);
            // walk off the right edge, then jump in the coyote window: not grounded, so no charge
            for (int i = 0; i < 40 && s.Grounded; i++) sim.Step(ref s, SimInput.Right);
            Assert.False(s.Grounded);
            sim.Step(ref s, SimInput.Jump);
            SimConfig cfg = new SimConfig();
            Assert.True(s.Vy <= cfg.JumpSpeed(cfg.MaxSpeed), "coyote jump must not be charged, vy " + D(s.Vy));
        }

        // ------------------------------------------------------------------ 2.2 Sprout Lift

        [Fact]
        public void SproutLift_Rises1Point6UnitsIn1Point2SecondsStaysWhileStoodOnAndSinksBackWhenEmpty()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SproutLift, 500, 300, 400));
            SimState s = Drop(sim, 1, 500, 100);
            StepUntilGrounded(sim, ref s, 1);
            Steps(sim, ref s, 36);
            Assert.InRange(D(s.Y) - 3.0, 0.75, 0.85);
            Steps(sim, ref s, 40);
            Assert.InRange(D(s.Y) - 3.0, 1.58, 1.62);
            Steps(sim, ref s, 200);
            Assert.True(s.Grounded);
            Assert.InRange(D(s.Y) - 3.0, 1.58, 1.62);   // stays up while stood on
            Assert.Equal(sim.PlatformY(1, s.Tick, s.CrumbleTick).Raw, s.Y.Raw);

            // jump off: the empty sprout sinks 1.6 u in 1.0 s (120 percent of the rise speed)
            sim.Step(ref s, SimInput.Jump);
            Assert.False(s.Grounded);
            int left = s.Tick;
            Assert.True(s.CrumbleTick[1] < -1, "released state");
            Assert.InRange(D(sim.PlatformY(1, left + 30, s.CrumbleTick)) - 3.0, 0.75, 0.85);
            Assert.Equal(Fx.FromInt(3).Raw, sim.PlatformY(1, left + 60, s.CrumbleTick).Raw);
            Assert.Equal(Fx.FromInt(3).Raw, sim.PlatformY(1, left + 600, s.CrumbleTick).Raw);
        }

        [Fact]
        public void SproutLift_AFallingPlayerLandsOnTheRisenAndSinkingSurfaceAndTheRiseContinuesFromThere()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SproutLift, 500, 300, 400));
            SimState s = Drop(sim, 1, 500, 100);
            StepUntilGrounded(sim, ref s, 1);
            Steps(sim, ref s, 100);
            sim.Step(ref s, SimInput.Jump);
            // fall back onto the same sprout while it is still partly risen
            for (int i = 0; i < 300 && !s.Grounded; i++) sim.Step(ref s, 0);
            Assert.True(s.Grounded && s.GroundId == 1);
            Assert.Equal(sim.PlatformY(1, s.Tick, s.CrumbleTick).Raw, s.Y.Raw);
            Assert.True(s.CrumbleTick[1] >= 0, "occupied state again");
            Steps(sim, ref s, 100);
            Assert.InRange(D(s.Y) - 3.0, 1.58, 1.62);
        }

        [Fact]
        public void SproutLift_WalkingOffAlsoReleasesIt()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SproutLift, 500, 300, 200));
            SimState s = Drop(sim, 1, 580, 100);
            StepUntilGrounded(sim, ref s, 1);
            Steps(sim, ref s, 30);
            for (int i = 0; i < 60 && s.GroundId == 1 && s.Grounded; i++) sim.Step(ref s, SimInput.Right);
            Assert.False(s.Grounded && s.GroundId == 1);
            Assert.True(s.CrumbleTick[1] < -1);
            Assert.False(s.Dead);
        }

        [Fact]
        public void SproutLift_ReviveOnItDoesNotLeaveThePlayerInsideOrBelowIt()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SproutLift, 500, 300, 400));
            SimState s = Drop(sim, 1, 500, 100);
            StepUntilGrounded(sim, ref s, 1);
            Steps(sim, ref s, 100);
            s.Dead = true;
            sim.Revive(ref s);
            Assert.True(s.Grounded);
            Assert.Equal(sim.PlatformY(1, s.Tick, s.CrumbleTick).Raw, s.Y.Raw);
            Steps(sim, ref s, 20);
            Assert.True(s.Grounded && s.GroundId == 1);
        }

        // ------------------------------------------------------------------ 2.3 Puff Float

        [Fact]
        public void PuffFloat_SlowsTheFallAfterAJumpFromItFor1Point2SecondsAndNeverTheRise()
        {
            SimConfig cfg = new SimConfig();
            int[] airTicks = new int[2];
            double[] worstFall = new double[2];
            for (int variant = 0; variant < 2; variant++)
            {
                Sim sim = World(Start(), P(1, 1, variant == 0 ? PlatformKind.PuffFloat : PlatformKind.Normal, 500, 300, 600));
                SimState s = Stand(sim, 1);
                sim.Step(ref s, SimInput.Jump);
                double vyMax = 0;
                int n = 1;
                while (!s.Grounded && n < 400)
                {
                    sim.Step(ref s, 0);
                    if (D(s.Vy) > vyMax) vyMax = D(s.Vy);
                    worstFall[variant] = Math.Min(worstFall[variant], D(s.Vy));
                    n++;
                }
                airTicks[variant] = n;
                if (variant == 0) Assert.Equal(0, s.FloatTicks);   // landing ends it
            }
            output.WriteLine("air ticks puff " + airTicks[0] + " normal " + airTicks[1] + ", worst fall speed per tick " + worstFall[0].ToString("F4") + " vs " + worstFall[1].ToString("F4"));
            Assert.True(airTicks[0] > airTicks[1] + 15, "the puff jump must hang longer");
            Assert.True(worstFall[0] >= -D(cfg.PuffMaxFall) - 0.0001, "fall speed capped at 5 u/s");
            Assert.True(worstFall[1] < -D(cfg.PuffMaxFall), "a plain jump falls faster than 5 u/s");
        }

        [Fact]
        public void PuffFloat_RisingIsNormalAndTheEffectEndsAfter72TicksOfFalling()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.PuffFloat, 500, 300, 600));
            Sim plain = World(Start(), P(1, 1, PlatformKind.Normal, 500, 300, 600));
            SimState a = Stand(sim, 1), b = Stand(plain, 1);
            sim.Step(ref a, SimInput.Jump); plain.Step(ref b, SimInput.Jump);
            while (a.Vy > Fx.Zero) { Assert.Equal(b.Vy.Raw, a.Vy.Raw); Assert.Equal(b.Y.Raw, a.Y.Raw); sim.Step(ref a, 0); plain.Step(ref b, 0); }
            Assert.True(a.FloatTicks > 0 || a.Grounded);
            // the window runs while falling: 72 slow ticks, then normal gravity again
            SimConfig cfg = new SimConfig();
            SimState c = Stand(sim, 1);
            c.Grounded = false; c.Y = Fx.FromInt(200); c.Vy = -F(1, 100); c.FloatTicks = 72; c.GroundId = 0;
            Steps(sim, ref c, 72);
            Assert.Equal(0, c.FloatTicks);
            Assert.True(c.Vy >= -cfg.PuffMaxFall);
            sim.Step(ref c, 0);
            Assert.Equal(-cfg.PuffMaxFall.Raw - cfg.Gravity.Raw, c.Vy.Raw);
        }

        // ------------------------------------------------------------------ 2.4 Fairy Ring

        [Fact]
        public void FairyRing_TheHoleIsNoLandingSpotAndTheTwoHalvesAre()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.FairyRing, 500, 300, 600, ampHundredths: 70));
            foreach (int x in new[] { 500, 460, 540, 569, 431 })   // all inside the hole (half width 0.7)
            {
                SimState s = Drop(sim, 1, x, 100);
                Steps(sim, ref s, 150);
                Assert.True(s.Grounded && s.GroundId == 0, "x " + x + " must fall through the hole to the platform below");
            }
            foreach (int x in new[] { 300, 420, 575, 700, 790 })
            {
                SimState s = Drop(sim, 1, x, 100);
                Steps(sim, ref s, 60);
                Assert.True(s.Grounded && s.GroundId == 1, "x " + x + " must land on a half");
                Assert.Equal(1, s.Row);
            }
            SimState edge = Drop(sim, 1, 500 + 71, 100);
            Steps(sim, ref edge, 60);
            Assert.True(edge.Grounded && edge.GroundId == 1, "just outside the hole lands");
        }

        [Fact]
        public void FairyRing_SteppingIntoTheHoleDropsThePlayerButNeverKills()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.FairyRing, 500, 300, 600, ampHundredths: 70));
            SimState s = Stand(sim, 1, 350);
            Steps(sim, ref s, 90, SimInput.Right);   // walks right across the ring
            Assert.False(s.Dead);
            // a walker at 0.38 u per tick at full speed crosses a 1.4 u hole in 4 ticks: it must have fallen through or jumped over by the tick-based test
            Steps(sim, ref s, 150);
            Assert.False(s.Dead);
            Assert.True(s.GroundId == 0 || s.GroundId == 1);
            SimState inside = Stand(sim, 1, 500);
            sim.Step(ref inside, 0);
            Assert.False(inside.Grounded, "standing in the hole drops the player");
            Steps(sim, ref inside, 100);
            Assert.Equal(0, inside.GroundId);
            Assert.False(inside.Dead);
        }

        [Fact]
        public void FairyRing_ReviveNeverPutsThePlayerIntoTheHole()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.FairyRing, 500, 300, 600, ampHundredths: 80));
            SimState s = Stand(sim, 1, 300);
            s.Dead = true;
            sim.Revive(ref s);
            Steps(sim, ref s, 40);
            Assert.True(s.Grounded && s.GroundId == 1);
            Assert.True(Math.Abs(D(s.X) - 5.0) >= 0.8);
        }

        // ------------------------------------------------------------------ revive onto timed platforms (QA review findings 1 and 2)

        private static SimState DeadOn(Sim sim, int id, int tick, int clock)
        {
            SimState s = Stand(sim, id);
            s.Tick = tick;
            s.CrumbleTick[id] = clock;
            s.Dead = true;
            return s;
        }

        [Fact]
        public void Revive_OnAShutSnapDaisyDoesNotDropThePlayerAgain()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SnapDaisy, 500, 300, 300));
            SimState s = DeadOn(sim, 1, 120, 0);   // landed 120 ticks ago: shut
            Assert.False(sim.IsSolid(1, s.Tick, s.CrumbleTick));
            sim.Revive(ref s);
            Steps(sim, ref s, 60);
            Assert.True(s.Grounded && s.GroundId == 1);
        }

        [Fact]
        public void Revive_OnACrumbledCrumbleDoesNotDropThePlayerAgain()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.Crumble, 500, 300, 300));
            SimState s = DeadOn(sim, 1, 100, 0);
            Assert.False(sim.IsSolid(1, s.Tick, s.CrumbleTick));
            sim.Revive(ref s);
            Steps(sim, ref s, 20);
            Assert.True(s.Grounded && s.GroundId == 1);
        }

        [Fact]
        public void Revive_NeverPicksAHiddenVanishingPlatform()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.Vanishing, 500, 300, 300, phase: 160));   // hidden at tick 0
            SimState s = DeadOn(sim, 1, 0, -1);
            sim.Revive(ref s);
            Assert.Equal(0, s.GroundId);
            Steps(sim, ref s, 60);
            Assert.True(s.Grounded && s.GroundId == 0);
        }

        [Fact]
        public void Revive_OnASwellingSporePopResetsTheSwellClock()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SporePop, 500, 300, 400, dir: 1));
            SimState s = DeadOn(sim, 1, 70, 0);   // 2 ticks before the fling
            sim.Revive(ref s);
            Steps(sim, ref s, 10);
            Assert.Equal(0, s.SporePops);
            Assert.True(s.Grounded && s.GroundId == 1);
        }

        // ------------------------------------------------------------------ 2.5 Orbit Cap

        [Fact]
        public void OrbitCap_TravelsASmallEllipseWith4Point2SecondPeriodAndStaysWithinItsReach()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.OrbitCap, 500, 300, 300, phase: 0, period: 252, ampHundredths: 90));
            double minX = 99, maxX = -99, minY = 99, maxY = -99;
            for (int t = 0; t < 252; t++)
            {
                double x = D(sim.PlatformX(1, t)), y = D(sim.PlatformY(1, t));
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                Assert.Equal(sim.PlatformX(1, t).Raw, sim.PlatformX(1, t + 252).Raw);
                Assert.Equal(sim.PlatformY(1, t).Raw, sim.PlatformY(1, t + 252).Raw);
                double a = 2 * Math.PI * t / 252;
                Assert.InRange(Math.Abs(x - (5.0 + 0.9 * Math.Cos(a))), 0, 0.004);
                Assert.InRange(Math.Abs(y - (3.0 + 0.5 * Math.Sin(a))), 0, 0.003);
            }
            Assert.InRange(maxX - minX, 1.79, 1.81);
            Assert.InRange(maxY - minY, 0.99, 1.01);
            // always the same direction: x = cos, y = sin (counter-clockwise): a quarter lap after the right extreme the cap is at its top
            Assert.True(D(sim.PlatformY(1, 63)) > D(sim.PlatformY(1, 0)) && D(sim.PlatformX(1, 63)) < D(sim.PlatformX(1, 0)));
        }

        [Fact]
        public void OrbitCap_CarriesThePlayerOnItsLapAndAFallingPlayerLandsOnItAtEveryPhase()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.OrbitCap, 500, 300, 300, phase: 40, period: 252, ampHundredths: 90));
            SimState s = Stand(sim, 1);
            for (int t = 0; t < 600; t++)
            {
                sim.Step(ref s, 0);
                Assert.True(s.Grounded && s.GroundId == 1, "carried off at tick " + t);
                Assert.Equal(sim.PlatformY(1, s.Tick).Raw, s.Y.Raw);
                Assert.InRange(Math.Abs(D(s.X) - D(sim.PlatformX(1, s.Tick))), 0, 0.001);
            }
            for (int phase = 0; phase < 252; phase += 9)
            {
                Sim w = World(Start(), P(1, 1, PlatformKind.OrbitCap, 500, 300, 300, phase: phase, period: 252, ampHundredths: 90));
                SimState f = Drop(w, 1, 500, 150);
                Steps(w, ref f, 200);
                Assert.True(f.Grounded && f.GroundId == 1, "phase " + phase + " did not land");
            }
        }

        // ------------------------------------------------------------------ 2.6 Dizzy Spores

        [Fact]
        public void DizzySpores_IgnoresLeftRightFor1SecondAfterLandingButNeverInvertsAndJumpStaysPossible()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = World(Start(), P(1, 1, PlatformKind.DizzySpores, 500, 300, 600));
            SimState s = Drop(sim, 1, 500, 100);
            StepUntilGrounded(sim, ref s, 1);
            Assert.True(s.Grounded && s.SteerLock > 0);
            double x0 = D(s.X);
            Steps(sim, ref s, 50, SimInput.Right);
            Assert.True(s.Grounded);
            Assert.InRange(D(s.X) - x0, -0.001, 0.001);   // no movement: not inverted either
            Steps(sim, ref s, 20, SimInput.Right);
            Assert.True(D(s.X) - x0 > 0.2, "steering works again after the lock");
            Assert.Equal(0, s.SteerLock);
            // jump at once: allowed, and the air steering is locked until the 60 ticks are over
            SimState j = Drop(sim, 1, 500, 100);
            StepUntilGrounded(sim, ref j, 1);
            sim.Step(ref j, SimInput.Jump | SimInput.Right);
            Assert.False(j.Grounded);
            Steps(sim, ref j, 10, SimInput.Right);
            Assert.Equal(0, j.Vx.Raw);
            Assert.Equal(60, cfg.DizzyLockTicks);
        }

        [Fact]
        public void DizzySpores_TheLockEndsWithTheNextLandingAndNeverEndsTheRun()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.DizzySpores, 500, 300, 600));
            SimState s = Drop(sim, 1, 500, 100);
            StepUntilGrounded(sim, ref s, 1);
            sim.Step(ref s, SimInput.Jump);
            s.SteerLock = 50;
            for (int i = 0; i < 400 && !(s.Grounded && s.GroundId == 1 && i > 5); i++) sim.Step(ref s, 0);
            Rng rng = new Rng(7);
            for (int i = 0; i < 100; i++)
            {
                SimState r = Drop(sim, 1, 250 + rng.Next(500), 100 + rng.Next(200));
                for (int t = 0; t < 150; t++) sim.Step(ref r, (t % 31 == 0 ? SimInput.Jump : 0) | (rng.Next(3) == 0 ? SimInput.Left : (rng.Next(3) == 0 ? SimInput.Right : 0)));
                Assert.False(r.Dead);
                Assert.Equal(0, r.Hits);
            }
            // landing on a plain platform clears a running lock
            Sim w = World(Start());
            SimState d = Drop(w, 0, 500, 100);
            d.SteerLock = 55;
            Steps(w, ref d, 60);
            Assert.True(d.Grounded);
            Assert.Equal(0, d.SteerLock);
        }

        // ------------------------------------------------------------------ 2.7 Spore Pop

        [Fact]
        public void SporePop_SwellsFor1Point2SecondsThenFlingsAStandingPlayer2UnitsUpAndAboutThreeTowardItsDir()
        {
            foreach (int dir in new[] { 1, -1 })
            {
                Sim sim = World(Start(), P(1, 1, PlatformKind.SporePop, 500, 300, 600, dir: dir));
                SimState s = Drop(sim, 1, 500, 100);
                StepUntilGrounded(sim, ref s, 1);
                int land = s.Tick;
                int[] c = s.CrumbleTick;
                Assert.Equal(land, c[1]);
                Assert.Equal(1, sim.SporePopPhase(1, land + 71, c));
                Assert.Equal(2, sim.SporePopPhase(1, land + 72, c));
                Assert.Equal(2, sim.SporePopPhase(1, land + 191, c));
                Assert.Equal(0, sim.SporePopPhase(1, land + 192, c));
                while (s.Tick < land + 71) sim.Step(ref s, 0);
                Assert.True(s.Grounded);
                Assert.Equal(0, s.SporePops);
                sim.Step(ref s, 0);   // tick land + 72
                Assert.False(s.Grounded);
                Assert.Equal(1, s.SporePops);
                Assert.True(s.Vy > Fx.Zero);
                double x0 = D(s.X), peak = 0;
                for (int i = 0; i < 200 && !s.Grounded; i++) { sim.Step(ref s, 0); peak = Math.Max(peak, D(s.Y) - 3.0); }
                Assert.InRange(peak, 1.85, 2.15);
                Assert.InRange((D(s.X) - x0) * dir, 2.4, 3.4);
                Assert.False(s.Dead);
                Assert.Equal(0, s.Hits);
            }
        }

        [Fact]
        public void SporePop_LeavingBeforeThePopCostsNothingAndALandingDuringTheFlatPhaseStartsNoCountdown()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SporePop, 500, 300, 600, dir: 1));
            SimState s = Drop(sim, 1, 500, 100);
            StepUntilGrounded(sim, ref s, 1);
            Steps(sim, ref s, 40);
            sim.Step(ref s, SimInput.Jump);
            Steps(sim, ref s, 300);
            Assert.Equal(0, s.SporePops);
            Assert.False(s.Dead);

            // after a pop the flat platform is still solid; landing on it again does not restart the countdown until the flat phase is over
            SimState t = Drop(sim, 1, 500, 100);
            StepUntilGrounded(sim, ref t, 1);
            int land = t.CrumbleTick[1];
            Steps(sim, ref t, 75);
            Assert.Equal(1, t.SporePops);
            for (int i = 0; i < 300 && !(t.Grounded && t.GroundId == 1); i++) sim.Step(ref t, 0);
            Assert.True(t.Grounded && t.GroundId == 1);
            if (t.Tick < land + 192) Assert.Equal(land, t.CrumbleTick[1]);
            Steps(sim, ref t, 250);
            Assert.True(t.Dead == false);
        }

        [Fact]
        public void SporePop_ManyRandomLandingsNeverEndTheRunOrCountAsDamage()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SporePop, 500, 300, 500, dir: -1));
            Rng rng = new Rng(123);
            for (int i = 0; i < 500; i++)
            {
                SimState s = Drop(sim, 1, 250 + rng.Next(500), 100 + rng.Next(200));
                for (int t = 0; t < 260; t++) sim.Step(ref s, (t % 53 == 0 ? SimInput.Jump : 0) | (rng.Next(4) == 0 ? SimInput.Left : 0) | (rng.Next(4) == 0 ? SimInput.Right : 0));
                Assert.False(s.Dead);
                Assert.Equal(0, s.Hits);
            }
        }

        // ------------------------------------------------------------------ determinism and the whole kit on a generated level

        private static List<LoadedLevel> Region2()
        {
            string root = ContentLoader.FindRoot();
            return root == null ? new List<LoadedLevel>() : ContentLoader.LoadRegions(root, 2, 2);
        }

        private static long StateHash(SimState s)
        {
            long h = 17;
            unchecked
            {
                h = h * 31 + s.Tick; h = h * 31 + s.X.Raw; h = h * 31 + s.Y.Raw; h = h * 31 + s.Row; h = h * 31 + s.Score;
                h = h * 31 + s.SporePops; h = h * 31 + s.GroundId; h = h * 31 + s.SteerLock; h = h * 31 + s.FloatTicks; h = h * 31 + s.LandTick;
                if (s.CrumbleTick != null) foreach (int c in s.CrumbleTick) h = h * 31 + c;
            }
            return h;
        }

        [Fact]
        public void TheKitIsDeterministic_SameLevelSameInputsSameState()
        {
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in Region2().Where(l => l.Spec.LevelIndex >= 3))
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

        private static bool IsRow2Special(PlatformKind k)
        {
            return PlatformKinds.IsRegion2New(k) || k == PlatformKind.BounceCap || k == PlatformKind.Crumble || k == PlatformKind.Slippery;
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
                kinds[p.Row] = IsRow2Special(p.Kind) ? p.Kind : PlatformKind.Normal;
            }
            return kinds;
        }

        private static Fx FirstYOfRow(LevelData d, int row)
        {
            foreach (Platform p in d.Platforms) if (p.Row == row) return p.Y;
            return Fx.Zero;
        }

        [Fact]
        public void Region2UsesItsSevenKindsWithTheSignatureCapAndTwoGenericsAndTheOtherRegionsAreUntouched()
        {
            PlatformKind[] set = RegionPlatformSets.For(SignatureKind.BounceCaps);
            Assert.Equal(7, set.Length);
            Assert.All(set, k => Assert.True(PlatformKinds.IsRegion2New(k)));
            Assert.Equal(new[] { PlatformKind.Crumble, PlatformKind.Slippery }, RegionPlatformSets.GenericsFor(SignatureKind.BounceCaps));
            Assert.Empty(RegionPlatformSets.GenericsFor(SignatureKind.BounceFlowers));
            Assert.Equal(7, RegionPlatformSets.For(SignatureKind.StickyGum).Length);   // region 4 has its own set since D5
            Assert.Equal(7, RegionPlatformSets.For(SignatureKind.SwingingGongs).Length);   // region 5 got its seven kinds (Region5PlatformTests)
            Assert.True(PlatformKinds.IsHazardType(PlatformKind.DizzySpores) && PlatformKinds.IsHazardType(PlatformKind.SporePop));
            Assert.True(PlatformKinds.IsDemanding(PlatformKind.FairyRing) && PlatformKinds.IsDemanding(PlatformKind.OrbitCap));
            // the enum values of regions 1 and 2 are stable
            Assert.Equal(14, (int)PlatformKind.Seesaw);
            Assert.Equal(15, (int)PlatformKind.SquishCap);
            Assert.Equal(21, (int)PlatformKind.SporePop);
        }

        [Fact]
        public void OtherRegionsGetNoRegion2PlatformsAndRegion2GetsNoRegion1Platforms()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 4, 5))
                foreach (Platform p in LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(1))).Platforms)
                    Assert.False(PlatformKinds.IsRegion2New(p.Kind), ll.Id + " has " + p.Kind);
            foreach (LoadedLevel ll in Region2())
                foreach (Platform p in LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(1))).Platforms)
                    Assert.False(PlatformKinds.IsRegion1New(p.Kind), ll.Id + " has " + p.Kind);
        }

        [Fact]
        public void Region2SpecialShareFollowsTheDoc25RampAndLevel1HasNoHazardTypes()
        {
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = Region2();
            Assert.Equal(25, levels.Count);
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
            foreach (LoadedLevel ll in Region2().Where(l => l.Spec.FixedChunks.Length == 0))
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
        public void PlacementRulesHoldOnEveryRandomRegion2Layout()
        {
            SimConfig cfg = new SimConfig();
            List<string> bad = new List<string>();
            Dictionary<PlatformKind, int> seen = new Dictionary<PlatformKind, int>();
            foreach (LoadedLevel ll in Region2())
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
                    }
                    foreach (Platform p in d.Platforms)
                    {
                        if (p.Row < 1 || p.Row > d.Rows) continue;
                        if (p.Kind == PlatformKind.FairyRing)
                        {
                            if (p.Width < Fx.FromRatio(44, 10)) bad.Add(ll.Id + ": fairy ring narrower than 4.4 (" + D(p.Width) + ")");
                            if (p.Amplitude < Fx.FromRatio(6, 10) || p.Amplitude > Fx.FromRatio(8, 10)) bad.Add(ll.Id + ": hole half width " + D(p.Amplitude));
                            if (p.Width / 2 - p.Amplitude < Fx.FromRatio(14, 10)) bad.Add(ll.Id + ": a ring half is narrower than 1.4 u");
                        }
                        if (p.Kind == PlatformKind.OrbitCap)
                        {
                            if (p.Period != cfg.OrbitPeriodTicks) bad.Add(ll.Id + ": orbit period " + p.Period);
                            if (p.X - p.Width / 2 - p.Amplitude < Fx.Zero || p.X + p.Width / 2 + p.Amplitude > cfg.ScreenWidth) bad.Add(ll.Id + ": orbit lap leaves the walls");
                        }
                        if (p.Kind == PlatformKind.SporePop && p.Dir == 0) bad.Add(ll.Id + ": spore pop without direction");
                        if (p.Kind == PlatformKind.SporePop && (p.X < cfg.ScreenWidth / 2 ? p.Dir != 1 : p.Dir != -1)) bad.Add(ll.Id + ": spore pop does not point to the middle");
                        if (p.Kind == PlatformKind.SproutLift || p.Kind == PlatformKind.OrbitCap)
                        {
                            Fx lo = p.Kind == PlatformKind.SproutLift ? cfg.HiveGapMin : cfg.OrbitGapMin;
                            Fx hi = p.Kind == PlatformKind.SproutLift ? cfg.NormalGapMax : cfg.NormalGapMax - cfg.OrbitAmplitudeY;
                            Fx tol = Fx.FromRatio(1, 100);
                            Fx below = FirstYOfRow(d, p.Row - 1), above = FirstYOfRow(d, p.Row + 1);
                            if (p.Y - below < lo - tol || p.Y - below > hi + tol) bad.Add(ll.Id + " seed " + i + ": " + p.Kind + " gap below " + D(p.Y - below).ToString("F2"));
                            if (above.Raw != 0 && (above - p.Y < lo - tol || above - p.Y > hi + tol)) bad.Add(ll.Id + " seed " + i + ": " + p.Kind + " gap above " + D(above - p.Y).ToString("F2"));
                        }
                    }
                }
            }
            foreach (string s in bad.Take(30)) output.WriteLine(s);
            foreach (KeyValuePair<PlatformKind, int> kv in seen.OrderBy(k => (int)k.Key)) output.WriteLine(kv.Key + ": " + kv.Value);
            Assert.True(bad.Count == 0, bad.Count + " rule violations, first: " + (bad.Count > 0 ? bad[0] : ""));
            foreach (PlatformKind k in RegionPlatformSets.For(SignatureKind.BounceCaps)) Assert.True(seen.ContainsKey(k), k + " never placed");
            Assert.True(seen.ContainsKey(PlatformKind.BounceCap), "the cap is one of the options");
            Assert.True(seen.ContainsKey(PlatformKind.Crumble) && seen.ContainsKey(PlatformKind.Slippery), "the two generics are options");
        }

        [Fact]
        public void KindMixIsEvenAndHazardTypesStayAFifthOfTheSpecialRows()
        {
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in Region2().Where(l => l.Spec.LevelIndex >= 3 && l.Spec.FixedChunks.Length == 0))
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
                Assert.True(hazard <= special * 0.25 + 1, ll.Id + ": hazards " + hazard + " of " + special);
                Assert.True(launch <= special * 0.15 + 1, ll.Id + ": launchers " + launch + " of " + special);
                Assert.True(demanding <= special * 0.30 + 1, ll.Id + ": demanding " + demanding + " of " + special);
            }
        }

        // ------------------------------------------------------------------ bot proofs

        [Fact]
        public void BotSolvesEveryRandomRegion2LayoutOfLevels1To15With24Seeds()
        {
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = Region2().ToList();
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
            output.WriteLine("region 2 levels 1..15 x 24 seeds, unsolved: " + all.Count);
            Assert.True(all.Count == 0, all.Count + " unsolved layouts, first: " + (all.Count > 0 ? all[0] : ""));
        }

        [Fact]
        public void BotSolvesTheBaseLayoutOfEveryRegion2Level()
        {
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = Region2().ToList();
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

        private static LevelSpec Endless(int seed) { return EndlessTower.MakeSpec(seed, Fx.Zero, SignatureKind.BounceCaps, 400); }

        [Fact]
        public void Endless_Region2UsesTheNewPlatformsWithAFloorBasedRampAndTheSameRules()
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
                    if (IsRow2Special(p.Kind)) { kinds[p.Row] = p.Kind; seen.Add(p.Kind); }
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
            Assert.InRange(ep, 8, 16);   // endless cycle: floors 5-40 are the start of level 1 (10 to 30 percent ramp over its 100 floors)
            Assert.InRange(lp, 18, 30);   // floors 200-320 are levels 2 to 4 (Bug List 6 curve: 10 to 35 percent ramps)
            Assert.Equal(10, seen.Count);   // seven new platforms, the cap and the two generics appear
        }

        [Fact]
        public void Endless_Region2GrowthIsPrefixStable()
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
        public void Endless_TheBotClimbs60FloorsOfRegion2WithAllNewPlatforms()
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
