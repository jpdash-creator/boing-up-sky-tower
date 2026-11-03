using System.Collections.Generic;

namespace BoingUpSkyTower.Economy
{
    /// <summary>
    /// The hidden hero card inside every bundle (owner Issue 34 item 4, docs/37-bundles.md). Pure rules, no Unity types, so the simulation test project compiles it too.
    /// Exactly ONE card per bundle, drawn when the bundle is BOUGHT (never shown before): Uncommon or better, weights Uncommon 100, Rare 40, Elite 20, Legendary 5
    /// (about 60.6, 24.2, 12.1 and 3.0 percent), then a character picked uniformly among those of that tier that exist.
    /// Characters are read from <see cref="CharacterDefs.All"/> (or any list passed in), so a new Rare, Elite or Legendary hero is picked up the moment its row exists.
    /// </summary>
    public static class BundleCards
    {
        /// <summary>The tiers a bundle card can be, lowest first. (CharacterRarity.Epic is shown to players as "Elite".)</summary>
        public static readonly CharacterRarity[] Tiers = { CharacterRarity.Uncommon, CharacterRarity.Rare, CharacterRarity.Epic, CharacterRarity.Legendary };

        /// <summary>Draw weights per tier, same order as <see cref="Tiers"/>. Common is never drawn.</summary>
        public static readonly int[] Weights = { 100, 40, 20, 5 };

        /// <summary>
        /// Dollar value of one card per tier (placeholders, owner may change them here only; the generator reads the same numbers from
        /// Tools/content_cli/gen_bundles.py, keep both in step: a test compares them). Used for the "Worth $X" tag and the price rule.
        /// </summary>
        public static readonly double[] TierUsd = { 2.99, 7.99, 19.99, 49.99 };

        public static int WeightTotal
        {
            get { int t = 0; for (int i = 0; i < Weights.Length; i++) t += Weights[i]; return t; }
        }

        /// <summary>Expected dollar value of the hidden card: sum of weight x tier value / total weight (7.687 with the placeholders). Constant for every bundle.</summary>
        public static double ExpectedUsd
        {
            get
            {
                double v = 0.0;
                for (int i = 0; i < Tiers.Length; i++) v += Weights[i] * TierUsd[i];
                return v / WeightTotal;
            }
        }

        /// <summary>Dollars per gem: a copy of SubscriptionCatalog.UsdPerGem (that class needs Newtonsoft, this file must stay compilable by the simulation tests); a Unity test checks they are equal.</summary>
        public const double UsdPerGem = 0.011;

        /// <summary>Expected card value in gems at the shop rate.</summary>
        public static double ExpectedGems { get { return ExpectedUsd / UsdPerGem; } }

        /// <summary>Maps a roll (any ulong) to the drawn tier by the weights. Pure and repeatable.</summary>
        public static CharacterRarity DrawTier(ulong roll)
        {
            ulong r = roll % (ulong)WeightTotal;
            for (int i = 0; i < Tiers.Length; i++)
            {
                if (r < (ulong)Weights[i]) return Tiers[i];
                r -= (ulong)Weights[i];
            }
            return Tiers[Tiers.Length - 1];
        }

        /// <summary>The characters of exactly this rarity that can be a bundle card, in list order (region Commons are never cards; merge tiers count).</summary>
        public static List<CharacterSpec> Pool(CharacterRarity rarity, IList<CharacterSpec> all)
        {
            var list = new List<CharacterSpec>(8);
            if (all == null || rarity < CharacterRarity.Uncommon) return list;
            foreach (CharacterSpec c in all) if (c != null && c.Rarity == rarity) list.Add(c);
            list.Sort((x, y) => string.CompareOrdinal(x.Id, y.Id));   // stable: a draw does not change when the list order of CharacterDefs changes
            return list;
        }

        /// <summary>
        /// The tier that is really used for a drawn tier. When the drawn tier has no characters yet, the HIGHEST existing tier at or below it is used
        /// (Elite with no Elite heroes gives Rare, or Uncommon when there is no Rare either). If nothing exists at or below, the lowest existing tier above it.
        /// Returns false when no Uncommon-or-better character exists at all.
        /// </summary>
        public static bool TryResolveTier(CharacterRarity drawn, IList<CharacterSpec> all, out CharacterRarity resolved)
        {
            resolved = drawn;
            int start = System.Array.IndexOf(Tiers, drawn);
            if (start < 0) start = 0;
            for (int i = start; i >= 0; i--)
                if (Pool(Tiers[i], all).Count > 0) { resolved = Tiers[i]; return true; }
            for (int i = start + 1; i < Tiers.Length; i++)
                if (Pool(Tiers[i], all).Count > 0) { resolved = Tiers[i]; return true; }
            return false;
        }

        /// <summary>One card from two rolls (tier, then character). Null only when no Uncommon-or-better character exists.</summary>
        public static CharacterSpec Draw(ulong tierRoll, ulong characterRoll, IList<CharacterSpec> all)
        {
            CharacterRarity tier;
            if (!TryResolveTier(DrawTier(tierRoll), all, out tier)) return null;
            List<CharacterSpec> pool = Pool(tier, all);
            return pool[(int)(characterRoll % (ulong)pool.Count)];
        }

        /// <summary>Rarity word as the player sees it ("Elite" for Epic).</summary>
        public static string RarityName(CharacterRarity r)
        {
            int i = (int)r;
            return i >= 0 && i < CharacterDefs.RarityNames.Length ? CharacterDefs.RarityNames[i] : r.ToString();
        }
    }
}
