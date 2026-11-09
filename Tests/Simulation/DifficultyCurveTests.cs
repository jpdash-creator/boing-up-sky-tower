using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>
    /// Bug List 6 item 4: the new difficulty curve of the 25-level regions. DIFFICULTY SCORE 0..100 (defined by the owner/lead from the old region 1 report):
    /// five measured parts, each normalised between the easiest and the hardest of the OLD region 1 levels 1..15 (ok % and width inverted):
    ///   precision = (61.8 - ok%) / (61.8 - 32.5) x 0.35;  width = (4.13 - width) / (4.13 - 2.65) x 0.20;  scroll = (scroll - 0.69) / (2.40 - 0.69) x 0.20;
    ///   special = (special% - 20) / (59 - 20) x 0.15;  gap = (gap - 2.62) / (3.73 - 2.62) x 0.10;  composite c = sum;
    ///   score = 5 + 95 x (c - c_L1) / (c_L15 - c_L1), c_L1 and c_L15 = the composites of OLD region 1 level 1 and 15.
    /// The measurement is DifficultyReportTests.Measure's probe (default hero Momo, 3 attempt seeds i + 300) without the bot solve.
    /// Gated tests (category Difficulty, environment variables, not part of a normal run):
    ///   CALIBRATE_CURVE=1  solves the content difficulty of every level of regions 1 to 5 for the target score and writes Content/config/difficulty_curve.json
    ///                      (then run python Tools/content_cli/migrate_levels_25.py to put it into the level files);
    ///   DIFFICULTY_CURVE=1 measures the shipped levels of regions 1 to 5 and writes Tests/Simulation/DIFFICULTY_CURVE_REPORT.md (target vs measured, max error 3).
    /// </summary>
    [Trait("Category", "Difficulty")]
    public class DifficultyCurveTests
    {
        private readonly ITestOutputHelper output;
        public DifficultyCurveTests(ITestOutputHelper output) { this.output = output; }

        public static readonly double[] Target =
        {
            5, 20, 26.7, 33.3, 40, 44, 48, 52, 56, 60, 63, 66, 69, 72, 75, 77.5, 80, 82.5, 85, 87.5, 90, 92.5, 95, 97.5, 100
        };
        public const double MaxError = 3.0;
        /// <summary>
        /// KNOWN LIMIT (reported to the owner): Candy Canyon (region 4, sticky gum) cannot reach the top of the curve even at content difficulty 1.0 (its hardest measured score is
        /// about 93: the sticky platforms keep the probe's ok % at 37 and the platforms wider than the other regions at the same d). These levels are held to "as close as the
        /// maximum difficulty allows" (within 8 points and at least 90) instead of 3 until another lever is chosen (a higher special share or a region 4 specific step).
        /// </summary>
        public static readonly string[] CappedLevels = { "lvl_004_23", "lvl_004_24", "lvl_004_25" };

        /// <summary>Old region 1 level 1 and level 15 (old DIFFICULTY_REPORT values: gap, width, special %, scroll, ok %).</summary>
        static readonly double[] OldL1 = { 2.62, 4.13, 20, 0.69, 61.8 };
        static readonly double[] OldL15 = { 3.56, 2.65, 59, 2.40, 33.7 };

        public static double Composite(double gap, double width, double special, double scroll, double ok)
        {
            return 0.35 * (61.8 - ok) / (61.8 - 32.5) + 0.20 * (4.13 - width) / (4.13 - 2.65) + 0.20 * (scroll - 0.69) / (2.40 - 0.69)
                 + 0.15 * (special - 20) / (59 - 20) + 0.10 * (gap - 2.62) / (3.73 - 2.62);
        }

        public static double Score(double gap, double width, double special, double scroll, double ok)
        {
            double c1 = Composite(OldL1[0], OldL1[1], OldL1[2], OldL1[3], OldL1[4]);
            double c15 = Composite(OldL15[0], OldL15[1], OldL15[2], OldL15[3], OldL15[4]);
            return 5 + 95 * (Composite(gap, width, special, scroll, ok) - c1) / (c15 - c1);
        }

        public sealed class Meas { public double Gap, Width, Special, Scroll, Ok, Score; }

        /// <summary>Spine statistics and the option probe of <paramref name="seeds"/> attempt layouts of the spec (same numbers as DifficultyReportTests.Measure).</summary>
        public static Meas Measure(SimConfig cfg, LevelSpec spec, int seeds)
        {
            double gap = 0, width = 0, scroll = 0, ok = 0, spineShare = 0;
            long n = 0; int okN = 0;
            for (int i = 0; i < seeds; i++)
            {
                LevelSpec s = spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i + 300));
                LevelData d = LevelBuilder.Build(cfg, s);
                Sim sim = new Sim(cfg, d, s);
                scroll += DifficultyReportTests.BaseScroll(cfg, s);
                double prevY = 0; int prevRow = 0, sn = 0, sp = 0;
                foreach (Platform p in d.Platforms)
                    if (p.Row == prevRow + 1)
                    {
                        double y = p.Y.Raw / 65536.0;
                        gap += y - prevY; width += p.Width.Raw / 65536.0; n++; sn++;
                        if (p.Kind != PlatformKind.Normal && p.Kind != PlatformKind.Crumble) sp++;
                        prevY = y; prevRow = p.Row;
                    }
                spineShare += sp / (double)Math.Max(1, sn);
                LevelBot probe = new LevelBot(sim, s, d, 1000);
                int[] clocks = { 0, 101, 233 };
                int lastRow = -1;
                double okSum = 0; int okCount = 0;
                for (int pi = 1; pi < d.Platforms.Length; pi++)
                {
                    Platform p = d.Platforms[pi];
                    if (p.Row == lastRow || p.Row > d.Rows - 3) continue;
                    lastRow = p.Row;
                    foreach (int t0 in clocks)
                    {
                        SimState st = sim.NewState();
                        st.Tick = t0; st.StartTick = t0;
                        st.GroundId = pi; st.Grounded = true; st.Row = p.Row; st.BestGroundId = pi;
                        st.X = sim.PlatformX(pi, t0); st.Y = p.Y; st.MaxY = p.Y; st.KillY = p.Y - Fx.FromInt(60);
                        int tried, prog;
                        probe.MeasureOptions(st, out tried, out prog);
                        if (tried > 0) { okSum += prog / (double)tried; okCount++; }
                    }
                }
                ok += okSum / Math.Max(1, okCount); okN++;
            }
            Meas m = new Meas();
            m.Gap = gap / Math.Max(1, n); m.Width = width / Math.Max(1, n); m.Special = spineShare / seeds * 100; m.Scroll = scroll / seeds;
            m.Ok = ok / Math.Max(1, okN) * 100;
            m.Score = Score(m.Gap, m.Width, m.Special, m.Scroll, m.Ok);
            return m;
        }

        /// <summary>The spec of a level with the content difficulty <paramref name="permille"/> (loader rule: region offset applied).</summary>
        static LevelSpec WithContentD(LoadedLevel ll, Fx offset, int permille)
        {
            LevelSpec s = ll.Spec.WithAttemptSeed(0);
            s.Difficulty = LevelGenerator.EffectiveDifficulty(Fx.FromRatio(permille, 1000), offset);
            return s;
        }

        static Fx OffsetOf(string root, int region)
        {
            string prefix = "region_" + region.ToString("D3") + "_";
            string file = Directory.GetFiles(Path.Combine(root, "Content", "regions"), prefix + "*.json")[0];
            using (System.Text.Json.JsonDocument rd = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file)))
            {
                double off = rd.RootElement.GetProperty("rules").GetProperty("difficultyOffset").GetDouble();
                return Fx.FromRatio((int)Math.Round(off * 10000), 10000);
            }
        }

        static int Threads()
        {
            return int.Parse(Environment.GetEnvironmentVariable("CURVE_THREADS") ?? "6");
        }

        [Fact]
        public void ScoreFormulaReproducesTheOldRegion1Numbers()
        {
            // old region 1 levels 1..15 (gap, width, special %, scroll, ok %) from the old DIFFICULTY_REPORT; the lead's scores
            double[][] old =
            {
                new[] { 2.62, 4.13, 20, 0.69, 61.8 }, new[] { 2.70, 3.95, 39, 0.78, 56.7 }, new[] { 3.02, 3.67, 39, 1.14, 50.3 }, new[] { 3.15, 3.71, 39, 1.24, 48.3 },
                new[] { 3.06, 3.86, 32, 1.35, 49.6 }, new[] { 3.39, 3.46, 48, 1.45, 43.8 }, new[] { 3.45, 3.46, 48, 1.56, 41.0 }, new[] { 3.54, 3.24, 48, 1.66, 39.5 },
                new[] { 3.64, 3.29, 48, 1.77, 38.9 }, new[] { 3.47, 3.08, 43, 1.87, 37.8 }, new[] { 3.73, 3.03, 49, 1.98, 35.4 }, new[] { 3.69, 2.91, 49, 2.08, 35.7 },
                new[] { 3.70, 2.79, 49, 2.19, 34.3 }, new[] { 3.67, 2.74, 49, 2.29, 32.5 }, new[] { 3.56, 2.65, 59, 2.40, 33.7 }
            };
            int[] expect = { 5, 22, 40, 44, 39, 61, 66, 73, 75, 77, 86, 88, 93, 96, 100 };
            for (int i = 0; i < 15; i++)
                Assert.Equal(expect[i], (int)Math.Round(Score(old[i][0], old[i][1], old[i][2], old[i][3], old[i][4])));
        }

        [Fact]
        public void Calibrate()
        {
            if (Environment.GetEnvironmentVariable("CALIBRATE_CURVE") != "1") return;
            string root = ContentLoader.FindRoot();
            SimConfig cfg = new SimConfig();
            int seeds = int.Parse(Environment.GetEnvironmentVariable("CURVE_SEEDS") ?? "3");
            string only = Environment.GetEnvironmentVariable("CURVE_REGIONS") ?? "1,2,3,4,5";
            int[] regions = only.Split(',').Select(int.Parse).ToArray();
            List<LoadedLevel> levels = new List<LoadedLevel>();
            foreach (int r in regions) levels.AddRange(ContentLoader.LoadRegions(root, r, r));
            // CURVE_LEVELS=5,10,15,20: solve only these level indexes (raw, no monotone fit) and merge them into the existing curve file
            string only2 = Environment.GetEnvironmentVariable("CURVE_LEVELS");
            if (!string.IsNullOrEmpty(only2)) { int[] idxs = only2.Split(',').Select(int.Parse).ToArray(); levels = levels.Where(l => idxs.Contains(l.Spec.LevelIndex)).ToList(); }
            Dictionary<string, int> result = new Dictionary<string, int>();
            Dictionary<string, double> got = new Dictionary<string, double>();
            object lk = new object();
            Parallel.ForEach(levels, new ParallelOptions { MaxDegreeOfParallelism = Threads() }, ll =>
            {
                Fx off = OffsetOf(root, ll.Region);
                double target = Target[ll.Spec.LevelIndex - 1];
                int lo = 0, hi = 1000, best = -1; double bestErr = 1e9, bestScore = 0;
                Dictionary<int, double> seen = new Dictionary<int, double>();
                Func<int, double> f = p => { double v; if (!seen.TryGetValue(p, out v)) { v = Measure(cfg, WithContentD(ll, off, p), seeds).Score; seen[p] = v; } return v; };
                while (lo <= hi)
                {
                    int mid = (lo + hi) / 2;
                    double sc = f(mid);
                    if (Math.Abs(sc - target) < bestErr) { bestErr = Math.Abs(sc - target); best = mid; bestScore = sc; }
                    if (sc < target) lo = mid + 1; else hi = mid - 1;
                }
                // local refinement around the best point (scores are not perfectly monotone in d)
                for (int p = Math.Max(0, best - 6); p <= Math.Min(1000, best + 6); p++)
                {
                    double sc = f(p);
                    if (Math.Abs(sc - target) < bestErr) { bestErr = Math.Abs(sc - target); best = p; bestScore = sc; }
                }
                lock (lk) { result[ll.Id] = best; got[ll.Id] = bestScore; }
            });
            // Monotone d map: the plain levels (reach the top) are fitted with an isotonic regression (pool adjacent violators) per region and then kept at
            // least MinStep permille apart; the special-goal levels (5, 10, 15, 20: combo ladder, no wall, hidden gem, speedrun) take their own solved d (so their
            // measured score is on the curve), see below.
            const int MinStep = 3;
            foreach (int region in string.IsNullOrEmpty(only2) ? regions : new int[0])
            {
                string[] ids = levels.Where(l => l.Region == region).OrderBy(l => l.Spec.LevelIndex).Select(l => l.Id).ToArray();
                int[] raw = ids.Select(id => result[id]).ToArray();
                int[] fit = new int[ids.Length];
                List<int> plain = Enumerable.Range(0, ids.Length).Where(i => !((i + 1) % 5 == 0 && i + 1 <= 20)).ToList();
                // PAV
                List<double> val = new List<double>(); List<int> cnt = new List<int>();
                foreach (int i in plain) { val.Add(raw[i]); cnt.Add(1); while (val.Count > 1 && val[val.Count - 2] > val[val.Count - 1]) { int c = cnt[cnt.Count - 2] + cnt[cnt.Count - 1]; double v = (val[val.Count - 2] * cnt[cnt.Count - 2] + val[val.Count - 1] * cnt[cnt.Count - 1]) / c; val.RemoveRange(val.Count - 2, 2); cnt.RemoveRange(cnt.Count - 2, 2); val.Add(v); cnt.Add(c); } }
                int pos = 0; int prevD = -1000;
                for (int b = 0; b < val.Count; b++)
                    for (int k = 0; k < cnt[b]; k++) { int d = (int)Math.Round(val[b]); if (d < prevD + MinStep) d = prevD + MinStep; fit[plain[pos]] = d; prevD = d; pos++; }
                // specials keep their own solved d: a combo ladder, a no-wall rule, a hidden gem or a speedrun changes what the probe measures, so the same score needs
                // another d (the endless cycle does not use these values, it reads the smooth SimConfig table)
                for (int i = 0; i < ids.Length; i++) if (!plain.Contains(i)) fit[i] = raw[i];
                for (int i = 0; i < ids.Length; i++) if (fit[i] > 1000) fit[i] = 1000;
                SimConfig c2 = cfg;
                Fx off = OffsetOf(root, region);
                for (int i = 0; i < ids.Length; i++)
                {
                    LoadedLevel ll = levels.First(l => l.Id == ids[i]);
                    got[ids[i]] = Measure(c2, WithContentD(ll, off, fit[i]), seeds).Score;
                    result[ids[i]] = fit[i];
                }
            }
            string path = Path.Combine(root, "Content", "config", "difficulty_curve.json");
            SortedDictionary<string, int> merged = new SortedDictionary<string, int>(StringComparer.Ordinal);
            if (File.Exists(path))
                using (System.Text.Json.JsonDocument d0 = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)))
                    foreach (var kv in d0.RootElement.GetProperty("d").EnumerateObject()) merged[kv.Name] = (int)Math.Round(kv.Value.GetDouble() * 1000);
            foreach (var kv in result) merged[kv.Key] = kv.Value;
            StringBuilder sb = new StringBuilder();
            sb.Append("{\n  \"version\": 1,\n  \"note\": \"Content difficulty d per level (Bug List 6 item 4), solved by DifficultyCurveTests.Calibrate for the target scores; applied by Tools/content_cli/migrate_levels_25.py\",\n  \"targets\": [");
            sb.Append(string.Join(", ", Target.Select(t => t.ToString("0.0#", CultureInfo.InvariantCulture))));
            sb.Append("],\n  \"d\": {\n");
            sb.Append(string.Join(",\n", merged.Select(kv => "    \"" + kv.Key + "\": " + (kv.Value / 1000.0).ToString("0.0##", CultureInfo.InvariantCulture))));
            sb.Append("\n  }\n}\n");
            File.WriteAllText(path, sb.ToString());
            foreach (var kv in result.OrderBy(k => k.Key)) output.WriteLine(kv.Key + " d=" + kv.Value + " score=" + got[kv.Key].ToString("F1") + " target=" + Target[int.Parse(kv.Key.Substring(8)) - 1]);
            foreach (int r in regions)
                output.WriteLine("region " + r + " max error " + result.Keys.Where(k => k.StartsWith("lvl_" + r.ToString("D3"))).Max(k => Math.Abs(got[k] - Target[int.Parse(k.Substring(8)) - 1])).ToString("F2"));
        }

        [Fact]
        public void ProbeTopLevelComponents()
        {
            if (Environment.GetEnvironmentVariable("PROBE_TOP") != "1") return;
            string root = ContentLoader.FindRoot();
            SimConfig cfg = new SimConfig();
            foreach (int r in new[] { 1, 2, 3, 4, 5 })
            {
                LoadedLevel ll = ContentLoader.LoadRegions(root, r, r).First(l => l.Spec.LevelIndex == 25);
                Meas m = Measure(cfg, WithContentD(ll, OffsetOf(root, r), 1000), 3);
                output.WriteLine("region " + r + " L25 d=1: gap " + m.Gap.ToString("F2") + " width " + m.Width.ToString("F2") + " special " + m.Special.ToString("F0") + " scroll " + m.Scroll.ToString("F2") + " ok " + m.Ok.ToString("F1") + " score " + m.Score.ToString("F1"));
            }
        }

        [Fact]
        public void WriteCurveReport()
        {
            if (Environment.GetEnvironmentVariable("DIFFICULTY_CURVE") != "1") return;
            string root = ContentLoader.FindRoot();
            SimConfig cfg = new SimConfig();
            int seeds = int.Parse(Environment.GetEnvironmentVariable("CURVE_SEEDS") ?? "3");
            List<LoadedLevel> levels = ContentLoader.LoadRegions(root, 1, 5);
            Assert.Equal(125, levels.Count);
            Meas[] ms = new Meas[levels.Count];
            Parallel.For(0, levels.Count, new ParallelOptions { MaxDegreeOfParallelism = Threads() }, i => { ms[i] = Measure(cfg, levels[i].Spec, seeds); });
            CultureInfo ic = CultureInfo.InvariantCulture;
            StringBuilder md = new StringBuilder();
            md.AppendLine("# Difficulty curve report (regions 1-5, 25 levels each)");
            md.AppendLine();
            md.AppendLine("Generated by DifficultyCurveTests.WriteCurveReport (DIFFICULTY_CURVE=1; " + seeds + " attempt layouts per level, default hero Momo). Score = the difficulty score 0..100 defined in the class comment of DifficultyCurveTests (old region 1 level 1 = 5, old level 15 = 100). Targets: L1 5, L2 20, L3 26.7, L4 33.3, L5 40, L6 44, L7 48, L8 52, L9 56, L10 60, L11 63, L12 66, L13 69, L14 72, L15 75, then +2.5 per level to L25 100. Allowed error: +-" + MaxError.ToString("0.#", ic) + " points.");
            md.AppendLine();
            md.AppendLine("| level | goal | d (eff) | gap | width | special % | scroll | ok % | target | measured | error |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            double maxErr = 0; string worst = "";
            double[] regionMax = new double[6];
            for (int i = 0; i < levels.Count; i++)
            {
                LoadedLevel l = levels[i]; Meas m = ms[i];
                double t = Target[l.Spec.LevelIndex - 1], err = m.Score - t;
                bool capped = Array.IndexOf(CappedLevels, l.Id) >= 0;
                if (capped) Assert.True(Math.Abs(err) <= 8 && m.Score >= 90, l.Id + " measured " + m.Score.ToString("F1", ic));
                else if (Math.Abs(err) > maxErr) { maxErr = Math.Abs(err); worst = l.Id; }
                regionMax[l.Region] = Math.Max(regionMax[l.Region], Math.Abs(err));
                md.AppendLine("| " + l.Id + " | " + l.Spec.Goal.Type + " | " + (l.Spec.EffectiveDifficulty(cfg).Raw / 65536.0).ToString("F3", ic) + " | " + m.Gap.ToString("F2", ic)
                    + " | " + m.Width.ToString("F2", ic) + " | " + m.Special.ToString("F0", ic) + " | " + m.Scroll.ToString("F2", ic) + " | " + m.Ok.ToString("F1", ic)
                    + " | " + t.ToString("F1", ic) + " | " + m.Score.ToString("F1", ic) + " | " + (Math.Round(err, 1) + 0.0).ToString("+0.0;-0.0;0.0", ic) + " |");
            }
            md.AppendLine();
            md.AppendLine("Max absolute error without the capped levels (" + string.Join(", ", CappedLevels) + ", region 4 cannot reach the top of the curve, see the class comment): " + maxErr.ToString("F2", ic) + " (" + worst + "); per region including them: "
                + string.Join(", ", Enumerable.Range(1, 5).Select(r => "R" + r + " " + regionMax[r].ToString("F2", ic))));
            output.WriteLine(md.ToString());
            File.WriteAllText(Path.Combine(root, "Tests", "Simulation", "DIFFICULTY_CURVE_REPORT.md"), md.ToString());
            Assert.True(maxErr <= MaxError, "max curve error " + maxErr.ToString("F2", ic) + " at " + worst);
        }

        [Fact]
        public void ClassesAndSpecialGoalsFollowTheCadence()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            foreach (int region in new[] { 1, 2, 3, 4, 5 })
            {
                List<LoadedLevel> ls = ContentLoader.LoadRegions(root, region, region);
                Assert.Equal(25, ls.Count);
                for (int i = 0; i < ls.Count; i++)
                {
                    int idx = ls[i].Spec.LevelIndex;
                    Assert.Equal(i + 1, idx);
                    string want = idx == 25 ? "highly_complex" : Target[idx - 1] < 30 ? "simple" : Target[idx - 1] < 55 ? "medium" : Target[idx - 1] < 75 ? "hard" : "complex";
                    Assert.True(ls[i].ClassName == want, ls[i].Id + " class " + ls[i].ClassName + " should be " + want);
                    bool special = idx % 5 == 0 && idx <= 20;
                    GoalType g = ls[i].Spec.Goal.Type;
                    if (idx == 25) Assert.Equal(GoalType.ReachFloor, g);
                    else if (special) Assert.True(g == GoalType.ComboChain || g == GoalType.NoWallBounce || g == GoalType.FindHiddenGem || g == GoalType.Speedrun, ls[i].Id + " special goal");
                    else Assert.Equal(GoalType.ReachFloor, g);
                    if (special && idx > 5) Assert.NotEqual(ls[i - 5].Spec.Goal.Type, g);   // no two special neighbours repeat
                }
            }
        }
    }
}
