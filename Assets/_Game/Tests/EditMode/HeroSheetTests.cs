using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using BoingUpSkyTower.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Tests
{
    /// <summary>The hero sheet (docs/21, 29): data from the stats system, plates cut for 9-slice, rarity colours, and the header, tab and use buttons.</summary>
    public class HeroSheetTests
    {
        static readonly string[] Plates = { "header", "portrait", "name", "ribbon", "ribbon_gray", "region", "gold", "tab_a", "tab_b", "stat_icon", "stat_value", "panel" };

        [SetUp]
        public void SetUp() { GameSession.Reset(); GameSession.InitForTests(new MemorySaveStore()); }

        [TearDown]
        public void TearDown() { GameSession.Reset(); }

        [Test]
        public void CommonMomoShowsTheBaselineAndNoPower()
        {
            HeroSheetData d = HeroSheetData.For(CharacterCatalog.Momo);
            Assert.AreEqual("Momo", d.Name);
            Assert.AreEqual("Common", d.RarityName);
            Assert.AreEqual("No special power", d.PowerText);
            Assert.AreEqual("4.5", d.Stats[0, 1]);
            Assert.AreEqual("9", d.Stats[1, 1]);
            Assert.AreEqual("30", d.Stats[2, 1]);
            Assert.AreEqual("6.9", d.Stats[3, 1]);
            Assert.AreEqual("+0", d.Stats[4, 1]);
            Assert.AreEqual("+0", d.Stats[5, 1]);
            Assert.IsFalse(string.IsNullOrEmpty(d.Region));
        }

        [Test]
        public void ADerivedHeroListsItsPowersAndCooldown()
        {
            HeroSheetData d = HeroSheetData.From("Test", CharacterRarity.Legendary, "Nowhere", CharacterStats.Default, new[] { PowerUp.Flight, PowerUp.Rocket, PowerUp.SlowTime });
            StringAssert.Contains("Flight", d.PowerText);
            StringAssert.Contains("Rocket", d.PowerText);
            StringAssert.Contains("Slow Time", d.PowerText);
            StringAssert.Contains("10 s", d.PowerText);
        }

        [Test]
        public void EveryPlateExistsAndHasItsSliceBorder()
        {
            var ta = Resources.Load<TextAsset>("Art/HeroSheet/slices");
            Assert.IsNotNull(ta, "slices.json is missing");
            foreach (string p in Plates)
            {
                Assert.IsNotNull(Resources.Load<Sprite>("Art/HeroSheet/" + p), p + ".png is missing");
                StringAssert.Contains("\"" + p + "\"", ta.text);
                Assert.IsNotNull(HeroSheetArt.Plate(p), p);
            }
        }

        [Test]
        public void RibbonSpriteSkipsTheBlackSliversOfTheNeighbouringPlates()
        {
            foreach (string n in new[] { "ribbon", "ribbon_gray" })
            {
                Sprite raw = Resources.Load<Sprite>("Art/HeroSheet/" + n);
                Sprite cut = HeroSheetArt.Plate(n);
                Assert.AreEqual(raw.rect.height - HeroSheetArt.RibbonCropTop - HeroSheetArt.RibbonCropBottom, cut.rect.height, 0.01f, n + " height");
                Assert.AreEqual(raw.rect.y + HeroSheetArt.RibbonCropBottom, cut.rect.y, 0.01f, n + " bottom crop");
            }
        }

        [Test]
        public void RarityWordIsCentredOnTheRibbonBand()
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            try
            {
                var parent = (RectTransform)go.transform;
                parent.sizeDelta = new Vector2(1080f, 1920f);
                var screen = new HeroSheetScreen(parent, CharacterCatalog.Momo, null, HeroSheetData.For(CharacterCatalog.Momo), false, null, null, null);
                RectTransform banner = null, word = null;
                foreach (RectTransform rt in screen.Root.GetComponentsInChildren<RectTransform>(true))
                {
                    if (rt.name == "RarityBanner") banner = rt;
                    if (rt.name == "Rarity") word = rt;
                }
                Assert.IsNotNull(banner); Assert.IsNotNull(word);
                float boxCentre = -word.anchoredPosition.y + word.sizeDelta.y * 0.5f;
                Assert.AreEqual(banner.sizeDelta.y * HeroSheetArt.RibbonBandCenter, boxCentre, 0.5f);
                Assert.AreEqual(TextAnchor.MiddleCenter, word.GetComponent<Text>().alignment);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void RibbonTextReadsOnEveryRarityColour()
        {
            for (int i = 0; i < 5; i++)
            {
                var r = (CharacterRarity)i;
                Color f = RarityUi.Fill(r);
                Color onRibbon = new Color(f.r * 0.9f, f.g * 0.9f, f.b * 0.9f, 1f);
                Assert.GreaterOrEqual(UiKit.Contrast(HeroSheetArt.RibbonTextColor(r), onRibbon), 4.5f, r.ToString());
            }
        }

        [Test]
        public void HeaderBackCloseTabsAndUseButtonsWork()
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            try
            {
                var parent = (RectTransform)go.transform;
                parent.sizeDelta = new Vector2(1080f, 1920f);
                int back = 0, close = 0, use = 0;
                var screen = new HeroSheetScreen(parent, CharacterCatalog.Momo, null, HeroSheetData.For(CharacterCatalog.Momo), false, () => back++, () => close++, () => use++);
                Button b = Find(screen.Root, "Back"), c = Find(screen.Root, "Close");
                Assert.IsNotNull(b);
                Assert.IsNotNull(c);
                Assert.GreaterOrEqual(((RectTransform)b.transform).sizeDelta.x, UiKit.MinTouchPx - 1f);
                Assert.GreaterOrEqual(((RectTransform)c.transform).sizeDelta.x, UiKit.MinTouchPx - 1f);
                b.onClick.Invoke();
                c.onClick.Invoke();
                Assert.AreEqual(1, back);
                Assert.AreEqual(1, close);
                Find(screen.Root, "UseHero").onClick.Invoke();
                Assert.AreEqual(1, use);
                Assert.AreEqual(0, screen.Tab);
                Find(screen.Root, "Tab_Details").onClick.Invoke();
                Assert.AreEqual(1, screen.Tab);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheChosenHeroShowsInUseInsteadOfAnActiveButton()
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            try
            {
                var parent = (RectTransform)go.transform;
                parent.sizeDelta = new Vector2(1080f, 1920f);
                var screen = new HeroSheetScreen(parent, CharacterCatalog.Momo, null, HeroSheetData.For(CharacterCatalog.Momo), true, null, null, null);
                Assert.IsFalse(Find(screen.Root, "UseHero").interactable);
            }
            finally { Object.DestroyImmediate(go); }
        }

        static Button Find(Transform t, string name)
        {
            foreach (Button b in t.GetComponentsInChildren<Button>(true)) if (b.name == name && b.gameObject.activeInHierarchy) return b;
            return null;
        }
    }
}
