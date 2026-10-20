using UnityEngine;

namespace SkyHopper.Gameplay
{
    /// <summary>
    /// Development-only "real player": a component that feeds the Run scene through the same input path as the touch pad
    /// (RunInputReader.ReadTick, one input word per simulation tick). The decisions come from AutopilotBrain (planner + reaction lag).
    /// Works only in a development build on a device (GameSettings.DevBuild) or when a test sets ForceEnable in the editor.
    /// </summary>
    public sealed class Autopilot : MonoBehaviour, IInputSource
    {
        /// <summary>Editor / test switch. Ignored unless this is the editor or a development build, so a release build can never be driven.</summary>
        public static bool ForceEnable;

        /// <summary>True while an AutopilotSuite is running (the player's save is swapped for an in-memory one for the whole time).</summary>
        public static bool SuiteActive;

        public static bool Allowed
        {
            get { return GameSettings.DevBuild || (ForceEnable && (Application.isEditor || Debug.isDebugBuild)); }
        }

        public AutopilotBrain Brain { get; private set; }
        RunController controller;

        /// <summary>Attaches an autopilot to a started Run scene (rc.Input must exist, which is the case after RunController.Start). Null when not allowed.</summary>
        public static Autopilot Attach(RunController rc, float skill, int seed, int depth)
        {
            if (!Allowed || rc == null || rc.Input == null) return null;
            Autopilot ap = rc.GetComponent<Autopilot>();
            if (ap == null) ap = rc.gameObject.AddComponent<Autopilot>();
            ap.controller = rc;
            ap.Brain = new AutopilotBrain(skill, seed, depth);
            rc.Input.Source = ap;
            return ap;
        }

        /// <summary>Called by RunInputReader.ReadTick once per simulation tick.</summary>
        public int ReadTick()
        {
            if (controller == null || controller.Session == null || Brain == null) return 0;
            return Brain.Next(controller.Session);
        }

        void OnDestroy()
        {
            if (controller != null && controller.Input != null && ReferenceEquals(controller.Input.Source, this)) controller.Input.Source = null;
        }
    }
}
