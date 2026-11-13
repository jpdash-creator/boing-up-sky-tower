using System;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Owner report: the speed-up percent on screen does not match the real game speed.
    /// These tests measure the real danger-line speed (KillY gained per tick, no Slow Time) and compare it with the percent the HUD shows
    /// (Sim.ScrollSpeedUpPercent = current speed / starting speed - 1, rounded), for campaign and endless and all five difficulty settings.
    /// The old HUD showed step * ScrollStepPercent and ignored the 6-step limit, the +150 cap, the +campaign speed ceiling and the endless/boss row ramp.
    /// </summary>
    public class ScrollSpeedDisplayTests
    {
        private readonly ITestOutputHelper output;
        public ScrollSpeedDisplayTests(ITestOutputHelper output) { this.output = output; }

        private static readonly int[] StepPercentByDifficulty = SimConfig.DifficultyStepPercent;   // GameSettings.DifficultyStepPercent
        private static int GameSessionCapPercent(int mode) { return SimConfig.TopSpeedUpPercent(mode); }   // GameSession.ApplyDifficulty

        private static SimConfig CfgFor(int difficultySetting)
        {
            SimConfig cfg = new SimConfig();
            cfg.ScrollStepPercent = StepPercentByDifficulty[difficultySetting];
            cfg.ScrollStepCapPercent = GameSessionCapPercent(difficultySetting);
            return cfg;
        }

        private static Sim MakeSim(SimConfig cfg, bool endless, bool ramp, Fx difficulty, out SimState start)
        {
            LevelSpec spec = EndlessTower.MakeSpec(4242, difficulty, SignatureKind.BounceFlowers, 400);
            LevelData data = EndlessTower.BuildFixed(cfg, spec, 80);
            Sim sim = new Sim(cfg, data, difficulty, endless, ramp);
            start = sim.NewState();
            return sim;
        }

        /// <summary>Real speed in units/tick: how far the danger line really moves in one step at the given play time and row.</summary>
        private static long MeasureRawPerTick(Sim sim, SimState baseState, int playTick, int row)
        {
            SimState s = baseState;
            s.StartTick = 0;
            s.Row = row;
            s.Tick = playTick - 1;
            s.Dead = false;
            s.SlowTicks = 0;
            s.Y = Fx.FromInt(1000);                 // high above the line so the follow clamp (MaxY - KillDistance) never moves it
            s.MaxY = s.Y;
            s.KillY = s.Y - Fx.FromInt(5);
            Fx before = s.KillY;
            sim.Step(ref s, 0);
            return s.KillY.Raw - before.Raw;
        }

        private static int ShownPercent(Sim sim, SimState baseState, int playTick, int row)
        {
            SimState s = baseState;
            s.StartTick = 0;
            s.Row = row;
            s.Tick = playTick;
            return sim.ScrollSpeedUpPercent(in s);
        }

        private void Check(string label, Sim sim, SimState st, long baseRaw, int seconds, int row)
        {
            int tick = seconds * SimConfig.TicksPerSecond;
            long real = MeasureRawPerTick(sim, st, tick, row);
            double realPct = (real / (double)baseRaw - 1.0) * 100.0;
            int shown = ShownPercent(sim, st, tick, row);
            output.WriteLine(label + " t=" + seconds + "s row=" + row + "  real=" + (real * 60.0 / Fx.OneRaw).ToString("0.000") + " u/s  realPercent=" + realPct.ToString("0.0") + "  shown=+" + shown + "%");
            Assert.True(Math.Abs(realPct - shown) <= 0.6, label + " t=" + seconds + "s row=" + row + ": real +" + realPct.ToString("0.0") + "% but shown +" + shown + "%");
        }

        [Fact]
        public void Campaign_ShownPercentEqualsRealSpeed_AllDifficulties()
        {
            Fx[] levelDifficulty = { Fx.FromRatio(0, 1), Fx.FromRatio(1, 2), Fx.FromRatio(1, 1) };
            for (int setting = 0; setting < 5; setting++)
            {
                foreach (Fx d in levelDifficulty)
                {
                    SimConfig cfg = CfgFor(setting);
                    SimState st;
                    Sim sim = MakeSim(cfg, false, false, d, out st);
                    long baseRaw = MeasureRawPerTick(sim, st, 1, 0);      // before the first step (t = 0..5 s)
                    foreach (int sec in new[] { 1, 5, 10, 20, 30, 31, 45, 90 })
                        Check("campaign setting=" + setting + " d=" + d.Raw, sim, st, baseRaw, sec, 10);
                }
            }
        }

        [Fact]
        public void Campaign_BossRamp_ShownPercentEqualsRealSpeed()
        {
            for (int setting = 0; setting < 5; setting++)
            {
                SimConfig cfg = CfgFor(setting);
                SimState st;
                Sim sim = MakeSim(cfg, false, true, Fx.FromRatio(1, 2), out st);
                long baseRaw = MeasureRawPerTick(sim, st, 1, 0);
                foreach (int row in new[] { 0, 30, 60, 90, 200 })
                    foreach (int sec in new[] { 0, 10, 40 })
                        Check("boss setting=" + setting, sim, st, baseRaw, sec == 0 ? 1 : sec, row);
            }
        }

        [Fact]
        public void Endless_ShownPercentEqualsRealSpeed_AllDifficulties()
        {
            for (int setting = 0; setting < 5; setting++)
            {
                SimConfig cfg = CfgFor(setting);
                SimState st;
                Sim sim = MakeSim(cfg, true, false, Fx.FromRatio(1, 4), out st);
                long baseRaw = MeasureRawPerTick(sim, st, 1, 0);
                foreach (int row in new[] { 0, 24, 25, 50, 100, 300, 1000 })
                    foreach (int sec in new[] { 1, 5, 12, 30, 60, 120 })
                        Check("endless setting=" + setting, sim, st, baseRaw, sec, row);
            }
        }

        [Fact]
        public void BeforeFirstJump_NoSpeedUpIsShown()
        {
            SimConfig cfg = CfgFor(4);
            SimState st;
            Sim sim = MakeSim(cfg, false, false, Fx.FromRatio(1, 2), out st);
            Assert.Equal(0, sim.ScrollSpeedUpPercent(in st));
        }

        /// <summary>Documents what the old HUD text claimed (step x ScrollStepPercent) next to the true value; the old formula is wrong after 30 s on every difficulty.</summary>
        [Fact]
        public void OldHudFormula_WasWrong_Documented()
        {
            for (int setting = 0; setting < 5; setting++)
            {
                SimConfig cfg = CfgFor(setting);
                SimState st;
                Sim sim = MakeSim(cfg, false, false, Fx.FromRatio(1, 2), out st);
                foreach (int sec in new[] { 5, 10, 15, 20, 25, 30, 60 })
                {
                    int step = sec / cfg.ScrollStepSeconds;
                    int old = step * cfg.ScrollStepPercent;
                    int real = ShownPercent(sim, st, sec * SimConfig.TicksPerSecond, 10);
                    output.WriteLine("setting=" + setting + " t=" + sec + "s old HUD said +" + old + "%, game really is +" + real + "%" + (old == real ? "" : "   <-- MISMATCH"));
                }
            }
        }
    }
}
