using System;
using System.Collections.Generic;
using Xunit;

namespace BoingUpSkyTower.Simulation.Tests
{
    internal static class T3
    {
        public static Platform P(int row, double x, double y, double w, PlatformKind kind)
        {
            Platform p = new Platform();
            p.Row = row; p.X = F(x); p.Y = F(y); p.Width = F(w); p.Kind = kind;
            return p;
        }

        public static Fx F(double v) { return Fx.FromRatio((int)Math.Round(v * 1000), 1000); }

        public static LevelData World(SimConfig cfg, Pickup[] pickups, Hazard[] hazards, params Platform[] extra)
        {
            List<Platform> list = new List<Platform>();
            Platform start = new Platform();
            start.Row = 0; start.X = cfg.ScreenWidth / 2; start.Y = Fx.Zero; start.Width = cfg.ScreenWidth;
            list.Add(start);
            for (int i = 0; i < extra.Length; i++) { Platform p = extra[i]; p.Id = list.Count; list.Add(p); }
            LevelData d = LevelData.FromPlatforms(list.ToArray());
            if (pickups != null)
            {
                for (int i = 0; i < pickups.Length; i++) pickups[i].Id = i;
                d.Pickups = pickups;
            }
            if (hazards != null)
            {
                for (int i = 0; i < hazards.Length; i++) hazards[i].Id = i;
                d.Hazards = hazards;
            }
            return d;
        }

        public static Pickup Pk(PickupKind kind, double x, double y, double r)
        {
            Pickup p = new Pickup();
            p.Kind = kind; p.X = F(x); p.Y = F(y); p.Radius = F(r);
            return p;
        }

        public static Hazard Gong(double x, double y, double r, double amp, int period, int phase)
        {
            Hazard h = new Hazard();
            h.Kind = HazardKind.Gong; h.X = F(x); h.Y = F(y); h.Radius = F(r); h.Amplitude = F(amp); h.Period = period; h.Phase = phase;
            return h;
        }

        public static LevelSpec Spec(int seed, int dMilli, int level, SignatureKind sig, GoalType goal, int value = 0, int count = 0, int timeSec = 0)
        {
            LevelSpec s = new LevelSpec();
            s.Seed = seed;
            s.Difficulty = Fx.FromRatio(dMilli, 1000);
            s.LevelIndex = level;
            s.Signature = sig;
            s.Goal = new GoalSpec();
            s.Goal.Type = goal; s.Goal.Value = value; s.Goal.Count = count; s.Goal.TimeSec = timeSec;
            return s;
        }

        public static Platform OnRow(LevelData d, int row)
        {
            for (int i = 0; i < d.Platforms.Length; i++) if (d.Platforms[i].Row == row) return d.Platforms[i];
            throw new InvalidOperationException("row " + row);
        }

        public static int Count(LevelData d, PlatformKind kind)
        {
            int n = 0;
            for (int i = 0; i < d.Platforms.Length; i++) if (d.Platforms[i].Kind == kind) n++;
            return n;
        }

        public static void RunTicks(Sim sim, ref SimState s, int ticks, int input)
        {
            for (int i = 0; i < ticks; i++) sim.Step(ref s, input);
        }
    }

    public class BuilderTests
    {
        [Fact]
        public void BuilderIsDeterministicAndSorted()
        {
            SimConfig cfg = new SimConfig();
            LevelSpec spec = T3.Spec(4242, 700, 10, SignatureKind.SwingingGongs, GoalType.SurviveWave);
            spec.FixedChunks = new string[] { "chunk_complex_a" };
            LevelData a = LevelBuilder.Build(cfg, spec);
            LevelData b = LevelBuilder.Build(cfg, spec);
            Assert.Equal(a.ComputeHash(), b.ComputeHash());
            spec.Seed = 4243;
            Assert.NotEqual(a.ComputeHash(), LevelBuilder.Build(cfg, spec).ComputeHash());

            for (int i = 0; i < a.Platforms.Length; i++)
            {
                if (i > 0) Assert.True(a.Platforms[i].Y >= a.Platforms[i - 1].Y);
                Assert.Equal(i, a.Platforms[i].Id);
            }
            for (int i = 1; i < a.Pickups.Length; i++) Assert.True(a.Pickups[i].Y >= a.Pickups[i - 1].Y);
            for (int i = 1; i < a.Hazards.Length; i++) Assert.True(a.Hazards[i].Y >= a.Hazards[i - 1].Y);
            Assert.True(a.Hazards.Length > 0);
        }

        [Fact]
        public void GoldenLevelHashesAreStable()
        {
            // Guards cross-device determinism: a change here means every shipped level changed (update the literals
            // only on purpose, together with the owner's sign-off on re-rolling levels).
            SimConfig cfg = new SimConfig();
            LevelSpec a = T3.Spec(12345, 400, 5, SignatureKind.BounceFlowers, GoalType.ReachFloor, 60);
            LevelSpec b = T3.Spec(777, 700, 10, SignatureKind.SwingingGongs, GoalType.SurviveWave);
            b.FixedChunks = new string[] { "chunk_complex_a" };
            b.Class = 3;
            Assert.Equal(5559277721996333721L, LevelBuilder.Build(cfg, a).ComputeHash());
            Assert.Equal(-8519198174591667113L, LevelBuilder.Build(cfg, b).ComputeHash());
        }

        [Fact]
        public void RowsRule()
        {
            Assert.Equal(60, T3.Spec(1, 100, 1, SignatureKind.None, GoalType.ReachFloor, 0).RowsFor());
            Assert.Equal(45, T3.Spec(1, 100, 1, SignatureKind.None, GoalType.ReachFloor, 45).RowsFor());
            Assert.Equal(90, T3.Spec(1, 100, 1, SignatureKind.None, GoalType.Speedrun, 90, 0, 45).RowsFor());
            Assert.Equal(150, T3.Spec(1, 100, 1, SignatureKind.None, GoalType.BossChase, 150).RowsFor());
            Assert.Equal(60, T3.Spec(1, 100, 1, SignatureKind.None, GoalType.CollectCoins, 0, 15).RowsFor());
            Assert.Equal(40, T3.Spec(1, 100, 1, SignatureKind.None, GoalType.NoWallBounce, 40).RowsFor());
            Assert.Equal(60, T3.Spec(1, 100, 1, SignatureKind.None, GoalType.SurviveWave).RowsFor());
            Assert.Equal(30, T3.Spec(1, 100, 1, SignatureKind.None, GoalType.ReachFloor, 10).RowsFor()); // minimum is 30
            LevelSpec explicitRows = T3.Spec(1, 100, 1, SignatureKind.None, GoalType.CollectCoins, 0, 15);
            explicitRows.Rows = 75;
            Assert.Equal(75, explicitRows.RowsFor());
            LevelData big = LevelBuilder.Build(new SimConfig(), explicitRows);
            Assert.Equal(75, big.Platforms[big.Platforms.Length - 1].Row);
        }

