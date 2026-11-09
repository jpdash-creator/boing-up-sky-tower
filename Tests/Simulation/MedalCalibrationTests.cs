using System;
using System.Collections.Concurrent;
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
    /// Medal calibration (doc 46 section 6 and 12 point 2). For every level of regions 1 to 5 and every Gameplay mode (Relaxed..Insane = scroll step table
    /// 20/22/25/28/30 percent, cap 150 like GameSession.ApplyDifficulty) the default hero Momo (default SimConfig) is played by the solvability bot on
    /// <see cref="Layouts"/> random layouts (attempt seeds RandomLayoutTests.AttemptSeed(0..4), the same layouts as the random layout tests): the bot searches the
    /// fastest line to the top row (speedrun best-first search, goal replaced like the Region 1 audit does). Pace = rows / seconds from the first jump, averaged over the layouts
    /// the bot solved. That Pace is written to Content/config/medal_targets.json (milli plat/s, 0 = the bot solved no layout); the medal lines are
    /// TimeRecords.MedalPercents (55, 70, 85) of it and are applied at run time by FileMedalTargets + TimeRecords.MedalFor.
    ///
    /// Env: MEDAL_CAL=1 runs the full calibration and (re)writes the data file and Tests/Simulation/MEDAL_CALIBRATION.md. MEDAL_BUDGET = bot node budget per 60 rows
    /// (default 20000 = the solvability budget; the probe uses 2000). MEDAL_THREADS = parallel bot runs (default: processors minus one).
    /// MEDAL_PROBE=1 runs a timed sample (levels 1, 13 and 25 of every region, modes Relaxed and Insane, one layout) and prints how long the full run will take.
    /// </summary>
    [Trait("Category", "MedalCalibration")]
    public class MedalCalibrationTests
    {
        public const int Layouts = 5;
        public const int ProbeBudget = 2000;
        public const int FullBudget = 20000;
        public const int CapPercent = 150;

        private readonly ITestOutputHelper output;
        public MedalCalibrationTests(ITestOutputHelper output) { this.output = output; }

        /// <summary>One bot run: whether it solved, the rows climbed, and the ticks from the first jump to the goal row.</summary>
        public struct PaceSample
        {
            public bool Solved;
            public int Floors;
            public int Ticks;
            public long PaceMilli { get { return Solved && Floors > 0 && Ticks > 0 ? (long)Floors * SimConfig.TicksPerSecond * 1000 / Ticks : 0; } }
        }

        /// <summary>The config the game runs a level with in a Gameplay mode (GameSession.ApplyDifficulty), Momo = the default stats.</summary>
        public static SimConfig ConfigFor(int mode)
        {
            SimConfig c = new SimConfig();
            c.ScrollStepPercent = SimConfig.DifficultyStepPercent[mode];
            c.GemSpotPermille = SimConfig.DifficultyGemPermille[mode];
            c.ScrollStepCapPercent = CapPercent;
            return c;
        }

        /// <summary>Same level, goal replaced by "reach the top row as fast as possible" so the best-first bot returns its fastest line.</summary>
        public static LevelSpec FastSpec(LevelSpec spec)
        {
            LevelSpec f = spec.WithAttemptSeed(0);   // identical copy
            f.Rows = spec.RowsFor();
            f.Goal = new GoalSpec { Type = GoalType.Speedrun, Value = spec.TargetRow(), TimeSec = 3600 };
            return f;
        }

        /// <summary>The bot's fastest line on one layout (attempt seed index 0..Layouts-1) of a level in a Gameplay mode.</summary>
        public static PaceSample Measure(LoadedLevel level, int mode, int layout, int budgetPer60Rows)
        {
            SimConfig cfg = ConfigFor(mode);
            LevelSpec spec = level.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(layout));
            LevelData data = LevelBuilder.Build(cfg, spec);
            LevelSpec fs = FastSpec(spec);
            int budget = budgetPer60Rows * Math.Max(1, fs.RowsFor() / 60);
            BotResult b = new LevelBot(new Sim(cfg, data, fs), fs, data, budget).Solve();
            PaceSample s = new PaceSample();
            s.Solved = b.Solved;
            s.Floors = fs.Goal.Value;
            // the clock starts at the first jump (doc 46 section 3), the bot's tick count starts at spawn
            int start = b.Final.StartTick > 0 ? b.Final.StartTick : 0;
            s.Ticks = Math.Max(1, b.Ticks - start);
            return s;
        }

        /// <summary>Mean Pace of the solved samples in milli plat/s (integer maths), 0 when none solved.</summary>
        public static int MeanPaceMilli(IList<PaceSample> samples)
        {
            long sum = 0; int n = 0;
            foreach (PaceSample s in samples) if (s.Solved && s.PaceMilli > 0) { sum += s.PaceMilli; n++; }
            return n == 0 ? 0 : (int)((sum + n / 2) / n);
        }

        static int EnvInt(string name, int fallback)
        {
            int v;
            return int.TryParse(Environment.GetEnvironmentVariable(name), out v) && v > 0 ? v : fallback;
        }

        static int Threads() { return EnvInt("MEDAL_THREADS", Math.Max(1, Environment.ProcessorCount - 1)); }

        static string Plat(long milli) { return (milli / 1000.0).ToString("0.000", CultureInfo.InvariantCulture); }

        // ---------------------------------------------------------------- always-on checks (fast)

        [Fact]
        public void TheBotPaceOfALevelIsPositiveAndDeterministic()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) { output.WriteLine("Content not found; skipped"); return; }
            LoadedLevel l1 = ContentLoader.LoadRegions(root, 1, 1).First(l => l.Spec.LevelIndex == 1);
            PaceSample a = Measure(l1, 0, 0, ProbeBudget);
            PaceSample b = Measure(l1, 0, 0, ProbeBudget);
            Assert.True(a.Solved, "the bot solves level 1 of region 1 on Relaxed");
            Assert.True(a.PaceMilli > 100 && a.PaceMilli < 5000, "pace " + a.PaceMilli);
            Assert.Equal(a.Ticks, b.Ticks);   // deterministic
            Assert.Equal(a.Floors, b.Floors);
            output.WriteLine(l1.Id + " relaxed pace " + Plat(a.PaceMilli) + " plat/s over " + a.Floors + " rows in " + a.Ticks + " ticks");
        }

        [Fact]
        public void MeanPaceAveragesOnlySolvedLayoutsAndRoundsHalfUp()
        {
            var samples = new List<PaceSample>
            {
                new PaceSample { Solved = true, Floors = 60, Ticks = 3600 },    // 1.000
                new PaceSample { Solved = true, Floors = 60, Ticks = 4800 },    // 0.750
                new PaceSample { Solved = false, Floors = 60, Ticks = 100 },    // ignored
            };
            Assert.Equal(875, MeanPaceMilli(samples));
            Assert.Equal(0, MeanPaceMilli(new List<PaceSample> { new PaceSample { Solved = false, Floors = 60, Ticks = 10 } }));
            Assert.Equal(0, MeanPaceMilli(new List<PaceSample>()));
        }

        // ---------------------------------------------------------------- the timed probe

        [Fact]
        public void Probe()
        {
            if (Environment.GetEnvironmentVariable("MEDAL_PROBE") != "1") return;
            string root = ContentLoader.FindRoot();
            Assert.NotNull(root);
            int budget = EnvInt("MEDAL_BUDGET", ProbeBudget);
            var picks = ContentLoader.LoadRegions(root, 1, 5).Where(l => l.Spec.LevelIndex == 1 || l.Spec.LevelIndex == 13 || l.Spec.LevelIndex == 25).ToList();
            int[] modes = { 0, 4 };
            var jobs = new List<Tuple<LoadedLevel, int>>();
            foreach (LoadedLevel l in picks) foreach (int m in modes) jobs.Add(Tuple.Create(l, m));
            var rows = new ConcurrentDictionary<string, string>();
            long solved = 0;
            Stopwatch sw = Stopwatch.StartNew();
            Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = Threads() }, j =>
            {
                Stopwatch t = Stopwatch.StartNew();
                PaceSample s = Measure(j.Item1, j.Item2, 0, budget);
                if (s.Solved) System.Threading.Interlocked.Increment(ref solved);
                rows[j.Item1.Id + "/" + j.Item2] = j.Item1.Id + " mode " + j.Item2 + " " + (s.Solved ? Plat(s.PaceMilli) + " plat/s" : "unsolved") + " (" + t.ElapsedMilliseconds + " ms)";
            });
            foreach (string k in rows.Keys.OrderBy(x => x, StringComparer.Ordinal)) output.WriteLine(rows[k]);
            double perRun = sw.Elapsed.TotalSeconds * Threads() / jobs.Count;
            int totalRuns = 125 * TimeRecords.ModeCount * Layouts;
            output.WriteLine("probe: " + jobs.Count + " runs, " + solved + " solved, " + sw.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s wall on " + Threads() + " threads, about "
                + perRun.ToString("0.0", CultureInfo.InvariantCulture) + " cpu-s per run; the full run is " + totalRuns + " runs, about "
                + (perRun * totalRuns / Threads() / 60).ToString("0", CultureInfo.InvariantCulture) + " min at this budget");
        }

        // ---------------------------------------------------------------- the full calibration

        [Fact]
        public void Calibrate()
        {
            if (Environment.GetEnvironmentVariable("MEDAL_CAL") != "1") return;
            string root = ContentLoader.FindRoot();
            Assert.NotNull(root);
            int budget = EnvInt("MEDAL_BUDGET", FullBudget);
            List<LoadedLevel> levels = ContentLoader.LoadRegions(root, 1, 5);
            Assert.Equal(125, levels.Count);

            var jobs = new List<Tuple<LoadedLevel, int, int>>();
            foreach (LoadedLevel l in levels)
                for (int m = 0; m < TimeRecords.ModeCount; m++)
                    for (int k = 0; k < Layouts; k++) jobs.Add(Tuple.Create(l, m, k));
            var samples = new ConcurrentDictionary<string, PaceSample>();
            int done = 0;
            Stopwatch sw = Stopwatch.StartNew();
            Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = Threads() }, j =>
            {
                samples[j.Item1.Id + "|" + j.Item2 + "|" + j.Item3] = Measure(j.Item1, j.Item2, j.Item3, budget);
                int n = System.Threading.Interlocked.Increment(ref done);
                if (n % 100 == 0) Console.WriteLine("medal calibration " + n + "/" + jobs.Count + " runs, " + sw.Elapsed.TotalMinutes.ToString("0.0", CultureInfo.InvariantCulture) + " min");
            });

            var targets = new SortedDictionary<string, int[]>(StringComparer.Ordinal);
            var solvedCounts = new Dictionary<string, int[]>();
            foreach (LoadedLevel l in levels)
            {
                int[] row = new int[TimeRecords.ModeCount];
                int[] cnt = new int[TimeRecords.ModeCount];
                for (int m = 0; m < row.Length; m++)
                {
                    var list = new List<PaceSample>();
                    for (int k = 0; k < Layouts; k++) list.Add(samples[l.Id + "|" + m + "|" + k]);
                    row[m] = MeanPaceMilli(list);
                    cnt[m] = list.Count(s => s.Solved);
                }
                targets[l.Id] = row;
                solvedCounts[l.Id] = cnt;
            }

            string note = "Medal reference Pace per level and Gameplay mode (Relaxed, Easy, Normal, Hard, Insane) in milli plat/s (830 = 0.83): the default hero Momo, the solvability bot's fastest line to the top row, averaged over the layouts it solved. "
                + "Written by Tests/Simulation/MedalCalibrationTests.cs (MEDAL_CAL=1); never hand-edit. Bronze, silver and gold are the percents below of it. 0 = the bot solved no layout (no medal).";
            string json = FileMedalTargets.ToJson(targets, Layouts, note);
            string dir = Path.Combine(root, "Content", "config");
            File.WriteAllText(Path.Combine(dir, "medal_targets.json"), json, new UTF8Encoding(false));
            string resources = Path.Combine(root, "Assets", "Resources", "Content");
            if (Directory.Exists(resources)) File.WriteAllText(Path.Combine(resources, "medal_targets.json"), json, new UTF8Encoding(false));

            var md = new StringBuilder();
            md.AppendLine("# Medal calibration (doc 46)");
            md.AppendLine();
            md.AppendLine("Generated by `MedalCalibrationTests.Calibrate` (`MEDAL_CAL=1`). Hero: Momo (default stats). Bot: fastest line to the top row (speedrun best-first search), node budget " + budget + " per 60 rows, " + Layouts + " random layouts per level and mode (attempt seeds 0 to 4 of RandomLayoutTests), the game's scroll step table (" + string.Join("/", SimConfig.DifficultyStepPercent) + " percent, cap " + CapPercent + "). Total run time " + sw.Elapsed.TotalMinutes.ToString("0.0", CultureInfo.InvariantCulture) + " min on " + Threads() + " threads.");
            md.AppendLine();
            md.AppendLine("Gold reference Pace in plat/s (layouts the bot solved, of " + Layouts + ", in brackets). Bronze, silver and gold lines = " + TimeRecords.MedalPercents[0] + ", " + TimeRecords.MedalPercents[1] + ", " + TimeRecords.MedalPercents[2] + " percent of it.");
            md.AppendLine();
            md.AppendLine("| level | Relaxed | Easy | Normal | Hard | Insane |");
            md.AppendLine("|---|---|---|---|---|---|");
            foreach (KeyValuePair<string, int[]> kv in targets)
            {
                md.Append("| ").Append(kv.Key);
                for (int m = 0; m < TimeRecords.ModeCount; m++) md.Append(" | ").Append(kv.Value[m] > 0 ? Plat(kv.Value[m]) : "none").Append(" (").Append(solvedCounts[kv.Key][m]).Append(")");
                md.AppendLine(" |");
            }
            File.WriteAllText(Path.Combine(root, "Tests", "Simulation", "MEDAL_CALIBRATION.md"), md.ToString());
            int none = targets.Values.Sum(r => r.Count(v => v == 0));
            Console.WriteLine("medal calibration done in " + sw.Elapsed.TotalMinutes.ToString("0.0", CultureInfo.InvariantCulture) + " min; level/mode pairs without a solved layout: " + none + " of " + targets.Count * TimeRecords.ModeCount);
        }
    }
}
