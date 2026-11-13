using System;
using System.Collections.Generic;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Monetization;

namespace FluffyClimbers.Leaderboard
{
    /// <summary>The result of tapping a Weekly Prize card.</summary>
    public enum ClaimStatus { Granted, AlreadyClaimed, Busy, Offline, NotEligible, Failed }

    public sealed class ClaimOutcome
    {
        public ClaimStatus Status;
        /// <summary>What was granted (coins, power-ups, hero cards) when <see cref="Status"/> is Granted; null otherwise.</summary>
        public RewardClaimOutcome Reward;
        /// <summary>Server error code of a refusal ("week_open", "not_eligible" ...), "" otherwise.</summary>
        public string Code = "";
    }

    /// <summary>
    /// The one place that knows the weekly leaderboard state of the app (static, like AccountSync): the switch, the sign-in, the run tickets, the offline upload queue, the own standing, the boards and
    /// the prize claims. Everything is asynchronous and silent on failure: a missing network, a signed-out player, a switched-off feature or a refused call only means "not on the boards", never an
    /// error for play. All state is plain fields; the only periodic work is <see cref="Tick"/> (every 2 s, driven by <see cref="LeaderboardRunner"/>). Tests inject the parts with <see cref="Setup"/>.
    /// </summary>
    public static class LeaderboardHub
    {
        /// <summary>A ticket is not used when it expires within this many seconds.</summary>
        public const int TicketMarginSeconds = 60;
        /// <summary>The boards and the own standing are re-read at most this often while a screen is open.</summary>
        public const int CacheSeconds = 20;
        const int MaxSignInTries = 5;

        static ProgressService progress;
        static LeaderboardConfig cfg = LeaderboardConfig.Off;
        static ILeaderboardAuth auth;
        static ILeaderboardTransport net;
        static LeaderboardClient client;
        static bool slowTickHooked;

        /// <summary>The clock (UTC). Tests replace it.</summary>
        public static Func<DateTime> Clock = () => DateTime.UtcNow;
        /// <summary>Raised when the standing, a board, the sign-in or the prizes changed. Screens subscribe while they are on screen and unsubscribe when they are destroyed.</summary>
        public static event Action Changed;

        static readonly Dictionary<string, StartTicket> tickets = new Dictionary<string, StartTicket>();
        static readonly HashSet<string> ticketsInFlight = new HashSet<string>();
        static readonly Dictionary<string, BoardData> boards = new Dictionary<string, BoardData>();
        static readonly Dictionary<string, long> boardsAt = new Dictionary<string, long>();
        static readonly HashSet<string> boardsInFlight = new HashSet<string>();
        static MyWeekData myWeek;
        static long myWeekAt;
        static bool myWeekInFlight, sending, claiming, signingIn;
        static string profileKey = "";
        static int signInTries;
        static long nextSignInTry;

        // ---------------- setup ----------------

        /// <summary>Real setup, called once when the menu starts: reads the remote config switch and builds the Firebase auth (or the stub) and the web transport. Safe to call again.</summary>
        public static void Init(ProgressService p, RemoteConfigDef remote, bool forceOn = false)
        {
            LeaderboardConfig c = LeaderboardConfig.From(remote, forceOn);
            bool same = client != null && ReferenceEquals(progress, p) && cfg.Enabled == c.Enabled && cfg.BaseUrl == c.BaseUrl;
            if (!same)
            {
                ILeaderboardAuth a = auth != null && ReferenceEquals(progress, p) && !(c.IsLive && auth is UnavailableLeaderboardAuth) ? auth : (c.IsLive ? LeaderboardServices.CreateAuth() : new UnavailableLeaderboardAuth());
                Setup(p, c, a, net ?? new UnityWebTransport());
            }
            if (!slowTickHooked && cfg.IsLive && UnityEngine.Application.isPlaying)
            {
                slowTickHooked = true;
                LeaderboardRunner.Get().OnTick = Tick;
            }
        }

        /// <summary>Injects the parts (tests and Init). Clears every cache.</summary>
        public static void Setup(ProgressService p, LeaderboardConfig config, ILeaderboardAuth authService, ILeaderboardTransport transport)
        {
            progress = p;
            cfg = config ?? LeaderboardConfig.Off;
            auth = authService ?? new UnavailableLeaderboardAuth();
            net = transport;
            client = net != null ? new LeaderboardClient(auth, net, cfg) : null;
            ClearCaches();
        }