        [Fact]
        public void ModuleIdMapping()
        {
            Assert.Equal(SignatureKind.BounceFlowers, SignatureKinds.FromModuleId("mod_bounce_flowers"));
            Assert.Equal(SignatureKind.BounceCaps, SignatureKinds.FromModuleId("mod_bounce_caps"));
            Assert.Equal(SignatureKind.DriftingPlatforms, SignatureKinds.FromModuleId("mod_drifting_platforms"));
            Assert.Equal(SignatureKind.StickyGum, SignatureKinds.FromModuleId("mod_sticky_gum"));
            Assert.Equal(SignatureKind.SwingingGongs, SignatureKinds.FromModuleId("mod_swinging_gongs"));
            Assert.Equal(SignatureKind.None, SignatureKinds.FromModuleId("mod_wind_gusts"));
            Assert.Equal(SignatureKind.None, SignatureKinds.FromModuleId(null));
            Assert.Equal(SignatureKind.None, SignatureKinds.FromModuleId(""));
        }

        [Theory]
        [InlineData(SignatureKind.BounceFlowers, PlatformKind.Bounce)]
        [InlineData(SignatureKind.BounceCaps, PlatformKind.BounceCap)]
        [InlineData(SignatureKind.DriftingPlatforms, PlatformKind.Moving)]
        [InlineData(SignatureKind.StickyGum, PlatformKind.Sticky)]
        public void SignatureAppearsOnlyPerTemplate(SignatureKind sig, PlatformKind kind)
        {
            SimConfig cfg = new SimConfig();
            int l3 = 0, l6 = 0, l12 = 0;
            for (int seed = 1; seed <= 12; seed++)
            {
                foreach (int level in new int[] { 1, 2 })
                {
                    LevelData none = LevelBuilder.Build(cfg, T3.Spec(seed * 977, 300, level, sig, GoalType.ReachFloor, 60));
                    for (int i = 0; i < none.Platforms.Length; i++) Assert.True(none.Platforms[i].Kind == PlatformKind.Normal || none.Platforms[i].Kind == PlatformKind.Crumble);
                    Assert.Empty(none.Hazards);
                }
                l3 += T3.Count(LevelBuilder.Build(cfg, T3.Spec(seed * 977, 150, 3, sig, GoalType.ReachFloor, 60)), kind);
                l6 += T3.Count(LevelBuilder.Build(cfg, T3.Spec(seed * 977, 350, 6, sig, GoalType.ReachFloor, 60)), kind);
                l12 += T3.Count(LevelBuilder.Build(cfg, T3.Spec(seed * 977, 700, 12, sig, GoalType.ReachFloor, 60)), kind);
                // no other special kinds ever appear
                LevelData any = LevelBuilder.Build(cfg, T3.Spec(seed * 977, 700, 12, sig, GoalType.ReachFloor, 60));
                for (int i = 0; i < any.Platforms.Length; i++)
                    Assert.True(any.Platforms[i].Kind == PlatformKind.Normal || any.Platforms[i].Kind == PlatformKind.Crumble || any.Platforms[i].Kind == kind);
            }
            Assert.True(l3 > 0, "level 3 introduces the signature");
            Assert.True(l6 > l3, "level 4+ has the normal (larger) share: " + l3 + " vs " + l6);
            Assert.True(l12 > l6, "share grows with d: " + l6 + " vs " + l12);
            // share never exceeds the doc 08 maximum of 40 percent
            Assert.True(l12 <= 12 * 60 * 40 / 100 + 40);
        }

        [Fact]
        public void NoSignatureRegionHasPlainPlatforms()
        {
            SimConfig cfg = new SimConfig();
            LevelData d = LevelBuilder.Build(cfg, T3.Spec(5, 800, 9, SignatureKind.None, GoalType.ReachFloor, 60));
            for (int i = 0; i < d.Platforms.Length; i++) Assert.True(d.Platforms[i].Kind == PlatformKind.Normal || d.Platforms[i].Kind == PlatformKind.Crumble);
            Assert.Empty(d.Hazards);
        }

        [Fact]
        public void DriftingPlatformsSwingWider()
        {
            SimConfig cfg = new SimConfig();
            Fx maxAmp = Fx.Zero;
            for (int seed = 1; seed <= 12; seed++)
            {
                LevelData d = LevelBuilder.Build(cfg, T3.Spec(seed * 31, 500, 8, SignatureKind.DriftingPlatforms, GoalType.ReachFloor, 60));
                for (int i = 0; i < d.Platforms.Length; i++)
                    if (d.Platforms[i].Kind == PlatformKind.Moving)
                    {
                        Assert.True(d.Platforms[i].Period > 0);
                        Assert.True(d.Platforms[i].X - d.Platforms[i].Width / 2 - d.Platforms[i].Amplitude >= -Fx.FromRatio(1, 100));
                        Assert.True(d.Platforms[i].X + d.Platforms[i].Width / 2 + d.Platforms[i].Amplitude <= cfg.ScreenWidth + Fx.FromRatio(1, 100));
                        if (d.Platforms[i].Amplitude > maxAmp) maxAmp = d.Platforms[i].Amplitude;
                    }
            }
            Assert.True(maxAmp >= cfg.DriftAmplitudeMin, "drifting amplitude exceeds the plain moving range of 0.5 to 1.0");
        }

        [Fact]
        public void StickyIsFollowedByShortGap()
        {
            SimConfig cfg = new SimConfig();
            for (int seed = 1; seed <= 10; seed++)
            {
                LevelData d = LevelBuilder.Build(cfg, T3.Spec(seed * 53, 900, 12, SignatureKind.StickyGum, GoalType.ReachFloor, 60));
                for (int i = 0; i + 1 < d.Platforms.Length; i++)
                {
                    if (d.Platforms[i].Kind != PlatformKind.Sticky) continue;
                    for (int j = i + 1; j < d.Platforms.Length; j++)
                        if (d.Platforms[j].Row == d.Platforms[i].Row + 1)
                        {
                            Assert.True(d.Platforms[j].Y - d.Platforms[i].Y <= cfg.StickyNextGapMax + Fx.FromRatio(1, 100));
                            break;
                        }
                }
            }
        }

