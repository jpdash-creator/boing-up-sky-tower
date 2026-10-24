using System.Collections.Generic;
using BoingUpSkyTower.Simulation;

namespace BoingUpSkyTower.Economy
{
    public enum CharacterRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }

    /// <summary>
    /// One character (docs/21). Common characters belong to a region and are owned when it unlocks, and have no power. Uncommon and above come only from chests and the
    /// shop (gems) and may carry powers: free use ready at the start of a run, then a cooldown that gets shorter with rarity.
    /// </summary>
    public sealed class CharacterSpec
    {
        public string Id;
        public string Name;
        public CharacterRarity Rarity;
        /// <summary>Region index whose unlock gives this Common character; 0 for the others.</summary>
        public int Region;
        public PowerUp[] Powers = new PowerUp[0];
    }

    public static class CharacterDefs
    {
        public static readonly string[] RarityNames = { "Common", "Uncommon", "Rare", "Epic", "Legendary" };
        /// <summary>Cooldown in seconds of every power of a character, by rarity (Common has no power).</summary>
        public static readonly int[] CooldownSeconds = { 0, 30, 22, 15, 10 };
        /// <summary>How many powers a character of this rarity has.</summary>
        public static readonly int[] PowerCount = { 0, 1, 1, 2, 3 };
        /// <summary>Coins a duplicate from a chest turns into.</summary>
        public static readonly int[] DuplicateCoins = { 100, 200, 500, 1000, 2000 };

        public static readonly CharacterSpec Momo = new CharacterSpec { Id = "momo", Name = "Momo", Rarity = CharacterRarity.Common, Region = 1 };
        public static readonly CharacterSpec Sporeling = new CharacterSpec { Id = "sporeling", Name = "Sporeling", Rarity = CharacterRarity.Common, Region = 2 };

        /// <summary>Every character that exists (has art). More are added here as they are made.</summary>
        public static readonly CharacterSpec[] All = { Momo, Sporeling };

        public static CharacterSpec Get(string id)
        {
            if (id == null) return null;
            foreach (CharacterSpec c in All) if (c.Id == id) return c;
            return null;
        }

        public static List<CharacterSpec> OfRarity(CharacterRarity r)
        {
            var list = new List<CharacterSpec>(4);
            foreach (CharacterSpec c in All) if (c.Rarity == r) list.Add(c);
            return list;
        }
    }
}
