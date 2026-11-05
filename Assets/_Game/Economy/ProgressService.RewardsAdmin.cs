using System;

namespace BoingUpSkyTower.Economy
{
    /// <summary>
    /// Admin "Main rewards" test switch (docs/38, Bug List 4 E3): every Main-tab reward (the five region rewards and all star milestones) reads as complete but
    /// stays unclaimed and unpaid, so the owner can review and claim each one. It is an override stored in the save (additive field, no version break); the
    /// real level and star progress is never touched, so switching it off restores the previous progress exactly.
    /// </summary>
    public sealed partial class ProgressService
    {
        /// <summary>Pushed by GameSettings.ApplyFlags: true only while System Test is on. With it false the saved override is ignored and dropped (PROD READY).</summary>
        public static bool TestMainRewardsAllowed;

        /// <summary>True when the Main rewards test override is on and allowed. Clears a stale stored flag when System Test is off, so test progress never leaks.</summary>
        public bool AdminMainRewardsReady
        {
            get
            {
                RewardsData r = Data.Rewards;
                if (r == null || !r.TestMainReady) return false;
                if (TestMainRewardsAllowed) return true;
                r.TestMainReady = false;
                return false;
            }
        }

        /// <summary>Admin: turns the Main rewards override on or off and saves. Returns the new state (false when System Test is off).</summary>
        public bool AdminSetMainRewardsReady(bool on)
        {
            if (on && !TestMainRewardsAllowed) on = false;
            Rw.TestMainReady = on;
            Commit();
            return on;
        }

        /// <summary>Main progress with the override applied: a region reward is complete (all its levels), a star reward reaches the largest star target.</summary>
        int MainProgress(RewardEntry e)
        {
            bool region = IsRegionEntry(e);
            int real = region ? RegionLevelsCleared(e.Target) : TotalStars();
            if (!AdminMainRewardsReady) return real;
            int need;
            if (region) need = RewardTarget(RewardTab.Main, e);
            else
            {
                need = 0;
                foreach (RewardEntry s in RewardDefs.StarMilestones) need = Math.Max(need, s.Target);
            }
            return Math.Max(real, need);
        }
    }
}
