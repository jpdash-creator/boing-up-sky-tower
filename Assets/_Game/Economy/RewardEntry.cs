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

        /// <summary>
        /// Hero-or-coins gift (owner 2026-11-13): with <see cref="HeroRegion"/> = <see cref="RandomOpenRegionHero"/> and a value above 0, the claim rolls
        /// <see cref="HeroOrCoinsPercent"/> percent for the random Common hero and otherwise pays these coins instead. The roll is deterministic and stored in the save
        /// (ProgressService.HeroOrCoinsWins). 0 = the hero is certain (or there is no hero).
        /// </summary>
        public int HeroOrCoins;

        public const int RandomOpenRegionHero = -1;
        /// <summary>Chance (percent) of the hero in a hero-or-coins gift.</summary>
        public const int HeroOrCoinsPercent = 50;

        /// <summary>True for the 50 percent hero, otherwise coins gifts.</summary>
        public bool IsHeroOrCoins { get { return HeroRegion == RandomOpenRegionHero && HeroOrCoins > 0; } }

        public bool GivesHero { get { return HeroRegion != 0 || !string.IsNullOrEmpty(HeroId); } }

        public RewardGrant Grant { get { return new RewardGrant { Gems = Gems, Coins = Coins, PowerUps = PowerUps }; } }
    }
}
