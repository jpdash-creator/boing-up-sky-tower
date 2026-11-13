using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;

namespace FluffyClimbers.LeaderboardService
{
    /// <summary>Source of the public keys a token is checked against (replaceable in tests).</summary>
    public interface ISigningKeyProvider
    {
        /// <summary>The key for a token header "kid", or null when unknown.</summary>
        Task<SecurityKey> GetKeyAsync(string kid, CancellationToken ct);
    }

    /// <summary>
    /// Google's public certificates for Firebase ID tokens: https://www.googleapis.com/robot/v1/metadata/x509/securetoken@system.gserviceaccount.com
    /// (a JSON map kid -> PEM certificate). Cached until the Cache-Control max-age (or one hour); an unknown kid forces ONE refresh at most every minute.
    /// </summary>
    public sealed class GoogleCertKeyProvider : ISigningKeyProvider
    {
        public const string FirebaseUrl = "https://www.googleapis.com/robot/v1/metadata/x509/securetoken@system.gserviceaccount.com";

        readonly string url;
        readonly HttpClient http;
        readonly Func<DateTime> now;
        readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        Dictionary<string, SecurityKey> keys = new Dictionary<string, SecurityKey>();
        DateTime expires = DateTime.MinValue, lastRefresh = DateTime.MinValue;

        public GoogleCertKeyProvider(string url, HttpClient http, Func<DateTime> now) { this.url = url; this.http = http; this.now = now; }

        public async Task<SecurityKey> GetKeyAsync(string kid, CancellationToken ct)
        {
            if (kid == null) return null;
            SecurityKey k;
            if (now() < expires && keys.TryGetValue(kid, out k)) return k;
            await gate.WaitAsync(ct);
            try
            {
                if (now() < expires && keys.TryGetValue(kid, out k)) return k;
                if ((now() - lastRefresh).TotalSeconds >= 60 || now() >= expires) await RefreshAsync(ct);
                return keys.TryGetValue(kid, out k) ? k : null;
            }
            finally { gate.Release(); }
        }

        async Task RefreshAsync(CancellationToken ct)
        {
            lastRefresh = now();
            using (HttpResponseMessage r = await http.GetAsync(url, ct))
            {
                r.EnsureSuccessStatusCode();
                var map = new Dictionary<string, SecurityKey>();
                JObject o = JObject.Parse(await r.Content.ReadAsStringAsync(ct));
                foreach (JProperty p in o.Properties())
                    map[p.Name] = new X509SecurityKey(X509Certificate2.CreateFromPem((string)p.Value)) { KeyId = p.Name };
                keys = map;
                int maxAge = 3600;
                if (r.Headers.CacheControl != null && r.Headers.CacheControl.MaxAge.HasValue) maxAge = (int)r.Headers.CacheControl.MaxAge.Value.TotalSeconds;
                expires = now().AddSeconds(Math.Max(60, maxAge));
            }
        }
    }

    public sealed class VerifiedUser
    {
        public string Uid;
        public string Email;
        public DateTime AuthTime;
    }

    /// <summary>
    /// Firebase ID token validation, exactly what the Firebase docs require for a custom backend:
    ///  1. three-part JWT, header alg = RS256 (anything else, "none" included, is refused) and a "kid" that matches one of Google's current certificates;
    ///  2. signature valid for that certificate (RS256);
    ///  3. iss = https://securetoken.google.com/{projectId}; aud = {projectId};
    ///  4. exp in the future and iat in the past (clock skew 60 s); sub non-empty, a string of at most 128 characters (it is the Firebase uid);
    ///  5. auth_time in the past (not after now + skew).
    /// The tokens are short lived (one hour), so no revocation check is made per request (checkRevoked would add a lookup; accounts are deleted by the owner on request).
    /// </summary>
    public sealed class FirebaseTokenVerifier : ITokenVerifier
    {
        readonly string projectId;
        readonly ISigningKeyProvider keys;
        readonly Func<DateTime> now;

        public FirebaseTokenVerifier(string projectId, ISigningKeyProvider keys, Func<DateTime> now)
        {
            this.projectId = projectId; this.keys = keys; this.now = now;
        }

        public string Issuer { get { return "https://securetoken.google.com/" + projectId; } }

        public async Task<VerifiedUser> VerifyAsync(string bearer, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(bearer) || bearer.Length > 4096) return null;
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            if (!handler.CanReadToken(bearer)) return null;
            JwtSecurityToken unverified;
            try { unverified = handler.ReadJwtToken(bearer); } catch (Exception) { return null; }
            if (unverified.Header.Alg != SecurityAlgorithms.RsaSha256) return null;
            SecurityKey key = await keys.GetKeyAsync(unverified.Header.Kid, ct);
            if (key == null) return null;

            var p = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = Issuer,
                ValidateAudience = true, ValidAudience = projectId,
                ValidateLifetime = true, RequireExpirationTime = true,
                ClockSkew = TimeSpan.FromSeconds(60),
                ValidateIssuerSigningKey = true, IssuerSigningKey = key, RequireSignedTokens = true,
                ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 },
                LifetimeValidator = (nbf, exp, tok, prm) => exp.HasValue && exp.Value > now().AddSeconds(-60) && (!nbf.HasValue || nbf.Value <= now().AddSeconds(60))
            };
            try
            {
                handler.ValidateToken(bearer, p, out SecurityToken validated);
                var jwt = (JwtSecurityToken)validated;
                string sub = jwt.Subject;
                if (string.IsNullOrEmpty(sub) || sub.Length > 128) return null;
                DateTime iat = jwt.Payload.IssuedAt;   // MinValue when absent
                if (iat > now().AddSeconds(60)) return null;
                object at;
                DateTime authTime = DateTime.MinValue;
                if (jwt.Payload.TryGetValue("auth_time", out at))
                {
                    authTime = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(at)).UtcDateTime;
                    if (authTime > now().AddSeconds(60)) return null;
                }
                string email = null;
                object e;
                if (jwt.Payload.TryGetValue("email", out e)) email = e as string;
                return new VerifiedUser { Uid = sub, Email = email, AuthTime = authTime };
            }
            catch (SecurityTokenException) { return null; }
            catch (ArgumentException) { return null; }
        }
    }

    public interface ITokenVerifier
    {
        /// <summary>The verified user, or null when the token is missing, malformed, expired, wrongly signed or for another project.</summary>
        Task<VerifiedUser> VerifyAsync(string bearer, CancellationToken ct);
    }
}
