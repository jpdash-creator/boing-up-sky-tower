using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json.Linq;

namespace FluffyClimbers.Economy
{
    /// <summary>A chest a plan opens for the player on given claim numbers (position in the purchased period, 1 = first daily claim).</summary>
    public sealed class SubscriptionChestGrant
    {
        /// <summary>"iron" or "gold".</summary>
        public string Kind;
        /// <summary>Claim positions (1 to Days) that open this chest.</summary>
        public int[] Claims = new int[0];

        public ChestKind ChestKind { get { return Kind == "gold" ? ChestKind.Golden : ChestKind.Iron; } }
        public string KindName { get { return Kind == "gold" ? "Gold" : "Iron"; } }
    }

    /// <summary>One subscription plan (docs/26 section 2, Content/economy/subscriptions.json).</summary>
    public sealed class SubscriptionDef
    {
        /// <summary>"weekly", "weekly_plus", "monthly" or "monthly_premium". Permanent; also the suffix of the store product id.</summary>
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
        /// <summary>Power-ups paid by a claim that qualifies (by PowerUpEveryClaims or PowerUpOnClaims); at most once per claim.</summary>
        public int PowerUpCount;
        /// <summary>Extra claim positions that pay the power-ups (for example weekly: claim 4).</summary>
        public int[] PowerUpOnClaims = new int[0];
        /// <summary>Chests opened for the player on given claims.</summary>
        public List<SubscriptionChestGrant> Chests = new List<SubscriptionChestGrant>();
        /// <summary>The bigger plan of a pair (Plus, Premium): shown with a BEST VALUE badge.</summary>
        public bool Featured;

        /// <summary>True for the monthly pair (30-day plans use the monthly ticket art).</summary>
        public bool IsMonthly { get { return Days >= 30; } }

        /// <summary>Position inside one purchased period for the running claim number (1-based, wraps every Days claims, so an extension repeats the schedule).</summary>
        public int Position(int claimNumber)
        {
            if (Days <= 0 || claimNumber <= 0) return 0;
            return ((claimNumber - 1) % Days) + 1;
        }

        /// <summary>Free power-ups the claim at this position pays (0 or PowerUpCount).</summary>
        public int PowerUpsAt(int position)
        {
            if (PowerUpCount <= 0 || position <= 0) return 0;
            bool hit = PowerUpEveryClaims > 0 && position % PowerUpEveryClaims == 0;
            for (int i = 0; !hit && PowerUpOnClaims != null && i < PowerUpOnClaims.Length; i++) hit = PowerUpOnClaims[i] == position;
            return hit ? PowerUpCount : 0;
        }

        /// <summary>Power-ups one purchase pays in all.</summary>
        public int TotalPowerUps()
        {
            int n = 0;
            for (int p = 1; p <= Days; p++) n += PowerUpsAt(p);
            return n;
        }

        /// <summary>Chests of the given kind one purchase opens in all.</summary>
        public int TotalChests(ChestKind kind)
        {
            int n = 0;
            for (int i = 0; i < Chests.Count; i++) if (Chests[i].ChestKind == kind) n += Chests[i].Claims.Length;
            return n;
        }

        /// <summary>Gems the daily claims pay over one purchase (without the instant gems).</summary>
        public int TotalDailyGems() { return DailyGems * Days; }

        public int TotalDailyCoins() { return DailyCoins * Days; }
    }

    /// <summary>
    /// The subscription plans. The runtime uses the built-in copy below (no manifest change needed); a unit test keeps it identical to
    /// Content/economy/subscriptions.json, which is the editable source (parsed by FromJson for tools and remote overrides).
    /// Four fixed-period passes (owner 2026-11-13): Weekly Starter, Weekly Plus, Monthly Standard, Monthly Premium; one plan runs at a time.
    /// </summary>
    public sealed class SubscriptionCatalog
    {
        public const string Weekly = "weekly", WeeklyPlus = "weekly_plus", Monthly = "monthly", MonthlyPremium = "monthly_premium";

        /// <summary>List rates for the "worth about N x" text: one gem costs $0.011, 1 gem = 20 coins, a random power-up counts 60.7 gems, chests count at their gem price.</summary>
        public const double UsdPerGem = 0.011;
        public const int CoinsPerGem = 20;
        public const double PowerUpGems = 60.7;

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

        static SubscriptionChestGrant Chest(string kind, params int[] claims) { return new SubscriptionChestGrant { Kind = kind, Claims = claims }; }

