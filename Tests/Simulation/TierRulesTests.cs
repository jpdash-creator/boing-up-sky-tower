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
                new[] { 3, 10, 25, 200, 500, 5, 10, 2 },
                new[] { 3, 10, 25, 500, 1000, 5, 10, 3 },
                new[] { 4, 10, 25, 1000, 2000, 5, 10, 4 },
            };
            for (int i = 0; i < Tiers.Length; i++)
            {
                TierRule r = TierRules.RuleFor(Tiers[i]);
                Assert.NotNull(r);
                Assert.Equal(want[i], new[] { r.BoostedStats, r.PctMin, r.PctMax, r.RunCoinsMin, r.RunCoinsMax, r.RunGemsMin, r.RunGemsMax, r.PowerCount });
                Assert.Equal(CharacterDefs.PowerCount[(int)Tiers[i]], r.PowerCount);
                Assert.Equal(CharacterDefs.CooldownSeconds[(int)Tiers[i]], r.CooldownSeconds);
            }
            foreach (CharacterRarity t in Tiers) Assert.True(TierRules.RuleFor(t).OverPreviousTier, "chain over the previous tier");
            Assert.Equal(new[] { 2, 3, 3, 4 }, new[] { TierRules.RuleFor(CharacterRarity.Uncommon).BoostedStats, TierRules.RuleFor(CharacterRarity.Rare).BoostedStats, TierRules.RuleFor(CharacterRarity.Epic).BoostedStats, TierRules.RuleFor(CharacterRarity.Legendary).BoostedStats });
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
            Assert.Equal(5 + 19 * 5, MergeTierDefs.Rows.Length);   // 5 Uncommon heroes + Regions 1 to 5 (Momo/Capi/Otto/Bonbon/Bao Rare/Elite/Legendary, 4 new heroes x 4 tiers each per region)
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
            Assert.Null(TierRules.Validate(new TierRow { Id = "l", Rarity = CharacterRarity.Legendary, JumpPct = 10, SpeedPct = 15, ControlPct = 20, WallPct = 25, RunCoinsAdd = 2000, RunGemsAdd = 10 }));
            Assert.NotNull(TierRules.Validate(new TierRow { Id = "l", Rarity = CharacterRarity.Legendary, JumpPct = 10, SpeedPct = 15, ControlPct = 20, RunCoinsAdd = 2000, RunGemsAdd = 10 }));   // Legendary needs all 4
        }

        static ShownStats CommonShown(string line) { return ShownStats.From(CharacterDefs.Get(line).Stats, new SimConfig()); }

        static int[] Pcts(TierRow r) { return new[] { r.JumpPct, r.SpeedPct, r.ControlPct, r.WallPct }; }
        static double[] Vals(ShownStats s) { return new[] { s.Jump, s.Speed, s.Control, s.WallBounce }; }

        /// <summary>Generates the whole chain Uncommon, Rare, Elite, Legendary for one line from one base seed.</summary>
        static TierRow[] Chain(string line, int seed, out ShownStats[] shown)
        {
            ShownStats common = CommonShown(line);
            var rows = new TierRow[4];
            shown = new ShownStats[4];
            ShownStats prev = common;
            for (int i = 0; i < 4; i++)
            {
                rows[i] = TierRules.Generate(line, line + "_" + i, "T" + i, line + "_" + i, Tiers[i], seed + i * 101, prev);
                shown[i] = TierRules.Apply(prev, common, rows[i]);
                prev = shown[i];
            }
            return rows;
        }

        [Fact]
        public void TheGeneratorIsDeterministicAndEveryTierRowObeysCountBandAndChainExactness()
        {
            int[] counts = { 2, 3, 3, 4 };
            foreach (string line in TierRules.Lines)
                for (int seed = 0; seed < 150; seed++)
                {
                    ShownStats[] shown, shown2;
                    TierRow[] a = Chain(line, seed, out shown);
                    TierRow[] b = Chain(line, seed, out shown2);
                    ShownStats prev = CommonShown(line);
                    for (int t = 0; t < 4; t++)
                    {
                        Assert.Null(TierRules.Validate(a[t]));
                        Assert.Null(TierRules.ValidateChain(prev, a[t]));
                        Assert.Equal(Nums(a[t]), Nums(b[t]));
                        Assert.Equal(counts[t], TierRules.BoostedCount(a[t]));
                        // the printed numbers: exactly the stored percent on the boosted stats, exactly 0 on the others
                        double[] p = Vals(prev), n = Vals(shown[t]);
                        int[] pc = Pcts(a[t]);
                        for (int s = 0; s < 4; s++) Assert.Equal(pc[s], ShownStats.Percent(p[s], n[s]));
                        prev = shown[t];
                    }
                }
            Assert.Null(TierRules.Generate("momo", "x", "X", "x", CharacterRarity.Common, 1, CommonShown("momo")));
        }

        [Fact]
        public void TheGeneratorCoversEveryStatChoice()
        {
            var seen = new HashSet<string>();
            for (int seed = 0; seed < 300; seed++)
            {
                TierRow r = TierRules.Generate("momo", "x", "X", "x", CharacterRarity.Rare, seed, CommonShown("momo"));
                seen.Add((r.JumpPct > 0 ? "J" : "") + (r.SpeedPct > 0 ? "S" : "") + (r.ControlPct > 0 ? "C" : "") + (r.WallPct > 0 ? "W" : ""));
            }
            Assert.Equal(4, seen.Count);   // any 3 of 4 stats can come up
        }

        [Fact]
        public void TheFiveUncommonRowsAreTheFixedSeedDrawAndNeverChange()
        {
            // The constants in MergeTierDefs.Rows are what Generate gave for BaseSeed (20261104) + line index over the Common hero. Changing the generator or the rule changes this test on purpose.
            for (int i = 0; i < 5; i++)   // the first five rows are the original Uncommon heroes; the Region 1 rows use their own seeds (Tools/tier_rows.py)
            {
                TierRow row = MergeTierDefs.Rows[i];
                Assert.Equal(TierRules.Lines[i], row.LineId);
                TierRow g = TierRules.Generate(row.LineId, row.Id, row.Name, row.ArtId, row.Rarity, TierRules.BaseSeed + i, CommonShown(row.LineId));
                Assert.Equal(Nums(g), Nums(row));
            }
        }

        [Fact]
        public void UncommonDisplayedPercentOverCommonIsInside10To25ForExactlyTwoStatsAndZeroForTheOthers()
        {
            foreach (TierRow row in MergeTierDefs.Rows)
            {
                if (row.Rarity != CharacterRarity.Uncommon) continue;
                CharacterSpec spec = CharacterDefs.Get(row.Id);
                ShownStats common = CharacterDefs.Get(row.LineId).ShownValues();
                ShownStats shown = spec.ShownValues();
                double[] c = Vals(common), u = Vals(shown);
                int boosted = 0;
                for (int s = 0; s < 4; s++)
                {
                    int pct = ShownStats.Percent(c[s], u[s]);
                    if (pct == 0) { Assert.Equal(c[s], u[s]); continue; }
                    boosted++;
                    Assert.InRange(pct, 10, 25);
                    Assert.Equal(Pcts(row)[s], pct);
                }
                Assert.Equal(2, boosted);
                // the merge text prints those exact numbers
                List<string> lines = TierRules.GainLines(common, shown);
                var expect = new List<string>();
                string[] names = { "Jump", "Speed", "Control", "Wall Bounce" };
                for (int s = 0; s < 4; s++) if (Pcts(row)[s] > 0) expect.Add(names[s] + " +" + Pcts(row)[s] + "%");
                expect.Add("Run coins +" + row.RunCoinsAdd);
                expect.Add("Run gems +" + row.RunGemsAdd);
                Assert.Equal(expect, lines);
            }
        }

        [Fact]
        public void EveryRegion1RowIsExactOverThePreviousTierAndCarriesTheHeroIdentity()
        {
            // identity stat of the four new heroes: boosted at EVERY tier (Jump, Speed, Control, Wall Bounce)
            var identity = new Dictionary<string, int> { { "ribbi", 0 }, { "bumble", 1 }, { "dandy", 2 }, { "pip", 3 }, { "moss", 3 } };
            foreach (TierRow row in MergeTierDefs.Rows)
            {
                CharacterSpec common = CharacterDefs.Get(row.LineId);
                ShownStats prev = common.ShownValues();
                for (int r = 1; r < (int)row.Rarity; r++)
                {
                    TierRow t = null;
                    foreach (TierRow x in MergeTierDefs.Rows) if (x.LineId == row.LineId && (int)x.Rarity == r) t = x;
                    Assert.NotNull(t);
                    prev = TierRules.Apply(prev, common.ShownValues(), t);
                }
                Assert.Null(TierRules.ValidateChain(prev, row));
                int id;
                if (identity.TryGetValue(row.LineId, out id)) Assert.True(Pcts(row)[id] > 0, row.Id + " boosts its identity stat");
            }
        }

        [Fact]
        public void TheChainStacksOnThePreviousTierNotOnCommonAndRunBonusIsNotCumulative()
        {
            ShownStats common = CommonShown("momo");
            var unc = new TierRow { Id = "u", Rarity = CharacterRarity.Uncommon, JumpPct = 20, SpeedPct = 10, RunCoinsAdd = 100, RunGemsAdd = 5 };
            ShownStats u = TierRules.Apply(common, common, unc);
            var rare = new TierRow { Id = "r", Rarity = CharacterRarity.Rare, JumpPct = 20, SpeedPct = 10, ControlPct = 10, RunCoinsAdd = 200, RunGemsAdd = 5 };
            ShownStats r = TierRules.Apply(u, common, rare);
            Assert.Equal(ShownStats.Boosted(u.Jump, 20), r.Jump);
            Assert.True(r.Jump > ShownStats.Boosted(common.Jump, 20), "compounds over the Uncommon value");
            Assert.Equal(common.RunBonusCoins + 200, r.RunBonusCoins);   // its OWN total on top of the Common bonus, not on top of Uncommon's
            Assert.Equal(common.RunBonusGems + 5, r.RunBonusGems);
            Assert.Equal(u.WallBounce, r.WallBounce);
        }

        [Fact]
        public void ChainedShownStatsMayPassTheLimitsButEffectiveStatsNeverDo()
        {
            // worst case: +25 percent on every stat at every tier (1.25^4 = 2.4x)
            foreach (string line in TierRules.Lines)
            {
                ShownStats common = CommonShown(line), prev = common;
                bool passedLimit = false;
                foreach (CharacterRarity r in Tiers)
                {
                    TierRule rule = TierRules.RuleFor(r);
                    var row = new TierRow { Id = "w", Rarity = r, JumpPct = 25, SpeedPct = 25, ControlPct = 25, WallPct = 25, RunCoinsAdd = rule.RunCoinsMax, RunGemsAdd = rule.RunGemsMax };
                    prev = TierRules.Apply(prev, common, row);
                    CharacterStats raw = prev.ToPhysics(new SimConfig());
                    if (raw.ValidatePhysics() != null) passedLimit = true;
                    CharacterStats eff = MergeTierDefs.Cap(raw);
                    Assert.Null(eff.ValidateTier());
                    Assert.True(eff.RunBonusCoins <= CharacterStats.TierRunBonusCoinsMax && eff.RunBonusGems <= CharacterStats.TierRunBonusGemsMax, line + " " + r);
                }
                Assert.True(passedLimit, line + ": the chain is expected to hit the caps by Legendary");
            }
        }

        [Fact]
        public void EffectiveStatsOfEveryUncommonRowAreInsideTheLimitsAndNotWorseThanCommon()
        {
            foreach (TierRow row in MergeTierDefs.Rows)
            {
                CharacterSpec spec = CharacterDefs.Get(row.Id);
                CharacterSpec common = CharacterDefs.Get(row.LineId);
                Assert.Null(spec.Stats.ValidateTier());
                Assert.Null(spec.Stats.ValidatePhysics());
                CharacterStats e = spec.Stats, c = common.Stats;
                Assert.True(e.JumpLaunch >= c.JumpLaunch && e.MaxSpeed >= c.MaxSpeed && e.Control >= c.Control, row.Id);
                Assert.Equal(common.ShownValues().RunBonusCoins + row.RunCoinsAdd, e.RunBonusCoins);
                Assert.Equal(common.ShownValues().RunBonusGems + row.RunGemsAdd, e.RunBonusGems);
                SimConfig cfg = new SimConfig();
                Assert.True(e.Sheet(cfg).FullSpeedJump > cfg.NormalGapMax.Raw / 65536.0, row.Id);
            }
        }

        [Fact]
        public void ShownBaseOfACommonHeroRoundTripsToItsOwnPhysics()
        {
            foreach (string line in TierRules.Lines)
            {
                CharacterStats s = CharacterDefs.Get(line).Stats;
                CharacterStats back = CommonShown(line).ToPhysics(new SimConfig());
                Assert.Equal(s.JumpLaunch, back.JumpLaunch);
                Assert.Equal(s.MaxSpeed, back.MaxSpeed);
                Assert.Equal(s.Control, back.Control);
                Assert.InRange(back.WallBoostHundredths, s.WallBoostHundredths - 1, s.WallBoostHundredths + 1);   // 1-decimal Wall Bounce over Speed: Otto 5.8 / 8 = 72.5
            }
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
