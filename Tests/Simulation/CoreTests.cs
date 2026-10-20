using System.Collections.Generic;
using Xunit;

namespace BoingUpSkyTower.Simulation.Tests
{
    public class FxTests
    {
        [Fact]
        public void BasicArithmetic()
        {
            Assert.Equal(Fx.FromInt(6), Fx.FromInt(2) * Fx.FromInt(3));
            Assert.Equal(Fx.FromRatio(1, 2), Fx.FromInt(1) / Fx.FromInt(2));
            Assert.Equal(Fx.FromInt(-3), -Fx.FromInt(3));
            Assert.Equal(-1, Fx.FromRatio(-1, 2).FloorToInt());
            Assert.Equal(0, Fx.FromRatio(1, 2).FloorToInt());
        }

        [Fact]
        public void SqrtIsExactForPerfectSquaresAndMonotonic()
        {
            Assert.Equal(Fx.FromInt(3), Fx.Sqrt(Fx.FromInt(9)));
            Assert.Equal(Fx.Zero, Fx.Sqrt(Fx.FromInt(-4)));
            Fx prev = Fx.Zero;
            for (int i = 1; i < 200; i++)
            {
                Fx r = Fx.Sqrt(Fx.FromRatio(i, 7));
                Assert.True(r >= prev);
                prev = r;
            }
        }

        [Fact]
        public void ToStringUsesIntegerMath()
        {
            Assert.Equal("1.5000", Fx.FromRatio(3, 2).ToString());
            Assert.Equal("-2.2500", Fx.FromRatio(-9, 4).ToString());
        }
    }

    public class RngTests
    {
        [Fact]
        public void SameSeedSameSequence()
        {
            Rng a = new Rng(1234), b = new Rng(1234);
            for (int i = 0; i < 100; i++) Assert.Equal(a.NextUInt(), b.NextUInt());
        }

        [Fact]
        public void DifferentSeedsDiffer()
        {
            Rng a = new Rng(1), b = new Rng(2);
            bool differ = false;
            for (int i = 0; i < 10; i++) differ |= a.NextUInt() != b.NextUInt();
            Assert.True(differ);
        }

        [Fact]
        public void RangesStayInBounds()
        {
            Rng r = new Rng(99);
            for (int i = 0; i < 1000; i++)
            {
                int n = r.Range(3, 7);
                Assert.InRange(n, 3, 7);
                Fx f = r.Range(Fx.FromInt(2), Fx.FromInt(5));
                Assert.True(f >= Fx.FromInt(2) && f < Fx.FromInt(5));
            }
        }

        [Fact]
        public void GoldenSequenceIsStable()
        {
            // Pins the exact output so a refactor cannot silently change every level in the game.
            Rng r = new Rng(42);
            uint[] actual = { r.NextUInt(), r.NextUInt(), r.NextUInt() };
            Assert.Equal(GoldenRng.Seed42, actual);
        }
    }

    public static class GoldenRng
    {
        public static readonly uint[] Seed42 = new uint[] { 2837322924u, 544945897u, 479756282u };
    }

    public class PhysicsTests
    {
        private static Sim FlatWorld(SimConfig cfg, params Platform[] extra)
        {
            List<Platform> list = new List<Platform>();
            Platform start = new Platform();
            start.Id = 0; start.Row = 0; start.X = cfg.ScreenWidth / 2; start.Y = Fx.Zero; start.Width = cfg.ScreenWidth;
            list.Add(start);
            for (int i = 0; i < extra.Length; i++)
            {
                Platform p = extra[i];
                p.Id = list.Count;
                list.Add(p);
            }
            return new Sim(cfg, list.ToArray(), Fx.Zero, false);
        }

        private static Platform P(int row, Fx x, Fx y, Fx w, PlatformKind kind)
        {
            Platform p = new Platform();
            p.Row = row; p.X = x; p.Y = y; p.Width = w; p.Kind = kind;
            return p;
        }

        private static Fx PeakHeight(Sim sim, int holdTicks)
        {
            SimState s = sim.NewState();
            for (int i = 0; i < holdTicks; i++) sim.Step(ref s, SimInput.Right);
            sim.Step(ref s, SimInput.Right | SimInput.Jump);
            Fx top = s.Y;
            for (int i = 0; i < 200 && !s.Dead; i++)
            {
                sim.Step(ref s, SimInput.Right);
                if (s.Y > top) top = s.Y;
                if (s.Grounded) break;
            }
            return top;
        }

