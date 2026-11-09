using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Issue list 24 (owner 2026-10-25), points 5 and 6: gem spot share and rarity, gem budget per region, campaign coin payout
    /// (70 + 60 per star + 5 per combo step, times the region factor), 50 percent replay rule, and the coin income of a perfect player.
    /// Category: Economy.
    /// </summary>
    public class GemCoinEconomyTests
    {
        const int Regions = 5;
        // Owner issue list 26 (2026-10-31): 30 percent fewer gems in game, so the bands are the old ones x0.7 (70..110 became about 55..75, hard cap 130 became 91).
        // Bug List 6 item 4 moved to 25 levels per region; the owner then decided (2026-11-09) to scale the gem share down by about 0.55 so the income per region stays
        // at the old bands: 55..75 placed gem value per region, hard cap 91 (placed plus milestone gems).
        const int TargetMin = 55, TargetMax = 75, HardCap = 91;
        static readonly double[] RegionFactor = { 1.0, 1.03, 1.06, 1.09, 1.12 };

        readonly ITestOutputHelper output;
        public GemCoinEconomyTests(ITestOutputHelper output) { this.output = output; }

        static double D(Fx f) { return f.Raw / 65536.0; }

        // gem spot share (per mille) -> per region { placed gem pickups, placed gem value }
        static readonly Dictionary<int, int[][]> cache = new Dictionary<int, int[][]>();
        static readonly object gate = new object();

        /// <summary>[region 1..5] = { pickups, value } of every gem the real LevelBuilder places in the 25 levels of the region.</summary>
        static int[][] PlacedPerRegion(int permille)
        {
            lock (gate)
            {
                int[][] r;
                if (cache.TryGetValue(permille, out r)) return r;
                string root = ContentLoader.FindRoot();
                SimConfig cfg = new SimConfig();
                cfg.GemSpotPermille = permille;
                r = new int[Regions + 1][];
                for (int g = 1; g <= Regions; g++)
                {
                    int count = 0, value = 0;
                    foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, g, g))
                    {
                        LevelData d = LevelBuilder.Build(cfg, ll.Spec);
                        foreach (Pickup p in d.Pickups)
                            if (p.Kind == PickupKind.Gem) { count++; value += p.Tag == SimConfig.DoubleGemTag ? cfg.DoubleGemValue : cfg.GemValues[p.Tag]; }
                    }
                    r[g] = new[] { count, value };
                }
                cache[permille] = r;
                return r;
            }
        }

        // Milestone gems a first clear can pay in a region (L5, L10, L15, L20, L25), same arithmetic as RewardCalculator.Compute.
        static int MilestoneGems(int region)
        {
            string root = ContentLoader.FindRoot();
            string file = Directory.GetFiles(Path.Combine(root, "Content", "levels"), "region_" + region.ToString("D3") + "_*_levels.json")[0];
            int total = 0;
            using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file)))
                foreach (JsonElement lv in doc.RootElement.EnumerateArray())
                {
                    if (lv.GetProperty("index").GetInt32() % 5 != 0) continue;   // RewardCalculator.IsMilestone: the special levels 5, 10, 15, 20 and the last level 25
                    JsonElement gems;
                    if (!lv.GetProperty("rewards").TryGetProperty("gems", out gems)) continue;
                    JsonElement v;
                    int guaranteed = gems.TryGetProperty("firstClearGuaranteed", out v) ? v.GetInt32() : 0;
                    total += CoinPayoutRules.MilestoneGems(lv.GetProperty("seed").GetInt32(), gems.GetProperty("chance").GetDouble(),
                        gems.GetProperty("min").GetInt32(), gems.GetProperty("max").GetInt32(), guaranteed);
                }
            return total;
        }

        // ---------------------------------------------------------------- point 5: gems

        [Fact]
        [Trait("Category", "Economy")]
        public void GemSpotSharesAreOrderedAndRarityWeightsAreUnchanged()
        {
            int[] shares = SimConfig.DifficultyGemPermille;
            Assert.Equal(5, shares.Length);
            for (int i = 1; i < shares.Length; i++) Assert.True(shares[i] >= shares[i - 1], "harder difficulties never place fewer gems");
            Assert.True(shares[4] <= 50, "never above the owner's top share of 5 percent");
            Assert.Equal(new[] { 1, 2, 5, 10 }, new SimConfig().GemValues);
            Assert.Equal(new[] { 70, 22, 7, 1 }, new SimConfig().GemRarityPercent);
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void PlacedGemsPerRegionAreWithin55To75OnAllFiveDifficulties()
        {
            for (int d = 0; d < SimConfig.DifficultyGemPermille.Length; d++)
            {
                int pm = SimConfig.DifficultyGemPermille[d];
                int[][] placed = PlacedPerRegion(pm);
                double sum = 0;
                string line = "difficulty " + d + " (" + pm / 10.0 + "%):";
                for (int g = 1; g <= Regions; g++)
                {
                    sum += placed[g][1];
                    line += " R" + g + "=" + placed[g][1] + " (" + placed[g][0] + " pickups)";
                    if (placed[g][1] < TargetMin || placed[g][1] > TargetMax) output.WriteLine(line);
                    Assert.InRange(placed[g][1], TargetMin, TargetMax);
                }
                output.WriteLine(line + " average " + (sum / Regions).ToString("F1"));
                Assert.InRange(sum / Regions, TargetMin, TargetMax);
            }
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void TheRetunedSharesPlaceFewerGemsThanTheOldOnes()
        {
            // Owner 2026-11-09: with 25 levels per region the shares were scaled down by about 0.55 (27 / 27 / 29 / 30 / 32 to 14 / 14 / 15 / 15 / 15; the 55..75 band
            // does not allow more) so the income per region stays at the old level. Compared here on the same 25-level campaign against the previous shares.
            int[] old = { 27, 27, 29, 30, 32 };
            Assert.Equal(new[] { 16, 16, 17, 17, 17 }, SimConfig.DifficultyGemPermille);   // Bug List 8 item 3: was 14 / 14 / 15 / 15 / 15 before the levels got shorter
            double before = 0, after = 0;
            for (int d = 0; d < old.Length; d++)
                for (int g = 1; g <= Regions; g++) { before += PlacedPerRegion(old[d])[g][1]; after += PlacedPerRegion(SimConfig.DifficultyGemPermille[d])[g][1]; }
            output.WriteLine("placed gem value over 5 difficulties x 5 regions: old shares " + before + ", new shares " + after + " = " + (after / before * 100).ToString("F1") + " percent");
            Assert.InRange(after / before, 0.45, 0.60);
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void RarityOver100000SpotsFollows70_22_7_1()
        {
            SimConfig cfg = new SimConfig();
            const int N = 100000;
            int[] tier = new int[4];
            for (int i = 0; i < N; i++) tier[LevelBuilder.GemTierAt(cfg, i)]++;
            double[] want = { 70, 22, 7, 1 }, tol = { 0.5, 0.5, 0.3, 0.2 };
            for (int t = 0; t < 4; t++)
            {
                double pct = tier[t] * 100.0 / N;
                output.WriteLine("tier " + t + ": " + pct.ToString("F2") + "% (want " + want[t] + ")");
                Assert.InRange(pct, want[t] - tol[t], want[t] + tol[t]);
            }
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void PlacedGemTiersOfTheRealLevelsAreNearTheWeights()
        {
            SimConfig cfg = new SimConfig();
            cfg.GemSpotPermille = SimConfig.DifficultyGemPermille[4];
            string root = ContentLoader.FindRoot();
            int[] tier = new int[4];
            int n = 0;
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 1, Regions))
                foreach (Pickup p in LevelBuilder.Build(cfg, ll.Spec).Pickups)
                    if (p.Kind == PickupKind.Gem && p.Tag < 4) { tier[p.Tag]++; n++; }   // double gems (Tag 4, levels 8 and 25) are not a rarity tier
            output.WriteLine("placed gems " + n + ": " + tier[0] + "/" + tier[1] + "/" + tier[2] + "/" + tier[3]);
            Assert.True(n > 200, "enough gems placed to judge: " + n);
            Assert.InRange(tier[0] * 100.0 / n, 62, 78);
            Assert.InRange(tier[1] * 100.0 / n, 14, 30);
            Assert.InRange(tier[2] * 100.0 / n, 3, 12);
            Assert.InRange(tier[3] * 100.0 / n, 0, 3);
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void GemBudgetPerRegionStaysUnderTheHardCap()
        {
            const int earnedChestGems = 0;   // issue list 24 point 5: no earned chests are granted for now
            foreach (int pm in SimConfig.DifficultyGemPermille)
            {
                int[][] placed = PlacedPerRegion(pm);
                for (int g = 1; g <= Regions; g++)
                {
                    int milestone = MilestoneGems(g);
                    int budget = placed[g][1] + milestone + earnedChestGems;
                    output.WriteLine("permille " + pm + " region " + g + ": pickups " + placed[g][1] + " + milestone " + milestone + " + chests " + earnedChestGems + " = " + budget);
                    Assert.True(budget <= HardCap, "region " + g + " at " + pm + " per mille: " + budget);
                }
            }
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void TheFirstClearOfTheLastLevelAlwaysPaysAtLeastFiveGems()
        {
            string root = ContentLoader.FindRoot();
            for (int g = 1; g <= Regions; g++)
            {
                string file = Directory.GetFiles(Path.Combine(root, "Content", "levels"), "region_" + g.ToString("D3") + "_*_levels.json")[0];
                using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file)))
                    foreach (JsonElement lv in doc.RootElement.EnumerateArray())
                    {
                        if (lv.GetProperty("index").GetInt32() != 25) continue;
                        JsonElement gems = lv.GetProperty("rewards").GetProperty("gems");
                        int amount = CoinPayoutRules.MilestoneGems(lv.GetProperty("seed").GetInt32(), gems.GetProperty("chance").GetDouble(),
                            gems.GetProperty("min").GetInt32(), gems.GetProperty("max").GetInt32(), gems.GetProperty("firstClearGuaranteed").GetInt32());
                        Assert.True(amount >= 5, "region " + g + " level 25 pays " + amount);
                    }
            }
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void GemsNeverSitOnTopOfAStarOrEachOther()
        {
            // Regression for the Q16.16 overflow in the pickup distance test: far pairs (more than 181 u apart) were reported as touching.
            SimConfig cfg = new SimConfig();
            cfg.GemSpotPermille = SimConfig.DifficultyGemPermille[4];
            string root = ContentLoader.FindRoot();
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 1, 1))
            {
                Pickup[] ps = LevelBuilder.Build(cfg, ll.Spec).Pickups;
                for (int i = 0; i < ps.Length; i++)
                    for (int j = i + 1; j < ps.Length; j++)
                    {
                        if (ps[i].Kind == PickupKind.Coin || ps[j].Kind == PickupKind.Coin) continue;
                        double dx = D(ps[i].X) - D(ps[j].X), dy = D(ps[i].Y) - D(ps[j].Y);
                        Assert.True(Math.Sqrt(dx * dx + dy * dy) >= 1.0, ll.Id + ": pickups " + i + " and " + j + " overlap");
                    }
            }
        }

        // ---------------------------------------------------------------- point 6: coins

        [Fact]
        [Trait("Category", "Economy")]
        public void ClearPayoutIs70Plus60PerStarBeforeComboAndFactor()
        {
            Assert.Equal(70, CoinPayoutRules.BaseCoins(0));
            Assert.Equal(130, CoinPayoutRules.BaseCoins(1));
            Assert.Equal(190, CoinPayoutRules.BaseCoins(2));
            Assert.Equal(250, CoinPayoutRules.BaseCoins(3));
            Assert.Equal(5, CoinPayoutRules.ComboStepCoins);
        }

        [Theory]
        [Trait("Category", "Economy")]
        [InlineData(1, 1.00)]
        [InlineData(2, 1.03)]
        [InlineData(3, 1.06)]
        [InlineData(4, 1.09)]
        [InlineData(5, 1.12)]
        public void PayoutFormulaForZeroToThreeStarsAndComboInEveryRegion(int region, double factor)
        {
            Assert.Equal(RegionFactor[region - 1], factor);
            for (int stars = 0; stars <= 3; stars++)
                foreach (int comboSteps in new[] { 0, 1, 7, 40 })
                {
                    int combo = comboSteps * CoinPayoutRules.ComboStepCoins;
                    long expected = (long)Math.Round((70 + 60 * stars) * factor, MidpointRounding.AwayFromZero) + (long)Math.Round(combo * factor, MidpointRounding.AwayFromZero);
                    Assert.Equal((int)expected, CoinPayoutRules.Payout(stars, combo, factor, false));
                }
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void PayoutMatchesTheOwnerSpotValues()
        {
            Assert.Equal(70, CoinPayoutRules.Payout(0, 0, 1.0, false));
            Assert.Equal(130, CoinPayoutRules.Payout(1, 0, 1.0, false));
            Assert.Equal(190, CoinPayoutRules.Payout(2, 0, 1.0, false));
            Assert.Equal(250, CoinPayoutRules.Payout(3, 0, 1.0, false));
            Assert.Equal(250 + 5 * 12, CoinPayoutRules.Payout(3, 60, 1.0, false));
            Assert.Equal(280, CoinPayoutRules.Payout(3, 0, 1.12, false));
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void ReplayPaysHalfOnlyOnceTheLevelHasThreeStars()
        {
            Assert.False(CoinPayoutRules.IsReducedReplay(0));
            Assert.False(CoinPayoutRules.IsReducedReplay(1));
            Assert.False(CoinPayoutRules.IsReducedReplay(2));
            Assert.True(CoinPayoutRules.IsReducedReplay(3));
            Assert.Equal(50, CoinPayoutRules.ReplayPercent);
            for (int stars = 0; stars <= 3; stars++)
            {
                int full = CoinPayoutRules.Payout(stars, 20, 1.0, false), half = CoinPayoutRules.Payout(stars, 20, 1.0, true);
                Assert.Equal(full / 2, half);
            }
            Assert.Equal(125, CoinPayoutRules.Payout(3, 0, 1.0, true));
            Assert.Equal(65, CoinPayoutRules.Payout(1, 0, 1.0, true));
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void PerfectPlayerEarnsAbout6250CoinsPerRegion()
        {
            string root = ContentLoader.FindRoot();
            SimConfig cfg = new SimConfig();
            for (int g = 1; g <= Regions; g++)
            {
                double factor = RegionFactor[g - 1];
                long total = 0, noCombo = 0; int levels = 0;
                foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, g, g))
                {
                    LevelData data = LevelBuilder.Build(cfg, ll.Spec);
                    Sim sim = new Sim(cfg, data, ll.Spec);
                    BotResult br = new LevelBot(sim, ll.Spec, data, 20000 * Math.Max(1, ll.Spec.RowsFor() / 60)).Solve();
                    total += CoinPayoutRules.Payout(3, br.Solved ? br.Coins : 0, factor, false);   // a perfect player: 3 stars every level, the bot's combo line
                    noCombo += CoinPayoutRules.Payout(3, 0, factor, false);
                    levels++;
                }
                double normalised = total / factor;
                output.WriteLine("region " + g + ": " + levels + " levels, " + total + " coins (" + noCombo + " without combo), " + normalised.ToString("F0") + " at factor 1.00");
                Assert.Equal(25, levels);
                Assert.InRange(normalised, 6250 * 0.9, 6250 * 1.1);   // 25 levels x about 250 coins (was 15 x 250 = 3750)
            }
        }
    }
}
