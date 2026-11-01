using Xunit;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>docs/29: the character stat model. The baseline must be bit-identical to the SimConfig defaults (golden hashes and bot proofs depend on it).</summary>
    public class CharacterStatsTests
    {
        [Fact]
        public void BaselineApplyLeavesThePhysicsBitIdentical()
        {
            SimConfig expected = new SimConfig();
            SimConfig run = new SimConfig();
            run.JumpBase = Fx.Zero; run.MaxSpeed = Fx.Zero; run.Accel = Fx.Zero; run.WallBoost = Fx.Zero;
            CharacterStats.Default.ApplyTo(run);
            Assert.Equal(expected.JumpBase.Raw, run.JumpBase.Raw);
            Assert.Equal(expected.MaxSpeed.Raw, run.MaxSpeed.Raw);
            Assert.Equal(expected.Accel.Raw, run.Accel.Raw);
            Assert.Equal(expected.WallBoost.Raw, run.WallBoost.Raw);
        }

        [Fact]
        public void BrakingAsAFactorOfControlEqualsTheOldFixedFriction()
        {
            SimConfig cfg = new SimConfig();
            // the old fixed value was Fx.FromRatio(45, 3600): the factor must give the same raw number, or replays would drift
            Assert.Equal(Fx.FromRatio(45, 3600).Raw, (cfg.Accel * cfg.BrakeFactor).Raw);
        }

        [Fact]
        public void MomoSheetMatchesTheDocument()
        {
            StatSheet s = CharacterStats.Default.Sheet(new SimConfig());
            Assert.InRange(s.Jump, 4.49, 4.52);
            Assert.InRange(s.Speed, 8.99, 9.01);
            Assert.InRange(s.Control, 29.9, 30.1);
            Assert.InRange(s.WallBounce, 6.9, 6.95);
            Assert.Equal(0, s.CoinFind);
            Assert.Equal(0, s.GemFind);
            Assert.InRange(s.AirSteering, 17.9, 18.1);
            Assert.InRange(s.Braking, 44.9, 45.1);
            Assert.InRange(s.TurnAroundGround, 59.8, 60.2);
            Assert.InRange(s.TurnAroundAir, 35.8, 36.2);
            Assert.InRange(s.TimeToStop, 0.19, 0.21);
            Assert.InRange(s.StopDistance, 0.89, 0.91);
            Assert.InRange(s.FullSpeedLaunch, 22.6, 22.75);
            Assert.InRange(s.HalfSpeedJump, 5.4, 5.46);
            Assert.InRange(s.FullSpeedJump, 6.4, 6.47);
            Assert.InRange(s.StickyJump, 2.2, 2.23);
            Assert.InRange(s.BounceLaunch, 26.5, 26.7);
            Assert.InRange(s.BounceHeight, 8.8, 8.9);
        }

        [Fact]
        public void TheSheetAgreesWithTheSimsOwnJumpHeight()
        {
            SimConfig cfg = new SimConfig();
            double sim = cfg.MaxJumpHeight().Raw / 65536.0;
            Assert.InRange(CharacterStats.Default.Sheet(cfg).FullSpeedJump, sim - 0.05, sim + 0.05);
        }

        [Fact]
        public void ABetterCharacterScalesTheDerivedValues()
        {
            var c = new CharacterStats { JumpLaunch = 21, MaxSpeed = 10, Control = 36 };
            Assert.Null(c.Validate());
            StatSheet s = c.Sheet(new SimConfig());
            Assert.InRange(s.Jump, 5.5, 5.52);          // 21^2 / 80
            Assert.InRange(s.Braking, 53.9, 54.1);      // 36 x 1.5
            Assert.InRange(s.AirSteering, 21.5, 21.7);  // 36 x 0.6
            Assert.True(s.Jump > CharacterStats.Default.Sheet(new SimConfig()).Jump);
        }

        [Fact]
        public void ValidationHoldsTheBaselineAsTheFloor()
        {
            Assert.Null(new CharacterStats().Validate());
            Assert.NotNull(new CharacterStats { JumpLaunch = 18 }.Validate());
            Assert.NotNull(new CharacterStats { MaxSpeed = 8 }.Validate());
            Assert.NotNull(new CharacterStats { Control = 29 }.Validate());
            Assert.NotNull(new CharacterStats { WallBoostHundredths = 76 }.Validate());
            Assert.NotNull(new CharacterStats { JumpLaunch = 24 }.Validate());
            Assert.NotNull(new CharacterStats { CoinFind = -1 }.Validate());
            Assert.NotNull(new CharacterStats { GemFind = 6 }.Validate());
        }

        [Fact]
        public void AStandingJumpAtTheFloorStillClearsTheLargestNormalGap()
        {
            SimConfig cfg = new SimConfig();
            double standing = CharacterStats.Default.Sheet(cfg).Jump;
            Assert.True(standing > cfg.NormalGapMax.Raw / 65536.0);
        }
    }
}
