using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using FluffyClimbers.Monetization;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FluffyClimbers.Editor.Build
{
    /// <summary>
    /// Play Games Services Android setup, done by the build so nobody has to click through the plugin's setup window (Google guide:
    /// developer.android.com/games/pgs/android/android-signin, "New client integration").
    ///
    /// The guide wants, inside the application: meta-data com.google.android.gms.games.APP_ID with android:value="@string/game_services_project_id", and a string resource
    /// game_services_project_id (translatable=false) holding the Play Games Services "Project ID". The plugin (com.google.play.games v2) keeps its manifest as the Android library
    /// Assets/Plugins/Android/GooglePlayGamesManifest.androidlib and writes the id into it as a literal that stays EMPTY ("\u003") until its setup window is used, which is a malformed
    /// value. Before every Android build this step (IPreprocessBuildWithReport, so before Unity copies the library into the Gradle project):
    ///   1. reads the id from Assets/Resources/Services/play_games_config.json (projectId; the placeholder stays a placeholder),
    ///   2. rewrites only the APP_ID meta-data of the library manifest to the @string reference (every other entry the plugin generated is left untouched),
    ///   3. writes res/values/strings.xml of that library with the id,
    ///   4. sets PlayGamesSettings.asset mAppId (the runtime asset of the plugin) and the plugin's "setup done" flag so its post-build dialog does not nag,
    ///   5. makes sure the External Dependency Manager resolved the Games v2 SDK (see <see cref="EnsureDependenciesResolved"/>).
    /// A placeholder id still builds a development APK (the game then never initialises the Play Games SDK: PlayGamesConfig.IsConfigured is false); the release build refuses it
    /// (StoreConfigPreflight).
    /// </summary>
    public sealed class PlayGamesAndroidSetup : IPreprocessBuildWithReport
    {
        public const string LibraryDir = "Assets/Plugins/Android/GooglePlayGamesManifest.androidlib";
        public const string ConfigPath = "Assets/Resources/Services/play_games_config.json";
        public const string SettingsAssetPath = "Assets/GooglePlayGames/Resources/PlayGamesSettings.asset";
        public const string PluginSettingsFile = "ProjectSettings/GooglePlayGameSettings.txt";
        public const string StringName = "game_services_project_id";
        const string Tag = "[PlayGames] ";

        public int callbackOrder { get { return 0; } }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            try
            {
                Apply();
                EnsureDependenciesResolved();
            }
            catch (Exception e) { Debug.LogWarning(Tag + "setup step failed (the build goes on): " + e.Message); }
        }

        /// <summary>The project id from the config file (placeholder when the file is missing or unreadable).</summary>
        public static string ReadProjectId()
        {
            return File.Exists(ConfigPath) ? PlayGamesConfig.ParseProjectId(File.ReadAllText(ConfigPath)) : PlayGamesConfig.Placeholder;
        }

        /// <summary>Pure: the manifest text with the APP_ID meta-data pointing at the string resource (added before &lt;/application&gt; when it is missing). Idempotent.</summary>
        public static string RewriteManifest(string manifest)
        {
            var rx = new Regex("(<meta-data\\s+android:name=\"com\\.google\\.android\\.gms\\.games\\.APP_ID\"\\s+android:value=\")[^\"]*(\")");
            string reference = "@string/" + StringName;
            if (rx.IsMatch(manifest)) return rx.Replace(manifest, "${1}" + reference + "${2}", 1);
            string meta = "    <meta-data android:name=\"com.google.android.gms.games.APP_ID\"\n            android:value=\"" + reference + "\" />\n    ";
            int i = manifest.IndexOf("</application>", StringComparison.Ordinal);
            return i >= 0 ? manifest.Insert(i, meta) : manifest;
        }

        /// <summary>Pure: res/values/strings.xml text holding the project id.</summary>
        public static string StringsXml(string projectId)
        {
            string v = (projectId ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<resources>\n    <string translatable=\"false\" name=\"" + StringName + "\">" + v + "</string>\n</resources>\n";
        }

        const string FallbackManifest =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"\n" +
            "    package=\"com.google.example.games.mainlibproj\"\n" +
            "    android:versionCode=\"1\"\n" +
            "    android:versionName=\"1.0\" >\n\n" +
            "    <application>\n\n" +
            "        <meta-data android:name=\"com.google.android.gms.games.APP_ID\"\n" +
            "            android:value=\"@string/" + StringName + "\" />\n\n" +
            "        <meta-data android:name=\"com.google.android.gms.games.unityVersion\"\n" +
            "            android:value=\"\\u0032.1.0\" />\n\n" +
            "        <activity android:name=\"com.google.games.bridge.NativeBridgeActivity\"\n" +
            "            android:theme=\"@android:style/Theme.Translucent.NoTitleBar.Fullscreen\" />\n" +
            "    </application>\n" +
            "</manifest>\n";

        /// <summary>Steps 1 to 4. Writes a file only when its content changes. Returns true when anything changed.</summary>
        public static bool Apply()
        {
            string id = ReadProjectId();
            bool changed = false;
            string manifestPath = LibraryDir + "/AndroidManifest.xml";
            string manifest = File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : FallbackManifest;
            string newManifest = RewriteManifest(manifest);
            changed |= WriteIfChanged(manifestPath, newManifest);
            changed |= WriteIfChanged(LibraryDir + "/res/values/strings.xml", StringsXml(id));
            bool valid = PlayGamesConfig.IsValidProjectId(id);
            changed |= SetPluginAppId(valid ? id : "");
            if (valid) SetPluginSetupDone();
            if (changed) AssetDatabase.Refresh();
            if (!valid) Debug.LogWarning(Tag + "play_games_config.json has no valid projectId yet: this build will not use Play Games sign-in. A release build refuses this.");
            return changed;
        }

        static bool WriteIfChanged(string path, string text)
        {
            if (File.Exists(path) && File.ReadAllText(path).Replace("\r\n", "\n") == text.Replace("\r\n", "\n")) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text, new UTF8Encoding(false));
            Debug.Log(Tag + "wrote " + path);
            return true;
        }

        static bool SetPluginAppId(string id)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(SettingsAssetPath);
            if (asset == null) return false;
            var so = new SerializedObject(asset);
            SerializedProperty p = so.FindProperty("mAppId");
            if (p == null || p.stringValue == id) return false;
            p.stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return true;
        }

        static void SetPluginSetupDone()
        {
            try
            {
                string text = File.Exists(PluginSettingsFile) ? File.ReadAllText(PluginSettingsFile) : "";
                if (Regex.IsMatch(text, "^android\\.SetupDone=true\\s*$", RegexOptions.Multiline)) return;
                text = Regex.Replace(text, "^android\\.SetupDone=.*\\r?\\n?", "", RegexOptions.Multiline);
                if (text.Length > 0 && !text.EndsWith("\n")) text += "\n";
                File.WriteAllText(PluginSettingsFile, text + "android.SetupDone=true\n");
            }
            catch (Exception e) { Debug.LogWarning(Tag + "could not set the plugin setup flag: " + e.Message); }
        }

        // ---------------- External Dependency Manager ----------------

        /// <summary>
        /// True when the Android Resolver left the Games v2 SDK (play-services-games-v2, pulled by the plugin's GooglePlayGamesPluginDependencies.xml) in Assets/Plugins/Android.
        /// There is no custom mainTemplate in this project, so the resolver (Google EDM4U 1.2.182, "Resolve Automatically On Build" is its default) copies the resolved AAR into
        /// Assets/Plugins/Android and Unity's Gradle picks it up there. Verified only by reading: that default and the public PlayServicesResolver.ResolveSync entry exist in the
        /// installed Google.JarResolver.dll; the first real resolve needs Unity, Gradle and network.
        /// </summary>
        public static bool GamesSdkResolved()
        {
            const string dir = "Assets/Plugins/Android";
            if (!Directory.Exists(dir)) return false;
            // This project has a custom mainTemplate.gradle: the resolver writes the dependency line there instead of copying an AAR.
            string template = Path.Combine(dir, "mainTemplate.gradle");
            if (File.Exists(template) && File.ReadAllText(template).IndexOf("play-services-games", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            foreach (string e in Directory.GetFileSystemEntries(dir))
                if (Path.GetFileName(e).IndexOf("play-services-games", StringComparison.OrdinalIgnoreCase) >= 0 && !e.EndsWith(".meta", StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>If the Games SDK is not resolved yet, runs the Android Resolver synchronously (reflection, so this file compiles without the EDM dll). Returns <see cref="GamesSdkResolved"/>.</summary>
        public static bool EnsureDependenciesResolved()
        {
            if (GamesSdkResolved()) return true;
            Debug.Log(Tag + "Games SDK not resolved yet: running the Android Resolver.");
            try
            {
                Type t = null;
                foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = a.GetType("GooglePlayServices.PlayServicesResolver", false);
                    if (t != null) break;
                }
                if (t == null) { Debug.LogWarning(Tag + "Android Resolver (PlayServicesResolver) not found; use Assets > External Dependency Manager > Android Resolver > Force Resolve."); return false; }
                MethodInfo best = null;
                foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name != "ResolveSync") continue;
                    bool allBool = true;
                    foreach (ParameterInfo p in m.GetParameters()) if (p.ParameterType != typeof(bool)) allBool = false;
                    if (allBool && (best == null || m.GetParameters().Length < best.GetParameters().Length)) best = m;
                }
                if (best == null) { Debug.LogWarning(Tag + "PlayServicesResolver.ResolveSync not found; use Force Resolve."); return false; }
                ParameterInfo[] ps = best.GetParameters();
                var args = new object[ps.Length];
                for (int i = 0; i < args.Length; i++) args[i] = true;
                best.Invoke(null, args);
            }
            catch (Exception e) { Debug.LogWarning(Tag + "Android Resolver call failed: " + (e.InnerException != null ? e.InnerException.Message : e.Message)); }
            bool ok = GamesSdkResolved();
            Debug.Log(Tag + (ok ? "Games SDK resolved." : "Games SDK still not found in Assets/Plugins/Android."));
            return ok;
        }

        [MenuItem("Tools/Fluffy Climbers: Tower Jump/Apply Play Games setup")]
        static void ApplyFromMenu()
        {
            Apply();
            Debug.Log(Tag + "project id is " + (PlayGamesConfig.IsValidProjectId(ReadProjectId()) ? "set" : "still the placeholder") + "; Games SDK resolved: " + EnsureDependenciesResolved());
        }
    }

    /// <summary>
    /// Release-only checks of the store configuration (called once by AndroidReleaseBuild before the build number is used up). Returns an error text or null.
    /// Play Games project id: a placeholder or anything but 10 or more digits fails. Play licence key: the placeholder or an undecodable key fails (UnityIapPaymentService never
    /// grants unvalidated purchases in a release build). Games SDK: must be resolved by the Android Resolver.
    /// </summary>
    public static class StoreConfigPreflight
    {
        public static string Check()
        {
            string id = PlayGamesAndroidSetup.ReadProjectId();
            if (!PlayGamesConfig.IsValidProjectId(id))
                return "Assets/Resources/Services/play_games_config.json projectId is '" + id + "'. Put the Play Games Services Project ID there (10 or more digits, Play Console > Play Games Services > Configuration). See docs/35.";
            string iapKey = File.Exists("Assets/Resources/Services/iap_config.json") ? IapConfig.ParseKey(File.ReadAllText("Assets/Resources/Services/iap_config.json")) : IapConfig.Placeholder;
            if (IapConfig.DecodeKey(iapKey) == null)
                return "Assets/Resources/Services/iap_config.json playLicenseKey is missing or still the placeholder. Paste the base64 licence key (Play Console > Monetize > Monetization setup). See docs/33.";
            PlayGamesAndroidSetup.Apply();
            if (!PlayGamesAndroidSetup.EnsureDependenciesResolved())
                return "The Play Games Android SDK (play-services-games-v2) is not resolved in Assets/Plugins/Android. Run Assets > External Dependency Manager > Android Resolver > Force Resolve, then build again.";
            return null;
        }
    }
}
