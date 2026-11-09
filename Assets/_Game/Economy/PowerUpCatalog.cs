using System.Collections.Generic;
using FluffyClimbers.Simulation;
using Newtonsoft.Json.Linq;

namespace FluffyClimbers.Economy
{
    /// <summary>One sellable power-up (docs/15 section 3.2, Content/economy/powerups.json). Effect numbers live in SimConfig.</summary>
    public sealed class PowerUpDef
    {
        public string Id;
        public PowerUp Type;
        public int Order;
        public string Name;
        public string Description;
        /// <summary>"button" or "auto".</summary>
        public string Trigger;
        public int DurationSec;
        public int Uses;
        public int CoinPrice;
        public int GemPrice;
        public int UnlockRegion;
        /// <summary>False for ids that are reserved by the data format but have no simulation feature yet; they are hidden everywhere.</summary>
        public bool Implemented;
        public string Icon;
    }

    public enum PowerUpCurrency
    {
        Coins,
        Gems
    }

    public enum PowerUpBuyResult
    {
        Ok,
        NotEnoughCoins,
        NotEnoughGems,
        /// <summary>The region that unlocks this power-up has not been reached.</summary>
        Locked,
        Invalid
    }

    /// <summary>
    /// The power-up table. The runtime uses the built-in copy below (no manifest change needed); a unit test keeps it identical to
    /// Content/economy/powerups.json, which is the editable source (and is parsed by FromJson for tools and remote overrides).
    /// </summary>
    public sealed class PowerUpCatalog
    {
        readonly List<PowerUpDef> all = new List<PowerUpDef>();
        readonly Dictionary<string, PowerUpDef> byId = new Dictionary<string, PowerUpDef>();
        readonly List<PowerUpDef> implemented = new List<PowerUpDef>();

        public IReadOnlyList<PowerUpDef> All { get { return all; } }
        /// <summary>Only the power-ups that exist in the simulation, in shop order.</summary>
        public IReadOnlyList<PowerUpDef> Implemented { get { return implemented; } }

        public PowerUpCatalog(IEnumerable<PowerUpDef> defs)
        {
            foreach (PowerUpDef d in defs)
            {
                all.Add(d);
                byId[d.Id] = d;
            }
            all.Sort((a, b) => a.Order.CompareTo(b.Order));
            foreach (PowerUpDef d in all) if (d.Implemented) implemented.Add(d);
        }

        public PowerUpDef Get(string id)
        {
            PowerUpDef d;
            return id != null && byId.TryGetValue(id, out d) ? d : null;
        }

        public PowerUpDef Get(PowerUp type)
        {
            return Get(PowerUps.IdOf(type));
        }

        // type, display name, description, trigger, duration s per tap, uses per tap, coins, gems (docs/22 4.3: 50 gems for the 1,000-coin types, 75 for the 1,500-coin types), implemented.
        // Rework 2026-10-31: a tap while active adds the full duration onto the remaining time; no stock cap, no loadout, no carry cap.
        static readonly object[][] Rows =
        {
            new object[] { PowerUp.Flight, "Flight", "Fly upward for 10 seconds and steer left and right. Gongs cannot hurt you. Tap again to add 10 more seconds.", "button", 10, 1, 1500, 75, true },
            new object[] { PowerUp.Rocket, "Rocket", "Auto-climb 20 floors in about 4 seconds. Gongs cannot hurt you. Tap again to add 20 more floors. Stops before the last floors of a level.", "button", 4, 1, 1500, 75, true },
            new object[] { PowerUp.DoubleJump, "Double Jump", "For 30 seconds you can jump once more in mid-air. Tap again to add 30 seconds.", "button", 30, 1, 1000, 50, true },
            new object[] { PowerUp.WallStick, "Wall Stick", "For 30 seconds you stick to the side walls for up to 1.5 seconds. Jump to leap off. Tap again to add 30 seconds.", "button", 30, 1, 1000, 50, true },
            new object[] { PowerUp.Shield, "Shield", "Gongs cannot hurt you for 60 seconds. Tap again to add 60 seconds.", "button", 60, 1, 1000, 50, true },
            new object[] { PowerUp.Magnet, "Magnet", "Gems and stars come to you from farther away for 30 seconds. Tap again to add 30 seconds.", "button", 30, 1, 1000, 50, true },
            new object[] { PowerUp.SlowTime, "Slow Time", "The rising danger zone moves 50 percent slower for 30 seconds. Tap again to add 30 seconds.", "button", 30, 1, 1500, 75, true },
            new object[] { PowerUp.SpringBoots, "Spring Boots", "The next 3 jumps are 20 percent higher.", "button", 0, 3, 1000, 0, false },
            new object[] { PowerUp.CoinDoubler, "Coin Doubler", "Doubles the coins paid at the end of this level.", "button", 0, 1, 120, 0, false },
            new object[] { PowerUp.StarSeeker, "Star Seeker", "Arrows at the screen edge point to stars you have not collected yet.", "button", 12, 1, 800, 0, false },
            new object[] { PowerUp.SafetyNet, "Safety Net", "Catches you the first time you would fall off the bottom.", "auto", 0, 1, 1500, 0, false },
        };

