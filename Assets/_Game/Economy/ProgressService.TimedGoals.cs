using BoingUpSkyTower.Content;

namespace BoingUpSkyTower.Economy
{
    /// <summary>
    /// Reward time goals (doc 46 section 8). The Daily and Weekly goals read the period counters <see cref="TimedCounters"/> that ProgressService.Records.cs keeps
    /// (they restart with the local day and week and are saved at once with every recorded run); the Main goal counts levels with a Gold medal.
    /// Everything here is read-only: progress is derived from the save, so an old save simply starts at 0 and nothing can be paid twice.
    /// </summary>
    public sealed partial class ProgressService
    {
        /// <summary>The Daily endless goal wants floor 100 within 3:00 (the Time Attack length).</summary>
        public const int Floor100GoalTicks = TimeRecords.TimeAttackTicks;

        /// <summary>Value of a timed-goal counter (see the timed names in <see cref="RewardStat"/>); false for every other counter name.</summary>
        bool TryTimedGoalStat(string stat, out int value)
        {
            value = 0;
            if (stat == RewardStat.PaceClears) { value = TimedPaceClearsToday(); return true; }
            if (stat == RewardStat.Floor100In3)
            {
                int t = TimedSplit100TodayTicks();
                value = t > 0 && t <= Floor100GoalTicks ? 1 : 0;
                return true;
            }
            if (stat == RewardStat.BestPaceLevels) { value = TimedBestPaceLevelsThisWeek(); return true; }
            if (stat == RewardStat.GoldMedals) { value = TimedGoldThisWeek(); return true; }
            if (stat == RewardStat.TimeAttackFloors) { value = TimedTimeAttackFloorsThisWeek(); return true; }
            return false;
        }

        /// <summary>
        /// Levels of the region that hold a Gold medal in at least one Gameplay mode (0 to 25): the counter of the Main "gold medals on 5 / 10 / 15 / 20 / 25 levels" goals.
        /// (<see cref="GoldMedalsInRegion"/> counts level x mode pairs instead.)
        /// </summary>
        public int GoldMedalLevelsInRegion(int regionIndex)
        {
            RegionDef def = repo.TryGetRegionByIndex(regionIndex);
            if (def == null) return 0;
            int n = 0;
            foreach (string id in def.Levels)
                for (int m = 0; m < TimeRecords.ModeCount; m++)
                    if (Medal(id, m) == MedalLevel.Gold) { n++; break; }
            return n;
        }
    }
}
