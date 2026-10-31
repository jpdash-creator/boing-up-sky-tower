using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace BoingUpSkyTower.Economy
{
    /// <summary>One subscription plan (docs/26 section 2, Content/economy/subscriptions.json).</summary>
    public sealed class SubscriptionDef
    {
        /// <summary>"weekly" or "monthly".</summary>
        public string Id;
        public int Order;
        public string Name;
        /// <summary>List price in US dollars (a number, 1.49). The store's localized price replaces the shown text later (see PriceText).</summary>
        public double PriceUsd;
        /// <summary>Length of one purchase in days; also the number of daily claims one purchase pays.</summary>
        public int Days;
        public int InstantGems;
        public int DailyGems;
        public int DailyCoins;
        /// <summary>Every Nth daily claim also pays free power-ups (0 = never).</summary>
        public int PowerUpEveryClaims;
        public int PowerUpCount;
    }

    /// <summary>
    /// The subscription plans. The runtime uses the built-in copy below (no manifest change needed); a unit test keeps it identical to
    /// Content/economy/subscriptions.json, which is the editable source (parsed by FromJson for tools and remote overrides).
    /// </summary>
    public sealed class SubscriptionCatalog
    {
        public const string Weekly = "weekly", Monthly = "monthly";

        /// <summary>List rates for the "worth about N x" text: one gem costs $0.011, 1,000 coins are worth 50 gems.</summary>
        public const double UsdPerGem = 0.011;
        public const int GemsPerThousandCoins = 50;

        readonly List<SubscriptionDef> all = new List<SubscriptionDef>();

        public IReadOnlyList<SubscriptionDef> All { get { return all; } }

        public SubscriptionCatalog(IEnumerable<SubscriptionDef> defs)
        {
            all.AddRange(defs);
            all.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        public SubscriptionDef Get(string id)
        {
            for (int i = 0; i < all.Count; i++) if (all[i].Id == id) return all[i];
            return null;
        }

        public static SubscriptionCatalog BuiltIn()
        {
            return new SubscriptionCatalog(new[]
            {
                new SubscriptionDef { Id = Weekly, Order = 1, Name = "Weekly Subscription", PriceUsd = 1.49, Days = 7, InstantGems = 100, DailyGems = 10, DailyCoins = 500, PowerUpEveryClaims = 0, PowerUpCount = 0 },
                new SubscriptionDef { Id = Monthly, Order = 2, Name = "Monthly Subscription", PriceUsd = 4.99, Days = 30, InstantGems = 200, DailyGems = 10, DailyCoins = 600, PowerUpEveryClaims = 5, PowerUpCount = 1 },
            });
        }

        /// <summary>Parses Content/economy/subscriptions.json (an array).</summary>
        public static SubscriptionCatalog FromJson(string json)
        {
            var list = new List<SubscriptionDef>();
            foreach (JToken t in JArray.Parse(json))
            {
                JToken daily = t["daily"];
                list.Add(new SubscriptionDef
                {
                    Id = (string)t["id"], Order = (int?)t["order"] ?? 0, Name = (string)t["name"],
                    PriceUsd = (double?)t["priceUsd"] ?? 0.0, Days = (int?)t["days"] ?? 0, InstantGems = (int?)t["instantGems"] ?? 0,
                    DailyGems = daily != null ? ((int?)daily["gems"] ?? 0) : 0, DailyCoins = daily != null ? ((int?)daily["coins"] ?? 0) : 0,
                    PowerUpEveryClaims = (int?)t["powerUpEveryClaims"] ?? 0, PowerUpCount = (int?)t["powerUpCount"] ?? 0
                });
            }
            return new SubscriptionCatalog(list);
        }

        /// <summary>Gems the whole plan pays at list rates: the instant gems, plus the daily gems and daily coins (1,000 coins = 50 gems) for every day. Free power-ups are NOT counted (conservative; docs/26 values 2.4x and 3.2x).</summary>
        public static double TotalGemValue(SubscriptionDef def)
        {
            if (def == null) return 0.0;
            double perDay = def.DailyGems + def.DailyCoins / 1000.0 * GemsPerThousandCoins;
            return def.InstantGems + def.Days * perDay;
        }

        /// <summary>Value for the money: gem value at $0.011 per gem divided by the price. Weekly about 2.5, monthly about 3.1.</summary>
        public static double WorthMultiple(SubscriptionDef def)
        {
            if (def == null || def.PriceUsd <= 0.0) return 0.0;
            return TotalGemValue(def) * UsdPerGem / def.PriceUsd;
        }

        /// <summary>"Worth about 2.4x" (one decimal, invariant culture).</summary>
        public static string WorthText(SubscriptionDef def)
        {
            return "Worth about " + WorthMultiple(def).ToString("0.0", CultureInfo.InvariantCulture) + "x";
        }

        /// <summary>
        /// The ONE place that formats a plan's price, e.g. "$1.49 / 7 Days". When billing exists, replace <paramref name="symbolAndPrice"/> with the
        /// store's localized price (Google Play) and keep the " / N Days" part.
        /// </summary>
        public static string PriceText(SubscriptionDef def, string symbolAndPrice = null)
        {
            return (symbolAndPrice ?? AmountText(def)) + " / " + def.Days.ToString(CultureInfo.InvariantCulture) + " Days";
        }

        /// <summary>"$1.49".</summary>
        public static string AmountText(SubscriptionDef def)
        {
            return "$" + def.PriceUsd.ToString("0.00", CultureInfo.InvariantCulture);
        }
    }
}
