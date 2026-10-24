using System.Collections.Generic;

namespace BoingUpSkyTower.Economy
{
    public enum GearSlot
    {
        Hat,
        Shirt,
        Pants,
        Boots
    }

    public enum Rarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    /// <summary>Names and fixed numbers per rarity (docs/15 sections 4 and 5). Rarity is also shown by a shape cue (stripe count), never by colour alone.</summary>
    public static class RarityInfo
    {
        public static string Name(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return "Rare";
                case Rarity.Epic: return "Epic";
                case Rarity.Legendary: return "Legendary";
                default: return "Common";
            }
        }

        /// <summary>Style Shards a duplicate pays (docs/15 section 4.1).</summary>
        public static int DuplicateShards(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return 15;
                case Rarity.Epic: return 40;
                case Rarity.Legendary: return 100;
                default: return 5;
            }
        }

        /// <summary>The shape cue: 0 stripes for Common up to 3 for Legendary.</summary>
        public static int Stripes(Rarity r) { return (int)r; }

        public static string SlotName(GearSlot s)
        {
            switch (s)
            {
                case GearSlot.Hat: return "Hat";
                case GearSlot.Shirt: return "Shirt";
                case GearSlot.Pants: return "Pants";
                default: return "Boots";
            }
        }
    }

    /// <summary>One ornamental gear piece (docs/15 section 5). Gear never changes physics, pickups, payouts or scoring.</summary>
    public sealed class GearDef
    {
        public string Id;
        public string Name;
        public GearSlot Slot;
        public Rarity Rarity;
        /// <summary>0 or 1: which of the two placeholder designs of this slot and rarity.</summary>
        public int Design;
        /// <summary>Coin price in the Gear screen; 0 = not sold (Legendary pieces come from chests only).</summary>
        public int CoinPrice;
    }

    /// <summary>The 32 placeholder pieces: 4 slots x 4 rarities x 2 designs. Ids are permanent (project rule).</summary>
    public sealed class GearCatalog
    {
        public const int Count = 32;

        readonly List<GearDef> all = new List<GearDef>();
        readonly Dictionary<string, GearDef> byId = new Dictionary<string, GearDef>();

        public IReadOnlyList<GearDef> All { get { return all; } }

        static readonly int[] CoinPrices = { 1200, 4000, 12000, 0 };

        // Names per slot: Common x2, Rare x2, Epic x2, Legendary x2.
        static readonly string[][] Names =
        {
            new[] { "Straw Hat", "Knit Beanie", "Sailor Cap", "Explorer Hat", "Wizard Hat", "Captain Hat", "Cloud Crown", "Sun Crown" },
            new[] { "Plain Tee", "Striped Tee", "Sky Hoodie", "Denim Vest", "Star Jacket", "Storm Cape", "Golden Tunic", "Sun Armor" },
            new[] { "Blue Shorts", "Brown Trousers", "Cargo Pants", "Denim Overalls", "Night Trousers", "Racing Pants", "Golden Leggings", "Royal Pants" },
            new[] { "Sneakers", "Rain Boots", "Hiking Boots", "Jet Sneakers", "Moon Boots", "Cloud Runners", "Golden Boots", "Rainbow Boots" },
        };

        static readonly string[] SlotKey = { "hat", "shirt", "pants", "boots" };

        public GearCatalog()
        {
            for (int s = 0; s < 4; s++)
            {
                for (int i = 0; i < 8; i++)
                {
                    var r = (Rarity)(i / 2);
                    string name = Names[s][i];
                    var def = new GearDef
                    {
                        Id = SlotKey[s] + "_" + name.ToLowerInvariant().Replace(' ', '_'),
                        Name = name,
                        Slot = (GearSlot)s,
                        Rarity = r,
                        Design = i % 2,
                        CoinPrice = CoinPrices[(int)r],
                    };
                    all.Add(def);
                    byId[def.Id] = def;
                }
            }
        }

        static GearCatalog builtIn;

        public static GearCatalog BuiltIn() { return builtIn ?? (builtIn = new GearCatalog()); }

        public GearDef Get(string id)
        {
            GearDef d;
            return id != null && byId.TryGetValue(id, out d) ? d : null;
        }

        /// <summary>Pieces of one slot, Common first.</summary>
        public List<GearDef> InSlot(GearSlot slot)
        {
            var list = new List<GearDef>(8);
            foreach (GearDef d in all) if (d.Slot == slot) list.Add(d);
            return list;
        }

        public List<GearDef> OfRarity(Rarity r)
        {
            var list = new List<GearDef>(8);
            foreach (GearDef d in all) if (d.Rarity == r) list.Add(d);
            return list;
        }
    }
}
