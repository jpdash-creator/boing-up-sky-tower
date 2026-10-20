using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace SkyHopper.Simulation.Tests
{
    /// <summary>Result of the stack scan of one built level.</summary>
    public sealed class StackStats
    {
        public int Platforms;
        /// <summary>Largest number of platforms linked to one anchor: y within 1.5 u of the anchor (above it) and horizontal gap (edge to edge, moving platforms with their swing) at most 1 u.</summary>
        public int MaxCluster;
        public int ClusterAnchorRow;
        public Fx ClusterY;
        public string ClusterKinds = "";
        /// <summary>Largest number of platforms (any x) whose surfaces lie inside one 3 u tall window.</summary>
        public int MaxIn3u;
        public int Max3uAnchorRow;
        /// <summary>Smallest vertical gap between two platforms of neighbouring rows that overlap (or lie within 1 u) horizontally.</summary>
        public Fx MinOverlapGap = Fx.FromInt(99);
        public int MinGapRow;
        public int StacksOf5;
    }

    public static class StackAnalysis
    {
        public static readonly Fx ClusterHeight = Fx.FromRatio(15, 10);
        public static readonly Fx ClusterReach = Fx.One;
        public static readonly Fx Window = Fx.FromInt(3);

        private static Fx HalfExtent(Platform p) { return p.Width / 2 + p.Amplitude; }

        public static Fx HorizontalGap(Platform a, Platform b)
        {
            return Fx.Abs(a.X - b.X) - HalfExtent(a) - HalfExtent(b);
        }

        public static StackStats Scan(Platform[] plats)
        {
            StackStats st = new StackStats();
            st.Platforms = plats.Length;
            for (int i = 0; i < plats.Length; i++)
            {
                Platform a = plats[i];
                int cluster = 1, win = 0;
                string kinds = a.Kind.ToString().Substring(0, 1);
                for (int j = 0; j < plats.Length; j++)
                {
                    Platform b = plats[j];
                    Fx dy = b.Y - a.Y;
                    if (dy >= Fx.Zero && dy <= Window) win++;
                    if (j == i) continue;
                    if (dy < Fx.Zero || dy > ClusterHeight) continue;
                    if (HorizontalGap(a, b) <= ClusterReach) { cluster++; kinds += b.Kind.ToString().Substring(0, 1); }
                }
                if (cluster > st.MaxCluster) { st.MaxCluster = cluster; st.ClusterAnchorRow = a.Row; st.ClusterY = a.Y; st.ClusterKinds = kinds; }
                if (cluster >= 5) st.StacksOf5++;
                if (win > st.MaxIn3u) { st.MaxIn3u = win; st.Max3uAnchorRow = a.Row; }
                // smallest vertical gap to any platform above that is horizontally linked
                for (int j = i + 1; j < plats.Length; j++)
                {
                    Platform b = plats[j];
                    Fx dy = b.Y - a.Y;
                    if (dy <= Fx.Zero) continue;
                    if (dy > Fx.FromInt(2)) break;
                    if (HorizontalGap(a, b) <= Fx.Zero && dy < st.MinOverlapGap) { st.MinOverlapGap = dy; st.MinGapRow = a.Row; }
                }
            }
            return st;
        }
    }

    /// <summary>
    /// Audit of region 1 (owner report 2026-10-20: "8 to 10 platforms stacked, game over the moment I touched it").
    /// Builds lvl_001_01..15 with the real LevelBuilder under the default config and under the game's runtime config
    /// (GameSession.ApplyDifficulty: ScrollStepPercent = 20/40/60/80/100, cap 400), scans for stacks, compares the scroll speed with the
    /// best possible climb and writes Tests/Simulation/REGION1_AUDIT.md. Category Audit.
    /// </summary>
    [Trait("Category", "Audit")]
    public class Region1AuditTests
    {
        /// <summary>Bot node budget per run (a tenth of the proof budget; the audit wants numbers, not proof). Env AUDIT_BUDGET overrides.</summary>
        public static readonly int AuditBudget = int.Parse(Environment.GetEnvironmentVariable("AUDIT_BUDGET") ?? "2000");
        public static readonly int[] StepTable = { 20, 40, 60, 80, 100 };
        private static readonly string[] StepNames = { "Relaxed", "Easy", "Normal", "Hard", "Insane" };
        private readonly ITestOutputHelper output;
        public Region1AuditTests(ITestOutputHelper output) { this.output = output; }

        /// <summary>The config the game runs with (GameSession.Cfg after ApplyDifficulty).</summary>
        public static SimConfig GameConfig(int stepPercent)
        {
            SimConfig c = new SimConfig();
            c.ScrollStepPercent = stepPercent;
            c.ScrollStepCapPercent = 400;
            return c;
        }

        private static string F(double v) { return v.ToString("F2", CultureInfo.InvariantCulture); }
        private static string F(Fx v) { return F(v.Raw / 65536.0); }

        /// <summary>Scroll speed in u/s after t seconds of play, as Sim applies it (base x (100 + min(cap, steps x step)) / 100).</summary>
        public static double ScrollSpeedAt(SimConfig c, Fx difficulty, double t)
        {
            double baseSpeed = (c.ScrollSpeedEasy.Raw + (c.ScrollSpeedHard.Raw - c.ScrollSpeedEasy.Raw) * (difficulty.Raw / 65536.0)) / 65536.0;
            baseSpeed = baseSpeed * c.ScrollBasePercent / 100.0;
            int steps = (int)(t / c.ScrollStepSeconds);
            int pct = steps * c.ScrollStepPercent;
            if (pct > c.ScrollStepCapPercent) pct = c.ScrollStepCapPercent;
            return baseSpeed * (100 + pct) / 100.0;
        }

        private static LevelSpec FastSpec(LevelSpec spec)
        {
            // Same level, goal replaced by "reach the target row as fast as possible" so the best-first bot returns the fastest line.
            LevelSpec f = new LevelSpec();
            f.Seed = spec.Seed; f.Difficulty = spec.Difficulty; f.LevelIndex = spec.LevelIndex; f.Class = spec.Class;
            f.Signature = spec.Signature; f.Ramp = spec.Ramp; f.FixedChunks = spec.FixedChunks; f.Stars = spec.Stars;
            f.Rows = spec.RowsFor();
            f.Goal = new GoalSpec();
            f.Goal.Type = GoalType.Speedrun;
            f.Goal.Value = spec.TargetRow();
            f.Goal.TimeSec = 3600;
            return f;
        }

        [Fact]
        public void Region1Audit()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) { output.WriteLine("Content not found; skipped"); return; }
            List<LoadedLevel> levels = ContentLoader.LoadRegions(root, 1, 1);
            Assert.Equal(15, levels.Count);

            StringBuilder md = new StringBuilder();
            md.AppendLine("# Region 1 audit: stacks and scroll versus climb");
            md.AppendLine();
            md.AppendLine("Generated by `Region1AuditTests` (category Audit). Stack = platforms whose surfaces are within 1.5 u above an anchor and whose horizontal gap to it is at most 1 u (moving platforms with their swing). `win3` = platforms inside the busiest 3 u window. `minGap` = smallest vertical gap between two platforms that overlap horizontally.");
            md.AppendLine();
            md.AppendLine("## Stacks per level (default config)");
            md.AppendLine();
            md.AppendLine("| level | goal | rows | platforms | kinds | max stack | at row (y) | stack kinds | stacks>=5 | win3 | win3 row | minGap (row) |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
            SimConfig def = new SimConfig();
            foreach (LoadedLevel ll in levels)
            {
                LevelData data = LevelBuilder.Build(def, ll.Spec);
                StackStats st = StackAnalysis.Scan(data.Platforms);
                SortedDictionary<string, int> kinds = new SortedDictionary<string, int>();
                for (int i = 0; i < data.Platforms.Length; i++)
                {
                    string k = data.Platforms[i].Kind.ToString();
                    kinds[k] = kinds.ContainsKey(k) ? kinds[k] + 1 : 1;
                }
                StringBuilder ks = new StringBuilder();
                foreach (KeyValuePair<string, int> kv in kinds) ks.Append(kv.Key).Append(' ').Append(kv.Value).Append(' ');
                md.Append("| ").Append(ll.Id).Append(" | ").Append(ll.Spec.Goal.Type).Append(" | ").Append(data.Rows).Append(" | ").Append(st.Platforms)
                  .Append(" | ").Append(ks.ToString().Trim()).Append(" | ").Append(st.MaxCluster).Append(" | ").Append(st.ClusterAnchorRow).Append(" (").Append(F(st.ClusterY)).Append(")")
                  .Append(" | ").Append(st.ClusterKinds).Append(" | ").Append(st.StacksOf5).Append(" | ").Append(st.MaxIn3u).Append(" | ").Append(st.Max3uAnchorRow)
                  .Append(" | ").Append(F(st.MinOverlapGap)).Append(" (").Append(st.MinGapRow).AppendLine(") |");
                output.WriteLine(ll.Id + " stack=" + st.MaxCluster + "@row" + st.ClusterAnchorRow + " [" + st.ClusterKinds + "] win3=" + st.MaxIn3u + "@row" + st.Max3uAnchorRow + " minGap=" + F(st.MinOverlapGap) + "@" + st.MinGapRow);
            }

            File.WriteAllText(Path.Combine(root, "Tests", "Simulation", "REGION1_AUDIT.md"), md.ToString());
            md.AppendLine();
            md.AppendLine("## Scroll speed (u/s) after t seconds of play: base at the level d, game config (base 120 percent, step every 5 s, cap 400 percent)");
            md.AppendLine();
            md.AppendLine("| level | d | step | 0 s | 10 s | 20 s | 30 s | 40 s | 60 s |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (LoadedLevel ll in levels)
            {
                for (int k = 0; k < StepTable.Length; k++)
                {
                    SimConfig c = GameConfig(StepTable[k]);
                    md.Append("| ").Append(ll.Id).Append(" | ").Append(F(ll.Spec.Difficulty)).Append(" | ").Append(StepTable[k]);
                    foreach (int t in new int[] { 0, 10, 20, 30, 40, 60 }) md.Append(" | ").Append(F(ScrollSpeedAt(c, ll.Spec.Difficulty, t)));
                    md.AppendLine(" |");
                }
            }

            md.AppendLine();
            md.AppendLine("## Fastest climb and survival (bot, best-first fastest line to the goal row)");
            md.AppendLine();
            md.AppendLine("`climb u/s` = level height / fastest bot time on the default config (the best sustainable climb rate of that level). Under each game step table the bot is run again: `ok` = fastest line still reaches the goal row alive (time in s), otherwise the best row reached.");
            md.AppendLine();
            md.AppendLine("| level | goal row | top y (u) | fastest default (s) | climb u/s | 20 | 40 | 60 | 80 | 100 |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            foreach (LoadedLevel ll in levels)
            {
                LevelData data = LevelBuilder.Build(def, ll.Spec);
                LevelSpec fs = FastSpec(ll.Spec);
                int budget = AuditBudget * Math.Max(1, fs.RowsFor() / 60);
                BotResult b0 = new LevelBot(new Sim(def, data, fs), fs, data, budget).Solve();
                double topY = data.Platforms[data.Platforms.Length - 1].Y.Raw / 65536.0;
                double secs = b0.Ticks / 60.0;
                md.Append("| ").Append(ll.Id).Append(" | ").Append(fs.Goal.Value).Append(" | ").Append(F(topY)).Append(" | ").Append(b0.Solved ? F(secs) : "unsolved").Append(" | ")
                  .Append(b0.Solved ? F(topY / secs) : "-");
                if (Environment.GetEnvironmentVariable("AUDIT_STEPS") == "1")
                    for (int k = 0; k < StepTable.Length; k++)
                    {
                        SimConfig c = GameConfig(StepTable[k]);
                        BotResult br = new LevelBot(new Sim(c, data, fs), fs, data, budget).Solve();
                        md.Append(" | ").Append(br.Solved ? "ok " + F(br.Ticks / 60.0) : "DEAD best row " + br.BestRow);
                    }
                else md.Append(" | - | - | - | - | -");
                md.AppendLine(" |");
                output.WriteLine(ll.Id + " fastest " + (b0.Solved ? F(secs) + " s" : "unsolved"));
            }

            File.WriteAllText(Path.Combine(root, "Tests", "Simulation", "REGION1_AUDIT.md"), md.ToString());
        }
    }
}
