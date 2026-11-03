using BoingUpSkyTower.Economy;
using Xunit;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>Pure rules of the hidden Admin tools (docs/38): release/development gating, the code check and the 7-tap counter.</summary>
    public class AdminGateTests
    {
        [Fact]
        public void AvailableInDevelopmentBuildsAndAfterTheCodeOnly()
        {
            Assert.True(AdminGate.Available(true, false));
            Assert.True(AdminGate.Available(true, true));
            Assert.True(AdminGate.Available(false, true));
            Assert.False(AdminGate.Available(false, false));
        }

        [Theory]
        [InlineData("0000-TEST", true)]
        [InlineData("  0000-test ", true)]
        [InlineData("0000-TES", false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData(null, false)]
        public void CodeCheck(string typed, bool expected)
        {
            Assert.Equal(expected, AdminGate.CodeMatches(typed, AdminGate.DefaultCode));
        }

        [Fact]
        public void EmptyConfiguredCodeNeverMatches()
        {
            Assert.False(AdminGate.CodeMatches("", ""));
            Assert.False(AdminGate.CodeMatches("x", null));
        }

        [Fact]
        public void SevenQuickTapsOpenTheBoxOnce()
        {
            var c = new AdminGate.TapCounter();
            for (int i = 0; i < 6; i++) Assert.False(c.Tap(i * 0.3));
            Assert.True(c.Tap(1.8));
            Assert.False(c.Tap(2.0));   // starts over
        }

        [Fact]
        public void SlowTapsNeverCount()
        {
            var c = new AdminGate.TapCounter();
            for (int i = 0; i < 20; i++) Assert.False(c.Tap(i * 2.5));
        }
    }
}