        public static SubscriptionCatalog BuiltIn()
        {
            return new SubscriptionCatalog(new[]
            {
                new SubscriptionDef { Id = Weekly, Order = 1, Name = "Weekly Starter", PriceUsd = 1.49, Days = 7, InstantGems = 100, DailyGems = 10, DailyCoins = 500,
                    PowerUpEveryClaims = 0, PowerUpCount = 1, PowerUpOnClaims = new[] { 4 } },
                new SubscriptionDef { Id = WeeklyPlus, Order = 2, Name = "Weekly Plus", PriceUsd = 2.99, Days = 7, InstantGems = 250, DailyGems = 20, DailyCoins = 1000,
                    PowerUpEveryClaims = 2, PowerUpCount = 1, Chests = new List<SubscriptionChestGrant> { Chest("iron", 1) }, Featured = true },
                new SubscriptionDef { Id = Monthly, Order = 3, Name = "Monthly Standard", PriceUsd = 4.99, Days = 30, InstantGems = 200, DailyGems = 10, DailyCoins = 600,
                    PowerUpEveryClaims = 5, PowerUpCount = 1 },
                new SubscriptionDef { Id = MonthlyPremium, Order = 4, Name = "Monthly Premium", PriceUsd = 9.99, Days = 30, InstantGems = 300, DailyGems = 20, DailyCoins = 1000,
                    PowerUpEveryClaims = 5, PowerUpCount = 1, Chests = new List<SubscriptionChestGrant> { Chest("gold", 1, 11, 21) }, Featured = true },
            });
        }

        static int[] Ints(JToken t)
        {
            var list = new List<int>();
            if (t is JArray) foreach (JToken x in (JArray)t) list.Add((int)x);
            return list.ToArray();
        }

        /// <summary>Parses Content/economy/subscriptions.json (an array).</summary>
        public static SubscriptionCatalog FromJson(string json)
        {
            var list = new List<SubscriptionDef>();
            foreach (JToken t in JArray.Parse(json))
            {
                JToken daily = t["daily"];
                var def = new SubscriptionDef
                {
                    Id = (string)t["id"], Order = (int?)t["order"] ?? 0, Name = (string)t["name"],
                    PriceUsd = (double?)t["priceUsd"] ?? 0.0, Days = (int?)t["days"] ?? 0, InstantGems = (int?)t["instantGems"] ?? 0,
                    DailyGems = daily != null ? ((int?)daily["gems"] ?? 0) : 0, DailyCoins = daily != null ? ((int?)daily["coins"] ?? 0) : 0,
                    PowerUpEveryClaims = (int?)t["powerUpEveryClaims"] ?? 0, PowerUpCount = (int?)t["powerUpCount"] ?? 0,
                    PowerUpOnClaims = Ints(t["powerUpOnClaims"]), Featured = (bool?)t["featured"] ?? false
                };
                if (t["chests"] is JArray)
                    foreach (JToken c in (JArray)t["chests"]) def.Chests.Add(new SubscriptionChestGrant { Kind = (string)c["kind"], Claims = Ints(c["claims"]) });
                list.Add(def);
            }
            return new SubscriptionCatalog(list);
        }

        /// <summary>
        /// The "price measure" of the whole plan in gems: instant gems + daily gems + daily coins / 20 + free power-ups x 60.7 + chests at their gem price
        /// (Iron 100, Gold 300). Luck of the chest cards is NOT counted (conservative).
        /// </summary>
        public static double TotalGemValue(SubscriptionDef def)
        {
            if (def == null) return 0.0;
            double v = def.InstantGems + (double)def.TotalDailyGems() + (double)def.TotalDailyCoins() / CoinsPerGem + def.TotalPowerUps() * PowerUpGems;
            v += def.TotalChests(ChestKind.Iron) * ChestRolls.Price(ChestKind.Iron, 1) + def.TotalChests(ChestKind.Golden) * ChestRolls.Price(ChestKind.Golden, 1);
            return v;
        }

        /// <summary>Value for the money: gem value at $0.011 per gem divided by the price. Starter about 3.0, Plus 3.8, Standard 3.9, Premium 4.0.</summary>
        public static double WorthMultiple(SubscriptionDef def)
        {
            if (def == null || def.PriceUsd <= 0.0) return 0.0;
            return TotalGemValue(def) * UsdPerGem / def.PriceUsd;
        }

        /// <summary>"Worth about 3.0x" (one decimal, invariant culture).</summary>
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

        // ---------------- card texts (plain; the UI adds colour) ----------------

