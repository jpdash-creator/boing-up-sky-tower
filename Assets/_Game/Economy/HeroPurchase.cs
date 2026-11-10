using System.Collections.Generic;

namespace FluffyClimbers.Economy
{
    /// <summary>Outcome of buying a hero card in the Shop (Heroes section of the Character / Power-ups page).</summary>
    public enum HeroBuyResult
    {
        Ok,
        NotEnoughCoins,
        /// <summary>No hero of that rarity exists in an open region (today Rare, Elite and Legendary). Nothing is charged.</summary>
        NoHeroes,
        /// <summary>The rarity is not sold, or the picked hero has no inventory row. Nothing is charged.</summary>
        Invalid
    }

    /// <summary>
    /// The Shop "Heroes" section (Bug List 4 E2, docs/26 section 8b). Pure rules, no Unity types. Four cards: Uncommon, Rare, Elite (code tier Epic), Legendary. Buying pays COINS (no gem option, owner Bug List Shop 2a) and
    /// adds exactly ONE copy of a RANDOM hero of that rarity chosen among the heroes whose region is open (same open-region rule as the bundle card, <see cref="BundleCards"/>).
    /// The pick is repeatable from the per-account seed and a counter, never from UnityEngine.Random.
    /// </summary>
    public static class HeroPurchase
    {
        /// <summary>The sold tiers, in card order: row 1 Uncommon, Rare; row 2 Elite, Legendary.</summary>
        public static readonly CharacterRarity[] Tiers = { CharacterRarity.Uncommon, CharacterRarity.Rare, CharacterRarity.Epic, CharacterRarity.Legendary };

        /// <summary>
        /// THE ONE TABLE: coin price of one hero card per tier, same order as <see cref="Tiers"/>: Uncommon 3,000, Rare 6,000, Epic (Elite) 14,000, Legendary 30,000
        /// (the old gem prices 150, 300, 700, 1,500 x 20 coins per gem).
        /// The owner may change the numbers here only.
        /// </summary>
        public static readonly int[] Prices = { 3000, 6000, 14000, 30000 };

        /// <summary>Coin price of a hero card of this rarity, 0 when the rarity is not sold (Common).</summary>
        public static int CoinPrice(CharacterRarity rarity)
        {
            int i = System.Array.IndexOf(Tiers, rarity);
            return i >= 0 ? Prices[i] : 0;
        }

        /// <summary>The rarity word the player sees ("Elite" for Epic).</summary>
        public static string RarityName(CharacterRarity rarity)
        {
            return BundleCards.RarityName(rarity);
        }

        /// <summary>
        /// The hero picked by a roll: uniform among the heroes of exactly this rarity in an open region (id order, so the list order of CharacterDefs never changes a pick).
        /// Null when none exists.
        /// </summary>
        public static CharacterSpec Pick(CharacterRarity rarity, IList<CharacterSpec> all, System.Func<int, bool> regionOpen, ulong roll)
        {
            List<CharacterSpec> pool = BundleCards.Pool(rarity, all, regionOpen);
            return pool.Count == 0 ? null : pool[(int)(roll % (ulong)pool.Count)];
        }

        /// <summary>The roll of buy number <paramref name="counter"/> of this rarity for an account seed. Pure and repeatable.</summary>
        public static ulong Roll(int seed, int counter, CharacterRarity rarity)
        {
            return Mix(((ulong)(uint)seed << 32) ^ (ulong)(uint)counter ^ 0x4E40B0CA4DUL ^ ((ulong)(int)rarity * 0x9E37UL));
        }

        static ulong Mix(ulong z)
        {
            unchecked
            {
                z += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>
        /// Short property lines of a tier for the "i" popup, read from <see cref="TierRules"/> (docs/34 section 2b): powers and cooldown, the Jump band of the rarity (HeroIndex),
        /// the run bonus ranges. Three lines, empty list for an unknown tier.
        /// </summary>
        public static List<string> InfoLines(CharacterRarity rarity)
        {
            var lines = new List<string>(3);
            TierRule r = TierRules.RuleFor(rarity);
            if (r == null) return lines;
            lines.Add(r.PowerCount + (r.PowerCount == 1 ? " hero power" : " hero powers") + ", ready again after " + r.CooldownSeconds + " s");
            lines.Add(TierRules.JumpBandText(rarity) + " over the default hero, Speed, Control and Wall Bounce set by limits");
            lines.Add("Run bonus +" + r.RunCoinsMin + " to " + r.RunCoinsMax + " coins, +" + r.RunGemsMin + " to " + r.RunGemsMax + " gems");
            return lines;
        }
    }
}
