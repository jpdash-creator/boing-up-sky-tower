using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// The per-player hero variant pool generator (owner decision 2026-11-12, docs/44 section 11). Every hero line (INCLUDING Momo) has a pool of validated physics variants for each of its
    /// four upper tiers (Uncommon .. Legendary); the Common hero of every line stays one fixed row. A player is dealt one variant per hero when the hero is first obtained (Economy/HeroRolls.cs).
    /// SAMPLE THEN VERIFY (owner decision 2026-11-12): a candidate is screened cheaply (level 15 of Region 1 on 8 layouts, then a fixed 15-level sample on 8 layouts, see <see cref="VarEngine.SampleIds"/>)
    /// and only a survivor gets the FULL gate, which is exactly the gate of the single-hero generator (<see cref="HeroGeneratorTests"/>, stage 3): 24 layouts on every shipped level, level 15 of Region 1 and the
    /// mean within <see cref="HeroIndex.GateTolerance"/> of Momo's value of the SAME tier, no level further than <see cref="HeroIndex.PerLevelTolerance"/>, ceiling <see cref="HeroIndex.BandMax"/>.
    /// The screens only reject; every shipped variant passed the full gate. "Full legal range": any (Jump in the tier band, Speed, Control, Wall) that passes is legal.
    /// Results are appended to Tests/Simulation/hero_gen/variants.csv (resumable); every tried candidate goes to variant_attempts.csv; Tools/hero_table_gen.py turns variants.csv into Assets/_Game/Economy/HeroVariants.cs.
    /// Momo's per-layout reference is cached in FI_REFERENCE_VAR.csv with a fingerprint of the levels (rebuilt automatically when the levels change).
    /// Set HERO_VAR=1. Optional: HERO_LINES=a,b  HERO_TIERS=1,2  HERO_VARS=8 (target per hero)  HERO_MINVARS=4 (every hero gets this many first)  HERO_MAXATT=1500 (candidates per hero per run)
    /// HERO_THREADS=3  HERO_LOG=file (progress log)  HERO_OUT_DIR=dir (where the csv files live)  HERO_SAMPLE_MARGIN=0.012  HERO_SAMPLE_LAYOUTS=8  a file named STOP in the out dir ends the run after the current candidates.
    /// </summary>
    [Trait("Category", "HeroGen")]
    public class HeroVariantGeneratorTests
    {
        private readonly ITestOutputHelper output;
        public HeroVariantGeneratorTests(ITestOutputHelper output) { this.output = output; }

        /// <summary>Attempt numbers of the variant search start here (the single-hero generator used 0..39), so variants are different draws.</summary>
        public const int AttemptBase = 1000;
        public const string Header = "id,variant,launchTenths,speed,control,wall,fi15,mean75,maxdev,attempt";
        public const string AttemptHeader = "id,attempt,key,result,fi15sample,sampleFi,sampleTarget,fi15,mean,maxdev,probes,seconds";

        static string TierSuffix(int tier) { return tier == 0 ? "" : tier == 1 ? "_u" : tier == 2 ? "_r" : tier == 3 ? "_e" : "_l"; }
        static string F3(double x) { return VarEngine.F3(x); }
        static int EnvInt(string n, int d) { string s = Environment.GetEnvironmentVariable(n); return string.IsNullOrEmpty(s) ? d : int.Parse(s); }
        static double EnvDbl(string n, double d) { string s = Environment.GetEnvironmentVariable(n); return string.IsNullOrEmpty(s) ? d : double.Parse(s, CultureInfo.InvariantCulture); }

        Action<string> MakeLog(Stopwatch sw)
        {
            string logFile = Environment.GetEnvironmentVariable("HERO_LOG");   // optional progress file (xunit prints only at the end)
            object gate = new object();
            return m =>
            {
                string line = DateTime.Now.ToString("HH:mm:ss") + " +" + sw.Elapsed.TotalMinutes.ToString("F1") + "min " + m;
                lock (gate) { output.WriteLine(line); if (!string.IsNullOrEmpty(logFile)) File.AppendAllText(logFile, line + Environment.NewLine); }
            };
        }

        /// <summary>Re-measures EVERY variant of the shipped table with the FULL gate (24 layouts on every level, no screens). Set HERO_VAR_VERIFY=1 (slow: about 2 core minutes per variant); HERO_THREADS=3.</summary>
        [Fact]
        public void EveryShippedVariantPassesTheMeasuredGate()
        {
            if (Environment.GetEnvironmentVariable("HERO_VAR_VERIFY") != "1") return;
            var sw = Stopwatch.StartNew();
            Action<string> log = MakeLog(sw);
            int threads = EnvInt("HERO_THREADS", 3);
            VarEngine.Reference R = VarEngine.LoadOrBuildReference(threads, log);
            FluffyClimbers.Economy.HeroRow[] rows = FluffyClimbers.Economy.HeroVariants.Rows;
            var failures = new List<string>();
            var settings = new VarEngine.Settings { Screens = false };
            VarEngine.RunWorkers(threads, rows.Length, i =>
            {
                FluffyClimbers.Economy.HeroRow r = rows[i];
                VarEngine.Verdict v = VarEngine.Evaluate(R, r.Tier, new PhysicsKey(r.LaunchTenths, r.Speed, r.Control, r.Wall), settings);
                log("verify " + r.Id + " " + r.LaunchTenths + "/" + r.Speed + "/" + r.Control + "/" + r.Wall + " " + v.Stage + " fi15=" + F3(v.F15) + " mean=" + F3(v.Mean) + " maxdev=" + F3(v.Dev));
                if (!v.Pass) lock (failures) failures.Add(r.Id + " " + r.LaunchTenths + "/" + r.Speed + "/" + r.Control + "/" + r.Wall + " " + v.Stage + " fi15=" + F3(v.F15) + " mean=" + F3(v.Mean) + " maxdev=" + F3(v.Dev));
            });
            Assert.True(failures.Count == 0, "variants failing the full gate: " + string.Join("; ", failures));
        }

        /// <summary>Informational (HERO_VAR_ROWS=1): the FI numbers of the HeroTable rows (pool entry 0) against the current levels. Never asserts; the table is generated by the single-hero generator.</summary>
        [Fact]
        public void TableRowsAgainstTheCurrentReferenceInfoOnly()
        {
            if (Environment.GetEnvironmentVariable("HERO_VAR_ROWS") != "1") return;
            var sw = Stopwatch.StartNew();
            Action<string> log = MakeLog(sw);
            int threads = EnvInt("HERO_THREADS", 3);
            VarEngine.Reference R = VarEngine.LoadOrBuildReference(threads, log);
            var rows = FluffyClimbers.Economy.HeroTable.Rows.Where(r => r.Active && r.Tier >= 1 && r.Line != "momo").ToArray();
            var settings = new VarEngine.Settings { Screens = false };
            VarEngine.RunWorkers(threads, rows.Length, i =>
            {
                FluffyClimbers.Economy.HeroRow r = rows[i];
                VarEngine.Verdict v = VarEngine.Evaluate(R, r.Tier, new PhysicsKey(r.LaunchTenths, r.Speed, r.Control, r.Wall), settings);
                log("table row " + r.Id + " " + v.Stage + " fi15=" + F3(v.F15) + " (target " + F3(R.Target15[r.Tier]) + ") mean=" + F3(v.Mean) + " (target " + F3(R.TargetMean[r.Tier]) + ") maxdev=" + F3(v.Dev));
            });
        }

        sealed class Hero
        {
            public string Id, Line; public int Tier;
            public int Count, InFlight, Tried, NextAttempt = AttemptBase;
            public HashSet<int> TriedAttempts = new HashSet<int>();
            public HashSet<string> Keys = new HashSet<string>();
            public int[] Rejects = new int[5];   // s1, s2, s3dev, s3part, s3
            public double Seconds;
            public bool Exhausted;
        }

        [Fact]
        public void GenerateVariants()
        {
            if (Environment.GetEnvironmentVariable("HERO_VAR") != "1") return;
            string dir = VarEngine.Dir();
            Directory.CreateDirectory(dir);
            string outFile = Path.Combine(dir, "variants.csv"), attFile = Path.Combine(dir, "variant_attempts.csv"), stopFile = Path.Combine(dir, "STOP");
            if (!File.Exists(outFile)) File.WriteAllText(outFile, Header + "\n");
            if (!File.Exists(attFile)) File.WriteAllText(attFile, AttemptHeader + "\n");
            int target = EnvInt("HERO_VARS", 8), minTarget = Math.Min(target, EnvInt("HERO_MINVARS", 4)), maxAtt = EnvInt("HERO_MAXATT", 1500), threads = EnvInt("HERO_THREADS", 3);
            var settings = new VarEngine.Settings { SampleLayouts = EnvInt("HERO_SAMPLE_LAYOUTS", 8), SampleMargin = EnvDbl("HERO_SAMPLE_MARGIN", 0.012) };
            string only = Environment.GetEnvironmentVariable("HERO_LINES");
            HashSet<string> lines = string.IsNullOrEmpty(only) ? null : new HashSet<string>(only.Split(','));
            string onlyTiers = Environment.GetEnvironmentVariable("HERO_TIERS");
            HashSet<int> tiers = string.IsNullOrEmpty(onlyTiers) ? null : new HashSet<int>(onlyTiers.Split(',').Select(int.Parse));
            if (File.Exists(stopFile)) File.Delete(stopFile);

            var sw = Stopwatch.StartNew();
            Action<string> log = MakeLog(sw);
            VarEngine.Reference R = VarEngine.LoadOrBuildReference(threads, log);
            log("reference ready; sample " + string.Join(",", VarEngine.SampleIds) + "; sample layouts " + settings.SampleLayouts + ", margin " + settings.SampleMargin + ", threads " + threads + ", target " + target + " (min first " + minTarget + ")");

            var heroes = new List<Hero>();
            foreach (string line in FluffyClimbers.Economy.HeroTable.ActiveLines)
            {
                if (lines != null && !lines.Contains(line)) continue;
                for (int tier = 1; tier < 5; tier++)
                {
                    if (tiers != null && !tiers.Contains(tier)) continue;
                    var h = new Hero { Id = line + TierSuffix(tier), Line = line, Tier = tier };
                    FluffyClimbers.Economy.HeroRow legacy = FluffyClimbers.Economy.HeroTable.Find(h.Id);
                    if (legacy != null) h.Keys.Add(legacy.LaunchTenths + "/" + legacy.Speed + "/" + legacy.Control + "/" + legacy.Wall);
                    heroes.Add(h);
                }
            }
            var byId = heroes.ToDictionary(h => h.Id);
            foreach (string l in File.ReadAllLines(outFile).Skip(1))
            {
                string[] f = l.Split(',');
                Hero h; if (f.Length < 10 || !byId.TryGetValue(f[0], out h)) continue;
                if (h.Keys.Add(f[2] + "/" + f[3] + "/" + f[4] + "/" + f[5])) h.Count++;
                h.TriedAttempts.Add(int.Parse(f[9]));
            }
            foreach (string l in File.ReadAllLines(attFile).Skip(1))
            {
                string[] f = l.Split(',');
                Hero h; if (f.Length < 4 || !byId.TryGetValue(f[0], out h)) continue;
                h.TriedAttempts.Add(int.Parse(f[1])); h.Keys.Add(f[2]);
            }
            log("resume: " + string.Join(" ", heroes.Select(h => h.Id + "=" + h.Count)));

            object gate = new object();
            int rr = 0, candidatesTotal = 0, fullTotal = 0, acceptedTotal = 0;
            DateTime lastSummary = DateTime.Now;

            // the scheduler hands out (hero, attempt) pairs: first every hero up to minTarget, then every hero up to target; the hero with the fewest variants (counting running candidates) goes first
            Func<int, Hero> pick = goal =>
            {
                Hero best = null;
                for (int i = 0; i < heroes.Count; i++)
                {
                    Hero h = heroes[(rr + i) % heroes.Count];
                    if (h.Exhausted || h.Count + h.InFlight >= goal) continue;
                    if (best == null || h.Count + h.InFlight < best.Count + best.InFlight) best = h;
                }
                if (best != null) rr = (heroes.IndexOf(best) + 1) % heroes.Count;
                return best;
            };

            Action summary = () =>
            {
                log("SUMMARY " + string.Join(" ", heroes.Select(h => h.Id + "=" + h.Count + "/" + target)) + " | candidates " + candidatesTotal + ", full gates " + fullTotal + ", accepted " + acceptedTotal);
            };

            ThreadStart worker = () =>
            {
                for (;;)
                {
                    Hero h; int attempt; HeroPhysics p = default(HeroPhysics);
                    lock (gate)
                    {
                        if (File.Exists(stopFile)) return;
                        h = pick(minTarget) ?? pick(target);
                        if (h == null) return;
                        for (;;)
                        {
                            attempt = h.NextAttempt++;
                            if (h.Tried >= maxAtt) { h.Exhausted = true; log(h.Id + " exhausted its " + maxAtt + " attempts this run with " + h.Count + " variants"); h = null; break; }
                            if (h.TriedAttempts.Contains(attempt)) continue;
                            h.TriedAttempts.Add(attempt);
                            p = HeroIndex.Draw(h.Line, h.Tier, attempt);
                            if (HeroIndex.Validate(h.Tier, p) != null) continue;
                            string k0 = p.LaunchTenths + "/" + p.Speed + "/" + p.Control + "/" + p.Wall;
                            if (!h.Keys.Add(k0)) continue;     // the table row, an accepted variant or an earlier candidate of the same numbers
                            h.Tried++; h.InFlight++;
                            break;
                        }
                        if (h == null) continue;
                    }
                    p = HeroIndex.Draw(h.Line, h.Tier, attempt);
                    string key = p.LaunchTenths + "/" + p.Speed + "/" + p.Control + "/" + p.Wall;
                    var cs = Stopwatch.StartNew();
                    VarEngine.Verdict v = VarEngine.Evaluate(R, h.Tier, new PhysicsKey(p.LaunchTenths, p.Speed, p.Control, p.Wall), settings);
                    double secs = cs.Elapsed.TotalSeconds;
                    lock (gate)
                    {
                        h.InFlight--; h.Seconds += secs; candidatesTotal++;
                        bool full = v.Stage == "OK" || v.Stage == "s3" || v.Stage == "s3dev" || v.Stage == "s3part";
                        if (full) fullTotal++;
                        File.AppendAllText(attFile, h.Id + "," + attempt + "," + key + "," + v.Stage + "," + F3(v.L15Sample) + "," + F3(v.SampleFi) + "," + F3(v.SampleTarget) + "," + F3(v.F15) + "," + F3(v.Mean) + "," + F3(v.Dev) + "," + v.Cells + "," + secs.ToString("F1", CultureInfo.InvariantCulture) + "\n");
                        if (v.Pass)
                        {
                            File.AppendAllText(outFile, h.Id + "," + h.Count + "," + p.LaunchTenths + "," + p.Speed + "," + p.Control + "," + p.Wall + "," + F3(v.F15) + "," + F3(v.Mean) + "," + F3(v.Dev) + "," + attempt + "\n");
                            h.Count++; acceptedTotal++;
                            log("ACCEPT " + h.Id + " variant " + (h.Count - 1) + " " + p + " fi15=" + F3(v.F15) + " mean=" + F3(v.Mean) + " maxdev=" + F3(v.Dev) + " (sample " + F3(v.SampleFi) + " vs " + F3(v.SampleTarget) + ", " + secs.ToString("F0") + " s, attempt " + attempt + ")");
                        }
                        else
                        {
                            int ix = v.Stage == "s1" ? 0 : v.Stage == "s2" ? 1 : v.Stage == "s3dev" ? 2 : v.Stage == "s3part" ? 3 : 4;
                            h.Rejects[ix]++;
                        }
                        if ((DateTime.Now - lastSummary).TotalMinutes >= 10) { lastSummary = DateTime.Now; summary(); }
                    }
                }
            };
            var ts = new List<Thread>();
            for (int w = 0; w < threads; w++) { var t = new Thread(worker, 16 * 1024 * 1024); t.Start(); ts.Add(t); }
            foreach (Thread t in ts) t.Join();
            summary();
            foreach (Hero h in heroes)
                log(h.Id + ": " + h.Count + "/" + target + " variants (" + h.Tried + " candidates this run; rejected s1 " + h.Rejects[0] + ", sample " + h.Rejects[1] + ", level dev " + h.Rejects[2] + ", partial mean " + h.Rejects[3] + ", full gate " + h.Rejects[4] + "; " + (h.Seconds / 60).ToString("F1") + " core min)");
        }
    }
}
