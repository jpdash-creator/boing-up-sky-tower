using System;
using UnityEngine;

namespace FluffyClimbers.Monetization
{
    /// <summary>
    /// Editor/dev ad service used when the AdMob SDK is not compiled in or in the editor: always ready, completes true after DelaySeconds
    /// (unscaled time, driven by a short-lived hidden pump, so nothing runs while idle).
    /// </summary>
    public sealed class StubAdService : IAdService
    {
        public float DelaySeconds = 1f;
        public bool Ready = true;
        /// <summary>Tests set this to complete at once without a MonoBehaviour pump.</summary>
        public bool Immediate;
        public int ShowCount { get; private set; }
        bool showing;

        sealed class Pump : MonoBehaviour
        {
            public float Left;
            public Action Done;
            void Update()
            {
                Left -= Time.unscaledDeltaTime;
                if (Left > 0f) return;
                Action d = Done;
                Done = null;
                Destroy(gameObject);
                if (d != null) d();
            }
        }

        public bool IsReady(string placement) { return Ready && !showing; }

        public void ShowRewarded(string placement, Action<bool> onDone)
        {
            if (!IsReady(placement)) { if (onDone != null) onDone(false); return; }
            ShowCount++;
            showing = true;
            Action finish = () => { showing = false; if (onDone != null) onDone(true); };
            if (Immediate || !Application.isPlaying) { finish(); return; }
            var go = new GameObject("StubAdPump");
            UnityEngine.Object.DontDestroyOnLoad(go);
            Pump p = go.AddComponent<Pump>();
            p.Left = DelaySeconds;
            p.Done = finish;
        }
    }
}
