using Xunit;
using FluffyClimbers.Economy;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>The crown (owner 2026-11-12): (floors x 10 + combo^2 x 5) x (1 + setting percent), the percents are the scroll step table 10/20/25/40/50, integer basis-point maths, rounded half up.</summary>
    public class CrownScoreTests
    {
        [Fact]
        public void TheSettingPercentsAreTheScrollStepPercents()
        {
            for (int m = 0; m < 5; m++) Assert.Equal(SimConfig.DifficultyStepPercent[m], CrownScore.BonusPercent(m));
            Assert.Equal(new[] { 11000, 12000, 12500, 14000, 15000 }, System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(new[] { 0, 1, 2, 3, 4 }, CrownScore.MultiplierBasisPoints)));
        }

        [Fact]
        public void TheFormulaIsFloorsTimesTenPlusComboSquaredTimesFiveWithTheSettingBonus()
        {
            // 20 floors, combo 3: 200 + 45 = 245
            Assert.Equal(270, CrownScore.FromFloorsCombo(20, 3, 0));   // 245 x 1.10 = 269.5 -> 270 (half up)
            Assert.Equal(294, CrownScore.FromFloorsCombo(20, 3, 1));   // 245 x 1.20
            Assert.Equal(306, CrownScore.FromFloorsCombo(20, 3, 2));   // 245 x 1.25 = 306.25
            Assert.Equal(343, CrownScore.FromFloorsCombo(20, 3, 3));   // 245 x 1.40
            Assert.Equal(368, CrownScore.FromFloorsCombo(20, 3, 4));   // 245 x 1.50 = 367.5 -> 368
        }

        [Fact]
        public void RoundingIsHalfUpAndNeverNegative()
        {
            Assert.Equal(1, CrownScore.FromScore(1, 0));    // 1.10 -> 1
            Assert.Equal(2, CrownScore.FromScore(1, 4));    // 1.50 -> 2 (half up)
            Assert.Equal(3, CrownScore.FromScore(2, 4));    // 3.00
            Assert.Equal(0, CrownScore.FromScore(0, 4));
            Assert.Equal(0, CrownScore.FromScore(-50, 4));
            Assert.Equal(0, CrownScore.FromFloorsCombo(-3, -2, 2));
            Assert.Equal(int.MaxValue, CrownScore.FromScore(int.MaxValue, 4));   // saturates instead of wrapping
        }

        [Fact]
        public void ABadModeClampsAndAHarderSettingNeverPaysLess()
        {
            Assert.Equal(CrownScore.FromScore(1000, 4), CrownScore.FromScore(1000, 99));
            Assert.Equal(CrownScore.FromScore(1000, 0), CrownScore.FromScore(1000, -1));
            for (int m = 1; m < 5; m++) Assert.True(CrownScore.FromScore(1000, m) > CrownScore.FromScore(1000, m - 1));
        }

        [Fact]
        public void TheBaseConstantsMatchTheSimulationScore()
        {
            SimConfig c = new SimConfig();
            Assert.Equal(c.FloorPoints, CrownScore.FloorPoints);
            Assert.Equal(c.ComboPointsMultiplier, CrownScore.ComboPoints);
        }
    }
}
