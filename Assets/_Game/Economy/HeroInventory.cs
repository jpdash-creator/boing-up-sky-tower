using System;
using System.Collections.Generic;

namespace BoingUpSkyTower.Economy
{
    /// <summary>
    /// The HERO INVENTORY (docs/36): the heroes one player (account or guest) owns, with a copy count per character id. It is a view over the save
    /// (<see cref="SaveData.Copies"/>, JSON "copies": no format change) and is what the Heroes screen lists. Owned = copies of 1 or more.
    /// Get it with <c>progress.HeroInventory</c>. The master list of all characters is the hidden <see cref="CharacterInventory"/>.
    /// Every changing call saves once.
    /// </summary>
    public sealed class HeroInventory
    {
        readonly ProgressService p;

        internal HeroInventory(ProgressService progress) { p = progress; }

        /// <summary>Copies of the character the player holds (0 for an unknown or not owned one). An owned character that was never counted is 1 copy.</summary>
        public int Copies(string id)
        {
            CharacterSpec c = CharacterInventory.Get(id);
            if (c == null) return 0;
            int n;
            if (p.Data.Copies.TryGetValue(id, out n)) return n > 0 ? n : 0;
            return p.OwnedWithoutCopyCount(c) ? 1 : 0;
        }

        /// <summary>True when the player has the character (copies of 1 or more; SYSTEM_TEST shows every hero as owned).</summary>
        public bool Owns(string id) { return p.OwnsCharacter(id); }

        /// <summary>The number to show on a card: never below 1, because a card in the list means the hero is available.</summary>
        public int BadgeCount(string id) { return Math.Max(1, Copies(id)); }

        /// <summary>Adds copies (rewards, bundles, debug). Returns the new count; an unknown id or a count below 1 changes nothing. Adding a character the player does not have yet adds its card to the list.</summary>
        public int AddCopies(string id, int count)
        {
            if (!CharacterInventory.Exists(id) || count < 1) return Copies(id);
            int now = Math.Min(MergeRules.MaxCopies, Copies(id) + count);
            p.Data.Copies[id] = now;
            p.SaveNow();
            return now;
        }

        /// <summary>The heroes the player may see, ordered by hero line then tier. A region hero that is owned but whose reveal is pending is not listed yet.</summary>
        public List<CharacterSpec> Owned()
        {
            var list = new List<CharacterSpec>(8);
            foreach (CharacterSpec c in CharacterInventory.All)
                if (p.IsCharacterVisible(c.Id)) list.Add(c);
            return list;
        }

        /// <summary>Number of different heroes in the list.</summary>
        public int OwnedCount { get { return Owned().Count; } }
    }
}
