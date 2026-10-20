using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Xunit;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>
    /// T29 economy check (analysis only, no tuning). Builds every shipped level of regions 1 to 5 with the real LevelBuilder,
    /// sums the gem pickups placed (value by rarity tier), recomputes the 3-star coin payout and the milestone gem rewards with the
    /// same arithmetic as Assets/_Game/Economy/Rewards.cs, and compares with the revive and region-skip gem prices.
    /// Writes Tests/Simulation/ECONOMY_TABLES.md (the hand-written ECONOMY_REPORT.md quotes it).
    /// Category: Economy.
    /// </summary>
    public class EconomyTests
    {
        public const int Regions = 5;
        // Economy v2 constants (Rewards.cs EconomyRules).
        const int ClearCoins = 30, StarCoins = 40, ComboStepCoins = 5;
        // Revive prices (RunSession.ReviveCostFor): 2 then 5, max 2 per run.
        const int Revive1 = 2, Revive2 = 5;
        // Assumption: a human needs this many times the bot's tick count for the same level (bot plays near-optimally).
        const double HumanTimeFactor = 2.0;

        sealed class LevelRow
        {
            public LoadedLevel L;
            public int Rows, StarsPlaced, GemPickups;
            public int[] TierCount = new int[4];
            public int GemsPlaced;          // sum of gem values over all gem pickups
            public double CoinFactor;
            public int Coins3Star, CoinsCombo;
            public int MilestoneFirst;      // deterministic first-clear milestone gems at 3 stars (0 for non milestone levels)
            public int BotTicks, BotGems, BotCombo;
            public bool BotSolved;
        }

        sealed class RegionInfo { public int Index; public int? GemSkip; public double CoinFactor; }

        static readonly object Gate = new object();
        static string cachedTables;

        static JsonElement Rewards(string root, LoadedLevel ll, out bool ok)
        {
            string[] files = Directory.GetFiles(Path.Combine(root, "Content", "levels"), "region_" + ll.Region.ToString("D3") + "_*_levels.json");
            using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(files[0])))
                foreach (JsonElement lv in doc.RootElement.EnumerateArray())
                    if (lv.GetProperty("id").GetString() == ll.Id) { ok = true; return lv.GetProperty("rewards").Clone(); }
            ok = false; return default(JsonElement);
        }

        // First clear (clearsBefore 0) milestone gems, same arithmetic as RewardCalculator.Compute.
        static int Milestone(JsonElement gems, int seed, int stars)
        {
            double chance = gems.GetProperty("chance").GetDouble();
            int min = gems.GetProperty("min").GetInt32(), max = gems.GetProperty("max").GetInt32();
            JsonElement v;
            Rng rng = new Rng(unchecked(seed ^ 13));
            int amount = 0;
            if (rng.Next(100) < (int)Math.Round(chance * 100.0)) amount = rng.Range(min, max);
            if (gems.TryGetProperty("firstClearGuaranteed", out v) && v.GetInt32() > amount) amount = v.GetInt32();
            if (gems.TryGetProperty("starBonusMax", out v) && stars > 1) amount += v.GetInt32() * (stars - 1) / 2;
            return amount;
        }

        static string Build()
        {
            lock (Gate)
            {
                if (cachedTables != null) return cachedTables;
                string root = ContentLoader.FindRoot();
                SimConfig cfg = new SimConfig();
                StringBuilder sb = new StringBuilder();

                RegionInfo[] regions = new RegionInfo[Regions + 2];
                for (int r = 1; r <= Regions + 1; r++)
                {
                    string f = Directory.GetFiles(Path.Combine(root, "Content", "regions"), "region_" + r.ToString("D3") + "_*.json")[0];
                    using (JsonDocument d = JsonDocument.Parse(File.ReadAllText(f)))
                    {
                        RegionInfo ri = new RegionInfo();
                        ri.Index = r;
                        ri.CoinFactor = d.RootElement.GetProperty("coinFactor").GetDouble();
                        JsonElement gs = d.RootElement.GetProperty("unlock").GetProperty("gemSkip");
                        ri.GemSkip = gs.ValueKind == JsonValueKind.Null ? (int?)null : gs.GetInt32();
                        regions[r] = ri;
                    }
                }

                int coinsPerGem, replayPctCfg;
                using (JsonDocument d = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Content", "config", "remote_config.json"))))
                {
                    JsonElement eco = d.RootElement.GetProperty("economy");
                    coinsPerGem = eco.GetProperty("coinsPerGem").GetInt32();
                    replayPctCfg = eco.GetProperty("replayCoinPercent").GetInt32();
                }

                List<LevelRow> rows = new List<LevelRow>();
                foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 1, Regions))
                {
                    LevelRow lr = new LevelRow();
                    lr.L = ll;
                    lr.CoinFactor = regions[ll.Region].CoinFactor;
                    LevelData data = LevelBuilder.Build(cfg, ll.Spec);
                    lr.Rows = data.Rows;
                    for (int i = 0; i < data.Pickups.Length; i++)
                    {
                        Pickup p = data.Pickups[i];
                        if (p.Kind == PickupKind.Star) lr.StarsPlaced++;
                        if (p.Kind == PickupKind.Gem)
                        {
                            int tier = Math.Max(0, Math.Min(cfg.GemValues.Length - 1, p.Tag));
                            lr.GemPickups++;
                            lr.TierCount[tier]++;
                            lr.GemsPlaced += cfg.GemValues[tier];
                        }
                    }
                    bool ok;
                    JsonElement rw = Rewards(root, ll, out ok);
                    JsonElement gems;
                    if (ok && ll.Spec.Class >= 3 && rw.TryGetProperty("gems", out gems))
                        lr.MilestoneFirst = Milestone(gems, ll.Spec.Seed, 3);

                    // Bot: near-optimal line; gives a lower bound on play time and the gems/combo it picks up on the way.
                    Sim sim = new Sim(cfg, data, ll.Spec);
                    int budget = 20000 * Math.Max(1, ll.Spec.RowsFor() / 60);
                    BotResult br = new LevelBot(sim, ll.Spec, data, budget).Solve();
                    lr.BotSolved = br.Solved;
                    lr.BotTicks = br.Solved ? br.Final.Tick : 0;
                    lr.BotGems = br.Gems;
                    lr.BotCombo = br.Coins;

                    long factor = (long)Math.Round(lr.CoinFactor * 10000);
                    lr.Coins3Star = (int)(((long)(ClearCoins + StarCoins * 3) * factor + 5000) / 10000);
                    lr.CoinsCombo = (int)(((long)lr.BotCombo * ComboStepCoins * factor + 5000) / 10000);
                    rows.Add(lr);
                }

                sb.AppendLine("# Economy tables (generated by EconomyTests, do not edit)");
                sb.AppendLine();
                sb.AppendLine("Gem value per tier " + string.Join("/", cfg.GemValues) + ", tier odds % " + string.Join("/", cfg.GemRarityPercent) +
                    ", gem spot chance per row " + cfg.GemInsteadOfCoinPercent + "%, extra edge gem " + cfg.GemChancePercent + "% per level. coinsPerGem " + coinsPerGem +
                    ", remote_config replayCoinPercent " + replayPctCfg + " (code EconomyRules: 50).");
                sb.AppendLine("Expected gem value per pickup = " + ExpectedTier(cfg).ToString("F2") + ".");
                sb.AppendLine();
                sb.AppendLine("## Per level (gems placed = value of every gem pickup; tiers C/R/E/L = common/rare/epic/legendary counts)");
                sb.AppendLine();
                sb.AppendLine("| Level | Class | Rows | Gem pickups | C/R/E/L | Gems placed | Milestone gems (1st clear, 3 stars) | Coins 3-star (no combo) | Bot s | Bot gems on line | Bot combo steps |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
                foreach (LevelRow r in rows)
                    sb.AppendLine("| " + r.L.Id + " | " + r.L.ClassName + " | " + r.Rows + " | " + r.GemPickups + " | " + r.TierCount[0] + "/" + r.TierCount[1] + "/" + r.TierCount[2] + "/" + r.TierCount[3] +
                        " | " + r.GemsPlaced + " | " + r.MilestoneFirst + " | " + r.Coins3Star + " | " + (r.BotSolved ? (r.BotTicks / 60.0).ToString("F0") : "unsolved") + " | " + r.BotGems + " | " + r.BotCombo + " |");

                int[] placed = new int[Regions + 1], mile = new int[Regions + 1], coins = new int[Regions + 1], combo = new int[Regions + 1], pick = new int[Regions + 1], n = new int[Regions + 1], botGems = new int[Regions + 1];
                double[] secs = new double[Regions + 1];
                foreach (LevelRow r in rows)
                {
                    int g = r.L.Region;
                    placed[g] += r.GemsPlaced; mile[g] += r.MilestoneFirst; coins[g] += r.Coins3Star; combo[g] += r.CoinsCombo; pick[g] += r.GemPickups; n[g]++;
                    secs[g] += r.BotTicks / 60.0; botGems[g] += r.BotGems;
                }

                sb.AppendLine();
                sb.AppendLine("## Per region");
                sb.AppendLine();
                sb.AppendLine("| Region | Levels | Gem pickups | Gems placed | Placed per level | Milestone gems (1st clear, 3 stars) | Max gems, first pass | Bot-line gems | Coins 3-star base | Combo coins (bot line) | Bot minutes (1 pass) | Skip price of NEXT region | Skip price / max gems |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
                for (int g = 1; g <= Regions; g++)
                {
                    int? skip = regions[g + 1].GemSkip;
                    int maxPass = placed[g] + mile[g];
                    sb.AppendLine("| " + g + " | " + n[g] + " | " + pick[g] + " | " + placed[g] + " | " + ((double)placed[g] / n[g]).ToString("F1") + " | " + mile[g] + " | " + maxPass + " | " + botGems[g] + " | " + coins[g] + " | " + combo[g] + " | " + (secs[g] / 60.0).ToString("F1") + " | " + skip + " | " + (skip.HasValue ? ((double)skip.Value / maxPass).ToString("F2") : "-") + " |");
                }

                sb.AppendLine();
                sb.AppendLine("## Progression model: playing region k once (first clears, 3 stars)");
                sb.AppendLine();
                sb.AppendLine("c = fraction of placed gem value the player picks up (gems sit on risky spots, so c below 1 is realistic). Milestone gems are fixed rewards. Human time = bot time x " + HumanTimeFactor + " (assumption).");
                sb.AppendLine("Cell pairs: gems earned in the region, minutes of play until the running total reaches the NEXT region's skip price ('never' = one pass of the region is not enough).");
                sb.AppendLine();
                double[] rates = { 1.0, 0.6, 0.3 };
                sb.AppendLine("| Region | Skip price of next | c=100% gems | c=100% min | c=60% gems | c=60% min | c=30% gems | c=30% min | Gems/min at c=60% |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
                for (int g = 1; g <= Regions; g++)
                {
                    int? skip = regions[g + 1].GemSkip;
                    StringBuilder line = new StringBuilder("| " + g + " | " + skip);
                    foreach (double c in rates)
                    {
                        double total = c * placed[g] + mile[g];
                        line.Append(" | " + total.ToString("F0") + " | " + TimeToAfford(rows, g, c, skip ?? 0));
                    }
                    double rate60 = (0.6 * placed[g] + mile[g]) / (secs[g] * HumanTimeFactor / 60.0);
                    line.Append(" | " + rate60.ToString("F1") + " |");
                    sb.AppendLine(line.ToString());
                }

                sb.AppendLine();
                sb.AppendLine("## Cumulative wallet from region 1, first clears only, no revives, no spending (c=60%)");
                sb.AppendLine();
                sb.AppendLine("| Skip to region | Price | Gems earned in regions 1..k-1 | Surplus | Same at c=100% |");
                sb.AppendLine("|---|---|---|---|---|");
                double cum = 0, cum100 = 0;
                for (int g = 1; g <= Regions - 1; g++)
                {
                    cum += 0.6 * placed[g] + mile[g];
                    cum100 += placed[g] + mile[g];
                    int price = regions[g + 1].GemSkip ?? 0;
                    sb.AppendLine("| " + (g + 1) + " | " + price + " | " + cum.ToString("F0") + " | " + (cum - price).ToString("F0") + " | " + cum100.ToString("F0") + " |");
                }

                sb.AppendLine();
                sb.AppendLine("## Revive drain per region (c=60%)");
                sb.AppendLine();
                sb.AppendLine("A run pays " + Revive1 + " then " + Revive2 + " gems (7 for both). 'Sustainable first revives per level' = average gems earned per level / " + Revive1 + ". A replay of a 3-star level pays no gems, so revives there are pure loss.");
                sb.AppendLine();
                sb.AppendLine("| Region | Avg gems per level | Sustainable first revives per level | Levels of income one 2+5 revive pair costs |");
                sb.AppendLine("|---|---|---|---|");
                for (int g = 1; g <= Regions; g++)
                {
                    double per = (0.6 * placed[g] + mile[g]) / n[g];
                    sb.AppendLine("| " + g + " | " + per.ToString("F2") + " | " + (per / Revive1).ToString("F2") + " | " + (7.0 / Math.Max(per, 0.001)).ToString("F1") + " |");
                }

                sb.AppendLine();
                sb.AppendLine("## Coins versus gems");
                sb.AppendLine();
                sb.AppendLine("| Region | Coins for one 3-star pass (no combo) | Gem equivalent at " + coinsPerGem + " coins/gem | Skip price of next region (gems) | Skip price in coins |");
                sb.AppendLine("|---|---|---|---|---|");
                for (int g = 1; g <= Regions; g++)
                {
                    int? skip = regions[g + 1].GemSkip;
                    sb.AppendLine("| " + g + " | " + coins[g] + " | " + ((double)coins[g] / coinsPerGem).ToString("F1") + " | " + skip + " | " + (skip * coinsPerGem) + " |");
                }

                cachedTables = sb.ToString();
                File.WriteAllText(Path.Combine(root, "Tests", "Simulation", "ECONOMY_TABLES.md"), cachedTables);
                return cachedTables;
            }
        }

        static double ExpectedTier(SimConfig cfg)
        {
            double e = 0;
            for (int i = 0; i < cfg.GemValues.Length; i++) e += cfg.GemValues[i] * cfg.GemRarityPercent[i] / 100.0;
            return e;
        }

        // Plays region g once at collect rate c; returns minutes until the running gem total reaches price, else "never".
        static string TimeToAfford(List<LevelRow> rows, int g, double c, int price)
        {
            double gems = 0, secs = 0;
            foreach (LevelRow r in rows)
            {
                if (r.L.Region != g) continue;
                gems += c * r.GemsPlaced + r.MilestoneFirst;
                secs += r.BotTicks / 60.0 * HumanTimeFactor;
                if (gems >= price) return (secs / 60.0).ToString("F1");
            }
            return "never";
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void EconomyTablesAreWritten()
        {
            Assert.Contains("Per region", Build());
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void EveryLevelHasThreeStarPickups()
        {
            string root = ContentLoader.FindRoot();
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 1, Regions))
            {
                LevelData data = LevelBuilder.Build(cfg, ll.Spec);
                int stars = 0;
                for (int i = 0; i < data.Pickups.Length; i++) if (data.Pickups[i].Kind == PickupKind.Star) stars++;
                Assert.True(stars == 3, ll.Id + " has " + stars + " star pickups");
            }
        }
    }
}
