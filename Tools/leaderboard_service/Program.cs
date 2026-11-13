using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FluffyClimbers.Content;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FluffyClimbers.LeaderboardService
{
    /// <summary>
    /// Cloud Run entry point. Environment:
    ///   FIREBASE_PROJECT   Firebase / GCP project id (default fluffy-climbers-tower-jump)
    ///   LB_STORE           "firestore" (default) or "memory" (local runs; data is lost on exit)
    ///   LB_PID_SALT        secret salt of the public player id (Secret Manager)
    ///   LB_ADMIN_KEY       secret for POST /admin/tick (Cloud Scheduler sends it as X-Admin-Key)
    ///   CONTENT_MANIFEST   path of content_manifest.json (default: next to the binary)
    ///   LB_DEV_UID         ONLY with LB_STORE=memory: accept "Authorization: Dev {uid}" instead of a Firebase token (local manual testing, never set in production)
    /// </summary>
    public static class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            string project = Environment.GetEnvironmentVariable("FIREBASE_PROJECT") ?? "fluffy-climbers-tower-jump";
            bool memory = (Environment.GetEnvironmentVariable("LB_STORE") ?? "firestore") == "memory";
            string devUid = memory ? Environment.GetEnvironmentVariable("LB_DEV_UID") : null;
            string adminKey = Environment.GetEnvironmentVariable("LB_ADMIN_KEY");
            string salt = Environment.GetEnvironmentVariable("LB_PID_SALT") ?? (memory ? "dev-salt" : null);
            if (salt == null) throw new InvalidOperationException("LB_PID_SALT is required");

            string port = Environment.GetEnvironmentVariable("PORT");
            if (!string.IsNullOrEmpty(port)) builder.WebHost.UseUrls("http://0.0.0.0:" + port);   // Cloud Run
            builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 512 * 1024);   // 256 KB of inputs as base64 is about 350 KB
            string manifestPath = Environment.GetEnvironmentVariable("CONTENT_MANIFEST") ?? Path.Combine(AppContext.BaseDirectory, "content_manifest.json");
            ContentRepository repo = ContentRepository.LoadFromJson(File.ReadAllText(manifestPath));
            ILeaderboardStore store = memory ? (ILeaderboardStore)new InMemoryStore() : new FirestoreStore(project);
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            ITokenVerifier verifier = new FirebaseTokenVerifier(project, new GoogleCertKeyProvider(GoogleCertKeyProvider.FirebaseUrl, http, () => DateTime.UtcNow), () => DateTime.UtcNow);
            var api = new LeaderboardApi(store, repo, () => DateTime.UtcNow, salt);   // tickets are signed with a key derived from LB_PID_SALT
            builder.Services.AddSingleton(api);

            WebApplication app = builder.Build();

            async Task<string> Uid(HttpContext c)
            {
                string h = c.Request.Headers["Authorization"];
                if (devUid != null && h != null && h.StartsWith("Dev ", StringComparison.Ordinal)) return h.Substring(4).Trim();
                if (h == null || !h.StartsWith("Bearer ", StringComparison.Ordinal)) return null;
                VerifiedUser u = await verifier.VerifyAsync(h.Substring(7).Trim(), c.RequestAborted);
                return u == null ? null : u.Uid;
            }
            IResult Out(ApiResult r) { return Results.Json(r.Body, statusCode: r.Status); }

            app.MapGet("/healthz", () => Results.Ok(new { ok = true, content = repo.ContentHash, weekKey = WeekMath.WeekKey(DateTime.UtcNow) }));

            app.MapPost("/start-run", async (HttpContext c, LeaderboardApi.StartRunRequest req) =>
            {
                string uid = await Uid(c);
                return uid == null ? Results.Json(new { error = "unauthorized" }, statusCode: 401) : Out(await api.StartRunAsync(uid, req));
            });

            app.MapPost("/submit-run", async (HttpContext c, RunSubmission sub) =>
            {
                string uid = await Uid(c);
                return uid == null ? Results.Json(new { error = "unauthorized" }, statusCode: 401) : Out(await api.SubmitRunAsync(uid, sub));
            });

            app.MapGet("/my-week", async (HttpContext c, bool? slots, bool? prizes) =>
            {
                string uid = await Uid(c);
                return uid == null ? Results.Json(new { error = "unauthorized" }, statusCode: 401) : Out(await api.MyWeekAsync(uid, slots == true, prizes == true));
            });

            app.MapPost("/claim-prize", async (HttpContext c, LeaderboardApi.ClaimRequest req) =>
            {
                string uid = await Uid(c);
                return uid == null ? Results.Json(new { error = "unauthorized" }, statusCode: 401) : Out(await api.ClaimPrizeAsync(uid, req));
            });

            app.MapPost("/admin/tick", async (HttpContext c) =>
            {
                string given = c.Request.Headers["X-Admin-Key"];
                if (string.IsNullOrEmpty(adminKey) || given == null || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(adminKey)))
                    return Results.Json(new { error = "unauthorized" }, statusCode: 401);
                return Out(await api.AdminTickAsync());
            });

            app.Run();
        }
    }
}
