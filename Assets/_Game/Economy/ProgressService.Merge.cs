using System;
using System.Collections.Generic;

namespace FluffyClimbers.Economy
{
    /// <summary>Character copies and merging (docs/34). Every public change saves once.</summary>
    public sealed partial class ProgressService
    {
        /// <summary>The ownership rules that existed before copies: bought or received characters, and the Common hero of an unlocked region.</summary>
        HeroInventory heroInventory;

        /// <summary>The player's hero inventory (docs/36): the heroes this account or guest owns, with copy counts. The Heroes screen lists it.</summary>
        public HeroInventory HeroInventory { get { return heroInventory ?? (heroInventory = new HeroInventory(this)); } }

        /// <summary>Saves once (used by <see cref="HeroInventory"/>).</summary>
        internal void SaveNow() { Commit(); }

        internal bool OwnedWithoutCopyCount(CharacterSpec c)
        {
            if (c == null) return false;
            if (Data.Characters.Contains(c.Id)) return true;
            if (c.Rarity != CharacterRarity.Common || c.Region <= 0 || !c.RegionStarter) return false;   // only the region's starter hero comes with the region; the other Commons need a copy
            if (c.Region == 1) return true;
            FluffyClimbers.Content.RegionDef r = repo.TryGetRegionByIndex(c.Region);
            return r != null && IsRegionUnlocked(r);
        }

        /// <summary>
        /// Safe defaults for the copies table and the one-time migration: a save older than version 3 gets an explicit 1 for every character it owns
        /// (Momo, the unlocked region heroes, anything from a chest or the shop). Unknown ids and negative counts are dropped.
        /// </summary>
        void EnsureCopyFields(int loadedVersion)
        {
            if (Data.Copies == null) Data.Copies = new Dictionary<string, int>();
            var bad = new List<string>();
            foreach (KeyValuePair<string, int> kv in Data.Copies)
                if (CharacterDefs.Get(kv.Key) == null || kv.Value < 0) bad.Add(kv.Key);
            foreach (string id in bad) Data.Copies.Remove(id);
            if (loadedVersion >= 3) return;
            foreach (CharacterSpec c in CharacterDefs.All)
                if (!Data.Copies.ContainsKey(c.Id) && OwnedWithoutCopyCount(c)) Data.Copies[c.Id] = 1;
        }

        /// <summary>How many copies of the character the player holds (0 for an unknown or not owned one). An owned character that was never counted is 1 copy.</summary>
        public int Copies(string id) { return HeroInventory.Copies(id); }

        /// <summary>Adds copies (rewards later, debug now). Returns the new count; an unknown id or a count below 1 changes nothing.</summary>
        public int AddCopies(string id, int count) { return HeroInventory.AddCopies(id, count); }

        /// <summary>The next tier of the character, or null when it has none yet.</summary>
        public CharacterSpec NextTierOf(string id) { return MergeRules.NextTier(CharacterDefs.Get(id)); }

        /// <summary>True when a merge of this character would succeed now (3 or more copies and a next tier).</summary>
        public bool CanMerge(string id)
        {
            return MergeRules.Check(Copies(id), NextTierOf(id) != null) == MergeResult.Ok;
        }

        /// <summary>
        /// Merges 3 copies of the character into 1 copy of its next tier and saves at once. Nothing changes unless the result is Ok.
        /// The lower tier stays owned only while copies are left.
        /// </summary>
        public MergeOutcome TryMerge(string id)
        {
            CharacterSpec from = CharacterDefs.Get(id);
            if (from == null) return new MergeOutcome { Result = MergeResult.Unknown, FromId = id };
            CharacterSpec to = MergeRules.NextTier(from);
            string toId = to != null ? to.Id : null;
            var work = new Dictionary<string, int>(2) { { from.Id, Copies(from.Id) } };
            if (toId != null) work[toId] = Copies(toId);
            MergeOutcome o = MergeRules.Apply(work, from.Id, toId);
            if (o.Result != MergeResult.Ok) return o;
            Data.Copies[from.Id] = o.FromCopiesLeft;
            Data.Copies[toId] = o.ToCopies;
            RollIfNew(toId);   // the merge result is a new hero: its stats are dealt now (a hero that already has a roll keeps it)
            Commit();
            return o;
        }
    }
}
