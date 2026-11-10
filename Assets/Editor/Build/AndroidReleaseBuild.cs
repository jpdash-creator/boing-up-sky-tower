using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Debug = UnityEngine.Debug;

namespace FluffyClimbers.Editor.Build
{
    /// <summary>
    /// Google Play RELEASE build: a signed Android App Bundle (.aab), non-development, no AltTester, no test scenes.
    /// Menu: Tools/Fluffy Climbers: Tower Jump/Build Release AAB.   Command line: -executeMethod FluffyClimbers.Editor.Build.AndroidReleaseBuild.BuildReleaseFromCommandLine
    /// (exit code 0 = ok, 1 = failed). Output: E:/Unity/builds/fluffyclimbers_v(M.m.b).aab plus the copy E:/Unity/builds/fluffyclimbers.aab.
    ///
    /// The upload keystore lives OUTSIDE the repo (KeystoreFolder). Its secrets are read from the .properties file next to it only while the build runs and are
    /// set only in memory (PlayerSettings.Android.keystorePass / keyaliasPass, which Unity does not serialize); they are cleared again afterwards and are
    /// never logged. Versioning and the build log are the same code as the APK build (AndroidBuild.BumpAndApplyVersion / AppendBuildLog).
    /// Every editor build setting this class changes is restored at the end, so the development APK build keeps working.
    /// The Gradle hooks (AdMob app id and permissions, short label, no backup) are IPostGenerateGradleAndroidProject and run for every Android build, so they apply here too.
    /// </summary>
    public static class AndroidReleaseBuild
    {
        const string KeystoreFolder = "E:/Unity/keys";
        const string DefaultKeystoreFile = "fluffyclimbers-upload.keystore";
        const string PropertiesFile = "fluffyclimbers-upload.properties";
        const string ExpectedAlias = "fluffyclimbers-upload";
        const string BuildsFolder = "E:/Unity/builds";
        const string LatestAab = "E:/Unity/builds/fluffyclimbers.aab";
        const string Tag = "[ReleaseBuild] ";

        // Same three scenes as the APK build (AndroidBuild). Nothing else ever goes into the release.
        static readonly string[] Scenes = { "Assets/Scenes/Boot.unity", "Assets/Scenes/Meta.unity", "Assets/Scenes/Run.unity" };
        static readonly Regex TestSceneName = new Regex("test|sandbox|debug|autopilot|alttester|scratch", RegexOptions.IgnoreCase);

        [MenuItem("Tools/Fluffy Climbers: Tower Jump/Build Release AAB")]
        public static void BuildReleaseFromMenu() { BuildRelease(); }

        public static void BuildReleaseFromCommandLine()
        {
            EditorApplication.Exit(BuildRelease() ? 0 : 1);
        }

        /// <summary>The settings this build changes, so they can be put back.</summary>
        sealed class SavedSettings
        {
            public bool BuildAppBundle, Development, AllowDebugging, ConnectProfiler, UseCustomKeystore;
            public string KeystoreName, KeyaliasName;
            public ScriptingImplementation Backend;
            public AndroidArchitecture Architectures;
            public UIOrientation Orientation;

            public static SavedSettings Capture()
            {
                return new SavedSettings
                {
                    BuildAppBundle = EditorUserBuildSettings.buildAppBundle,
                    Development = EditorUserBuildSettings.development,
                    AllowDebugging = EditorUserBuildSettings.allowDebugging,
                    ConnectProfiler = EditorUserBuildSettings.connectProfiler,
                    UseCustomKeystore = PlayerSettings.Android.useCustomKeystore,
                    KeystoreName = PlayerSettings.Android.keystoreName,
                    KeyaliasName = PlayerSettings.Android.keyaliasName,
                    Backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android),
                    Architectures = PlayerSettings.Android.targetArchitectures,
                    Orientation = PlayerSettings.defaultInterfaceOrientation
                };
            }

