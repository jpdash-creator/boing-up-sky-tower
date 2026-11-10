using UnityEngine;

namespace FluffyClimbers.Monetization
{
    /// <summary>
    /// Play Games Services project id from Resources/Services/play_games_config.json: {"projectId"}. It is the number shown under the game name on the Play Console
    /// Play Games Services > Configuration page (the leading digits of the OAuth client id, 10 or more digits). The Editor build step (PlayGamesAndroidSetup) writes it into
    /// the Android library as the string resource game_services_project_id that the manifest meta-data com.google.android.gms.games.APP_ID points to (Google guide:
    /// developer.android.com/games/pgs/android/android-signin). With the placeholder the game never touches the Play Games SDK (guest play only). See docs/35.
    /// </summary>
    public static class PlayGamesConfig
    {
        public const string Placeholder = "REPLACE_WITH_PLAY_GAMES_PROJECT_ID";
        public const string ResourcePath = "Services/play_games_config";
        public const int MinDigits = 10;

        [System.Serializable]
        sealed class Data { public string projectId; }

        static bool loaded;
        static string projectId = Placeholder;

        /// <summary>Pure: the project id in a config text (placeholder when missing or unreadable).</summary>
        public static string ParseProjectId(string json)
        {
            try
            {
                Data d = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Data>(json);
                if (d != null && !string.IsNullOrWhiteSpace(d.projectId)) return d.projectId.Trim();
            }
            catch (System.Exception) { }
            return Placeholder;
        }

        /// <summary>Pure: true for 10 or more digits and nothing else (the placeholder is false).</summary>
        public static bool IsValidProjectId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length < MinDigits) return false;
            for (int i = 0; i < id.Length; i++) if (id[i] < '0' || id[i] > '9') return false;
            return true;
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                TextAsset t = Resources.Load<TextAsset>(ResourcePath);
                if (t != null) projectId = ParseProjectId(t.text);
            }
            catch (System.Exception e) { Debug.LogWarning("[PlayGames] play_games_config.json unreadable: " + e.Message); }
        }

        public static string ProjectId { get { Load(); return projectId; } }
        public static bool IsConfigured { get { Load(); return IsValidProjectId(projectId); } }
    }
}
