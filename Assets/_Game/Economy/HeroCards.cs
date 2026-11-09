using System;
using System.Collections.Generic;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// One CARD of the Heroes and Merge screens (docs/34 section 10): exactly one owned copy of a hero. The save keeps a count per hero, the screens show one card per
    /// copy with the same artwork and NO number. <see cref="Copy"/> is the 0-based position among the copies of that hero (card 0 is the "first" card of its type).
    /// </summary>
    public sealed class HeroCard
    {
        public CharacterSpec Spec;
        public int Copy;
        public string Id { get { return Spec.Id; } }
        /// <summary>True for the first card of the hero type: the one that carries the chosen tick on the Heroes screen.</summary>
        public bool IsFirstOfType { get { return Copy == 0; } }
        public bool Same(HeroCard o) { return o != null && o.Spec != null && Spec != null && o.Spec.Id == Spec.Id && o.Copy == Copy; }
        public string Key { get { return Spec.Id + "#" + Copy; } }
    }

    /// <summary>
    /// The lists of hero cards, pure and unit-testable (no Unity, no save): <paramref name="owned"/> is the heroes the player may see in list order and
    /// <paramref name="copies"/> reads how many copies of an id the player holds. Counts never reach the UI: every list is a list of cards.
    /// </summary>
    public static class HeroCards
    {
        /// <summary>
        /// The main Heroes list: one card per owned copy, grouped by hero in the given order (2 Common Momo = 2 cards, same artwork). A visible hero always has at least
        /// one card (Momo stays selectable even with no copy left). Capped at <see cref="MergeRules.MaxCopies"/> cards per hero.
        /// </summary>
        public static List<HeroCard> Owned(IEnumerable<CharacterSpec> owned, Func<string, int> copies)
        {
            var list = new List<HeroCard>(16);
            if (owned == null) return list;
            foreach (CharacterSpec s in owned)
            {
                if (s == null) continue;
                int n = Math.Min(MergeRules.MaxCopies, Math.Max(1, copies != null ? copies(s.Id) : 1));
                for (int i = 0; i < n; i++) list.Add(new HeroCard { Spec = s, Copy = i });
            }
            return list;
        }

        /// <summary>
        /// The Merge list: only hero types that CAN merge (a next tier exists and the player holds at least <see cref="MergeRules.CopiesNeeded"/> real copies), and then ALL
        /// the copies of each as individual cards. Types with fewer copies and top-tier heroes are not listed at all.
        /// </summary>
        public static List<HeroCard> Mergeable(IEnumerable<CharacterSpec> owned, Func<string, int> copies)
        {
            var list = new List<HeroCard>(12);
            if (owned == null || copies == null) return list;
            foreach (CharacterSpec s in owned)
            {
                if (s == null || MergeRules.NextTier(s) == null) continue;
                int n = Math.Min(MergeRules.MaxCopies, copies(s.Id));
                if (n < MergeRules.CopiesNeeded) continue;
                for (int i = 0; i < n; i++) list.Add(new HeroCard { Spec = s, Copy = i });
            }
            return list;
        }

        /// <summary>True when the selection is exactly <see cref="MergeRules.CopiesNeeded"/> cards of ONE hero type that has a next tier: the only selection that enables Merge.</summary>
        public static bool IsMergeSet(IList<HeroCard> picked)
        {
            if (picked == null || picked.Count != MergeRules.CopiesNeeded) return false;
            string id = picked[0].Id;
            for (int i = 1; i < picked.Count; i++) if (picked[i].Id != id) return false;
            return MergeRules.NextTier(picked[0].Spec) != null;
        }

        /// <summary>True when the selection holds cards of two or more different hero types (a wrong mix).</summary>
        public static bool IsMixed(IList<HeroCard> picked)
        {
            if (picked == null || picked.Count < 2) return false;
            for (int i = 1; i < picked.Count; i++) if (picked[i].Id != picked[0].Id) return true;
            return false;
        }

        /// <summary>Index of the card in the list (same hero and copy), or -1.</summary>
        public static int IndexOf(IList<HeroCard> list, HeroCard card)
        {
            if (list == null || card == null) return -1;
            for (int i = 0; i < list.Count; i++) if (list[i].Same(card)) return i;
            return -1;
        }

        /// <summary>
        /// Toggles a card in the selection: a selected card is removed, an unselected one is added while fewer than <see cref="MergeRules.CopiesNeeded"/> are picked
        /// (a full selection ignores more taps: the player removes one first). Returns true when the selection changed.
        /// </summary>
        public static bool Toggle(List<HeroCard> picked, HeroCard card)
        {
            if (picked == null || card == null) return false;
            int at = IndexOf(picked, card);
            if (at >= 0) { picked.RemoveAt(at); return true; }
            if (picked.Count >= MergeRules.CopiesNeeded) return false;
            picked.Add(card);
            return true;
        }

        /// <summary>Keeps only the picked cards that are still in the list (after a merge the consumed copies are gone). Returns the cleaned selection.</summary>
        public static List<HeroCard> Prune(IList<HeroCard> picked, IList<HeroCard> list)
        {
            var keep = new List<HeroCard>(3);
            if (picked == null) return keep;
            foreach (HeroCard c in picked) { int i = IndexOf(list, c); if (i >= 0) keep.Add(list[i]); }
            return keep;
        }
    }
}
