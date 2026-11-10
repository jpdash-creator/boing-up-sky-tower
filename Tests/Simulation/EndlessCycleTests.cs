using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Owner spec 2026-11-05 ("Endless is endless: go till floor 100000 via code"): the endless difficulty is stepped by EndlessTower.LevelForFloor.
    /// Bug List 6 item 4 (25 levels per region): floors 1-2500 are campaign levels 1..25 (100 floors each); 2501-3500 are levels 15..24 (100 floors each); level 25 is held
    /// 3501-3900; at 3901 the same 1400-floor cycle restarts at level 15, and so on without end. A floor at level N uses everything the region's campaign level N uses.
    /// (Regions 6 to 30 keep the old 15-level cycle until they are migrated: LevelForFloor(floor, 15), see EndlessLegacyCycleTests.)
    /// These tests pin: the function itself (boundaries and a full reference table to floor 100001), the tie to the SHIPPED levels (difficulty and kind list
    /// of every level of every region), the per-level content of the generated tower (special share, hazard spacing, layout), the scroll speed, the
    /// prefix-stable generation to floor 100000 (time and memory), and the bot proof at the reset boundaries and the level 25 holds. All fast.
    /// </summary>
    [Trait("Category", "EndlessCycle")]
    public class EndlessCycleTests
    {
        private readonly ITestOutputHelper output;
        public EndlessCycleTests(ITestOutputHelper output) { this.output = output; }

        private static readonly Fx[] RegionOffsets = new Fx[5];
        private static readonly SignatureKind[] Signatures = new SignatureKind[5];
        static EndlessCycleTests()
        {
            // Offsets and signatures come from Content/regions (owner 2026-11-10: no hardcoded region arrays).
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            for (int r = 0; r < 5; r++) ContentLoader.LoadRegionRules(root, r + 1, out RegionOffsets[r], out Signatures[r]);
        }

        private static Fx OffsetOf(int region0) { return RegionOffsets[region0]; }

        private static LevelSpec SpecFor(int region0, int seed)
        {
            return EndlessTower.MakeSpec(seed, OffsetOf(region0), Signatures[region0], 400);
        }

        // ------------------------------------------------------------------ (1) the function

        [Theory]
        [InlineData(1, 1)]
        [InlineData(2, 1)]
        [InlineData(99, 1)]
        [InlineData(100, 1)]
        [InlineData(101, 2)]
        [InlineData(200, 2)]
        [InlineData(201, 3)]
        [InlineData(1500, 15)]
        [InlineData(1501, 16)]
        [InlineData(2400, 24)]
        [InlineData(2401, 25)]
        [InlineData(2500, 25)]
        [InlineData(2501, 15)]
        [InlineData(2600, 15)]
        [InlineData(2601, 16)]
        [InlineData(3500, 24)]
        [InlineData(3501, 25)]
        [InlineData(3900, 25)]
        [InlineData(3901, 15)]
        [InlineData(99999, 23)]
        [InlineData(100000, 23)]
        [InlineData(100001, 24)]
        public void LevelForFloorBoundaries(int floor, int level)
        {
            Assert.Equal(level, EndlessTower.LevelForFloor(floor));
        }

        [Fact]
        public void LevelForFloorMatchesAnIndependentReferenceTableToFloor100001AndNeverFails()
        {
            // reference: build the owner's table by walking it block by block
            List<int> table = new List<int>();   // table[f] = level of floor f (index 0 = start platform, level 1)
            table.Add(1);
            for (int lvl = 1; lvl <= 25; lvl++) for (int i = 0; i < 100; i++) table.Add(lvl);
            while (table.Count <= 100001)
            {
                for (int lvl = 15; lvl <= 24; lvl++) for (int i = 0; i < 100; i++) table.Add(lvl);
                for (int i = 0; i < 400; i++) table.Add(25);
            }
            for (int f = 0; f <= 100001; f++) Assert.True(EndlessTower.LevelForFloor(f) == table[f], "floor " + f);
            // cycle shape: within a cycle never down; the only drops are 25 -> 15 at 2501 + 1400 k
            for (int f = 2; f <= 100001; f++)
            {
                int a = EndlessTower.LevelForFloor(f - 1), b = EndlessTower.LevelForFloor(f);
                if (b < a) { Assert.True(a == 25 && b == 15 && (f - 2501) % 1400 == 0, "unexpected drop at floor " + f); }
                else Assert.True(b - a <= 1, "jump at floor " + f);
            }
            // out of range input never throws and stays in 1..25
            foreach (int f in new[] { int.MinValue, -1, 0, 100002, 1000000, int.MaxValue - 1, int.MaxValue })
            {
                int l = EndlessTower.LevelForFloor(f);
                Assert.InRange(l, 1, 25);
            }
            Assert.Equal(1, EndlessTower.LevelForFloor(int.MinValue));
        }

        [Fact]
        public void CycleTableForTheReport()
        {
            output.WriteLine("floors | level");
            output.WriteLine("1-2500: level = ceil(floor / 100), 1 at 1-100 ... 25 at 2401-2500");
            int from = 2501;
            for (int f = 2501; f <= 3901; f++)
            {
                if (f == 3901 || EndlessTower.LevelForFloor(f) != EndlessTower.LevelForFloor(f - 1))
                {
                    if (f > from) output.WriteLine(from + "-" + (f - 1) + " | L" + EndlessTower.LevelForFloor(from));
                    from = f;
                }
            }
            output.WriteLine("3901-5300 repeats 2501-3900, and so on every 1400 floors up to 100000 and beyond");
        }

        // ------------------------------------------------------------------ (2) the tie to the shipped levels

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public void EveryLevelNOfTheRegionHasTheDifficultyAndKindListTheEndlessFloorsOfLevelNUse(int region)
        {
            SimConfig cfg = new SimConfig();
            string root = ContentLoader.FindRoot();
            Assert.NotNull(root);
            List<LoadedLevel> levels = ContentLoader.LoadRegions(root, region, region);
            Assert.Equal(25, levels.Count);
            int r0 = region - 1;
            LevelSpec probe = SpecFor(r0, 1);
            EndlessTower tower = new EndlessTower(cfg, probe, 10);
            foreach (LoadedLevel ll in levels)
            {
                int n = ll.Spec.LevelIndex;
                // difficulty: the endless function reproduces the d the level builder and the Sim use for the shipped level
                Fx shipped = ll.Spec.EffectiveDifficulty(cfg);
                Fx endless = LevelSpec.DifficultyForLevel(cfg, n, OffsetOf(r0));
                // Bug List 6 item 4: the endless cycle reads the smooth region 1 table (SimConfig.LevelContentDifficultyPermille) with the region offset. Region 1 ships exactly that
                // curve (except the combo level 5, whose own d is higher, see Content/config/difficulty_curve.json); regions 2 to 5 ship their own calibrated d (the same SCORE
                // per level needs another d with another signature), so they stay within 0.2 of it.
                // Bug List 8 item 3: the region 1 d values were re-solved for the new level lengths and differ from the (unchanged, endless stays as it is) table by at most 0.05.
                if (region == 1 && n != 5) Assert.True(Math.Abs(shipped.Raw - endless.Raw) <= Fx.OneRaw * 5 / 100, "region " + region + " level " + n + ": shipped d " + shipped + " but endless level d " + endless);
                else Assert.True(n == 5 || Math.Abs(shipped.Raw - endless.Raw) <= Fx.OneRaw * 20 / 100, "region " + region + " level " + n + ": shipped d " + shipped + " far from the endless level d " + endless);
                // and the tower reads the same value for the floors of that level
                Assert.Equal(endless.Raw, tower.DifficultyAt((n - 1) * 100 + 50).Raw);
                // kind list: the shipped level's options (SpecialPlatforms.Options, minus the wall-bounce exclusions of a no-wall-bounce goal) equal the endless list of level n
                HashSet<PlatformKind> expected = new HashSet<PlatformKind>(SpecialPlatforms.Options(ll.Spec, SignatureKinds.PlatformFor(ll.Spec.Signature)));
                HashSet<PlatformKind> got = new HashSet<PlatformKind>(EndlessTower.OptionsForLevel(probe, n));
                if (ll.Spec.Goal.Type == GoalType.NoWallBounce) { got.Remove(PlatformKind.SurfBoostWave); got.Remove(PlatformKind.PinwheelGust); }
                Assert.True(expected.SetEquals(got), "region " + region + " level " + n + ": shipped {" + string.Join(",", expected) + "} endless {" + string.Join(",", got) + "}");
                Assert.True(ll.Spec.Signature == Signatures[r0]);
            }
        }

        [Fact]
        public void DifficultyStepsInsideACycleNeverExceedTheStepsOfTheNormalLevelsAndResetsDrop()
        {
            SimConfig cfg = new SimConfig();
            for (int r0 = 0; r0 < 5; r0++)
            {
                EndlessTower t = new EndlessTower(cfg, SpecFor(r0, 1), 10);
                // the biggest level-to-level step of the normal levels 1..25 in this region
                double maxCampaignStep = 0;
                for (int n = 2; n <= 25; n++)
                    maxCampaignStep = Math.Max(maxCampaignStep, (LevelSpec.DifficultyForLevel(cfg, n, OffsetOf(r0)).Raw - LevelSpec.DifficultyForLevel(cfg, n - 1, OffsetOf(r0)).Raw) / (double)Fx.OneRaw);
                Fx last = t.DifficultyAt(1);
                for (int f = 2; f <= 8000; f++)
                {
                    Fx cur = t.DifficultyAt(f);
                    double step = (cur.Raw - last.Raw) / (double)Fx.OneRaw;
                    int lvl = EndlessTower.LevelForFloor(f), prevLvl = EndlessTower.LevelForFloor(f - 1);
                    if (lvl < prevLvl) Assert.True(cur < last, "d must drop at the reset, floor " + f);
                    else
                    {
                        Assert.True(step >= 0, "d fell inside a cycle at floor " + f + " region " + (r0 + 1));
                        Assert.True(step <= maxCampaignStep + 1e-9, "d cliff " + step + " at floor " + f + " region " + (r0 + 1));
                    }
                    last = cur;
                }
                // level 25 is held: constant (the difficulty of campaign level 25) across the whole hold
                Fx top = LevelSpec.DifficultyForLevel(cfg, 25, OffsetOf(r0));
                for (int f = 3501; f <= 3900; f++) Assert.Equal(top.Raw, t.DifficultyAt(f).Raw);
                for (int f = 2401; f <= 2500; f++) Assert.Equal(top.Raw, t.DifficultyAt(f).Raw);
                // the reset really lands on level 15: same d as floor 1401-1500
                Assert.Equal(t.DifficultyAt(1450).Raw, t.DifficultyAt(2501).Raw);
                Assert.Equal(t.DifficultyAt(1450).Raw, t.DifficultyAt(3901).Raw);
            }
        }

        // ------------------------------------------------------------------ (3) the content of the generated tower follows the level

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

        private sealed class Block { public double Gap, Width, Special, Hazard, N; }

        /// <summary>Per 100-floor block (block b = floors 100b+1..100b+100): spine statistics summed over many towers of a region.</summary>
        private static Block[] Measure(SimConfig cfg, int region0, int seeds, int blocks, out int minHazardGapViolations, out int[] minHazardRows)
        {
            Block[] sum = new Block[blocks];
            for (int i = 0; i < blocks; i++) sum[i] = new Block();
            int violations = 0;
            int[] minRows = new int[blocks];
            for (int i = 0; i < blocks; i++) minRows[i] = int.MaxValue;
            object gate = new object();
            Parallel.For(0, seeds, k =>
            {
                LevelData d = EndlessTower.BuildFixed(cfg, SpecFor(region0, 61000 + k * 7919 + region0), blocks * 100);
                List<Platform> spine = Spine(d);
                Block[] local = new Block[blocks];
                int[] localMin = new int[blocks];
                for (int i = 0; i < blocks; i++) { local[i] = new Block(); localMin[i] = int.MaxValue; }
                int lastHazard = -1000;
                for (int i = 1; i < spine.Count; i++)
                {
                    Platform a = spine[i - 1], b = spine[i];
                    if (b.Row <= LevelGenerator.FixedRows) continue;
                    int bi = (b.Row - 1) / 100;
                    if (bi >= blocks) break;
                    local[bi].Gap += (b.Y.Raw - a.Y.Raw) / (double)Fx.OneRaw;
                    local[bi].Width += b.Width.Raw / (double)Fx.OneRaw;
                    local[bi].N += 1;
                    if (b.Kind != PlatformKind.Normal) local[bi].Special += 1;
                    if (PlatformKinds.IsHazardType(b.Kind))
                    {
                        local[bi].Hazard += 1;
                        if (b.Row - lastHazard < localMin[bi]) localMin[bi] = b.Row - lastHazard;
                        lastHazard = b.Row;
                    }
                }
                lock (gate)
                    for (int i = 0; i < blocks; i++)
                    {
                        sum[i].Gap += local[i].Gap; sum[i].Width += local[i].Width; sum[i].N += local[i].N; sum[i].Special += local[i].Special; sum[i].Hazard += local[i].Hazard;
                        if (localMin[i] < minRows[i]) minRows[i] = localMin[i];
                    }
            });
            minHazardGapViolations = violations;
            minHazardRows = minRows;
            return sum;
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void SpecialShareHazardSpacingAndLayoutFollowTheLevelOfTheFloorWithResetsAndHolds(int region0)
        {
            SimConfig cfg = new SimConfig();
            int blocks = 40;   // floors 1..4000
            int viol;
            int[] minHaz;
            Block[] b = Measure(cfg, region0, 30, blocks, out viol, out minHaz);
            output.WriteLine("region " + (region0 + 1) + " block(floors) | level | mean gap u | mean width u | special % (campaign mean) | hazard % | min rows between hazards");
            for (int i = 0; i < blocks; i++)
            {
                int lvl = EndlessTower.LevelForFloor(i * 100 + 50);
                double want = SpecialPlatforms.SharePercent(cfg, lvl);
                output.WriteLine("  " + (i * 100 + 1) + "-" + (i * 100 + 100) + " | L" + lvl + " | " + (b[i].Gap / b[i].N).ToString("F2") + " | " + (b[i].Width / b[i].N).ToString("F2") + " | "
                    + (100 * b[i].Special / b[i].N).ToString("F1") + " (" + want + ") | " + (100 * b[i].Hazard / b[i].N).ToString("F1") + " | " + (minHaz[i] == int.MaxValue ? "-" : minHaz[i].ToString()));
            }
            Func<int, double> gap = i => b[i].Gap / b[i].N;
            Func<int, double> width = i => b[i].Width / b[i].N;
            Func<int, double> special = i => b[i].Special / b[i].N;

            for (int i = 0; i < blocks; i++)
            {
                int lvl = EndlessTower.LevelForFloor(i * 100 + 50);
                // special share: the level's own ramp (RowPercent), a little lower because the placement rules turn some draws into plain rows
                double want = SpecialPlatforms.SharePercent(cfg, lvl) / 100.0;
                Assert.True(special(i) <= want + 0.05 && special(i) >= want * 0.6 - 0.02, "block " + i + " level " + lvl + " special share " + special(i) + " vs campaign " + want);
                // hazard spacing of the level (6 rows up to level 6, 2 from level 7), never violated
                if (minHaz[i] != int.MaxValue)
                    Assert.True(minHaz[i] >= SpecialPlatforms.HazardSpacing(cfg, lvl), "hazard spacing broken in block " + i);
            }
            // level 1 has no hazard types at all
            Assert.Equal(0.0, b[0].Hazard);

            // First climb (blocks 0..24 = levels 1..25). Platform width is the layout measure that keeps tightening to level 25 (it never grows beyond sampling noise between
            // consecutive levels). The mean gap saturates at the builder's gap caps (NormalGapMax, small-platform cap), exactly as in the campaign levels:
            // it rises through level 14 and then stays within a narrow band.
            for (int i = 1; i < 25; i++)
            {
                Assert.True(width(i) <= width(i - 1) + 0.12, "width grew inside the first climb, block " + i);
                Assert.True(width(i) - width(i - 1) >= (i == 1 ? -0.6 : -0.35), "width cliff in block " + i);   // level 1 -> 2 is the biggest step of the 25-level curve (score 5 -> 20)
            }
            for (int i = 1; i < 14; i++) Assert.True(gap(i) >= gap(i - 1) - 0.08, "gap fell inside the first climb, block " + i);
            for (int i = 14; i < 25; i++) Assert.True(Math.Abs(gap(i) - gap(i - 1)) <= 0.2, "gap cliff in block " + i);
            Assert.True(gap(13) > gap(1) + 0.6 && width(24) < width(1) - 0.8, "level 25 is clearly harder than level 2");
            // the reset: floors 2501-2600 (level 15) are clearly easier than floors 2401-2500 (level 25) and play like floors 1401-1500 (level 15)
            Assert.True(width(25) > width(24) + 0.18, "width resets at 2501");
            Assert.True(Math.Abs(gap(25) - gap(14)) < 0.15 && Math.Abs(width(25) - width(14)) < 0.15, "floors 2501-2600 play like floors 1401-1500 (both level 15)");
            Assert.True(special(25) < special(24) - 0.05, "special share resets at 2501");
            // rises again 2501..3500 and holds from 3501 to 3900
            for (int i = 26; i <= 34; i++) Assert.True(width(i) <= width(i - 1) + 0.12, "width grew inside the second climb, block " + i);
            for (int i = 35; i <= 38; i++) Assert.True(Math.Abs(width(i) - width(34)) < 0.2 && Math.Abs(gap(i) - gap(34)) < 0.2, "level 25 hold, block " + i);
            Assert.True(Math.Abs(width(35) - width(24)) < 0.15, "the hold plays like the first level 25");
            // second reset at 3901
            Assert.True(width(39) > width(38) + 0.18 && special(39) < special(38) - 0.05, "second reset at 3901");
        }

        // ------------------------------------------------------------------ (4) scroll speed

        [Fact]
        public void ScrollSpeedOfAFloorIsTheSpeedOfTheCampaignLevelAndResetsWithIt()
        {
            SimConfig cfg = new SimConfig();
            for (int r0 = 0; r0 < 5; r0++)
            {
                LevelSpec spec = SpecFor(r0, 5);
                Sim endless = new EndlessTower(cfg, spec, 10).CreateSim();
                SimState es = endless.NewState();
                es.StartTick = 0;
                // campaign Sims of level n in this region (the d the shipped level gets)
                Platform[] flat = { new Platform { Id = 0, Row = 0, Kind = PlatformKind.Normal, X = Fx.FromInt(5), Y = Fx.Zero, Width = Fx.FromInt(10) } };
                foreach (int tick in new[] { 0, 600, 1800, 5000 })
                {
                    long prev = 0;
                    for (int n = 1; n <= 25; n++)
                    {
                        Sim campaign = new Sim(cfg, LevelData.FromPlatforms(flat), LevelSpec.DifficultyForLevel(cfg, n, OffsetOf(r0)), false, false);
                        SimState cs = campaign.NewState();
                        cs.StartTick = 0;
                        long want = campaign.ScrollPerTickAt(cs, tick).Raw;
                        foreach (int f in new[] { (n - 1) * 100 + 1, (n - 1) * 100 + 100 })
                        {
                            es.Row = f;
                            Assert.True(endless.ScrollPerTickAt(es, tick).Raw == want, "region " + (r0 + 1) + " tick " + tick + " floor " + f + " is level " + n + " but its speed differs from the campaign level " + n);
                        }
                        Assert.True(want >= prev, "speed of level " + n + " below level " + (n - 1));
                        prev = want;
                    }
                    // resets and holds
                    Func<int, long> at = f => { es.Row = f; return endless.ScrollPerTickAt(es, tick).Raw; };
                    Assert.True(at(2501) < at(2500), "speed eases at 2501");
                    Assert.Equal(at(2501), at(2600));
                    Assert.True(at(2601) > at(2600));
                    Assert.True(at(3500) < at(3501), "level 24 -> 25");
                    Assert.Equal(at(3501), at(3900));
                    Assert.Equal(at(2401), at(3900));
                    Assert.True(at(3901) < at(3900), "speed eases at 3901");
                    Assert.Equal(at(2501), at(3901));
                    Assert.Equal(at(1450), at(2501 + 1400 * 69));   // far cycles repeat the level 15 speed (floor 99101 is level 15)
                    // no ramp by floor inside a level and none beyond it: floor 1401 and floor 1500 are both level 15
                    Assert.Equal(at(1401), at(1500));
                }
                // the old +4 percent per 25 floors / 150 percent cap is gone: floor 937 is not faster than floor 901 (both level 10)
                es.Row = 937; long a937 = endless.ScrollPerTickAt(es, 0).Raw;
                es.Row = 901; long a801 = endless.ScrollPerTickAt(es, 0).Raw;
                Assert.Equal(a801, a937);
            }
        }

        // ------------------------------------------------------------------ (5) generation up to floor 100000

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        public void TowerIsPrefixStableAndAFiveThousandFloorTowerEqualsTheFirst5000FloorsOfA100000FloorTower(int region0)
        {
            SimConfig cfg = new SimConfig();
            LevelSpec spec = SpecFor(region0, 20261105 + region0);
            EndlessTower small = new EndlessTower(cfg, spec, 100);
            small.GenerateTo(5000);
            Assert.Equal(5000, small.TopRow);

            EndlessTower big = new EndlessTower(cfg, spec, 100);
            big.Stream = true;
            big.GenerateTo(100000);
            Assert.Equal(100000, big.TopRow);
            Assert.Equal(100, big.Milestones.Count);
            for (int m = 0; m < 5; m++) Assert.True(small.Milestones[m] == big.Milestones[m], "row " + (m + 1) * 1000 + " differs between the 5000 and the 100000 floor tower");
            Assert.Equal(small.Fingerprint, big.Milestones[4]);

            // growing in game-sized chunks gives the very same tower
            EndlessTower chunked = new EndlessTower(cfg, spec, 100);
            chunked.Stream = true;
            for (int rows = 140; rows <= 100000; rows += EndlessTower.ChunkRows) chunked.GenerateTo(rows);
            chunked.GenerateTo(100000);
            Assert.Equal(big.Fingerprint, chunked.Fingerprint);
            for (int m = 0; m < 100; m++) Assert.Equal(big.Milestones[m], chunked.Milestones[m]);
        }

        [Fact]
        public void RebasingKeepsEveryAbsoluteHeightAndTheWindowSmall()
        {
            SimConfig cfg = new SimConfig();
            LevelSpec spec = SpecFor(4, 777);
            EndlessTower plain = new EndlessTower(cfg, spec, 100);
            plain.GenerateTo(3000);
            EndlessTower s = new EndlessTower(cfg, spec, 100);
            s.Stream = true;
            s.RebaseAtYRaw = Fx.FromInt(400).Raw;   // forces many re-bases
            s.GenerateTo(3000);
            Assert.Equal(plain.Fingerprint, s.Fingerprint);
            Assert.True(s.OriginYRaw > 0);
            LevelData w = s.Data;
            Assert.True(w.Platforms.Length < plain.Data.Platforms.Length / 2, "window is small");
            for (int i = 0; i < w.Platforms.Length; i++)
            {
                Assert.Equal(i, w.Platforms[i].Id);
                if (i > 0) Assert.True(w.Platforms[i].Y >= w.Platforms[i - 1].Y);
            }
            for (int i = 0; i < w.Hazards.Length; i++) Assert.Equal(i, w.Hazards[i].Id);
            Platform top = w.Platforms[w.Platforms.Length - 1], ptop = plain.Data.Platforms[plain.Data.Platforms.Length - 1];
            Assert.Equal(ptop.Row, top.Row);
            Assert.Equal((long)ptop.Y.Raw, s.OriginYRaw + top.Y.Raw);
        }

        [Fact]
        public void The100000FloorTowerGeneratesInAFewSecondsAndLittleMemoryInEveryRegion()
        {
            SimConfig cfg = new SimConfig();
            for (int r0 = 0; r0 < 5; r0++)
            {
                GC.Collect();
                long mem0 = GC.GetTotalMemory(true);
                long alloc0 = GC.GetAllocatedBytesForCurrentThread();
                Stopwatch sw = Stopwatch.StartNew();
                EndlessTower t = new EndlessTower(cfg, SpecFor(r0, 90000 + r0), 100);
                t.Stream = true;
                t.GenerateTo(100000);
                sw.Stop();
                long alloc = GC.GetAllocatedBytesForCurrentThread() - alloc0;
                long retained = GC.GetTotalMemory(true) - mem0;
                output.WriteLine("region " + (r0 + 1) + ": 100000 floors in " + sw.ElapsedMilliseconds + " ms, retained " + retained / 1024 + " KB, allocated while generating " + alloc / (1024 * 1024) + " MB, window " + t.Data.Platforms.Length + " platforms, origin " + (t.OriginYRaw / 65536) + " u");
                Assert.Equal(100000, t.TopRow);
                Assert.True(sw.ElapsedMilliseconds < 8000, "100000 floors took " + sw.ElapsedMilliseconds + " ms");
                Assert.True(retained < 8L * 1024 * 1024, "retained " + retained);
                Assert.True(t.Data.Platforms.Length < 6000, "window of " + t.Data.Platforms.Length);   // at most the platforms of one 12000 u stretch
                GC.KeepAlive(t);
            }
        }

        [Fact]
        public void ThePlayableTowerStopsAtMaxRowsWithoutOverflow()
        {
            SimConfig cfg = new SimConfig();
            for (int r0 = 0; r0 < 5; r0++)
            {
                EndlessTower t = new EndlessTower(cfg, SpecFor(r0, 31 + r0), 100);
                t.GenerateTo(100000);   // the game never asks this, but it must not crash or wrap
                Assert.Equal(EndlessTower.MaxRows, t.TopRow);
                Platform[] p = t.Data.Platforms;
                for (int i = 1; i < p.Length; i++) Assert.True(p[i].Y >= p[i - 1].Y, "heights wrapped at platform " + i);
                double top = p[p.Length - 1].Y.Raw / (double)Fx.OneRaw;
                output.WriteLine("region " + (r0 + 1) + ": top of floor 7000 at " + top.ToString("F0") + " u (Q16.16 limit 32767)");
                Assert.True(top < 30000);
                Assert.False(t.EnsureAhead(EndlessTower.MaxRows));
            }
        }

        // ------------------------------------------------------------------ (6) bot proof at the reset boundaries and the level 15 holds

        private static SimState StateOn(Sim sim, int row, int tick)
        {
            SimState s = sim.NewState();
            int id = -1;
            for (int i = 0; i < sim.Platforms.Length && id < 0; i++)
                if (sim.Platforms[i].Row >= row && sim.Platforms[i].Kind == PlatformKind.Normal) id = i;
            Assert.True(id >= 0, "a plain platform near row " + row);
            Platform p = sim.Platforms[id];
            s.GroundId = id;
            s.BestGroundId = id;
            s.X = p.X;
            s.Y = p.Y;
            s.MaxY = p.Y;
            s.Row = p.Row;
            s.JumpStartRow = p.Row;
            s.Grounded = true;
            s.Coyote = sim.Cfg.CoyoteTicks;
            s.StartTick = 0;
            s.Tick = tick;   // 30 s into the run: every time step-up of the scroll speed is on
            s.KillY = p.Y - sim.Cfg.StartKillDistance;
            return s;
        }

        /// <summary>(first floor, floors to climb): the climbs cross 1500/1501 and 2500/2501 and cover the level 25 holds and the climb back to 15.</summary>
        public static IEnumerable<object[]> Windows()
        {
            yield return new object[] { 2380, 130 };   // L24 -> L25 at 2401, the first 100 floors of L25, reset at 2501 to L15
            yield return new object[] { 2440, 120 };   // 60 floors of L25, then the reset to L15
            yield return new object[] { 3840, 160 };   // L25 hold, reset at 3901 to L15, 100 floors of L15
            yield return new object[] { 3480, 100 };   // L24 -> L25 at 3501, the hold begins
            yield return new object[] { 2560, 130 };   // L15 -> L16 at 2601 .. L16 -> L17
        }

        [Theory]
        [MemberData(nameof(Windows))]
        public void BotClimbsTheWindowInEveryRegionOnFixedSeeds(int firstFloor, int floors)
        {
            SimConfig cfg = new SimConfig();
            List<string> fails = new List<string>();
            object gate = new object();
            Stopwatch sw = Stopwatch.StartNew();
            Parallel.For(0, 5 * 2, idx =>
            {
                int r0 = idx / 2, k = idx % 2;
                LevelSpec spec = SpecFor(r0, 7000 + k * 104729 + r0 * 13 + firstFloor);
                LevelData data = EndlessTower.BuildFixed(cfg, spec, firstFloor + floors + 80);
                Sim sim = new Sim(cfg, data, spec.Difficulty, true, false);
                SimState start = StateOn(sim, firstFloor, 1800);
                BotResult res = new LevelBot(sim, firstFloor + floors, 400000).SolveFrom(start);
                if (!res.Solved) lock (gate) fails.Add("region " + (r0 + 1) + " seed " + k + " best row " + res.BestRow + " nodes " + res.Nodes);
            });
            output.WriteLine("window " + firstFloor + "+" + floors + ": " + sw.ElapsedMilliseconds + " ms");
            Assert.True(fails.Count == 0, string.Join("; ", fails));
        }
    }

    /// <summary>Regions 6 to 30 still have 15 levels (not migrated yet): their endless tower keeps the old cycle (first climb 1500, reset to level 9, 1000-floor cycles, hold 15).</summary>
    [Trait("Category", "EndlessCycle")]
    public class EndlessLegacyCycleTests
    {
        [Fact]
        public void FifteenLevelRegionsKeepTheOldCycle()
        {
            List<int> table = new List<int> { 1 };
            for (int lvl = 1; lvl <= 15; lvl++) for (int i = 0; i < 100; i++) table.Add(lvl);
            while (table.Count <= 20001)
            {
                for (int lvl = 9; lvl <= 14; lvl++) for (int i = 0; i < 100; i++) table.Add(lvl);
                for (int i = 0; i < 400; i++) table.Add(15);
            }
            for (int f = 0; f <= 20001; f++) Assert.True(EndlessTower.LevelForFloor(f, 15) == table[f], "floor " + f);
        }

        [Fact]
        public void TheTowerAndTheSimOfAFifteenLevelRegionFollowTheLegacyCycle()
        {
            SimConfig cfg = new SimConfig();
            LevelSpec spec = EndlessTower.MakeSpec(1234, Fx.FromRatio(500, 10000), SignatureKind.BounceFlowers, 400, 15);
            EndlessTower t = new EndlessTower(cfg, spec, 10);
            Assert.Equal(Fx.One.Raw, t.DifficultyAt(1401).Raw);
            Assert.Equal(Fx.One.Raw, t.DifficultyAt(1500).Raw);
            Assert.True(t.DifficultyAt(1501) < t.DifficultyAt(1500));
            Assert.Equal(t.DifficultyAt(850).Raw, t.DifficultyAt(1501).Raw);
            Assert.Equal(15, t.CreateSim().Level.EndlessLevels);
            LevelData d = EndlessTower.BuildFixed(cfg, spec, 200);
            Assert.Equal(15, d.EndlessLevels);
        }
    }
}
