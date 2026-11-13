using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluffyClimbers.Economy;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// End-to-end solvability LADDER (owner decision 2026-11-12). For every Gameplay setting (Relaxed .. Insane, the scroll step table 10/20/25/40/50 percent, top speed-up step x 8 like GameSession)
    /// x every level of regions 1 to 5 (25 levels each = 625 pairs) the weakest tier that lets the solvability bot win the 1-star goal is searched:
    ///   tier 1: a Common or Uncommon hero, no power-ups (Momo Common, the default hero everyone has; then the strongest Uncommon hero),
    ///   tier 2: the same heroes plus power-ups (up to PowerBudget activations per run of Flight, Rocket, Shield, Slow Time),
    ///   tier 3: Rare, Elite and Legendary heroes (the strongest line of each rarity), without and then with power-ups.
    /// Requirement: Relaxed, Easy and Normal are solvable at tier 1; Hard and Insane at tier 3 at the latest. A pair no tier solves is reported as UNSOLVED (never hidden, the requirement is not weakened).
    ///
    /// The bot is time-boxed per attempt (BotTimeBox, AttemptSeconds). The bot never presses jump in the air or hugs walls, so Double Jump, Wall Stick and Magnet are not offered to it.
    /// Env: LADDER=1 runs the full table and writes Tests/Simulation/SOLVABILITY_LADDER.md (and .csv); LADDER_MODES=0,1 / LADDER_REGIONS=3 / LADDER_LEVELS=10,14 restrict it (then the file is SOLVABILITY_LADDER_PARTIAL.md);
    /// LADDER_THREADS (default processors minus one), LADDER_SECONDS (attempt limit, default 90). Category SolvabilityLadder.
    /// </summary>
    [Trait("Category", "SolvabilityLadder")]
    public class SolvabilityLadderTests
    {
        public const int PowerBudget = 3;
        public const int AttemptSeconds = 90;   // lvl_003_20 needs about 60 to 75 s of plain search for Momo (the 20 s box of the older tests reported a false tier 3)
        public static readonly PowerUp[] LadderPowers = { PowerUp.Flight, PowerUp.Rocket, PowerUp.Shield, PowerUp.SlowTime };
        public static readonly string[] ModeNames = { "Relaxed", "Easy", "Normal", "Hard", "Insane" };

        private readonly ITestOutputHelper output;
        public SolvabilityLadderTests(ITestOutputHelper output) { this.output = output; }

        /// <summary>One rung of the ladder: which hero, whether power-ups are on, and the tier number it belongs to.</summary>
        public sealed class Step
        {
            public int Tier;
            public string Label;
            public CharacterSpec Hero;
            public bool Powers;
        }

        /// <summary>The strongest hero line of a rarity: highest launch, then control, then speed (the bot's best chance within the rarity).</summary>
        public static CharacterSpec Strongest(CharacterRarity r)
        {
            return CharacterDefs.AllIncludingInactive.Where(h => h.Rarity == r)
                .OrderByDescending(h => (h.Stats ?? CharacterStats.Default).LaunchTenths)
                .ThenByDescending(h => (h.Stats ?? CharacterStats.Default).Control)
                .ThenByDescending(h => (h.Stats ?? CharacterStats.Default).MaxSpeed)
                .ThenBy(h => h.Id, StringComparer.Ordinal).First();
        }

        /// <summary>The rungs in the order they are tried (the first one that solves a pair decides its tier).</summary>
        public static List<Step> Steps()
        {
            CharacterSpec momo = CharacterDefs.Momo;
            CharacterSpec unc = Strongest(CharacterRarity.Uncommon);
            var steps = new List<Step>
            {
                new Step { Tier = 1, Label = "T1 " + momo.Id, Hero = momo },
                new Step { Tier = 1, Label = "T1 " + unc.Id, Hero = unc },
                new Step { Tier = 2, Label = "T2 " + momo.Id + " +PU", Hero = momo, Powers = true },
                new Step { Tier = 2, Label = "T2 " + unc.Id + " +PU", Hero = unc, Powers = true },
            };
            foreach (CharacterRarity r in new[] { CharacterRarity.Rare, CharacterRarity.Epic, CharacterRarity.Legendary })
            {
                CharacterSpec h = Strongest(r);
                steps.Add(new Step { Tier = 3, Label = "T3 " + h.Id, Hero = h });
                steps.Add(new Step { Tier = 3, Label = "T3 " + h.Id + " +PU", Hero = h, Powers = true });
            }
            return steps;
        }

        /// <summary>The config the game runs a level with in a Gameplay mode and hero (GameSession.ApplyDifficulty + BuildRunCfg).</summary>
        public static SimConfig ConfigFor(int mode, CharacterSpec hero)
        {
            SimConfig cfg = MedalCalibrationTests.ConfigFor(mode);
            (hero.Stats ?? CharacterStats.Default).ApplyTo(cfg);
            return cfg;
        }

        public struct Attempt { public bool Solved, TimedOut; public double Seconds; public int Nodes; }

        /// <summary>One time-boxed bot run of a pair on a rung.</summary>
        public static Attempt Try(LoadedLevel level, int mode, Step step, int seconds)
        {
            SimConfig layoutCfg = MedalCalibrationTests.ConfigFor(mode);
            LevelData data = LevelBuilder.Build(layoutCfg, level.Spec);   // the layout never depends on the hero
            SimConfig cfg = ConfigFor(mode, step.Hero);
            int budget = SolvabilityT4Tests.NodeBudget * Math.Max(1, level.Spec.RowsFor() / 60);
            Stopwatch sw = Stopwatch.StartNew();
            BotResult r;
            bool finished = BotTimeBox.Solve(out r, cfg, level.Spec, data, budget, bot =>
            {
                if (step.Powers) { bot.PowerBudget = PowerBudget; bot.PowerSet = LadderPowers; }
            }, seconds);
            return new Attempt { Solved = finished && r.Solved, TimedOut = !finished, Seconds = sw.Elapsed.TotalSeconds, Nodes = finished ? r.Nodes : 0 };
        }

        public sealed class PairResult
        {
            public string Level;
            public int Region, Index, Mode;
            public int Tier;            // 0 = UNSOLVED
            public string Via = "";     // label of the rung that solved it
            public double WinSeconds;   // time of the solving attempt
            public double TotalSeconds; // all attempts of the pair
            public int Attempts;
            public int TimedOut;
            public string TierText { get { return Tier == 0 ? "UNSOLVED" : Tier.ToString(CultureInfo.InvariantCulture); } }
        }

        /// <summary>Walks the ladder for one pair and stops at the first rung that solves it.</summary>
        public static PairResult Climb(LoadedLevel level, int mode, List<Step> steps, int seconds)
        {
            var res = new PairResult { Level = level.Id, Region = level.Region, Index = level.Spec.LevelIndex, Mode = mode };
            foreach (Step st in steps)
            {
                Attempt a = Try(level, mode, st, seconds);
                res.Attempts++;
                res.TotalSeconds += a.Seconds;
                if (a.TimedOut) res.TimedOut++;
                if (a.Solved) { res.Tier = st.Tier; res.Via = st.Label; res.WinSeconds = a.Seconds; return res; }
            }
            return res;
        }

        /// <summary>The owner's requirement for a setting: the highest tier allowed (1 for Relaxed, Easy, Normal; 3 for Hard and Insane).</summary>
        public static int RequiredTier(int mode) { return mode <= 2 ? 1 : 3; }

        /// <summary>True when the pair meets the requirement (UNSOLVED never does).</summary>
        public static bool Meets(PairResult r) { return r.Tier != 0 && r.Tier <= RequiredTier(r.Mode); }

        static int EnvInt(string n, int d) { int v; return int.TryParse(Environment.GetEnvironmentVariable(n), out v) && v > 0 ? v : d; }

        static HashSet<int> EnvSet(string n, int lo, int hi)
        {
            string s = Environment.GetEnvironmentVariable(n);
            var set = new HashSet<int>();
            if (string.IsNullOrEmpty(s)) { for (int i = lo; i <= hi; i++) set.Add(i); return set; }
            foreach (string p in s.Split(',')) { int v; if (int.TryParse(p.Trim(), out v)) set.Add(v); }
            return set;
        }

        // ------------------------------------------------------------------ always-on sample (fast)

        [Fact]
        public void TheRequirementPerSettingIsTierOneForRelaxedEasyNormalAndTierThreeForHardInsane()
        {
            Assert.Equal(new[] { 1, 1, 1, 3, 3 }, Enumerable.Range(0, 5).Select(RequiredTier).ToArray());
            Assert.True(Meets(new PairResult { Mode = 2, Tier = 1 }));
            Assert.False(Meets(new PairResult { Mode = 2, Tier = 2 }));
            Assert.True(Meets(new PairResult { Mode = 4, Tier = 3 }));
            Assert.False(Meets(new PairResult { Mode = 4, Tier = 0 }));
        }

        [Fact]
        public void TheLadderRungsAreOrderedWeakToStrongAndUseRealHeroes()
        {
            List<Step> steps = Steps();
            Assert.Equal(10, steps.Count);
            for (int i = 1; i < steps.Count; i++) Assert.True(steps[i].Tier >= steps[i - 1].Tier);
            Assert.Equal(CharacterRarity.Common, steps[0].Hero.Rarity);
            Assert.Equal(CharacterRarity.Uncommon, steps[1].Hero.Rarity);
            Assert.Contains(steps, s => s.Tier == 3 && s.Hero.Rarity == CharacterRarity.Rare);
            Assert.Contains(steps, s => s.Tier == 3 && s.Hero.Rarity == CharacterRarity.Epic);
            Assert.Contains(steps, s => s.Tier == 3 && s.Hero.Rarity == CharacterRarity.Legendary);
            foreach (Step s in steps) Assert.Equal(s.Tier == 2 || s.Label.EndsWith("+PU"), s.Powers);
        }

        [Fact]
        public void TheBotUsesNoPowerUpsUnlessAskedAndTheBranchOnlyAddsOptions()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            LoadedLevel l = ContentLoader.LoadRegions(root, 1, 1).First(x => x.Spec.LevelIndex == 3);
            SimConfig cfg = MedalCalibrationTests.ConfigFor(0);
            LevelData data = LevelBuilder.Build(cfg, l.Spec);
            int budget = SolvabilityT4Tests.NodeBudget * Math.Max(1, l.Spec.RowsFor() / 60);
            BotResult plain = new LevelBot(new Sim(cfg, data, l.Spec), l.Spec, data, budget).Solve();
            LevelBot withPu = new LevelBot(new Sim(cfg, data, l.Spec), l.Spec, data, budget);
            withPu.PowerBudget = PowerBudget; withPu.PowerSet = LadderPowers;
            BotResult powered = withPu.Solve();
            Assert.True(plain.Solved);
            Assert.Equal(0, plain.Final.PwUsed);
            Assert.True(powered.Solved);
            Assert.True(powered.Nodes <= plain.Nodes);   // the power-up branch only runs after every plain jump failed at a landing, so it is never worse than the plain search (Region 1 has Crumble and Moving since 2026-11-10, so the plain bot can backtrack here and the branch may save nodes)
        }

        [Fact]
        public void SampleEarlyLevelsAreSolvedAtTierOneOnEverySetting()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            LoadedLevel l1 = ContentLoader.LoadRegions(root, 1, 1).First(l => l.Spec.LevelIndex == 1);
            LoadedLevel l7 = ContentLoader.LoadRegions(root, 2, 2).First(l => l.Spec.LevelIndex == 7);
            List<Step> steps = Steps();
            for (int mode = 0; mode < 5; mode++)
                foreach (LoadedLevel l in new[] { l1, l7 })
                {
                    PairResult r = Climb(l, mode, steps, AttemptSeconds);
                    output.WriteLine(l.Id + " " + ModeNames[mode] + " tier " + r.TierText + " via " + r.Via + " " + r.WinSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s");
                    Assert.True(Meets(r), l.Id + " on " + ModeNames[mode] + ": tier " + r.TierText);
                    if (mode <= 2) Assert.Equal(1, r.Tier);
                }
        }

        // ------------------------------------------------------------------ the full ladder

        [Fact]
        public void TheFullLadderMeetsTheRequirement()
        {
            if (Environment.GetEnvironmentVariable("LADDER") != "1") return;
            string root = ContentLoader.FindRoot();
            Assert.NotNull(root);
            HashSet<int> modes = EnvSet("LADDER_MODES", 0, 4), regions = EnvSet("LADDER_REGIONS", 1, 5), only = EnvSet("LADDER_LEVELS", 1, 25);
            bool partial = Environment.GetEnvironmentVariable("LADDER_MODES") != null || Environment.GetEnvironmentVariable("LADDER_REGIONS") != null || Environment.GetEnvironmentVariable("LADDER_LEVELS") != null;
            int seconds = EnvInt("LADDER_SECONDS", AttemptSeconds);
            int threads = EnvInt("LADDER_THREADS", Math.Max(1, Environment.ProcessorCount - 1));
            List<LoadedLevel> levels = ContentLoader.LoadRegions(root, 1, 5).Where(l => regions.Contains(l.Region) && only.Contains(l.Spec.LevelIndex)).ToList();
            List<Step> steps = Steps();
            var jobs = new List<KeyValuePair<LoadedLevel, int>>();
            foreach (LoadedLevel l in levels) foreach (int m in modes.OrderBy(x => x)) jobs.Add(new KeyValuePair<LoadedLevel, int>(l, m));
            var results = new ConcurrentBag<PairResult>();
            string progress = Path.Combine(root, "Tests", "Simulation", "LADDER_PROGRESS.txt");
            File.WriteAllText(progress, "start " + DateTime.Now.ToString("s") + " pairs " + jobs.Count + " threads " + threads + "\n");
            int done = 0;
            Stopwatch sw = Stopwatch.StartNew();
            Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = threads }, j =>
            {
                PairResult r = Climb(j.Key, j.Value, steps, seconds);
                results.Add(r);
                int n = Interlocked.Increment(ref done);
                if (n % 10 == 0 || n == jobs.Count) File.AppendAllText(progress, n + "/" + jobs.Count + " unsolved so far " + results.Count(x => x.Tier == 0) + " elapsed " + sw.Elapsed.TotalMinutes.ToString("F1", CultureInfo.InvariantCulture) + " min\n");
            });
            PairResult[] all = results.OrderBy(r => r.Region).ThenBy(r => r.Index).ThenBy(r => r.Mode).ToArray();
            WriteReport(root, all, steps, seconds, threads, sw.Elapsed, partial);
            PairResult[] bad = all.Where(r => !Meets(r)).ToArray();
            output.WriteLine("pairs " + all.Length + ", below the requirement " + bad.Length + ": " + string.Join("; ", bad.Select(r => r.Level + " " + ModeNames[r.Mode] + " " + r.TierText)));
            Assert.True(bad.Length == 0, bad.Length + " level x setting pairs do not meet the ladder requirement; see SOLVABILITY_LADDER.md: " + string.Join("; ", bad.Take(30).Select(r => r.Level + " " + ModeNames[r.Mode] + " " + r.TierText)));
        }

        static void WriteReport(string root, PairResult[] all, List<Step> steps, int seconds, int threads, TimeSpan elapsed, bool partial)
        {
            var md = new StringBuilder();
            md.AppendLine("# Solvability ladder (owner decision 2026-11-12)" + (partial ? " (PARTIAL RUN)" : ""));
            md.AppendLine();
            md.AppendLine("Generated by `SolvabilityLadderTests` (`LADDER=1`). Settings = scroll step table " + string.Join("/", SimConfig.DifficultyStepPercent) + " percent per 5 s, top speed-up per setting " + string.Join("/", Enumerable.Range(0, 5).Select(MedalCalibrationTests.CapPercent)) + " percent (8 steps); pairs " + all.Length + "; attempt time box " + seconds + " s; node budget " + SolvabilityT4Tests.NodeBudget + " per 60 rows; " + threads + " threads; total " + elapsed.TotalMinutes.ToString("F1", CultureInfo.InvariantCulture) + " min.");
            md.AppendLine();
            md.AppendLine("Requirement: Relaxed, Easy, Normal solvable at tier 1; Hard and Insane at tier 3 at the latest.");
            md.AppendLine();
            md.AppendLine("Rungs, in the order tried (the first one that solves a pair decides its tier; power-ups = up to " + PowerBudget + " activations per run of " + string.Join(", ", LadderPowers) + "; the bot never air-jumps, so Double Jump, Wall Stick and Magnet are not offered):");
            md.AppendLine();
            foreach (Step s in steps) md.AppendLine("- " + s.Label + " (launch " + (s.Hero.Stats ?? CharacterStats.Default).LaunchTenths + ", speed " + (s.Hero.Stats ?? CharacterStats.Default).MaxSpeed + ", control " + (s.Hero.Stats ?? CharacterStats.Default).Control + ")");
            md.AppendLine();
            md.AppendLine("## Summary: pairs per tier needed");
            md.AppendLine();
            md.AppendLine("| setting | tier 1 | tier 2 | tier 3 | UNSOLVED | requirement met |");
            md.AppendLine("|---|---|---|---|---|---|");
            foreach (var g in all.GroupBy(r => r.Mode).OrderBy(g => g.Key))
                md.AppendLine("| " + ModeNames[g.Key] + " | " + g.Count(r => r.Tier == 1) + " | " + g.Count(r => r.Tier == 2) + " | " + g.Count(r => r.Tier == 3) + " | " + g.Count(r => r.Tier == 0) + " | " + (g.All(Meets) ? "yes" : "NO (" + g.Count(r => !Meets(r)) + " pairs)") + " |");
            md.AppendLine();
            md.AppendLine("| region | setting | tier 1 | tier 2 | tier 3 | UNSOLVED |");
            md.AppendLine("|---|---|---|---|---|---|");
            foreach (var g in all.GroupBy(r => new { r.Region, r.Mode }).OrderBy(g => g.Key.Region).ThenBy(g => g.Key.Mode))
                md.AppendLine("| " + g.Key.Region + " | " + ModeNames[g.Key.Mode] + " | " + g.Count(r => r.Tier == 1) + " | " + g.Count(r => r.Tier == 2) + " | " + g.Count(r => r.Tier == 3) + " | " + g.Count(r => r.Tier == 0) + " |");
            PairResult[] bad = all.Where(r => !Meets(r)).ToArray();
            md.AppendLine();
            md.AppendLine("## Pairs below the requirement (" + bad.Length + ")");
            md.AppendLine();
            if (bad.Length == 0) md.AppendLine("None.");
            else foreach (PairResult r in bad) md.AppendLine("- " + r.Level + " " + ModeNames[r.Mode] + ": " + r.TierText + (r.Tier != 0 ? " via " + r.Via : "") + " (" + r.TimedOut + " of " + r.Attempts + " attempts hit the time box)");
            md.AppendLine();
            md.AppendLine("## All pairs");
            md.AppendLine();
            md.AppendLine("Bot time = the winning attempt (or the sum of all attempts for UNSOLVED).");
            md.AppendLine();
            md.AppendLine("| region | level | setting | tier needed | via | bot time s |");
            md.AppendLine("|---|---|---|---|---|---|");
            foreach (PairResult r in all)
                md.AppendLine("| " + r.Region + " | " + r.Level + " | " + ModeNames[r.Mode] + " | " + r.TierText + " | " + (r.Tier == 0 ? "-" : r.Via) + " | " + (r.Tier == 0 ? r.TotalSeconds : r.WinSeconds).ToString("0.0", CultureInfo.InvariantCulture) + " |");
            string name = partial ? "SOLVABILITY_LADDER_PARTIAL" : "SOLVABILITY_LADDER";
            File.WriteAllText(Path.Combine(root, "Tests", "Simulation", name + ".md"), md.ToString());
            var csv = new StringBuilder("region,level,setting,tier,via,winSeconds,totalSeconds,attempts,timedOut\n");
            foreach (PairResult r in all) csv.Append(r.Region).Append(',').Append(r.Level).Append(',').Append(ModeNames[r.Mode]).Append(',').Append(r.TierText).Append(',').Append(r.Via).Append(',').Append(r.WinSeconds.ToString("0.0", CultureInfo.InvariantCulture)).Append(',').Append(r.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)).Append(',').Append(r.Attempts).Append(',').Append(r.TimedOut).Append('\n');
            File.WriteAllText(Path.Combine(root, "Tests", "Simulation", name + ".csv"), csv.ToString());
        }
    }
}
