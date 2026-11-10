namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// One jingle per burst (owner decision): a jingle-like sound plays only when at least <c>cooldown</c> seconds passed since the last one,
    /// so doing many merges or claims in a row never repeats or stacks it. Same rule as RewardsScreen.WinSoundCooldown. Pure and allocation free.
    /// </summary>
    public static class SoundCooldown
    {
        /// <summary>Seconds after one merge jingle in which further merges stay quiet (the visual effect and the save still happen).</summary>
        public const float MergeCooldown = 2.5f;

        /// <summary>True when the sound may play now (<paramref name="now"/> and <paramref name="last"/> in unscaled seconds).</summary>
        public static bool ShouldPlay(float now, float last, float cooldown) { return now - last >= cooldown; }
    }
}
