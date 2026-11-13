using Xunit;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>Owner decision 2026-11-12: the scroll speed-up step size per gameplay setting is 10, 20, 25, 40, 50 percent every 5 s (was 20/22/25/28/30), integer maths only.</summary>
    public class StepSizeTableTests
    {
        [Fact]
        public void StepSizesAre10_20_25_40_50()
        {
            Assert.Equal(new[] { 10, 20, 25, 40, 50 }, SimConfig.DifficultyStepPercent);
        }

        [Fact]
        public void OneStepPerGameplaySetting()
        {
            Assert.Equal(SimConfig.DifficultyGemPermille.Length, SimConfig.DifficultyStepPercent.Length);
            Assert.Equal(5, SimConfig.DifficultyStepPercent.Length);
        }

        [Fact]
        public void StepsNeverShrinkWithAHarderSetting()
        {
            for (int i = 1; i < SimConfig.DifficultyStepPercent.Length; i++)
                Assert.True(SimConfig.DifficultyStepPercent[i] > SimConfig.DifficultyStepPercent[i - 1]);
        }
    }
}
