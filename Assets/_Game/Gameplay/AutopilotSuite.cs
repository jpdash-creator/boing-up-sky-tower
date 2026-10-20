using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// Development-only runner that plays a list of levels in sequence with the Autopilot, like a real player, without any UI tap:
    /// it loads the Run scene for each level, waits for the run to end (cleared, fell or timeout), records the result and moves on.
    /// A fail is a result, nothing is restarted. Results go to logcat ("BTAUTO {json}") and to persistentDataPath/autoplay_results.csv.
    /// The player's save is never touched: for the whole suite the progress lives in a MemorySaveStore, and the real save is reloaded at the end.
    /// Started by AutopilotBootstrap when persistentDataPath/autoplay.json exists (at launch or later, while the Meta scene is up).
    /// </summary>
    public sealed class AutopilotSuite : MonoBehaviour
    {
        enum Phase { Idle, Loading, Running, Hold, Cleanup, Finished }

        sealed class Job
        {
            public string LevelId;
            public int LevelNo;
            public int Attempt;
        }

        const float LoadTimeoutSeconds = 30f;
        const int StallSeconds = 90;

        AutoplayOptions opt;
        string flagPath;
        string runId = "";
        readonly List<Job> jobs = new List<Job>();
        readonly List<AutoResult> results = new List<AutoResult>();
        int jobIndex = -1;
        Job job;
        Phase phase = Phase.Idle;
        RunController rc;
        Autopilot ap;
        int loadFrame;
        float loadStarted;
        float holdUntil;
        float runStartedReal;
        int bestRow;
        int bestRowTick;
        bool lastCleared;
        int savedDifficulty;
        int savedSleep;
        bool isolated;

        public bool IsFinished { get { return phase == Phase.Finished; } }
        public IReadOnlyList<AutoResult> Results { get { return results; } }

        /// <summary>Starts the suite. <paramref name="flagFile"/> is deleted when the suite is done.</summary>
        public void Begin(AutoplayOptions options, string flagFile)
        {
            if (!Autopilot.Allowed) { phase = Phase.Finished; Destroy(this); return; }
            opt = options;
            flagPath = flagFile;
            runId = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);
            GameSession.EnsureInit();

            RegionDef region = GameSession.Repo.TryGetRegionByIndex(opt.region);
            if (region == null)
            {
                Error("no such region " + opt.region);
                End();
                return;
            }
            for (int i = 0; i < opt.levels.Length; i++)
            {
                int no = opt.levels[i];
                if (no < 1 || no > region.Levels.Count) { Error("no level " + no + " in region " + opt.region); continue; }
                for (int a = 1; a <= opt.seeds; a++)
                    jobs.Add(new Job { LevelId = region.Levels[no - 1], LevelNo = no, Attempt = a });
            }

            // Isolation from the real save: progress and rewards of the autoplay live in memory only.
            Autopilot.SuiteActive = true;
            isolated = true;
            savedDifficulty = GameSettings.Difficulty;
            GameSettings.Difficulty = opt.difficulty;
            GameSession.InitForTests(new MemorySaveStore());
            ProgressService.TestUnlockAll = true;
            Time.timeScale = opt.speed;
            savedSleep = Screen.sleepTimeout;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            AutopilotReport.Emit("{\"ev\":\"start\",\"run\":\"" + runId + "\",\"v\":\"" + AutopilotReport.Escape(Application.version) + "\",\"region\":" + opt.region +
                                 ",\"jobs\":" + jobs.Count + ",\"skill\":" + opt.skill.ToString("0.##", CultureInfo.InvariantCulture) + ",\"diff\":" + opt.difficulty +
                                 ",\"step\":" + GameSettings.DifficultyStepPercent[opt.difficulty] + ",\"speed\":" + opt.speed.ToString("0.##", CultureInfo.InvariantCulture) + "}");
            StartNext();
        }

        void StartNext()
        {
            jobIndex++;
            if (jobIndex >= jobs.Count) { End(); return; }
            job = jobs[jobIndex];
            rc = null;
            ap = null;
            GameSession.CurrentLevelId = job.LevelId;
            GameSession.EndlessMode = false;
            phase = Phase.Loading;
            loadFrame = Time.frameCount;
            loadStarted = Time.unscaledTime;
            SceneManager.LoadScene("Run");
        }

        void Update()
        {
            switch (phase)
            {
                case Phase.Loading: TryAttach(); break;
                case Phase.Running: Drive(); break;
                case Phase.Hold:
                    if (Time.unscaledTime >= holdUntil)
                    {
                        if (opt.stopOnFail && !lastCleared) End(); else StartNext();
                    }
                    break;
                case Phase.Cleanup: DoCleanup(); break;
            }
        }

        void TryAttach()
        {
            if (Time.frameCount <= loadFrame) return;   // the scene switch completes at the end of the frame
            if (SceneManager.GetActiveScene().name == "Run")
            {
                RunController found = FindFirstObjectByType<RunController>();
                if (found != null && found.Session != null && found.Input != null)
                {
                    rc = found;
                    ap = Autopilot.Attach(rc, opt.skill, opt.seedBase + job.LevelNo * 131 + job.Attempt * 7919, opt.depth);
                    if (ap == null) { Error("autopilot not allowed"); End(); return; }
                    phase = Phase.Running;
                    runStartedReal = Time.unscaledTime;
                    bestRow = 0;
                    bestRowTick = 0;
                    return;
                }
            }
            if (Time.unscaledTime - loadStarted > LoadTimeoutSeconds) { Error("Run scene did not start for " + job.LevelId); End(); }
        }

        void Drive()
        {
            if (rc == null || rc.Session == null) { Error("Run scene lost during " + job.LevelId); End(); return; }
            RunSession ses = rc.Session;
            if (rc.Paused) rc.Resume();   // an interruption (notification shade) must not stall the suite
            if (ses.Over) { Finish(ses.GoalReached ? AutoResult.Cleared : AutoResult.Fell); return; }
            int row = ses.State.Row;
            int tick = ses.State.Tick;
            if (row > bestRow) { bestRow = row; bestRowTick = tick; }
            if (tick > opt.maxSeconds * 60 || tick - bestRowTick > StallSeconds * 60 || Time.unscaledTime - runStartedReal > opt.maxSeconds * 3f)
                Finish(AutoResult.Timeout);
        }

        void Finish(string result)
        {
            AutoResult r = AutopilotReport.Capture(rc.Session, runId, Application.version, job.Attempt, opt.skill, opt.difficulty, result, ap != null ? ap.Brain : null);
            results.Add(r);
            AutopilotReport.Emit(AutopilotReport.ToJson(r));
            AutopilotReport.AppendCsv(AutopilotReport.CsvPath, r);
            lastCleared = result == AutoResult.Cleared;
            holdUntil = Time.unscaledTime + (lastCleared ? opt.holdClear : opt.holdFail);   // the death stays on screen for the host screenshot
            phase = Phase.Hold;
        }

        void End()
        {
            int cleared = 0, fell = 0, timeout = 0;
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].Result == AutoResult.Cleared) cleared++;
                else if (results[i].Result == AutoResult.Fell) fell++;
                else timeout++;
            }
            AutopilotReport.Emit("{\"ev\":\"summary\",\"run\":\"" + runId + "\",\"v\":\"" + AutopilotReport.Escape(Application.version) + "\",\"total\":" + results.Count +
                                 ",\"cleared\":" + cleared + ",\"fell\":" + fell + ",\"timeout\":" + timeout + "}");
            try { if (!string.IsNullOrEmpty(flagPath) && File.Exists(flagPath)) File.Delete(flagPath); }
            catch (Exception e) { Debug.LogWarning("autoplay flag: " + e.Message); }
            Debug.Log("BTAUTO done");
            phase = Phase.Cleanup;
            loadFrame = Time.frameCount;
            SceneManager.LoadScene("Meta");
        }

        // Runs one frame after the Meta scene was requested, when the Run scene (which still points at the in-memory save) is gone.
        void DoCleanup()
        {
            if (Time.frameCount <= loadFrame) return;
            if (isolated)
            {
                Time.timeScale = 1f;
                Screen.sleepTimeout = savedSleep;
                GameSettings.Difficulty = savedDifficulty;
            }
            Autopilot.SuiteActive = false;
            GameSession.Reset();
            GameSession.EnsureInit();   // reloads the real save file, which the suite never wrote
            phase = Phase.Finished;
            Destroy(this);   // the bootstrap on the same object keeps polling for the next autoplay.json
        }

        static void Error(string message)
        {
            AutopilotReport.Emit("{\"ev\":\"error\",\"msg\":\"" + AutopilotReport.Escape(message) + "\"}");
        }
    }
}
