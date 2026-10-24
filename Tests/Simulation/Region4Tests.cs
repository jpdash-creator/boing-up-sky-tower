using System;
using System.Collections.Generic;
using BoingUpSkyTower.Simulation;
using Xunit;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>QA region 4 (Candy Canyon, sticky gum).</summary>
    public class Region4Tests
    {
        static double D(Fx v) { return v.Raw / 65536.0; }

        /// <summary>Measured peak (as the Sim integrates it) of a standing jump from a platform of this kind.</summary>
        static Fx SimulatedStandingPeak(SimConfig cfg, PlatformKind kind)
        {
            Platform p0 = new Platform(); p0.Id = 0; p0.X = cfg.ScreenWidth / 2; p0.Width = cfg.ScreenWidth; p0.Kind = kind;
            Sim sim = new Sim(cfg, new[] { p0 }, Fx.Zero, false);
            SimState s = sim.NewState();
            sim.Step(ref s, SimInput.Jump);
            Fx peak = s.Y;
            for (int t = 0; t < 200 && !s.Grounded; t++) { sim.Step(ref s, 0); if (s.Y > peak) peak = s.Y; }
            return peak;
        }

        /// <summary>
        /// QA region 4: the generator placed the row above a sticky platform up to 2.2 u higher, but a standing jump from sticky
        /// gum (jump speed x0.7) peaks at only 2.10 u in the Sim, so on a small sticky platform the next one was out of reach
        /// ("touching it kills me"). Every platform above a sticky one must lie within the standing peak minus the 0.3 u margin
        /// the normal gaps keep (PickupAndJumpTests.StandingJumpReachesTheLargestNormalRowGapWithMargin).
        /// </summary>
        [Fact]
        [Trait("Category", "CriticalUX")]
        public void StandingJumpFromStickyReachesTheNextPlatformWithMargin()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            SimConfig cfg = new SimConfig();
            Fx peak = SimulatedStandingPeak(cfg, PlatformKind.Sticky);
            Fx largest = Fx.Zero;
            string where = "";
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 1, 5))
            {
                LevelData d = LevelBuilder.Build(cfg, ll.Spec);
                for (int i = 0; i + 1 < d.Platforms.Length; i++)
                {
                    if (d.Platforms[i].Kind != PlatformKind.Sticky) continue;
                    Fx gap = d.Platforms[i + 1].Y - d.Platforms[i].Y;
                    if (gap > largest) { largest = gap; where = ll.Id + " #" + i; }
                }
            }
            Assert.True(largest > Fx.Zero, "no sticky platforms found");
            Assert.True(peak >= largest + Fx.FromRatio(3, 10), "standing sticky peak " + D(peak).ToString("F3") + " vs largest gap above a sticky platform " + D(largest).ToString("F3") + " (" + where + ")");
        }

        /// <summary>QA region 4 (lvl_004_15 gauntlet): vanishing platforms went from fully drawn to ghost with no warning, so standing on one dropped the player without a cue (CUX-7).</summary>
        [Fact]
        [Trait("Category", "CriticalUX")]
        public void VanishingPlatformWarnsBeforeItVanishes()
        {
            SimConfig cfg = new SimConfig();
            Platform p0 = new Platform(); p0.Id = 0; p0.X = cfg.ScreenWidth / 2; p0.Width = cfg.ScreenWidth; p0.Kind = PlatformKind.Normal;
            Platform p1 = new Platform(); p1.Id = 1; p1.Row = 1; p1.X = cfg.ScreenWidth / 2; p1.Y = Fx.FromInt(3); p1.Width = Fx.FromInt(3); p1.Kind = PlatformKind.Vanishing; p1.Phase = 17;
            Sim sim = new Sim(cfg, new[] { p0, p1 }, Fx.Zero, false);
            int cycle = cfg.VanishVisibleTicks + cfg.VanishHiddenTicks;
            int[] crumble = new int[2];
            int warned = 0, lastWarn = -1;
            for (int tick = 0; tick < cycle; tick++)
            {
                bool solid = sim.IsSolid(1, tick, crumble);
                bool warn = sim.VanishWarning(1, tick);
                Assert.False(sim.VanishWarning(0, tick));
                if (warn) { Assert.True(solid, "warning on a hidden platform at tick " + tick); warned++; lastWarn = tick; }
                // the tick after the last warning tick the platform is gone
                if (lastWarn == tick && !sim.IsSolid(1, tick + 1, crumble)) Assert.Equal(cfg.VanishWarnTicks, warned);
            }
            Assert.Equal(cfg.VanishWarnTicks, warned);
            Assert.True(cfg.VanishWarnTicks >= 30, "warning shorter than half a second");
        }
    }
}
