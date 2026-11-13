using System.Collections.Generic;
using FluffyClimbers.Simulation;

namespace FluffyClimbers.Economy
{
    /// <summary>Per-player hero stats (owner 2026-11-12, docs/44 section 11): the roll of every tier hero is dealt once, when the hero is first obtained, and kept in the save (heroRolls).</summary>
    public sealed partial class ProgressService
    {
        /// <summary>Safe defaults for the roll fields; a stored variant that is no longer inside the hero's pool (or a hero that is not rolled) is dropped and dealt again at first use.</summary>
        void EnsureRollFields()
        {
            if (Data.HeroRolls == null) Data.HeroRolls = new Dictionary<string, int>();
            if (Data.HeroRollNo == null) Data.HeroRollNo = new Dictionary<string, int>();
            if (Data.HeroRollCount < 0) Data.HeroRollCount = 0;
            var bad = new List<string>();
            foreach (KeyValuePair<string, int> kv in Data.HeroRolls)
                if (!HeroRolls.IsValidVariant(kv.Key, kv.Value)) bad.Add(kv.Key);
            foreach (string id in bad) { Data.HeroRolls.Remove(id); Data.HeroRollNo.Remove(id); }
        }

        /// <summary>
        /// Deals the roll of a hero that is obtained now, unless it already has one (a duplicate copy, or the hero came back after its copies were merged away, keeps the FIRST roll).
        /// Common heroes and heroes of inactive lines are not rolled. Changes the save data only: the caller saves (every obtaining path already does, once).
        /// </summary>
        internal void RollIfNew(string id)
        {
            if (!HeroRolls.IsRollable(id)) return;
            if (Data.HeroRolls == null) EnsureRollFields();
            if (Data.HeroRolls.ContainsKey(id)) return;
            BundleSaveData b = BundleState;
            EnsureBundleSeed(b);   // the per-account seed the card draws use
            int counter = Data.HeroRollCount++;
            Data.HeroRolls[id] = HeroRolls.Pick(b.Seed, id, counter);
            Data.HeroRollNo[id] = counter;
        }

        /// <summary>
        /// The variant index of a hero for this player (0 = the table row). Rolled heroes return their stored roll; an OWNED tier hero without one (an older save) is dealt its roll now and saved;
        /// a hero the player does not own yet shows the table row (nothing is stored until it is obtained).
        /// </summary>
        public int HeroVariant(string id)
        {
            if (!HeroRolls.IsRollable(id)) return 0;
            if (Data.HeroRolls == null) EnsureRollFields();
            int v;
            if (Data.HeroRolls.TryGetValue(id, out v)) return v;
            if (!OwnsCharacter(id)) return 0;
            RollIfNew(id);
            Commit();
            return Data.HeroRolls[id];
        }

        /// <summary>The stats this player's copy of the hero plays with (the rolled physics, the hero's own finds and run bonus). THE single source for the run, the hero sheet and the tests.</summary>
        public CharacterStats StatsOf(CharacterSpec c)
        {
            if (c == null) return CharacterStats.Default;
            return HeroRolls.StatsFor(c, HeroVariant(c.Id));
        }

        /// <summary>The numbers this player's copy of the hero shows on its sheet (null for a hero without shown stats).</summary>
        public ShownStats ShownOf(CharacterSpec c)
        {
            return c == null ? null : HeroRolls.ShownFor(c, HeroVariant(c.Id));
        }
    }
}
