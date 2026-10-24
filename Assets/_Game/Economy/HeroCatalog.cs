using System.Collections.Generic;

namespace BoingUpSkyTower.Economy
{
    /// <summary>One character of the 20 (docs/15 section 7.1): 4 per region, 1 free that unlocks with its region, 3 bought.</summary>
    public sealed class HeroDef
    {
        public string Id;
        public int Region;
        public string Name;
        public string Species;
        public Rarity Rarity;
        public string AbilityName;
        public string AbilityText;
        /// <summary>Permanent small bonuses in percent (caps: jump 6, gem find 5, coin find 5). Data only until the bonus pipeline exists.</summary>
        public int JumpPct, GemFindPct, CoinFindPct;
        public int CoinPrice, GemPrice;
        /// <summary>Placeholder look: Momo multiplied by this colour (0xRRGGBB). Real art for region 1 needs the owner's approval.</summary>
        public int Tint;
        public bool Free { get { return CoinPrice <= 0 && GemPrice <= 0; } }

        public string StatsText
        {
            get
            {
                var parts = new List<string>();
                if (JumpPct > 0) parts.Add("Jump +" + JumpPct + "%");
                if (GemFindPct > 0) parts.Add("Gem find +" + GemFindPct + "%");
                if (CoinFindPct > 0) parts.Add("Coin find +" + CoinFindPct + "%");
                return parts.Count == 0 ? "No stat bonus" : string.Join(", ", parts);
            }
        }
    }

    public sealed class HeroCatalog
    {
        /// <summary>The abilities and stat bonuses are data only: the simulation does not read them yet (needs the bonus pipeline, docs/15 FR-REGION-2). The screens say so.</summary>
        public const bool AbilitiesActive = false;
        public const string DefaultId = "char_momo";

        readonly List<HeroDef> all = new List<HeroDef>();
        readonly Dictionary<string, HeroDef> byId = new Dictionary<string, HeroDef>();

        public IReadOnlyList<HeroDef> All { get { return all; } }

