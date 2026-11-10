using FluffyClimbers.Gameplay;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>One jingle per burst: the merge sound cooldown logic.</summary>
    public class SoundCooldownTests
    {
        const float C = SoundCooldown.MergeCooldown;

        [Test] public void FirstSound_PlaysFromTheInitialSentinel() { Assert.IsTrue(SoundCooldown.ShouldPlay(0f, -100f, C)); }
        [Test] public void InsideWindow_StaysSilent() { Assert.IsFalse(SoundCooldown.ShouldPlay(11f, 10f, C)); }
        [Test] public void JustBeforeEnd_StaysSilent() { Assert.IsFalse(SoundCooldown.ShouldPlay(12.49f, 10f, C)); }
        [Test] public void AtWindowEnd_Plays() { Assert.IsTrue(SoundCooldown.ShouldPlay(12.5f, 10f, C)); }
        [Test] public void AfterWindow_Plays() { Assert.IsTrue(SoundCooldown.ShouldPlay(20f, 10f, C)); }
        [Test] public void Cooldown_IsTwoAndAHalfSeconds() { Assert.AreEqual(2.5f, C); }

        [Test]
        public void Burst_PlaysOnlyOnce()
        {
            float last = -100f; int played = 0;
            for (int i = 0; i < 5; i++)
            {
                float now = 50f + i * 0.4f;   // five merges inside 1.6 s
                if (SoundCooldown.ShouldPlay(now, last, C)) { last = now; played++; }
            }
            Assert.AreEqual(1, played);
        }
    }
}
