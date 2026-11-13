using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FluffyClimbers.LeaderboardService.Tests
{
    /// <summary>Firebase ID token validation with a locally generated RSA key (no network): what is accepted and what is refused.</summary>
    public class TokenTests
    {
        const string Project = "fluffy-climbers-tower-jump";
        static readonly DateTime Now = new DateTime(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc);

        sealed class FixedKeys : ISigningKeyProvider
        {
            public RsaSecurityKey Key;
            public Task<SecurityKey> GetKeyAsync(string kid, CancellationToken ct) { return Task.FromResult<SecurityKey>(kid == "k1" ? Key : null); }
        }

        static RsaSecurityKey NewKey(string kid) { return new RsaSecurityKey(RSA.Create(2048)) { KeyId = kid }; }

        static string Token(SecurityKey key, string alg = SecurityAlgorithms.RsaSha256, string iss = "https://securetoken.google.com/" + Project, string aud = Project, string sub = "uid-123",
            DateTime? exp = null, DateTime? iat = null, DateTime? authTime = null, string kid = "k1")
        {
            var claims = new System.Collections.Generic.List<Claim>();
            if (sub != null) claims.Add(new Claim("sub", sub));
            claims.Add(new Claim("auth_time", new DateTimeOffset(authTime ?? Now.AddMinutes(-10)).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64));
            var creds = new SigningCredentials(key, alg);
            var t = new JwtSecurityToken(iss, aud, claims, notBefore: null, expires: exp ?? Now.AddMinutes(50), signingCredentials: creds);
            t.Header["kid"] = kid;
            t.Payload["iat"] = new DateTimeOffset(iat ?? Now.AddMinutes(-10)).ToUnixTimeSeconds();
            return new JwtSecurityTokenHandler().WriteToken(t);
        }

        static FirebaseTokenVerifier Verifier(RsaSecurityKey key) { return new FirebaseTokenVerifier(Project, new FixedKeys { Key = key }, () => Now); }

        [Fact]
        public async Task AValidFirebaseTokenGivesTheUid()
        {
            RsaSecurityKey key = NewKey("k1");
            VerifiedUser u = await Verifier(key).VerifyAsync(Token(key), CancellationToken.None);
            Assert.NotNull(u);
            Assert.Equal("uid-123", u.Uid);
        }

        [Fact]
        public async Task EverythingElseIsRefused()
        {
            RsaSecurityKey key = NewKey("k1"), other = NewKey("k1");
            FirebaseTokenVerifier v = Verifier(key);
            Assert.Null(await v.VerifyAsync(Token(key, iss: "https://securetoken.google.com/other-project"), CancellationToken.None));          // wrong issuer
            Assert.Null(await v.VerifyAsync(Token(key, iss: "https://accounts.google.com"), CancellationToken.None));
            Assert.Null(await v.VerifyAsync(Token(key, aud: "other-project"), CancellationToken.None));                                          // wrong audience
            Assert.Null(await v.VerifyAsync(Token(key, exp: Now.AddMinutes(-5), iat: Now.AddMinutes(-65)), CancellationToken.None));            // expired (beyond the 60 s skew)
            Assert.Null(await v.VerifyAsync(Token(key, iat: Now.AddMinutes(10)), CancellationToken.None));                                       // issued in the future
            Assert.Null(await v.VerifyAsync(Token(key, authTime: Now.AddMinutes(10)), CancellationToken.None));                                 // auth_time in the future
            Assert.Null(await v.VerifyAsync(Token(key, sub: ""), CancellationToken.None));                                                       // empty subject
            Assert.Null(await v.VerifyAsync(Token(key, sub: new string('x', 129)), CancellationToken.None));                                     // subject too long
            Assert.Null(await v.VerifyAsync(Token(other), CancellationToken.None));                                                              // signed by another key
            Assert.Null(await v.VerifyAsync(Token(key, kid: "unknown"), CancellationToken.None));                                                // unknown kid
            Assert.Null(await v.VerifyAsync(Token(key, alg: SecurityAlgorithms.RsaSha512), CancellationToken.None));                             // only RS256
            Assert.Null(await v.VerifyAsync("", CancellationToken.None));
            Assert.Null(await v.VerifyAsync("not.a.jwt", CancellationToken.None));
            Assert.Null(await v.VerifyAsync(new string('a', 5000), CancellationToken.None));

            // alg none (unsigned) must never pass
            string header = Base64Url("{\"alg\":\"none\",\"kid\":\"k1\",\"typ\":\"JWT\"}");
            string payload = Base64Url("{\"iss\":\"https://securetoken.google.com/" + Project + "\",\"aud\":\"" + Project + "\",\"sub\":\"uid-123\",\"exp\":" + new DateTimeOffset(Now.AddMinutes(50)).ToUnixTimeSeconds() + "}");
            Assert.Null(await v.VerifyAsync(header + "." + payload + ".", CancellationToken.None));
        }

        static string Base64Url(string s) { return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }
    }
}