        // region, id, name, species, rarity, ability, ability text, jump, gem, coin, coins, gems, tint
        static readonly object[][] Rows =
        {
            new object[] { 1, "char_momo", "Momo", "fluffy cat", Rarity.Common, "Flower Friend", "Bounce flowers launch you 15% higher.", 0, 0, 0, 0, 0, 0xFFFFFF },
            new object[] { 1, "char_pip", "Pip", "bee", Rarity.Rare, "Hover", "Hold jump while falling to slow the fall for half a second.", 0, 3, 0, 4000, 0, 0xFFE680 },
            new object[] { 1, "char_clover", "Clover", "rabbit", Rarity.Epic, "Bunny Streak", "After a 5-step combo the next jump is 20% higher, once.", 4, 0, 3, 9000, 0, 0xB8F0A8 },
            new object[] { 1, "char_petal", "Petal", "dragonfly", Rarity.Legendary, "Wing Rescue", "Once per run, falling off the bottom turns into a 2 second flight up.", 6, 5, 0, 0, 250, 0xF8B4E8 },
            new object[] { 2, "char_spora", "Spora", "mushroom sprite", Rarity.Common, "Cap Cushion", "Landing on a bounce cap adds one combo step.", 0, 0, 0, 0, 0, 0xF0B0A0 },
            new object[] { 2, "char_truffle", "Truffle", "mole", Rarity.Rare, "Burrow Grip", "Touching a wall holds you for 0.4 seconds.", 0, 0, 3, 5000, 0, 0xC8A888 },
            new object[] { 2, "char_glim", "Glim", "glow-worm", Rarity.Epic, "Glow Reach", "Pickups are collected from 25% farther away.", 3, 4, 0, 11000, 0, 0xD8F890 },
            new object[] { 2, "char_baroncap", "Baron Cap", "stag beetle", Rarity.Legendary, "Cap Surge", "After a bounce-cap launch you drift upward for 1.2 seconds more.", 6, 5, 0, 0, 300, 0xB898E8 },
            new object[] { 3, "char_shelly", "Shelly", "hermit crab", Rarity.Common, "Sand Grip", "Landing on a drifting platform cancels its sideways push for 0.3 seconds.", 0, 0, 0, 0, 0, 0xFFC8A0 },
            new object[] { 3, "char_gully", "Gully", "gull", Rarity.Rare, "Sea Breeze", "Drifting platforms near you move 30% slower.", 0, 0, 3, 6000, 0, 0xB8E4F8 },
            new object[] { 3, "char_tortuga", "Tortuga", "turtle", Rarity.Epic, "Sandcastle", "Crumbling platforms last twice as long.", 0, 3, 5, 13000, 0, 0x98E8B8 },
            new object[] { 3, "char_inky", "Inky", "octopus", Rarity.Legendary, "Ink Cloud", "Once per run, near the bottom edge the scroll slows by half for 4 seconds.", 6, 5, 0, 0, 350, 0xE898C8 },
            new object[] { 4, "char_gumdrop", "Gumdrop", "jelly bear", Rarity.Common, "Gum Boots", "The sticky-platform jump penalty is halved.", 0, 0, 0, 0, 0, 0xFF9CB0 },
            new object[] { 4, "char_toffee", "Toffee", "otter", Rarity.Rare, "Peel Off", "No delay after landing on a sticky platform.", 0, 3, 0, 7000, 0, 0xE8B070 },
            new object[] { 4, "char_mint", "Mint", "pup", Rarity.Epic, "Sugar Rush", "Every 10 pickups, 3 seconds of faster air speed.", 4, 4, 0, 15000, 0, 0xA0F0D8 },
            new object[] { 4, "char_sprinkles", "Sprinkles", "cake dragon", Rarity.Legendary, "Sugar Wings", "Tap in the air for 1.5 seconds of flight, then wait 20 seconds.", 6, 0, 5, 0, 400, 0xFFB0F0 },
            new object[] { 5, "char_bao", "Bao", "panda", Rarity.Common, "Gentle Thud", "Gong knockback is 40% weaker.", 0, 0, 0, 0, 0, 0xD8D8E0 },
            new object[] { 5, "char_kiri", "Kiri", "crane", Rarity.Rare, "Gong Dodge", "A gong hit is ignored once every 15 seconds.", 0, 0, 3, 8000, 0, 0xFFD0D0 },
            new object[] { 5, "char_ronin", "Ronin", "red panda", Rarity.Epic, "Wall Run", "Touching a wall holds you for 0.8 seconds and the next jump is 10% higher.", 4, 4, 0, 17000, 0, 0xF8A070 },
            new object[] { 5, "char_longyi", "Longyi", "dragon", Rarity.Legendary, "Gong Surge", "A gong hit becomes an upward boost of about 1.5 floors.", 6, 5, 5, 0, 450, 0xF8D060 },
        };

        public HeroCatalog()
        {
            foreach (object[] r in Rows)
            {
                var d = new HeroDef
                {
                    Region = (int)r[0], Id = (string)r[1], Name = (string)r[2], Species = (string)r[3], Rarity = (Rarity)r[4],
                    AbilityName = (string)r[5], AbilityText = (string)r[6],
                    JumpPct = (int)r[7], GemFindPct = (int)r[8], CoinFindPct = (int)r[9], CoinPrice = (int)r[10], GemPrice = (int)r[11], Tint = (int)r[12],
                };
                all.Add(d);
                byId[d.Id] = d;
            }
        }

        static HeroCatalog builtIn;

        public static HeroCatalog BuiltIn() { return builtIn ?? (builtIn = new HeroCatalog()); }

        public HeroDef Get(string id)
        {
            HeroDef d;
            return id != null && byId.TryGetValue(id, out d) ? d : null;
        }

        public List<HeroDef> OfRegion(int region)
        {
            var list = new List<HeroDef>(4);
            foreach (HeroDef d in all) if (d.Region == region) list.Add(d);
            return list;
        }
    }
}
