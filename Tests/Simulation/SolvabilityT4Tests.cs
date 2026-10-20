using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>One row of the solvability report.</summary>
    public sealed class LevelOutcome
    {
        public LoadedLevel Level;
        public int Rows;
        public int PickupCount;
        public int HazardCount;
        public int PlatformCount;
        public BotResult Solve;
        public bool CoinSearchRan;
        public BotResult CoinSearch;
        public int CoinTarget;          // 0 = no coin star
        public string Star3Kind;        // "noRevive" or "time"
        public int Star3TimeTicks;
    }

    /// <summary>
    /// T4: the bot plays the 1-star goal of every one of the 75 shipped levels of regions 1 to 5 (levels built by
    /// LevelBuilder from the Content JSON) and writes Tests/Simulation/SOLVABILITY_REPORT.md. Stars 2 and 3 are
    /// reported as information only.
    /// </summary>
    public class SolvabilityT4Tests
    {
        public const int NodeBudget = 20000;
        // Longer levels need a proportionally larger search (60 rows = the base budget).
        private static int Budget(LevelSpec spec) { return NodeBudget * System.Math.Max(1, spec.RowsFor() / 60); }
        public const int CoinBeamWidth = 10;

        private readonly ITestOutputHelper output;
        public SolvabilityT4Tests(ITestOutputHelper output) { this.output = output; }

        private static readonly object Gate = new object();
        private static List<LevelOutcome> cached;

        internal static List<LevelOutcome> Outcomes()
        {
            lock (Gate)
            {
                if (cached != null) return cached;
                string root = ContentLoader.FindRoot();
                List<LevelOutcome> list = new List<LevelOutcome>();
                if (root == null) { cached = list; return list; }
                SimConfig cfg = new SimConfig();
                foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 1, 5))
                {
                    LevelOutcome o = new LevelOutcome();
                    o.Level = ll;
                    LevelData data = LevelBuilder.Build(cfg, ll.Spec);
                    o.Rows = data.Rows;
                    o.PickupCount = data.Pickups.Length;
                    o.HazardCount = data.Hazards.Length;
                    o.PlatformCount = data.Platforms.Length;
                    Sim sim = new Sim(cfg, data, ll.Spec);
                    o.Solve = new LevelBot(sim, ll.Spec, data, Budget(ll.Spec)).Solve();

                    for (int i = 0; i < ll.Spec.Stars.Length; i++)
                    {
                        StarSpec st = ll.Spec.Stars[i];
                        if (st.Kind == StarSpec.KindCoins) o.CoinTarget = st.Value;
                        if (st.Kind == StarSpec.KindNoRevive) o.Star3Kind = "noRevive";
                        if (st.Kind == StarSpec.KindTime) { o.Star3Kind = "time"; o.Star3TimeTicks = st.Value * SimConfig.TicksPerSecond; }
                    }
                    if (o.Solve.Solved && o.CoinTarget > 0)
                    {
                        // Best known coin count at the moment the run ends (owner decision 2026-10-19: the run ends at goal completion).
                        // Goals that complete before the top row (collectCoins, comboChain, findHiddenGem): beam search for the
                        // most coins held when the goal is met. All other goals complete at the top row: the solving line if
                        // it reaches the top, a beam search from the start, and a beam continued from the end of the solving line.
                        int top = data.Rows;
                        BotResult bestRun = new BotResult();
                        bool have = false;
                        GoalType gt = ll.Spec.Goal.Type;
                        if (EndsBeforeTop(gt))
                        {
                            bestRun = o.Solve; have = true;
                            BotResult atGoal = new LevelBot(sim, ll.Spec, data, Budget(ll.Spec)).MaxCoinsAtGoal(CoinBeamWidth);
                            if (atGoal.Solved && atGoal.Coins > bestRun.Coins) bestRun = atGoal;
                        }
                        else
                        {
                            if (o.Solve.Final.Row >= top) { bestRun = o.Solve; have = true; }
                            BotResult scratch = new LevelBot(sim, ll.Spec, data, Budget(ll.Spec)).MaxCoins(CoinBeamWidth);
                            if (scratch.Solved && (!have || scratch.Coins > bestRun.Coins)) { bestRun = scratch; have = true; }
                            if (o.Solve.Final.Row < top)
                            {
                                BotResult cont = new LevelBot(sim, ll.Spec, data, Budget(ll.Spec)).MaxCoinsFrom(o.Solve.Final, CoinBeamWidth);
                                if (cont.Solved && (!have || cont.Coins > bestRun.Coins)) { bestRun = cont; have = true; }
                            }
                        }
                        o.CoinSearch = bestRun;
                        o.CoinSearchRan = have;
                    }
                    list.Add(o);
                }
                WriteReport(root, list);
                cached = list;
                return list;
            }
        }

        /// <summary>Goals that complete before the top row, so the run (and the coin count for star 2) ends earlier.</summary>
        internal static bool EndsBeforeTop(GoalType t)
        {
            return t == GoalType.CollectCoins || t == GoalType.ComboChain || t == GoalType.FindHiddenGem;
        }

        private static string GoalText(GoalSpec g)
        {
            switch (g.Type)
            {
                case GoalType.ReachFloor: return "reachFloor " + g.Value;
                case GoalType.CollectCoins: return "collectCoins " + g.Count;
                case GoalType.ComboChain: return "comboChain " + g.Value + "x" + g.Count;
                case GoalType.NoWallBounce: return "noWallBounce " + g.Value;
                case GoalType.FindHiddenGem: return "findHiddenGem";
                case GoalType.Speedrun: return "speedrun " + g.Value + " in " + g.TimeSec + "s";
                case GoalType.SurviveWave: return "surviveWave";
                case GoalType.BossChase: return "bossChase " + g.Value;
                default: return g.Type.ToString();
            }
        }

        private static string ClassText(int c)
        {
            switch (c)
            {
                case 0: return "simple";
                case 1: return "medium";
                case 2: return "hard";
                case 3: return "complex";
                default: return "highly complex";
            }
        }

        private static string Star2Text(LevelOutcome o)
        {
            if (o.CoinTarget == 0) return "n/a";
            if (!o.Solve.Solved) return "n/a (unsolved)";
            if (!o.CoinSearchRan || !o.CoinSearch.Solved)
                return "unknown (beam found no full run); path coins " + o.Solve.Coins + " / " + o.CoinTarget;
            return (o.CoinSearch.Coins >= o.CoinTarget ? "yes" : "NO") + " (" + o.CoinSearch.Coins + " / " + o.CoinTarget + ")";
        }

        private static string Star3Text(LevelOutcome o)
        {
            if (!o.Solve.Solved) return "n/a (unsolved)";
            if (o.Star3Kind == "time") return (o.Solve.Ticks <= o.Star3TimeTicks ? "yes" : "NO") + " (time " + o.Solve.Ticks / 60 + " s)";
            return "yes (solved without a revive)";
        }

        private static void WriteReport(string root, List<LevelOutcome> list)
        {
            StringBuilder sb = new StringBuilder();
            int solved = 0;
            foreach (LevelOutcome o in list) if (o.Solve.Solved) solved++;
            sb.AppendLine("# Solvability report: regions 1 to 5 (75 levels)");
            sb.AppendLine();
            sb.AppendLine("Generated by `SolvabilityT4Tests` (do not edit by hand; run `dotnet test Tests/Simulation`). Levels are built by `LevelBuilder.Build` from `Content/levels` and `Content/regions`; the bot (`LevelBot`) plays the 1-star goal with a budget of " + NodeBudget + " search nodes.");
            sb.AppendLine();
            sb.AppendLine("Physics (2026-10-20): `JumpBase` 19 u/s and `JumpSpeedFactor` 0.58 (was 14 / 0.85): standing peak 4.51 u (1.3 median row gaps, was 2.45 u), full-speed peak 7.33 u (2.1 median gaps); the generator caps row gaps at `NormalGapMax` 4.2 u so one standing jump always reaches the next platform; layouts still sized for the old 14 u/s reference (`LayoutJumpBase`). Star pickups are swept along the path between ticks and always sit in clear air.");
            sb.AppendLine();
            sb.AppendLine("Stack rule (2026-10-20, owner report of 8 to 10 stacked platforms in region 1): at most 4 platforms in any 3 u window, no stack of 5, rows under 1.2 u apart only when 2 u apart horizontally; combo ladders of 4+ floors use 1.0 u rungs in two staggered columns; the campaign scroll flattens after 30 s (`ScrollStepMaxSteps` 6) and never exceeds 4 u/s (`ScrollMaxSpeed`). `StackRuleTests` checks all 75 levels and proves the bot survives region 1 at the hardest step table (+100 percent per 5 s, runtime cap 150). See `REGION1_AUDIT.md` and `NOTES.md`.");
            sb.AppendLine();
            sb.AppendLine("**Star 1 (goal) solved: " + solved + " of " + list.Count + ".**");
            sb.AppendLine();
            sb.AppendLine("Columns: `d` = effective difficulty; `rows` = platform rows; `nodes` = search nodes used; `ticks` = length of the solving line (60 per second); `coins` = coins on the solving line; `hits` = gong hits on that line; `star 2` = coin target reachable by a run that completes the goal and climbs to the top row (best of: for collectCoins, comboChain and findHiddenGem the coins held when the goal is met (the run ends there); for all other goals the best of the solving line when it reaches the top, a row-layered beam search, and a beam continued from the end of the solving line; a lower bound of the true maximum; landing coins count, see NOTES.md); `star 3` = no-revive star (the bot never revives) or time star.");
            sb.AppendLine();
            sb.AppendLine("| level | class | d | goal | rows | solved | nodes | ticks | coins | hits | star 2 (coin target) | star 3 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (LevelOutcome o in list)
            {
                sb.Append("| ").Append(o.Level.Id)
                  .Append(" | ").Append(ClassText(o.Level.Spec.Class))
                  .Append(" | ").Append(o.Level.Spec.Difficulty.ToString().Substring(0, 5))
                  .Append(" | ").Append(GoalText(o.Level.Spec.Goal))
                  .Append(" | ").Append(o.Rows)
                  .Append(" | ").Append(o.Solve.Solved ? "yes" : "NO")
                  .Append(" | ").Append(o.Solve.Nodes)
                  .Append(" | ").Append(o.Solve.Ticks)
                  .Append(" | ").Append(o.Solve.Coins)
                  .Append(" | ").Append(o.Solve.Hits)
                  .Append(" | ").Append(Star2Text(o))
                  .Append(" | ").Append(Star3Text(o))
                  .AppendLine(" |");
            }
            sb.AppendLine();
            int s2yes = 0, s2no = 0;
            foreach (LevelOutcome o in list)
            {
                if (o.CoinTarget == 0 || !o.CoinSearchRan || !o.CoinSearch.Solved) continue;
                if (o.CoinSearch.Coins >= o.CoinTarget) s2yes++; else s2no++;
            }
            sb.AppendLine("Star 2 feasible: " + s2yes + " yes, " + s2no + " no (of the solved levels). Star 3 (no revive) is feasible wherever star 1 is solved.");
            sb.AppendLine();
            sb.AppendLine("## Speedrun (level index 9)");
            sb.AppendLine();
            sb.AppendLine("Slowest = the longest best-first bot time over the regions; margin = timeSec / bot time.");
            sb.AppendLine();
            sb.AppendLine("| level | goal timeSec | bot time (s) | margin |");
            sb.AppendLine("|---|---|---|---|");
            foreach (LevelOutcome o in list)
            {
                if (o.Level.Spec.Goal.Type != GoalType.Speedrun) continue;
                sb.Append("| ").Append(o.Level.Id).Append(" | ").Append(o.Level.Spec.Goal.TimeSec)
                  .Append(" | ").Append((o.Solve.Ticks / 60.0).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)).Append(" | ")
                  .Append(o.Solve.Ticks > 0 ? (o.Level.Spec.Goal.TimeSec * 60.0 / o.Solve.Ticks).ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "n/a")
                  .AppendLine(" |");
            }
            File.WriteAllText(Path.Combine(root, "Tests", "Simulation", "SOLVABILITY_REPORT.md"), sb.ToString());
        }

        [Fact]
        public void AllSeventyFiveLevelsHaveTheirGoalSolvedByTheBot()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) { output.WriteLine("Content not found; skipped"); return; }
            List<LevelOutcome> list = Outcomes();
            Assert.Equal(75, list.Count);
            List<string> failures = new List<string>();
            foreach (LevelOutcome o in list)
                if (!o.Solve.Solved)
                    failures.Add(o.Level.Id + " " + GoalText(o.Level.Spec.Goal) + " d=" + o.Level.Spec.Difficulty + " bestRow=" + o.Solve.BestRow + " nodes=" + o.Solve.Nodes);
            foreach (string f in failures) output.WriteLine(f);
            Assert.Empty(failures);
        }

        [Fact]
        public void SolvedRunsReallyMeetTheirGoal()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            foreach (LevelOutcome o in Outcomes())
            {
                if (!o.Solve.Solved) continue;
                Assert.True(GoalEvaluator.IsComplete(o.Level.Spec, o.Solve.Final), o.Level.Id);
            }
        }

        [Fact]
        public void RegionsOneToFiveLevelsOneAndTwoHaveNoSignatureElements()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            SimConfig cfg = new SimConfig();
            int checkedLevels = 0, signatureLevels = 0;
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 1, 5))
            {
                LevelData d = LevelBuilder.Build(cfg, ll.Spec);
                int index = ll.Spec.LevelIndex;
                int special = 0;
                for (int i = 0; i < d.Platforms.Length; i++) if (d.Platforms[i].Kind != PlatformKind.Normal && d.Platforms[i].Kind != PlatformKind.Crumble) special++;
                if (index <= 2)
                {
                    Assert.Equal(0, special);
                    Assert.Empty(d.Hazards);
                    Assert.Empty(ll.Spec.FixedChunks);
                    checkedLevels++;
                }
                else if (index == 3 || index == 4 || index == 6)
                {
                    // plain reach/collect levels of a signature region show the signature
                    if (ll.Spec.Signature == SignatureKind.SwingingGongs) Assert.True(d.Hazards.Length > 0, ll.Id);
                    else Assert.True(special > 0, ll.Id);
                    signatureLevels++;
                }
            }
            Assert.Equal(10, checkedLevels);
            Assert.Equal(15, signatureLevels);
        }

        [Fact]
        public void ShippedChunkIdsAreAllInTheLibrary()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            int chunked = 0;
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 1, 5))
                foreach (string id in ll.Spec.FixedChunks)
                {
                    Assert.NotNull(ChunkLibrary.Get(id));
                    chunked++;
                }
            Assert.Equal(15, chunked); // L5, L10, L15 in five regions
        }

        [Fact]
        public void LoaderMapsTheContentFields()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            List<LoadedLevel> all = ContentLoader.LoadRegions(root, 1, 5);
            Assert.Equal(75, all.Count);
            LoadedLevel l15 = all.Find(x => x.Id == "lvl_005_15");
            Assert.Equal(GoalType.BossChase, l15.Spec.Goal.Type);
            Assert.Equal(250, l15.Spec.Goal.Value);
            Assert.True(l15.Spec.Ramp);
            Assert.Equal(SignatureKind.SwingingGongs, l15.Spec.Signature);
            Assert.Equal(4, l15.Spec.Class);
            Assert.Equal(new string[] { "chunk_gauntlet_a" }, l15.Spec.FixedChunks);
            Assert.Equal(3, l15.Spec.Stars.Length);
            Assert.Equal(StarSpec.KindCoins, l15.Spec.Stars[1].Kind);
            Assert.True(l15.Spec.Stars[1].Value >= 95); // raised targets (coin_star_targets.json)
            LoadedLevel l9 = all.Find(x => x.Id == "lvl_001_09");
            Assert.Equal(GoalType.ReachFloor, l9.Spec.Goal.Type); // the timed goal was removed (owner, 2026-10-19)
            Assert.Equal(180, l9.Spec.Goal.Value);
            Assert.Equal(SignatureKind.BounceFlowers, l9.Spec.Signature);
            // effective difficulty applies the region offset: d + offset (1 - d)
            LoadedLevel l1 = all.Find(x => x.Id == "lvl_005_01");
            Assert.True(l1.Spec.Difficulty > Fx.FromRatio(5, 100));
            Assert.True(l1.Spec.Difficulty < Fx.FromRatio(9, 100));
        }
    }
}
