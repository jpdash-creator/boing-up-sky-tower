using System.Collections.Generic;
using BoingUpSkyTower.Simulation;

namespace BoingUpSkyTower.Economy
{
    /// <summary>
    /// The owner's rule for one merge tier (docs/34 section 2b), as plain data: how many of the four stats get a boost, the percent band, the run-bonus
    /// ranges and the number of hero powers. Percent is over the Common hero of the line. Re-randomising a character later means calling
    /// <see cref="TierRules.Generate"/> with a new seed and pasting the result into <see cref="MergeTierDefs.Rows"/>.
    /// </summary>
    public sealed class TierRule
    {
        public CharacterRarity Rarity;
        /// <summary>How many of {Jump, Speed, Control, Wall Bounce} are boosted (the rest stay at the Common value).</summary>
        public int BoostedStats;
        /// <summary>Whole percent added to each boosted stat, inclusive.</summary>
        public int PctMin, PctMax;
        /// <summary>Run bonus coins ADDED on top of the Common hero's own run bonus: inclusive range, multiples of <see cref="TierRules.CoinStep"/> only.</summary>
        public int RunCoinsMin, RunCoinsMax;
        /// <summary>Run bonus gems added on top of the Common hero's own run bonus, inclusive.</summary>
        public int RunGemsMin, RunGemsMax;
        /// <summary>Hero powers of the tier (same as <see cref="CharacterDefs.PowerCount"/>; a test keeps them equal).</summary>
        public int PowerCount;
        /// <summary>Cooldown in seconds of every power (same as <see cref="CharacterDefs.CooldownSeconds"/>; a test keeps them equal).</summary>
        public int CooldownSeconds;
    }

    /// <summary>
    /// Rule tables for Uncommon, Rare, Elite (code enum: Epic) and Legendary, the region power pools, a pure validator of a <see cref="TierRow"/> against its rule
    /// and a seeded generator for future characters. No Unity types; compiled by the Simulation test project as well.
    /// </summary>
    public static class TierRules
    {
        public const int CoinStep = 10;
        /// <summary>The seed the five Uncommon rows were drawn with is BaseSeed + the line's index in <see cref="Lines"/> (docs/34 section 2b).</summary>
        public const int BaseSeed = 20261103;

        /// <summary>The five hero lines in region order (Meadow Springs, Mushroom Grove, Sunny Beach, Candy Canyon, Bamboo Temple).</summary>
        public static readonly string[] Lines = { "momo", "forestgirl", "beachotter", "candygirl", "monk" };

        public static readonly TierRule[] Rules =
        {
            null,   // Common: no tier rule
            new TierRule { Rarity = CharacterRarity.Uncommon, BoostedStats = 2, PctMin = 10, PctMax = 25, RunCoinsMin = 50, RunCoinsMax = 100, RunGemsMin = 1, RunGemsMax = 5, PowerCount = 1, CooldownSeconds = 30 },
            new TierRule { Rarity = CharacterRarity.Rare, BoostedStats = 3, PctMin = 25, PctMax = 50, RunCoinsMin = 200, RunCoinsMax = 500, RunGemsMin = 5, RunGemsMax = 10, PowerCount = 2, CooldownSeconds = 22 },
            new TierRule { Rarity = CharacterRarity.Epic, BoostedStats = 3, PctMin = 50, PctMax = 75, RunCoinsMin = 500, RunCoinsMax = 1000, RunGemsMin = 5, RunGemsMax = 10, PowerCount = 3, CooldownSeconds = 15 },
            new TierRule { Rarity = CharacterRarity.Legendary, BoostedStats = 4, PctMin = 75, PctMax = 100, RunCoinsMin = 1000, RunCoinsMax = 2000, RunGemsMin = 5, RunGemsMax = 10, PowerCount = 4, CooldownSeconds = 10 },
        };

        /// <summary>
        /// Hero power pools, one per line, themed on the hero's region and ordered: tier n uses the first n entries (Uncommon 1, Rare 2, Elite 3, Legendary 4), so a
        /// higher tier keeps the earlier powers and adds the next one. Only power-ups that exist in the simulation (PowerUpCatalog.Implemented) appear.
        /// </summary>
        public static readonly Dictionary<string, PowerUp[]> Pools = new Dictionary<string, PowerUp[]>
        {
            // Meadow Springs (R1): bouncy hops and easy pickups
            { "momo", new[] { PowerUp.DoubleJump, PowerUp.Magnet, PowerUp.Shield, PowerUp.Rocket } },
            // Mushroom Grove (R2): clinging to trunks and walls, safe footing
            { "forestgirl", new[] { PowerUp.WallStick, PowerUp.Shield, PowerUp.Magnet, PowerUp.SlowTime } },
            // Sunny Beach (R3): sea breeze lift and floating
            { "beachotter", new[] { PowerUp.Magnet, PowerUp.DoubleJump, PowerUp.Flight, PowerUp.Shield } },
            // Candy Canyon (R4): sticky sweets, springy jumps, sugar rush
            { "candygirl", new[] { PowerUp.DoubleJump, PowerUp.SlowTime, PowerUp.Magnet, PowerUp.Rocket } },
            // Bamboo Temple (R5): calm focus, wall mastery, guardian
            { "monk", new[] { PowerUp.WallStick, PowerUp.SlowTime, PowerUp.Shield, PowerUp.Flight } },
        };

        /// <summary>The rule of a tier, or null for Common.</summary>
        public static TierRule RuleFor(CharacterRarity r)
        {
            int i = (int)r;
            return i >= 0 && i < Rules.Length ? Rules[i] : null;
        }

