#if FLUFFY_GPGS && FLUFFY_FIREBASE
using System;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using FluffyClimbers.Monetization;
using GooglePlayGames;
using UnityEngine;

namespace FluffyClimbers.Leaderboard
{
    /// <summary>
    /// The real leaderboard identity (Planning/LEADERBOARD-DESIGN.md section 12 step 6). Compiled only with the Firebase packages (com.google.firebase.app and com.google.firebase.auth, define
    /// FLUFFY_FIREBASE from this assembly's versionDefines) and Play Games (FLUFFY_GPGS). Flow:
    ///   1. FirebaseApp.CheckAndFixDependenciesAsync (google-services.json is read by the Firebase Android plugin at build time).
    ///   2. Play Games v2: PlayGamesPlatform.RequestServerSideAccess(false, callback) returns a server auth code for the Play Games project web client
    ///      (LeaderboardConfig.WebClientId; the plugin reads it from PlayGamesSettings.WebClientId, set below when the asset has none).
    ///   3. PlayGamesAuthProvider.GetCredential(code) -> FirebaseAuth.SignInWithCredentialAsync -> the Firebase user (uid) and ID tokens (TokenAsync).
    /// The Firebase user is kept by the SDK between sessions; a failed or offline sign-in falls back to it. Profile name and picture URL come from the Play Games profile.
    /// Callbacks run on the main thread (ContinueWithOnMainThread). Nothing here throws to the game.
    /// </summary>
    public sealed class FirebaseLeaderboardAuth : ILeaderboardAuth
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() { LeaderboardServices.AuthFactory = () => new FirebaseLeaderboardAuth(); }

        FirebaseAuth auth;
        bool depsChecked, depsOk;
        readonly LbProfile profile = new LbProfile();

        public bool IsAvailable { get { return Application.platform == RuntimePlatform.Android && PlayGamesConfig.IsConfigured; } }

        public bool IsReady
        {
            get
            {
                try { return auth != null && auth.CurrentUser != null; }
                catch (Exception) { return false; }
            }
        }

        public string Uid
        {
            get
            {
                try { return IsReady ? auth.CurrentUser.UserId ?? "" : ""; }
                catch (Exception) { return ""; }
            }
        }

        public LbProfile Profile
        {
            get
            {
                try
                {
                    if (PlayGamesPlatform.Instance != null && PlayGamesPlatform.Instance.IsAuthenticated())
                    {
                        profile.DisplayName = PlayGamesPlatform.Instance.GetUserDisplayName() ?? "";
                        profile.AvatarUrl = PlayGamesPlatform.Instance.GetUserImageUrl() ?? "";
                    }
                }
                catch (Exception) { }
                return profile;
            }
        }

        public void SignIn(Action<LbSignInOutcome, string> done)
        {
            if (!IsAvailable) { Finish(done, LbSignInOutcome.Unavailable, "not Android or no Play Games project id"); return; }
            try
            {
                if (!PlayGamesPlatform.Instance.IsAuthenticated()) { Finish(done, LbSignInOutcome.Failed, "Play Games is not signed in"); return; }
                EnsureDependencies(ok =>
                {
                    if (!ok) { Finish(done, LbSignInOutcome.Failed, "Firebase dependencies missing"); return; }
                    ExchangeCode(done);
                });
            }
            catch (Exception e) { Finish(done, LbSignInOutcome.Failed, e.Message); }
        }

        void EnsureDependencies(Action<bool> done)
        {
            if (depsChecked) { done(depsOk); return; }
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(t =>
            {
                depsChecked = true;
                depsOk = !t.IsFaulted && !t.IsCanceled && t.Result == DependencyStatus.Available;
                if (depsOk) auth = FirebaseAuth.DefaultInstance;
                done(depsOk);
            });
        }

        void ExchangeCode(Action<LbSignInOutcome, string> done)
        {
            try
            {
                // the plugin reads the web client id from its settings asset; set it in memory when the asset has none (the Editor asset is filled too, see the checklist)
                GooglePlayGames.PlayGamesSettings settings = GooglePlayGames.PlayGamesSettings.LoadInstance();
                if (settings != null && string.IsNullOrEmpty(settings.WebClientId)) settings.WebClientId = LeaderboardConfig.WebClientId;
                PlayGamesPlatform.Instance.RequestServerSideAccess(false, code =>
                {
                    if (string.IsNullOrEmpty(code)) { FallbackToSavedUser(done, "no server auth code"); return; }
                    Credential credential = PlayGamesAuthProvider.GetCredential(code);
                    auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(task =>
                    {
                        if (task.IsCanceled || task.IsFaulted || task.Result == null) FallbackToSavedUser(done, task.Exception != null ? task.Exception.GetBaseException().Message : "firebase sign-in failed");
                        else Finish(done, LbSignInOutcome.Success, null);
                    });
                });
            }
            catch (Exception e) { FallbackToSavedUser(done, e.Message); }
        }

        /// <summary>The auth code or the exchange failed (offline): a Firebase user saved by the SDK from an earlier session still works.</summary>
        void FallbackToSavedUser(Action<LbSignInOutcome, string> done, string error)
        {
            if (IsReady) Finish(done, LbSignInOutcome.Success, null);
            else Finish(done, LbSignInOutcome.Failed, error);
        }

        public void GetIdToken(bool forceRefresh, Action<string> done)
        {
            try
            {
                if (!IsReady) { done(null); return; }
                auth.CurrentUser.TokenAsync(forceRefresh).ContinueWithOnMainThread(t =>
                {
                    done(!t.IsFaulted && !t.IsCanceled && !string.IsNullOrEmpty(t.Result) ? t.Result : null);
                });
            }
            catch (Exception) { done(null); }
        }

        public void SignOut()
        {
            try { if (auth != null) auth.SignOut(); }
            catch (Exception) { }
        }

        static void Finish(Action<LbSignInOutcome, string> done, LbSignInOutcome outcome, string error)
        {
            if (done != null) done(outcome, error);
        }
    }
}
#endif
