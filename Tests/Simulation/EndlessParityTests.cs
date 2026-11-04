using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>
    /// Owner question 2026-11-05: across the 5 regions, does endless have the same platforms as the levels and does it keep getting harder gradually?
    /// (1) parity: the kinds endless can make equal the kinds the shipped levels of the region are allowed to use (read from the shipped content, not from a
    /// hand-written table), every kind appears, nothing foreign leaks in; (2) ramp: scroll speed, layout difficulty, gaps, widths, special and hazard
    /// share, hazard spacing and gong density rise without a cliff from floor 0 to 800 and beyond the unlock floors; (3) the bot solves endless towers.
    /// Fast tests only (no bot beyond a short climb); the long bot proofs are in EndlessAllRegionsTests.
    /// </summary>
    [Trait("Category", "EndlessParity")]
    public class EndlessParityTests
    {
        private readonly ITestOutputHelper output;
        public EndlessParityTests(ITestOutputHelper output) { this.output = output; }

        private static readonly int[] OffsetTenThousandths = { 0, 86, 172, 259, 345 };
        private static readonly SignatureKind[] Signatures =
        {
            SignatureKind.BounceFlowers, SignatureKind.BounceCaps, SignatureKind.DriftingPlatforms, SignatureKind.StickyGum, SignatureKind.SwingingGongs
        };

        private static LevelSpec SpecFor(int region0, int seed)
        {
            return EndlessTower.MakeSpec(seed, Fx.FromRatio(OffsetTenThousandths[region0], 10000), Signatures[region0], 400);
        }

        private static double D(Fx v) { return v.Raw / (double)Fx.OneRaw; }

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

        private static List<LoadedLevel> Levels(int region)
        {
            string root = ContentLoader.FindRoot();
            Assert.NotNull(root);
            return ContentLoader.LoadRegions(root, region, region);
        }

        // ------------------------------------------------------------------ (1) platform parity with the levels

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public void EndlessKindsEqualTheKindsOfTheShippedLevelsOfTheRegion(int region)
        {
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = Levels(region);
            Assert.True(levels.Count >= 15, "region " + region + " has levels");
            int r0 = region - 1;

            // the kinds the levels are ALLOWED to draw (any level with index >= 2; level 1 is the tutorial without hazard types) and the kinds they really contain
            HashSet<PlatformKind> allowed = new HashSet<PlatformKind>();
            HashSet<PlatformKind> built = new HashSet<PlatformKind>();
            foreach (LoadedLevel ll in levels)
            {
                Assert.Equal(Signatures[r0], ll.Spec.Signature);
                foreach (PlatformKind k in SpecialPlatforms.Options(ll.Spec, SignatureKinds.PlatformFor(ll.Spec.Signature))) allowed.Add(k);
                for (int a = 0; a < 2; a++)
                    foreach (Platform p in LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(a))).Platforms)
                        if (p.Kind != PlatformKind.Normal) built.Add(p.Kind);
            }
            // the "sees no wall bounce" goal drops the Surf wave and the Pinwheel in single levels; the union over the region still holds them
            LevelSpec probe = SpecFor(r0, 1);
            HashSet<PlatformKind> endlessOptions = new HashSet<PlatformKind>(EndlessTower.EndlessOptions(probe, SignatureKinds.PlatformFor(probe.Signature)));
            Assert.True(endlessOptions.SetEquals(allowed), "region " + region + ": endless options {" + string.Join(",", endlessOptions) + "} differ from the level options {" + string.Join(",", allowed) + "}");
            // the region's 7 new kinds are all there
            foreach (PlatformKind k in RegionPlatformSets.For(Signatures[r0])) Assert.Contains(k, endlessOptions);
            // the signature platform is there (region 5's gongs are objects, not a platform)
            PlatformKind sig = SignatureKinds.PlatformFor(Signatures[r0]);
            if (sig != PlatformKind.Normal) Assert.Contains(sig, endlessOptions);

            // what the level builder really produced is inside the endless set (chunks may add Crumble/Normal-like helpers: those are named here and must be endless kinds too)
            output.WriteLine("region " + region + " levels contain: " + string.Join(",", built.OrderBy(x => (int)x)));
            output.WriteLine("region " + region + " endless options: " + string.Join(",", endlessOptions.OrderBy(x => (int)x)));
            List<PlatformKind> onlyInLevels = built.Where(k => !endlessOptions.Contains(k)).ToList();
            Assert.True(onlyInLevels.Count == 0 || onlyInLevels.All(k => k == PlatformKind.Crumble), "region " + region + ": level kinds missing from endless: " + string.Join(",", onlyInLevels));

            // endless really produces every option and nothing else (30 seeds x 300 floors; the per-kind first-floor limit is checked in EndlessAllRegionsTests)
            Dictionary<PlatformKind, int> first = new Dictionary<PlatformKind, int>();
            Dictionary<PlatformKind, int> count = new Dictionary<PlatformKind, int>();
            int totalSpine = 0;
            for (int k = 0; k < 30; k++)
            {
                LevelData d = EndlessTower.BuildFixed(cfg, SpecFor(r0, 7000 + k * 31 + r0), 300);
                foreach (Platform p in d.Platforms)
                {
                    if (p.Row < 1) continue;
                    if (p.Kind != PlatformKind.Normal) Assert.True(endlessOptions.Contains(p.Kind) || p.Kind == PlatformKind.Crumble, "region " + region + " foreign kind " + p.Kind);
                }
                foreach (Platform p in Spine(d))
                {
                    if (p.Row < 1) continue;
                    totalSpine++;
                    int f = p.Row - LevelGenerator.FixedRows;
                    if (!first.ContainsKey(p.Kind) || f < first[p.Kind]) first[p.Kind] = f;
                    count[p.Kind] = (count.ContainsKey(p.Kind) ? count[p.Kind] : 0) + 1;
                }
            }
            foreach (PlatformKind k in endlessOptions.OrderBy(x => (int)x))
            {
                bool seen = first.ContainsKey(k);
                output.WriteLine("R" + region + " " + k + ": unlock " + EndlessTower.KindUnlockFloor(cfg, probe, k) + ", first seen floor " + (seen ? first[k].ToString() : "never") + ", " + (seen ? count[k] : 0) + " of " + totalSpine + " spine rows ("
                    + (seen ? (100.0 * count[k] / totalSpine).ToString("F1") : "0") + "%)");
                Assert.True(seen, "region " + region + ": " + k + " never appears in endless");
            }
            foreach (PlatformKind k in first.Keys) Assert.True(k == PlatformKind.Normal || endlessOptions.Contains(k), "region " + region + " spine leak " + k);
        }

        // ------------------------------------------------------------------ (2) the ramp

        private static readonly int[] ReportFloors = { 0, 25, 50, 100, 200, 400, 800 };

        /// <summary>Spine statistics of the 25-floor window [f+1, f+25] over many towers of a region (floors past the fixed start).</summary>
        private sealed class Win { public double Gap, Width, Special, Hazard, N; }

        private static Win[] MeasureWindows(SimConfig cfg, int region0, int seeds, int windows)
        {
            int floors = windows * 25 + 5;
            Win[] w = new Win[windows];
            for (int i = 0; i < windows; i++) w[i] = new Win();
            object gate = new object();
            Parallel.For(0, seeds, k =>
            {
                LevelData d = EndlessTower.BuildFixed(cfg, SpecFor(region0, 31000 + k * 7919 + region0), floors + LevelGenerator.FixedRows);
                List<Platform> spine = Spine(d);
                Win[] local = new Win[windows];
                for (int i = 0; i < windows; i++) local[i] = new Win();
                for (int i = 1; i < spine.Count; i++)
                {
                    Platform a = spine[i - 1], b = spine[i];
                    int f = b.Row - LevelGenerator.FixedRows;
                    if (f < 1) continue;
                    int wi = (f - 1) / 25;
                    if (wi >= windows) break;
                    local[wi].Gap += D(b.Y - a.Y);
                    local[wi].Width += D(b.Width);
                    local[wi].N += 1;
                    if (b.Kind != PlatformKind.Normal) local[wi].Special += 1;
                    if (PlatformKinds.IsHazardType(b.Kind)) local[wi].Hazard += 1;
                }
                lock (gate)
                    for (int i = 0; i < windows; i++) { w[i].Gap += local[i].Gap; w[i].Width += local[i].Width; w[i].Special += local[i].Special; w[i].Hazard += local[i].Hazard; w[i].N += local[i].N; }
            });
            return w;
        }

        [Fact]
        public void EndlessScrollSpeedAndLayoutRampRiseSmoothlyFromFloor0To800AndBeyond()
        {
            SimConfig cfg = new SimConfig();
            // analytic ramps (identical for every region except the layout offset)
            Platform[] flat = { new Platform { Id = 0, Row = 0, Kind = PlatformKind.Normal, X = Fx.FromInt(5), Y = Fx.Zero, Width = Fx.FromInt(10) } };
            Sim sim = new Sim(cfg, flat, Fx.Half, true);
            SimState st = sim.NewState();
            st.StartTick = 0;
            double baseScroll = sim.ScrollPerTickAt(st, 0).Raw;
            Func<int, double> scroll = row => { st.Row = row; return sim.ScrollPerTickAt(st, 0).Raw / baseScroll; };
            Func<int, int> share = f => SpecialPlatforms.EndlessSharePercent(cfg, f);
            Func<int, int> spacing = f => EndlessTower.HazardSpacingAt(cfg, f);

            output.WriteLine("floor | scroll x | player speed % | special share % | hazard spacing | gong chance % | d(R1) d(R5)");
            foreach (int f in ReportFloors)
            {
                int row = f + LevelGenerator.FixedRows;
                EndlessTower t1 = new EndlessTower(cfg, SpecFor(0, 1), 5), t5 = new EndlessTower(cfg, SpecFor(4, 1), 5);
                int gong = Fx.Lerp(Fx.FromInt(cfg.GongDensityEasyPercent), Fx.FromInt(cfg.GongDensityHardPercent), t5.DifficultyAt(row)).FloorToInt();
                output.WriteLine(f + " | " + scroll(row).ToString("F3") + " | " + (cfg.RampPercentAt(row) * cfg.PlayerSpeedSharePercent / 100) + " | " + share(f) + " | " + spacing(f) + " | " + gong + " | "
                    + D(t1.DifficultyAt(row)).ToString("F3") + " " + D(t5.DifficultyAt(row)).ToString("F3"));
            }

            // scroll speed: never falls, adjacent 25-floor steps are +4 percent at most (1.04 x), and it is still rising after floor 800 (cap at 150 percent, floor ~937)
            double prev = scroll(LevelGenerator.FixedRows);
            for (int f = 25; f <= 1000; f += 25)
            {
                double cur = scroll(f + LevelGenerator.FixedRows);
                Assert.True(cur >= prev, "scroll fell at floor " + f);
                Assert.True(cur / prev <= 1.05, "scroll cliff at floor " + f);
                prev = cur;
            }
            Assert.True(scroll(800 + LevelGenerator.FixedRows) > scroll(400 + LevelGenerator.FixedRows) * 1.1, "scroll still rises between floor 400 and 800");
            Assert.True(scroll(800 + LevelGenerator.FixedRows) > scroll(25 + LevelGenerator.FixedRows) * 1.2);

            // special share and hazard spacing: monotonic, no step bigger than the per-floor slope, saturating exactly at EndlessSpecialRampFloors
            for (int f = 1; f <= 400; f++)
            {
                Assert.True(share(f) >= share(f - 1) && share(f) - share(f - 1) <= 1, "share step at floor " + f);
                Assert.True(spacing(f) <= spacing(f - 1) && spacing(f - 1) - spacing(f) <= 1, "spacing step at floor " + f);
            }
            Assert.Equal(cfg.SpecialShareLevel15Percent, share(cfg.EndlessSpecialRampFloors));
            Assert.Equal(2, spacing(cfg.EndlessSpecialRampFloors));

            // layout difficulty d: non-decreasing to 1 at EndlessDifficultyFloors, continuous
            for (int r0 = 0; r0 < 5; r0++)
            {
                EndlessTower t = new EndlessTower(cfg, SpecFor(r0, 1), 5);
                Fx last = t.DifficultyAt(0);
                for (int row = 1; row <= 450; row++)
                {
                    Fx cur = t.DifficultyAt(row);
                    Assert.True(cur >= last && D(cur - last) <= 0.01, "difficulty step at row " + row + " region " + (r0 + 1));
                    last = cur;
                }
                Assert.True(D(t.DifficultyAt(400)) >= 0.99);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void MeasuredGapWidthSpecialAndHazardSharesRiseWithoutCliffsFloor0To800(int region0)
        {
            SimConfig cfg = new SimConfig();
            Win[] w = MeasureWindows(cfg, region0, 40, 32);   // floors 1..800 in 25-floor windows
            double[] gap = w.Select(x => x.Gap / x.N).ToArray();
            double[] width = w.Select(x => x.Width / x.N).ToArray();
            double[] special = w.Select(x => x.Special / x.N).ToArray();
            double[] hazard = w.Select(x => x.Hazard / x.N).ToArray();
            output.WriteLine("region " + (region0 + 1) + " window(floors) | mean gap u | mean width u | special % | hazard %");
            for (int i = 0; i < w.Length; i++)
                if (i < 20 || i % 4 == 0) output.WriteLine("  " + (i * 25 + 1) + "-" + (i * 25 + 25) + " | " + gap[i].ToString("F2") + " | " + width[i].ToString("F2") + " | " + (special[i] * 100).ToString("F1") + " | " + (hazard[i] * 100).ToString("F1"));

            for (int i = 1; i < w.Length; i++)
            {
                string at = " (region " + (region0 + 1) + ", floors " + (i * 25 + 1) + "-" + (i * 25 + 25) + ")";
                // no cliff: adjacent 25-floor windows differ by less than 1.5x (the shares start from zero hazards, so they use an absolute step)
                Assert.True(gap[i] / gap[i - 1] <= 1.5 && gap[i - 1] / gap[i] <= 1.5, "gap cliff" + at);
                Assert.True(width[i - 1] / width[i] <= 1.5 && width[i] / width[i - 1] <= 1.5, "width cliff" + at);
                Assert.True(special[i] - special[i - 1] <= 0.14, "special share jump" + at);
                Assert.True(hazard[i] - hazard[i - 1] <= 0.08, "hazard share jump" + at);
                // no fall beyond sampling noise (40 towers x 25 rows)
                Assert.True(gap[i] >= gap[i - 1] - 0.12, "gap fell" + at);
                Assert.True(width[i] <= width[i - 1] + 0.25, "width grew" + at);
                Assert.True(special[i] >= special[i - 1] - 0.08, "special share fell" + at);
                Assert.True(hazard[i] >= hazard[i - 1] - 0.05, "hazard share fell" + at);
            }
            // overall: later is clearly harder, and the layout is still tightening after every kind is unlocked (floor 90) and the share saturates (150)
            Func<double[], int, int, double> mean = (a, lo, hi) => a.Skip(lo).Take(hi - lo).Average();
            Assert.True(mean(gap, 8, 12) > mean(gap, 0, 2) + 0.4, "gaps grow");
            Assert.True(mean(width, 8, 12) < mean(width, 0, 2) - 0.5, "platforms shrink");
            Assert.True(mean(gap, 12, 16) > mean(gap, 6, 10), "gaps still grow after floor 150");
            Assert.True(mean(width, 12, 16) < mean(width, 6, 10), "widths still shrink after floor 150");
            Assert.True(mean(special, 8, 12) > mean(special, 0, 2) + 0.15, "special share grows");
            Assert.True(mean(hazard, 8, 12) > mean(hazard, 0, 2) + 0.03, "hazard share grows");
        }

        [Fact]
        public void GongDensityOfRegion5RisesWithTheFloorWithoutACliff()
        {
            SimConfig cfg = new SimConfig();
            int[] perWindow = new int[16];
            int[] rows = new int[16];
            for (int k = 0; k < 30; k++)
            {
                LevelData d = EndlessTower.BuildFixed(cfg, SpecFor(4, 500 + k * 13), 400 + LevelGenerator.FixedRows);
                foreach (Hazard h in d.Hazards)
                {
                    // the gong sits between rows; map back to the nearest platform row below its y
                    int wi = -1;
                    for (int i = 0; i < d.Platforms.Length; i++) if (d.Platforms[i].Y >= h.Y) { wi = (d.Platforms[i].Row - LevelGenerator.FixedRows - 1) / 25; break; }
                    if (wi >= 0 && wi < 16) perWindow[wi]++;
                }
                for (int i = 0; i < 16; i++) rows[i] += 25;
            }
            double[] dens = new double[16];
            for (int i = 0; i < 16; i++) dens[i] = perWindow[i] / (double)rows[i];
            output.WriteLine("R5 gongs per row by 25-floor window: " + string.Join(" ", dens.Select(x => x.ToString("F3"))));
            Assert.True(dens.Skip(12).Average() > dens.Take(3).Average() * 1.8, "gong density rises");
            for (int i = 1; i < 16; i++) Assert.True(dens[i] - dens[i - 1] <= 0.08, "gong density jump at window " + i);
        }

        // ------------------------------------------------------------------ (3) solvability with all kinds on (short, deterministic)

        [Fact]
        public void BotClimbs60FloorsOfEndlessOnSeveralSeedsInEveryRegionWithTheNewKinds()
        {
            SimConfig cfg = new SimConfig();
            List<string> fails = new List<string>();
            object gate = new object();
            Parallel.For(0, 5 * 4, idx =>
            {
                int r = idx / 4, k = idx % 4;
                LevelSpec spec = SpecFor(r, 515151 + k * 7753 + r);
                LevelData data = EndlessTower.BuildFixed(cfg, spec, 80);
                Sim sim = new Sim(cfg, data, spec.Difficulty, true, false);
                BotResult res = new LevelBot(sim, 60, 150000).Solve();
                if (!res.Solved) lock (gate) fails.Add("region " + (r + 1) + " seed " + k + " best row " + res.BestRow + " nodes " + res.Nodes);
            });
            Assert.True(fails.Count == 0, string.Join("; ", fails));
        }
    }
}