        public static PowerUpCatalog BuiltIn()
        {
            var list = new List<PowerUpDef>(Rows.Length);
            int order = 1;
            foreach (object[] r in Rows)
            {
                var type = (PowerUp)r[0];
                list.Add(new PowerUpDef
                {
                    Id = PowerUps.IdOf(type), Type = type, Order = order++, Name = (string)r[1], Description = (string)r[2],
                    Trigger = (string)r[3], DurationSec = (int)r[4], Uses = (int)r[5],
                    CoinPrice = (int)r[6], GemPrice = (int)r[7], UnlockRegion = 1, Implemented = (bool)r[8], Icon = PowerUps.IdOf(type)
                });
            }
            return new PowerUpCatalog(list);
        }

        /// <summary>Parses Content/economy/powerups.json (an array). Entries with an unknown id are skipped.</summary>
        public static PowerUpCatalog FromJson(string json)
        {
            var list = new List<PowerUpDef>();
            JArray arr = JArray.Parse(json);
            foreach (JToken t in arr)
            {
                string id = (string)t["id"];
                PowerUp type = PowerUps.FromId(id);
                if (type == PowerUp.None) continue;
                JToken price = t["price"];
                list.Add(new PowerUpDef
                {
                    Id = id, Type = type, Order = (int?)t["order"] ?? 0, Name = (string)t["name"], Description = (string)t["description"],
                    Trigger = (string)t["trigger"] ?? "button", DurationSec = (int?)t["durationSec"] ?? 0, Uses = (int?)t["uses"] ?? 1,
                    CoinPrice = price != null ? ((int?)price["coins"] ?? 0) : 0, GemPrice = price != null ? ((int?)price["gems"] ?? 0) : 0,
                    UnlockRegion = (int?)t["unlockRegion"] ?? 1, Implemented = (bool?)t["implemented"] ?? false, Icon = (string)t["icon"]
                });
            }
            return new PowerUpCatalog(list);
        }

        /// <summary>
        /// Price of <paramref name="quantity"/> units: unit price x quantity, with the bundle discounts of docs/15 (5 units -10 percent,
        /// 10 or more units -20 percent), rounded half up. 0 when the currency is not offered for this power-up.
        /// </summary>
        public static int PriceFor(PowerUpDef def, PowerUpCurrency currency, int quantity)
        {
            if (def == null || quantity <= 0) return 0;
            long unit = currency == PowerUpCurrency.Coins ? def.CoinPrice : def.GemPrice;
            if (unit <= 0) return 0;
            int percent = quantity >= 10 ? 80 : (quantity >= 5 ? 90 : 100);
            long total = unit * quantity * percent;
            long price = (total + 50) / 100;
            return price > int.MaxValue ? int.MaxValue : (int)price;
        }
    }
}
