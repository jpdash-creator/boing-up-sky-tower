using System.Collections.Generic;
using BoingUpSkyTower.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>
    /// H7 (owner 2026-10-31): in Candy Canyon level 7 touching the wall ended the run ("Wall bounce!", twice). The no-wall level (7, now 10) of every region
    /// had the NoWallBounce goal, whose rule ended the run at the first wall touch. Touching a wall must NEVER end or fail a run.
    /// </summary>
    public class WallSafetyTests
    {
        readonly ITestOutputHelper output;
        public WallSafetyTests(ITestOutputHelper output) { this.output = output; }

        static List<LoadedLevel> All()
        {
            string root = ContentLoader.FindRoot();
            return root == null ? new List<LoadedLevel>() : ContentLoader.LoadRegions(root, 1, 5);
        }

        /// <summary>Presses into one wall (jumping all the time) on all 125 levels: the run must never be ended by the wall.</summary>
        [Theory]
        [InlineData(SimInput.Left)]
        [InlineData(SimInput.Right)]
        [Trait("Category", "CriticalUX")]
        public void PressingIntoAWallOnlyFailsTheNoWallBounceLevels(int dir)
        {
            SimConfig cfg = new SimConfig();
            var bad = new List<string>();
            int levels = 0, wallTicks = 0, noWallFails = 0;
            foreach (LoadedLevel ll in All())
            {
                levels++;
                LevelData d = LevelBuilder.Build(cfg, ll.Spec);
                Sim sim = new Sim(cfg, d, ll.Spec);
                SimState s = sim.NewState();
                for (int t = 0; t < 1500 && !s.Dead; t++)
                {
                    sim.Step(ref s, dir | SimInput.Jump);
                    if (s.X <= cfg.PlayerHalfWidth || s.X >= cfg.ScreenWidth - cfg.PlayerHalfWidth) wallTicks++;
                    bool noWall = ll.Spec.Goal.Type == GoalType.NoWallBounce;
                    bool failed = GoalEvaluator.IsFailed(ll.Spec, s);
                    if (failed != (noWall && s.WallBounces > 0)) { bad.Add(ll.Id + " wrong fail state at tick " + t + " (WallBounces " + s.WallBounces + ", failed " + failed + ")"); break; }
                    if (failed) { noWallFails++; break; }
                }
            }
            output.WriteLine(levels + " levels, " + wallTicks + " ticks on a wall, " + bad.Count + " problems");
            foreach (string b in bad) output.WriteLine(b);
            Assert.Equal(125, levels);
            Assert.True(wallTicks > 0, "the scripted run must really touch the wall");
            Assert.Equal(5, noWallFails);   // exactly lvl_00X_10 fail (the no-wall level moved from 7 to 10, Bug List 6 item 4) on a wall touch, with the explicit reason (GoalLost)
            Assert.Empty(bad);
        }

        /// <summary>A wall touch is not a goal rule either: the goal must stay reachable after one (no goal depends on WallBounces).</summary>
        [Fact]
        [Trait("Category", "CriticalUX")]
        public void OnlyTheNoWallBounceLevelsReactToAWallBounce()
        {
            foreach (LoadedLevel ll in All())
            {
                SimState s = new SimState();
                s.WallBounces = 5;
                Assert.Equal(ll.Spec.Goal.Type == GoalType.NoWallBounce, GoalEvaluator.IsFailed(ll.Spec, s));
                Assert.True(ll.Id.EndsWith("_10") == (ll.Spec.Goal.Type == GoalType.NoWallBounce), ll.Id);
                s.Row = ll.Spec.RowsFor() + 5;
                s.Coins = 100000;
                s.WallBounces = 0;
                bool clean = GoalEvaluator.IsComplete(ll.Spec, s);
                s.WallBounces = 5;
                if (ll.Spec.Goal.Type != GoalType.NoWallBounce) Assert.Equal(clean, GoalEvaluator.IsComplete(ll.Spec, s));
            }
        }

        /// <summary>Static audit: hazards (gongs) legitimately swing near the walls, so the guarantee is that a hazard touch never kills: it only knocks back.
        /// Every hazard in all 75 levels is a Gong, and a player standing in a gong at the wall is knocked back, alive, with the run not failed.</summary>
        [Fact]
        [Trait("Category", "CriticalUX")]
        public void AHazardTouchAtTheWallNeverKills()
        {
            SimConfig cfg = new SimConfig();
            int tested = 0;
            foreach (LoadedLevel ll in All())
            {
                LevelData d = LevelBuilder.Build(cfg, ll.Spec);
                Sim sim = new Sim(cfg, d, ll.Spec);
                foreach (Hazard h in d.Hazards)
                {
                    Assert.Equal(HazardKind.Gong, h.Kind);
                    if (tested >= 400 || h.X - h.Amplitude - h.Radius > cfg.PlayerHalfWidth + cfg.PlayerHitRadius) continue;
                    SimState s = sim.NewState();
                    Fx gx, gy;
                    sim.HazardPosition(h.Id, s.Tick + 1, out gx, out gy);
                    s.X = cfg.PlayerHalfWidth;
                    s.Y = gy - cfg.PlayerCenterY;
                    s.Grounded = false;
                    s.KillY = s.Y - Fx.FromInt(50);
                    s.MaxY = s.Y;
                    sim.Step(ref s, SimInput.Left);
                    Assert.False(s.Dead, ll.Id + " hazard " + h.Id);
                    tested++;
                }
            }
            Assert.True(tested > 0, "some gong must sit next to a wall");
        }
    }
}
