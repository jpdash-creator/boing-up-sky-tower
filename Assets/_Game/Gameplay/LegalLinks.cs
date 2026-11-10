using System;
using UnityEngine;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// The public legal pages (docs/policy.html and docs/terms.html, built from POLICY.md and TERMS.md and published by GitHub Pages from the /docs folder, see Planning/LEGAL-HOSTING.md) and the support mailbox, read once from
    /// Resources/Services/legal_config.json: {"privacyUrl","termsUrl","supportEmail"}. A missing, empty or non-https value falls back to the default below, so a
    /// build never ships without working links. Settings shows the two links (owner 2026-11-09; Google Play's User Data policy wants the privacy policy
    /// reachable inside the app). Opening goes through <see cref="Opener"/> so tests can intercept it instead of launching a browser.
    /// </summary>
    public static class LegalLinks
    {
        public const string ResourcePath = "Services/legal_config";
        public const string DefaultPrivacyUrl = "https://jpdash-creator.github.io/boing-up-sky-tower/policy.html";
        public const string DefaultTermsUrl = "https://jpdash-creator.github.io/boing-up-sky-tower/terms.html";
        public const string DefaultSupportEmail = "jpdash.creator@gmail.com";

        /// <summary>Button words (short like Reset and Restore so they fit half a row at 130 percent text) and the screen-reader texts, which say that the game is left.</summary>
        public const string PrivacyLabel = "Privacy";
        public const string TermsLabel = "Terms";
        public const string PrivacyA11y = "Privacy policy, opens in your browser";
        public const string TermsA11y = "Terms of use, opens in your browser";

        [Serializable]
        sealed class Data { public string privacyUrl; public string termsUrl; public string supportEmail; }

        /// <summary>The three resolved values (never null, never empty).</summary>
        public sealed class Config
        {
            public readonly string PrivacyUrl;
            public readonly string TermsUrl;
            public readonly string SupportEmail;
            public Config(string privacyUrl, string termsUrl, string supportEmail) { PrivacyUrl = privacyUrl; TermsUrl = termsUrl; SupportEmail = supportEmail; }
        }

        static Config loaded;

        /// <summary>How a page is opened. Production: the system browser. Tests replace it to capture the URL.</summary>
        public static Action<string> Opener = url => Application.OpenURL(url);

        public static Config Current { get { if (loaded == null) loaded = Load(); return loaded; } }
        public static string PrivacyUrl { get { return Current.PrivacyUrl; } }
        public static string TermsUrl { get { return Current.TermsUrl; } }
        public static string SupportEmail { get { return Current.SupportEmail; } }

        /// <summary>Pure parse (unit-tested): each missing, empty, malformed or non-https value falls back to its default on its own.</summary>
        public static Config Parse(string json)
        {
            Data d = null;
            if (!string.IsNullOrEmpty(json))
            {
                try { d = JsonUtility.FromJson<Data>(json); }
                catch (Exception) { d = null; }
            }
            string privacy = d != null && IsWebUrl(d.privacyUrl) ? d.privacyUrl.Trim() : DefaultPrivacyUrl;
            string terms = d != null && IsWebUrl(d.termsUrl) ? d.termsUrl.Trim() : DefaultTermsUrl;
            string email = d != null && IsEmail(d.supportEmail) ? d.supportEmail.Trim() : DefaultSupportEmail;
            return new Config(privacy, terms, email);
        }

        /// <summary>Only absolute https pages are ever opened: no http, no custom schemes, no spaces.</summary>
        public static bool IsWebUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            string s = url.Trim();
            if (s.IndexOf(' ') >= 0 || !s.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return false;
            Uri u;
            return Uri.TryCreate(s, UriKind.Absolute, out u) && u.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(u.Host) && u.Host.IndexOf('.') > 0;
        }

        /// <summary>A plain mailbox check: one @ with a dotted domain after it and no spaces.</summary>
        public static bool IsEmail(string email)
        {
            if (string.IsNullOrEmpty(email)) return false;
            string s = email.Trim();
            int at = s.IndexOf('@');
            if (at <= 0 || at != s.LastIndexOf('@') || s.IndexOf(' ') >= 0) return false;
            string domain = s.Substring(at + 1);
            return domain.IndexOf('.') > 0 && !domain.EndsWith(".");
        }

        static Config Load()
        {
            try
            {
                TextAsset t = Resources.Load<TextAsset>(ResourcePath);
                return Parse(t != null ? t.text : null);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Legal] legal_config.json unreadable: " + e.Message);
                return Parse(null);
            }
        }

        public static void OpenPrivacy() { Open(PrivacyUrl); }
        public static void OpenTerms() { Open(TermsUrl); }

        /// <summary>Opens a page through <see cref="Opener"/>; refuses anything that is not an https URL. Never throws. Returns true when the opener was called.</summary>
        public static bool Open(string url)
        {
            if (!IsWebUrl(url)) return false;
            try { Opener(url.Trim()); return true; }
            catch (Exception e)
            {
                Debug.LogWarning("[Legal] could not open " + url + ": " + e.Message);
                return false;
            }
        }

        /// <summary>Tests: forget the loaded config and restore the real opener.</summary>
        public static void ResetForTests()
        {
            loaded = null;
            Opener = url => Application.OpenURL(url);
        }

        /// <summary>Tests: use these values instead of the resource file.</summary>
        public static void SetForTests(Config config) { loaded = config; }
    }
}
