using System.Collections;
using System.Collections.Generic;
using System.Text;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using BoingUpSkyTower.Monetization;
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
            RunController.HeroOverride = null;
            PlayerPrefs.DeleteAll();
            GameSettings.ApplyTextScale();
            Services.Account = null;      // account scenarios use stub services: back to the default
            AccountSync.ResetForTests();
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
                    if (n == "Bg" || n == "Fill" || n == "Panel" || n == "Ribbon" || n == "WalletPanel" || n == "StarPill" || n == "RowsBacking" || n == "TopBacking" || n == "SeamSky" || n == "NameBacking" || n == "Ground" || n == "GroundEdge" || n == "DirtOval" || n == "Backdrop") continue;   // GearPic: number badges sit on their own backing over its corner by design; the others are scenery
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
                    if (safe == FullSafe) Shot(screen, a, holder[0]);
                }
            }
            Restore(holder);
        }

        /// <summary>Visual validation: with BU_SHOTS=dir set, every audited screen is saved as a PNG (9x16 and 9x21, normal text, right-handed) for review.</summary>
        static void Shot(string screen, Aspect a, RenderTexture rt)
        {
            string dir = System.Environment.GetEnvironmentVariable("BU_SHOTS");
            if (string.IsNullOrEmpty(dir) || rt == null) return;
            if (a.Name != "9x16" && a.Name != "9x21") return;
            if (GameSettings.TextPercent != 100 || GameSettings.LeftHanded) return;
            System.IO.Directory.CreateDirectory(dir);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, screen + "_" + a.Name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
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
            // Account (owner issue 35.3): the Home button and pop-up in the three states, plus the Settings account block signed in and in a build without sign-in.
            yield return CheckScreen("menu-signed-in", () => { AccountScenario("signed-in"); meta.ShowMenu(); }, problems);
            yield return CheckScreen("account-popup-guest", () => { AccountScenario("guest"); meta.ShowMenu(); meta.ShowAccount(); }, problems);
            yield return CheckScreen("account-popup-signed-in", () => { AccountScenario("signed-in"); meta.ShowMenu(); meta.ShowAccount(); }, problems);
            yield return CheckScreen("account-popup-unavailable", () => { AccountScenario("unavailable"); meta.ShowMenu(); meta.ShowAccount(); }, problems);
            yield return CheckScreen("settings-account-signed-in", () => { AccountScenario("signed-in"); meta.ShowSettings(); }, problems);
            yield return CheckScreen("settings-account-unavailable", () => { AccountScenario("unavailable"); meta.ShowSettings(); }, problems);
            AccountScenario("guest");
            yield return CheckScreen("map", () => meta.ShowMap(), problems);
            yield return CheckScreen("levels", () => meta.ShowLevels(r1), problems);
            yield return CheckScreen("settings", () => meta.ShowSettings(), problems);
            var levels = GameSession.Repo.LevelsOf(r1.Id);
            yield return CheckScreen("level-popup", () => { meta.ShowLevels(r1); meta.OnLevelTapped(levels[2]); }, problems);
            yield return CheckScreen("locked-popup", () => { meta.ShowLevels(r1); meta.OnLevelTapped(levels[12]); }, problems);
            // Power-ups: no loadout any more (2026-10-31); the level and endless popups with and without owned power-ups look the same.
            yield return CheckScreen("endless-popup-empty", () => { meta.ShowLevels(r1); meta.OnEndlessTapped(r1); }, problems);
            GrantAllPowerUps(3);
            yield return CheckScreen("level-popup-with-powerups", () => { meta.ShowLevels(r1); meta.OnLevelTapped(levels[2]); }, problems);
            yield return CheckScreen("endless-popup-with-powerups", () => { meta.ShowLevels(r1); meta.OnEndlessTapped(r1); }, problems);
            yield return CheckScreen("region-locked-popup", () => { meta.ShowMap(); meta.OnRegionTapped(GameSession.Repo.GetRegionByIndex(2)); }, problems);
            yield return CheckScreen("coming-soon-popup", () => { meta.ShowMap(); meta.OnRegionTapped(GameSession.Repo.GetRegionByIndex(6)); }, problems);
            yield return CheckScreen("reset-popup", () => { meta.ShowSettings(); meta.ConfirmReset(); }, problems);
            // Settings extras (docs/38): the Admin popup (both pages), the reset confirm (signed-in text is the longest), the restore result and the hidden code box.
            yield return CheckScreen("settings-admin", () => { meta.ShowSettings(); meta.ShowAdmin(); }, problems);
            yield return CheckScreen("settings-admin-heroes", () => { meta.ShowSettings(); meta.ShowAdmin(1); }, problems);
            yield return CheckScreen("settings-reset-confirm", () => { meta.ShowSettings(); GameSession.Progress.SetAccount("google", "test"); meta.ConfirmReset(); }, problems);
            yield return CheckScreen("settings-restored", () => { meta.ShowSettings(); meta.ShowPurchasesRestored(2); }, problems);
            yield return CheckScreen("settings-nothing-to-restore", () => { meta.ShowSettings(); meta.ShowPurchasesRestored(0); }, problems);
            yield return CheckScreen("settings-code-box", () => { meta.ShowSettings(); meta.ShowAdminCodeBox(); }, problems);
            GameSession.Progress.SetAccount("guest", "");

            // Characters: the chosen character on the land stage and the roster
            yield return CheckScreen("characters", () => meta.ShowCharacters(), problems);
            ProgressService.TestAllCharacters = true;   // SYSTEM_TEST: every hero is owned, so the roster has five tiles (Momo, Capi, Otto, Bonbon, Bao)
            foreach (CharacterDef hero in new[] { CharacterCatalog.ForestGirl, CharacterCatalog.BeachOtter, CharacterCatalog.CandyGirl, CharacterCatalog.Monk })
            {
                CharacterCatalog.Selected = hero;
                yield return CheckScreen("characters-" + hero.Id, () => meta.ShowCharacters(), problems);
                yield return CheckScreen("hero-reveal-" + hero.Id, () => { meta.ShowMenu(); meta.ShowHeroReveal(hero); }, problems);
            }
            CharacterCatalog.Selected = CharacterCatalog.Momo;
            ProgressService.TestAllCharacters = false;

            // Hero inventory badges (docs/36): copies of several heroes (a one-digit, a two-digit count and a brand new card), no overlap and the number fits at this text size
            var savedCopies = new Dictionary<string, int>(GameSession.Progress.Data.Copies);
            GameSession.Progress.HeroInventory.AddCopies("momo", 11);      // 12 copies
            GameSession.Progress.HeroInventory.AddCopies("momo_u", 2);     // a new card with 2 copies
            yield return CheckScreen("characters-copies", () => meta.ShowCharacters(), problems);
            meta.ShowCharacters();
            yield return null;
            var badges = new Dictionary<string, string>();
            foreach (Image im in Object.FindObjectsByType<Image>(FindObjectsSortMode.None))
                if (im.name == CharactersScreen.BadgeName && im.transform.parent != null && im.transform.parent.parent != null)
                {
                    Assert.IsFalse(im.raycastTarget, "the badge is not a touch target");
                    badges[im.transform.parent.parent.name] = im.GetComponentInChildren<Text>().text;
                }
            Assert.AreEqual("12", badges["Char_momo"], "badge shows the copies");
            Assert.AreEqual("2", badges["Char_momo_u"], "a new card appears with its copies");
            GameSession.Progress.Data.Copies.Clear();
            foreach (KeyValuePair<string, int> kv in savedCopies) GameSession.Progress.Data.Copies[kv.Key] = kv.Value;

            // Hero sheet (docs/21): Common Momo on both tabs, plus fake heroes of every higher rarity (long name, long region, maximum stats, powers) so the banner colours and the longest texts are audited.
            yield return CheckScreen("hero-sheet-common", () => meta.ShowHeroSheet(CharacterCatalog.Momo), problems);
            yield return CheckScreen("hero-sheet-common-details", () => { meta.ShowHeroSheet(CharacterCatalog.Momo); meta.CurrentHeroSheet.SelectTab(1); }, problems);
            var maxStats = new CharacterStats { JumpLaunch = 23, MaxSpeed = 12, Control = 45, WallBoostHundredths = 100, CoinFind = 50, GemFind = 5 };
            foreach (CharacterRarity rar in new[] { CharacterRarity.Uncommon, CharacterRarity.Rare, CharacterRarity.Epic, CharacterRarity.Legendary })
            {
                CharacterRarity rr = rar;
                int powers = CharacterDefs.PowerCount[(int)rr];
                var list = new[] { PowerUp.Flight, PowerUp.DoubleJump, PowerUp.SlowTime, PowerUp.Magnet };   // up to 4 powers (Legendary)
                var pw = new PowerUp[powers];
                for (int i = 0; i < powers; i++) pw[i] = list[i];
                string label = "hero-sheet-" + rr.ToString().ToLowerInvariant();
                HeroSheetData fake = HeroSheetData.From("Starlight Dragon", rr, "Crystal Caverns of Dawn", maxStats, pw);
                yield return CheckScreen(label, () => meta.ShowHeroSheet(CharacterCatalog.Momo, null, fake), problems);
                yield return CheckScreen(label + "-details", () => { meta.ShowHeroSheet(CharacterCatalog.Momo, null, fake); meta.CurrentHeroSheet.SelectTab(1); }, problems);
            }
            // entry points (owner 2026-11-02): the HOME screen has no (i) button and no hero tap area; the HEROES screen has the (i) button right of the shown hero (at least 48 dp)
            meta.ShowMenu();
            yield return null;
            foreach (Button bt in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                Assert.IsFalse(bt.name == "HeroInfo" || bt.name == "HeroTap", bt.name + " must not exist on the home screen");
            meta.ShowCharacters();
            yield return null;
            Button found = null;
            foreach (Button bt in Object.FindObjectsByType<Button>(FindObjectsSortMode.None)) if (bt.name == "HeroInfo") found = bt;
            Assert.IsNotNull(found, "HeroInfo is missing on the Heroes screen");
            Assert.GreaterOrEqual(((RectTransform)found.transform).rect.width, UiKit.MinTouchPx - 1f, "HeroInfo is smaller than 48 dp");
            found.onClick.Invoke();
            Assert.AreEqual("Hero", meta.CurrentScreen, "HeroInfo must open the hero sheet");

            // The one-time "New hero joins you!" pop-up (docs/21): card, hero picture, name, rarity and the single OK button on every aspect (and at 130 percent text in the second run).
            yield return CheckScreen("hero-reveal", () => { meta.ShowMenu(); meta.ShowHeroReveal(CharacterCatalog.ForestGirl); }, problems);
            AccountCleanup();
            Report(problems);
        }

        // ---- emulator report 2 regressions (2026-11-02): settings reset button, level popup, rewards region names, list ends ----

        static Rect CanvasBox(RectTransform rt)
        {
            Canvas c = rt.GetComponentInParent<Canvas>().rootCanvas;
            return ToCanvasUnits(rt, Camera.main, c.scaleFactor);
        }

        static RectTransform FindRt(string name, Transform under = null)
        {
            foreach (RectTransform rt in Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
            {
                if (rt.name != name || !rt.gameObject.activeInHierarchy) continue;
                if (under != null && !IsDescendant(rt, under)) continue;
                return rt;
            }
            return null;
        }

        /// <summary>The touch-target rule is in dp on the real density: 720 x 1280 at 320 dpi needs 144 reference px (the old fixed 126 was 42 dp).</summary>
        [Test]
        public void TouchMinimumFollowsRealDensity()
        {
            Assert.GreaterOrEqual(UiKit.MinTouchRefFor(320f, 720, 1280), 143.9f, "720x1280 @320 dpi");
            Assert.GreaterOrEqual(UiKit.MinTouchRefFor(320f, 1080, 1920), UiKit.MinTouchPx - 0.1f, "1080x1920 @320 dpi never drops below the reference minimum");
            Assert.AreEqual(UiKit.MinTouchPx, UiKit.MinTouchRefFor(420f, 1080, 1920), 0.5f, "1080x1920 @420 dpi stays at the 411 dp reference");
            Assert.AreEqual(UiKit.MinTouchPx, UiKit.MinTouchRefFor(0f, 1080, 1920), 0.01f, "unknown dpi falls back");
        }

        /// <summary>Settings: every row, both dev flag rows and the reset button sit inside the card with clear gaps (also where the card scrolls); the header stays fixed.</summary>
        [UnityTest]
        public IEnumerator SettingsRowsAndResetButtonStayInsideThePanel()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            GameSettings.ShowDevRowsForTests = true;
            var holder = new RenderTexture[1];
            foreach (Aspect a in Aspects)
            {
                UiKit.SafeAreaNormalized = () => FullSafe;
                meta.ShowSettings();
                yield return ApplyAspect(a, holder);
                yield return null;
                RectTransform card = FindRt("Card");
                RectTransform reset = FindRt("Reset");
                RectTransform restore = FindRt("RestorePurchases");
                if (card == null || reset == null || restore == null) { problems.Add(a.Name + ": settings card, Reset Progress or Restore Purchases button missing"); continue; }
                if (!IsDescendant(reset, card)) problems.Add(a.Name + ": Reset must be a child of the settings card");
                Rect cb = CanvasBox(card), rb = CanvasBox(reset);
                const float gap = 16f;
                if (rb.xMin < cb.xMin + gap || rb.xMax > cb.xMax - gap || rb.yMin < cb.yMin + gap)
                    problems.Add(a.Name + ": Reset " + rb + " is not inside the card " + cb + " with a gap");
                int n = 0;
                float lowestRowBottom = float.MaxValue;
                bool sawAdmin = false;
                foreach (Button b in card.GetComponentsInChildren<Button>(false))
                {
                    if (b.transform == reset) continue;
                    if (b.name == "RestorePurchases" || b.name == "Admin" || b.name == "VersionTap")
                    {
                        // the buttons under the rows: inside the card, 48 dp, and never touching the rows or each other
                        Rect fb = CanvasBox((RectTransform)b.transform);
                        if (fb.xMin < cb.xMin || fb.xMax > cb.xMax || fb.yMin < cb.yMin || fb.yMax > cb.yMax) problems.Add(a.Name + ": " + b.name + " leaves the card");
                        if (fb.height < UiKit.MinTouchPx - 1f) problems.Add(a.Name + ": " + b.name + " below 48 dp");
                        sawAdmin |= b.name == "Admin";
                        continue;
                    }
                    n++;
                    Rect bb = CanvasBox((RectTransform)b.transform);
                    if (bb.xMin < cb.xMin || bb.xMax > cb.xMax || bb.yMin < cb.yMin || bb.yMax > cb.yMax) problems.Add(a.Name + ": " + b.name + " leaves the card");
                    if (bb.height < UiKit.MinTouchPx - 1f) problems.Add(a.Name + ": " + b.name + " below 48 dp");
                    lowestRowBottom = Mathf.Min(lowestRowBottom, bb.yMin);
                }
                if (n < 7) problems.Add(a.Name + ": expected 7 rows, found " + n);
                if (!sawAdmin) problems.Add(a.Name + ": the small Admin button must be visible in dev builds");
                Rect restoreBox = CanvasBox(restore);
                if (restoreBox.yMax > lowestRowBottom - gap + 0.5f) problems.Add(a.Name + ": Restore Purchases " + restoreBox + " touches the last row (bottom " + lowestRowBottom + ")");
                if (rb.yMax > restoreBox.yMin - gap + 0.5f) problems.Add(a.Name + ": Reset Progress " + rb + " touches Restore Purchases " + restoreBox);
                ScrollRect sr = card.GetComponentInParent<ScrollRect>();
                if (sr == null || !sr.vertical || sr.content != card) problems.Add(a.Name + ": the settings card must scroll vertically");
            }
            GameSettings.ShowDevRowsForTests = false;
            Restore(holder);
            Report(problems);
        }

        /// <summary>Puts the account layer in one of three states: "guest" (stub available), "signed-in" (stub signed in as Test Player, save linked) or "unavailable" (release build without the plugin).</summary>
        static void AccountScenario(string which)
        {
            ProgressService p = GameSession.Progress;
            AccountSync.Hook(p);
            if (which == "unavailable") { Services.Account = new UnavailableAccountService(); p.SetAccount("guest", ""); return; }
            var stub = new StubAccountService { UseMemoryOnly = true };
            Services.Account = stub;
            if (which == "signed-in") { stub.SignIn(null); p.SetAccount("google", stub.PlayerId); }
            else p.SetAccount("guest", "");
        }

        static void AccountCleanup()
        {
            GameSession.Progress.SetAccount("guest", "");
            Services.Account = null;   // back to the default (stub in the editor)
            AccountSync.ResetForTests();
        }

        /// <summary>
        /// Owner issue 35.3: Home bar is Shop | PLAY | Heroes (no VIP crown), PLAY exactly two icon buttons wide (gap included) and as tall as the others, the row
        /// centred, and a round Account button (at least 48 dp) at the left of the top row that does not touch the wallet pills; the crown stays on the map and the heroes screen.
        /// </summary>
        [UnityTest]
        public IEnumerator HomeBarHasPlayTwoIconsWideAndAnAccountButton()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            var holder = new RenderTexture[1];
            foreach (Aspect a in Aspects)
            {
                UiKit.SafeAreaNormalized = () => FullSafe;
                meta.ShowMenu();
                yield return ApplyAspect(a, holder);
                yield return null;
                RectTransform bar = FindRt("TabBar"), shop = FindRt("TabShop"), play = FindRt("TabPlay"), heroes = FindRt("TabCharacters");
                if (bar == null || shop == null || play == null || heroes == null) { problems.Add(a.Name + ": the home bar needs Shop, Play and Heroes"); continue; }
                if (FindRt("TabVIP") != null) problems.Add(a.Name + ": the VIP crown must not be on the home bar");
                Rect bb = CanvasBox(bar), sb = CanvasBox(shop), pb = CanvasBox(play), hb = CanvasBox(heroes);
                const float gap = 12f;
                if (Mathf.Abs(pb.width - (sb.width + gap + hb.width)) > 2f || Mathf.Abs(sb.width - hb.width) > 1f) problems.Add(a.Name + ": PLAY " + pb.width + " is not two icon widths (" + sb.width + " + " + hb.width + " + gap)");
                if (Mathf.Abs(pb.height - sb.height) > 1f || Mathf.Abs(pb.height - hb.height) > 1f || Mathf.Abs(pb.yMin - sb.yMin) > 1f) problems.Add(a.Name + ": PLAY must have the same height and base line as the other buttons");
                if (Mathf.Abs((pb.xMin - sb.xMax) - (hb.xMin - pb.xMax)) > 2f) problems.Add(a.Name + ": uneven gaps around PLAY");
                if (Mathf.Abs((sb.xMin - bb.xMin) - (bb.xMax - hb.xMax)) > 2f) problems.Add(a.Name + ": the home bar is not centred");

                RectTransform acc = FindRt("Account");
                if (acc == null) { problems.Add(a.Name + ": the Account button is missing on Home"); continue; }
                Rect ab = CanvasBox(acc);
                if (ab.width < UiKit.MinTouchPx - 1f || ab.height < UiKit.MinTouchPx - 1f) problems.Add(a.Name + ": the Account button is below 48 dp");
                foreach (string other in new[] { "CoinsPill", "GemsPill", "StarsPill", "Settings" })
                {
                    RectTransform o = FindRt(other);
                    if (o != null && ab.Overlaps(CanvasBox(o))) problems.Add(a.Name + ": the Account button overlaps " + other);
                    if (o != null && CanvasBox(o).xMin < ab.xMin) problems.Add(a.Name + ": the Account button must be the leftmost item of the top row");
                }
                if (acc.GetComponent<A11yLabel>() == null || !acc.GetComponent<A11yLabel>().Text.StartsWith("Account")) problems.Add(a.Name + ": the Account button needs an accessibility label");

                // the crown stays on the other screens that have a bar
                meta.ShowMap();
                yield return null;
                if (FindRt("TabVIP") == null) problems.Add(a.Name + ": the VIP crown must stay on the world map bar");
                meta.ShowCharacters();
                yield return null;
                if (FindRt("TabVIP") == null) problems.Add(a.Name + ": the VIP crown must stay on the heroes bar");
            }
            Restore(holder);
            Report(problems);
        }

        /// <summary>The Account pop-up in each state: the right buttons, the right texts, taps do what they say and the pop-up rebuilds into the new state.</summary>
        [UnityTest]
        public IEnumerator AccountPopupStatesAndActions()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            AccountScenario("guest");
            meta.ShowMenu();
            meta.ShowAccount();
            yield return null;
            Assert.IsNotNull(FindRt("Google"), "guest: Sign in with Google Play");
            Assert.IsNull(FindRt("SyncNow"), "guest: no Sync now");
            Assert.IsNotNull(FindRt("CloseX"), "a Close X");
            FindRt("Google").GetComponent<Button>().onClick.Invoke();   // the stub signs in at once
            yield return null;
            yield return null;
            Assert.IsTrue(AccountSync.IsSignedIn, "signed in after the tap");
            Assert.IsNotNull(FindRt("SyncNow"), "signed in: Sync now");
            Assert.IsNotNull(FindRt("SignOut"), "signed in: Sign out");
            Assert.IsNull(FindRt("Google"), "signed in: no sign-in button");
            FindRt("SignOut").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            Assert.IsFalse(AccountSync.IsGoogleMode, "guest again after Sign out");
            Assert.IsNotNull(FindRt("Google"), "guest again: Sign in with Google Play");
            AccountScenario("unavailable");
            meta.ShowMenu();
            meta.ShowAccount();
            yield return null;
            Assert.IsNull(FindRt("Google"), "unavailable: no sign-in button");
            Assert.IsNull(FindRt("SyncNow"), "unavailable: no Sync now");
            Assert.IsNotNull(FindRt("CloseX"), "unavailable: only close");
            AccountCleanup();
        }

        /// <summary>Level popup: exactly one close control, PLAY and the close X at least 48 dp, and no big empty middle between the pills and PLAY.</summary>
        [UnityTest]
        public IEnumerator LevelPopupHasOneCloseAndNoEmptyMiddle()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            RegionDef r1 = GameSession.Repo.GetRegionByIndex(1);
            var levels = GameSession.Repo.LevelsOf(r1.Id);
            var problems = new List<string>();
            var holder = new RenderTexture[1];
            foreach (Aspect a in Aspects)
            {
                UiKit.SafeAreaNormalized = () => FullSafe;
                meta.ShowLevels(r1);
                meta.OnLevelTapped(levels[0]);
                yield return ApplyAspect(a, holder);
                yield return null;
                RectTransform popup = FindRt("Popup");
                RectTransform panel = popup != null ? popup.Find("Panel") as RectTransform : null;
                if (panel == null) { problems.Add(a.Name + ": level popup missing"); continue; }
                int closes = 0;
                Rect playBox = default(Rect);
                foreach (Button b in panel.GetComponentsInChildren<Button>(false))
                {
                    Rect bb = CanvasBox((RectTransform)b.transform);
                    if (b.name == "Close" || b.name == "CloseX") closes++;
                    if (b.name == "PLAY") playBox = bb;
                    if (bb.width < UiKit.MinTouchPx - 1f || bb.height < UiKit.MinTouchPx - 1f) problems.Add(a.Name + ": popup button " + b.name + " below 48 dp");
                }
                if (closes != 1) problems.Add(a.Name + ": level popup has " + closes + " close buttons, expected 1");
                if (playBox.height <= 0f) { problems.Add(a.Name + ": PLAY missing"); continue; }
                // the lowest content row (pills) to PLAY: a gap, not a void
                float lowestContent = float.MaxValue;
                foreach (Transform t in panel)
                {
                    if (t.name == "Pays" || t.name == "Best" || t.name.StartsWith("Star")) lowestContent = Mathf.Min(lowestContent, CanvasBox((RectTransform)t).yMin);
                }
                float gapToPlay = lowestContent - playBox.yMax;
                if (gapToPlay > 160f) problems.Add(a.Name + ": empty middle of " + gapToPlay.ToString("F0") + " between the content and PLAY");
                if (gapToPlay < 16f) problems.Add(a.Name + ": content touches PLAY (" + gapToPlay.ToString("F0") + ")");
            }
            Restore(holder);
            Report(problems);
        }

        /// <summary>Rewards: a region name never breaks inside a word, and the list ends above the tab row with room to scroll the last card fully into view.</summary>
        [UnityTest]
        public IEnumerator RewardsRegionNamesAndListEndAreSafe()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            var holder = new RenderTexture[1];
            foreach (Aspect a in Aspects)
            {
                UiKit.SafeAreaNormalized = () => FullSafe;
                meta.ShowRewards(RewardTab.Main);
                yield return ApplyAspect(a, holder);
                yield return null;
                foreach (Text t in Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
                {
                    if (t.name != "Name" || t.transform.parent == null || !t.transform.parent.name.StartsWith("Slot") || !t.gameObject.activeInHierarchy || string.IsNullOrEmpty(t.text)) continue;
                    if (t.horizontalOverflow != HorizontalWrapMode.Overflow || t.text.IndexOf(' ') >= 0)
                        problems.Add(a.Name + ": region name \"" + t.text + "\" may wrap inside a word");
                }
                RectTransform tabs = FindRt("Tabs");
                foreach (ScrollRect sr in Object.FindObjectsByType<ScrollRect>(FindObjectsSortMode.None))
                {
                    if (sr.viewport == null || tabs == null || !sr.gameObject.activeInHierarchy) continue;
                    if (CanvasBox(sr.viewport).yMin < CanvasBox(tabs).yMax - 0.5f) problems.Add(a.Name + ": the rewards list runs under the tab row");
                    var vl = sr.content.GetComponent<VerticalLayoutGroup>();
                    if (vl != null && vl.padding.bottom < 48) problems.Add(a.Name + ": rewards list has no end padding for the last card");
                }
            }
            Restore(holder);
            Report(problems);
        }

        /// <summary>Shop lists: the scroll content is longer than the cards by the end padding, so the last card can be pulled fully above the tab bar.</summary>
        [Test]
        public void ShopListsHaveEndPadding()
        {
            Assert.GreaterOrEqual(ShopChrome.ScrollEndPad, 24f);
        }

        /// <summary>The run draws the character from its parts: every part picture sits on the same canvas bounds.</summary>
        static void AssertPartsFitPlayer(RunView view, List<string> problems)
        {
            SpriteRenderer[] parts = view.PartRenderers;
            if (parts.Length < 6) problems.Add("run: only " + parts.Length + " part pictures were built");
            if (parts.Length == 0) return;
            Bounds a = parts[0].bounds;
            foreach (SpriteRenderer w in parts)
            {
                Bounds b = w.bounds;
                if (Vector3.Distance(a.size, b.size) > 0.02f * a.size.magnitude || Vector3.Distance(a.center, b.center) > 0.02f * a.size.magnitude)
                    problems.Add("run: " + w.name + " bounds " + b + " differ from the character canvas " + a);
            }
        }

        /// <summary>CUX-9 addendum (owner, 2026-10-23): no platform image is stretched. Every platform sprite of a region that has painted width pieces is drawn
        /// Simple with the same X and Y scale (one uniform scale); a stretched slice or a non-uniform scale fails the test.</summary>
        [UnityTest]
        [Category("CriticalUX")]
        public IEnumerator NoPlatformImageIsStretched()
        {
            var problems = new List<string>();
            int checkedPlatforms = 0;
            foreach (string id in new[] { "lvl_001_01", "lvl_001_05", "lvl_001_10", "lvl_002_03", "lvl_002_09", "lvl_003_04", "lvl_003_12", "lvl_004_06", "lvl_004_14", "lvl_005_02", "lvl_005_11" })
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
            Assert.Greater(checkedPlatforms, 60, "platforms were inspected");
            Report(problems);
        }

        // ---- emulator report 2 (2026-11-02), CUX-3 in the run: the HUD and the pads are in reserved strips, the camera shows the world between them ----

        /// <summary>The strips leave a play field of at least MinPlayUnits (the player can be 10 units above the danger line) on every aspect, and are consistent between HUD (ref px) and camera (world).</summary>
        [Test]
        public void RunStripsLeavePlayFieldOnEveryAspect()
        {
            UiKit.SafeAreaNormalized = () => FullSafe;
            foreach (float aspect in new[] { 9f / 16f, 9f / 19.5f, 9f / 21f, 3f / 4f })
            {
                float top, bottom;
                RunLayout.StripsWorld(aspect, 10f, out top, out bottom);
                Assert.Greater(top, 0f, "top strip at " + aspect);
                Assert.AreEqual(0f, bottom, 0.0001f, "no bottom reserve: the screen bottom is the danger edge (owner 2026-11-02) at " + aspect);
                Assert.GreaterOrEqual(10f / aspect - top - bottom, RunLayout.MinPlayUnits - 0.01f, "play height at " + aspect);
                float tr, br;
                RunLayout.StripsRef(aspect, 10f, out tr, out br);
                Assert.AreEqual(0f, br, 0.0001f, "HUD ref bottom reserve is 0 at " + aspect);
                Assert.AreEqual(top, tr * 10f / RunLayout.CanvasW(aspect), 0.001f, "HUD and camera top reserve agree at " + aspect);
                if (aspect < 0.6f)
                {
                    Assert.GreaterOrEqual(tr, 16f + RunLayout.RowH + 12f + RunLayout.Row2H - 0.1f, "top strip holds both HUD rows at " + aspect);
                }
            }
        }

        /// <summary>Real run scene: every top HUD element lies inside the top strip, every pad inside the bottom strip, and the camera puts the danger line on the strip edge (no platform under a pill or a pad).</summary>
        [UnityTest]
        public IEnumerator RunHudAndPadsStayInTheirStripsAndCameraAgrees()
        {
            GameSession.CurrentLevelId = "lvl_001_01";
            yield return Load("Run");
            var rc = Object.FindFirstObjectByType<RunController>();
            rc.Hud.SkipIntro();
            var problems = new List<string>();
            var holder = new RenderTexture[1];
            foreach (Aspect a in Aspects)
            {
                if (a.Name == "3x4") continue;   // wide tablets: the strips shrink to keep a play field (see RunLayout.MinPlayUnits), documented in docs/11
                UiKit.SafeAreaNormalized = () => FullSafe;
                yield return ApplyAspect(a, holder);
                yield return null;
                RectTransform topStrip = FindRt("TopStrip");
                if (topStrip == null) { problems.Add(a.Name + ": top strip missing"); continue; }
                if (FindRt("BottomStrip") != null) problems.Add(a.Name + ": a bottom strip exists (owner 2026-11-02: none)");
                if (FindRt("DangerLine") != null) problems.Add(a.Name + ": a red danger line exists (owner 2026-11-02: hidden)");
                Rect tb = CanvasBox(topStrip);
                foreach (string n in new[] { "FloorPill", "GoalPill", "GemPill", "Gauge", "Pause", "Star0", "Star1", "Star2" })
                {
                    RectTransform rt = FindRt(n);
                    if (rt == null) continue;
                    Rect r = CanvasBox(rt);
                    if (r.yMin < tb.yMin - 1.5f) problems.Add(a.Name + ": " + n + " reaches into the play field (" + r.yMin.ToString("F0") + " < " + tb.yMin.ToString("F0") + ")");
                }
                foreach (string n in new[] { "PadLeft", "PadRight", "PadJump" })
                    if (FindRt(n) == null) problems.Add(a.Name + ": " + n + " missing");   // pads float over the field; their overlap rules are in the audits
                // the kill line of the simulation is the physical bottom edge of the screen
                Camera cam = Camera.main;
                float lineY = cam.WorldToScreenPoint(new Vector3(0f, rc.Session.State.KillY.F(), 0f)).y;
                if (Mathf.Abs(lineY) > 3f) problems.Add(a.Name + ": kill line at screen y " + lineY.ToString("F1") + ", expected the screen bottom (0)");
            }
            Restore(holder);
            Report(problems);
        }

        /// <summary>CUX-3 (owner 2026-11-02): the combo and speed-up banners are in the CENTRE of the screen (horizontally centred, around the vertical middle), below the top HUD strip and above the pads, never at the bottom.</summary>
        [UnityTest]
        public IEnumerator CentreBannersStayInTheScreenCentre()
        {
            GameSession.CurrentLevelId = "lvl_001_01";
            yield return Load("Run");
            var rc = Object.FindFirstObjectByType<RunController>();
            rc.Hud.SkipIntro();
            var problems = new List<string>();
            var holder = new RenderTexture[1];
            foreach (Aspect a in Aspects)
            {
                UiKit.SafeAreaNormalized = () => FullSafe;
                yield return ApplyAspect(a, holder);
                yield return null;
                RectTransform top = FindRt("TopStrip");
                foreach (RectTransform rt in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (rt.name != "ComboBanner" && rt.name != "SpeedBanner") continue;
                    bool was = rt.gameObject.activeSelf;
                    rt.gameObject.SetActive(true);
                    Canvas.ForceUpdateCanvases();
                    Rect r = CanvasBox(rt);
                    Rect screen = CanvasBox(rt.GetComponentInParent<Canvas>().rootCanvas.GetComponent<RectTransform>());
                    if (Mathf.Abs(r.center.x - screen.center.x) > 2f) problems.Add(a.Name + ": " + rt.name + " not horizontally centred");
                    if (r.center.y < screen.yMin + screen.height * 0.3f || r.center.y > screen.yMin + screen.height * 0.7f) problems.Add(a.Name + ": " + rt.name + " is not in the vertical centre (y " + r.center.y.ToString("F0") + ")");
                    if (top != null && r.yMax > CanvasBox(top).yMin + 1.5f) problems.Add(a.Name + ": " + rt.name + " overlaps the top HUD strip");
                    rt.gameObject.SetActive(was);
                }
            }
            Restore(holder);
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
            AssertPartsFitPlayer(rc.View, problems);
            yield return CheckScreen("run-intro", () => { }, problems);
            rc.Hud.SkipIntro();
            int frame = 0;
            rc.Input.ScriptedMove = () => SimInput.Right;
            rc.Input.ScriptedJump = () => frame % 45 == 0;
            for (frame = 1; frame <= 120; frame++) yield return null;
            yield return CheckScreen("run-hud", () => { }, problems);
            yield return CheckScreen("run-pause", () => rc.Hud.ShowPause(), problems);
            rc.Hud.CloseOverlay();
            yield return CheckScreen("run-failed", () => rc.Hud.ShowFailed(rc.Session, 10, true), problems);
            rc.Hud.CloseOverlay();
            yield return CheckScreen("run-failed-gems-only", () => rc.Hud.ShowFailed(rc.Session, 0, false), problems);
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
            GameSession.CurrentLevelId = "lvl_001_03";
            yield return Load("Run");
            var rc = Object.FindFirstObjectByType<RunController>();
            var problems = new List<string>();
            Assert.AreEqual(7, rc.Hud.Powers.ButtonCount, "one button per owned type, no loadout limit");
            yield return CheckScreen("run-powerups-intro", () => { }, problems);   // the buttons wait until the intro text is gone
            Assert.IsFalse(rc.Hud.Powers.Visible, "hidden during the intro");
            rc.Hud.SkipIntro();
            int frame = 0;
            rc.Input.ScriptedMove = () => SimInput.Right;
            rc.Input.ScriptedJump = () => frame % 45 == 0;
            for (frame = 1; frame <= 60; frame++) yield return null;
            Assert.IsTrue(rc.Hud.Powers.Visible);

            // thumb side: the buttons sit on the jump side, are well over 48 dp, keep at least 8 dp between ALL pairs and never touch the pad
            var rects = new List<Rect>();
            for (int i = 0; i < 7; i++)
            {
                RectTransform b = rc.Hud.Powers.ButtonRect(i);
                Assert.IsNotNull(b, "button " + i);
                Assert.GreaterOrEqual(b.rect.width, UiKit.MinTouchPx - 1f);
                Assert.GreaterOrEqual(b.rect.height, UiKit.MinTouchPx - 1f);
                Assert.IsNotNull(b.GetComponent<A11yLabel>());
                StringAssert.Contains("left", b.GetComponent<A11yLabel>().Text, "spoken label names the units left");
                rects.Add(LayoutRect(b));
            }
            for (int i = 0; i < rects.Count; i++)
            {
                for (int j = i + 1; j < rects.Count; j++)
                    Assert.GreaterOrEqual(RectDistance(rects[i], rects[j]), UiKit.MinSpacingPx, "power-up buttons " + i + " and " + j + " are closer than 8 dp");
                foreach (HoldButton pad in new[] { rc.Hud.Pad.Left, rc.Hud.Pad.Right, rc.Hud.Pad.Jump })
                    Assert.GreaterOrEqual(RectDistance(rects[i], LayoutRect((RectTransform)pad.transform)), UiKit.MinSpacingPx, "power-up button " + i + " is too close to the pad");
            }
            yield return CheckScreen("run-powerups-ready", () => { }, problems);

            // activate every type through the input path: rings drain, counts stay, seconds show, nothing overlaps; Rocket is refused while Flight runs
            foreach (PowerUp t in new[] { PowerUp.Flight, PowerUp.Rocket, PowerUp.DoubleJump, PowerUp.WallStick, PowerUp.Shield, PowerUp.Magnet, PowerUp.SlowTime })
                rc.Input.LatchPower(t);
            for (frame = 61; frame <= 90; frame++) yield return null;
            Assert.AreEqual(6, rc.Session.State.PwUsed, "six buttons reached the simulation, the Rocket waits for the Flight");
            yield return CheckScreen("run-powerups-active", () => { }, problems);
            // stacking: another tap on the Magnet adds the full 30 s onto the remaining time
            int magnetBefore = rc.Session.State.MagnetTicks;
            rc.Input.LatchPower(PowerUp.Magnet);
            for (frame = 91; frame <= 100; frame++) yield return null;
            Assert.Greater(rc.Session.State.MagnetTicks, magnetBefore + 1500, "a second tap adds the base time");
            yield return CheckScreen("run-powerups-stacked", () => { }, problems);
            rc.Session.SettlePowerUps(GameSession.Progress);
            Assert.AreEqual(2, GameSession.Progress.PowerUpCount("pw_flight"), "a used unit is gone after the run settles");
            Assert.AreEqual(1, GameSession.Progress.PowerUpCount("pw_magnet"), "two taps used two units");
            Assert.AreEqual(3, GameSession.Progress.PowerUpCount("pw_rocket"), "a refused tap costs nothing");
            Report(problems);
        }

        // ---- hero powers (owner 2026-11-02, docs/34): up to 4 round buttons in a second column next to the bought power-up block ----

        [UnityTest]
        public IEnumerator HeroPowerButtonsFitEveryAspectNextToSevenBoughtPowerUps() { return HeroPowerRunAudit(100, false); }

        [UnityTest]
        public IEnumerator HeroPowerButtonsSurvive130PercentTextAndLeftHandedLayout() { return HeroPowerRunAudit(130, true); }

        IEnumerator HeroPowerRunAudit(int textPercent, bool leftHanded)
        {
            GameSettings.TextPercent = textPercent;
            GameSettings.LeftHanded = leftHanded;
            GameSettings.ReducedMotion = leftHanded;
            for (int i = 1; i <= 2; i++)
                GameSession.Progress.ApplyRun("lvl_001_0" + i, new RunOutcome { Completed = true, StarMask = 1, Score = 100, CoinsCollected = 10 });
            GrantAllPowerUps(3);
            // a Legendary hero with the 4 powers its tier rule gives (no Legendary row exists yet, so it is built from the rule)
            RunController.HeroOverride = new CharacterSpec { Id = "test_legendary", Name = "Test Legendary", Rarity = CharacterRarity.Legendary, Powers = TierRules.PowersFor("momo", CharacterRarity.Legendary) };
            GameSession.CurrentLevelId = "lvl_001_03";
            yield return Load("Run");
            var rc = Object.FindFirstObjectByType<RunController>();
            var problems = new List<string>();
            Assert.AreEqual(7, rc.Hud.Powers.ButtonCount, "seven bought power-up buttons stacked");
            Assert.AreEqual(4, rc.Hud.HeroPowers.ButtonCount, "a Legendary hero has four hero power buttons");
            Assert.IsFalse(rc.Hud.HeroPowers.Visible, "hidden during the intro, like the bought buttons");
            rc.Hud.SkipIntro();
            int frame = 0;
            rc.Input.ScriptedMove = () => SimInput.Right;
            rc.Input.ScriptedJump = () => frame % 45 == 0;
            for (frame = 1; frame <= 60; frame++) yield return null;
            Assert.IsTrue(rc.Hud.HeroPowers.Visible);

            // every hero button: at least 48 dp, labelled, at least 8 dp from every other button (bought, hero and the pad), same alpha as the other run buttons
            var heroRects = new List<Rect>();
            var otherRects = new List<Rect>();
            for (int i = 0; i < 4; i++)
            {
                RectTransform b = rc.Hud.HeroPowers.ButtonRect(i);
                Assert.IsNotNull(b, "hero button " + i);
                Assert.GreaterOrEqual(b.rect.width, UiKit.MinTouchPx - 1f);
                Assert.GreaterOrEqual(b.rect.height, UiKit.MinTouchPx - 1f);
                Assert.IsNotNull(b.GetComponent<A11yLabel>());
                StringAssert.Contains("hero power", b.GetComponent<A11yLabel>().Text);
                Assert.AreEqual(ControlPad.ButtonAlpha, b.GetComponent<CanvasGroup>().alpha, 0.001f);
                heroRects.Add(LayoutRect(b));
            }
            for (int i = 0; i < 7; i++) otherRects.Add(LayoutRect(rc.Hud.Powers.ButtonRect(i)));
            foreach (HoldButton pad in new[] { rc.Hud.Pad.Left, rc.Hud.Pad.Right, rc.Hud.Pad.Jump }) otherRects.Add(LayoutRect((RectTransform)pad.transform));
            for (int i = 0; i < heroRects.Count; i++)
            {
                for (int j = i + 1; j < heroRects.Count; j++)
                    Assert.GreaterOrEqual(RectDistance(heroRects[i], heroRects[j]), UiKit.MinSpacingPx, "hero buttons " + i + " and " + j + " are closer than 8 dp");
                for (int j = 0; j < otherRects.Count; j++)
                    Assert.GreaterOrEqual(RectDistance(heroRects[i], otherRects[j]), UiKit.MinSpacingPx, "hero button " + i + " is too close to bought button or pad " + j);
            }
            yield return CheckScreen("run-heropowers", () => { }, problems);   // every aspect (9:16, 9:19.5, 9:21, 3:4), full and notched safe area

            // a hero power through the input path: free (no unit taken), cooldown starts, the button turns to "cooling down"
            PowerUp first = rc.Hud.HeroPowers.ButtonType(0);
            int stockBefore = rc.Session.PowerStock(first);
            rc.Input.LatchHeroPower(first);
            for (frame = 61; frame <= 75; frame++) yield return null;
            Assert.IsTrue(PowerUps.IsActive(in rc.Session.State, first));
            Assert.AreEqual(stockBefore, rc.Session.PowerStock(first), "a hero power takes no inventory unit");
            Assert.IsFalse(rc.Session.Hero.IsReady(0));
            Assert.IsTrue(rc.Session.Hero.IsReady(1), "each hero button has its own cooldown");
            yield return CheckScreen("run-heropowers-cooldown", () => { }, problems);
            Report(problems);
        }

        /// <summary>
        /// Layout rectangle in canvas reference pixels from anchor, anchored position, pivot and size of a child of the HUD root, so it does not
        /// depend on the canvas scale or on the aspect (power-up buttons and pad buttons are siblings).
        /// </summary>
        static Rect LayoutRect(RectTransform rt)
        {
            Vector2 size = rt.rect.size;
            Vector2 parent = ((RectTransform)rt.parent).rect.size;
            float x = rt.anchorMin.x * parent.x + rt.anchoredPosition.x - rt.pivot.x * size.x;
            float y = rt.anchorMin.y * parent.y + rt.anchoredPosition.y - rt.pivot.y * size.y;
            return new Rect(x, y, size.x, size.y);
        }

        /// <summary>Empty space between two rectangles (0 when they touch or overlap; the larger axis gap when separated on both).</summary>
        static float RectDistance(Rect a, Rect b)
        {
            float dx = Mathf.Max(0f, Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax));
            float dy = Mathf.Max(0f, Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax));
            return Mathf.Sqrt(dx * dx + dy * dy);
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
            yield return CheckScreen("shop-tab2-powerups", () => meta.ShowShop(ShopScreen.TabPowerups, ShopScreen.GemsSection), problems);
            yield return CheckScreen("shop-tab2-powerups-bought", () => { meta.ShowShop(ShopScreen.TabPowerups, ShopScreen.GemsSection); meta.CurrentShop.TapBuyPowerUp(GameSession.Progress.PowerUpDefs.Get("pw_flight"), PowerUpCurrency.Coins); }, problems);
            // Treasure Chest screen (docs/22 point 3): rows, the (i) texts, the confirmation, the single and the ten reveal
            yield return CheckScreen("treasure-chests", () => meta.ShowTreasureChests(), problems);
            // VIP tab (owner issue 35): one scrollable page, Subscription section first, Chests under it; the old 'Chests' tab is gone
            yield return CheckScreen("shop-tab-vip", () => meta.ShowShop(ShopScreen.TabVip), problems);
            yield return CheckScreen("shop-tab-vip-chests-section", () => meta.ShowShop(ShopScreen.TabVip, ShopScreen.VipChestsSection), problems);
            yield return CheckScreen("shop-tab-heroes", () => meta.ShowShop(ShopScreen.TabHeroes), problems);   // placeholder: Heroes coming soon, nothing to buy
            yield return CheckScreen("treasure-confirm", () => { GameSession.Progress.AddGems(3000); meta.ShowTreasureChests(); meta.CurrentTreasure.TapBuy(ChestKind.Golden, 10); }, problems);
            yield return CheckScreen("treasure-reveal-single", () => { GameSession.Progress.AddGems(100); meta.ShowTreasureChests(); meta.CurrentTreasure.OpenNow(ChestKind.Iron, 1); meta.CurrentTreasure.Reveal.Skip(); }, problems);
            yield return CheckScreen("treasure-reveal-ten", () => { GameSession.Progress.AddGems(3000); meta.ShowTreasureChests(); meta.CurrentTreasure.OpenNow(ChestKind.Golden, 10); meta.CurrentTreasure.Reveal.Skip(); }, problems);
            yield return CheckScreen("shop-tab-bundles", () => meta.ShowShop(ShopScreen.TabBundles), problems);
            yield return CheckScreen("shop-gems-notbuyable-popup", () => { meta.ShowShop(); meta.CurrentShop.TapGemPack(3); }, problems);

            // Development layout: "Buy (test)" and the test purchase popups.
            ShopScreen.TestPurchasesOverride = true;
            yield return CheckScreen("shop-gems-test", () => meta.ShowShop(ShopScreen.TabGems, ShopScreen.GemsSection), problems);
            yield return CheckScreen("shop-test-buy-popup", () => { meta.ShowShop(); meta.CurrentShop.TapGemPack(6); }, problems);
            yield return CheckScreen("shop-tab2-powerups-test", () => meta.ShowShop(ShopScreen.TabPowerups, ShopScreen.GemsSection), problems);
            ShopScreen.TestPurchasesOverride = null;

            // Coin popups: confirm above 50 gems, not enough gems, done.
            yield return CheckScreen("shop-coin-confirm-popup", () => { meta.ShowShop(ShopScreen.TabGems, ShopScreen.CoinsSection); meta.CurrentShop.TapCoinPack(4); }, problems);
            yield return CheckScreen("shop-coin-short-goes-to-gems",() => { meta.ShowShop(ShopScreen.TabGems, ShopScreen.CoinsSection); meta.CurrentShop.TapCoinPack(6); }, problems);
            yield return CheckScreen("shop-coins-done-popup", () => { meta.ShowShop(ShopScreen.TabGems, ShopScreen.CoinsSection); meta.CurrentShop.TapCoinPack(0); }, problems);

            // Promo off: list prices only, no crossed-out price and no Launch offer badge.
            GameSettings.LaunchPromo = false;
            yield return CheckScreen("shop-gems-list-prices", () => meta.ShowShop(ShopScreen.TabGems, ShopScreen.GemsSection), problems);
            yield return CheckScreen("shop-coins-list-prices", () => meta.ShowShop(ShopScreen.TabGems, ShopScreen.CoinsSection), problems);
            GameSettings.LaunchPromo = true;

            // Power-up shop: the not-enough-coins popup (with an empty wallet) points to the coins section.
            GameSession.Progress.TrySpendCoins(GameSession.Progress.Coins);
            yield return CheckScreen("treasure-need-gems", () => { GameSession.Progress.TrySpendGems(GameSession.Progress.Gems); meta.ShowTreasureChests(); meta.CurrentTreasure.TapBuy(ChestKind.Iron, 1); }, problems);
            yield return CheckScreen("shop-tab2-not-enough-popup", () => { meta.ShowShop(ShopScreen.TabPowerups, ShopScreen.GemsSection); meta.CurrentShop.TapBuyPowerUp(GameSession.Progress.PowerUpDefs.Get("pw_rocket"), PowerUpCurrency.Coins); }, problems);

            // Rewards (docs/26 section 4): the three tabs with something claimable (stars, points, play time), and the "Reward!" card
            for (int lvl = 1; lvl <= 9; lvl++) GameSession.Progress.ApplyRun("lvl_001_0" + lvl, new RunOutcome { Completed = true, StarMask = 7, Score = 100, CoinsCollected = 10 });
            GameSession.Progress.RecordLogin();
            GameSession.Progress.AddPlaySeconds(600);
            yield return CheckScreen("rewards-main", () => meta.ShowRewards(RewardTab.Main), problems);
            yield return CheckScreen("rewards-daily", () => meta.ShowRewards(RewardTab.Daily), problems);
            yield return CheckScreen("rewards-weekly", () => meta.ShowRewards(RewardTab.Weekly), problems);
            yield return CheckScreen("rewards-main-region3", () => { meta.ShowRewards(RewardTab.Main); meta.CurrentRewards.SelectRegion(3); }, problems);   // a locked region: lock on the circle, empty bar
            yield return CheckScreen("rewards-claim-card", () =>
            {
                meta.ShowRewards(RewardTab.Daily);
                var sample = new RewardClaimOutcome { Result = RewardClaimResult.Ok, Gems = 40, Coins = 2000 };
                sample.PowerUpIds.Add("pw_flight"); sample.PowerUpIds.Add("pw_flight"); sample.PowerUpIds.Add("pw_shield");
                meta.CurrentRewards.ShowClaimed(sample);
            }, problems);
            Report(problems);
        }

        // ---- Merge screen (docs/34): ready, not enough copies, top tier, a full grid, and the "New hero" result card ----

        [UnityTest]
        public IEnumerator MergeScreenHasNoOverlapAndFitsEveryAspect() { return MergeAudit(100); }

        /// <summary>CUX-5: the Merge screen survives the 130 percent text size too.</summary>
        [UnityTest]
        public IEnumerator MergeScreenSurvives130PercentText() { return MergeAudit(130); }

        /// <summary>A clean save: Momo with 3 copies (ready), and every other hero revealed with a few copies so the grid has two rows.</summary>
        static void MergeState(bool full)
        {
            GameSession.Progress.ResetAll();
            GameSession.Progress.AddCopies("momo", 2);
            if (!full) return;
            int n = 0;
            foreach (CharacterSpec s in CharacterDefs.All)
            {
                if (s.Id == "momo") continue;
                GameSession.Progress.AddCopies(s.Id, 1 + (n++ % 3));
                GameSession.Progress.MarkHeroRevealed(s.Id);
            }
        }

        IEnumerator MergeAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            yield return CheckScreen("merge-ready", () => { MergeState(false); meta.ShowMerge(); }, problems);
            yield return CheckScreen("merge-full-grid", () => { MergeState(true); meta.ShowMerge(); }, problems);
            yield return CheckScreen("merge-not-enough", () => { GameSession.Progress.ResetAll(); meta.ShowMerge(); }, problems);   // Momo with his one starting copy
            yield return CheckScreen("merge-top-tier", () => { GameSession.Progress.ResetAll(); GameSession.Progress.AddCopies("momo_u", 2); meta.ShowMerge("momo_u"); }, problems);
            yield return CheckScreen("merge-pick-other", () => { MergeState(true); meta.ShowMerge("candygirl"); }, problems);
            yield return CheckScreen("merge-result-card", () => { MergeState(false); meta.ShowMerge(); meta.CurrentMerge.TapMerge(); }, problems);
            Assert.AreEqual(1, GameSession.Progress.Copies("momo_u"), "the audit's merge produced the Uncommon hero");
            Report(problems);
        }

        // ---- VIP page (docs/26 section 2, renamed from Subscription): release and test layouts, an active plan with Claim, the claimed state with countdown, the popups ----

        [UnityTest]
        public IEnumerator VipScreenHasNoOverlapAndFitsEveryAspect() { return SubscriptionAudit(100); }

        /// <summary>CUX-5: the VIP page survives the 130 percent text size too.</summary>
        [UnityTest]
        public IEnumerator VipScreenSurvives130PercentText() { return SubscriptionAudit(130); }

        /// <summary>Clean wallet, optionally with a running plan; the show action may run once per aspect, so every state is rebuilt from scratch.</summary>
        static void SubscriptionState(string plan, bool claimedToday)
        {
            GameSession.Progress.ResetAll();
            GameSession.Progress.SubscriptionClock = () => System.DateTime.UtcNow;
            GameSession.Progress.SubscriptionLocalOffset = null;
            if (plan == null) return;
            GameSession.Progress.GrantTestSubscriptionPurchase(plan, true);
            if (claimedToday) GameSession.Progress.ClaimDaily();
        }

        IEnumerator SubscriptionAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            ShopScreen.TestPurchasesOverride = null;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();

            // Release layout: price buttons only, the buy tap says "Not available yet".
            yield return CheckScreen("vip-release", () => { SubscriptionState(null, false); meta.ShowSubscription(); }, problems);
            yield return CheckScreen("vip-release-buy-popup", () => { SubscriptionState(null, false); meta.ShowSubscription(); meta.CurrentSubscription.TapBuy("weekly"); }, problems);
            yield return CheckScreen("vip-opened-from-shop", () => { SubscriptionState(null, false); meta.ShowShop(); meta.ShowSubscription(); }, problems);

            // Development layout: the test note, the confirm popup, then an active plan.
            ShopScreen.TestPurchasesOverride = true;
            yield return CheckScreen("vip-test", () => { SubscriptionState(null, false); meta.ShowSubscription(); }, problems);
            yield return CheckScreen("vip-test-confirm-popup", () => { SubscriptionState(null, false); meta.ShowSubscription(); meta.CurrentSubscription.TapBuy("monthly"); }, problems);
            yield return CheckScreen("vip-weekly-active-claim", () => { SubscriptionState("weekly", false); meta.ShowSubscription(); }, problems);
            yield return CheckScreen("vip-weekly-claimed-popup", () => { SubscriptionState("weekly", false); meta.ShowSubscription(); meta.CurrentSubscription.TapClaim(); }, problems);
            yield return CheckScreen("vip-weekly-come-back-tomorrow", () => { SubscriptionState("weekly", true); meta.ShowSubscription(); }, problems);
            yield return CheckScreen("vip-monthly-active-claim", () => { SubscriptionState("monthly", false); meta.ShowSubscription(); }, problems);
            yield return CheckScreen("vip-monthly-come-back-tomorrow", () => { SubscriptionState("monthly", true); meta.ShowSubscription(); }, problems);
            yield return CheckScreen("vip-other-plan-popup", () => { SubscriptionState("monthly", false); meta.ShowSubscription(); meta.CurrentSubscription.TapBuy("weekly"); }, problems);
            ShopScreen.TestPurchasesOverride = null;
            Report(problems);
        }

        // ---- login screen and the choose-your-save popup (docs/35) ----

        [UnityTest]
        public IEnumerator LoginScreensHaveNoOverlapAndFitEveryAspect() { return LoginAudit(100); }

        /// <summary>CUX-5: the login screen and the conflict popup survive the 130 percent text size too.</summary>
        [UnityTest]
        public IEnumerator LoginScreensSurvive130PercentText() { return LoginAudit(130); }

        static GameObject loginTestCanvas;

        static void DropLoginCanvas()
        {
            if (loginTestCanvas != null) Object.DestroyImmediate(loginTestCanvas);
            loginTestCanvas = null;
        }

        static void ShowLoginTest(bool conflict, bool googleAvailable)
        {
            DropLoginCanvas();
            MetaUiActive(false);
            Canvas canvas = UiKit.CreateCanvas("LoginTestUi", 20);
            loginTestCanvas = canvas.gameObject;
            if (conflict)
            {
                // the biggest numbers the cards must hold
                var device = new SaveSummary { LevelsCleared = 75, Gems = 1234567, Coins = 12345678, HeroCopies = 120, LastPlayedUtc = "2026-11-03T10:00:00Z" };
                var google = new SaveSummary { LevelsCleared = 1, Gems = 0, Coins = 0, HeroCopies = 1, LastPlayedUtc = "2026-10-01T10:00:00Z" };
                LoginScreen.BuildConflict(canvas.transform, device, google, "A Very Long Player Display Name", () => { }, () => { });
            }
            else LoginScreen.BuildLogin(canvas.transform, GameSession.Art, googleAvailable, () => { }, () => { });
        }

        IEnumerator LoginAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            yield return Load("Meta");
            var problems = new List<string>();
            yield return CheckScreen("login", () => ShowLoginTest(false, true), problems);
            yield return CheckScreen("login-guest-only", () => ShowLoginTest(false, false), problems);
            yield return CheckScreen("login-conflict", () => ShowLoginTest(true, true), problems);
            DropLoginCanvas();
            MetaUiActive(true);
            Report(problems);
        }

        // ---- bundles page and timer offer (docs/26 section 3) ----

        [UnityTest]
        public IEnumerator BundlesPageAndTimerOfferHaveNoOverlapAndFitEveryAspect() { return BundlesAudit(100); }

        /// <summary>CUX-5: the bundles page, its popups and the timer offer survive the 130 percent text size too.</summary>
        [UnityTest]
        public IEnumerator BundlesPageAndTimerOfferSurvive130PercentText() { return BundlesAudit(130); }

        static GameObject bundlesTestCanvas;

        static void MetaUiActive(bool on)
        {
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (c.name == "MetaUi") c.gameObject.SetActive(on);
        }

        static void DropBundlesCanvas()
        {
            if (bundlesTestCanvas != null) Object.DestroyImmediate(bundlesTestCanvas);
            bundlesTestCanvas = null;
        }

        /// <summary>A popup host for the page when it is drawn on its own canvas: the same modal "Popup" panel the Meta scene uses.</summary>
        static ShopScreen.Host BundlesTestHost(Canvas canvas, ArtCatalog art)
        {
            RectTransform popup = null;
            var host = new ShopScreen.Host();
            host.ClosePopup = () => { if (popup != null) Object.DestroyImmediate(popup.gameObject); popup = null; };
            host.CloseOverlay = host.ClosePopup;
            host.NewOverlay = () =>
            {
                host.ClosePopup();
                popup = UiKit.Rect(canvas.transform, "Popup");
                UiKit.Stretch(popup);
                popup.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
                return popup;
            };
            host.OpenPopup = (title, body, height) =>
            {
                RectTransform panel = UiKit.BuildPopupFrame(host.NewOverlay(), title, art, new Vector2(860f, height), host.ClosePopup);
                Text b = UiKit.AddText(panel, "Body", body, 44, Color.white);
                UiKit.OnPanel(b);
                UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0f, -214f), new Vector2(740f, Mathf.Min(520f, height - 214f - 200f)));
                b.alignment = TextAnchor.MiddleCenter;
            };
            host.PopupButton = (label, primary, y, action) => ToyKit.PopupAction(popup.Find("Panel"), label, ToyKit.Button(primary ? 1 : 0), Color.white, y, action);
            return host;
        }

        /// <summary>Draws the Bundles page the way the shop does (scene, ribbon, body, tab bar) on its own canvas, with the Meta canvas hidden so only the page is audited.</summary>
        static ShopScreen.Host ShowBundlesPage(int gems, int coins)
        {
            DropBundlesCanvas();
            MetaUiActive(false);
            GameSession.Progress.ResetAll();
            GameSession.Progress.AddGems(gems);
            GameSession.Progress.AddCoins(coins);
            Canvas canvas = UiKit.CreateCanvas("BundlesTestUi", 20);
            bundlesTestCanvas = canvas.gameObject;
            RectTransform screen = UiKit.SafeArea(canvas.transform);
            Text c, g, s;
            ShopChrome.BuildScene(screen, GameSession.Art, GameSession.Progress, out c, out g, out s);
            RectTransform body = ShopChrome.BuildBody(screen, "Bundles", ShopChrome.RibbonColor.Yellow);
            ShopScreen.Host host = BundlesTestHost(canvas, GameSession.Art);
            BundlesScreen.Build(body, GameSession.Progress, GameSession.Art, host, null);
            ShopChrome.BuildTabBar(screen, ShopChrome.TabBundles, null, null);
            return host;
        }

        static void ClickByName(string name)
        {
            foreach (Button b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (b.gameObject.activeInHierarchy && b.name.StartsWith(name)) { b.onClick.Invoke(); return; }
        }

        /// <summary>Clean wallet with a running timer offer (the play-time counter is ticked past the longest gap).</summary>
        static void TimerOfferState()
        {
            DropBundlesCanvas();
            MetaUiActive(true);
            GameSession.Progress.ResetAll();
            for (int i = 0; i < 70 && GameSession.Progress.CurrentTimerOffer(System.DateTime.UtcNow) == null; i++) GameSession.Progress.TickPlayTime(60);
        }

        IEnumerator BundlesAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();

            yield return CheckScreen("bundles-page-empty-wallet", () => ShowBundlesPage(0, 0), problems);
            yield return CheckScreen("bundles-page-rich", () => ShowBundlesPage(5000, 500), problems);
            yield return CheckScreen("bundles-not-enough-gems-popup", () => { ShowBundlesPage(0, 0); ClickByName("Buy"); }, problems);
            yield return CheckScreen("bundles-confirm-popup", () => { ShowBundlesPage(5000, 0); ClickByName("Buy"); }, problems);
            yield return CheckScreen("bundles-card-reveal", () => { ShowBundlesPage(5000, 0); ClickByName("Buy"); ClickByName("Buy for"); }, problems);   // "You got: name (rarity)" with the hero portrait
            yield return CheckScreen("bundles-card-reveal-long-name", () =>
            {
                ShopScreen.Host h = ShowBundlesPage(5000, 0);
                BundleDef four = null;
                foreach (BundleDef d in GameSession.Progress.Bundles.All) if (d.PowerUps.Count == 4) { four = d; break; }   // the longest "also in" line
                BundlesScreen.ShowReveal(h, four ?? GameSession.Progress.Bundles.All[0], CharacterDefs.Get("candygirl_u"));
            }, problems);
            yield return CheckScreen("bundles-card-reveal-no-hero-fallback", () => { ShopScreen.Host h = ShowBundlesPage(5000, 0); BundlesScreen.ShowReveal(h, GameSession.Progress.Bundles.All[0], null); }, problems);
            yield return CheckScreen("bundles-refreshed-with-coins", () => { ShowBundlesPage(5000, 500); ClickByName("RefreshCoins"); }, problems);
            yield return CheckScreen("bundles-refresh-toast-broke", () => { ShowBundlesPage(0, 0); ClickByName("RefreshGems"); }, problems);
            DropBundlesCanvas();
            MetaUiActive(true);

            // Timer offer: the corner button on the home and map screens, and its popup.
            yield return CheckScreen("timer-offer-home", () => { TimerOfferState(); meta.ShowMenu(); }, problems);
            yield return CheckScreen("timer-offer-map", () => { TimerOfferState(); meta.ShowMap(); }, problems);
            yield return CheckScreen("timer-offer-popup", () => { TimerOfferState(); meta.ShowMenu(); ClickByName("BundleOffer"); }, problems);
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

            // The editor runs on a shared PC: a single OS hitch can add a few ms to one frame. A real regression is slow in EVERY window, so the
            // best of up to three 600-frame windows is judged (average and allocations are judged on that same window).
            double bestMax = double.MaxValue, bestAvg = 0.0, bestAlloc = 0.0;
            const int measured = 600;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                rc.Stats.Reset();
                System.GC.Collect();
                long before = System.GC.GetAllocatedBytesForCurrentThread();
                for (int k = 0; k < measured; k++) { frame++; yield return null; }
                long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
                Debug.Log("[CUX-PERF] window " + attempt + " avg " + rc.Stats.AverageMs.ToString("F3") + " ms, max " + rc.Stats.MaxMs.ToString("F3") + " ms, alloc " + (allocated / (double)measured).ToString("F1") + " B/frame");
                if (rc.Stats.MaxMs < bestMax) { bestMax = rc.Stats.MaxMs; bestAvg = rc.Stats.AverageMs; bestAlloc = allocated / (double)measured; }
                if (bestMax <= 6.0) break;
            }
            Assert.LessOrEqual(bestAvg, 2.0, "average script time per frame (CUX-1)");
            Assert.LessOrEqual(bestMax, 6.0, "worst script time per frame (CUX-1), best of 3 windows");
            Assert.LessOrEqual(bestAlloc, AllocBudget, "managed allocation per frame (CUX-1)");
            Assert.IsFalse(rc.Paused, "the run was not interrupted");
        }

        const double AllocBudget = 1024.0;
    }
}
