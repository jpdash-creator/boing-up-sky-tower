#if FLUFFY_GPGS
using System;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using GooglePlayGames.BasicApi.SavedGame;
using UnityEngine;

namespace FluffyClimbers.Monetization
{
    /// <summary>
    /// Google Play Games Services account (docs/35) with Saved Games as the cloud save. Compiled only with the define FLUFFY_GPGS and the
    /// "Google Play Games plugin for Unity" (v2.x, github.com/playgameservices/play-games-plugin-for-unity) imported.
    /// Sign-in: PlayGamesPlatform.Instance.Authenticate (silent) and ManuallyAuthenticate (sheet). Cloud: ISavedGameClient
    /// OpenWithAutomaticConflictResolution, ReadBinaryData, CommitUpdate on one file (<see cref="FileName"/>). Android only.
    /// </summary>
    public sealed class GooglePlayGamesAccountService : IAccountService
    {
        public const string FileName = "fluffy_save";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() { ServiceFactories.GooglePlay = () => new GooglePlayGamesAccountService(); }

        public bool IsAvailable { get { return Application.platform == RuntimePlatform.Android; } }
        public bool LastLoadFailed { get; private set; }

        public bool IsSignedIn
        {
            get { return IsAvailable && PlayGamesPlatform.Instance != null && PlayGamesPlatform.Instance.IsAuthenticated(); }
        }

        public string DisplayName { get { return IsSignedIn ? (PlayGamesPlatform.Instance.GetUserDisplayName() ?? "") : ""; } }
        public string PlayerId { get { return IsSignedIn ? (PlayGamesPlatform.Instance.GetUserId() ?? "") : ""; } }

        public void SilentSignIn(Action<SignInResult> done)
        {
            if (!IsAvailable) { Finish(done, SignInOutcome.Unavailable, "not Android"); return; }
            if (IsSignedIn) { Finish(done, SignInOutcome.Success, null); return; }
            PlayGamesPlatform.Instance.Authenticate(status => Finish(done, Map(status), status.ToString()));
        }

        public void SignIn(Action<SignInResult> done)
        {
            if (!IsAvailable) { Finish(done, SignInOutcome.Unavailable, "not Android"); return; }
            if (IsSignedIn) { Finish(done, SignInOutcome.Success, null); return; }
            PlayGamesPlatform.Instance.ManuallyAuthenticate(status => Finish(done, Map(status), status.ToString()));
        }

        // Play Games has no sign-out call: the game just stops using the account (the next ManuallyAuthenticate shows the sheet again).
        public void SignOut() { }

        static SignInOutcome Map(GooglePlayGames.BasicApi.SignInStatus s)
        {
            if (s == GooglePlayGames.BasicApi.SignInStatus.Success) return SignInOutcome.Success;
            if (s == GooglePlayGames.BasicApi.SignInStatus.Canceled) return SignInOutcome.Cancelled;
            return SignInOutcome.Failed;
        }

        void Finish(Action<SignInResult> done, SignInOutcome status, string error)
        {
            if (done == null) return;
            var r = new SignInResult { Status = status, Error = status == SignInOutcome.Success ? null : error };
            if (status == SignInOutcome.Success) { r.PlayerId = PlayerId; r.DisplayName = DisplayName; }
            done(r);
        }

        ISavedGameClient Client { get { return PlayGamesPlatform.Instance.SavedGame; } }

        public void LoadCloudSave(Action<byte[]> done)
        {
            LastLoadFailed = false;
            if (!IsSignedIn) { LastLoadFailed = true; if (done != null) done(null); return; }
            try
            {
                Client.OpenWithAutomaticConflictResolution(FileName, DataSource.ReadCacheOrNetwork, ConflictResolutionStrategy.UseMostRecentlySaved,
                    (openStatus, meta) =>
                    {
                        if (openStatus != SavedGameRequestStatus.Success) { LastLoadFailed = true; if (done != null) done(null); return; }
                        // a file that was never written has no data: reading it returns an empty array or null (= no cloud save)
                        Client.ReadBinaryData(meta, (readStatus, data) =>
                        {
                            if (readStatus != SavedGameRequestStatus.Success) { LastLoadFailed = true; if (done != null) done(null); return; }
                            if (done != null) done(data != null && data.Length > 0 ? data : null);
                        });
                    });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Account] load failed: " + e.Message);
                LastLoadFailed = true;
                if (done != null) done(null);
            }
        }

        public void SaveCloud(byte[] data, Action<bool> done)
        {
            if (!IsSignedIn || data == null) { if (done != null) done(false); return; }
            try
            {
                Client.OpenWithAutomaticConflictResolution(FileName, DataSource.ReadCacheOrNetwork, ConflictResolutionStrategy.UseMostRecentlySaved,
                    (openStatus, meta) =>
                    {
                        if (openStatus != SavedGameRequestStatus.Success) { if (done != null) done(false); return; }
                        SavedGameMetadataUpdate update = new SavedGameMetadataUpdate.Builder()
                            .WithUpdatedDescription("Fluffy Climbers: Tower Jump " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC")
                            .Build();
                        Client.CommitUpdate(meta, update, data, (commitStatus, committed) =>
                        {
                            if (done != null) done(commitStatus == SavedGameRequestStatus.Success);
                        });
                    });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Account] save failed: " + e.Message);
                if (done != null) done(false);
            }
        }
    }
}
#endif
