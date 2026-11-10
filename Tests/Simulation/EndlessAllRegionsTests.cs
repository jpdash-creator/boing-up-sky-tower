using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
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

        private static readonly Fx[] RegionOffsets = new Fx[5];
        private static readonly SignatureKind[] Signatures = new SignatureKind[5];
        static EndlessAllRegionsTests()
        {
            // Offsets and signatures come from Content/regions (owner 2026-11-10: no hardcoded region arrays).
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            for (int r = 0; r < 5; r++) ContentLoader.LoadRegionRules(root, r + 1, out RegionOffsets[r], out Signatures[r]);
        }

        private static LevelSpec SpecFor(int region0, int seed)
        {
            return EndlessTower.MakeSpec(seed, RegionOffsets[region0], Signatures[region0], 400);
        }

        /// <summary>The kinds each region's endless tower may use (spine rows), written out here on purpose: a change to a region's set must be a deliberate change of this table.</summary>
        private static PlatformKind[] Expected(int region0)
        {
            switch (region0)
            {
                case 0: return new[] { PlatformKind.WindPetal, PlatformKind.HiveLift, PlatformKind.SnapDaisy, PlatformKind.Dandelion, PlatformKind.ThornBush, PlatformKind.Ladybug, PlatformKind.Seesaw, PlatformKind.Bounce, PlatformKind.Crumble, PlatformKind.Moving };
                case 1: return new[] { PlatformKind.SquishCap, PlatformKind.SproutLift, PlatformKind.PuffFloat, PlatformKind.FairyRing, PlatformKind.OrbitCap, PlatformKind.DizzySpores, PlatformKind.SporePop, PlatformKind.BounceCap, PlatformKind.Crumble, PlatformKind.Slippery };
                case 2: return new[] { PlatformKind.SurfBoostWave, PlatformKind.SandcastleStep, PlatformKind.ClamShellPad, PlatformKind.SinkingRaft, PlatformKind.TurtleShuttle, PlatformKind.KelpTangle, PlatformKind.PufferfishPad, PlatformKind.Moving, PlatformKind.Crumble, PlatformKind.Vanishing };
                case 3: return new[] { PlatformKind.CandyBowl, PlatformKind.FlossGlide, PlatformKind.FizzVent, PlatformKind.LicoriceBelt, PlatformKind.TaffyStretch, PlatformKind.JellyWobble, PlatformKind.PinwheelGust, PlatformKind.Sticky, PlatformKind.Crumble, PlatformKind.Slippery };
                default: return new[] { PlatformKind.BambooFlick, PlatformKind.LanternLift, PlatformKind.KoiStone, PlatformKind.SwingPlank, PlatformKind.PagodaRidge, PlatformKind.MalletSmash, PlatformKind.RollingLog, PlatformKind.Moving, PlatformKind.Vanishing };
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
                    int f = p.Row;
                    if (!first.ContainsKey(p.Kind) || f < first[p.Kind]) first[p.Kind] = f;
                    count[p.Kind] = (count.ContainsKey(p.Kind) ? count[p.Kind] : 0) + 1;
                }
                // row mates and extras are Normal or Crumble in every region
                foreach (Platform p in d.Platforms)
                    if (p.Row >= 1 && p.Kind != PlatformKind.Normal && p.Kind != PlatformKind.Crumble && Array.IndexOf(expected, p.Kind) < 0) foreign.Add("seed " + k + " row " + p.Row + " " + p.Kind);
            }
            Assert.True(foreign.Count == 0, "foreign kinds in region " + (region0 + 1) + ": " + string.Join("; ", foreign.Take(5)));

            // Per kind: it has appeared at least once by (its unlock floor + 120 floors) in the sample, and never before its unlock floor (hazard types: floor 100,
            // the first floor of level 2; every other kind: from the start).
            foreach (PlatformKind kind in expected)
            {
                int unlock = EndlessTower.KindUnlockFloor(cfg, probe, kind);
                Assert.True(unlock >= 0, kind + " is not an option");
                int limit = unlock + 120;
                output.WriteLine("region " + (region0 + 1) + " " + kind + ": unlock floor " + unlock + ", first seen floor " + (first.ContainsKey(kind) ? first[kind].ToString() : "never") + " (limit " + limit + "), " + (count.ContainsKey(kind) ? count[kind] : 0) + " spine rows in " + Seeds + " towers");
                Assert.True(first.ContainsKey(kind), kind + " never appears in region " + (region0 + 1));
                Assert.True(first[kind] >= unlock, kind + " appeared at floor " + first[kind] + " before its unlock floor " + unlock);
                Assert.True(first[kind] <= limit, kind + " first appears at floor " + first[kind] + ", later than " + limit);
            }
        }

        [Fact]
        public void HazardTypesAreLeftOutOfLevel1AndEveryOtherKindIsThereFromTheStart()
        {
            // Endless cycle (owner 2026-11-05): the kind list of a floor is the list of the campaign level of that floor (SpecialPlatforms.Options). The only
            // level whose list differs is level 1 (no hazard types), so hazard types unlock at floor 100 and nothing else phases in.
            SimConfig cfg = new SimConfig();
            for (int r = 0; r < 5; r++)
            {
                LevelSpec spec = SpecFor(r, 1);
                PlatformKind[] all = EndlessTower.EndlessOptions(spec, SignatureKinds.PlatformFor(spec.Signature));
                foreach (PlatformKind k in all)
                {
                    int unlock = EndlessTower.KindUnlockFloor(cfg, spec, k);
                    Assert.Equal(PlatformKinds.IsHazardType(k) ? EndlessTower.FloorsPerLevel : 0, unlock);
                }
                Assert.Equal(-1, EndlessTower.KindUnlockFloor(cfg, spec, PlatformKind.Normal));
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
