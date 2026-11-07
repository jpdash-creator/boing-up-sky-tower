using System;
using System.Collections.Generic;
using BoingUpSkyTower.Simulation;

namespace BoingUpSkyTower.Economy
{
    public enum MergeResult
    {
        Ok,
        NotEnoughCopies,
        /// <summary>The character has no next tier defined (top of its line, or the tier is not made yet).</summary>
        MaxTier,
        Unknown
    }

    /// <summary>What a merge did (docs/34). FromId and ToId are character ids; the counts are the copies AFTER the merge.</summary>
    public sealed class MergeOutcome
    {
        public MergeResult Result;
        public string FromId, ToId;
        public int FromCopiesLeft, ToCopies;
    }

    /// <summary>
    /// The merge rules (docs/34), pure and unit-testable: 3 copies of the same character (same hero, same tier) become 1 copy of the next tier of that hero,
    /// which is its own character with its own id and name. Copies are counted per character id, and the tier hero has its own id, so a count is per (hero, tier).
    /// </summary>
    public static class MergeRules
    {
        /// <summary>Copies of one character that one merge consumes.</summary>
        public const int CopiesNeeded = 3;
        /// <summary>A stack never holds more than this (keeps the save and the screen sane).</summary>
        public const int MaxCopies = 99;

        /// <summary>The same hero one tier up (rarity + 1), or null when that tier is not defined (top of the line).</summary>
        public static CharacterSpec NextTier(CharacterSpec s)
        {
            if (s == null) return null;
            int next = (int)s.Rarity + 1;
            foreach (CharacterSpec c in CharacterDefs.All)
                if ((int)c.Rarity == next && c.LineId == s.LineId) return c;
            return null;
        }

        /// <summary>The tier below, or null for a Common (used to show where a character came from).</summary>
        public static CharacterSpec PreviousTier(CharacterSpec s)
        {
            if (s == null) return null;
            int prev = (int)s.Rarity - 1;
            foreach (CharacterSpec c in CharacterDefs.All)
                if ((int)c.Rarity == prev && c.LineId == s.LineId) return c;
            return null;
        }

        /// <summary>Whether a stack of <paramref name="copies"/> could merge into a defined next tier.</summary>
        public static MergeResult Check(int copies, bool hasNextTier)
        {
            if (!hasNextTier) return MergeResult.MaxTier;
            return copies >= CopiesNeeded ? MergeResult.Ok : MergeResult.NotEnoughCopies;
        }

        /// <summary>
        /// Applies one merge to a copies table (missing = 0). Nothing changes unless the result is Ok; on Ok the source loses
        /// <see cref="CopiesNeeded"/> copies and the target gains one (capped at <see cref="MaxCopies"/>).
        /// </summary>
        public static MergeOutcome Apply(IDictionary<string, int> counts, string fromId, string toId)
        {
            var o = new MergeOutcome { FromId = fromId, ToId = toId };
            if (counts == null || string.IsNullOrEmpty(fromId)) { o.Result = MergeResult.Unknown; return o; }
            int have = Get(counts, fromId);
            o.FromCopiesLeft = have;
            o.ToCopies = string.IsNullOrEmpty(toId) ? 0 : Get(counts, toId);
            o.Result = Check(have, !string.IsNullOrEmpty(toId));
            if (o.Result != MergeResult.Ok) return o;
            o.FromCopiesLeft = have - CopiesNeeded;
            o.ToCopies = Math.Min(MaxCopies, o.ToCopies + 1);
            counts[fromId] = o.FromCopiesLeft;
            counts[toId] = o.ToCopies;
            return o;
        }

        static int Get(IDictionary<string, int> counts, string id)
        {
            int n;
            return counts.TryGetValue(id, out n) && n > 0 ? n : 0;
        }

        // ---- stat gains shown on the Merge screen ----

        /// <summary>One line per stat that gets better, in the hero sheet's words and units: Jump, Speed, Control, Wall Bounce in percent, Coins, Gems and the two run bonuses as flat numbers.</summary>
        public static List<string> StatGainLines(CharacterStats from, CharacterStats to)
        {
            var lines = new List<string>(6);
            if (from == null || to == null) return lines;
            AddPercent(lines, "Jump", (double)to.LaunchTenths * to.LaunchTenths, (double)from.LaunchTenths * from.LaunchTenths);   // the card shows the height, which grows with the square of the launch
            AddPercent(lines, "Speed", to.MaxSpeed, from.MaxSpeed);
            AddPercent(lines, "Control", to.Control, from.Control);
            AddPercent(lines, "Wall Bounce", (double)to.MaxSpeed * to.WallBoostHundredths, (double)from.MaxSpeed * from.WallBoostHundredths);   // the card shows speed x wall share
            if (to.CoinFind > from.CoinFind) lines.Add("Coins +" + (to.CoinFind - from.CoinFind));
            if (to.GemFind > from.GemFind) lines.Add("Gems +" + (to.GemFind - from.GemFind));
            if (to.RunBonusCoins > from.RunBonusCoins) lines.Add("Run coins +" + (to.RunBonusCoins - from.RunBonusCoins));
            if (to.RunBonusGems > from.RunBonusGems) lines.Add("Run gems +" + (to.RunBonusGems - from.RunBonusGems));
            return lines;
        }

        /// <summary>
        /// Gain lines from the SHOWN stats (hero sheet numbers of two heroes): one line per stat that rose, as the exact whole percent between the two printed numbers
        /// ("Jump +17%"), then the two run bonuses as flat numbers. The percent is read between the two printed numbers.
        /// </summary>
        public static List<string> ShownGainLines(ShownStats from, ShownStats to) { return TierRules.GainLines(from, to); }

        static void AddPercent(List<string> lines, string label, double now, double before)
        {
            if (before <= 0.0 || now <= before) return;
            int pct = (int)Math.Round((now / before - 1.0) * 100.0, MidpointRounding.AwayFromZero);
            if (pct > 0) lines.Add(label + " +" + pct + "%");
        }
    }
}
