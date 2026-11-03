using System.Collections.Generic;

namespace BoingUpSkyTower.Economy
{
    /// <summary>
    /// The CHARACTER INVENTORY (docs/36): the hidden, static, read-only master list of every character that exists in the game (the Common heroes and
    /// every merge tier), with id, hero line, tier, region, rarity and unlock rules. It is the source for chest and bundle pools and for the rules.
    /// It holds nothing of a player and is NEVER shown to players as a list: the Heroes screen lists the player's <see cref="HeroInventory"/> instead.
    /// Pure data over <see cref="CharacterDefs"/> (no Unity, no save), so the dotnet tests compile it.
    /// </summary>
    public static class CharacterInventory
    {
        static readonly CharacterSpec[] ordered = BuildOrdered();

        /// <summary>Every character, ordered by hero line (list order of the Common heroes) and then tier (Common first, then Uncommon ...). A copy: callers cannot change the master list.</summary>
        public static CharacterSpec[] All { get { return (CharacterSpec[])ordered.Clone(); } }

        /// <summary>How many characters exist.</summary>
        public static int Count { get { return ordered.Length; } }

        /// <summary>The character with this id, or null.</summary>
        public static CharacterSpec Get(string id) { return CharacterDefs.Get(id); }

        /// <summary>True when the id names an existing character.</summary>
        public static bool Exists(string id) { return CharacterDefs.Get(id) != null; }

        /// <summary>Position of the id in the hero-then-tier order (-1 for an unknown id). Used to sort a player's heroes.</summary>
        public static int OrderOf(string id)
        {
            for (int i = 0; i < ordered.Length; i++) if (ordered[i].Id == id) return i;
            return -1;
        }

        /// <summary>The tier index of a character in its line: 0 for the Common hero, then 1 for the next tier, in rarity order.</summary>
        public static int TierOf(string id)
        {
            CharacterSpec c = CharacterDefs.Get(id);
            if (c == null) return -1;
            int tier = 0;
            foreach (CharacterSpec o in ordered)
                if (o.LineId == c.LineId && o.Rarity < c.Rarity) tier++;
            return tier;
        }

        static CharacterSpec[] BuildOrdered()
        {
            var list = new List<CharacterSpec>(CharacterDefs.All.Length);
            foreach (CharacterSpec baseHero in CharacterDefs.All)
            {
                if (baseHero.IsMergeTier) continue;
                list.Add(baseHero);
                var tiers = new List<CharacterSpec>(4);
                foreach (CharacterSpec t in CharacterDefs.All) if (t.IsMergeTier && t.LineId == baseHero.Id) tiers.Add(t);
                tiers.Sort((a, b) => a.Rarity.CompareTo(b.Rarity));
                list.AddRange(tiers);
            }
            return list.ToArray();
        }
    }
}
