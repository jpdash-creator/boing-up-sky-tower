using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Difficulty measurements for doc 24 (levels 1-2 gentle, 3 to 15 clearly harder, region 1 level 15 about as hard as region 3 level 15).
    /// Metrics per level (balanced random layouts, 3 attempt seeds):
    ///   gap / width: mean vertical gap and mean spine platform width; special: share of rows whose spine platform is not Normal;
    ///   scroll: starting scroll speed in u/s; ok%: share of homing jump options (targets of the next 3 rows x back-off x run-up) that land
    ///   alive on a higher row, measured from the middle of every spine platform at three clock offsets (lower = harder, a proxy of how
    ///   forgiving the layout is); nodes: bot search effort to solve the level (higher = harder).
    /// The report is written to Tests/Simulation/DIFFICULTY_REPORT.md when UPDATE_GOLDEN=1.
    /// </summary>
    [Trait("Category", "Difficulty")]
    public class DifficultyReportTests
    {
        private readonly ITestOutputHelper output;
        public DifficultyReportTests(ITestOutputHelper output) { this.output = output; }

        public sealed class Row
        {
            public string Id;
            public int Region, Index;
            public double D, Gap, Width, Special, Hazard, Scroll, OkPercent, Nodes, Solved;
        }

        public static double BaseScroll(SimConfig cfg, LevelSpec s)
        {
            Fx v = Fx.Lerp(cfg.ScrollSpeedEasy, cfg.ScrollSpeedHard, s.EffectiveDifficulty(cfg)) * cfg.ScrollBasePercent / 100;
            return v.Raw / 65536.0;
        }

        public static Row Measure(SimConfig cfg, LoadedLevel ll, int seeds)
        {
            Row r = new Row { Id = ll.Id, Region = ll.Region, Index = ll.Spec.LevelIndex, D = ll.Spec.EffectiveDifficulty(cfg).Raw / 65536.0 };
            double gap = 0, width = 0, special = 0, hazard = 0, ok = 0, nodes = 0, scroll = 0, solved = 0;
            int okN = 0;
            for (int i = 0; i < seeds; i++)
            {
                LevelSpec s = ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i + 300));
                LevelData d = LevelBuilder.Build(cfg, s);
                Sim sim = new Sim(cfg, d, s);
                scroll += BaseScroll(cfg, s);
                // spine statistics
                double prevY = 0; int prevRow = 0, n = 0, sp = 0;
                foreach (Platform p in d.Platforms)
                {
                    if (p.Row == prevRow + 1)
                    {
                        double y = p.Y.Raw / 65536.0;
                        gap += y - prevY; width += p.Width.Raw / 65536.0; n++;
                        if (p.Kind != PlatformKind.Normal && p.Kind != PlatformKind.Crumble) sp++;
                        if (p.Kind == PlatformKind.ThornBush || p.Kind == PlatformKind.SnapDaisy) hazard++;
                        prevY = y; prevRow = p.Row;
                    }
                }
                gap /= Math.Max(1, n) * 1.0; // normalised below
                special += sp / (double)Math.Max(1, n);
                // option success probe
                LevelBot probe = new LevelBot(sim, s, d, 1000);
                int[] clocks = { 0, 101, 233 };
                int firstOfRow = -1, lastRow = -1;
                double okSum = 0; int okCount = 0;
                for (int pi = 1; pi < d.Platforms.Length; pi++)
                {
                    Platform p = d.Platforms[pi];
                    if (p.Row == lastRow || p.Row > d.Rows - 3) continue;
                    firstOfRow = pi; lastRow = p.Row;
                    foreach (int t0 in clocks)
                    {
                        SimState st = sim.NewState();
                        st.Tick = t0; st.StartTick = t0;
                        st.GroundId = firstOfRow; st.Grounded = true; st.Row = p.Row; st.BestGroundId = firstOfRow;
                        st.X = sim.PlatformX(firstOfRow, t0); st.Y = p.Y; st.MaxY = p.Y; st.KillY = p.Y - Fx.FromInt(60);
                        int tried, prog;
                        probe.MeasureOptions(st, out tried, out prog);
                        if (tried > 0) { okSum += prog / (double)tried; okCount++; }
                    }
                }
                ok += okSum / Math.Max(1, okCount); okN++;
                BotResult br = new LevelBot(sim, s, d, SolvabilityT4Tests.NodeBudget * Math.Max(1, s.RowsFor() / 60)).Solve();
                nodes += br.Nodes; solved += br.Solved ? 1 : 0;
            }
            // gap/width were summed per seed over rows: recompute simple means across seeds
            r.Gap = gap / seeds; r.Width = width / seeds; r.Special = special / seeds * 100; r.Hazard = hazard / seeds;
            r.Scroll = scroll / seeds; r.OkPercent = ok / Math.Max(1, okN) * 100; r.Nodes = nodes / seeds; r.Solved = solved / seeds;
            return r;
        }

        /// <summary>Row means need the row count: Measure keeps gap/width as per-row means (see below), so fix them up here.</summary>
        public static Row MeasureFixed(SimConfig cfg, LoadedLevel ll, int seeds)
        {
            // Spine mean gap and width computed separately and exactly.
            double gap = 0, width = 0; int n = 0;
            for (int i = 0; i < seeds; i++)
            {
                LevelSpec s = ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i + 300));
                LevelData d = LevelBuilder.Build(cfg, s);
                double prevY = 0; int prevRow = 0;
                foreach (Platform p in d.Platforms)
                    if (p.Row == prevRow + 1)
                    {
                        double y = p.Y.Raw / 65536.0;
                        gap += y - prevY; width += p.Width.Raw / 65536.0; n++;
                        prevY = y; prevRow = p.Row;
                    }
            }
            Row r = Measure(cfg, ll, seeds);
            r.Gap = gap / Math.Max(1, n); r.Width = width / Math.Max(1, n);
            return r;
        }

        [Fact]
        public void WriteDifficultyReport()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = ContentLoader.LoadRegions(root, 1, 5);
            Row[] rows = new Row[levels.Count];
            Parallel.For(0, levels.Count, i => { rows[i] = MeasureFixed(cfg, levels[i], 3); });

            StringBuilder md = new StringBuilder();
            md.AppendLine("# Difficulty report (levels 1-15, regions 1-5)");
            md.AppendLine();
            md.AppendLine("Generated by DifficultyReportTests (3 random attempt layouts per level). d = difficulty used by the builder and the scroll; gap / width = spine means in u; special % = share of rows with a non-Normal spine platform; haz = hazard-type rows (thorn, snap daisy); scroll = start speed u/s; ok% = share of homing jump options that land on a higher row (lower = harder); nodes = bot search effort.");
            md.AppendLine();
            md.AppendLine("| level | d | gap | width | special % | haz | scroll | ok % | nodes | solved |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            foreach (Row r in rows)
                md.AppendLine("| " + r.Id + " | " + r.D.ToString("F2") + " | " + r.Gap.ToString("F2") + " | " + r.Width.ToString("F2") + " | " + r.Special.ToString("F0") + " | " + r.Hazard.ToString("F1")
                    + " | " + r.Scroll.ToString("F2") + " | " + r.OkPercent.ToString("F1") + " | " + r.Nodes.ToString("F0") + " | " + (r.Solved * 100).ToString("F0") + "% |");
            output.WriteLine(md.ToString());
            if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
                File.WriteAllText(Path.Combine(root, "Tests", "Simulation", Environment.GetEnvironmentVariable("DIFFICULTY_REPORT_NAME") ?? "DIFFICULTY_REPORT.md"), md.ToString());
            foreach (Row r in rows) Assert.True(r.Solved > 0.99, r.Id + " not solved");
        }
    }
}
