using System.Collections.Generic;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Tests
{
    /// <summary>
    /// Bug List 5 F2 (docs/34 section 10): the Merge list (only mergeable types, one card per copy, no numbers), the selection rule (exactly 3 of one hero type), the list cleaning after
    /// a merge, and the All Heroes screen (every character, grouped by region, locked state, Rare/Elite/Legendary always locked, details popup).
    /// </summary>
    public class HeroScreensTests
    {
        bool oldChars;
        GameObject go;
        RectTransform canvas;

        [SetUp]
        public void SetUp()
        {
            oldChars = ProgressService.TestAllCharacters;
            ProgressService.TestAllCharacters = false;
            GameSession.Reset();
            GameSession.InitForTests(new MemorySaveStore());
            go = new GameObject("Canvas", typeof(RectTransform));
            canvas = (RectTransform)go.transform;
            canvas.sizeDelta = new Vector2(1080f, 1920f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
            GameSession.Reset();
            ProgressService.TestAllCharacters = oldChars;
        }

        static ProgressService P { get { return GameSession.Progress; } }

        static void Give(string id, int copies)
        {
            P.AddCopies(id, copies);
            P.MarkHeroRevealed(id);
        }

        MergeScreen NewMerge()
        {
            var host = new MergeScreen.Host { NewOverlay = () => UiKit.Rect(canvas, "Popup"), CloseOverlay = () => { } };
            var m = new MergeScreen(canvas, canvas, P, GameSession.Art, host);
            m.Build();
            return m;
        }

        static List<Transform> Tiles(MergeScreen m, Transform root)
        {
            var list = new List<Transform>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Tile_")) list.Add(t);
            return list;
        }

        // ---------------------------------------------------------------- Merge list

        [Test]
        public void MergeListShowsOnlyTypesWithThreeOrMoreCopiesAsIndividualCardsWithNoNumbers()
        {
            P.AddCopies("momo", 2);   // 3 Momo
            Give("forestgirl", 2);    // 2 Capi
            MergeScreen m = NewMerge();
            Assert.AreEqual(3, m.Cards.Count, "3 Momo are listed, 2 Capi are not");
            foreach (HeroCard c in m.Cards) Assert.AreEqual("momo", c.Id);
            Assert.AreEqual(3, Tiles(m, canvas).Count, "one tile per copy");

            P.AddCopies("momo", 1);     // 4 Momo
            Give("forestgirl", 1);      // 3 Capi
            Give("candygirl", 2);       // 2 Bonbon
            m.Build();
            Assert.AreEqual(7, m.Cards.Count, "4 Momo and 3 Capi");
            var ids = new List<string>();
            foreach (HeroCard c in m.Cards) ids.Add(c.Id);
            CollectionAssert.DoesNotContain(ids, "candygirl");
            foreach (Transform tile in Tiles(m, canvas))
                Assert.AreEqual(0, tile.GetComponentsInChildren<Text>(true).Length, "no number or text on a hero card: " + tile.name);
        }

        [Test]
        public void NothingToMergeShowsAnEmptyListAndADisabledButton()
        {
            MergeScreen m = NewMerge();   // a new player: 1 Momo
            Assert.AreEqual(0, m.Cards.Count);
            Assert.IsFalse(m.MergeButton.interactable);
            Assert.AreEqual("No hero to merge yet", m.HintText());
        }

        [Test]
        public void MergeIsEnabledOnlyForThreeSelectedCardsOfOneType()
        {
            P.AddCopies("momo", 3);   // 4 Momo
            Give("forestgirl", 3);    // 3 Capi (a hero the player does not own starts at 0 copies, so AddCopies 3 = 3 cards)
            MergeScreen m = NewMerge();
            Assert.IsFalse(m.CanMerge);
            m.TapCard(0);
            m.TapCard(1);
            Assert.IsFalse(m.MergeButton.interactable, "two cards are not enough");
            Assert.AreEqual(2, m.Picked.Count);
            m.TapCard(2);
            Assert.IsTrue(m.MergeButton.interactable, "three Momo");
            Assert.AreEqual("Ready to merge", m.HintText());
            // remove one Momo, add a Capi: a wrong mix keeps Merge disabled
            m.TapCard(2);
            m.TapCard(4);
            Assert.AreEqual(3, m.Picked.Count);
            Assert.IsFalse(m.MergeButton.interactable, "wrong mix");
            StringAssert.Contains("Wrong mix", m.HintText());
            // a full selection ignores another tap
            m.TapCard(3);
            Assert.AreEqual(3, m.Picked.Count);
        }

        [Test]
        public void SelectedCardsFillTheSlotsAndShowATick()
        {
            P.AddCopies("momo", 2);
            MergeScreen m = NewMerge();
            m.TapCard(0);
            m.TapCard(2);
            int ticks = 0;
            foreach (Image im in canvas.GetComponentsInChildren<Image>(true)) if (im.name == "Picked") ticks++;
            Assert.AreEqual(2, ticks, "a tick on every selected card");
            int filled = 0;
            foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Slot_") && t.Find("Fill") != null && t.Find("Fill").Find("FaceArea") != null) filled++;
            Assert.AreEqual(2, filled, "two of the three slots hold a hero");
        }

        [Test]
        public void AfterAMergeTheConsumedCardsLeaveTheListAndTheNewTierIsOwned()
        {
            P.AddCopies("momo", 3);   // 4 Momo (list has 4 cards)
            MergeScreen m = NewMerge();
            Assert.AreEqual(4, m.Cards.Count);
            m.SelectType("momo");
            Assert.IsTrue(m.CanMerge);
            Assert.AreEqual(MergeResult.Ok, m.TapMerge().Result);
            Assert.AreEqual(1, P.Copies("momo"), "exactly 3 copies consumed");
            Assert.AreEqual(1, P.Copies("momo_u"));
            Assert.AreEqual(0, m.Cards.Count, "1 Momo is left: the type vanishes from the merge list");
            Assert.AreEqual(0, m.Picked.Count, "the selection is cleared");
            Assert.IsFalse(m.MergeButton.interactable);
            var owned = CharacterCatalog.OwnedCards(P);
            Assert.AreEqual(2, owned.Count, "1 Momo and the new Uncommon Momo on the Heroes screen");
            Assert.AreEqual("momo_u", owned[1].Id);
        }

        [Test]
        public void AMergeWithFiveCopiesLeavesTwoAndTheTypeVanishes()
        {
            P.AddCopies("momo", 5);   // 6 Momo
            MergeScreen m = NewMerge();
            m.SelectType("momo");
            m.TapMerge();
            Assert.AreEqual(3, P.Copies("momo"));
            Assert.AreEqual(3, m.Cards.Count, "3 Momo are still mergeable, so they stay in the list");
            m.SelectType("momo");
            m.TapMerge();
            Assert.AreEqual(0, P.Copies("momo"));
            Assert.AreEqual(2, P.Copies("momo_u"));
            Assert.AreEqual(0, m.Cards.Count);
        }

        [Test]
        public void ATopTierTypeIsNeverListedEvenWithManyCopies()
        {
            Give("momo_l", 6);
            MergeScreen m = NewMerge();
            Assert.AreEqual(0, m.Cards.Count, "Legendary has no next tier");
        }

        [Test]
        public void EveryRegion1UncommonRareAndEliteMergesUpTheWholeChain()
        {
            foreach (string line in new[] { "momo", "forestgirl", "beachotter", "candygirl", "monk" })
            {
                string[] chain = { line, line + "_u", line + "_r", line + "_e", line + "_l" };
                for (int i = 0; i < chain.Length - 1; i++)
                {
                    int need = 3 - P.Copies(chain[i]);   // the previous merge left 1 copy of this tier (Momo starts with 1)
                    if (need > 0) Give(chain[i], need);
                    MergeScreen m = NewMerge();
                    Assert.AreEqual(3, m.Cards.Count, chain[i] + ": three copies are listed");
                    m.SelectType(chain[i]);
                    Assert.IsTrue(m.CanMerge, chain[i]);
                    Assert.AreEqual(MergeResult.Ok, m.TapMerge().Result, chain[i]);
                    Assert.AreEqual(1, P.Copies(chain[i + 1]), chain[i] + " merges into " + chain[i + 1]);
                    Object.DestroyImmediate(go);
                    go = new GameObject("Canvas", typeof(RectTransform));
                    canvas = (RectTransform)go.transform;
                    canvas.sizeDelta = new Vector2(1080f, 1920f);
                }
            }
        }

        // ---------------------------------------------------------------- All Heroes

        AllHeroesScreen NewAll()
        {
            var host = new AllHeroesScreen.Host { NewOverlay = () => UiKit.Rect(canvas, "Popup"), CloseOverlay = () => { }, RegionName = i => "Region name " + i };
            var s = new AllHeroesScreen(canvas, canvas, P, GameSession.Art, host);
            s.Build();
            return s;
        }

        static List<RectTransform> HeroCardsOn(Transform root)
        {
            var list = new List<RectTransform>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Hero_")) list.Add((RectTransform)t);
            return list;
        }

        [Test]
        public void AllHeroesListsEveryCharacterGroupedByRegionThreePerRow()
        {
            AllHeroesScreen s = NewAll();
            Assert.AreEqual(5, s.Groups.Count, "regions 1 to 5");
            for (int g = 0; g < 5; g++) Assert.AreEqual(5, s.Groups[g].Entries.Count, "Region " + (g + 1) + ": one ACTIVE hero line x 5 tiers (Momo, Capi, Otto, Bonbon, Bao)");
            Assert.AreEqual(25, HeroCardsOn(canvas).Count, "All Heroes lists the 25 active heroes only (5 lines x 5 tiers)");
            for (int i = 0; i < s.Groups.Count; i++) Assert.AreEqual(i + 1, s.Groups[i].Region);
            var cols = new HashSet<float>();
            foreach (RectTransform card in HeroCardsOn(canvas)) cols.Add(Mathf.Round(card.anchorMin.x * 3f));
            Assert.AreEqual(3, cols.Count, "three columns");
            // every hero line starts on its own row in the first column (Common), so a ladder is never split across other heroes
            foreach (CharacterSpec c in CharacterInventory.All)
                if (!c.IsMergeTier) Assert.AreEqual(0f, Mathf.Round(((RectTransform)FindDeep(canvas, "Hero_" + c.Id)).anchorMin.x * 3f), c.Id + ": Common card in column 1");
            int banners = 0;
            foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Banner_")) banners++;
            Assert.AreEqual(5, banners, "a banner per region");
        }

        [Test]
        public void EverythingTheMasterListHasIsOnTheAllHeroesScreen()
        {
            NewAll();
            var names = new HashSet<string>();
            foreach (RectTransform c in HeroCardsOn(canvas)) names.Add(c.name);
            foreach (CharacterSpec c in CharacterInventory.All) Assert.IsTrue(names.Contains("Hero_" + c.Id), c.Id);
        }

        [Test]
        public void NotOwnedHeroesAreLockedAndRareEliteLegendaryAlwaysAre()
        {
            ProgressService.TestAllCharacters = true;   // even with Character Test on, only the Commons are owned
            AllHeroesScreen s = NewAll();
            foreach (AllHeroEntry e in AllHeroes.Flatten(s.Groups))
            {
                bool shouldBeOpen = e.Rarity == CharacterRarity.Common;
                Assert.AreEqual(!shouldBeOpen, e.Locked, e.Id);
                Transform card = FindDeep(canvas, "Hero_" + e.Id);
                Assert.IsNotNull(card, e.Id);
                Assert.AreEqual(e.Locked, FindDeep(card, "Lock") != null, e.Id + ": padlock exactly on locked cards");
                Assert.IsNotNull(FindDeep(card, "Info"), e.Id + ": every card has an i button");
                Assert.GreaterOrEqual(((RectTransform)FindDeep(card, "Info")).sizeDelta.x, UiKit.MinTouchPx - 1f, "48 dp");
            }
        }

        [Test]
        public void OwningAMergedHeroUnlocksItsCard()
        {
            Give("momo_u", 1);
            AllHeroesScreen s = NewAll();
            Assert.IsFalse(AllHeroes.Flatten(s.Groups).Find(e => e.Id == "momo_u").Locked);
            Assert.IsTrue(AllHeroes.Flatten(s.Groups).Find(e => e.Id == "forestgirl_u").Locked);
        }

        [Test]
        public void TheDetailsPopupShowsStatsAndHowToGetForLockedAndOwnedHeroes()
        {
            AllHeroesScreen s = NewAll();
            Assert.IsTrue(s.OpenInfo("momo_u"));   // real, locked
            Transform pop = FindDeep(canvas, AllHeroesScreen.InfoPopupName);
            Assert.IsNotNull(pop);
            StringAssert.Contains("How to get: Merge 3 Common Momo", FindDeep(pop, "HowToGet").GetComponent<Text>().text);
            StringAssert.Contains("Jump", FindDeep(pop, "StatLines").GetComponent<Text>().text);
            StringAssert.Contains("Powers:", FindDeep(pop, "Powers").GetComponent<Text>().text);

            Object.DestroyImmediate(FindDeep(canvas, "Popup").gameObject);
            Assert.IsTrue(s.OpenInfo("candygirl_r"));   // a real Rare tier, locked
            pop = FindDeep(canvas, AllHeroesScreen.InfoPopupName);
            StringAssert.Contains("Merge 3 Uncommon Bonbon", FindDeep(pop, "HowToGet").GetComponent<Text>().text);
            StringAssert.Contains("Jump", FindDeep(pop, "StatLines").GetComponent<Text>().text);

            Object.DestroyImmediate(FindDeep(canvas, "Popup").gameObject);
            Assert.IsTrue(s.OpenInfo("momo"));   // owned
            pop = FindDeep(canvas, AllHeroesScreen.InfoPopupName);
            Assert.AreEqual("You own this hero", FindDeep(pop, "HowToGet").GetComponent<Text>().text);
            Assert.IsNull(FindDeep(pop, "Lock"));
        }

        [Test]
        public void TheInfoIconOpensTheExistingHeroSheetWhenTheHostProvidesIt()
        {
            string opened = null;
            var host = new AllHeroesScreen.Host { NewOverlay = () => UiKit.Rect(canvas, "Popup"), CloseOverlay = () => { }, OpenHeroSheet = id => opened = id };
            var s = new AllHeroesScreen(canvas, canvas, P, GameSession.Art, host);
            s.Build();
            Assert.IsTrue(s.OpenInfo("momo"));
            Assert.AreEqual("momo", opened, "the hero stat sheet is opened, not a new popup");
            Assert.IsNull(FindDeep(canvas, AllHeroesScreen.InfoPopupName), "no separate info popup");
        }

        [Test]
        public void NoCardTextIsACount()
        {
            Give("momo", 4);
            NewAll();
            foreach (Text t in canvas.GetComponentsInChildren<Text>(true))
            {
                int n;
                Assert.IsFalse(int.TryParse(t.text, out n), "a number on the All Heroes screen: " + t.text);
            }
        }

        // ---------------------------------------------------------------- Heroes tabs and Powers (Bug List 6 item 3a)

        PowersScreen NewPowers()
        {
            var host = new PowersScreen.Host { NewOverlay = () => UiKit.Rect(canvas, "Popup"), CloseOverlay = () => { } };
            var s = new PowersScreen(canvas, canvas, P, host);
            s.Build();
            return s;
        }

        [Test]
        public void PowersListsAllSevenPowerUpsWithTheOwnedNumberAtTheBottomRightAndNothingToBuy()
        {
            P.GrantPowerUp("pw_flight", 3);
            P.GrantPowerUp("pw_slowtime", 40);
            PowersScreen s = NewPowers();
            Assert.AreEqual(7, s.Cards.Count);
            string[] ids = { "pw_flight", "pw_rocket", "pw_doublejump", "pw_magnet", "pw_shield", "pw_slowtime", "pw_wallstick" };
            for (int i = 0; i < ids.Length; i++)
            {
                Assert.AreEqual(ids[i], s.Cards[i].Id, "owner order");
                Transform card = FindDeep(canvas, "Power_" + ids[i]);
                Assert.IsNotNull(card, ids[i]);
                Text owned = FindDeep(card, PowersScreen.OwnedName).GetComponent<Text>();
                Assert.AreEqual(P.PowerUpCount(ids[i]).ToString(), owned.text, ids[i] + ": the number owned");
                RectTransform badge = (RectTransform)owned.transform.parent;
                Assert.AreEqual(0.5f, badge.pivot.x, 0.001f, "centred horizontally");
                Assert.AreEqual(0.5f, (badge.anchorMin.x + badge.anchorMax.x) * 0.5f, 0.001f, "centred horizontally");
                Assert.GreaterOrEqual(badge.anchorMax.x - badge.anchorMin.x, 0.9f, "the number box spans the card width");
                Assert.AreEqual(0f, badge.anchorMin.y, "bottom edge");
            }
            Assert.AreEqual("3", FindDeep(FindDeep(canvas, "Power_pw_flight"), PowersScreen.OwnedName).GetComponent<Text>().text);
            Assert.AreEqual("40", FindDeep(FindDeep(canvas, "Power_pw_slowtime"), PowersScreen.OwnedName).GetComponent<Text>().text);
            foreach (Button b in canvas.GetComponentsInChildren<Button>(true)) Assert.IsFalse(b.name.StartsWith("Buy"), "no buy button: " + b.name);
        }

        [Test]
        public void PowersTapShowsOnlyNameAndDescription()
        {
            PowersScreen s = NewPowers();
            Assert.IsTrue(s.OpenInfo("pw_flight"));
            Transform pop = FindDeep(canvas, PowersScreen.InfoPopupName);
            Assert.IsNotNull(pop);
            var texts = new List<string>();
            foreach (Text t in pop.GetComponentsInChildren<Text>(true)) texts.Add(t.text);
            CollectionAssert.AreEquivalent(new[] { "Flight", P.PowerUpDefs.Get("pw_flight").Description }, texts);
            Assert.IsFalse(s.OpenInfo("pw_not_there"));
        }

        [Test]
        public void EveryHeroPageHasTheThreeTabsAndTheOwnTabIsSelected()
        {
            var seen = new List<string>();
            var all = NewAll();
            Assert.IsNotNull(all);
            AssertTabs(canvas, HeroTabs.AllHeroes);
            Object.DestroyImmediate(FindDeep(canvas, "AllHeroesBody").gameObject);
            Object.DestroyImmediate(FindDeep(canvas, "AllHeroesBackground").gameObject);
            NewMerge();
            AssertTabs(canvas, HeroTabs.Merge);
            Object.DestroyImmediate(FindDeep(canvas, "MergeBody").gameObject);
            Object.DestroyImmediate(FindDeep(canvas, "MergeBackground").gameObject);
            NewPowers();
            AssertTabs(canvas, HeroTabs.Powers);
            seen.Add("ok");
            Assert.AreEqual(1, seen.Count);
        }

        static void AssertTabs(Transform root, int selected)
        {
            Assert.AreEqual("All Heroes", HeroTabs.Captions[0]);
            Assert.AreEqual("Merge", HeroTabs.Captions[1]);
            Assert.AreEqual("Powers", HeroTabs.Captions[2]);
            for (int i = 0; i < 3; i++)
            {
                Transform tab = FindDeep(root, HeroTabs.ButtonNames[i]);
                Assert.IsNotNull(tab, HeroTabs.ButtonNames[i]);
                Assert.IsNotNull(tab.GetComponent<Button>());
                Assert.GreaterOrEqual(((RectTransform)tab).rect.height, 0f);
                Assert.AreEqual(HeroTabs.Captions[i], FindDeep(tab, "Label").GetComponent<Text>().text);
                Assert.AreEqual(i == selected, tab.GetComponent<A11yLabel>().Text.Contains("selected"), "tab " + i);
            }
            Assert.IsNull(FindDeep(root, "TabBar"), "no bottom bar");
        }

        [Test]
        public void HeroTabsHaveNoOwnedCountBadge()
        {
            // Bug List 8 item 1a/1b: no count badge on any tab of any Heroes screen
            NewAll();
            AssertNoBadge(canvas);
            Object.DestroyImmediate(FindDeep(canvas, "AllHeroesBody").gameObject);
            Object.DestroyImmediate(FindDeep(canvas, "AllHeroesBackground").gameObject);
            NewMerge();
            AssertNoBadge(canvas);
            Object.DestroyImmediate(FindDeep(canvas, "MergeBody").gameObject);
            Object.DestroyImmediate(FindDeep(canvas, "MergeBackground").gameObject);
            NewPowers();
            AssertNoBadge(canvas);
        }

        static void AssertNoBadge(Transform root)
        {
            for (int i = 0; i < 3; i++)
            {
                Transform cellT = FindDeep(root, HeroTabs.ButtonNames[i]);
                Assert.IsNotNull(cellT, HeroTabs.ButtonNames[i]);
                Assert.IsNull(cellT.Find("Count"), "no count badge on tab " + i);
                StringAssert.DoesNotContain("owned", cellT.GetComponent<A11yLabel>().Text);
            }
        }


        /// <summary>Rectangle of <paramref name="child"/> in the lower-left based space of <paramref name="cell"/>.</summary>
        static Rect Rel(RectTransform cell, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector3 a = cell.InverseTransformPoint(corners[0]), c = cell.InverseTransformPoint(corners[2]);
            Vector2 origin = new Vector2(cell.rect.xMin, cell.rect.yMin);
            return Rect.MinMaxRect(a.x - origin.x, a.y - origin.y, c.x - origin.x, c.y - origin.y);
        }

        [Test]
        public void HeroPagesUseTheMergeBackgroundArt()
        {
            NewMerge();
            Sprite merge = FindDeep(FindDeep(canvas, "MergeBackground"), "Bg").GetComponent<Image>().sprite;
            Object.DestroyImmediate(FindDeep(canvas, "MergeBackground").gameObject);
            NewAll();
            Assert.AreSame(merge, FindDeep(FindDeep(canvas, "AllHeroesBackground"), "Bg").GetComponent<Image>().sprite);
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }
    }
}
