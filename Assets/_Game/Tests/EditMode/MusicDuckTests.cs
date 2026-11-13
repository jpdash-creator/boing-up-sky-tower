using FluffyClimbers.Gameplay;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>Owner bug list 2 item 3: music never restarts on a jingle and never layers; jingles duck the background.</summary>
    public class MusicDuckTests
    {
        const float Dt = 1f / 60f;

        static void Run(MusicDuck d, float seconds)
        {
            int n = (int)(seconds / Dt + 0.5f);
            for (int i = 0; i < n; i++) d.Tick(Dt);
        }

        [Test] public void Idle_IsFullGain() { var d = new MusicDuck(); Run(d, 1f); Assert.AreEqual(1f, d.Gain); Assert.IsFalse(d.Active); }

        [Test]
        public void Duck_DipsToFloorWithinAttackTime()
        {
            var d = new MusicDuck();
            d.Duck(1f);
            Run(d, MusicDuck.AttackSeconds + 0.02f);
            Assert.AreEqual(MusicDuck.Floor, d.Gain, 0.001f);
        }

        [Test]
        public void Duck_HoldsWhileTheJinglePlays()
        {
            var d = new MusicDuck();
            d.Duck(1f);
            Run(d, 0.9f);
            Assert.AreEqual(MusicDuck.Floor, d.Gain, 0.001f);
        }

        [Test]
        public void Duck_ReturnsToFullAfterReleaseTime()
        {
            var d = new MusicDuck();
            d.Duck(0.5f);
            Run(d, 0.5f + MusicDuck.ReleaseSeconds + 0.05f);
            Assert.AreEqual(1f, d.Gain, 0.001f);
            Assert.IsFalse(d.Active);
        }

        [Test]
        public void Duck_IsMidwayWhileReleasing()
        {
            var d = new MusicDuck();
            d.Duck(0.3f);
            Run(d, 0.3f + MusicDuck.ReleaseSeconds * 0.5f);
            Assert.Greater(d.Gain, MusicDuck.Floor + 0.05f);
            Assert.Less(d.Gain, 0.95f);
        }

        [Test]
        public void OverlappingJingles_ExtendTheHoldAndNeverGoBelowFloor()
        {
            var d = new MusicDuck();
            for (int k = 0; k < 5; k++) { d.Duck(1f); Run(d, 0.4f); Assert.GreaterOrEqual(d.Gain, MusicDuck.Floor - 0.0001f); }
            Assert.AreEqual(MusicDuck.Floor, d.Gain, 0.001f);   // still down after 2 s of a 1 s jingle repeated
            d.Duck(0.2f);   // a shorter one never shortens the hold
            Assert.Greater(d.HoldRemaining, 0.5f);
        }

        [Test]
        public void LongerJingle_ExtendsTheHold()
        {
            var d = new MusicDuck();
            d.Duck(0.5f);
            Run(d, 0.3f);
            d.Duck(1.5f);
            Assert.AreEqual(1.5f, d.HoldRemaining, 0.0001f);
        }

        [Test]
        public void Reset_RestoresFullGainAtOnce()
        {
            var d = new MusicDuck();
            d.Duck(1f);
            Run(d, 0.3f);
            d.Reset();
            Assert.AreEqual(1f, d.Gain);
            Assert.IsFalse(d.Active);
        }

        [Test]
        public void Envelope_IsFrameRateIndependent()
        {
            var a = new MusicDuck(); var b = new MusicDuck();
            a.Duck(0.4f); b.Duck(0.4f);
            for (int i = 0; i < 60; i++) a.Tick(1f / 120f);   // 0.5 s at 120 Hz
            for (int i = 0; i < 15; i++) b.Tick(1f / 30f);    // 0.5 s at 30 Hz
            Assert.AreEqual(a.Gain, b.Gain, 0.1f);   // the hold ends on a tick edge: one 30 Hz tick of release (about 0.058 gain) is the inherent difference
        }

        [Test]
        public void SameTrack_KeepsPosition()
        {
            var s = new MusicSelector();
            Assert.AreEqual(MusicSelector.Action.Start, s.Request(11));
            int slot = s.Active;
            for (int i = 0; i < 5; i++) Assert.AreEqual(MusicSelector.Action.Keep, s.Request(11));
            Assert.AreEqual(slot, s.Active);
            Assert.AreEqual(11, s.ActiveClip);
        }

        [Test]
        public void DifferentTrack_SwitchesSlotAndOldOneFadesOut()
        {
            var s = new MusicSelector();
            s.Request(11);
            int first = s.Active;
            Assert.AreEqual(MusicSelector.Action.Start, s.Request(22));
            Assert.AreNotEqual(first, s.Active);
            Assert.IsTrue(s.IsPlaying(first));   // still fading out, the audio layer stops it when silent
            s.Stopped(first);
            Assert.AreEqual(0, s.ClipIn(first));
        }

        [Test]
        public void BackToATrackThatIsStillFadingOut_ResumesWithoutRestart()
        {
            var s = new MusicSelector();
            s.Request(11);            // menu loop
            int menuSlot = s.Active;
            s.Request(22);            // run music starts, menu fades out
            Assert.AreEqual(MusicSelector.Action.Resume, s.Request(11));
            Assert.AreEqual(menuSlot, s.Active);
        }

        [Test]
        public void MusicOff_ForgetsTracks_NextRequestStarts()
        {
            var s = new MusicSelector();
            s.Request(11);
            s.StopAll();
            Assert.AreEqual(0, s.ActiveClip);
            Assert.AreEqual(MusicSelector.Action.Start, s.Request(11));
        }
    }
}
