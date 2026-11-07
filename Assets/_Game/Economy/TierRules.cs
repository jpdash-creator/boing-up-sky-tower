using System.Collections.Generic;
using BoingUpSkyTower.Simulation;

namespace BoingUpSkyTower.Economy
{
    /// <summary>
    /// The owner's rule for one merge tier (docs/34 section 2b, docs/44), as plain data: the run-bonus ranges and the number and cooldown of the hero powers. The PHYSICS of a tier hero are no longer a percent
    /// chain: every hero has an absolute row in <see cref="HeroTable"/> and <see cref="HeroIndex"/> holds the rules (Jump band per rarity, legal limits, measured FI gate).
    /// </summary>
    public sealed class TierRule
    {
        public CharacterRarity Rarity;
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
    /// Rule tables for Uncommon, Rare, Elite (code enum: Epic) and Legendary, the region power pools and pure validators of a <see cref="TierRow"/> (run bonus) and of a
    /// <see cref="HeroRow"/> (physics, by <see cref="HeroIndex"/>). No Unity types; compiled by the Simulation test project as well.
    /// </summary>
    public static class TierRules
    {
        public const int CoinStep = 10;
        /// <summary>The seed the run bonuses of the first rows were drawn with (kept for the record; nothing is random at run time).</summary>
        public const int BaseSeed = 20261104;

        /// <summary>
        /// The hero lines: the five original heroes in region order (Meadow Springs, Mushroom Grove, Sunny Beach, Candy Canyon, Bamboo Temple) FIRST (their positions are part of the Uncommon seeds), then the
        /// four extra Region 1 heroes (Bumble, Ribbi, Dandy, Pip). The order of the lines is the order of their rows in <see cref="HeroTable.Rows"/>.
        /// </summary>
        public static readonly string[] Lines = { "momo", "forestgirl", "beachotter", "candygirl", "monk", "bumble", "ribbi", "dandy", "pip", "nib", "spike", "moss", "fenn", "pinch", "shelly", "bloop", "coco", "gummi", "choco", "marsh", "cookie", "sprout", "tora", "rusu", "ryu" };

