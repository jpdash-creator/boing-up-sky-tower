using System.Collections.Generic;
using BoingUpSkyTower.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BoingUpSkyTower.Economy
{
    /// <summary>Units of one power-up inside a bundle.</summary>
    public sealed class BundlePowerUp
    {
        public string Id;
        public int Count;
    }

    /// <summary>
    /// One gem bundle (docs/26 section 3 and docs/37, Content/economy/bundles.json made by Tools/content_cli/gen_bundles.py): a price in gems and
    /// contents of coins and power-ups (no gems back) plus one HIDDEN hero card (Uncommon or better) that is drawn when the bundle is bought.
    /// </summary>
    public sealed class BundleDef
    {
        public string Id;
        public string Name;
        /// <summary>1 to 3, picks the bag icon size/tint (follows the price).</summary>
        public int Tier;
        public int PriceGems;
        public int Coins;
        /// <summary>Obsolete shim: bundles no longer contain gems, always 0. Kept so older UI code still compiles; remove with the UI rebuild.</summary>
        [System.Obsolete("Bundles contain no gems any more (always 0).")]
        public int Gems { get { return 0; } }
        public List<BundlePowerUp> PowerUps = new List<BundlePowerUp>();
        /// <summary>True when the bundle holds one hidden hero card (every generated bundle does). Which hero is decided at purchase (<see cref="BundleCards"/>).</summary>
        public bool HiddenCard;
        /// <summary>One funny line about the bundle's name (Bug List 2 C2.b.vi), shown only in the info popup. "" when the file has none.</summary>
        public string Joke = "";

        /// <summary>Value of the visible contents at list rates in gems: coins / 20 + power-ups at their gem prices (50 or 75). The hidden card is NOT in it (see <see cref="TotalValueGems"/>).</summary>
        public double ValueGems(PowerUpCatalog powerUps)
        {
            double v = Coins / 20.0;
            foreach (BundlePowerUp p in PowerUps)
            {
                PowerUpDef d = powerUps != null ? powerUps.Get(p.Id) : null;
                if (d != null) v += (double)d.GemPrice * p.Count;
            }
            return v;
        }

        /// <summary>Visible contents plus the expected value of the hidden card (<see cref="BundleCards.ExpectedGems"/>). The worth multiplier works on this number.</summary>
        public double TotalValueGems(PowerUpCatalog powerUps)
        {
            return ValueGems(powerUps) + (HiddenCard ? BundleCards.ExpectedGems : 0.0);
        }
    }

    /// <summary>
    /// How many times its price a bundle is worth (owner Bug List 1 B4.a.ii): total value in GEMS (coins at the shop rate coins / 20, power-ups at their gem prices,
    /// the hidden card at its expected gem value from <see cref="BundleCards.TierGems"/>) divided by the gems paid. Shown as "Worth x2.1" (Bug List 2 C2.b.ii: 1.5 to 2.5). No currency amounts anywhere. Pure.
    /// </summary>
    public static class BundleWorth
    {
        public static double Multiplier(BundleDef bundle) { return Multiplier(bundle, null, 0); }

        /// <param name="powerUps">Price list for the power-ups; the built-in catalog when null.</param>
        /// <param name="gemsPaid">Gems actually paid; 0 = the list price (the timer offer passes its discounted price).</param>
        public static double Multiplier(BundleDef bundle, PowerUpCatalog powerUps, int gemsPaid)
        {
            if (bundle == null) return 0.0;
            int paid = gemsPaid > 0 ? gemsPaid : bundle.PriceGems;
            if (paid <= 0) return 0.0;
            return bundle.TotalValueGems(powerUps ?? Defs()) / paid;
        }

        /// <summary>"Worth x2.1" (one decimal, invariant culture), always at the LIST price (so 1.5 to 2.5).</summary>
        public static string Text(BundleDef bundle) { return Text(bundle, 0); }

        /// <summary>
        /// Same. <paramref name="gemsPaid"/> is accepted for older callers but IGNORED: the timer offer (30 percent off) shows the same worth as the normal card;
        /// the discount shows only in the cheaper gem price.
        /// </summary>
        public static string Text(BundleDef bundle, int gemsPaid)
        {
            return "Worth x" + Multiplier(bundle, null, 0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>"Worth x2.1 for 340 gems" (the info popup line): worth at the list price, then the gems really paid (the discounted price for a timer offer).</summary>
        public static string Line(BundleDef bundle, int gemsPaid)
        {
            int paid = gemsPaid > 0 ? gemsPaid : (bundle != null ? bundle.PriceGems : 0);
            return Text(bundle, 0) + " for " + paid.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " gems";
        }

        static PowerUpCatalog builtIn;
        static PowerUpCatalog Defs() { return builtIn ?? (builtIn = PowerUpCatalog.BuiltIn()); }
    }

    /// <summary>
    /// The 2,000 bundles. The game loads Resources/Content/bundles.json (an identical copy of Content/economy/bundles.json that
    /// gen_bundles.py writes next to the source file); a missing or broken file gives an EMPTY catalog and the Bundles page shows a
    /// notice instead of crashing (CUX-10). Entries with an unknown power-up, a bad price or a repeated id are skipped.
    /// </summary>
    public sealed class BundleCatalog
    {
        /// <summary>Path below Assets/Resources, without the extension.</summary>
        public const string ResourcePath = "Content/bundles";

        /// <summary>
        /// Version of the bundle pool (3 = the Bug List 2 C2.b pool: 200 to 500 gems, worth 1.5 to 2.5, with jokes; 2 = the 200 to 600 gem pool). A save that holds offers or a timer offer from an older pool
        /// is reset once (the offers are drawn again), because ids bnd_0001 to bnd_2000 now mean different bundles.
        /// </summary>
        public const int PoolVersion = 3;

        readonly List<BundleDef> all = new List<BundleDef>();
        readonly Dictionary<string, BundleDef> byId = new Dictionary<string, BundleDef>();

        public IReadOnlyList<BundleDef> All { get { return all; } }
        public int Count { get { return all.Count; } }

        public BundleDef Get(string id)
        {
            BundleDef d;
            return id != null && byId.TryGetValue(id, out d) ? d : null;
        }

        public static BundleCatalog FromDefs(IEnumerable<BundleDef> defs)
        {
            var c = new BundleCatalog();
            foreach (BundleDef d in defs)
            {
                if (d == null || string.IsNullOrEmpty(d.Id) || d.PriceGems <= 0 || c.byId.ContainsKey(d.Id)) continue;
                c.all.Add(d);
                c.byId[d.Id] = d;
            }
            return c;
        }

        /// <summary>Parses the JSON array. Never throws: bad JSON gives an empty catalog, bad entries are skipped.</summary>
        public static BundleCatalog FromJson(string json)
        {
            var list = new List<BundleDef>();
            try
            {
                JArray arr = JArray.Parse(json);
                foreach (JToken t in arr)
                {
                    var d = new BundleDef
                    {
                        Id = (string)t["id"], Name = (string)t["name"], Tier = (int?)t["tier"] ?? 1, PriceGems = (int?)t["priceGems"] ?? 0,
                        Coins = (int?)t["coins"] ?? 0, Joke = (string)t["joke"] ?? ""
                    };
                    JToken hc = t["hiddenCard"];
                    d.HiddenCard = hc != null && hc.Type == JTokenType.Boolean && (bool)hc;
                    bool ok = d.Coins >= 0;
                    JArray pw = t["powerups"] as JArray;
                    if (pw != null)
                    {
                        foreach (JToken p in pw)
                        {
                            string pid = (string)p["id"];
                            int count = (int?)p["count"] ?? 0;
                            if (PowerUps.FromId(pid) == PowerUp.None || count <= 0) { ok = false; break; }
                            d.PowerUps.Add(new BundlePowerUp { Id = pid, Count = count });
                        }
                    }
                    if (ok) list.Add(d);
                }
            }
            catch (JsonException) { list.Clear(); }
            return FromDefs(list);
        }

        static BundleCatalog shared;

        /// <summary>Reads Resources/Content/bundles once and keeps it (the file is about 450 KB). Empty when the file is missing.</summary>
        public static BundleCatalog LoadFromResources()
        {
            if (shared != null) return shared;
            var asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>(ResourcePath);
            shared = asset != null ? FromJson(asset.text) : new BundleCatalog();
            return shared;
        }
    }

    public enum BundleResult
    {
        Ok,
        NotEnoughGems,
        NotEnoughCoins,
        Invalid
    }

    /// <summary>Saved bundle state (SaveData.Bundles): the current offer set, the draw counter and the timer offer. Plain data.</summary>
    public sealed class BundleSaveData
    {
        /// <summary>Ids of the 6 bundles on the Bundles page (stable until refreshed).</summary>
        [JsonProperty("offers")] public List<string> Offers = new List<string>();
        /// <summary>Per-account seed set at the first draw; with Draws it makes every draw repeatable.</summary>
        [JsonProperty("seed")] public int Seed;
        /// <summary>Draws of an offer set so far (first set and each refresh).</summary>
        [JsonProperty("draws")] public int Draws;
        [JsonProperty("timerStarted")] public bool TimerStarted;
        /// <summary>Play seconds until the next timer offer.</summary>
        [JsonProperty("timerNextSec")] public double TimerNextSec;
        /// <summary>Id of the timer offer being shown, "" when none.</summary>
        [JsonProperty("timerOffer")] public string TimerOfferId = "";
        /// <summary>UTC ticks at which the timer offer ends, 0 when none.</summary>
        [JsonProperty("timerExpiry")] public long TimerExpiryTicks;
        /// <summary>The previous timer offer: the next one is never the same bundle.</summary>
        [JsonProperty("timerLast")] public string TimerLastId = "";
        /// <summary>Timer offers made so far (seeds the next ones).</summary>
        [JsonProperty("timerCount")] public int TimerOffers;
        /// <summary>Pool version the saved offers belong to (see <see cref="BundleCatalog.PoolVersion"/>); 0 in older saves.</summary>
        [JsonProperty("poolVer")] public int PoolVersion;
        /// <summary>Hidden hero cards drawn so far (with Seed it makes every card draw repeatable and different).</summary>
        [JsonProperty("cardDraws")] public int CardDraws;
        /// <summary>Hero cards bought in the Shop Heroes section so far (Bug List 4 E2; with Seed it makes every pick repeatable and different).</summary>
        [JsonProperty("heroBuys")] public int HeroBuys;
    }
}
