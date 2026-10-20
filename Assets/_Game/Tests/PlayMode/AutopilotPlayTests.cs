using System.Collections;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BoingUpSkyTower.Tests
{
    /// <summary>The development-only Autopilot driving the real Run scene through the normal input path.</summary>
    public class AutopilotPlayTests
    {
        [SetUp]
        public void SetUp()
        {
            Time.captureFramerate = 60;   // one rendered frame = exactly one simulation tick, so the frame counts are game time
            GameSession.Reset();
            GameSession.InitForTests(new MemorySaveStore());
            Autopilot.ForceEnable = false;
        }

        [TearDown]
        public void TearDown()
        {
            Autopilot.ForceEnable = false;
            Time.captureFramerate = 0;
            foreach (var rc in Object.FindObjectsByType<RunController>(FindObjectsSortMode.None)) rc.enabled = false;
            GameSession.Reset();
        }

        [Test]
        public void AutopilotIsOffInTheEditorUntilATestForcesItOn()
        {
            Assert.IsFalse(Autopilot.Allowed, "an editor session is not a development build on a device");
            Autopilot.ForceEnable = true;
            Assert.IsTrue(Autopilot.Allowed);
        }

        [UnityTest]
        public IEnumerator AutopilotClearsRegionOneLevelOneAtSkillZero()
        {
            Autopilot.ForceEnable = true;
            GameSession.CurrentLevelId = "lvl_001_01";
            var op = SceneManager.LoadSceneAsync("Run");
            while (!op.isDone) yield return null;
            yield return null;

            var rc = Object.FindFirstObjectByType<RunController>();
            Assert.IsNotNull(rc);
            rc.Hud.SkipIntro();
            Autopilot ap = Autopilot.Attach(rc, 0f, 1, 2);
            Assert.IsNotNull(ap, "the autopilot attached");
            Assert.AreSame(ap, rc.Input.Source, "the input comes from the autopilot, not the touch pad");

            const int maxFrames = 60 * 60;   // at most 60 s of game time
            int frames = 0;
            while (!rc.Session.Over && frames < maxFrames)
            {
                frames++;
                yield return null;
            }
            Assert.IsTrue(rc.Session.GoalReached,
                "level 1 at skill 0: row " + rc.Session.State.Row + " of " + rc.Session.Spec.TargetRow() + " after " + frames + " ticks, dead " + rc.Session.State.Dead);
        }

        [UnityTest]
        public IEnumerator WithoutTheForceFlagTheAutopilotRefusesToAttach()
        {
            GameSession.CurrentLevelId = "lvl_001_01";
            var op = SceneManager.LoadSceneAsync("Run");
            while (!op.isDone) yield return null;
            yield return null;
            var rc = Object.FindFirstObjectByType<RunController>();
            Assert.IsNull(Autopilot.Attach(rc, 0f, 1, 2));
            Assert.IsNull(rc.Input.Source);
        }
    }
}
