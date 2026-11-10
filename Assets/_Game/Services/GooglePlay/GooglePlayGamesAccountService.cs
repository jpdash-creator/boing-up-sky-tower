#if FLUFFY_GPGS
using System;
using FluffyClimbers.Economy;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using GooglePlayGames.BasicApi.SavedGame;
using UnityEngine;

namespace FluffyClimbers.Monetization
{
    /// <summary>
    /// Google Play Games Services account (docs/35) with Saved Games as the cloud save. Compiled only with the define FLUFFY_GPGS and the plugin
    /// com.google.play.games (v2, Games v2 SDK play-services-games-v2). Verified against the plugin sources in Library/PackageCache:
    /// - The SDK is initialised by the plugin itself: the first access of PlayGamesPlatform.Instance builds AndroidClient, whose constructor calls
    ///   com.google.android.gms.games.PlayGamesSdk.initialize(activity). No Activate() call is needed (it only sets Social.Active, which this game never uses).
    ///   Instance is therefore touched only when the build is Android AND play_games_config.json holds a real project id (see PlayGamesConfig), so a build
    ///   without the id never loads the SDK.
    /// - Sign-in: PlayGamesPlatform.Authenticate = the automatic sign-in check (isAuthenticated, no UI; run once at launch by AutoSignInAtLaunch, no button
    ///   needed, retried by AccountSync when the app regains focus); ManuallyAuthenticate = GamesSignInClient.signIn (optional button on the login screen and Settings).
    ///   Every failure (sign-in required, offline, cancelled) is reported silently through the result; nothing here blocks play or shows an error by itself.
    /// - Cloud: ISavedGameClient on one file (FileName). Open with MANUAL conflict resolution so two devices that wrote at once are decided by the game's own
    ///   rule (SaveSync.PickGreater: readable, more levels, more heroes, newer, bigger wallet; whole saves, never merged). Network first, cache when offline.
    ///   A failed read is LastLoadFailed (not "no save"), so local progress is never overwritten or wiped by a cloud error. Uploads are throttled by AccountSync (debounce).
    /// The player id is kept only in the local save and in the cloud blob; there is no own backend.
    /// </summary>
    public sealed class GooglePlayGamesAccountService : IAccountService
    {
        public const string FileName = SaveSync.CloudFileName;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() { ServiceFactories.GooglePlay = () => new GooglePlayGamesAccountService(); }

        /// <summary>Automatic sign-in at launch (Google guide: no mandatory sign-in button). Silent; a failure changes nothing and the game stays playable offline.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoSignInAtLaunch()
        {
            try
            {
                if (Application.platform != RuntimePlatform.Android || !PlayGamesConfig.IsConfigured) return;
                IAccountService acc = Services.Account;
                if (acc != null && acc.IsAvailable) acc.SilentSignIn(null);
            }
            catch (Exception e) { Debug.LogWarning("[PlayGames] automatic sign-in skipped: " + e.Message); }
        }

        /// <summary>Android build with a real Play Games Services project id. Everything else is "not available": the login screen then offers guest play only.</summary>
        public bool IsAvailable { get { return Application.platform == RuntimePlatform.Android && PlayGamesConfig.IsConfigured; } }
        public bool LastLoadFailed { get; private set; }

        public bool IsSignedIn
        {
            get
            {
                if (!IsAvailable) return false;
                try { return PlayGamesPlatform.Instance != null && PlayGamesPlatform.Instance.IsAuthenticated(); }
                catch (Exception) { return false; }
            }
        }

        public string DisplayName
        {
            get
            {
                if (!IsSignedIn) return "";
                try { return PlayGamesPlatform.Instance.GetUserDisplayName() ?? ""; } catch (Exception) { return ""; }
            }
        }

        public string PlayerId
        {
            get
            {
                if (!IsSignedIn) return "";
                try { return PlayGamesPlatform.Instance.GetUserId() ?? ""; } catch (Exception) { return ""; }
            }
        }

        public void SilentSignIn(Action<SignInResult> done)
        {
            if (!IsAvailable) { Finish(done, SignInOutcome.Unavailable, "not Android or no project id"); return; }
            try
            {
                if (IsSignedIn) { Finish(done, SignInOutcome.Success, null); return; }
                PlayGamesPlatform.Instance.Authenticate(status => Finish(done, Map(status), status.ToString()));
            }
            catch (Exception e) { Finish(done, SignInOutcome.Failed, e.Message); }
        }

        public void SignIn(Action<SignInResult> done)
        {
            if (!IsAvailable) { Finish(done, SignInOutcome.Unavailable, "not Android or no project id"); return; }
            try
            {
                if (IsSignedIn) { Finish(done, SignInOutcome.Success, null); return; }
                PlayGamesPlatform.Instance.ManuallyAuthenticate(status => Finish(done, Map(status), status.ToString()));
            }
            catch (Exception e) { Finish(done, SignInOutcome.Failed, e.Message); }
        }

        // Play Games has no sign-out call: the game just stops using the account (the next ManuallyAuthenticate shows the sheet again).
        public void SignOut() { }

        static SignInOutcome Map(SignInStatus s)
        {
            if (s == SignInStatus.Success) return SignInOutcome.Success;
            if (s == SignInStatus.Canceled) return SignInOutcome.Cancelled;
            return SignInOutcome.Failed;
        }

        void Finish(Action<SignInResult> done, SignInOutcome status, string error)
        {
            if (done == null) return;
            var r = new SignInResult { Status = status, Error = status == SignInOutcome.Success ? null : error };
            if (status == SignInOutcome.Success) { r.PlayerId = PlayerId; r.DisplayName = DisplayName; }
            done(r);
        }

        // ---------------- Saved Games ----------------

        ISavedGameClient Client { get { return PlayGamesPlatform.Instance.SavedGame; } }

        /// <summary>
        /// Opens the save file: network first (so a stale cache never hides a newer cloud save), then the cache when the network fails. A conflict between two writes
        /// is resolved by the game rule (SaveSync.PickGreater). done runs exactly once.
        /// </summary>
        void OpenFile(Action<SavedGameRequestStatus, ISavedGameMetadata> done)
        {
            bool finished = false;
            Action<SavedGameRequestStatus, ISavedGameMetadata> once = (st, meta) =>
            {
                if (finished) return;
                finished = true;
                done(st, meta);
            };
            ConflictCallback onConflict = (resolver, original, originalData, unmerged, unmergedData) =>
            {
                bool first = SaveSync.PickGreater(originalData, unmergedData) == 0;
                resolver.ChooseMetadata(first ? original : unmerged);
            };
            Client.OpenWithManualConflictResolution(FileName, DataSource.ReadNetworkOnly, true, onConflict, (st, meta) =>
            {
                if (st == SavedGameRequestStatus.Success) { once(st, meta); return; }
                Client.OpenWithManualConflictResolution(FileName, DataSource.ReadCacheOrNetwork, true, onConflict, once);
            });
        }

        public void LoadCloudSave(Action<byte[]> done)
        {
            LastLoadFailed = false;
            if (!IsSignedIn) { LastLoadFailed = true; if (done != null) done(null); return; }
            try
            {
                OpenFile((openStatus, meta) =>
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
                OpenFile((openStatus, meta) =>
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
