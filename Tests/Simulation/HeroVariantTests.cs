using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FluffyClimbers.Economy;
using Xunit;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>Per-player hero variants (owner 2026-11-12, docs/44 section 11): the pool rules, the pure deterministic pick and the recorded FI gate values of every variant.</summary>
    public class HeroVariantTests
    {
        /// <summary>Smallest number of generated variants a hero may have (the pool also holds the table row); the generator aims for 8 (HERO_VARS) and gives every hero 4 first.</summary>
        const int MinVariants = 4;

        static IEnumerable<string> TierIds()
        {
            foreach (string line in HeroTable.ActiveLines)
                for (int t = 1; t < 5; t++) yield return line + HeroTable.TierSuffix[t];
        }

        [Fact]
        public void OnlyTheTwentyUpperTierHeroesAreRolledCommonStaysFixed()
        {
            foreach (HeroRow r in HeroTable.Rows)
            {
                bool expect = r.Active && r.Tier >= 1;
                Assert.Equal(expect, HeroRolls.IsRollable(r.Id));
            }
            Assert.Equal(20, TierIds().Count(HeroRolls.IsRollable));
            Assert.False(HeroRolls.IsRollable("momo"));
            Assert.Equal(0, HeroRolls.Pick(1, "momo", 0));
            Assert.Equal(190, HeroTable.Find("momo").LaunchTenths);   // Momo Common is still the fixed 19.0 default
        }

        [Fact]
        public void EveryHeroHasAtLeastTheMinimumVariantsAndTheTableRowIsEntryZero()
        {
            foreach (string id in TierIds())
            {
                Assert.True(HeroRolls.PoolSize(id) >= 1 + MinVariants, id + " pool " + HeroRolls.PoolSize(id));
                Assert.Same(HeroTable.Find(id), HeroRolls.Row(id, 0));
            }
        }

        [Fact]
        public void EveryVariantIsInsideItsRarityBandAndTheLegalLimitsWithNoDuplicates()
        {
            foreach (string id in TierIds())
            {
                int tier = HeroTable.Find(id).Tier;
                var seen = new HashSet<string>();
                for (int v = 0; v < HeroRolls.PoolSize(id); v++)
                {
                    HeroRow r = HeroRolls.Row(id, v);
                    Assert.Equal(id, r.Id);
                    Assert.Null(HeroIndex.Validate(tier, new HeroPhysics(r.LaunchTenths, r.Speed, r.Control, r.Wall)));
                    Assert.True(seen.Add(r.LaunchTenths + "/" + r.Speed + "/" + r.Control + "/" + r.Wall), id + " duplicate variant " + v);
                }
            }
        }

        [Fact]
        public void ThePickIsPureAndSpreadsOverThePool()
        {
            foreach (string id in TierIds())
            {
                int size = HeroRolls.PoolSize(id);
                var counts = new int[size];
                for (int seed = 1; seed <= 4000; seed++)
                {
                    int v = HeroRolls.Pick(seed, id, 0);
                    Assert.Equal(v, HeroRolls.Pick(seed, id, 0));
                    Assert.InRange(v, 0, size - 1);
                    counts[v]++;
                }
                double expect = 4000.0 / size;
                foreach (int c in counts) Assert.InRange(c, expect * 0.5, expect * 1.5);
            }
        }

        [Fact]
        public void TheHeroAndTheCounterChangeTheDraw()
        {
            int sameHero = 0, sameCounter = 0;
            for (int seed = 1; seed <= 500; seed++)
            {
                if (HeroRolls.Pick(seed, "momo_u", 0) == HeroRolls.Pick(seed, "forestgirl_u", 0)) sameHero++;
                if (HeroRolls.Pick(seed, "momo_u", 0) == HeroRolls.Pick(seed, "momo_u", 1)) sameCounter++;
            }
            Assert.True(sameHero < 250, "hero id is part of the draw");
            Assert.True(sameCounter < 250, "counter is part of the draw");
        }

        [Fact]
        public void StatsFollowTheVariantAndVariantZeroIsTheSpecItself()
        {
            foreach (string id in TierIds())
            {
                CharacterSpec c = CharacterInventory.Get(id);
                Assert.Same(c.Stats, HeroRolls.StatsFor(c, 0));
                for (int v = 1; v < HeroRolls.PoolSize(id); v++)
                {
                    CharacterStats s = HeroRolls.StatsFor(c, v);
                    HeroRow r = HeroRolls.Row(id, v);
                    Assert.Equal(r.LaunchTenths, s.LaunchTenths);
                    Assert.Equal(r.Speed, s.MaxSpeed);
                    Assert.Equal(r.Control, s.Control);
                    Assert.Equal(r.Wall, s.WallBoostHundredths);
                    Assert.Equal(c.Stats.RunBonusCoins, s.RunBonusCoins);
                    Assert.Null(s.ValidateTier());
                    Assert.Same(s, HeroRolls.StatsFor(c, v));   // cached
                }
                Assert.Same(c.Stats, HeroRolls.StatsFor(c, 100000));   // outside the pool: the table row
            }
        }

        // ---- the recorded gate values (the measured proof is HeroVariantGeneratorTests, HERO_VAR=1; HERO_VAR_VERIFY=1 re-measures every variant) ----

        sealed class Rec { public string Id; public int L, S, C, W, Att; public double Fi15, Mean, Dev; }

        static List<Rec> ReadCsv()
        {
            string path = Path.Combine(ContentLoader.FindRoot(), "Tests", "Simulation", "hero_gen", "variants.csv");
            var list = new List<Rec>();
            foreach (string l in File.ReadAllLines(path).Skip(1))
            {
                if (l.Trim().Length == 0) continue;
                string[] f = l.Split(',');
                Func<string, double> d = x => double.Parse(x, CultureInfo.InvariantCulture);
                list.Add(new Rec { Id = f[0], L = int.Parse(f[2]), S = int.Parse(f[3]), C = int.Parse(f[4]), W = int.Parse(f[5]), Fi15 = d(f[6]), Mean = d(f[7]), Dev = d(f[8]), Att = int.Parse(f[9]) });
            }
            return list;
        }

        [Fact]
        public void TheGeneratedTableEqualsTheVariantsCsv()
        {
            List<Rec> recs = ReadCsv();
            foreach (string id in TierIds())
            {
                var fromCsv = recs.Where(r => r.Id == id).Select(r => r.L + "/" + r.S + "/" + r.C + "/" + r.W).Distinct().ToList();
                var fromCode = Enumerable.Range(1, HeroRolls.PoolSize(id) - 1).Select(v => HeroRolls.Row(id, v)).Select(r => r.LaunchTenths + "/" + r.Speed + "/" + r.Control + "/" + r.Wall).ToList();
                Assert.Equal(fromCsv, fromCode);
            }
        }

        [Fact]
        public void EveryRecordedVariantPassesTheGateAgainstMomosSameTier()
        {
            // Momo's same-tier reference from the committed per-layout reference (hero_gen/FI_REFERENCE_VAR.csv, written by the generator): the same numbers it gates against
            string[] lines = File.ReadAllLines(Path.Combine(ContentLoader.FindRoot(), "Tests", "Simulation", "hero_gen", "FI_REFERENCE_VAR.csv")).Skip(2).Where(l => l.Length > 0).ToArray();
            var levelIds = lines.Select(l => l.Split(',')[0]).Distinct().ToList();
            double[][] ok = new double[5][];
            for (int t = 0; t < 5; t++) ok[t] = new double[levelIds.Count];
            foreach (string l in lines)
            {
                string[] f = l.Split(',');
                ok[int.Parse(f[1])][levelIds.IndexOf(f[0])] = f.Skip(2).Average(x => double.Parse(x, CultureInfo.InvariantCulture));
            }
            int l15 = levelIds.IndexOf("lvl_001_15");
            var target15 = new double[5]; var mean = new double[5];
            for (int t = 0; t < 5; t++)
            {
                target15[t] = ok[t][l15] / ok[0][l15];
                mean[t] = Enumerable.Range(0, levelIds.Count).Average(i => ok[t][i] / ok[0][i]);
            }
            foreach (Rec r in ReadCsv())
            {
                int tier = HeroTable.Find(r.Id).Tier;
                Assert.True(Math.Abs(r.Fi15 - target15[tier]) <= HeroIndex.GateTolerance, r.Id + " fi15 " + r.Fi15 + " vs " + target15[tier]);
                Assert.True(Math.Abs(r.Mean - mean[tier]) <= HeroIndex.GateTolerance, r.Id + " mean " + r.Mean + " vs " + mean[tier]);
                Assert.True(r.Dev <= HeroIndex.PerLevelTolerance, r.Id + " maxdev " + r.Dev);
                Assert.True(r.Fi15 <= HeroIndex.BandMax + 0.005 && r.Mean <= HeroIndex.BandMax + 0.005, r.Id + " ceiling");
            }
        }
    }
}
