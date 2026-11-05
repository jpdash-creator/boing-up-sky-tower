using System.Collections.Generic;

namespace BoingUpSkyTower.Economy
{
    public enum ChestKind
    {
        Iron = 0,
        Golden = 1
    }

    /// <summary>
    /// The pure rules of the Treasure Chests (owner Bug List 5 F3, docs/26 section on chests, docs/37). No Unity types, so the simulation test project compiles this file too.
    /// A chest holds coins, power-ups and exactly ONE hero card, drawn the same way as the hidden card of a bundle (<see cref="BundleCards"/>): a tier by the chest's odds,
    /// then a hero uniformly among the heroes of that tier whose REGION IS OPEN for the player. A tier with no hero in an open region falls back to the next lower tier that has one.
    /// Iron: 70 Common, 20 Uncommon, 10 Rare. Gold: 50 Uncommon, 40 Rare, 10 Elite (code tier Epic). Common heroes are the base hero of each open region (Momo is region 1).
    /// A duplicate hero is simply another copy (a new card); nothing is refunded.
    /// </summary>
    public static partial class ChestRolls
    {
        public const int IronPowerUps = 1, GoldenPowerUps = 3;
        /// <summary>Retuned for Bug List 5 F3 so the worth lands in 1.5 to 2.5 including the expected hero card (was 400 to 600 and 1,000 to 1,400).</summary>
        public const int IronMinCoins = 800, IronMaxCoins = 1200, GoldenMinCoins = 3000, GoldenMaxCoins = 4200;
        public const int IronPrice = 100, IronPriceTen = 1000, GoldenPrice = 300, GoldenPriceTen = 3000;
        /// <summary>The shop rate used in every worth number: 20 coins are worth 1 gem (same as bundles).</summary>
        public const int CoinsPerGem = 20;
        /// <summary>The worth must stay inside this band (Bug List 2 C2.b.ii, same as bundles).</summary>
        public const double WorthMin = 1.5, WorthMax = 2.5;

        /// <summary>Tiers of the hero card, lowest first. Iron: Common, Uncommon, Rare. Gold: Uncommon, Rare, Epic (shown as "Elite").</summary>
        public static readonly CharacterRarity[] IronTiers = { CharacterRarity.Common, CharacterRarity.Uncommon, CharacterRarity.Rare };
        public static readonly CharacterRarity[] GoldenTiers = { CharacterRarity.Uncommon, CharacterRarity.Rare, CharacterRarity.Epic };
        /// <summary>Draw weights per tier (they add up to 100, so they are also the percentages).</summary>
        public static readonly int[] IronWeights = { 70, 20, 10 };
        public static readonly int[] GoldenWeights = { 50, 40, 10 };

        /// <summary>
        /// THE ONE TABLE for the gem value of a hero card in a worth number: Uncommon, Rare, Epic come from <see cref="BundleCards.TierGems"/> (100, 250, 600), so chests and bundles
        /// value a card identically; a Common card is worth <see cref="CommonCardGems"/>. These are WORTH values (what the card counts for in "Worth x..."), deliberately below the
        /// retail prices of the Heroes shop (<see cref="HeroPurchase.Prices"/>: 150, 300, 700, 1,500), because a card from a chest or bundle is random.
        /// </summary>
        public const int CommonCardGems = 40;

        public static CharacterRarity[] Tiers(ChestKind kind) { return kind == ChestKind.Iron ? IronTiers : GoldenTiers; }
        public static int[] Weights(ChestKind kind) { return kind == ChestKind.Iron ? IronWeights : GoldenWeights; }

        /// <summary>Gem value of one hero card of this rarity in a worth number (0 for a rarity a chest never gives).</summary>
        public static int CardGems(CharacterRarity rarity)
        {
            if (rarity == CharacterRarity.Common) return CommonCardGems;
            int i = System.Array.IndexOf(BundleCards.Tiers, rarity);
            return i >= 0 ? BundleCards.TierGems[i] : 0;
        }

        /// <summary>Chance in whole percent of tier index <paramref name="tierIndex"/> of the chest (the numbers the info popup shows).</summary>
        public static int ChancePercent(ChestKind kind, int tierIndex)
        {
            int[] w = Weights(kind);
            return tierIndex >= 0 && tierIndex < w.Length ? w[tierIndex] : 0;
        }

        /// <summary>Expected gem value of the hero card of one chest: sum of chance x card gems. Iron 73, Gold 210.</summary>
        public static double ExpectedCardGems(ChestKind kind)
        {
            CharacterRarity[] t = Tiers(kind);
            int[] w = Weights(kind);
            double v = 0.0, total = 0.0;
            for (int i = 0; i < t.Length; i++) { v += (double)w[i] * CardGems(t[i]); total += w[i]; }
            return v / total;
        }

        public static int Price(ChestKind kind, int opens)
        {
            if (opens != 1 && opens != 10) return 0;
            if (kind == ChestKind.Iron) return opens == 1 ? IronPrice : IronPriceTen;
            return opens == 1 ? GoldenPrice : GoldenPriceTen;
        }

