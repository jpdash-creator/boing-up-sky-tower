using System;
using System.Collections.Generic;
using System.Text;
using SkyHopper.Content;
using SkyHopper.Gameplay;
using SkyHopper.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace SkyHopper.Tests
{
    /// <summary>The Autopilot's decision code (planner bridge, brain, stack counter, CSV / JSON writers). No scene needed.</summary>
    public class AutopilotTests
    {
        ContentRepository repo;

        [SetUp]
        public void SetUp() { repo = ContentRepository.LoadFromResources(); }

        /// <summary>The configuration the game uses at the Relaxed difficulty (GameSession.ApplyDifficulty).</summary>
        static SimConfig GameCfg(int difficulty = 0)
        {
            var cfg = new SimConfig();
            cfg.ScrollStepPercent = GameSettings.DifficultyStepPercent[difficulty];
            cfg.GemInsteadOfCoinPercent = GameSettings.DifficultyGemPercent[difficulty];
            cfg.ScrollStepCapPercent = 400;
            return cfg;
        }

        static Platform P(int id, int row, double x, double y, PlatformKind kind = PlatformKind.Normal)
        {
            return new Platform { Id = id, Row = row, Kind = kind, X = Fx.FromRatio((int)(x * 100), 100), Y = Fx.FromRatio((int)(y * 100), 100), Width = Fx.FromInt(2) };
        }

        // ------------------------------------------------------------------ small pieces

        [Test]
        public void DelayLineReturnsTheValuePushedDelayStepsAgo()
        {
            var d = new DelayLine();
            d.Reset(3);
            int[] outs = new int[6];
            for (int i = 0; i < outs.Length; i++) outs[i] = d.Push(i + 1);
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 1, 2, 3 }, outs);

            d.Reset(0);
            Assert.AreEqual(7, d.Push(7));
        }

        [Test]
        public void DelayLineCopyContinuesTheSameSequence()
        {
            var a = new DelayLine();
            a.Reset(2);
            a.Push(5); a.Push(6);
            var b = new DelayLine();
            b.CopyFrom(a);
            Assert.AreEqual(a.Push(9), b.Push(9));
            Assert.AreEqual(a.Push(8), b.Push(8));
        }

        [Test]
        public void SkillZeroIsPerfectAndSkillOneIsAboutAHundredFiftyMilliseconds()
        {
            Assert.AreEqual(0, AutopilotBrain.DelayTicksFor(0f));
            Assert.AreEqual(0, AutopilotBrain.JitterTicksFor(0f));
            foreach (float skill in new[] { 0.01f, 0.25f, 0.5f, 0.75f, 1f })
            {
                int ms = AutopilotBrain.DelayTicksFor(skill) * 1000 / SimConfig.TicksPerSecond;
                Assert.That(ms, Is.InRange(80, 150), "lag at skill " + skill);
                Assert.That(AutopilotBrain.JitterTicksFor(skill), Is.InRange(1, 3));
            }
            Assert.AreEqual(9, AutopilotBrain.DelayTicksFor(5f), "skill is clamped to 1");
        }

        [Test]
        public void StackCounterCountsPlatformsInTheThreeUnitWindow()
        {
            var ps = new List<Platform>();
            for (int i = 0; i < 8; i++) ps.Add(P(i, i, 5.0 + 0.1 * i, 10.0 + 0.3 * i));   // eight platforms stacked over 2.1 u
            ps.Add(P(8, 9, 5.0, 14.5));                                                      // 4.5 u above the first: outside the window around the stack
            ps.Add(P(9, 9, 12.0, 11.0));                                                     // same height but 7 u to the side: outside
            var sim = new Sim(new SimConfig(), ps.ToArray(), Fx.Zero, false);

            var ids = new int[4];
            int stored;
            int count = StackCounter.Count(sim, 0, Fx.FromInt(5), Fx.FromInt(11), StackCounter.Window, ids, out stored);
            Assert.AreEqual(8, count);
            Assert.AreEqual(4, stored, "ids are capped at the buffer size, the count is not");
            Assert.IsTrue(StackCounter.IsDense(count));

            count = StackCounter.Count(sim, 0, Fx.FromInt(12), Fx.FromInt(11), StackCounter.Window, null, out stored);
            Assert.AreEqual(1, count);
            Assert.AreEqual(0, stored);
            Assert.IsFalse(StackCounter.IsDense(count));
        }

        [Test]
        public void StackCounterUsesTheMovingPlatformPositionAtThatTick()
        {
            var mover = P(0, 0, 5.0, 1.0, PlatformKind.Moving);
            mover.Amplitude = Fx.FromInt(4);
            mover.Period = 120;
            mover.Phase = 0;
            var sim = new Sim(new SimConfig(), new[] { mover }, Fx.Zero, false);
            int stored;
            Fx atStart = sim.PlatformX(0, 0);
            Fx atHalf = sim.PlatformX(0, 60);
            Assert.AreNotEqual(atStart.Raw, atHalf.Raw);
            Assert.AreEqual(1, StackCounter.Count(sim, 0, atStart, Fx.FromInt(1), StackCounter.Window, null, out stored));
            Assert.AreEqual(1, StackCounter.Count(sim, 60, atHalf, Fx.FromInt(1), StackCounter.Window, null, out stored));
            Assert.AreEqual(0, StackCounter.Count(sim, 60, atStart, Fx.FromInt(1), Fx.FromRatio(1, 2), null, out stored));
        }

        // ------------------------------------------------------------------ report writers

        static AutoResult SampleResult()
        {
            return new AutoResult
            {
                RunId = "20261020T101500", Version = "0.11.0.7", LevelId = "lvl_001_08", LevelNo = 8, Attempt = 1, Difficulty = 0, StepPercent = 20, Skill = 0.5f,
                Goal = "ReachFloor", Result = AutoResult.Fell, Floors = 17, TargetRow = 30, Seconds = 41.25f, Tick = 2475, DeathRow = 17, X = 3.456f, Y = 21.5f,
                GroundId = 33, GroundKind = "N", NearIds = new[] { 31, 32, 33, 34, 35, 36, 37, 38 }, NearKinds = "N,N,M,N,N,C,N,N", ScrollPercent = 340,
                KillGap = -0.25f, StackCount = 8, Dense = true, Hazards = 0, Hits = 0, Coins = 12, Plans = 22, Misses = 3, PlanMaxMs = 41
            };
        }

        /// <summary>Splits a CSV line at commas that are outside double quotes.</summary>
        static List<string> SplitCsv(string line)
        {
            var cells = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else quoted = !quoted;
                }
                else if (c == ',' && !quoted) { cells.Add(sb.ToString()); sb.Length = 0; }
                else sb.Append(c);
            }
            cells.Add(sb.ToString());
            return cells;
        }

        [Test]
        public void CsvRowHasOneCellPerHeaderColumn()
        {
            List<string> header = SplitCsv(AutopilotReport.CsvHeader());
            List<string> row = SplitCsv(AutopilotReport.ToCsvRow(SampleResult()));
            Assert.AreEqual(AutopilotReport.CsvColumns.Length, header.Count);
            Assert.AreEqual(header.Count, row.Count);
            Assert.AreEqual("lvl_001_08", row[header.IndexOf("level")]);
            Assert.AreEqual("fell", row[header.IndexOf("result")]);
            Assert.AreEqual("31|32|33|34|35|36|37|38", row[header.IndexOf("nearIds")]);
            Assert.AreEqual("N|N|M|N|N|C|N|N", row[header.IndexOf("nearKinds")]);
            Assert.AreEqual("1", row[header.IndexOf("dense")]);
            Assert.AreEqual("0.5", row[header.IndexOf("skill")], "numbers use the invariant culture");
            Assert.AreEqual("3.46", row[header.IndexOf("x")]);
        }

        [Test]
        public void CsvQuotesFieldsThatContainCommasOrQuotes()
        {
            AutoResult r = SampleResult();
            r.LevelId = "a,\"b\"";
            List<string> row = SplitCsv(AutopilotReport.ToCsvRow(r));
            Assert.AreEqual(AutopilotReport.CsvColumns.Length, row.Count);
            Assert.AreEqual("a,\"b\"", row[2]);
        }

        [Test]
        public void CsvAppendWritesTheHeaderOnceAndOneRowPerCall()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "autoplay_test_" + Guid.NewGuid().ToString("N") + ".csv");
            try
            {
                AutopilotReport.AppendCsv(path, SampleResult());
                AutopilotReport.AppendCsv(path, SampleResult());
                string[] lines = System.IO.File.ReadAllLines(path);
                Assert.AreEqual(3, lines.Length);
                Assert.AreEqual(AutopilotReport.CsvHeader(), lines[0]);
                Assert.AreEqual(lines[1], lines[2]);
            }
            finally
            {
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }

        [Test]
        public void LevelJsonIsOneShortLogcatLine()
        {
            var lines = new List<string>();
            AutopilotReport.ToLogLines(AutopilotReport.ToJson(SampleResult()), lines);
            Assert.AreEqual(1, lines.Count, "a normal record fits one line");
            Assert.That(lines[0], Does.StartWith("BTAUTO {"));
            Assert.Less(lines[0].Length, 600);
            StringAssert.Contains("\"lvl\":\"lvl_001_08\"", lines[0]);
            StringAssert.Contains("\"res\":\"fell\"", lines[0]);
            StringAssert.Contains("\"near\":[31,32,33,34,35,36,37,38]", lines[0]);
            StringAssert.Contains("\"dense\":true", lines[0]);
        }

        [Serializable]
        class ChunkLine { public int chunk; public int of; public int seq; public string d; }

        [Test]
        public void LongRecordsAreSplitIntoChunksUnderSixHundredCharsAndCanBeJoined()
        {
            AutoResult r = SampleResult();
            r.Goal = new string('g', 300) + "\"quoted\"" + new string('h', 400);
            string json = AutopilotReport.ToJson(r);
            Assert.Greater(json.Length, 600, "the test record must be long");

            var lines = new List<string>();
            AutopilotReport.ToLogLines(json, lines);
            Assert.Greater(lines.Count, 1);
            var joined = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                Assert.Less(lines[i].Length, 600, "line " + i);
                Assert.That(lines[i], Does.StartWith(AutopilotReport.Prefix));
                ChunkLine c = JsonUtility.FromJson<ChunkLine>(lines[i].Substring(AutopilotReport.Prefix.Length));
                Assert.AreEqual(i, c.chunk);
                Assert.AreEqual(lines.Count, c.of);
                joined.Append(c.d);
            }
            Assert.AreEqual(json, joined.ToString());
        }

        [Test]
        public void OptionsParseTheHostFileAndFillDefaults()
        {
            AutoplayOptions o = AutoplayOptions.Parse("{\"region\":1,\"levels\":[2,5,9],\"skill\":0.5,\"difficulty\":2,\"stopOnFail\":true,\"seeds\":3}");
            Assert.AreEqual(1, o.region);
            CollectionAssert.AreEqual(new[] { 2, 5, 9 }, o.levels);
            Assert.AreEqual(0.5f, o.skill, 1e-5f);
            Assert.AreEqual(2, o.difficulty);
            Assert.IsTrue(o.stopOnFail);
            Assert.AreEqual(3, o.seeds);

            AutoplayOptions d = AutoplayOptions.Parse("{}");
            Assert.AreEqual(15, d.levels.Length);
            Assert.AreEqual(1, d.levels[0]);
            Assert.AreEqual(15, d.levels[14]);
            Assert.AreEqual(1, d.seeds);
            Assert.AreEqual(1f, d.speed);

            AutoplayOptions bad = AutoplayOptions.Parse("{\"skill\":7,\"difficulty\":99,\"seeds\":0,\"speed\":50}");
            Assert.AreEqual(1f, bad.skill);
            Assert.AreEqual(GameSettings.DifficultyNames.Length - 1, bad.difficulty);
            Assert.AreEqual(1, bad.seeds);
            Assert.AreEqual(4f, bad.speed);
        }

        // ------------------------------------------------------------------ planner bridge and brain

        [Test]
        public void PlannedHopIsExecutedByTheRunnerAndLandsOnAHigherRow()
        {
            var ses = new RunSession(repo, GameCfg(), "lvl_001_01");
            var planner = new AutopilotPlanner();
            var delay = new DelayLine();
            delay.Reset(0);
            Maneuver m = planner.Plan(ses.Sim, ses.Spec, ses.Data, ses.State, delay);
            Assert.IsTrue(m.Valid);
            Assert.GreaterOrEqual(m.Target, 0, "a target platform was found");
            Assert.Greater(planner.LastSims, 0);

            var c = new ManeuverCursor();
            ManeuverRunner.Begin(ref c, m);
            for (int i = 0; i < 700 && !c.Done && !ses.Over; i++)
            {
                int bits = ManeuverRunner.Next(ses.Sim, ses.State, m, ref c);
                if (c.Done) break;
                ses.Step(bits);
            }
            Assert.IsTrue(c.Done, "the hop ended");
            Assert.IsFalse(ses.State.Dead, "and the player is alive");
            Assert.IsTrue(ses.State.Grounded);
            Assert.Greater(ses.Sim.Platforms[ses.State.GroundId].Row, 0, "on a higher row than the start");
        }

        [Test]
        public void PlanWithReactionLagIsExactWhenThereIsNoJitter()
        {
            // The planner simulates through the same delay line the live run uses, so lag alone does not break a plan.
            var ses = new RunSession(repo, GameCfg(), "lvl_001_01");
            var delay = new DelayLine();
            delay.Reset(7);
            var planner = new AutopilotPlanner();
            Maneuver m = planner.Plan(ses.Sim, ses.Spec, ses.Data, ses.State, delay);

            var live = new DelayLine();
            live.Reset(7);
            var c = new ManeuverCursor();
            ManeuverRunner.Begin(ref c, m);
            for (int i = 0; i < 700 && !c.Done && !ses.Over; i++)
            {
                int want = ManeuverRunner.Next(ses.Sim, ses.State, m, ref c);
                if (c.Done) break;
                ses.Step(live.Push(want));
            }
            Assert.IsTrue(c.Done);
            Assert.IsFalse(ses.State.Dead);
            Assert.IsTrue(ses.State.Grounded);
            Assert.Greater(ses.Sim.Platforms[ses.State.GroundId].Row, 0);
        }

        static int PlayHeadless(RunSession ses, AutopilotBrain brain, int maxTicks)
        {
            int ticks = 0;
            while (!ses.Over && ticks < maxTicks)
            {
                ses.Step(brain.Next(ses));
                ticks++;
            }
            return ticks;
        }

        [Test]
        public void PerfectAutopilotClearsRegionOneLevelOne()
        {
            var ses = new RunSession(repo, GameCfg(), "lvl_001_01");
            var brain = new AutopilotBrain(0f, 1, 2);
            PlayHeadless(ses, brain, 60 * 240);
            Assert.IsTrue(ses.GoalReached, "level 1 at skill 0: row " + ses.State.Row + " of " + ses.Spec.TargetRow() + ", tick " + ses.State.Tick + ", dead " + ses.State.Dead);
            Assert.Greater(brain.Plans, 0);
        }

        [Test]
        public void SameSeedGivesTheSameRun()
        {
            var a = new RunSession(repo, GameCfg(), "lvl_001_01");
            var b = new RunSession(repo, GameCfg(), "lvl_001_01");
            PlayHeadless(a, new AutopilotBrain(0.5f, 42, 1), 60 * 60);
            PlayHeadless(b, new AutopilotBrain(0.5f, 42, 1), 60 * 60);
            Assert.AreEqual(a.State.Tick, b.State.Tick);
            Assert.AreEqual(a.State.X.Raw, b.State.X.Raw);
            Assert.AreEqual(a.State.Row, b.State.Row);
        }

        [Test]
        public void HumanLikeAutopilotClimbsAndNeverStallsTheTest()
        {
            var ses = new RunSession(repo, GameCfg(), "lvl_001_01");
            var brain = new AutopilotBrain(1f, 7, 2);
            PlayHeadless(ses, brain, 60 * 120);
            Assert.GreaterOrEqual(ses.State.Row, 3, "even a laggy player gets off the ground floor");
        }

        [Test]
        public void CaptureDescribesTheStateAtTheEnd()
        {
            var ses = new RunSession(repo, GameCfg(), "lvl_001_01");
            var brain = new AutopilotBrain(0f, 1, 1);
            PlayHeadless(ses, brain, 600);
            AutoResult r = AutopilotReport.Capture(ses, "run1", "0.0.0.1", 1, 0f, 0, ses.Over ? AutoResult.Fell : AutoResult.Timeout, brain);
            Assert.AreEqual("lvl_001_01", r.LevelId);
            Assert.AreEqual(1, r.LevelNo);
            Assert.AreEqual(ses.State.Tick, r.Tick);
            Assert.AreEqual(ses.State.Row, r.Floors);
            Assert.AreEqual(GameSettings.DifficultyStepPercent[0], r.StepPercent);
            Assert.LessOrEqual(r.NearIds.Length, r.StackCount);
            Assert.AreEqual(r.NearIds.Length == 0 ? 0 : r.NearKinds.Split(',').Length, r.NearIds.Length);
            Assert.GreaterOrEqual(r.ScrollPercent, 0);
            StringAssert.Contains("\"lvl\":\"lvl_001_01\"", AutopilotReport.ToJson(r));
        }

        /// <summary>
        /// Plays all 15 levels of region 1 headless at skill 0 and prints one line per level. Not asserted (a failing level is a finding for the level
        /// generator, not for the autopilot); run it from the Test Runner when checking the stacked-platform fix.
        /// </summary>
        [Test, Explicit("Plays 15 levels; prints the results to the console")]
        public void Region1AllLevelsReport()
        {
            RegionDef region = repo.GetRegionByIndex(1);
            var sb = new StringBuilder();
            foreach (string id in region.Levels)
            {
                var ses = new RunSession(repo, GameCfg(), id);
                var brain = new AutopilotBrain(0f, 1, 2);
                PlayHeadless(ses, brain, 60 * 300);
                AutoResult r = AutopilotReport.Capture(ses, "edit", "editor", 1, 0f, 0, ses.GoalReached ? AutoResult.Cleared : (ses.Over ? AutoResult.Fell : AutoResult.Timeout), brain);
                sb.AppendLine(AutopilotReport.ToJson(r));
            }
            Debug.Log(sb.ToString());
            Assert.Pass();
        }
    }
}