            public void Restore()
            {
                // Passwords first: they only ever lived in memory and must not stay in the editor session.
                PlayerSettings.Android.keystorePass = "";
                PlayerSettings.Android.keyaliasPass = "";
                PlayerSettings.Android.useCustomKeystore = UseCustomKeystore;
                PlayerSettings.Android.keystoreName = KeystoreName;
                PlayerSettings.Android.keyaliasName = KeyaliasName;
                EditorUserBuildSettings.buildAppBundle = BuildAppBundle;
                EditorUserBuildSettings.development = Development;
                EditorUserBuildSettings.allowDebugging = AllowDebugging;
                EditorUserBuildSettings.connectProfiler = ConnectProfiler;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, Backend);
                PlayerSettings.Android.targetArchitectures = Architectures;
                PlayerSettings.defaultInterfaceOrientation = Orientation;
            }
        }

        static bool BuildRelease()
        {
            SavedSettings saved = null;
            try
            {
                // ---- 1. checks that must pass BEFORE the build number is used up ----
                if (AltTesterBuildHook.Requested())
                    return Fail("AltTester was requested (-altTester or BT_ALTTESTER=1). A release build never contains AltTester. Unset it and run again.");
                string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android) ?? "";
                if (Regex.IsMatch(defines, @"(^|;)(" + AltTesterBuildHook.Symbol + @"|DEVELOPMENT_BUILD)(;|$)"))
                    return Fail("The Android scripting define symbols contain a test symbol (" + defines + "). Remove it in Player Settings first.");
                if (!CheckScenes()) return false;
                string adProblem = AdMobBuildConfig.ReleaseProblem();   // placeholder AdMob app id = no release (docs/33)
                if (adProblem != null) return Fail(adProblem);
                string storeProblem = StoreConfigPreflight.Check();   // Play Games project id, Play licence key, Games SDK resolved (docs/33, docs/35)
                if (storeProblem != null) return Fail(storeProblem);

                string keystorePath, alias, storePass, keyPass;
                if (!ReadSigning(out keystorePath, out alias, out storePass, out keyPass)) return false;
                if (!VerifyKeystoreWithKeytool(keystorePath, alias, storePass)) return false;

                // ---- 2. version (same code as the APK build) ----
                Directory.CreateDirectory(BuildsFolder);
                string versionName = AndroidBuild.BumpAndApplyVersion();
                string versionedPath = BuildsFolder + "/fluffyclimbers_v" + versionName + ".aab";
                Debug.Log(Tag + "version v" + versionName + " (version code " + PlayerSettings.Android.bundleVersionCode + "), output " + versionedPath);

                // ---- 3. release settings (restored in finally) ----
                saved = SavedSettings.Capture();
                EditorUserBuildSettings.buildAppBundle = true;
                EditorUserBuildSettings.development = false;
                EditorUserBuildSettings.allowDebugging = false;
                EditorUserBuildSettings.connectProfiler = false;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;   // as the APK build: arm64-v8a only (Google Play needs 64-bit; BT_X86 is ignored here)
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystorePath;
                PlayerSettings.Android.keyaliasName = alias;
                PlayerSettings.Android.keystorePass = storePass;    // memory only, never serialized, never logged
                PlayerSettings.Android.keyaliasPass = keyPass;
                storePass = null; keyPass = null;

                Debug.Log(Tag + "managed stripping level (unchanged project setting): " + PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Android)
                          + ", engine code stripping: " + PlayerSettings.stripEngineCode);

                var options = new BuildPlayerOptions
                {
                    scenes = Scenes,
                    locationPathName = versionedPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.None   // NOT Development, no AllowDebugging, no profiler connection
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);   // AltTesterBuildHook.Begin is never called, so its scene hook stays inactive

                // ---- 4. result, log row, post-build assertion ----
                Debug.Log(Tag + "result " + report.summary.result + ", size " + (report.summary.totalSize / (1024 * 1024)) + " MB, errors " + report.summary.totalErrors);
                AndroidBuild.AppendBuildLog(versionName, report, versionedPath);
                bool ok = report.summary.result == BuildResult.Succeeded;
                bool devFlag = (report.summary.options & BuildOptions.Development) != 0;
                Debug.Log(Tag + "effective flags: development=" + devFlag + " (editor development=" + EditorUserBuildSettings.development + "), buildAppBundle="
                          + EditorUserBuildSettings.buildAppBundle + ", allowDebugging=" + EditorUserBuildSettings.allowDebugging + ", connectProfiler="
                          + EditorUserBuildSettings.connectProfiler + ", useCustomKeystore=" + PlayerSettings.Android.useCustomKeystore + ", keystore=" + keystorePath
                          + ", alias=" + alias + ", architectures=" + PlayerSettings.Android.targetArchitectures + ", scripting=" + PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)
                          + ", scenes=" + string.Join(",", Scenes));
                if (devFlag) return Fail("post-build assertion failed: the build report says Development. Do not upload this file.");
                if (!EditorUserBuildSettings.buildAppBundle) return Fail("post-build assertion failed: buildAppBundle is false.");
                if (!ok) return Fail("the Unity build failed (see the errors above). No file was copied.");
                if (!File.Exists(versionedPath) || new FileInfo(versionedPath).Length == 0) return Fail("the build reported success but " + versionedPath + " is missing or empty.");
                File.Copy(versionedPath, LatestAab, true);
                Debug.Log(Tag + "OK: " + versionedPath + " (copy: " + LatestAab + "), " + (new FileInfo(versionedPath).Length / (1024 * 1024)) + " MB. Upload the versioned file to Google Play.");
                return true;
            }
            catch (Exception e)
            {
                // Never print the exception text of the signing setup: it could quote a setting. Type and stack of our own code are enough.
                return Fail("unexpected " + e.GetType().Name + ": " + (e is IOException || e is InvalidOperationException ? e.Message : "see the editor log") + "\n" + e.StackTrace);
            }
            finally
            {
                if (saved != null) saved.Restore();
            }
        }

        static bool Fail(string message)
        {
            Debug.LogError(Tag + message);
            return false;
        }

        /// <summary>Fails when a scene that is not one of the three game scenes is in the build, or when any scene name looks like a test scene.</summary>
        static bool CheckScenes()
        {
            foreach (string scene in Scenes)
            {
                if (!File.Exists(scene)) return Fail("scene missing: " + scene);
                if (TestSceneName.IsMatch(Path.GetFileNameWithoutExtension(scene))) return Fail("a test scene is in the release scene list: " + scene);
            }
            // The release passes its own list, but the Build Settings list is checked too so a test scene there is noticed.
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (!s.enabled) continue;
                if (TestSceneName.IsMatch(Path.GetFileNameWithoutExtension(s.path))) return Fail("a test scene is enabled in Build Settings: " + s.path + ". Disable it first.");
                if (Array.IndexOf(Scenes, s.path) < 0) return Fail("Build Settings has an enabled scene that is not part of the release: " + s.path);
            }
            return true;
        }

        /// <summary>Reads keystore path, alias and the two passwords from the properties file (simple key=value lines). Logs only which problem was found, never a value.</summary>
        static bool ReadSigning(out string keystorePath, out string alias, out string storePass, out string keyPass)
        {
            keystorePath = alias = storePass = keyPass = null;
            string propsPath = KeystoreFolder + "/" + PropertiesFile;
            if (!File.Exists(propsPath)) return Fail("signing properties file not found: " + propsPath);
            var map = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in File.ReadAllLines(propsPath))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == '!') continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                map[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            string ks;
            map.TryGetValue("keystore", out ks);
            map.TryGetValue("alias", out alias);
            map.TryGetValue("storepass", out storePass);
            map.TryGetValue("keypass", out keyPass);
            if (string.IsNullOrEmpty(ks)) ks = KeystoreFolder + "/" + DefaultKeystoreFile;
            else if (!Path.IsPathRooted(ks)) ks = Path.Combine(KeystoreFolder, ks);
            keystorePath = Path.GetFullPath(ks).Replace('\\', '/');
            if (!File.Exists(keystorePath)) return Fail("keystore file not found: " + keystorePath);
            if (string.IsNullOrEmpty(alias)) return Fail("the properties file has no 'alias' entry.");
            if (alias != ExpectedAlias) return Fail("the properties file names alias '" + alias + "', expected '" + ExpectedAlias + "'.");
            if (string.IsNullOrEmpty(storePass)) return Fail("the properties file has no 'storepass' entry.");
            if (string.IsNullOrEmpty(keyPass)) return Fail("the properties file has no 'keypass' entry.");
            return true;
        }

        /// <summary>
        /// Opens the keystore with keytool (the JDK that ships with Unity) to prove the alias exists and the store password is right. The password is handed over through
        /// an environment variable of the child process, never on the command line. If keytool cannot be found the check is skipped with a warning (Gradle signing still fails loudly).
        /// </summary>
        static bool VerifyKeystoreWithKeytool(string keystorePath, string alias, string storePass)
        {
            string keytool = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer", "OpenJDK", "bin", "keytool.exe");
            if (!File.Exists(keytool)) keytool = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer", "OpenJDK", "bin", "keytool");
            if (!File.Exists(keytool))
            {
                Debug.LogWarning(Tag + "keytool not found under the Unity Android OpenJDK, alias check skipped.");
                return true;
            }
            var psi = new ProcessStartInfo(keytool, "-list -keystore \"" + keystorePath + "\" -alias " + alias + " -storepass:env FC_RELEASE_STOREPASS")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.EnvironmentVariables["FC_RELEASE_STOREPASS"] = storePass;
            using (Process p = Process.Start(psi))
            {
                p.StandardOutput.ReadToEnd();
                string err = p.StandardError.ReadToEnd();
                if (!p.WaitForExit(60000)) { try { p.Kill(); } catch (Exception) { } return Fail("keytool did not finish."); }
                if (p.ExitCode != 0)
                    return Fail("keytool could not open alias '" + alias + "' in " + keystorePath + " (wrong alias or wrong store password). keytool said: " + (err ?? "").Replace(storePass, "***").Trim());
            }
            Debug.Log(Tag + "keystore opened, alias '" + alias + "' exists.");
            return true;
        }
    }
}
