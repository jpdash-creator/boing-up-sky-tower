using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BoingTower.Editor.Build
{
    /// <summary>Headless Android build: -executeMethod BoingTower.Editor.Build.AndroidBuild.BuildFromCommandLine (output E:/Unity/builds/boingtower.apk).</summary>
    public static class AndroidBuild
    {
        const string OutputPath = "E:/Unity/builds/boingtower.apk";   // always the latest build (the device test kit installs this one)

        // Semantic build version vMAJOR.MINOR.HOTFIX.BUILD (.claude/rules/versioning.md): the first three parts are bumped by hand with
        // Tools/version.py, BUILD is incremented here for every produced APK and never reset. It becomes the app version name, and the
        // Android version code is BUILD, so every install is newer than the one before.
        [Serializable] class VersionInfo { public int major, minor, hotfix, build; }

        static string ProjectRoot { get { return Directory.GetParent(Application.dataPath).FullName; } }
        static string VersionFile { get { return Path.Combine(ProjectRoot, "Tools", "version.json"); } }

        static VersionInfo ReadVersion() { return JsonUtility.FromJson<VersionInfo>(File.ReadAllText(VersionFile)); }

        static void WriteVersion(VersionInfo v) { File.WriteAllText(VersionFile, JsonUtility.ToJson(v, true) + "\n"); }

        [MenuItem("Tools/Boing Tower/Build Android APK")]
        public static void BuildFromMenu() { Build(); }

        public static void BuildFromCommandLine()
        {
            EditorApplication.Exit(Build() ? 0 : 1);
        }

        static bool Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            VersionInfo ver = ReadVersion();
            ver.build++;
            WriteVersion(ver);
            string versionName = ver.major + "." + ver.minor + "." + ver.hotfix + "." + ver.build;
            PlayerSettings.bundleVersion = versionName;
            PlayerSettings.Android.bundleVersionCode = ver.build;
            string versionedPath = "E:/Unity/builds/boingtower_v" + versionName + ".apk";
            Debug.Log("[BUILD] version v" + versionName);
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Boot.unity", "Assets/Scenes/Meta.unity", "Assets/Scenes/Run.unity" },
                locationPathName = versionedPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development   // development build: on-device profiler and logcat while we tune
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[BUILD] result " + report.summary.result + ", size " + (report.summary.totalSize / (1024 * 1024)) + " MB, errors " + report.summary.totalErrors);
            bool ok = report.summary.result == BuildResult.Succeeded;
            if (ok) File.Copy(versionedPath, OutputPath, true);
            string log = Path.Combine(ProjectRoot, "Content", "builds", "build_log.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(log));
            if (!File.Exists(log)) File.WriteAllText(log, "date,version,result,sizeMB,apk\n");
            File.AppendAllText(log, DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ",v" + versionName + "," + report.summary.result + "," + (report.summary.totalSize / (1024 * 1024)) + "," + versionedPath + "\n");
            return ok;
        }
    }
}
