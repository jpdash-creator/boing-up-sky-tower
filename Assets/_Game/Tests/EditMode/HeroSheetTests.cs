using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Tests
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

        // ---- docs/44 roster, run bonus ----

        [Test]
        public void EveryRosterHeroIsInRangeAndHasADistinctRunBonus()
        {
            var pairs = new System.Collections.Generic.HashSet<string>();   // nine Common heroes, only six coin steps (50..100): the PAIR (coins, gems) is distinct
            foreach (CharacterSpec c in CharacterDefs.All)
            {
                if (c.IsMergeTier) continue;   // tiers add their bonus on top of the Common one (docs/34 2b); they are checked in MergeTests and TierRulesTests
                Assert.IsNull(c.Stats.Validate(), c.Id);
                Assert.That(c.Stats.RunBonusCoins, Is.InRange(50, 100), c.Id);
                Assert.AreEqual(0, c.Stats.RunBonusCoins % 10, c.Id);
                Assert.That(c.Stats.RunBonusGems, Is.InRange(1, 5), c.Id);
                Assert.IsTrue(pairs.Add(c.Stats.RunBonusCoins + "/" + c.Stats.RunBonusGems), c.Id + " run bonus (coins/gems) is not distinct");
            }
        }

        [Test]
        public void MomoKeepsTheBaselineMainStatsAndEveryCommonStaysInsideTheCommonBand()
        {
            CharacterStats m = CharacterDefs.Momo.Stats;
            Assert.AreEqual(190, m.LaunchTenths); Assert.AreEqual(9, m.MaxSpeed); Assert.AreEqual(30, m.Control); Assert.AreEqual(77, m.WallBoostHundredths);
            foreach (CharacterSpec c in CharacterDefs.All)
            {
                if (c.IsMergeTier) continue;   // the tiers are checked row by row in TierRulesTests
                // the Forgiveness Index rules (docs/44): a Common hero's Jump is 0 to 5 percent over the default, the other three stats sit inside the legal limits
                Assert.IsNull(HeroIndex.Validate(0, new HeroPhysics(c.Stats.LaunchTenths, c.Stats.MaxSpeed, c.Stats.Control, c.Stats.WallBoostHundredths)), c.Id);
            }
        }

        [Test]
        public void TheSheetCarriesTheRunBonus()
        {
            HeroSheetData d = HeroSheetData.For(CharacterCatalog.Momo);
            Assert.AreEqual(FluffyClimbers.Simulation.CoinPayoutRules.HeroBonusCoins((int)CharacterDefs.Momo.Rarity), d.RunBonusCoins, "the sheet shows the milestone bonus of the hero's rarity (owner 2026-11-13)");
            Assert.AreEqual(FluffyClimbers.Simulation.CoinPayoutRules.HeroBonusGemsByRarity[(int)CharacterDefs.Momo.Rarity], d.RunBonusGems);
            var go = new GameObject("Canvas", typeof(RectTransform));
            try
            {
                var parent = (RectTransform)go.transform;
                parent.sizeDelta = new Vector2(1080f, 1920f);
                var screen = new HeroSheetScreen(parent, CharacterCatalog.Momo, null, d, false, null, null, null);
                bool label = false, coin = false, gem = false;
                foreach (Text t in screen.Root.GetComponentsInChildren<Text>(true))
                {
                    if (t.text == "Milestone bonus") label = true;
                    if (t.name == "CoinAmount" && t.text == "+" + d.RunBonusCoins) coin = true;
                    if (t.name == "GemAmount" && t.text == "+" + d.RunBonusGems) gem = true;
                }
                Assert.IsTrue(label && coin && gem, "Milestone bonus row (label, coin and gem amounts)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheOldPerRunHeroBonusIsGoneAndOnlyMilestoneLevelsPayTheHeroBonus()
        {
            // the chosen hero's stats come from CharacterDefs.ActiveStats; use a fake with a known bonus
            var old = CharacterDefs.ActiveStats;
            try
            {
                const string level = "lvl_001_01";
                CharacterDefs.ActiveStats = () => new CharacterStats();
                int c0 = GameSession.Progress.Coins, g0 = GameSession.Progress.Gems;
                GameSession.Progress.ApplyRun(level, new RunOutcome { Completed = true, StarMask = 1 });
                int plainCoins = GameSession.Progress.Coins - c0, plainGems = GameSession.Progress.Gems - g0;

                GameSession.Reset(); GameSession.InitForTests(new MemorySaveStore());
                CharacterDefs.ActiveStats = () => new CharacterStats { RunBonusCoins = 70, RunBonusGems = 4 };
                c0 = GameSession.Progress.Coins; g0 = GameSession.Progress.Gems;
                GameSession.Progress.ApplyRun(level, new RunOutcome { Completed = true, StarMask = 1 });
                Assert.AreEqual(plainCoins, GameSession.Progress.Coins - c0, "owner 2026-11-13: no per-run hero bonus any more; the hero bonus is paid on the first clear of levels 5/10/15/20/25 by rarity");
                Assert.AreEqual(plainGems, GameSession.Progress.Gems - g0);
            }
            finally { CharacterDefs.ActiveStats = old; }
        }

        // ---- header rows and hero arrows ----

        [Test]
        public void BackAndCloseSitInARowBelowTheTitleBar()
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            try
            {
                var parent = (RectTransform)go.transform;
                parent.sizeDelta = new Vector2(1080f, 1920f);
                var screen = new HeroSheetScreen(parent, CharacterCatalog.Momo, null, HeroSheetData.For(CharacterCatalog.Momo), false, null, null, null);
                Assert.AreEqual("NavRow", Find(screen.Root, "Back").transform.parent.name);
                Assert.AreEqual("NavRow", Find(screen.Root, "Close").transform.parent.name);
                RectTransform header = null, nav = null;
                foreach (RectTransform rt in screen.Root.GetComponentsInChildren<RectTransform>(true))
                {
                    if (rt.name == "Header") header = rt;
                    if (rt.name == "NavRow") nav = rt;
                }
                Assert.Less(nav.anchoredPosition.y, header.anchoredPosition.y - header.sizeDelta.y + 0.5f, "the row starts below the title bar");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheOnlyOwnedHeroHasBothArrowsDisabled()
        {
            bool oldAll = ProgressService.TestAllCharacters;
            ProgressService.TestAllCharacters = false;
            var go = new GameObject("Canvas", typeof(RectTransform));
            try
            {
                var parent = (RectTransform)go.transform;
                parent.sizeDelta = new Vector2(1080f, 1920f);
                var screen = new HeroSheetScreen(parent, CharacterCatalog.Momo, null, HeroSheetData.For(CharacterCatalog.Momo), false, null, null, null, h => { });
                Button p = Find(screen.Root, "PrevHero"), n = Find(screen.Root, "NextHero");
                Assert.IsNotNull(p); Assert.IsNotNull(n);
                Assert.IsFalse(p.interactable);
                Assert.IsFalse(n.interactable);
                Assert.GreaterOrEqual(((RectTransform)p.transform).sizeDelta.x, UiKit.MinTouchPx - 1f);
                Assert.AreEqual("Previous hero, not available", p.GetComponent<A11yLabel>().Text);
            }
            finally { Object.DestroyImmediate(go); ProgressService.TestAllCharacters = oldAll; }
        }

        static Button Find(Transform t, string name)
        {
            foreach (Button b in t.GetComponentsInChildren<Button>(true)) if (b.name == name && b.gameObject.activeInHierarchy) return b;
            return null;
        }
    }
}
