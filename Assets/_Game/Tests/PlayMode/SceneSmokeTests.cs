using System.Collections;
using System.IO;
using SkyHopper.Content;
using SkyHopper.Economy;
using SkyHopper.Gameplay;
using SkyHopper.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SkyHopper.Tests
{
    /// <summary>
    /// Plays the real scenes. Screenshots go to the folder in the environment variable SKYHOPPER_SHOTS (default E:/Unity/shots)
    /// so a developer (or Claude) can look at the result. Run with graphics enabled (no -nographics) to get real pixels.
    /// </summary>
    public class SceneSmokeTests
    {
        static string ShotDir
        {
            get
            {
                string d = System.Environment.GetEnvironmentVariable("SKYHOPPER_SHOTS");
                return string.IsNullOrEmpty(d) ? "E:/Unity/shots" : d;
            }
        }

        // Renders the main camera (and the UI, temporarily switched to camera space) into a texture and saves it as a PNG.
        // This does not depend on a window or on WaitForEndOfFrame, so it also works in batch mode.
        static IEnumerator Shot(string name, int w = 540, int h = 960)
        {
            Directory.CreateDirectory(ShotDir);
            Camera cam = Camera.main;
            if (cam == null) yield break;
            // The batch-mode Game view is 640x480, so the aspect is forced to the capture size.
            RunView.AspectOverride = (float)w / h;
            cam.aspect = (float)w / h;
            yield return null;
            cam.aspect = (float)w / h;
            var changed = new System.Collections.Generic.List<Canvas>();
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f;
                changed.Add(c);
            }
            Canvas.ForceUpdateCanvases();
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(ShotDir, name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            cam.ResetAspect();
            RunView.AspectOverride = 0f;
            RenderTexture.active = previous;
            Object.Destroy(tex);
            rt.Release();
            foreach (Canvas c in changed) { c.renderMode = RenderMode.ScreenSpaceOverlay; }
            yield return null;
        }

        static IEnumerator Load(string scene)
        {
            var op = SceneManager.LoadSceneAsync(scene);
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;
        }

        [SetUp]
        public void SetUp()
        {
            Time.captureFramerate = 60;   // every frame advances exactly 1/60 s, so frame counts are deterministic in batch mode
            GameSession.Reset();
            GameSession.InitForTests(new MemorySaveStore());
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            foreach (var rc in Object.FindObjectsByType<RunController>(FindObjectsSortMode.None)) rc.enabled = false;
            foreach (var mc in Object.FindObjectsByType<MetaController>(FindObjectsSortMode.None)) mc.enabled = false;
            GameSession.Reset();
        }

        [UnityTest]
        public IEnumerator BootSceneLoadsTheMetaScene()
        {
            yield return Load("Boot");
            yield return Shot("00_boot");
            float t = 0f;
            while (SceneManager.GetActiveScene().name != "Meta" && t < 6f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual("Meta", SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator MetaScreensRenderAndNavigate()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            Assert.IsNotNull(meta);
            Assert.AreEqual("Menu", meta.CurrentScreen);
            yield return Shot("10_menu");

            meta.ShowMap();
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual("Map", meta.CurrentScreen);
            for (int w = 0; w < 30; w++) yield return null;
            yield return Shot("11_map_fresh");
            var mapScroll = Object.FindFirstObjectByType<UnityEngine.UI.ScrollRect>();
            foreach (float n in new[] { 0f, 0.02f, 0.3f, 0.6f, 1f })
            {
                mapScroll.verticalNormalizedPosition = n;
                yield return null;
                yield return null;
                yield return Shot("11x_map_scroll_" + (int)(n * 100));
            }

            RegionDef r1 = GameSession.Repo.GetRegionByIndex(1);
            meta.ShowLevels(r1);
            yield return null;
            Assert.AreEqual("Levels", meta.CurrentScreen);
            yield return Shot("12_levels_fresh");

            // Play some progress and look again.
            var progress = GameSession.Progress;
            for (int i = 1; i <= 6; i++)
                progress.ApplyRun("lvl_001_" + i.ToString("D2"), new RunOutcome { Completed = true, StarMask = i % 2 == 0 ? 7 : 3, Score = 100 * i, CoinsCollected = 30 });
            meta.ShowLevels(r1);
            yield return null;
            yield return Shot("13_levels_progress");

            meta.ShowMap();
            yield return null;
            yield return null;
            yield return null;
            yield return Shot("14_map_progress");

            meta.ShowSettings();
            yield return null;
            yield return Shot("15_settings");
        }

        [UnityTest]
        public IEnumerator RunSceneRunsAndThePlayerMoves()
        {
            GameSession.CurrentLevelId = "lvl_001_01";
            yield return Load("Run");
            var rc = Object.FindFirstObjectByType<RunController>();
            Assert.IsNotNull(rc);
            Assert.IsNotNull(rc.Session);
            yield return Shot("20_run_intro");

            rc.Hud.SkipIntro();
            int frame = 0;
            rc.Input.ScriptedMove = () => (frame / 90) % 2 == 0 ? SimInput.Right : SimInput.Left;
            rc.Input.ScriptedJump = () => frame % 40 == 0;
            for (frame = 1; frame <= 300; frame++) yield return null;
            Assert.Greater(rc.Session.State.Tick, 100, "the simulation advanced");
            Assert.IsFalse(float.IsNaN(rc.View.CameraY));
            yield return Shot("21_run_playing");

            rc.Pause();
            yield return null;
            yield return Shot("22_run_pause");
            rc.Resume();
        }

        [UnityTest]
        public IEnumerator RunSceneForegroundWithoutBackground()
        {
            GameSession.CurrentLevelId = "lvl_001_03";
            yield return Load("Run");
            var rc = Object.FindFirstObjectByType<RunController>();
            rc.Hud.SkipIntro();
            int frame = 0;
            rc.Input.ScriptedMove = () => SimInput.Right;
            rc.Input.ScriptedJump = () => frame % 45 == 0;
            for (frame = 1; frame <= 200; frame++) yield return null;
            rc.View.SetBackgroundVisible(false);
            yield return null;
            yield return Shot("25_run_foreground_only");
            for (int n = 1; n <= 3; n++)
            {
                rc.View.SetBackgroundLayerCount(n);
                yield return null;
                yield return Shot("27_layers" + n);
            }
            rc.View.SetBackgroundVisible(true);
            yield return null;
            yield return Shot("26_run_with_background");
            yield return Shot("28_run_tall_9x21", 450, 1050);
            yield return Shot("29_run_tablet_3x4", 720, 960);
        }

        [UnityTest]
        public IEnumerator RunSceneShowsTheResultsScreenAfterAWin()
        {
            GameSession.CurrentLevelId = "lvl_001_01";
            yield return Load("Run");
            var rc = Object.FindFirstObjectByType<RunController>();
            rc.Hud.SkipIntro();
            rc.Session.State.Row = rc.Session.Spec.TargetRow();   // fabricate the climb
            for (int i = 0; i < 30; i++) yield return null;
            yield return Shot("23a_warp_into_galaxy");
            for (int i = 0; i < 60; i++) yield return null;   // the warp takes one second before the results show
            Assert.IsTrue(rc.Session.GoalReached);
            Assert.IsTrue(rc.Hud.OverlayOpen);
            Assert.IsTrue(GameSession.Progress.IsLevelCleared("lvl_001_01"));
            Assert.IsNotNull(GameSession.LastResult);
            yield return Shot("23_run_results");
        }

        [UnityTest]
        public IEnumerator GalaxyPortalHangsAboveTheLastPlatform()
        {
            GameSession.CurrentLevelId = "lvl_001_01";
            yield return Load("Run");
            var rc = Object.FindFirstObjectByType<RunController>();
            rc.Hud.SkipIntro();
            Platform top = rc.Session.Data.Platforms[rc.Session.Data.Platforms.Length - 1];
            rc.Session.State.X = top.X;
            rc.Session.State.Y = top.Y;
            rc.Session.State.MaxY = top.Y;
            rc.Session.State.KillY = top.Y - Fx.FromInt(6);
            rc.Session.State.Row = top.Row - 1;   // not yet at the goal row
            for (int i = 0; i < 5; i++) yield return null;
            yield return Shot("23b_galaxy_portal");
            Assert.IsNotNull(GameObject.Find("Portal"), "the portal exists");
        }

        [UnityTest]
        public IEnumerator RunSceneShowsTheFailedScreenWhenThePlayerFalls()
        {
            GameSession.CurrentLevelId = "lvl_001_01";
            yield return Load("Run");
            var rc = Object.FindFirstObjectByType<RunController>();
            rc.Hud.SkipIntro();
            rc.Session.State.Grounded = false;
            rc.Session.State.Y = Fx.FromInt(-100);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsTrue(rc.Session.Failed);
            Assert.IsTrue(rc.Hud.OverlayOpen);
            Assert.AreEqual(0, GameSession.Progress.Coins, "a failed run pays nothing");
            yield return Shot("24_run_failed");
        }

        [UnityTest]
        public IEnumerator EveryRegionRunSceneRenders()
        {
            for (int r = 1; r <= 5; r++)
            {
                GameSession.CurrentLevelId = "lvl_00" + r + "_03";
                yield return Load("Run");
                var rc = Object.FindFirstObjectByType<RunController>();
                rc.Hud.SkipIntro();
                int frame = 0;
                rc.Input.ScriptedMove = () => SimInput.Right;
                rc.Input.ScriptedJump = () => frame % 35 == 0;
                for (frame = 1; frame <= 150; frame++) yield return null;
                Assert.Greater(rc.Session.State.Tick, 50, "region " + r);
                yield return Shot("30_region" + r);
            }
        }
    }
}
