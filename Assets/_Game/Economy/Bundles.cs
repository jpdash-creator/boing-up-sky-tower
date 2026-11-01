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
    /// One gem bundle (docs/26 section 3, Content/economy/bundles.json made by Tools/content_cli/gen_bundles.py): a price in gems and
    /// contents of coins and power-ups only (no gems back since the 2026-11 owner decision) and optionally a character (not generated yet).
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
        /// <summary>Character id or null (the format supports it, none are generated yet).</summary>
        public string CharacterId;

        /// <summary>Value at list rates in gems: coins / 20 + power-ups at their gem prices (50 or 75). Used by tests and the "worth" hint.</summary>
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
    }

    /// <summary>
    /// The 1,000 bundles. The game loads Resources/Content/bundles.json (an identical copy of Content/economy/bundles.json that
    /// gen_bundles.py writes next to the source file); a missing or broken file gives an EMPTY catalog and the Bundles page shows a
    /// notice instead of crashing (CUX-10). Entries with an unknown power-up, a bad price or a repeated id are skipped.
    /// </summary>
    public sealed class BundleCatalog
    {
        /// <summary>Path below Assets/Resources, without the extension.</summary>
        public const string ResourcePath = "Content/bundles";

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
                        Coins = (int?)t["coins"] ?? 0
                    };
                    JToken cid = t["characterId"];
                    d.CharacterId = cid != null && cid.Type == JTokenType.String ? (string)cid : null;
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

        /// <summary>Reads Resources/Content/bundles once and keeps it (the file is about 200 KB). Empty when the file is missing.</summary>
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
    }
}
