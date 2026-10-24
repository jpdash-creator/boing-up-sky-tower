using System;
using System.Collections.Generic;
using BoingUpSkyTower.Content;

namespace BoingUpSkyTower.Economy
{
    public enum StoreBuyResult
    {
        Ok,
        NotEnoughCoins,
        NotEnoughGems,
        AlreadyOwned,
        /// <summary>Its region is not open yet.</summary>
        Locked,
        /// <summary>Not for sale (Legendary gear comes from chests) or unknown.</summary>
        NotForSale,
        Invalid
    }

    public enum HeroState
    {
        Owned,
        /// <summary>Region open, not yet bought.</summary>
        ForSale,
        /// <summary>Its region is not open yet.</summary>
        RegionLocked
    }

    /// <summary>
    /// Gear, characters, chests and VIP (docs/15). Every mutation saves once through Commit, like the rest of the service. Partial so the
    /// wallet, progress and power-up code stays in ProgressService.cs.
    /// </summary>
    public sealed partial class ProgressService
    {
        readonly GearCatalog gearDefs = GearCatalog.BuiltIn();
        readonly HeroCatalog heroDefs = HeroCatalog.BuiltIn();
        readonly ChestCatalog chestDefs = ChestCatalog.BuiltIn();

        public GearCatalog GearDefs { get { return gearDefs; } }
        public HeroCatalog HeroDefs { get { return heroDefs; } }
        public ChestCatalog ChestDefs { get { return chestDefs; } }

        static readonly string[] SlotKeys = { "hat", "shirt", "pants", "boots" };

        public static string SlotKey(GearSlot s) { return SlotKeys[(int)s]; }

        // ---------------- gear ----------------

        public bool OwnsGear(string id) { return id != null && Data.Inventory.Gear.ContainsKey(id); }

        public int GearStyleLevel(string id)
        {
            int v;
            return id != null && Data.Inventory.Gear.TryGetValue(id, out v) ? v : 0;
        }

        public int OwnedGearCount()
        {
            return Data.Inventory.Gear.Count;
        }

        /// <summary>Adds a piece to the wardrobe (chest, VIP, test). False for an unknown id or a piece already owned.</summary>
        public bool GrantGear(string id)
        {
            if (gearDefs.Get(id) == null || OwnsGear(id)) return false;
            Data.Inventory.Gear[id] = 0;
            Commit();
            return true;
        }

        /// <summary>Buys a piece with coins (atomic, one save). Legendary pieces are not sold.</summary>
        public StoreBuyResult TryBuyGear(string id)
        {
            GearDef def = gearDefs.Get(id);
            if (def == null) return StoreBuyResult.Invalid;
            if (OwnsGear(id)) return StoreBuyResult.AlreadyOwned;
            if (def.CoinPrice <= 0) return StoreBuyResult.NotForSale;
            if (Data.Coins < def.CoinPrice) return StoreBuyResult.NotEnoughCoins;
            Data.Coins -= def.CoinPrice;
            Data.Inventory.Gear[id] = 0;
            Commit();
            return StoreBuyResult.Ok;
        }

        /// <summary>Gear id worn in a slot by a character, or null.</summary>
        public string EquippedGear(string heroId, GearSlot slot)
        {
            Dictionary<string, string> worn;
            string id;
            if (heroId == null || !Data.Characters.Equipped.TryGetValue(heroId, out worn) || !worn.TryGetValue(SlotKey(slot), out id)) return null;
            return OwnsGear(id) ? id : null;
        }

        /// <summary>Wears an owned piece (replaces the piece in that slot). Free, saved at once.</summary>
        public bool EquipGear(string heroId, string gearId)
        {
            GearDef def = gearDefs.Get(gearId);
            if (def == null || heroDefs.Get(heroId) == null || !OwnsGear(gearId)) return false;
            Dictionary<string, string> worn;
            if (!Data.Characters.Equipped.TryGetValue(heroId, out worn))
                Data.Characters.Equipped[heroId] = worn = new Dictionary<string, string>();
            worn[SlotKey(def.Slot)] = gearId;
            Commit();
            return true;
        }

