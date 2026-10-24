using System;
using System.Collections.Generic;
using BoingUpSkyTower.Simulation;

namespace BoingUpSkyTower.Economy
{
    public enum ChestKind
    {
        Coin,
        Gear,
        Powerup
    }

    /// <summary>One chest type: a kind and a tier (docs/15 section 4). Sold for coins OR gems (one currency per chest, a compact card).</summary>
    public sealed class ChestDef
    {
        public string Id;
        public ChestKind Kind;
        public Rarity Tier;
        public string Name;
        public int CoinPrice, GemPrice;
        /// <summary>Percent chance of a Common, Rare, Epic and Legendary result; sums to 100.</summary>
        public int[] Odds;
        public int CoinMin, CoinMax;
        public int UnitsMin, UnitsMax, Types;
        /// <summary>How much one opening moves the pity counters (a Legendary chest counts as 3).</summary>
        public int PityWeight;
    }

    /// <summary>What one opening paid.</summary>
    public sealed class ChestResult
    {
        public ChestDef Chest;
        /// <summary>The best rarity rolled (what the pity counters look at).</summary>
        public Rarity Rarity;
        public int Coins;
        public string GearId;
        public bool Duplicate;
        public int Shards;
        public readonly List<KeyValuePair<string, int>> PowerUps = new List<KeyValuePair<string, int>>();
        /// <summary>True when the opening was a pity guarantee.</summary>
        public bool PityHit;
    }

    public sealed class ChestRng
    {
        ulong state;

        public ChestRng(ulong seed) { state = seed; }

        public uint Next()
        {
            unchecked
            {
                state += 0x9E3779B97F4A7C15UL;
                ulong z = state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return (uint)((z ^ (z >> 31)) >> 32);
            }
        }

        /// <summary>0 .. n-1.</summary>
        public int Range(int n) { return n <= 1 ? 0 : (int)(((ulong)Next() * (ulong)n) >> 32); }

        /// <summary>lo .. hi inclusive.</summary>
        public int Range(int lo, int hi) { return hi <= lo ? lo : lo + Range(hi - lo + 1); }
    }

    public sealed class ChestCatalog
    {
        /// <summary>Chests opened without a Rare-or-better / Epic-or-better / Legendary result at most this many minus one (docs/15 FR-CHEST-6).</summary>
        public const int PityRare = 10, PityEpic = 30, PityLegendary = 90;

        static readonly string[] KindKey = { "coin", "gear", "powerup" };
        static readonly string[] KindName = { "Coin", "Gear", "Power-up" };
        static readonly string[] TierKey = { "common", "rare", "epic", "legendary" };

        // Odds are the same for every kind (docs/15 4.1 gives them for the Gear chest).
        static readonly int[][] TierOdds = { new[] { 80, 18, 2, 0 }, new[] { 45, 40, 14, 1 }, new[] { 10, 40, 42, 8 }, new[] { 0, 15, 55, 30 } };
        static readonly int[][] CoinRange = { new[] { 400, 900 }, new[] { 1500, 3000 }, new[] { 5000, 9000 }, new[] { 15000, 25000 } };
        static readonly int[][] UnitRange = { new[] { 2, 3 }, new[] { 3, 5 }, new[] { 5, 8 }, new[] { 8, 12 } };
        static readonly int[] TypeCount = { 1, 2, 3, 4 };

        // Sale prices (owner decision pending, see docs/15 section 13): [kind][tier] = {coins, gems}; one currency per chest.
        static readonly int[][][] Prices =
        {
            new[] { new[] { 0, 30 }, new[] { 0, 90 }, new[] { 0, 250 }, new[] { 0, 600 } },       // Coin chest: gems only (a gem to coin conversion with a bonus)
            new[] { new[] { 1500, 0 }, new[] { 5000, 0 }, new[] { 0, 150 }, new[] { 0, 400 } },   // Gear chest
            new[] { new[] { 1200, 0 }, new[] { 3500, 0 }, new[] { 0, 100 }, new[] { 0, 300 } },   // Power-up chest
        };

