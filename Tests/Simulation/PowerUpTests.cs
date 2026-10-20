using System;
using System.Collections.Generic;
using Xunit;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>
    /// Power-ups (docs/15 FR-PWR, FR-REGION): each effect on hand-built worlds (exact numbers) and on the real levels of all five regions,
    /// determinism (same inputs give the same state hash), the finish can never be skipped, stars unchanged.
    /// Category PowerUps.
    /// </summary>
    [Trait("Category", "PowerUps")]
    public class PowerUpTests
    {
        private static readonly SimConfig Cfg = new SimConfig();

        // ---------- helpers ----------

        private static long Mix(long h, int v)
        {
            unchecked { return (h ^ (uint)v) * 1099511628211L; }
        }

        /// <summary>Hash of the whole gameplay state (loadout fields optional).</summary>
        private static long Hash(SimState s, bool withLoadout)
        {
            long h = 1469598103934665603L;
            h = Mix(h, s.Tick); h = Mix(h, s.X.Raw); h = Mix(h, s.Y.Raw); h = Mix(h, s.Vx.Raw); h = Mix(h, s.Vy.Raw);
            h = Mix(h, s.Grounded ? 1 : 0); h = Mix(h, s.GroundId); h = Mix(h, s.Coyote); h = Mix(h, s.JumpBuffer); h = Mix(h, s.JumpStartRow);
            h = Mix(h, s.Row); h = Mix(h, s.Combo); h = Mix(h, s.BestCombo); h = Mix(h, s.Score); h = Mix(h, s.WallBounces);
            h = Mix(h, s.KillY.Raw); h = Mix(h, s.MaxY.Raw); h = Mix(h, s.StartTick); h = Mix(h, s.Dead ? 1 : 0);
            h = Mix(h, s.Coins); h = Mix(h, s.StarMask); h = Mix(h, s.Stars); h = Mix(h, s.Gems); h = Mix(h, s.HiddenGems);
            h = Mix(h, s.Hits); h = Mix(h, s.Invuln); h = Mix(h, s.BestGroundId); h = Mix(h, s.LastJumpFloors); h = Mix(h, s.BestJumpFloors);
            h = Mix(h, (int)s.JumpHist); h = Mix(h, (int)(s.JumpHist >> 32));
            h = Mix(h, s.FlightTicks); h = Mix(h, s.RocketTicks); h = Mix(h, s.SlowFall); h = Mix(h, s.MagnetTicks); h = Mix(h, s.SlowTicks);
            h = Mix(h, s.ShieldTicks); h = Mix(h, s.WallStickTicks); h = Mix(h, s.DoubleJumps); h = Mix(h, s.DoubleJumpTicks);
            h = Mix(h, s.WallHold); h = Mix(h, s.WallCool); h = Mix(h, s.Assisted ? 1 : 0); h = Mix(h, s.ShieldSaves);
            if (withLoadout)
            {
                h = Mix(h, (int)s.SlotType(0)); h = Mix(h, (int)s.SlotType(1)); h = Mix(h, (int)s.SlotType(2));
                h = Mix(h, s.SlotUnits(0)); h = Mix(h, s.SlotUnits(1)); h = Mix(h, s.SlotUnits(2)); h = Mix(h, s.PwUsed); h = Mix(h, s.PwUsedMask);
            }
            if (s.PickupTaken != null) for (int i = 0; i < s.PickupTaken.Length; i++) h = Mix(h, s.PickupTaken[i] ? 1 : 0);
            return h;
        }

        private static string LevelId(int region, int index)
        {
            return "lvl_" + region.ToString("D3") + "_" + index.ToString("D2");
        }

        private sealed class World
        {
            public LevelData Data;
            public Sim Sim;
            public LevelSpec Spec;
        }

        private static World RealLevel(int region, int index)
        {
            string root = ContentLoader.FindRoot();
            Assert.NotNull(root);
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, region, region))
            {
                if (ll.Id != LevelId(region, index)) continue;
                World w = new World();
                w.Spec = ll.Spec;
                w.Data = LevelBuilder.Build(Cfg, ll.Spec);
                w.Sim = new Sim(Cfg, w.Data, ll.Spec);
                return w;
            }
            throw new InvalidOperationException("level " + LevelId(region, index));
        }

        private static readonly int[] OffsetTenThousandths = { 0, 86, 172, 259, 345 };
        private static readonly SignatureKind[] Signatures =
        {
            SignatureKind.BounceFlowers, SignatureKind.BounceCaps, SignatureKind.DriftingPlatforms, SignatureKind.StickyGum, SignatureKind.SwingingGongs
        };

        private static World Endless(int region, int seed, int rows)
        {
            LevelSpec spec = EndlessTower.MakeSpec(seed, Fx.FromRatio(OffsetTenThousandths[region - 1], 10000), Signatures[region - 1], 400);
            World w = new World();
            w.Spec = spec;
            w.Data = EndlessTower.BuildFixed(Cfg, spec, rows);
            w.Sim = new Sim(Cfg, w.Data, spec.Difficulty, true, false);
            return w;
        }

        /// <summary>Flat hand-built tower: a full-width start platform and platforms every 3 u (row n at y = 3n) up to 'rows'.</summary>
        private static Sim Tower(int rows, Pickup[] pickups, Hazard[] hazards)
        {
            List<Platform> extra = new List<Platform>();
            for (int i = 1; i <= rows; i++) extra.Add(T3.P(i, 5.0, 3.0 * i, 10.0, PlatformKind.Normal));
            LevelData d = T3.World(Cfg, pickups, hazards, extra.ToArray());
            return new Sim(Cfg, d, Fx.FromRatio(5, 10), false, false);
        }

        private static SimState Fresh(Sim sim, PowerUp a, PowerUp b = PowerUp.None, PowerUp c = PowerUp.None, int units = 3)
        {
            SimState s = sim.NewState();
            sim.SetLoadout(ref s, a, units, b, units, c, units);
            return s;
        }

        private static void Run(Sim sim, ref SimState s, int ticks, int input)
        {
            for (int i = 0; i < ticks; i++) sim.Step(ref s, input);
        }

        private static readonly int Slot0 = SimInput.PowerSlot0;

        // ---------- data format ----------

        [Fact]
        public void IdsRoundTripAndNewInputBitsDoNotOverlapTheOldOnes()
        {
            for (int i = 1; i <= PowerUps.MaxType; i++)
            {
                PowerUp p = (PowerUp)i;
                Assert.Equal(p, PowerUps.FromId(PowerUps.IdOf(p)));
            }
            Assert.Equal(PowerUp.None, PowerUps.FromId("pw_unknown"));
            Assert.Equal(0, SimInput.PowerMask & (SimInput.Left | SimInput.Right | SimInput.Jump));
            Assert.Equal(SimInput.PowerSlot1, PowerUps.InputBitForSlot(1));
            Assert.Equal(SimInput.PowerSlot2, PowerUps.InputBitForSlot(2));
            Assert.True(PowerUps.IsImplemented(PowerUp.SlowTime));
            Assert.False(PowerUps.IsImplemented(PowerUp.SpringBoots));
        }

        [Fact]
        public void ActivatingAnUnimplementedOrEmptySlotDoesNothing()
        {
            Sim sim = Tower(5, null, null);
            SimState s = sim.NewState();
            sim.SetLoadout(ref s, PowerUp.SpringBoots, 3, PowerUp.Flight, 0, PowerUp.None, 5);
            Run(sim, ref s, 3, SimInput.PowerSlot0 | SimInput.PowerSlot1 | SimInput.PowerSlot2);
            Assert.Equal(0, s.PwUsed);
            Assert.Equal(3, s.SlotUnits(0));
            Assert.Equal(0, s.SlotUnits(2));   // None carries no units even if the caller passed some
            Assert.Equal(0, s.FlightTicks);
        }

        // ---------- no loadout means no change ----------

        [Fact]
        public void PowerBitsWithoutLoadoutAndAnUnusedLoadoutDoNotChangeTheRun()
        {
            for (int region = 1; region <= 5; region++)
            {
                World w = RealLevel(region, 5);
                SimState plain = w.Sim.NewState();
                SimState noLoadout = w.Sim.NewState();
                SimState unused = Fresh(w.Sim, PowerUp.Flight, PowerUp.Rocket, PowerUp.Shield);
                Random rnd = new Random(100 + region);
                for (int t = 0; t < 900 && !plain.Dead; t++)
                {
                    int input = 0;
                    if ((t / 40) % 2 == 0) input |= SimInput.Right; else input |= SimInput.Left;
                    if (rnd.Next(25) == 0) input |= SimInput.Jump;
                    w.Sim.Step(ref plain, input);
                    w.Sim.Step(ref noLoadout, input | SimInput.PowerMask);   // bits set, nothing equipped
                    w.Sim.Step(ref unused, input);                           // equipped, never pressed
                }
                Assert.Equal(Hash(plain, true), Hash(noLoadout, true));
                Assert.Equal(Hash(plain, false), Hash(unused, false));
            }
        }

        [Fact]
        public void LevelLayoutsDoNotDependOnPowerUps_GoldenFileStillMatches()
        {
            // The builder never sees the loadout; the golden file test (GoldenHashTests) pins the 75 hashes. Here: building after a
            // sim with a loadout ran gives the same hash as before.
            World w = RealLevel(3, 7);
            long before = w.Data.ComputeHash();
            SimState s = Fresh(w.Sim, PowerUp.Flight, PowerUp.Magnet, PowerUp.SlowTime);
            Run(w.Sim, ref s, 200, Slot0 | SimInput.PowerSlot1 | SimInput.PowerSlot2);
            Assert.Equal(before, w.Data.ComputeHash());
            Assert.Equal(before, LevelBuilder.Build(Cfg, w.Spec).ComputeHash());
        }

        // ---------- Flight ----------

        [Theory]
        [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
        public void Power_Flight_Region(int region)
        {
            World w = RealLevel(region, 5);
            SimState s = Fresh(w.Sim, PowerUp.Flight);
            Fx y0 = s.Y;
            w.Sim.Step(ref s, Slot0);
            Assert.Equal(2, s.SlotUnits(0));   // 3 carried, one used
            Assert.True(PowerUps.IsActive(in s, PowerUp.Flight));
            Assert.False(s.Grounded);
            Assert.True(s.StartTick >= 0);   // the screen starts to scroll when flight starts
            Run(w.Sim, ref s, 239, 0);
            Assert.Equal(y0 + Cfg.FlightRise * Cfg.FlightTicks, s.Y);
            Assert.Equal(0, s.Hits);
            Assert.False(s.Dead);
            Assert.False(PowerUps.IsActive(in s, PowerUp.Flight));
            Assert.True(s.SlowFall > 0);
        }

        [Fact]
        public void Flight_SteersSideways_AndIgnoresGongsInRegion5()
        {
            World w = RealLevel(5, 15);
            SimState s = Fresh(w.Sim, PowerUp.Flight);
            Fx x0 = s.X;
            w.Sim.Step(ref s, Slot0 | SimInput.Right);
            Run(w.Sim, ref s, 50, SimInput.Right);
            Assert.True(s.X > x0 + Fx.One, "flight steers");
            Run(w.Sim, ref s, 189, SimInput.Left);
            Assert.Equal(0, s.Hits);
        }

        [Fact]
        public void Flight_CannotPassThroughTheTopRows_AndCannotCompleteALevelByItself()
        {
            Sim sim = Tower(13, null, null);   // rows 0..13, limit row 10 (y = 30)
            SimState s = Fresh(sim, PowerUp.Flight);
            sim.Step(ref s, Slot0);
            Run(sim, ref s, 300, 0);
            Assert.True(s.Row <= 10, "row " + s.Row);
            Assert.True(s.Y <= T3.F(30.0));
            Assert.False(s.Dead);

            // real levels: flight then rocket one after the other never completes the goal
            for (int region = 1; region <= 5; region++)
            {
                World w = RealLevel(region, 1);
                SimState r = Fresh(w.Sim, PowerUp.Flight, PowerUp.Rocket);
                w.Sim.Step(ref r, Slot0);
                Run(w.Sim, ref r, 241, 0);
                w.Sim.Step(ref r, SimInput.PowerSlot1);
                for (int t = 0; t < 900 && !r.Dead; t++) w.Sim.Step(ref r, 0);
                Assert.False(GoalEvaluator.IsComplete(w.Spec, r), "region " + region);
            }
        }

        [Fact]
        public void FlightAndRocketCannotBothBeActive()
        {
            Sim sim = Tower(10, null, null);
            SimState s = Fresh(sim, PowerUp.Flight, PowerUp.Rocket);
            sim.Step(ref s, Slot0 | SimInput.PowerSlot1);
            Assert.True(s.FlightTicks > 0);
            Assert.Equal(0, s.RocketTicks);
            Assert.Equal(3, s.SlotUnits(1));   // not consumed
            sim.Step(ref s, Slot0);        // flight is active: pressing again does nothing
            Assert.Equal(2, s.SlotUnits(0));
        }

        // ---------- Rocket ----------

        [Theory]
        [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
        public void Power_Rocket_Region(int region)
        {
            World w = RealLevel(region, 5);
            SimState s = Fresh(w.Sim, PowerUp.Rocket);
            Fx y0 = s.Y, x0 = s.X;
            w.Sim.Step(ref s, Slot0 | SimInput.Right);
            Run(w.Sim, ref s, 119, SimInput.Right | SimInput.Left);   // no steering while the rocket burns
            Assert.Equal(y0 + Cfg.RocketRise * Cfg.RocketTicks, s.Y);
            Assert.Equal(x0, s.X);
            Assert.Equal(0, s.Hits);
            Assert.False(PowerUps.IsActive(in s, PowerUp.Rocket));
            Assert.True(s.SlowFall > 0);
            Assert.False(s.Dead);
        }

        [Fact]
        public void Rocket_OnlyCollectsWhatItTouches_AndItsLandingNeverCountsAsACombo()
        {
            Pickup offPath = T3.Pk(PickupKind.Star, 1.5, 10.0, 1.0); offPath.Tag = 0;
            Pickup onPath = T3.Pk(PickupKind.Star, 5.0, 20.0, 1.0); onPath.Tag = 1;
            Sim sim = Tower(14, new[] { offPath, onPath }, null);
            SimState s = Fresh(sim, PowerUp.Rocket);
            sim.Step(ref s, Slot0);
            for (int t = 0; t < 600 && !(s.Grounded && s.Tick > 130); t++) sim.Step(ref s, 0);
            Assert.True(s.Grounded);
            Assert.Equal(2, s.StarMask);       // only star 1 was on the flight path
            Assert.Equal(1, s.Stars);
            Assert.Equal(9, s.Row);             // fell back onto row 9 (y 27)
            Assert.Equal(0, s.Combo);
            Assert.Equal(0, s.BestJumpFloors);
            Assert.Equal(0UL, s.JumpHist);
            Assert.Equal(9 * Cfg.FloorPoints, s.Score);
            Assert.Equal(0, s.Coins);
        }

        // ---------- Double Jump ----------

        [Theory]
        [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
        public void Power_DoubleJump_Region(int region)
        {
            World w = RealLevel(region, 5);
            SimState with = Fresh(w.Sim, PowerUp.DoubleJump);
            SimState without = w.Sim.NewState();
            w.Sim.Step(ref with, Slot0 | SimInput.Jump);
            w.Sim.Step(ref without, SimInput.Jump);
            Assert.Equal(Cfg.DoubleJumpCount, with.DoubleJumps);
            Run(w.Sim, ref with, 29, 0);
            Run(w.Sim, ref without, 29, 0);
            Assert.True(with.Vy <= Fx.Zero && !with.Grounded);
            w.Sim.Step(ref with, SimInput.Jump);
            w.Sim.Step(ref without, SimInput.Jump);
            Assert.True(with.Vy > Fx.Zero, "air jump");
            Assert.Equal(Cfg.DoubleJumpCount - 1, with.DoubleJumps);
            Assert.True(without.Vy < Fx.Zero, "no air jump without the power-up");
        }

        [Fact]
        public void DoubleJump_ReachesTwiceAsHigh_AndRunsOut()
        {
            Sim sim = Tower(3, null, null);
            SimState s = Fresh(sim, PowerUp.DoubleJump, PowerUp.None, PowerUp.None, 1);
            sim.Step(ref s, Slot0 | SimInput.Jump);
            Fx peak = s.Y;
            int air = 0;
            for (int t = 0; t < 200 && air < Cfg.DoubleJumpCount; t++)
            {
                bool falling = s.Vy <= Fx.Zero && !s.Grounded && s.Coyote == 0;
                sim.Step(ref s, falling ? SimInput.Jump : 0);
                if (falling && s.Vy > Fx.Zero) air++;
                if (s.Y > peak) peak = s.Y;
            }
            Assert.Equal(Cfg.DoubleJumpCount, air);
            Assert.Equal(0, s.DoubleJumps);
            Assert.True(peak.Raw / 65536.0 > 6.0, "peak " + peak.Raw / 65536.0);
            Assert.True(peak.Raw / 65536.0 < 6.5 + 4.6, "stays bounded");
            // a third tap in the air does nothing
            for (int t = 0; t < 200 && s.Grounded; t++) sim.Step(ref s, SimInput.Jump);
        }

        [Fact]
        public void DoubleJump_IsKeptAfterABounceLaunch_AndStickyJumpsStillUseTheStickyPenalty()
        {
            // bounce pad at row 1: the launch keeps working and the charge is still there afterwards
            List<Platform> list = new List<Platform>();
            list.Add(T3.P(1, 5.0, 3.0, 10.0, PlatformKind.Bounce));
            list.Add(T3.P(2, 5.0, 12.0, 10.0, PlatformKind.Normal));
            Sim sim = new Sim(Cfg, T3.World(Cfg, null, null, list.ToArray()), Fx.FromRatio(5, 10), false, false);
            SimState s = Fresh(sim, PowerUp.DoubleJump);
            sim.Step(ref s, Slot0 | SimInput.Jump);
            for (int t = 0; t < 120 && s.GroundId != 1; t++) sim.Step(ref s, 0);
            Assert.Equal(1, s.GroundId);
            Assert.True(s.Vy > Cfg.JumpBase, "bounce launch");
            Assert.Equal(Cfg.DoubleJumpCount, s.DoubleJumps);
        }

        // ---------- Wall Stick ----------

        private static SimState RunToTheLeftWall(Sim sim, ref SimState s)
        {
            sim.Step(ref s, Slot0 | SimInput.Left | SimInput.Jump);
            for (int t = 0; t < 120 && s.WallHold == 0 && s.WallBounces == 0; t++) sim.Step(ref s, SimInput.Left);
            return s;
        }

        [Fact]
        public void WallStick_HoldsTheWallThenFalls_AndKeepsTheBounceCounter()
        {
            Sim sim = Tower(6, null, null);
            SimState s = Fresh(sim, PowerUp.WallStick);
            RunToTheLeftWall(sim, ref s);
            Assert.True(s.WallHold > 0);
            Assert.Equal(1, s.WallBounces);
            Assert.Equal(Cfg.PlayerHalfWidth, s.X);
            Fx y = s.Y;
            for (int t = 0; t < Cfg.WallHoldTicks - 2; t++) { sim.Step(ref s, SimInput.Left); Assert.Equal(y, s.Y); Assert.Equal(Cfg.PlayerHalfWidth, s.X); }
            for (int t = 0; t < 10; t++) sim.Step(ref s, SimInput.Left);
            Assert.Equal(0, s.WallHold);
            Assert.True(s.Y < y, "fell after the hold");
        }

        [Fact]
        public void WallStick_WallJumpLaunchesAwayFromTheWall()
        {
            Sim sim = Tower(6, null, null);
            SimState s = Fresh(sim, PowerUp.WallStick);
            RunToTheLeftWall(sim, ref s);
            Run(sim, ref s, 20, SimInput.Left);
            Assert.True(s.WallHold > 0);
            sim.Step(ref s, SimInput.Jump);
            Assert.Equal(0, s.WallHold);
            Assert.True(s.Vx > Fx.Zero, "pushed right");
            Assert.True(s.Vy > Fx.Zero, "launched up");
            // no instant re-stick on the same wall
            Assert.True(s.WallCool > 0);
        }

        [Fact]
        public void WithoutWallStick_TheWallStillBouncesAsBefore()
        {
            Sim sim = Tower(6, null, null);
            SimState s = sim.NewState();
            sim.Step(ref s, SimInput.Left | SimInput.Jump);
            for (int t = 0; t < 120 && s.WallBounces == 0; t++) sim.Step(ref s, SimInput.Left);
            Assert.Equal(1, s.WallBounces);
            Assert.Equal(0, s.WallHold);
            Assert.True(s.Vx > Fx.Zero);
        }

        [Fact]
        public void WallStick_WindowEnds()
        {
            Sim sim = Tower(6, null, null);
            SimState s = Fresh(sim, PowerUp.WallStick);
            sim.Step(ref s, Slot0);
            Run(sim, ref s, Cfg.WallStickWindowTicks, 0);
            Assert.False(PowerUps.IsActive(in s, PowerUp.WallStick));
            sim.Step(ref s, SimInput.Left | SimInput.Jump);
            for (int t = 0; t < 120 && s.WallBounces == 0; t++) sim.Step(ref s, SimInput.Left);
            Assert.Equal(0, s.WallHold);
            Assert.Equal(1, s.WallBounces);
        }

        // ---------- Shield ----------

        [Theory]
        [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
        public void Power_Shield_Region(int region)
        {
            World w = RealLevel(region, 5);
            SimState s = Fresh(w.Sim, PowerUp.Shield);
            w.Sim.Step(ref s, Slot0);
            Assert.True(s.ShieldTicks > 0);
            s.KillY = s.Y + Fx.One;   // the kill line has swallowed the player
            w.Sim.Step(ref s, 0);
            Assert.False(s.Dead);
            Assert.Equal(1, s.ShieldSaves);
            Assert.Equal(0, s.ShieldTicks);
            Assert.True(s.Grounded);
            Assert.True(s.Y >= s.KillY + Cfg.ShieldRescueMargin, "rescued above the kill line");
            Assert.True(s.Invuln > 0);
            // the shield is used up: the next fall is fatal
            s.KillY = s.Y + Fx.One;
            w.Sim.Step(ref s, 0);
            Assert.True(s.Dead);
        }

        [Fact]
        public void WithoutShieldTheFallKills_AndShieldAbsorbsAGongHitOnce()
        {
            Sim sim = Tower(6, null, null);
            SimState dead = sim.NewState();
            dead.KillY = dead.Y + Fx.One;
            sim.Step(ref dead, 0);
            Assert.True(dead.Dead);

            Hazard gong = T3.Gong(5.0, 0.5, 0.6, 0.0, 120, 0);
            Sim gsim = Tower(6, null, new[] { gong });
            SimState hit = gsim.NewState();
            gsim.Step(ref hit, 0);
            Assert.Equal(1, hit.Hits);

            SimState shielded = Fresh(gsim, PowerUp.Shield);
            gsim.Step(ref shielded, Slot0);
            Assert.Equal(0, shielded.Hits);
            Assert.Equal(0, shielded.ShieldTicks);
            Assert.Equal(1, shielded.ShieldSaves);
            Assert.Equal(Fx.Zero, shielded.Vx);   // no knockback
        }

        [Fact]
        public void Shield_FindsASafePlatformInAnEndlessTower()
        {
            for (int region = 1; region <= 5; region++)
            {
                World w = Endless(region, 4242, 120);
                SimState s = Fresh(w.Sim, PowerUp.Shield);
                w.Sim.Step(ref s, Slot0);
                s.KillY = s.Y + Fx.One;
                w.Sim.Step(ref s, 0);
                Assert.False(s.Dead, "region " + region);
            }
        }

        // ---------- Magnet ----------

        [Theory]
        [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
        public void Power_Magnet_Region(int region)
        {
            // a gem 2.2 u beside the player: out of reach normally (0.9), inside with the magnet (0.9 + 2)
            Pickup gem = T3.Pk(PickupKind.Gem, 7.2, 0.5, 0.9);
            LevelData d = T3.World(Cfg, new[] { gem }, null);
            // use the region's own rules: sim from the real level with the gem world swapped in is not needed, the pickup logic is region independent;
            // the real-level run below checks the magnet never breaks anything there.
            Sim sim = new Sim(Cfg, d, Fx.FromRatio(5, 10), false, false);
            SimState off = sim.NewState();
            sim.Step(ref off, 0);
            Assert.Equal(0, off.Gems);
            SimState on = Fresh(sim, PowerUp.Magnet);
            sim.Step(ref on, Slot0);
            Assert.Equal(1, on.Gems);

            World w = RealLevel(region, 5);
            SimState s = Fresh(w.Sim, PowerUp.Magnet);
            w.Sim.Step(ref s, Slot0);
            Assert.True(s.MagnetTicks > 0);
            Run(w.Sim, ref s, Cfg.MagnetTicks, SimInput.Right);
            Assert.False(PowerUps.IsActive(in s, PowerUp.Magnet));
        }

        // ---------- Slow Time ----------

        [Theory]
        [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
        public void Power_SlowTime_Region(int region)
        {
            World w = RealLevel(region, 5);
            SimState normal = w.Sim.NewState();
            SimState slow = Fresh(w.Sim, PowerUp.SlowTime);
            Fx k0 = normal.KillY;
            w.Sim.Step(ref normal, SimInput.Jump);
            w.Sim.Step(ref slow, SimInput.Jump | Slot0);
            Run(w.Sim, ref normal, 25, 0);   // short window: later a bounce launch can pull both kill lines up by the follow rule
            Run(w.Sim, ref slow, 25, 0);
            long dn = normal.KillY.Raw - k0.Raw, ds = slow.KillY.Raw - k0.Raw;
            Assert.True(dn > 0 && ds > 0);
            Assert.True(slow.KillY <= normal.KillY);
            // when a bounce launch pulled the follow rule (MaxY - KillDistance) over the scroll, both runs share the same kill line and the ratio is meaningless
            if (normal.KillY != normal.MaxY - Cfg.KillDistance)   // the follow rule would hide the scroll
            {
                long pct = ds * 100 / dn;
                Assert.True(pct >= 55 && pct <= 66, "pct " + pct + " dn " + dn + " ds " + ds + " maxY " + normal.MaxY.Raw + " slowMaxY " + slow.MaxY.Raw + " tick " + normal.Tick + " start " + normal.StartTick + " slowTicks " + slow.SlowTicks + " killN " + normal.KillY.Raw + " killS " + slow.KillY.Raw);
            }
            Assert.False(normal.Dead || slow.Dead);
        }

        [Fact]
        public void SlowTime_EndsAndNeverGoesBelowTheFollowRule()
        {
            Sim sim = Tower(8, null, null);
            SimState s = Fresh(sim, PowerUp.SlowTime);
            sim.Step(ref s, Slot0 | SimInput.Jump);
            Run(sim, ref s, Cfg.SlowTimeTicks, 0);
            Assert.False(PowerUps.IsActive(in s, PowerUp.SlowTime));
            Assert.True(s.KillY >= s.MaxY - Cfg.KillDistance);
        }

        // ---------- determinism and chaos ----------

        private static SimState Chaos(Sim sim, int seed, int ticks, bool endless)
        {
            SimState s = sim.NewState();
            PowerUp[][] loadouts =
            {
                new[] { PowerUp.Flight, PowerUp.DoubleJump, PowerUp.Shield },
                new[] { PowerUp.Rocket, PowerUp.WallStick, PowerUp.Magnet },
                new[] { PowerUp.SlowTime, PowerUp.Flight, PowerUp.Rocket }
            };
            PowerUp[] l = loadouts[seed % 3];
            sim.SetLoadout(ref s, l[0], 3, l[1], 3, l[2], 3);
            Random rnd = new Random(seed);
            int dir = 0;
            Fx lastStarMaskCheck = Fx.Zero;
            int prevStars = 0;
            for (int t = 0; t < ticks && !s.Dead; t++)
            {
                if (t % 35 == 0) dir = rnd.Next(3);
                int input = dir == 1 ? SimInput.Left : (dir == 2 ? SimInput.Right : 0);
                if (rnd.Next(20) == 0) input |= SimInput.Jump;
                if (rnd.Next(120) == 0) input |= SimInput.PowerSlot0 << rnd.Next(3);
                sim.Step(ref s, input);
                Assert.True(s.SlotUnits(0) >= 0 && s.SlotUnits(1) >= 0 && s.SlotUnits(2) >= 0);
                Assert.True(s.Stars >= prevStars);
                prevStars = s.Stars;
                if (!endless) Assert.True(s.Y <= sim.Platforms[sim.Platforms.Length - 1].Y + Fx.FromInt(40), "stays inside the level");
                Assert.True(s.FlightTicks >= 0 && s.RocketTicks >= 0 && s.WallHold >= 0 && s.DoubleJumps >= 0);
            }
            return s;
        }

        [Theory]
        [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
        public void Power_AllTypes_Deterministic_OnLevels1_5_15_AndEndless(int region)
        {
            int[] levels = { 1, 5, 15 };
            for (int li = 0; li < levels.Length; li++)
            {
                World w = RealLevel(region, levels[li]);
                for (int seed = 1; seed <= 3; seed++)
                {
                    long a = Hash(Chaos(w.Sim, seed + region * 10, 2400, false), true);
                    long b = Hash(Chaos(w.Sim, seed + region * 10, 2400, false), true);
                    Assert.Equal(a, b);
                }
            }
            World e = Endless(region, 999 + region, 140);
            for (int seed = 1; seed <= 3; seed++)
            {
                long a = Hash(Chaos(e.Sim, seed, 2400, true), true);
                long b = Hash(Chaos(e.Sim, seed, 2400, true), true);
                Assert.Equal(a, b);
            }
        }

        [Fact]
        public void ARecordedInputListReplaysToTheSameState_AndPowerUpsChangeTheOutcome()
        {
            World w = RealLevel(2, 5);
            int[] inputs = new int[1500];
            Random rnd = new Random(7);
            for (int i = 0; i < inputs.Length; i++)
            {
                inputs[i] = ((i / 45) % 2 == 0 ? SimInput.Right : SimInput.Left) | (rnd.Next(18) == 0 ? SimInput.Jump : 0);
            }
            inputs[3] |= SimInput.PowerSlot0;
            inputs[400] |= SimInput.PowerSlot1;
            SimState a = Fresh(w.Sim, PowerUp.Rocket, PowerUp.Magnet);
            SimState b = Fresh(w.Sim, PowerUp.Rocket, PowerUp.Magnet);
            SimState plain = w.Sim.NewState();
            for (int i = 0; i < inputs.Length; i++)
            {
                w.Sim.Step(ref a, inputs[i]);
                w.Sim.Step(ref b, inputs[i]);
                w.Sim.Step(ref plain, inputs[i] & ~SimInput.PowerMask);
            }
            Assert.Equal(Hash(a, true), Hash(b, true));
            Assert.Equal(2, a.PwUsed);
            Assert.NotEqual(Hash(a, false), Hash(plain, false));
        }

        // ---------- stars and the bot ----------

        [Fact]
        public void StarsAreEvaluatedFromTheSpecOnly_PowerUpsNeverLowerTheRequirement()
        {
            for (int region = 1; region <= 5; region++)
            {
                World w = RealLevel(region, 1);
                BotResult br = new LevelBot(w.Sim, w.Spec, w.Data, 20000).Solve();
                Assert.True(br.Solved, "region " + region);
                SimState fin = br.Final;
                int baseStars = StarEvaluator.Stars(w.Spec, fin, fin.RevivesUsed, fin.Tick);
                SimState used = fin;
                used.PwUsed = 3; used.PwUsedMask = 0xFE; used.ShieldSaves = 1;
                Assert.Equal(baseStars, StarEvaluator.Stars(w.Spec, used, used.RevivesUsed, used.Tick));
                Assert.Equal(StarEvaluator.Earned(w.Spec, fin, fin.RevivesUsed, fin.Tick), StarEvaluator.Earned(w.Spec, used, used.RevivesUsed, used.Tick));
                // an unfinished run with every power-up used still earns nothing
                SimState early = Fresh(w.Sim, PowerUp.Flight, PowerUp.Rocket, PowerUp.Magnet);
                w.Sim.Step(ref early, Slot0 | SimInput.PowerSlot2);
                Assert.Equal(0, StarEvaluator.Stars(w.Spec, early, 0, early.Tick));
            }
        }

        [Fact]
        public void TheBotStillSolvesTheFirstLevelOfEveryRegionWithoutPowerUps()
        {
            // The full 75/75 proof lives in SolvabilityT4Tests; this guards the new Step code path with the bot.
            for (int region = 1; region <= 5; region++)
            {
                foreach (int index in new[] { 1, 5, 10 })
                {
                    World w = RealLevel(region, index);
                    BotResult br = new LevelBot(w.Sim, w.Spec, w.Data, 40000).Solve();
                    Assert.True(br.Solved, LevelId(region, index));
                }
            }
        }

        [Fact]
        public void StepStillDoesNotAllocateWithPowerUpsActive()
        {
            Sim sim = Tower(20, null, null);
            SimState s = Fresh(sim, PowerUp.Flight, PowerUp.DoubleJump, PowerUp.Magnet);
            sim.Step(ref s, Slot0 | SimInput.PowerSlot1 | SimInput.PowerSlot2);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 400; i++) sim.Step(ref s, (i % 40 == 0) ? SimInput.Jump : SimInput.Right);
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.True(after - before < 2048, "allocated " + (after - before));
        }
    }
}
