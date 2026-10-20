using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>
    /// Regression tests for the owner report of 2026-10-20 ("8 to 10 platforms stacked in region 1, game over on touching").
    /// Stack rule: at most 4 platforms in any 3 u window, no stack of 5 linked platforms, and rows closer than 1.2 u only when they are
    /// horizontally separated by at least 2 u. Scroll rule: the campaign scroll speed flattens after 30 s and never exceeds 4 u/s.
    /// </summary>
    public class StackRuleTests
    {
        private readonly ITestOutputHelper output;
        public StackRuleTests(ITestOutputHelper output) { this.output = output; }

        private static readonly Fx MinStackGap = Fx.FromRatio(119, 100);   // 1.2 u less rounding of 3.6 / 3
        private static readonly Fx SeparatedBy = Fx.FromInt(2);

        /// <summary>Returns a description of the first violation of the stack rule, or null.</summary>
        public static string FindViolation(Platform[] plats)
        {
            StackStats st = StackAnalysis.Scan(plats);
            if (st.MaxIn3u > LevelBuilder.MaxPerWindow) return st.MaxIn3u + " platforms in a 3 u window at row " + st.Max3uAnchorRow;
            if (st.MaxCluster >= 5) return "stack of " + st.MaxCluster + " at row " + st.ClusterAnchorRow;
            for (int i = 0; i < plats.Length; i++)
                for (int j = i + 1; j < plats.Length; j++)
                {
                    Fx dy = plats[j].Y - plats[i].Y;
                    if (dy > MinStackGap) break;
                    if (dy <= Fx.Zero) continue;
                    if (StackAnalysis.HorizontalGap(plats[i], plats[j]) < SeparatedBy)
                        return "rows " + plats[i].Row + "/" + plats[j].Row + " only " + dy + " u apart and not separated";
                }
            return null;
        }

        [Fact]
        public void NoStackAboveTheLimitInAll75Levels()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            SimConfig cfg = new SimConfig();
            List<string> bad = new List<string>();
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 1, 5))
            {
                string v = FindViolation(LevelBuilder.Build(cfg, ll.Spec).Platforms);
                if (v != null) bad.Add(ll.Id + ": " + v);
            }
            foreach (string b in bad) output.WriteLine(b);
            Assert.Empty(bad);
        }

        [Fact]
        public void NoStackAboveTheLimitInGeneratedTowers()
        {
            SimConfig cfg = new SimConfig();
            for (int seed = 1; seed <= 40; seed++)
                for (int dd = 0; dd <= 10; dd += 2)
                {
                    GeneratorParams p = new GeneratorParams();
                    p.Seed = seed * 7919; p.Difficulty = Fx.FromRatio(dd, 10); p.Rows = 120;
                    string v = FindViolation(LevelGenerator.Generate(cfg, p));
                    Assert.True(v == null, "seed " + p.Seed + " d " + dd + ": " + v);
                }
        }

        [Fact]
        public void NoStackAboveTheLimitInEndlessTowers()
        {
            SimConfig cfg = new SimConfig();
            for (int region = 0; region < 5; region++)
                for (int seed = 1; seed <= 6; seed++)
                {
                    LevelSpec spec = EndlessTower.MakeSpec(seed * 4099, Fx.FromRatio(region * 500, 10000), SignatureKind.None, 400);
                    string v = FindViolation(EndlessTower.BuildFixed(cfg, spec, 300).Platforms);
                    Assert.True(v == null, "endless region " + region + " seed " + seed + ": " + v);
                }
        }

        [Fact]
        public void ComboLaddersDoNotStack()
        {
            SimConfig cfg = new SimConfig();
            foreach (int v in new int[] { 3, 4, 5, 6 })
            {
                LevelData d = LevelBuilder.Build(cfg, T3.Spec(9 + v, 400, 5, SignatureKind.None, GoalType.ComboChain, v, 1));
                Assert.Null(FindViolation(d.Platforms));
                Assert.True(d.LadderStartRow > 0);
            }
        }

        [Fact]
        public void ScrollSpeedFlattensAfterThirtySecondsAndStaysUnderTheCeiling()
        {
            SimConfig cfg = Region1AuditTests.GameConfig(100);
            Assert.Equal(6, cfg.ScrollStepMaxSteps);
            // d = 1: base 2.4 u/s; step 100 percent per 5 s would be 7.2 u/s at 10 s without the ceiling.
            Platform[] plats = new Platform[] { MakeStart(), MakeTop() };
            Sim sim = new Sim(cfg, LevelData.FromPlatforms(plats), Fx.One, false, false);
            SimState s = sim.NewState();
            s.StartTick = 0;
            s.Grounded = true;
            Fx last = s.KillY;
            double maxPerSecond = 0;
            for (int t = 0; t < 70 * 60; t++)
            {
                s.Y = s.MaxY = s.KillY + Fx.FromInt(8);   // alive, and MaxY - KillDistance stays below KillY so only the scroll moves it
                sim.Step(ref s, 0);
                double perSecond = (s.KillY.Raw - last.Raw) / 65536.0 * 60;
                if (perSecond > maxPerSecond) maxPerSecond = perSecond;
                last = s.KillY;
            }
            Assert.True(maxPerSecond <= 4.02, "scroll " + maxPerSecond);
            // Flat from 30 s on: speed at 31 s equals speed at 69 s.
            SimConfig c2 = Region1AuditTests.GameConfig(20);
            Sim sim2 = new Sim(c2, LevelData.FromPlatforms(plats), Fx.FromRatio(2, 10), false, false);
            double v31 = Speed(sim2, 31), v69 = Speed(sim2, 69);
            Assert.Equal(v31, v69, 3);
            Assert.True(Speed(sim2, 29) < Speed(sim2, 36) + 0.001);
            Assert.True(Speed(sim2, 6) > Speed(sim2, 1));
        }

        private static double Speed(Sim sim, int second)
        {
            SimState s = sim.NewState();
            s.StartTick = 0;
            s.Tick = second * 60;
            s.Y = s.KillY + Fx.FromInt(8);
            s.MaxY = s.Y;
            Fx before = s.KillY;
            sim.Step(ref s, 0);
            return (s.KillY.Raw - before.Raw) / 65536.0 * 60;
        }

        private static Platform MakeStart()
        {
            Platform p = new Platform(); p.Id = 0; p.Row = 0; p.X = Fx.FromInt(5); p.Y = Fx.Zero; p.Width = Fx.FromInt(10); return p;
        }

        private static Platform MakeTop()
        {
            Platform p = new Platform(); p.Id = 1; p.Row = 1; p.X = Fx.FromInt(5); p.Y = Fx.FromInt(3); p.Width = Fx.FromInt(10); return p;
        }

        [Fact]
        public void BotSurvivesRegion1AtTheHardestStepTable()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            // The game config at Insane: +100 percent per 5 s, runtime cap 150 percent (GameSession), step limit and ceiling from SimConfig.
            SimConfig cfg = Region1AuditTests.GameConfig(100);
            cfg.ScrollStepCapPercent = 150;
            List<string> failures = new List<string>();
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 1, 1))
            {
                LevelData data = LevelBuilder.Build(cfg, ll.Spec);
                int budget = SolvabilityT4Tests.NodeBudget * System.Math.Max(1, ll.Spec.RowsFor() / 60);
                BotResult r = new LevelBot(new Sim(cfg, data, ll.Spec), ll.Spec, data, budget).Solve();
                output.WriteLine(ll.Id + " solved=" + r.Solved + " ticks=" + r.Ticks + " bestRow=" + r.BestRow);
                if (!r.Solved) failures.Add(ll.Id + " bestRow=" + r.BestRow);
                else if (r.Ticks < 90 * 60 && ll.Spec.TargetRow() < 150) { /* short level: finished before 90 s, fine */ }
            }
            Assert.Empty(failures);
        }
    }
}
