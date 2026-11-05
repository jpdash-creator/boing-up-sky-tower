using System.Collections.Generic;
using System.Linq;
using BoingUpSkyTower.Economy;
using Xunit;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>Hero cards (one card per owned copy, no counts), the merge list and selection rule, and the All Heroes grouping (docs/34 section 10).</summary>
    public class HeroCardsTests
    {
        static CharacterSpec S(string id) { return CharacterDefs.Get(id); }

        static System.Func<string, int> Copies(params object[] pairs)
        {
            var d = new Dictionary<string, int>();
            for (int i = 0; i < pairs.Length; i += 2) d[(string)pairs[i]] = (int)pairs[i + 1];
            return id => d.TryGetValue(id, out int n) ? n : 0;
        }

        [Fact]
        public void OwnedListHasOneCardPerCopyWithTheSameHero()
        {
            var owned = new[] { S("momo"), S("momo_u"), S("forestgirl") };
            List<HeroCard> cards = HeroCards.Owned(owned, Copies("momo", 2, "momo_u", 1, "forestgirl", 1));
            Assert.Equal(4, cards.Count);
            Assert.Equal(new[] { "momo", "momo", "momo_u", "forestgirl" }, cards.Select(c => c.Id).ToArray());
            Assert.Equal(new[] { 0, 1, 0, 0 }, cards.Select(c => c.Copy).ToArray());
            Assert.True(cards[0].IsFirstOfType);
            Assert.False(cards[1].IsFirstOfType);
        }

        [Fact]
        public void AVisibleHeroAlwaysHasACardAndTheStackIsCapped()
        {
            Assert.Single(HeroCards.Owned(new[] { S("momo") }, Copies("momo", 0)));
            Assert.Equal(MergeRules.MaxCopies, HeroCards.Owned(new[] { S("momo") }, Copies("momo", 500)).Count);
        }

        [Fact]
        public void MergeListShowsOnlyTypesWithThreeOrMoreCopiesAsIndividualCards()
        {
            var owned = new[] { S("momo"), S("forestgirl"), S("candygirl") };
            List<HeroCard> a = HeroCards.Mergeable(owned, Copies("momo", 3, "forestgirl", 2));
            Assert.Equal(new[] { "momo", "momo", "momo" }, a.Select(c => c.Id).ToArray());
            List<HeroCard> b = HeroCards.Mergeable(owned, Copies("momo", 4, "forestgirl", 3, "candygirl", 2));
            Assert.Equal(7, b.Count);
            Assert.Equal(4, b.Count(c => c.Id == "momo"));
            Assert.Equal(3, b.Count(c => c.Id == "forestgirl"));
            Assert.DoesNotContain(b, c => c.Id == "candygirl");
        }

        [Fact]
        public void TopTierHeroesNeverAppearInTheMergeList()
        {
            // no Rare exists yet, so even 5 Uncommon copies cannot merge
            Assert.Empty(HeroCards.Mergeable(new[] { S("momo_u") }, Copies("momo_u", 5)));
        }

        [Fact]
        public void OnlyThreeCardsOfTheSameTypeEnableMerge()
        {
            var cards = HeroCards.Mergeable(new[] { S("momo"), S("forestgirl") }, Copies("momo", 4, "forestgirl", 3));
            var picked = new List<HeroCard>();
            Assert.False(HeroCards.IsMergeSet(picked));
            Assert.True(HeroCards.Toggle(picked, cards[0]));
            Assert.True(HeroCards.Toggle(picked, cards[1]));
            Assert.False(HeroCards.IsMergeSet(picked));
            Assert.True(HeroCards.Toggle(picked, cards[2]));
            Assert.True(HeroCards.IsMergeSet(picked));
            Assert.False(HeroCards.IsMixed(picked));
            Assert.False(HeroCards.Toggle(picked, cards[3]));
            Assert.Equal(3, picked.Count);
            // swap one Momo for a Capi: a wrong mix
            Assert.True(HeroCards.Toggle(picked, cards[2]));
            Assert.True(HeroCards.Toggle(picked, cards[4]));
            Assert.Equal(3, picked.Count);
            Assert.True(HeroCards.IsMixed(picked));
            Assert.False(HeroCards.IsMergeSet(picked));
        }

        [Fact]
        public void ToggleTwiceDeselects()
        {
            var cards = HeroCards.Mergeable(new[] { S("momo") }, Copies("momo", 3));
            var picked = new List<HeroCard>();
            HeroCards.Toggle(picked, cards[1]);
            Assert.Single(picked);
            HeroCards.Toggle(picked, cards[1]);
            Assert.Empty(picked);
        }

        [Fact]
        public void PruneDropsCardsThatAreGoneAfterAMerge()
        {
            var before = HeroCards.Mergeable(new[] { S("momo") }, Copies("momo", 3));
            var after = HeroCards.Mergeable(new[] { S("momo") }, Copies("momo", 0));
            Assert.Empty(HeroCards.Prune(before, after));
            var still = HeroCards.Mergeable(new[] { S("momo") }, Copies("momo", 4));
            Assert.Equal(3, HeroCards.Prune(before, still).Count);
        }

        static List<AllHeroGroup> Groups(params string[] owned)
        {
            var set = new HashSet<string>(owned);
            return AllHeroes.Build(i => "Region name " + i, id => set.Contains(id));
        }

        [Fact]
        public void AllHeroesGroupsByRegionWithTheWholeLadderPerHero()
        {
            List<AllHeroGroup> g = Groups("momo");
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, g.Select(x => x.Region).ToArray());
            Assert.Equal("Region name 1", g[0].RegionName);
            foreach (AllHeroGroup grp in g)
            {
                Assert.Equal(5, grp.Entries.Count);
                Assert.Equal(new[] { CharacterRarity.Common, CharacterRarity.Uncommon, CharacterRarity.Rare, CharacterRarity.Epic, CharacterRarity.Legendary }, grp.Entries.Select(e => e.Rarity).ToArray());
            }
            Assert.Equal(25, AllHeroes.Flatten(g).Count);
        }

        [Fact]
        public void EverythingTheMasterListHasIsInAllHeroes()
        {
            var ids = AllHeroes.Flatten(Groups()).Where(e => !e.Placeholder).Select(e => e.Id).ToList();
            foreach (CharacterSpec c in CharacterInventory.All) Assert.Contains(c.Id, ids);
        }

        [Fact]
        public void LockedStateFollowsOwnershipAndRareEliteLegendaryAreAlwaysLockedPlaceholders()
        {
            List<AllHeroEntry> all = AllHeroes.Flatten(Groups("momo", "forestgirl"));
            Assert.False(all.First(e => e.Id == "momo").Locked);
            Assert.False(all.First(e => e.Id == "forestgirl").Locked);
            Assert.True(all.First(e => e.Id == "momo_u").Locked);
            Assert.True(all.First(e => e.Id == "beachotter").Locked);
            foreach (AllHeroEntry e in all.Where(x => x.Rarity >= CharacterRarity.Rare))
            {
                Assert.True(e.Locked, e.Id);
                Assert.True(e.Placeholder, e.Id);
                Assert.Null(e.Spec);
                Assert.NotNull(e.BaseSpec);
            }
            AllHeroEntry rare = all.First(e => e.Id == "momo_rare");
            Assert.Equal("Rare Momo", rare.Name);
            Assert.Equal("Merge 3 Uncommon Momo", rare.HowToGet);
        }

        [Fact]
        public void HowToGetNamesTheMergeSourceOrTheRegion()
        {
            List<AllHeroEntry> all = AllHeroes.Flatten(Groups());
            Assert.Equal("Merge 3 Common Momo", all.First(e => e.Id == "momo_u").HowToGet);
            Assert.Equal("Merge 3 Elite Bao", all.First(e => e.Id == "monk_legendary").HowToGet);
            Assert.Contains("Region name 3", all.First(e => e.Id == "beachotter").HowToGet);
            Assert.Contains("Bundles / Chests / Rewards", all.First(e => e.Id == "beachotter").HowToGet);
        }

        [Fact]
        public void PlaceholderIdsNeverClashWithRealCharacters()
        {
            foreach (AllHeroEntry e in AllHeroes.Flatten(Groups()).Where(x => x.Placeholder))
                Assert.Null(CharacterDefs.Get(e.Id));
        }
    }
}