        /// <summary>Takes off whatever a character wears in a slot ("none" is allowed).</summary>
        public bool UnequipGear(string heroId, GearSlot slot)
        {
            Dictionary<string, string> worn;
            if (heroId == null || !Data.Characters.Equipped.TryGetValue(heroId, out worn) || !worn.Remove(SlotKey(slot))) return false;
            Commit();
            return true;
        }

        // ---------------- characters ----------------

        public string SelectedHeroId
        {
            get
            {
                HeroDef h = heroDefs.Get(Data.Characters.Selected);
                return h != null && HeroOwned(h) ? h.Id : HeroCatalog.DefaultId;
            }
        }

        /// <summary>True when the hero's region has been reached (region 1 always).</summary>
        public bool HeroRegionOpen(HeroDef hero)
        {
            return hero != null && (TestUnlockAll || hero.Region <= HighestUnlockedRegionIndex());
        }

        public bool HeroOwned(HeroDef hero)
        {
            if (hero == null) return false;
            if (hero.Free) return HeroRegionOpen(hero);
            return Data.Characters.Owned.Contains(hero.Id);
        }

        public HeroState StateOf(HeroDef hero)
        {
            if (HeroOwned(hero)) return HeroState.Owned;
            return HeroRegionOpen(hero) ? HeroState.ForSale : HeroState.RegionLocked;
        }

        /// <summary>Buys a character with coins or gems (whichever the price names), only when its region is open. Atomic, one save.</summary>
        public StoreBuyResult TryBuyHero(string id)
        {
            HeroDef h = heroDefs.Get(id);
            if (h == null) return StoreBuyResult.Invalid;
            if (HeroOwned(h)) return StoreBuyResult.AlreadyOwned;
            if (h.Free) return StoreBuyResult.Locked;
            if (!HeroRegionOpen(h)) return StoreBuyResult.Locked;
            if (h.CoinPrice > 0)
            {
                if (Data.Coins < h.CoinPrice) return StoreBuyResult.NotEnoughCoins;
                Data.Coins -= h.CoinPrice;
            }
            else
            {
                if (Data.Gems < h.GemPrice) return StoreBuyResult.NotEnoughGems;
                Data.Gems -= h.GemPrice;
            }
            Data.Characters.Owned.Add(h.Id);
            Commit();
            return StoreBuyResult.Ok;
        }

        /// <summary>Makes an owned character the one shown and used. Free, saved at once.</summary>
        public bool SelectHero(string id)
        {
            HeroDef h = heroDefs.Get(id);
            if (h == null || !HeroOwned(h)) return false;
            if (Data.Characters.Selected == id) return true;
            Data.Characters.Selected = id;
            Commit();
            return true;
        }

        // ---------------- chests ----------------

        public int ChestCount(string id)
        {
            int n;
            return id != null && Data.Inventory.Chests.TryGetValue(id, out n) ? n : 0;
        }

        public int TotalChests()
        {
            int n = 0;
            foreach (int c in Data.Inventory.Chests.Values) n += c;
            return n;
        }

        /// <summary>Adds unopened chests (earn rules, test). Returns how many were added.</summary>
        public int GrantChest(string id, int count)
        {
            if (chestDefs.Get(id) == null || count <= 0) return 0;
            int have = ChestCount(id);
            int add = Math.Min(count, 99 - have);
            if (add <= 0) return 0;
            Data.Inventory.Chests[id] = have + add;
            Commit();
            return add;
        }

