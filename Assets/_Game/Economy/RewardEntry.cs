namespace FluffyClimbers.Economy
{
    /// <summary>What a claim pays: gems, coins and a number of RANDOM power-up units (any of the implemented types, repeats allowed).</summary>
    public struct RewardGrant
    {
        public int Gems, Coins, PowerUps;
        public bool IsEmpty { get { return Gems <= 0 && Coins <= 0 && PowerUps <= 0; } }
    }

    /// <summary>
    /// One reward row: a region goal (see <see cref="RegionGoal"/>), a daily points milestone, a daily action or a weekly task.
    /// Pure data (no engine or JSON types) so the tables can be tested outside Unity.
    /// </summary>
    public class RewardEntry
    {
        public string Id;
        public string Title;
        /// <summary>The counter this entry reads (RewardStat), or null for points milestones. Region goals carry their type token (see RegionGoalTable.Token).</summary>
        public string Stat;
        /// <summary>The counter value (or points) that completes the entry.</summary>
        public int Target;
        /// <summary>Daily actions only: the points the action is worth.</summary>
        public int Points;
        public int Gems, Coins, PowerUps;
        /// <summary>Hero reward (1 copy via ProgressService.AddCopies): 0 none, a region index = that region's Common hero, -1 = one random Common hero of an OPEN region (deterministic, per-account seed).</summary>
        public int HeroRegion;
        /// <summary>Hero reward of ONE named hero (1 copy), for example the Region 1 goals that hand out Bumble, Ribbi, Dandy and Pip (docs/39 section 1 rule 7). Null = none. Wins over <see cref="HeroRegion"/>.</summary>
        public string HeroId;

        public const int RandomOpenRegionHero = -1;

        public bool GivesHero { get { return HeroRegion != 0 || !string.IsNullOrEmpty(HeroId); } }

        public RewardGrant Grant { get { return new RewardGrant { Gems = Gems, Coins = Coins, PowerUps = PowerUps }; } }
    }
}
