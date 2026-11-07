using System;
using System.Globalization;

namespace BoingUpSkyTower.Simulation
{
    /// <summary>
    /// The four main stats exactly as the player SEES them on the hero sheet (docs/29 section 2): Jump as a height (launch squared over 2g), Speed, Control, Wall Bounce (speed times wall share),
    /// each rounded to ONE decimal. Merge-tier boosts are defined on these numbers (owner 2026-11-03), so "+17 percent" reads +17 percent on the sheet and in the merge text.
    /// The physics integers (<see cref="CharacterStats"/>) are derived from them with <see cref="ToPhysics"/> and then capped. Pure, no engine types.
    /// </summary>
    public sealed class ShownStats
    {
        public double Jump, Speed, Control, WallBounce;
        public int CoinFind, GemFind, RunBonusCoins, RunBonusGems;

        /// <summary>One decimal, the way the sheet prints it ("0.#" format), so a stored shown value always equals its printed text.</summary>
        public static double R1(double v)
        {
            return double.Parse(v.ToString("0.0", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        /// <summary>Whole percent between two shown values, rounded half away from zero (what the merge text prints).</summary>
        public static int Percent(double prev, double now)
        {
            if (prev <= 0.0) return 0;
            return (int)Math.Round((now / prev - 1.0) * 100.0, MidpointRounding.AwayFromZero);
        }

        /// <summary>The shown value after a percent boost: prev x (1 + pct/100), one decimal. pct 0 returns prev unchanged.</summary>
        public static double Boosted(double prev, int pct)
        {
            return pct == 0 ? prev : R1(prev * (100 + pct) / 100.0);
        }

        /// <summary>True when boosting prev by pct prints as exactly +pct percent (one-decimal rounding can move it by one point on small numbers).</summary>
        public static bool IsExact(double prev, int pct)
        {
            return Percent(prev, Boosted(prev, pct)) == pct;
        }

        /// <summary>The shown values of a stats block under the given config (same numbers as <see cref="CharacterStats.Sheet"/>, rounded to one decimal).</summary>
        public static ShownStats From(CharacterStats s, SimConfig cfg)
        {
            StatSheet sh = s.Sheet(cfg);
            return new ShownStats
            {
                Jump = R1(sh.Jump),
                Speed = R1(sh.Speed),
                Control = R1(sh.Control),
                WallBounce = R1(sh.WallBounce),
                CoinFind = s.CoinFind,
                GemFind = s.GemFind,
                RunBonusCoins = s.RunBonusCoins,
                RunBonusGems = s.RunBonusGems,
            };
        }

        /// <summary>
        /// The whole-number physics that matches these shown values, NOT yet limited: launch (whole and in tenths) from the Jump height (v = sqrt(2 g h)), speed and control rounded, the wall share
        /// from Wall Bounce over Speed. Rounding to whole units can differ from the shown value by half a unit; the cap (MergeTierDefs.Cap) then applies the docs/29 section 7 limits.
        /// </summary>
        public CharacterStats ToPhysics(SimConfig cfg)
        {
            double g = new CharacterStats().Sheet(cfg).Gravity;
            double speed = Speed > 0.0 ? Speed : 1.0;
            return new CharacterStats
            {
                JumpLaunch = (int)Math.Round(Math.Sqrt(2.0 * g * Jump), MidpointRounding.AwayFromZero),
                JumpLaunchTenths = (int)Math.Round(Math.Sqrt(2.0 * g * Jump) * 10.0, MidpointRounding.AwayFromZero),
                MaxSpeed = (int)Math.Round(Speed, MidpointRounding.AwayFromZero),
                Control = (int)Math.Round(Control, MidpointRounding.AwayFromZero),
                WallBoostHundredths = (int)Math.Round(WallBounce / speed * 100.0, MidpointRounding.AwayFromZero),
                CoinFind = CoinFind,
                GemFind = GemFind,
                RunBonusCoins = RunBonusCoins,
                RunBonusGems = RunBonusGems,
            };
        }
    }
}
