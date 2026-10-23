using System.Collections;
using System.Collections.Generic;
using System.Text;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using BoingUpSkyTower.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BoingUpSkyTower.Tests
{
    /// <summary>
    /// Enforces the critical UX rules (docs/11, CUX-3/4/5): no overlapping text or buttons, nothing outside the safe area,
    /// touch targets of at least 48 dp and text of at least 16 sp on every supported aspect ratio. Category CriticalUX.
    /// The canvas follows the camera's target texture, so each aspect is simulated by rendering to a texture of that size.
    /// </summary>
    [Category("CriticalUX")]
    public class CriticalUxLayoutTests
    {
        struct Aspect
        {
            public string Name; public int W, H;
            public Aspect(string n, int w, int h) { Name = n; W = w; H = h; }
        }

        static readonly Aspect[] Aspects =
        {
            new Aspect("9x16", 540, 960),
            new Aspect("9x19.5", 450, 975),
            new Aspect("9x21", 450, 1050),
            new Aspect("3x4", 720, 960),
        };

        sealed class Item { public RectTransform Rt; public Rect Box; public string Name; public bool IsButton; public bool IsImage; }

        [SetUp]
        public void SetUp()
        {
            Time.captureFramerate = 60;
            GameSession.Reset();
            GameSession.InitForTests(new MemorySaveStore());
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            foreach (var rc in Object.FindObjectsByType<RunController>(FindObjectsSortMode.None)) rc.enabled = false;
            foreach (var mc in Object.FindObjectsByType<MetaController>(FindObjectsSortMode.None)) mc.enabled = false;
            UiKit.SafeAreaNormalized = () => new Rect(0f, 0f, 1f, 1f);
            RunView.AspectOverride = 0f;
            PlayerPrefs.DeleteAll();
            GameSettings.ApplyTextScale();
            GameSession.Reset();
        }

        static IEnumerator Load(string scene)
        {
            var op = SceneManager.LoadSceneAsync(scene);
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;
        }

        static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null && p.GetComponent<Canvas>() == null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        static bool IsDescendant(Transform child, Transform ancestor)
        {
            for (Transform t = child; t != null; t = t.parent) if (t == ancestor) return true;
            return false;
        }

        /// <summary>Collects the visible text and button boxes in canvas units (reference pixels) and returns the violations found.</summary>
        static List<string> Audit(string screen, Aspect a, Rect safe)
        {
            var problems = new List<string>();
            var items = new List<Item>();
            Camera cam = Camera.main;
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isActiveAndEnabled || canvas.renderMode != RenderMode.ScreenSpaceCamera) continue;
                float scale = canvas.scaleFactor;

                foreach (Text t in canvas.GetComponentsInChildren<Text>(false))
                {
                    if (string.IsNullOrEmpty(t.text) || !t.gameObject.activeInHierarchy) continue;
                    if (t.color.a < 0.05f) continue;
                    Rect box = ToCanvasUnits(t.rectTransform, cam, scale);
                    // Use the size the text really needs, placed according to its alignment.
                    float pw = Mathf.Min(t.preferredWidth, box.width);
                    float ph = t.preferredHeight;
                    float x = box.x, y = box.y;
                    switch (t.alignment)
                    {
                        case TextAnchor.UpperCenter: case TextAnchor.MiddleCenter: case TextAnchor.LowerCenter: x += (box.width - pw) * 0.5f; break;
                        case TextAnchor.UpperRight: case TextAnchor.MiddleRight: case TextAnchor.LowerRight: x += box.width - pw; break;
                    }
                    switch (t.alignment)
                    {
                        case TextAnchor.UpperLeft: case TextAnchor.UpperCenter: case TextAnchor.UpperRight: y += box.height - ph; break;
                        case TextAnchor.MiddleLeft: case TextAnchor.MiddleCenter: case TextAnchor.MiddleRight: y += (box.height - ph) * 0.5f; break;
                    }
                    items.Add(new Item { Rt = t.rectTransform, Box = new Rect(x, y, pw, ph), Name = Path(t.transform) + " \"" + Trim(t.text) + "\"" });
                    if (t.fontSize < UiKit.MinBodyTextPx - 1f)
                        problems.Add(screen + ": text below 16 sp (" + t.fontSize + "): " + Path(t.transform));
                }

                // Decorative images (icons, stars, badges): text must not run over them unless the image is the text's own backing.
                foreach (Image img in canvas.GetComponentsInChildren<Image>(false))
                {
                    if (img.sprite == null || img.color.a < 0.3f || !img.gameObject.activeInHierarchy || Theme.IsSurfaceSprite(img.sprite)) continue;
                    if (img.GetComponent<Button>() != null) continue;          // buttons are audited as buttons
                    string n = img.name;
                    if (n == "Bg" || n == "Fill" || n == "Panel" || n == "Ribbon" || n == "WalletPanel" || n == "StarPill" || n == "RowsBacking" || n == "TopBacking" || n == "NameBacking") continue;
                    Rect ib = ToCanvasUnits(img.rectTransform, cam, scale);
                    if (ib.width * ib.height > 0.35f * (a.W / scale) * (a.H / scale)) continue;   // full-screen art
                    items.Add(new Item { Rt = img.rectTransform, Box = ib, Name = Path(img.transform) + " [image]", IsImage = true });
                }

                foreach (Button b in canvas.GetComponentsInChildren<Button>(false))
                {
                    if (!b.gameObject.activeInHierarchy) continue;
                    var rt = (RectTransform)b.transform;
                    Rect box = ToCanvasUnits(rt, cam, scale);
                    items.Add(new Item { Rt = rt, Box = box, Name = Path(rt) + " [button]", IsButton = true });
                    if (box.width < UiKit.MinTouchPx - 1f || box.height < UiKit.MinTouchPx - 1f)
                        problems.Add(screen + ": touch target below 48 dp (" + Mathf.RoundToInt(box.width) + "x" + Mathf.RoundToInt(box.height) + "): " + Path(rt));
                }

                // Content inside a scroll view (the world map) is only audited where the viewport shows it; the mask clips the rest.
                for (int n = items.Count - 1; n >= 0; n--)
                {
                    ScrollRect sr = items[n].Rt.GetComponentInParent<ScrollRect>();
                    if (sr == null || sr.viewport == null || sr.GetComponentInParent<Canvas>() != canvas) continue;
                    Rect vp = ToCanvasUnits(sr.viewport, cam, scale);
                    Transform topBar = null;
                    foreach (Transform tr in sr.transform.parent.GetComponentsInChildren<Transform>(false)) if (tr.name == "TopBacking") { topBar = tr; break; }
                    if (topBar != null) vp.yMax = Mathf.Min(vp.yMax, ToCanvasUnits((RectTransform)topBar, cam, scale).yMin);   // the map shows through the floating top bar; only the part below it is audited
                    Rect b = items[n].Box;
                    float x0 = Mathf.Max(b.xMin, vp.xMin), x1 = Mathf.Min(b.xMax, vp.xMax);
                    float y0 = Mathf.Max(b.yMin, vp.yMin), y1 = Mathf.Min(b.yMax, vp.yMax);
                    if (x1 <= x0 || y1 <= y0) { items.RemoveAt(n); continue; }
                    items[n].Box = new Rect(x0, y0, x1 - x0, y1 - y0);
                }

                // A modal popup or overlay hides everything behind it: only the modal is audited.
                if (items.Exists(i => i.Name.StartsWith("Popup/") || i.Name.StartsWith("Overlay/")))
                    items.RemoveAll(i => !(i.Name.StartsWith("Popup/") || i.Name.StartsWith("Overlay/")));

                // Inside the safe area (normalised to the canvas size in canvas units).
                float cw = a.W / scale, ch = a.H / scale;
                var sa = new Rect(safe.x * cw, safe.y * ch, safe.width * cw, safe.height * ch);
                foreach (Item it in items)
                {
                    if (it.IsImage) continue;
                    const float tol = 2f;
                    if (it.Box.xMin < sa.xMin - tol || it.Box.xMax > sa.xMax + tol || it.Box.yMin < sa.yMin - tol || it.Box.yMax > sa.yMax + tol)
                        problems.Add(screen + ": outside the safe area: " + it.Name + " " + it.Box + " safe " + sa);
                }
            }

            // Overlap of text or buttons (shared ancestors and button labels are not overlaps).
            for (int i = 0; i < items.Count; i++)
            {
                for (int j = i + 1; j < items.Count; j++)
                {
                    Item p = items[i], q = items[j];
                    if (IsDescendant(p.Rt, q.Rt) || IsDescendant(q.Rt, p.Rt)) continue;
                    if (p.IsImage != q.IsImage)
                    {
                        // image vs text only; an image next to a button is fine
                        Item other = p.IsImage ? q : p;
                        if (other.IsButton) continue;
                    }
                    else if (p.IsImage && q.IsImage) continue;
                    if (!p.Box.Overlaps(q.Box)) continue;
                    float ox = Mathf.Min(p.Box.xMax, q.Box.xMax) - Mathf.Max(p.Box.xMin, q.Box.xMin);
                    float oy = Mathf.Min(p.Box.yMax, q.Box.yMax) - Mathf.Max(p.Box.yMin, q.Box.yMin);
                    if (ox < 6f || oy < 6f) continue;
                    problems.Add(screen + ": overlap " + ox.ToString("F0") + "x" + oy.ToString("F0") + ": " + p.Name + "  <>  " + q.Name);
                }
            }
            return problems;
        }

        static string Trim(string s) { s = s.Replace("\n", " "); return s.Length > 24 ? s.Substring(0, 24) : s; }

        static Rect ToCanvasUnits(RectTransform rt, Camera cam, float scale)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            Vector3 a = cam.WorldToScreenPoint(c[0]);
            Vector3 b = cam.WorldToScreenPoint(c[2]);
            return new Rect(a.x / scale, a.y / scale, (b.x - a.x) / scale, (b.y - a.y) / scale);
        }

        /// <summary>Points the canvases at a render texture of the given size so the CanvasScaler lays out for that aspect.</summary>
        static IEnumerator ApplyAspect(Aspect a, RenderTexture[] holder)
        {
            Camera cam = Camera.main;
            if (holder[0] != null) holder[0].Release();
            holder[0] = new RenderTexture(a.W, a.H, 24);
            cam.targetTexture = holder[0];
            cam.aspect = (float)a.W / a.H;
            RunView.AspectOverride = (float)a.W / a.H;
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay || c.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = 1f;
                }
            }
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        static void Restore(RenderTexture[] holder)
        {
            Camera cam = Camera.main;
            if (cam != null) { cam.targetTexture = null; cam.ResetAspect(); }
            RunView.AspectOverride = 0f;
            if (holder[0] != null) holder[0].Release();
        }

        static readonly Rect FullSafe = new Rect(0f, 0f, 1f, 1f);
        static readonly Rect NotchSafe = new Rect(0.03f, 0.02f, 0.94f, 0.93f);   // a rounded, notched phone

        static IEnumerator CheckScreen(string screen, System.Action show, List<string> all)
        {
            var holder = new RenderTexture[1];
            foreach (Aspect a in Aspects)
            {
                foreach (Rect safe in new[] { FullSafe, NotchSafe })
                {
                    Rect s = safe;
                    UiKit.SafeAreaNormalized = () => s;
                    show();
                    yield return ApplyAspect(a, holder);
                    yield return null;
                    all.AddRange(Audit(screen + "@" + a.Name + (safe == NotchSafe ? "+notch" : ""), a, safe));
                }
            }
            Restore(holder);
        }

        static void Report(List<string> problems)
        {
            // De-duplicate while keeping order, so one real defect is not repeated per aspect.
            var seen = new HashSet<string>();
            var sb = new StringBuilder();
            foreach (string p in problems)
            {
                string key = p.Substring(p.IndexOf(':') + 1);
                if (seen.Add(key)) sb.AppendLine(p);
            }
            Assert.IsEmpty(problems, "Critical UX violations (" + seen.Count + " distinct):\n" + sb);
        }

        [UnityTest]
        public IEnumerator MetaScreensHaveNoOverlapAndFitEveryAspect() { return MetaAudit(100); }

        /// <summary>CUX-5: every screen must survive the 130 percent text size.</summary>
        [UnityTest]
        public IEnumerator MetaScreensSurvive130PercentText() { return MetaAudit(130); }

        IEnumerator MetaAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            RegionDef r1 = GameSession.Repo.GetRegionByIndex(1);
            for (int i = 1; i <= 6; i++)
                GameSession.Progress.ApplyRun("lvl_001_" + i.ToString("D2"), new RunOutcome { Completed = true, StarMask = i % 2 == 0 ? 7 : 3, Score = 100 * i, CoinsCollected = 30 });

            yield return CheckScreen("menu", () => meta.ShowMenu(), problems);
            yield return CheckScreen("map", () => meta.ShowMap(), problems);
            yield return CheckScreen("levels", () => meta.ShowLevels(r1), problems);
            yield return CheckScreen("settings", () => meta.ShowSettings(), problems);
            var levels = GameSession.Repo.LevelsOf(r1.Id);
            yield return CheckScreen("level-popup", () => { meta.ShowLevels(r1); meta.OnLevelTapped(levels[2]); }, problems);
            yield return CheckScreen("locked-popup", () => { meta.ShowLevels(r1); meta.OnLevelTapped(levels[12]); }, problems);
            // Power-ups (docs/15 FR-INV-5): the loadout picker without stock (shop hint), the chooser, the endless start popup, then with stock.
            yield return CheckScreen("loadout-chooser-empty", () => { meta.ShowLevels(r1); meta.OnLevelTapped(levels[2]); meta.CurrentPicker.OpenChooser(0); }, problems);
            yield return CheckScreen("endless-popup-empty", () => { meta.ShowLevels(r1); meta.OnEndlessTapped(r1); }, problems);
            GrantAllPowerUps(3);
            GameSession.Progress.SetLoadout(new List<string> { "pw_doublejump", "pw_flight", "pw_slowtime" });
            yield return CheckScreen("level-popup-loadout", () => { meta.ShowLevels(r1); meta.OnLevelTapped(levels[2]); }, problems);
            yield return CheckScreen("loadout-chooser", () => { meta.ShowLevels(r1); meta.OnLevelTapped(levels[2]); meta.CurrentPicker.OpenChooser(1); }, problems);
            yield return CheckScreen("endless-popup-loadout", () => { meta.ShowLevels(r1); meta.OnEndlessTapped(r1); }, problems);
            yield return CheckScreen("region-locked-popup", () => { meta.ShowMap(); meta.OnRegionTapped(GameSession.Repo.GetRegionByIndex(2)); }, problems);
            yield return CheckScreen("coming-soon-popup", () => { meta.ShowMap(); meta.OnRegionTapped(GameSession.Repo.GetRegionByIndex(6)); }, problems);
            yield return CheckScreen("reset-popup", () => { meta.ShowSettings(); meta.ConfirmReset(); }, problems);
            Report(problems);
        }

        /// <summary>CUX-9 addendum (owner, 2026-10-23): no platform image is stretched. Every platform sprite of a region that has painted width pieces is drawn
        /// Simple with the same X and Y scale (one uniform scale); a stretched slice or a non-uniform scale fails the test.</summary>
        [UnityTest]
        [Category("CriticalUX")]
        public IEnumerator NoPlatformImageIsStretched()
        {
            var problems = new List<string>();
            int checkedPlatforms = 0;
            foreach (string id in new[] { "lvl_001_01", "lvl_001_05", "lvl_001_07", "lvl_001_10" })
            {
                GameSession.CurrentLevelId = id;
                yield return Load("Run");
                var rc = Object.FindFirstObjectByType<RunController>();
                Assert.IsNotNull(rc, id);
                yield return null;
                foreach (SpriteRenderer sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                {
                    if (!sr.gameObject.name.StartsWith("plat") || !sr.gameObject.activeInHierarchy) continue;
                    checkedPlatforms++;
                    Vector3 ls = sr.transform.lossyScale;
                    if (Mathf.Abs(ls.x - ls.y) > 0.001f * Mathf.Max(ls.x, ls.y)) problems.Add(id + " " + sr.name + " non-uniform scale " + ls);
                    if (sr.drawMode != SpriteDrawMode.Simple) problems.Add(id + " " + sr.name + " draw mode " + sr.drawMode + " (stretches a slice)");
                }
            }
            Assert.Greater(checkedPlatforms, 20, "platforms were inspected");
            Report(problems);
        }

        [UnityTest]
        public IEnumerator RunScreensHaveNoOverlapAndFitEveryAspect() { return RunAudit(100, false); }

        [UnityTest]
        public IEnumerator RunScreensSurvive130PercentTextAndLeftHandedLayout() { return RunAudit(130, true); }

        IEnumerator RunAudit(int textPercent, bool leftHanded)
        {
            GameSettings.TextPercent = textPercent;
            GameSettings.LeftHanded = leftHanded;
            GameSettings.ReducedMotion = leftHanded;   // the second scenario also exercises reduced motion
            for (int i = 1; i <= 2; i++)
                GameSession.Progress.ApplyRun("lvl_001_0" + i, new RunOutcome { Completed = true, StarMask = 1, Score = 100, CoinsCollected = 10 });
            GameSession.CurrentLevelId = "lvl_001_03";
            yield return Load("Run");
            var rc = Object.FindFirstObjectByType<RunController>();
            var problems = new List<string>();
            yield return CheckScreen("run-intro", () => { }, problems);
            rc.Hud.SkipIntro();
            int frame = 0;
            rc.Input.ScriptedMove = () => SimInput.Right;
            rc.Input.ScriptedJump = () => frame % 45 == 0;
            for (frame = 1; frame <= 120; frame++) yield return null;
            yield return CheckScreen("run-hud", () => { }, problems);
            yield return CheckScreen("run-pause", () => rc.Hud.ShowPause(), problems);
            rc.Hud.CloseOverlay();
            yield return CheckScreen("run-failed", () => rc.Hud.ShowFailed(rc.Session, true), problems);
            rc.Hud.CloseOverlay();
            var outcome = rc.Session.BuildOutcome();
            var reward = GameSession.Progress.ApplyRun(GameSession.CurrentLevelId, outcome);
            yield return CheckScreen("run-results", () => rc.Hud.ShowResults(rc.Session, reward, true), problems);
            rc.Hud.CloseOverlay();

            // Endless mode (owner decision 2026-10-20): its HUD (Floor / Best) and the game over overlay obey the same rules.
            rc.StartEndlessRun(GameSession.Repo.GetRegionByIndex(1).Id);
            rc.Hud.SkipIntro();
            yield return CheckScreen("run-endless-hud", () => { }, problems);
            var endlessReward = new EndlessReward { Floors = 142, BestBefore = 120, NewBest = true, FullRateCoins = 121, Coins = 61, OverCap = true, RunsToday = 4 };
            yield return CheckScreen("run-endless-failed", () => rc.Hud.ShowEndlessFailed(rc.Session, endlessReward), problems);
            Report(problems);
        }

        // ---- power-ups (docs/15): the in-run buttons on every aspect, at 130 percent text, right- and left-handed ----

        static void GrantAllPowerUps(int each)
        {
            foreach (PowerUpDef d in GameSession.Progress.PowerUpDefs.Implemented) GameSession.Progress.GrantPowerUp(d.Id, each);
        }

        [UnityTest]
        public IEnumerator PowerUpButtonsFitEveryAspect() { return PowerUpRunAudit(100, false); }

        [UnityTest]
        public IEnumerator PowerUpButtonsSurvive130PercentTextAndLeftHandedLayout() { return PowerUpRunAudit(130, true); }

        IEnumerator PowerUpRunAudit(int textPercent, bool leftHanded)
        {
            GameSettings.TextPercent = textPercent;
            GameSettings.LeftHanded = leftHanded;
            GameSettings.ReducedMotion = leftHanded;
            for (int i = 1; i <= 2; i++)
                GameSession.Progress.ApplyRun("lvl_001_0" + i, new RunOutcome { Completed = true, StarMask = 1, Score = 100, CoinsCollected = 10 });
            GrantAllPowerUps(3);
            GameSession.Progress.SetLoadout(new List<string> { "pw_flight", "pw_doublejump", "pw_shield" });
            GameSession.CurrentLevelId = "lvl_001_03";
            yield return Load("Run");
            var rc = Object.FindFirstObjectByType<RunController>();
            var problems = new List<string>();
            Assert.AreEqual(3, rc.Hud.Powers.ButtonCount, "one button per loadout slot");
            yield return CheckScreen("run-powerups-intro", () => { }, problems);   // the buttons wait until the intro text is gone
            Assert.IsFalse(rc.Hud.Powers.Visible, "hidden during the intro");
            rc.Hud.SkipIntro();
            int frame = 0;
            rc.Input.ScriptedMove = () => SimInput.Right;
            rc.Input.ScriptedJump = () => frame % 45 == 0;
            for (frame = 1; frame <= 60; frame++) yield return null;
            Assert.IsTrue(rc.Hud.Powers.Visible);

            // thumb side: the buttons sit on the jump side, in the lower half, and are well over 48 dp
            for (int i = 0; i < 3; i++)
            {
                RectTransform b = rc.Hud.Powers.ButtonRect(i);
                Assert.IsNotNull(b, "button " + i);
                Assert.GreaterOrEqual(b.rect.width, UiKit.MinTouchPx - 1f);
                Assert.GreaterOrEqual(b.rect.height, UiKit.MinTouchPx - 1f);
                Assert.IsNotNull(b.GetComponent<A11yLabel>());
                StringAssert.Contains("left", b.GetComponent<A11yLabel>().Text, "spoken label names the units left");
            }
            yield return CheckScreen("run-powerups-ready", () => { }, problems);

            // activate all three through the input path: rings drain, counts turn into seconds, nothing overlaps
            rc.Input.LatchPower(0);
            rc.Input.LatchPower(1);
            rc.Input.LatchPower(2);
            for (frame = 61; frame <= 90; frame++) yield return null;
            Assert.AreEqual(3, rc.Session.State.PwUsed, "the three buttons reached the simulation");
            yield return CheckScreen("run-powerups-active", () => { }, problems);
            rc.Session.SettlePowerUps(GameSession.Progress);
            Assert.AreEqual(2, GameSession.Progress.PowerUpCount("pw_flight"), "a used unit is gone after the run settles");
            Report(problems);
        }

        // ---- shop (docs/14): all four tabs, both sections, promo on and off, and the shop popups ----

        [UnityTest]
        public IEnumerator ShopScreenHasNoOverlapAndFitsEveryAspect() { return ShopAudit(100); }

        /// <summary>CUX-5: the shop survives the 130 percent text size too.</summary>
        [UnityTest]
        public IEnumerator ShopScreenSurvives130PercentText() { return ShopAudit(130); }

        IEnumerator ShopAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            ShopScreen.TestPurchasesOverride = null;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            GameSession.Progress.AddGems(300);
            GameSession.Progress.AddCoins(12345);

            // Release layout: the gem buttons say "Not available yet".
            yield return CheckScreen("shop-gems", () => meta.ShowShop(ShopScreen.TabGems, ShopScreen.GemsSection), problems);
            yield return CheckScreen("shop-coins", () => meta.ShowShop(ShopScreen.TabGems, ShopScreen.CoinsSection), problems);
            yield return CheckScreen("shop-tab1-chests", () => meta.ShowShop(ShopScreen.TabChests, ShopScreen.GemsSection), problems);
            yield return CheckScreen("shop-tab2-powerups", () => meta.ShowShop(ShopScreen.TabPowerups, ShopScreen.GemsSection), problems);
            yield return CheckScreen("shop-tab2-powerups-bought", () => { meta.ShowShop(ShopScreen.TabPowerups, ShopScreen.GemsSection); meta.CurrentShop.TapBuyPowerUp(GameSession.Progress.PowerUpDefs.Get("pw_flight"), PowerUpCurrency.Coins); }, problems);
            yield return CheckScreen("shop-tab3-vip", () => meta.ShowShop(ShopScreen.TabVip, ShopScreen.GemsSection), problems);
            yield return CheckScreen("shop-gems-notbuyable-popup", () => { meta.ShowShop(); meta.CurrentShop.TapGemPack(3); }, problems);

            // Development layout: "Buy (test)" and the test purchase popups.
            ShopScreen.TestPurchasesOverride = true;
            yield return CheckScreen("shop-gems-test", () => meta.ShowShop(ShopScreen.TabGems, ShopScreen.GemsSection), problems);
            yield return CheckScreen("shop-test-buy-popup", () => { meta.ShowShop(); meta.CurrentShop.TapGemPack(6); }, problems);
            yield return CheckScreen("shop-tab2-powerups-test", () => meta.ShowShop(ShopScreen.TabPowerups, ShopScreen.GemsSection), problems);
            ShopScreen.TestPurchasesOverride = null;

            // Coin popups: confirm above 50 gems, not enough gems, done.
            yield return CheckScreen("shop-coin-confirm-popup", () => { meta.ShowShop(ShopScreen.TabGems, ShopScreen.CoinsSection); meta.CurrentShop.TapCoinPack(4); }, problems);
            yield return CheckScreen("shop-not-enough-popup", () => { meta.ShowShop(ShopScreen.TabGems, ShopScreen.CoinsSection); meta.CurrentShop.TapCoinPack(6); }, problems);
            yield return CheckScreen("shop-coins-done-popup", () => { meta.ShowShop(ShopScreen.TabGems, ShopScreen.CoinsSection); meta.CurrentShop.TapCoinPack(0); }, problems);

            // Promo off: list prices only, no crossed-out price and no Launch offer badge.
            GameSettings.LaunchPromo = false;
            yield return CheckScreen("shop-gems-list-prices", () => meta.ShowShop(ShopScreen.TabGems, ShopScreen.GemsSection), problems);
            yield return CheckScreen("shop-coins-list-prices", () => meta.ShowShop(ShopScreen.TabGems, ShopScreen.CoinsSection), problems);
            GameSettings.LaunchPromo = true;

            // Power-up shop: the not-enough-coins popup (with an empty wallet) points to the coins section.
            GameSession.Progress.TrySpendCoins(GameSession.Progress.Coins);
            yield return CheckScreen("shop-tab2-not-enough-popup", () => { meta.ShowShop(ShopScreen.TabPowerups, ShopScreen.GemsSection); meta.CurrentShop.TapBuyPowerUp(GameSession.Progress.PowerUpDefs.Get("pw_rocket"), PowerUpCurrency.Coins); }, problems);
            Report(problems);
        }
    }

    /// <summary>CUX-1: the run loop stays inside its frame budget and does not allocate per frame, on the hardest content.</summary>
    [Category("CriticalUX")]
    public class CriticalUxPerformanceTests
    {
        [SetUp]
        public void SetUp()
        {
            Time.captureFramerate = 60;
            GameSession.Reset();
            GameSession.InitForTests(new MemorySaveStore());
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            foreach (var rc in Object.FindObjectsByType<RunController>(FindObjectsSortMode.None)) rc.enabled = false;
            GameSession.Reset();
        }

        [UnityTest]
        public IEnumerator HardestLevelStaysInTheFrameBudgetWithoutAllocations()
        {
            GameSession.CurrentLevelId = "lvl_005_15";
            var op = SceneManager.LoadSceneAsync("Run");
            while (!op.isDone) yield return null;
            yield return null;
            var rc = Object.FindFirstObjectByType<RunController>();
            rc.Hud.SkipIntro();
            int frame = 0;
            rc.Input.ScriptedMove = () => (frame / 70) % 2 == 0 ? SimInput.Right : SimInput.Left;
            rc.Input.ScriptedJump = () => frame % 30 == 0;
            for (frame = 1; frame <= 120; frame++) yield return null;     // warm-up (JIT, first-use caches)

            rc.Stats.Reset();
            System.GC.Collect();
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            const int measured = 600;
            for (frame = 121; frame < 121 + measured; frame++) yield return null;
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            double perFrame = allocated / (double)measured;
            Debug.Log("[CUX-PERF] avg " + rc.Stats.AverageMs.ToString("F3") + " ms, max " + rc.Stats.MaxMs.ToString("F3") + " ms, alloc " + perFrame.ToString("F1") + " B/frame");
            Assert.LessOrEqual(rc.Stats.AverageMs, 2.0, "average script time per frame (CUX-1)");
            Assert.LessOrEqual(rc.Stats.MaxMs, 6.0, "worst script time per frame (CUX-1)");
            Assert.LessOrEqual(perFrame, AllocBudget, "managed allocation per frame (CUX-1)");
            Assert.IsFalse(rc.Paused, "the run was not interrupted");
        }

        const double AllocBudget = 1024.0;
    }
}
