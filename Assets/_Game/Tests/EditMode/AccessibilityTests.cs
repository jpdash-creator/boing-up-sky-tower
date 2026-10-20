using SkyHopper.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace SkyHopper.Tests.EditModeTests
{
    /// <summary>CUX-5 / CUX-6: contrast of the fixed colour combinations, accessibility settings and haptics gating.</summary>
    [Category("CriticalUX")]
    public class AccessibilityTests
    {
        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteAll();
            GameSettings.ApplyTextScale();
        }

        static Color Over(Color fg, float alpha, Color bg) { return new Color(fg.r * alpha + bg.r * (1f - alpha), fg.g * alpha + bg.g * (1f - alpha), fg.b * alpha + bg.b * (1f - alpha), 1f); }

        [Test]
        public void PanelInkIsReadableOnTheCreamPopupPanel()
        {
            Color worstCream = new Color(0.93f, 0.85f, 0.72f);   // darker than the sampled panel interior
            Assert.GreaterOrEqual(UiKit.Contrast(UiKit.PanelInk, worstCream), 4.5f);
        }

        [Test]
        public void ButtonInkIsReadableOnEveryButtonTint()
        {
            Color[] tints =
            {
                new Color(0.95f, 0.45f, 0.65f), new Color(0.35f, 0.75f, 0.95f), new Color(0.75f, 0.78f, 0.85f),
                new Color(0.95f, 0.75f, 0.2f), new Color(0.45f, 0.8f, 0.45f), new Color(0.5f, 0.5f, 0.5f)
            };
            foreach (Color c in tints)
                Assert.GreaterOrEqual(UiKit.Contrast(UiKit.ButtonInk, c), 4.5f, "button tint " + c);
        }

        [Test]
        public void HudWhiteTextIsReadableOverTheWorstBackground()
        {
            Color strip = new Color(0.05f, 0.07f, 0.14f);
            Color overWhiteSky = Over(strip, 0.72f, Color.white);
            Assert.GreaterOrEqual(UiKit.Contrast(Color.white, overWhiteSky), 4.5f, "HUD strip");
            Color nameBacking = Over(Color.black, 0.75f, Color.white);
            Assert.GreaterOrEqual(UiKit.Contrast(Color.white, nameBacking), 4.5f, "map name backing");
        }

        [Test]
        public void TextSizeCyclesThroughTheThreeSteps()
        {
            PlayerPrefs.DeleteAll();
            Assert.AreEqual(100, GameSettings.TextPercent);
            GameSettings.NextTextPercent(); Assert.AreEqual(115, GameSettings.TextPercent); Assert.AreEqual(1.15f, UiKit.TextScale, 0.001f);
            GameSettings.NextTextPercent(); Assert.AreEqual(130, GameSettings.TextPercent); Assert.AreEqual(1.3f, UiKit.TextScale, 0.001f);
            GameSettings.NextTextPercent(); Assert.AreEqual(100, GameSettings.TextPercent); Assert.AreEqual(1f, UiKit.TextScale, 0.001f);
        }

        [Test]
        public void TextIsNeverBuiltBelowTheMinimumSize()
        {
            var go = new GameObject("t", typeof(RectTransform));
            try
            {
                var t = UiKit.AddText(go.transform, "x", "hello", 10, Color.white);
                Assert.GreaterOrEqual(t.fontSize, Mathf.FloorToInt(UiKit.MinBodyTextPx));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void HapticsOnlyFireWhenEnabled()
        {
            Haptics.Pulses = 0;
            GameSettings.Haptics = false;
            Haptics.Pulse();
            Assert.AreEqual(0, Haptics.Pulses);
            GameSettings.Haptics = true;
            Haptics.Pulse();
            Assert.AreEqual(1, Haptics.Pulses);
        }

        [Test]
        public void ReducedMotionAndLeftHandedPersist()
        {
            GameSettings.ReducedMotion = true;
            GameSettings.LeftHanded = true;
            Assert.IsTrue(GameSettings.ReducedMotion);
            Assert.IsTrue(GameSettings.LeftHanded);
        }
    }
}