        /// <summary>The powers of a tier hero: the first PowerCount entries of the line's pool (empty when the line or tier is unknown).</summary>
        public static PowerUp[] PowersFor(string lineId, CharacterRarity r)
        {
            TierRule rule = RuleFor(r);
            PowerUp[] pool;
            if (rule == null || lineId == null || !Pools.TryGetValue(lineId, out pool)) return new PowerUp[0];
            int n = rule.PowerCount < pool.Length ? rule.PowerCount : pool.Length;
            var list = new PowerUp[n];
            for (int i = 0; i < n; i++) list[i] = pool[i];
            return list;
        }

        /// <summary>Number of boosted stats in a row (percent above zero).</summary>
        public static int BoostedCount(TierRow r)
        {
            return (r.JumpPct > 0 ? 1 : 0) + (r.SpeedPct > 0 ? 1 : 0) + (r.ControlPct > 0 ? 1 : 0) + (r.WallPct > 0 ? 1 : 0);
        }

        /// <summary>Null when the row obeys its tier rule (stat count, percent band, run bonus ranges and steps), else a short reason. Pure; used by the tests.</summary>
        public static string Validate(TierRow r)
        {
            if (r == null) return "no row";
            TierRule rule = RuleFor(r.Rarity);
            if (rule == null) return r.Id + ": " + r.Rarity + " has no tier rule";
            int[] pcts = { r.JumpPct, r.SpeedPct, r.ControlPct, r.WallPct };
            foreach (int p in pcts)
                if (p != 0 && (p < rule.PctMin || p > rule.PctMax)) return r.Id + ": percent " + p + " outside " + rule.PctMin + ".." + rule.PctMax;
            if (BoostedCount(r) != rule.BoostedStats) return r.Id + ": " + BoostedCount(r) + " boosted stats, rule says " + rule.BoostedStats;
            if (r.RunCoinsAdd < rule.RunCoinsMin || r.RunCoinsAdd > rule.RunCoinsMax || r.RunCoinsAdd % CoinStep != 0) return r.Id + ": run coins " + r.RunCoinsAdd + " outside " + rule.RunCoinsMin + ".." + rule.RunCoinsMax + " in steps of " + CoinStep;
            if (r.RunGemsAdd < rule.RunGemsMin || r.RunGemsAdd > rule.RunGemsMax) return r.Id + ": run gems " + r.RunGemsAdd + " outside " + rule.RunGemsMin + ".." + rule.RunGemsMax;
            return null;
        }

        /// <summary>Null when <paramref name="powers"/> is exactly the pool prefix of the line for that tier (right count, right order, each power in the pool), else a reason.</summary>
        public static string ValidatePowers(string lineId, CharacterRarity r, PowerUp[] powers)
        {
            PowerUp[] want = PowersFor(lineId, r);
            if (want.Length == 0 && RuleFor(r) != null) return lineId + ": no power pool";
            if (powers == null || powers.Length != want.Length) return lineId + " " + r + ": " + (powers == null ? 0 : powers.Length) + " powers, rule says " + want.Length;
            for (int i = 0; i < want.Length; i++)
                if (powers[i] != want[i]) return lineId + " " + r + ": power " + i + " is " + powers[i] + ", pool says " + want[i];
            return null;
        }

        /// <summary>
        /// Draws a new row for a character from a seed (deterministic, no System.Random): picks the boosted stats, the percent of each, the run coins and gems,
        /// all inside the rule of <paramref name="rarity"/>. Paste the printed values into <see cref="MergeTierDefs.Rows"/> as constants; nothing is random at run time.
        /// </summary>
        public static TierRow Generate(string lineId, string id, string name, string artId, CharacterRarity rarity, int seed)
        {
            TierRule rule = RuleFor(rarity);
            if (rule == null) return null;
            var rng = new Rng(seed);
            int[] order = { 0, 1, 2, 3 };
            for (int i = 0; i < rule.BoostedStats; i++)
            {
                int j = i + rng.Range(0, order.Length - 1 - i);
                int t = order[i]; order[i] = order[j]; order[j] = t;
            }
            var chosen = new bool[4];
            for (int i = 0; i < rule.BoostedStats; i++) chosen[order[i]] = true;
            var row = new TierRow { LineId = lineId, Id = id, Name = name, ArtId = artId, Rarity = rarity };
            for (int s = 0; s < 4; s++)
            {
                if (!chosen[s]) continue;
                int pct = rng.Range(rule.PctMin, rule.PctMax);
                if (s == 0) row.JumpPct = pct; else if (s == 1) row.SpeedPct = pct; else if (s == 2) row.ControlPct = pct; else row.WallPct = pct;
            }
            row.RunCoinsAdd = rng.Range(rule.RunCoinsMin / CoinStep, rule.RunCoinsMax / CoinStep) * CoinStep;
            row.RunGemsAdd = rng.Range(rule.RunGemsMin, rule.RunGemsMax);
            return row;
        }

        /// <summary>Tiny deterministic generator (SplitMix32 style) so a seed gives the same numbers on every runtime.</summary>
        sealed class Rng
        {
            uint state;
            public Rng(int seed) { state = (uint)seed; }
            uint Next()
            {
                unchecked
                {
                    state += 0x9E3779B9u;
                    uint z = state;
                    z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
                    z = (z ^ (z >> 13)) * 0xC2B2AE35u;
                    return z ^ (z >> 16);
                }
            }
            /// <summary>Uniform whole number in lo..hi inclusive.</summary>
            public int Range(int lo, int hi) { return lo + (int)(Next() % (uint)(hi - lo + 1)); }
        }
    }
}
