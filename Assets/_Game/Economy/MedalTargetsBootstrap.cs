using System;
using UnityEngine;

namespace BoingUpSkyTower.Economy
{
    /// <summary>Installs the shipped target file as <see cref="MedalTargets.Source"/> when the game starts (before the first scene). A missing or broken file leaves "no data" and logs once.</summary>
    public static class MedalTargetsBootstrap
    {
        public const string ResourceName = "Content/medal_targets";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install() { InstallFromResources(); }

        /// <summary>Loads the target file from Resources and sets it as the medal source. Returns true when installed.</summary>
        public static bool InstallFromResources()
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourceName);
            if (asset == null) { Debug.LogWarning("[Medals] " + ResourceName + " not found: no medal targets, nobody earns a medal."); return false; }
            try
            {
                MedalTargets.Source = FileMedalTargets.FromJson(asset.text);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[Medals] " + ResourceName + " is broken: " + e.Message);
                return false;
            }
        }
    }
}
