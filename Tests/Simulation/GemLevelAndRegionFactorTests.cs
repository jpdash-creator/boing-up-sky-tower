using System.IO;
using System.Linq;
using System.Text.Json;
using FluffyClimbers.Economy;
using Xunit;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>Economy review 2026-11-13: the hidden gem lives ONLY on the FindHiddenGem level (15) in all regions (the level 8 bonus gem is gone) and is a plain goal object; the main-goal coin rewards use the region coin factor of the level payout.</summary>
    public class GemLevelAndRegionFactorTests
    {
        [Fact]
        public void GemLevelsAreOnlyTheFindHiddenGemLevels()
        {
            Assert.False(GemLevels.IsGemLevel(8, GoalType.ReachFloor));
            Assert.True(GemLevels.IsGemLevel(15, GoalType.FindHiddenGem));
            Assert.True(GemLevels.IsGemLevel(15, "findHiddenGem"));
            Assert.False(GemLevels.IsGemLevel(15, GoalType.ReachFloor));
            Assert.False(GemLevels.IsGemLevel(7, "reachFloor"));
        }

        [Fact]
        public void HiddenGemOfLevel15IsAPlainGoalObjectInEveryRegionAndLevel8HasNone()
        {
            string root = ContentLoader.FindRoot();
            Assert.NotNull(root);
            SimConfig cfg = new SimConfig();
            for (int region = 1; region <= 5; region++)
            {
                var levels = ContentLoader.LoadRegions(root, region, region);
                LoadedLevel l8 = levels.First(l => l.Spec.LevelIndex == 8);
                Assert.False(GemLevels.IsGemLevel(8, l8.Spec.Goal.Type), l8.Id);
                Assert.DoesNotContain(LevelBuilder.Build(cfg, l8.Spec).Pickups, p => p.Kind == PickupKind.HiddenGem);
                LoadedLevel ll = levels.First(l => l.Spec.LevelIndex == 15);
                Assert.True(GemLevels.IsGemLevel(15, ll.Spec.Goal.Type), ll.Id);
                int hidden = 0, doubles = 0;
                for (int i = 0; i < 60; i++)
                {
                    LevelData d = LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i)));
                    foreach (Pickup p in d.Pickups)
                        if (p.Kind == PickupKind.HiddenGem) { hidden++; if (p.Tag == 1) doubles++; }
                }
                Assert.Equal(60, hidden);
                Assert.Equal(0, doubles);   // never a double: its 2 gems are the milestone reward, the pickup pays none
            }
        }

        [Fact]
        public void GoalCoinFactorEqualsTheRegionCoinFactorOfTheLevelPayout()
        {
            string root = ContentLoader.FindRoot();
            Assert.NotNull(root);
            for (int r = 1; r <= 5; r++)
            {
                string f = Directory.GetFiles(Path.Combine(root, "Content", "regions"), "region_" + r.ToString("D3") + "_*.json")[0];
                using (JsonDocument d = JsonDocument.Parse(File.ReadAllText(f)))
                {
                    double factor = d.RootElement.GetProperty("coinFactor").GetDouble();
                    Assert.Equal((int)System.Math.Round(factor * 100), RegionGoalTable.CoinPercent[r - 1]);
                    Assert.Equal(CoinPayoutRules.RegionFactorPercent[r - 1], RegionGoalTable.CoinPercent[r - 1]);
                    // 200-coin goal reward: same factor, rounded to 10 coins
                    Assert.Equal((200 * RegionGoalTable.CoinPercent[r - 1] / 100 + 5) / 10 * 10, RegionGoalTable.ScaledCoins(200, r));
                }
            }
        }
    }
}
