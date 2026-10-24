using System.Collections.Generic;
using BoingUpSkyTower.Simulation;

namespace BoingUpSkyTower.Economy
{
    /// <summary>
    /// Gear drops on a level clear (docs/17 section B). Each of the 5 rarities rolls on its own at the percent below, then every hit picks a random
    /// slot and a random piece of that slot and rarity. Deterministic: the Simulation Rng seeded from the level seed and the clear count, so a server
    /// can recompute a drop.
    /// </summary>
    public static class GearDrops
    {
        // Common, Uncommon, Rare, Epic, Legendary
        public static readonly int[] NormalPercent = { 20, 10, 5, 2, 1 };
        public static readonly int[] MilestonePercent = { 40, 20, 10, 5, 2 };      // levels 5 and 10
        public static readonly int[] FinalPercent = { 80, 40, 20, 10, 5 };         // level 15

        public static int[] PercentsFor(int levelIndex)
        {
            if (levelIndex == 15) return FinalPercent;
            if (levelIndex == 5 || levelIndex == 10) return MilestonePercent;
            return NormalPercent;
        }

        /// <summary>Seed of a clear: the level seed mixed with how many times the level was cleared before (0 on the first clear).</summary>
        public static int SeedOf(int levelSeed, int clearsBefore)
        {
            unchecked { return (levelSeed * 1103515245) ^ (clearsBefore * 40503 + 0x6D2B79F5); }
        }

        /// <summary>Piece ids dropped by one clear (0 to 5 of them, in rarity order, duplicates possible).</summary>
        public static List<string> Roll(int levelIndex, int levelSeed, int clearsBefore)
        {
            var rng = new Rng(SeedOf(levelSeed, clearsBefore));
            return RollWith(ref rng, PercentsFor(levelIndex));
        }

        /// <summary>One independent roll per rarity at <paramref name="percents"/>; a hit picks a random slot and a random piece.</summary>
        public static List<string> RollWith(ref Rng rng, int[] percents)
        {
            var drops = new List<string>(2);
            for (int r = 0; r < GearCatalog.RarityCount; r++)
            {
                bool hit = rng.Next(100) < percents[r];
                if (!hit) continue;
                GearDef piece = RandomPiece(ref rng, (GearRarity)r);
                if (piece != null) drops.Add(piece.Id);
            }
            return drops;
        }

        /// <summary>A random slot, then a random piece of that slot and rarity that has art. Null when that slot and rarity has no painted piece yet (no drop).</summary>
        public static GearDef RandomPiece(ref Rng rng, GearRarity rarity)
        {
            var slot = (GearSlot)rng.Next(GearCatalog.SlotCount);
            List<GearDef> pieces = GearCatalog.PiecesWithArt(slot, rarity);
            if (pieces.Count == 0) return null;
            return pieces[rng.Next(pieces.Count)];
        }
    }

    public enum ChestKind
    {
        Iron = 0,
        Golden = 1
    }

    /// <summary>What one chest gave.</summary>
    public sealed class ChestContents
    {
        public readonly List<string> Gear = new List<string>();
        /// <summary>Power-up ids, one entry per unit (3 in an Iron chest, 6 in a Golden one; the same type may repeat).</summary>
        public readonly List<string> PowerUps = new List<string>();
        public int Coins;
    }

    /// <summary>
    /// Treasure chests (docs/17 section D), opened with gems only. Iron: gear Common 100 / Uncommon 50 / Rare 25 percent, 3 power-ups, up to 1000 coins.
    /// Golden: Rare 100 / Epic 50 / Legendary 25 percent, 6 power-ups, up to 3000 coins. Open 10 costs 10 times the single price (no discount).
    /// Coins are rolled uniformly from a tenth of the maximum up to the maximum (assumption, to confirm).
    /// </summary>
    public static class ChestRolls
    {
        public static readonly int[] IronGearPercent = { 100, 50, 25 };        // Common, Uncommon, Rare
        public static readonly int[] GoldenGearPercent = { 100, 50, 25 };      // Rare, Epic, Legendary
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

        /// <summary>The first rarity of the chest's gear ladder (Common for Iron, Rare for Golden).</summary>
        public static GearRarity LowestRarity(ChestKind kind) { return kind == ChestKind.Iron ? GearRarity.Common : GearRarity.Rare; }

        public static int PowerUpCount(ChestKind kind) { return kind == ChestKind.Iron ? IronPowerUps : GoldenPowerUps; }

        /// <summary>Seed of one chest opening: the kind and the running count of chests opened (stored in the save).</summary>
        public static int SeedOf(ChestKind kind, int openIndex)
        {
            unchecked { return ((openIndex + 1) * (int)0x9E3779B1) ^ (((int)kind + 1) * 0x5bd1e995); }
        }

        /// <summary>Rolls one chest. The 7 implemented power-up types are equally likely, one roll per unit.</summary>
        public static ChestContents Roll(ChestKind kind, int openIndex, IList<PowerUpDef> powerUpTypes)
        {
            var rng = new Rng(SeedOf(kind, openIndex));
            var result = new ChestContents();
            int[] percents = kind == ChestKind.Iron ? IronGearPercent : GoldenGearPercent;
            GearRarity low = LowestRarity(kind);
            for (int i = 0; i < percents.Length; i++)
            {
                if (rng.Next(100) >= percents[i]) continue;
                GearDef piece = GearDrops.RandomPiece(ref rng, (GearRarity)((int)low + i));
                if (piece != null) result.Gear.Add(piece.Id);
            }
            int units = PowerUpCount(kind);
            for (int i = 0; i < units; i++)
                result.PowerUps.Add(powerUpTypes[rng.Next(powerUpTypes.Count)].Id);
            int max = MaxCoins(kind);
            result.Coins = rng.Range(max / 10, max);
            return result;
        }
    }
}