        [Fact]
        public void GongsFollowTemplateAndDensityRisesWithDifficulty()
        {
            SimConfig cfg = new SimConfig();
            int low = 0, high = 0;
            for (int seed = 1; seed <= 12; seed++)
            {
                Assert.Empty(LevelBuilder.Build(cfg, T3.Spec(seed, 500, 1, SignatureKind.SwingingGongs, GoalType.ReachFloor, 60)).Hazards);
                Assert.Empty(LevelBuilder.Build(cfg, T3.Spec(seed, 500, 2, SignatureKind.SwingingGongs, GoalType.ReachFloor, 60)).Hazards);
                low += LevelBuilder.Build(cfg, T3.Spec(seed, 200, 4, SignatureKind.SwingingGongs, GoalType.ReachFloor, 60)).Hazards.Length;
                high += LevelBuilder.Build(cfg, T3.Spec(seed, 900, 14, SignatureKind.SwingingGongs, GoalType.ReachFloor, 60)).Hazards.Length;
                // other regions never get gongs from the signature
                Assert.Empty(LevelBuilder.Build(cfg, T3.Spec(seed, 900, 14, SignatureKind.StickyGum, GoalType.ReachFloor, 60)).Hazards);
            }
            Assert.True(low > 0);
            Assert.True(high > low * 2, "density rises with d: " + low + " vs " + high);
            int l3 = 0;
            for (int seed = 1; seed <= 12; seed++) l3 += LevelBuilder.Build(cfg, T3.Spec(seed, 150, 3, SignatureKind.SwingingGongs, GoalType.ReachFloor, 60)).Hazards.Length;
            Assert.True(l3 > 0, "gongs start at level 3");
        }

        [Fact]
        public void SurviveWaveHasDenseGongSectionInTheMiddle()
        {
            SimConfig cfg = new SimConfig();
            LevelData d = LevelBuilder.Build(cfg, T3.Spec(77, 700, 10, SignatureKind.StickyGum, GoalType.SurviveWave));
            Assert.True(d.WaveStartRow > 0 && d.WaveEndRow > d.WaveStartRow);
            Fx lo = T3.OnRow(d, d.WaveStartRow).Y - Fx.FromInt(6);
            Fx hi = T3.OnRow(d, d.WaveEndRow).Y + Fx.FromInt(6);
            int inside = 0, outside = 0;
            for (int i = 0; i < d.Hazards.Length; i++)
            {
                if (d.Hazards[i].Y >= lo && d.Hazards[i].Y <= hi) inside++; else outside++;
            }
            Assert.True(inside >= 12, "wave section is dense: " + inside);
            Assert.Equal(0, outside);
        }

        [Fact]
        public void HiddenGemSitsOnASidePlatformNearTheMiddle()
        {
            SimConfig cfg = new SimConfig();
            for (int seed = 1; seed <= 8; seed++)
            {
                LevelSpec spec = T3.Spec(seed * 101, 500, 8, SignatureKind.None, GoalType.FindHiddenGem);
                LevelData d = LevelBuilder.Build(cfg, spec);
                int gems = 0;
                Pickup gem = new Pickup();
                for (int i = 0; i < d.Pickups.Length; i++) if (d.Pickups[i].Kind == PickupKind.HiddenGem) { gems++; gem = d.Pickups[i]; }
                Assert.Equal(1, gems);
                Assert.True(Math.Abs(d.HiddenGemRow - spec.RowsFor() / 2) <= 6);
                int onRow = 0;
                bool sideUnderGem = false;
                for (int i = 0; i < d.Platforms.Length; i++)
                    if (d.Platforms[i].Row == d.HiddenGemRow)
                    {
                        onRow++;
                        if (Fx.Abs(d.Platforms[i].X - gem.X) < Fx.FromRatio(1, 100)) sideUnderGem = true;
                    }
                Assert.True(d.HiddenGemOnSidePlatform);
                Assert.True(onRow >= 2, "side platform next to the main one (seed " + seed + ")");
                Assert.True(sideUnderGem);
            }
        }

        [Fact]
        public void ComboLadderHasTightRows()
        {
            SimConfig cfg = new SimConfig();
            LevelData d = LevelBuilder.Build(cfg, T3.Spec(9, 400, 5, SignatureKind.None, GoalType.ComboChain, 3, 2));
            Assert.True(d.LadderStartRow > 0);
            Assert.Equal(d.LadderStartRow + 3 * 2 + 1, d.LadderEndRow);
            Fx rung = cfg.ComboLadderSpan / 3;
            for (int r = d.LadderStartRow + 1; r <= d.LadderEndRow; r++)
            {
                Fx gap = T3.OnRow(d, r).Y - T3.OnRow(d, r - 1).Y;
                Assert.True(Fx.Abs(gap - rung) < Fx.FromRatio(1, 100));
            }
        }

        [Fact]
        public void SpeedrunLevelsUseNormalGaps()
        {
            // Owner decision 2026-10-19: speedrun levels keep the normal vertical gaps (the time goal was relaxed instead).
            SimConfig cfg = new SimConfig();
            LevelData fast = LevelBuilder.Build(cfg, T3.Spec(3, 580, 9, SignatureKind.None, GoalType.Speedrun, 90, 0, 45));
            LevelData plain = LevelBuilder.Build(cfg, T3.Spec(3, 580, 9, SignatureKind.None, GoalType.ReachFloor, 90));
            Assert.Equal(T3.OnRow(plain, 90).Y, T3.OnRow(fast, 90).Y);
            Assert.Equal(plain.ComputeHash(), fast.ComputeHash());
        }

        [Fact]
        public void HiddenGemCountsAsHiddenGemNotAsGemAndCompletesTheGoal()
        {
            // Economy contract (NOTES.md): FindHiddenGem completes with SimState.HiddenGems >= 1; Gems stays 0.
            SimConfig cfg = new SimConfig();
            Pickup[] pk = new Pickup[] { T3.Pk(PickupKind.HiddenGem, 5, 1.5, 0.9) };
            LevelData d = T3.World(cfg, pk, null);
            LevelSpec spec = T3.Spec(8, 500, 8, SignatureKind.None, GoalType.FindHiddenGem);
            Sim sim = new Sim(cfg, d, Fx.Zero, false, false);
            SimState s = sim.NewState();
            Assert.False(GoalEvaluator.IsComplete(spec, s));
            for (int round = 0; round < 3 && s.HiddenGems == 0; round++)
            {
                sim.Step(ref s, SimInput.Jump);
                T3.RunTicks(sim, ref s, 80, 0);
            }
            Assert.Equal(1, s.HiddenGems);
            Assert.Equal(0, s.Gems);
            Assert.True(GoalEvaluator.IsComplete(spec, s));
        }