        public static readonly TierRule[] Rules =
        {
            null,   // Common: no tier rule
            new TierRule { Rarity = CharacterRarity.Uncommon, RunCoinsMin = 50, RunCoinsMax = 100, RunGemsMin = 1, RunGemsMax = 5, PowerCount = 1, CooldownSeconds = 30 },
            new TierRule { Rarity = CharacterRarity.Rare, RunCoinsMin = 200, RunCoinsMax = 500, RunGemsMin = 5, RunGemsMax = 10, PowerCount = 2, CooldownSeconds = 22 },
            new TierRule { Rarity = CharacterRarity.Epic, RunCoinsMin = 500, RunCoinsMax = 1000, RunGemsMin = 5, RunGemsMax = 10, PowerCount = 3, CooldownSeconds = 15 },
            new TierRule { Rarity = CharacterRarity.Legendary, RunCoinsMin = 1000, RunCoinsMax = 2000, RunGemsMin = 5, RunGemsMax = 10, PowerCount = 4, CooldownSeconds = 10 },
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
            // Sunny Beach extra heroes (F4.3): the first power is the hero's identity (Otto keeps his pool above)
            // Pinch the crab: Shield (claws up), then a clinging claw grip and calm tides
            { "pinch", new[] { PowerUp.Shield, PowerUp.WallStick, PowerUp.SlowTime, PowerUp.Magnet } },
            // Shelly the sea turtle: Slow Time (a calm swimmer), then a shell shield and pickups
            { "shelly", new[] { PowerUp.SlowTime, PowerUp.Shield, PowerUp.Magnet, PowerUp.DoubleJump } },
            // Bloop the pufferfish kid: Double Jump (bounces off the water), then a rocket pop
            { "bloop", new[] { PowerUp.DoubleJump, PowerUp.SlowTime, PowerUp.Rocket, PowerUp.Shield } },
            // Coco the monkey: Magnet and a coin find stat, then leaps and a rocket
            { "coco", new[] { PowerUp.Magnet, PowerUp.DoubleJump, PowerUp.Rocket, PowerUp.SlowTime } },
            // Candy Canyon extra heroes (F4.4): the first power is the hero's identity (Bonbon keeps her pool above)
            // Gummi the gummy bear: Shield (a bouncy gummy hide), then sticky wall grip, calm and pickups
            { "gummi", new[] { PowerUp.Shield, PowerUp.WallStick, PowerUp.SlowTime, PowerUp.Magnet } },
            // Choco the chocolate bunny: Double Jump (hops), then a rocket pop
            { "choco", new[] { PowerUp.DoubleJump, PowerUp.Rocket, PowerUp.Shield, PowerUp.SlowTime } },
            // Marsh the marshmallow kid: Slow Time (soft and floaty), then a toasty shield
            { "marsh", new[] { PowerUp.SlowTime, PowerUp.Shield, PowerUp.DoubleJump, PowerUp.Magnet } },
            // Cookie the cookie kid: Magnet and a coin find stat, then leaps and a rocket
            { "cookie", new[] { PowerUp.Magnet, PowerUp.DoubleJump, PowerUp.Rocket, PowerUp.Shield } },
            // Bamboo Temple extra heroes (F4.5): the first power is the hero's identity (Bao keeps his pool above)
            // Sprout the bamboo sprout: Shield (a leafy guard), then wall grip and calm
            { "sprout", new[] { PowerUp.Shield, PowerUp.WallStick, PowerUp.SlowTime, PowerUp.DoubleJump } },
            // Tora the tiger cub: Double Jump (pounce), then a wall claw grip and a rocket
            { "tora", new[] { PowerUp.DoubleJump, PowerUp.WallStick, PowerUp.Rocket, PowerUp.Shield } },
            // Rusu the red panda: Magnet and a coin find stat, then leaps and calm
            { "rusu", new[] { PowerUp.Magnet, PowerUp.DoubleJump, PowerUp.SlowTime, PowerUp.Rocket } },
            // Ryu the wingless dragon pup: Rocket (a fiery dash, no wings), then a scale shield
            { "ryu", new[] { PowerUp.Rocket, PowerUp.Shield, PowerUp.Magnet, PowerUp.DoubleJump } },
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

        /// <summary>Null when the row obeys its tier rule (run bonus ranges and steps), else a short reason. The physics are checked by <see cref="ValidatePhysics"/>. Pure; used by the tests.</summary>
        public static string Validate(TierRow r)
        {
            if (r == null) return "no row";
            TierRule rule = RuleFor(r.Rarity);
            if (rule == null) return r.Id + ": " + r.Rarity + " has no tier rule";
            if (r.RunCoinsAdd < rule.RunCoinsMin || r.RunCoinsAdd > rule.RunCoinsMax || r.RunCoinsAdd % CoinStep != 0) return r.Id + ": run coins " + r.RunCoinsAdd + " outside " + rule.RunCoinsMin + ".." + rule.RunCoinsMax + " in steps of " + CoinStep;
            if (r.RunGemsAdd < rule.RunGemsMin || r.RunGemsAdd > rule.RunGemsMax) return r.Id + ": run gems " + r.RunGemsAdd + " outside " + rule.RunGemsMin + ".." + rule.RunGemsMax;
            return null;
        }

        /// <summary>Null when the hero row obeys the index rules (<see cref="HeroIndex.Validate"/>: legal limits, the tier's Jump band and wall range), else a short reason with the id. Pure; used by the tests.</summary>
        public static string ValidatePhysics(HeroRow r)
        {
            if (r == null) return "no row";
            if (r.Tier < 0 || r.Tier >= HeroIndex.Tiers) return r.Id + ": tier " + r.Tier + " outside 0.." + (HeroIndex.Tiers - 1);
            string why = HeroIndex.Validate(r.Tier, new HeroPhysics(r.LaunchTenths, r.Speed, r.Control, r.Wall));
            return why == null ? null : r.Id + ": " + why;
        }

        /// <summary>
        /// The Jump band of a tier as the "i" popup and the hero list print it: "+6 to +10% Jump" (launch speed over the default 19.0, <see cref="HeroIndex.JumpPctMin"/> / <see cref="HeroIndex.JumpPctMax"/>).
        /// Common is "0 to +5% Jump".
        /// </summary>
        public static string JumpBandText(CharacterRarity r)
        {
            int t = (int)r;
            if (t < 0 || t >= HeroIndex.Tiers) return "";
            int lo = HeroIndex.JumpPctMin[t];
            return (lo == 0 ? "0" : "+" + lo) + " to +" + HeroIndex.JumpPctMax[t] + "% Jump";
        }

        /// <summary>Gain lines between two shown stat blocks ("Jump +17%", "Run coins +60"); the percent is the whole percent between the printed numbers, a line only when the stat rose. Used by the merge screen (MergeRules.ShownGainLines).</summary>
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
    }
}
