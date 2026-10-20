using System.Collections.Generic;
using SkyHopper.Content;
using SkyHopper.Economy;
using SkyHopper.Gameplay;
using SkyHopper.Simulation;
using NUnit.Framework;

namespace SkyHopper.Tests
{
    public class TouchControlTests
    {
        static TouchSample S(int id, TouchPhaseKind phase, float x, float t) { return new TouchSample { Id = id, Phase = phase, X = x, Time = t }; }

        [Test]
        public void HoldingTheLeftHalfMovesLeftAndTheRightHalfMovesRight()
        {
            var logic = new TouchControlLogic();
            logic.Update(new List<TouchSample> { S(1, TouchPhaseKind.Began, 100f, 0f) }, 1000f);
            Assert.AreEqual(SimInput.Left, logic.Move);
            logic.Update(new List<TouchSample> { S(1, TouchPhaseKind.Ended, 100f, 1f) }, 1000f);
            Assert.AreEqual(0, logic.Move);
            logic.Update(new List<TouchSample> { S(2, TouchPhaseKind.Began, 900f, 2f) }, 1000f);
            Assert.AreEqual(SimInput.Right, logic.Move);
        }

        [Test]
        public void ASecondFingerJumpsWhileTheFirstKeepsMoving()
        {
            var logic = new TouchControlLogic();
            Assert.IsFalse(logic.Update(new List<TouchSample> { S(1, TouchPhaseKind.Began, 900f, 0f) }, 1000f));
            bool jump = logic.Update(new List<TouchSample> { S(1, TouchPhaseKind.Held, 900f, 0.1f), S(2, TouchPhaseKind.Began, 100f, 0.1f) }, 1000f);
            Assert.IsTrue(jump);
            Assert.AreEqual(SimInput.Right, logic.Move, "the oldest finger still decides the direction");
        }

        [Test]
        public void AQuickSingleTapJumpsButALongHoldDoesNot()
        {
            var logic = new TouchControlLogic();
            logic.Update(new List<TouchSample> { S(1, TouchPhaseKind.Began, 500f, 0f) }, 1000f);
            Assert.IsTrue(logic.Update(new List<TouchSample> { S(1, TouchPhaseKind.Ended, 500f, 0.12f) }, 1000f));

            logic.Update(new List<TouchSample> { S(2, TouchPhaseKind.Began, 500f, 1f) }, 1000f);
            Assert.IsFalse(logic.Update(new List<TouchSample> { S(2, TouchPhaseKind.Ended, 500f, 1.6f) }, 1000f));
        }

        [Test]
        public void ReleasingTheSecondFingerKeepsTheFirstMoving()
        {
            var logic = new TouchControlLogic();
            logic.Update(new List<TouchSample> { S(1, TouchPhaseKind.Began, 100f, 0f) }, 1000f);
            logic.Update(new List<TouchSample> { S(2, TouchPhaseKind.Began, 900f, 0.2f) }, 1000f);
            Assert.IsFalse(logic.Update(new List<TouchSample> { S(2, TouchPhaseKind.Ended, 900f, 0.3f) }, 1000f));
            Assert.AreEqual(SimInput.Left, logic.Move);
        }

        [Test]
        public void ReaderLatchesAJumpUntilTheNextTick()
        {
            var reader = new RunInputReader();
            bool press = true;
            reader.ScriptedMove = () => SimInput.Right;
            reader.ScriptedJump = () => { bool p = press; press = false; return p; };
            reader.Update();
            int first = reader.ReadTick();
            Assert.AreEqual(SimInput.Right | SimInput.Jump, first);
            reader.Update();
            Assert.AreEqual(SimInput.Right, reader.ReadTick(), "the jump edge is consumed once");
        }
    }

    public class RunSessionTests
    {
        ContentRepository repo;

        [SetUp]
        public void SetUp() { repo = ContentRepository.LoadFromResources(); }

        [Test]
        public void SessionStartsGroundedAndStepsDeterministically()
        {
            var a = new RunSession(repo, new SimConfig(), "lvl_001_01");
            var b = new RunSession(repo, new SimConfig(), "lvl_001_01");
            Assert.IsTrue(a.State.Grounded);
            for (int i = 0; i < 240; i++)
            {
                int input = (i % 50 == 0 ? SimInput.Jump : 0) | (i % 120 < 60 ? SimInput.Right : SimInput.Left);
                a.Step(input);
                b.Step(input);
            }
            Assert.AreEqual(a.State.X.Raw, b.State.X.Raw);
            Assert.AreEqual(a.State.Y.Raw, b.State.Y.Raw);
            Assert.AreEqual(a.State.Score, b.State.Score);
        }

