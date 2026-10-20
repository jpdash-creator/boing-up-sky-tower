using System;
using System.Collections.Generic;
using Xunit;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>
    /// Owner playtest 2026-10-20 (list 6): stars sometimes needed two passes, and a single standing jump must reach the next platform.
    /// </summary>
    public class PickupAndJumpTests
    {
        private static Sim WorldWithPickup(SimConfig cfg, Pickup pk)
        {
            Platform p0 = new Platform();
            p0.Id = 0; p0.Row = 0; p0.X = cfg.ScreenWidth / 2; p0.Y = Fx.Zero; p0.Width = cfg.ScreenWidth; p0.Kind = PlatformKind.Normal;
            LevelData d = LevelData.FromPlatforms(new[] { p0 });
            pk.Id = 0;
            d.Pickups = new[] { pk };
            return new Sim(cfg, d, Fx.Zero, false, false);
        }

        private static Pickup Star(SimConfig cfg, Fx x, Fx y, int tag)
        {
            Pickup pk = new Pickup();
            pk.Kind = PickupKind.Star; pk.X = x; pk.Y = y; pk.Radius = cfg.StarRadius; pk.Tag = tag;
            return pk;
        }

        private static SimState Airborne(Sim sim, SimConfig cfg, Fx centreX, Fx centreY, Fx vx, Fx vyAfterGravity)
        {
            SimState s = sim.NewState();
            s.Grounded = false;
            s.Coyote = 0;
            s.X = centreX;
            s.Y = centreY - cfg.PlayerCenterY;
            s.Vx = vx;
            s.Vy = vyAfterGravity + cfg.Gravity;     // Step subtracts gravity first
            s.KillY = Fx.FromInt(-100);
            s.StartTick = 0;
            s.MaxY = s.Y;
            return s;
        }

        [Fact]
        [Trait("Category", "CriticalUX")]
        public void FastPassThroughTheEdgeOfAStarStillCollectsIt()
        {
            // The player centre samples at 0.36 u/tick; a pass that crosses the star circle 0.99 u off-centre has a chord of only
            // 0.28 u, shorter than one step. Both tick positions lie outside the circle, but the path through it does not.
            SimConfig cfg = new SimConfig();
            Pickup pk = Star(cfg, Fx.FromInt(5), Fx.FromInt(20), 1);
            Sim sim = WorldWithPickup(cfg, pk);
            Fx vy = cfg.JumpSpeed(cfg.MaxSpeed);
            SimState s = Airborne(sim, cfg, pk.X - Fx.FromRatio(99, 100), pk.Y - Fx.FromRatio(20, 100), Fx.Zero, vy);
            sim.Step(ref s, 0);   // before: 0.20 below the centre line, after: 0.16 above
            Assert.True(s.PickupTaken[0], "star at the edge of the radius was skipped between two ticks");
            Assert.Equal(1 << 1, s.StarMask);
        }

        [Fact]
        [Trait("Category", "CriticalUX")]
        public void EveryStarIsCollectedOnTheFirstPassAtMaximumSpeed()
        {
            string root = ContentLoader.FindRoot();
            SimConfig cfg = new SimConfig();
            Fx vy = cfg.JumpSpeed(cfg.MaxSpeed);
            int checkedStars = 0;
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 1, 5))
            {
                LevelData data = LevelBuilder.Build(cfg, ll.Spec);
                Sim sim = new Sim(cfg, data, Fx.Zero, false, false);
                for (int i = 0; i < data.Pickups.Length; i++)
                {
                    if (data.Pickups[i].Kind != PickupKind.Star) continue;
                    Pickup pk = data.Pickups[i];
                    for (int dirSign = -1; dirSign <= 1; dirSign += 2)
                    {
                        Fx vx = cfg.MaxSpeed * dirSign;
                        // Straight through the centre: three ticks before it, at maximum horizontal and vertical speed.
                        SimState s = Airborne(sim, cfg, pk.X - vx * 3, pk.Y - vy * 3, vx, vy);
                        s.Invuln = 1000;
                        if (s.X < cfg.PlayerHalfWidth || s.X > cfg.ScreenWidth - cfg.PlayerHalfWidth) continue;   // would hit the wall first
                        for (int t = 0; t < 6; t++) sim.Step(ref s, 0);
                        Assert.True(s.PickupTaken[i], ll.Id + " star " + pk.Tag + " missed on a centre pass (dir " + dirSign + ")");
                    }
                    checkedStars++;
                }
            }
            Assert.True(checkedStars >= 75 * 3);
        }

        [Fact]
        [Trait("Category", "CriticalUX")]
        public void StarsAndGemsSitInClearAirSoTheDrawnPositionIsTheCollectedPosition()
        {
            string root = ContentLoader.FindRoot();
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 1, 5))
            {
                LevelData data = LevelBuilder.Build(cfg, ll.Spec);
                for (int i = 0; i < data.Pickups.Length; i++)
                {
                    Pickup pk = data.Pickups[i];
                    if (pk.Kind != PickupKind.Star && pk.Kind != PickupKind.Gem) continue;
                    Assert.True(LevelBuilder.IsPickupClearOfPlatforms(data.Platforms, pk),
                        ll.Id + " pickup " + pk.Id + " (" + pk.Kind + ") is drawn over a platform slab");
                }
            }
        }

        [Fact]
        [Trait("Category", "CriticalUX")]
        public void StandingJumpReachesTheLargestNormalRowGapWithMargin()
        {
            string root = ContentLoader.FindRoot();
            SimConfig cfg = new SimConfig();
            Fx largest = Fx.Zero;
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 1, 5))
            {
                LevelData d = LevelBuilder.Build(cfg, ll.Spec);
                for (int i = 0; i + 1 < d.Platforms.Length; i++)
                {
                    Platform a = d.Platforms[i];
                    if (a.Kind == PlatformKind.Sticky || a.Kind == PlatformKind.Bounce || a.Kind == PlatformKind.BounceCap) continue;
                    Fx gap = d.Platforms[i + 1].Y - a.Y;
                    if (gap > largest) largest = gap;
                }
            }
            Fx peak = cfg.JumpBase * cfg.JumpBase / (cfg.Gravity * 2);
            Assert.True(peak >= largest + Fx.FromRatio(3, 10), "standing peak " + peak + " largest normal gap " + largest);
            Assert.True(largest <= cfg.NormalGapMax, "generator exceeded NormalGapMax: " + largest);
            Assert.True(peak <= largest + Fx.FromRatio(5, 10), "standing peak " + peak + " is more than needed (largest gap " + largest + ")");

            // And in the simulation itself: a standing jump from the start platform lands on a platform one largest-gap above.
            Platform p0 = new Platform(); p0.Id = 0; p0.X = cfg.ScreenWidth / 2; p0.Width = cfg.ScreenWidth; p0.Kind = PlatformKind.Normal;
            Platform p1 = new Platform(); p1.Id = 1; p1.Row = 1; p1.X = cfg.ScreenWidth / 2; p1.Y = largest; p1.Width = Fx.FromInt(3); p1.Kind = PlatformKind.Normal;
            Sim sim = new Sim(cfg, new[] { p0, p1 }, Fx.Zero, false);
            SimState s = sim.NewState();
            sim.Step(ref s, SimInput.Jump);
            for (int t = 0; t < 400 && s.Row < 1 && !s.Dead; t++) sim.Step(ref s, 0);
            Assert.Equal(1, s.Row);
            Assert.True(s.Grounded);
        }

        [Fact]
        [Trait("Category", "CriticalUX")]
        public void JumpPeaksAreAboutOneAndTwoMedianGaps()
        {
            SimConfig cfg = new SimConfig();
            List<double> gaps = new List<double>();
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(ContentLoader.FindRoot(), 1, 5))
            {
                LevelData d = LevelBuilder.Build(cfg, ll.Spec);
                for (int i = 0; i + 1 < d.Platforms.Length; i++)
                {
                    Platform a = d.Platforms[i];
                    if (a.Kind == PlatformKind.Sticky || a.Kind == PlatformKind.Bounce || a.Kind == PlatformKind.BounceCap) continue;
                    double g = (d.Platforms[i + 1].Y - a.Y).Raw / 65536.0;
                    if (g > 0.01) gaps.Add(g);
                }
            }
            gaps.Sort();
            double median = gaps[gaps.Count / 2];
            double standing = (cfg.JumpBase * cfg.JumpBase / (cfg.Gravity * 2)).Raw / 65536.0;
            double full = cfg.MaxJumpHeight().Raw / 65536.0;
            Assert.InRange(standing / median, 1.2, 1.4);
            Assert.InRange(full / median, 1.7, 2.3);
        }

        [Fact]
        public void LayoutReachNeverExceedsTheRealFullSpeedReach()
        {
            SimConfig cfg = new SimConfig();
            for (int h = 0; h <= 42; h++)
            {
                Fx gap = Fx.FromRatio(h, 10);
                Assert.True(cfg.LayoutMaxReachDx(gap, Fx.One) <= cfg.MaxReachDx(gap), "gap " + gap);
            }
        }
    }
}
