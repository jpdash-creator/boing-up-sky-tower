namespace FluffyClimbers.Simulation
{
    /// <summary>
    /// Which levels carry the hidden gem. Since the economy review of 2026-11-13 only the level whose goal is FindHiddenGem (level 15 of every region) has one: the hidden gem is the goal
    /// object, and the 2 gems that level pays are the fixed milestone reward of its first clear (no separate gem for the pickup). The old bonus gem of level 8 is gone.
    /// One helper so no caller repeats the literal.
    /// </summary>
    public static class GemLevels
    {
        /// <summary>True for a FindHiddenGem level (the level index no longer matters).</summary>
        public static bool IsGemLevel(int levelIndex, GoalType goal)
        {
            return goal == GoalType.FindHiddenGem;
        }

        /// <summary>Same from the content goal id string (LevelDef.Goal.Type).</summary>
        public static bool IsGemLevel(int levelIndex, string goalTypeId)
        {
            return IsGemLevel(levelIndex, GoalTypes.FromId(goalTypeId));
        }
    }
}