        /// <summary>Tests: forget everything.</summary>
        public static void ResetForTests()
        {
            progress = null; cfg = LeaderboardConfig.Off; auth = null; net = null; client = null;
            Clock = () => DateTime.UtcNow;
            ClearCaches();
        }

        static void ClearCaches()
        {
            tickets.Clear(); ticketsInFlight.Clear(); boards.Clear(); boardsAt.Clear(); boardsInFlight.Clear();
            myWeek = null; myWeekAt = 0; myWeekInFlight = sending = claiming = signingIn = false;
            profileKey = ""; signInTries = 0; nextSignInTry = 0;
        }

        static long Now { get { return LeaderboardTime.ToUnix(Clock()); } }
        static void Raise() { if (Changed != null) Changed(); }

        // ---------------- state ----------------

        /// <summary>The feature is switched on in the remote config, has a real service URL and this build can sign in to it.</summary>
        public static bool Enabled { get { return cfg.IsLive && auth != null && auth.IsAvailable; } }
        public static LeaderboardConfig Config { get { return cfg; } }
        /// <summary>A Firebase user exists: boards can be read and runs uploaded (consent permitting).</summary>
        public static bool IsSignedIn { get { return Enabled && auth.IsReady; } }
        /// <summary>Set by the game: true when the player chose the Google account mode (a guest never takes part, even when Play Games happens to be signed in on the phone). Null = no extra condition.</summary>
        public static Func<bool> AccountModeIsGoogle;
        /// <summary>The Google Play Games account is signed in (the first step of the sign-in) and the player plays with it, not as a guest.</summary>
        public static bool IsGoogleSignedIn { get { return Services.Account != null && Services.Account.IsSignedIn && (AccountModeIsGoogle == null || AccountModeIsGoogle()); } }
        public static bool HasConsent { get { return progress != null && progress.HasLeaderboardConsent; } }
        /// <summary>The player's runs are sent: switched on, signed in, consent given.</summary>
        public static bool CanUpload { get { return IsSignedIn && HasConsent && client != null; } }
        public static LbProfile Profile { get { return auth != null ? auth.Profile : new LbProfile(); } }
        public static string Uid { get { return auth != null ? auth.Uid : ""; } }
        public static MyWeekData MyWeek { get { return myWeek; } }
        public static bool QueueBusy { get { return sending; } }
        public static int QueuedRuns { get { return progress != null ? progress.LeaderboardQueue.Count : 0; } }

        /// <summary>Time left in the current week (Monday 00:00 India time).</summary>
        public static TimeSpan WeekRemaining { get { return LeaderboardTime.Remaining(Clock()); } }

        /// <summary>The current week key from the device clock (the service decides with its own clock when a run is received).</summary>
        public static string CurrentWeekKey { get { return LeaderboardTime.WeekKey(Clock()); } }

        // ---------------- sign-in and consent ----------------

        /// <summary>
        /// Signs in to Firebase (Play Games must be signed in already). Silent when called at start, interactive when the player pressed the button; either way done(true) only when a Firebase user exists.
        /// </summary>
        public static void SignIn(Action<bool> done)
        {
            if (!Enabled) { if (done != null) done(false); return; }
            if (auth.IsReady) { AfterSignIn(); if (done != null) done(true); return; }
            if (signingIn) { if (done != null) done(false); return; }
            signingIn = true;
            auth.SignIn((outcome, error) =>
            {
                signingIn = false;
                bool ok = outcome == LbSignInOutcome.Success && auth.IsReady;
                if (ok) AfterSignIn();
                Raise();
                if (done != null) done(ok);
            });
        }

        static void AfterSignIn()
        {
            if (!CanUpload) return;
            SyncProfile();
            Pump();
            RefreshMyWeek(true, null);
        }

        /// <summary>Called when the menu starts: a returning player who accepted the consent is signed in to Firebase silently (retried a few times while Play Games is still signing in).</summary>
        public static void OnMenuStart()
        {
            if (!Enabled || !HasConsent) return;
            TrySilentSignIn();
        }

        static void TrySilentSignIn()
        {
            if (auth.IsReady || signingIn || signInTries >= MaxSignInTries || Now < nextSignInTry) return;
            if (!IsGoogleSignedIn) return;
            signInTries++;
            nextSignInTry = Now + 20;
            SignIn(null);
        }

        /// <summary>The player pressed "Join leaderboards": stores the consent, writes the profile, sends what is queued.</summary>
        public static void AcceptConsent()
        {
            if (progress == null) return;
            progress.GrantLeaderboardConsent(Now);
            if (auth != null && auth.IsReady) AfterSignIn();
            else if (Enabled) { signInTries = 0; nextSignInTry = 0; TrySilentSignIn(); }
            Raise();
        }

