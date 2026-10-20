using SkyHopper.Simulation;
using NUnit.Framework;

namespace SkyHopper.Tests
{
    // Runs the engine-agnostic Simulation inside Unity as a cross-check of the xunit suite in Tests/Simulation.
    public class SimulationSmokeTests
    {
        [Test]
        public void StandingJumpLeavesTheGroundAndLands()
        {
            var cfg = new SimConfig();
            var p = new Platform { Id = 0, Row = 0, X = cfg.ScreenWidth / 2, Y = Fx.Zero, Width = cfg.ScreenWidth };
            var sim = new Sim(cfg, new[] { p }, Fx.Zero, false);
            var s = sim.NewState();
            sim.Step(ref s, SimInput.Jump);
            Assert.IsFalse(s.Grounded);
            for (int i = 0; i < 300 && !s.Grounded; i++) sim.Step(ref s, 0);
            Assert.IsTrue(s.Grounded);
        }

        [Test]
        public void GeneratorIsDeterministic()
        {
            var cfg = new SimConfig();
            var gp = new GeneratorParams { Seed = 4242, Difficulty = Fx.FromRatio(1, 2), Rows = 50 };
            var a = LevelGenerator.Generate(cfg, gp);
            var b = LevelGenerator.Generate(cfg, gp);
            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].X.Raw, b[i].X.Raw);
                Assert.AreEqual(a[i].Y.Raw, b[i].Y.Raw);
                Assert.AreEqual(a[i].Width.Raw, b[i].Width.Raw);
            }
        }

        [Test]
        public void BotSolvesAMeadowSpringsStyleLevel()
        {
            var cfg = new SimConfig();
            var gp = new GeneratorParams { Seed = 1, Difficulty = Fx.FromRatio(1, 20), Rows = 30 };
            var plats = LevelGenerator.Generate(cfg, gp);
            var sim = new Sim(cfg, plats, gp.Difficulty, false);
            var r = new LevelBot(sim, 30, 20000).Solve();
            Assert.IsTrue(r.Solved, "best row " + r.BestRow);
        }
    }
}