        // ---------------- pickups ----------------

        private static int Coins(LevelData d)
        {
            int n = 0;
            for (int i = 0; i < d.Pickups.Length; i++) if (d.Pickups[i].Kind == PickupKind.Coin) n++;
            return n;
        }

        [Fact]
        public void EveryLevelHasThreeStarsAndNoCoinPickups()
        {
            SimConfig cfg = new SimConfig();
            for (int seed = 1; seed <= 40; seed++)
            {
                LevelData d = LevelBuilder.Build(cfg, T3.Spec(seed * 7919, 50 + seed * 20, 1 + seed % 5, SignatureKind.None, GoalType.ReachFloor, 60));
                int mask = 0, stars = 0;
                for (int i = 0; i < d.Pickups.Length; i++)
                {
                    Assert.NotEqual(PickupKind.Coin, d.Pickups[i].Kind);
                    if (d.Pickups[i].Kind == PickupKind.Star) { stars++; mask |= 1 << d.Pickups[i].Tag; }
                }
                Assert.Equal(3, stars);
                Assert.Equal(7, mask);
            }
        }

        [Fact]
        public void GemsAreRareAndFollowTheOwnerOdds()
        {
            // Owner 2026-10-24: ordinary levels 8 percent for one gem (never two), levels 5 and 10 60 percent for one,
            // level 15 always one and 40 percent for a second.
            SimConfig cfg = new SimConfig();
            const int N = 400;
            int[] withGem = new int[16];
            int level15Two = 0;
            foreach (int idx in new int[] { 1, 4, 5, 10, 15 })
            {
                for (int seed = 1; seed <= N; seed++)
                {
                    LevelSpec sp = T3.Spec(seed * 104729, 300, idx, SignatureKind.None, GoalType.ReachFloor, 60);
                    LevelData d = LevelBuilder.Build(cfg, sp);
                    int gems = 0;
                    for (int i = 0; i < d.Pickups.Length; i++) if (d.Pickups[i].Kind == PickupKind.Gem) { gems++; Assert.Equal(0, d.Pickups[i].Tag); }
                    Assert.True(gems <= (idx == 15 ? 2 : 1), "level " + idx + " has " + gems + " gems");
                    if (gems > 0) withGem[idx]++;
                    if (idx == 15) { Assert.True(gems >= 1); if (gems == 2) level15Two++; }
                }
            }
            Assert.InRange(withGem[1] * 100 / N, 4, 12);
            Assert.InRange(withGem[4] * 100 / N, 4, 12);
            Assert.InRange(withGem[5] * 100 / N, 52, 68);
            Assert.InRange(withGem[10] * 100 / N, 52, 68);
            Assert.Equal(N, withGem[15]);
            Assert.InRange(level15Two * 100 / N, 32, 48);
        }

        [Fact]
        public void PickupsStayInsideTheScreenAboveTheStart()
        {
            SimConfig cfg = new SimConfig();
            LevelData d = LevelBuilder.Build(cfg, T3.Spec(31337, 900, 12, SignatureKind.SwingingGongs, GoalType.ReachFloor, 100));
            for (int i = 0; i < d.Pickups.Length; i++)
            {
                Assert.Equal(i, d.Pickups[i].Id);
                Assert.True(d.Pickups[i].X >= Fx.Zero && d.Pickups[i].X <= cfg.ScreenWidth);
                Assert.True(d.Pickups[i].Y > Fx.Zero);
            }
        }

        [Fact]
        public void PickupIsCollectedOnceAndCountedByKind()
        {
            SimConfig cfg = new SimConfig();
            Pickup[] pk = new Pickup[]
            {
                T3.Pk(PickupKind.Coin, 5, 1.5, 0.9),
                T3.Pk(PickupKind.Gem, 5, 2.0, 0.6),
                T3.Pk(PickupKind.HiddenGem, 9, 0.5, 0.6)
            };
            LevelData d = T3.World(cfg, pk, null);
            Sim sim = new Sim(cfg, d, Fx.Zero, false, false);
            SimState s = sim.NewState();
            for (int round = 0; round < 3; round++)
            {
                sim.Step(ref s, SimInput.Jump);
                T3.RunTicks(sim, ref s, 80, 0);
            }
            Assert.Equal(1, s.Coins);
            Assert.Equal(1, s.Gems);
            Assert.Equal(0, s.HiddenGems);
            Assert.True(s.PickupTaken[0] && s.PickupTaken[1] && !s.PickupTaken[2]);
            // a copy keeps its own flags
            SimState copy = s.Clone();
            copy.PickupTaken[2] = true;
            Assert.False(s.PickupTaken[2]);
        }

        [Fact]
        public void PickupOutOfRadiusIsNotCollected()
        {
            SimConfig cfg = new SimConfig();
            LevelData d = T3.World(cfg, new Pickup[] { T3.Pk(PickupKind.Coin, 8, 1.0, 0.5) }, null);
            Sim sim = new Sim(cfg, d, Fx.Zero, false, false);
            SimState s = sim.NewState();
            sim.Step(ref s, SimInput.Jump);
            T3.RunTicks(sim, ref s, 80, 0);
            Assert.Equal(0, s.Coins);
        }

        // ---------------- coin rules ----------------

        private static Sim CoinWorld(SimConfig cfg, bool endless)
        {
            LevelData d = T3.World(cfg, null, null,
                T3.P(1, 1, 1.8, 1, PlatformKind.Normal),
                T3.P(2, 5, 3.6, 8, PlatformKind.Normal),
                T3.P(3, 1, 5.4, 1, PlatformKind.Normal),
                T3.P(4, 5, 7.2, 8, PlatformKind.Normal));
            return new Sim(cfg, d, Fx.Zero, endless, false);
        }

        private static void JumpUntilRow(Sim sim, ref SimState s, int row)
        {
            sim.Step(ref s, SimInput.Jump);
            for (int i = 0; i < 200 && !(s.Row >= row && s.Grounded); i++) sim.Step(ref s, 0);
        }

