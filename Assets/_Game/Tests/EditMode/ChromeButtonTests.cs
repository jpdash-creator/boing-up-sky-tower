using BoingUpSkyTower.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Tests
{
    /// <summary>CriticalUX: back (blue) and close X (red) buttons are drawn at the shared 70 percent scale while the hit target stays at least 48 dp (Issue 32).</summary>
    public class ChromeButtonTests
    {
        [Test]
        [Category("CriticalUX")]
        public void CUX4_BackAndCloseDrawnAt70PercentWithFullHitTarget()
        {
            Assert.AreEqual(0.7f, ToyKit.ChromeButtonScale, 0.001f);
            var go = new GameObject("Canvas", typeof(RectTransform));
            try
            {
                var parent = (RectTransform)go.transform;
                for (int role = 2; role <= 3; role++)
                {
                    Button b = ToyKit.GlyphButton(parent, "B" + role, "test", role, role == 2 ? Glyphs.Left : Glyphs.Cross, new Vector2(150f, 150f), null);
                    var rt = (RectTransform)b.transform;
                    Assert.GreaterOrEqual(rt.sizeDelta.x, UiKit.MinTouchPx - 1f, "hit target below 48 dp");
                    Assert.GreaterOrEqual(rt.sizeDelta.y, UiKit.MinTouchPx - 1f, "hit target below 48 dp");
                    var plate = (RectTransform)rt.Find("Plate");
                    Assert.IsNotNull(plate, "drawn plate");
                    Assert.AreEqual(ToyKit.ChromeButtonScale, plate.anchorMax.x - plate.anchorMin.x, 0.001f);
                    Assert.AreEqual(ToyKit.ChromeButtonScale, plate.anchorMax.y - plate.anchorMin.y, 0.001f);
                    Assert.IsFalse(plate.GetComponent<Image>().raycastTarget, "the invisible root takes the touch");
                    Assert.IsTrue(rt.GetComponent<Image>().raycastTarget);
                    Assert.IsNotNull(rt.Find("Plate/Glyph"));
                }
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
