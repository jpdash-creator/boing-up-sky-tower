namespace FluffyClimbers.Simulation
{
    /// <summary>
    /// Pure integer rules of the campaign coin payout, the milestone gems and the hero milestone bonus (owner, issue list 24 points 5 and 6, 2026-10-25; economy review 2026-11-13).
    /// Kept in the Simulation folder (no engine types) so the dotnet tests run the same arithmetic as Economy/Rewards.cs.
    /// </summary>
    public static class CoinPayoutRules
    {
        // Economy review 2026-11-13: the level payout is 20 percent lower than economy v3 (was 70 + 60 per star + 5 per combo step).
        /// <summary>Clear payout before stars and combo.</summary>
        public const int ClearCoins = 56;
        /// <summary>Coins per star earned.</summary>
        public const int StarCoins = 48;
        /// <summary>Coins a level pays per combo step.</summary>
        public const int ComboStepCoins = 4;
        /// <summary>Coins the simulation counts per combo step (<c>SimConfig.ComboLandingCoins</c>; Endless pays these unchanged). A level pays <see cref="ComboStepCoins"/> per step instead.</summary>
        public const int SimComboStepCoins = 5;
        /// <summary>Once the level has 3 stars a replay pays this percent of the coins and drops no gems (was 50 before 2026-11-13).</summary>
        public const int ReplayPercent = 25;

        /// <summary>True when a run pays the reduced replay rate: the level already had all 3 stars before this run.</summary>
        public static bool IsReducedReplay(int starsBefore)
        {
            return starsBefore >= 3;
        }

        /// <summary>
        /// The region coin factor of regions 1 to 5 in percent (owner 2026-11-10): ONE factor for the level payout (region JSON coinFactor 1.00 to 1.12) and for the
        /// main-goal coin rewards (RegionGoalTable.ScaledCoins). A test keeps it equal to the shipped region files.
        /// </summary>
        public static readonly int[] RegionFactorPercent = { 100, 103, 106, 109, 112 };

        /// <summary>Clear payout before the region factor: 56 + 48 per star.</summary>
        public static int BaseCoins(int stars)
        {
            int s = stars < 0 ? 0 : (stars > 3 ? 3 : stars);
            return ClearCoins + StarCoins * s;
        }

        /// <summary>The level's combo coins from the coins the simulation counted (5 per combo step): 4 per step, the same 20 percent cut as the rest of the payout.</summary>
        public static int ComboCoins(int simCoins)
        {
            if (simCoins <= 0) return 0;
            return (int)((long)simCoins * ComboStepCoins / SimComboStepCoins);
        }

        /// <summary>x factor (4 decimals, rounded half up).</summary>
        public static long Scale(long coins, double regionCoinFactor)
        {
            long factor = (long)System.Math.Round(regionCoinFactor * 10000);
            return (coins * factor + 5000) / 10000;
        }

        /// <summary>Total payout: (base + combo) x region factor, then the replay percent when reduced. <paramref name="simComboCoins"/> is the simulation's coin count (5 per combo step). Failed runs are not passed here (they pay 0).</summary>
        public static int Payout(int stars, int simComboCoins, double regionCoinFactor, bool reducedReplay)
        {
            long gross = Scale(BaseCoins(stars), regionCoinFactor) + Scale(ComboCoins(simComboCoins), regionCoinFactor);
            if (reducedReplay) gross = gross * ReplayPercent / 100;
            return (int)gross;
        }

        // ---- milestone gems (owner 2026-11-13): only levels 5, 10, 15, 20, 25, fixed amounts, first clear only ----

        /// <summary>Fixed gems of the milestone levels 5, 10, 15, 20, 25 (paid once, on the first clear).</summary>
        public static readonly int[] MilestoneGemTable = { 1, 1, 2, 2, 3 };

        /// <summary>The fixed gems of a level index: 1, 1, 2, 2, 3 on levels 5, 10, 15, 20, 25, otherwise 0.</summary>
        public static int MilestoneGemsOfLevel(int levelIndex)
        {
            if (levelIndex <= 0 || levelIndex % 5 != 0) return 0;
            int slot = levelIndex / 5 - 1;
            return slot < MilestoneGemTable.Length ? MilestoneGemTable[slot] : 0;
        }

        /// <summary>The milestone gems of a first clear as the level JSON states them (<c>rewards.gems.firstClearGuaranteed</c>, fixed: no chance roll any more).</summary>
        public static int MilestoneGems(int firstClearGuaranteed)
        {
            return firstClearGuaranteed > 0 ? firstClearGuaranteed : 0;
        }

        // ---- hero milestone bonus (owner 2026-11-13): paid by the hero selected for the run, first clear of levels 5/10/15/20/25 only ----

        /// <summary>Fixed bonus coins by rarity index 0..4 (Common, Uncommon, Rare, Elite, Legendary): no region factor, no replay cut.</summary>
        public static readonly int[] HeroBonusCoinsByRarity = { 50, 100, 200, 300, 500 };
        /// <summary>Sure bonus gems by rarity index 0..4.</summary>
        public static readonly int[] HeroBonusGemsByRarity = { 0, 1, 2, 3, 5 };
        /// <summary>The one extra gem on top of the sure gems (50 percent chance) by rarity index 0..4: 0 = none, else the amount added when the roll hits (the 2nd/3rd/4th/6th gem).</summary>
        public static readonly int[] HeroBonusChanceGemByRarity = { 0, 1, 1, 1, 1 };
        /// <summary>Chance of the extra gem, percent.</summary>
        public const int HeroBonusChancePercent = 50;

        static int Slot(int rarity)
        {
            return rarity < 0 ? 0 : (rarity > 4 ? 4 : rarity);
        }

        /// <summary>Bonus coins of the hero's rarity on a milestone first clear.</summary>
        public static int HeroBonusCoins(int rarity)
        {
            return HeroBonusCoinsByRarity[Slot(rarity)];
        }

        /// <summary>
        /// Bonus gems of the hero's rarity on a milestone first clear: the sure gems plus, with <see cref="HeroBonusChancePercent"/> percent, one more (the Common hero has none).
        /// The roll is seeded from the level seed and the hero id (FNV-1a), so it is the same on every device and on a server.
        /// </summary>
        public static int HeroBonusGems(int rarity, int levelSeed, string heroId)
        {
            int slot = Slot(rarity);
            int gems = HeroBonusGemsByRarity[slot];
            if (HeroBonusChanceGemByRarity[slot] > 0)
            {
                Rng rng = new Rng(unchecked(levelSeed ^ (int)0x4E5B1C37 ^ StableHash(heroId)));
                if (rng.Next(100) < HeroBonusChancePercent) gems += HeroBonusChanceGemByRarity[slot];
            }
            return gems;
        }

        /// <summary>FNV-1a over the characters of the id (0 for null): a fixed hash, independent of the runtime's string hashing.</summary>
        public static int StableHash(string s)
        {
            uint h = 2166136261u;
            if (s != null) for (int i = 0; i < s.Length; i++) h = unchecked((h ^ s[i]) * 16777619u);
            return unchecked((int)h);
        }
    }
}
