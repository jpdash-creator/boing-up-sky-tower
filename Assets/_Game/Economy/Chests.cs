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
        /// <summary>Character ids that dropped. Always empty while <see cref="ChestRolls.CharactersEnabled"/> is false.</summary>
        public readonly List<string> Characters = new List<string>();
        /// <summary>Per character above: true when the player already had it (it turned into gems).</summary>
        public readonly List<bool> Duplicate = new List<bool>();
        /// <summary>Power-up ids, one entry per unit (1 in an Iron chest, 3 in a Golden one; the same type may repeat).</summary>
        public readonly List<string> PowerUps = new List<string>();
        /// <summary>All coins of this chest.</summary>
        public int Coins;
        /// <summary>Gems refunded for duplicate characters (25 percent of the character's gem price).</summary>
        public int Gems;
    }

    /// <summary>
    /// Treasure chests, opened with gems only (docs/22 point 3). Iron: 1 power-up and 400 to 600 coins. Golden: 3 power-ups and 1,000 to 1,400 coins.
    /// Each power-up is one of the implemented types with equal chance. Open 10 costs 10 times the single price (no discount).
    /// Characters are switched off behind <see cref="CharactersEnabled"/> (the roll code stays for when they launch).
    /// </summary>
    public static class ChestRolls
    {
        /// <summary>Master switch for characters in chests. OFF until characters launch (docs/22 owner answer 1).</summary>
        public const bool CharactersEnabled = false;
        /// <summary>A duplicate character refunds this share of its gem price, paid in gems.</summary>
        public const int DuplicateRefundPercent = 25;

        public const int IronPowerUps = 1, GoldenPowerUps = 3;
        public const int IronMinCoins = 400, IronMaxCoins = 600, GoldenMinCoins = 1000, GoldenMaxCoins = 1400;
        public static readonly int[] IronPercent = { 100, 50, 25 };        // Common, Uncommon, Rare (used only when characters are enabled)
        public static readonly int[] GoldenPercent = { 100, 50, 25 };      // Rare, Epic, Legendary (used only when characters are enabled)
        public const int IronMaxCharacters = 1, GoldenMaxCharacters = 3;
        public const int IronPrice = 100, IronPriceTen = 1000, GoldenPrice = 300, GoldenPriceTen = 3000;

        /// <summary>Gems a duplicate character turns into.</summary>
        public static int DuplicateRefundGems(int characterGemPrice)
        {
            return characterGemPrice <= 0 ? 0 : characterGemPrice * DuplicateRefundPercent / 100;
        }

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
            if (CharactersEnabled)
            {
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
            int loadedVersion = Data.Version;
            if (Data.Characters == null) Data.Characters = new List<string>();
            Data.Characters.RemoveAll(id => CharacterDefs.Get(id) == null);
            if (Data.HeroesRevealed == null) Data.HeroesRevealed = new List<string>();
            Data.HeroesRevealed.RemoveAll(id => CharacterDefs.Get(id) == null);
            if (Data.ChestOpens < 0) Data.ChestOpens = 0;
            EnsureCopyFields(loadedVersion);   // ProgressService.Merge.cs
            if (Data.Version < SaveData.CurrentVersion) Data.Version = SaveData.CurrentVersion;
        }

        /// <summary>True when the player has the character: a Common one is owned when its region is unlocked; any other came from a chest or the shop.</summary>
        public bool OwnsCharacter(string id)
        {
            CharacterSpec c = CharacterDefs.Get(id);
            if (c == null) return false;
            if (TestAllCharacters) return true;
            int counted;
            if (Data.Copies != null && Data.Copies.TryGetValue(id, out counted)) return counted > 0;   // docs/34: once a character is counted, its copies decide
            return OwnedWithoutCopyCount(c);
        }

        /// <summary>True when the hero's reveal is done (or it never needs one: Momo and every non-hero character).</summary>
        public bool IsHeroRevealed(string id)
        {
            CharacterSpec c = CharacterDefs.Get(id);
            return c != null && (!c.NeedsReveal || Data.HeroesRevealed.Contains(id));
        }

        /// <summary>True when the character may be shown and chosen: owned, and for a region hero also revealed. SYSTEM_TEST shows everything.</summary>
        public bool IsCharacterVisible(string id)
        {
            if (!OwnsCharacter(id)) return false;
            return TestAllCharacters || IsHeroRevealed(id);
        }

        /// <summary>Heroes whose region is unlocked but whose reveal pop-up was not confirmed yet, in list order (shown one after the other on the next home or map screen).</summary>
        public List<string> PendingHeroReveals()
        {
            var list = new List<string>(2);
            if (TestAllCharacters) return list;
            foreach (CharacterSpec c in CharacterDefs.All)
                if (c.NeedsReveal && !Data.HeroesRevealed.Contains(c.Id) && OwnsCharacter(c.Id)) list.Add(c.Id);
            return list;
        }

        /// <summary>Records a confirmed reveal and saves at once, so the pop-up shows once. False for an unknown, not needing or already revealed hero.</summary>
        public bool MarkHeroRevealed(string id)
        {
            CharacterSpec c = CharacterDefs.Get(id);
            if (c == null || !c.NeedsReveal || Data.HeroesRevealed.Contains(id) || !OwnsCharacter(id)) return false;
            Data.HeroesRevealed.Add(id);
            Commit();
            return true;
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
        /// A character the player already has turns into gems (25 percent of its gem price). Nothing changes unless the result is Ok. <paramref name="contents"/> has one entry per chest.
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
                    if (dup) c.Gems += ChestRolls.DuplicateRefundGems(CharacterDefs.Get(cid).GemPrice);
                    else Data.Characters.Add(cid);
                }
                foreach (string pid in c.PowerUps)
                {
                    int have = PowerUpCount(pid);
                    Data.Inventory.Powerups[pid] = SaturatingAdd(have, 1);   // no stock cap: a chest always gives its power-ups
                }
                Data.Coins = SaturatingAdd(Data.Coins, c.Coins);
                if (c.Gems > 0) Data.Gems = SaturatingAdd(Data.Gems, c.Gems);
                contents.Add(c);
            }
            Commit();
            return ChestOpenResult.Ok;
        }
    }
}
