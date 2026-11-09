using System;
using FluffyClimbers.Economy;
using FluffyClimbers.Monetization;
using UnityEngine;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// Glue between the progress (ProgressService) and the Google account (Services.Account): signs in, links a guest save to the account, decides
    /// with <see cref="SaveSync"/> what to upload or download, shows the "choose your save" popup when both sides differ, and uploads after changes
    /// (debounced; a failure is ignored and retried). No per-frame work apart from one float compare in the runner (CUX-1). See docs/35.
    /// </summary>
    public static class AccountSync
    {
        /// <summary>Seconds after the last change before the save is uploaded (a level result then a purchase become one upload).</summary>
        public const float DebounceSeconds = 6f;
        /// <summary>Seconds before a failed upload is tried again.</summary>
        public const float RetrySeconds = 45f;
        const int MaxStartAttempts = 6;

        static ProgressService progress;
        static Runner runner;
        static bool dirty, uploading, syncReady, blockUpload, startInFlight, pendingCheck;
        static float uploadAt;
        static int startAttempts;

        /// <summary>Canvas that hosts the popups; set by <see cref="LoginFlow.Begin"/>. A destroyed canvas (scene change) defers the popup.</summary>
        public static Transform Parent;
        /// <summary>Called after the cloud save replaced the device save, so the open screen can redraw.</summary>
        public static Action RefreshUi;
        static GameObject conflictRoot;

        public static IAccountService Account { get { return Services.Account; } }

        public static bool IsGoogleMode { get { return progress != null && progress.Data.AccountMode == SaveSync.ModeGoogle; } }
        public static bool IsSignedIn { get { return IsGoogleMode && Account.IsSignedIn; } }
        public static bool CloudReady { get { return syncReady && !blockUpload; } }

        /// <summary>For Settings: "Signed in as Name" or "Guest: progress is saved on this device only".</summary>
        public static string AccountLine
        {
            get
            {
                if (!IsGoogleMode) return "Guest: progress is saved on this device only";
                if (Account.IsSignedIn && !string.IsNullOrEmpty(Account.DisplayName)) return "Signed in as " + Account.DisplayName;
                return "Google account (offline)";
            }
        }

        /// <summary>Starts watching a ProgressService (once per instance): every change schedules an upload while a Google account is linked and in step.</summary>
        public static void Hook(ProgressService p)
        {
            if (p == null || ReferenceEquals(p, progress)) return;
            if (progress != null) progress.Changed -= OnChanged;
            progress = p;
            progress.Changed += OnChanged;
            dirty = uploading = syncReady = blockUpload = startInFlight = pendingCheck = false;
            startAttempts = 0;
            if (runner == null)
            {
                var go = new GameObject("AccountSync");
                if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(go);
                runner = go.AddComponent<Runner>();
            }
        }

        static bool Active { get { return progress != null && syncReady && !blockUpload && IsGoogleMode && Account.IsSignedIn; } }

        static void OnChanged()
        {
            if (!Active) return;
            if (!dirty) uploadAt = Time.unscaledTime + DebounceSeconds;   // keeps the first deadline: a busy stretch still uploads every few seconds
            dirty = true;
        }

        static void QueueUpload(bool immediate)
        {
            dirty = true;
            uploadAt = immediate ? 0f : Time.unscaledTime + DebounceSeconds;
        }

        static void Tick()
        {
            if (!dirty || uploading) return;
            if (Time.unscaledTime < uploadAt) return;
            Flush();
        }

        /// <summary>Uploads now if something is waiting (app pause or quit).</summary>
        public static void FlushNow()
        {
            if (dirty && !uploading) Flush();
        }

        static void Flush()
        {
            dirty = false;
            if (!Active) return;
            uploading = true;
            byte[] bytes = SaveSync.Serialize(progress.Data);
            ProgressService p = progress;
            Account.SaveCloud(bytes, ok =>
            {
                uploading = false;
                if (!ReferenceEquals(p, progress)) return;
                if (ok) progress.MarkCloudSynced();
                else { dirty = true; uploadAt = Time.unscaledTime + RetrySeconds; }   // ignored now, retried later
            });
        }

        // ---------------- app start ----------------

        /// <summary>A returning Google player: sign in quietly, read the cloud save and apply the rules. Offline or failed: play on, try again when the app regains focus.</summary>
        public static void OnAppStart()
        {
            if (progress == null || !IsGoogleMode || startInFlight || syncReady) return;
            if (++startAttempts > MaxStartAttempts) return;
            startInFlight = true;
            Account.SilentSignIn(r =>
            {
                if (!r.Success) { startInFlight = false; pendingCheck = true; return; }
                LoadAndResolve(null);
            });
        }

        static void LoadAndResolve(Action after)
        {
            Account.LoadCloudSave(bytes =>
            {
                startInFlight = false;
                if (Account.LastLoadFailed) { pendingCheck = true; if (after != null) after(); return; }
                pendingCheck = false;
                Resolve(bytes, Account.PlayerId, after);
            });
        }

        static void Resume()
        {
            if (pendingCheck && IsGoogleMode && !startInFlight && !syncReady)
            {
                if (Account.IsSignedIn) { startInFlight = true; LoadAndResolve(null); }
                else OnAppStart();
            }
        }

        // ---------------- sign in / out (login screen and Settings) ----------------

        /// <summary>Interactive sign-in. done(ok, message): message is a short player-facing reason when ok is false.</summary>
        public static void SignInAndLink(Action<bool, string> done)
        {
            if (progress == null) { if (done != null) done(false, "Not ready yet."); return; }
            if (!Account.IsAvailable) { if (done != null) done(false, "Google Play sign-in is not available in this build."); return; }
            Account.SignIn(r =>
            {
                if (!r.Success)
                {
                    if (done != null) done(false, r.Status == SignInOutcome.Cancelled ? "Sign-in cancelled." : "Could not sign in. Check your connection and try again.");
                    return;
                }
                string id = Account.PlayerId;
                Account.LoadCloudSave(bytes =>
                {
                    if (Account.LastLoadFailed)
                    {
                        // signed in, but the cloud is out of reach: link now, compare later (uploads wait until then)
                        progress.SetAccount(SaveSync.ModeGoogle, id);
                        syncReady = false;
                        pendingCheck = true;
                        if (done != null) done(true, null);
                        return;
                    }
                    Resolve(bytes, id, () => { if (done != null) done(true, null); });
                });
            });
        }

        /// <summary>Back to guest play: the progress stays on this device and is no longer uploaded. The cloud save is left untouched.</summary>
        public static void SignOut()
        {
            if (progress == null) return;
            FlushNow();
            Account.SignOut();
            progress.SetAccount(SaveSync.ModeGuest, "");
            syncReady = false;
            dirty = false;
            blockUpload = false;
        }

        /// <summary>
        /// "Sync now" (Account popup): uploads the save at once when the account is in step, otherwise signs in quietly and compares the saves again
        /// (the choose-your-save popup appears if they differ). done(ok, message) always runs once, with a short player-facing message.
        /// </summary>
        public static void SyncNow(Action<bool, string> done)
        {
            if (progress == null || !IsGoogleMode) { if (done != null) done(false, "Sign in first."); return; }
            if (blockUpload) { if (done != null) done(false, "Your account has a newer save. Update the app to sync."); return; }
            if (uploading || startInFlight) { if (done != null) done(true, "Already syncing."); return; }
            if (Active)
            {
                dirty = false;
                uploading = true;
                byte[] bytes = SaveSync.Serialize(progress.Data);
                ProgressService p = progress;
                Account.SaveCloud(bytes, ok =>
                {
                    uploading = false;
                    if (ReferenceEquals(p, progress))
                    {
                        if (ok) progress.MarkCloudSynced();
                        else { dirty = true; uploadAt = Time.unscaledTime + RetrySeconds; }
                    }
                    if (done != null) done(ok, ok ? "Synced." : "Could not sync. It will be tried again later.");
                });
                return;
            }
            startInFlight = true;
            Action<string> finish = fail =>
            {
                if (done != null) done(syncReady && fail == null, fail ?? (syncReady ? "Synced." : "Could not sync yet."));
            };
            if (Account.IsSignedIn) { LoadAndResolve(() => finish(null)); return; }
            Account.SilentSignIn(r =>
            {
                if (!r.Success) { startInFlight = false; pendingCheck = true; finish("Could not reach your account. Check your connection."); return; }
                LoadAndResolve(() => finish(null));
            });
        }

        public static void ChooseGuest()
        {
            if (progress != null) progress.SetAccount(SaveSync.ModeGuest, "");
        }

        // ---------------- decide and apply ----------------

        static void Resolve(byte[] bytes, string accountId, Action after)
        {
            SaveEnvelope env = SaveSync.TryDeserialize(bytes);   // empty or corrupt = no usable cloud save
            SyncDecision decision = SaveSync.Decide(progress.Data, env, accountId);
            bool newerCloud = env != null && !SaveSync.IsReadable(env);
            switch (decision)
            {
                case SyncDecision.UseCloud:
                    ApplyCloud(env, accountId);
                    break;
                case SyncDecision.UploadLocal:
                    Link(accountId);
                    syncReady = true;
                    QueueUpload(true);
                    break;
                case SyncDecision.AskPlayer:
                    if (Parent == null) { pendingCheck = true; if (after != null) after(); return; }   // no screen to show it on: ask next time
                    ShowConflict(env, accountId, after);
                    return;
                default:
                    Link(accountId);
                    blockUpload = newerCloud;          // a save from a newer app version is never overwritten
                    syncReady = true;
                    if (!newerCloud && env != null) progress.MarkCloudSynced();
                    break;
            }
            if (after != null) after();
        }

        static void Link(string accountId)
        {
            if (!(IsGoogleMode && progress.Data.AccountId == accountId)) progress.SetAccount(SaveSync.ModeGoogle, accountId);
        }

        static void ApplyCloud(SaveEnvelope env, string accountId)
        {
            progress.ReplaceData(SaveSync.PrepareCloudForDevice(env, accountId));
            progress.MarkCloudSynced();
            syncReady = true;
            blockUpload = false;
            if (RefreshUi != null && Parent != null) RefreshUi();
        }

        static void ShowConflict(SaveEnvelope env, string accountId, Action after)
        {
            CloseConflict();
            SaveSummary device = SaveSync.Summarize(progress.Data);
            SaveSummary google = env.Summary ?? SaveSync.Summarize(env.Data);
            RectTransform root = LoginScreen.BuildConflict(Parent, device, google, Account.DisplayName,
                () =>
                {
                    CloseConflict();
                    Link(accountId);
                    syncReady = true;
                    QueueUpload(true);     // the device save replaces the account save
                    if (after != null) after();
                },
                () =>
                {
                    CloseConflict();
                    ApplyCloud(env, accountId);
                    if (after != null) after();
                });
            conflictRoot = root.gameObject;
        }

        static void CloseConflict()
        {
            if (conflictRoot != null) UnityEngine.Object.Destroy(conflictRoot);
            conflictRoot = null;
        }

        /// <summary>Tests: forget everything.</summary>
        public static void ResetForTests()
        {
            if (progress != null) progress.Changed -= OnChanged;
            progress = null;
            dirty = uploading = syncReady = blockUpload = startInFlight = pendingCheck = false;
            startAttempts = 0;
            Parent = null;
            RefreshUi = null;
            CloseConflict();
            if (runner != null) UnityEngine.Object.DestroyImmediate(runner.gameObject);
            runner = null;
        }

        /// <summary>Tests: run one debounce check.</summary>
        public static void TickForTests() { Tick(); }
        public static bool UploadPending { get { return dirty; } }

        sealed class Runner : MonoBehaviour
        {
            void Update() { Tick(); }
            void OnApplicationPause(bool paused) { if (paused) FlushNow(); else Resume(); }
            void OnApplicationQuit() { FlushNow(); }
        }
    }

    /// <summary>
    /// The entry points the Meta scene uses (docs/35): <see cref="NeedsLogin"/> and <see cref="Begin"/>. One call from MetaController.Start is enough:
    /// <c>LoginFlow.Begin(canvas.transform, ShowMenu);</c>
    /// </summary>
    public static class LoginFlow
    {
        static GameObject loginRoot;

        public static bool NeedsLogin(ProgressService progress)
        {
            return progress != null && SaveSync.NeedsLogin(progress.Data);
        }

        /// <summary>
        /// Call at the end of MetaController.Start. First launch: covers the screen with the login. Later launches: nothing visible happens; a
        /// returning Google player is synced in the background (a popup appears only when the two saves differ). refreshUi redraws the open
        /// screen after a cloud save was applied.
        /// </summary>
        public static void Begin(Transform canvas, Action refreshUi)
        {
            ProgressService p = GameSession.Progress;
            if (p == null || canvas == null) return;
            AccountSync.Hook(p);
            if (Application.isBatchMode) return;   // automated test runs (layout audits, PlayMode tests) start on a fresh save: never cover their screens with the login
            AccountSync.Parent = canvas;
            AccountSync.RefreshUi = refreshUi;
            if (NeedsLogin(p)) Show(canvas, null);
            else AccountSync.OnAppStart();
        }

        /// <summary>The login screen on the given canvas; onDone runs when the player has chosen (Google signed in, or guest).</summary>
        public static LoginScreen.LoginView Show(Transform canvas, Action onDone)
        {
            ProgressService p = GameSession.Progress;
            AccountSync.Hook(p);
            if (AccountSync.Parent == null) AccountSync.Parent = canvas;
            if (loginRoot != null) UnityEngine.Object.Destroy(loginRoot);
            LoginScreen.LoginView view = null;
            bool busy = false;
            Action finish = () =>
            {
                if (view != null && view.Root != null) UnityEngine.Object.Destroy(view.Root.gameObject);
                loginRoot = null;
                if (onDone != null) onDone();
            };
            view = LoginScreen.BuildLogin(canvas, GameSession.Art, AccountSync.Account.IsAvailable,
                () =>
                {
                    if (busy) return;
                    busy = true;
                    UiKit.SetText(view.Status, "Signing in...");
                    AccountSync.SignInAndLink((ok, message) =>
                    {
                        busy = false;
                        if (view == null || view.Root == null) return;
                        if (ok) finish();
                        else UiKit.SetText(view.Status, message ?? "");
                    });
                },
                () =>
                {
                    if (busy) return;
                    AccountSync.ChooseGuest();
                    finish();
                });
            loginRoot = view.Root.gameObject;
            return view;
        }
    }
}
