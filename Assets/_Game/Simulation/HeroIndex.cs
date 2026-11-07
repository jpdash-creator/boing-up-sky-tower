using System;

namespace BoingUpSkyTower.Simulation
{
    /// <summary>The four physics numbers of a hero as the index works on them: launch in tenths of u/s (190 = 19.0), speed, control, wall share in hundredths.</summary>
    public struct HeroPhysics
    {
        public int LaunchTenths, Speed, Control, Wall;
        public HeroPhysics(int launchTenths, int speed, int control, int wall) { LaunchTenths = launchTenths; Speed = speed; Control = control; Wall = wall; }
        public double Launch { get { return LaunchTenths / 10.0; } }
        public override string ToString() { return Launch.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "/" + Speed + "/" + Control + "/" + Wall; }
    }

    /// <summary>
    /// The power mechanics index and rule engine (the bible: docs/44-power-mechanics-bible.md). One number, the Forgiveness Index (FI), decides every hero's physics.
    /// FI = the share of safe jump options a hero has on a level, divided by the default hero's (Momo Common = 1.000) share on the same layouts. Higher = more forgiving.
    /// The rules, per rarity tier (0 Common, 1 Uncommon, 2 Rare, 3 Elite, 4 Legendary):
    ///   1. Jump is the only percent-banded stat: launch speed over the default 19 u/s, Common 0-5, Uncommon 6-10, Rare 10-15, Elite 15-20, Legendary 20-25 percent.
    ///   2. Speed, Control and Wall bounce are drawn at random inside the legal limits (wall bounce also inside a rarity range).
    ///   3. The draw is only valid when the measured FI matches the approved FI of the tier (<see cref="ApprovedFi"/>) on level 15 of Region 1 and the 75-level mean matches Momo's
    ///      mean of the same tier (the generator tool, Tests/Simulation/HeroGeneratorTests.cs). Momo is the default hero and his five tiers are fixed (<see cref="LadderFinal"/>).
    /// Everything here is pure data and arithmetic (no engine types); the measured gate lives in the test project because it needs the shipped levels.
    /// </summary>
    public static class HeroIndex
    {
        public const int Tiers = 5;
        public const int DefaultLaunchTenths = 190;

        /// <summary>Jump band per tier, whole percent of launch speed over the default (inclusive).</summary>
        public static readonly int[] JumpPctMin = { 0, 6, 10, 15, 20 };
        public static readonly int[] JumpPctMax = { 5, 10, 15, 20, 25 };

        // Legal limits of the four physics stats (launch in tenths).
        public const int LaunchMinTenths = 190, LaunchMaxTenths = 237;
        public const int SpeedMin = 8, SpeedMax = 12;
        public const int ControlMin = 27, ControlMax = 45;
        public const int WallMin = 69, WallMax = 100;

        /// <summary>Wall bounce range per tier (hundredths): the rarity raises the floor; wall bounce costs no FI on the measured levels.</summary>
        public static readonly int[] WallRangeMin = { 69, 72, 76, 80, 85 };
        public static readonly int[] WallRangeMax = { 85, 90, 94, 97, 100 };

        /// <summary>The approved FI per tier (Ladder Final, measured on level 15 of Region 1 with the full probe).</summary>
        public static readonly double[] ApprovedFi = { 1.000, 1.002, 1.040, 1.012, 1.048 };
        /// <summary>
        /// Where the prediction model has to point so that the MEASURED level-15 FI (fast probe) lands on Momo's value of the tier: Momo's fast reading minus the model's bias per tier
        /// (measured minus predicted, from the first 8 hero lines of the generator log: +0.027, -0.027, -0.011, +0.044, +0.059). Screening only; the measured gate decides.
        /// </summary>
        public static readonly double[] ScreenCenter = { 0.973, 1.014, 1.029, 0.978, 1.041 };

        /// <summary>A hero's FI must be within this of the approved value on level 15 of Region 1 and of Momo's own 75-level mean for its tier.</summary>
        public const double GateTolerance = 0.02;
        /// <summary>No single level may be further than this from Momo's FI of the same tier on that level.</summary>
        public const double PerLevelTolerance = 0.08;
        /// <summary>The FI band every tier has to stay inside (physics only; hero powers are not in the index yet).</summary>
        public const double BandMin = 0.97, BandMax = 1.10;

        /// <summary>Ladder Final: Momo's five tiers, measured. Common is the default hero (19.0 / 9 / 30 / 77).</summary>
        public static readonly HeroPhysics[] LadderFinal =
        {
            new HeroPhysics(190, 9, 30, 77),     // Common      FI 1.000
            new HeroPhysics(209, 9, 34, 82),     // Uncommon    FI 1.002
            new HeroPhysics(216, 9, 38, 88),     // Rare        FI 1.040
            new HeroPhysics(219, 10, 41, 94),    // Elite       FI 1.012
            new HeroPhysics(228, 11, 41, 100),   // Legendary   FI 1.048
        };

