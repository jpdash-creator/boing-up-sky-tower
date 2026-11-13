using FluffyClimbers.Content;

namespace FluffyClimbers.Leaderboard
{
    /// <summary>
    /// What the client needs to know about the leaderboard service. The switch and the service URL come from the remote config (<c>leaderboard.enabled</c>, <c>leaderboard.baseUrl</c> in
    /// Content/config/remote_config.json, class <see cref="LeaderboardConfigDef"/>). Missing section = OFF: nothing is called, queued or shown as a live feature.
    /// Everything else is a constant of the Firebase project (public identifiers, not secrets).
    /// </summary>
    public sealed class LeaderboardConfig
    {
        /// <summary>Firebase / Google Cloud project id.</summary>
        public const string ProjectId = "fluffy-climbers-tower-jump";
        /// <summary>Play Games project web client (OAuth 2.0 client id of type Web) used to get the server auth code for Firebase.</summary>
        public const string WebClientId = "794061115424-kuj7okc9p7c7n07263gonqqngeipvug1.apps.googleusercontent.com";
        /// <summary>Rules version of the simulation this client runs; the service replays only versions it knows (design doc 5.2). Bump with the simulation.</summary>
        public const int SimVersion = 1;
        /// <summary>The deployed Cloud Run service (public identifier). Used only by the development-build Admin switch LEADERBOARD TEST; releases read the remote config.</summary>
        public const string DefaultBaseUrl = "https://fluffy-leaderboard-763742589585.asia-south1.run.app";
        public const string FirestoreRestRoot = "https://firestore.googleapis.com/v1/";

        public bool Enabled;
        /// <summary>Cloud Run URL without a trailing slash (placeholder until the service is deployed).</summary>
        public string BaseUrl = "";

        /// <summary>The switch is on AND a real URL is set (the placeholder or an empty value counts as OFF).</summary>
        public bool IsLive { get { return Enabled && IsUsableUrl(BaseUrl); } }

        public static LeaderboardConfig Off { get { return new LeaderboardConfig(); } }

        /// <summary>Reads the section of the remote config; a missing section or an unusable URL gives OFF.</summary>
        public static LeaderboardConfig From(RemoteConfigDef rc, bool forceOn = false)
        {
            LeaderboardConfigDef d = rc != null ? rc.Leaderboard : null;
            if (forceOn && (d == null || !d.Enabled || !IsUsableUrl(d.BaseUrl)))
                return new LeaderboardConfig { Enabled = true, BaseUrl = d != null && IsUsableUrl(d.BaseUrl) ? d.BaseUrl.Trim().TrimEnd('/') : DefaultBaseUrl };
            if (d == null) return Off;
            return new LeaderboardConfig { Enabled = d.Enabled, BaseUrl = (d.BaseUrl ?? "").Trim().TrimEnd('/') };
        }

        /// <summary>https URL with a host (http only for localhost, for the local test service).</summary>
        public static bool IsUsableUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            if (url.StartsWith("https://", System.StringComparison.Ordinal)) return url.Length > 12 && url.IndexOf("REPLACE", System.StringComparison.OrdinalIgnoreCase) < 0;
            return url.StartsWith("http://localhost", System.StringComparison.Ordinal) || url.StartsWith("http://127.0.0.1", System.StringComparison.Ordinal);
        }

        public string Url(string path) { return BaseUrl + path; }

        /// <summary>The Firestore REST address of a board document.</summary>
        public static string BoardDocUrl(string weekKey, string board)
        {
            return FirestoreRestRoot + "projects/" + ProjectId + "/databases/(default)/documents/weeks/" + weekKey + "/boards/" + board;
        }

        public static string CommitUrl { get { return FirestoreRestRoot + "projects/" + ProjectId + "/databases/(default)/documents:commit"; } }
    }
}