        /// <summary>Buys one chest for its price (coins or gems), atomic and saved once. The chest goes into the stock; opening is a separate tap.</summary>
        public StoreBuyResult TryBuyChest(string id)
        {
            ChestDef def = chestDefs.Get(id);
            if (def == null) return StoreBuyResult.Invalid;
            if (ChestCount(id) >= 99) return StoreBuyResult.AlreadyOwned;
            if (def.CoinPrice > 0)
            {
                if (Data.Coins < def.CoinPrice) return StoreBuyResult.NotEnoughCoins;
                Data.Coins -= def.CoinPrice;
            }
            else
            {
                if (Data.Gems < def.GemPrice) return StoreBuyResult.NotEnoughGems;
                Data.Gems -= def.GemPrice;
            }
            Data.Inventory.Chests[id] = ChestCount(id) + 1;
            Commit();
            return StoreBuyResult.Ok;
        }

        /// <summary>A roll seed for the development builds: mixes the opened-chest counter with the clock so openings differ but a test can pass its own.</summary>
        public ulong NextChestSeed()
        {
            unchecked { return (ulong)Environment.TickCount * 0x9E3779B97F4A7C15UL ^ (ulong)(Data.ChestsOpened + 1) * 0xD6E8FEB86659FD93UL; }
        }

        /// <summary>
        /// Opens one owned chest: rolls with the pity counters, pays the result into the wallet, the wardrobe or the power-up stock and saves once
        /// (atomic: a failed open changes nothing). Returns null when none is owned.
        /// </summary>
        public ChestResult TryOpenChest(string id, ulong seed)
        {
            ChestDef def = chestDefs.Get(id);
            if (def == null || ChestCount(id) <= 0) return null;
            var rng = new ChestRng(seed);
            ChestResult res = ChestCatalog.Roll(def, Data.ChestPity, OwnsGear, gearDefs, rng);

            int left = ChestCount(id) - 1;
            if (left <= 0) Data.Inventory.Chests.Remove(id); else Data.Inventory.Chests[id] = left;
            Data.ChestsOpened++;
            if (res.Coins > 0) Data.Coins = SaturatingAdd(Data.Coins, res.Coins);
            if (res.GearId != null && !res.Duplicate) Data.Inventory.Gear[res.GearId] = 0;
            if (res.Shards > 0) Data.Inventory.Shards += res.Shards;
            foreach (KeyValuePair<string, int> pu in res.PowerUps)
            {
                PowerUpDef pd = PowerUpDefs.Get(pu.Key);
                if (pd == null) continue;
                int have = PowerUpCount(pu.Key);
                Data.Inventory.Powerups[pu.Key] = Math.Min(pd.MaxStack, have + pu.Value);
            }
            Commit();
            return res;
        }

        /// <summary>The pity counter shown on the chest dialog: chests opened since the last result of that rarity or better.</summary>
        public int PityCount(ChestKind kind, string tier)
        {
            return ChestCatalog.PityCounter(Data.ChestPity, kind, tier);
        }

        /// <summary>Upgrades an owned piece one style level for 100 Style Shards (cosmetic shine only).</summary>
        public bool TryUpgradeStyle(string gearId)
        {
            const int cost = 100, maxLevel = 3;
            if (!OwnsGear(gearId) || Data.Inventory.Shards < cost || GearStyleLevel(gearId) >= maxLevel) return false;
            Data.Inventory.Shards -= cost;
            Data.Inventory.Gear[gearId] = GearStyleLevel(gearId) + 1;
            Commit();
            return true;
        }

        // ---------------- VIP (UI only) ----------------

        public int VipTier { get { return Data.Vip.Tier; } }

        /// <summary>
        /// Development builds only (the caller checks): sets a local VIP tier so the screens can show the state. A real subscription arrives with billing
        /// and the server; no bonus of the tier is applied anywhere yet.
        /// </summary>
        public bool TestSetVipTier(int tier)
        {
            if (tier < 0 || tier > VipCatalog.TierCount) return false;
            Data.Vip.Tier = tier;
            Data.Vip.Source = tier == 0 ? "none" : "test";
            Commit();
            return true;
        }
    }
}
