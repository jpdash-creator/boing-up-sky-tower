using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace BoingUpSkyTower.Tests
{
    /// <summary>Audit gaps of the doc 46 timed goals: Rewards icons of the new goals and the Time Attack record line of the Endless popup.</summary>
    public class AuditGapTests
    {
        [TestCase("d_pace2")]
        [TestCase("d_floor100")]
        [TestCase("w_pace3")]
        [TestCase("w_gold5")]
        [TestCase("w_ta100")]
        [TestCase("g_1_gold_1")]
        [TestCase("g_3_gold_5")]
        public void TimedGoalsHaveTheirOwnIconAndNotTheStarFallback(string id)
        {
            string slot, icon, kit;
            RewardsScreen.TaskSources(id, out slot, out icon, out kit);
            bool fallback = icon == "star1" && kit == "icon_star" && slot == null;
            Assert.IsFalse(fallback, id + " must not fall through to the plain star");
            Assert.IsNotNull(kit, id + " keeps a UI kit fallback");
        }

        [Test]
        public void GoldMedalGoalsAreRecognisedAndTheOthersAreNot()
        {
            Assert.IsTrue(RewardsScreen.IsGoldMedalTask("w_gold5"));
            for (int r = 1; r <= 5; r++) for (int t = 1; t <= 5; t++) Assert.IsTrue(RewardsScreen.IsGoldMedalTask("g_" + r + "_gold_" + t), "g_" + r + "_gold_" + t);
            Assert.IsFalse(RewardsScreen.IsGoldMedalTask("g_1_stars_5"));
            Assert.IsFalse(RewardsScreen.IsGoldMedalTask("w_stars20"));
            Assert.IsFalse(RewardsScreen.IsGoldMedalTask(null));
            Assert.IsFalse(RewardsScreen.IsGoldMedalTask(""));
            Assert.IsNotNull(MedalIcons.For(MedalLevel.Gold, true), "the code-drawn gold medal exists");
        }

        [Test]
        public void TimeAttackLineShowsFloorsPaceAndTheEndTimeOfAnEarlyRun()
        {
            Assert.AreEqual("3:00, floors in three minutes", MetaController.TimeAttackLineText(null));
            Assert.AreEqual(1, MetaController.TimeAttackLineLines(null));
            Assert.AreEqual(1, MetaController.TimeAttackLineLines(new TimeAttackRecord()), "no floors: still the one-line hint");

            var full = new TimeAttackRecord { BestFloors = 108, Ticks = TimeRecords.TimeAttackTicks };
            string t = MetaController.TimeAttackLineText(full);
            StringAssert.StartsWith("3:00, best 108 floors\n", t);
            StringAssert.Contains(TimeRecords.FormatPace(108, TimeRecords.TimeAttackTicks), t, "108 floors in 180 s is 0.60 plat/s");
            StringAssert.DoesNotContain("fell", t, "a full run did not fall");
            Assert.AreEqual(2, MetaController.TimeAttackLineLines(full));

            var early = new TimeAttackRecord { BestFloors = 96, Ticks = 9100 };
            string e = MetaController.TimeAttackLineText(early);
            StringAssert.Contains(TimeRecords.FormatPace(96, 9100), e);
            StringAssert.Contains("fell at " + TimeRecords.FormatTime(9100), e);

            var old = new TimeAttackRecord { BestFloors = 50 };   // saved before the time was stored
            StringAssert.Contains(TimeRecords.FormatPace(50, TimeRecords.TimeAttackTicks), MetaController.TimeAttackLineText(old));
            Assert.Greater(MetaController.TimeAttackLineHeight(full), MetaController.TimeAttackLineHeight(null), "the popup reserves room for the second line");
        }
    }
}
