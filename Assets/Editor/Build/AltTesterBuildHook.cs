using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using AltTester.AltTesterSDK.Driver;
using AltTester.AltTesterUnitySDK.Commands;

namespace BoingUpSkyTower.Editor.Build
{
    /// <summary>
    /// Opt-in AltTester build (roadmap T26). OFF by default: a normal build contains nothing of AltTester.
    /// Turn it on with the command-line argument -altTester or the environment variable BT_ALTTESTER=1.
    /// While it is on, AndroidBuild defines the scripting symbol ALTTESTER for this build only (BuildPlayerOptions.extraScriptingDefines,
    /// nothing is written to the player settings) and this class puts the AltTester runner prefab into the Boot scene IN MEMORY while the
    /// scene is processed for the build, so the scene file on disk is never touched. Development builds only.
    /// </summary>
    public class AltTesterBuildHook : IProcessSceneWithReport
    {
        public const string EnvVar = "BT_ALTTESTER";
        public const string CommandLineFlag = "-altTester";
        public const string Symbol = "ALTTESTER";
        /// <summary>The port the game connects to on the PC (AltTester Desktop listens on it); the driver uses the same port.</summary>
        public const int ServerPort = 13000;
        public const string ServerHost = "127.0.0.1";   // reached through "adb reverse tcp:13000 tcp:13000"
        public const string AppName = "BoingUpSkyTower";
        const string RunnerPrefabPath = "Packages/com.alttester.sdk/Runtime/Prefab/AltTesterPrefab.prefab";
        const string BootSceneName = "Boot";

        static bool active;
        static string connectionUid = "";

        public int callbackOrder { get { return 0; } }

        /// <summary>True when the owner asked for an AltTester build (argument -altTester or BT_ALTTESTER=1).</summary>
        public static bool Requested()
        {
            foreach (string arg in Environment.GetCommandLineArgs())
                if (string.Equals(arg, CommandLineFlag, StringComparison.OrdinalIgnoreCase)) return true;
            string env = Environment.GetEnvironmentVariable(EnvVar);
            return env == "1" || string.Equals(env, "true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Call right before BuildPipeline.BuildPlayer. Refuses anything but a development build.</summary>
        public static void Begin(ref BuildPlayerOptions options)
        {
            if ((options.options & BuildOptions.Development) == 0)
                throw new InvalidOperationException("[ALTTESTER] refused: AltTester is for development builds only, never a release build.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RunnerPrefabPath) == null)
                throw new InvalidOperationException("[ALTTESTER] runner prefab not found at " + RunnerPrefabPath + " (is com.alttester.sdk in Packages/manifest.json?)");
            string[] defines = options.extraScriptingDefines ?? new string[0];
            string[] withSymbol = new string[defines.Length + 1];
            Array.Copy(defines, withSymbol, defines.Length);
            withSymbol[defines.Length] = Symbol;
            options.extraScriptingDefines = withSymbol;
            // A fresh UID per build makes the device take host, port and app name from the build instead of an older saved value.
            connectionUid = Guid.NewGuid().ToString("N");
            active = true;
            Debug.Log("[ALTTESTER] on: runner goes into scene " + BootSceneName + " (memory only), server " + ServerHost + ":" + ServerPort + ", app name " + AppName);
        }

        /// <summary>Call after BuildPlayer, also when it failed.</summary>
        public static void End()
        {
            active = false;
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (!active || report == null) return;                          // not an AltTester build (or an editor play-mode pass)
            if (scene.name != BootSceneName) return;
            if ((report.summary.options & BuildOptions.Development) == 0) return;   // never in a release build
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RunnerPrefabPath);
            if (prefab == null) { Debug.LogError("[ALTTESTER] runner prefab missing, scene left untouched"); return; }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            AltRunner runner = instance.GetComponent<AltRunner>();
            if (runner == null) { Debug.LogError("[ALTTESTER] prefab has no AltRunner component"); return; }
            runner.InstrumentationSettings = new AltInstrumentationSettings
            {
                AltServerHost = ServerHost,
                AltServerPort = ServerPort,
                AppName = AppName,
                ResetConnectionData = true,
                UID = connectionUid,
                hideGreenPopup = true
            };
            Debug.Log("[ALTTESTER] runner added to scene " + scene.path + " for this build only");
        }
    }
}
