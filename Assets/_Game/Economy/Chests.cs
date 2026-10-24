using System.Collections.Generic;
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
        /// <summary>Power-up ids, one entry per unit (3 in an Iron chest, 6 in a Golden one; the same type may repeat).</summary>
        public readonly List<string> PowerUps = new List<string>();
        public int Coins;
    }

    /// <summary>
    /// Treasure chests, opened with gems only. Iron: 3 power-ups and up to 1000 coins. Golden: 6 power-ups and up to 3000 coins. Open 10 costs 10 times the
    /// single price (no discount). Coins are rolled uniformly from a tenth of the maximum up to the maximum.
    /// </summary>
    public static class ChestRolls
    {
        public const int IronPowerUps = 3, GoldenPowerUps = 6;
        public const int IronMaxCoins = 1000, GoldenMaxCoins = 3000;
        public const int IronPrice = 100, IronPriceTen = 1000, GoldenPrice = 300, GoldenPriceTen = 3000;

        public static int Price(ChestKind kind, int opens)
        {
            if (opens != 1 && opens != 10) return 0;
            if (kind == ChestKind.Iron) return opens == 1 ? IronPrice : IronPriceTen;
            return opens == 1 ? GoldenPrice : GoldenPriceTen;
        }

        public static int MaxCoins(ChestKind kind) { return kind == ChestKind.Iron ? IronMaxCoins : GoldenMaxCoins; }

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
            int units = PowerUpCount(kind);
            for (int i = 0; i < units; i++)
                result.PowerUps.Add(powerUpTypes[rng.Next(powerUpTypes.Count)].Id);
            int max = MaxCoins(kind);
            result.Coins = rng.Range(max / 10, max);
            return result;
        }
    }

    /// <summary>Treasure chests. Every public change saves once.</summary>
    public sealed partial class ProgressService
    {
        /// <summary>Saves with a bad chest counter get a safe default; the current save version is stamped.</summary>
        void EnsureChestFields()
        {
            if (Data.ChestOpens < 0) Data.ChestOpens = 0;
            if (Data.Version < SaveData.CurrentVersion) Data.Version = SaveData.CurrentVersion;
        }

        /// <summary>
        /// Opens 1 or 10 chests for gems only. Debits the gems and credits the power-ups and coins in one step and saves once (atomic).
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
