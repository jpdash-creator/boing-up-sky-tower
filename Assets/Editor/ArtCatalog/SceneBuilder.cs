using System;
using BoingUpSkyTower.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BoingUpSkyTower.Editor
{
    /// <summary>
    /// Puts the bootstrap component into each of the three scenes (Boot, Meta, Run) created by ProjectSetup and saves them.
    /// Idempotent. Headless: -executeMethod BoingUpSkyTower.Editor.SceneBuilder.BuildFromCommandLine
    /// </summary>
    public static class SceneBuilder
    {
        [MenuItem("Tools/Boing Up: Sky Tower/Build Scenes")]
        public static void Build()
        {
            Setup("Assets/Scenes/Boot.unity", go => go.AddComponent<BootLoader>(), "BootLoader");
            Setup("Assets/Scenes/Meta.unity", go => go.AddComponent<MetaController>(), "MetaController");
            Setup("Assets/Scenes/Run.unity", go => go.AddComponent<RunController>(), "RunController");
            Debug.Log("[SceneBuilder] Boot, Meta and Run scenes wired.");
        }

        static void Setup(string path, Func<GameObject, Component> add, string rootName)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == rootName) UnityEngine.Object.DestroyImmediate(go);
                else if (go.name == "Directional Light") UnityEngine.Object.DestroyImmediate(go);   // 2D game: no 3D light
            }
            var root = new GameObject(rootName);
            add(root);
            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color32(0x14, 0x16, 0x24, 0xff);
                cam.transform.position = new Vector3(5f, 0f, -10f);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
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
                Debug.LogError("[SceneBuilder] Failed: " + e);
                EditorApplication.Exit(1);
            }
        }
    }
}
