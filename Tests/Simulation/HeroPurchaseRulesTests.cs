using System;
using System.Collections.Generic;
using System.Linq;
using FluffyClimbers.Economy;
using Xunit;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>The pure rules of the Shop hero cards (Bug List 4 E2): the one price table, the pick among open-region heroes of the right rarity, repeatable rolls, the tier info lines.</summary>
    public class HeroPurchaseRulesTests
    {
        static CharacterSpec Base(string id, int region) { return new CharacterSpec { Id = id, Name = id, Rarity = CharacterRarity.Common, Region = region }; }
        static CharacterSpec Tier(string id, string line, CharacterRarity r) { return new CharacterSpec { Id = id, Name = id, Rarity = r, Line = line, IsMergeTier = true }; }

        [Fact]
        public void PriceTableIsUncommon3000Rare6000Elite14000Legendary30000Coins()
        {
            Assert.Equal(new[] { 3000, 6000, 14000, 30000 }, HeroPurchase.Tiers.Select(HeroPurchase.CoinPrice).ToArray());
            Assert.Equal(0, HeroPurchase.CoinPrice(CharacterRarity.Common));
            Assert.Equal(new[] { "Uncommon", "Rare", "Elite", "Legendary" }, HeroPurchase.Tiers.Select(HeroPurchase.RarityName).ToArray());
        }

        [Fact]
        public void PickIsOfTheRightRarityFromOpenRegionsOnly()
        {
            var all = new List<CharacterSpec>
            {
                Base("b1", 1), Base("b2", 2), Base("b3", 3),
                Tier("r1", "b1", CharacterRarity.Rare), Tier("r2", "b2", CharacterRarity.Rare), Tier("r3", "b3", CharacterRarity.Rare),
                Tier("u2", "b2", CharacterRarity.Uncommon),
            };
            Func<int, bool> only2 = region => region == 2;
            var seen = new HashSet<string>();
            for (ulong roll = 0; roll < 200; roll++)
            {
                CharacterSpec c = HeroPurchase.Pick(CharacterRarity.Rare, all, only2, roll);
                Assert.Equal(CharacterRarity.Rare, c.Rarity);
                seen.Add(c.Id);
            }
            Assert.True(seen.SetEquals(new[] { "r1", "r2" }));
            Assert.Null(HeroPurchase.Pick(CharacterRarity.Legendary, all, only2, 1));
            Assert.Null(HeroPurchase.Pick(CharacterRarity.Epic, all, only2, 1));
        }

        [Fact]
        public void RollIsRepeatableAndVaries()
        {
            Assert.Equal(HeroPurchase.Roll(9, 2, CharacterRarity.Rare), HeroPurchase.Roll(9, 2, CharacterRarity.Rare));
            Assert.NotEqual(HeroPurchase.Roll(9, 2, CharacterRarity.Rare), HeroPurchase.Roll(9, 3, CharacterRarity.Rare));
            Assert.NotEqual(HeroPurchase.Roll(9, 2, CharacterRarity.Rare), HeroPurchase.Roll(10, 2, CharacterRarity.Rare));
            Assert.NotEqual(HeroPurchase.Roll(9, 2, CharacterRarity.Rare), HeroPurchase.Roll(9, 2, CharacterRarity.Legendary));
        }

        [Fact]
        public void RegionOneHasOnlyMomosLineAtEveryTierWhileNothingElseIsOpen()
        {
            // owner 2026-11-09: only the five active lines exist; Region 1 is Momo's line at every tier (closed regions never count, inactive heroes never exist)
            foreach (CharacterRarity r in HeroPurchase.Tiers)
                Assert.Equal(new[] { "momo" }, BundleCards.Pool(r, CharacterDefs.All, region => region <= 1).Select(c => c.LineId).OrderBy(x => x, System.StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void InfoLinesReadTheTierRules()
        {
            foreach (CharacterRarity r in HeroPurchase.Tiers)
            {
                TierRule rule = TierRules.RuleFor(r);
                List<string> lines = HeroPurchase.InfoLines(r);
                Assert.Equal(3, lines.Count);
                Assert.Contains(rule.PowerCount.ToString(), lines[0]);
                Assert.Contains(rule.CooldownSeconds + " s", lines[0]);
                Assert.Contains(TierRules.JumpBandText(r), lines[1]);
                Assert.Contains(rule.RunCoinsMin + " to " + rule.RunCoinsMax + " coins", lines[2]);
            }
            Assert.Empty(HeroPurchase.InfoLines(CharacterRarity.Common));
        }
    }
}
