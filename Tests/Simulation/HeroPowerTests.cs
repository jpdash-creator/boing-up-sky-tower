using System;
using System.Collections.Generic;
using Xunit;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>
    /// Hero powers (owner 2026-11-02, docs/34): a hero activation is a normal power-up activation of the same type for HeroPowerDurationPercent (60) percent of the time,
    /// it uses its own input bits, never stacks onto itself, is deterministic, and runs without hero powers (bot proofs, golden files) are untouched.
    /// The cooldown clock (HeroPowerCooldown) is pure logic. Category HeroPowers. (The "costs no inventory unit" rule lives in RunSession: Unity EditMode HeroPowerEditTests.)
    /// </summary>
    [Trait("Category", "HeroPowers")]
    public class HeroPowerTests
    {
        private static readonly SimConfig Cfg = new SimConfig();

        private static Sim Tower(int rows)
        {
            List<Platform> extra = new List<Platform>();
            for (int i = 1; i <= rows; i++) extra.Add(T3.P(i, 5.0, 3.0 * i, 10.0, PlatformKind.Normal));
            LevelData d = T3.World(Cfg, null, null, extra.ToArray());
            return new Sim(Cfg, d, Fx.FromRatio(5, 10), false, false);
        }

        private static int Bought(PowerUp p) { return PowerUps.InputBit(p); }
        private static int Hero(PowerUp p) { return PowerUps.HeroInputBit(p); }

        private static long Mix(long h, int v) { unchecked { return (h ^ (uint)v) * 1099511628211L; } }

        private static long Hash(SimState s)
        {
            long h = 1469598103934665603L;
            h = Mix(h, s.Tick); h = Mix(h, s.X.Raw); h = Mix(h, s.Y.Raw); h = Mix(h, s.Vx.Raw); h = Mix(h, s.Vy.Raw);
            h = Mix(h, s.Row); h = Mix(h, s.Score); h = Mix(h, s.Dead ? 1 : 0); h = Mix(h, s.PwUsed); h = Mix(h, s.PwUsedMask);
            h = Mix(h, s.FlightTicks); h = Mix(h, s.RocketTicks); h = Mix(h, s.MagnetTicks); h = Mix(h, s.SlowTicks);
            h = Mix(h, s.ShieldTicks); h = Mix(h, s.WallStickTicks); h = Mix(h, s.DoubleJumpTicks); h = Mix(h, s.RocketTargetRow);
            return h;
        }

        // ---------- constants and bits ----------

        [Fact]
        public void DurationFactorIsSixtyPercent()
        {
            Assert.Equal(60, Cfg.HeroPowerDurationPercent);
            Assert.Equal(0.6f, SimConfig.HeroPowerDurationFactor);
            Assert.Equal(SimConfig.HeroPowerDurationFactor, Cfg.HeroPowerDurationPercent / 100f);
            Assert.Equal(1080, Cfg.HeroTicks(1800));   // Magnet 30 s -> 18 s
            Assert.Equal(2160, Cfg.HeroTicks(3600));   // Shield 60 s -> 36 s
            Assert.Equal(360, Cfg.HeroTicks(600));     // Flight 10 s -> 6 s
            Assert.Equal(144, Cfg.HeroTicks(240));     // Rocket 4 s -> 2.4 s
            Assert.Equal(12, Cfg.HeroRocketFloors);    // Rocket 20 floors -> 12
            Assert.Equal(1, Cfg.HeroTicks(0));
        }

        [Fact]
        public void HeroBitsAreOwnAndDoNotOverlapAnythingElse()
        {
            int all = 0;
            for (int i = 1; i <= PowerUps.TypeCount; i++)
            {
                int bit = Hero((PowerUp)i);
                Assert.NotEqual(0, bit);
                Assert.Equal(0, all & bit);
                all |= bit;
            }
            Assert.Equal(SimInput.HeroMask, all);
            Assert.Equal(1024, Hero(PowerUp.Flight));
            Assert.Equal(65536, Hero(PowerUp.SlowTime));
            Assert.Equal(0, SimInput.HeroMask & (SimInput.Left | SimInput.Right | SimInput.Jump | SimInput.PowerMask));
            Assert.Equal(0, Hero(PowerUp.SpringBoots));
            Assert.Equal(0, Hero(PowerUp.None));
        }

        // ---------- effect: same as bought, 60 percent of the time ----------

        [Theory]
        [InlineData(PowerUp.Flight)]
        [InlineData(PowerUp.DoubleJump)]
        [InlineData(PowerUp.WallStick)]
        [InlineData(PowerUp.Shield)]
        [InlineData(PowerUp.Magnet)]
        [InlineData(PowerUp.SlowTime)]
        [InlineData(PowerUp.Rocket)]
        public void HeroActivationLastsSixtyPercentOfTheBoughtOne(PowerUp p)
        {
            Sim sim = Tower(40);
            SimState bought = sim.NewState();
            SimState hero = sim.NewState();
            sim.Step(ref bought, Bought(p));
            sim.Step(ref hero, Hero(p));
            int full = PowerUps.ActiveTicks(in bought, p) + 1;   // one tick already counted down in the activation tick
            int part = PowerUps.ActiveTicks(in hero, p) + 1;
            Assert.Equal(PowerUps.TotalTicks(Cfg, p), full);
            Assert.Equal(Cfg.HeroTicks(PowerUps.TotalTicks(Cfg, p)), part);
            Assert.True(PowerUps.IsActive(in hero, p));
            Assert.Equal(1, hero.PwUsed);
            Assert.Equal(1 << (int)p, hero.PwUsedMask);
            Assert.InRange(part / (double)full, 0.599, 0.601);
        }

        [Fact]
        public void HeroRocketClimbsSixtyPercentOfTheFloorsInSixtyPercentOfTheTime()
        {
            Sim sim = Tower(60);
            SimState b = sim.NewState();
            SimState h = sim.NewState();
            sim.Step(ref b, Bought(PowerUp.Rocket));
            sim.Step(ref h, Hero(PowerUp.Rocket));
            Assert.Equal(20, b.RocketTargetRow);
            Assert.Equal(12, h.RocketTargetRow);
            int guardB = 0, guardH = 0;
            while (b.RocketTicks > 0 && guardB++ < 1000) sim.Step(ref b, 0);
            while (h.RocketTicks > 0 && guardH++ < 1000) sim.Step(ref h, 0);
            Assert.Equal(239, guardB);
            Assert.Equal(143, guardH);
            Assert.True(h.Y > Fx.FromInt(30), "the hero rocket climbed (about 12 rows of 3 u)");
            Assert.True(h.Y < b.Y, "shorter climb");
        }

        [Fact]
        public void HeroPowerNeverStacksOntoItselfButBoughtStillStacks()
        {
            Sim sim = Tower(20);
            SimState s = sim.NewState();
            sim.Step(ref s, Hero(PowerUp.Magnet));
            int after = s.MagnetTicks;
            sim.Step(ref s, Hero(PowerUp.Magnet));   // refused: already running
            Assert.Equal(after - 1, s.MagnetTicks);
            Assert.Equal(1, s.PwUsed);
            sim.Step(ref s, Bought(PowerUp.Magnet));  // a bought tap adds the full base time
            Assert.Equal(after - 2 + Cfg.MagnetTicks, s.MagnetTicks);
            Assert.Equal(2, s.PwUsed);
        }

        [Fact]
        public void BoughtAndHeroOfTheSameTypeInOneTickRunsTheBoughtOneOnly()
        {
            Sim sim = Tower(20);
            SimState s = sim.NewState();
            sim.Step(ref s, Bought(PowerUp.Shield) | Hero(PowerUp.Shield));
            Assert.Equal(Cfg.ShieldTicks - 1, s.ShieldTicks);
            Assert.Equal(1, s.PwUsed);
        }

        [Fact]
        public void HeroFlightAndRocketExcludeEachOther()
        {
            Sim sim = Tower(40);
            SimState s = sim.NewState();
            sim.Step(ref s, Hero(PowerUp.Flight) | Hero(PowerUp.Rocket));
            Assert.True(s.FlightTicks > 0);
            Assert.Equal(0, s.RocketTicks);
            Assert.False(PowerUps.CanActivateHero(in s, PowerUp.Rocket));
            Assert.False(PowerUps.CanActivateHero(in s, PowerUp.Flight));   // running
        }

        [Fact]
        public void HeroBitOfAnUnimplementedTypeDoesNothingAndANoHeroRunHasNoHeroState()
        {
            Sim sim = Tower(10);
            SimState s = sim.NewState();
            sim.Step(ref s, Hero(PowerUp.SpringBoots));
            Assert.Equal(0, s.PwUsed);
            Assert.Equal(0, s.PwUsedMask);
        }

        // ---------- determinism and untouched runs ----------

        [Fact]
        public void ReplayWithHeroActivationsIsDeterministic()
        {
            PowerUp[] types = { PowerUp.Flight, PowerUp.Rocket, PowerUp.DoubleJump, PowerUp.WallStick, PowerUp.Shield, PowerUp.Magnet, PowerUp.SlowTime };
            for (int seed = 1; seed <= 4; seed++)
            {
                Sim sim = Tower(60);
                var script = new int[1500];
                Random rnd = new Random(seed);
                for (int t = 0; t < script.Length; t++)
                {
                    int input = ((t / 37) % 2 == 0) ? SimInput.Right : SimInput.Left;
                    if (rnd.Next(20) == 0) input |= SimInput.Jump;
                    if (rnd.Next(150) == 0) input |= Hero(types[rnd.Next(types.Length)]);
                    if (rnd.Next(300) == 0) input |= Bought(types[rnd.Next(types.Length)]);
                    script[t] = input;
                }
                SimState a = sim.NewState();
                SimState b = sim.NewState();
                for (int t = 0; t < script.Length; t++) { sim.Step(ref a, script[t]); sim.Step(ref b, script[t]); }
                Assert.Equal(Hash(a), Hash(b));
                Assert.True(a.PwUsed > 0, "seed " + seed + " used something");
            }
        }

        [Fact]
        public void RunsWithoutHeroPowersAreUnchanged_BotProofsAndGoldenLayoutsDoNotSeeHeroBits()
        {
            // Hero activation never touches the level: the layout hash is the same before and after a hero run (the golden file test pins the 75 hashes).
            string root = ContentLoader.FindRoot();
            Assert.NotNull(root);
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 1, 1))
            {
                if (ll.Id != "lvl_001_05") continue;
                LevelData data = LevelBuilder.Build(Cfg, ll.Spec);
                long before = data.ComputeHash();
                Sim sim = new Sim(Cfg, data, ll.Spec);
                SimState s = sim.NewState();
                for (int t = 0; t < 300; t++) sim.Step(ref s, SimInput.Right | (t == 5 ? Hero(PowerUp.Shield) | Hero(PowerUp.Magnet) : 0));
                Assert.Equal(before, data.ComputeHash());
                Assert.Equal(before, LevelBuilder.Build(Cfg, ll.Spec).ComputeHash());

                // The solvability bot only uses Left/Right/Jump: its winning line has no power-up or hero activation at all.
                LevelData d2 = LevelBuilder.Build(Cfg, ll.Spec);
                BotResult br = new LevelBot(new Sim(Cfg, d2, ll.Spec), ll.Spec, d2, 20000).Solve();
                Assert.True(br.Solved);
                Assert.Equal(0, br.Final.PwUsed);
                Assert.Equal(0, br.Final.PwUsedMask);
                return;
            }
            throw new InvalidOperationException("lvl_001_05 not found");
        }

        [Fact]
        public void BitsOutsideTheHeroMaskNeverActivateAHeroPower()
        {
            // An old replay word (Left/Right/Jump and bought power bits) with no hero bit leaves a run identical to one where the hero bits are absent.
            Sim sim = Tower(30);
            SimState a = sim.NewState();
            SimState b = sim.NewState();
            for (int t = 0; t < 400; t++)
            {
                int input = SimInput.Right | (t % 40 == 0 ? SimInput.Jump : 0) | (t == 10 ? Bought(PowerUp.Magnet) : 0);
                sim.Step(ref a, input);
                sim.Step(ref b, input & ~SimInput.HeroMask);
            }
            Assert.Equal(Hash(a), Hash(b));
        }

        // ---------- cooldown clock ----------

        [Fact]
        public void CooldownStartsReadyAndEachPowerHasItsOwnClock()
        {
            var cd = new HeroPowerCooldown(new[] { PowerUp.Magnet, PowerUp.Shield, PowerUp.Rocket }, 22);
            Assert.Equal(3, cd.Count);
            Assert.Equal(22 * 60, cd.CooldownTicks);
            for (int i = 0; i < 3; i++) { Assert.True(cd.IsReady(i)); Assert.Equal(0, cd.RemainingSeconds(i)); Assert.Equal(0f, cd.Fraction(i)); }
            Assert.True(cd.TryUse(1));
            Assert.False(cd.IsReady(1));
            Assert.True(cd.IsReady(0));
            Assert.True(cd.IsReady(2));
            Assert.False(cd.TryUse(1), "still cooling down");
            Assert.Equal(22, cd.RemainingSeconds(1));
            Assert.Equal(1f, cd.Fraction(1));
            for (int t = 0; t < 22 * 60 - 1; t++) cd.Tick();
            Assert.False(cd.IsReady(1));
            Assert.Equal(1, cd.RemainingSeconds(1));
            cd.Tick();
            Assert.True(cd.IsReady(1));
            Assert.True(cd.TryUse(1));
            Assert.True(cd.TryUse(0), "other powers are independent");
        }

        [Fact]
        public void CooldownKeepsOnlyImplementedUniquePowersUpToFour()
        {
            var cd = new HeroPowerCooldown(new[] { PowerUp.Magnet, PowerUp.Magnet, PowerUp.SpringBoots, PowerUp.None, PowerUp.Shield, PowerUp.Flight, PowerUp.Rocket, PowerUp.SlowTime }, 10);
            Assert.Equal(4, cd.Count);
            Assert.Equal(PowerUp.Magnet, cd.Type(0));
            Assert.Equal(PowerUp.Shield, cd.Type(1));
            Assert.Equal(PowerUp.Flight, cd.Type(2));
            Assert.Equal(PowerUp.Rocket, cd.Type(3));
            Assert.Equal(PowerUp.None, cd.Type(4));
            Assert.Equal(1, cd.IndexOf(PowerUp.Shield));
            Assert.Equal(-1, cd.IndexOf(PowerUp.SlowTime));
            Assert.False(cd.TryUse(4));
            Assert.False(cd.IsReady(-1));
            Assert.Equal(0, new HeroPowerCooldown(null, 30).Count);
        }
    }
}
