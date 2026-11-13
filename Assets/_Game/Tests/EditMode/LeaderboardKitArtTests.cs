using FluffyClimbers.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace FluffyClimbers.Tests
{
    /// <summary>The painted leaderboard kit (<c>lb_*</c>, owner picks 2026-11-13): every sprite loads and the stadium and frame pieces carry the 9-slice borders the screen relies on (CUX-9: only flat middles stretch).</summary>
    public class LeaderboardKitArtTests
    {
        static readonly string[] All =
        {
            "lb_row_gold", "lb_row_silver", "lb_row_bronze", "lb_row_green", "lb_row_grey", "lb_row_you",
            "lb_frame", "lb_frame_top", "lb_frame_body",
            "lb_icon_gift", "lb_icon_clock", "lb_icon_crown", "lb_icon_info", "lb_icon_trophy", "lb_icon_map", "lb_icon_infinity",
            "lb_medal_gold", "lb_medal_silver", "lb_medal_bronze", "lb_badge_green", "lb_badge_grey",
            "lb_tab_on", "lb_tab_off", "lb_pill_timer", "lb_pill_score", "lb_plate_round", "lb_strip_header"
        };

        static readonly string[] SlicedLeftRight =
        {
            "lb_row_gold", "lb_row_silver", "lb_row_bronze", "lb_row_green", "lb_row_grey", "lb_row_you", "lb_tab_on", "lb_tab_off", "lb_pill_timer", "lb_pill_score"
        };

        [Test]
        public void EveryKitSpriteExistsInResources()
        {
            foreach (string n in All) Assert.IsNotNull(ToyKit.Get(n), n + " is missing in Resources/Art/Ui");
        }

        [Test]
        public void StadiumPlatesSliceOnlyTheirRoundedCaps()
        {
            foreach (string n in SlicedLeftRight)
            {
                Sprite s = ToyKit.Get(n);
                Assert.IsNotNull(s, n);
                Assert.Greater(s.border.x, 50f, n + " left cap border");
                Assert.Greater(s.border.z, 50f, n + " right cap border");
                Assert.AreEqual(0f, s.border.y + s.border.w, 0.01f, n + ": nothing is sliced vertically, so the height scales uniformly with the caps");
                Assert.Less(s.border.x + s.border.z, s.rect.width * 0.7f, n + ": a flat middle remains");
            }
            Sprite body = ToyKit.Get("lb_frame_body");
            Assert.Greater(body.border.x, 50f); Assert.Greater(body.border.z, 50f); Assert.Greater(body.border.y, 50f);
            Assert.AreEqual(0f, body.border.w, 0.01f);
            Assert.AreEqual(ToyKit.Get("lb_frame_top").rect.width, body.rect.width, 0.5f, "the ribbon piece and the body are cut from one frame");
        }

        [Test]
        public void KitIconsAreNotSliced()
        {
            foreach (string n in new[] { "lb_icon_gift", "lb_icon_crown", "lb_medal_gold", "lb_badge_green", "lb_plate_round", "lb_frame_top" })
                Assert.AreEqual(0f, ToyKit.Get(n).border.sqrMagnitude, 0.01f, n + " is drawn at one uniform scale");
        }
    }
}
