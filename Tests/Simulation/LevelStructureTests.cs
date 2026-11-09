using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Doc 25 (owner list 27): level structure of every campaign level of regions 1 to 5: rows, 1.3 platforms per row, the special platform share
    /// ramping inside the level, the goals (no survive wave, hidden gem search or boss chase), combo levels without gems, double gems on levels 8 and 25
    /// and the hidden bonus gem of level 8.
    /// </summary>
    [Trait("Category", "LevelStructure")]
    public class LevelStructureTests
    {
        private readonly ITestOutputHelper output;
        public LevelStructureTests(ITestOutputHelper output) { this.output = output; }

        public static int ExpectedRows(int index)
        {
            // Bug List 8 item 3 (owner): L1 60, L2-5 75, L6-10 90, L11-15 105, L16-20 120, L21-25 150
            if (index == 1) return 60;
            if (index <= 5) return 75;
            if (index <= 10) return 90;
            if (index <= 15) return 105;
            if (index <= 20) return 120;
            return 150;
        }

        private static List<LoadedLevel> All()
        {
            string root = ContentLoader.FindRoot();
            return root == null ? new List<LoadedLevel>() : ContentLoader.LoadRegions(root, 1, 5);
        }

        /// <summary>Kind of the spine platform (the first platform) of every row; row mates are Normal or Crumble and are not counted.</summary>
        public static PlatformKind[] SpineKinds(LevelData d)
        {
            PlatformKind[] kinds = new PlatformKind[d.Rows + 2];
            bool[] seen = new bool[d.Rows + 2];
            foreach (Platform p in d.Platforms)
                if (p.Row >= 1 && p.Row <= d.Rows && !seen[p.Row]) { seen[p.Row] = true; kinds[p.Row] = p.Kind; }
            return kinds;
        }

        [Fact]
        public void EveryLevelHasTheRowsAndTheGoalOfDoc25()
        {
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = All();
            Assert.Equal(125, levels.Count);
            foreach (LoadedLevel ll in levels)
            {
                int idx = ll.Spec.LevelIndex;
                Assert.Equal(ExpectedRows(idx), ll.Spec.RowsFor());
                GoalType g = ll.Spec.Goal.Type;
                Assert.True(g != GoalType.SurviveWave && g != GoalType.BossChase && g != GoalType.CollectCoins, ll.Id + " uses " + g);
                // Bug List 6 item 4: special goals at 5 (combo), 10 (no wall bounce), 15 (hidden gem), 20 (speedrun); everything else reaches the top
                if (idx == 5) Assert.Equal(GoalType.ComboChain, g);
                else if (idx == 10) Assert.Equal(GoalType.NoWallBounce, g);
                else if (idx == 15) Assert.Equal(GoalType.FindHiddenGem, g);
                else if (idx == 20) Assert.Equal(GoalType.Speedrun, g);
                else Assert.Equal(GoalType.ReachFloor, g);
                if (g != GoalType.ComboChain) Assert.Equal(ExpectedRows(idx), ll.Spec.Goal.Value);
                Assert.Empty(ll.Spec.FixedChunks);
                Assert.False(ll.Spec.Ramp);
                LevelData d = LevelBuilder.Build(cfg, ll.Spec);
                Assert.Equal(ExpectedRows(idx), d.Rows);
            }
        }

        [Fact]
        public void PlatformsPerRowIs1Point3OnEveryLevel()
        {
            SimConfig cfg = new SimConfig();
            List<string> bad = new List<string>();
            double sum = 0; int n = 0;
            foreach (LoadedLevel ll in All())
            {
                if (ll.Spec.Goal.Type == GoalType.ComboChain) continue;   // the ladder rungs are one platform per row (or two columns), measured apart
                for (int i = 0; i < 6; i++)
                {
                    LevelData d = LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i)));
                    int platforms = d.Platforms.Count(p => p.Row >= 1);
                    double perRow = platforms / (double)d.Rows;
                    sum += perRow; n++;
                    if (Math.Abs(perRow - 1.3) > 0.06) bad.Add(ll.Id + " seed " + i + ": " + perRow.ToString("F3") + " platforms per row");
                }
            }
            output.WriteLine("mean platforms per row over " + n + " layouts: " + (sum / n).ToString("F3"));
            foreach (string b in bad.Take(20)) output.WriteLine(b);
            Assert.True(bad.Count == 0, bad.Count + " layouts off 1.3, first: " + (bad.Count > 0 ? bad[0] : ""));
        }

        [Fact]
        public void SpecialShareRampsInsideEachLevel()
        {
            SimConfig cfg = new SimConfig();
            List<string> bad = new List<string>();
            double[] lowSum = new double[26], highSum = new double[26];
            int[] cnt = new int[26];
            foreach (LoadedLevel ll in All())
            {
                int idx = ll.Spec.LevelIndex;
                for (int i = 0; i < 6; i++)
                {
                    LevelData d = LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i)));
                    PlatformKind[] k = SpineKinds(d);
                    int rows = d.Rows, third = rows / 3;
                    int ladderLo = d.LadderStartRow, ladderHi = d.LadderEndRow;
                    // free rows only: the combo ladder is plain
                    int lowN = 0, lowS = 0, highN = 0, highS = 0, all = 0, allS = 0;
                    for (int r = 1; r <= rows; r++)
                    {
                        if (ladderLo > 0 && r >= ladderLo && r <= ladderHi) continue;
                        bool sp = k[r] != PlatformKind.Normal;
                        all++; if (sp) allS++;
                        if (r <= third) { lowN++; if (sp) lowS++; }
                        else if (r > rows - third) { highN++; if (sp) highS++; }
                    }
                    double lowPct = 100.0 * lowS / Math.Max(1, lowN), highPct = 100.0 * highS / Math.Max(1, highN), allPct = 100.0 * allS / Math.Max(1, all);
                    lowSum[idx] += lowPct; highSum[idx] += highPct; cnt[idx]++;
                    double mean = (cfg.RampLowPercent[idx] + cfg.RampHighPercent[idx]) / 2.0;
                    // ladder levels carry their plain ladder at the top, so only the mean of the free rows is held to the band there
                    if (Math.Abs(allPct - mean) > 12 && ll.Spec.Goal.Type != GoalType.ComboChain) bad.Add(ll.Id + " seed " + i + ": overall " + allPct.ToString("F0") + "% vs " + mean + "%");
                }
            }
            for (int idx = 1; idx <= 25; idx++)
                if (idx != 5) Assert.True(highSum[idx] / cnt[idx] > lowSum[idx] / cnt[idx] + 6, "level " + idx + ": the top third must carry clearly more specials than the bottom third");
            for (int idx = 1; idx <= 25; idx++)
                output.WriteLine("level " + idx + ": ramp " + cfg.RampLowPercent[idx] + " to " + cfg.RampHighPercent[idx] + " percent; measured bottom third " + (lowSum[idx] / cnt[idx]).ToString("F1") + ", top third " + (highSum[idx] / cnt[idx]).ToString("F1"));
            foreach (string b in bad.Take(20)) output.WriteLine(b);
            Assert.True(bad.Count == 0, bad.Count + " ramp problems, first: " + (bad.Count > 0 ? bad[0] : ""));
        }

        [Fact]
        public void SpecialRowCountIsTheSameForEverySeedOfALevel()
        {
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in All())
            {
                HashSet<int> counts = new HashSet<int>();
                for (int i = 0; i < 12; i++)
                    counts.Add(SpineKinds(LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i)))).Count(x => x != PlatformKind.Normal));
                Assert.True(counts.Count == 1, ll.Id + " special row counts differ between seeds: " + string.Join(",", counts));
            }
        }

        [Fact]
        public void GemsComboLevelsHaveNoneGemLevelsHaveDoublesAndLevel8HasTheBonusGem()
        {
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in All())
            {
                int idx = ll.Spec.LevelIndex;
                int gems = 0, doubles = 0, hidden = 0, hiddenDouble = 0;
                for (int i = 0; i < 10; i++)
                {
                    LevelData d = LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i)));
                    foreach (Pickup p in d.Pickups)
                    {
                        if (p.Kind == PickupKind.Gem) { gems++; if (p.Tag == SimConfig.DoubleGemTag) doubles++; }
                        if (p.Kind == PickupKind.HiddenGem) { hidden++; if (p.Tag == 1) hiddenDouble++; }
                    }
                    Assert.Equal(idx == 8 || ll.Spec.Goal.Type == GoalType.FindHiddenGem ? 1 : 0, d.Pickups.Count(p => p.Kind == PickupKind.HiddenGem));
                    Assert.Equal(3, d.Pickups.Count(p => p.Kind == PickupKind.Star));
                }
                if (ll.Spec.Goal.Type == GoalType.ComboChain) Assert.Equal(0, gems);
                else Assert.True(gems > 0, ll.Id + " has no gems");
                if (idx == 8 || idx == 25) Assert.True(doubles > 0 && doubles < gems, ll.Id + " double gems " + doubles + " of " + gems);
                else Assert.Equal(0, doubles);
                if (idx == 8 || ll.Spec.Goal.Type == GoalType.FindHiddenGem) Assert.True(hiddenDouble >= 0 && hidden == 10);
            }
        }

        [Fact]
        public void DoubleGemsAreWorthTwoAndTheBonusGemCountsItsValue()
        {
            SimConfig cfg = new SimConfig();
            Platform[] plats = { new Platform { Id = 0, Row = 0, Kind = PlatformKind.Normal, X = Fx.FromInt(5), Y = Fx.Zero, Width = Fx.FromInt(10) } };
            LevelData d = LevelData.FromPlatforms(plats);
            d.Pickups = new[]
            {
                new Pickup { Id = 0, Kind = PickupKind.Gem, X = Fx.FromInt(5), Y = Fx.FromRatio(5, 10), Radius = cfg.GemRadius, Tag = SimConfig.DoubleGemTag },
                new Pickup { Id = 1, Kind = PickupKind.Gem, X = Fx.FromInt(5), Y = Fx.FromRatio(5, 10), Radius = cfg.GemRadius, Tag = 1 },
                new Pickup { Id = 2, Kind = PickupKind.HiddenGem, X = Fx.FromInt(5), Y = Fx.FromRatio(5, 10), Radius = cfg.HiddenGemRadius, Tag = 1 },
            };
            Sim sim = new Sim(cfg, d, Fx.Zero, false, false);
            SimState s = sim.NewState();
            sim.Step(ref s, 0);
            Assert.Equal(2 + 2, s.Gems);        // double gem 2 + rare gem 2
            Assert.Equal(2, s.HiddenGems);       // a double bonus gem counts 2
        }

        [Fact]
        public void BotSolvesTheComboAndTheGemLevelsOfEveryRegion()
        {
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = All().Where(l => new[] { 5, 8, 10, 15, 20, 25 }.Contains(l.Spec.LevelIndex)).ToList();
            string[][] failures = new string[levels.Count][];
            Parallel.For(0, levels.Count, li =>
            {
                LoadedLevel ll = levels[li];
                List<string> f = new List<string>();
                for (int i = 0; i < 12; i++)
                {
                    LevelSpec s = ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i + 900));
                    LevelData d = LevelBuilder.Build(cfg, s);
                    BotResult r = new LevelBot(new Sim(cfg, d, s), s, d, SolvabilityT4Tests.NodeBudget * Math.Max(1, s.RowsFor() / 60)).Solve();
                    if (!r.Solved) f.Add(ll.Id + " seed " + (i + 900) + ": best row " + r.BestRow + "/" + d.Rows);
                    else if (!GoalEvaluator.IsComplete(s, r.Final)) f.Add(ll.Id + " seed " + (i + 900) + ": goal not met");
                }
                failures[li] = f.ToArray();
            });
            List<string> all = failures.SelectMany(x => x).ToList();
            foreach (string x in all.Take(20)) output.WriteLine(x);
            Assert.True(all.Count == 0, all.Count + " unsolved, first: " + (all.Count > 0 ? all[0] : ""));
        }

        [Fact]
        public void Level8BonusGemIsReachableOnTheSamePlatformRules()
        {
            // The bonus gem uses the hidden gem machinery: swap the goal for the legacy hidden-gem search on the same layout and the bot must be able to take it.
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = All().Where(l => l.Spec.LevelIndex == 8).ToList();
            string[][] failures = new string[levels.Count][];
            Parallel.For(0, levels.Count, li =>
            {
                LoadedLevel ll = levels[li];
                List<string> f = new List<string>();
                for (int i = 0; i < 20; i++)
                {
                    LevelSpec s = ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i + 1300));
                    s.Rows = 120;
                    s.Goal = new GoalSpec { Type = GoalType.FindHiddenGem };
                    LevelData d = LevelBuilder.Build(cfg, s);
                    BotResult r = new LevelBot(new Sim(cfg, d, s), s, d, SolvabilityT4Tests.NodeBudget * 2).Solve();
                    if (!r.Solved) f.Add(ll.Id + " seed " + (i + 1300) + ": bonus gem not taken, best row " + r.BestRow);
                }
                failures[li] = f.ToArray();
            });
            List<string> all = failures.SelectMany(x => x).ToList();
            foreach (string x in all.Take(20)) output.WriteLine(x);
            Assert.True(all.Count == 0, all.Count + " layouts without a reachable bonus gem, first: " + (all.Count > 0 ? all[0] : ""));
        }
    }
}
