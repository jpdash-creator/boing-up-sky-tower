using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>
    /// Owner issue (doc 23, Gameplay 4, CRITICAL): the platform layout must change on EVERY attempt of a level. The campaign run seed is
    /// LevelSpec.MixAttemptSeed(level.Seed, attemptSeed) with a fresh attemptSeed per attempt; attemptSeed 0 is the base layout that the
    /// golden hashes and every audit test pin. These tests prove that a random attempt seed gives a layout that is
    ///   valid (stack rule, same-row separation, rows, goal objects), solvable (LevelBot proof), different from the other seeds,
    ///   reproducible (same seed, same hash), and equally hard (average gap, max gap, special mix and height stay in the level's band).
    /// Stars, goals and coin targets live in the LevelSpec, which a new seed never touches (asserted too).
    /// </summary>
    [Trait("Category", "RandomLayout")]
    public class RandomLayoutTests
    {
        private readonly ITestOutputHelper output;
        public RandomLayoutTests(ITestOutputHelper output) { this.output = output; }

        /// <summary>Attempt seeds tried per level (the task asks for at least 20).</summary>
        public const int SeedsPerLevel = 20;

        /// <summary>The fixed list of attempt seeds: 1..N-th values of a mixed sequence, so the test is deterministic but not "small ints".</summary>
        public static int AttemptSeed(int i) { return unchecked((int)(0x6D2B79F5u * (uint)(i + 1)) ^ 0x1234567); }

        private static List<LoadedLevel> Levels()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return new List<LoadedLevel>();
            return ContentLoader.LoadRegions(root, 1, 5);
        }

        // ---------------------------------------------------------------- metrics

        public sealed class Metrics
        {
            public int Platforms, Special, Crumble, Rows;
            public double AvgGap, MaxGap, Height;
            public long Hash;
            public int Stars, Gems, Hazards;
        }

        private static bool IsSpecial(PlatformKind k) { return k != PlatformKind.Normal; }

        public static Metrics Measure(LevelData d)
        {
            Metrics m = new Metrics();
            m.Platforms = d.Platforms.Length;
            m.Rows = d.Rows;
            m.Hash = d.ComputeHash();
            m.Hazards = d.Hazards.Length;
            foreach (Pickup p in d.Pickups)
            {
                if (p.Kind == PickupKind.Star) m.Stars++;
                else if (p.Kind == PickupKind.Gem) m.Gems++;
            }
            // Spine gap = height difference between the first platform of consecutive rows.
            double prevY = 0, sum = 0, max = 0;
            int prevRow = 0, n = 0;
            int lastSpineRow = 0;
            foreach (Platform p in d.Platforms)
            {
                // Doc 25: Special counts the SPINE platform of each row (the first of its row); row mates are Normal or Crumble and count as Crumble.
                bool spine = p.Row != lastSpineRow;
                lastSpineRow = p.Row;
                if (spine) { if (IsSpecial(p.Kind)) m.Special++; }
                else if (p.Kind == PlatformKind.Crumble) m.Crumble++;
                if (p.Row == prevRow + 1)
                {
                    double y = p.Y.Raw / 65536.0;
                    double g = y - prevY;
                    sum += g; n++;
                    if (g > max) max = g;
                    prevY = y; prevRow = p.Row;
                    m.Height = y;
                }
            }
            m.AvgGap = n > 0 ? sum / n : 0;
            m.MaxGap = max;
            return m;
        }

        /// <summary>Layout validity that does not need the bot: returns null when fine, else a description.</summary>
        public static string Validate(SimConfig cfg, LevelSpec spec, LevelData d, Metrics baseM)
        {
            if (d.Rows != spec.RowsFor()) return "rows " + d.Rows + " != " + spec.RowsFor();
            if (d.Platforms.Length < d.Rows) return "fewer platforms than rows";
            string v = StackRuleTests.FindViolation(d.Platforms);
            if (v != null) return "stack rule: " + v;
            Fx screen = cfg.ScreenWidth;
            for (int i = 0; i < d.Platforms.Length; i++)
            {
                Platform p = d.Platforms[i];
                if (p.Width.Raw >= 9 * 65536) continue;
                Fx ext = p.Width / 2 + p.Amplitude;
                if (p.X - ext < Fx.Zero || p.X + ext > screen) return "platform " + p.Id + " outside the walls";
                if (i > 0 && d.Platforms[i - 1].Row == p.Row && d.Platforms[i - 1].Width.Raw < 9 * 65536)
                {
                    Platform q = d.Platforms[i - 1];
                    Fx gap = Fx.Abs(p.X - q.X) - p.Width / 2 - p.Amplitude - q.Width / 2 - q.Amplitude;
                    if (gap < Fx.FromRatio(5, 100)) return "platforms " + q.Id + "/" + p.Id + " touch on row " + p.Row + " [" + q.Kind + " x=" + q.X + " w=" + q.Width + " amp=" + q.Amplitude + "] [" + p.Kind + " x=" + p.X + " w=" + p.Width + " amp=" + p.Amplitude + "]";
                }
            }
            Metrics m = Measure(d);
            if (m.Stars != baseM.Stars) return "star pickups " + m.Stars + " != " + baseM.Stars;
            if (spec.Goal.Type == GoalType.FindHiddenGem && d.HiddenGemRow <= 0) return "hidden gem missing";
            if (spec.Goal.Type == GoalType.ComboChain && d.LadderStartRow < 0) return "combo ladder missing";
            if (spec.Goal.Type == GoalType.SurviveWave && d.WaveEndRow <= d.WaveStartRow) return "wave missing";
            return null;
        }

        private static int Budget(LevelSpec spec) { return SolvabilityT4Tests.NodeBudget * Math.Max(1, spec.RowsFor() / 60); }

        // ---------------------------------------------------------------- tests

        [Fact]
        public void AttemptSeedZeroIsTheBaseLayoutAndTheSpecIsOtherwiseUntouched()
        {
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in Levels())
            {
                LevelSpec s0 = ll.Spec.WithAttemptSeed(0);
                Assert.Equal(ll.Spec.Seed, s0.Seed);
                Assert.Equal(LevelBuilder.Build(cfg, ll.Spec).ComputeHash(), LevelBuilder.Build(cfg, s0).ComputeHash());
                LevelSpec s1 = ll.Spec.WithAttemptSeed(AttemptSeed(0));
                Assert.NotEqual(ll.Spec.Seed, s1.Seed);
                Assert.Equal(AttemptSeed(0), s1.AttemptSeed);
                // Everything but the seed is the level's: difficulty, goal, star targets, signature, chunks, rows.
                Assert.Equal(ll.Spec.Difficulty, s1.Difficulty);
                Assert.Equal(ll.Spec.LevelIndex, s1.LevelIndex);
                Assert.Equal(ll.Spec.Class, s1.Class);
                Assert.Equal(ll.Spec.Signature, s1.Signature);
                Assert.Equal(ll.Spec.Ramp, s1.Ramp);
                Assert.Equal(ll.Spec.Rows, s1.Rows);
                Assert.Equal(ll.Spec.RowsFor(), s1.RowsFor());
                Assert.Equal(ll.Spec.Goal.Type, s1.Goal.Type);
                Assert.Equal(ll.Spec.Goal.Value, s1.Goal.Value);
                Assert.Equal(ll.Spec.Goal.Count, s1.Goal.Count);
                Assert.Equal(ll.Spec.Goal.TimeSec, s1.Goal.TimeSec);
                Assert.Equal(ll.Spec.Stars.Length, s1.Stars.Length);
                for (int i = 0; i < ll.Spec.Stars.Length; i++)
                {
                    Assert.Equal(ll.Spec.Stars[i].Kind, s1.Stars[i].Kind);
                    Assert.Equal(ll.Spec.Stars[i].Value, s1.Stars[i].Value);
                }
                Assert.Equal(ll.Spec.FixedChunks, s1.FixedChunks);
            }
        }

        [Fact]
        public void MixedSeedsAreStableAndSpreadOut()
        {
            Assert.Equal(LevelSpec.MixAttemptSeed(123, 77), LevelSpec.MixAttemptSeed(123, 77));
            HashSet<int> seen = new HashSet<int>();
            for (int a = 1; a <= 1000; a++) seen.Add(LevelSpec.MixAttemptSeed(4242, a));
            Assert.True(seen.Count >= 999, "mixed seeds collide: " + seen.Count);
            Assert.Equal(55, LevelSpec.MixAttemptSeed(55, 0));
        }

        [Fact]
        public void SameAttemptSeedGivesTheIdenticalLayoutAndHash()
        {
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in Levels())
            {
                for (int i = 0; i < 3; i++)
                {
                    LevelSpec s = ll.Spec.WithAttemptSeed(AttemptSeed(i));
                    LevelData a = LevelBuilder.Build(cfg, s), b = LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(AttemptSeed(i)));
                    Assert.Equal(a.ComputeHash(), b.ComputeHash());
                    Assert.Equal(a.Platforms.Length, b.Platforms.Length);
                    for (int p = 0; p < a.Platforms.Length; p++)
                    {
                        Assert.Equal(a.Platforms[p].X, b.Platforms[p].X);
                        Assert.Equal(a.Platforms[p].Y, b.Platforms[p].Y);
                        Assert.Equal(a.Platforms[p].Kind, b.Platforms[p].Kind);
                    }
                }
            }
        }

        /// <summary>Fraction of the platforms (by row) whose x differs by more than half a unit, or whose kind differs, between two layouts.</summary>
        public static double DifferingShare(LevelData a, LevelData b)
        {
            int n = Math.Min(a.Platforms.Length, b.Platforms.Length), diff = 0;
            for (int i = 0; i < n; i++)
            {
                Platform p = a.Platforms[i], q = b.Platforms[i];
                if (p.Kind != q.Kind || Fx.Abs(p.X - q.X) > Fx.Half || Fx.Abs(p.Y - q.Y) > Fx.Half) diff++;
            }
            return n == 0 ? 0 : diff / (double)n;
        }

        [Fact]
        public void LayoutsDifferBetweenSeedsAndFromTheBaseLayout()
        {
            SimConfig cfg = new SimConfig();
            List<string> bad = new List<string>();
            double worst = 1;
            foreach (LoadedLevel ll in Levels())
            {
                LevelData baseData = LevelBuilder.Build(cfg, ll.Spec);
                LevelData[] data = new LevelData[SeedsPerLevel];
                HashSet<long> hashes = new HashSet<long>();
                for (int i = 0; i < SeedsPerLevel; i++)
                {
                    data[i] = LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(AttemptSeed(i)));
                    hashes.Add(data[i].ComputeHash());
                    double vsBase = DifferingShare(baseData, data[i]);
                    worst = Math.Min(worst, vsBase);
                    if (vsBase < 0.5) bad.Add(ll.Id + " seed " + i + " vs base: only " + (vsBase * 100).ToString("F0") + "% differ");
                    if (i > 0)
                    {
                        double vsPrev = DifferingShare(data[i - 1], data[i]);
                        worst = Math.Min(worst, vsPrev);
                        if (vsPrev < 0.5) bad.Add(ll.Id + " seed " + i + " vs seed " + (i - 1) + ": only " + (vsPrev * 100).ToString("F0") + "% differ");
                    }
                }
                if (hashes.Count != SeedsPerLevel) bad.Add(ll.Id + ": " + (SeedsPerLevel - hashes.Count) + " duplicate layouts");
            }
            output.WriteLine("least different pair: " + (worst * 100).ToString("F0") + "% of the platforms");
            Assert.True(bad.Count == 0, bad.Count + " problems, first: " + (bad.Count > 0 ? bad[0] : ""));
        }

        [Fact]
        public void RandomLayoutsAreValidAndStayInTheLevelsDifficultyBand()
        {
            SimConfig cfg = new SimConfig();
            List<string> bad = new List<string>();
            foreach (LoadedLevel ll in Levels())
            {
                LevelData baseData = LevelBuilder.Build(cfg, ll.Spec);
                Metrics bm = Measure(baseData);
                // Reference mix: independent draws of the same level (the original builder with other seeds), 20 of them. A random attempt must
                // land near the mean of these, not near one lucky draw.
                double refSp = 0, refCr = 0, refHz = 0;
                for (int i = 0; i < SeedsPerLevel; i++)
                {
                    LevelSpec rs = ll.Spec.WithAttemptSeed(AttemptSeed(i + 100));
                    rs.AttemptSeed = 0;
                    Metrics rm = Measure(LevelBuilder.Build(cfg, rs));
                    refSp += rm.Special; refCr += rm.Crumble; refHz += rm.Hazards;
                }
                refSp /= SeedsPerLevel; refCr /= SeedsPerLevel; refHz /= SeedsPerLevel;
                // Band: average gap within 15 percent of the base layout's (both are random draws of the same ranges), the max gap never above
                // the jump limit the builder enforces, special share within 12 platforms or 60 percent of the base, height within 12 percent.
                Fx limit = cfg.NormalGapMax;
                int minSp = int.MaxValue, maxSp = 0, minCr = int.MaxValue, maxCr = 0, minHz = int.MaxValue, maxHz = 0;
                for (int i = 0; i < SeedsPerLevel; i++)
                {
                    LevelSpec s = ll.Spec.WithAttemptSeed(AttemptSeed(i));
                    LevelData d = LevelBuilder.Build(cfg, s);
                    string v = Validate(cfg, s, d, bm);
                    if (v != null) { bad.Add(ll.Id + " seed " + i + ": " + v); continue; }
                    Metrics m = Measure(d);
                    string tag = ll.Id + " seed " + i + ": ";
                    minSp = Math.Min(minSp, m.Special); maxSp = Math.Max(maxSp, m.Special);
                    minCr = Math.Min(minCr, m.Crumble); maxCr = Math.Max(maxCr, m.Crumble);
                    minHz = Math.Min(minHz, m.Hazards); maxHz = Math.Max(maxHz, m.Hazards);
                    if (Math.Abs(m.AvgGap - bm.AvgGap) > 0.15 * bm.AvgGap) bad.Add(tag + "avg gap " + m.AvgGap.ToString("F2") + " vs base " + bm.AvgGap.ToString("F2"));
                    if (m.MaxGap > limit.Raw / 65536.0 + 0.001) bad.Add(tag + "max gap " + m.MaxGap.ToString("F2") + " over the limit " + (limit.Raw / 65536.0).ToString("F2"));
                    if (Math.Abs(m.Height - bm.Height) > 0.12 * bm.Height) bad.Add(tag + "height " + m.Height.ToString("F1") + " vs base " + bm.Height.ToString("F1"));
                }
                // Same special mix in every attempt: chunk-free levels hold exactly the same number of signature platforms, chunk levels (fixed
                // authored rows with their own flips) within 3; the base layout (independent draws) within a wide band of that number.
                int spreadTol = ll.Spec.FixedChunks.Length > 0 ? 3 : 0;
                if (maxSp - minSp > spreadTol) bad.Add(ll.Id + ": special platform count differs between seeds (" + minSp + ".." + maxSp + ")");
                if (Math.Abs(minSp - refSp) > Math.Max(4, 0.15 * refSp)) bad.Add(ll.Id + ": special count " + minSp + " vs independent-draw mean " + refSp.ToString("F1"));
                if (maxCr > 2.5 * refCr + 8) bad.Add(ll.Id + ": crumble count up to " + maxCr + " vs independent-draw mean " + refCr.ToString("F1"));
                if ((refHz > 0) != (maxHz > 0) && refHz >= 1 || (refHz >= 3 && minHz == 0)) bad.Add(ll.Id + ": hazards " + minHz + ".." + maxHz + " vs independent-draw mean " + refHz.ToString("F1"));
                if (refHz > 0 && (maxHz - minHz > Math.Max(12, 0.45 * refHz) || Math.Abs(0.5 * (minHz + maxHz) - refHz) > Math.Max(5, 0.25 * refHz)))
                    bad.Add(ll.Id + ": hazard count " + minHz + ".." + maxHz + " vs independent-draw mean " + refHz.ToString("F1"));
                output.WriteLine(ll.Id + " special ref " + refSp.ToString("F1") + " seeds " + minSp + ".." + maxSp + " | crumble ref " + refCr.ToString("F1") + " seeds " + minCr + ".." + maxCr + " | hazards ref " + refHz.ToString("F1") + " seeds " + minHz + ".." + maxHz);
            }
            foreach (string s in bad.Take(40)) output.WriteLine(s);
            Assert.True(bad.Count == 0, bad.Count + " problems, first: " + (bad.Count > 0 ? bad[0] : ""));
        }

        /// <summary>The trickiest goal for random layouts (side platform, bounce neighbours): more seeds for the hidden-gem levels only.</summary>
        [Fact]
        public void BotSolvesHiddenGemLevelsForManySeeds()
        {
            SimConfig cfg = new SimConfig();
            // Doc 25: no level has the hidden gem search as its goal any more; level 8 keeps the hidden bonus gem, so the same layouts are solved with that
            // goal swapped in (the bot must be able to take the bonus gem).
            List<LoadedLevel> levels = Levels().Where(l => l.Spec.LevelIndex == 8).ToList();
            Assert.True(levels.Count >= 5);
            const int Seeds = 30;
            string[][] failures = new string[levels.Count][];
            Parallel.For(0, levels.Count, li =>
            {
                LoadedLevel ll = levels[li];
                List<string> f = new List<string>();
                for (int i = 0; i < Seeds; i++)
                {
                    LevelSpec s = ll.Spec.WithAttemptSeed(AttemptSeed(i + 500));
                    s.Rows = 120;
                    s.Goal = new GoalSpec { Type = GoalType.FindHiddenGem };
                    LevelData d = LevelBuilder.Build(cfg, s);
                    BotResult r = new LevelBot(new Sim(cfg, d, s), s, d, Budget(s)).Solve();
                    if (!r.Solved) f.Add(ll.Id + " attempt seed " + AttemptSeed(i + 500) + ": best row " + r.BestRow);
                }
                failures[li] = f.ToArray();
            });
            List<string> all = failures.SelectMany(x => x).ToList();
            foreach (string s in all) output.WriteLine(s);
            Assert.True(all.Count == 0, all.Count + " unsolved hidden gem layouts, first: " + (all.Count > 0 ? all[0] : ""));
        }

        [Fact]
        public void NoBounceInTheLastThreeRowsOfRandomLayouts()
        {
            SimConfig cfg = new SimConfig();
            List<string> bad = new List<string>();
            foreach (LoadedLevel ll in Levels())
                for (int i = 0; i < SeedsPerLevel; i++)
                {
                    LevelData d = LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(AttemptSeed(i)));
                    foreach (Platform p in d.Platforms)
                        if (p.Row > d.Rows - 3 && (p.Kind == PlatformKind.Bounce || p.Kind == PlatformKind.BounceCap) && ll.Spec.FixedChunks.Length == 0)
                            bad.Add(ll.Id + " seed " + i + " row " + p.Row);
                }
            Assert.True(bad.Count == 0, bad.Count + " bounce platforms below the goal row, first: " + (bad.Count > 0 ? bad[0] : ""));
        }

        [Fact]
        public void BotSolvesEveryRandomLayoutOfAll75Levels()
        {
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = Levels();
            string[][] failures = new string[levels.Count][];
            Parallel.For(0, levels.Count, li =>
            {
                LoadedLevel ll = levels[li];
                List<string> f = new List<string>();
                for (int i = 0; i < SeedsPerLevel; i++)
                {
                    LevelSpec s = ll.Spec.WithAttemptSeed(AttemptSeed(i));
                    LevelData d = LevelBuilder.Build(cfg, s);
                    Sim sim = new Sim(cfg, d, s);
                    BotResult r = new LevelBot(sim, s, d, Budget(s)).Solve();
                    if (!r.Solved) f.Add(ll.Id + " attempt seed " + AttemptSeed(i) + " (mixed " + s.Seed + "): bot did not solve, best row " + r.BestRow + "/" + d.Rows);
                    else if (!GoalEvaluator.IsComplete(s, r.Final)) f.Add(ll.Id + " attempt seed " + AttemptSeed(i) + ": solved run does not meet the goal");
                }
                failures[li] = f.ToArray();
            });
            List<string> all = failures.SelectMany(x => x).ToList();
            foreach (string s in all) output.WriteLine(s);
            output.WriteLine("levels x seeds checked: " + levels.Count + " x " + SeedsPerLevel + ", failures: " + all.Count);
            Assert.Equal(75, levels.Count);
            Assert.True(all.Count == 0, all.Count + " unsolved random layouts, first: " + (all.Count > 0 ? all[0] : ""));
        }
    }
}