        public static int MinCoins(ChestKind kind) { return kind == ChestKind.Iron ? IronMinCoins : GoldenMinCoins; }
        public static int MaxCoins(ChestKind kind) { return kind == ChestKind.Iron ? IronMaxCoins : GoldenMaxCoins; }
        public static int PowerUpCount(ChestKind kind) { return kind == ChestKind.Iron ? IronPowerUps : GoldenPowerUps; }

        /// <summary>
        /// Worth multiplier of ONE chest at its Open 1 price (Open 10 is ten of them for ten times the gems, so the same): (average coins / 20 + power-ups x the average gem price of one
        /// random power-up + expected hero card gems) / gems paid. <paramref name="avgPowerUpGems"/> is the mean gem price of the implemented power-up types (60.71 today).
        /// </summary>
        public static double WorthMultiplier(ChestKind kind, double avgPowerUpGems)
        {
            double coins = (MinCoins(kind) + MaxCoins(kind)) / 2.0 / CoinsPerGem;
            return (coins + PowerUpCount(kind) * avgPowerUpGems + ExpectedCardGems(kind)) / Price(kind, 1);
        }

        /// <summary>"Worth x1.8" (one decimal, invariant culture): the tag on the chest card, the same words as a bundle.</summary>
        public static string WorthText(ChestKind kind, double avgPowerUpGems)
        {
            return "Worth x" + WorthMultiplier(kind, avgPowerUpGems).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        // ---------------- the hero draw ----------------

        /// <summary>Maps a roll (any ulong) to the drawn tier by the chest's weights. Pure and repeatable.</summary>
        public static CharacterRarity DrawTier(ChestKind kind, ulong roll)
        {
            CharacterRarity[] t = Tiers(kind);
            int[] w = Weights(kind);
            ulong total = 0;
            for (int i = 0; i < w.Length; i++) total += (ulong)w[i];
            ulong r = roll % total;
            for (int i = 0; i < t.Length; i++)
            {
                if (r < (ulong)w[i]) return t[i];
                r -= (ulong)w[i];
            }
            return t[t.Length - 1];
        }

        static bool Open(System.Func<int, bool> regionOpen, int region)
        {
            return regionOpen == null || region <= 1 || regionOpen(region);
        }

        /// <summary>
        /// The heroes of exactly this rarity a chest can give, in id order. Common: the base hero of each open region. Uncommon and up: the bundle pool (merge tiers of open regions).
        /// <paramref name="regionOpen"/> gets the region index (2 and up); null = every region is open.
        /// </summary>
        public static List<CharacterSpec> Pool(CharacterRarity rarity, IList<CharacterSpec> all, System.Func<int, bool> regionOpen)
        {
            if (rarity != CharacterRarity.Common) return BundleCards.Pool(rarity, all, regionOpen);
            var list = new List<CharacterSpec>(6);
            if (all == null) return list;
            foreach (CharacterSpec c in all)
                if (c != null && c.IsRegionHero && !c.IsMergeTier && Open(regionOpen, c.Region)) list.Add(c);
            list.Sort((x, y) => string.CompareOrdinal(x.Id, y.Id));
            return list;
        }

        static readonly CharacterRarity[] Ladder = { CharacterRarity.Common, CharacterRarity.Uncommon, CharacterRarity.Rare, CharacterRarity.Epic };

        /// <summary>
        /// The tier really used for a drawn tier: the drawn one when it has a hero in an open region, else the HIGHEST lower tier that has one (Elite with nothing open at Elite or Rare
        /// gives Uncommon). If nothing exists at or below, the lowest tier above it. False when no hero of Common..Elite exists in an open region at all.
        /// </summary>
        public static bool TryResolveTier(CharacterRarity drawn, IList<CharacterSpec> all, System.Func<int, bool> regionOpen, out CharacterRarity resolved)
        {
            resolved = drawn;
            int start = System.Array.IndexOf(Ladder, drawn);
            if (start < 0) start = drawn > Ladder[Ladder.Length - 1] ? Ladder.Length - 1 : 0;
            for (int i = start; i >= 0; i--)
                if (Pool(Ladder[i], all, regionOpen).Count > 0) { resolved = Ladder[i]; return true; }
            for (int i = start + 1; i < Ladder.Length; i++)
                if (Pool(Ladder[i], all, regionOpen).Count > 0) { resolved = Ladder[i]; return true; }
            return false;
        }

        /// <summary>One hero card from two rolls (tier, then hero). Null only when no hero exists in an open region.</summary>
        public static CharacterSpec DrawHero(ChestKind kind, ulong tierRoll, ulong heroRoll, IList<CharacterSpec> all, System.Func<int, bool> regionOpen)
        {
            CharacterRarity tier;
            if (!TryResolveTier(DrawTier(kind, tierRoll), all, regionOpen, out tier)) return null;
            List<CharacterSpec> pool = Pool(tier, all, regionOpen);
            return pool[(int)(heroRoll % (ulong)pool.Count)];
        }
    }
}
