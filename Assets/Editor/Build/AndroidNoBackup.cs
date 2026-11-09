using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEngine;

namespace FluffyClimbers.Editor.Build
{
    /// <summary>
    /// Turns Android backup off for the game. By default Android Auto Backup (Google backup) copies the app's private files
    /// (persistentDataPath/save.json, PlayerPrefs) to the player's Google account and restores them when the same package is
    /// installed again, even after a manual uninstall. That is why an old save came back after a reinstall. With
    /// android:allowBackup="false" (plus empty extraction rules for Android 12+ cloud backup and device transfer) a clean install
    /// always starts with no save. The game keeps no data keyed to a device serial or id.
    /// Runs after the other Gradle hooks (callbackOrder 30). It edits only the launcher AndroidManifest.xml application tag and adds
    /// one small xml resource. tools:replace avoids a manifest-merger conflict if an SDK library declares its own backup attributes.
    /// If the manifest is not found it only logs a warning (the build is never broken).
    /// </summary>
    public sealed class AndroidNoBackup : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder { get { return 30; } }

        const string RulesName = "fluffy_no_backup_rules";

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            DirectoryInfo parent = Directory.GetParent(path);
            string root = parent != null ? parent.FullName : path;
            string main = Path.Combine(root, "launcher", "src", "main");
            string manifest = Path.Combine(main, "AndroidManifest.xml");
            if (!File.Exists(manifest))
            {
                Debug.LogWarning("[Backup] launcher AndroidManifest.xml not found at " + manifest + "; backup flags not changed.");
                return;
            }

            string xmlDir = Path.Combine(main, "res", "xml");
            Directory.CreateDirectory(xmlDir);
            File.WriteAllText(Path.Combine(xmlDir, RulesName + ".xml"),
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<data-extraction-rules>\n" +
                "    <cloud-backup><exclude domain=\"root\" path=\".\" /><exclude domain=\"file\" path=\".\" /><exclude domain=\"database\" path=\".\" /><exclude domain=\"sharedpref\" path=\".\" /><exclude domain=\"external\" path=\".\" /></cloud-backup>\n" +
                "    <device-transfer><exclude domain=\"root\" path=\".\" /><exclude domain=\"file\" path=\".\" /><exclude domain=\"database\" path=\".\" /><exclude domain=\"sharedpref\" path=\".\" /><exclude domain=\"external\" path=\".\" /></device-transfer>\n" +
                "</data-extraction-rules>\n");

            string text = File.ReadAllText(manifest);
            if (!text.Contains("xmlns:tools="))
                text = Regex.Replace(text, "<manifest\\b", "<manifest xmlns:tools=\"http://schemas.android.com/tools\"", RegexOptions.None);
            Match app = Regex.Match(text, "<application\\b[^>]*>");
            if (!app.Success)
            {
                Debug.LogWarning("[Backup] <application> tag not found in " + manifest + "; backup flags not changed.");
                return;
            }
            string tag = app.Value;
            tag = Regex.Replace(tag, "\\s+android:(allowBackup|fullBackupContent|dataExtractionRules)=\"[^\"]*\"", "");
            tag = Regex.Replace(tag, "\\s+tools:replace=\"[^\"]*\"", "");
            // Unity writes the launcher <application .../> tag self-closing, so keep whichever ending the tag has.
            bool selfClosing = tag.EndsWith("/>");
            string ending = selfClosing ? "/>" : ">";
            tag = tag.Substring(0, tag.Length - ending.Length).TrimEnd()
                + " android:allowBackup=\"false\" android:fullBackupContent=\"false\" android:dataExtractionRules=\"@xml/" + RulesName + "\""
                + " tools:replace=\"android:allowBackup,android:fullBackupContent,android:dataExtractionRules\"" + (selfClosing ? " />" : ">");
            text = text.Substring(0, app.Index) + tag + text.Substring(app.Index + app.Length);
            File.WriteAllText(manifest, text);
            Debug.Log("[Backup] allowBackup=false set in " + manifest);
        }
    }
}
