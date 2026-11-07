using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BoingUpSkyTower.Economy;
using Xunit;
using Xunit.Abstractions;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>
    /// The hero generator tool (the bible, docs/44): draws candidates with <see cref="HeroIndex.Draw"/> and keeps one only when its MEASURED FI passes the gates, in three stages:
    ///   stage 1 (screen): level 15 of Region 1, 8 layouts, within 0.04 of the approved FI of the tier;
    ///   stage 2 (screen): all 75 levels, 4 layouts, mean FI within 0.03 of Momo's mean of the same tier;
    ///   stage 3 (final):  all 75 levels, 24 layouts: level 15 of Region 1 within 0.02 of the approved FI, mean within 0.02 of Momo's mean, no level more than 0.08 from Momo's FI on it.
    /// Momo's tiers are the reference (<see cref="HeroIndex.LadderFinal"/>), measured with the same fast probe. Results are appended to Tests/Simulation/hero_gen/heroes.csv (resumable).
    /// Set HERO_GEN=1; optional HERO_LINES=a,b to restrict the lines, HERO_MAXATT (default 40) attempts per hero.
    /// </summary>
    [Trait("Category", "HeroGen")]
    public class HeroGeneratorTests
    {
        private readonly ITestOutputHelper output;
        public HeroGeneratorTests(ITestOutputHelper output) { this.output = output; }

        static string TierSuffix(int tier) { return tier == 0 ? "" : tier == 1 ? "_u" : tier == 2 ? "_r" : tier == 3 ? "_e" : "_l"; }
        static PhysicsKey Key(HeroPhysics p) { return new PhysicsKey(p.LaunchTenths, p.Speed, p.Control, p.Wall); }

        sealed class Reference
        {
            public double[][] Ok24;   // [tier][level]
            public double[][] Ok4;    // [tier][level]
            public double[] L15Ok8;   // [tier] level 15 of Region 1, 8 layouts
            public int L15;
            public double[] Mean24 = new double[5], Mean4 = new double[5], Target15 = new double[5];
        }

        static Reference LoadOrBuildReference(ITestOutputHelper output)
        {
            var r = new Reference();
            List<LoadedLevel> ls = HeroFi.Levels();
            r.L15 = ls.FindIndex(l => l.Id == "lvl_001_15");
            int[] all = Enumerable.Range(0, ls.Count).ToArray();
            PhysicsKey[] mo = HeroIndex.LadderFinal.Select(Key).ToArray();
            string csv = Path.Combine(ContentLoader.FindRoot(), "Tests", "Simulation", "FI_REFERENCE_FAST.csv");
            r.Ok24 = new double[5][];
            for (int t = 0; t < 5; t++) r.Ok24[t] = new double[ls.Count];
            if (File.Exists(csv))
            {
                string[] lines = File.ReadAllLines(csv);
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] f = lines[i].Split(',');
                    int li = ls.FindIndex(l => l.Id == f[0]);
                    for (int t = 0; t < 5; t++) r.Ok24[t][li] = double.Parse(f[1 + t], CultureInfo.InvariantCulture);
                }
            }
            else
            {
                r.Ok24 = HeroFi.OkLevels(mo, all, 24);
            }
            r.Ok4 = HeroFi.OkLevels(mo, all, 4);
            double[][] l15 = HeroFi.OkLevel(mo, r.L15, 0, 8);
            r.L15Ok8 = l15.Select(a => a.Average()).ToArray();
            for (int t = 0; t < 5; t++)
            {
                double s24 = 0, s4 = 0;
                for (int li = 0; li < ls.Count; li++) { s24 += r.Ok24[t][li] / r.Ok24[0][li]; s4 += r.Ok4[t][li] / r.Ok4[0][li]; }
                r.Mean24[t] = s24 / ls.Count; r.Mean4[t] = s4 / ls.Count;
                double fast15 = r.Ok24[t][r.L15] / r.Ok24[0][r.L15];
                // the approved values were measured with the full probe: keep them when the fast probe agrees, else the gate follows Momo measured with the fast probe
                r.Target15[t] = Math.Abs(fast15 - HeroIndex.ApprovedFi[t]) <= 0.015 ? HeroIndex.ApprovedFi[t] : fast15;
            }
            output.WriteLine("reference mean24: " + string.Join(" / ", r.Mean24.Select(x => x.ToString("F3", CultureInfo.InvariantCulture))) + "   target15: " + string.Join(" / ", r.Target15.Select(x => x.ToString("F3", CultureInfo.InvariantCulture))));
            return r;
        }

        [Fact]
        public void GenerateHeroes()
        {
            if (Environment.GetEnvironmentVariable("HERO_GEN") != "1") return;
            string root = ContentLoader.FindRoot();
            string dir = Path.Combine(root, "Tests", "Simulation", "hero_gen");
            Directory.CreateDirectory(dir);
            string outFile = Path.Combine(dir, "heroes.csv");
            if (!File.Exists(outFile)) File.WriteAllText(outFile, "id,line,tier,launchTenths,speed,control,wall,fi15,mean75,maxdev,attempts,status\n");
            var done = new HashSet<string>(File.ReadAllLines(outFile).Skip(1).Select(l => l.Split(',')[0]));
            int maxAtt = int.Parse(Environment.GetEnvironmentVariable("HERO_MAXATT") ?? "40");
            string only = Environment.GetEnvironmentVariable("HERO_LINES");
            HashSet<string> lines = string.IsNullOrEmpty(only) ? null : new HashSet<string>(only.Split(','));

            List<LoadedLevel> ls = HeroFi.Levels();
            int[] all = Enumerable.Range(0, ls.Count).ToArray();
            Reference R = LoadOrBuildReference(output);
            var sw = Stopwatch.StartNew();

            foreach (string line in TierRules.Lines)
            {
                if (line == "momo") continue;
                if (lines != null && !lines.Contains(line)) continue;
                for (int tier = 0; tier < 5; tier++)
                {
                    string id = line + TierSuffix(tier);
                    if (done.Contains(id)) continue;
                    HeroPhysics accepted = default(HeroPhysics);
                    double fi15 = 0, mean75 = 0, maxdev = 0;
                    int attempts = 0, s1 = 0, s2 = 0;
                    string status = "FAILED";
                    for (int a = 0; a < maxAtt && status != "OK"; a++)
                    {
                        attempts = a + 1;
                        HeroPhysics p = HeroIndex.Draw(line, tier, a);
                        if (HeroIndex.Validate(tier, p) != null) continue;
                        PhysicsKey k = Key(p);
                        // stage 1: level 15 of Region 1, 8 layouts
                        double[][] one = HeroFi.OkLevel(new[] { k }, R.L15, 0, 8);
                        double f1 = one[0].Average() / R.L15Ok8[0];
                        if (Math.Abs(f1 - R.Target15[tier]) > 0.04) continue;
                        s1++;
                        // stage 2: 75 levels, 4 layouts
                        double[][] m4 = HeroFi.OkLevels(new[] { k }, all, 4);
                        double sum4 = 0; for (int li = 0; li < ls.Count; li++) sum4 += m4[0][li] / R.Ok4[0][li];
                        if (Math.Abs(sum4 / ls.Count - R.Mean4[tier]) > 0.03) continue;
                        s2++;
                        // stage 3: 75 levels, 24 layouts
                        double[][] m24 = HeroFi.OkLevels(new[] { k }, all, 24);
                        double sum = 0, dev = 0;
                        for (int li = 0; li < ls.Count; li++)
                        {
                            double fi = m24[0][li] / R.Ok24[0][li];
                            sum += fi;
                            dev = Math.Max(dev, Math.Abs(fi - R.Ok24[tier][li] / R.Ok24[0][li]));
                        }
                        double f15 = m24[0][R.L15] / R.Ok24[0][R.L15];
                        double mean = sum / ls.Count;
                        if (Math.Abs(f15 - R.Target15[tier]) <= HeroIndex.GateTolerance && Math.Abs(mean - R.Mean24[tier]) <= HeroIndex.GateTolerance && dev <= HeroIndex.PerLevelTolerance)
                        { accepted = p; fi15 = f15; mean75 = mean; maxdev = dev; status = "OK"; }
                    }
                    File.AppendAllText(outFile, id + "," + line + "," + tier + "," + accepted.LaunchTenths + "," + accepted.Speed + "," + accepted.Control + "," + accepted.Wall + ","
                        + fi15.ToString("F3", CultureInfo.InvariantCulture) + "," + mean75.ToString("F3", CultureInfo.InvariantCulture) + "," + maxdev.ToString("F3", CultureInfo.InvariantCulture) + "," + attempts + "," + status + "\n");
                    output.WriteLine(id + " " + status + " " + accepted + " fi15=" + fi15.ToString("F3") + " mean=" + mean75.ToString("F3") + " maxdev=" + maxdev.ToString("F3") + " attempts=" + attempts + " (pass s1 " + s1 + ", s2 " + s2 + ") t=" + sw.Elapsed.TotalMinutes.ToString("F1") + " min");
                }
            }
        }
    }
}
