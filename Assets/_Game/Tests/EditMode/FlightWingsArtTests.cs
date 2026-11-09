using FluffyClimbers.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace FluffyClimbers.Tests
{
    // Flight wings (owner pick A, 2026-11-01): one painted symmetric picture, with the code-drawn wing as the fallback when the PNG is missing (CUX-10).
    [Category("CriticalUX")]
    public class FlightWingsArtTests
    {
        [Test]
        public void CUX10_PaintedWingsLoadAsOneWidePicture()
        {
            Sprite s = Resources.Load<Sprite>(PowerGearArt.WingsPath);
            Assert.IsNotNull(s, "Assets/Resources/" + PowerGearArt.WingsPath + ".png must exist and import as a Sprite");
            Assert.LessOrEqual(s.texture.width, 1024);
            Assert.Greater(s.rect.width, s.rect.height, "a wing pair is wider than tall");
            Assert.AreEqual(0.5f, s.pivot.x / s.rect.width, 0.01f, "pivot centred so the pair sits on the player's back");
        }

        [Test]
        public void CUX10_MissingWingsPictureFallsBackToTheCodeDrawnWing()
        {
            Assert.IsNull(PowerGearArt.LoadWings("Art/Power/does_not_exist"), "a missing picture returns null, RunView then builds the mirrored code-drawn pair");
            Assert.IsNotNull(PowerGearArt.Wing, "the code-drawn fallback always exists");
        }
    }
}
