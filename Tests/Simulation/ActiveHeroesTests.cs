using System;
using System.Collections.Generic;
using System.Linq;
using BoingUpSkyTower.Economy;
using Xunit;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>
    /// Owner Bug List 6 (2026-11-09): only five hero lines are ACTIVE (Momo, Capi, Otto, Bonbon, Bao) = 25 heroes. The other 20 lines (100 heroes) keep their generated physics rows as DATA
    /// (HeroTable, CharacterDefs.AllIncludingInactive) but never exist at runtime: not in CharacterDefs.All, not in any pool, shop, reward, region goal, All Heroes screen or save.
    /// </summary>
    public class ActiveHeroesTests
    {
        static readonly string[] ActiveLines = { "momo", "forestgirl", "beachotter", "candygirl", "monk" };
        static readonly string[] InactiveLines =
        {
            "bumble", "ribbi", "dandy", "pip", "nib", "spike", "moss", "fenn", "pinch", "shelly", "bloop", "coco", "gummi", "choco", "marsh", "cookie", "sprout", "tora", "rusu", "ryu"
        };

        static ulong Mix(ref ulong z)
        {
            unchecked
            {
                z += 0x9E3779B97F4A7C15UL;
                ulong x = z;
                x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
                x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
                return x ^ (x >> 31);
            }
        }

        static bool IsInactiveId(string id)
        {
            return InactiveLines.Any(l => id == l || id.StartsWith(l + "_", StringComparison.Ordinal));
        }

        [Fact]
        public void ExactlyTwentyFiveActiveHeroesFiveLinesTimesFiveTiers()
        {
            Assert.Equal(25, CharacterDefs.All.Length);
            Assert.Equal(25, CharacterDefs.All.Select(c => c.Id).Distinct().Count());
            Assert.Equal(new HashSet<string>(ActiveLines), new HashSet<string>(CharacterDefs.All.Select(c => c.LineId)));
            Assert.Equal(new HashSet<string>(ActiveLines), new HashSet<string>(HeroTable.ActiveLines));
            foreach (string line in ActiveLines)
            {
                CharacterSpec[] tiers = CharacterDefs.All.Where(c => c.LineId == line).OrderBy(c => (int)c.Rarity).ToArray();
                Assert.Equal(5, tiers.Length);
                Assert.Equal(new[] { CharacterRarity.Common, CharacterRarity.Uncommon, CharacterRarity.Rare, CharacterRarity.Epic, CharacterRarity.Legendary }, tiers.Select(c => c.Rarity).ToArray());
            }
            Assert.Equal(5, CharacterDefs.All.Count(c => !c.IsMergeTier));
            Assert.All(CharacterDefs.All, c => Assert.True(CharacterDefs.IsActive(c)));
        }

        [Fact]
        public void TheHeroTableKeepsAll125RowsWithExactly25Active()
        {
            Assert.Equal(125, HeroTable.Rows.Length);
            Assert.Equal(25, HeroTable.Rows.Count(r => r.Active));
            Assert.Equal(100, HeroTable.Rows.Count(r => !r.Active));
            foreach (HeroRow r in HeroTable.Rows)
            {
                Assert.Equal(ActiveLines.Contains(r.Line), r.Active);
                Assert.Equal(r.Active, HeroTable.IsActiveLine(r.Line));
            }
            Assert.Equal(125, CharacterDefs.AllIncludingInactive.Length);
            Assert.Equal(125, CharacterDefs.AllIncludingInactive.Select(c => c.Id).Distinct().Count());
            foreach (string id in InactiveLines.SelectMany(l => HeroTable.TierSuffix.Select(s => l + s)))
            {
                Assert.NotNull(HeroTable.Find(id));
                Assert.NotNull(CharacterDefs.GetIncludingInactive(id));
                Assert.Null(CharacterDefs.Get(id));   // an inactive id is not a character at runtime (saves fall back to Momo)
                Assert.False(CharacterInventory.Exists(id));
            }
        }

        [Fact]
        public void NoInactiveIdIsInAnyPool()
        {
            Func<int, bool>[] openings = { null, r => false, r => r <= 2, r => r <= 3, r => true };
            foreach (Func<int, bool> open in openings)
            {
                foreach (CharacterRarity rarity in Enum.GetValues(typeof(CharacterRarity)).Cast<CharacterRarity>())
                {
                    foreach (CharacterSpec c in BundleCards.Pool(rarity, CharacterDefs.All, open)) Assert.False(IsInactiveId(c.Id), "bundle/shop pool " + c.Id);
                    foreach (CharacterSpec c in ChestRolls.Pool(rarity, CharacterDefs.All, open)) Assert.False(IsInactiveId(c.Id), "chest pool " + c.Id);
                }
            }
            foreach (CharacterSpec c in CharacterDefs.OfRarity(CharacterRarity.Common)) Assert.False(IsInactiveId(c.Id));
            foreach (CharacterRarity rarity in HeroPurchase.Tiers)
                foreach (CharacterSpec c in BundleCards.Pool(rarity, CharacterDefs.All, null))
                {
                    Assert.False(IsInactiveId(c.Id), c.Id);
                    Assert.Equal(rarity, c.Rarity);
                }
            foreach (CharacterSpec c in CharacterInventory.All) Assert.False(IsInactiveId(c.Id), c.Id);
        }

        [Fact]
        public void RollsOverManySeedsNeverReturnAnInactiveHero()
        {
            ulong s = 20261109;
            Func<int, bool>[] openings = { null, r => false, r => r <= 3 };
            foreach (Func<int, bool> open in openings)
                for (int i = 0; i < 20000; i++)
                {
                    foreach (ChestKind kind in new[] { ChestKind.Iron, ChestKind.Golden })
                    {
                        CharacterSpec c = ChestRolls.DrawHero(kind, Mix(ref s), Mix(ref s), CharacterDefs.All, open);
                        Assert.NotNull(c);
                        Assert.False(IsInactiveId(c.Id), "chest " + c.Id);
                    }
                    CharacterSpec b = BundleCards.Draw(Mix(ref s), Mix(ref s), CharacterDefs.All, open);
                    Assert.NotNull(b);
                    Assert.False(IsInactiveId(b.Id), "bundle " + b.Id);
                    CharacterRarity tier = HeroPurchase.Tiers[(int)(Mix(ref s) % (ulong)HeroPurchase.Tiers.Length)];
                    CharacterSpec h = HeroPurchase.Pick(tier, CharacterDefs.All, open, Mix(ref s));
                    if (h != null) { Assert.False(IsInactiveId(h.Id), "hero shop " + h.Id); Assert.Equal(tier, h.Rarity); }
                }
            // the account-seeded shop roll as the game uses it
            for (int seed = 0; seed < 500; seed++)
                foreach (CharacterRarity tier in HeroPurchase.Tiers)
                {
                    CharacterSpec h = HeroPurchase.Pick(tier, CharacterDefs.All, null, HeroPurchase.Roll(seed * 7919, seed, tier));
                    Assert.NotNull(h);
                    Assert.False(IsInactiveId(h.Id), h.Id);
                }
        }

        [Fact]
        public void TheRarityIsRolledFirstThenAnActiveHeroOfThatRarity()
        {
            // the odds of the rarity are unchanged (Iron 70/20/10, Golden 50/40/10, bundles 70/20/10); every region open, so no fallback happens
            ulong s = 99;
            const int N = 60000;
            var iron = new Dictionary<CharacterRarity, int>();
            for (int i = 0; i < N; i++)
            {
                CharacterSpec c = ChestRolls.DrawHero(ChestKind.Iron, Mix(ref s), Mix(ref s), CharacterDefs.All, null);
                int n; iron.TryGetValue(c.Rarity, out n); iron[c.Rarity] = n + 1;
            }
            Assert.InRange(iron[CharacterRarity.Common] / (double)N, 0.68, 0.72);
            Assert.InRange(iron[CharacterRarity.Uncommon] / (double)N, 0.18, 0.22);
            Assert.InRange(iron[CharacterRarity.Rare] / (double)N, 0.08, 0.12);
            var bundle = new Dictionary<CharacterRarity, int>();
            for (int i = 0; i < N; i++)
            {
                CharacterSpec c = BundleCards.Draw(Mix(ref s), Mix(ref s), CharacterDefs.All, null);
                int n; bundle.TryGetValue(c.Rarity, out n); bundle[c.Rarity] = n + 1;
            }
            Assert.InRange(bundle[CharacterRarity.Uncommon] / (double)N, 0.68, 0.72);
            Assert.InRange(bundle[CharacterRarity.Rare] / (double)N, 0.18, 0.22);
            Assert.InRange(bundle[CharacterRarity.Epic] / (double)N, 0.08, 0.12);
            // each pool of a rarity holds the five active lines at that rarity
            foreach (CharacterRarity r in HeroPurchase.Tiers)
                Assert.Equal(ActiveLines.OrderBy(x => x, StringComparer.Ordinal).ToArray(), BundleCards.Pool(r, CharacterDefs.All, null).Select(c => c.LineId).OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void RegionGoalsNameNoHeroAndGiveOnlyTheStarterOrARandomActiveCommon()
        {
            for (int region = 1; region <= RegionGoalTable.PlayableRegions; region++)
            {
                int random = 0, starter = 0;
                foreach (RegionGoal g in RegionGoalTable.Build(region))
                {
                    Assert.True(string.IsNullOrEmpty(g.HeroId), g.Id + " must not name a hero");
                    if (g.HeroRegion == RewardEntry.RandomOpenRegionHero) random++;
                    else if (g.HeroRegion != 0) { Assert.Equal(region, g.HeroRegion); starter++; }
                }
                Assert.Equal(5, random);    // clear 6, 9 and 12 levels, earn 45 stars, 25 gold medal levels: each one random ACTIVE Common hero of an open region
                Assert.Equal(1, starter);   // clear all levels: the region's starter (active)
            }
        }

        [Fact]
        public void EveryActiveStarterOfARegionExists()
        {
            for (int region = 1; region <= 5; region++)
                Assert.Single(CharacterDefs.All, c => c.RegionStarter && c.Region == region);
        }

        [Fact]
        public void AllHeroesListsOnlyTheTwentyFiveActiveHeroes()
        {
            List<AllHeroEntry> all = AllHeroes.Flatten(AllHeroes.Build(i => "Region " + i, id => false));
            Assert.Equal(25, all.Count);
            Assert.All(all, e => { Assert.False(e.Placeholder, e.Id); Assert.False(IsInactiveId(e.Id), e.Id); });
            Assert.Equal(25, all.Select(e => e.Id).Distinct().Count());
        }

        [Fact]
        public void ActiveHeroesPlayWithTheirTableRowsAndNoMergeLineLeadsToAnInactiveHero()
        {
            foreach (CharacterSpec c in CharacterDefs.All)
            {
                HeroRow row = HeroTable.Find(c.Id);
                Assert.NotNull(row);
                Assert.True(row.Active, c.Id);
                CharacterSpec next = MergeRules.NextTier(c);
                if (next != null) Assert.False(IsInactiveId(next.Id));
                Assert.Equal(c.Rarity == CharacterRarity.Legendary, next == null);
            }
        }
    }
}
