using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace BoingUpSkyTower.Tests.EditModeTests
{
    /// <summary>Timed gameplay UI helpers (doc 46): cached HUD strings, the code-drawn medals, the Pace Ghost setting and the strip reserve of the third HUD row.</summary>
    [Category("CriticalUX")]
    public class TimedUiTests
    {
        IMedalTargetSource savedTargets;

        [SetUp]
        public void SetUp() { savedTargets = MedalTargets.Source; }

        [TearDown]
        public void TearDown()
        {
            MedalTargets.Source = savedTargets;
            PlayerPrefs.DeleteAll();
        }

        [Test]
        public void CachedClockTextMatchesTheFormatterAndIsTheSameInstance()
        {
            TimedText.Warmup();
            int[] ticks = { 0, 1, 3, 5, 6, 59, 60, 719, 720, 1799, 4320, 10800, 21599, 36000 };
            foreach (int t in ticks)
            {
                Assert.AreEqual(TimeRecords.FormatTime(t), TimedText.Time(t), "ticks " + t);
                Assert.AreSame(TimedText.Time(t), TimedText.Time(t), "a cached string is returned again, nothing is built per call (ticks " + t + ")");
            }
            Assert.AreEqual("00:00.0", TimedText.Time(-5));
            Assert.AreEqual("03:00.0", TimedText.Time(TimeRecords.TimeAttackTicks), "the Time Attack countdown start");
        }

        [Test]
        public void DeltaTextHasASignAndIsCached()
        {
            Assert.AreEqual(TimeRecords.FormatDeltaTime(19), TimedText.Delta(19));
            Assert.AreEqual(TimeRecords.FormatDeltaTime(-19), TimedText.Delta(-19));
            Assert.AreEqual(TimeRecords.FormatDeltaTime(0), TimedText.Delta(0));
            Assert.AreSame(TimedText.Delta(-19), TimedText.Delta(-19));
            StringAssert.StartsWith("+", TimedText.Delta(120));
            StringAssert.StartsWith("-", TimedText.Delta(-120));
        }

        [Test]
        public void PaceShortHasASignAndNoUnit()
        {
            Assert.AreEqual("+0.05", TimedText.PaceShort(5));
            Assert.AreEqual("-0.12", TimedText.PaceShort(-12));
            Assert.AreEqual("0.00", TimedText.PaceShort(0));
            Assert.AreEqual("12", TimedText.Int(12));
            Assert.AreSame(TimedText.Int(321), TimedText.Int(321));
        }

        [Test]
        public void MedalsDifferByShapeNotOnlyByColour()
        {
            // sample the silhouettes on a grid: the three tiers must not cover the same cells
            int[] inside = new int[4];
            int[,] map = new int[4, 40 * 40];
            for (int m = 1; m <= 3; m++)
                for (int j = 0; j < 40; j++)
                    for (int i = 0; i < 40; i++)
                        if (MedalIcons.Shape((MedalLevel)m, (i + 0.5f) / 40f, (j + 0.5f) / 40f) <= 0f) { map[m, j * 40 + i] = 1; inside[m]++; }
            for (int m = 1; m <= 3; m++) Assert.Greater(inside[m], 150, "medal " + m + " has a body");
            for (int a = 1; a <= 3; a++)
                for (int b = a + 1; b <= 3; b++)
                {
                    int diff = 0;
                    for (int k = 0; k < 1600; k++) if (map[a, k] != map[b, k]) diff++;
                    Assert.Greater(diff, 90, "tiers " + a + " and " + b + " have visibly different silhouettes");
                }
            Assert.Less(inside[3], inside[1], "the star covers less than the disc");
        }

        [Test]
        public void MedalSpritesAreBuiltOnceAndEarnedDiffersFromHollow()
        {
            Assert.IsNull(MedalIcons.For(MedalLevel.None, true));
            for (int m = 1; m <= 3; m++)
            {
                var tier = (MedalLevel)m;
                Sprite a = MedalIcons.For(tier, true), h = MedalIcons.For(tier, false);
                Assert.IsNotNull(a);
                Assert.IsNotNull(h);
                Assert.AreNotSame(a, h);
                Assert.AreSame(a, MedalIcons.For(tier, true), "cached");
                Assert.AreEqual("Bronze Silver Gold".Split(' ')[m - 1], MedalIcons.Name(tier));
            }
        }

        [Test]
        public void NoMedalStripWithoutTargetData()
        {
            MedalTargets.Source = new NoMedalTargets();
            Assert.IsFalse(MedalStrip.HasTargets("lvl_001_01", 0), "targets may be empty until calibration lands: nothing is shown");
            var d = new DictionaryMedalTargets();
            d.Set("lvl_001_01", 2, 800);
            MedalTargets.Source = d;
            Assert.IsTrue(MedalStrip.HasTargets("lvl_001_01", 2));
            Assert.IsFalse(MedalStrip.HasTargets("lvl_001_01", 3), "per Gameplay mode");
        }

        [Test]
        public void PaceGhostIsOffByDefaultAndStored()
        {
            PlayerPrefs.DeleteAll();
            Assert.IsFalse(GameSettings.PaceGhost);
            GameSettings.PaceGhost = true;
            Assert.IsTrue(GameSettings.PaceGhost);
            Assert.AreEqual(1, PlayerPrefs.GetInt("settings.paceghost", 0));
            GameSettings.PaceGhost = false;
            Assert.IsFalse(GameSettings.PaceGhost);
        }

        [Test]
        public void TopStripReservesTheTimerRow()
        {
            UiKit.SafeAreaNormalized = () => new Rect(0f, 0f, 1f, 1f);
            try
            {
                float top, bottom;
                RunLayout.StripsRef(0.5625f, 10f, out top, out bottom);
                Assert.GreaterOrEqual(top, 16f + RunLayout.RowH + 12f + RunLayout.Row2H + 12f + RunLayout.Row3H - 0.1f, "the strip holds the pills, the gauge and the timer row");
                Assert.GreaterOrEqual(RunLayout.Row3H, 64f, "the timer row is tall enough for 130 percent text");
            }
            finally { UiKit.SafeAreaNormalized = () => new Rect(0f, 0f, 1f, 1f); }
        }
    }
}
