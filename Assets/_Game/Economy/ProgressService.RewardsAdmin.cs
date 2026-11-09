using System;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// Admin "Main rewards" test switch (docs/38, Bug List 4 E3): every Main-tab region goal (all goals of regions 1 to 5) reads as complete but
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

        /// <summary>Main progress with the override applied: with the switch on every region goal of every playable region reads as complete (progress at least its target).</summary>
        int MainProgress(RewardEntry e)
        {
            RegionGoal g = e as RegionGoal;
            if (g == null) return 0;
            int real = RealGoalProgress(g);
            return AdminMainRewardsReady ? Math.Max(real, g.Target) : real;
        }
    }
}
