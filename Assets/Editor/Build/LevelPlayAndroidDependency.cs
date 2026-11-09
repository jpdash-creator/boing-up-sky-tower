using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEngine;

namespace FluffyClimbers.Editor.Build
{
    /// <summary>
    /// The Unity package com.unity.services.levelplay ships only the C# and Java bridge; the native LevelPlay Android SDK is normally added
    /// by the LevelPlay Network Manager (External Dependency Manager). We do not use EDM, so this step adds the Maven Central artifact to the
    /// generated unityLibrary module and makes sure the AD_ID permission (Android 13 and up) is declared.
    /// Keep <see cref="NativeSdkVersion"/> equal to the Android SDK the package wraps (package 9.5.1 wraps Android SDK 9.6.0).
    /// </summary>
    public sealed class LevelPlayAndroidDependency : IPostGenerateGradleAndroidProject
    {
        public const string NativeSdkVersion = "9.6.0";
        const string Marker = "com.unity3d.ads-mediation:mediation-sdk";

        public int callbackOrder { get { return 10; } }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // Only when the LevelPlay package is part of the project.
            if (!LevelPlayPackageInstalled()) return;

            string gradle = Path.Combine(path, "build.gradle");
            if (File.Exists(gradle))
            {
                string text = File.ReadAllText(gradle);
                if (!text.Contains(Marker))
                {
                    var rx = new Regex(@"dependencies\s*\{");
                    Match m = rx.Match(text);
                    string line = "\n    implementation '" + Marker + ":" + NativeSdkVersion + "'";
                    if (m.Success) text = text.Insert(m.Index + m.Length, line);
                    else text += "\ndependencies {" + line + "\n}\n";
                    File.WriteAllText(gradle, text);
                    Debug.Log("[Ads] added " + Marker + ":" + NativeSdkVersion + " to " + gradle);
                }
            }
            else Debug.LogWarning("[Ads] unityLibrary build.gradle not found at " + gradle);

            string manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (File.Exists(manifest))
            {
                string xml = File.ReadAllText(manifest);
                if (!xml.Contains("com.google.android.gms.permission.AD_ID"))
                {
                    int i = xml.IndexOf("<application", System.StringComparison.Ordinal);
                    const string perm = "<uses-permission android:name=\"com.google.android.gms.permission.AD_ID\" />\n    ";
                    if (i >= 0) { xml = xml.Insert(i, perm); File.WriteAllText(manifest, xml); }
                    else Debug.LogWarning("[Ads] no <application> element, AD_ID permission not added");
                }
            }
        }

        static bool LevelPlayPackageInstalled()
        {
            try
            {
                string manifest = File.ReadAllText(Path.GetFullPath("Packages/manifest.json"));
                return manifest.Contains("com.unity.services.levelplay");
            }
            catch { return false; }
        }
    }
}
