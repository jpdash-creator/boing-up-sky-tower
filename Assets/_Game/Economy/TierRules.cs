using System.Collections.Generic;
using BoingUpSkyTower.Simulation;

namespace BoingUpSkyTower.Economy
{
    /// <summary>
    /// The owner's rule for one merge tier (docs/34 section 2b), as plain data: how many of the four stats get a boost, the percent band, the run-bonus
    /// ranges and the number of hero powers. The percent is a CHAIN (owner 2026-11-03): each tier is +10 to +25 percent over the SHOWN stats of the previous tier of the same hero (Uncommon over Common, Rare over Uncommon, Elite over Rare, Legendary over Elite). Re-randomising a character later means calling
    /// <see cref="TierRules.Generate"/> with a new seed and pasting the result into <see cref="MergeTierDefs.Rows"/>.
    /// </summary>
    public sealed class TierRule
    {
        public CharacterRarity Rarity;
        /// <summary>How many of {Jump, Speed, Control, Wall Bounce} are boosted (the rest stay at the Common value).</summary>
        public int BoostedStats;
        /// <summary>Whole percent added to each boosted stat over the PREVIOUS tier's shown value, inclusive (10..25 for every tier).</summary>
        public int PctMin, PctMax;
        /// <summary>True: the percent is over the previous tier of the same hero (always true; Uncommon's previous tier is the Common hero).</summary>
        public bool OverPreviousTier;
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
        public const int BaseSeed = 20261104;

        /// <summary>
        /// The hero lines: the five original heroes in region order (Meadow Springs, Mushroom Grove, Sunny Beach, Candy Canyon, Bamboo Temple) FIRST (their positions are part of the Uncommon seeds), then the
        /// four extra Region 1 heroes (Bumble, Ribbi, Dandy, Pip). The order of the new lines is the order of their rows in <see cref="MergeTierDefs.Rows"/>.
        /// </summary>
        public static readonly string[] Lines = { "momo", "forestgirl", "beachotter", "candygirl", "monk", "bumble", "ribbi", "dandy", "pip", "nib", "spike", "moss", "fenn" };

