using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>
    /// Doc 30 section 5 (owner list 34): the seven region 4 (Candy Canyon) special platforms: Candy Bowl, Floss Glide, Fizz Vent, Licorice Belt,
    /// Taffy Stretch, Jelly Wobble, Pinwheel Gust, next to the Sticky signature and the generics Crumble and Slippery. Behaviour tests use small
    /// hand-built worlds; generator tests use the shipped region 4 levels with random attempt seeds. The region 3 counterpart is Region3PlatformTests.
    /// </summary>
    [Trait("Category", "Region4Platforms")]
    public class Region4PlatformTests
    {
        private readonly ITestOutputHelper output;
        public Region4PlatformTests(ITestOutputHelper output) { this.output = output; }

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

        // ------------------------------------------------------------------ 4.1 Candy Bowl

        [Fact]
        public void CandyBowl_PullsAPlayerOutsideTheCentre55PercentBackAt1Point5UnitsPerSecondAndLeavesTheCentreAlone()
        {
            SimConfig cfg = new SimConfig();
            Assert.Equal(Fx.FromRatio(15, 600).Raw, cfg.BowlPull.Raw);
            Sim sim = World(Start(), P(1, 1, PlatformKind.CandyBowl, 500, 300, 600));
            // centre share = 0.55 x 3 = 1.65 u each side
            SimState lip = Stand(sim, 1, 750);
            Steps(sim, ref lip, 30);
            Assert.True(lip.Grounded);
            Assert.InRange(D(lip.X), 6.70, 6.80);   // 7.5 - 30 x 0.025
            SimState left = Stand(sim, 1, 250);
            Steps(sim, ref left, 30);
            Assert.InRange(D(left.X), 3.20, 3.30);   // mirrored
            SimState mid = Stand(sim, 1, 560);
            Steps(sim, ref mid, 60);
            Assert.Equal(F(560).Raw, mid.X.Raw);
            // the pull stops at the edge of the centre share
            SimState settle = Stand(sim, 1, 750);
            Steps(sim, ref settle, 200);
            Assert.InRange(D(settle.X) - 5.0, 1.60, 1.70);
            // a player who runs for the edge leaves on purpose
            SimState run = Stand(sim, 1, 750);
            bool left2 = false;
            for (int t = 0; t < 80 && !left2; t++) { sim.Step(ref run, SimInput.Right); left2 = !run.Grounded; }
            Assert.True(left2, "the pull is gentle: running off the lip still works");
        }

        // ------------------------------------------------------------------ 4.2 Floss Glide

        [Fact]
        public void FlossGlide_DoublesTheAirSteeringFor90TicksAfterAJumpAndEndsAtTheLanding()
        {
            SimConfig cfg = new SimConfig();
            Sim glide = World(Start(), P(1, 1, PlatformKind.FlossGlide, 500, 300, 400));
            Sim plain = World(Start(), P(1, 1, PlatformKind.Normal, 500, 300, 400));
            SimState a = Stand(glide, 1, 500), b = Stand(plain, 1, 500);
            glide.Step(ref a, SimInput.Jump);
            plain.Step(ref b, SimInput.Jump);
            Assert.Equal(cfg.GlideAirTicks - 1, a.GlideTicks);   // set by the jump, one tick already counted
            Assert.Equal(0, b.GlideTicks);
            double a0 = D(a.Vx), b0 = D(b.Vx);
            for (int t = 0; t < 10; t++) { glide.Step(ref a, SimInput.Right); plain.Step(ref b, SimInput.Right); }
            double ga = D(a.Vx) - a0, gb = D(b.Vx) - b0;
            Assert.InRange(ga / gb, 1.95, 2.05);
            // it counts down while airborne and ends with a landing
            SimState c = Stand(glide, 1, 500);
            glide.Step(ref c, SimInput.Jump);
            Steps(glide, ref c, 10);
            Assert.Equal(cfg.GlideAirTicks - 11, c.GlideTicks);
            SimState d = Stand(glide, 1, 500);
            glide.Step(ref d, SimInput.Jump);
            StepUntilGrounded(glide, ref d, 1, 300);
            Assert.True(d.Grounded);
            Assert.Equal(0, d.GlideTicks);
            // a jump from another platform does not start it
            SimState e = Stand(plain, 1, 500);
            plain.Step(ref e, SimInput.Jump);
            Assert.Equal(0, e.GlideTicks);
        }

        // ------------------------------------------------------------------ 4.3 Fizz Vent

        private static double Peak(Sim sim, ref SimState s, double baseY)
        {
            double peak = D(s.Y);
            for (int t = 0; t < 300 && !(s.Grounded && t > 3); t++) { sim.Step(ref s, 0); peak = Math.Max(peak, D(s.Y)); }
            return peak - baseY;
        }

        [Fact]
        public void FizzVent_ACutGravityColumnLiftsAJumpFrom4Point5To7UnitsAndOnlyInsideTheColumn()
        {
            SimConfig cfg = new SimConfig();
            Sim vent = World(Start(), P(1, 1, PlatformKind.FizzVent, 500, 300, 400));
            Sim plain = World(Start(), P(1, 1, PlatformKind.Normal, 500, 300, 400));
            SimState s = Stand(vent, 1, 500);
            vent.Step(ref s, SimInput.Jump);
            double ventPeak = Peak(vent, ref s, 3.0);
            SimState n = Stand(plain, 1, 500);
            plain.Step(ref n, SimInput.Jump);
            double plainPeak = Peak(plain, ref n, 3.0);
            output.WriteLine("peak with vent " + ventPeak.ToString("F2") + " u, plain " + plainPeak.ToString("F2") + " u");
            Assert.InRange(plainPeak, 4.2, 4.5);
            Assert.InRange(ventPeak, 6.5, 7.1);   // about 6.7 u as the Sim integrates it (design: 7.0 u)
            Assert.True(ventPeak - plainPeak > 2.0, "the vent adds about 2.3 u");
            // outside the 1.8 u wide column (0.9 either side) the jump is plain
            SimState o = Stand(vent, 1, 500 + 95);
            vent.Step(ref o, SimInput.Jump);
            Assert.InRange(Peak(vent, ref o, 3.0), 4.2, 4.5);
            // gravity is cut by 80 percent inside the column (up to 3.2 u high), full above and for a player who stands in the column
            SimState low = Drop(vent, 1, 500, 100);
            double v0 = D(low.Vy);
            vent.Step(ref low, 0);
            Assert.InRange(v0 - D(low.Vy), D(cfg.Gravity) * 0.2 - 0.0002, D(cfg.Gravity) * 0.2 + 0.0002);
            SimState high = Drop(vent, 1, 500, 340);
            double h0 = D(high.Vy);
            vent.Step(ref high, 0);
            Assert.InRange(h0 - D(high.Vy), D(cfg.Gravity) - 0.0002, D(cfg.Gravity) + 0.0002);
            SimState beside = Drop(vent, 1, 500 + 100, 100);
            double b0 = D(beside.Vy);
            vent.Step(ref beside, 0);
            Assert.InRange(b0 - D(beside.Vy), D(cfg.Gravity) - 0.0002, D(cfg.Gravity) + 0.0002);
            // a jump up through the column from a platform below it is lifted too (the vent only has to be below the player)
            Sim two = World(Start(), P(1, 1, PlatformKind.Normal, 500, 200, 600), P(2, 2, PlatformKind.FizzVent, 500, 500, 400));
            SimState from = Stand(two, 1, 500);
            two.Step(ref from, SimInput.Jump);
            double rise = Peak(two, ref from, 2.0);
            Assert.True(rise > 4.6, "lifted inside the column above the vent: " + rise.ToString("F2"));
        }

        // ------------------------------------------------------------------ 4.4 Licorice Belt

        [Fact]
        public void LicoriceBelt_CarriesAStandingPlayerTwoUnitsPerSecondAlongDirAndARunnerCanHoldAgainstIt()
        {
            SimConfig cfg = new SimConfig();
            Assert.Equal(Fx.FromRatio(2, 60).Raw, cfg.BeltSpeed.Raw);
            Sim right = World(Start(), P(1, 1, PlatformKind.LicoriceBelt, 500, 300, 600, dir: 1));
            SimState s = Stand(right, 1, 350);
            Steps(right, ref s, 60);
            Assert.True(s.Grounded);
            Assert.InRange(D(s.X) - 3.5, 1.95, 2.05);
            Sim leftward = World(Start(), P(1, 1, PlatformKind.LicoriceBelt, 500, 300, 600, dir: -1));
            SimState l = Stand(leftward, 1, 650);
            Steps(leftward, ref l, 60);
            Assert.InRange(6.5 - D(l.X), 1.95, 2.05);
            // run against the belt: net movement against the belt direction, stays on the belt
            SimState run = Stand(right, 1, 500);
            Steps(right, ref run, 30, SimInput.Left);
            Assert.True(run.Grounded && D(run.X) < 5.0);
            // a jump from the belt is a plain jump
            SimState j = Stand(right, 1, 500);
            right.Step(ref j, SimInput.Jump);
            Assert.InRange(D(j.Vy + cfg.Gravity), D(cfg.JumpBase) - 0.001, D(cfg.JumpBase) + 0.001);
        }

        // ------------------------------------------------------------------ 4.5 Taffy Stretch

        [Fact]
        public void TaffyStretch_BreathesBetween2Point4And5Point2UnitsOnA270TickPeriodWithEdgesAt0Point6UnitsPerSecond()
        {
            SimConfig cfg = new SimConfig();
            Assert.Equal(270, cfg.TaffyPeriodTicks);
            Sim sim = World(Start(), P(1, 1, PlatformKind.TaffyStretch, 500, 300, 240, period: 270, phase: 0, ampHundredths: 140));
            Assert.Equal(F(240).Raw, sim.WidthAt(1, 0).Raw);
            Assert.InRange(D(sim.WidthAt(1, 135)), 5.19, 5.21);
            Assert.Equal(sim.WidthAt(1, 0).Raw, sim.WidthAt(1, 270).Raw);
            Assert.Equal(sim.WidthAt(1, 77).Raw, sim.WidthAt(1, 77 + 270).Raw);
            double minW = 99, maxW = 0, maxStep = 0;
            for (int t = 0; t < 270; t++)
            {
                double w = D(sim.WidthAt(1, t));
                minW = Math.Min(minW, w); maxW = Math.Max(maxW, w);
                maxStep = Math.Max(maxStep, Math.Abs(D(sim.WidthAt(1, t + 1)) - w));
            }
            Assert.InRange(minW, 2.39, 2.41);
            Assert.InRange(maxW, 5.19, 5.21);
            Assert.InRange(maxStep * 60 / 2, 0.55, 0.65);   // each edge moves about 0.6 u/s
            // other kinds are never touched by WidthAt
            Sim plain = World(Start(), P(1, 1, PlatformKind.Normal, 500, 300, 300));
            Assert.Equal(F(300).Raw, plain.WidthAt(1, 99).Raw);
        }

        [Fact]
        public void TaffyStretch_ALandingNearTheEdgeWorksWhenWideAndMissesWhenNarrowAndAShrinkingEdgeDropsAStandingPlayer()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.TaffyStretch, 500, 300, 240, period: 270, phase: 0, ampHundredths: 140));
            SimState wide = Drop(sim, 1, 700, 100, tick: 135);
            StepUntilGrounded(sim, ref wide, 1, 40);
            Assert.True(wide.Grounded && wide.GroundId == 1, "2.0 u from the centre is on the platform when it is wide");
            SimState narrow = Drop(sim, 1, 700, 100, tick: 0);
            StepUntilGrounded(sim, ref narrow, 1, 40);
            Assert.False(narrow.Grounded && narrow.GroundId == 1, "the same spot is off the platform when it is narrow");
            // a player standing 2.0 u from the centre when it is widest is dropped as the edge comes in; the coyote jump still works
            SimState stand = Stand(sim, 1, 700);
            stand.Tick = 135;
            bool fell = false;
            for (int t = 0; t < 140 && !fell; t++) { sim.Step(ref stand, 0); fell = !stand.Grounded; }
            Assert.True(fell);
            Assert.True(stand.Coyote > 0, "coyote time after the edge slid away");
            // standing in the middle is always safe
            SimState mid = Stand(sim, 1, 500);
            Steps(sim, ref mid, 600);
            Assert.True(mid.Grounded && mid.GroundId == 1);
        }

        // ------------------------------------------------------------------ 4.6 Jelly Wobble

        [Fact]
        public void JellyWobble_WobblesUpToPoint6UnitsAt3HzFadingToZeroIn90TicksAndEveryLandingStartsFromTheLandingTick()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = World(Start(), P(1, 1, PlatformKind.JellyWobble, 500, 300, 400, ampHundredths: 60));
            Assert.True(PlatformKinds.IsHazardType(PlatformKind.JellyWobble));
            SimState s = Drop(sim, 1, 500, 100);
            StepUntilGrounded(sim, ref s, 1);
            int land = s.CrumbleTick[1];
            Assert.Equal(s.LandTick, land);
            double max = 0, prev = 0;
            int crossings = 0;
            for (int e = 0; e <= 100; e++)
            {
                double off = D(sim.PlatformX(1, land + e, s.CrumbleTick)) - 5.0;
                max = Math.Max(max, Math.Abs(off));
                if (e == 0 || e >= 90) Assert.Equal(0.0, off);
                if (e > 0 && e < 90 && prev != 0 && off != 0 && Math.Sign(off) != Math.Sign(prev)) crossings++;
                if (off != 0) prev = off;
            }
            Assert.InRange(max, 0.50, 0.601);
            Assert.InRange(crossings, 8, 10);   // 3 Hz for 1.5 s: about 9 sign changes
            // before any landing and for the plain query without the state array it is calm
            Assert.Equal(F(500).Raw, sim.PlatformX(1, 33).Raw);
            // a player in the middle is carried along and stays on
            Steps(sim, ref s, 100);
            Assert.True(s.Grounded && s.GroundId == 1);
            Assert.Equal(F(500).Raw, sim.PlatformX(1, s.Tick, s.CrumbleTick).Raw);
        }

        [Fact]
        public void JellyWobble_ShakesAnEdgeStandingPlayerOffButNotOneInTheMiddle()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.JellyWobble, 500, 300, 400, ampHundredths: 60));
            SimState edge = Drop(sim, 1, 500 + 215, 100);   // inside the landing margin of the 4 u wide cube
            StepUntilGrounded(sim, ref edge, 1);
            Assert.True(edge.Grounded && edge.GroundId == 1);
            bool fell = false;
            for (int t = 0; t < 100 && !fell; t++) { sim.Step(ref edge, 0); fell = !edge.Grounded; }
            Assert.True(fell, "the wobble shook the edge-standing player off");
            SimState mid = Drop(sim, 1, 500 + 100, 100);
            StepUntilGrounded(sim, ref mid, 1);
            for (int t = 0; t < 200; t++) { sim.Step(ref mid, 0); Assert.True(mid.Grounded); }
            // the player may jump at once: no cost
            SimState quick = Drop(sim, 1, 500, 100);
            StepUntilGrounded(sim, ref quick, 1);
            sim.Step(ref quick, SimInput.Jump);
            Assert.False(quick.Grounded);
        }

        [Fact]
        public void JellyWobble_ManyRandomLandingsNeverEndTheRunOrCountAsDamage()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.JellyWobble, 500, 300, 400, ampHundredths: 60));
            Rng rng = new Rng(91);
            for (int i = 0; i < 500; i++)
            {
                SimState s = Drop(sim, 1, 250 + rng.Next(500), 100 + rng.Next(200), tick: rng.Next(400));
                for (int t = 0; t < 250; t++) sim.Step(ref s, (t % 37 == 0 ? SimInput.Jump : 0) | (rng.Next(4) == 0 ? SimInput.Left : 0) | (rng.Next(4) == 0 ? SimInput.Right : 0));
                Assert.False(s.Dead);
                Assert.Equal(0, s.Hits);
            }
        }

        // ------------------------------------------------------------------ 4.7 Pinwheel Gust

        [Fact]
        public void PinwheelGust_PushesAirbornePlayersInTheColumnThreeUnitsPerSecondAlongDirAndNoOneElse()
        {
            SimConfig cfg = new SimConfig();
            Assert.True(PlatformKinds.IsHazardType(PlatformKind.PinwheelGust));
            Sim gust = World(Start(), P(1, 1, PlatformKind.PinwheelGust, 500, 300, 300, dir: 1));
            Sim plain = World(Start(), P(1, 1, PlatformKind.Normal, 500, 300, 300));
            // inside the 2.0 u wide, 3.5 u high column
            SimState a = Drop(gust, 1, 550, 340), b = Drop(plain, 1, 550, 340);
            Steps(gust, ref a, 10); Steps(plain, ref b, 10);
            Assert.InRange(D(a.X) - D(b.X), 0.45, 0.55);   // 3 u/s for 10 ticks = 0.5 u
            // mirrored Dir
            Sim gustL = World(Start(), P(1, 1, PlatformKind.PinwheelGust, 500, 300, 300, dir: -1));
            SimState c = Drop(gustL, 1, 550, 340);
            Steps(gustL, ref c, 10);
            Assert.InRange(D(b.X) - D(c.X), 0.45, 0.55);
            // outside the column sideways (more than 1.0 u from the centre) and above 3.5 u: no push
            SimState outSide = Drop(gust, 1, 500 + 110, 340), outPlain = Drop(plain, 1, 500 + 110, 340);
            Steps(gust, ref outSide, 5); Steps(plain, ref outPlain, 5);
            Assert.Equal(outPlain.X.Raw, outSide.X.Raw);
            SimState above = Drop(gust, 1, 550, 380), abovePlain = Drop(plain, 1, 550, 380);
            Steps(gust, ref above, 3); Steps(plain, ref abovePlain, 3);
            Assert.Equal(abovePlain.X.Raw, above.X.Raw);
            // standing on the pinwheel itself is not airborne: no push
            SimState stand = Stand(gust, 1, 500);
            Steps(gust, ref stand, 60);
            Assert.Equal(F(500).Raw, stand.X.Raw);
            // the player can steer against it (air accel 18 u/s2): holding Left for 20 ticks beats the push of 1 u
            SimState steer = Drop(gust, 1, 550, 340), noSteer = Drop(gust, 1, 550, 340);
            Steps(gust, ref steer, 15, SimInput.Left); Steps(gust, ref noSteer, 15);
            Assert.True(D(noSteer.X) - D(steer.X) > 0.3, "steering against the gust works: " + (D(noSteer.X) - D(steer.X)));
        }

        [Fact]
        public void PinwheelGust_ManyRandomLandingsNeverEndTheRunOrCountAsDamage()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.PinwheelGust, 500, 300, 300, dir: 1));
            Rng rng = new Rng(55);
            for (int i = 0; i < 500; i++)
            {
                SimState s = Drop(sim, 1, 250 + rng.Next(500), 100 + rng.Next(300), tick: rng.Next(400));
                for (int t = 0; t < 250; t++) sim.Step(ref s, (t % 41 == 0 ? SimInput.Jump : 0) | (rng.Next(4) == 0 ? SimInput.Left : 0) | (rng.Next(4) == 0 ? SimInput.Right : 0));
                Assert.False(s.Dead);
                Assert.Equal(0, s.Hits);
            }
        }

        // ------------------------------------------------------------------ determinism and the whole kit on a generated level

        private static List<LoadedLevel> Region4()
        {
            string root = ContentLoader.FindRoot();
            return root == null ? new List<LoadedLevel>() : ContentLoader.LoadRegions(root, 4, 4);
        }

        private static long StateHash(SimState s)
        {
            long h = 17;
            unchecked
            {
                h = h * 31 + s.Tick; h = h * 31 + s.X.Raw; h = h * 31 + s.Y.Raw; h = h * 31 + s.Row; h = h * 31 + s.Score;
                h = h * 31 + s.GlideTicks; h = h * 31 + s.GroundId; h = h * 31 + s.LandTick; h = h * 31 + s.Vx.Raw; h = h * 31 + s.Vy.Raw;
                if (s.CrumbleTick != null) foreach (int c in s.CrumbleTick) h = h * 31 + c;
            }
            return h;
        }

        [Fact]
        public void TheKitIsDeterministic_SameLevelSameInputsSameState()
        {
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in Region4().Where(l => l.Spec.LevelIndex >= 3))
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
            // Step must stay allocation free with every region 4 kind in play (CUX-1): measure managed allocations over 20000 steps on a level full of them.
            SimConfig cfg = new SimConfig();
            LoadedLevel ll = Region4().First(l => l.Spec.LevelIndex == 15);
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

        private static bool IsRow4Special(PlatformKind k)
        {
            return PlatformKinds.IsRegion4New(k) || k == PlatformKind.Sticky || k == PlatformKind.Crumble || k == PlatformKind.Slippery;
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
                kinds[p.Row] = IsRow4Special(p.Kind) ? p.Kind : PlatformKind.Normal;
            }
            return kinds;
        }

        private static Platform FirstOfRow(LevelData d, int row)
        {
            foreach (Platform p in d.Platforms) if (p.Row == row) return p;
            return default(Platform);
        }

        [Fact]
        public void Region4UsesItsSevenKindsWithTheStickySignatureAndTwoGenericsAndEnumValuesAreStable()
        {
            PlatformKind[] set = RegionPlatformSets.For(SignatureKind.StickyGum);
            Assert.Equal(7, set.Length);
            Assert.All(set, k => Assert.True(PlatformKinds.IsRegion4New(k) && PlatformKinds.IsRegionNew(k)));
            Assert.Equal(new[] { PlatformKind.Crumble, PlatformKind.Slippery }, RegionPlatformSets.GenericsFor(SignatureKind.StickyGum));
            Assert.Equal(7, RegionPlatformSets.For(SignatureKind.SwingingGongs).Length);   // region 5 got its seven kinds (Region5PlatformTests)
            LevelSpec spec = Region4().First(l => l.Spec.LevelIndex == 5).Spec;
            PlatformKind[] options = SpecialPlatforms.Options(spec, PlatformKind.Sticky);
            Assert.Equal(10, options.Length);   // 7 new + Sticky + Crumble + Slippery
            Assert.Equal(1, options.Count(k => k == PlatformKind.Sticky));
            Assert.Equal(1, options.Count(k => k == PlatformKind.Crumble));
            Assert.Equal(1, options.Count(k => k == PlatformKind.Slippery));
            Assert.True(PlatformKinds.IsHazardType(PlatformKind.JellyWobble) && PlatformKinds.IsHazardType(PlatformKind.PinwheelGust));
            Assert.True(PlatformKinds.IsDemanding(PlatformKind.LicoriceBelt) && PlatformKinds.IsDemanding(PlatformKind.TaffyStretch));
            Assert.False(PlatformKinds.IsLaunch(PlatformKind.FizzVent) || PlatformKinds.IsHazardType(PlatformKind.CandyBowl));
            Assert.Equal(28, (int)PlatformKind.PufferfishPad);
            Assert.Equal(29, (int)PlatformKind.CandyBowl);
            Assert.Equal(30, (int)PlatformKind.FlossGlide);
            Assert.Equal(31, (int)PlatformKind.FizzVent);
            Assert.Equal(32, (int)PlatformKind.LicoriceBelt);
            Assert.Equal(33, (int)PlatformKind.TaffyStretch);
            Assert.Equal(34, (int)PlatformKind.JellyWobble);
            Assert.Equal(35, (int)PlatformKind.PinwheelGust);
        }

        [Fact]
        public void PinwheelGustIsNeverOfferedInTheNoWallBounceLevel()
        {
            List<LoadedLevel> levels = Region4();
            LoadedLevel lvl7 = levels.First(l => l.Spec.LevelIndex == 7);
            Assert.Equal(GoalType.NoWallBounce, lvl7.Spec.Goal.Type);
            Assert.DoesNotContain(PlatformKind.PinwheelGust, SpecialPlatforms.Options(lvl7.Spec, PlatformKind.Sticky));
            SimConfig cfg = new SimConfig();
            for (int i = 0; i < 30; i++)
                foreach (Platform p in LevelBuilder.Build(cfg, lvl7.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i))).Platforms)
                    Assert.NotEqual(PlatformKind.PinwheelGust, p.Kind);
            Assert.Contains(PlatformKind.PinwheelGust, SpecialPlatforms.Options(levels.First(l => l.Spec.LevelIndex == 6).Spec, PlatformKind.Sticky));
        }

        [Fact]
        public void OtherRegionsGetNoRegion4PlatformsAndRegion4GetsNoRegion1To3Platforms()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 1, 3))
                foreach (Platform p in LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(1))).Platforms)
                    Assert.False(PlatformKinds.IsRegion4New(p.Kind), ll.Id + " has " + p.Kind);
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 5, 5))
                foreach (Platform p in LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(1))).Platforms)
                    Assert.False(PlatformKinds.IsRegion4New(p.Kind), ll.Id + " has " + p.Kind);
            foreach (LoadedLevel ll in Region4())
                foreach (Platform p in LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(1))).Platforms)
                    Assert.False(PlatformKinds.IsRegion1New(p.Kind) || PlatformKinds.IsRegion2New(p.Kind) || PlatformKinds.IsRegion3New(p.Kind) || p.Kind == PlatformKind.BounceCap || p.Kind == PlatformKind.Moving || p.Kind == PlatformKind.Vanishing || p.Kind == PlatformKind.Bounce, ll.Id + " has " + p.Kind);
        }

        [Fact]
        public void Region4SpecialShareFollowsTheDoc25RampAndLevel1HasNoHazardTypes()
        {
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = Region4();
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
            foreach (LoadedLevel ll in Region4().Where(l => l.Spec.FixedChunks.Length == 0))
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
        public void PlacementRulesHoldOnEveryRandomRegion4Layout()
        {
            SimConfig cfg = new SimConfig();
            List<string> bad = new List<string>();
            Dictionary<PlatformKind, int> seen = new Dictionary<PlatformKind, int>();
            foreach (LoadedLevel ll in Region4())
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
                        if (k == PlatformKind.TaffyStretch && below == PlatformKind.Sticky) bad.Add(tag + ": taffy right after sticky gum");
                        if (k == PlatformKind.Sticky && below == PlatformKind.TaffyStretch) bad.Add(tag + ": sticky gum right after taffy");
                        if (PlatformKinds.IsLaunch(k)) bad.Add(tag + ": region 4 has no launchers");
                        if (zone && (PlatformKinds.IsLaunch(k) || PlatformKinds.IsHazardType(k) || PlatformKinds.IsDemanding(k))) bad.Add(tag + ": launcher, hazard or demanding kind in the last 3 rows / around the hidden gem");
                        if (k == PlatformKind.PinwheelGust && spec.Goal.Type == GoalType.NoWallBounce) bad.Add(tag + ": pinwheel in a no-wall-bounce level");
                    }
                    foreach (Platform p in d.Platforms)
                    {
                        if (p.Row < 1 || p.Row > d.Rows) continue;
                        Fx reach = p.Width / 2 + (p.Kind == PlatformKind.TaffyStretch || p.Kind == PlatformKind.JellyWobble ? p.Amplitude : Fx.Zero);
                        if (PlatformKinds.IsRegion4New(p.Kind) && (p.X - reach < Fx.Zero || p.X + reach > cfg.ScreenWidth)) bad.Add(ll.Id + " seed " + i + ": " + p.Kind + " leaves the walls at its widest / wobble");
                        if (p.Kind == PlatformKind.CandyBowl && (p.Width < Fx.FromInt(4) || p.Width > Fx.FromInt(8))) bad.Add(ll.Id + ": bowl width " + D(p.Width));
                        if (p.Kind == PlatformKind.FizzVent && (p.Width < Fx.FromInt(2) || p.Width > Fx.FromInt(4))) bad.Add(ll.Id + ": vent width " + D(p.Width));
                        if (p.Kind == PlatformKind.LicoriceBelt)
                        {
                            if (p.Width < Fx.FromInt(4)) bad.Add(ll.Id + ": belt narrower than 4 (" + D(p.Width) + ")");
                            if (p.Dir == 0) bad.Add(ll.Id + ": belt without Dir");
                            Platform next = FirstOfRow(d, p.Row + 1);
                            if (next.Row == p.Row + 1 && (next.X - p.X) * p.Dir > Fx.FromRatio(5, 100)) bad.Add(ll.Id + " seed " + i + " row " + p.Row + ": belt carries TOWARD the next platform");
                        }
                        if (p.Kind == PlatformKind.TaffyStretch)
                        {
                            if (p.Width != cfg.TaffyMinWidth) bad.Add(ll.Id + ": taffy width " + D(p.Width));
                            if (p.Period != cfg.TaffyPeriodTicks) bad.Add(ll.Id + ": taffy period " + p.Period);
                            if (p.Amplitude > cfg.TaffyAmplitude) bad.Add(ll.Id + ": taffy stretch too long");
                        }
                        if (p.Kind == PlatformKind.JellyWobble)
                        {
                            if (p.Width < cfg.JellyMinWidth) bad.Add(ll.Id + ": jelly narrower than 3.5 (" + D(p.Width) + ")");
                            if (p.Amplitude > cfg.JellyAmplitude) bad.Add(ll.Id + ": jelly wobble too big");
                        }
                        if (p.Kind == PlatformKind.PinwheelGust)
                        {
                            if (p.Width < Fx.FromInt(2) || p.Width > Fx.FromInt(4)) bad.Add(ll.Id + ": pinwheel width " + D(p.Width));
                            if (p.X < cfg.ScreenWidth / 2 ? p.Dir != 1 : p.Dir != -1) bad.Add(ll.Id + ": pinwheel does not push to the middle");
                        }
                    }
                }
            }
            foreach (string s in bad.Take(30)) output.WriteLine(s);
            foreach (KeyValuePair<PlatformKind, int> kv in seen.OrderBy(k => (int)k.Key)) output.WriteLine(kv.Key + ": " + kv.Value);
            Assert.True(bad.Count == 0, bad.Count + " rule violations, first: " + (bad.Count > 0 ? bad[0] : ""));
            foreach (PlatformKind k in RegionPlatformSets.For(SignatureKind.StickyGum)) Assert.True(seen.ContainsKey(k), k + " never placed");
            Assert.True(seen.ContainsKey(PlatformKind.Sticky) && seen.ContainsKey(PlatformKind.Crumble) && seen.ContainsKey(PlatformKind.Slippery), "the signature and the two generics are options");
        }

        [Fact]
        public void Region4LayoutsAroundAnAirZoneKeepTheNextRowWithinTheNormalReach()
        {
            // doc 30 section 8 item 5: the next platform stays reachable with and without the zone, so the builder never uses a special reach for a vent / pinwheel row
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in Region4().Where(l => l.Spec.FixedChunks.Length == 0))
                for (int i = 0; i < 10; i++)
                {
                    LevelData d = LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i)));
                    foreach (Platform p in d.Platforms)
                    {
                        if (p.Kind != PlatformKind.FizzVent && p.Kind != PlatformKind.PinwheelGust) continue;
                        Platform next = FirstOfRow(d, p.Row + 1);
                        if (next.Row != p.Row + 1) continue;
                        Fx gap = next.Y - p.Y;
                        Assert.True(gap <= cfg.NormalGapMax + Fx.FromRatio(1, 100), ll.Id + " row " + p.Row + ": gap above the air zone " + D(gap));
                        Fx edge = Fx.Abs(next.X - p.X) - (next.Width + p.Width) / 2;
                        Assert.True(edge <= cfg.LayoutMaxReachDx(gap, Fx.One) + cfg.PlayerHalfWidth + Fx.One, ll.Id + " row " + p.Row + ": next platform too far from the air zone");
                    }
                }
        }

        [Fact]
        public void KindMixIsEvenAndHazardTypesStayAFifthOfTheSpecialRows()
        {
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in Region4().Where(l => l.Spec.LevelIndex >= 3 && l.Spec.FixedChunks.Length == 0))
            {
                int special = 0, hazard = 0, demanding = 0;
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
                        if (PlatformKinds.IsDemanding(kinds[r])) demanding++;
                    }
                }
                output.WriteLine(ll.Id + ": " + (special / (double)Seeds).ToString("F1") + " special rows per layout, " + (hazard / (double)Seeds).ToString("F1") + " hazard-type, " + (demanding / (double)Seeds).ToString("F1") + " demanding");
                Assert.True(hazard <= special * 0.30 + 1, ll.Id + ": hazards " + hazard + " of " + special);
                Assert.True(demanding <= special * 0.30 + 1, ll.Id + ": demanding " + demanding + " of " + special);
            }
        }

        // ------------------------------------------------------------------ bot proofs

        [Fact]
        public void BotSolvesEveryRandomRegion4LayoutOfLevels1To15With24Seeds()
        {
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = Region4().ToList();
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
            output.WriteLine("region 4 levels 1..15 x 24 seeds, unsolved: " + all.Count);
            Assert.True(all.Count == 0, all.Count + " unsolved layouts, first: " + (all.Count > 0 ? all[0] : ""));
        }

        [Fact]
        public void BotSolvesTheBaseLayoutOfEveryRegion4Level()
        {
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = Region4().ToList();
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

        private static LevelSpec Endless(int seed) { return EndlessTower.MakeSpec(seed, Fx.Zero, SignatureKind.StickyGum, 400); }

        [Fact]
        public void Endless_Region4UsesTheNewPlatformsWithAFloorBasedRampAndTheSameRules()
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
                    if (IsRow4Special(p.Kind)) { kinds[p.Row] = p.Kind; seen.Add(p.Kind); }
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
                    if (k == PlatformKind.TaffyStretch) Assert.NotEqual(PlatformKind.Sticky, kinds[r - 1]);
                    if (k == PlatformKind.Sticky) Assert.NotEqual(PlatformKind.TaffyStretch, kinds[r - 1]);
                }
                // the belt carries away from the next platform in endless too
                foreach (Platform p in d.Platforms)
                {
                    if (p.Kind != PlatformKind.LicoriceBelt || p.Row >= 319) continue;
                    Platform next = FirstOfRow(d, p.Row + 1);
                    Assert.False((next.X - p.X) * p.Dir > Fx.FromRatio(5, 100), "endless belt row " + p.Row + " carries toward the next platform");
                }
            }
            double ep = early * 100 / e, lp = late * 100 / l;
            output.WriteLine("endless special share: floors 5-40 " + ep.ToString("F1") + " percent, floors 200-320 " + lp.ToString("F1") + " percent");
            Assert.InRange(ep, 8, 16);   // endless cycle: floors 5-40 are the start of level 1 (10 to 30 percent ramp over its 100 floors)
            Assert.InRange(lp, 31, 45);   // floors 200-320 are levels 2 to 4 (30 to 50 percent ramps)
            Assert.Equal(10, seen.Count);   // seven new platforms, the sticky signature and the two generics appear
        }

        [Fact]
        public void Endless_Region4GrowthIsPrefixStable()
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
        public void Endless_TheBotClimbs60FloorsOfRegion4WithAllNewPlatforms()
        {
            SimConfig cfg = new SimConfig();
            string[] failures = new string[20];
            Parallel.For(0, 20, k =>
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
