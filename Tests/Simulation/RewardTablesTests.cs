using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BoingUpSkyTower.Economy;
using Xunit;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>
    /// The Main tab reward table (RegionGoalTable, docs/39): shape, monotonic targets, the gem budget per region and the economic totals. Pure data, no engine.
    /// Writes Tests/Simulation/REWARDS_TABLES.md (the numbers quoted in docs/39). Category: Economy.
    /// </summary>
    public class RewardTablesTests
    {
        // value rates (docs/27): 1 gem = 20 coins, a random power-up unit is worth 60.7 gems, a Common hero copy is assumed worth 50 gems (a third of an Uncommon at 150)
        const double CoinGe = 0.05, PowerUpGe = 60.7, HeroGe = 50.0;

        static double Ge(int gems, int coins, int powerUps, int heroes = 0) { return gems + coins * CoinGe + powerUps * PowerUpGe + heroes * HeroGe; }

        struct Sum
        {
            public int Gems, Coins, PowerUps, Heroes, Goals;
            public double GeValue { get { return Ge(Gems, Coins, PowerUps, Heroes); } }
        }

        static Sum Total(IEnumerable<RegionGoal> goals, Func<RegionGoal, bool> pick)
        {
            var s = new Sum();
            foreach (RegionGoal g in goals)
            {
                if (!pick(g)) continue;
                s.Gems += g.Gems; s.Coins += g.Coins; s.PowerUps += g.PowerUps; s.Goals++;
                if (g.HeroRegion > 0) s.Heroes++;   // the random-hero extras (HeroRegion -1) were never counted in the budgets
            }
            return s;
        }

        [Fact]
        public void RegionsOneToFiveHaveGoalsAndSixToThirtyHaveNone()
        {
            for (int r = 1; r <= RegionGoalTable.TotalRegions; r++)
            {
                int n = RegionGoalTable.Build(r).Count;
                if (r <= RegionGoalTable.PlayableRegions) Assert.Equal(49, n);
                else Assert.Equal(0, n);
            }
            Assert.Empty(RegionGoalTable.Build(0));
            Assert.Empty(RegionGoalTable.Build(-3));
            Assert.Empty(RegionGoalTable.Build(99));
        }

        [Fact]
        public void IdsAreStableUniqueAndTypeTokenised()
        {
            var seen = new HashSet<string>();
            for (int r = 1; r <= 5; r++)
                foreach (RegionGoal g in RegionGoalTable.Build(r))
                {
                    Assert.True(seen.Add(g.Id), g.Id);
                    Assert.Equal("g_" + r + "_" + RegionGoalTable.Token(g.Type) + "_" + g.Tier, g.Id);
                    Assert.Equal(RegionGoalTable.Token(g.Type), g.Stat);
                }
            Assert.Equal(5 * 49, seen.Count);
            Assert.Contains("g_1_stars_1", seen);
            Assert.Contains("g_3_endless_11", seen);
            Assert.Contains("g_5_levels_5", seen);
        }

        [Fact]
        public void TargetsRiseStrictlyWithinATypeAndNeverFallBetweenRegions()
        {
            var byRegion = new Dictionary<int, List<RegionGoal>>();
            for (int r = 1; r <= 5; r++) byRegion[r] = RegionGoalTable.Build(r);
            foreach (RegionGoalType t in RegionGoalTable.Order)
            {
                for (int r = 1; r <= 5; r++)
                {
                    int prev = 0;
                    foreach (RegionGoal g in byRegion[r])
                    {
                        if (g.Type != t) continue;
                        Assert.True(g.Target > prev, g.Id + " target " + g.Target + " must beat " + prev);
                        prev = g.Target;
                    }
                }
                for (int r = 2; r <= 5; r++)
                {
                    var a = byRegion[r - 1].FindAll(g => g.Type == t);
                    var b = byRegion[r].FindAll(g => g.Type == t);
                    for (int i = 0; i < a.Count; i++)
                    {
                        Assert.True(b[i].Target >= a[i].Target, b[i].Id + " is not easier than " + a[i].Id);
                        Assert.True(b[i].Coins >= a[i].Coins, b[i].Id + " coins");
                        Assert.Equal(a[i].Gems, b[i].Gems);
                        Assert.Equal(a[i].PowerUps, b[i].PowerUps);
                    }
                }
            }
        }

        [Fact]
        public void TargetsFitTheRegionsCaps()
        {
            for (int r = 1; r <= 5; r++)
                foreach (RegionGoal g in RegionGoalTable.Build(r))
                {
                    if (g.Type == RegionGoalType.Stars) Assert.True(g.Target <= RegionGoalTable.StarsPerRegion, g.Id);
                    if (g.Type == RegionGoalType.Levels || g.Type == RegionGoalType.ThreeStarLevels) Assert.True(g.Target <= RegionGoalTable.LevelsPerRegion, g.Id);
                }
            var stars = RegionGoalTable.Build(1).FindAll(g => g.Type == RegionGoalType.Stars);
            Assert.Equal(75, stars[stars.Count - 1].Target);
            var levels = RegionGoalTable.Build(1).FindAll(g => g.Type == RegionGoalType.Levels);
            Assert.Equal(new[] { 5, 10, 15, 20, 25 }, levels.ConvertAll(g => g.Target).ToArray());
            var three = RegionGoalTable.Build(1).FindAll(g => g.Type == RegionGoalType.ThreeStarLevels);
            Assert.Equal(new[] { 5, 13, 25 }, three.ConvertAll(g => g.Target).ToArray());
            var endless = RegionGoalTable.Build(2).FindAll(g => g.IsEndless);
            Assert.Equal(new[] { 100, 200, 500, 1000, 1500, 2000, 3000, 4000, 5000, 7500, 10000 }, endless.ConvertAll(g => g.Target).ToArray());
        }

        [Fact]
        public void OnlyTheLastLevelsGoalGivesTheRegionHero()
        {
            for (int r = 1; r <= 5; r++)
            {
                int heroes = 0;
                foreach (RegionGoal g in RegionGoalTable.Build(r))
                    if (g.HeroRegion > 0)
                    {
                        heroes++;
                        Assert.Equal(RegionGoalType.Levels, g.Type);
                        Assert.Equal(25, g.Target);
                        Assert.Equal(r, g.HeroRegion);
                    }
                Assert.Equal(1, heroes);
            }
        }

        [Fact]
        public void EveryRegionStaysInsideTheTightGemBudget()
        {
            // owner: about 80 gems per region from the Main goals, the endless tiers (and top goals) may pay slightly more; coins and power-ups carry the value
            for (int r = 1; r <= 5; r++)
            {
                List<RegionGoal> goals = RegionGoalTable.Build(r);
                // the gold medal tiers (doc 46) pay the star tiers again by owner decision and are checked on their own (GoldMedalTiersPayLikeTheStarTiers)
                Sum core = Total(goals, g => !g.IsEndless && !g.IsMedal), endless = Total(goals, g => g.IsEndless), all = Total(goals, g => !g.IsMedal);
                Assert.InRange(core.Gems, 65, 85);
                Assert.True(endless.Gems <= 30, "endless gems " + endless.Gems);
                Assert.True(all.Gems <= 105, "region " + r + " gems " + all.Gems);
                Assert.Equal(2, core.PowerUps);
                Assert.True(endless.PowerUps <= 4);
                Assert.True(core.Coins <= 5600, "region " + r + " core coins " + core.Coins);
                Assert.True(core.GeValue <= 560, "region " + r + " core value " + core.GeValue);
                Assert.True(all.GeValue <= 1300, "region " + r + " total value " + all.GeValue);
                Assert.True(endless.Gems > core.Gems * 0.2 && endless.Gems < core.Gems * 0.5, "endless pays slightly more than a pure tight policy, not a lot more");
            }
        }

        [Fact]
        public void GoldMedalTiersPayLikeTheStarTiersAndTargetFiveToTwentyFiveLevels()
        {
            // doc 46 section 8: "each medal tier pays the same as the star tier at the same position"; no new economy numbers; tier 5 gives the same random Common hero as the star tier 5 (owner 2026-11-09)
            Assert.Equal(RegionGoalTable.StarsTiers.Length, RegionGoalTable.GoldMedalTiers.Length);
            for (int i = 0; i < RegionGoalTable.StarsTiers.Length; i++)
            {
                Assert.Equal(RegionGoalTable.StarsTiers[i].Gems, RegionGoalTable.GoldMedalTiers[i].Gems);
                Assert.Equal(RegionGoalTable.StarsTiers[i].Coins, RegionGoalTable.GoldMedalTiers[i].Coins);
                Assert.Equal(RegionGoalTable.StarsTiers[i].PowerUps, RegionGoalTable.GoldMedalTiers[i].PowerUps);
            }
            for (int r = 1; r <= 5; r++)
            {
                List<RegionGoal> stars = RegionGoalTable.Build(r).FindAll(g => g.Type == RegionGoalType.Stars);
                List<RegionGoal> gold = RegionGoalTable.Build(r).FindAll(g => g.IsMedal);
                Assert.Equal(new[] { 5, 10, 15, 20, 25 }, gold.ConvertAll(g => g.Target).ToArray());
                Assert.Equal(5, gold.Count);
                for (int i = 0; i < 5; i++)
                {
                    Assert.Equal(stars[i].Gems, gold[i].Gems);
                    Assert.Equal(stars[i].Coins, gold[i].Coins);
                    Assert.Equal(stars[i].PowerUps, gold[i].PowerUps);
                    Assert.Equal(stars[i].HeroRegion, gold[i].HeroRegion);   // 0, except tier 5: a random Common hero like the star tier 5
                    Assert.Equal(i == 4 ? RewardEntry.RandomOpenRegionHero : 0, gold[i].HeroRegion);
                    Assert.Equal("g_" + r + "_gold_" + (i + 1), gold[i].Id);
                    Assert.Equal("gold", gold[i].Stat);
                    Assert.True(gold[i].Target <= RegionGoalTable.LevelsPerRegion);
                }
            }
        }

        [Fact]
        public void EveryGoalPaysSomethingAndNoSingleGoalPaysTooMuch()
        {
            for (int r = 1; r <= 5; r++)
                foreach (RegionGoal g in RegionGoalTable.Build(r))
                {
                    Assert.True(g.Gems > 0 || g.Coins > 0 || g.PowerUps > 0, g.Id);
                    Assert.True(g.Gems <= 8, g.Id + " gems");
                    Assert.True(g.Coins <= 6000, g.Id + " coins");
                    Assert.True(g.PowerUps <= 2, g.Id + " power-ups");
                }
        }

        [Fact]
        public void WritesTheRewardTablesReport()
        {
            string root = ContentLoader.FindRoot();
            Assert.NotNull(root);
            var sb = new StringBuilder();
            sb.AppendLine("# Rewards tables (generated by RewardTablesTests, docs/39)");
            sb.AppendLine();
            sb.AppendLine("Generated from `RegionGoalTable` (Assets/_Game/Economy/RegionGoals.cs). GE = gem-equivalent: 1 gem, 20 coins, 60.7 per power-up unit, 50 per hero copy (assumption).");
            sb.AppendLine();
            sb.AppendLine("## Per region (all goals)");
            sb.AppendLine();
            sb.AppendLine("| Region | Goals | Gems core | Gems endless | Gems total | Coins core | Coins endless | PU core | PU endless | Hero | GE core | GE total |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
            var grand = new Sum(); var grandCore = new Sum();
            for (int r = 1; r <= 5; r++)
            {
                List<RegionGoal> goals = RegionGoalTable.Build(r);
                Sum core = Total(goals, g => !g.IsEndless && !g.IsMedal), end = Total(goals, g => g.IsEndless), all = Total(goals, g => true);
                sb.AppendLine("| " + r + " | " + all.Goals + " | " + core.Gems + " | " + end.Gems + " | " + all.Gems + " | " + core.Coins + " | " + end.Coins + " | " + core.PowerUps + " | " + end.PowerUps + " | " + all.Heroes + " | " + core.GeValue.ToString("0") + " | " + all.GeValue.ToString("0") + " |");
                grand.Gems += all.Gems; grand.Coins += all.Coins; grand.PowerUps += all.PowerUps; grand.Heroes += all.Heroes; grand.Goals += all.Goals;
                grandCore.Gems += core.Gems; grandCore.Coins += core.Coins; grandCore.PowerUps += core.PowerUps; grandCore.Heroes += core.Heroes;
            }
            sb.AppendLine("| **1-5 total** | " + grand.Goals + " | " + grandCore.Gems + " | " + (grand.Gems - grandCore.Gems) + " | " + grand.Gems + " | " + grandCore.Coins + " | " + (grand.Coins - grandCore.Coins) + " | " + grandCore.PowerUps + " | " + (grand.PowerUps - grandCore.PowerUps) + " | " + grand.Heroes + " | " + grandCore.GeValue.ToString("0") + " | " + grand.GeValue.ToString("0") + " |");
            foreach (int region in new[] { 1, 5 })
            {
                sb.AppendLine();
                sb.AppendLine("## Region " + region + " goals");
                sb.AppendLine();
                sb.AppendLine("| Id | Goal | Target | Gems | Coins | PU | Hero |");
                sb.AppendLine("|---|---|---|---|---|---|---|");
                foreach (RegionGoal g in RegionGoalTable.Build(region))
                    sb.AppendLine("| " + g.Id + " | " + g.Title + " | " + g.Target + " | " + g.Gems + " | " + g.Coins + " | " + g.PowerUps + " | " + (g.HeroRegion != 0 ? "Common hero" : "") + " |");
            }
            File.WriteAllText(Path.Combine(root, "Tests", "Simulation", "REWARDS_TABLES.md"), sb.ToString());
        }
    }
}
