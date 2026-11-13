using FluffyClimbers.Simulation;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// The crown (owner decision 2026-11-12): the per-level, per-Gameplay-setting best score the future leaderboard ranks.
    /// crown = (floors x 10 + combo^2 x 5) x (1 + setting percent), the setting percents are the scroll step percents of
    /// <see cref="SimConfig.DifficultyStepPercent"/> (Relaxed 10, Easy 20, Normal 25, Hard 40, Insane 50). Integer basis-point maths, rounded half up, no floats.
    /// The run score of the simulation (<c>SimState.Score</c>, floors x 10 plus combo^2 x 5 per landing) is the base; the crown is that base with the setting bonus.
    /// </summary>
    public static class CrownScore
    {
        /// <summary>Points per floor and per combo-step square (same as SimConfig.FloorPoints and ComboPointsMultiplier).</summary>
        public const int FloorPoints = 10;
        public const int ComboPoints = 5;
        public const int BasisPoints = 10000;

        /// <summary>The bonus of a Gameplay setting in percent (clamped to a valid setting).</summary>
        public static int BonusPercent(int mode)
        {
            return SimConfig.DifficultyStepPercent[TimeRecords.ClampMode(mode)];
        }

        /// <summary>The multiplier in basis points: 10000 + percent x 100 (11000 for Relaxed ... 15000 for Insane).</summary>
        public static int MultiplierBasisPoints(int mode)
        {
            return BasisPoints + BonusPercent(mode) * 100;
        }

        /// <summary>The literal formula: (floors x 10 + combo^2 x 5) x (1 + percent), rounded half up. Negative inputs count as 0.</summary>
        public static int FromFloorsCombo(int floors, int combo, int mode)
        {
            long f = floors < 0 ? 0 : floors, c = combo < 0 ? 0 : combo;
            return FromScore(f * FloorPoints + c * c * ComboPoints, mode);
        }

        /// <summary>Combo points of a run: the base score minus the floor points (never negative).</summary>
        public static int ComboPointsOf(int baseScore, int floors)
        {
            long v = (long)baseScore - (long)(floors < 0 ? 0 : floors) * FloorPoints;
            return v < 0 ? 0 : (v > int.MaxValue ? int.MaxValue : (int)v);
        }

        /// <summary>
        /// A base score whose crown in <paramref name="mode"/> is exactly <paramref name="crown"/> (the smallest one), or 0 when none exists (a crown can be skipped by the rounding of a big bonus).
        /// Used when a saved crown has no stored run.
        /// </summary>
        public static int BaseFromCrown(int crown, int mode)
        {
            if (crown <= 0) return 0;
            long guess = (long)crown * BasisPoints / MultiplierBasisPoints(mode);
            for (long b = guess > 2 ? guess - 2 : 0; b <= guess + 3; b++)
            {
                if (b > 0 && b <= int.MaxValue && FromScore(b, mode) == crown) return (int)b;
            }
            return 0;
        }

        /// <summary>The crown of a base run score (SimState.Score) in a Gameplay setting, rounded half up, saturating at int.MaxValue.</summary>
        public static int FromScore(long baseScore, int mode)
        {
            if (baseScore <= 0) return 0;
            long v = (baseScore * MultiplierBasisPoints(mode) + BasisPoints / 2) / BasisPoints;
            return v > int.MaxValue ? int.MaxValue : (int)v;
        }
    }
}
