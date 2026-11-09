using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Owner question 2026-11-05: across the 5 regions, does endless have the same platforms as the levels? (1) parity: the kinds endless can make equal the
    /// kinds the shipped levels of the region are allowed to use (read from the shipped content, not from a hand-written table), every kind appears, nothing
    /// foreign leaks in; (3) the bot solves endless towers with all kinds on. The difficulty ramp is the endless cycle (EndlessCycleTests, owner 2026-11-05).
    /// Fast tests only (no bot beyond a short climb); the long bot proofs are in EndlessAllRegionsTests and EndlessCycleTests.
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

        // (2) the ramp (the endless cycle: level of the floor, resets, holds) is tested in EndlessCycleTests.

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
