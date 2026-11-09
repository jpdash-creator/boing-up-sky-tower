using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace FluffyClimbers.Editor.ProjectBootstrap
{
    /// <summary>
    /// Applies the Fluffy Climbers app icons (source: Assets/Art/Marketing/icon/out, copies in Assets/Art/AppIcon).
    /// Menu: Tools/Fluffy Climbers: Tower Jump/Apply App Icons
    /// Headless: Unity.exe -batchmode -nographics -projectPath [path] -executeMethod FluffyClimbers.Editor.ProjectBootstrap.AppIconSetup.Apply -logFile [file]
    /// Idempotent. What it does:
    ///  1. copies any missing PNG from the marketing out folder into Assets/Art/AppIcon and imports them (Default texture, sRGB, no mipmaps,
    ///     Bilinear, uncompressed, no NPOT scaling, alpha only where needed);
    ///  2. PlayerSettings.SetIcons default icon (the 1024 icon; iOS derives every size from it, also the Android fallback);
    ///  3. Android adaptive (background + foreground layers), round and legacy icons via PlayerSettings.SetPlatformIcons.
    /// Not wired: the monochrome (themed) layer. Unity 6000.3 has no AndroidPlatformIconKind for it (only Adaptive, Round, Legacy), so
    /// icon_adaptive_monochrome.png is imported but not assigned. iOS-specific kinds are not used on purpose: the iOS module is not
    /// installed on this machine, so UnityEditor.iOS types would not compile; the default icon covers the App Store 1024 image.
    /// </summary>
    public static class AppIconSetup
    {
        const string SourceDir = "Assets/Art/Marketing/icon/out";
        public const string TargetDir = "Assets/Art/AppIcon";
        const string Ios = TargetDir + "/icon_ios_1024.png";
        const string Bg = TargetDir + "/icon_adaptive_background.png";
        const string Fg = TargetDir + "/icon_adaptive_foreground.png";
        const string Mono = TargetDir + "/icon_adaptive_monochrome.png";
        const string Legacy = TargetDir + "/icon_legacy_192.png";
        const string Round = TargetDir + "/icon_legacy_round_192.png";
        const string Play = TargetDir + "/icon_play_512.png";

        [MenuItem("Tools/Fluffy Climbers: Tower Jump/Apply App Icons")]
        public static void ApplyFromMenu() { Apply(); }

        /// <summary>Menu and headless entry point (batch mode: exit code 0 ok, 1 failure).</summary>
        public static void Apply()
        {
            bool headless = Application.isBatchMode;
            try
            {
                Run();
                if (headless) EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[AppIcon] failed: " + e);
                if (headless) EditorApplication.Exit(1);
            }
        }

        static void Run()
        {
            Directory.CreateDirectory(TargetDir);
            foreach (string name in new[] { "icon_ios_1024.png", "icon_play_512.png", "icon_adaptive_background.png", "icon_adaptive_foreground.png",
                                            "icon_adaptive_monochrome.png", "icon_legacy_192.png", "icon_legacy_round_192.png" })
            {
                string dst = TargetDir + "/" + name;
                if (!File.Exists(dst) && File.Exists(SourceDir + "/" + name)) File.Copy(SourceDir + "/" + name, dst);
            }
            AssetDatabase.Refresh();

            ConfigureImport(Ios, false);
            ConfigureImport(Play, false);
            ConfigureImport(Bg, false);
            ConfigureImport(Fg, true);
            ConfigureImport(Mono, true);
            ConfigureImport(Legacy, true);
            ConfigureImport(Round, true);

            Texture2D ios = Load(Ios), bg = Load(Bg), fg = Load(Fg), legacy = Load(Legacy), round = Load(Round);

            // Default icon: Unity builds every platform size from it where no specific icon is set (iOS, Android fallback).
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { ios }, IconKind.Any);

            // Android: adaptive icons have two layers (0 background, 1 foreground), round and legacy have one.
            NamedBuildTarget android = NamedBuildTarget.Android;
            Assign(android, AndroidPlatformIconKind.Adaptive, bg, fg);
            Assign(android, AndroidPlatformIconKind.Round, round, null);
            Assign(android, AndroidPlatformIconKind.Legacy, legacy, null);

            AssetDatabase.SaveAssets();
            Debug.Log("[AppIcon] applied: default icon, Android adaptive/round/legacy (monochrome not supported by this Unity version).");
        }

        static void Assign(NamedBuildTarget target, PlatformIconKind kind, Texture2D layer0, Texture2D layer1)
        {
            PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(target, kind);
            foreach (PlatformIcon icon in icons)
            {
                if (layer1 != null) icon.SetTextures(layer0, layer1);
                else icon.SetTextures(layer0);
            }
            PlayerSettings.SetPlatformIcons(target, kind, icons);
        }

        static Texture2D Load(string path)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t == null) throw new FileNotFoundException("icon texture missing: " + path);
            return t;
        }

        static void ConfigureImport(string path, bool alpha)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) throw new FileNotFoundException("not imported: " + path);
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.alphaIsTransparency = alpha;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Bilinear;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.isReadable = true;
            ti.maxTextureSize = 2048;
            ti.SaveAndReimport();
        }
    }
}
