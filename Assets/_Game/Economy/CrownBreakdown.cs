namespace FluffyClimbers.Economy
{
    /// <summary>
    /// The numbers of the crown calculation pop-up (owner 2026-11-13): Crown = (floors x 10 + combo points) x (1 + setting bonus), filled with the best run of a level
    /// (or an Endless region) in one Gameplay setting. Pure: built from the stored <see cref="CrownRun"/>, so the shown sum is exactly the stored crown (<see cref="CrownScore.FromScore"/>).
    /// </summary>
    public sealed class CrownBreakdown
    {
        /// <summary>Gameplay setting 0..4 (Relaxed .. Insane) and its bonus in percent (CrownScore.BonusPercent, the same numbers as SimConfig.DifficultyStepPercent).</summary>
        public int Mode, BonusPercent;
        /// <summary>A crown exists in this setting (the player has finished a run that counts). False shows the formula with placeholders.</summary>
        public bool HasRun;
        /// <summary>Floors and best combo chain of the run were stored. False for a crown from an old save: only the base score is known.</summary>
        public bool PartsKnown;
        public int Floors, BestCombo;
        /// <summary>Floors x 10 and the combo points (each combo step adds step x step x 5); their sum is <see cref="BaseScore"/>.</summary>
        public int FloorPoints, ComboPoints, BaseScore;
        /// <summary>BaseScore x (1 + bonus), rounded half up: the number shown on the level and Endless pop-ups.</summary>
        public int Crown;

        /// <summary>Builds the breakdown of a stored run (null = no run yet) in a Gameplay setting.</summary>
        public static CrownBreakdown From(CrownRun run, int mode, int knownCrown = 0)
        {
            mode = TimeRecords.ClampMode(mode);
            var b = new CrownBreakdown { Mode = mode, BonusPercent = CrownScore.BonusPercent(mode) };
            if (run == null || run.BaseScore <= 0)
            {
                // a stored crown whose base score no integer can make (rounding of the bonus skips some crowns): shown as stored, the parts are unknown
                if (knownCrown > 0) { b.HasRun = true; b.Crown = knownCrown; }
                return b;
            }
            b.HasRun = true;
            b.BaseScore = run.BaseScore;
            b.PartsKnown = run.Floors > 0 && (long)run.Floors * CrownScore.FloorPoints <= run.BaseScore;
            if (b.PartsKnown)
            {
                b.Floors = run.Floors;
                b.BestCombo = run.BestCombo;
                b.FloorPoints = run.Floors * CrownScore.FloorPoints;
                b.ComboPoints = run.BaseScore - b.FloorPoints;
            }
            b.Crown = CrownScore.FromScore(run.BaseScore, mode);
            return b;
        }

        /// <summary>Points of combo step <paramref name="step"/> (the 3rd landing of a chain pays 3 x 3 x 5 = 45).</summary>
        public static int StepPoints(int step) { return step * step * CrownScore.ComboPoints; }
    }
}
