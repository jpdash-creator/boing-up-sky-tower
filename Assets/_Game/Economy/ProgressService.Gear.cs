using System.Collections.Generic;
using BoingUpSkyTower.Simulation;

namespace BoingUpSkyTower.Economy
{
    public enum ChestOpenResult
    {
        Ok,
        NotEnoughGems,
        Invalid
    }

    /// <summary>Gear, worn gear and treasure chests (docs/17, docs/19). Every public change saves once.</summary>
    public sealed partial class ProgressService
    {
        /// <summary>
        /// Saves written before gear (version 1), with the old four slot list (version 2: head, body, pants, boots) or with bad entries get safe defaults and are marked as
        /// the current version: unknown or removed ids and non-positive counts are dropped (the pants slot and the forge levels are gone), the worn list has 3 entries and
        /// each must be an owned piece of its own slot.
        /// </summary>
        void EnsureGearFields()
        {
            if (Data.Inventory == null) Data.Inventory = new InventoryData();
            if (Data.Inventory.Gear == null) Data.Inventory.Gear = new Dictionary<string, int>();
            var bad = new List<string>();
            foreach (var kv in Data.Inventory.Gear) if (GearCatalog.Get(kv.Key) == null || kv.Value <= 0) bad.Add(kv.Key);
            foreach (string id in bad) Data.Inventory.Gear.Remove(id);

            if (Data.Equipped == null) Data.Equipped = new List<string>();
            if (Data.Version < 3 && Data.Equipped.Count == 4)
                Data.Equipped.RemoveAt(2);   // the old pants entry; boots move up to slot 2
            while (Data.Equipped.Count < GearCatalog.SlotCount) Data.Equipped.Add("");
            if (Data.Equipped.Count > GearCatalog.SlotCount) Data.Equipped.RemoveRange(GearCatalog.SlotCount, Data.Equipped.Count - GearCatalog.SlotCount);
            for (int s = 0; s < GearCatalog.SlotCount; s++)
            {
                GearDef d = GearCatalog.Get(Data.Equipped[s]);
                if (d == null || (int)d.Slot != s || !Data.Inventory.Gear.ContainsKey(d.Id)) Data.Equipped[s] = "";
            }
            if (Data.ChestOpens < 0) Data.ChestOpens = 0;
            if (Data.Version < SaveData.CurrentVersion) Data.Version = SaveData.CurrentVersion;
        }

        // ---- owned gear ----

        /// <summary>Owned copies of a piece (0 when none or unknown).</summary>
        public int GearCount(string id)
        {
            int n;
            return id != null && Data.Inventory.Gear.TryGetValue(id, out n) ? n : 0;
        }

        public bool OwnsGear(string id) { return GearCount(id) > 0; }

        /// <summary>Distinct pieces owned.</summary>
        public int OwnedGearKinds { get { return Data.Inventory.Gear.Count; } }

        /// <summary>Adds one copy without saving (callers commit once). Returns false for an unknown id.</summary>
        bool AddGearCopy(string id)
        {
            if (GearCatalog.Get(id) == null) return false;
            Data.Inventory.Gear[id] = GearCount(id) + 1;
            return true;
        }

        /// <summary>Adds copies of a piece (rewards, tests) and saves. Returns false for an unknown id or a count below 1.</summary>
        public bool GrantGear(string id, int count = 1)
        {
            if (GearCatalog.Get(id) == null || count <= 0) return false;
            for (int i = 0; i < count; i++) AddGearCopy(id);
            Commit();
            return true;
        }

        // ---- worn gear ----

        public string EquippedId(GearSlot slot)
        {
            string id = Data.Equipped[(int)slot];
            return string.IsNullOrEmpty(id) ? null : id;
        }

        /// <summary>The piece worn in a slot. A worn piece without art counts as nothing worn (it neither shows nor boosts) until its art exists.</summary>
        public GearDef EquippedDef(GearSlot slot)
        {
            GearDef d = GearCatalog.Get(Data.Equipped[(int)slot]);
            return GearCatalog.HasArt(d) ? d : null;
        }

        /// <summary>Wears an owned piece in its own slot, replacing what was there. Saves at once. False when the piece is not owned or has no art.</summary>
        public bool Equip(string id)
        {
            GearDef d = GearCatalog.Get(id);
            if (d == null || !OwnsGear(id) || !GearCatalog.HasArt(d)) return false;
            if (Data.Equipped[(int)d.Slot] == id) return true;
            Data.Equipped[(int)d.Slot] = id;
            Commit();
            return true;
        }

        public void Unequip(GearSlot slot)
        {
            if (string.IsNullOrEmpty(Data.Equipped[(int)slot])) return;
            Data.Equipped[(int)slot] = "";
            Commit();
        }

        /// <summary>Boost in percent per power-up (index = PowerUp enum value) from what is worn.</summary>
        public int[] GearBoostPercents()
        {
            var worn = new GearDef[GearCatalog.SlotCount];
            for (int s = 0; s < worn.Length; s++) worn[s] = EquippedDef((GearSlot)s);
            return GearBoosts.Compute(worn);
        }

        /// <summary>A copy of <paramref name="baseCfg"/> with the worn gear's boosts applied. The shared config itself is never changed.</summary>
        public SimConfig BuildRunConfig(SimConfig baseCfg)
        {
            SimConfig cfg = baseCfg.Clone();
            GearBoosts.Apply(cfg, GearBoostPercents());
            return cfg;
        }

        // ---- treasure chests (docs/17 section D) ----

        /// <summary>
        /// Opens 1 or 10 chests for gems only. Debits the gems and credits the gear, power-ups and coins in one step and saves once (atomic).
        /// Nothing changes unless the result is Ok. <paramref name="contents"/> has one entry per chest.
        /// </summary>
        public ChestOpenResult TryOpenChests(ChestKind kind, int opens, out List<ChestContents> contents)
        {
            contents = null;
            int price = ChestRolls.Price(kind, opens);
            if (price <= 0) return ChestOpenResult.Invalid;
            if (Data.Gems < price) return ChestOpenResult.NotEnoughGems;
            var types = new List<PowerUpDef>(PowerUpDefs.Implemented);
            if (types.Count == 0) return ChestOpenResult.Invalid;
            Data.Gems -= price;
            contents = new List<ChestContents>(opens);
            for (int i = 0; i < opens; i++)
            {
                ChestContents c = ChestRolls.Roll(kind, Data.ChestOpens++, types);
                foreach (string gid in c.Gear) AddGearCopy(gid);
                foreach (string pid in c.PowerUps)
                {
                    int have = PowerUpCount(pid);
                    if (have < PowerUpDefs.Get(pid).MaxStack) Data.Inventory.Powerups[pid] = have + 1;   // a full stack takes no more
                }
                Data.Coins = SaturatingAdd(Data.Coins, c.Coins);
                contents.Add(c);
            }
            Commit();
            return ChestOpenResult.Ok;
        }
    }
}