        /// <summary>Whole-percent bounds of the launch speed of a tier in tenths: lowest and highest launch that is inside the band and the legal limits.</summary>
        public static void LaunchRange(int tier, out int lo, out int hi)
        {
            lo = (int)Math.Ceiling(DefaultLaunchTenths * (100 + JumpPctMin[tier]) / 100.0 - 1e-9);
            hi = (int)Math.Floor(DefaultLaunchTenths * (100 + JumpPctMax[tier]) / 100.0 + 1e-9);
            if (hi > LaunchMaxTenths) hi = LaunchMaxTenths;
            if (lo < LaunchMinTenths) lo = LaunchMinTenths;
        }

        /// <summary>Jump percent over the default, measured on launch speed.</summary>
        public static double JumpPercent(int launchTenths) { return (launchTenths / (double)DefaultLaunchTenths - 1.0) * 100.0; }

        /// <summary>Null when the physics sit inside the legal limits and the tier's Jump band, else a short reason.</summary>
        public static string Validate(int tier, HeroPhysics p)
        {
            int lo, hi;
            LaunchRange(tier, out lo, out hi);
            if (p.LaunchTenths < lo || p.LaunchTenths > hi) return "launch " + p.Launch + " outside the tier band " + (lo / 10.0) + ".." + (hi / 10.0);
            if (p.Speed < SpeedMin || p.Speed > SpeedMax) return "speed " + p.Speed + " outside " + SpeedMin + ".." + SpeedMax;
            if (p.Control < ControlMin || p.Control > ControlMax) return "control " + p.Control + " outside " + ControlMin + ".." + ControlMax;
            if (p.Wall < WallRangeMin[tier] || p.Wall > WallRangeMax[tier]) return "wall " + p.Wall + " outside the tier range " + WallRangeMin[tier] + ".." + WallRangeMax[tier];
            return null;
        }

        // ---- the predictive FI model (a screening aid only: the MEASURED FI is the gate) ----
        // FI = J(launch) x S(speed) x C(control) x I. Fitted on the level-15 measurements of ladders A to H (error about +-0.04). Wall bounce has no effect on level 15.
        static readonly double[] JLaunch = { 17, 19, 20, 21, 22, 23, 24 };
        static readonly double[] JValue = { 0.696, 1.0, 1.045, 1.12, 1.172, 1.334, 1.441 };
        static readonly double[] SValue = { 1.033, 1.0, 0.961, 0.958, 0.958 };          // speed 8..12
        static readonly double[] CAt = { 27, 30, 30.01, 41, 42, 45 };
        static readonly double[] CValue = { 0.994, 1.0, 0.895, 0.895, 0.86, 0.81 };

        static double Lerp(double[] xs, double[] ys, double x)
        {
            if (x <= xs[0]) return ys[0];
            for (int i = 1; i < xs.Length; i++)
                if (x <= xs[i]) return ys[i - 1] + (ys[i] - ys[i - 1]) * (x - xs[i - 1]) / (xs[i] - xs[i - 1]);
            return ys[ys.Length - 1];
        }

        /// <summary>Predicted FI of a physics set on level 15 of Region 1 (screening only).</summary>
        public static double PredictFi(HeroPhysics p)
        {
            double j = Lerp(JLaunch, JValue, p.LaunchTenths / 10.0);
            double s = SValue[Math.Max(0, Math.Min(4, p.Speed - 8))];
            double c = Lerp(CAt, CValue, p.Control);
            double interaction = 1.0;
            if (p.Control >= 43 && p.Speed >= 10) interaction = 0.90;
            else if (p.Speed >= 11 && p.Control >= 41) interaction = 0.95;
            return j * s * c * interaction;
        }

        // ---- the seeded generator ----

        /// <summary>Deterministic hash of a text (FNV-1a), the same on every runtime.</summary>
        public static int Hash(string text)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (char ch in text) { h ^= ch; h *= 16777619u; }
                return (int)h;
            }
        }

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
            public int Range(int lo, int hi) { return lo + (int)(Next() % (uint)(hi - lo + 1)); }
        }

        /// <summary>
        /// Candidate number <paramref name="attempt"/> for a hero (its line id, its tier). Jump is drawn inside the tier band, Speed, Control and Wall bounce inside the legal limits
        /// and the rarity range. The draw is repeated until the PREDICTED FI is within <paramref name="screen"/> of the approved FI of the tier, so the expensive measured gate sees
        /// plausible candidates only. Deterministic: the same (line, tier, attempt) always gives the same candidate. Momo's tiers are not drawn: use <see cref="LadderFinal"/>.
        /// </summary>
        public static HeroPhysics Draw(string lineId, int tier, int attempt, double screen = 0.03)
        {
            var rng = new Rng(Hash(lineId) ^ (tier * 7919) ^ (attempt * 104729 + 17));
            int lo, hi;
            LaunchRange(tier, out lo, out hi);
            HeroPhysics best = new HeroPhysics(lo, SpeedMin + 1, 30, WallRangeMin[tier]);
            double bestErr = double.MaxValue;
            for (int i = 0; i < 4000; i++)
            {
                var p = new HeroPhysics(rng.Range(lo, hi), rng.Range(SpeedMin, SpeedMax), rng.Range(ControlMin, ControlMax), rng.Range(WallRangeMin[tier], WallRangeMax[tier]));
                double err = Math.Abs(PredictFi(p) - ScreenCenter[tier]);
                if (err <= screen) return p;
                if (err < bestErr) { bestErr = err; best = p; }
            }
            return best;
        }
    }
}
