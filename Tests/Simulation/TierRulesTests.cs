using System.Collections.Generic;
using BoingUpSkyTower.Economy;
using Xunit;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>Merge tier rules (docs/34 section 2b, docs/29 section 6b): rule tables, the validator, the seeded generator, the five Uncommon rows and the safety caps.</summary>
    public class TierRulesTests
    {
        static readonly CharacterRarity[] Tiers = { CharacterRarity.Uncommon, CharacterRarity.Rare, CharacterRarity.Epic, CharacterRarity.Legendary };

        static int[] Nums(TierRow r) { return new[] { r.JumpPct, r.SpeedPct, r.ControlPct, r.WallPct, r.RunCoinsAdd, r.RunGemsAdd }; }

        [Fact]
        public void RuleTablesMatchTheOwnersNumbers()
        {
            // stats boosted, percent min, max, run coins min, max, run gems min, max, powers
            int[][] want =
            {
                new[] { 2, 10, 25, 50, 100, 1, 5, 1 },
                new[] { 3, 25, 50, 200, 500, 5, 10, 2 },
                new[] { 3, 50, 75, 500, 1000, 5, 10, 3 },
                new[] { 4, 75, 100, 1000, 2000, 5, 10, 4 },
            };
            for (int i = 0; i < Tiers.Length; i++)
            {
                TierRule r = TierRules.RuleFor(Tiers[i]);
                Assert.NotNull(r);
                Assert.Equal(want[i], new[] { r.BoostedStats, r.PctMin, r.PctMax, r.RunCoinsMin, r.RunCoinsMax, r.RunGemsMin, r.RunGemsMax, r.PowerCount });
                Assert.Equal(CharacterDefs.PowerCount[(int)Tiers[i]], r.PowerCount);
                Assert.Equal(CharacterDefs.CooldownSeconds[(int)Tiers[i]], r.CooldownSeconds);
            }
            Assert.Null(TierRules.RuleFor(CharacterRarity.Common));
            Assert.Equal(new[] { 0, 1, 2, 3, 4 }, CharacterDefs.PowerCount);
            Assert.Equal("Elite", CharacterDefs.RarityNames[(int)CharacterRarity.Epic]);
            for (int i = 1; i < 4; i++) Assert.True(CharacterDefs.CooldownSeconds[i + 1] < CharacterDefs.CooldownSeconds[i], "cooldown shortens with rarity");
        }

        [Fact]
        public void PowerPoolsUseOnlyImplementedPowersAndAreOrderedPrefixes()
        {
            var implemented = new HashSet<PowerUp> { PowerUp.Flight, PowerUp.Rocket, PowerUp.DoubleJump, PowerUp.WallStick, PowerUp.Shield, PowerUp.Magnet, PowerUp.SlowTime };
            Assert.Equal(TierRules.Lines.Length, TierRules.Pools.Count);
            foreach (string line in TierRules.Lines)
            {
                PowerUp[] pool = TierRules.Pools[line];
                Assert.Equal(4, pool.Length);
                Assert.Equal(4, new HashSet<PowerUp>(pool).Count);
                foreach (PowerUp p in pool) Assert.Contains(p, implemented);
                PowerUp[] prev = new PowerUp[0];
                foreach (CharacterRarity r in Tiers)
                {
                    PowerUp[] now = TierRules.PowersFor(line, r);
                    Assert.Equal(CharacterDefs.PowerCount[(int)r], now.Length);
                    Assert.Null(TierRules.ValidatePowers(line, r, now));
                    for (int i = 0; i < prev.Length; i++) Assert.Equal(prev[i], now[i]);   // a higher tier keeps the earlier powers
                    prev = now;
                }
            }
            Assert.Empty(TierRules.PowersFor("momo", CharacterRarity.Common));
            Assert.NotNull(TierRules.ValidatePowers("momo", CharacterRarity.Rare, new[] { PowerUp.DoubleJump }));
            Assert.NotNull(TierRules.ValidatePowers("momo", CharacterRarity.Uncommon, new[] { PowerUp.Magnet }));
        }

        [Fact]
        public void EveryDefinedTierRowObeysItsRule()
        {
            Assert.Equal(5, MergeTierDefs.Rows.Length);
            foreach (TierRow row in MergeTierDefs.Rows)
            {
                Assert.Null(TierRules.Validate(row));
                CharacterSpec spec = CharacterDefs.Get(row.Id);
                Assert.NotNull(spec);
                Assert.Null(TierRules.ValidatePowers(row.LineId, row.Rarity, spec.Powers));
                Assert.Equal(CharacterDefs.PowerCount[(int)row.Rarity], spec.Powers.Length);
            }
        }

        [Fact]
        public void TheValidatorRejectsRowsOutsideTheRule()
        {
            var ok = new TierRow { Id = "t", Rarity = CharacterRarity.Uncommon, JumpPct = 10, WallPct = 25, RunCoinsAdd = 50, RunGemsAdd = 1 };
            Assert.Null(TierRules.Validate(ok));
            Assert.NotNull(TierRules.Validate(new TierRow { Id = "t", Rarity = CharacterRarity.Uncommon, JumpPct = 9, WallPct = 25, RunCoinsAdd = 50, RunGemsAdd = 1 }));
            Assert.NotNull(TierRules.Validate(new TierRow { Id = "t", Rarity = CharacterRarity.Uncommon, JumpPct = 10, WallPct = 26, RunCoinsAdd = 50, RunGemsAdd = 1 }));
            Assert.NotNull(TierRules.Validate(new TierRow { Id = "t", Rarity = CharacterRarity.Uncommon, JumpPct = 10, RunCoinsAdd = 50, RunGemsAdd = 1 }));                         // only 1 stat
            Assert.NotNull(TierRules.Validate(new TierRow { Id = "t", Rarity = CharacterRarity.Uncommon, JumpPct = 10, WallPct = 20, ControlPct = 20, RunCoinsAdd = 50, RunGemsAdd = 1 })); // 3 stats
            Assert.NotNull(TierRules.Validate(new TierRow { Id = "t", Rarity = CharacterRarity.Uncommon, JumpPct = 10, WallPct = 25, RunCoinsAdd = 55, RunGemsAdd = 1 }));              // not a step of 10
            Assert.NotNull(TierRules.Validate(new TierRow { Id = "t", Rarity = CharacterRarity.Uncommon, JumpPct = 10, WallPct = 25, RunCoinsAdd = 110, RunGemsAdd = 1 }));
            Assert.NotNull(TierRules.Validate(new TierRow { Id = "t", Rarity = CharacterRarity.Uncommon, JumpPct = 10, WallPct = 25, RunCoinsAdd = 50, RunGemsAdd = 6 }));
            Assert.NotNull(TierRules.Validate(new TierRow { Id = "t", Rarity = CharacterRarity.Common, JumpPct = 10, WallPct = 25, RunCoinsAdd = 50, RunGemsAdd = 1 }));
            Assert.NotNull(TierRules.Validate(null));
            Assert.Null(TierRules.Validate(new TierRow { Id = "l", Rarity = CharacterRarity.Legendary, JumpPct = 75, SpeedPct = 80, ControlPct = 90, WallPct = 100, RunCoinsAdd = 2000, RunGemsAdd = 10 }));
            Assert.NotNull(TierRules.Validate(new TierRow { Id = "l", Rarity = CharacterRarity.Legendary, JumpPct = 75, SpeedPct = 80, ControlPct = 90, RunCoinsAdd = 2000, RunGemsAdd = 10 }));   // Legendary needs all 4
        }

        [Fact]
        public void TheGeneratorIsDeterministicAndAlwaysProducesValidRows()
        {
            foreach (CharacterRarity r in Tiers)
                for (int seed = 0; seed < 500; seed++)
                {
                    TierRow a = TierRules.Generate("momo", "x", "X", "x", r, seed);
                    TierRow b = TierRules.Generate("momo", "x", "X", "x", r, seed);
                    Assert.Null(TierRules.Validate(a));
                    Assert.Equal(Nums(a), Nums(b));
                }
            Assert.Null(TierRules.Generate("momo", "x", "X", "x", CharacterRarity.Common, 1));
        }

        [Fact]
        public void TheGeneratorCoversEveryStatChoice()
        {
            var seen = new HashSet<string>();
            for (int seed = 0; seed < 300; seed++)
            {
                TierRow r = TierRules.Generate("momo", "x", "X", "x", CharacterRarity.Rare, seed);
                seen.Add((r.JumpPct > 0 ? "J" : "") + (r.SpeedPct > 0 ? "S" : "") + (r.ControlPct > 0 ? "C" : "") + (r.WallPct > 0 ? "W" : ""));
            }
            Assert.Equal(4, seen.Count);   // any 3 of 4 stats can come up
        }

        [Fact]
        public void TheFiveUncommonRowsAreTheFixedSeedDrawAndNeverChange()
        {
            // The constants in MergeTierDefs.Rows are what Generate gave for BaseSeed + line index. Changing the generator or the rule changes this test on purpose.
            for (int i = 0; i < MergeTierDefs.Rows.Length; i++)
            {
                TierRow row = MergeTierDefs.Rows[i];
                Assert.Equal(TierRules.Lines[i], row.LineId);
                TierRow g = TierRules.Generate(row.LineId, row.Id, row.Name, row.ArtId, row.Rarity, TierRules.BaseSeed + i);
                Assert.Equal(Nums(g), Nums(row));
            }
        }

        [Fact]
        public void EffectiveStatsOfEveryTierRowAreInsideTheLimitsAndNeverWorseThanTheCommonHero()
        {
            foreach (TierRow row in MergeTierDefs.Rows)
            {
                CharacterSpec spec = CharacterDefs.Get(row.Id);
                CharacterSpec common = CharacterDefs.Get(row.LineId);
                Assert.Null(spec.Stats.ValidateTier());            // physics inside the limits, run bonus inside the tier ceilings
                Assert.Null(spec.Stats.ValidatePhysics());
                CharacterStats e = spec.Stats, d = spec.SheetStats, c = common.Stats;
                Assert.True(e.JumpLaunch >= c.JumpLaunch && e.MaxSpeed >= c.MaxSpeed && e.Control >= c.Control && e.WallBoostHundredths >= c.WallBoostHundredths, row.Id);
                Assert.True(d.JumpLaunch >= e.JumpLaunch && d.MaxSpeed >= e.MaxSpeed && d.Control >= e.Control && d.WallBoostHundredths >= e.WallBoostHundredths, "the sheet shows at least what plays: " + row.Id);
                Assert.Equal(c.RunBonusCoins + row.RunCoinsAdd, e.RunBonusCoins);
                Assert.Equal(c.RunBonusGems + row.RunGemsAdd, e.RunBonusGems);
                Assert.Equal(d.RunBonusCoins, e.RunBonusCoins);
                // a moving jump still clears the largest normal gap
                SimConfig cfg = new SimConfig();
                Assert.True(e.Sheet(cfg).FullSpeedJump > cfg.NormalGapMax.Raw / 65536.0, row.Id);
            }
        }

        [Fact]
        public void EveryRuleAtItsMaximumStillPlaysInsideTheLimits()
        {
            // worst case for every line and tier: the top of the percent band on all four stats; the effective (capped) stats must still validate
            foreach (CharacterSpec common in new[] { CharacterDefs.Momo, CharacterDefs.ForestGirl, CharacterDefs.BeachOtter, CharacterDefs.CandyGirl, CharacterDefs.Monk })
                foreach (CharacterRarity r in Tiers)
                {
                    TierRule rule = TierRules.RuleFor(r);
                    var row = new TierRow { Id = "w", Rarity = r, JumpPct = rule.PctMax, SpeedPct = rule.PctMax, ControlPct = rule.PctMax, WallPct = rule.PctMax, RunCoinsAdd = rule.RunCoinsMax, RunGemsAdd = rule.RunGemsMax };
                    CharacterStats eff = MergeTierDefs.Cap(MergeTierDefs.Boost(common.Stats, row));
                    Assert.Null(eff.ValidateTier());
                    Assert.True(eff.RunBonusCoins <= CharacterStats.TierRunBonusCoinsMax && eff.RunBonusGems <= CharacterStats.TierRunBonusGemsMax, common.Id + " " + r);
                }
        }

        [Fact]
        public void CapKeepsTheShownValueAboveTheLimitButPlaysAtTheLimit()
        {
            // Momo 19 + 25 percent = 24 shown, plays at 23 (the maximum)
            var row = new TierRow { Id = "m", Rarity = CharacterRarity.Uncommon, JumpPct = 25, SpeedPct = 25, RunCoinsAdd = 50, RunGemsAdd = 1 };
            CharacterStats shown = MergeTierDefs.Boost(CharacterDefs.Momo.Stats, row);
            CharacterStats plays = MergeTierDefs.Cap(shown);
            Assert.Equal(24, shown.JumpLaunch);
            Assert.Equal(CharacterStats.JumpLaunchMax, plays.JumpLaunch);
            Assert.Equal(11, shown.MaxSpeed);
            Assert.Equal(11, plays.MaxSpeed);
            Assert.Equal(130, plays.RunBonusCoins);   // 80 + 50, meta only: never capped by the physics limits
        }

        [Fact]
        public void BotProofsAndGoldenHashesStayOnTheBaselineConfig()
        {
            // Levels and bot proofs use new SimConfig(); the baseline hero stats reproduce it bit for bit, and no tier changes the baseline.
            SimConfig baseline = new SimConfig();
            SimConfig viaMomo = baseline.Clone();
            CharacterDefs.Momo.Stats.ApplyTo(viaMomo);
            Assert.Equal(baseline.JumpBase.Raw, viaMomo.JumpBase.Raw);
            Assert.Equal(baseline.MaxSpeed.Raw, viaMomo.MaxSpeed.Raw);
            Assert.Equal(baseline.Accel.Raw, viaMomo.Accel.Raw);
            Assert.Equal(baseline.WallBoost.Raw, viaMomo.WallBoost.Raw);
            Assert.Equal(19, CharacterStats.Default.JumpLaunch);
            Assert.Equal(9, CharacterStats.Default.MaxSpeed);
            Assert.Equal(30, CharacterStats.Default.Control);
            Assert.Equal(77, CharacterStats.Default.WallBoostHundredths);
            foreach (TierRow row in MergeTierDefs.Rows) Assert.NotSame(CharacterStats.Default, CharacterDefs.Get(row.Id).Stats);
        }
    }
}
