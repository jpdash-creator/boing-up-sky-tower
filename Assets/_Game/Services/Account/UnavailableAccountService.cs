using System;

namespace BoingUpSkyTower.Monetization
{
    /// <summary>Release build without the Play Games SDK: no account. The login screen then shows only "Play as guest".</summary>
    public sealed class UnavailableAccountService : IAccountService
    {
        public bool IsAvailable { get { return false; } }
        public bool IsSignedIn { get { return false; } }
        public string DisplayName { get { return ""; } }
        public string PlayerId { get { return ""; } }
        public bool LastLoadFailed { get { return false; } }
        public void SignIn(Action<SignInResult> done) { if (done != null) done(new SignInResult { Status = SignInOutcome.Unavailable, Error = "no Play Games SDK in this build" }); }
        public void SilentSignIn(Action<SignInResult> done) { SignIn(done); }
        public void SignOut() { }
        public void LoadCloudSave(Action<byte[]> done) { if (done != null) done(null); }
        public void SaveCloud(byte[] data, Action<bool> done) { if (done != null) done(false); }
    }
}
