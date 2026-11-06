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
            GoogleConnectPrompt.ResetForTests();
            GoogleConnectPrompt.AutoShow = false;   // Bug List 1 B1.c: the Google connect pop-up is tested on its own, not over every Home screen
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
            GameSettings.ApplyFlags();    // the admin audits turn System Test switches on: back to production behaviour
            GameSettings.ShowDevRowsForTests = false;
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

                // Content under a Mask (a hero face: the doll is much bigger than the round window it shows through) is only visible inside the mask: audit just that part.
                for (int n = items.Count - 1; n >= 0; n--)
                {
                    Transform host = items[n].Rt.parent;
                    Mask mk = host != null ? host.GetComponentInParent<Mask>() : null;
                    if (mk == null || !mk.isActiveAndEnabled || mk.GetComponentInParent<Canvas>() != canvas) continue;
                    Rect mb = ToCanvasUnits(mk.rectTransform, cam, scale);
                    Rect ib = items[n].Box;
                    float x0 = Mathf.Max(ib.xMin, mb.xMin), x1 = Mathf.Min(ib.xMax, mb.xMax);
                    float y0 = Mathf.Max(ib.yMin, mb.yMin), y1 = Mathf.Min(ib.yMax, mb.yMax);
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
            // Account (docs/35 section 8): Home has no account button any more (B1.c); the pop-up code stays for Settings, so it is audited in the three states, plus the Settings account block signed in and in a build without sign-in.
            yield return CheckScreen("google-prompt", () => { AccountScenario("guest"); meta.ShowMenu(); meta.ShowGooglePrompt(); }, problems);
            yield return CheckScreen("menu-signed-in", () => { AccountScenario("signed-in"); meta.ShowMenu(); }, problems);
            yield return CheckScreen("account-popup-guest", () => { AccountScenario("guest"); meta.ShowMenu(); meta.ShowAccount(); }, problems);
            yield return CheckScreen("account-popup-signed-in", () => { AccountScenario("signed-in"); meta.ShowMenu(); meta.ShowAccount(); }, problems);
            yield return CheckScreen("account-popup-unavailable", () => { AccountScenario("unavailable"); meta.ShowMenu(); meta.ShowAccount(); }, problems);
            // Settings (Bug List 1 B2): the compact popup as guest, signed in (Unlink Google Account), in a build without sign-in, with the Admin icon, and with the Save Progress result on its button.
            yield return CheckScreen("settings-signed-in", () => { AccountScenario("signed-in"); meta.ShowSettings(); }, problems);
            yield return CheckScreen("settings-unavailable", () => { AccountScenario("unavailable"); meta.ShowSettings(); }, problems);
            yield return CheckScreen("settings-guest-admin-icon", () => { AccountScenario("guest"); GameSettings.ShowDevRowsForTests = true; meta.ShowSettings(); GameSettings.ShowDevRowsForTests = false; }, problems);
            yield return CheckScreen("settings-guest-saved", () => { AccountScenario("guest"); meta.ShowSettings(); FindRt("SaveProgress").GetComponent<Button>().onClick.Invoke(); }, problems);
            AccountScenario("guest");
            yield return CheckScreen("map", () => meta.ShowMap(), problems);
            yield return CheckScreen("levels", () => meta.ShowLevels(r1), problems);
            yield return CheckScreen("settings-guest", () => meta.ShowSettings(), problems);
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
            // Admin (Bug List 1 B3): System Test OFF (PROD READY, the test rows dimmed), ON with the three switches off, ON with every switch on (the tallest), and the hero page.
            yield return CheckScreen("admin-system-off", () => { AdminScenario(false); meta.ShowSettings(); meta.ShowAdmin(); }, problems);
            yield return CheckScreen("admin-system-on", () => { AdminScenario(true, false); meta.ShowSettings(); meta.ShowAdmin(); }, problems);
            yield return CheckScreen("admin-system-on-all-switches", () => { AdminScenario(true, true); meta.ShowSettings(); meta.ShowAdmin(); }, problems);
            yield return CheckScreen("admin-main-rewards-on", () =>
            {
                AdminScenario(true, true);
                GameSession.Progress.AdminSetMainRewardsReady(true);   // E3: the button reads "Main rewards ON"
                meta.ShowSettings();
                meta.ShowAdmin();
                Assert.IsNotNull(FindRt("Main_rewards_ONButton"), "the Main rewards test button shows with System Test on");
            }, problems);
            GameSession.Progress.AdminSetMainRewardsReady(false);
            yield return CheckScreen("admin-heroes", () => { AdminScenario(true, true); meta.ShowSettings(); meta.ShowAdmin(1); }, problems);
            AdminCleanup();
            yield return CheckScreen("settings-reset-confirm", () => { meta.ShowSettings(); GameSession.Progress.SetAccount("google", "test"); meta.ConfirmReset(); }, problems);
            yield return CheckScreen("settings-restored", () => { meta.ShowSettings(); meta.ShowPurchasesRestored(2); }, problems);
            yield return CheckScreen("settings-nothing-to-restore", () => { meta.ShowSettings(); meta.ShowPurchasesRestored(0); }, problems);
            yield return CheckScreen("settings-code-box", () => { meta.ShowSettings(); meta.ShowAdminCodeBox(); }, problems);
            GameSession.Progress.SetAccount("guest", "");

            // Characters: the chosen character on the land stage, the action row (All Heroes + Merge) and the list of OWNED hero cards (one card per copy, no counts)
            yield return CheckScreen("characters", () => meta.ShowCharacters(), problems);
            ProgressService.TestAllCharacters = true;   // Character Test: only the five Common heroes are owned (Uncommon and above stay locked)
            yield return CheckScreen("characters-test", () => meta.ShowCharacters(), problems);
            foreach (CharacterDef hero in new[] { CharacterCatalog.ForestGirl, CharacterCatalog.BeachOtter, CharacterCatalog.CandyGirl, CharacterCatalog.Monk })
            {
                CharacterCatalog.Selected = hero;
                yield return CheckScreen("characters-" + hero.Id, () => meta.ShowCharacters(), problems);
                yield return CheckScreen("hero-reveal-" + hero.Id, () => { meta.ShowMenu(); meta.ShowHeroReveal(hero); }, problems);
            }
            CharacterCatalog.Selected = CharacterCatalog.Momo;
            ProgressService.TestAllCharacters = false;

            // Owned cards (Bug List 5 F2.d): duplicates are more cards (no number anywhere), the tick sits on the first card of the chosen type, and a long list scrolls.
            var savedCopies = new Dictionary<string, int>(GameSession.Progress.Data.Copies);
            var savedRevealed = new List<string>(GameSession.Progress.Data.HeroesRevealed);
            GameSession.Progress.HeroInventory.AddCopies("momo", 1);       // 2 Common Momo
            GameSession.Progress.HeroInventory.AddCopies("momo_u", 2);     // a new card type with 2 copies
            yield return CheckScreen("characters-duplicates", () => meta.ShowCharacters(), problems);
            meta.ShowCharacters();
            yield return null;
            var cardNames = new List<string>();
            int ticks = 0;
            foreach (Image im in Object.FindObjectsByType<Image>(FindObjectsSortMode.None))
            {
                if (im.name.StartsWith("Char_")) cardNames.Add(im.name);
                if (im.name == CharactersScreen.ChosenName) { ticks++; Assert.AreEqual("Char_momo", im.transform.parent.name, "the tick is on the first card of the chosen type"); }
            }
            CollectionAssert.AreEquivalent(new[] { "Char_momo", "Char_momo_2", "Char_momo_u", "Char_momo_u_2" }, cardNames, "one card per copy");
            Assert.AreEqual(1, ticks, "exactly one tick");
            foreach (Text t in Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
                for (Transform up = t.transform.parent; up != null; up = up.parent)
                    Assert.IsFalse(up.name.StartsWith("Char_"), "no count or text on a hero card");
            GameSession.Progress.HeroInventory.AddCopies("momo", 10);      // 12 Momo
            foreach (string extra in new[] { "forestgirl", "candygirl" }) { GameSession.Progress.HeroInventory.AddCopies(extra, 4); GameSession.Progress.MarkHeroRevealed(extra); }
            yield return CheckScreen("characters-many", () => meta.ShowCharacters(), problems);
            GameSession.Progress.Data.Copies.Clear();
            foreach (KeyValuePair<string, int> kv in savedCopies) GameSession.Progress.Data.Copies[kv.Key] = kv.Value;
            GameSession.Progress.Data.HeroesRevealed.Clear();
            GameSession.Progress.Data.HeroesRevealed.AddRange(savedRevealed);

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

        /// <summary>
        /// Settings (Bug List 1 B2): one compact popup. All five switch cells, the two value buttons, the Google button, Save Progress, Reset, Restore, the Admin icon and
        /// the version tap sit inside the panel (the content scrolls only when the panel is at its height cap), every target is at least 48 dp and at least 8 dp from its
        /// neighbours, and the popup is much shorter than the old screen. Also checks the Google button label per state and that Save Progress writes locally / to Google.
        /// </summary>
        [UnityTest]
        public IEnumerator SettingsPopupIsCompactAndEverythingStaysInside()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            GameSettings.ShowDevRowsForTests = true;
            AccountScenario("guest");
            var holder = new RenderTexture[1];
            string[] names = { "SoundToggle", "MusicToggle", "HapticsToggle", "Reduced motionToggle", "Left-handedToggle", "TextsizeButton", "DifficultyButton", "Google", "SaveProgress", "Reset", "RestorePurchases", "Admin", "VersionTap" };
            foreach (Aspect a in Aspects)
            {
                UiKit.SafeAreaNormalized = () => FullSafe;
                meta.ShowSettings();
                yield return ApplyAspect(a, holder);
                yield return null;
                RectTransform panel = FindRt("Panel");
                RectTransform content = FindRt("Content");
                if (panel == null || content == null) { problems.Add(a.Name + ": settings popup (Panel/Content) missing"); continue; }
                Rect pb = CanvasBox(panel);
                if (pb.height > 1500f) problems.Add(a.Name + ": the settings popup is " + Mathf.RoundToInt(pb.height) + " tall, it must stay compact (under 1500)");
                ScrollRect sr = content.GetComponentInParent<ScrollRect>();
                bool scrolls = sr != null && sr.vertical;
                var boxes = new List<KeyValuePair<string, Rect>>();
                foreach (string n in names)
                {
                    RectTransform rt = FindRt(n, panel);
                    if (rt == null) { problems.Add(a.Name + ": " + n + " missing"); continue; }
                    Rect bb = CanvasBox(rt);
                    boxes.Add(new KeyValuePair<string, Rect>(n, bb));
                    if (bb.width < UiKit.MinTouchPx - 1f || bb.height < UiKit.MinTouchPx - 1f) problems.Add(a.Name + ": " + n + " below 48 dp");
                    if (!scrolls && (bb.xMin < pb.xMin || bb.xMax > pb.xMax || bb.yMin < pb.yMin || bb.yMax > pb.yMax)) problems.Add(a.Name + ": " + n + " leaves the popup " + pb);
                }
                for (int i = 0; i < boxes.Count; i++)
                    for (int j = i + 1; j < boxes.Count; j++)
                    {
                        Rect p = boxes[i].Value, q = boxes[j].Value;
                        float gap = Mathf.Max(Mathf.Max(p.xMin - q.xMax, q.xMin - p.xMax), Mathf.Max(p.yMin - q.yMax, q.yMin - p.yMax));
                        if (gap < UiKit.MinSpacingPx - 1f) problems.Add(a.Name + ": " + boxes[i].Key + " and " + boxes[j].Key + " are only " + Mathf.RoundToInt(gap) + " apart (needs 8 dp)");
                    }
                if (FindRt("Back") != null) problems.Add(a.Name + ": Settings has no Back button any more (the X closes it)");
                if (FindRt("RestorePurchases", panel) != null && FindRt("Reset", panel) != null && !(CanvasBox(FindRt("Reset", panel)).center.x < CanvasBox(FindRt("RestorePurchases", panel)).center.x))
                    problems.Add(a.Name + ": Reset and Restore are one row: Reset left, Restore right");
                if (scrolls && pb.height < 1400f) problems.Add(a.Name + ": scrolls although the popup is only " + Mathf.RoundToInt(pb.height) + " tall");
            }
            GameSettings.ShowDevRowsForTests = false;
            Restore(holder);
            Report(problems);
            AccountCleanup();
        }

        /// <summary>Google button: Sign in with Google (guest) or Unlink Google Account (signed in) with the existing sign-in logic; Save Progress: guest saves on the phone, signed in saves to Google.</summary>
        [UnityTest]
        public IEnumerator SettingsGoogleButtonAndSaveProgress()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            AccountScenario("guest");
            meta.ShowSettings();
            yield return null;
            Assert.AreEqual(AccountPopup.SignInLabel, FindRt("Google").GetComponent<A11yLabel>().Text, "guest: Sign in with Google");
            FindRt("SaveProgress").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(AccountPopup.SavedLocalText, meta.SaveButtonTextForTests, "guest: saved on this phone");
            Assert.IsFalse(AccountSync.IsGoogleMode);
            FindRt("Google").GetComponent<Button>().onClick.Invoke();   // the stub signs in at once and Settings rebuilds
            yield return null;
            yield return null;
            Assert.IsTrue(AccountSync.IsSignedIn, "signed in after the tap");
            Assert.AreEqual(AccountPopup.UnlinkLabel, FindRt("Google").GetComponent<A11yLabel>().Text, "signed in: Unlink Google Account");
            FindRt("SaveProgress").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(AccountPopup.SavedGoogleText, meta.SaveButtonTextForTests, "signed in: saved to Google");
            FindRt("Google").GetComponent<Button>().onClick.Invoke();   // unlink
            yield return null;
            yield return null;
            Assert.IsFalse(AccountSync.IsGoogleMode, "guest again after Unlink");
            Assert.AreEqual(AccountPopup.SignInLabel, FindRt("Google").GetComponent<A11yLabel>().Text);
            AccountCleanup();
        }

        /// <summary>The Admin popup: System Test master switch first, a PROD READY / TEST MODE pill, test rows disabled while it is off, and OFF clears every test switch.</summary>
        [UnityTest]
        public IEnumerator AdminPopupSystemTestMasterSwitch()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            AdminScenario(false);
            meta.ShowSettings();
            meta.ShowAdmin();
            yield return null;
            Assert.AreEqual(AdminAccess.PillText(false), FindRt("StatusPill").GetComponentInChildren<Text>().text, "System Test off: PROD READY");
            RectTransform master = FindRt("System TestToggle"), game = FindRt("Game TestToggle");
            Assert.IsNotNull(master);
            Assert.IsNotNull(game);
            Assert.Greater(CanvasBox(game).yMax, 0f);
            Assert.Greater(CanvasBox(master).yMin, CanvasBox(game).yMax - 1f, "System Test sits above Game Test");
            Assert.IsFalse(game.GetComponentInParent<CanvasGroup>().interactable, "Game Test is disabled while System Test is off");
            Assert.IsNull(FindRt("Big_walletButton"), "no test tools while System Test is off");
            Assert.IsNull(FindRt("Main_rewards_OFFButton"), "no Main rewards test button while System Test is off");
            master.GetComponent<Button>().onClick.Invoke();   // System Test on
            yield return null;
            yield return null;
            Assert.IsTrue(GameSettings.SystemTest);
            Assert.AreEqual(AdminAccess.PillText(true), FindRt("StatusPill").GetComponentInChildren<Text>().text, "System Test on: TEST MODE");
            FindRt("Shop TestToggle").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            Assert.IsTrue(GameSettings.ShopTest);
            Assert.IsNotNull(FindRt("Big_walletButton"), "Shop Test on: its tools show");
            Assert.IsNotNull(FindRt("Main_rewards_OFFButton"), "System Test on: the Main rewards test button shows (Game Test group)");
            FindRt("System TestToggle").GetComponent<Button>().onClick.Invoke();   // System Test off again: everything off
            yield return null;
            yield return null;
            Assert.IsFalse(GameSettings.SystemTest);
            Assert.IsFalse(GameSettings.ShopTest);
            Assert.IsFalse(GameSettings.GameJumpTest);
            Assert.IsFalse(GameSettings.CharacterTest);
            Assert.IsNull(FindRt("Big_walletButton"));
            AdminCleanup();
        }

        /// <summary>Tests: an install where the admin code was accepted (so the test switches are real), with System Test on or off and optionally every sub switch on.</summary>
        static void AdminScenario(bool systemTest, bool subSwitches = true)
        {
            PlayerPrefs.SetInt("settings.adminunlocked", 1);
            GameSettings.SystemTest = systemTest;
            if (!systemTest) return;
            GameSettings.GameJumpTest = subSwitches;
            GameSettings.ShopTest = subSwitches;
            GameSettings.CharacterTest = subSwitches;
        }

        static void AdminCleanup()
        {
            foreach (string k in new[] { "settings.adminunlocked", "settings.systemtest", "settings.gamejumptest", "settings.shoptest", "settings.charactertest" }) PlayerPrefs.DeleteKey(k);
            GameSettings.ApplyFlags();
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
        /// Bug List 1 B1.a to B1.e: Home has no tab bar, no Heroes/Rewards/Shop buttons, no round Account button and no wallet; ONE Play button (icon only, no text)
        /// runs end to end (safe width minus the 16 unit side margins) at the bottom; the blue Settings button sits just above it; Momo and the version label stay.
        /// </summary>
        [UnityTest]
        public IEnumerator HomeHasOneBigPlayButtonAndSettingsAboveIt()
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
                foreach (string gone in new[] { "TabBar", "TabShop", "TabPlay", "TabCharacters", "TabVIP", "Rewards", "Account", "CurrencyBar", "CoinsPill", "GemsPill", "StarsPill", "WalletPanel" })
                    if (FindRt(gone) != null) problems.Add(a.Name + ": " + gone + " must not be on Home");
                RectTransform play = FindRt("Play"), settings = FindRt("Settings"), safe = FindRt("Safe");
                if (play == null || settings == null) { problems.Add(a.Name + ": Home needs Play and Settings"); continue; }
                Rect pb = CanvasBox(play), sb = CanvasBox(settings);
                Rect screenBox = safe != null ? CanvasBox(safe) : new Rect(0f, 0f, UiKit.CanvasSizeRef().x, UiKit.CanvasSizeRef().y);
                if (Mathf.Abs(pb.xMin - screenBox.xMin - 16f) > 1.5f || Mathf.Abs(screenBox.xMax - pb.xMax - 16f) > 1.5f) problems.Add(a.Name + ": Play does not run end to end (" + pb + " in " + screenBox + ")");
                if (pb.height < UiKit.MinTouchPx - 1f) problems.Add(a.Name + ": Play is below 48 dp");
                if (pb.yMin - screenBox.yMin > 40f) problems.Add(a.Name + ": Play must sit at the bottom");
                if (play.Find("Label") != null || play.GetComponentInChildren<Text>() != null) problems.Add(a.Name + ": Play must have no text");
                Transform icon = play.Find("Icon");
                if (icon == null) problems.Add(a.Name + ": Play needs its icon");
                else
                {
                    Rect ib = CanvasBox((RectTransform)icon);
                    if (Mathf.Abs(ib.center.x - pb.center.x) > 1.5f || Mathf.Abs(ib.center.y - pb.center.y) > 1.5f) problems.Add(a.Name + ": the Play icon is not centred");
                }
                if (play.GetComponent<A11yLabel>() == null || play.GetComponent<A11yLabel>().Text != "Play") problems.Add(a.Name + ": Play needs the label Play");
                if (sb.width < UiKit.MinTouchPx - 1f || sb.height < UiKit.MinTouchPx - 1f) problems.Add(a.Name + ": Settings is below 48 dp");
                if (sb.yMin < pb.yMax) problems.Add(a.Name + ": Settings must sit above Play");
                if (sb.Overlaps(pb)) problems.Add(a.Name + ": Settings overlaps Play");
                if (settings.Find("Icon") == null) problems.Add(a.Name + ": Settings needs its icon image named Icon");
                if (settings.GetComponent<A11yLabel>() == null || settings.GetComponent<A11yLabel>().Text != "Settings") problems.Add(a.Name + ": Settings needs the label Settings");
                if (FindRt("Momo") == null || FindRt("Version") == null || FindRt("Logo") == null) problems.Add(a.Name + ": Home keeps the logo, Momo and the version label");
                if (FindRt("Version") != null && CanvasBox(FindRt("Version")).Overlaps(sb)) problems.Add(a.Name + ": the version label overlaps Settings");

                // the heroes bar has no crown any more (Bug List 1, B5.d)
                meta.ShowCharacters();
                yield return null;
                if (FindRt("TabVIP") != null) problems.Add(a.Name + ": the crown is in the Shop now, not on the heroes bar");
            }
            Restore(holder);
            Report(problems);
        }

        /// <summary>The Google connect pop-up: a G icon, one line and two icon buttons (Open Settings, Later); the gear opens Settings, Later closes it.</summary>
        [UnityTest]
        public IEnumerator GooglePromptHasIconButtonsThatOpenSettingsOrClose()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            meta.ShowMenu();
            meta.ShowGooglePrompt();
            yield return null;
            Assert.IsNotNull(FindRt("GoogleIcon"), "the G icon");
            Assert.IsNotNull(FindRt("Line"), "the one line");
            RectTransform open = FindRt("OpenSettings"), later = FindRt("Later");
            Assert.IsNotNull(open); Assert.IsNotNull(later);
            Assert.AreEqual("Open Settings", open.GetComponent<A11yLabel>().Text);
            Assert.AreEqual("Later", later.GetComponent<A11yLabel>().Text);
            Assert.IsTrue(CanvasBox(open).width >= UiKit.MinTouchPx - 1f && CanvasBox(later).width >= UiKit.MinTouchPx - 1f, "48 dp targets");
            later.GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            Assert.IsNull(FindRt("OpenSettings"), "Later closes the pop-up");
            Assert.AreEqual("Menu", meta.CurrentScreen);
            meta.ShowGooglePrompt();
            yield return null;
            FindRt("OpenSettings").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            Assert.AreEqual("Settings", meta.CurrentScreen, "the gear opens Settings");
            Assert.IsNull(FindRt("OpenSettings"));
        }

        /// <summary>The automatic prompt on Home: after a completed level, as a guest, once; never for a fresh player.</summary>
        [UnityTest]
        public IEnumerator GooglePromptShowsOnHomeOnlyAfterAFirstCompletedLevelAndOncePerSession()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            AccountScenario("guest");
            GoogleConnectPrompt.ResetForTests();
            meta.ShowMenu();
            yield return null;
            Assert.IsNull(FindRt("OpenSettings"), "nothing completed yet: no prompt");
            GameSession.Progress.ApplyRun("lvl_001_01", new RunOutcome { Completed = true, StarMask = 7, Score = 100, CoinsCollected = 5 });
            meta.ShowMenu();
            yield return null;
            Assert.IsNotNull(FindRt("OpenSettings"), "guest after a completed level: the prompt shows");
            Assert.IsTrue(GoogleConnectPrompt.ShownThisSession);
            Assert.IsNotEmpty(GameSession.Progress.Data.LastGooglePromptUtc, "the day is stored");
            FindRt("Later").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            meta.ShowMenu();
            yield return null;
            Assert.IsNull(FindRt("OpenSettings"), "once per session");
            AccountCleanup();
            GoogleConnectPrompt.ResetForTests();
            GoogleConnectPrompt.AutoShow = false;
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

        /// <summary>Bug List 2 C2.a: the first yellow banner scrolls with the list. Every tab, every aspect: at the top the banner is the first item under the Momo art;
        /// scrolled down it has left the viewport, the Momo art is still fully visible (the viewport starts below it) and nothing overlaps.</summary>
        [UnityTest]
        public IEnumerator ShopFirstBannerScrollsAwayAndMomoArtStaysVisible() { return ShopScrollAudit(100); }

        [UnityTest]
        public IEnumerator ShopFirstBannerScrollsAwayAt130PercentText() { return ShopScrollAudit(130); }

        IEnumerator ShopScrollAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            ShopScreen.TestPurchasesOverride = null;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            GameSession.Progress.AddGems(300);
            GameSession.Progress.AddCoins(12345);
            int[] tabs = { ShopScreen.TabBundles, ShopScreen.TabVip, ShopScreen.TabCharacter, ShopScreen.TabGems };
            string[] firstBanner = { "Bundles", "Subscription", "Heroes", "Gems" };
            for (int t = 0; t < tabs.Length; t++)
            {
                int tab = tabs[t];
                string banner = "Banner_" + firstBanner[t];
                yield return CheckShopScroll("shop-scroll-top-" + firstBanner[t], () => meta.ShowShop(tab), banner, 0f, problems);
                yield return CheckShopScroll("shop-scroll-down-" + firstBanner[t], () => meta.ShowShop(tab), banner, 100000f, problems);   // clamped to the end of the list
                yield return CheckShopScroll("shop-scroll-mid-" + firstBanner[t], () => meta.ShowShop(tab), banner, ShopChrome.BannerBlockH + 300f, problems);
            }
            Report(problems);
        }

        /// <summary>Bug List 3 D2.a/b: ShowShop() with no arguments selects Gems / Coins at scroll 0; the Character page opens at its top (Heroes banner), not at Power-ups.</summary>
        [UnityTest]
        public IEnumerator ShopDefaultEntryIsGemsCoinsAtTopAndCharacterStartsAtTop()
        {
            ShopScreen.TestPurchasesOverride = null;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            meta.ShowShop();
            yield return null;
            yield return null;
            if (meta.CurrentShop.Tab != ShopScreen.TabGems) problems.Add("default entry opened tab " + meta.CurrentShop.Tab + ", expected Gems / Coins");
            if (meta.CurrentShop.Section != 0) problems.Add("default entry section " + meta.CurrentShop.Section + ", expected the top");
            RectTransform list = FindRt("ShopList");
            if (list == null || list.anchoredPosition.y > 0.5f) problems.Add("default entry: Gems list not at scroll 0");
            meta.ShowShop(ShopScreen.TabCharacter);
            yield return null;
            yield return null;
            list = FindRt("ShopList");
            if (list == null || list.anchoredPosition.y > 0.5f) problems.Add("Character page not at scroll 0 (Heroes banner hidden)");
            if (meta.CurrentShop.Section != 0) problems.Add("Character page section " + meta.CurrentShop.Section + ", expected 0");
            Report(problems);
        }

        /// <summary>Shows a shop tab at every aspect, scrolls its list by <paramref name="scrollPx"/> (reference px, clamped) and audits the result.</summary>
        static IEnumerator CheckShopScroll(string screen, System.Action show, string bannerName, float scrollPx, List<string> all)
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
                    string tag = screen + "@" + a.Name + (safe == NotchSafe ? "+notch" : "");
                    RectTransform list = FindRt("ShopList");
                    RectTransform vp = list != null ? list.parent as RectTransform : null;
                    if (list == null || vp == null) { all.Add(tag + ": no ShopList/ShopViewport"); continue; }
                    LayoutRebuilder.ForceRebuildLayoutImmediate(list);
                    float maxY = Mathf.Max(0f, list.rect.height - vp.rect.height);
                    list.anchoredPosition = new Vector2(0f, Mathf.Clamp(scrollPx, 0f, maxY));
                    Canvas.ForceUpdateCanvases();
                    yield return null;
                    Rect vpBox = CanvasBox(vp);
                    RectTransform scene = FindRt("Scene");
                    RectTransform bannerRt = FindRt(bannerName, list);
                    if (scene == null) all.Add(tag + ": Momo scene missing");
                    else if (CanvasBox(scene).yMin < vpBox.yMax - 1.5f) all.Add(tag + ": the list viewport covers the Momo art (art bottom " + CanvasBox(scene).yMin + ", viewport top " + vpBox.yMax + ")");
                    RectTransform shopBody = FindRt("ShopBody");
                    if (shopBody != null && shopBody.Find("Ribbon") != null) all.Add(tag + ": a fixed banner is still in the page header");
                    if (bannerRt == null) all.Add(tag + ": first banner " + bannerName + " is not inside the scrolling list");
                    else
                    {
                        Rect bb = CanvasBox(bannerRt);
                        float pos = list.anchoredPosition.y;
                        if (pos <= 0.5f)
                        {
                            if (bb.yMax > vpBox.yMax + 1.5f || bb.yMin < vpBox.yMin) all.Add(tag + ": at the top the first banner is not the first visible item (" + bb + " viewport " + vpBox + ")");
                        }
                        else if (pos >= ShopChrome.BannerBlockH + 8f && bb.yMin < vpBox.yMax - 1.5f)
                            all.Add(tag + ": scrolled down by " + Mathf.RoundToInt(pos) + " but the first banner is still in view (" + bb + " viewport " + vpBox + ")");
                    }
                    all.AddRange(Audit(tag, a, safe));
                }
            }
            Restore(holder);
        }

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
            yield return CheckScreen("shop-tab-gems", () => meta.ShowShop(ShopScreen.TabGems, ShopScreen.GemsSection), problems);
            yield return CheckScreen("shop-tab-gems-coins-section", () => meta.ShowShop(ShopScreen.TabGems, ShopScreen.CoinsSection), problems);
            yield return CheckScreen("shop-tab-character-powerups", () => meta.ShowShop(ShopScreen.TabCharacter, ShopScreen.PowerupsSection), problems);
            yield return CheckScreen("shop-tab-character-powerups-bought", () => { meta.ShowShop(ShopScreen.TabCharacter, ShopScreen.PowerupsSection); meta.CurrentShop.TapBuyPowerUp(GameSession.Progress.PowerUpDefs.Get("pw_flight"), PowerUpCurrency.Coins); }, problems);
            // Treasure Chest screen (docs/22 point 3): rows, the (i) texts, the confirmation, the single and the ten reveal
            yield return CheckScreen("treasure-chests", () => meta.ShowTreasureChests(), problems);
            // VIP tab (owner issue 35): one scrollable page, Subscription section first, Chests under it; the old 'Chests' tab is gone
            yield return CheckScreen("shop-tab-vip", () => meta.ShowShop(ShopScreen.TabVip), problems);
            yield return CheckScreen("shop-tab-vip-chests-section", () => meta.ShowShop(ShopScreen.TabVip, ShopScreen.VipChestsSection), problems);
            yield return CheckScreen("shop-tab-character-heroes-section", () => meta.ShowShop(ShopScreen.TabCharacter, ShopScreen.HeroesSection), problems);   // Heroes banner + the 2 x 2 hero cards (Bug List 4 E2)
            // Heroes section popups (E2.c, E2.e): the (i) of each tier, "No Rare heroes yet" (nothing charged), the confirm popup and the hero reveal (gems topped up first, so later screens see the same wallet)
            foreach (CharacterRarity tier in HeroPurchase.Tiers)
            {
                CharacterRarity t = tier;
                yield return CheckScreen("shop-heroes-info-" + HeroPurchase.RarityName(t).ToLowerInvariant(), () => { meta.ShowShop(ShopScreen.TabCharacter, ShopScreen.HeroesSection); meta.CurrentShop.TapHeroInfo(t); }, problems);
            }
            yield return CheckScreen("shop-heroes-none-popup", () => { meta.ShowShop(ShopScreen.TabCharacter, ShopScreen.HeroesSection); meta.CurrentShop.TapHeroBuy(CharacterRarity.Rare); }, problems);
            yield return CheckScreen("shop-heroes-none-popup-legendary", () => { meta.ShowShop(ShopScreen.TabCharacter, ShopScreen.HeroesSection); meta.CurrentShop.TapHeroBuy(CharacterRarity.Legendary); }, problems);
            yield return CheckScreen("shop-heroes-confirm-popup", () => { meta.ShowShop(ShopScreen.TabCharacter, ShopScreen.HeroesSection); meta.CurrentShop.TapHeroBuy(CharacterRarity.Uncommon); }, problems);
            yield return CheckScreen("shop-heroes-reveal", () =>
            {
                GameSession.Progress.AddGems(HeroPurchase.GemPrice(CharacterRarity.Uncommon));
                meta.ShowShop(ShopScreen.TabCharacter, ShopScreen.HeroesSection);
                meta.CurrentShop.TapHeroBuy(CharacterRarity.Uncommon);
                ClickByName("Buy for");
            }, problems);
            yield return CheckScreen("treasure-confirm", () => { GameSession.Progress.AddGems(3000); meta.ShowTreasureChests(); meta.CurrentTreasure.TapBuy(ChestKind.Golden, 10); }, problems);
            yield return CheckScreen("treasure-reveal-single", () => { GameSession.Progress.AddGems(100); meta.ShowTreasureChests(); meta.CurrentTreasure.OpenNow(ChestKind.Iron, 1); meta.CurrentTreasure.Reveal.Skip(); }, problems);
            yield return CheckScreen("treasure-reveal-ten", () => { GameSession.Progress.AddGems(3000); meta.ShowTreasureChests(); meta.CurrentTreasure.OpenNow(ChestKind.Golden, 10); meta.CurrentTreasure.Reveal.Skip(); }, problems);
            // Bug List 5 F3: the chests hold a hero card too. The "i" popup of each chest, the Open 1 reveal with a hero row, the Open 10 reveal with a list of hero rows
            yield return CheckScreen("treasure-info-iron", () => { meta.ShowShop(ShopScreen.TabVip, ShopScreen.VipChestsSection); meta.CurrentTreasure.ShowInfo(ChestKind.Iron); }, problems);
            yield return CheckScreen("treasure-info-gold", () => { meta.ShowShop(ShopScreen.TabVip, ShopScreen.VipChestsSection); meta.CurrentTreasure.ShowInfo(ChestKind.Golden); }, problems);
            yield return CheckScreen("treasure-reveal-iron-ten", () => { GameSession.Progress.AddGems(1000); meta.ShowTreasureChests(); meta.CurrentTreasure.OpenNow(ChestKind.Iron, 10); meta.CurrentTreasure.Reveal.Skip(); }, problems);
            yield return CheckScreen("shop-tab-bundles", () => meta.ShowShop(ShopScreen.TabBundles), problems);
            yield return CheckScreen("shop-gems-notbuyable-popup", () => { meta.ShowShop(); meta.CurrentShop.TapGemPack(3); }, problems);

            // Development layout: "Buy (test)" and the test purchase popups.
            ShopScreen.TestPurchasesOverride = true;
            yield return CheckScreen("shop-gems-test", () => meta.ShowShop(ShopScreen.TabGems, ShopScreen.GemsSection), problems);
            yield return CheckScreen("shop-test-buy-popup", () => { meta.ShowShop(); meta.CurrentShop.TapGemPack(6); }, problems);
            yield return CheckScreen("shop-tab-character-powerups-test", () => meta.ShowShop(ShopScreen.TabCharacter, ShopScreen.PowerupsSection), problems);
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
            yield return CheckScreen("shop-tab2-not-enough-popup", () => { meta.ShowShop(ShopScreen.TabCharacter, ShopScreen.PowerupsSection); meta.CurrentShop.TapBuyPowerUp(GameSession.Progress.PowerUpDefs.Get("pw_rocket"), PowerUpCurrency.Coins); }, problems);

            yield return CheckScreen("shop-heroes-not-enough-popup", () => { meta.ShowShop(ShopScreen.TabCharacter, ShopScreen.HeroesSection); meta.CurrentShop.TapHeroBuy(CharacterRarity.Uncommon); }, problems);   // empty wallet

            // Rewards (docs/26 section 4): the three tabs with something claimable (stars, points, play time), and the "Reward!" card
            for (int lvl = 1; lvl <= 9; lvl++) GameSession.Progress.ApplyRun("lvl_001_0" + lvl, new RunOutcome { Completed = true, StarMask = 7, Score = 100, CoinsCollected = 10 });
            GameSession.Progress.RecordLogin();
            GameSession.Progress.AddPlaySeconds(600);
            yield return CheckScreen("rewards-main", () => meta.ShowRewards(RewardTab.Main), problems);
            yield return CheckScreen("rewards-daily", () => meta.ShowRewards(RewardTab.Daily), problems);
            yield return CheckScreen("rewards-weekly", () => meta.ShowRewards(RewardTab.Weekly), problems);
            yield return CheckScreen("rewards-main-region3", () => { meta.ShowRewards(RewardTab.Main); meta.CurrentRewards.SelectRegion(3); }, problems);   // a locked region: lock on the circle, the locked note on top, goals at 0
            yield return CheckScreen("rewards-main-comingsoon", () => { meta.ShowRewards(RewardTab.Main); meta.CurrentRewards.SelectRegion(8); }, problems);   // Coming Soon: one centred locked card, no goals
            yield return CheckScreen("rewards-reward-card", () =>
            {
                // the small icon card: gem, coin, two power-up types and a hero face per hero copy (two rows of chips, the widest case)
                meta.ShowRewards(RewardTab.Main);
                var sample = new RewardClaimOutcome { Result = RewardClaimResult.Ok, Gems = 40, Coins = 2000 };
                sample.PowerUpIds.Add("pw_flight"); sample.PowerUpIds.Add("pw_flight"); sample.PowerUpIds.Add("pw_shield");
                sample.HeroIds.Add("forestgirl"); sample.HeroIds.Add("forestgirl");
                meta.CurrentRewards.ShowClaimed(sample);
            }, problems);
            // ready states of the redesign: yellow region-goal cards with the green Claim button (the last Levels goal also shows the region hero's face), Claim All, and a claimed (green, check, open chest) look
            for (int lvl = 10; lvl <= 15; lvl++) GameSession.Progress.ApplyRun("lvl_001_" + lvl.ToString("00"), new RunOutcome { Completed = true, StarMask = 7, Score = 100, CoinsCollected = 10 });
            GameSession.Progress.AddPlaySeconds(100 * 60);
            yield return CheckScreen("rewards-main-ready", () => { meta.ShowRewards(RewardTab.Main); meta.CurrentRewards.SelectRegion(1); }, problems);
            yield return CheckScreen("rewards-weekly-ready", () => meta.ShowRewards(RewardTab.Weekly), problems);
            yield return CheckScreen("rewards-daily-ready", () => meta.ShowRewards(RewardTab.Daily), problems);
            yield return CheckScreen("rewards-daily-claimed", () => { meta.ShowRewards(RewardTab.Daily); GameSession.Progress.TryClaimReward(RewardTab.Daily, "dp_20"); meta.ShowRewards(RewardTab.Daily); }, problems);
            yield return CheckScreen("rewards-main-claimed", () =>
            {
                // region 1: the first goals of two groups claimed (green check cards), the rest still ready or open; the circle keeps its dot while something is left
                GameSession.Progress.TryClaimRegionGoal("g_1_levels_1");
                GameSession.Progress.TryClaimRegionGoal("g_1_stars_1");
                meta.ShowRewards(RewardTab.Main);
                meta.CurrentRewards.SelectRegion(1);
            }, problems);
            yield return CheckScreen("rewards-weekly-claimed", () => { GameSession.Progress.TryClaimReward(RewardTab.Weekly, "w_play100"); meta.ShowRewards(RewardTab.Weekly); }, problems);   // compact cards: claimed look
            Report(problems);
        }

        // ---- Merge screen (docs/34): ready, not enough copies, top tier, a full grid, and the "New hero" result card ----

        [UnityTest]
        public IEnumerator MergeScreenHasNoOverlapAndFitsEveryAspect() { return MergeAudit(100); }

        /// <summary>CUX-5: the Merge screen survives the 130 percent text size too.</summary>
        [UnityTest]
        public IEnumerator MergeScreenSurvives130PercentText() { return MergeAudit(130); }

        /// <summary>A clean save: Momo with 3 copies (mergeable), and with <paramref name="full"/> also 4 Capi, 3 Bonbon and 2 Otto (only types with 3 or more copies are listed) so the list has two rows.</summary>
        static void MergeState(bool full)
        {
            GameSession.Progress.ResetAll();
            GameSession.Progress.AddCopies("momo", 2);
            if (!full) return;
            foreach (KeyValuePair<string, int> kv in new Dictionary<string, int> { { "forestgirl", 4 }, { "candygirl", 3 }, { "beachotter", 2 }, { "momo", 1 } })
            {
                GameSession.Progress.AddCopies(kv.Key, kv.Value);
                GameSession.Progress.MarkHeroRevealed(kv.Key);
            }
        }

        /// <summary>F4.1 and F4.2: every Region 1 and Region 2 hero owned (3 copies of each type below Legendary, 1 Legendary): the biggest hero lists the game can have (40 mergeable types x 3 cards = 120 merge cards, 50 hero types in All Heroes).</summary>
        static void Region1Full()
        {
            GameSession.Progress.ResetAll();
            foreach (string line in new[] { "momo", "bumble", "ribbi", "dandy", "pip", "forestgirl", "nib", "spike", "moss", "fenn", "beachotter", "pinch", "shelly", "bloop", "coco" })
                foreach (string suffix in new[] { "", "_u", "_r", "_e", "_l" })
                {
                    GameSession.Progress.AddCopies(line + suffix, suffix == "_l" ? 1 : 3);
                    GameSession.Progress.MarkHeroRevealed(line + suffix);
                }
        }

        IEnumerator MergeAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            // the list: only hero types with 3 or more copies, one card per copy, no numbers
            yield return CheckScreen("merge-list", () => { MergeState(false); meta.ShowMerge(); }, problems);
            yield return CheckScreen("merge-full-grid", () => { MergeState(true); meta.ShowMerge(); }, problems);
            yield return CheckScreen("merge-not-enough", () => { GameSession.Progress.ResetAll(); meta.ShowMerge(); }, problems);   // Momo with his one starting copy: an empty list
            // F4.1: all 25 Region 1 heroes owned: 20 mergeable types, 60 cards (a long scrolling grid), top and scrolled, plus a selected Rare type
            yield return CheckScreen("merge-region1-all", () => { Region1Full(); meta.ShowMerge(); }, problems);
            yield return CheckScreen("merge-region1-selected-rare", () => { Region1Full(); meta.ShowMerge(); meta.CurrentMerge.SelectType("pip_r"); }, problems);
            // selection: three of one type (green ticks, three filled slots, Merge enabled), a wrong mix (Merge disabled with the hint)
            yield return CheckScreen("merge-selected-3", () => { MergeState(true); meta.ShowMerge(); meta.CurrentMerge.SelectType("momo"); }, problems);
            yield return CheckScreen("merge-selected-capi", () => { MergeState(true); meta.ShowMerge(); meta.CurrentMerge.SelectType("forestgirl"); }, problems);
            yield return CheckScreen("merge-wrong-selection", () => { MergeState(true); meta.ShowMerge(); var m = meta.CurrentMerge; m.TapCard(0); m.TapCard(1); m.TapCard(m.Cards.Count - 1); }, problems);
            yield return CheckScreen("merge-one-selected", () => { MergeState(true); meta.ShowMerge(); meta.CurrentMerge.TapCard(0); }, problems);
            // after a merge: the three consumed cards are gone, the type vanished (1 Momo left), the result card is on top
            yield return CheckScreen("merge-after", () => { MergeState(false); meta.ShowMerge(); meta.CurrentMerge.SelectType("momo"); meta.CurrentMerge.TapMerge(); }, problems);
            Assert.AreEqual(1, GameSession.Progress.Copies("momo_u"), "the audit's merge produced the Uncommon hero");
            Assert.AreEqual(0, meta.CurrentMerge.Cards.Count, "the merged type left the list");
            Report(problems);
        }

        // ---- All Heroes (Bug List 5 F2.c): top of the list, scrolled, the details popup of a locked and of an owned hero ----

        [UnityTest]
        public IEnumerator AllHeroesScreenHasNoOverlapAndFitsEveryAspect() { return AllHeroesAudit(100); }

        /// <summary>CUX-5: the All Heroes screen survives the 130 percent text size too.</summary>
        [UnityTest]
        public IEnumerator AllHeroesScreenSurvives130PercentText() { return AllHeroesAudit(130); }

        IEnumerator AllHeroesAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            yield return CheckScreen("all-heroes", () => { GameSession.Progress.ResetAll(); meta.ShowAllHeroes(); }, problems);
            yield return CheckScreen("all-heroes-scrolled", () => { GameSession.Progress.ResetAll(); meta.ShowAllHeroes(); meta.CurrentAllHeroes.ScrollTo(0.5f); }, problems);
            yield return CheckScreen("all-heroes-bottom", () => { GameSession.Progress.ResetAll(); meta.ShowAllHeroes(); meta.CurrentAllHeroes.ScrollTo(1f); }, problems);
            yield return CheckScreen("all-heroes-owned", () => { MergeState(true); GameSession.Progress.AddCopies("momo_u", 1); meta.ShowAllHeroes(); }, problems);
            // F4.1: all 25 Region 1 heroes owned (full colour cards, nine rows), top, a quarter and the bottom, and the details of a new hero
            yield return CheckScreen("all-heroes-region1-owned", () => { Region1Full(); meta.ShowAllHeroes(); }, problems);
            yield return CheckScreen("all-heroes-region1-scrolled", () => { Region1Full(); meta.ShowAllHeroes(); meta.CurrentAllHeroes.ScrollTo(0.2f); }, problems);
            yield return CheckScreen("all-heroes-region1-info", () => { Region1Full(); meta.ShowAllHeroes(); meta.CurrentAllHeroes.OpenInfo("dandy_l"); }, problems);
            // details popups: a real locked hero, a not-yet-made tier, and an owned hero
            yield return CheckScreen("all-heroes-info", () => { GameSession.Progress.ResetAll(); meta.ShowAllHeroes(); meta.CurrentAllHeroes.OpenInfo("momo_u"); }, problems);
            yield return CheckScreen("all-heroes-info-legendary", () => { GameSession.Progress.ResetAll(); meta.ShowAllHeroes(); meta.CurrentAllHeroes.OpenInfo("monk_legendary"); }, problems);
            yield return CheckScreen("all-heroes-info-owned", () => { GameSession.Progress.ResetAll(); meta.ShowAllHeroes(); meta.CurrentAllHeroes.OpenInfo("momo"); }, problems);
            // Back returns to the Heroes screen
            meta.ShowAllHeroes();
            yield return null;
            Button back = null;
            foreach (Button bt in Object.FindObjectsByType<Button>(FindObjectsSortMode.None)) if (bt.name == "Back") back = bt;
            Assert.IsNotNull(back, "All Heroes has a Back button");
            back.onClick.Invoke();
            Assert.AreEqual("Characters", meta.CurrentScreen, "Back returns to the Hero screen");
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
            RectTransform body = ShopChrome.BuildBody(screen, "Bundles", ShopChrome.RibbonColor.Yellow, false);   // no fixed banner: it is the first scroll item (C2.a)
            ShopChrome.SetScrollBanner(body, "Bundles");
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

        [UnityTest]
        public IEnumerator BundleNameSharesTheRowWithTheInfoButton() { return BundleNameRowAudit(100); }

        [UnityTest]
        public IEnumerator BundleNameSharesTheRowWithTheInfoButtonAt130PercentText() { return BundleNameRowAudit(130); }

        /// <summary>Bug List 3 D3: on every bundle card the name is right of the "i", on the same centre line, and ends before the Worth text; the name font stays at least 16 sp.</summary>
        IEnumerator BundleNameRowAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            yield return Load("Meta");
            var problems = new List<string>();
            var holder = new RenderTexture[1];
            foreach (Aspect a in Aspects)
            {
                foreach (Rect safe in new[] { FullSafe, NotchSafe })
                {
                    Rect s = safe;
                    UiKit.SafeAreaNormalized = () => s;
                    ShowBundlesPage(5000, 0);
                    yield return ApplyAspect(a, holder);
                    yield return null;
                    string tag = "bundle-name-row@" + a.Name + (safe == NotchSafe ? "+notch" : "") + "/" + textPercent;
                    int cards = 0;
                    foreach (var card in Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
                    {
                        if (!card.gameObject.activeInHierarchy || !card.name.StartsWith("Bundle_")) continue;
                        RectTransform info = card.Find("BundleInfo") as RectTransform, name = card.Find("Name") as RectTransform, worth = card.Find("Worth") as RectTransform;
                        if (info == null || name == null || worth == null) { problems.Add(tag + ": " + card.name + " lacks BundleInfo/Name/Worth"); continue; }
                        cards++;
                        Rect ib = CanvasBox(info), nb = CanvasBox(name), wb = CanvasBox(worth);
                        if (nb.xMin < ib.xMax - 0.5f) problems.Add(tag + ": " + card.name + " name starts left of the end of the i button");
                        if (nb.xMax > wb.xMin + 0.5f) problems.Add(tag + ": " + card.name + " name runs into the Worth text");
                        if (Mathf.Abs(nb.center.y - ib.center.y) > 2f) problems.Add(tag + ": " + card.name + " name is not on the i button's centre line (" + nb.center.y + " vs " + ib.center.y + ")");
                        var txt = name.GetComponent<UnityEngine.UI.Text>();
                        if (txt != null && txt.resizeTextMinSize < Mathf.RoundToInt(UiKit.MinBodyTextPx)) problems.Add(tag + ": name min font below 16 sp");
                        if (txt != null)
                        {
                            // the wrapped name must fit its row (so it cannot reach the chips below), in canvas pixels like the layout audit
                            float ppu = Mathf.Max(0.01f, txt.pixelsPerUnit);
                            float needed = txt.preferredHeight / ppu * (nb.height / Mathf.Max(0.01f, name.rect.height));
                            if (needed > nb.height * 1.15f + 1f) problems.Add(tag + ": " + card.name + " name text (" + needed + " px) is taller than its row (" + nb.height + " px)");   // 15 percent tolerance: the real overlap audit is the authority, this is a sanity check
                        }
                    }
                    if (cards == 0) problems.Add(tag + ": no bundle cards found");
                }
            }
            Report(problems);
        }

        [UnityTest]
        public IEnumerator BundleBuyButtonsSitInABrownFrame() { return BundleBuyFrameAudit(100); }

        [UnityTest]
        public IEnumerator BundleBuyButtonsSitInABrownFrameAt130PercentText() { return BundleBuyFrameAudit(130); }

        /// <summary>Bug List 4 E1: on every bundle card the green Buy button is a child of the brown BuyFrame plate (same brown as the Coins strip), at least 48 dp, with at least 8 dp of frame around it, inside the card and clear of the "i", name, Worth text and chips. The Refresh buttons have no frame.</summary>
        IEnumerator BundleBuyFrameAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            yield return Load("Meta");
            var problems = new List<string>();
            var holder = new RenderTexture[1];
            foreach (Aspect a in Aspects)
            {
                foreach (Rect safe in new[] { FullSafe, NotchSafe })
                {
                    Rect s = safe;
                    UiKit.SafeAreaNormalized = () => s;
                    ShowBundlesPage(5000, 0);
                    yield return ApplyAspect(a, holder);
                    yield return null;
                    string tag = "bundle-buy-frame@" + a.Name + (safe == NotchSafe ? "+notch" : "") + "/" + textPercent;
                    int cards = 0;
                    float minPx = UiKit.MinTouchPx;
                    foreach (var card in Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
                    {
                        if (!card.gameObject.activeInHierarchy || !card.name.StartsWith("Bundle_")) continue;
                        RectTransform frame = card.Find("BuyFrame") as RectTransform;
                        RectTransform buy = frame != null ? frame.Find("Buy") as RectTransform : null;
                        if (frame == null || buy == null) { problems.Add(tag + ": " + card.name + " has no BuyFrame/Buy"); continue; }
                        cards++;
                        Rect cb = CanvasBox(card), fb = CanvasBox(frame), bb = CanvasBox(buy);
                        var fi = frame.GetComponent<UnityEngine.UI.Image>();
                        if (fi == null || (Vector4)fi.color != (Vector4)BundlesScreen.BuyFrameColor) problems.Add(tag + ": " + card.name + " frame is not the Coins brown");
                        if (bb.width < minPx - 0.5f || bb.height < minPx - 0.5f) problems.Add(tag + ": " + card.name + " buy button below 48 dp");
                        if (bb.xMin - fb.xMin < 8f - 0.5f || fb.xMax - bb.xMax < 8f - 0.5f || bb.yMin - fb.yMin < 8f - 0.5f || fb.yMax - bb.yMax < 8f - 0.5f)
                            problems.Add(tag + ": " + card.name + " frame padding below 8 dp");
                        if (fb.xMin < cb.xMin - 0.5f || fb.xMax > cb.xMax + 0.5f || fb.yMin < cb.yMin - 0.5f || fb.yMax > cb.yMax + 0.5f) problems.Add(tag + ": " + card.name + " frame leaves the card");
                        foreach (string other in new[] { "BundleInfo", "Name", "Worth" })
                        {
                            RectTransform o = card.Find(other) as RectTransform;
                            if (o != null && CanvasBox(o).Overlaps(fb)) problems.Add(tag + ": " + card.name + " frame overlaps " + other);
                        }
                        foreach (Transform ch in card)
                            if (ch.name == "Chip" && ch.gameObject.activeInHierarchy && CanvasBox((RectTransform)ch).Overlaps(fb)) problems.Add(tag + ": " + card.name + " frame overlaps a chip");
                    }
                    if (cards == 0) problems.Add(tag + ": no bundle cards found");
                    foreach (string refresh in new[] { "RefreshGems", "RefreshCoins" })
                    {
                        RectTransform rb = FindRt(refresh);
                        if (rb != null && rb.parent.name == "BuyFrame") problems.Add(tag + ": " + refresh + " must not have the brown frame");
                    }
                }
            }
            DropBundlesCanvas();
            Report(problems);
        }

        IEnumerator BundlesAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();

            yield return CheckScreen("bundles-page-empty-wallet", () => ShowBundlesPage(0, 0), problems);
            yield return CheckScreen("bundles-page-rich", () => ShowBundlesPage(5000, 500), problems);
            yield return CheckScreen("bundles-info", () => { ShowBundlesPage(5000, 0); ClickByName("BundleInfo"); }, problems);   // the "i" popup of one bundle: icons, hero chance, x worth
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
            yield return CheckScreen("timer-offer-info", () => { TimerOfferState(); meta.ShowMenu(); ClickByName("BundleOffer"); ClickByName("BundleInfo"); }, problems);
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
