using UnityEngine;

namespace SkyHopper.Gameplay
{
    /// <summary>
    /// One place for frame pacing (CUX-1). Mobile builds default to 30 fps unless the target is set explicitly, so this runs on boot.
    /// The simulation is fixed at 60 Hz and the view interpolates, so higher display rates also look smooth.
    /// </summary>
    public static class PerformanceSettings
    {
        public const int TargetFps = 60;
        static bool applied;

        public static void Apply()
        {
            if (applied) return;
            applied = true;
            QualitySettings.vSyncCount = 0;                 // the target frame rate paces the loop
            Application.targetFrameRate = TargetFps;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;  // never dim the screen mid-run
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToPortrait = true;
        }
    }
}
