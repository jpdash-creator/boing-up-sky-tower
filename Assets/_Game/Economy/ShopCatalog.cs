using System.Collections.Generic;
using System.Globalization;
using FluffyClimbers.Content;
using FluffyClimbers.Monetization;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// The shop's gem and coin packs (docs/14-shop-requirements.md). Loaded from the content manifest (Content/economy/gem_packs.json and
    /// coin_packs.json); a manifest built before the shop existed falls back to the built-in copy of the owner's tables below,
    /// which a unit test keeps identical to the JSON files.
    /// </summary>
    public sealed class ShopCatalog
    {
        public IReadOnlyList<GemPackDef> GemPacks { get; private set; }
        public IReadOnlyList<CoinPackDef> CoinPacks { get; private set; }

        public ShopCatalog(IReadOnlyList<GemPackDef> gemPacks, IReadOnlyList<CoinPackDef> coinPacks)
        {
            GemPacks = gemPacks;
            CoinPacks = coinPacks;
        }

        /// <summary>Uses the repository's packs when they are in the docs/14 format, the built-in tables otherwise.</summary>
        public static ShopCatalog FromRepository(ContentRepository repo)
        {
            IReadOnlyList<GemPackDef> gems = repo != null ? repo.GemPacks : null;
            IReadOnlyList<CoinPackDef> coins = repo != null ? repo.CoinPacks : null;
            bool gemsOk = gems != null && gems.Count > 0;
            if (gemsOk) foreach (GemPackDef g in gems) if (g.UsdList <= 0.0 || g.UsdPromo <= 0.0 || g.InrList <= 0) { gemsOk = false; break; }
            bool coinsOk = coins != null && coins.Count > 0;
            return new ShopCatalog(gemsOk ? gems : BuiltInGemPacks(), coinsOk ? coins : BuiltInCoinPacks());
        }

        public static ShopCatalog BuiltIn()
        {
            return new ShopCatalog(BuiltInGemPacks(), BuiltInCoinPacks());
        }

        /// <summary>The once-only first-purchase double applies to packs of up to this many gems (docs/22 4.1: packs 50 to 1,000).</summary>
        public const int FirstPurchaseMaxGems = 1000;

        // Owner tables, docs/22 4.1: gems, list price (USD), launch promo price (USD, placeholder while the promo is off), list price (INR), badge.
        static readonly object[][] GemRows =
        {
            new object[] { 50, 0.79, 0.49, 75, "" },
            new object[] { 100, 1.49, 0.79, 142, "popular" },
            new object[] { 200, 2.49, 1.29, 237, "" },
            new object[] { 500, 5.99, 2.99, 569, "" },
            new object[] { 1000, 10.99, 5.49, 1044, "best_value" },
            new object[] { 2000, 20.99, 10.49, 1994, "" },
            new object[] { 5000, 49.99, 24.99, 4749, "" },
            new object[] { 10000, 89.99, 44.99, 8549, "" },
            new object[] { 15000, 119.99, 59.99, 11399, "" },
        };

        // Owner table, docs/22 4.2: coins, price in gems at list, price in gems with the promo (placeholder while the promo is off).
        static readonly int[][] CoinRows =
        {
            new[] { 1000, 50, 25 }, new[] { 2000, 100, 50 }, new[] { 5000, 250, 125 }, new[] { 10000, 500, 250 },
            new[] { 20000, 900, 450 }, new[] { 50000, 2200, 1100 }, new[] { 100000, 4000, 2000 },
        };

        public static List<GemPackDef> BuiltInGemPacks()
        {
            var list = new List<GemPackDef>(GemRows.Length);
            foreach (object[] r in GemRows)
            {
                int gems = (int)r[0];
                list.Add(new GemPackDef
                {
                    Id = "gempack_" + gems, Gems = gems, UsdList = (double)r[1], UsdPromo = (double)r[2], InrList = (int)r[3],
                    FirstPurchaseDouble = gems <= FirstPurchaseMaxGems, Sku = ProductCatalog.Prefix + "gems.pack_" + gems, Badge = (string)r[4]
                });
            }
            return list;
        }

        public static List<CoinPackDef> BuiltInCoinPacks()
        {
            var list = new List<CoinPackDef>(CoinRows.Length);
            foreach (int[] r in CoinRows)
                list.Add(new CoinPackDef { Id = "coinpack_" + r[0], Coins = r[0], GemsList = r[1], GemsPromo = r[2] });
            return list;
        }

        /// <summary>
        /// Index of the smallest gem pack that covers <paramref name="shortfall"/> gems (first-purchase bonus not counted), or the largest
        /// pack when none does; -1 when there are no packs. Used by the "Get gems" link on coin packs the player cannot afford yet.
        /// </summary>
        public int SmallestGemPackCovering(int shortfall)
        {
            int best = -1;
            for (int i = 0; i < GemPacks.Count; i++)
            {
                if (GemPacks[i].Gems >= shortfall && (best < 0 || GemPacks[i].Gems < GemPacks[best].Gems)) best = i;
            }
            if (best >= 0) return best;
            for (int i = 0; i < GemPacks.Count; i++) if (best < 0 || GemPacks[i].Gems > GemPacks[best].Gems) best = i;
            return best;
        }

        /// <summary>Price shown as "$0.99" (invariant culture; the store's localised price replaces it once real billing exists).</summary>
        public static string FormatUsd(double usd)
        {
            return "$" + usd.ToString("0.00", CultureInfo.InvariantCulture);
        }

        /// <summary>Real-money price of a gem pack: the promo price while the launch promotion is on, the list price otherwise.</summary>
        public static double GemPackUsd(GemPackDef pack, bool promo)
        {
            return promo ? pack.UsdPromo : pack.UsdList;
        }

        /// <summary>Gem price of a coin pack: the promo price while the launch promotion is on, the list price otherwise.</summary>
        public static int CoinPackGems(CoinPackDef pack, bool promo)
        {
            return promo ? pack.GemsPromo : pack.GemsList;
        }
    }
}