        /// <summary>"Not now": no consent is stored; the popup comes back tomorrow at the earliest.</summary>
        public static void DeclineConsent()
        {
            if (progress != null) progress.DeclineLeaderboardConsent(LeaderboardTime.DayKey(Clock()));
            Raise();
        }

        /// <summary>The consent popup should be shown now (consent missing and not declined today).</summary>
        public static bool ShouldAskConsent { get { return progress != null && progress.ShouldAskLeaderboardConsent(LeaderboardTime.DayKey(Clock())); } }

        /// <summary>"Leave leaderboards" (Settings): consent and queue are cleared, the cached standing and tickets are dropped. Play Games itself stays signed in.</summary>
        public static void LeaveLeaderboards()
        {
            if (progress != null) progress.RevokeLeaderboardConsent();
            tickets.Clear(); boards.Clear(); boardsAt.Clear(); myWeek = null; myWeekAt = 0; profileKey = "";
            Raise();
        }

        /// <summary>The name shown to other players: the player's own display name when set (Settings), else the Google Play Games name.</summary>
        public static string ShownName
        {
            get { return LeaderboardNameRules.Resolve(progress != null ? progress.LeaderboardDisplayName : "", Profile.DisplayName); }
        }

        /// <summary>
        /// Sets (or, with an empty text, clears) the player's own display name. Validates with <see cref="LeaderboardNameRules"/>; nothing is stored unless the verdict is Ok (or the text is empty).
        /// Then the public profile is written again (<see cref="SyncProfile"/>: only when switched on, signed in and consent given; otherwise nothing is sent and the name goes out with the next sign-in).
        /// </summary>
        public static LeaderboardNameCheck SetDisplayName(string raw)
        {
            if (progress == null) return LeaderboardNameCheck.TooShort;
            string n = LeaderboardNameRules.Normalize(raw);
            if (n.Length > 0)
            {
                LeaderboardNameCheck v = LeaderboardNameRules.Validate(n);
                if (v != LeaderboardNameCheck.Ok) return v;
            }
            if (!progress.SetLeaderboardDisplayName(n)) return LeaderboardNameCheck.TooLong;
            SyncProfile();
            Raise();
            return LeaderboardNameCheck.Ok;
        }

        /// <summary>Writes the shown name (own display name, else the Play Games name) and the picture URL to players/{uid} once per sign-in (and again when either changes).</summary>
        public static void SyncProfile()
        {
            if (!CanUpload) return;
            LbProfile p = new LbProfile { DisplayName = ShownName, AvatarUrl = auth.Profile.AvatarUrl };
            string key = auth.Uid + "|" + LbJson.SafeName(p.DisplayName) + "|" + LbJson.SafeAvatar(p.AvatarUrl);
            if (key == profileKey) return;
            profileKey = key;   // set first: a failure below clears it so the next tick tries again
            client.WriteProfile(auth.Uid, p, ok => { if (!ok) profileKey = ""; });
        }

        // ---------------- run tickets ----------------

        public static string TicketKey(string kind, string target, int mode) { return kind + "|" + target + "|" + mode; }

        /// <summary>True when a run of this kind should try to get a ticket (signed in, consent given, switched on).</summary>
        public static bool WantsTicket { get { return CanUpload; } }

        /// <summary>True when a usable ticket for exactly this run is already cached (it is not removed).</summary>
        public static bool HasTicket(string kind, string target, int mode)
        {
            StartTicket t;
            return tickets.TryGetValue(TicketKey(kind, target, mode), out t) && t.ExpiresAtUnix - TicketMarginSeconds > Now;
        }

        /// <summary>A ready ticket for exactly this run, removed from the cache (a ticket is single use); null when there is none or it is about to expire.</summary>
        public static StartTicket TryTakeTicket(string kind, string target, int mode)
        {
            string key = TicketKey(kind, target, mode);
            StartTicket t;
            if (!tickets.TryGetValue(key, out t)) return null;
            tickets.Remove(key);
            return t.ExpiresAtUnix - TicketMarginSeconds > Now ? t : null;
        }

