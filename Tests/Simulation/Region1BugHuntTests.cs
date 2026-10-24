using System;
using System.Collections.Generic;
using BoingUpSkyTower.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>
    /// Region 1 (Meadow Springs) bug hunt, 2026-10-24. Each test pins one finding:
    /// stars above the point where the goal ends the run, a NoWallBounce run that kept going after its goal was lost,
    /// vanishing platforms that disappeared without warning, plus guards for the platform geometry and the gong wave.
    /// </summary>
    public class Region1BugHuntTests
    {
        readonly ITestOutputHelper output;
        public Region1BugHuntTests(ITestOutputHelper output) { this.output = output; }

        static double D(Fx f) { return f.Raw / 65536.0; }

        static List<LoadedLevel> Levels(int first, int last)
        {
            string root = ContentLoader.FindRoot();
            return root == null ? new List<LoadedLevel>() : ContentLoader.LoadRegions(root, first, last);
        }

        static double RowY(LevelData d, int row)
        {
            for (int i = 0; i < d.Platforms.Length; i++) if (d.Platforms[i].Row == row) return D(d.Platforms[i].Y);
            throw new InvalidOperationException("no platform on row " + row);
        }

        /// <summary>The run ends when the goal is met (RunSession), so stars and gems above that point could never be taken: lvl_001_05, _08 and _11 (and the same levels of regions 2-5) could not give 3 stars.</summary>
        [Fact]
        [Trait("Category", "CriticalUX")]
        public void StarsAndGemsSitBelowThePointWhereTheGoalEndsTheRun()
        {
            SimConfig cfg = new SimConfig();
            var bad = new List<string>();
            foreach (LoadedLevel ll in Levels(1, 5))
            {
                LevelData d = LevelBuilder.Build(cfg, ll.Spec);
                double limit = double.MaxValue;
                if (ll.Spec.Goal.Type == GoalType.ComboChain) limit = RowY(d, d.LadderStartRow);
                else if (ll.Spec.Goal.Type == GoalType.FindHiddenGem) limit = RowY(d, d.HiddenGemRow);
                int stars = 0;
                foreach (Pickup p in d.Pickups)
                {
                    if (p.Kind == PickupKind.Star) stars++;
                    if ((p.Kind == PickupKind.Star || p.Kind == PickupKind.Gem) && D(p.Y) >= limit)
                        bad.Add(ll.Id + " " + ll.Spec.Goal.Type + " " + p.Kind + " at y " + D(p.Y).ToString("F1") + " >= " + limit.ToString("F1"));
                }
                if (stars != 3) bad.Add(ll.Id + " has " + stars + " stars");
            }
            foreach (string b in bad) output.WriteLine(b);
            Assert.Empty(bad);
        }

        [Fact]
        public void WallBounceEndsANoWallBounceRunInsteadOfLettingItClimbToAGoalThatNoLongerCounts()
        {
            SimConfig cfg = new SimConfig();
            LoadedLevel l7 = null;
            foreach (LoadedLevel ll in Levels(1, 1)) if (ll.Spec.Goal.Type == GoalType.NoWallBounce) l7 = ll;
            Assert.NotNull(l7);
            LevelData d = LevelBuilder.Build(cfg, l7.Spec);
            Sim sim = new Sim(cfg, d, l7.Spec);
            SimState s = sim.NewState();
            Assert.False(GoalEvaluator.IsFailed(l7.Spec, s));
            // Hop to the right (jump whenever grounded) until a wall is hit in the air.
            for (int i = 0; i < 400 && s.WallBounces == 0 && !s.Dead; i++) sim.Step(ref s, SimInput.Right | SimInput.Jump);
            Assert.True(s.WallBounces > 0, "the test run must hit the wall");
            Assert.False(s.Dead);
            Assert.True(GoalEvaluator.IsFailed(l7.Spec, s));
            Assert.False(GoalEvaluator.IsComplete(l7.Spec, s));
        }

        [Fact]
        public void OnlyTheNoWallBounceGoalCanBeLostWhileAlive()
        {
            foreach (LoadedLevel ll in Levels(1, 5))
            {
                SimState s = new SimState();
                s.WallBounces = 3;
                bool expect = ll.Spec.Goal.Type == GoalType.NoWallBounce;
                Assert.True(GoalEvaluator.IsFailed(ll.Spec, s) == expect, ll.Id + " " + ll.Spec.Goal.Type);
            }
        }

        [Fact]
        [Trait("Category", "CriticalUX")]
        public void VanishingPlatformsWarnBeforeTheyDisappear()
        {
            SimConfig cfg = new SimConfig();
            Platform floor = new Platform { Id = 0, Row = 0, Kind = PlatformKind.Normal, X = Fx.FromInt(5), Y = Fx.Zero, Width = Fx.FromInt(10) };
            Platform v = new Platform { Id = 1, Row = 1, Kind = PlatformKind.Vanishing, X = Fx.FromInt(5), Y = Fx.FromInt(3), Width = Fx.FromInt(2), Phase = 7 };
            Sim sim = new Sim(cfg, LevelData.FromPlatforms(new Platform[] { floor, v }), Fx.Zero, false, false);
            int cycle = cfg.VanishVisibleTicks + cfg.VanishHiddenTicks;
            int warned = 0;
            int[] noCrumble = new int[2] { -1, -1 };
            for (int t = 0; t < cycle * 3; t++)
            {
                bool solid = sim.IsSolid(1, t, noCrumble);
                bool warn = sim.VanishWarning(1, t);
                Assert.False(warn && !solid, "no warning while hidden, tick " + t);
                if (warn)
                {
                    warned++;
                    Assert.False(sim.IsSolid(1, t + cfg.VanishWarnTicks, noCrumble), "the warning must end in the vanish, tick " + t);
                }
            }
            Assert.Equal(3 * cfg.VanishWarnTicks, warned);
            Assert.False(sim.VanishWarning(0, 10), "normal platforms never warn");
        }

        /// <summary>Gongs only hit, never kill (Sim.HitHazards): the wave level must also be winnable without taking a single hit (CUX-7, no unavoidable damage).</summary>
        [Fact]
        [Trait("Category", "CriticalUX")]
        public void Region1WaveLevelIsWinnableWithoutAGongHit()
        {
            SimConfig cfg = new SimConfig();
            int checkedLevels = 0;
            foreach (LoadedLevel ll in Levels(1, 1))
            {
                if (ll.Spec.Goal.Type != GoalType.SurviveWave) continue;
                LevelData d = LevelBuilder.Build(cfg, ll.Spec);
                Assert.True(d.Hazards.Length > 0);
                LevelBot bot = new LevelBot(new Sim(cfg, d, ll.Spec), ll.Spec, d, 200000);
                bot.MaxHits = 0;
                BotResult r = bot.Solve();
                output.WriteLine(ll.Id + " solved without hits: " + r.Solved + ", nodes " + r.Nodes);
                Assert.True(r.Solved);
                Assert.Equal(0, r.Hits);
                checkedLevels++;
            }
            Assert.Equal(1, checkedLevels);
        }

        /// <summary>PlatformSeparationTests ignores the swing of moving platforms: a swing that sweeps through a neighbour on the same row looks like a stack and blocks the jump.</summary>
        [Fact]
        public void MovingPlatformSwingsKeepClearOfNeighboursOnTheSameRow()
        {
            SimConfig cfg = new SimConfig();
            var bad = new List<string>();
            foreach (LoadedLevel ll in Levels(1, 1)) bad.AddRange(CloseSwingPairs(ll.Id, LevelBuilder.Build(cfg, ll.Spec).Platforms));
            foreach (string b in bad) output.WriteLine(b);
            Assert.Empty(bad);
        }

        /// <summary>Endless towers of region 1 (60 seeds): no two platforms at the same height touch (swing included), and the stack rule holds.</summary>
        [Fact]
        public void EndlessRegion1TowersKeepPlatformsApartAndUnstacked()
        {
            SimConfig cfg = new SimConfig();
            var bad = new List<string>();
            for (int seed = 1; seed <= 60; seed++)
            {
                LevelSpec spec = EndlessTower.MakeSpec(seed * 7727, Fx.Zero, SignatureKind.BounceFlowers, 400);
                Platform[] p = EndlessTower.BuildFixed(cfg, spec, 300).Platforms;
                string v = StackRuleTests.FindViolation(p);
                if (v != null) bad.Add("seed " + seed * 7727 + ": " + v);
                bad.AddRange(CloseSwingPairs("seed " + seed * 7727, p));
            }
            foreach (string b in bad) output.WriteLine(b);
            Assert.Empty(bad);
        }

        static List<string> CloseSwingPairs(string id, Platform[] p)
        {
            var bad = new List<string>();
            for (int i = 0; i < p.Length; i++)
            {
                if (p[i].Width.Raw >= 9 * 65536) continue;
                double ai = p[i].Kind == PlatformKind.Moving ? D(p[i].Amplitude) : 0;
                for (int j = i + 1; j < p.Length; j++)
                {
                    if (p[j].Width.Raw >= 9 * 65536) continue;
                    if (D(p[j].Y) - D(p[i].Y) > 0.9) break;
                    double aj = p[j].Kind == PlatformKind.Moving ? D(p[j].Amplitude) : 0;
                    double gap = Math.Max(D(p[j].X) - D(p[j].Width) / 2 - aj - (D(p[i].X) + D(p[i].Width) / 2 + ai),
                                          D(p[i].X) - D(p[i].Width) / 2 - ai - (D(p[j].X) + D(p[j].Width) / 2 + aj));
                    if (gap < 0.05) bad.Add(id + " #" + p[i].Id + "/#" + p[j].Id + " swing gap " + gap.ToString("F2"));
                }
            }
            return bad;
        }
    }
}