        static string Num(int n) { return n.ToString("N0", CultureInfo.InvariantCulture); }

        /// <summary>"2nd", "5th", "3rd", "11th".</summary>
        public static string Ordinal(int n)
        {
            int m100 = n % 100, m10 = n % 10;
            string suffix = (m100 >= 11 && m100 <= 13) ? "th" : m10 == 1 ? "st" : m10 == 2 ? "nd" : m10 == 3 ? "rd" : "th";
            return n.ToString(CultureInfo.InvariantCulture) + suffix;
        }

        /// <summary>"1", "1 and 11", "1, 11 and 21".</summary>
        public static string DayList(int[] days)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < days.Length; i++)
            {
                if (i > 0) sb.Append(i == days.Length - 1 ? " and " : ", ");
                sb.Append(days[i].ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>"Now: 300 gems".</summary>
        public static string NowLine(SubscriptionDef def) { return "Now: " + Num(def.InstantGems) + " gems"; }

        /// <summary>"20 gems every day for 30 days".</summary>
        public static string DailyGemsLine(SubscriptionDef def) { return Num(def.DailyGems) + " gems every day for " + def.Days + " days"; }

        /// <summary>"1,000 coins every day for 30 days".</summary>
        public static string DailyCoinsLine(SubscriptionDef def) { return Num(def.DailyCoins) + " coins every day for " + def.Days + " days"; }

        /// <summary>"Power-up every 5th day (6 in all)", "Power-up on day 4", or null when the plan pays none.</summary>
        public static string PowerUpLine(SubscriptionDef def)
        {
            int total = def.TotalPowerUps();
            if (total <= 0) return null;
            string each = def.PowerUpCount > 1 ? def.PowerUpCount + " power-ups" : "Power-up";
            if (def.PowerUpEveryClaims > 0 && (def.PowerUpOnClaims == null || def.PowerUpOnClaims.Length == 0))
                return each + " every " + Ordinal(def.PowerUpEveryClaims) + " day (" + total + " in all)";
            if (def.PowerUpEveryClaims <= 0 && def.PowerUpOnClaims != null && def.PowerUpOnClaims.Length == 1)
                return each + " on day " + def.PowerUpOnClaims[0];
            return each + " on " + total + " days";
        }

        /// <summary>One line per chest grant: "Gold chest on days 1, 11 and 21", "Iron chest on day 1".</summary>
        public static string ChestLine(SubscriptionChestGrant g)
        {
            return g.KindName + " chest on " + (g.Claims.Length == 1 ? "day " : "days ") + DayList(g.Claims);
        }

        /// <summary>"In all: 900 gems + 30,000 coins" (instant plus daily).</summary>
        public static string TotalsLine1(SubscriptionDef def)
        {
            return "In all: " + Num(def.InstantGems + def.TotalDailyGems()) + " gems + " + Num(def.TotalDailyCoins()) + " coins";
        }

        /// <summary>"6 power-ups + 3 Gold chests", "1 power-up", or null when there are neither.</summary>
        public static string TotalsLine2(SubscriptionDef def)
        {
            var parts = new List<string>();
            int pu = def.TotalPowerUps();
            if (pu > 0) parts.Add(pu + (pu == 1 ? " power-up" : " power-ups"));
            for (int i = 0; i < def.Chests.Count; i++)
            {
                int n = def.Chests[i].Claims.Length;
                if (n > 0) parts.Add(n + " " + def.Chests[i].KindName + (n == 1 ? " chest" : " chests"));
            }
            return parts.Count == 0 ? null : string.Join(" + ", parts.ToArray());
        }

        /// <summary>The whole card as one sentence list for the accessibility label.</summary>
        public static string Describe(SubscriptionDef def)
        {
            var sb = new StringBuilder();
            sb.Append(def.Name).Append(", ").Append(def.Days).Append(" days. ");
            sb.Append(NowLine(def)).Append(". ").Append(DailyGemsLine(def)).Append(". ").Append(DailyCoinsLine(def)).Append(". ");
            string pu = PowerUpLine(def);
            if (pu != null) sb.Append(pu).Append(". ");
            for (int i = 0; i < def.Chests.Count; i++) sb.Append(ChestLine(def.Chests[i])).Append(". ");
            sb.Append(TotalsLine1(def)).Append(". ");
            string t2 = TotalsLine2(def);
            if (t2 != null) sb.Append(t2).Append(". ");
            sb.Append(WorthText(def)).Append(".");
            return sb.ToString();
        }
    }
}