        [Fact]
        public void LandingOnNewRowGivesCoinsAndMultiFloorExtras()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = CoinWorld(cfg, false);
            SimState s = sim.NewState();
            JumpUntilRow(sim, ref s, 2);
            Assert.Equal(2, s.Row);
            // economy v2: a plain landing pays nothing; only continuing a combo pays (5)
            Assert.Equal(0, s.Coins);
            JumpUntilRow(sim, ref s, 4);
            Assert.Equal(4, s.Row);
            // continues the combo: +5
            Assert.Equal(5, s.Coins);
            Assert.Equal(2, s.Combo);
            // score formula unchanged: floors x 10 + combo^2 x 5 (no coins in it)
            Assert.Equal(4 * 10 + 1 * 5 + 4 * 5, s.Score);
        }

        [Fact]
        public void SingleFloorLandingGivesNoCoins()
        {
            SimConfig cfg = new SimConfig();
            LevelData d = T3.World(cfg, null, null, T3.P(1, 5, 1.5, 8, PlatformKind.Normal));
            Sim sim = new Sim(cfg, d, Fx.Zero, false, false);
            SimState s = sim.NewState();
            JumpUntilRow(sim, ref s, 1);
            Assert.Equal(0, s.Coins);
            // drop back to row 0 is impossible here (walk off is not modelled); jump and land again on the same row: no new coin
            sim.Step(ref s, SimInput.Jump);
            for (int i = 0; i < 100; i++) sim.Step(ref s, 0);
            Assert.Equal(0, s.Coins);
        }

