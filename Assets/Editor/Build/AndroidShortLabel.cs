using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEngine;

namespace FluffyClimbers.Editor.Build
{
    /// <summary>
    /// Short launcher label. The Android home screen shows <see cref="LauncherLabel"/> under the icon, while PlayerSettings.productName
    /// stays "Fluffy Climbers: Tower Jump" (store listing, Application.productName).
    ///
    /// Mechanism: Unity writes the generated launcher module's res/values/strings.xml with an app_name string holding the product name,
    /// and the launcher manifest uses android:label="@string/app_name". After Gradle project generation this post-processor REWRITES THAT
    /// ONE EXISTING string value in place (it adds no new resource). Because no second app_name resource, .androidlib or
    /// Assets/Plugins/Android/res file is added, a duplicate-resource build failure cannot happen. If Unity ever changes the file layout
    /// we only log a warning and the long product name is shown (the build is never broken).
    /// callbackOrder 20 touches only strings.xml, so the AdMob and AltTester build
    /// hooks are unaffected. The path passed in is the unityLibrary module; the launcher module is its sibling directory.
    /// </summary>
    public sealed class AndroidShortLabel : IPostGenerateGradleAndroidProject
    {
        public const string LauncherLabel = "Fluffy Climbers";

        public int callbackOrder { get { return 20; } }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            DirectoryInfo parent = Directory.GetParent(path);
            string root = parent != null ? parent.FullName : path;
            string strings = Path.Combine(root, "launcher", "src", "main", "res", "values", "strings.xml");
            if (!File.Exists(strings))
            {
                Debug.LogWarning("[Label] launcher strings.xml not found at " + strings + "; the launcher will show the product name.");
                return;
            }
            string xml = File.ReadAllText(strings);
            var rx = new Regex("(<string\\s+name=\"app_name\"[^>]*>)[^<]*(</string>)");
            if (rx.IsMatch(xml))
            {
                xml = rx.Replace(xml, "${1}" + LauncherLabel + "${2}", 1);
                File.WriteAllText(strings, xml);
                Debug.Log("[Label] launcher app_name set to \"" + LauncherLabel + "\" in " + strings);
            }
            else Debug.LogWarning("[Label] app_name string not found in " + strings + "; the launcher will show the product name.");
        }
    }
}
