using System;
using UnityEngine;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// Editor and development-build rewarded ads: grants at once and logs. Tests can switch availability, force a failure or
    /// hold the callback (<see cref="Deferred"/>) to simulate an ad that is still playing.
    /// </summary>
    public sealed class TestRewardedAds : IRewardedAds
    {
        public bool Available = true;
        public bool GrantResult = true;
        public bool Deferred;
        public bool Log = true;
        public int ShowCount;
        Action<bool> pending;

        public bool IsReady { get { return Available && pending == null; } }
        public void Preload() { }

        public void Show(Action<bool> onFinished)
        {
            if (!IsReady) { if (onFinished != null) onFinished(false); return; }
            ShowCount++;
            if (Log) Debug.Log("[Ads] TestRewardedAds: simulated rewarded ad (" + (GrantResult ? "reward granted" : "no reward") + ")");
            if (Deferred) { pending = onFinished; return; }
            if (onFinished != null) onFinished(GrantResult);
        }

        /// <summary>Finishes a deferred ad.</summary>
        public void Complete(bool granted)
        {
            Action<bool> cb = pending;
            pending = null;
            if (cb != null) cb(granted);
        }
    }

    /// <summary>Used when no ad provider exists (a device build without the SDK): never ready, so only the gem option is offered.</summary>
    public sealed class NoRewardedAds : IRewardedAds
    {
        public bool IsReady { get { return false; } }
        public void Preload() { }
        public void Show(Action<bool> onFinished) { if (onFinished != null) onFinished(false); }
    }
}