        public static readonly TierRule[] Rules =
        {
            null,   // Common: no tier rule
            new TierRule { Rarity = CharacterRarity.Uncommon, BoostedStats = 2, PctMin = 10, PctMax = 25, OverPreviousTier = true, RunCoinsMin = 50, RunCoinsMax = 100, RunGemsMin = 1, RunGemsMax = 5, PowerCount = 1, CooldownSeconds = 30 },
            new TierRule { Rarity = CharacterRarity.Rare, BoostedStats = 3, PctMin = 10, PctMax = 25, OverPreviousTier = true, RunCoinsMin = 200, RunCoinsMax = 500, RunGemsMin = 5, RunGemsMax = 10, PowerCount = 2, CooldownSeconds = 22 },
            new TierRule { Rarity = CharacterRarity.Epic, BoostedStats = 3, PctMin = 10, PctMax = 25, OverPreviousTier = true, RunCoinsMin = 500, RunCoinsMax = 1000, RunGemsMin = 5, RunGemsMax = 10, PowerCount = 3, CooldownSeconds = 15 },
            new TierRule { Rarity = CharacterRarity.Legendary, BoostedStats = 4, PctMin = 10, PctMax = 25, OverPreviousTier = true, RunCoinsMin = 1000, RunCoinsMax = 2000, RunGemsMin = 5, RunGemsMax = 10, PowerCount = 4, CooldownSeconds = 10 },
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
            // Meadow Springs extra heroes (F4.1): the first power is the hero's identity, the rest follow the theme
            // Bumble the bee (mage): speed + Flight, then sweet pickups and a spell shield
            { "bumble", new[] { PowerUp.Flight, PowerUp.Magnet, PowerUp.SlowTime, PowerUp.Shield } },
            // Ribbi the frog: jump + Double Jump, then clinging and leaping
            { "ribbi", new[] { PowerUp.DoubleJump, PowerUp.WallStick, PowerUp.Magnet, PowerUp.Rocket } },
            // Dandy the dandelion puff: slow fall + Slow Time, then soft landings
            { "dandy", new[] { PowerUp.SlowTime, PowerUp.Shield, PowerUp.Magnet, PowerUp.DoubleJump } },
            // Pip the ladybug: wall bounce + Shield, then wall mastery
            { "pip", new[] { PowerUp.Shield, PowerUp.WallStick, PowerUp.SlowTime, PowerUp.Magnet } },
            // Mushroom Grove extra heroes (F4.2): the first power is the hero's identity
            // Nib the squirrel: Magnet (plus a flat coin find stat), then leaps and a rocket for far coins
            { "nib", new[] { PowerUp.Magnet, PowerUp.DoubleJump, PowerUp.SlowTime, PowerUp.Rocket } },
            // Spike the hedgehog: Shield, then pickups and calm
            { "spike", new[] { PowerUp.Shield, PowerUp.Magnet, PowerUp.SlowTime, PowerUp.Rocket } },
            // Moss the snail kid: Wall Stick, then a shield shell
            { "moss", new[] { PowerUp.WallStick, PowerUp.Shield, PowerUp.SlowTime, PowerUp.DoubleJump } },
            // Fenn the fox kit: the run bonus is the identity; quick power-ups to pick coins up
            { "fenn", new[] { PowerUp.Rocket, PowerUp.Magnet, PowerUp.DoubleJump, PowerUp.Shield } },
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

        /// <summary>
        /// Shown stats of a tier: the previous tier's SHOWN values (Common for Uncommon) with the row's percent on each boosted stat, one decimal; unboosted stats stay exactly as they were.
        /// Run bonus = the Common hero's own bonus plus the row's add (each tier's range is its own total, not cumulative on lower tiers).
        /// </summary>
        public static ShownStats Apply(ShownStats prev, ShownStats common, TierRow r)
        {
            return new ShownStats
            {
                Jump = ShownStats.Boosted(prev.Jump, r.JumpPct),
                Speed = ShownStats.Boosted(prev.Speed, r.SpeedPct),
                Control = ShownStats.Boosted(prev.Control, r.ControlPct),
                WallBounce = ShownStats.Boosted(prev.WallBounce, r.WallPct),
                CoinFind = prev.CoinFind,
                GemFind = prev.GemFind,
                RunBonusCoins = common.RunBonusCoins + r.RunCoinsAdd,
                RunBonusGems = common.RunBonusGems + r.RunGemsAdd,
            };
        }

        /// <summary>Gain lines between two shown stat blocks ("Jump +17%", "Run coins +60"); the percent is the whole percent between the printed numbers. Used by the merge screen (MergeRules.ShownGainLines).</summary>
        public static List<string> GainLines(ShownStats from, ShownStats to)
        {
            var lines = new List<string>(6);
            if (from == null || to == null) return lines;
            AddShown(lines, "Jump", from.Jump, to.Jump);
            AddShown(lines, "Speed", from.Speed, to.Speed);
            AddShown(lines, "Control", from.Control, to.Control);
            AddShown(lines, "Wall Bounce", from.WallBounce, to.WallBounce);
            if (to.CoinFind > from.CoinFind) lines.Add("Coins +" + (to.CoinFind - from.CoinFind));
            if (to.GemFind > from.GemFind) lines.Add("Gems +" + (to.GemFind - from.GemFind));
            if (to.RunBonusCoins > from.RunBonusCoins) lines.Add("Run coins +" + (to.RunBonusCoins - from.RunBonusCoins));
            if (to.RunBonusGems > from.RunBonusGems) lines.Add("Run gems +" + (to.RunBonusGems - from.RunBonusGems));
            return lines;
        }

        static void AddShown(List<string> lines, string label, double before, double now)
        {
            int pct = ShownStats.Percent(before, now);
            if (pct > 0) lines.Add(label + " +" + pct + "%");
        }

        /// <summary>Null when every boosted stat of the row prints as EXACTLY its percent over <paramref name="prev"/> (the shown, one-decimal numbers), else a reason. Used by the tests and the generator.</summary>
        public static string ValidateChain(ShownStats prev, TierRow r)
        {
            if (prev == null || r == null) return "no data";
            if (r.JumpPct != 0 && !ShownStats.IsExact(prev.Jump, r.JumpPct)) return r.Id + ": Jump +" + r.JumpPct + "% over " + prev.Jump + " prints as +" + ShownStats.Percent(prev.Jump, ShownStats.Boosted(prev.Jump, r.JumpPct)) + "%";
            if (r.SpeedPct != 0 && !ShownStats.IsExact(prev.Speed, r.SpeedPct)) return r.Id + ": Speed +" + r.SpeedPct + "% over " + prev.Speed + " is not exact";
            if (r.ControlPct != 0 && !ShownStats.IsExact(prev.Control, r.ControlPct)) return r.Id + ": Control +" + r.ControlPct + "% over " + prev.Control + " is not exact";
            if (r.WallPct != 0 && !ShownStats.IsExact(prev.WallBounce, r.WallPct)) return r.Id + ": Wall Bounce +" + r.WallPct + "% over " + prev.WallBounce + " is not exact";
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
        /// all inside the rule of <paramref name="rarity"/>. <paramref name="prev"/> is the SHOWN stats of the previous tier of the same hero (the Common hero for Uncommon, the
        /// Uncommon row's result for Rare, and so on): a percent is only accepted when it prints as exactly that percent on the one-decimal sheet. Paste the numbers into
        /// <see cref="MergeTierDefs.Rows"/> as constants; nothing is random at run time. Chain: generate Uncommon from Common, Rare from <see cref="Apply"/>(Uncommon), and so on.
        /// </summary>
        public static TierRow Generate(string lineId, string id, string name, string artId, CharacterRarity rarity, int seed, ShownStats prev)
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
                double before = s == 0 ? prev.Jump : s == 1 ? prev.Speed : s == 2 ? prev.Control : prev.WallBounce;
                int pct = rng.Range(rule.PctMin, rule.PctMax);
                for (int tries = 0; tries < 64 && !ShownStats.IsExact(before, pct); tries++) pct = rng.Range(rule.PctMin, rule.PctMax);
                for (int p = rule.PctMin; !ShownStats.IsExact(before, pct) && p <= rule.PctMax; p++) pct = p;   // fallback scan: always finds one (step 0.1 is under 2.5 percent of any stat)
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
