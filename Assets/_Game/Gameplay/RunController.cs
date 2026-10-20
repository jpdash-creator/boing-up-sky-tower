using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The Run scene driver: a fixed 60 Hz simulation clock (accumulator), input, view, HUD and the end-of-run flow.
    /// Owner decision: a run ends at goal completion or when the player falls; failed runs pay nothing.
    /// </summary>
    public sealed class RunController : MonoBehaviour
    {
        const float StepSeconds = 1f / SimConfig.TicksPerSecond;
        const int MaxStepsPerFrame = 6;

        public RunSession Session { get; private set; }
        public RunInputReader Input { get; private set; }
        public RunHud Hud { get; private set; }
        public RunView View { get; private set; }
        public bool Paused { get; private set; }
        public RewardResult LastReward { get; private set; }

        float acc;
        bool endHandled;
        int lastPicked;
        float shotTimer;

        void Start()
        {
            GameSession.EnsureInit();
            Input = new RunInputReader();
            if (GameSession.EndlessMode && !string.IsNullOrEmpty(GameSession.EndlessRegionId))
            {
                StartEndlessRun(GameSession.EndlessRegionId);
                return;
            }
            string id = string.IsNullOrEmpty(GameSession.CurrentLevelId) ? "lvl_001_01" : GameSession.CurrentLevelId;
            StartRun(id);
        }

        public void StartRun(string levelId)
        {
            GameSession.EndlessMode = false;
            GameSession.CurrentLevelId = levelId;
            GameSession.ApplyDifficulty();
            BeginRun(new RunSession(GameSession.Repo, GameSession.Cfg, levelId));
        }

        /// <summary>Endless mode (owner decision 2026-10-20): a new tower of the region with a fresh seed, stored in GameSession.EndlessSeed.</summary>
        public void StartEndlessRun(string regionId)
        {
            GameSession.EndlessMode = true;
            GameSession.EndlessRegionId = regionId;
            GameSession.EndlessSeed = GameSession.NewEndlessSeed();
            GameSession.ApplyDifficulty();
            BeginRun(new RunSession(GameSession.Repo, GameSession.Cfg, GameSession.Repo.GetRegion(regionId), GameSession.EndlessSeed));
        }

        void BeginRun(RunSession session)
        {
            if (Session != null && GameSession.Progress != null) Session.SettlePowerUps(GameSession.Progress);   // units used in the previous attempt (retry, next level)
            if (View != null) Destroy(View.gameObject);
            if (Hud != null) Destroy(Hud.gameObject);

            Session = session;
            if (GameSession.Progress != null) Session.ApplyLoadout(GameSession.Progress.BuildRunLoadout());   // before the HUD is built: it makes one button per slot
            acc = 0f;
            endHandled = false;
            Paused = false;
            lastPicked = 0;
            Input.Clear();

            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
            }
            var viewGo = new GameObject("RunView");
            View = viewGo.AddComponent<RunView>();
            View.Build(Session, GameSession.Art, cam);

            var hudGo = new GameObject("RunHudRoot");
            Hud = hudGo.AddComponent<RunHud>();
            Hud.Build(Session, GameSession.Art);
            Input.Pad = Hud.Pad;
            Hud.OnPause = Pause;
            Hud.OnResume = Resume;
            Hud.OnRetry = () => { if (Session.IsEndless) StartEndlessRun(Session.Region.Id); else StartRun(GameSession.CurrentLevelId); };
            Hud.OnMenu = ExitToMeta;
            Hud.OnNext = PlayNext;
            Hud.OnRevive = Revive;
            Hud.OnPower = slot => Input.LatchPower(slot);
        }

        /// <summary>Script time per frame (milliseconds), for the CUX-1 budget tests and on-device profiling.</summary>
        public sealed class FrameStats
        {
            public int Frames;
            public double TotalMs;
            public double MaxMs;
            public double AverageMs { get { return Frames == 0 ? 0.0 : TotalMs / Frames; } }
            public void Reset() { Frames = 0; TotalMs = 0.0; MaxMs = 0.0; }
        }

        public readonly FrameStats Stats = new FrameStats();

        void Update()
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            Tick();
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Stats.Frames++;
            Stats.TotalMs += ms;
            if (ms > Stats.MaxMs) Stats.MaxMs = ms;
        }

        // A phone call, the home button or a notification shade must never cost the player a run (CUX-6).
        void OnApplicationPause(bool paused) { if (paused) Pause(); }
        void OnApplicationFocus(bool focus) { if (!focus) Pause(); }

        void Tick()
        {
            if (Session == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);   // the game never changes timeScale; deltaTime also honours Time.captureFramerate in tests
            Input.Update();

            bool introDone = Hud.IntroRemaining <= 0f;
            if (!Paused && !Session.Over && introDone)
            {
                acc += dt;
                int steps = 0;
                while (acc >= StepSeconds && steps < MaxStepsPerFrame && !Session.Over)
                {
                    int bits = Input.ReadTick();
                    Session.Step(bits);
                    int picked = Session.State.Stars + Session.State.Gems + Session.State.HiddenGems + Session.State.Coins;
                    bool got = picked > lastPicked;
                    lastPicked = picked;
                    View.OnTick(got, Session.State.X.F(), Session.State.Y.F() + 0.5f);
                    acc -= StepSeconds;
                    steps++;
                }
                if (steps == MaxStepsPerFrame) acc = 0f;   // never spiral after a long frame
            }
            View.Render(Mathf.Clamp01(acc / StepSeconds), dt);
            Hud.Refresh(Session, dt);

            if (Session.Over && !endHandled)
            {
                if (Session.GoalReached && !GameSettings.ReducedMotion)
                {
                    View.PlayWarp();                       // Momo flies into the galaxy portal first
                    if (View.WarpDone) HandleEnd();
                }
                else HandleEnd();
            }
        }

        void HandleEnd()
        {
            endHandled = true;
            if (GameSession.Progress != null) Session.SettlePowerUps(GameSession.Progress);
            if (Session.IsEndless)
            {
                // The run is over for good (no revive in endless): best floor and coins are saved right now (CUX-8).
                Haptics.Pulse();
                EndlessReward er = GameSession.Progress.ApplyEndlessRun(Session.Region.Id, Session.State.Row, EndlessRewards.DayKey(System.DateTime.UtcNow));
                GameSession.LastResult = new RunResult { Endless = true, RegionId = Session.Region.Id, Floors = Session.State.Row, Score = Session.State.Score, EndlessReward = er };
                Hud.ShowEndlessFailed(Session, er);
                return;
            }
            if (Session.GoalReached)
            {
                RewardResult reward = GameSession.Progress.ApplyRun(Session.Level.Id, Session.BuildOutcome());
                LastReward = reward;
                GameSession.LastResult = new RunResult { LevelId = Session.Level.Id, Completed = true, Reward = reward, Stars = reward.Stars, Score = Session.State.Score };
                Hud.ShowResults(Session, reward, GameSession.NextLevel(Session.Level) != null && GameSession.Progress.IsLevelUnlocked(GameSession.NextLevel(Session.Level)));
            }
            else
            {
                Haptics.Pulse();
                Hud.ShowFailed(Session, Session.CanRevive && GameSession.Progress.Gems >= Session.NextReviveCost);
            }
        }

        public void Pause()
        {
            if (Session == null || Session.Over || Paused) return;
            Paused = true;
            if (GameSession.Progress != null) Session.SettlePowerUps(GameSession.Progress);
            Hud.ShowPause();
        }

        public void Resume()
        {
            Paused = false;
            Hud.CloseOverlay();
            Input.Clear();
        }

        void Revive()
        {
            if (Session.TryRevive(GameSession.Progress))
            {
                endHandled = false;
                Hud.CloseOverlay();
                Input.Clear();
            }
        }

        void PlayNext()
        {
            LevelDef next = GameSession.NextLevel(Session.Level);
            if (next == null) { ExitToMeta(); return; }
            StartRun(next.Id);
        }

        public void ExitToMeta()
        {
            if (Session != null && GameSession.Progress != null) Session.SettlePowerUps(GameSession.Progress);
            GameSession.PendingRegionId = Session != null ? Session.Region.Id : null;
            GameSession.EndlessMode = false;
            SceneManager.LoadScene("Meta");
        }
    }
}
