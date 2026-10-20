using System.Collections.Generic;
using System.Globalization;
using SkyHopper.Content;

namespace SkyHopper.Economy
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
            if (gemsOk) foreach (GemPackDef g in gems) if (g.UsdList <= 0.0 || g.UsdPromo <= 0.0) { gemsOk = false; break; }
            bool coinsOk = coins != null && coins.Count > 0;
            return new ShopCatalog(gemsOk ? gems : BuiltInGemPacks(), coinsOk ? coins : BuiltInCoinPacks());
        }

        public static ShopCatalog BuiltIn()
        {
            return new ShopCatalog(BuiltInGemPacks(), BuiltInCoinPacks());
        }

        // Owner tables, 2026-10-20: gems, list price, launch promo price, name, badge.
        static readonly object[][] GemRows =
        {
            new object[] { "pinch", 10, 0.99, 0.49, "" },
            new object[] { "handful", 20, 1.99, 0.99, "" },
            new object[] { "pouch", 50, 3.99, 1.99, "" },
            new object[] { "sack", 100, 7.99, 3.99, "popular" },
            new object[] { "chest", 200, 11.99, 5.99, "" },
            new object[] { "vault", 500, 19.99, 9.99, "" },
            new object[] { "treasury", 1000, 29.99, 14.99, "best_value" },
        };

        // Coins, price in gems at list, price in gems with the promo.
        static readonly int[][] CoinRows =
        {
            new[] { 100, 10, 5 }, new[] { 200, 20, 10 }, new[] { 500, 50, 25 }, new[] { 1000, 100, 50 },
            new[] { 2000, 200, 100 }, new[] { 5000, 500, 200 }, new[] { 10000, 1000, 500 },
        };

        public static List<GemPackDef> BuiltInGemPacks()
        {
            var list = new List<GemPackDef>(GemRows.Length);
            foreach (object[] r in GemRows)
                list.Add(new GemPackDef { Id = "gempack_" + (string)r[0], Gems = (int)r[1], UsdList = (double)r[2], UsdPromo = (double)r[3], Sku = "skyhopper.gems." + (string)r[0], Badge = (string)r[4] });
            return list;
        }

        public static List<CoinPackDef> BuiltInCoinPacks()
        {
            var list = new List<CoinPackDef>(CoinRows.Length);
            foreach (int[] r in CoinRows)
                list.Add(new CoinPackDef { Id = "coinpack_" + r[0], Coins = r[0], GemsList = r[1], GemsPromo = r[2] });
            return list;
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
