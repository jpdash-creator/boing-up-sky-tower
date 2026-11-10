namespace FluffyClimbers.Simulation
{
    /// <summary>
    /// Which levels carry the hidden gem (owner 2026-11-10, same in all 5 regions): level 8 (the bonus gem) and every level whose goal is FindHiddenGem
    /// (level 15). Both pay the hidden-gem reward on the first clear and both can hold the double (worth 2) gem. One helper so no caller repeats the literal.
    /// </summary>
    public static class GemLevels
    {
        /// <summary>The level index that always holds the bonus hidden gem.</summary>
        public const int BonusLevelIndex = 8;

        /// <summary>True for level 8 and for a FindHiddenGem level.</summary>
        public static bool IsGemLevel(int levelIndex, GoalType goal)
        {
            return levelIndex == BonusLevelIndex || goal == GoalType.FindHiddenGem;
        }

        /// <summary>Same from the content goal id string (LevelDef.Goal.Type).</summary>
        public static bool IsGemLevel(int levelIndex, string goalTypeId)
        {
            return IsGemLevel(levelIndex, GoalTypes.FromId(goalTypeId));
        }
    }
}
