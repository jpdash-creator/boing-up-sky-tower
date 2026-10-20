using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace SkyHopper.Simulation.Tests
{
    /// <summary>Endless mode (T13): deterministic tower, reachable gaps, prefix stability, bot climbs in every region.</summary>
    public class EndlessTests
    {
        private readonly ITestOutputHelper output;
        public EndlessTests(ITestOutputHelper output) { this.output = output; }

        // Regions 1-5: difficultyOffset from Content/regions (in 1/10000) and the signature (bounce flowers, bounce caps, drifting, sticky gum, gongs).
        private static readonly int[] OffsetTenThousandths = { 0, 86, 172, 259, 345 };
        private static readonly SignatureKind[] Signatures =
        {
            SignatureKind.BounceFlowers, SignatureKind.BounceCaps, SignatureKind.DriftingPlatforms, SignatureKind.StickyGum, SignatureKind.SwingingGongs
        };

        private static LevelSpec SpecFor(int regionIndex0, int seed)
        {
            return EndlessTower.MakeSpec(seed, Fx.FromRatio(OffsetTenThousandths[regionIndex0], 10000), Signatures[regionIndex0], 400);
        }

        [Fact]
        public void SameSeedGivesIdenticalTower_DifferentSeedDiffers()
        {
            SimConfig cfg = new SimConfig();
            for (int r = 0; r < 5; r++)
            {
                long a = EndlessTower.BuildFixed(cfg, SpecFor(r, 777), 120).ComputeHash();
                long b = EndlessTower.BuildFixed(cfg, SpecFor(r, 777), 120).ComputeHash();
                long c = EndlessTower.BuildFixed(cfg, SpecFor(r, 778), 120).ComputeHash();
                Assert.Equal(a, b);
                Assert.NotEqual(a, c);
            }
        }

        [Fact]
        public void TowerIsPrefixStable_GrowingNeverChangesExistingRows()
        {
            SimConfig cfg = new SimConfig();
            for (int r = 0; r < 5; r++)
            {
                LevelData small = EndlessTower.BuildFixed(cfg, SpecFor(r, 31337), 70);
                EndlessTower grown = new EndlessTower(cfg, SpecFor(r, 31337), 70);
                grown.GenerateTo(250);
                LevelData big = grown.Data;
                Assert.True(big.Platforms.Length > small.Platforms.Length);
                for (int i = 0; i < small.Platforms.Length; i++)
                {
                    Platform p = small.Platforms[i], q = big.Platforms[i];
                    Assert.True(p.Id == q.Id && p.Row == q.Row && p.Kind == q.Kind && p.X.Raw == q.X.Raw && p.Y.Raw == q.Y.Raw
                        && p.Width.Raw == q.Width.Raw && p.Amplitude.Raw == q.Amplitude.Raw && p.Period == q.Period && p.Phase == q.Phase,
                        "platform " + i + " changed in region " + (r + 1));
                }
                for (int i = 0; i < small.Hazards.Length; i++)
                {
                    Assert.Equal(small.Hazards[i].X.Raw, big.Hazards[i].X.Raw);
                    Assert.Equal(small.Hazards[i].Y.Raw, big.Hazards[i].Y.Raw);
                }
            }
        }

        [Fact]
        public void GapsStayWithinTheStandingJump_AndPlatformsAreSorted()
        {
            SimConfig cfg = new SimConfig();
            Fx v0 = cfg.JumpBase;
            Fx standingPeak = v0 * v0 / (cfg.Gravity * 2);   // 4.51 u
            Assert.True((standingPeak.Raw / 65536.0) > 4.5 && (standingPeak.Raw / 65536.0) < 4.52, "standing peak " + (standingPeak.Raw / 65536.0));
            Assert.True(cfg.NormalGapMax < standingPeak);

            for (int r = 0; r < 5; r++)
            {
                // 400 rows reach difficulty 1; go to 600 so the plateau is covered too.
                LevelData d = EndlessTower.BuildFixed(cfg, SpecFor(r, 4242 + r), 600);
                Fx[] rowMaxY = new Fx[d.Rows + 1];
                bool[] seen = new bool[d.Rows + 1];
                for (int i = 0; i < d.Platforms.Length; i++)
                {
                    Platform p = d.Platforms[i];
                    Assert.Equal(i, p.Id);
                    if (i > 0) Assert.True(p.Y >= d.Platforms[i - 1].Y, "sorted by Y");
                    if (!seen[p.Row] || p.Y > rowMaxY[p.Row]) rowMaxY[p.Row] = p.Y;
                    seen[p.Row] = true;
                }
                for (int row = 1; row <= d.Rows; row++)
                {
                    Assert.True(seen[row], "row " + row + " exists");
                    Fx gap = rowMaxY[row] - rowMaxY[row - 1];
                    Assert.True(gap > Fx.Zero, "rows rise");
                    Assert.True(gap <= cfg.NormalGapMax, "gap " + (gap.Raw / 65536.0) + " at row " + row + " region " + (r + 1));
                    Assert.True(gap < standingPeak);
                }
                for (int i = 1; i < d.Hazards.Length; i++) Assert.True(d.Hazards[i].Y >= d.Hazards[i - 1].Y);
                if (r == 4) Assert.True(d.Hazards.Length > 10, "region 5 has gongs");
                else Assert.Empty(d.Hazards);
            }
        }

        [Fact]
        public void DifficultyGrowsWithTheFloorFromTheRegionOffset()
        {
            SimConfig cfg = new SimConfig();
            EndlessTower t = new EndlessTower(cfg, SpecFor(4, 1), 10);
            Assert.Equal(Fx.FromRatio(345, 10000).Raw, t.DifficultyAt(0).Raw);
            Assert.True(t.DifficultyAt(100) > t.DifficultyAt(10));
            Assert.True(t.DifficultyAt(200) > t.DifficultyAt(100));
            Assert.Equal(Fx.One.Raw, t.DifficultyAt(400).Raw);
            Assert.Equal(Fx.One.Raw, t.DifficultyAt(5000).Raw);
        }

        [Fact]
        public void BotClimbsAtLeast40FloorsInEveryRegion()
        {
            SimConfig cfg = new SimConfig();
            int[] seeds = { 11, 2026, 90210 };
            for (int r = 0; r < 5; r++)
            {
                foreach (int seed in seeds)
                {
                    LevelSpec spec = SpecFor(r, seed);
                    LevelData data = EndlessTower.BuildFixed(cfg, spec, 70);
                    Sim sim = new Sim(cfg, data, spec.Difficulty, true, false);
                    BotResult res = new LevelBot(sim, 40, 40000).Solve();
                    output.WriteLine("region " + (r + 1) + " seed " + seed + " solved=" + res.Solved + " bestRow=" + res.BestRow + " nodes=" + res.Nodes);
                    Assert.True(res.Solved, "region " + (r + 1) + " seed " + seed + " bestRow " + res.BestRow);
                }
            }
        }

        [Fact]
        public void BotClimbs150FloorsInEveryRegion()
        {
            SimConfig cfg = new SimConfig();
            for (int r = 0; r < 5; r++)
            {
                LevelSpec spec = SpecFor(r, 600 + r);
                LevelData data = EndlessTower.BuildFixed(cfg, spec, 170);
                Sim sim = new Sim(cfg, data, spec.Difficulty, true, false);
                BotResult res = new LevelBot(sim, 150, 200000).Solve();
                output.WriteLine("region " + (r + 1) + " solved=" + res.Solved + " bestRow=" + res.BestRow + " nodes=" + res.Nodes);
                Assert.True(res.Solved, "region " + (r + 1) + " bestRow " + res.BestRow);
            }
        }

        [Fact]
        public void GrowingTheTower_KeepsTheRunGoing()
        {
            SimConfig cfg = new SimConfig();
            LevelSpec spec = SpecFor(1, 5);
            EndlessTower tower = new EndlessTower(cfg, spec);
            Sim sim = tower.CreateSim();
            SimState s = sim.NewState();
            int version = tower.Version;
            int platformsBefore = tower.Data.Platforms.Length;
            Assert.False(tower.EnsureAhead(10));           // 100 rows built, far enough ahead
            Assert.Equal(version, tower.Version);
            Assert.True(tower.EnsureAhead(60));            // needs 120 rows
            Assert.True(tower.Version > version);
            Assert.True(tower.Data.Platforms.Length > platformsBefore);
            Assert.True(tower.TopRow >= 120);

            Sim bigger = tower.CreateSim();
            bigger.GrowState(ref s);
            Assert.Equal(bigger.Platforms.Length, s.CrumbleTick.Length);
            Assert.Equal(-1, s.CrumbleTick[s.CrumbleTick.Length - 1]);
            for (int i = 0; i < 120; i++) bigger.Step(ref s, SimInput.Jump);
            Assert.False(s.Dead);
        }

        [Fact]
        public void GoldenHash_Region3Seed12345_100Rows()
        {
            SimConfig cfg = new SimConfig();
            LevelData d = EndlessTower.BuildFixed(cfg, SpecFor(2, 12345), 100);
            output.WriteLine("hash " + d.ComputeHash() + " platforms " + d.Platforms.Length);
            Assert.Equal(GoldenR3S12345, d.ComputeHash());
        }

        private const long GoldenR3S12345 = 5513047740284291827L;
    }
}
