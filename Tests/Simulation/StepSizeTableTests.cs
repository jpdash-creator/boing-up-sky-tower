using Xunit;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>Bug List 6 item 6: the scroll speed-up step size per gameplay setting (Relaxed x1.00, Easy x1.10, Normal x1.25, Hard x1.40, Insane x1.50 of 20 percent), integer maths only.</summary>
    public class StepSizeTableTests
    {
        [Fact]
        public void StepSizesAre20_22_25_28_30()
        {
            Assert.Equal(new[] { 20, 22, 25, 28, 30 }, SimConfig.DifficultyStepPercent);
        }

        [Fact]
        public void EveryStepIsTheBaseTimesItsMultiplier()
        {
            Assert.Equal(5, SimConfig.DifficultyStepMultiplierPercent.Length);
            Assert.Equal(SimConfig.DifficultyGemPermille.Length, SimConfig.DifficultyStepPercent.Length);
            for (int i = 0; i < SimConfig.DifficultyStepPercent.Length; i++)
                Assert.Equal(SimConfig.ScrollStepBasePercent * SimConfig.DifficultyStepMultiplierPercent[i] / 100, SimConfig.DifficultyStepPercent[i]);
            Assert.Equal(20, SimConfig.ScrollStepBasePercent);
        }

        [Fact]
        public void StepsNeverShrinkWithAHarderSetting()
        {
            for (int i = 1; i < SimConfig.DifficultyStepPercent.Length; i++)
                Assert.True(SimConfig.DifficultyStepPercent[i] > SimConfig.DifficultyStepPercent[i - 1]);
        }
    }
}
