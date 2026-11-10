using System;
using UnityEngine;

namespace FluffyClimbers.Monetization
{
    /// <summary>
    /// Google Play licence key (base64 public key of Play Console > Monetize > Monetization setup) from Resources/Services/iap_config.json: {"playLicenseKey"}.
    /// UnityIapPaymentService uses it for local receipt validation (CrossPlatformValidator). A public key is not a secret, but keep it out of the docs. With the placeholder
    /// a development build grants with a warning; a release build refuses to grant (and the release preflight fails). Server-side validation is the stronger option (docs/33).
    /// </summary>
    public static class IapConfig
    {
        public const string Placeholder = "REPLACE_WITH_PLAY_LICENSE_KEY";
        public const string ResourcePath = "Services/iap_config";

        [Serializable]
        sealed class Data { public string playLicenseKey; }

        static bool loaded;
        static string key = Placeholder;

        public static string ParseKey(string json)
        {
            try
            {
                Data d = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Data>(json);
                if (d != null && !string.IsNullOrWhiteSpace(d.playLicenseKey)) return d.playLicenseKey.Trim();
            }
            catch (Exception) { }
            return Placeholder;
        }

        /// <summary>Pure: decodes the base64 licence key; null when it is the placeholder, empty or not base64 (shorter than 100 bytes is not an RSA key either).</summary>
        public static byte[] DecodeKey(string base64)
        {
            if (string.IsNullOrEmpty(base64) || base64 == Placeholder) return null;
            try
            {
                byte[] b = Convert.FromBase64String(base64.Replace("\n", "").Replace("\r", "").Replace(" ", ""));
                return b.Length >= 100 ? b : null;
            }
            catch (FormatException) { return null; }
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                TextAsset t = Resources.Load<TextAsset>(ResourcePath);
                if (t != null) key = ParseKey(t.text);
            }
            catch (Exception e) { Debug.LogWarning("[IAP] iap_config.json unreadable: " + e.Message); }
        }

        public static string PlayLicenseKey { get { Load(); return key; } }
        public static byte[] LicenseKeyBytes { get { return DecodeKey(PlayLicenseKey); } }
        public static bool HasLicenseKey { get { return LicenseKeyBytes != null; } }
    }
}