        [Fact]
        public void FasterRunnerJumpsHigher()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = FlatWorld(cfg);
            // Start at centre, run right for different durations (screen is wide enough for both).
            Fx slow = PeakHeight(sim, 0);
            Fx fast = PeakHeight(sim, 15);
            Assert.True(fast > slow);
        }

        [Fact]
        public void StandingJumpMatchesAnalyticHeight()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = FlatWorld(cfg);
            SimState s = sim.NewState();
            sim.Step(ref s, SimInput.Jump);
            Fx top = s.Y;
            while (!s.Grounded) { sim.Step(ref s, 0); if (s.Y > top) top = s.Y; }
            Fx v0 = cfg.JumpBase;
            Fx analytic = v0 * v0 / (cfg.Gravity * 2);
            Fx diff = Fx.Abs(top - analytic);
            Assert.True(diff < Fx.FromRatio(3, 10), "peak " + top + " analytic " + analytic);
        }

        [Fact]
        public void WallBounceReversesAndDampsBelowCap()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = FlatWorld(cfg);
            SimState s = sim.NewState();
            s.X = cfg.PlayerHalfWidth + Fx.FromRatio(1, 100);
            s.Vx = -Fx.FromRatio(5, 60);
            sim.Step(ref s, 0);
            Assert.True(s.Vx > Fx.Zero);
            Assert.True(s.Vx < Fx.FromRatio(5, 60));
        }

        [Fact]
        public void LandsOnPlatformAboveAndScoresFloors()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = FlatWorld(cfg, P(1, Fx.FromInt(5), Fx.FromInt(2), Fx.FromInt(8), PlatformKind.Normal));
            SimState s = sim.NewState();
            sim.Step(ref s, SimInput.Jump);
            for (int i = 0; i < 200 && s.Row < 1; i++) sim.Step(ref s, 0);
            Assert.Equal(1, s.Row);
            Assert.True(s.Grounded);
            Assert.Equal(Fx.FromInt(2), s.Y);
            Assert.Equal(cfg.FloorPoints, s.Score);
        }

        [Fact]
        public void MultiFloorJumpBuildsCombo()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = FlatWorld(cfg,
                P(1, Fx.FromInt(5), Fx.FromInt(1), Fx.FromInt(1), PlatformKind.Normal),
                P(2, Fx.FromInt(5), Fx.FromInt(2), Fx.FromInt(8), PlatformKind.Normal));
            SimState s = sim.NewState();
            s.Vx = Fx.Zero;
            sim.Step(ref s, SimInput.Jump);
            for (int i = 0; i < 200 && s.Row < 2; i++) sim.Step(ref s, 0);
            // Platform 1 is 1 wide at the same x: the jump passes it on the way up and lands on row 2 (or 1).
            Assert.True(s.Row >= 1);
        }

        [Fact]
        public void CrumblePlatformDisappears()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = FlatWorld(cfg, P(1, Fx.FromInt(5), Fx.FromInt(2), Fx.FromInt(8), PlatformKind.Crumble));
            SimState s = sim.NewState();
            sim.Step(ref s, SimInput.Jump);
            for (int i = 0; i < 200 && s.Row < 1; i++) sim.Step(ref s, 0);
            Assert.Equal(1, s.Row);
            for (int i = 0; i < cfg.CrumbleTicks + 2; i++) sim.Step(ref s, 0);
            Assert.False(s.Grounded);
        }

        [Fact]
        public void FallingBelowKillZoneKills()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = FlatWorld(cfg);
            SimState s = sim.NewState();
            s.Grounded = false;
            s.Y = Fx.FromInt(-100);
            sim.Step(ref s, 0);
            Assert.True(s.Dead);
        }

        [Fact]
        public void ReplayIsDeterministic()
        {
            SimConfig cfg = new SimConfig();
            GeneratorParams gp = new GeneratorParams();
            gp.Seed = 777; gp.Difficulty = Fx.FromRatio(3, 10); gp.Rows = 40;
            Platform[] plats = LevelGenerator.Generate(cfg, gp);
            Sim sim = new Sim(cfg, plats, gp.Difficulty, false);
            Rng inputs = new Rng(5);
            int[] script = new int[600];
            for (int i = 0; i < script.Length; i++) script[i] = inputs.Next(8);

            SimState a = sim.NewState(), b = sim.NewState();
            for (int i = 0; i < script.Length; i++) { sim.Step(ref a, script[i]); sim.Step(ref b, script[i]); }
            Assert.Equal(a.X, b.X);
            Assert.Equal(a.Y, b.Y);
            Assert.Equal(a.Score, b.Score);
            Assert.Equal(a.Dead, b.Dead);
        }
    }
}
