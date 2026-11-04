using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>
    /// Doc 30 section 15 (Bug List 3 item D5, region 5 half): the seven region 5 (Bamboo Temple) special platforms: Bamboo Flick, Lantern Lift, Koi Stone,
    /// Swing Plank, Pagoda Ridge, Mallet Smash, Rolling Log, next to the generics Moving and Vanishing (the swinging gongs are hazard objects, not a platform kind).
    /// Behaviour tests use small hand-built worlds; generator tests use the shipped region 5 levels with random attempt seeds. The region 4 counterpart is Region4PlatformTests.
    /// </summary>
    [Trait("Category", "Region5Platforms")]
    public class Region5PlatformTests
    {
        private readonly ITestOutputHelper output;
        public Region5PlatformTests(ITestOutputHelper output) { this.output = output; }

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

        private static SimState Stand(Sim sim, int id, int xHundredths = int.MinValue, int tick = 0)
        {
            SimState s = sim.NewState();
            Platform p = sim.Platforms[id];
            s.GroundId = id; s.BestGroundId = id; s.Grounded = true; s.Row = p.Row; s.JumpStartRow = p.Row; s.Tick = tick;
            s.X = xHundredths == int.MinValue ? sim.PlatformX(id, tick) : F(xHundredths);
            s.Y = sim.PlatformY(id, tick);
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

        private static double Peak(Sim sim, ref SimState s, double baseY)
        {
            double peak = D(s.Y);
            for (int t = 0; t < 300 && !(s.Grounded && t > 3); t++) { sim.Step(ref s, 0); peak = Math.Max(peak, D(s.Y)); }
            return peak - baseY;
        }

        // ------------------------------------------------------------------ 5.3 Bamboo Flick

        [Fact]
        public void BambooFlick_FlicksPoint8OfTheStandingJumpAndEightUnitsPerSecondTowardDirWhateverTheLandingSpeed()
        {
            SimConfig cfg = new SimConfig();
            Assert.Equal(Fx.FromRatio(8, 60).Raw, cfg.FlickVx.Raw);
            Sim sim = World(Start(), P(1, 1, PlatformKind.BambooFlick, 200, 300, 1000, dir: 1));   // a wide stalk seat here only so the sideways drift of the test landings still hits it
            foreach (int vx in new[] { 0, 3, -4 })
            {
                SimState s = Drop(sim, 1, 200);
                s.Vx = F(vx, 60);
                for (int t = 0; t < 100 && s.GroundId != 1; t++) sim.Step(ref s, 0);
                Assert.Equal(1, s.GroundId);
                Assert.False(s.Grounded, "a flick launches in the landing step");
                Assert.Equal((cfg.JumpBase * cfg.FlickLaunch).Raw, s.Vy.Raw);
                Assert.Equal(cfg.FlickVx.Raw, s.Vx.Raw);
            }
            // peak: 0.8 of the standing speed = 0.64 of the jump height (about 2.9 u)
            SimState a = Drop(sim, 1, 200);
            for (int t = 0; t < 100 && a.GroundId != 1; t++) sim.Step(ref a, 0);
            double peak = Peak(sim, ref a, 3.0);
            output.WriteLine("flick peak " + peak.ToString("F2") + " u");
            Assert.InRange(peak, 2.7, 2.9);
            // a mirrored flick goes left
            Sim left = World(Start(), P(1, 1, PlatformKind.BambooFlick, 800, 300, 300, dir: -1));
            SimState b = Drop(left, 1, 800);
            for (int t = 0; t < 100 && b.GroundId != 1; t++) left.Step(ref b, 0);
            Assert.Equal(-cfg.FlickVx.Raw, b.Vx.Raw);
            Assert.True(PlatformKinds.IsLaunch(PlatformKind.BambooFlick) && PlatformKinds.IsRegion5New(PlatformKind.BambooFlick));
        }

        [Fact]
        public void BambooFlick_TheUnsteeredFlightReachesThePlatformPlacedForIt()
        {
            // the next row is FlickNextDx 3.6 to 4.6 u away and FlickGap 2.0 to 2.2 u higher: with no steering at all the flight lands on it (platform width 2)
            SimConfig cfg = new SimConfig();
            int landed = 0, tried = 0;
            for (int dx = 360; dx <= 460; dx += 20)
                for (int gap = 200; gap <= 220; gap += 10)
                {
                    tried++;
                    Sim sim = World(Start(), P(1, 1, PlatformKind.BambooFlick, 200, 300, 300, dir: 1), P(2, 2, PlatformKind.Normal, 200 + dx, 300 + gap, 200));
                    SimState s = Drop(sim, 1, 200);
                    for (int t = 0; t < 200 && !(s.Grounded && s.GroundId == 2); t++) sim.Step(ref s, 0);
                    if (s.Grounded && s.GroundId == 2) landed++;
                }
            Assert.Equal(tried, landed);
            Assert.True(cfg.FlickGapMin >= Fx.FromInt(2) && cfg.FlickGapMax <= Fx.FromRatio(22, 10));
        }

        // ------------------------------------------------------------------ 5.x Lantern Lift

        [Fact]
        public void LanternLift_Rises2UnitsIn90TicksWhileStoodOnSinksBackSlowlyWhenEmptyAndSwaysPoint3Units()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = World(Start(), P(1, 1, PlatformKind.LanternLift, 500, 300, 300, phase: 0, period: 180, ampHundredths: 30));
            // sway: sine of 180 ticks, +-0.3 u
            Assert.Equal(500, (int)Math.Round(D(sim.PlatformX(1, 0)) * 100));
            Assert.InRange(D(sim.PlatformX(1, 45)), 5.29, 5.31);
            Assert.InRange(D(sim.PlatformX(1, 135)), 4.69, 4.71);
            // landing starts the rise; rise = 2.0 u in 90 ticks, then it stays up while stood on
            SimState s = Drop(sim, 1, 500, 100);
            for (int t = 0; t < 100 && !s.Grounded; t++) sim.Step(ref s, 0);
            Assert.True(s.Grounded && s.GroundId == 1);
            int landTick = s.Tick;
            Steps(sim, ref s, 45);
            Assert.InRange(D(s.Y) - 3.0, 0.95, 1.05);   // half way
            Steps(sim, ref s, 45);
            Assert.InRange(D(s.Y) - 3.0, 1.97, 2.03);
            Steps(sim, ref s, 120);
            Assert.True(s.Grounded);
            Assert.InRange(D(s.Y) - 3.0, 1.97, 2.03);   // stays up
            // the player rides the sway
            Assert.InRange(Math.Abs(D(s.X) - D(sim.PlatformX(1, s.Tick))), 0.0, 0.31);
            // jump off: the empty lantern floats down at 60 percent of the rise speed (150 ticks for the whole 2 u)
            double before = D(sim.PlatformY(1, s.Tick, s.CrumbleTick));
            for (int t = 0; t < 200 && s.Grounded && s.GroundId == 1; t++) sim.Step(ref s, SimInput.Right);   // walk off the edge (the player lands on the start platform below)
            Assert.False(s.Grounded && s.GroundId == 1);
            Steps(sim, ref s, 75);
            double mid = D(sim.PlatformY(1, s.Tick, s.CrumbleTick)) - 3.0;
            Assert.InRange(mid, 0.9, 1.15);   // about 2.0 - 75 ticks x 0.8 u/s
            Steps(sim, ref s, 90);
            Assert.Equal(0.0, D(sim.PlatformY(1, s.Tick, s.CrumbleTick)) - 3.0, 3);
            Assert.True(before > 4.9);
            Assert.True(PlatformKinds.IsTimeDependent(PlatformKind.LanternLift) && !PlatformKinds.IsHazardType(PlatformKind.LanternLift) && !PlatformKinds.IsDemanding(PlatformKind.LanternLift));
        }

        [Fact]
        public void LanternLift_ARisenLanternLiftsAJumpByItsRiseAndIsDifferentFromTheSproutLift()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = World(Start(), P(1, 1, PlatformKind.LanternLift, 500, 300, 300, phase: 0, period: 180, ampHundredths: 0));
            SimState s = Drop(sim, 1, 500, 100);
            for (int t = 0; t < 100 && !s.Grounded; t++) sim.Step(ref s, 0);
            Steps(sim, ref s, 100);
            double topY = D(s.Y);
            Assert.InRange(topY - 3.0, 1.97, 2.03);
            sim.Step(ref s, SimInput.Jump);
            double peak = Peak(sim, ref s, 3.0);
            Assert.InRange(peak, 6.3, 6.6);   // 2.0 u of lift + the 4.5 u standing jump
            Assert.NotEqual(cfg.SproutRise.Raw, cfg.LanternRise.Raw);
            Assert.NotEqual(cfg.SproutRiseTicks, cfg.LanternRiseTicks);
            Assert.NotEqual(cfg.SproutSinkPercent, cfg.LanternSinkPercent);
            // a landing from above on a half-risen lantern works (the surface moves up into the player)
            SimState d = Drop(sim, 1, 500, 400);
            for (int t = 0; t < 120 && !d.Grounded; t++) sim.Step(ref d, 0);
            Assert.True(d.Grounded && d.GroundId == 1);
        }

        // ------------------------------------------------------------------ 5.x Koi Stone

        [Fact]
        public void KoiStone_StandingStillForOneSecondChargesTheNextJumpPlus10PercentHeightAndFullAirSteering()
        {
            SimConfig cfg = new SimConfig();
            Sim koi = World(Start(), P(1, 1, PlatformKind.KoiStone, 500, 300, 700));
            Sim plain = World(Start(), P(1, 1, PlatformKind.Normal, 500, 300, 700));
            SimState n = Stand(plain, 1, 500);
            plain.Step(ref n, SimInput.Jump);
            double plainPeak = Peak(plain, ref n, 3.0);

            SimState early = Stand(koi, 1, 500);
            Steps(koi, ref early, cfg.KoiStillTicks - 1);
            koi.Step(ref early, SimInput.Jump);
            Assert.Equal(0, early.KoiAir);
            Assert.InRange(Peak(koi, ref early, 3.0) / plainPeak, 0.999, 1.001);

            SimState rested = Stand(koi, 1, 500);
            Steps(koi, ref rested, cfg.KoiStillTicks);
            Assert.Equal(cfg.KoiStillTicks, rested.KoiStill);
            koi.Step(ref rested, SimInput.Jump);
            Assert.Equal(cfg.KoiAirTicks - 1, rested.KoiAir);
            double peak = Peak(koi, ref rested, 3.0);
            output.WriteLine("koi peak " + peak.ToString("F2") + " u, plain " + plainPeak.ToString("F2") + " u");
            Assert.InRange(peak / plainPeak, 1.09, 1.12);   // +10 percent height (launch speed x 1.05)
            Assert.Equal(0, rested.KoiAir);                 // the charge ends at the landing

            // moving resets the rest
            SimState walker = Stand(koi, 1, 500);
            Steps(koi, ref walker, 50);
            Steps(koi, ref walker, 3, SimInput.Right);
            Assert.Equal(0, walker.KoiStill);
            Steps(koi, ref walker, 40);   // braking to a stop and standing again
            koi.Step(ref walker, SimInput.Jump);
            Assert.Equal(0, walker.KoiAir);

            // the charged flight keeps the full ground steering (AirControl is 0.6), ending after KoiAirTicks
            SimState c = Stand(koi, 1, 500);
            SimState p = Stand(plain, 1, 500);
            Steps(koi, ref c, cfg.KoiStillTicks);
            koi.Step(ref c, SimInput.Jump);
            plain.Step(ref p, SimInput.Jump);
            double c0 = D(c.Vx), p0 = D(p.Vx);
            for (int t = 0; t < 10; t++) { koi.Step(ref c, SimInput.Right); plain.Step(ref p, SimInput.Right); }
            double ratio = (D(c.Vx) - c0) / (D(p.Vx) - p0);
            Assert.InRange(ratio, 1.6, 1.72);   // 1 / 0.6
            // only a Koi Stone does it: the stone never moves and is at least 5 u wide
            Assert.True(SpecialPlatforms.ConstrainWidth(cfg, PlatformKind.KoiStone, Fx.FromInt(1)) >= Fx.FromInt(5));
            Assert.Equal(koi.PlatformX(1, 0).Raw, koi.PlatformX(1, 5000).Raw);
            Assert.Equal(koi.PlatformY(1, 0).Raw, koi.PlatformY(1, 5000).Raw);
        }

        // ------------------------------------------------------------------ 5.4 Swing Plank

        [Fact]
        public void SwingPlank_SwingsPlusMinus1Point5UnitsOnA264TickPendulumRisingPoint6AtTheEndsAndCarriesThePlayer()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = World(Start(), P(1, 1, PlatformKind.SwingPlank, 500, 300, 450, phase: 0, period: 264, ampHundredths: 150));
            Assert.Equal(5.0, D(sim.PlatformX(1, 0)), 3);
            Assert.InRange(D(sim.PlatformX(1, 66)), 6.49, 6.51);    // quarter period: full swing right
            Assert.InRange(D(sim.PlatformX(1, 198)), 3.49, 3.51);   // three quarters: full swing left
            Assert.Equal(5.0, D(sim.PlatformX(1, 132)), 2);
            Assert.Equal(3.0, D(sim.PlatformY(1, 0)), 3);           // lowest in the middle
            Assert.Equal(3.0, D(sim.PlatformY(1, 132)), 2);
            Assert.InRange(D(sim.PlatformY(1, 66)), 3.59, 3.61);    // 0.6 higher at the ends
            Assert.InRange(D(sim.PlatformY(1, 198)), 3.59, 3.61);
            // slow at the ends, fast in the middle
            double mid = Math.Abs(D(sim.PlatformX(1, 133)) - D(sim.PlatformX(1, 131)));
            double end = Math.Abs(D(sim.PlatformX(1, 67)) - D(sim.PlatformX(1, 65)));
            Assert.True(mid > end * 5, "speed in the middle " + mid + " vs at the ends " + end);
            // the player standing on it rides along (x and y)
            SimState s = Stand(sim, 1, 500);
            Steps(sim, ref s, 66);
            Assert.True(s.Grounded);
            Assert.InRange(D(s.X), 6.45, 6.55);
            Assert.InRange(D(s.Y), 3.55, 3.65);
            // a landing on the swinging plank works (also when it comes up from below the player)
            int landed = 0;
            for (int phase = 0; phase < 264; phase += 24)
            {
                Sim w = World(Start(), P(1, 1, PlatformKind.SwingPlank, 500, 300, 450, phase: phase, period: 264, ampHundredths: 150));
                SimState d = Drop(w, 1, (int)Math.Round(D(w.PlatformX(1, 0)) * 100), 300);
                for (int t = 0; t < 120 && !d.Grounded; t++) w.Step(ref d, 0);
                if (d.Grounded && d.GroundId == 1) landed++;
            }
            Assert.Equal(11, landed);
            Assert.True(PlatformKinds.IsDemanding(PlatformKind.SwingPlank) && PlatformKinds.IsTimeDependent(PlatformKind.SwingPlank));
            Assert.Equal(cfg.PlankPeriodTicks, 264);
        }

        // ------------------------------------------------------------------ 5.5 Pagoda Ridge

        [Fact]
        public void PagodaRidge_OnlyTheMiddleMetreIsFlatAndTheSlopesSlideOutwardAt2Point4UnitsPerSecond()
        {
            SimConfig cfg = new SimConfig();
            Assert.Equal(Fx.FromRatio(24, 600).Raw, cfg.RidgeSlide.Raw);
            Sim sim = World(Start(), P(1, 1, PlatformKind.PagodaRidge, 500, 300, 450));
            // in the flat middle (0.5 u either side) nothing moves
            SimState mid = Stand(sim, 1, 540);
            Steps(sim, ref mid, 120);
            Assert.True(mid.Grounded);
            Assert.Equal(F(540).Raw, mid.X.Raw);
            // on the slope: 0.04 u per tick outward, on both sides
            SimState right = Stand(sim, 1, 600);
            Steps(sim, ref right, 20);
            Assert.InRange(D(right.X), 6.79, 6.81);
            SimState left = Stand(sim, 1, 400);
            Steps(sim, ref left, 20);
            Assert.InRange(D(left.X), 3.19, 3.21);
            // it carries a player who just stands there off the end (2.25 + 0.25 u from the centre)
            SimState off = Stand(sim, 1, 600);
            Steps(sim, ref off, 120);
            Assert.False(off.Grounded && off.GroundId == 1, "the slope slides a still player off");
            // jumping at once leaves normally (the jump comes before the slide)
            SimState jump = Stand(sim, 1, 700);
            sim.Step(ref jump, SimInput.Jump);
            Assert.False(jump.Grounded);
            Assert.InRange(D(jump.X), 6.99, 7.01);
            // a runner can hold against the slide and walk back to the middle
            SimState run = Stand(sim, 1, 650);
            Steps(sim, ref run, 30, SimInput.Left);
            Assert.True(run.Grounded);
            Assert.True(D(run.X) < 6.0, "a player who runs inward is not held by the slope: " + D(run.X));
            Assert.True(PlatformKinds.IsDemanding(PlatformKind.PagodaRidge) && !PlatformKinds.IsHazardType(PlatformKind.PagodaRidge));
        }

        // ------------------------------------------------------------------ 5.6 Mallet Smash

        [Fact]
        public void MalletSmash_SmashesTicks60To69OfA150TickCycleOnA1Point6UnitZoneAndHopsAStandingPlayerInward()
        {
            SimConfig cfg = new SimConfig();
            // mallet at the right end (Dir +1): the zone is x = 6.45 +- 0.8 on a 4.5 u platform centred on 5 (the left 2.9 u are always safe)
            Sim sim = World(Start(), P(1, 1, PlatformKind.MalletSmash, 500, 300, 450, dir: 1, phase: 0));
            Assert.False(sim.MalletSmashing(1, 59));
            Assert.True(sim.MalletSmashing(1, 60));
            Assert.True(sim.MalletSmashing(1, 69));
            Assert.False(sim.MalletSmashing(1, 70));
            Assert.True(sim.MalletSmashing(1, 150 + 65));      // global cycle
            Assert.True(sim.MalletDown(1, 100) && !sim.MalletDown(1, 20) && !sim.MalletDown(1, 120));
            Assert.True(sim.InMalletZone(sim.Platforms[1], F(640)));
            Assert.False(sim.InMalletZone(sim.Platforms[1], F(560)));
            Assert.False(sim.InMalletZone(sim.Platforms[1], F(500)));
            // standing in the zone at the smash: hopped inward 3 u and 0.7 u up, invulnerable, never a landing
            SimState s = Stand(sim, 1, 640, tick: 59);
            sim.Step(ref s, 0);
            Assert.False(s.Grounded);
            Assert.Equal(1, s.Bumps);
            Assert.Equal(-cfg.MalletKnockVx.Raw, s.Vx.Raw);
            Assert.True(s.Invuln > 55);
            double peak = D(s.Y);
            double x0 = D(s.X);
            for (int t = 0; t < 60 && !s.Grounded; t++) { sim.Step(ref s, 0); peak = Math.Max(peak, D(s.Y)); }
            Assert.True(s.Grounded && s.GroundId == 1);
            Assert.InRange(peak - 3.0, 0.65, 0.75);
            Assert.InRange(x0 - D(s.X), 2.7, 3.3);
            Assert.False(s.Dead);
            Assert.Equal(1, s.Bumps);   // the 60 ticks of invulnerability keep it from chaining
            // outside the zone, or outside the smash window, nothing happens
            SimState safe = Stand(sim, 1, 500, tick: 59);
            Steps(sim, ref safe, 20);
            Assert.True(safe.Grounded && safe.Bumps == 0);
            SimState early = Stand(sim, 1, 640, tick: 20);
            Steps(sim, ref early, 38);
            Assert.True(early.Grounded && early.Bumps == 0, "the mallet is still rising: it is not a hit");
            SimState late = Stand(sim, 1, 640, tick: 70);
            Steps(sim, ref late, 60);
            Assert.True(late.Grounded && late.Bumps == 0);
            // a landing in the zone at the smash is no landing; a landing outside it is
            SimState drop = Drop(sim, 1, 640, 40, tick: 58);
            drop.Vy = -F(5, 100);
            for (int t = 0; t < 30 && drop.Bumps == 0 && !drop.Grounded; t++) sim.Step(ref drop, 0);
            Assert.Equal(1, drop.Bumps);
            Assert.Equal(0, drop.Row);   // it did not count as a landing on row 1
            // the mirrored mallet (Dir -1) sits at the left end and hops to the right
            Sim mirror = World(Start(), P(1, 1, PlatformKind.MalletSmash, 500, 300, 450, dir: -1, phase: 0));
            SimState m = Stand(mirror, 1, 360, tick: 59);
            mirror.Step(ref m, 0);
            Assert.Equal(cfg.MalletKnockVx.Raw, m.Vx.Raw);
            Assert.True(PlatformKinds.IsHazardType(PlatformKind.MalletSmash));
            Assert.True(SpecialPlatforms.ConstrainWidth(cfg, PlatformKind.MalletSmash, Fx.FromInt(1)) - cfg.MalletZoneHalf * 2 >= Fx.FromRatio(29, 10));
        }

        // ------------------------------------------------------------------ 5.7 Rolling Log

        [Fact]
        public void RollingLog_RollsAlongTheTrackAt2Point4UnitsPerSecondAndBumpsOnTouchButNotWhenJumpedOver()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = World(Start(), P(1, 1, PlatformKind.RollingLog, 500, 300, 500, phase: 0, period: 190));
            // triangle wave over the 3.8 u the log travels (its 1.2 u width stays on the 5 u track), 95 ticks one way
            Assert.InRange(D(sim.LogX(1, 0)), 3.09, 3.11);
            Assert.InRange(D(sim.LogX(1, 95)), 6.89, 6.91);
            Assert.InRange(D(sim.LogX(1, 190)), 3.09, 3.11);
            Assert.InRange(D(sim.LogX(1, 10)) - D(sim.LogX(1, 9)), 0.039, 0.041);   // 0.04 u per tick = 2.4 u/s
            // the log runs into a standing player: up 1.0 u, 2 u toward the middle of the platform, invulnerable
            SimState s = Stand(sim, 1, 400, tick: 0);   // the log (3.1 + 0.04 t) reaches x = 4.0 - 0.7 at t = 7.5
            int hit = -1;
            for (int t = 0; t < 40 && hit < 0; t++) { sim.Step(ref s, 0); if (s.Bumps > 0) hit = t; }
            Assert.InRange(hit, 3, 14);
            Assert.Equal(1, s.Bumps);
            Assert.Equal(cfg.LogBumpVx.Raw, s.Vx.Raw);   // the player is left of the middle: pushed right (toward it)
            double peak = D(s.Y), x0 = D(s.X);
            for (int t = 0; t < 60 && !s.Grounded; t++) { sim.Step(ref s, 0); peak = Math.Max(peak, D(s.Y)); }
            Assert.InRange(peak - 3.0, 0.95, 1.05);
            Assert.InRange(D(s.X) - x0, 1.8, 2.2);
            Assert.False(s.Dead);
            // jumping over it is safe: jump while the log is still 1.2 u away
            SimState j = Stand(sim, 1, 400, tick: 0);
            sim.Step(ref j, SimInput.Jump);   // log at 3.14 + 0.04, 0.8 u from the player's x... still outside 0.7
            Steps(sim, ref j, 40);
            Assert.Equal(0, j.Bumps);
            // landing on top of the log bumps as well
            SimState d = Drop(sim, 1, (int)Math.Round(D(sim.LogX(1, 30)) * 100), 100, tick: 20);
            for (int t = 0; t < 60 && d.Bumps == 0 && !d.Grounded; t++) sim.Step(ref d, 0);
            Assert.Equal(1, d.Bumps);
            // a revive (invulnerable 2 s) never starts with a bump, and the log never ends the run
            Assert.True(PlatformKinds.IsHazardType(PlatformKind.RollingLog) && PlatformKinds.IsTimeDependent(PlatformKind.RollingLog));
            Assert.Equal(cfg.LogPlatformWidth.Raw, SpecialPlatforms.ConstrainWidth(cfg, PlatformKind.RollingLog, Fx.FromInt(2)).Raw);
        }

        [Fact]
        public void MalletAndLog_ManyRandomStandingAndLandingStatesNeverEndTheRunOrCountAsDamage()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.MalletSmash, 300, 300, 450, dir: -1, phase: 7), P(2, 2, PlatformKind.RollingLog, 500, 600, 500, phase: 31, period: 190));
            Rng rng = new Rng(77);
            for (int n = 0; n < 200; n++)
            {
                SimState s = Stand(sim, 1 + rng.Next(2), 100 + rng.Next(700), rng.Next(500));
                for (int t = 0; t < 400; t++)
                {
                    int input = rng.Next(3) == 0 ? SimInput.Left : (rng.Next(2) == 0 ? SimInput.Right : 0);
                    if (rng.Next(30) == 0) input |= SimInput.Jump;
                    sim.Step(ref s, input);
                    if (s.Dead) break;
                }
                Assert.Equal(0, s.Hits);
            }
        }

        // ------------------------------------------------------------------ determinism and the whole kit on a generated level

        private static List<LoadedLevel> Region5()
        {
            string root = ContentLoader.FindRoot();
            return root == null ? new List<LoadedLevel>() : ContentLoader.LoadRegions(root, 5, 5);
        }

        private static long StateHash(SimState s)
        {
            long h = 17;
            unchecked
            {
                h = h * 31 + s.Tick; h = h * 31 + s.X.Raw; h = h * 31 + s.Y.Raw; h = h * 31 + s.Row; h = h * 31 + s.Score;
                h = h * 31 + s.KoiStill; h = h * 31 + s.KoiAir; h = h * 31 + s.Bumps; h = h * 31 + s.Invuln;
                h = h * 31 + s.GroundId; h = h * 31 + s.LandTick; h = h * 31 + s.Vx.Raw; h = h * 31 + s.Vy.Raw;
                if (s.CrumbleTick != null) foreach (int c in s.CrumbleTick) h = h * 31 + c;
            }
            return h;
        }

        [Fact]
        public void TheKitIsDeterministic_SameLevelSameInputsSameState()
        {
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in Region5().Where(l => l.Spec.LevelIndex >= 3))
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
            // Step must stay allocation free with every region 5 kind in play (CUX-1): measure managed allocations over 20000 steps on a level full of them.
            SimConfig cfg = new SimConfig();
            LoadedLevel ll = Region5().First(l => l.Spec.LevelIndex == 15);
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

        private static bool IsRow5Special(PlatformKind k)
        {
            return PlatformKinds.IsRegion5New(k) || k == PlatformKind.Moving || k == PlatformKind.Vanishing;
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
                kinds[p.Row] = IsRow5Special(p.Kind) ? p.Kind : PlatformKind.Normal;
            }
            return kinds;
        }

        private static Platform FirstOfRow(LevelData d, int row)
        {
            foreach (Platform p in d.Platforms) if (p.Row == row) return p;
            return default(Platform);
        }

        [Fact]
        public void Region5UsesItsSevenKindsWithTheMovingAndVanishingGenericsAndEnumValuesAreStable()
        {
            PlatformKind[] set = RegionPlatformSets.For(SignatureKind.SwingingGongs);
            Assert.Equal(7, set.Length);
            Assert.All(set, k => Assert.True(PlatformKinds.IsRegion5New(k) && PlatformKinds.IsRegionNew(k)));
            Assert.Equal(new[] { PlatformKind.Moving, PlatformKind.Vanishing }, RegionPlatformSets.GenericsFor(SignatureKind.SwingingGongs));
            LevelSpec spec = Region5().First(l => l.Spec.LevelIndex == 5).Spec;
            PlatformKind[] options = SpecialPlatforms.Options(spec, SignatureKinds.PlatformFor(SignatureKind.SwingingGongs));
            Assert.Equal(9, options.Length);   // 7 new + Moving + Vanishing (the gongs are objects, the signature adds no platform option)
            Assert.Equal(1, options.Count(k => k == PlatformKind.Moving));
            Assert.Equal(1, options.Count(k => k == PlatformKind.Vanishing));
            Assert.Equal(2, options.Count(PlatformKinds.IsHazardType));
            Assert.Equal(1, options.Count(PlatformKinds.IsLaunch));
            Assert.Equal(2, options.Count(PlatformKinds.IsDemanding));
            Assert.True(PlatformKinds.IsHazardType(PlatformKind.MalletSmash) && PlatformKinds.IsHazardType(PlatformKind.RollingLog));
            Assert.True(PlatformKinds.IsDemanding(PlatformKind.SwingPlank) && PlatformKinds.IsDemanding(PlatformKind.PagodaRidge));
            Assert.True(PlatformKinds.IsLaunch(PlatformKind.BambooFlick));
            Assert.False(PlatformKinds.IsLaunch(PlatformKind.LanternLift) || PlatformKinds.IsHazardType(PlatformKind.KoiStone) || PlatformKinds.IsDemanding(PlatformKind.KoiStone));
            Assert.Equal(35, (int)PlatformKind.PinwheelGust);
            Assert.Equal(36, (int)PlatformKind.BambooFlick);
            Assert.Equal(37, (int)PlatformKind.LanternLift);
            Assert.Equal(38, (int)PlatformKind.KoiStone);
            Assert.Equal(39, (int)PlatformKind.SwingPlank);
            Assert.Equal(40, (int)PlatformKind.PagodaRidge);
            Assert.Equal(41, (int)PlatformKind.MalletSmash);
            Assert.Equal(42, (int)PlatformKind.RollingLog);
            Assert.False(PlatformKinds.IsRegion4New(PlatformKind.BambooFlick) || PlatformKinds.IsRegion5New(PlatformKind.PinwheelGust));
        }

        [Fact]
        public void OtherRegionsGetNoRegion5PlatformsAndRegion5GetsNoRegion1To4Platforms()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 1, 4))
                foreach (Platform p in LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(1))).Platforms)
                    Assert.False(PlatformKinds.IsRegion5New(p.Kind), ll.Id + " has " + p.Kind);
            foreach (LoadedLevel ll in Region5())
                foreach (Platform p in LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(1))).Platforms)
                    Assert.False(PlatformKinds.IsRegion1New(p.Kind) || PlatformKinds.IsRegion2New(p.Kind) || PlatformKinds.IsRegion3New(p.Kind) || PlatformKinds.IsRegion4New(p.Kind)
                        || p.Kind == PlatformKind.BounceCap || p.Kind == PlatformKind.Bounce || p.Kind == PlatformKind.Sticky || p.Kind == PlatformKind.Slippery, ll.Id + " has " + p.Kind);
        }

        [Fact]
        public void Region5SpecialShareFollowsTheDoc25RampAndLevel1HasNoHazardTypes()
        {
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = Region5();
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
            foreach (LoadedLevel ll in Region5().Where(l => l.Spec.FixedChunks.Length == 0))
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
        public void PlacementRulesHoldOnEveryRandomRegion5Layout()
        {
            SimConfig cfg = new SimConfig();
            List<string> bad = new List<string>();
            Dictionary<PlatformKind, int> seen = new Dictionary<PlatformKind, int>();
            foreach (LoadedLevel ll in Region5())
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
                        if (zone && (PlatformKinds.IsLaunch(k) || PlatformKinds.IsHazardType(k) || PlatformKinds.IsDemanding(k) || k == PlatformKind.Vanishing)) bad.Add(tag + ": launcher, hazard, demanding or vanishing kind in the last 3 rows / around the hidden gem");
                    }
                    foreach (Platform p in d.Platforms)
                    {
                        if (p.Row < 1 || p.Row > d.Rows) continue;
                        Fx reach = p.Width / 2 + (p.Kind == PlatformKind.SwingPlank || p.Kind == PlatformKind.LanternLift ? p.Amplitude : Fx.Zero);
                        if (PlatformKinds.IsRegion5New(p.Kind) && (p.X - reach < Fx.Zero || p.X + reach > cfg.ScreenWidth)) bad.Add(ll.Id + " seed " + i + ": " + p.Kind + " leaves the walls at its widest swing");
                        if (p.Kind == PlatformKind.BambooFlick)
                        {
                            if (p.Width != cfg.FlickWidth) bad.Add(ll.Id + ": flick width " + D(p.Width));
                            if (p.X < cfg.ScreenWidth / 2 ? p.Dir != 1 : p.Dir != -1) bad.Add(ll.Id + ": flick does not fling to the middle");
                            Platform next = FirstOfRow(d, p.Row + 1);
                            if (next.Row == p.Row + 1)
                            {
                                Fx gap = next.Y - p.Y;
                                if (gap < cfg.FlickGapMin - F(1) || gap > cfg.FlickGapMax + F(1)) bad.Add(ll.Id + " seed " + i + " row " + p.Row + ": gap above a flick " + D(gap));
                                if ((next.X - p.X) * p.Dir <= Fx.Zero) bad.Add(ll.Id + " seed " + i + " row " + p.Row + ": the platform after a flick is not in its direction");
                                Fx dx = Fx.Abs(next.X - p.X);
                                if (dx > cfg.FlickNextDxMax + F(1)) bad.Add(ll.Id + " seed " + i + " row " + p.Row + ": platform after a flick too far (" + D(dx) + ")");
                            }
                        }
                        if (p.Kind == PlatformKind.LanternLift && (p.Width < Fx.FromInt(3) || p.Width > Fx.FromInt(5))) bad.Add(ll.Id + ": lantern width " + D(p.Width));
                        if (p.Kind == PlatformKind.LanternLift && p.Amplitude > cfg.LanternSwayAmplitude) bad.Add(ll.Id + ": lantern sway too big");
                        if (p.Kind == PlatformKind.KoiStone && p.Width < Fx.FromInt(5)) bad.Add(ll.Id + ": koi stone narrower than 5 (" + D(p.Width) + ")");
                        if (p.Kind == PlatformKind.SwingPlank)
                        {
                            if (p.Period != cfg.PlankPeriodTicks) bad.Add(ll.Id + ": plank period " + p.Period);
                            if (p.Amplitude > cfg.PlankAmplitude) bad.Add(ll.Id + ": plank swing too long");
                            if (p.Width < Fx.FromInt(3) || p.Width > Fx.FromInt(6)) bad.Add(ll.Id + ": plank width " + D(p.Width));
                        }
                        if (p.Kind == PlatformKind.PagodaRidge && (p.Width < F(250) || p.Width > F(450))) bad.Add(ll.Id + ": ridge width " + D(p.Width));
                        if (p.Kind == PlatformKind.MalletSmash)
                        {
                            if (p.Width < F(450)) bad.Add(ll.Id + ": mallet platform narrower than 4.5 (" + D(p.Width) + ")");
                            if (p.X < cfg.ScreenWidth / 2 ? p.Dir != -1 : p.Dir != 1) bad.Add(ll.Id + ": mallet not at the wall end");
                        }
                        if (p.Kind == PlatformKind.RollingLog && (p.Width != cfg.LogPlatformWidth || p.Period != cfg.LogPeriodTicks)) bad.Add(ll.Id + ": log track " + D(p.Width) + " period " + p.Period);
                        if (PlatformKinds.IsRegion5New(p.Kind) && spec.Goal.Type == GoalType.NoWallBounce && p.Kind == PlatformKind.BambooFlick && (p.X < F(150) || p.X > F(850))) { /* fine: the flick goes toward the middle */ }
                    }
                    // gaps next to the rising kinds (a Flick below a lantern keeps its own 2.0 to 2.2 u gap: the flick flight peaks at 2.76 u, and nothing sits above the lantern before it rises)
                    for (int r = 1; r < d.Rows; r++)
                    {
                        Platform a = FirstOfRow(d, r), b = FirstOfRow(d, r + 1);
                        if (a.Row != r || b.Row != r + 1) continue;
                        Fx gap = b.Y - a.Y;
                        if ((a.Kind == PlatformKind.LanternLift || b.Kind == PlatformKind.LanternLift) && a.Kind != PlatformKind.BambooFlick && gap < cfg.LanternGapMin - F(1)) bad.Add(ll.Id + " seed " + i + " row " + r + ": gap next to a lantern " + D(gap));
                        if ((a.Kind == PlatformKind.SwingPlank || b.Kind == PlatformKind.SwingPlank) && gap < cfg.PlankGapMin - F(1)) bad.Add(ll.Id + " seed " + i + " row " + r + ": gap next to a plank " + D(gap));
                    }
                }
            }
            foreach (string s in bad.Take(30)) output.WriteLine(s);
            foreach (KeyValuePair<PlatformKind, int> kv in seen.OrderBy(k => (int)k.Key)) output.WriteLine(kv.Key + ": " + kv.Value);
            Assert.True(bad.Count == 0, bad.Count + " rule violations, first: " + (bad.Count > 0 ? bad[0] : ""));
            foreach (PlatformKind k in RegionPlatformSets.For(SignatureKind.SwingingGongs)) Assert.True(seen.ContainsKey(k), k + " never placed");
            Assert.True(seen.ContainsKey(PlatformKind.Moving) && seen.ContainsKey(PlatformKind.Vanishing), "the two generics are options");
        }

        [Fact]
        public void KindMixIsEvenAndHazardTypesStayAboutAFifthOfTheSpecialRows()
        {
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in Region5().Where(l => l.Spec.LevelIndex >= 3 && l.Spec.FixedChunks.Length == 0))
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
        public void BotSolvesEveryRandomRegion5LayoutOfLevels1To15With24Seeds()
        {
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = Region5().ToList();
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
            output.WriteLine("region 5 levels 1..15 x 24 seeds, unsolved: " + all.Count);
            Assert.True(all.Count == 0, all.Count + " unsolved layouts, first: " + (all.Count > 0 ? all[0] : ""));
        }

        [Fact]
        public void BotSolvesTheBaseLayoutOfEveryRegion5Level()
        {
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = Region5().ToList();
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

        private static LevelSpec Endless(int seed) { return EndlessTower.MakeSpec(seed, Fx.FromRatio(345, 10000), SignatureKind.SwingingGongs, 400); }

        [Fact]
        public void Endless_Region5UsesTheNewPlatformsWithAFloorBasedRampAndTheSameRules()
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
                    if (IsRow5Special(p.Kind)) { kinds[p.Row] = p.Kind; seen.Add(p.Kind); }
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
                // the platform after a flick sits in its direction in endless too
                foreach (Platform p in d.Platforms)
                {
                    if (p.Kind != PlatformKind.BambooFlick || p.Row >= 319) continue;
                    Platform next = FirstOfRow(d, p.Row + 1);
                    Assert.True((next.X - p.X) * p.Dir > Fx.Zero, "endless flick row " + p.Row + ": the next platform is not in the flick direction");
                    Assert.InRange(D(next.Y - p.Y), 1.99, 2.21);
                }
            }
            double ep = early * 100 / e, lp = late * 100 / l;
            output.WriteLine("endless special share: floors 5-40 " + ep.ToString("F1") + " percent, floors 200-320 " + lp.ToString("F1") + " percent");
            Assert.InRange(ep, 12, 32);
            Assert.InRange(lp, 48, 62);
            Assert.Equal(9, seen.Count);   // seven new platforms and the two generics appear
        }

        [Fact]
        public void Endless_Region5GrowthIsPrefixStable()
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
        public void Endless_TheBotClimbs60FloorsOfRegion5WithAllNewPlatforms()
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