        [Test]
        public void RunEndsWhenTheGoalIsMet()
        {
            var s = new RunSession(repo, new SimConfig(), "lvl_001_01");
            Assert.IsFalse(s.Over);
            s.State.Row = s.Spec.TargetRow();   // fabricate a finished climb
            s.Step(0);
            Assert.IsTrue(s.GoalReached);
            Assert.IsTrue(s.Over);
            Assert.IsFalse(s.Failed);
            RunOutcome o = s.BuildOutcome();
            Assert.IsTrue(o.Completed);
            Assert.AreEqual(0, o.StarMask, "no stars were collected in this fabricated climb");
        }

        [Test]
        public void FallingEndsTheRunAsFailedWithNoStars()
        {
            var s = new RunSession(repo, new SimConfig(), "lvl_001_01");
            s.State.Grounded = false;
            s.State.Y = Fx.FromInt(-200);
            s.Step(0);
            Assert.IsTrue(s.Failed);
            Assert.IsTrue(s.Over);
            Assert.AreEqual(0, s.BuildOutcome().StarMask);
            Assert.IsFalse(s.BuildOutcome().Completed);
        }

        [Test]
        public void ReviveCostsTwoThenFiveGemsAndNeedsThem()
        {
            var progress = new ProgressService(repo, new MemorySaveStore(), 5);
            var s = new RunSession(repo, new SimConfig(), "lvl_001_01");
            s.State.Grounded = false;
            s.State.Y = Fx.FromInt(-200);
            s.Step(0);
            Assert.IsFalse(s.TryRevive(progress), "no gems");
            progress.AddGems(10);
            Assert.AreEqual(2, s.NextReviveCost);
            Assert.IsTrue(s.TryRevive(progress));
            Assert.AreEqual(8, progress.Gems, "the first revive costs 2");
            Assert.IsFalse(s.State.Dead);
            Assert.AreEqual(1, s.State.RevivesUsed);
            s.State.Grounded = false;
            s.State.Y = Fx.FromInt(-200);
            s.State.Invuln = 0;
            s.Step(0);
            Assert.AreEqual(5, s.NextReviveCost);
            Assert.IsTrue(s.TryRevive(progress));
            Assert.AreEqual(3, progress.Gems, "the second revive costs 5");
            s.State.Grounded = false;
            s.State.Y = Fx.FromInt(-200);
            s.State.Invuln = 0;
            s.Step(0);
            Assert.IsFalse(s.CanRevive, "at most two revives per run");
            Assert.IsFalse(s.TryRevive(progress));
        }

        [Test]
        public void GoalAndStarTextIsReadable()
        {
            var s = new RunSession(repo, new SimConfig(), "lvl_001_01");
            Assert.AreEqual("Reach floor 60", RunSession.DescribeGoal(s.Spec));
            Assert.AreEqual("Clear the level", RunSession.DescribeStar(s.Spec.Stars[0]));
            Assert.AreEqual("Do not revive", RunSession.DescribeStar(s.Spec.Stars[2]));
        }

        [Test]
        public void EveryLevelOfRegions1To5BuildsASession()
        {
            var cfg = new SimConfig();
            for (int r = 1; r <= 5; r++)
                foreach (LevelDef l in repo.LevelsOf(repo.GetRegionByIndex(r).Id))
                {
                    var s = new RunSession(repo, cfg, l.Id);
                    Assert.Greater(s.Data.Platforms.Length, 20, l.Id);
                }
        }

        [Test]
        public void NextLevelFollowsTheRegionAndStopsAtTheFifthRegionEnd()
        {
            GameSession.InitForTests(new MemorySaveStore());
            LevelDef l1 = GameSession.Repo.GetLevel("lvl_001_01");
            Assert.AreEqual("lvl_001_02", GameSession.NextLevel(l1).Id);
            LevelDef last = GameSession.Repo.GetLevel("lvl_005_15");
            Assert.IsNull(GameSession.NextLevel(last), "region 6 is not playable in the five-region build");
            GameSession.Reset();
        }
    }
}