        readonly List<ChestDef> all = new List<ChestDef>();
        readonly Dictionary<string, ChestDef> byId = new Dictionary<string, ChestDef>();

        public IReadOnlyList<ChestDef> All { get { return all; } }

        public ChestCatalog()
        {
            for (int k = 0; k < 3; k++)
            {
                for (int t = 0; t < 4; t++)
                {
                    var d = new ChestDef
                    {
                        Id = KindKey[k] + "_" + TierKey[t],
                        Kind = (ChestKind)k,
                        Tier = (Rarity)t,
                        Name = RarityInfo.Name((Rarity)t) + " " + KindName[k] + " Chest",
                        CoinPrice = Prices[k][t][0],
                        GemPrice = Prices[k][t][1],
                        Odds = (int[])TierOdds[t].Clone(),
                        CoinMin = CoinRange[t][0], CoinMax = CoinRange[t][1],
                        UnitsMin = UnitRange[t][0], UnitsMax = UnitRange[t][1], Types = TypeCount[t],
                        PityWeight = t == 3 ? 3 : 1,
                    };
                    all.Add(d);
                    byId[d.Id] = d;
                }
            }
        }

        static ChestCatalog builtIn;

        public static ChestCatalog BuiltIn() { return builtIn ?? (builtIn = new ChestCatalog()); }

        public ChestDef Get(string id)
        {
            ChestDef d;
            return id != null && byId.TryGetValue(id, out d) ? d : null;
        }

        public static string KindWord(ChestKind k) { return KindName[(int)k]; }

        /// <summary>The odds as a readable line, "Common 80%, Rare 18%, Epic 2%". Legendary shows only when it can drop.</summary>
        public static string OddsText(ChestDef d)
        {
            var parts = new List<string>(4);
            for (int i = 0; i < 4; i++) if (d.Odds[i] > 0) parts.Add(RarityInfo.Name((Rarity)i) + " " + d.Odds[i] + "%");
            return string.Join(", ", parts);
        }

        // ---------------- rolling ----------------

        static string PityKey(ChestKind kind, string tier) { return KindKey[(int)kind] + "." + tier; }

        /// <summary>Pity counters that apply to a kind: rare and epic for all, legendary for gear only.</summary>
        static bool HasLegendaryPity(ChestKind kind) { return kind == ChestKind.Gear; }

        /// <summary>The lowest rarity a pity guarantee forces for this opening (Common = none).</summary>
        public static Rarity PityFloor(ChestDef def, Dictionary<string, int> pity)
        {
            int w = def.PityWeight;
            if (HasLegendaryPity(def.Kind) && Get(pity, PityKey(def.Kind, "legendary")) + w >= PityLegendary) return Rarity.Legendary;
            if (Get(pity, PityKey(def.Kind, "epic")) + w >= PityEpic) return Rarity.Epic;
            if (Get(pity, PityKey(def.Kind, "rare")) + w >= PityRare) return Rarity.Rare;
            return Rarity.Common;
        }

        static int Get(Dictionary<string, int> d, string key) { int v; return d.TryGetValue(key, out v) ? v : 0; }

        public static int PityCounter(Dictionary<string, int> pity, ChestKind kind, string tier) { return Get(pity, PityKey(kind, tier)); }

        /// <summary>Rolls one rarity from the odds, never below the floor.</summary>
        public static Rarity RollRarity(int[] odds, Rarity floor, ChestRng rng)
        {
            int[] w = new int[4];
            int sum = 0;
            for (int i = (int)floor; i < 4; i++) { w[i] = odds[i]; sum += odds[i]; }
            if (sum <= 0) return floor;   // the table has nothing at or above the floor: the guarantee pays exactly the floor
            int roll = rng.Range(sum);
            for (int i = (int)floor; i < 4; i++)
            {
                if (roll < w[i]) return (Rarity)i;
                roll -= w[i];
            }
            return Rarity.Legendary;
        }