        [Fact]
        public void EndlessGivesNoLandingCoins()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = CoinWorld(cfg, true);
            SimState s = sim.NewState();
            JumpUntilRow(sim, ref s, 2);
            Assert.Equal(2, s.Row);
            Assert.Equal(0, s.Coins);
        }

        // ---------------- signature physics ----------------

        private static Fx PeakAfterLanding(SimConfig cfg, PlatformKind kind)
        {
            LevelData d = T3.World(cfg, null, null, T3.P(1, 5, 1.0, 8, kind));
            Sim sim = new Sim(cfg, d, Fx.Zero, false, false);
            SimState s = sim.NewState();
            sim.Step(ref s, SimInput.Jump);
            Fx peak = Fx.Zero;
            bool landed = false;
            for (int i = 0; i < 300; i++)
            {
                sim.Step(ref s, 0);
                if (s.Row >= 1) landed = true;
                if (landed && s.Y > peak) peak = s.Y;
            }
            return peak;
        }

        [Fact]
        public void BounceCapLaunchesHigherThanBounceThanNormal()
        {
            SimConfig cfg = new SimConfig();
            Fx normal = PeakAfterLanding(cfg, PlatformKind.Normal);
            Fx bounce = PeakAfterLanding(cfg, PlatformKind.Bounce);
            Fx cap = PeakAfterLanding(cfg, PlatformKind.BounceCap);
            Assert.True(bounce > normal + Fx.One);
            Assert.True(cap > bounce + Fx.One);
        }

        // ---------------- gongs ----------------

        [Fact]
        public void GongPositionIsATriangleWave()
        {
            SimConfig cfg = new SimConfig();
            LevelData d = T3.World(cfg, null, new Hazard[] { T3.Gong(5, 3, 0.6, 2, 100, 0) });
            Sim sim = new Sim(cfg, d, Fx.Zero, false, false);
            Fx x, y;
            sim.HazardPosition(0, 0, out x, out y);
            Assert.Equal(T3.F(3), x);                       // swing extreme left
            Assert.True(y > T3.F(3));                       // pendulum is higher at the extremes
            sim.HazardPosition(0, 50, out x, out y);
            Assert.Equal(T3.F(7), x);                       // extreme right
            sim.HazardPosition(0, 25, out x, out y);
            Assert.Equal(T3.F(5), x);                       // centre, lowest
            Assert.Equal(T3.F(3), y);
        }

        [Fact]
        public void GongKnocksBackWithInvulnerabilityAndNeverKills()
        {
            SimConfig cfg = new SimConfig();
            // Stationary gong on the player's start spot (amplitude 0): centre (5, 0.5).
            LevelData d = T3.World(cfg, null, new Hazard[] { T3.Gong(5.2, 0.5, 0.6, 0, 100, 0) });
            Sim sim = new Sim(cfg, d, Fx.Zero, false, false);
            SimState s = sim.NewState();
            sim.Step(ref s, 0);
            Assert.Equal(1, s.Hits);
            Assert.True(s.Vx < Fx.Zero, "pushed away from a gong on the right");
            Assert.False(s.Dead);
            Assert.True(s.Invuln > 0);
            // Put the player back into the gong repeatedly: no second hit while invulnerable.
            for (int i = 0; i < cfg.GongInvulnTicks - 5; i++)
            {
                s.X = T3.F(5.0);
                sim.Step(ref s, 0);
            }
            Assert.Equal(1, s.Hits);
            // after the window it can hit again
            for (int i = 0; i < 20; i++)
            {
                s.X = T3.F(5.0);
                sim.Step(ref s, 0);
            }
            Assert.Equal(2, s.Hits);
            // thousands of ticks inside gongs never kill the player
            for (int i = 0; i < 3000; i++)
            {
                s.X = T3.F(5.0);
                sim.Step(ref s, 0);
            }
            Assert.False(s.Dead);
            Assert.True(s.Hits >= 20);
        }

        [Fact]
        public void GongAirKnockbackHasAVerticalComponent()
        {
            SimConfig cfg = new SimConfig();
            LevelData d = T3.World(cfg, null, new Hazard[] { T3.Gong(5, 2.4, 0.6, 0, 100, 0) });
            Sim sim = new Sim(cfg, d, Fx.Zero, false, false);
            SimState s = sim.NewState();
            sim.Step(ref s, SimInput.Jump);
            for (int i = 0; i < 60 && s.Hits == 0; i++) sim.Step(ref s, 0);
            Assert.Equal(1, s.Hits);
            Assert.True(Fx.Abs(s.Vy) == cfg.GongKnockbackVy);
        }

        // ---------------- scroll ramp, revive, ctor ----------------

        [Fact]
        public void CampaignRampIsClampedToSixtyPercent()
        {
            SimConfig cfg = new SimConfig();
            LevelData d = T3.World(cfg, null, null);
            Sim plain = new Sim(cfg, d, Fx.One, false, false);
            Sim ramp = new Sim(cfg, d, Fx.One, false, true);
            Sim endless = new Sim(cfg, d, Fx.One, true, false);
            long dPlain = KillDelta(plain, 1000), dRamp = KillDelta(ramp, 1000), dEnd = KillDelta(endless, 1000);
            Assert.True(Math.Abs(dRamp * 10 - dPlain * 16) < dPlain / 2, "campaign ramp caps at +60 percent");
            Assert.True(dEnd > dRamp, "endless goes beyond the campaign cap (+150 percent)");
            long dLow = KillDelta(ramp, 100); // row 100: 4 steps x 4 percent = 16 percent
            Assert.True(Math.Abs(dLow * 100 - dPlain * 116) < dPlain, "step formula below the cap");
        }

        private static long KillDelta(Sim sim, int row)
        {
            SimState s = sim.NewState();
            s.Row = row;
            s.StartTick = 0;   // the screen scrolls once the run has started (first jump)
            Fx before = s.KillY;
            for (int i = 0; i < 60; i++) sim.Step(ref s, 0);
            return s.KillY.Raw - before.Raw;
        }

        [Fact]
        public void SimFromSpecUsesSpecRampAndDifficulty()
        {
            SimConfig cfg = new SimConfig();
            LevelSpec spec = T3.Spec(1, 1000, 15, SignatureKind.None, GoalType.BossChase, 150);
            spec.Ramp = true;
            Sim sim = new Sim(cfg, LevelBuilder.Build(cfg, spec), spec);
            Assert.True(sim.CampaignRamp);
            Assert.False(sim.Endless);
        }

        [Fact]
        public void ReviveReturnsToHighestPlatformAndCounts()
        {
            SimConfig cfg = new SimConfig();
            LevelData d = T3.World(cfg, null, null, T3.P(1, 5, 1.5, 8, PlatformKind.Normal));
            Sim sim = new Sim(cfg, d, Fx.Zero, false, false);
            SimState s = sim.NewState();
            JumpUntilRow(sim, ref s, 1);
            s.Dead = true;
            s.Y = Fx.FromInt(-50);
            sim.Revive(ref s);
            Assert.False(s.Dead);
            Assert.Equal(1, s.RevivesUsed);
            Assert.Equal(T3.F(1.5), s.Y);
            Assert.True(s.Invuln > 0);
            sim.Revive(ref s); // not dead: no effect
            Assert.Equal(1, s.RevivesUsed);
            T3.RunTicks(sim, ref s, 60, 0);
            Assert.False(s.Dead);
        }

        [Fact]
        public void StepAllocatesNothing()
        {
            SimConfig cfg = new SimConfig();
            Pickup[] pk = new Pickup[] { T3.Pk(PickupKind.Coin, 3, 1.0, 0.9), T3.Pk(PickupKind.Coin, 7, 2.0, 0.9) };
            Hazard[] hz = new Hazard[] { T3.Gong(5, 1.5, 0.6, 2, 100, 7), T3.Gong(4, 2.5, 0.6, 2, 120, 3) };
            LevelData d = T3.World(cfg, pk, hz,
                T3.P(1, 3, 2.0, 5, PlatformKind.Bounce),
                T3.P(2, 7, 3.2, 5, PlatformKind.Moving),
                T3.P(2, 5, 3.0, 8, PlatformKind.Crumble));
            d.Platforms[2].Amplitude = Fx.One; d.Platforms[2].Period = 120;
            Sim sim = new Sim(cfg, d, Fx.One, false, true);
            SimState s = sim.NewState();
            Rng rng = new Rng(5);
            for (int i = 0; i < 300; i++) sim.Step(ref s, rng.Next(8)); // warm up (JIT)
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++)
            {
                s.Dead = false; // keep exercising the full path even if the kill zone caught up
                sim.Step(ref s, rng.Next(8));
            }
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.Equal(0L, after - before);
        }

        [Fact]
        public void LegacyConstructorStillWorks()
        {
            SimConfig cfg = new SimConfig();
            GeneratorParams gp = new GeneratorParams();
            gp.Seed = 3; gp.Difficulty = Fx.FromRatio(3, 10); gp.Rows = 30;
            Platform[] plats = LevelGenerator.Generate(cfg, gp);
            Sim sim = new Sim(cfg, plats, gp.Difficulty, false);
            Assert.Empty(sim.Pickups);
            Assert.Empty(sim.Hazards);
            BotResult r = new LevelBot(sim, 30, 5000).Solve();
            Assert.True(r.Solved);
        }
    }

    public class ChunkTests
    {
        [Fact]
        public void LibraryHasSixComplexAndSixGauntletChunks()
        {
            int complex = 0, gauntlet = 0;
            for (int i = 0; i < ChunkLibrary.All.Length; i++)
            {
                if (ChunkLibrary.All[i].Class == 3) complex++;
                if (ChunkLibrary.All[i].Class == 4) gauntlet++;
            }
            Assert.True(complex >= 6);
            Assert.True(gauntlet >= 6);
            Assert.NotNull(ChunkLibrary.Get("chunk_complex_a"));
            Assert.NotNull(ChunkLibrary.Get("chunk_gauntlet_a"));
            Assert.Null(ChunkLibrary.Get("chunk_nope"));
            for (int i = 0; i < ChunkLibrary.All.Length; i++)
                for (int j = i + 1; j < ChunkLibrary.All.Length; j++)
                    Assert.NotEqual(ChunkLibrary.All[i].Id, ChunkLibrary.All[j].Id);
        }

        [Fact]
        public void EveryChunkRespectsTheReachabilityRules()
        {
            SimConfig cfg = new SimConfig();
            Fx maxGap = cfg.MaxJumpHeight() * 9 / 10;
            foreach (Chunk c in ChunkLibrary.All)
            {
                for (int i = 0; i < c.Rows.Length; i++)
                {
                    ChunkRow r = c.Rows[i];
                    Fx gap = Fx.FromRatio(r.GapHundredths, 100);
                    Fx w = Fx.FromRatio(r.WidthTenths, 10);
                    Assert.True(gap < maxGap, c.Id + " row " + i + " gap " + gap);
                    Assert.InRange(r.XPermille, 0, 1000);
                    Assert.True(w >= Fx.FromRatio(14, 10) && w <= Fx.FromInt(9));
                    Fx x = Fx.FromRatio(r.XPermille, 1000) * cfg.ScreenWidth;
                    Assert.True(x - w / 2 >= -Fx.FromRatio(1, 100), c.Id + " row " + i + " left edge");
                    Assert.True(x + w / 2 <= cfg.ScreenWidth + Fx.FromRatio(1, 100), c.Id + " row " + i + " right edge");
                    if (i == 0) continue;
                    ChunkRow p = c.Rows[i - 1];
                    Fx px = Fx.FromRatio(p.XPermille, 1000) * cfg.ScreenWidth;
                    Fx pw = Fx.FromRatio(p.WidthTenths, 10);
                    Fx dx = Fx.Abs(x - px);
                    // moving platforms swing by the authored amplitude: keep the worst case inside the reach rule
                    Fx swing = (r.Kind == PlatformKind.Moving ? cfg.ChunkMovingAmplitude : Fx.Zero) + (p.Kind == PlatformKind.Moving ? cfg.ChunkMovingAmplitude : Fx.Zero);
                    Fx limit = cfg.MaxReachDx(gap) * 6 / 10 + (pw + w) / 2 + cfg.PlayerHalfWidth;
                    Assert.True(dx - swing <= limit, c.Id + " row " + i + " dx " + dx + " limit " + limit);
                    if (p.Kind == PlatformKind.Bounce || p.Kind == PlatformKind.BounceCap)
                    {
                        Assert.True(gap <= cfg.BounceNextGapMax, c.Id + " row " + i + " gap after a bounce platform");
                        Assert.True(dx - (pw + w) / 2 <= cfg.BounceNextDxMax, c.Id + " row " + i + " dx after a bounce platform");
                    }
                }
            }
        }

        [Fact]
        public void BuilderSplicesChunksAtIntervalsAndAtTheEnd()
        {
            SimConfig cfg = new SimConfig();
            LevelSpec spec = T3.Spec(5, 400, 5, SignatureKind.None, GoalType.ReachFloor, 80);
            spec.FixedChunks = new string[] { "chunk_complex_c" }; // pillars: width 2.6, gap 3.6, six rows
            LevelData d = LevelBuilder.Build(cfg, spec);
            List<int> starts = new List<int>();
            int run = 0;
            for (int r = 1; r <= 80; r++)
            {
                Platform p = FirstOnRow(d, r);
                Platform prev = FirstOnRow(d, r - 1);
                bool authored = Fx.Abs(p.Width - T3.F(3.0)) < Fx.FromRatio(2, 100) && Fx.Abs((p.Y - prev.Y) - T3.F(3.6)) < Fx.FromRatio(2, 100);
                if (authored) { run++; if (run == 6) starts.Add(r - 5); } else run = 0;
            }
            Assert.True(starts.Count >= 3, "several chunks spliced: " + starts.Count);
            Assert.Equal(cfg.ChunkStartRow, starts[0]);
            Assert.True(starts.Contains(80 - 6 + 1), "one chunk ends the level");
            for (int i = 1; i < starts.Count - 1; i++) Assert.Equal(cfg.ChunkInterval + 6, starts[i] - starts[i - 1]);
        }

        private static Platform FirstOnRow(LevelData d, int row)
        {
            for (int i = 0; i < d.Platforms.Length; i++) if (d.Platforms[i].Row == row) return d.Platforms[i];
            throw new InvalidOperationException("row " + row);
        }

        [Fact]
        public void ChunkEntryIsReachableFromThePreviousPlatform()
        {
            SimConfig cfg = new SimConfig();
            foreach (Chunk c in ChunkLibrary.All)
            {
                for (int seed = 1; seed <= 6; seed++)
                {
                    LevelSpec spec = T3.Spec(seed * 11, c.Class == 4 ? 1000 : 600, c.Class == 4 ? 15 : 10, SignatureKind.None, GoalType.ReachFloor, 70);
                    spec.FixedChunks = new string[] { c.Id };
                    LevelData d = LevelBuilder.Build(cfg, spec);
                    Fx maxGap = cfg.MaxJumpHeight() * 9 / 10;
                    for (int r = 1; r <= 70; r++)
                    {
                        Platform a = FirstOnRow(d, r - 1), b = FirstOnRow(d, r);
                        Assert.True(b.Y - a.Y <= maxGap + Fx.FromRatio(1, 100), c.Id + " gap at row " + r);
                        Fx gap = b.Y - a.Y;
                        Fx limit = cfg.MaxReachDx(gap) * 6 / 10 + (a.Width + b.Width) / 2 + cfg.PlayerHalfWidth;
                        Fx swing = (a.Kind == PlatformKind.Moving ? a.Amplitude : Fx.Zero) + (b.Kind == PlatformKind.Moving ? b.Amplitude : Fx.Zero);
                        Assert.True(Fx.Abs(b.X - a.X) - swing <= limit + Fx.FromRatio(1, 100), c.Id + " dx at row " + r);
                    }
                }
            }
        }

        [Fact]
        public void BotSolvesLevelsContainingEachChunk()
        {
            SimConfig cfg = new SimConfig();
            List<string> failures = new List<string>();
            foreach (Chunk c in ChunkLibrary.All)
            {
                for (int seed = 1; seed <= 3; seed++)
                {
                    LevelSpec spec = T3.Spec(seed * 313, c.Class == 4 ? 1000 : 700, c.Class == 4 ? 15 : 10, SignatureKind.None, GoalType.ReachFloor, 60);
                    spec.FixedChunks = new string[] { c.Id };
                    LevelData d = LevelBuilder.Build(cfg, spec);
                    Sim sim = new Sim(cfg, d, spec);
                    BotResult r = new LevelBot(sim, spec, d, 12000).Solve();
                    if (!r.Solved) failures.Add(c.Id + " seed " + seed + " bestRow " + r.BestRow + " nodes " + r.Nodes);
                }
            }
            Assert.Empty(failures);
        }
    }

    public class GoalStarTests
    {
        private static SimState State()
        {
            SimState s = new SimState();
            s.CrumbleTick = new int[0];
            s.PickupTaken = new bool[0];
            return s;
        }

        private static LevelSpec Spec(GoalType type, int value, int count, int timeSec)
        {
            return T3.Spec(1, 500, 5, SignatureKind.None, type, value, count, timeSec);
        }

        [Fact]
        public void ReachFloorAndBossChase()
        {
            SimState s = State();
            LevelSpec reach = Spec(GoalType.ReachFloor, 30, 0, 0);
            Assert.False(GoalEvaluator.IsComplete(reach, s));
            s.Row = 29;
            Assert.False(GoalEvaluator.IsComplete(reach, s));
            s.Row = 30;
            Assert.True(GoalEvaluator.IsComplete(reach, s));
            LevelSpec boss = Spec(GoalType.BossChase, 150, 0, 0);
            s.Row = 149;
            Assert.False(GoalEvaluator.IsComplete(boss, s));
            s.Row = 150;
            Assert.True(GoalEvaluator.IsComplete(boss, s));
        }

        [Fact]
        public void CollectCoins()
        {
            SimState s = State();
            LevelSpec g = Spec(GoalType.CollectCoins, 0, 15, 0);
            s.Coins = 14;
            Assert.False(GoalEvaluator.IsComplete(g, s));
            s.Coins = 15;
            Assert.True(GoalEvaluator.IsComplete(g, s));
        }

        [Fact]
        public void NoWallBounce()
        {
            SimState s = State();
            LevelSpec g = Spec(GoalType.NoWallBounce, 60, 0, 0);
            s.Row = 60;
            Assert.True(GoalEvaluator.IsComplete(g, s));
            s.WallBounces = 1;
            Assert.False(GoalEvaluator.IsComplete(g, s));
            s.WallBounces = 0;
            s.Row = 59;
            Assert.False(GoalEvaluator.IsComplete(g, s));
        }

        [Fact]
        public void FindHiddenGem()
        {
            SimState s = State();
            LevelSpec g = Spec(GoalType.FindHiddenGem, 0, 0, 0);
            s.Gems = 3; // ordinary gems do not count
            Assert.False(GoalEvaluator.IsComplete(g, s));
            s.HiddenGems = 1;
            Assert.True(GoalEvaluator.IsComplete(g, s));
        }

        [Fact]
        public void Speedrun()
        {
            SimState s = State();
            LevelSpec g = Spec(GoalType.Speedrun, 90, 0, 45);
            s.Row = 90;
            s.Tick = 45 * 60;
            Assert.True(GoalEvaluator.IsComplete(g, s));
            s.Tick = 45 * 60 + 1;
            Assert.False(GoalEvaluator.IsComplete(g, s));
            s.Tick = 100;
            s.Row = 89;
            Assert.False(GoalEvaluator.IsComplete(g, s));
        }

        [Fact]
        public void SurviveWaveNeedsTheTopRow()
        {
            SimState s = State();
            LevelSpec g = Spec(GoalType.SurviveWave, 0, 0, 0);
            s.Row = 59;
            Assert.False(GoalEvaluator.IsComplete(g, s));
            s.Row = 60;
            s.Hits = 7; // gongs do not fail the level
            Assert.True(GoalEvaluator.IsComplete(g, s));
        }

        [Fact]
        public void ComboChainCountsJumpsOfEnoughFloors()
        {
            SimConfig cfg = new SimConfig();
            LevelData d = T3.World(cfg, null, null,
                T3.P(1, 1, 1.8, 1, PlatformKind.Normal),
                T3.P(2, 5, 3.6, 8, PlatformKind.Normal),
                T3.P(3, 1, 5.4, 1, PlatformKind.Normal),
                T3.P(4, 5, 7.2, 8, PlatformKind.Normal));
            Sim sim = new Sim(cfg, d, Fx.Zero, false, false);
            SimState s = sim.NewState();
            LevelSpec two = Spec(GoalType.ComboChain, 2, 2, 0);
            LevelSpec three = Spec(GoalType.ComboChain, 3, 1, 0);
            sim.Step(ref s, SimInput.Jump);
            for (int i = 0; i < 200 && s.Row < 2; i++) sim.Step(ref s, 0);
            Assert.False(GoalEvaluator.IsComplete(two, s));
            Assert.Equal(1, GoalEvaluator.JumpsWithFloors(s, 2));
            sim.Step(ref s, SimInput.Jump);
            for (int i = 0; i < 200 && s.Row < 4; i++) sim.Step(ref s, 0);
            Assert.True(GoalEvaluator.IsComplete(two, s));
            Assert.False(GoalEvaluator.IsComplete(three, s), "two 2-floor jumps are not a 3-floor jump");
            Assert.Equal(2, s.BestJumpFloors);
            Assert.Equal(0, GoalEvaluator.JumpsWithFloors(s, 3));
        }

        [Fact]
        public void JumpHistogramIsClampedAndSaturating()
        {
            SimState s = State();
            s.JumpHist = 0xFFUL << 56; // 255 jumps of 8+ floors
            Assert.Equal(255, GoalEvaluator.JumpsWithFloors(s, 8));
            Assert.Equal(255, GoalEvaluator.JumpsWithFloors(s, 12));
            Assert.Equal(255, GoalEvaluator.JumpsWithFloors(s, 1));
            Assert.Equal(255, GoalEvaluator.JumpsWithFloors(s, 0));
        }

        [Fact]
        public void StarsAreTheCollectedStarPickupsOfAClearedLevel()
        {
            LevelSpec spec = Spec(GoalType.ReachFloor, 30, 0, 0);
            SimState s = State();
            s.Row = 29;
            s.StarMask = 7;
            Assert.Equal(0, StarEvaluator.Stars(spec, s, 0, 100));   // goal not complete: nothing counts
            s.Row = 30;
            s.StarMask = 0;
            Assert.Equal(0, StarEvaluator.Stars(spec, s, 0, 100));   // cleared without stars
            s.StarMask = 0b101;
            Assert.Equal(2, StarEvaluator.Stars(spec, s, 0, 100));
            Assert.Equal(0b101, StarEvaluator.Earned(spec, s, 3, 100));   // revives no longer matter
            s.StarMask = 7;
            Assert.Equal(3, StarEvaluator.Stars(spec, s, 0, 100));
        }

        [Fact]
        public void IdMappings()
        {
            Assert.Equal(GoalType.CollectCoins, GoalTypes.FromId("collectCoins"));
            Assert.Equal(GoalType.ComboChain, GoalTypes.FromId("comboChain"));
            Assert.Equal(GoalType.NoWallBounce, GoalTypes.FromId("noWallBounce"));
            Assert.Equal(GoalType.FindHiddenGem, GoalTypes.FromId("findHiddenGem"));
            Assert.Equal(GoalType.Speedrun, GoalTypes.FromId("speedrun"));
            Assert.Equal(GoalType.SurviveWave, GoalTypes.FromId("surviveWave"));
            Assert.Equal(GoalType.BossChase, GoalTypes.FromId("bossChase"));
            Assert.Equal(GoalType.ReachFloor, GoalTypes.FromId("reachFloor"));
            Assert.Equal(StarSpec.KindCoins, StarSpec.KindFromId("coins"));
            Assert.Equal(StarSpec.KindNoRevive, StarSpec.KindFromId("noRevive"));
            Assert.Equal(StarSpec.KindTime, StarSpec.KindFromId("time"));
            Assert.Equal(StarSpec.KindClear, StarSpec.KindFromId("clear"));
        }
    }
}
