using System;
using System.IO;
using System.Text.RegularExpressions;
using FluffyClimbers.Monetization;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FluffyClimbers.Editor.Build
{
    /// <summary>
    /// Wires the AdMob app id from Resources/Services/ads_config.json into the Android build (docs/33).
    /// The Google Mobile Ads plugin (com.google.ads.mobile) takes the id from its settings asset (Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset,
    /// field adMobAndroidAppId) in its own pre-build step (order 0) and stops the build when it is empty. <see cref="AdMobAppIdPreBuild"/> (order -100) therefore
    /// writes the resolved id into that asset first: the real id, or Google's SAMPLE app id for development builds with placeholders (or testMode=true).
    /// A non-development build with a placeholder id fails here (AndroidReleaseBuild also checks it before the build number is used up).
    /// </summary>
    public static class AdMobBuildConfig
    {
        public const string PermInternet = "android.permission.INTERNET";
        public const string PermNetworkState = "android.permission.ACCESS_NETWORK_STATE";
        public const string PermAdId = "com.google.android.gms.permission.AD_ID";
        public const string MetaAppId = "com.google.android.gms.ads.APPLICATION_ID";

        /// <summary>The app id for a build, or null when it must not be built (non-development with placeholders).</summary>
        public static string ResolveAppId(bool developmentBuild)
        {
            AdsConfig.Reset();
            return AdsConfig.ResolveAppId(AdsConfig.RawAppId, AdsConfig.TestMode, developmentBuild);
        }

        /// <summary>Error text for a release (non-development) build that would ship without a real app id; null when fine.</summary>
        public static string ReleaseProblem()
        {
            AdsConfig.Reset();
            if (AdsConfig.TestMode) return null;   // sample ids shipped on purpose (testMode=true)
            if (AdsConfig.IsPlaceholder(AdsConfig.RawAppId))
                return "Resources/Services/ads_config.json still has the placeholder androidAppId (" + AdsConfig.PlaceholderAppId + "). Put the real AdMob App ID (ca-app-pub-...~...) and the rewarded unit ids there, or set testMode=true on purpose. See docs/33.";
            return null;
        }

        /// <summary>Writes the id into the plugin's settings asset (class is internal, so by reflection). Returns false when the asset could not be written.</summary>
        public static bool WriteSettingsAsset(string appId)
        {
            try
            {
                Type t = null;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = a.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings", false);
                    if (t != null) break;
                }
                if (t == null) { Debug.LogWarning("[AdMob] GoogleMobileAdsSettings type not found; set the Android app id in Assets > Google Mobile Ads > Settings."); return false; }
                var load = t.GetMethod("LoadInstance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                var instance = load.Invoke(null, null) as UnityEngine.Object;
                if (instance == null) return false;
                var so = new SerializedObject(instance);
                SerializedProperty p = so.FindProperty("adMobAndroidAppId");
                if (p == null) { Debug.LogWarning("[AdMob] adMobAndroidAppId field not found in the settings asset."); return false; }
                if (p.stringValue != appId)
                {
                    p.stringValue = appId;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(instance);
                    AssetDatabase.SaveAssets();
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AdMob] could not write the app id into the settings asset: " + e.Message);
                return false;
            }
        }
    }

    /// <summary>Runs before the Google Mobile Ads plugin's own pre-build step and gives it the app id (see AdMobBuildConfig).</summary>
    public sealed class AdMobAppIdPreBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return -100; } }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            bool dev = (report.summary.options & BuildOptions.Development) != 0;
            string id = AdMobBuildConfig.ResolveAppId(dev);
            if (id == null)
                throw new BuildFailedException("[AdMob] " + AdMobBuildConfig.ReleaseProblem());
            if (!AdMobBuildConfig.WriteSettingsAsset(id))
                Debug.LogWarning("[AdMob] the plugin may stop the build because its app id is empty.");
            Debug.Log("[AdMob] Android app id for this build: " + (id == AdsConfig.SampleAppId ? "Google SAMPLE app id (test ads)" : "from ads_config.json"));
        }
    }

    /// <summary>
    /// After Gradle project generation (order 40, after short label 20 and no-backup 30): makes sure the launcher manifest has INTERNET, ACCESS_NETWORK_STATE
    /// and AD_ID exactly once, and that some generated manifest carries com.google.android.gms.ads.APPLICATION_ID (adds it to the launcher if not). It
    /// only inserts elements and never touches the application tag attributes, so AndroidNoBackup's tools:replace is unaffected. Warnings only; never breaks the build.
    /// </summary>
    public sealed class AdMobManifestHook : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder { get { return 40; } }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            DirectoryInfo parent = Directory.GetParent(path);
            string root = parent != null ? parent.FullName : path;
            string manifest = Path.Combine(root, "launcher", "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifest)) { Debug.LogWarning("[AdMob] launcher AndroidManifest.xml not found at " + manifest + "; permissions not checked."); return; }
            string text = File.ReadAllText(manifest);
            string inserts = "";
            foreach (string perm in new[] { AdMobBuildConfig.PermInternet, AdMobBuildConfig.PermNetworkState, AdMobBuildConfig.PermAdId })
                if (!Regex.IsMatch(text, "<uses-permission[^>]*android:name=\"" + Regex.Escape(perm) + "\""))
                    inserts += "    <uses-permission android:name=\"" + perm + "\" />\n";

            string meta = "";
            if (!AnyManifestHas(root, AdMobBuildConfig.MetaAppId))
            {
                string id = AdMobBuildConfig.ResolveAppId(EditorUserBuildSettings.development);
                if (id != null)
                {
                    meta = "    <meta-data android:name=\"" + AdMobBuildConfig.MetaAppId + "\" android:value=\"" + id + "\" />\n";
                    Debug.LogWarning("[AdMob] no manifest had APPLICATION_ID; added it to the launcher manifest.");
                }
            }

            if (inserts.Length > 0)
            {
                Match app = Regex.Match(text, "<application\\b");
                if (!app.Success) { Debug.LogWarning("[AdMob] <application> not found in " + manifest + "; permissions not added."); return; }
                text = text.Insert(app.Index, inserts.TrimStart() + "    ");
            }
            if (meta.Length > 0)
            {
                // meta-data must be inside <application>: insert after its opening tag, or expand a self-closing one.
                Match app = Regex.Match(text, "<application\\b[^>]*?(/?)>");
                if (app.Success)
                {
                    string open = app.Value;
                    if (app.Groups[1].Value == "/")
                        text = text.Substring(0, app.Index) + open.Substring(0, open.Length - 2).TrimEnd() + ">\n" + meta + "  </application>" + text.Substring(app.Index + app.Length);
                    else
                        text = text.Insert(app.Index + app.Length, "\n" + meta);
                }
            }
            if (inserts.Length > 0 || meta.Length > 0)
            {
                File.WriteAllText(manifest, text);
                Debug.Log("[AdMob] launcher manifest updated (permissions/app id) in " + manifest);
            }
        }

        static bool AnyManifestHas(string root, string needle)
        {
            try
            {
                foreach (string f in Directory.GetFiles(root, "AndroidManifest.xml", SearchOption.AllDirectories))
                    if (File.ReadAllText(f).Contains(needle)) return true;
            }
            catch (Exception) { }
            return false;
        }
    }
}
