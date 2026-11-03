using System.Linq;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using BoingUpSkyTower.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace BoingUpSkyTower.Tests
{
    /// <summary>
    /// Hero powers in a run (owner 2026-11-02, docs/34): the cooldown clock (HeroPowerCooldown), one button per power by tier, the duration factor,
    /// the free use (no inventory unit), the layout numbers of the second column. Category HeroPowers.
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
        public void AHeroPowerIsFree_ItTakesNoInventoryUnitAndLastsSixtyPercent()
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
            Assert.AreEqual(cfg.HeroTicks(PowerUps.TotalTicks(cfg, p)) - 1, PowerUps.ActiveTicks(in run.State, p));
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

        // ---- layout numbers (CUX-3/4) ----

        [Test]
        public void HeroColumnClearsTheBoughtBlockAndStaysAboveThePad()
        {
            Assert.GreaterOrEqual(HeroPowerBar.ButtonSize, UiKit.MinTouchPx, "at least 48 dp");
            Assert.GreaterOrEqual(HeroPowerBar.Gap, UiKit.MinSpacingPx, "at least 8 dp between buttons");
            for (int bought = 0; bought <= 7; bought++)
            {
                for (int k = 0; k < HeroPowerBar.MaxButtons; k++)
                {
                    Vector2 h = HeroPowerBar.PositionFor(k, bought);
                    Assert.GreaterOrEqual(h.y, PowerUpBar.BottomOffset, "above the jump pad");
                    if (!HeroPowerBar.OnJumpSide) continue;   // the column is on the other side of the screen: no bought button can touch it
                    for (int b = 0; b < bought; b++)
                    {
                        Vector2 p = PowerUpBar.PositionFor(b, bought);
                        bool sepX = Mathf.Abs(h.x - p.x) >= HeroPowerBar.ButtonSize + HeroPowerBar.Gap - 0.01f;
                        bool sepY = Mathf.Abs(h.y - p.y) >= HeroPowerBar.ButtonSize + HeroPowerBar.Gap - 0.01f;
                        Assert.IsTrue(sepX || sepY, "hero " + k + " touches bought " + b + " (" + bought + " bought)");
                    }
                }
            }
            Assert.AreEqual(PowerUpBar.SideOffset, Mathf.Abs(HeroPowerBar.PositionFor(0, 0).x), "no bought block: the hero column takes the edge spot");
            Assert.AreEqual(0, HeroPowerBar.BoughtColumns(0));
            Assert.AreEqual(2, HeroPowerBar.BoughtColumns(7));
        }
    }
}
