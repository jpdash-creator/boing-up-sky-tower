using System.Collections.Generic;
using System.Globalization;

namespace BoingUpSkyTower.Monetization
{
    public enum ProductKind
    {
        Consumable,
        NonConsumable,
        Subscription
    }

    /// <summary>
    /// One real-money product. Id is the store product id (Google Play Console "Product ID", later App Store Connect "Product ID");
    /// it is permanent: never rename or reuse one. Prices are the USD list prices the UI shows today.
    /// </summary>
    public sealed class ProductDef
    {
        public readonly string Id;
        public readonly ProductKind Kind;
        public readonly string DisplayName;
        public readonly double PriceUsd;
        /// <summary>Launch-promo USD price (0 = none). Sold as a SEPARATE store product (PromoId), because a store product has one price.</summary>
        public readonly double PromoPriceUsd;
        /// <summary>Gems paid on purchase (the first-purchase double is added by the game, not the store).</summary>
        public readonly int Gems;
        public readonly int Coins;
        /// <summary>Subscriptions: days per period (7 or 30). 0 otherwise.</summary>
        public readonly int PeriodDays;
        /// <summary>False for planned products that must NOT be created in the store yet.</summary>
        public readonly bool Live;
        public readonly string Contents;

        public ProductDef(string id, ProductKind kind, string name, double usd, double promoUsd, int gems, int coins, int periodDays, bool live, string contents)
        {
            Id = id; Kind = kind; DisplayName = name; PriceUsd = usd; PromoPriceUsd = promoUsd; Gems = gems; Coins = coins;
            PeriodDays = periodDays; Live = live; Contents = contents;
        }

        /// <summary>Store id of the launch-promo variant (Id + "_launch"), or null when there is no promo price.</summary>
        public string PromoId { get { return PromoPriceUsd > 0.0 ? Id + "_launch" : null; } }
    }

    /// <summary>
    /// THE central list of every real-money product (docs/33-ids-ads-payments.md). Id format:
    /// com.jdlabs.boingupskytower.CATEGORY.NAME in lowercase letters, digits and underscores (valid for Google Play and App Store).
    /// Only gem packs and the two subscriptions are sold for money today (chests, bundles, coin packs and power-ups cost gems).
    /// Keep the prices identical to ShopCatalog.BuiltInGemPacks and SubscriptionCatalog.BuiltIn (a unit test checks it).
    /// </summary>
    public static class ProductCatalog
    {
        public const string Prefix = "com.jdlabs.boingupskytower.";

        public const string Gems50 = Prefix + "gems.pack_50";
        public const string Gems100 = Prefix + "gems.pack_100";
        public const string Gems200 = Prefix + "gems.pack_200";
        public const string Gems500 = Prefix + "gems.pack_500";
        public const string Gems1000 = Prefix + "gems.pack_1000";
        public const string Gems2000 = Prefix + "gems.pack_2000";
        public const string Gems5000 = Prefix + "gems.pack_5000";
        public const string Gems10000 = Prefix + "gems.pack_10000";
        public const string Gems15000 = Prefix + "gems.pack_15000";
        public const string SubWeekly = Prefix + "sub.weekly";
        public const string SubMonthly = Prefix + "sub.monthly";
        /// <summary>Planned (doc 22 point 7: $0.99 = 100 gems + 2,000 coins). Not live: do not create it in the store yet.</summary>
        public const string StarterPack = Prefix + "bundle.starter";

        static ProductDef Gem(string id, int gems, double usd, double promo)
        {
            return new ProductDef(id, ProductKind.Consumable, gems.ToString("N0", CultureInfo.InvariantCulture) + " Gems", usd, promo, gems, 0, 0, true,
                gems.ToString(CultureInfo.InvariantCulture) + " gems");
        }

        public static readonly IReadOnlyList<ProductDef> All = new ProductDef[]
        {
            Gem(Gems50, 50, 0.79, 0.49),
            Gem(Gems100, 100, 1.49, 0.79),
            Gem(Gems200, 200, 2.49, 1.29),
            Gem(Gems500, 500, 5.99, 2.99),
            Gem(Gems1000, 1000, 10.99, 5.49),
            Gem(Gems2000, 2000, 20.99, 10.49),
            Gem(Gems5000, 5000, 49.99, 24.99),
            Gem(Gems10000, 10000, 89.99, 44.99),
            Gem(Gems15000, 15000, 119.99, 59.99),
            new ProductDef(SubWeekly, ProductKind.Subscription, "Weekly Subscription", 1.49, 0.0, 100, 0, 7, true,
                "100 gems now, then each day 10 gems + 500 coins for 7 days"),
            new ProductDef(SubMonthly, ProductKind.Subscription, "Monthly Subscription", 4.99, 0.0, 200, 0, 30, true,
                "200 gems now, then each day 10 gems + 600 coins for 30 days, a power-up every 5th claim"),
            new ProductDef(StarterPack, ProductKind.Consumable, "Starter Pack", 0.99, 0.0, 100, 2000, 0, false,
                "100 gems + 2,000 coins (planned, once per account)"),
        };

        public static ProductDef Get(string id)
        {
            if (id == null) return null;
            for (int i = 0; i < All.Count; i++) if (All[i].Id == id) return All[i];
            return null;
        }

        /// <summary>The gem-pack product with exactly this many gems, or null.</summary>
        public static ProductDef ForGemPack(int gems)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].Kind == ProductKind.Consumable && All[i].Coins == 0 && All[i].Gems == gems) return All[i];
            return null;
        }

        /// <summary>Product id of a subscription plan id ("weekly" or "monthly"), or null.</summary>
        public static string ForSubscription(string planId)
        {
            return planId == "weekly" ? SubWeekly : planId == "monthly" ? SubMonthly : null;
        }
    }
}
