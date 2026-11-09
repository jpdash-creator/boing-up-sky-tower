using System;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// One rewarded-ad provider. <see cref="Show"/> calls <c>onFinished(true)</c> only when the ad was watched to the reward point;
    /// every error, no-fill, early close or interruption calls <c>onFinished(false)</c>. The callback runs exactly once, on the main thread.
    /// </summary>
    public interface IRewardedAds
    {
        /// <summary>True when an ad is loaded and <see cref="Show"/> can start at once. Never blocks.</summary>
        bool IsReady { get; }
        /// <summary>Starts loading the next ad (idempotent, non-blocking).</summary>
        void Preload();
        void Show(Action<bool> onFinished);
    }
}
