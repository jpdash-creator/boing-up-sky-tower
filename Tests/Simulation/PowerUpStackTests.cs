using System;
using System.Collections.Generic;
using Xunit;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Power-up rework of 2026-10-31: base durations, STACKING (a tap adds the full base time onto the remaining time, every tap uses one unit,
    /// no maximum), saturating counts, no stock cap / loadout, Shield/Rocket/Flight make gongs harmless, Slow Time exactly 50 percent,
    /// Rocket 20 floors in 4 s, Flight steering, timed Double Jump. Category PowerUps.
    /// </summary>
    [Trait("Category", "PowerUps")]
    public class PowerUpStackTests
    {
        private static readonly SimConfig Cfg = new SimConfig();

        private static Sim Tower(int rows, Pickup[] pickups = null, Hazard[] hazards = null)
        {
            List<Platform> extra = new List<Platform>();
            for (int i = 1; i <= rows; i++) extra.Add(T3.P(i, 5.0, 3.0 * i, 10.0, PlatformKind.Normal));
            LevelData d = T3.World(Cfg, pickups, hazards, extra.ToArray());
            return new Sim(Cfg, d, Fx.FromRatio(5, 10), false, false);
        }

        /// <summary>A fresh state. The owned stock is counted outside the simulation (RunSession), so the sim itself accepts every tap.</summary>
        private static SimState With(Sim sim, PowerUp p, int units)
        {
            return sim.NewState();
        }

        private static int Bit(PowerUp p)
        {
            return PowerUps.InputBit(p);
        }

        private static void Run(Sim sim, ref SimState s, int ticks, int input)
        {
            for (int i = 0; i < ticks; i++) sim.Step(ref s, input);
        }

        private static readonly PowerUp[] Timed =
        {
            PowerUp.Flight, PowerUp.Rocket, PowerUp.DoubleJump, PowerUp.WallStick, PowerUp.Shield, PowerUp.Magnet, PowerUp.SlowTime
        };

        // ---------- base durations ----------

        [Fact]
        public void BaseDurationsAreTheOwnerValues()
        {
            Assert.Equal(30 * 60, PowerUps.TotalTicks(Cfg, PowerUp.Magnet));
            Assert.Equal(60 * 60, PowerUps.TotalTicks(Cfg, PowerUp.Shield));
            Assert.Equal(30 * 60, PowerUps.TotalTicks(Cfg, PowerUp.WallStick));
            Assert.Equal(30 * 60, PowerUps.TotalTicks(Cfg, PowerUp.SlowTime));
            Assert.Equal(30 * 60, PowerUps.TotalTicks(Cfg, PowerUp.DoubleJump));
            Assert.Equal(4 * 60, PowerUps.TotalTicks(Cfg, PowerUp.Rocket));
            Assert.Equal(20, Cfg.RocketFloors);
            Assert.Equal(10 * 60, PowerUps.TotalTicks(Cfg, PowerUp.Flight));
            Assert.Equal(50, Cfg.SlowTimePercent);
        }

        [Theory]
        [InlineData(PowerUp.Flight)] [InlineData(PowerUp.Rocket)] [InlineData(PowerUp.DoubleJump)] [InlineData(PowerUp.WallStick)]
        [InlineData(PowerUp.Shield)] [InlineData(PowerUp.Magnet)] [InlineData(PowerUp.SlowTime)]
        public void OneTapStartsTheBaseDuration_AndUsesOneUnit(PowerUp p)
        {
            Sim sim = Tower(120);
            SimState s = With(sim, p, 5);
            sim.Step(ref s, Bit(p));
            Assert.Equal(1, s.PwUsed);
            // the activation tick already counted down once
            Assert.Equal(PowerUps.TotalTicks(Cfg, p) - 1, PowerUps.ActiveTicks(in s, p));
            Assert.True(PowerUps.IsActive(in s, p));
        }

        // ---------- stacking ----------

        [Theory]
        [InlineData(PowerUp.Flight)] [InlineData(PowerUp.Rocket)] [InlineData(PowerUp.DoubleJump)] [InlineData(PowerUp.WallStick)]
        [InlineData(PowerUp.Shield)] [InlineData(PowerUp.Magnet)] [InlineData(PowerUp.SlowTime)]
        public void ATapWhileActiveAddsTheFullBaseTimeOnTopOfTheRemainingTime(PowerUp p)
        {
            Sim sim = Tower(300);
            SimState s = With(sim, p, 10);
            sim.Step(ref s, Bit(p));
            Run(sim, ref s, 50, 0);
            int remaining = PowerUps.ActiveTicks(in s, p);
            Assert.Equal(PowerUps.TotalTicks(Cfg, p) - 51, remaining);
            sim.Step(ref s, Bit(p));
            Assert.Equal(2, s.PwUsed);   // every accepted tap counts as one used unit
            Assert.Equal(remaining + PowerUps.TotalTicks(Cfg, p) - 1, PowerUps.ActiveTicks(in s, p));
            // a third tap in the same tick window stacks again, with no maximum
            sim.Step(ref s, Bit(p));
            sim.Step(ref s, Bit(p));
            Assert.Equal(remaining + 3 * PowerUps.TotalTicks(Cfg, p) - 3, PowerUps.ActiveTicks(in s, p));
            Assert.Equal(4, s.PwUsed);
        }

        [Fact]
        public void MagnetExample_30sPlusTapIsRemainingPlus30s()
        {
            Sim sim = Tower(10);
            SimState s = With(sim, PowerUp.Magnet, 2);
            sim.Step(ref s, Bit(PowerUp.Magnet));
            Run(sim, ref s, 10 * 60, 0);   // 10 s gone, 20 s left
            Assert.Equal(20 * 60 - 1, s.MagnetTicks);
            sim.Step(ref s, Bit(PowerUp.Magnet));
            Assert.Equal(50 * 60 - 2, s.MagnetTicks);   // 50 s (less the two ticks already counted down)
        }

        [Fact]
        public void RocketStacksAnotherTwentyFloorsAndFourSecondsOnTop()
        {
            Sim sim = Tower(100);
            SimState s = With(sim, PowerUp.Rocket, 3);
            sim.Step(ref s, Bit(PowerUp.Rocket));
            Run(sim, ref s, 100, 0);
            Assert.Equal(20, s.RocketTargetRow);
            int remaining = s.RocketTicks;
            sim.Step(ref s, Bit(PowerUp.Rocket));
            Assert.Equal(40, s.RocketTargetRow);
            Assert.Equal(remaining + Cfg.RocketTicks - 1, s.RocketTicks);
            Assert.Equal(T3.F(120.0), s.RocketTargetY);   // row 40 of the flat tower: y = 3 * 40
            int guard = 0;
            while (s.RocketTicks > 0 && guard++ < 2000) sim.Step(ref s, 0);
            Assert.Equal(T3.F(120.0), s.Y);
            Assert.Equal(2 * Cfg.RocketTicks, s.Tick);   // two stacked rockets: 8 s in total
            Assert.False(s.Dead);
        }

        [Fact]
        public void TapsHaveNoMaximum_AndTimersAndCountsSaturateInsteadOfOverflowing()
        {
            Sim sim = Tower(10);
            SimState s = sim.NewState();
            for (int i = 0; i < 150; i++) sim.Step(ref s, Bit(PowerUp.Magnet));
            Assert.Equal(150, s.PwUsed);
            Assert.Equal(150 * Cfg.MagnetTicks - 150, s.MagnetTicks);

            Assert.Equal(int.MaxValue, PowerUps.SatAdd(int.MaxValue, 1));
            Assert.Equal(int.MaxValue, PowerUps.SatAdd(int.MaxValue - 1, 5));
            SimState nearMax = sim.NewState();
            nearMax.MagnetTicks = int.MaxValue - 5;
            sim.Step(ref nearMax, Bit(PowerUp.Magnet));
            Assert.True(nearMax.MagnetTicks > 0, "no overflow to negative");
            Assert.Equal(int.MaxValue - 1, nearMax.MagnetTicks);   // saturated at int.MaxValue, then one tick counted down
            nearMax.PwUsed = int.MaxValue;
            sim.Step(ref nearMax, Bit(PowerUp.Shield));
            Assert.Equal(int.MaxValue, nearMax.PwUsed);
        }

        [Fact]
        public void EveryTypeIsUsableTogether_NoLoadoutOfThree()
        {
            Sim sim = Tower(10);
            SimState s = sim.NewState();
            // Flight and Rocket exclude each other; the five others all run together with one of them
            sim.Step(ref s, SimInput.PowerMask);
            int active = 0;
            for (int i = 1; i <= PowerUps.TypeCount; i++) if (PowerUps.IsActive(in s, (PowerUp)i)) active++;
            Assert.Equal(6, active);
            Assert.Equal(6, s.PwUsed);
            Assert.Equal(0, s.RocketTicks);   // Flight won; the rocket was refused
        }

        [Fact]
        public void NoTapNothingHappens()
        {
            Sim sim = Tower(10);
            SimState s = sim.NewState();
            Run(sim, ref s, 5, 0);
            Assert.Equal(0, s.PwUsed);
            foreach (PowerUp p in Timed) Assert.False(PowerUps.IsActive(in s, p));
        }

        // ---------- gongs ----------

        private static Hazard PathGong()
        {
            return T3.Gong(5.0, 30.0, 0.8, 0.0, 120, 0);
        }

        [Fact]
        public void TheGongControlHitsAnUnprotectedPlayer()
        {
            Sim sim = Tower(30, null, new[] { PathGong() });
            SimState s = sim.NewState();
            s.Y = T3.F(30.0); s.X = T3.F(5.0); s.Grounded = false; s.Coyote = 0; s.Vy = Fx.Zero;
            sim.Step(ref s, 0);
            Assert.Equal(1, s.Hits);
            Assert.NotEqual(Fx.Zero, s.Vx);   // knocked back
        }

        [Fact]
        public void Shield_BlocksGongsCompletely_NoHitNoKnockbackForTheWholeDuration()
        {
            Sim sim = Tower(30, null, new[] { PathGong() });
            SimState s = With(sim, PowerUp.Shield, 1);
            sim.Step(ref s, Bit(PowerUp.Shield));
            s.Y = T3.F(30.0); s.X = T3.F(5.0); s.Grounded = false; s.Coyote = 0; s.Vy = Fx.Zero;
            for (int t = 0; t < 20; t++)
            {
                sim.Step(ref s, 0);
                Assert.Equal(0, s.Hits);
                Assert.Equal(Fx.Zero, s.Vx);
                s.Y = T3.F(30.0); s.Vy = Fx.Zero;   // keep it inside the gong
            }
            // once the Shield has run out, the same spot hurts again
            s.ShieldTicks = 0;
            s.Y = T3.F(30.0); s.X = T3.F(5.0); s.Vy = Fx.Zero; s.Invuln = 0;
            sim.Step(ref s, 0);
            Assert.Equal(1, s.Hits);
        }

        [Fact]
        public void RocketAndFlight_GongsCannotHurt()
        {
            Sim sim = Tower(60, null, new[] { PathGong() });
            SimState r = With(sim, PowerUp.Rocket, 1);
            sim.Step(ref r, Bit(PowerUp.Rocket));
            int guard = 0;
            while (r.RocketTicks > 0 && guard++ < 1000) { sim.Step(ref r, 0); Assert.Equal(0, r.Hits); }
            Assert.True(r.Y > T3.F(30.0), "the rocket passed the gong");

            SimState f = With(sim, PowerUp.Flight, 1);
            sim.Step(ref f, Bit(PowerUp.Flight));
            guard = 0;
            while (f.FlightTicks > 0 && guard++ < 1000) { sim.Step(ref f, 0); Assert.Equal(0, f.Hits); }
            Assert.True(f.Y > T3.F(30.0), "the flight passed the gong");
        }

        // ---------- Slow Time ----------

        [Fact]
        public void SlowTime_ScrollIsExactlyHalf()
        {
            Sim sim = Tower(20);
            SimState normal = sim.NewState();
            SimState slow = With(sim, PowerUp.SlowTime, 1);
            sim.Step(ref normal, SimInput.Jump);
            sim.Step(ref slow, SimInput.Jump | Bit(PowerUp.SlowTime));
            Fx n0 = normal.KillY, s0 = slow.KillY;
            const int ticks = 20;
            Run(sim, ref normal, ticks, 0);
            Run(sim, ref slow, ticks, 0);
            Assert.NotEqual(normal.MaxY - Cfg.KillDistance, normal.KillY);   // the follow rule does not hide the scroll
            long dn = normal.KillY.Raw - n0.Raw, ds = slow.KillY.Raw - s0.Raw;
            Assert.True(dn > 0);
            Assert.True(Math.Abs(dn - 2 * ds) <= ticks, "dn " + dn + " ds " + ds);   // 50 percent, within the 1 raw unit rounding per tick
        }

        // ---------- Rocket 20 floors in 4 s ----------

        [Fact]
        public void Rocket_ClimbsTwentyFloorsInFourSeconds_WithoutSteering()
        {
            Sim sim = Tower(80);
            SimState s = With(sim, PowerUp.Rocket, 1);
            Fx x0 = s.X;
            sim.Step(ref s, Bit(PowerUp.Rocket) | SimInput.Right);
            int ticks = 1;
            while (s.RocketTicks > 0 && ticks < 1000) { sim.Step(ref s, SimInput.Right | SimInput.Left); ticks++; }
            Assert.True(ticks <= 4 * 60, "ticks " + ticks);
            Assert.Equal(T3.F(60.0), s.Y);   // row 20 of the flat tower (3 u per row)
            Assert.Equal(x0, s.X);
            Assert.False(s.Dead);
        }

        [Fact]
        public void Rocket_NeverPassesTheFinishRows()
        {
            Sim sim = Tower(15);   // limit row 12 (y = 36), the rocket wants row 20
            SimState s = With(sim, PowerUp.Rocket, 2);
            sim.Step(ref s, Bit(PowerUp.Rocket));
            for (int t = 0; t < 600; t++) { sim.Step(ref s, t == 100 ? Bit(PowerUp.Rocket) : 0); Assert.True(s.Y <= T3.F(36.0)); }
        }

        // ---------- Flight ----------

        [Fact]
        public void Flight_SteersLeftAndRightAndRisesTenSeconds()
        {
            Sim sim = Tower(300);
            SimState s = With(sim, PowerUp.Flight, 1);
            Fx x0 = s.X, y0 = s.Y;
            sim.Step(ref s, Bit(PowerUp.Flight) | SimInput.Right);
            Run(sim, ref s, 40, SimInput.Right);
            Assert.True(s.X > x0 + Fx.One, "steers right");
            Fx xr = s.X;
            Run(sim, ref s, 60, SimInput.Left);
            Assert.True(s.X < xr, "steers left");
            int ticks = 101;
            while (s.FlightTicks > 0 && ticks < 2000) { sim.Step(ref s, 0); ticks++; }
            Assert.Equal(10 * 60, ticks);
            Assert.Equal(y0 + Cfg.FlightRise * 600, s.Y);
        }

        [Fact]
        public void FlightAndRocketRemainExclusive_ButEachStacksWithItself()
        {
            Sim sim = Tower(100);
            SimState s = sim.NewState();
            sim.Step(ref s, Bit(PowerUp.Flight));
            sim.Step(ref s, Bit(PowerUp.Rocket));
            Assert.Equal(0, s.RocketTicks);
            sim.Step(ref s, Bit(PowerUp.Flight));
            Assert.Equal(2 * Cfg.FlightTicks - 3, s.FlightTicks);
        }

        // ---------- Double Jump (timed) ----------

        [Fact]
        public void DoubleJump_IsAvailableInTheAirOncePerAirPhaseWhileTheWindowRuns()
        {
            Sim sim = Tower(2);
            SimState s = With(sim, PowerUp.DoubleJump, 1);
            sim.Step(ref s, Bit(PowerUp.DoubleJump));
            int airJumps = 0;
            bool prevUsed = false;
            for (int t = 0; t < 600; t++)
            {
                bool falling = !s.Grounded && s.Coyote == 0 && s.Vy <= Fx.Zero;
                sim.Step(ref s, (s.Grounded || falling) ? SimInput.Jump : 0);
                if (s.AirJumpUsed && !prevUsed) airJumps++;
                prevUsed = s.AirJumpUsed;
            }
            Assert.True(airJumps >= 2, "an extra jump in more than one air phase: " + airJumps);
        }

        [Fact]
        public void DoubleJump_StopsWorkingWhenTheTimerRunsOut_AndStacksForMoreTime()
        {
            Sim sim = Tower(2);
            SimState s = With(sim, PowerUp.DoubleJump, 2);
            sim.Step(ref s, Bit(PowerUp.DoubleJump) | SimInput.Jump);
            sim.Step(ref s, Bit(PowerUp.DoubleJump));
            Assert.Equal(2 * Cfg.DoubleJumpWindowTicks - 2, s.DoubleJumpTicks);
            s.DoubleJumpTicks = 0;   // window over
            for (int t = 0; t < 60 && s.Vy > Fx.Zero; t++) sim.Step(ref s, 0);
            Assert.True(!s.Grounded && s.Vy <= Fx.Zero);
            sim.Step(ref s, SimInput.Jump);
            Assert.True(s.Vy < Fx.Zero, "no air jump after the window");
        }

        // ---------- Wall Stick ----------

        [Fact]
        public void WallStick_LastsThirtySecondsPerTap()
        {
            Sim sim = Tower(6);
            SimState s = With(sim, PowerUp.WallStick, 1);
            sim.Step(ref s, Bit(PowerUp.WallStick));
            Run(sim, ref s, 29 * 60, 0);
            Assert.True(PowerUps.IsActive(in s, PowerUp.WallStick));
            Run(sim, ref s, 60, 0);
            Assert.False(PowerUps.IsActive(in s, PowerUp.WallStick));
        }

        // ---------- determinism ----------

        [Fact]
        public void StackedRunsReplayToTheSameState()
        {
            long a = Replay(), b = Replay();
            Assert.Equal(a, b);
        }

        private static long Replay()
        {
            Sim sim = Tower(120);
            SimState s = sim.NewState();
            Random rnd = new Random(31);
            long h = 17;
            for (int t = 0; t < 3000 && !s.Dead; t++)
            {
                int input = (t / 50) % 2 == 0 ? SimInput.Right : SimInput.Left;
                if (rnd.Next(15) == 0) input |= SimInput.Jump;
                if (rnd.Next(90) == 0) input |= PowerUps.InputBit((PowerUp)(1 + rnd.Next(PowerUps.TypeCount)));
                sim.Step(ref s, input);
                unchecked
                {
                    h = h * 31 + s.X.Raw; h = h * 31 + s.Y.Raw; h = h * 31 + s.FlightTicks; h = h * 31 + s.RocketTicks;
                    h = h * 31 + s.ShieldTicks; h = h * 31 + s.PwUsed; h = h * 31 + s.RocketTargetRow;
                }
            }
            return h;
        }
    }
}
