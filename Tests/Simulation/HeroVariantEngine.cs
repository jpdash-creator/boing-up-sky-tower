using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// The measurement engine of the hero variant generator (owner decision 2026-11-12: SAMPLE THEN VERIFY). Everything is single threaded per candidate (the driver runs one candidate per worker
    /// thread), every layout measurement is cached per candidate, and the reference of Momo's five tiers is kept PER LAYOUT in Tests/Simulation/hero_gen/FI_REFERENCE_VAR.csv together with a fingerprint
    /// of the shipped levels, so a stale reference (the levels changed since it was measured) is detected and rebuilt instead of silently failing every candidate.
    /// The gate is exactly the one of <see cref="HeroGeneratorTests"/> (stage 3): level 15 of Region 1 and the mean FI over ALL shipped levels within <see cref="HeroIndex.GateTolerance"/> of Momo's
    /// value of the SAME tier, no level further than <see cref="HeroIndex.PerLevelTolerance"/>, ceiling <see cref="HeroIndex.BandMax"/> (+0.005 slack); 24 layouts per level, fast probe.
    /// The screens in front of it only reject: a candidate has to pass all of them AND the full gate to ship.
    /// </summary>
    internal static class VarEngine
    {
        public const int Layouts = 24;

        /// <summary>The fixed 15-level screening sample: 3 per region, spread over the level classes (simple 2, medium 4, hard 4, complex 3, highly complex 2), level 15 of Region 1 included.</summary>
        public static readonly string[] SampleIds =
        {
            "lvl_001_05", "lvl_001_12", "lvl_001_15",
            "lvl_002_02", "lvl_002_09", "lvl_002_20",
            "lvl_003_04", "lvl_003_14", "lvl_003_25",
            "lvl_004_03", "lvl_004_08", "lvl_004_22",
            "lvl_005_06", "lvl_005_11", "lvl_005_25",
        };

        public static string F3(double x) { return x.ToString("F3", CultureInfo.InvariantCulture); }
        public static string Dir() { string o = Environment.GetEnvironmentVariable("HERO_OUT_DIR"); return string.IsNullOrEmpty(o) ? Path.Combine(ContentLoader.FindRoot(), "Tests", "Simulation", "hero_gen") : o; }

        /// <summary>Safe-jump share (percent) of one layout of one level: deterministic, so it can be cached and compared across runs.</summary>
        public static double Cell(SimConfig run, LoadedLevel ll, int layout)
        {
            LevelSpec s = HeroFi.Spec(ll, layout);
            LevelData d = LevelBuilder.Build(new SimConfig(), s);
            return HeroFi.OkLayout(run, s, d, true);
        }

        /// <summary>Fingerprint of the shipped levels (layout 0 of every level: platform rows, kinds, positions, widths).</summary>
        public static string Fingerprint(List<LoadedLevel> ls)
        {
            unchecked
            {
                ulong h = 1469598103934665603UL;
                foreach (LoadedLevel ll in ls)
                {
                    LevelData d = LevelBuilder.Build(new SimConfig(), HeroFi.Spec(ll, 0));
                    foreach (char c in ll.Id) { h ^= c; h *= 1099511628211UL; }
                    foreach (Platform p in d.Platforms)
                    {
                        h ^= (ulong)(uint)p.Row; h *= 1099511628211UL;
                        h ^= (ulong)(uint)(int)p.Kind; h *= 1099511628211UL;
                        h ^= (ulong)(uint)p.X.Raw; h *= 1099511628211UL;
                        h ^= (ulong)(uint)p.Width.Raw; h *= 1099511628211UL;
                    }
                }
                return h.ToString("x16") + "/" + ls.Count;
            }
        }

        public sealed class Reference
        {
            public List<LoadedLevel> Levels;
            public double[][][] V;          // [tier][level][layout] percent
            public int L15;
            public int[] Sample;            // level indices of the screening sample
            public string Fingerprint;
            public double[] Target15 = new double[5], TargetMean = new double[5];   // full gate, 24 layouts, all levels

            public double Mean(int tier, int li, int n) { double s = 0; for (int i = 0; i < n; i++) s += V[tier][li][i]; return s / n; }
            public double Fi(int tier, int li, int n) { return Mean(tier, li, n) / Mean(0, li, n); }

            public void Finish()
            {
                L15 = Levels.FindIndex(l => l.Id == "lvl_001_15");
                Sample = SampleIds.Select(id => Levels.FindIndex(l => l.Id == id)).ToArray();
                if (L15 < 0 || Sample.Any(i => i < 0)) throw new InvalidOperationException("the screening sample names a level that is not shipped");
                for (int t = 0; t < 5; t++)
                {
                    Target15[t] = Fi(t, L15, Layouts);
                    double s = 0; for (int li = 0; li < Levels.Count; li++) s += Fi(t, li, Layouts);
                    TargetMean[t] = s / Levels.Count;
                }
            }
        }

        static PhysicsKey MomoKey(int t) { HeroPhysics p = HeroIndex.LadderFinal[t]; return new PhysicsKey(p.LaunchTenths, p.Speed, p.Control, p.Wall); }

        public static void RunWorkers(int threads, int items, Action<int> work)
        {
            int next = -1;
            var ts = new List<Thread>();
            for (int w = 0; w < threads; w++)
            {
                var t = new Thread(() => { for (;;) { int i = Interlocked.Increment(ref next); if (i >= items) return; work(i); } }, 16 * 1024 * 1024);
                t.Start(); ts.Add(t);
            }
            foreach (Thread t in ts) t.Join();
        }

        /// <summary>Loads Momo's per-layout reference or measures it (5 tiers x all levels x 24 layouts) when the file is missing or stale.</summary>
        public static Reference LoadOrBuildReference(int threads, Action<string> log)
        {
            var r = new Reference { Levels = HeroFi.Levels() };
            r.Fingerprint = Fingerprint(r.Levels);
            string file = Path.Combine(Dir(), "FI_REFERENCE_VAR.csv");
            if (File.Exists(file) && TryLoad(file, r)) { r.Finish(); log("reference loaded " + r.Levels.Count + " levels, fingerprint " + r.Fingerprint); return r; }
            if (Environment.GetEnvironmentVariable("HERO_REF_NOBUILD") == "1") throw new InvalidOperationException("FI_REFERENCE_VAR.csv is missing or stale and HERO_REF_NOBUILD=1");
            log("reference missing or stale: measuring Momo's 5 tiers on " + r.Levels.Count + " levels x " + Layouts + " layouts (" + threads + " threads)");
            r.V = new double[5][][];
            for (int t = 0; t < 5; t++) { r.V[t] = new double[r.Levels.Count][]; for (int li = 0; li < r.Levels.Count; li++) r.V[t][li] = new double[Layouts]; }
            SimConfig[] cfg = Enumerable.Range(0, 5).Select(t => HeroFi.RunCfg(MomoKey(t))).ToArray();
            int levels = r.Levels.Count, done = 0;
            RunWorkers(threads, 5 * levels, i =>
            {
                int t = i / levels, li = i % levels;
                for (int k = 0; k < Layouts; k++) r.V[t][li][k] = Cell(cfg[t], r.Levels[li], k);
                int n = Interlocked.Increment(ref done);
                if (n % 50 == 0) log("reference " + n + "/" + (5 * levels));
            });
            var sb = new StringBuilder();
            sb.Append("# fp=").Append(r.Fingerprint).Append(" layouts=").Append(Layouts).Append(" probe=fast\n");
            sb.Append("level,tier,values (percent per layout)\n");
            for (int li = 0; li < levels; li++)
                for (int t = 0; t < 5; t++)
                    sb.Append(r.Levels[li].Id).Append(',').Append(t).Append(',').Append(string.Join(",", r.V[t][li].Select(x => x.ToString("R", CultureInfo.InvariantCulture)))).Append('\n');
            Directory.CreateDirectory(Dir());
            File.WriteAllText(file, sb.ToString());
            r.Finish();
            log("reference built; target15 " + string.Join("/", r.Target15.Select(F3)) + " mean " + string.Join("/", r.TargetMean.Select(F3)));
            return r;
        }

        static bool TryLoad(string file, Reference r)
        {
            string[] lines = File.ReadAllLines(file);
            if (lines.Length < 3 || !lines[0].StartsWith("# fp=" + r.Fingerprint + " layouts=" + Layouts)) return false;
            r.V = new double[5][][];
            for (int t = 0; t < 5; t++) { r.V[t] = new double[r.Levels.Count][]; }
            int found = 0;
            for (int i = 2; i < lines.Length; i++)
            {
                if (lines[i].Length == 0) continue;
                string[] f = lines[i].Split(',');
                int li = r.Levels.FindIndex(l => l.Id == f[0]), t = int.Parse(f[1]);
                if (li < 0 || f.Length != 2 + Layouts) return false;
                r.V[t][li] = new double[Layouts];
                for (int k = 0; k < Layouts; k++) r.V[t][li][k] = double.Parse(f[2 + k], CultureInfo.InvariantCulture);
                found++;
            }
            return found == 5 * r.Levels.Count;
        }

        public sealed class Settings
        {
            public int SampleLayouts = 8;
            public double SampleMargin = 0.012;      // sampled mean FI (15 levels) within this of Momo's same-tier sampled mean
            public double SampleL15Margin = 0.035;   // level 15 of Region 1 on the sample layouts
            public double PartialMargin = 0.04;      // running mean while the remaining levels are measured
            public bool Screens = true;              // false = the full gate only (verification of shipped rows)
        }

        public sealed class Verdict
        {
            public bool Pass; public string Stage = "";          // stage that rejected ("s1", "s2", "s3dev", "s3part", "s3") or "OK"
            public double L15Sample, SampleFi, SampleTarget, F15, Mean, Dev;
            public int Cells;                                    // layout probes spent
        }

        /// <summary>Sample then verify: stage 1 level 15 (sample layouts), stage 2 the 15-level sample, stage 3 the FULL gate on every level (aborts early on a clear miss).</summary>
        public static Verdict Evaluate(Reference R, int tier, PhysicsKey key, Settings S)
        {
            SimConfig cfg = HeroFi.RunCfg(key);
            int levels = R.Levels.Count;
            var cell = new double[levels][];
            var v = new Verdict();
            Func<int, int, double> get = (li, k) =>
            {
                double[] row = cell[li] ?? (cell[li] = Enumerable.Repeat(double.NaN, Layouts).ToArray());
                if (double.IsNaN(row[k])) { row[k] = Cell(cfg, R.Levels[li], k); v.Cells++; }
                return row[k];
            };
            Func<int, int, double> fi = (li, n) => { double s = 0; for (int k = 0; k < n; k++) s += get(li, k); return (s / n) / R.Mean(0, li, n); };
            Func<int, int, double> refFi = (li, n) => R.Fi(tier, li, n);
            int ns = S.SampleLayouts;
            if (S.Screens)
            {
                v.L15Sample = fi(R.L15, ns);
                if (Math.Abs(v.L15Sample - refFi(R.L15, ns)) > S.SampleL15Margin) { v.Stage = "s1"; return v; }
                double sf = 0, st = 0;
                foreach (int li in R.Sample) { sf += fi(li, ns); st += refFi(li, ns); }
                v.SampleFi = sf / R.Sample.Length; v.SampleTarget = st / R.Sample.Length;
                if (Math.Abs(v.SampleFi - v.SampleTarget) > S.SampleMargin) { v.Stage = "s2"; return v; }
            }
            // stage 3: every level, 24 layouts, level by level (sample and level 15 first so the cheap rejects come early)
            var order = new List<int>(R.Sample);
            for (int li = 0; li < levels; li++) if (!order.Contains(li)) order.Add(li);
            double sum = 0, tsum = 0, dev = 0;
            int measured = 0;
            foreach (int li in order)
            {
                double f = fi(li, Layouts), t = refFi(li, Layouts);
                sum += f; tsum += t; measured++;
                dev = Math.Max(dev, Math.Abs(f - t));
                if (dev > HeroIndex.PerLevelTolerance) { v.Dev = dev; v.Stage = "s3dev"; return v; }
                if (S.Screens && measured % 25 == 0 && measured < levels && Math.Abs((sum - tsum) / measured) > S.PartialMargin) { v.Dev = dev; v.Stage = "s3part"; return v; }
            }
            v.F15 = fi(R.L15, Layouts); v.Mean = sum / levels; v.Dev = dev;
            v.Pass = Math.Abs(v.F15 - R.Target15[tier]) <= HeroIndex.GateTolerance && Math.Abs(v.Mean - R.TargetMean[tier]) <= HeroIndex.GateTolerance
                && v.F15 <= HeroIndex.BandMax + 0.005 && v.Mean <= HeroIndex.BandMax + 0.005;
            v.Stage = v.Pass ? "OK" : "s3";
            return v;
        }
    }
}
