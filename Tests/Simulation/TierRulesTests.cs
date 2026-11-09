using System.Collections.Generic;
using System.Linq;
using BoingUpSkyTower.Economy;
using Xunit;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>
    /// Merge tier rules (docs/34 section 2b, docs/44): the rule tables (run bonus, powers), the validators, and the hero table (one absolute physics row per hero,
    /// 25 lines x 5 tiers) held to <see cref="HeroIndex"/>. The old percent chain over the tier below is retired.
    /// </summary>
    public class TierRulesTests
    {
        static readonly CharacterRarity[] Tiers = { CharacterRarity.Uncommon, CharacterRarity.Rare, CharacterRarity.Epic, CharacterRarity.Legendary };

        [Fact]
        public void RuleTablesMatchTheOwnersNumbers()
        {
            // run coins min, max, run gems min, max, powers
            int[][] want =
            {
                new[] { 50, 100, 1, 5, 1 },
                new[] { 200, 500, 5, 10, 2 },
                new[] { 500, 1000, 5, 10, 3 },
                new[] { 1000, 2000, 5, 10, 4 },
            };
            for (int i = 0; i < Tiers.Length; i++)
            {
                TierRule r = TierRules.RuleFor(Tiers[i]);
                Assert.NotNull(r);
                Assert.Equal(want[i], new[] { r.RunCoinsMin, r.RunCoinsMax, r.RunGemsMin, r.RunGemsMax, r.PowerCount });
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
        public void EveryDefinedTierRowObeysItsRunBonusRuleAndHasItsPowers()
        {
            Assert.Equal(5 + 19 * 5, MergeTierDefs.Rows.Length);   // 5 Uncommon heroes + Regions 1 to 5 (Momo/Capi/Otto/Bonbon/Bao Rare/Elite/Legendary, 4 new heroes x 4 tiers each per region)
            foreach (TierRow row in MergeTierDefs.Rows)
            {
                Assert.Null(TierRules.Validate(row));
                CharacterSpec spec = CharacterDefs.GetIncludingInactive(row.Id);
                Assert.NotNull(spec);
                Assert.Null(TierRules.ValidatePowers(row.LineId, row.Rarity, spec.Powers));
                Assert.Equal(CharacterDefs.PowerCount[(int)row.Rarity], spec.Powers.Length);
            }
        }

        [Fact]
        public void TheRunBonusValidatorRejectsRowsOutsideTheRule()
        {
            Assert.Null(TierRules.Validate(new TierRow { Id = "t", Rarity = CharacterRarity.Uncommon, RunCoinsAdd = 50, RunGemsAdd = 1 }));
            Assert.NotNull(TierRules.Validate(new TierRow { Id = "t", Rarity = CharacterRarity.Uncommon, RunCoinsAdd = 55, RunGemsAdd = 1 }));    // not a step of 10
            Assert.NotNull(TierRules.Validate(new TierRow { Id = "t", Rarity = CharacterRarity.Uncommon, RunCoinsAdd = 110, RunGemsAdd = 1 }));
            Assert.NotNull(TierRules.Validate(new TierRow { Id = "t", Rarity = CharacterRarity.Uncommon, RunCoinsAdd = 40, RunGemsAdd = 1 }));
            Assert.NotNull(TierRules.Validate(new TierRow { Id = "t", Rarity = CharacterRarity.Uncommon, RunCoinsAdd = 50, RunGemsAdd = 6 }));
            Assert.NotNull(TierRules.Validate(new TierRow { Id = "t", Rarity = CharacterRarity.Common, RunCoinsAdd = 50, RunGemsAdd = 1 }));
            Assert.NotNull(TierRules.Validate(null));
            Assert.Null(TierRules.Validate(new TierRow { Id = "l", Rarity = CharacterRarity.Legendary, RunCoinsAdd = 2000, RunGemsAdd = 10 }));
            Assert.NotNull(TierRules.Validate(new TierRow { Id = "l", Rarity = CharacterRarity.Legendary, RunCoinsAdd = 2010, RunGemsAdd = 10 }));
        }

        // ---- the hero table ----

        [Fact]
        public void TheTableHasEveryHeroExactlyOnce_25LinesTimesFiveTiers()
        {
            Assert.Equal(25, TierRules.Lines.Length);
            Assert.Equal(25 * 5, HeroTable.Rows.Length);
            Assert.Equal(HeroTable.Rows.Length, HeroTable.Rows.Select(r => r.Id).Distinct().Count());
            foreach (string line in TierRules.Lines)
                for (int t = 0; t < HeroIndex.Tiers; t++)
                {
                    string id = line + HeroTable.TierSuffix[t];
                    Assert.Single(HeroTable.Rows, r => r.Id == id);
                    HeroRow row = HeroTable.Of(line, t);
                    Assert.NotNull(row);
                    Assert.Equal(id, row.Id);
                    Assert.Equal(line, row.Line);
                    Assert.Equal(t, row.Tier);
                }
        }

        [Fact]
        public void EveryCommonAndTierIdExistsExactlyOnceInTheCharacterList()
        {
            // the data accessor still holds all 125 heroes; the runtime list holds the 25 ACTIVE ones only (owner 2026-11-09)
            Assert.Equal(125, CharacterDefs.AllIncludingInactive.Length);
            Assert.Equal(125, CharacterDefs.AllIncludingInactive.Select(c => c.Id).Distinct().Count());
            Assert.Equal(25, CharacterDefs.All.Length);
            Assert.Equal(25, CharacterDefs.All.Select(c => c.Id).Distinct().Count());
            foreach (HeroRow row in HeroTable.Rows)
            {
                CharacterSpec spec = CharacterDefs.GetIncludingInactive(row.Id);
                Assert.NotNull(spec);
                Assert.Equal(row.Tier, (int)spec.Rarity);
                Assert.Equal(row.Line, spec.LineId);
                Assert.Equal(row.Tier > 0, spec.IsMergeTier);
            }
        }

        [Fact]
        public void EveryGeneratedTableRowObeysTheHeroIndexRules()
        {
            // Rows listed in HeroTable.Legacy still carry the old hand-tuned stats until the generator tool replaces them; every other row must pass HeroIndex.Validate.
            foreach (HeroRow row in HeroTable.Rows)
                if (!HeroTable.IsLegacy(row.Id)) Assert.Null(TierRules.ValidatePhysics(row));
            foreach (string id in HeroTable.Legacy)
            {
                Assert.NotNull(HeroTable.Find(id));
                Assert.False(id.StartsWith("momo"), "Momo's five tiers are fixed (HeroIndex.LadderFinal), never legacy");
            }
            Assert.Empty(HeroTable.Legacy);   // every hero has been generated and measured (the whole roster, 25 lines x 5 tiers)
        }

        [Fact]
        public void EveryTableRowStaysInsideTheLegalLimitsEvenWhileItIsLegacy()
        {
            foreach (HeroRow row in HeroTable.Rows)
            {
                Assert.InRange(row.LaunchTenths, CharacterStats.JumpLaunchMinTenths, HeroIndex.LaunchMaxTenths);
                Assert.InRange(row.Speed, HeroIndex.SpeedMin, HeroIndex.SpeedMax);
                Assert.InRange(row.Control, HeroIndex.ControlMin, HeroIndex.ControlMax);
                Assert.InRange(row.Wall, HeroIndex.WallMin, HeroIndex.WallMax);
            }
        }

        [Fact]
        public void TheIndexValidatorRejectsRowsOutsideTheBandsAndLimits()
        {
            Assert.Null(TierRules.ValidatePhysics(new HeroRow("x", "x", 0, 190, 9, 30, 77)));
            Assert.NotNull(TierRules.ValidatePhysics(new HeroRow("x", "x", 0, 200, 9, 30, 77)));     // Common is 0 to 5 percent over 19.0
            Assert.NotNull(TierRules.ValidatePhysics(new HeroRow("x", "x", 1, 190, 9, 30, 77)));     // Uncommon is 6 to 10 percent
            Assert.NotNull(TierRules.ValidatePhysics(new HeroRow("x", "x", 4, 228, 13, 30, 100)));   // speed over the limit
            Assert.NotNull(TierRules.ValidatePhysics(new HeroRow("x", "x", 4, 228, 11, 46, 100)));   // control over the limit
            Assert.NotNull(TierRules.ValidatePhysics(new HeroRow("x", "x", 4, 228, 11, 41, 84)));    // wall below the Legendary range
            Assert.NotNull(TierRules.ValidatePhysics(new HeroRow("x", "x", 5, 228, 11, 41, 100)));   // no such tier
            Assert.NotNull(TierRules.ValidatePhysics(null));
        }

        [Fact]
        public void MomosFiveRowsAreTheLadderFinal()
        {
            Assert.Equal(HeroIndex.Tiers, HeroIndex.LadderFinal.Length);
            for (int t = 0; t < HeroIndex.Tiers; t++)
            {
                HeroRow row = HeroTable.Of("momo", t);
                HeroPhysics want = HeroIndex.LadderFinal[t];
                Assert.Equal(want.LaunchTenths, row.LaunchTenths);
                Assert.Equal(want.Speed, row.Speed);
                Assert.Equal(want.Control, row.Control);
                Assert.Equal(want.Wall, row.Wall);
                Assert.Null(TierRules.ValidatePhysics(row));
                // and the hero the game plays has exactly these numbers
                CharacterStats s = CharacterDefs.GetIncludingInactive(row.Id).Stats;
                Assert.Equal(want.LaunchTenths, s.LaunchTenths);
                Assert.Equal(want.Speed, s.MaxSpeed);
                Assert.Equal(want.Control, s.Control);
                Assert.Equal(want.Wall, s.WallBoostHundredths);
            }
        }

        [Fact]
        public void EveryHeroPlaysWithItsTableRow()
        {
            foreach (HeroRow row in HeroTable.Rows)
            {
                CharacterSpec spec = CharacterDefs.GetIncludingInactive(row.Id);
                CharacterStats s = spec.Stats;
                Assert.Equal(row.LaunchTenths, s.LaunchTenths);
                Assert.Equal(row.Speed, s.MaxSpeed);
                Assert.Equal(row.Control, s.Control);
                Assert.Equal(row.Wall, s.WallBoostHundredths);
                Assert.Null(spec.IsMergeTier ? s.ValidateTier() : s.Validate());
            }
        }

        [Fact]
        public void ATierHeroKeepsItsCommonHeroFindsAndAddsItsRunBonus()
        {
            foreach (TierRow row in MergeTierDefs.Rows)
            {
                CharacterStats e = CharacterDefs.GetIncludingInactive(row.Id).Stats, c = CharacterDefs.GetIncludingInactive(row.LineId).Stats;
                Assert.Equal(c.CoinFind, e.CoinFind);
                Assert.Equal(c.GemFind, e.GemFind);
                Assert.Equal(c.RunBonusCoins + row.RunCoinsAdd, e.RunBonusCoins);
                Assert.Equal(c.RunBonusGems + row.RunGemsAdd, e.RunBonusGems);
                Assert.Null(e.ValidateTier());
            }
        }

        [Fact]
        public void TierHeroesShowTheirAbsoluteStatsWithoutAChain()
        {
            foreach (TierRow row in MergeTierDefs.Rows)
            {
                CharacterSpec spec = CharacterDefs.GetIncludingInactive(row.Id);
                ShownStats want = ShownStats.From(spec.Stats, new SimConfig());
                ShownStats got = spec.ShownValues();
                Assert.Equal(want.Jump, got.Jump);
                Assert.Equal(want.Speed, got.Speed);
                Assert.Equal(want.Control, got.Control);
                Assert.Equal(want.WallBounce, got.WallBounce);
                Assert.Equal(want.RunBonusCoins, got.RunBonusCoins);
                Assert.Equal(want.RunBonusGems, got.RunBonusGems);
            }
        }

        static double[] Vals(ShownStats s) { return new[] { s.Jump, s.Speed, s.Control, s.WallBounce }; }

        [Fact]
        public void GainLinesShowOnlyThePositiveChangesBetweenTwoShownBlocks()
        {
            string[] names = { "Jump", "Speed", "Control", "Wall Bounce" };
            foreach (TierRow row in MergeTierDefs.Rows)
            {
                ShownStats common = CharacterDefs.GetIncludingInactive(row.LineId).ShownValues();
                ShownStats shown = CharacterDefs.GetIncludingInactive(row.Id).ShownValues();
                double[] c = Vals(common), u = Vals(shown);
                var expect = new List<string>();
                for (int s = 0; s < 4; s++)
                {
                    int pct = ShownStats.Percent(c[s], u[s]);
                    if (pct > 0) expect.Add(names[s] + " +" + pct + "%");
                }
                expect.Add("Run coins +" + row.RunCoinsAdd);
                expect.Add("Run gems +" + row.RunGemsAdd);
                Assert.Equal(expect, TierRules.GainLines(common, shown));
                Assert.Equal(expect, MergeRules.ShownGainLines(common, shown));
            }
            // a stat that did not rise (or fell) prints nothing
            var a = new ShownStats { Jump = 5.0, Speed = 9.0, Control = 30.0, WallBounce = 7.0 };
            var b = new ShownStats { Jump = 5.0, Speed = 8.0, Control = 33.0, WallBounce = 7.0 };
            Assert.Equal(new List<string> { "Control +10%" }, TierRules.GainLines(a, b));
            Assert.Empty(TierRules.GainLines(null, b));
        }

        [Fact]
        public void TheJumpBandTextReadsTheIndexBands()
        {
            Assert.Equal("0 to +5% Jump", TierRules.JumpBandText(CharacterRarity.Common));
            Assert.Equal("+6 to +10% Jump", TierRules.JumpBandText(CharacterRarity.Uncommon));
            Assert.Equal("+10 to +15% Jump", TierRules.JumpBandText(CharacterRarity.Rare));
            Assert.Equal("+15 to +20% Jump", TierRules.JumpBandText(CharacterRarity.Epic));
            Assert.Equal("+20 to +25% Jump", TierRules.JumpBandText(CharacterRarity.Legendary));
        }

        [Fact]
        public void EffectiveStatsOfEveryTierAreInsideTheLimitsAndJumpRisesWithRarity()
        {
            foreach (TierRow row in MergeTierDefs.Rows)
            {
                CharacterSpec spec = CharacterDefs.GetIncludingInactive(row.Id);
                CharacterSpec common = CharacterDefs.GetIncludingInactive(row.LineId);
                Assert.Null(spec.Stats.ValidateTier());
                Assert.Null(spec.Stats.ValidatePhysics());
                CharacterStats e = spec.Stats, c = common.Stats;
                // the bible's rule: Jump rises with the rarity (the bands do not overlap upwards); Speed and Control are independent draws (the FI gate balances them), so they may be lower than the Common hero's
                Assert.True(e.LaunchTenths >= c.LaunchTenths, row.Id);
                SimConfig cfg = new SimConfig();
                Assert.True(e.Sheet(cfg).FullSpeedJump > cfg.NormalGapMax.Raw / 65536.0, row.Id);
            }
        }

        [Fact]
        public void TheCapKeepsTenthsAndClampsToTheLimits()
        {
            var raw = new CharacterStats { JumpLaunchTenths = 300, MaxSpeed = 20, Control = 90, WallBoostHundredths = 120, RunBonusCoins = 70, RunBonusGems = 3 };
            CharacterStats eff = MergeTierDefs.Cap(raw);
            Assert.Equal(HeroIndex.LaunchMaxTenths, eff.LaunchTenths);
            Assert.Equal(HeroIndex.SpeedMax, eff.MaxSpeed);
            Assert.Equal(HeroIndex.ControlMax, eff.Control);
            Assert.Equal(HeroIndex.WallMax, eff.WallBoostHundredths);
            Assert.Equal(70, eff.RunBonusCoins);
            Assert.Equal(216, MergeTierDefs.Cap(new CharacterStats { JumpLaunchTenths = 216 }).LaunchTenths);
        }

        [Fact]
        public void ShownBaseOfACommonHeroRoundTripsToItsOwnPhysics()
        {
            foreach (string line in TierRules.Lines)
            {
                CharacterStats s = CharacterDefs.GetIncludingInactive(line).Stats;
                CharacterStats back = ShownStats.From(s, new SimConfig()).ToPhysics(new SimConfig());
                Assert.InRange(back.LaunchTenths, s.LaunchTenths - 2, s.LaunchTenths + 2);   // Jump is shown with one decimal, which is about 1 tenth of launch speed
                Assert.Equal(s.MaxSpeed, back.MaxSpeed);
                Assert.Equal(s.Control, back.Control);
                Assert.InRange(back.WallBoostHundredths, s.WallBoostHundredths - 1, s.WallBoostHundredths + 1);   // 1-decimal Wall Bounce over Speed: Otto 5.8 / 8 = 72.5
            }
        }

        [Fact]
        public void BotProofsAndGoldenHashesStayOnTheBaselineConfig()
        {
            // Levels and bot proofs use new SimConfig(); the default hero (Momo Common, taken from the hero table) reproduces it bit for bit.
            SimConfig baseline = new SimConfig();
            SimConfig viaMomo = baseline.Clone();
            viaMomo.JumpBase = Fx.Zero; viaMomo.MaxSpeed = Fx.Zero; viaMomo.Accel = Fx.Zero; viaMomo.WallBoost = Fx.Zero;
            CharacterDefs.Momo.Stats.ApplyTo(viaMomo);
            Assert.Equal(baseline.JumpBase.Raw, viaMomo.JumpBase.Raw);
            Assert.Equal(baseline.MaxSpeed.Raw, viaMomo.MaxSpeed.Raw);
            Assert.Equal(baseline.Accel.Raw, viaMomo.Accel.Raw);
            Assert.Equal(baseline.WallBoost.Raw, viaMomo.WallBoost.Raw);
            Assert.Equal(190, CharacterStats.Default.LaunchTenths);
            Assert.Equal(190, CharacterDefs.Momo.Stats.LaunchTenths);
            Assert.Equal(9, CharacterStats.Default.MaxSpeed);
            Assert.Equal(30, CharacterStats.Default.Control);
            Assert.Equal(77, CharacterStats.Default.WallBoostHundredths);
            foreach (TierRow row in MergeTierDefs.Rows) Assert.NotSame(CharacterStats.Default, CharacterDefs.GetIncludingInactive(row.Id).Stats);
        }
    }
}
