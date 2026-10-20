using UnityEngine;

namespace SkyHopper.Gameplay
{
    /// <summary>Haptic feedback behind the player's setting (CUX-6). Android fine-grained patterns arrive with the device build (T11).</summary>
    public static class Haptics
    {
        public static int Pulses;   // test hook: counts requested pulses

        public static void Pulse()
        {
            if (!GameSettings.Haptics) return;
            Pulses++;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
    }
}
