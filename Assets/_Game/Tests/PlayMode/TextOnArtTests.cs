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
    /// Issue list 24, point 1 (name tags, no cut letters). Every text that sits on a backing (region name tags on the world map, shop badges,
    /// wallet and HUD pills, banners) is measured: the text plus its padding must fit inside the backing, the backing must be solid, the text at
    /// least 16 sp, tags must stay inside the safe area, must not overlap each other and must not cover a pin. Run on 9:16, 9:19.5, 9:21 and 3:4,
    /// with and without a notch, at 130 percent text. Category CriticalUX.
    /// </summary>
    [Category("CriticalUX")]
    public class TextOnArtTests
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

        static readonly Rect FullSafe = new Rect(0f, 0f, 1f, 1f);
        static readonly Rect NotchSafe = new Rect(0.03f, 0.02f, 0.94f, 0.93f);

        sealed class TagBox { public RectTransform Rt; public Rect Box; public string Name; }

        readonly List<KeyValuePair<RegionDef, string>> renamed = new List<KeyValuePair<RegionDef, string>>();
        int measured;      // strict name tags measured by the last audits
        int measuredPills; // other backings measured

        [SetUp]
        public void SetUp()
        {
            Time.captureFramerate = 60;
            GameSession.Reset();
            GameSession.InitForTests(new MemorySaveStore());
            GoogleConnectPrompt.ResetForTests();
            GoogleConnectPrompt.AutoShow = false;   // Bug List 1 B1.c: the Google connect pop-up is tested on its own, not over every Home screen
            renamed.Clear();
            measured = 0;
            measuredPills = 0;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            foreach (var kv in renamed) kv.Key.Name = kv.Value;
            renamed.Clear();
            foreach (var rc in Object.FindObjectsByType<RunController>(FindObjectsSortMode.None)) rc.enabled = false;
            foreach (var mc in Object.FindObjectsByType<MetaController>(FindObjectsSortMode.None)) mc.enabled = false;
            UiKit.SafeAreaNormalized = () => new Rect(0f, 0f, 1f, 1f);
            RunView.AspectOverride = 0f;
            PlayerPrefs.DeleteAll();
            GameSettings.ApplyTextScale();
            GameSession.Reset();
        }

        // ---------------- tests ----------------

        /// <summary>The world map at 130 percent text with the default region names.</summary>
        [UnityTest]
        public IEnumerator MapNameTagsFitTheirBackingAt130Percent()
        {
            GameSettings.TextPercent = 130;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            yield return CheckScreen("map-130", () => meta.ShowMap(), problems);
            Assert.Greater(measured, 10, "the map has a name tag for every pin (five regions plus the coming soon pins)");
            Report(problems);
        }

        /// <summary>The longest region name, and every playable name made 30 percent longer, at 130 percent text.</summary>
        [UnityTest]
        public IEnumerator MapNameTagsFitWithTheLongestAndThirtyPercentLongerNames()
        {
            GameSettings.TextPercent = 130;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            const string filler = "Wonderful Skyline Gardens Above The Clouds";
            for (int i = 1; i <= GameSession.PlayableRegions; i++)
            {
                RegionDef r = GameSession.Repo.TryGetRegionByIndex(i);
                if (r == null) continue;
                renamed.Add(new KeyValuePair<RegionDef, string>(r, r.Name));
                int extra = Mathf.Clamp(Mathf.CeilToInt(r.Name.Length * 0.3f), 1, filler.Length);
                r.Name = r.Name + " " + filler.Substring(0, extra);
            }
            RegionDef longest = GameSession.Repo.TryGetRegionByIndex(3);
            if (longest != null) longest.Name = "Mushroom Meadows Of The Sky";   // the longest realistic name, two lines at 130 percent
            yield return CheckScreen("map-130-long-names", () => meta.ShowMap(), problems);
            Assert.Greater(measured, 10);
            Report(problems);
        }

        /// <summary>B5.c: every map tag of regions 6 to 30 reads "Coming Soon", regions 1 to 5 keep their names (the data is untouched).</summary>
        [UnityTest]
        public IEnumerator MapTagsOfRegions6To30ReadComingSoon()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            meta.ShowMap();
            yield return null;
            yield return null;
            for (int i = 1; i <= 30; i++)
            {
                RegionDef r = GameSession.Repo.TryGetRegionByIndex(i);
                if (r == null) continue;
                string expect = i >= 6 ? "Coming Soon" : r.Name;
                Assert.AreEqual(expect, MetaController.MapLabel(r), "map label of region " + i);
                Transform pin = FindDeep(Object.FindFirstObjectByType<MetaController>().transform, "Region" + i);
                if (pin == null) continue;   // that page is not painted
                Transform name = FindDeep(pin, "Name");
                Assert.IsNotNull(name, "tag of region " + i);
                Assert.AreEqual(expect, name.GetComponent<Text>().text, "tag text of region " + i);
            }
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        /// <summary>The map at normal text size, with some progress so unlocked, current and locked pins all appear.</summary>
        [UnityTest]
        public IEnumerator MapNameTagsFitTheirBackingAt100Percent()
        {
            GameSettings.TextPercent = 100;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            for (int i = 1; i <= 6; i++)
                GameSession.Progress.ApplyRun("lvl_001_" + i.ToString("D2"), new RunOutcome { Completed = true, StarMask = 7, Score = 100 * i, CoinsCollected = 30 });
            yield return CheckScreen("map-100", () => meta.ShowMap(), problems);
            Assert.Greater(measured, 10);
            Report(problems);
        }

        /// <summary>Every other text on a backing (shop badges, wallet pills, level screen, popups) at 130 percent text.</summary>
        [UnityTest]
        public IEnumerator MenuLevelAndShopTagsFitTheirBackingAt130Percent()
        {
            GameSettings.TextPercent = 130;
            ShopScreen.TestPurchasesOverride = null;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            GameSession.Progress.AddGems(300);
            GameSession.Progress.AddCoins(12345);
            RegionDef r1 = GameSession.Repo.GetRegionByIndex(1);
            for (int i = 1; i <= 6; i++)
                GameSession.Progress.ApplyRun("lvl_001_" + i.ToString("D2"), new RunOutcome { Completed = true, StarMask = i % 2 == 0 ? 7 : 3, Score = 100 * i, CoinsCollected = 30 });
            var levels = GameSession.Repo.LevelsOf(r1.Id);

            yield return CheckScreen("menu", () => meta.ShowMenu(), problems);
            yield return CheckScreen("levels", () => meta.ShowLevels(r1), problems);
            yield return CheckScreen("level-popup", () => { meta.ShowLevels(r1); meta.OnLevelTapped(levels[2]); }, problems);
            yield return CheckScreen("shop-gems", () => meta.ShowShop(ShopScreen.TabGems, ShopScreen.GemsSection), problems);
            yield return CheckScreen("shop-coins", () => meta.ShowShop(ShopScreen.TabGems, ShopScreen.CoinsSection), problems);
            yield return CheckScreen("shop-powerups", () => meta.ShowShop(ShopScreen.TabCharacter, ShopScreen.PowerupsSection), problems);
            Assert.Greater(measured + measuredPills, 0);
            Report(problems);
        }

        /// <summary>The run HUD pills and banners, the failed and the results screens at 130 percent text.</summary>
        [UnityTest]
        public IEnumerator RunHudAndResultTagsFitTheirBackingAt130Percent()
        {
            GameSettings.TextPercent = 130;
            for (int i = 1; i <= 2; i++)
                GameSession.Progress.ApplyRun("lvl_001_0" + i, new RunOutcome { Completed = true, StarMask = 1, Score = 100, CoinsCollected = 10 });
            GameSession.CurrentLevelId = "lvl_001_03";
            yield return Load("Run");
            var rc = Object.FindFirstObjectByType<RunController>();
            var problems = new List<string>();
            rc.Hud.SkipIntro();
            int frame = 0;
            rc.Input.ScriptedMove = () => SimInput.Right;
            rc.Input.ScriptedJump = () => frame % 45 == 0;
            for (frame = 1; frame <= 120; frame++) yield return null;
            yield return CheckScreen("run-hud", () => { }, problems);
            yield return CheckScreen("run-failed", () => rc.Hud.ShowFailed(rc.Session, 10, true), problems);
            rc.Hud.CloseOverlay();
            var outcome = rc.Session.BuildOutcome();
            var reward = GameSession.Progress.ApplyRun(GameSession.CurrentLevelId, outcome);
            yield return CheckScreen("run-results", () => rc.Hud.ShowResults(rc.Session, reward, true), problems);
            rc.Hud.CloseOverlay();
            Assert.Greater(measuredPills, 0, "the HUD pills were measured");
            Report(problems);
        }

        // ---------------- audit ----------------

        static readonly TextGenerator wordGen = new TextGenerator();

        static float Lum(Color c)
        {
            float r = Mathf.GammaToLinearSpace(c.r), g = Mathf.GammaToLinearSpace(c.g), b = Mathf.GammaToLinearSpace(c.b);
            return 0.2126f * r + 0.7152f * g + 0.0722f * b;
        }

        /// <summary>Contrast of a text colour against its backing blended over pure white art (the worst case).</summary>
        static float ContrastOverWhite(Color text, Color back)
        {
            Color bg = Color.Lerp(Color.white, new Color(back.r, back.g, back.b, 1f), back.a);
            float l1 = Lum(text), l2 = Lum(bg);
            return (Mathf.Max(l1, l2) + 0.05f) / (Mathf.Min(l1, l2) + 0.05f);
        }

        static bool IsStrictTag(string name) { return name == "NameBacking"; }

        static bool IsPillBacking(string name)
        {
            switch (name)
            {
                case "Pill": case "StarsPill": case "StarPill": case "WalletPanel": case "FloorPill": case "GoalPill": case "GemPill":
                case "ComboBanner": case "SpeedBanner": return true;
                default: return false;
            }
        }

        static bool IsDescendant(Transform child, Transform ancestor)
        {
            for (Transform t = child; t != null; t = t.parent) if (t == ancestor) return true;
            return false;
        }

        static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null && p.GetComponent<Canvas>() == null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        static Rect ToCanvasUnits(RectTransform rt, Camera cam, float scale)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            Vector3 a = cam.WorldToScreenPoint(c[0]);
            Vector3 b = cam.WorldToScreenPoint(c[2]);
            return new Rect(a.x / scale, a.y / scale, (b.x - a.x) / scale, (b.y - a.y) / scale);
        }

        List<string> Audit(string screen, Aspect a, Rect safe)
        {
            var problems = new List<string>();
            Camera cam = Camera.main;
            const float tol = 2f;
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isActiveAndEnabled || canvas.renderMode != RenderMode.ScreenSpaceCamera) continue;
                float scale = canvas.scaleFactor;
                float cw = a.W / scale, ch = a.H / scale;
                var sa = new Rect(safe.x * cw, safe.y * ch, safe.width * cw, safe.height * ch);
                var tags = new List<TagBox>();
                var pins = new List<TagBox>();

                foreach (Button b in canvas.GetComponentsInChildren<Button>(false))
                {
                    if (b.gameObject.activeInHierarchy && b.name.StartsWith("Region"))
                        pins.Add(new TagBox { Rt = (RectTransform)b.transform, Box = ToCanvasUnits((RectTransform)b.transform, cam, scale), Name = Path(b.transform) });
                }

                foreach (Text t in canvas.GetComponentsInChildren<Text>(false))
                {
                    if (string.IsNullOrEmpty(t.text) || !t.gameObject.activeInHierarchy || t.color.a < 0.05f) continue;
                    Transform parent = t.transform.parent;
                    Image back = parent != null ? parent.GetComponent<Image>() : null;
                    if (back == null) continue;
                    bool strict = IsStrictTag(back.name), pill = IsPillBacking(back.name);
                    if (!strict && !pill) continue;
                    if (back.rectTransform.rect.width < 1f) continue;

                    Rect bb = ToCanvasUnits(back.rectTransform, cam, scale);
                    Rect tb = ToCanvasUnits(t.rectTransform, cam, scale);
                    float us = t.rectTransform.rect.width > 0.01f ? tb.width / t.rectTransform.rect.width : 1f;   // local units to canvas units
                    float pw = t.preferredWidth;
                    if (t.horizontalOverflow == HorizontalWrapMode.Wrap) pw = Mathf.Min(pw, t.rectTransform.rect.width);
                    float pwC = pw * us, phC = t.preferredHeight * us;
                    float x = tb.x;
                    switch (t.alignment)
                    {
                        case TextAnchor.UpperCenter: case TextAnchor.MiddleCenter: case TextAnchor.LowerCenter: x += (tb.width - pwC) * 0.5f; break;
                        case TextAnchor.UpperRight: case TextAnchor.MiddleRight: case TextAnchor.LowerRight: x += tb.width - pwC; break;
                    }
                    var vis = new Rect(x, tb.center.y - phC * 0.5f, pwC, phC);   // where the glyphs really are
                    string who = screen + ": " + Path(t.transform) + " \"" + (t.text.Length > 24 ? t.text.Substring(0, 24) : t.text.Replace("\n", " ")) + "\"";

                    if (pill)
                    {
                        measuredPills++;
                        if (vis.xMin < bb.xMin - tol || vis.xMax > bb.xMax + tol)
                            problems.Add(who + " runs outside its backing (text " + vis.xMin.ToString("F0") + ".." + vis.xMax.ToString("F0") + ", backing " + bb.xMin.ToString("F0") + ".." + bb.xMax.ToString("F0") + ")");
                        continue;
                    }

                    measured++;
                    // A tag text must never be split in the middle of a word ("Popula" / "r"): every word must fit the text rect on one line.
                    if (t.horizontalOverflow == HorizontalWrapMode.Wrap)
                    {
                        TextGenerationSettings ws = t.GetGenerationSettings(Vector2.zero);
                        ws.horizontalOverflow = HorizontalWrapMode.Overflow;
                        foreach (string word in t.text.Split(' '))
                        {
                            if (word.Length == 0) continue;
                            float wordW = wordGen.GetPreferredWidth(word, ws) / t.pixelsPerUnit;
                            if (wordW > t.rectTransform.rect.width + 1f)
                                problems.Add(who + " is split in the middle of the word \"" + word + "\" (word " + wordW.ToString("F0") + " > text width " + t.rectTransform.rect.width.ToString("F0") + ")");
                        }
                    }
                    float bs = bb.width / back.rectTransform.rect.width;   // backing local units to canvas units
                    bool badge = parent.parent != null && parent.parent.name == "Panel";   // shop card badge: 8 dp sides, otherwise 12 dp
                    float padX = (badge ? 8f : 12f) * UiKit.PxPerDp * bs, padY = 6f * UiKit.PxPerDp * bs;
                    if (t.fontSize < Mathf.RoundToInt(UiKit.MinBodyTextPx) - 1) problems.Add(who + " is below 16 sp (" + t.fontSize + ")");
                    if (strict)
                    {
                        // B5.b: the map tags are 30 percent more transparent (alpha 0.665). Worst case art behind them is pure white: the text must still reach 4.5:1 on that blend.
                        if (back.color.a < TagKit.MapTagMinAlpha - 0.001f) problems.Add(who + " backing is too transparent (alpha " + back.color.a.ToString("F2") + ")");
                        float cr = ContrastOverWhite(t.color, back.color);
                        if (cr < 4.5f) problems.Add(who + " contrast over the brightest art is " + cr.ToString("F2") + ":1 (< 4.5:1)");
                    }
                    else if (back.color.a < 0.9f) problems.Add(who + " backing is not solid (alpha " + back.color.a.ToString("F2") + ")");
                    float gapL = vis.xMin - bb.xMin, gapR = bb.xMax - vis.xMax, gapB = vis.yMin - bb.yMin, gapT = bb.yMax - vis.yMax;
                    if (gapL < padX - tol || gapR < padX - tol)
                        problems.Add(who + " text width + padding exceeds the backing: text " + pwC.ToString("F0") + " + 2x" + padX.ToString("F0") + " > backing " + bb.width.ToString("F0") + " (gaps " + gapL.ToString("F0") + "/" + gapR.ToString("F0") + ")");
                    if (gapB < padY - tol || gapT < padY - tol)
                        problems.Add(who + " text height + padding exceeds the backing: text " + phC.ToString("F0") + " in backing " + bb.height.ToString("F0") + " (gaps " + gapB.ToString("F0") + "/" + gapT.ToString("F0") + ")");

                    // Backing inside the safe area (the vertical check is skipped for content in a scroll view; the viewport clips that on purpose).
                    bool inScroll = back.GetComponentInParent<ScrollRect>() != null;
                    if (bb.xMin < sa.xMin - tol || bb.xMax > sa.xMax + tol)
                        problems.Add(who + " backing leaves the safe area horizontally: " + bb + " safe " + sa);
                    if (!inScroll && (bb.yMin < sa.yMin - tol || bb.yMax > sa.yMax + tol))
                        problems.Add(who + " backing leaves the safe area vertically: " + bb + " safe " + sa);

                    tags.Add(new TagBox { Rt = back.rectTransform, Box = bb, Name = Path(back.transform) });
                }

                // Tags never overlap each other, and never cover a pin (their own or another one).
                for (int i = 0; i < tags.Count; i++)
                {
                    for (int j = i + 1; j < tags.Count; j++)
                    {
                        if (IsDescendant(tags[i].Rt, tags[j].Rt) || IsDescendant(tags[j].Rt, tags[i].Rt)) continue;
                        float ox = Mathf.Min(tags[i].Box.xMax, tags[j].Box.xMax) - Mathf.Max(tags[i].Box.xMin, tags[j].Box.xMin);
                        float oy = Mathf.Min(tags[i].Box.yMax, tags[j].Box.yMax) - Mathf.Max(tags[i].Box.yMin, tags[j].Box.yMin);
                        if (ox > tol && oy > tol) problems.Add(screen + ": name tags overlap " + ox.ToString("F0") + "x" + oy.ToString("F0") + ": " + tags[i].Name + "  <>  " + tags[j].Name);
                    }
                    foreach (TagBox pin in pins)
                    {
                        float ox = Mathf.Min(tags[i].Box.xMax, pin.Box.xMax) - Mathf.Max(tags[i].Box.xMin, pin.Box.xMin);
                        float oy = Mathf.Min(tags[i].Box.yMax, pin.Box.yMax) - Mathf.Max(tags[i].Box.yMin, pin.Box.yMin);
                        if (ox > 4f && oy > 4f) problems.Add(screen + ": name tag covers a pin " + ox.ToString("F0") + "x" + oy.ToString("F0") + ": " + tags[i].Name + " " + tags[i].Box + "  <>  " + pin.Name + " " + pin.Box);
                    }
                }
            }
            return problems;
        }

        // ---------------- plumbing (same approach as CriticalUxLayoutTests) ----------------

        static IEnumerator Load(string scene)
        {
            var op = SceneManager.LoadSceneAsync(scene);
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;
        }

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

        IEnumerator CheckScreen(string screen, System.Action show, List<string> all)
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
                    yield return null;   // the tags settle (TagKeepInside moves them inward once the map has its size)
                    all.AddRange(Audit(screen + "@" + a.Name + (safe == NotchSafe ? "+notch" : ""), a, safe));
                }
            }
            Restore(holder);
        }

        static void Report(List<string> problems)
        {
            var seen = new HashSet<string>();
            var sb = new StringBuilder();
            foreach (string p in problems)
            {
                string key = p.Substring(p.IndexOf(':') + 1);
                if (seen.Add(key)) sb.AppendLine(p);
            }
            Assert.IsEmpty(problems, "Text on art violations (" + seen.Count + " distinct):\n" + sb);
        }
    }
}