        /// <summary>Moves the pity counters after an opening: reset when the guaranteed rarity dropped, else count up by the chest's weight.</summary>
        public static void UpdatePity(ChestDef def, Dictionary<string, int> pity, Rarity got)
        {
            Step(pity, PityKey(def.Kind, "rare"), got >= Rarity.Rare, def.PityWeight);
            Step(pity, PityKey(def.Kind, "epic"), got >= Rarity.Epic, def.PityWeight);
            if (HasLegendaryPity(def.Kind)) Step(pity, PityKey(def.Kind, "legendary"), got >= Rarity.Legendary, def.PityWeight);
        }

        static void Step(Dictionary<string, int> pity, string key, bool hit, int weight)
        {
            if (hit) pity.Remove(key);
            else pity[key] = Get(pity, key) + weight;
        }

        /// <summary>
        /// Rolls one opening (no state changes except the pity dictionary): the rarity from the odds and the pity floor, then the payout of the
        /// kind. <paramref name="owned"/> says which gear pieces the player already has (a duplicate pays Style Shards).
        /// </summary>
        public static ChestResult Roll(ChestDef def, Dictionary<string, int> pity, Func<string, bool> owned, GearCatalog gear, ChestRng rng)
        {
            var res = new ChestResult { Chest = def };
            Rarity floor = PityFloor(def, pity);
            res.PityHit = floor > Rarity.Common;
            Rarity rolled = RollRarity(def.Odds, floor, rng);
            res.Rarity = rolled;

            switch (def.Kind)
            {
                case ChestKind.Coin:
                {
                    // Position inside the tier's range grows with the rarity: Common pays the low quarter, Legendary the top quarter.
                    int q = ((int)rolled * 1000 + rng.Range(1000)) / 4;   // 0 .. 999
                    long span = def.CoinMax - def.CoinMin;
                    res.Coins = (int)((def.CoinMin + span * q / 1000) / 10 * 10);
                    break;
                }
                case ChestKind.Gear:
                {
                    List<GearDef> pool = gear.OfRarity(rolled);
                    GearDef piece = pool[rng.Range(pool.Count)];
                    res.GearId = piece.Id;
                    if (owned != null && owned(piece.Id))
                    {
                        res.Duplicate = true;
                        bool allOwned = true;
                        foreach (GearDef g in pool) if (!owned(g.Id)) { allOwned = false; break; }
                        res.Shards = RarityInfo.DuplicateShards(rolled);
                        if (allOwned) { res.Coins = res.Shards * 10; res.Shards = 0; }   // everything of this rarity is owned: pay coins instead of shards
                    }
                    break;
                }
                default:
                    RollPowerUps(def, rolled, rng, res);
                    break;
            }
            UpdatePity(def, pity, rolled);
            return res;
        }

        static readonly PowerUp[][] Pools =
        {
            new[] { PowerUp.DoubleJump, PowerUp.WallStick, PowerUp.Magnet },   // Common
            new[] { PowerUp.SlowTime, PowerUp.Shield },                        // Rare
            new[] { PowerUp.Flight, PowerUp.Rocket },                          // Epic
            new[] { PowerUp.Flight, PowerUp.Rocket },                          // Legendary
        };

        static void RollPowerUps(ChestDef def, Rarity best, ChestRng rng, ChestResult res)
        {
            int units = rng.Range(def.UnitsMin, def.UnitsMax);
            int types = Math.Min(def.Types, units);
            var ids = new List<string>(types);
            // The first type carries the rolled rarity; the others roll again from the same odds (never above the best).
            for (int t = 0; t < types; t++)
            {
                Rarity r = t == 0 ? best : (Rarity)Math.Min((int)best, (int)RollRarity(def.Odds, Rarity.Common, rng));
                PowerUp[] pool = Pools[(int)r];
                string id = PowerUps.IdOf(pool[rng.Range(pool.Length)]);
                if (!ids.Contains(id)) ids.Add(id);
            }
            int each = units / ids.Count, extra = units % ids.Count;
            for (int i = 0; i < ids.Count; i++) res.PowerUps.Add(new KeyValuePair<string, int>(ids[i], each + (i < extra ? 1 : 0)));
        }
    }
}
