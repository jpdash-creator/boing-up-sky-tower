using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Simulation;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.SceneManagement;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// Static methods that the AltTester level sweep (Tools/android_test/alttester/level_sweep.py) calls on the phone with
    /// AltDriver.call_static_method("BoingUpSkyTower.Gameplay.LevelSweepApi", "MethodName", "BoingUpSkyTower.Gameplay", [args]).
    /// Every method returns one JSON string (AltTester wraps it once more; the Python side unwraps both).
    /// Development builds only: the methods that change anything refuse in a release build (Debug.isDebugBuild is false there; AltTester itself only exists in the alttester development build),
    /// and the Autopilot additionally needs SYSTEM_TEST (Autopilot.Allowed). Read-only except SetFlags, SelectCharacter,
    /// AttachAutopilot, Resume and SetSpeed. Overloads are avoided on purpose (AltTester picks a method by name and parameter count).
    /// </summary>
    [Preserve]
    public static class LevelSweepApi
    {
        // The layout fingerprint of the running session is computed once per session, not on every poll.
        static RunSession cachedSession;
        static string cachedHash = "";
        static string cachedKinds = "";
        static int cachedSpecial, cachedPlatforms, cachedGemsPlaced, cachedCoinsPlaced, cachedStarsPlaced, cachedHazards;

        static string Err(string msg) { return "{\"ok\":false,\"err\":\"" + AutopilotReport.Escape(msg) + "\"}"; }

        // ------------------------------------------------------------------ flags and setup

        [Preserve]
        public static string Ping()
        {
            return "{\"ok\":true,\"v\":\"" + AutopilotReport.Escape(Application.version) + "\",\"dev\":" + B(Debug.isDebugBuild) + ",\"scene\":\"" +
                   AutopilotReport.Escape(SceneManager.GetActiveScene().name) + "\"}";
        }

        [Preserve]
        public static string GetFlags()
        {
            return "{\"ok\":true,\"dev\":" + B(GameSettings.DevBuild) + ",\"system\":" + B(GameSettings.SystemTest) + ",\"jump\":" + B(GameSettings.GameJumpTest) +
                   ",\"unlockAll\":" + B(GameSettings.TestUnlockAll) + ",\"autopilot\":" + B(Autopilot.Allowed) + "}";
        }

        /// <summary>Sets SYSTEM_TEST and GAME_JUMP_TEST (PlayerPrefs, then ApplyFlags). The map screen must be re-entered to show the levels open.</summary>
        [Preserve]
        public static string SetFlags(bool systemTest, bool jumpTest)
        {
            if (!GameSettings.DevBuild) return Err("not a development build on a device");
            GameSettings.SystemTest = systemTest;
            GameSettings.GameJumpTest = jumpTest;
            return GetFlags();
        }

        /// <summary>Picks the character the run will use (needs SYSTEM_TEST so that every character is owned). Returns the effective selected id.</summary>
        [Preserve]
        public static string SelectCharacter(string id)
        {
            if (!GameSettings.DevBuild) return Err("not a development build on a device");
            CharacterCatalog.Selected = CharacterCatalog.Get(id);
            return "{\"ok\":true,\"selected\":\"" + CharacterCatalog.Selected.Id + "\"}";
        }

        [Preserve]
        public static string SetSpeed(float speed)
        {
            if (!Debug.isDebugBuild) return Err("not a development build");
            Time.timeScale = Mathf.Clamp(speed, 1f, 4f);
            return "{\"ok\":true,\"speed\":" + F(Time.timeScale) + "}";
        }

        /// <summary>Static facts about a level (base layout, attempt 0): goal text, rule line, target row, row count, platform and pickup counts.</summary>
        [Preserve]
        public static string LevelPreview(int region, int level)
        {
            if (!Debug.isDebugBuild) return Err("not a development build");
            if (GameSession.Repo == null) return Err("game not initialised");
            RegionDef rd = GameSession.Repo.TryGetRegionByIndex(region);
            if (rd == null || level < 1 || level > rd.Levels.Count) return Err("no level " + level + " in region " + region);
            LevelDef ld = GameSession.Repo.GetLevel(rd.Levels[level - 1]);
            LevelSpec spec = LevelSpecFactory.Build(ld, rd);
            LevelData data = LevelBuilder.Build(GameSession.BuildRunCfg(), spec);
            string kinds; int special, gems, coins, stars, hazards;
            Fingerprint(data, out kinds, out special, out gems, out coins, out stars, out hazards);
            string rule = RunSession.GoalRule(spec);
            StringBuilder sb = new StringBuilder(400);
            sb.Append("{\"ok\":true,\"id\":\"").Append(ld.Id).Append("\",\"n\":").Append(ld.Index).Append(",\"goalType\":\"").Append(spec.Goal.Type)
              .Append("\",\"goalText\":\"").Append(AutopilotReport.Escape(RunSession.DescribeGoal(spec))).Append("\",\"rule\":\"").Append(AutopilotReport.Escape(rule ?? ""))
              .Append("\",\"target\":").Append(spec.TargetRow()).Append(",\"rows\":").Append(data.Rows).Append(",\"platforms\":").Append(data.Platforms.Length)
              .Append(",\"special\":").Append(special).Append(",\"kinds\":\"").Append(kinds).Append("\",\"gemsPlaced\":").Append(gems)
              .Append(",\"coinsPlaced\":").Append(coins).Append(",\"starsPlaced\":").Append(stars).Append(",\"hazards\":").Append(hazards).Append('}');
            return sb.ToString();
        }

        // ------------------------------------------------------------------ the run

        /// <summary>Attaches the in-game Autopilot to the running Run scene (the same call AutopilotSuite makes). Needs SYSTEM_TEST.</summary>
        [Preserve]
        public static string AttachAutopilot(float skill, int seed, int depth)
        {
            RunController rc = Object.FindFirstObjectByType<RunController>();
            if (rc == null || rc.Session == null) return Err("no run in progress");
            Autopilot ap = Autopilot.Attach(rc, skill, seed, depth);
            if (ap == null) return Err("autopilot not allowed (SYSTEM_TEST off or release build)");
            return "{\"ok\":true,\"attached\":true,\"skill\":" + F(skill) + "}";
        }

        /// <summary>Closes the pause overlay (an interruption such as a notification shade pauses the run; the sweep must not stall).</summary>
        [Preserve]
        public static string Resume()
        {
            RunController rc = Object.FindFirstObjectByType<RunController>();
            if (rc == null) return Err("no run");
            if (rc.Paused) rc.Resume();
            return "{\"ok\":true}";
        }

        /// <summary>Everything the sweep reads about the current run (or the scene it is in when there is none).</summary>
        [Preserve]
        public static string RunState()
        {
            string scene = SceneManager.GetActiveScene().name;
            RunController rc = Object.FindFirstObjectByType<RunController>();
            StringBuilder sb = new StringBuilder(900);
            sb.Append("{\"ok\":true,\"scene\":\"").Append(AutopilotReport.Escape(scene)).Append("\",\"selected\":\"").Append(CharacterCatalog.Selected.Id).Append('"');
            if (rc == null || rc.Session == null || rc.Hud == null || rc.View == null)
            {
                sb.Append(",\"run\":false}");
                return sb.ToString();
            }
            RunSession ses = rc.Session;
            SimState s = ses.State;
            if (!ReferenceEquals(cachedSession, ses))
            {
                cachedSession = ses;
                Fingerprint(ses.Data, out cachedKinds, out cachedSpecial, out cachedGemsPlaced, out cachedCoinsPlaced, out cachedStarsPlaced, out cachedHazards);
                cachedPlatforms = ses.Data.Platforms.Length;
                cachedHash = Hash(ses.Data);
            }
            int owned = 0;
            for (int i = (int)PowerUp.Flight; i <= (int)PowerUp.SlowTime; i++) if (ses.OwnedAtStart((PowerUp)i)) owned++;
            bool reached = ses.GoalReached;
            string fail = "";
            if (ses.Over && !reached) fail = ses.GoalLost ? "Wall touched!" : "You fell!";
            sb.Append(",\"run\":true,\"level\":\"").Append(ses.Level != null ? ses.Level.Id : "endless").Append("\",\"n\":").Append(ses.Level != null ? ses.Level.Index : 0)
              .Append(",\"goalType\":\"").Append(ses.Spec.Goal.Type).Append("\",\"goalText\":\"").Append(AutopilotReport.Escape(RunSession.DescribeGoal(ses.Spec)))
              .Append("\",\"rule\":\"").Append(AutopilotReport.Escape(RunSession.GoalRule(ses.Spec) ?? "")).Append("\",\"target\":").Append(ses.Spec.TargetRow())
              .Append(",\"over\":").Append(B(ses.Over)).Append(",\"reached\":").Append(B(reached)).Append(",\"lost\":").Append(B(ses.GoalLost)).Append(",\"dead\":").Append(B(s.Dead))
              .Append(",\"fail\":\"").Append(fail).Append("\",\"paused\":").Append(B(rc.Paused)).Append(",\"intro\":").Append(B(rc.Hud.IntroRemaining > 0f))
              .Append(",\"overlay\":").Append(B(rc.Hud.OverlayOpen)).Append(",\"row\":").Append(s.Row).Append(",\"tick\":").Append(s.Tick).Append(",\"sec\":").Append(F(s.Tick / (float)SimConfig.TicksPerSecond))
              .Append(",\"coins\":").Append(s.Coins).Append(",\"gems\":").Append(s.Gems).Append(",\"hidden\":").Append(s.HiddenGems).Append(",\"starPickups\":").Append(s.Stars)
              .Append(",\"score\":").Append(s.Score).Append(",\"combo\":").Append(s.BestCombo).Append(",\"revives\":").Append(s.RevivesUsed).Append(",\"hits\":").Append(s.Hits)
              .Append(",\"ground\":\"").Append(AutopilotReport.KindCode(ses.Sim.Platforms[s.GroundId].Kind)).Append("\",\"x\":").Append(F(s.X.F())).Append(",\"y\":").Append(F(s.Y.F()))
              .Append(",\"layout\":\"").Append(AutopilotReport.Escape(ses.LayoutInfo)).Append("\",\"attemptSeed\":").Append(ses.AttemptSeed).Append(",\"layoutSeed\":").Append(ses.Spec.Seed)
              .Append(",\"layoutHash\":\"").Append(cachedHash).Append("\",\"rows\":").Append(ses.Data.Rows).Append(",\"platforms\":").Append(cachedPlatforms)
              .Append(",\"special\":").Append(cachedSpecial).Append(",\"kinds\":\"").Append(cachedKinds).Append("\",\"gemsPlaced\":").Append(cachedGemsPlaced)
              .Append(",\"coinsPlaced\":").Append(cachedCoinsPlaced).Append(",\"starsPlaced\":").Append(cachedStarsPlaced).Append(",\"hazards\":").Append(cachedHazards)
              .Append(",\"character\":\"").Append(rc.View.Character != null ? rc.View.Character.Id : "").Append("\",\"powerButtons\":").Append(rc.Hud.Powers != null ? rc.Hud.Powers.ButtonCount : 0)
              .Append(",\"powerOwned\":").Append(owned);
            if (ses.Over)
            {
                int mask = reached ? ses.BuildOutcome().StarMask : 0;
                int stars = (mask & 1) + ((mask >> 1) & 1) + ((mask >> 2) & 1);
                sb.Append(",\"stars\":").Append(stars);
                if (reached && !ses.IsEndless)
                {
                    RewardResult r = rc.LastReward;
                    sb.Append(",\"rewardCoins\":").Append(r.TotalCoins).Append(",\"rewardGems\":").Append(r.TotalGems).Append(",\"firstClear\":").Append(B(r.FirstClear));
                }
            }
            sb.Append('}');
            return sb.ToString();
        }

        // ------------------------------------------------------------------ helpers

        static string B(bool v) { return v ? "true" : "false"; }
        static string F(float v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }

        /// <summary>Platform kind histogram as "N:90,M:12" plus counts of special (non-Normal) platforms, gems, coins, stars and hazards.</summary>
        static void Fingerprint(LevelData data, out string kinds, out int special, out int gems, out int coins, out int stars, out int hazards)
        {
            int[] counts = new int[32];
            special = 0;
            for (int i = 0; i < data.Platforms.Length; i++)
            {
                int k = (int)data.Platforms[i].Kind;
                if (k < 0 || k >= counts.Length) k = counts.Length - 1;
                counts[k]++;
                if (data.Platforms[i].Kind != PlatformKind.Normal) special++;
            }
            StringBuilder sb = new StringBuilder(64);
            for (int k = 0; k < counts.Length; k++)
            {
                if (counts[k] == 0) continue;
                if (sb.Length > 0) sb.Append(',');
                sb.Append(AutopilotReport.KindCode((PlatformKind)k)).Append(':').Append(counts[k]);
            }
            kinds = sb.ToString();
            gems = 0; coins = 0; stars = 0;
            for (int i = 0; i < data.Pickups.Length; i++)
            {
                switch (data.Pickups[i].Kind)
                {
                    case PickupKind.Gem: gems++; break;
                    case PickupKind.Coin: coins++; break;
                    case PickupKind.Star: stars++; break;
                }
            }
            hazards = data.Hazards.Length;
        }

        /// <summary>FNV-1a over every platform (row, kind, x, y, width): two attempts with a different random layout give different hashes.</summary>
        static string Hash(LevelData data)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < data.Platforms.Length; i++)
                {
                    Platform p = data.Platforms[i];
                    h = (h ^ (uint)p.Row) * 16777619u;
                    h = (h ^ (uint)p.Kind) * 16777619u;
                    h = (h ^ (uint)p.X.Raw) * 16777619u;
                    h = (h ^ (uint)p.Y.Raw) * 16777619u;
                    h = (h ^ (uint)p.Width.Raw) * 16777619u;
                }
                return h.ToString("x8", CultureInfo.InvariantCulture);
            }
        }
    }
}
