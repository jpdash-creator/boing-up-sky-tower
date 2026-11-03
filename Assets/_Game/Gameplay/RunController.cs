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
        /// <summary>Test hook: when set, this hero's powers are used in the next run instead of the chosen character's (null = the chosen character).</summary>
        public static CharacterSpec HeroOverride;

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
            if (GameSession.Progress != null) GameSession.Progress.RecordLevelStarted(GameSettings.Difficulty);   // reward statistics: a hard level was played
            // Every attempt (first start, retry, next level, re-entering the Run scene) gets a new layout seed; a revive keeps the same session and layout.
            GameSession.AttemptSeed = GameSession.NewAttemptSeed();
            BeginRun(new RunSession(GameSession.Repo, GameSession.BuildRunCfg(), levelId, GameSession.Progress != null && GameSession.Progress.IsLevelCleared(levelId), GameSession.AttemptSeed));
            Debug.Log("Run layout: " + Session.LayoutInfo);
        }

        /// <summary>Endless mode (owner decision 2026-10-20): a new tower of the region with a fresh seed, stored in GameSession.EndlessSeed.</summary>
        public void StartEndlessRun(string regionId)
        {
            GameSession.EndlessMode = true;
            GameSession.EndlessRegionId = regionId;
            GameSession.EndlessSeed = GameSession.NewEndlessSeed();
            GameSession.ApplyDifficulty();
            BeginRun(new RunSession(GameSession.Repo, GameSession.BuildRunCfg(), GameSession.Repo.GetRegion(regionId), GameSession.EndlessSeed));
        }

        void BeginRun(RunSession session)
        {
            ReportRunStats();   // combos and power-ups of the previous attempt (retry, next level)
            if (Session != null && GameSession.Progress != null) Session.SettlePowerUps(GameSession.Progress);   // units used in the previous attempt (retry, next level)
            if (View != null) Destroy(View.gameObject);
            if (Hud != null) Destroy(Hud.gameObject);

            Session = session;
            if (GameSession.Progress != null) Session.ApplyStock(GameSession.Progress.BuildRunStock());   // before the HUD is built: it makes one button per owned type
            Session.ApplyHeroPowers(HeroOverride ?? CharacterCatalog.Selected.Info);   // the chosen hero's free powers (none for Common heroes); before the HUD is built too
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
            Hud.OnReviveAd = ReviveByAd;
            AdService.Preload();
            Hud.OnPower = type => Input.LatchPower((PowerUp)type);
            Hud.OnHeroPower = type => Input.LatchHeroPower((PowerUp)type);
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

        float bundleTickAcc;   // run time toward the 45 minute bundle timer (ProgressService.TickPlayTime)

        void Tick()
        {
            if (Session == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);   // the game never changes timeScale; deltaTime also honours Time.captureFramerate in tests
            Input.Update();
            if (!Paused && !Session.Over && GameSession.Progress != null) GameSession.Progress.FeedPlayTime(dt, false);   // reward statistics: play time (no file write in a run)
            if (!Paused && !Session.Over && GameSession.Progress != null) { bundleTickAcc += dt; if (bundleTickAcc >= 1f) { GameSession.Progress.TickPlayTime(bundleTickAcc, true); bundleTickAcc = 0f; } }   // bundle timer: run time counts, no offer starts in a run, no file write

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
                EndlessReward er = GameSession.Progress.ApplyEndlessRun(Session.Region.Id, Session.State.Row, EndlessRewards.DayKey(System.DateTime.UtcNow), Session.State.Coins);
                ReportRunStats();
                GameSession.LastResult = new RunResult { Endless = true, RegionId = Session.Region.Id, Floors = Session.State.Row, Score = Session.State.Score, EndlessReward = er };
                Hud.ShowEndlessFailed(Session, er);
                return;
            }
            if (Session.GoalReached)
            {
                RewardResult reward = GameSession.Progress.ApplyRun(Session.Level.Id, Session.BuildOutcome());
                Session.MarkStatsReported();   // the outcome carried the combo and power-up statistics
                GameSession.Progress.FlushPlayTime();
                LastReward = reward;
                GameSession.LastResult = new RunResult { LevelId = Session.Level.Id, Completed = true, Reward = reward, Stars = reward.Stars, Score = Session.State.Score };
                Hud.ShowResults(Session, reward, GameSession.NextLevel(Session.Level) != null && GameSession.Progress.IsLevelUnlocked(GameSession.NextLevel(Session.Level)));
            }
            else
            {
                Haptics.Pulse();
                ReportRunStats();
                ShowFailedOffer();
            }
        }

        /// <summary>Reward statistics of a run that did not complete a level (or ended endless): new combo chains, best combo, power-ups used, pending play time. Safe to call repeatedly.</summary>
        void ReportRunStats()
        {
            if (Session == null || GameSession.Progress == null) return;
            GameSession.Progress.RecordRunStats(Session.BuildOutcome());
            Session.MarkStatsReported();
            GameSession.Progress.FlushPlayTime();
        }

        public void Pause()
        {
            if (Session == null || Session.Over || Paused) return;
            Paused = true;
            if (GameSession.Progress != null) Session.SettlePowerUps(GameSession.Progress);
            ReportRunStats();
            Hud.ShowPause();
        }

        public void Resume()
        {
            Paused = false;
            Hud.CloseOverlay();
            Input.Clear();
        }

        void ShowFailedOffer()
        {
            Hud.ShowFailed(Session, GameSession.Progress != null ? GameSession.Progress.Gems : 0, AdService.IsRewardedReady);
        }

        bool adBusy;

        void AfterRevive()
        {
            endHandled = false;
            Hud.CloseOverlay();
            Input.Clear();
        }

        /// <summary>Revive for gems (5, then 10). The wallet is charged inside TryRevive only after the revive happened.</summary>
        void Revive()
        {
            if (adBusy || Session == null) return;
            if (Session.TryRevive(GameSession.Progress)) AfterRevive();
        }

        /// <summary>
        /// Free revive through a rewarded ad (2 per run: 30 s, then 60 s). Nothing is spent. If the ad was watched but the revive did not go through,
        /// the next tap revives for free without another ad. An early close or an error grants nothing.
        /// </summary>
        void ReviveByAd()
        {
            if (adBusy || Session == null) return;
            if (Session.AdRewardPending) { if (Session.TryReviveFree()) AfterRevive(); return; }
            if (!AdService.IsRewardedReady) { ShowFailedOffer(); return; }   // the ad vanished: redraw with the gem option only
            adBusy = true;
            RunSession sess = Session;
            AdService.ShowRewarded(sess.Revive.NextAdPlacement, granted =>
            {
                adBusy = false;
                if (this == null || sess != Session) return;               // left the run while the ad was up
                if (granted)
                {
                    sess.MarkAdWatched();
                    if (sess.TryReviveFree()) AfterRevive();
                    else ShowFailedOffer();                                // reward kept: the offer now reads "Revive: Free"
                }
                else ShowFailedOffer();
            });
        }

        void PlayNext()
        {
            LevelDef next = GameSession.NextLevel(Session.Level);
            if (next == null) { ExitToMeta(); return; }
            StartRun(next.Id);
        }

        public void ExitToMeta()
        {
            ReportRunStats();
            if (Session != null && GameSession.Progress != null) Session.SettlePowerUps(GameSession.Progress);
            GameSession.PendingRegionId = Session != null ? Session.Region.Id : null;
            GameSession.EndlessMode = false;
            SceneManager.LoadScene("Meta");
        }
    }
}