        /// <summary>
        /// Asks the service for a ticket and keeps it in the cache for <see cref="TryTakeTicket"/>; <paramref name="done"/> (may be null) gets it too. A late answer is still cached, so a slow call
        /// never blocks a run: the run starts with a local seed and the next attempt finds the ticket.
        /// </summary>
        public static void RequestTicket(string kind, string target, int mode, Action<StartTicket> done)
        {
            if (!CanUpload || string.IsNullOrEmpty(target)) { if (done != null) done(null); return; }
            string key = TicketKey(kind, target, mode);
            StartTicket have;
            if (tickets.TryGetValue(key, out have) && have.ExpiresAtUnix - TicketMarginSeconds > Now) { if (done != null) done(have); return; }
            if (!ticketsInFlight.Add(key)) { if (done != null) done(null); return; }
            client.StartRun(kind, kind == "level" ? target : null, kind == "level" ? null : target, mode, t =>
            {
                ticketsInFlight.Remove(key);
                if (t != null) { if (t.ExpiresAtUnix <= 0) t.ExpiresAtUnix = Now + 100 * 60; tickets[key] = t; }   // a ticket without a readable expiry is trusted for 100 minutes (the service lives 120)
                if (done != null) done(t);
            });
        }

        /// <summary>Fires RequestTicket without caring for the answer (the next attempt or the next level).</summary>
        public static void Prefetch(string kind, string target, int mode) { RequestTicket(kind, target, mode, null); }

        // ---------------- uploads ----------------

        /// <summary>
        /// Queues a finished run for upload and starts sending. Never blocks: a run without consent, sign-in or ticket is simply not uploaded. A run too large for the persisted queue is sent once, directly.
        /// </summary>
        public static void Submit(QueuedRun run)
        {
            if (run == null || !CanUpload) return;
            run.QueuedAtUnix = Now;
            if (!progress.EnqueueLeaderboardRun(run))
            {
                if (run.InputsB64 != null && run.InputsB64.Length > ProgressService.LeaderboardMaxRunChars && !sending)
                {
                    sending = true;
                    client.SubmitRun(run, r => { sending = false; if (r.Ok) MarkStale(); Pump(); });
                }
                return;
            }
            Pump();
        }

        /// <summary>Sends the first due run of the queue (one at a time). Called after a new run, after sign-in and every 2 s while runs wait.</summary>
        public static void Pump()
        {
            if (sending || !CanUpload) return;
            QueuedRun run = progress.NextDueLeaderboardRun(Now);
            if (run == null) return;
            sending = true;
            string id = run.RunId;
            client.SubmitRun(run, r =>
            {
                sending = false;
                if (r.Ok) { progress.RemoveLeaderboardRun(id); MarkStale(); Pump(); }
                else if (r.Retryable) progress.LeaderboardRunFailed(id, Now);   // backoff: 30 s, 1 min, 2 min ... at most 30 min, dropped after 12 tries
                else progress.RemoveLeaderboardRun(id);   // the service refused it for good (mismatch, ticket used or expired, update the app): never retried
            });
        }

        /// <summary>The slow tick: retry the queue, retry the silent sign-in, notice a new week.</summary>
        public static void Tick()
        {
            if (!Enabled) return;
            if (HasConsent && !auth.IsReady) TrySilentSignIn();
            if (!CanUpload) return;
            if (profileKey.Length == 0) SyncProfile();   // a failed profile write is retried
            if (QueuedRuns > 0) Pump();
            if (myWeek != null && myWeek.WeekKey != CurrentWeekKey && Now - myWeekAt > 5) RefreshMyWeek(true, null);
        }

        static void MarkStale() { myWeekAt = 0; foreach (string k in new List<string>(boardsAt.Keys)) boardsAt[k] = 0; }

        // ---------------- own standing and boards ----------------

        /// <summary>Reads GET /my-week (cached for <see cref="CacheSeconds"/> unless <paramref name="force"/>). done(null) when it could not be read; the previous standing stays in <see cref="MyWeek"/>.</summary>
        public static void RefreshMyWeek(bool withPrizes, Action<MyWeekData> done, bool force = false)
        {
            if (!CanUpload) { if (done != null) done(myWeek); return; }
            if (!force && myWeek != null && Now - myWeekAt < CacheSeconds && (!withPrizes || myWeek.PrizesKnown)) { if (done != null) done(myWeek); return; }
            if (myWeekInFlight) { if (done != null) done(myWeek); return; }
            myWeekInFlight = true;
            client.MyWeek(withPrizes, d =>
            {
                myWeekInFlight = false;
                if (d != null)
                {
                    if (!withPrizes && myWeek != null) { d.Pending = myWeek.Pending; d.PrizesKnown = myWeek.PrizesKnown; }   // keep the prizes already known
                    myWeek = d; myWeekAt = Now;
                    Raise();
                }
                if (done != null) done(d);
            });
        }

