namespace FluffyClimbers.Economy
{
    /// <summary>Wallet stub for the linked RunSession (the real ProgressService is Unity-side): gems and power-up stock counters only.</summary>
    public sealed class ProgressService
    {
        public int Gems = 1000;
        public bool TrySpendGems(int n) { if (Gems < n) return false; Gems -= n; return true; }
        public bool TryConsumePowerUp(string id, int n) { return true; }
    }

    /// <summary>Field list of the real RunOutcome (Rewards.cs) that RunSession.BuildOutcome fills; the tests never call BuildOutcome.</summary>
    public struct RunOutcome
    {
        public bool Completed;
        public int StarMask, Score, Mode, CoinsCollected, GemsCollected, HiddenGems, GemPickupsTotal, GemPickupsTaken, ComboChains, BestCombo, PowerUpsUsed, Region, Jumps;
        public int Floors;
        public string HeroId;
    }
}
