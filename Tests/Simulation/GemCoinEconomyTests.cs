using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Economy review 2026-11-13 (owner), levels: NO gem pickups on the rows (every gem spot share is 0), gems only as the fixed milestone gems of levels 5/10/15/20/25 (1, 1, 2, 2, 3),
    /// campaign coin payout 56 + 48 per star + 4 per combo step (times the region factor), replays of a 3-star level pay 25 percent, and the coin income of a perfect player.
    /// Category: Economy.
    /// </summary>
    public class GemCoinEconomyTests
    {
        const int Regions = 5;
        static readonly double[] RegionFactor = { 1.0, 1.03, 1.06, 1.09, 1.12 };

        readonly ITestOutputHelper output;
        public GemCoinEconomyTests(ITestOutputHelper output) { this.output = output; }

        static double D(Fx f) { return f.Raw / 65536.0; }

        /// <summary>The gem pickups the real LevelBuilder places in the 25 levels of a region at the given gem spot share (0 = the shipped setting).</summary>
        static int PlacedGemPickups(int region, int permille)
        {
            string root = ContentLoader.FindRoot();
            SimConfig cfg = new SimConfig();
            cfg.GemSpotPermille = permille;
            int count = 0;
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, region, region))
                foreach (Pickup p in LevelBuilder.Build(cfg, ll.Spec).Pickups)
                    if (p.Kind == PickupKind.Gem) count++;
            return count;
        }

        /// <summary>The gems the level JSON pays on the first clear, per level index (0 where it pays none).</summary>
        static int[] JsonGemsPerLevel(int region)
        {
            string root = ContentLoader.FindRoot();
            string file = Directory.GetFiles(Path.Combine(root, "Content", "levels"), "region_" + region.ToString("D3") + "_*_levels.json")[0];
            int[] gems = new int[26];
            using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file)))
                foreach (JsonElement lv in doc.RootElement.EnumerateArray())
                {
                    JsonElement g, v;
                    if (!lv.GetProperty("rewards").TryGetProperty("gems", out g)) continue;
                    gems[lv.GetProperty("index").GetInt32()] = g.TryGetProperty("firstClearGuaranteed", out v) ? v.GetInt32() : 0;
                }
            return gems;
        }

        // ---------------------------------------------------------------- gems

        [Fact]
        [Trait("Category", "Economy")]
        public void NoGemSpotShareAndNoGemPickupsAnywhere()
        {
            Assert.Equal(5, SimConfig.DifficultyGemPermille.Length);
            foreach (int share in SimConfig.DifficultyGemPermille) Assert.Equal(0, share);
            Assert.Equal(0, new SimConfig().GemSpotPermille);
            for (int g = 1; g <= Regions; g++) Assert.Equal(0, PlacedGemPickups(g, SimConfig.DifficultyGemPermille[4]));
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void RarityTiersAndValuesAreUnchanged()
        {
            Assert.Equal(new[] { 1, 2, 5, 10 }, new SimConfig().GemValues);
            Assert.Equal(new[] { 70, 22, 7, 1 }, new SimConfig().GemRarityPercent);
            SimConfig cfg = new SimConfig();
            const int N = 100000;
            int[] tier = new int[4];
            for (int i = 0; i < N; i++) tier[LevelBuilder.GemTierAt(cfg, i)]++;
            double[] want = { 70, 22, 7, 1 }, tol = { 0.5, 0.5, 0.3, 0.2 };
            for (int t = 0; t < 4; t++) Assert.InRange(tier[t] * 100.0 / N, want[t] - tol[t], want[t] + tol[t]);
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void MilestoneGemsAreFixedOneOneTwoTwoThreePerRegion()
        {
            Assert.Equal(new[] { 1, 1, 2, 2, 3 }, CoinPayoutRules.MilestoneGemTable);
            for (int g = 1; g <= Regions; g++)
            {
                int[] gems = JsonGemsPerLevel(g);
                int total = 0;
                for (int i = 1; i <= 25; i++)
                {
                    Assert.Equal(CoinPayoutRules.MilestoneGemsOfLevel(i), gems[i]);
                    total += gems[i];
                }
                Assert.Equal(1, gems[5]); Assert.Equal(1, gems[10]); Assert.Equal(2, gems[15]); Assert.Equal(2, gems[20]); Assert.Equal(3, gems[25]);
                Assert.Equal(9, total);
            }
            Assert.Equal(0, CoinPayoutRules.MilestoneGemsOfLevel(8));
            Assert.Equal(0, CoinPayoutRules.MilestoneGemsOfLevel(0));
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void GemsNeverSitOnTopOfAStarOrEachOther()
        {
            for (int region = 1; region <= 5; region++) GemsNeverSitOnTopOfAStarOrEachOtherIn(region);
        }

        private static void GemsNeverSitOnTopOfAStarOrEachOtherIn(int region)
        {
            // Regression for the Q16.16 overflow in the pickup distance test: far pairs (more than 181 u apart) were reported as touching.
            SimConfig cfg = new SimConfig();
            cfg.GemSpotPermille = 32;   // not shipped (0 since 2026-11-13); the separation code must still hold if gems ever come back
            string root = ContentLoader.FindRoot();
            Assert.NotNull(root);
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, region, region))
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
        public void ClearPayoutIs56Plus48PerStarBeforeComboAndFactor()
        {
            Assert.Equal(56, CoinPayoutRules.BaseCoins(0));
            Assert.Equal(104, CoinPayoutRules.BaseCoins(1));
            Assert.Equal(152, CoinPayoutRules.BaseCoins(2));
            Assert.Equal(200, CoinPayoutRules.BaseCoins(3));
            Assert.Equal(4, CoinPayoutRules.ComboStepCoins);
            Assert.Equal(new SimConfig().ComboLandingCoins, CoinPayoutRules.SimComboStepCoins);   // the sim still counts 5 per combo step (Endless pays them unchanged)
            Assert.Equal(8, CoinPayoutRules.ComboCoins(10));
            Assert.Equal(0, CoinPayoutRules.ComboCoins(0));
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
                    int simCoins = comboSteps * CoinPayoutRules.SimComboStepCoins;
                    int combo = comboSteps * CoinPayoutRules.ComboStepCoins;
                    long expected = (long)Math.Round((56 + 48 * stars) * factor, MidpointRounding.AwayFromZero) + (long)Math.Round(combo * factor, MidpointRounding.AwayFromZero);
                    Assert.Equal((int)expected, CoinPayoutRules.Payout(stars, simCoins, factor, false));
                }
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void PayoutMatchesTheOwnerSpotValues()
        {
            Assert.Equal(56, CoinPayoutRules.Payout(0, 0, 1.0, false));
            Assert.Equal(104, CoinPayoutRules.Payout(1, 0, 1.0, false));
            Assert.Equal(152, CoinPayoutRules.Payout(2, 0, 1.0, false));
            Assert.Equal(200, CoinPayoutRules.Payout(3, 0, 1.0, false));
            Assert.Equal(200 + 4 * 12, CoinPayoutRules.Payout(3, 60, 1.0, false));   // 12 combo steps = 60 sim coins = 48 level coins
            Assert.Equal(224, CoinPayoutRules.Payout(3, 0, 1.12, false));
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void ReplayPaysAQuarterOnlyOnceTheLevelHasThreeStars()
        {
            Assert.False(CoinPayoutRules.IsReducedReplay(0));
            Assert.False(CoinPayoutRules.IsReducedReplay(1));
            Assert.False(CoinPayoutRules.IsReducedReplay(2));
            Assert.True(CoinPayoutRules.IsReducedReplay(3));
            Assert.Equal(25, CoinPayoutRules.ReplayPercent);
            for (int stars = 0; stars <= 3; stars++)
            {
                int full = CoinPayoutRules.Payout(stars, 20, 1.0, false), reduced = CoinPayoutRules.Payout(stars, 20, 1.0, true);
                Assert.Equal(full * 25 / 100, reduced);
            }
            Assert.Equal(50, CoinPayoutRules.Payout(3, 0, 1.0, true));
            Assert.Equal(26, CoinPayoutRules.Payout(1, 0, 1.0, true));
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void PerfectPlayerEarnsAbout5000CoinsPerRegion()
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
                Assert.InRange(normalised, 5000 * 0.9, 5000 * 1.1);   // 25 levels x about 200 coins (was 6,250 before the 20 percent cut of 2026-11-13)
            }
        }

        // ---------------------------------------------------------------- hero milestone bonus (owner 2026-11-13)

        [Fact]
        [Trait("Category", "Economy")]
        public void HeroMilestoneBonusTableAndTheSeededExtraGem()
        {
            Assert.Equal(new[] { 50, 100, 200, 300, 500 }, CoinPayoutRules.HeroBonusCoinsByRarity);
            Assert.Equal(new[] { 0, 1, 2, 3, 5 }, CoinPayoutRules.HeroBonusGemsByRarity);
            for (int r = 0; r <= 4; r++) Assert.Equal(CoinPayoutRules.HeroBonusCoinsByRarity[r], CoinPayoutRules.HeroBonusCoins(r));
            // Common never rolls an extra gem: always 0
            for (int seed = 1; seed < 500; seed++) Assert.Equal(0, CoinPayoutRules.HeroBonusGems(0, seed, "momo"));
            // the others pay the sure gems, or one more; the roll is deterministic and hits about half the time
            for (int r = 1; r <= 4; r++)
            {
                int hits = 0;
                const int N = 2000;
                for (int seed = 1; seed <= N; seed++)
                {
                    int g = CoinPayoutRules.HeroBonusGems(r, seed * 7919, "hero_r" + r);
                    Assert.True(g == CoinPayoutRules.HeroBonusGemsByRarity[r] || g == CoinPayoutRules.HeroBonusGemsByRarity[r] + 1, "rarity " + r + " paid " + g);
                    Assert.Equal(g, CoinPayoutRules.HeroBonusGems(r, seed * 7919, "hero_r" + r));
                    if (g > CoinPayoutRules.HeroBonusGemsByRarity[r]) hits++;
                }
                Assert.InRange(hits * 100.0 / N, 45, 55);
            }
            // out of range rarities clamp; the hash is fixed
            Assert.Equal(CoinPayoutRules.HeroBonusCoins(4), CoinPayoutRules.HeroBonusCoins(9));
            Assert.Equal(CoinPayoutRules.HeroBonusCoins(0), CoinPayoutRules.HeroBonusCoins(-3));
            Assert.NotEqual(CoinPayoutRules.StableHash("momo"), CoinPayoutRules.StableHash("monk"));
            Assert.Equal(CoinPayoutRules.StableHash("momo"), CoinPayoutRules.StableHash("momo"));
        }
    }
}