        /// <summary>The cached board (or null) of the current week.</summary>
        public static BoardData CachedBoard(string board)
        {
            BoardData b;
            return boards.TryGetValue(CurrentWeekKey + "|" + board, out b) ? b : null;
        }

        /// <summary>Reads the top 100 of a board (Firestore REST, one document; cached for <see cref="CacheSeconds"/>). done(null) on failure.</summary>
        public static void LoadBoard(string board, Action<BoardData> done, bool force = false)
        {
            if (!IsSignedIn || client == null) { if (done != null) done(null); return; }
            string wk = CurrentWeekKey;
            string key = wk + "|" + board;
            long at;
            BoardData have;
            if (!force && boards.TryGetValue(key, out have) && boardsAt.TryGetValue(key, out at) && Now - at < CacheSeconds) { if (done != null) done(have); return; }
            if (!boardsInFlight.Add(key)) { if (done != null) done(CachedBoard(board)); return; }
            client.FetchBoard(wk, board, b =>
            {
                boardsInFlight.Remove(key);
                if (b != null) { boards[key] = b; boardsAt[key] = Now; Raise(); }
                if (done != null) done(b);
            });
        }

        // ---------------- prizes ----------------

        /// <summary>The prizes the service says can be claimed now (closed weeks, paid rank, not granted yet).</summary>
        public static List<PendingPrize> ClaimablePrizes()
        {
            var list = new List<PendingPrize>();
            if (myWeek == null || myWeek.Pending == null) return list;
            foreach (PendingPrize p in myWeek.Pending) if (p.Claimable) list.Add(p);
            return list;
        }

        /// <summary>
        /// Claims one prize exactly once: POST /claim-prize (the service creates the claim in a transaction), then the items are granted through ProgressService and saved at once, then the service
        /// is told (ack) that the grant is saved. A repeated tap, a second device or a crash between the steps never grants twice: the device remembers the claim id, the service remembers the state.
        /// </summary>
        public static void ClaimPrize(PendingPrize prize, Action<ClaimOutcome> done)
        {
            if (prize == null || !CanUpload) { done(new ClaimOutcome { Status = ClaimStatus.Offline }); return; }
            if (claiming) { done(new ClaimOutcome { Status = ClaimStatus.Busy }); return; }
            claiming = true;
            client.ClaimPrize(prize.WeekKey, prize.Board, false, (r, c) =>
            {
                if (c == null)
                {
                    claiming = false;
                    string code = r.ErrorCode;
                    ClaimStatus st = r.NetworkError || r.NoToken || r.Retryable ? ClaimStatus.Offline : (code == "not_eligible" || code == "not_ranked" ? ClaimStatus.NotEligible : ClaimStatus.Failed);
                    done(new ClaimOutcome { Status = st, Code = code });
                    return;
                }
                if (progress.LeaderboardPrizeGranted(c.ClaimId) || !c.Grant)
                {
                    // already in this save (an earlier tap whose ack was lost) or already granted elsewhere: only confirm, grant nothing
                    client.ClaimPrize(prize.WeekKey, prize.Board, true, (r2, c2) => { });
                    prize.State = "granted";
                    claiming = false;
                    Raise();
                    done(new ClaimOutcome { Status = ClaimStatus.AlreadyClaimed });
                    return;
                }
                RewardClaimOutcome o = progress.GrantLeaderboardPrize(c.ClaimId, c.Prize.Bundles, c.Prize.GoldChests, c.Prize.IronChests, c.Prize.PowerUps, c.Prize.Coins);
                if (o.Result == RewardClaimResult.Ok)
                {
                    prize.State = "granted";
                    client.ClaimPrize(prize.WeekKey, prize.Board, true, (r2, c2) => { });   // best effort: a lost ack only repeats the (harmless) confirm later
                    claiming = false;
                    Raise();
                    done(new ClaimOutcome { Status = ClaimStatus.Granted, Reward = o });
                }
                else
                {
                    claiming = false;
                    done(new ClaimOutcome { Status = o.Result == RewardClaimResult.AlreadyClaimed ? ClaimStatus.AlreadyClaimed : ClaimStatus.Failed });
                }
            });
        }

        // ---------------- avatars ----------------

        /// <summary>Downloads a picture through the transport (used by the avatar cache); null transport (disabled) answers null.</summary>
        public static void GetTexture(string url, Action<UnityEngine.Texture2D> done)
        {
            if (net == null || string.IsNullOrEmpty(url) || LbJson.SafeAvatar(url).Length == 0) { done(null); return; }
            net.GetTexture(url, done);
        }
    }
}
