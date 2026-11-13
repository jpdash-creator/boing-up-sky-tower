using System;

namespace FluffyClimbers.Leaderboard
{
    public enum LbSignInOutcome { Success, Cancelled, Failed, Unavailable }

    /// <summary>Who the leaderboards show: the Play Games display name and the profile picture URL.</summary>
    public sealed class LbProfile
    {
        public string DisplayName = "", AvatarUrl = "";
    }

    /// <summary>
    /// The player's Firebase identity for the leaderboard service: Play Games server auth code -> Firebase Auth credential -> uid and ID token. The real implementation is in the
    /// Firebase assembly (Assets/_Game/Services/Firebase, compiled only with the Firebase packages and Play Games); the stub below serves the editor and the tests.
    /// All callbacks run on the main thread, exactly once. Nothing blocks; failures are reported, never thrown.
    /// </summary>
    public interface ILeaderboardAuth
    {
        /// <summary>False when this build cannot sign in to Firebase at all (no SDK, not Android): the leaderboard then offers nothing to sign in to.</summary>
        bool IsAvailable { get; }
        /// <summary>A Firebase user exists (ID tokens can be fetched).</summary>
        bool IsReady { get; }
        /// <summary>Firebase uid, "" when not ready.</summary>
        string Uid { get; }
        /// <summary>Play Games name and picture URL ("" when unknown).</summary>
        LbProfile Profile { get; }
        /// <summary>Interactive or silent sign-in (Play Games must already be signed in; the real service asks Play Games for the server auth code and exchanges it).</summary>
        void SignIn(Action<LbSignInOutcome, string> done);
        /// <summary>A valid ID token (refreshed by the SDK when needed; <paramref name="forceRefresh"/> after a 401). null on failure.</summary>
        void GetIdToken(bool forceRefresh, Action<string> done);
        /// <summary>Forgets the Firebase user (Play Games has no real sign-out; the game just stops using it).</summary>
        void SignOut();
    }

    /// <summary>Registration point for the Firebase implementation (the Firebase assembly sets it at startup; the Leaderboard assembly does not reference it).</summary>
    public static class LeaderboardServices
    {
        /// <summary>Set by the Firebase assembly when the SDK is compiled in.</summary>
        public static Func<ILeaderboardAuth> AuthFactory;
        /// <summary>The editor and development builds without Firebase use the stub (signs in as "Test Player" at once). Release builds without the SDK get the unavailable auth.</summary>
        public static bool? StubAuthOverride;
        public static bool AllowStubAuth
        {
            get { return StubAuthOverride.HasValue ? StubAuthOverride.Value : (UnityEngine.Application.isEditor || UnityEngine.Debug.isDebugBuild); }
        }

        public static ILeaderboardAuth CreateAuth()
        {
            if (!UnityEngine.Application.isEditor && AuthFactory != null)
            {
                try { return AuthFactory(); }
                catch (Exception e) { UnityEngine.Debug.LogWarning("[Leaderboard] Firebase auth unavailable: " + e.Message); }
            }
            if (AllowStubAuth) return new StubLeaderboardAuth();
            return new UnavailableLeaderboardAuth();
        }
    }

    /// <summary>No Firebase in this build: the leaderboard never signs in.</summary>
    public sealed class UnavailableLeaderboardAuth : ILeaderboardAuth
    {
        static readonly LbProfile Empty = new LbProfile();
        public bool IsAvailable { get { return false; } }
        public bool IsReady { get { return false; } }
        public string Uid { get { return ""; } }
        public LbProfile Profile { get { return Empty; } }
        public void SignIn(Action<LbSignInOutcome, string> done) { if (done != null) done(LbSignInOutcome.Unavailable, "no Firebase in this build"); }
        public void GetIdToken(bool forceRefresh, Action<string> done) { if (done != null) done(null); }
        public void SignOut() { }
    }

    /// <summary>Editor, tests and development builds: signs in at once as "Test Player" with a fake token (the real service refuses it; the local test service accepts "Dev" tokens).</summary>
    public sealed class StubLeaderboardAuth : ILeaderboardAuth
    {
        bool ready;
        public bool FailSignIn, CancelSignIn;
        /// <summary>Tests: what GetIdToken returns (null = failure).</summary>
        public string Token = "stub-token";
        /// <summary>Tests: how many tokens were requested and how many of them were forced refreshes.</summary>
        public int TokenCalls, RefreshCalls;
        public LbProfile ProfileValue = new LbProfile { DisplayName = "Test Player", AvatarUrl = "" };

        public bool IsAvailable { get { return true; } }
        public bool IsReady { get { return ready; } }
        public string Uid { get { return ready ? "stub-uid" : ""; } }
        public LbProfile Profile { get { return ProfileValue; } }

        public void SignIn(Action<LbSignInOutcome, string> done)
        {
            if (CancelSignIn) { if (done != null) done(LbSignInOutcome.Cancelled, "cancelled"); return; }
            if (FailSignIn) { if (done != null) done(LbSignInOutcome.Failed, "stub failure"); return; }
            ready = true;
            if (done != null) done(LbSignInOutcome.Success, null);
        }

        public void GetIdToken(bool forceRefresh, Action<string> done)
        {
            TokenCalls++;
            if (forceRefresh) RefreshCalls++;
            if (done != null) done(ready ? Token : null);
        }

        public void SignOut() { ready = false; }
    }
}
