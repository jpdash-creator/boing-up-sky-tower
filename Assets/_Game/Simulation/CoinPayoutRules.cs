namespace FluffyClimbers.Simulation
{
    /// <summary>
    /// Pure integer rules of the campaign coin payout and the milestone gems (owner, issue list 24 points 5 and 6, 2026-10-25).
    /// Kept in the Simulation folder (no engine types) so the dotnet tests run the same arithmetic as Economy/Rewards.cs.
    /// </summary>
    public static class CoinPayoutRules
    {
        /// <summary>Clear payout before stars and combo.</summary>
        public const int ClearCoins = 70;
        /// <summary>Coins per star earned.</summary>
        public const int StarCoins = 60;
        /// <summary>Coins per combo step (the run's SimState.Coins counts steps times 5, already scaled).</summary>
        public const int ComboStepCoins = 5;
        /// <summary>Once the level has 3 stars a replay pays this percent of the coins and drops no gems.</summary>
        public const int ReplayPercent = 50;

        /// <summary>True when a run pays the reduced replay rate: the level already had all 3 stars before this run.</summary>
        public static bool IsReducedReplay(int starsBefore)
        {
            return starsBefore >= 3;
        }

        /// <summary>Clear payout before the region factor: 70 + 60 per star.</summary>
        public static int BaseCoins(int stars)
        {
            int s = stars < 0 ? 0 : (stars > 3 ? 3 : stars);
            return ClearCoins + StarCoins * s;
        }

        /// <summary>x factor (4 decimals, rounded half up).</summary>
        public static long Scale(long coins, double regionCoinFactor)
        {
            long factor = (long)System.Math.Round(regionCoinFactor * 10000);
            return (coins * factor + 5000) / 10000;
        }

        /// <summary>Total payout: (base + combo) x region factor, then the replay percent when reduced. Failed runs are not passed here (they pay 0).</summary>
        public static int Payout(int stars, int comboCoins, double regionCoinFactor, bool reducedReplay)
        {
            long gross = Scale(BaseCoins(stars), regionCoinFactor) + Scale(comboCoins < 0 ? 0 : comboCoins, regionCoinFactor);
            if (reducedReplay) gross = gross * ReplayPercent / 100;
            return (int)gross;
        }

        /// <summary>
        /// Milestone gems of a first clear (L5, L10, L15): the level's chance roll (seeded from the level seed) pays min..max, and the guaranteed
        /// amount is a floor (L15: 5). Deterministic, so a server can recompute it.
        /// </summary>
        public static int MilestoneGems(int levelSeed, double chance, int min, int max, int guaranteed)
        {
            Rng rng = new Rng(unchecked(levelSeed ^ 13));
            int amount = 0;
            if (rng.Next(100) < (int)System.Math.Round(chance * 100.0)) amount = rng.Range(min, max);
            return guaranteed > amount ? guaranteed : amount;
        }
    }
}
