using System;

namespace FluffyClimbers.Monetization
{
    /// <summary>Rewarded-ad service. See AdPlacements for the placement names.</summary>
    public interface IAdService
    {
        /// <summary>True when an ad for this placement can start at once. Never blocks.</summary>
        bool IsReady(string placement);

        /// <summary>
        /// Shows a rewarded ad. onDone runs exactly once on the main thread: true only when the reward was earned;
        /// false on no-fill, error, early close or if an ad is already showing.
        /// </summary>
        void ShowRewarded(string placement, Action<bool> onDone);
    }

    /// <summary>Implemented by an ad service that needs the GDPR/consent flag (Services.ConsentGiven).</summary>
    public interface IConsentAware
    {
        void SetConsent(bool consent);
    }
}
