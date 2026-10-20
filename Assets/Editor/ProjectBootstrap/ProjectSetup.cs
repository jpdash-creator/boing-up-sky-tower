using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectBootstrap
{
    // One-time (idempotent) project setup, run headless:
    //   Unity.exe -batchmode -nographics -projectPath <path> -executeMethod ProjectBootstrap.ProjectSetup.Run -logFile <file>
    // Exit code 0 = ok, 1 = failure (details in the log, lines start with [ProjectSetup]).
    public static class ProjectSetup
    {
        // Placeholder: trademark and store-availability check is still pending (doc 07).
        const string AppId = "com.skyhopper.game";

        public static void Run()
        {
            try
            {
                SetColorSpaceAndIdentity();
                CreateUrp2D();
                SetInputHandler();
                CreateScenes();
                AssetDatabase.SaveAssets();
                Debug.Log("[ProjectSetup] Done.");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[ProjectSetup] Failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        static void SetColorSpaceAndIdentity()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.companyName = "SkyHopper";
            PlayerSettings.productName = "Sky Hopper";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, AppId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            Debug.Log("[ProjectSetup] Identity: Linear color space, Sky Hopper, " + AppId + ", portrait, Android IL2CPP ARM64.");
        }

        static void CreateUrp2D()
        {
            const string dir = "Assets/Settings";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets", "Settings");

            var rendererPath = dir + "/Renderer2D.asset";
            var urpPath = dir + "/UniversalRP.asset";

            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }

            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(urpPath);
            if (urp == null)
            {
                urp = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(urp, urpPath);
            }

            GraphicsSettings.defaultRenderPipeline = urp;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            Debug.Log("[ProjectSetup] URP 2D renderer assigned to graphics and all quality levels.");
        }

        static void SetInputHandler()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("[ProjectSetup] ProjectSettings.asset not loadable; input handler unchanged.");
                return;
            }
            var so = new SerializedObject(assets[0]);
            var p = so.FindProperty("activeInputHandler");
            if (p != null)
            {
                p.intValue = 1;
                so.ApplyModifiedProperties();
                Debug.Log("[ProjectSetup] Active Input Handling = Input System package.");
            }
        }

        static void CreateScenes()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            var scenes = new[] { "Boot", "Meta", "Run" }; // doc 03 section 3
            var settings = new EditorBuildSettingsScene[scenes.Length];
            for (int i = 0; i < scenes.Length; i++)
            {
                var path = "Assets/Scenes/" + scenes[i] + ".unity";
                if (!System.IO.File.Exists(path))
                {
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                    var cam = Camera.main;
                    if (cam != null)
                    {
                        cam.orthographic = true;
                        cam.orthographicSize = 10f;
                        cam.backgroundColor = new Color32(0x1b, 0x1b, 0x24, 0xff);
                        cam.clearFlags = CameraClearFlags.SolidColor;
                    }
                    EditorSceneManager.SaveScene(scene, path);
                }
                settings[i] = new EditorBuildSettingsScene(path, true);
            }
            EditorBuildSettings.scenes = settings;
            Debug.Log("[ProjectSetup] Scenes Boot, Meta, Run created and added to Build Settings.");
        }
    }
}
