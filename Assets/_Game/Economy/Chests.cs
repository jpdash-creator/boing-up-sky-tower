using System.Collections.Generic;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Simulation;

namespace BoingUpSkyTower.Economy
{
    public enum ChestKind
    {
        Iron = 0,
        Golden = 1
    }

    public enum ChestOpenResult
    {
        Ok,
        NotEnoughGems,
        Invalid
    }

    /// <summary>What one chest gave.</summary>
    public sealed class ChestContents
    {
        /// <summary>Character ids that dropped (up to 1 from an Iron chest, up to 3 from a Golden one).</summary>
        public readonly List<string> Characters = new List<string>();
        /// <summary>Per character above: true when the player already had it (it turned into coins).</summary>
        public readonly List<bool> Duplicate = new List<bool>();
        /// <summary>Power-up ids, one entry per unit (3 in an Iron chest, 6 in a Golden one; the same type may repeat).</summary>
        public readonly List<string> PowerUps = new List<string>();
        /// <summary>All coins of this chest, duplicate refunds included.</summary>
        public int Coins;
    }

    /// <summary>
    /// Treasure chests, opened with gems only (docs/21). Iron: up to 1 character (Common 100, Uncommon 50, Rare 25 percent, the best hit wins), 3 power-ups and
    /// 800 to 1000 coins. Golden: up to 3 characters (Rare 100, Epic 50, Legendary 25 percent, each rolls on its own), 6 power-ups and 2500 to 3000 coins.
    /// Open 10 costs 10 times the single price (no discount). A rarity with no finished character drops nothing.
    /// </summary>
    public static class ChestRolls
    {
        public const int IronPowerUps = 3, GoldenPowerUps = 6;
        public const int IronMinCoins = 800, IronMaxCoins = 1000, GoldenMinCoins = 2500, GoldenMaxCoins = 3000;
        public static readonly int[] IronPercent = { 100, 50, 25 };        // Common, Uncommon, Rare
        public static readonly int[] GoldenPercent = { 100, 50, 25 };      // Rare, Epic, Legendary
        public const int IronMaxCharacters = 1, GoldenMaxCharacters = 3;
        public const int IronPrice = 100, IronPriceTen = 1000, GoldenPrice = 300, GoldenPriceTen = 3000;

        public static int Price(ChestKind kind, int opens)
        {
            if (opens != 1 && opens != 10) return 0;
            if (kind == ChestKind.Iron) return opens == 1 ? IronPrice : IronPriceTen;
            return opens == 1 ? GoldenPrice : GoldenPriceTen;
        }

        public static int MinCoins(ChestKind kind) { return kind == ChestKind.Iron ? IronMinCoins : GoldenMinCoins; }
        public static int MaxCoins(ChestKind kind) { return kind == ChestKind.Iron ? IronMaxCoins : GoldenMaxCoins; }
        public static int MaxCharacters(ChestKind kind) { return kind == ChestKind.Iron ? IronMaxCharacters : GoldenMaxCharacters; }
        public static int[] Percents(ChestKind kind) { return kind == ChestKind.Iron ? IronPercent : GoldenPercent; }

        /// <summary>The first rarity of the chest's ladder (Common for Iron, Rare for Golden).</summary>
        public static CharacterRarity LowestRarity(ChestKind kind) { return kind == ChestKind.Iron ? CharacterRarity.Common : CharacterRarity.Rare; }

        public static int PowerUpCount(ChestKind kind) { return kind == ChestKind.Iron ? IronPowerUps : GoldenPowerUps; }

        /// <summary>Seed of one chest opening: the kind and the running count of chests opened (stored in the save).</summary>
        public static int SeedOf(ChestKind kind, int openIndex)
        {
            unchecked { return ((openIndex + 1) * (int)0x9E3779B1) ^ (((int)kind + 1) * 0x5bd1e995); }
        }

        /// <summary>Rolls one chest. The implemented power-up types are equally likely, one roll per unit.</summary>
        public static ChestContents Roll(ChestKind kind, int openIndex, IList<PowerUpDef> powerUpTypes)
        {
            var rng = new Rng(SeedOf(kind, openIndex));
            var result = new ChestContents();
            int[] percents = Percents(kind);
            int low = (int)LowestRarity(kind), cap = MaxCharacters(kind);
            // best rarity first, so the one-character Iron chest keeps its best hit
            for (int i = percents.Length - 1; i >= 0; i--)
            {
                bool hit = rng.Next(100) < percents[i];
                List<CharacterSpec> pool = CharacterDefs.OfRarity((CharacterRarity)(low + i));
                int pick = pool.Count > 0 ? rng.Next(pool.Count) : 0;
                if (hit && pool.Count > 0 && result.Characters.Count < cap) result.Characters.Add(pool[pick].Id);
            }
            int units = PowerUpCount(kind);
            for (int i = 0; i < units; i++)
                result.PowerUps.Add(powerUpTypes[rng.Next(powerUpTypes.Count)].Id);
            result.Coins = rng.Range(MinCoins(kind), MaxCoins(kind));
            return result;
        }
    }

    /// <summary>Treasure chests and owned characters. Every public change saves once.</summary>
    public sealed partial class ProgressService
    {
        /// <summary>Saves with a bad chest counter or character list get safe defaults; the current save version is stamped.</summary>
        void EnsureChestFields()
        {
            if (Data.Characters == null) Data.Characters = new List<string>();
            Data.Characters.RemoveAll(id => CharacterDefs.Get(id) == null);
            if (Data.ChestOpens < 0) Data.ChestOpens = 0;
            if (Data.Version < SaveData.CurrentVersion) Data.Version = SaveData.CurrentVersion;
        }

        /// <summary>True when the player has the character: a Common one is owned when its region is unlocked; any other came from a chest or the shop.</summary>
        public bool OwnsCharacter(string id)
        {
            CharacterSpec c = CharacterDefs.Get(id);
            if (c == null) return false;
            if (Data.Characters.Contains(id)) return true;
            if (c.Rarity != CharacterRarity.Common || c.Region <= 0) return false;
            if (c.Region == 1) return true;
            RegionDef r = repo.TryGetRegionByIndex(c.Region);
            return r != null && IsRegionUnlocked(r);
        }

        /// <summary>Gives a character (shop, tests). Returns false for an unknown or already owned one.</summary>
        public bool GrantCharacter(string id)
        {
            if (CharacterDefs.Get(id) == null || OwnsCharacter(id)) return false;
            Data.Characters.Add(id);
            Commit();
            return true;
        }

        /// <summary>
        /// Opens 1 or 10 chests for gems only. Debits the gems and credits the characters, power-ups and coins in one step and saves once (atomic).
        /// A character the player already has turns into coins. Nothing changes unless the result is Ok. <paramref name="contents"/> has one entry per chest.
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
                foreach (string cid in c.Characters)
                {
                    bool dup = OwnsCharacter(cid);
                    c.Duplicate.Add(dup);
                    if (dup) c.Coins += CharacterDefs.DuplicateCoins[(int)CharacterDefs.Get(cid).Rarity];
                    else Data.Characters.Add(cid);
                }
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
