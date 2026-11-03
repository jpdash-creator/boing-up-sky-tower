using System;
using UnityEngine;

namespace BoingUpSkyTower.Monetization
{
    /// <summary>
    /// Editor and dev-build account: signs in as "Test Player" at once and keeps a fake cloud blob in PlayerPrefs (key boing.stub.cloud, base64), so the
    /// whole flow (first link, conflict, restore) can be tried without the Google SDK. Used whenever BOING_GPGS is off. Delete the key to clear the "cloud".
    /// </summary>
    public sealed class StubAccountService : IAccountService
    {
        public const string CloudKey = "boing.stub.cloud";
        public const string SignedInKey = "boing.stub.signedin";

        bool signedIn;
        /// <summary>Tests: make the next sign-in fail or get cancelled, or the cloud read/write fail.</summary>
        public bool FailSignIn, FailCloud, CancelSignIn;
        /// <summary>Tests: keep the blob in memory instead of PlayerPrefs.</summary>
        public bool UseMemoryOnly;
        byte[] memory;

        public bool IsAvailable { get { return true; } }
        public bool IsSignedIn { get { return signedIn; } }
        public string DisplayName { get { return signedIn ? "Test Player" : ""; } }
        public string PlayerId { get { return signedIn ? "stub-player-1" : ""; } }
        public bool LastLoadFailed { get; private set; }

        public void SignIn(Action<SignInResult> done)
        {
            var r = new SignInResult();
            if (CancelSignIn) r.Status = SignInOutcome.Cancelled;
            else if (FailSignIn) { r.Status = SignInOutcome.Failed; r.Error = "stub failure"; }
            else
            {
                signedIn = true;
                if (!UseMemoryOnly) PlayerPrefs.SetInt(SignedInKey, 1);
                r.Status = SignInOutcome.Success; r.PlayerId = PlayerId; r.DisplayName = DisplayName;
            }
            if (done != null) done(r);
        }

        public void SilentSignIn(Action<SignInResult> done)
        {
            // like the real plugin: only a player who signed in before comes back without a sheet
            bool before = !UseMemoryOnly && PlayerPrefs.GetInt(SignedInKey, 0) == 1;
            if (!before && !signedIn) { if (done != null) done(new SignInResult { Status = SignInOutcome.Failed, Error = "not signed in before" }); return; }
            SignIn(done);
        }

        public void SignOut() { signedIn = false; if (!UseMemoryOnly) PlayerPrefs.SetInt(SignedInKey, 0); }

        public void LoadCloudSave(Action<byte[]> done)
        {
            LastLoadFailed = false;
            if (!signedIn || FailCloud) { LastLoadFailed = true; if (done != null) done(null); return; }
            byte[] blob = memory;
            if (!UseMemoryOnly)
            {
                string s = PlayerPrefs.GetString(CloudKey, "");
                blob = string.IsNullOrEmpty(s) ? null : Convert.FromBase64String(s);
            }
            if (done != null) done(blob);
        }

        public void SaveCloud(byte[] data, Action<bool> done)
        {
            if (!signedIn || FailCloud || data == null) { if (done != null) done(false); return; }
            if (UseMemoryOnly) memory = data;
            else { PlayerPrefs.SetString(CloudKey, Convert.ToBase64String(data)); PlayerPrefs.Save(); }
            if (done != null) done(true);
        }

        /// <summary>Tests and QA: put a blob in the fake cloud.</summary>
        public void SeedCloud(byte[] data)
        {
            if (UseMemoryOnly) memory = data;
            else PlayerPrefs.SetString(CloudKey, data == null ? "" : Convert.ToBase64String(data));
        }
    }
}
