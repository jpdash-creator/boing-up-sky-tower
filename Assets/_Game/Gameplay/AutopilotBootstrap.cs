using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkyHopper.Gameplay
{
    /// <summary>
    /// Development-only start of the autoplay: while the game sits in the Meta scene it looks once a second for
    /// persistentDataPath/autoplay.json (the host pushes it with adb, see Tools/android_test/autoplay_r1.py) and hands the options to an
    /// AutopilotSuite. The file may be there already at launch or arrive later. Does nothing in release builds and in the editor.
    /// </summary>
    public sealed class AutopilotBootstrap : MonoBehaviour
    {
        public const string FlagFileName = "autoplay.json";
        const float PollSeconds = 1f;
        const float MetaWaitSeconds = 30f;

        float nextPoll;
        bool probed;
        long consumedStamp = -1;
        AutopilotSuite suite;

        public static string FlagPath { get { return Path.Combine(Application.persistentDataPath, FlagFileName); } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!Autopilot.Allowed) return;
            var go = new GameObject("AutopilotBootstrap");
            DontDestroyOnLoad(go);
            go.AddComponent<AutopilotBootstrap>();
        }

        void Update()
        {
            if (suite != null) return;   // a suite is running (it removes itself when done)
            float now = Time.realtimeSinceStartup;
            if (now < nextPoll) return;
            nextPoll = now + PollSeconds;

            string path = FlagPath;
            bool exists;
            long stamp = 0;
            try
            {
                exists = File.Exists(path);
                if (exists) stamp = File.GetLastWriteTimeUtc(path).Ticks;
            }
            catch (Exception) { return; }
            if (!probed)
            {
                probed = true;
                // One line per launch: tells the host script where persistentDataPath is on this device.
                Debug.Log("BTAUTO {\"ev\":\"probe\",\"path\":\"" + AutopilotReport.Escape(path) + "\",\"exists\":" + (exists ? "true" : "false") + "}");
            }
            if (!exists || stamp == consumedStamp) return;   // no file, or one the suite could not delete after it ran
            bool metaReady = GameSession.Initialized && SceneManager.GetActiveScene().name == "Meta";
            if (!metaReady && now < MetaWaitSeconds) return;

            consumedStamp = stamp;
            AutoplayOptions options;
            try
            {
                options = AutoplayOptions.Parse(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                AutopilotReport.Emit("{\"ev\":\"error\",\"msg\":\"bad autoplay.json: " + AutopilotReport.Escape(e.Message) + "\"}");
                Debug.Log("BTAUTO done");
                try { File.Delete(path); } catch (Exception) { }
                return;
            }
            suite = gameObject.AddComponent<AutopilotSuite>();
            suite.Begin(options, path);
        }
    }
}
