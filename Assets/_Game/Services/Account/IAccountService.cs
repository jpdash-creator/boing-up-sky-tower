using System;

namespace FluffyClimbers.Monetization
{
    public enum SignInOutcome
    {
        Success,
        /// <summary>The player closed the Google sign-in sheet.</summary>
        Cancelled,
        Failed,
        /// <summary>No SDK in this build (or not Android).</summary>
        Unavailable
    }

    public sealed class SignInResult
    {
        public SignInOutcome Status;
        public string PlayerId;
        public string DisplayName;
        public string Error;
        public bool Success { get { return Status == SignInOutcome.Success; } }
    }

    /// <summary>
    /// The player's Google account (docs/35). Progress lives in Play Games Saved Games and purchases in Google Play Billing: there is no own server.
    /// All callbacks run on the main thread, exactly once. Nothing here blocks; failures are reported, never thrown.
    /// </summary>
    public interface IAccountService
    {
        bool IsSignedIn { get; }
        /// <summary>The Play Games name of the signed-in player, or "" when signed out.</summary>
        string DisplayName { get; }
        /// <summary>The Play Games player id (stable per game), or "" when signed out.</summary>
        string PlayerId { get; }
        /// <summary>False when this build or device cannot sign in at all (the login screen then offers only the guest button).</summary>
        bool IsAvailable { get; }

        /// <summary>Interactive sign-in (the Google sheet may appear). Used by the login screen and Settings.</summary>
        void SignIn(Action<SignInResult> done);
        /// <summary>Signs in only when it needs no UI (a returning player). Used at app start.</summary>
        void SilentSignIn(Action<SignInResult> done);
        /// <summary>Forgets the account on this device. Play Games has no real sign-out, so the game just stops using it.</summary>
        void SignOut();

        /// <summary>Reads the cloud save. null means no cloud save or a failed read: check <see cref="LastLoadFailed"/>.</summary>
        void LoadCloudSave(Action<byte[]> done);
        /// <summary>True when the last LoadCloudSave failed (offline, error) instead of finding no save.</summary>
        bool LastLoadFailed { get; }
        /// <summary>Writes the cloud save (replaces the previous one). done(true) when stored.</summary>
        void SaveCloud(byte[] data, Action<bool> done);
    }
}
