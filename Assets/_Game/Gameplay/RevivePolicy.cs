namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// Revive schedule of ONE run (owner list 2026-11-02, item 4; new state for every run, see docs/32-revive-and-ads.md). Pure C#, no Unity types.
    /// Ad revives: the 1st is a 30 s ad, the 2nd a 60 s ad, after 2 ads there are no more ads in the run.
    /// Gem revives: the 1st gem revive costs 5, every later one 10 (it stays 10). Ads and gems are counted separately, so an ad never makes gems dearer.
    /// </summary>
    public sealed class RevivePolicy
    {
        public const int MaxAds = 2;
        public const int FirstAdSeconds = 30, SecondAdSeconds = 60;
        public const int FirstGemCost = 5, LaterGemCost = 10;
        public const string PlacementShort = "revive_30", PlacementLong = "revive_60";

        public int AdsUsed { get; private set; }
        public int GemRevivesUsed { get; private set; }

        public int AdsLeft { get { return MaxAds - AdsUsed; } }
        public bool AdAvailable { get { return AdsLeft > 0; } }
        /// <summary>Length of the next ad in seconds (30, then 60); 0 when no ad is left.</summary>
        public int NextAdSeconds { get { return AdSecondsFor(AdsUsed); } }
        /// <summary>Ad placement id for the ad service ("revive_30" / "revive_60"); null when no ad is left.</summary>
        public string NextAdPlacement { get { return PlacementFor(AdsUsed); } }
        /// <summary>Price in gems of the next gem revive (5 for the first, 10 after).</summary>
        public int NextGemCost { get { return GemCostFor(GemRevivesUsed); } }

        public static int AdSecondsFor(int adsUsed) { return adsUsed <= 0 ? FirstAdSeconds : (adsUsed == 1 ? SecondAdSeconds : 0); }
        public static string PlacementFor(int adsUsed) { return adsUsed <= 0 ? PlacementShort : (adsUsed == 1 ? PlacementLong : null); }
        public static int GemCostFor(int gemRevivesUsed) { return gemRevivesUsed <= 0 ? FirstGemCost : LaterGemCost; }

        /// <summary>Button text of the ad option: "Watch Ad" (no ad length, owner 2026-11-10); empty when no ad is left.</summary>
        public const string AdButtonText = "Watch Ad";
        public static string AdLabelFor(int adsUsed) { return adsUsed < MaxAds ? AdButtonText : ""; }
        public string AdLabel { get { return AdLabelFor(AdsUsed); } }

        /// <summary>An ad revive happened (the ad was watched and the player was revived).</summary>
        public void RecordAd() { if (AdsUsed < MaxAds) AdsUsed++; }

        /// <summary>A gem revive happened (the gems were paid).</summary>
        public void RecordGem() { GemRevivesUsed++; }
    }
}
