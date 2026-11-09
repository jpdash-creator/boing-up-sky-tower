using System.Linq;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// Hero powers in a run (owner 2026-11-02, docs/34): the cooldown clock (HeroPowerCooldown), one button per power by tier, the duration factor,
    /// the free use (no inventory unit, full bought duration since Bug List 6), the merged button rule (free first, then bought), the layout numbers of the two groups. Category HeroPowers.
    /// </summary>
    [Category("HeroPowers")]
    public class HeroPowerEditTests
    {
        ContentRepository repo;

        [SetUp]
        public void SetUp() { repo = ContentRepository.LoadFromResources(); }

        /// <summary>A hero of the rarity with the powers the tier rule gives it (only Uncommon tiers exist as rows today; the others are built from the rule, as the hero sheet tests do).</summary>
        static CharacterSpec HeroOf(CharacterRarity r)
        {
            return new CharacterSpec { Id = "test_" + r, Name = "Test " + r, Rarity = r, Powers = TierRules.PowersFor("momo", r) };
        }

        // ---- cooldown logic (pure class) ----

        [Test]
        public void CooldownIsReadyAtStartThenCountsDownInTicksPerPower()
        {
            var cd = new HeroPowerCooldown(new[] { PowerUp.Shield, PowerUp.Magnet }, 15);
            Assert.IsTrue(cd.IsReady(0));
            Assert.IsTrue(cd.IsReady(1));
            Assert.IsTrue(cd.TryUse(0));
            Assert.IsFalse(cd.TryUse(0));
            Assert.IsTrue(cd.IsReady(1), "each button has its own cooldown");
            for (int i = 0; i < 15 * SimConfig.TicksPerSecond; i++) cd.Tick();
            Assert.IsTrue(cd.IsReady(0));
            Assert.AreEqual(0f, cd.Fraction(0));
        }

        [Test]
        public void CooldownSecondsFollowTheRarityConstants()
        {
            Assert.AreEqual(new[] { 0, 30, 22, 15, 10 }, CharacterDefs.CooldownSeconds);
            foreach (CharacterRarity r in new[] { CharacterRarity.Uncommon, CharacterRarity.Rare, CharacterRarity.Epic, CharacterRarity.Legendary })
            {
                var run = new RunSession(repo, new SimConfig(), "lvl_001_01");
                run.ApplyHeroPowers(HeroOf(r));
                Assert.AreEqual(CharacterDefs.CooldownSeconds[(int)r] * SimConfig.TicksPerSecond, run.Hero.CooldownTicks, r.ToString());
            }
        }

        // ---- button count per tier ----

        [Test]
        public void ButtonCountPerTier_CommonNone_UncommonOne_RareTwo_EliteThree_LegendaryFour()
        {
            int[] want = { 0, 1, 2, 3, 4 };
            for (int r = 0; r <= 4; r++)
            {
                var run = new RunSession(repo, new SimConfig(), "lvl_001_01");
                CharacterSpec hero = r == 0 ? CharacterDefs.Momo : HeroOf((CharacterRarity)r);
                run.ApplyHeroPowers(hero);
                int count = run.Hero == null ? 0 : run.Hero.Count;
                Assert.AreEqual(want[r], count, ((CharacterRarity)r).ToString());
                Assert.AreEqual(CharacterDefs.PowerCount[r], count);
            }
            var none = new RunSession(repo, new SimConfig(), "lvl_001_01");
            none.ApplyHeroPowers(null);
            Assert.IsNull(none.Hero);
        }

        // ---- duration factor ----

        [Test]
        public void DurationFactorIsSixtyPercentAndDocumented()
        {
            var cfg = new SimConfig();
            Assert.AreEqual(0.6f, SimConfig.HeroPowerDurationFactor);
            Assert.AreEqual(60, cfg.HeroPowerDurationPercent);
            Assert.AreEqual(Mathf.RoundToInt(cfg.ShieldTicks * 0.6f), cfg.HeroTicks(cfg.ShieldTicks));
        }

        // ---- free use through the run session ----

        [Test]
        public void AHeroPowerIsFree_ItTakesNoInventoryUnitAndLastsTheFullBoughtDuration()
        {
            var cfg = new SimConfig();
            var run = new RunSession(repo, cfg, "lvl_001_01");
            var stock = new int[PowerUps.MaxType + 1];
            stock[(int)PowerUp.Magnet] = 2;
            run.ApplyStock(stock);
            CharacterSpec hero = HeroOf(CharacterRarity.Legendary);
            run.ApplyHeroPowers(hero);
            PowerUp p = hero.Powers[0];
            int idx = run.Hero.IndexOf(p);
            int[] before = Enumerable.Range(0, PowerUps.MaxType + 1).Select(i => run.PowerStock((PowerUp)i)).ToArray();

            run.Step(PowerUps.HeroInputBit(p));
            Assert.IsTrue(PowerUps.IsActive(in run.State, p));
            Assert.AreEqual(PowerUps.TotalTicks(cfg, p) - 1, PowerUps.ActiveTicks(in run.State, p), "Bug List 6: same duration as the bought power-up, no 60 percent");
            int[] after = Enumerable.Range(0, PowerUps.MaxType + 1).Select(i => run.PowerStock((PowerUp)i)).ToArray();
            Assert.AreEqual(before, after, "no unit was taken");
            Assert.AreEqual(0, run.SettlePowerUps(null), "nothing to settle");
            Assert.IsFalse(run.Hero.IsReady(idx), "the cooldown started");
            Assert.AreEqual(0, run.BuildOutcome().PowerUpsUsed, "hero uses are not counted as used power-up units");
        }

        [Test]
        public void ACooldownRefusesTheSecondTap_AndAHeroBitOfAForeignTypeIsDropped()
        {
            var cfg = new SimConfig();
            var run = new RunSession(repo, cfg, "lvl_001_01");
            CharacterSpec hero = HeroOf(CharacterRarity.Uncommon);
            run.ApplyHeroPowers(hero);
            PowerUp mine = hero.Powers[0];
            PowerUp foreign = System.Enum.GetValues(typeof(PowerUp)).Cast<PowerUp>().First(t => PowerUps.IsImplemented(t) && t != mine);
            run.Step(PowerUps.HeroInputBit(foreign));
            Assert.AreEqual(0, run.State.PwUsed, "a hero cannot use a type it does not have");
            run.Step(PowerUps.HeroInputBit(mine));
            Assert.AreEqual(1, run.State.PwUsed);
            int ticks = PowerUps.ActiveTicks(in run.State, mine);
            run.Step(PowerUps.HeroInputBit(mine));
            Assert.AreEqual(1, run.State.PwUsed, "on cooldown");
            Assert.AreEqual(ticks - 1, PowerUps.ActiveTicks(in run.State, mine));
        }

        [Test]
        public void CommonHeroHasNoHeroPowerAndHeroBitsAreDropped()
        {
            var run = new RunSession(repo, new SimConfig(), "lvl_001_01");
            run.ApplyHeroPowers(CharacterDefs.Momo);
            run.Step(SimInput.HeroMask);
            Assert.AreEqual(0, run.State.PwUsed);
        }

        // ---- merged buttons (Bug List 6, 2026-11-09): free hero powers share the button of their type; free first, then bought ----

        [Test]
        public void AMergedPress_UsesTheFreeHeroPowerFirstAndThenABoughtUnit()
        {
            var cfg = new SimConfig();
            var run = new RunSession(repo, cfg, "lvl_001_01");
            CharacterSpec hero = HeroOf(CharacterRarity.Uncommon);
            PowerUp p = hero.Powers[0];
            var stock = new int[PowerUps.MaxType + 1];
            stock[(int)p] = 2;
            run.ApplyStock(stock);
            run.ApplyHeroPowers(hero);
            int idx = run.Hero.IndexOf(p);

            run.Step(PowerUps.InputBit(p));   // the normal button press of the type
            Assert.AreEqual(2, run.PowerStock(p), "free first: no unit taken");
            Assert.IsFalse(run.Hero.IsReady(idx), "the hero cooldown started");
            Assert.AreEqual(PowerUps.TotalTicks(cfg, p) - 1, PowerUps.ActiveTicks(in run.State, p), "same duration as a bought unit");
            Assert.AreEqual(0, run.BuildOutcome().PowerUpsUsed, "a free use is not a used unit");

            run.Step(PowerUps.InputBit(p));   // second press: the free power is cooling down, so a bought unit is used (stacking)
            Assert.AreEqual(1, run.PowerStock(p), "then bought");
            Assert.Greater(PowerUps.ActiveTicks(in run.State, p), PowerUps.TotalTicks(cfg, p), "the bought unit stacked onto the running effect");
            Assert.AreEqual(1, run.BuildOutcome().PowerUpsUsed);
        }

        [Test]
        public void AHeroOnlyTypeWorksWithoutAnyBoughtUnit_AndThenWaitsForItsCooldown()
        {
            var cfg = new SimConfig();
            var run = new RunSession(repo, cfg, "lvl_001_01");
            CharacterSpec hero = HeroOf(CharacterRarity.Uncommon);
            PowerUp p = hero.Powers[0];
            run.ApplyStock(new int[PowerUps.MaxType + 1]);
            run.ApplyHeroPowers(hero);
            Assert.IsFalse(run.OwnedAtStart(p), "nothing bought");
            run.Step(PowerUps.InputBit(p));
            Assert.AreEqual(1, run.State.PwUsed, "the free power works with no stock");
            int ticks = PowerUps.ActiveTicks(in run.State, p);
            run.Step(PowerUps.InputBit(p));
            Assert.AreEqual(1, run.State.PwUsed, "no stock and the free power is cooling down: the press is dropped");
            Assert.AreEqual(ticks - 1, PowerUps.ActiveTicks(in run.State, p));
        }

        [Test]
        public void TheRarityCooldownStillAppliesToAFreePowerUsedThroughTheMergedButton()
        {
            var cfg = new SimConfig();
            var run = new RunSession(repo, cfg, "lvl_001_01");
            CharacterSpec hero = HeroOf(CharacterRarity.Legendary);
            PowerUp p = hero.Powers[0];
            run.ApplyStock(new int[PowerUps.MaxType + 1]);
            run.ApplyHeroPowers(hero);
            int idx = run.Hero.IndexOf(p);
            run.Step(PowerUps.InputBit(p));
            Assert.AreEqual(CharacterDefs.CooldownSeconds[(int)CharacterRarity.Legendary] * SimConfig.TicksPerSecond - 1, run.Hero.RemainingTicks(idx));
        }

        // ---- layout numbers (CUX-3/4) ----

        [Test]
        public void TheTwoGroupsSitOnTheirOwnEdgesAboveThePadWithGaps()
        {
            Assert.GreaterOrEqual(PowerUpBar.ButtonSize, UiKit.MinTouchPx, "at least 48 dp");
            Assert.GreaterOrEqual(PowerUpBar.Gap, UiKit.MinSpacingPx, "at least 8 dp between buttons");
            Assert.AreEqual(PowerUps.TypeCount, PowerUpBar.MaxButtons);
            Assert.AreEqual(PowerUpBar.MaxButtons, PowerGroups.MaxLeft + PowerGroups.MaxRight);
            foreach (bool leftGroup in new[] { true, false })
            {
                int max = leftGroup ? PowerGroups.MaxLeft : PowerGroups.MaxRight;
                for (int k = 0; k < max; k++)
                {
                    Vector2 a = PowerUpBar.PositionFor(k, leftGroup);
                    Assert.GreaterOrEqual(a.y, PowerUpBar.BottomOffset, "above the pads");
                    Assert.AreEqual(leftGroup ? PowerUpBar.SideOffset : -PowerUpBar.SideOffset, a.x, "its own edge");
                    for (int j = k + 1; j < max; j++)
                    {
                        Vector2 b = PowerUpBar.PositionFor(j, leftGroup);
                        Assert.GreaterOrEqual(Mathf.Abs(a.y - b.y), PowerUpBar.ButtonSize + PowerUpBar.Gap - 0.01f, "buttons " + k + " and " + j);
                    }
                }
            }
            Assert.AreEqual(0f, PowerUpBar.AnchorX(true));
            Assert.AreEqual(1f, PowerUpBar.AnchorX(false));
            // one narrow column per group: well clear of the centred banners (BannerW 380) on the narrowest canvas (about 942 px wide at 9:21)
            Assert.Less(PowerUpBar.ReachFromEdge + UiKit.MinSpacingPx, (942f - RunHud.BannerW) * 0.5f);
        }
    }
}
