using System;
using FluffyClimbers.Economy;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FluffyClimbers.Leaderboard
{
    /// <summary>The raw answer of one HTTP call. <see cref="NetworkError"/> = no answer at all (offline, timeout, DNS).</summary>
    public sealed class HttpReply
    {
        public int Status;
        public string Body = "";
        public bool NetworkError;
        /// <summary>No ID token could be fetched, so nothing was sent.</summary>
        public bool NoToken;

        public bool Ok { get { return !NetworkError && !NoToken && Status >= 200 && Status < 300; } }

        /// <summary>The "error" code of the service body ("ticket_used", "mismatch" ...), "" when there is none.</summary>
        public string ErrorCode
        {
            get
            {
                JObject o = LbJson.Parse(Body);
                return o == null ? "" : LbJson.Str(o["error"]);
            }
        }

        /// <summary>
        /// True when sending the same request again later may work: no answer, no token, 401 (token), 408, 429 and server errors. Every other refusal (400, 403, 409, 410, 422, 426 ...) is final:
        /// a queued run that gets one is dropped instead of retried for ever.
        /// </summary>
        public bool Retryable
        {
            get { return NetworkError || NoToken || Status == 401 || Status == 408 || Status == 429 || Status >= 500; }
        }
    }

    /// <summary>The transport of the client (UnityWebRequest in the game, a fake in the tests).</summary>
    public interface ILeaderboardTransport
    {
        /// <summary>One HTTP call with an optional JSON body and an optional bearer token. done runs on the main thread, exactly once.</summary>
        void Send(string method, string url, string bearer, string jsonBody, Action<HttpReply> done);
        /// <summary>Downloads a picture; null on failure.</summary>
        void GetTexture(string url, Action<Texture2D> done);
    }

    /// <summary>
    /// The service client (design doc sections 5 and 6): POST /start-run, POST /submit-run, GET /my-week, POST /claim-prize on our own Cloud Run service with a Firebase ID token, and the
    /// Firestore REST reads and writes the rules allow (a board document, the display profile). A 401 fetches one fresh token and repeats the call once. Nothing here blocks or throws.
    /// </summary>
    public sealed class LeaderboardClient
    {
        readonly ILeaderboardAuth auth;
        readonly ILeaderboardTransport net;
        readonly LeaderboardConfig cfg;

        public LeaderboardClient(ILeaderboardAuth auth, ILeaderboardTransport net, LeaderboardConfig cfg)
        {
            this.auth = auth; this.net = net; this.cfg = cfg;
        }

        public LeaderboardConfig Config { get { return cfg; } }

        void Call(string method, string url, string body, Action<HttpReply> done)
        {
            auth.GetIdToken(false, token =>
            {
                if (string.IsNullOrEmpty(token)) { done(new HttpReply { NoToken = true }); return; }
                net.Send(method, url, token, body, r =>
                {
                    if (r.NetworkError || r.Status != 401) { done(r); return; }
                    auth.GetIdToken(true, fresh =>
                    {
                        if (string.IsNullOrEmpty(fresh)) { done(r); return; }
                        net.Send(method, url, fresh, body, done);
                    });
                });
            });
        }

        /// <summary>POST /start-run: the seed ticket of the run that is about to begin. null on any failure (the run is then played with a local seed and never uploaded).</summary>
        public void StartRun(string kind, string levelId, string regionId, int mode, Action<StartTicket> done)
        {
            var body = new JObject { ["kind"] = kind, ["mode"] = mode };
            if (kind == "level") body["levelId"] = levelId; else body["regionId"] = regionId;
            Call("POST", cfg.Url("/start-run"), body.ToString(Newtonsoft.Json.Formatting.None), r =>
            {
                StartTicket t = r.Ok ? LbJson.ParseStartRun(r.Body) : null;
                if (t != null) { t.Kind = kind; t.Target = kind == "level" ? levelId : regionId; t.Mode = mode; }
                done(t);
            });
        }

        /// <summary>POST /submit-run with a queued run (the fresh RunRecord of the run, never the stored best).</summary>
        public void SubmitRun(QueuedRun run, Action<HttpReply> done)
        {
            Call("POST", cfg.Url("/submit-run"), LbJson.BuildSubmitBody(run), done);
        }

        /// <summary>GET /my-week (totals, ranks and, with <paramref name="withPrizes"/>, the claimable prizes of the closed weeks). null on failure.</summary>
        public void MyWeek(bool withPrizes, Action<MyWeekData> done)
        {
            Call("GET", cfg.Url("/my-week" + (withPrizes ? "?prizes=1" : "")), null, r => done(r.Ok ? LbJson.ParseMyWeek(r.Body) : null));
        }

        /// <summary>POST /claim-prize. With <paramref name="ack"/> the app confirms it saved the prize (the claim then becomes "granted"). The reply carries the status for the caller.</summary>
        public void ClaimPrize(string weekKey, string board, bool ack, Action<HttpReply, ClaimData> done)
        {
            var body = new JObject { ["weekKey"] = weekKey, ["board"] = board };
            if (ack) body["ack"] = true;
            Call("POST", cfg.Url("/claim-prize"), body.ToString(Newtonsoft.Json.Formatting.None), r => done(r, r.Ok ? LbJson.ParseClaim(r.Body) : null));
        }

        /// <summary>The top 100 of a board straight from Firestore (REST, one document read). A missing document (404) is an empty board; null on a real failure.</summary>
        public void FetchBoard(string weekKey, string board, Action<BoardData> done)
        {
            Call("GET", LeaderboardConfig.BoardDocUrl(weekKey, board), null, r =>
            {
                if (r.Ok) done(LbJson.ParseFirestoreBoard(r.Body, weekKey, board));
                else if (!r.NetworkError && !r.NoToken && r.Status == 404) done(new BoardData { WeekKey = weekKey, Board = board });
                else done(null);
            });
        }

        /// <summary>Writes players/{uid} (display name and picture URL; the rules allow exactly these fields). done(true) when stored.</summary>
        public void WriteProfile(string uid, LbProfile profile, Action<bool> done)
        {
            if (string.IsNullOrEmpty(uid) || profile == null) { done(false); return; }
            Call("POST", LeaderboardConfig.CommitUrl, LbJson.BuildProfileCommit(LeaderboardConfig.ProjectId, uid, profile.DisplayName, profile.AvatarUrl), r => done(r.Ok));
        }
    }
}
