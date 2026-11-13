using System;
using System.Security.Cryptography;
using System.Text;

namespace FluffyClimbers.LeaderboardService
{
    public sealed class TicketInfo { public string Id, Uid, Kind, Target; public int Mode, Seed; public DateTime ExpiresAt; }

    /// <summary>
    /// Run tickets (owner decision 2026-11-12): POST /start-run picks the seed on the server and signs it together with uid, kind, level or region, setting and an expiry.
    /// Format: base64url(payload) + "." + base64url(HMAC-SHA256(key, payload)), payload = "v1|uid|kind|target|mode|seed|expiryUnix|nonce".
    /// /submit-run only accepts a run whose ticket verifies, matches the upload, is not expired and is not used yet (the use is recorded in the store, one record per nonce, TTL).
    /// </summary>
    public sealed class TicketService
    {
        readonly byte[] key;
        readonly Func<DateTime> now;

        public TicketService(string secret, Func<DateTime> now) { key = Encoding.UTF8.GetBytes("ticket|" + secret); this.now = now; }

        static string B64(byte[] b) { return Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }

        static byte[] Unb64(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            while (s.Length % 4 != 0) s += "=";
            return Convert.FromBase64String(s);
        }

        byte[] Sign(byte[] payload) { using (var h = new HMACSHA256(key)) return h.ComputeHash(payload); }

        public static int NewSeed() { return RandomNumberGenerator.GetInt32(1, int.MaxValue); }

        /// <summary>Issues a ticket for a seed (tests pass their own seed; /start-run passes <see cref="NewSeed"/>).</summary>
        public string Issue(string uid, string kind, string target, int mode, int seed, int minutes)
        {
            long exp = new DateTimeOffset(DateTime.SpecifyKind(now(), DateTimeKind.Utc).AddMinutes(minutes)).ToUnixTimeSeconds();
            string payload = "v1|" + uid + "|" + kind + "|" + target + "|" + mode + "|" + seed + "|" + exp + "|" + Guid.NewGuid().ToString("N");
            byte[] p = Encoding.UTF8.GetBytes(payload);
            return B64(p) + "." + B64(Sign(p));
        }

        /// <summary>Null = valid signature and not expired; the caller still compares uid, kind, target, mode and seed with the upload.</summary>
        public string Parse(string ticket, out TicketInfo info)
        {
            info = null;
            if (string.IsNullOrEmpty(ticket) || ticket.Length > 600) return "bad_ticket";
            string[] parts = ticket.Split('.');
            if (parts.Length != 2) return "bad_ticket";
            byte[] p, sig;
            try { p = Unb64(parts[0]); sig = Unb64(parts[1]); } catch (FormatException) { return "bad_ticket"; }
            if (!CryptographicOperations.FixedTimeEquals(sig, Sign(p))) return "bad_ticket";
            string[] f = Encoding.UTF8.GetString(p).Split('|');
            long exp; int mode, seed;
            if (f.Length != 8 || f[0] != "v1" || !int.TryParse(f[4], out mode) || !int.TryParse(f[5], out seed) || !long.TryParse(f[6], out exp)) return "bad_ticket";
            info = new TicketInfo { Uid = f[1], Kind = f[2], Target = f[3], Mode = mode, Seed = seed, Id = f[7], ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime };
            return info.ExpiresAt < now() ? "ticket_expired" : null;
        }
    }
}
