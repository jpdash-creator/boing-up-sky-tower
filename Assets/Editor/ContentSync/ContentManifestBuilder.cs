using System;
using System.IO;
using System.Text;
using BoingUpSkyTower.Content;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BoingUpSkyTower.Editor.ContentSync
{
    /// <summary>
    /// Builds the offline fallback manifest Assets/Resources/Content/content_manifest.json from the Content/ source files.
    /// Headless:  Unity.exe -batchmode -nographics -projectPath ... -executeMethod BoingUpSkyTower.Editor.ContentSync.ContentManifestBuilder.BuildFromCommandLine -logFile ...
    /// (run without -quit; the method exits itself: 0 ok, 1 error).
    /// </summary>
    public static class ContentManifestBuilder
    {
        public const string OutputAssetPath = "Assets/Resources/Content/content_manifest.json";

        static string ProjectRoot { get { return Path.GetDirectoryName(Application.dataPath); } }
        public static string ContentRoot { get { return Path.Combine(ProjectRoot, "Content"); } }
        public static string OutputFile { get { return Path.Combine(ProjectRoot, OutputAssetPath.Replace('/', Path.DirectorySeparatorChar)); } }

        [MenuItem("Tools/Boing Up: Sky Tower/Build Content Manifest")]
        public static void BuildMenu()
        {
            Build();
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[ContentManifestBuilder] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Writes the manifest if it changed. Returns true when the file was (re)written.</summary>
        public static bool Build()
        {
            string text = ContentManifestMerger.Build(ContentRoot);
            string path = OutputFile;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            bool changed = !File.Exists(path) || File.ReadAllText(path, Encoding.UTF8) != text;
            if (changed)
            {
                File.WriteAllText(path, text, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(OutputAssetPath, ImportAssetOptions.ForceUpdate);
            }
            Debug.Log("[ContentManifestBuilder] " + (changed ? "wrote " : "up to date: ") + OutputAssetPath + " (" + text.Length + " chars)");
            return changed;
        }
    }

    /// <summary>Rebuilds the fallback manifest before every player build.</summary>
    public sealed class ContentManifestBuildStep : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return 0; } }

        public void OnPreprocessBuild(BuildReport report)
        {
            ContentManifestBuilder.Build();
        }
    }
}
