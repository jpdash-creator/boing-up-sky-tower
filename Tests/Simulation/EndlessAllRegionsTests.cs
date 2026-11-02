using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>
    /// Owner requirement 2026-11-02: endless mode in ALL five regions uses all of that region's platforms (its new set, its signature platform and the
    /// generic kinds it keeps), the new kinds phase in one by one, and the difficulty rises gradually with the floor (no step). The bot-proof side
    /// of the requirement lives in EndlessTests (40 and 150 floors in every region), Region2/3PlatformTests (60 floors with all new platforms) and
    /// the tail tests below (floors 250 to 300).
    /// </summary>
    [Trait("Category", "EndlessAllRegions")]
    public class EndlessAllRegionsTests
    {
        private readonly ITestOutputHelper output;
        public EndlessAllRegionsTests(ITestOutputHelper output) { this.output = output; }

        private static readonly int[] OffsetTenThousandths = { 0, 86, 172, 259, 345 };
        private static readonly SignatureKind[] Signatures =
        {
            SignatureKind.BounceFlowers, SignatureKind.BounceCaps, SignatureKind.DriftingPlatforms, SignatureKind.StickyGum, SignatureKind.SwingingGongs
        };

        private static LevelSpec SpecFor(int region0, int seed)
        {
            return EndlessTower.MakeSpec(seed, Fx.FromRatio(OffsetTenThousandths[region0], 10000), Signatures[region0], 400);
        }

        /// <summary>The kinds each region's endless tower may use (spine rows), written out here on purpose: a change to a region's set must be a deliberate change of this table.</summary>
        private static PlatformKind[] Expected(int region0)
        {
            switch (region0)
            {
                case 0: return new[] { PlatformKind.WindPetal, PlatformKind.HiveLift, PlatformKind.SnapDaisy, PlatformKind.Dandelion, PlatformKind.ThornBush, PlatformKind.Ladybug, PlatformKind.Seesaw, PlatformKind.Bounce };
                case 1: return new[] { PlatformKind.SquishCap, PlatformKind.SproutLift, PlatformKind.PuffFloat, PlatformKind.FairyRing, PlatformKind.OrbitCap, PlatformKind.DizzySpores, PlatformKind.SporePop, PlatformKind.BounceCap, PlatformKind.Crumble, PlatformKind.Slippery };
                case 2: return new[] { PlatformKind.SurfBoostWave, PlatformKind.SandcastleStep, PlatformKind.ClamShellPad, PlatformKind.SinkingRaft, PlatformKind.TurtleShuttle, PlatformKind.KelpTangle, PlatformKind.PufferfishPad, PlatformKind.Moving, PlatformKind.Crumble, PlatformKind.Vanishing };
                case 3: return new[] { PlatformKind.Sticky, PlatformKind.Moving, PlatformKind.Crumble, PlatformKind.Vanishing, PlatformKind.Slippery, PlatformKind.Bounce };
                default: return new[] { PlatformKind.Moving, PlatformKind.Crumble, PlatformKind.Vanishing, PlatformKind.Slippery, PlatformKind.Sticky, PlatformKind.Bounce };
            }
        }

        private const int Seeds = 40;
        private const int Floors = 330;

        private static LevelData Tower(SimConfig cfg, int region0, int k)
        {
            return EndlessTower.BuildFixed(cfg, SpecFor(region0, 1000 + k * 7919 + region0 * 131), Floors);
        }

        private static List<Platform> Spine(LevelData d)
        {
            List<Platform> spine = new List<Platform>();
            int last = 0;
            foreach (Platform p in d.Platforms)
            {
                if (p.Row == last) continue;
                last = p.Row;
                spine.Add(p);
            }
            return spine;
        }

        // ------------------------------------------------------------------ (1) every kind of the region appears, none from outside

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void EveryKindOfTheRegionAppearsAfterItsUnlockFloorAndNoForeignKindEver(int region0)
        {
            SimConfig cfg = new SimConfig();
            PlatformKind[] expected = Expected(region0);
            LevelSpec probe = SpecFor(region0, 1);
            // the option list of the endless tower is exactly the table
            Assert.True(new HashSet<PlatformKind>(EndlessTower.EndlessOptions(probe, SignatureKinds.PlatformFor(probe.Signature))).SetEquals(expected), "options differ from the table of region " + (region0 + 1));

            Dictionary<PlatformKind, int> first = new Dictionary<PlatformKind, int>();
            Dictionary<PlatformKind, int> count = new Dictionary<PlatformKind, int>();
            List<string> foreign = new List<string>();
            for (int k = 0; k < Seeds; k++)
            {
                LevelData d = Tower(cfg, region0, k);
                foreach (Platform p in Spine(d))
                {
                    if (p.Row < 1 || p.Kind == PlatformKind.Normal) continue;
                    if (Array.IndexOf(expected, p.Kind) < 0) foreign.Add("seed " + k + " row " + p.Row + " spine " + p.Kind);
                    int f = p.Row - LevelGenerator.FixedRows;
                    if (!first.ContainsKey(p.Kind) || f < first[p.Kind]) first[p.Kind] = f;
                    count[p.Kind] = (count.ContainsKey(p.Kind) ? count[p.Kind] : 0) + 1;
                }
                // row mates and extras are Normal or Crumble in every region
                foreach (Platform p in d.Platforms)
                    if (p.Row >= 1 && p.Kind != PlatformKind.Normal && p.Kind != PlatformKind.Crumble && Array.IndexOf(expected, p.Kind) < 0) foreign.Add("seed " + k + " row " + p.Row + " " + p.Kind);
            }
            Assert.True(foreign.Count == 0, "foreign kinds in region " + (region0 + 1) + ": " + string.Join("; ", foreign.Take(5)));

            // X per kind: it has appeared at least once by (its unlock floor + 60 floors) in the sample, and never before its unlock floor.
            // 60 floors is about 6 to 17 expected draws per tower at the endless share (15 to 55 percent of the rows, a pool of 6 to 11 kinds), so with 40
            // towers a kind that is really offered cannot be missed; the start-in-the-pool kinds (signature, Crumble) have unlock floor 0.
            foreach (PlatformKind kind in expected)
            {
                int unlock = EndlessTower.KindUnlockFloor(cfg, probe, kind);
                Assert.True(unlock >= 0, kind + " is not an option");
                int limit = unlock + 60;
                output.WriteLine("region " + (region0 + 1) + " " + kind + ": unlock floor " + unlock + ", first seen floor " + (first.ContainsKey(kind) ? first[kind].ToString() : "never") + " (limit " + limit + "), " + (count.ContainsKey(kind) ? count[kind] : 0) + " spine rows in " + Seeds + " towers");
                Assert.True(first.ContainsKey(kind), kind + " never appears in region " + (region0 + 1));
                Assert.True(first[kind] >= unlock, kind + " appeared at floor " + first[kind] + " before its unlock floor " + unlock);
                Assert.True(first[kind] <= limit, kind + " first appears at floor " + first[kind] + ", later than " + limit);
            }
        }

        [Fact]
        public void TheNewKindsPhaseInOneByOneEveryTenFloorsEasyOnesFirst()
        {
            SimConfig cfg = new SimConfig();
            for (int r = 0; r < 5; r++)
            {
                LevelSpec spec = SpecFor(r, 1);
                PlatformKind[] options = EndlessTower.EndlessOptions(spec, SignatureKinds.PlatformFor(spec.Signature));
                int[] floor = EndlessTower.UnlockFloors(cfg, spec, options);
                List<int> distinct = floor.Distinct().OrderBy(x => x).ToList();
                Assert.Equal(0, distinct[0]);
                for (int i = 1; i < distinct.Count; i++) Assert.Equal(10, distinct[i] - distinct[i - 1]);   // one more every 10 floors, no gaps, no batches
                Assert.True(distinct.Last() <= 90, "the last kind arrives by floor 90");
                // within a region the hazard types arrive after every plain kind and launcher
                int lastPlain = -1, firstHazard = int.MaxValue;
                for (int i = 0; i < options.Length; i++)
                {
                    if (PlatformKinds.IsHazardType(options[i])) firstHazard = Math.Min(firstHazard, floor[i]);
                    else lastPlain = Math.Max(lastPlain, floor[i]);
                }
                if (firstHazard != int.MaxValue) Assert.True(firstHazard >= lastPlain, "region " + (r + 1) + ": a hazard type arrives before a plain kind");
            }
        }

        // ------------------------------------------------------------------ (2) the difficulty rises smoothly

        private sealed class WindowStats
        {
            public double GapSum, ReachSum, Rows, Special, Hazard;
            public double Gap { get { return GapSum / Rows; } }
            public double Reach { get { return ReachSum / Rows; } }
            public double SpecialShare { get { return Special / Rows; } }
            public double HazardShare { get { return Hazard / Rows; } }
        }

        /// <summary>Per window of 10 floors (window w covers floors 10w+1 to 10w+10 past the fixed start), averaged over all towers of a region.</summary>
        private static WindowStats[] Measure(SimConfig cfg, int region0, int seeds, int windows)
        {
            WindowStats[] w = new WindowStats[windows];
            for (int i = 0; i < windows; i++) w[i] = new WindowStats();
            object gate = new object();
            Parallel.For(0, seeds, k =>
            {
                LevelData d = Tower(cfg, region0, k);
                List<Platform> spine = Spine(d);
                WindowStats[] local = new WindowStats[windows];
                for (int i = 0; i < windows; i++) local[i] = new WindowStats();
                for (int i = 1; i < spine.Count; i++)
                {
                    Platform a = spine[i - 1], b = spine[i];
                    int f = b.Row - LevelGenerator.FixedRows;
                    if (f < 1) continue;
                    int wi = (f - 1) / 10;
                    if (wi >= windows) break;
                    Fx gap = b.Y - a.Y;
                    Fx clear = Fx.Abs(b.X - a.X) - (a.Width + b.Width) / 2;
                    if (clear < Fx.Zero) clear = Fx.Zero;
                    Fx reach = cfg.LayoutMaxReachDx(gap, Fx.One);
                    double ratio = reach.Raw > 0 ? clear.Raw / (double)reach.Raw : 2.0;
                    local[wi].GapSum += gap.Raw / (double)cfg.NormalGapMax.Raw;
                    local[wi].ReachSum += ratio;
                    local[wi].Rows += 1;
                    if (b.Kind != PlatformKind.Normal) local[wi].Special += 1;
                    if (PlatformKinds.IsHazardType(b.Kind)) local[wi].Hazard += 1;
                }
                lock (gate)
                {
                    for (int i = 0; i < windows; i++)
                    {
                        w[i].GapSum += local[i].GapSum; w[i].ReachSum += local[i].ReachSum; w[i].Rows += local[i].Rows;
                        w[i].Special += local[i].Special; w[i].Hazard += local[i].Hazard;
                    }
                }
            });
            return w;
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void DifficultyMetricsNeverDropAndNeverJumpFrom10FloorWindowToWindow(int region0)
        {
            SimConfig cfg = new SimConfig();
            WindowStats[] w = Measure(cfg, region0, 150, 30);   // floors 1 to 300
            for (int i = 0; i < w.Length; i++)
                output.WriteLine("region " + (region0 + 1) + " floors " + (i * 10 + 1) + "-" + (i * 10 + 10) + ": gap/max " + w[i].Gap.ToString("F3") + "  reach need " + w[i].Reach.ToString("F3")
                    + "  special " + (w[i].SpecialShare * 100).ToString("F1") + "%  hazard " + (w[i].HazardShare * 100).ToString("F1") + "%");

            // smooth the sampling noise with a 3-window moving mean (30 floors), then require: non-decreasing within a tolerance, and no jump bigger than a small step
            double[] gap = Smooth(w.Select(x => x.Gap).ToArray());
            double[] reach = Smooth(w.Select(x => x.Reach).ToArray());
            double[] special = Smooth(w.Select(x => x.SpecialShare).ToArray());
            double[] hazard = Smooth(w.Select(x => x.HazardShare).ToArray());
            for (int i = 1; i < gap.Length; i++)
            {
                string at = " (region " + (region0 + 1) + ", window " + i + ")";
                Assert.True(gap[i] >= gap[i - 1] - 0.012, "mean gap fell" + at);
                Assert.True(reach[i] >= reach[i - 1] - 0.03, "needed reach fell" + at);
                Assert.True(special[i] >= special[i - 1] - 0.025, "special share fell" + at);
                Assert.True(hazard[i] >= hazard[i - 1] - 0.012, "hazard share fell" + at);
                Assert.True(gap[i] - gap[i - 1] <= 0.03, "mean gap jumped" + at);
                Assert.True(reach[i] - reach[i - 1] <= 0.06, "needed reach jumped" + at);
                Assert.True(special[i] - special[i - 1] <= 0.045, "special share jumped" + at);
                Assert.True(hazard[i] - hazard[i - 1] <= 0.025, "hazard share jumped" + at);
            }
            // and the end of the ramp is clearly harder than the start
            Assert.True(gap[gap.Length - 1] > gap[0] + 0.05, "gaps grow over 300 floors");
            Assert.True(special[special.Length - 1] > special[0] + 0.10, "special share grows");
        }

        private static double[] Smooth(double[] v)
        {
            double[] s = new double[v.Length - 2];
            for (int i = 0; i < s.Length; i++) s[i] = (v[i] + v[i + 1] + v[i + 2]) / 3;
            return s;
        }

        [Fact]
        public void HazardSpacingAndScrollSpeedRampInSmallSteps()
        {
            SimConfig cfg = new SimConfig();
            int prev = EndlessTower.HazardSpacingAt(cfg, 0);
            Assert.Equal(cfg.HazardEarlyRowsPerHazard, prev);
            for (int f = 1; f <= 400; f++)
            {
                int s = EndlessTower.HazardSpacingAt(cfg, f);
                Assert.True(s <= prev && prev - s <= 1, "spacing steps by at most one row at floor " + f);
                prev = s;
            }
            Assert.Equal(2, prev);
            // scroll speed (state with the run started, row r): non-decreasing, steps of at most the doc 08 +4 percent
            Platform[] flat = { new Platform { Id = 0, Row = 0, Kind = PlatformKind.Normal, X = Fx.FromInt(5), Y = Fx.Zero, Width = Fx.FromInt(10) } };
            Sim sim = new Sim(cfg, flat, Fx.Half, true);
            SimState st = sim.NewState();
            st.StartTick = 0;
            long last = 0;
            for (int row = 0; row <= 400; row++)
            {
                st.Row = row;
                long cur = sim.ScrollPerTickAt(st, 0).Raw;
                Assert.True(cur >= last);
                if (last > 0) Assert.True(cur * 100 <= last * 105, "scroll speed jumped by more than 5 percent at row " + row);
                last = cur;
            }
        }

        // ------------------------------------------------------------------ bot proof of the tail

        [Fact]
        public void BotClimbsTheEndlessTailAtFloors250To300InEveryRegion()
        {
            SimConfig cfg = new SimConfig();
            string[] fails = new string[5];
            Parallel.For(0, 5, r =>
            {
                // the bot starts at the bottom, so the proof covers floors 0..300 in one go (the tail is the hardest stretch: difficulty 0.75+ and every kind unlocked)
                LevelSpec spec = SpecFor(r, 4242 + r);
                LevelData data = EndlessTower.BuildFixed(cfg, spec, 330);
                Sim sim = new Sim(cfg, data, spec.Difficulty, true, false);
                BotResult res = new LevelBot(sim, 300, 1500000).Solve();
                if (!res.Solved) fails[r] = "region " + (r + 1) + " best row " + res.BestRow + " nodes " + res.Nodes;
            });
            Assert.True(fails.All(f => f == null), string.Join("; ", fails.Where(f => f != null)));
        }

        [Fact]
        public void BotClimbs150FloorsOnManySeedsInEveryRegionWithAllKinds()
        {
            SimConfig cfg = new SimConfig();
            List<string> fails = new List<string>();
            object gate = new object();
            Parallel.For(0, 5 * 12, idx =>
            {
                int r = idx / 12, k = idx % 12;
                LevelSpec spec = SpecFor(r, 90000 + k * 104729 + r);
                LevelData data = EndlessTower.BuildFixed(cfg, spec, 170);
                Sim sim = new Sim(cfg, data, spec.Difficulty, true, false);
                BotResult res = new LevelBot(sim, 150, 400000).Solve();
                if (!res.Solved) lock (gate) fails.Add("region " + (r + 1) + " seed " + k + " best row " + res.BestRow + " nodes " + res.Nodes);
            });
            foreach (string f in fails) output.WriteLine(f);
            Assert.True(fails.Count == 0, fails.Count + " unsolved towers, first: " + (fails.Count > 0 ? fails[0] : ""));
        }
    }
}
