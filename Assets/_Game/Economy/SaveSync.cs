using System;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;

namespace BoingUpSkyTower.Economy
{
    /// <summary>What the "choose your save" popup shows for one save (docs/35).</summary>
    public sealed class SaveSummary
    {
        [JsonProperty("levels")] public int LevelsCleared;
        [JsonProperty("gems")] public int Gems;
        [JsonProperty("coins")] public int Coins;
        [JsonProperty("heroCopies")] public int HeroCopies;
        [JsonProperty("lastPlayed")] public string LastPlayedUtc = "";

        public bool IsEmpty { get { return LevelsCleared == 0 && Gems == 0 && Coins == 0 && HeroCopies == 0; } }

        /// <summary>Same progress and wallet (the time is ignored): two such saves need no choice.</summary>
        public bool SameProgress(SaveSummary o)
        {
            return o != null && LevelsCleared == o.LevelsCleared && Gems == o.Gems && Coins == o.Coins && HeroCopies == o.HeroCopies;
        }
    }

    /// <summary>The bytes stored in Play Games Saved Games: a small header (summary and times, readable without parsing the save) and the save itself.</summary>
    public sealed class SaveEnvelope
    {
        public const int CurrentFormat = 1;
        [JsonProperty("format")] public int Format = CurrentFormat;
        [JsonProperty("savedUtc")] public string SavedUtc = "";
        [JsonProperty("saveVersion")] public int SaveVersion;
        [JsonProperty("summary")] public SaveSummary Summary = new SaveSummary();
        [JsonProperty("data")] public SaveData Data;
    }

    public enum SyncDecision
    {
        /// <summary>Nothing to do (both sides already agree, or the cloud save cannot be read by this app version).</summary>
        Nothing,
        /// <summary>Send the device save to the account (the account has no save, or this device is simply newer).</summary>
        UploadLocal,
        /// <summary>Replace the device save by the account save (the device has no progress, or the account is simply newer).</summary>
        UseCloud,
        /// <summary>Both sides changed in different ways: show both and let the player pick.</summary>
        AskPlayer
    }

    /// <summary>
    /// Pure rules for the Google Play Games cloud save (docs/35): serialise, summarise, decide what to do when a device save meets an account save.
    /// No Unity types, no clock except NowText(), so it is fully unit-testable. Whole saves are chosen, never merged field by field (a mixed wallet
    /// could duplicate or lose gems); the player decides when both sides differ.
    /// </summary>
    public static class SaveSync
    {
        public const string ModeGuest = "guest";
        public const string ModeGoogle = "google";
        /// <summary>Name of the Saved Games file in Play Games.</summary>
        public const string CloudFileName = "boing_save";

        public static string NowText() { return DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture); }

        public static bool TryParseTime(string text, out DateTime utc)
        {
            utc = DateTime.MinValue;
            if (string.IsNullOrEmpty(text)) return false;
            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out utc);
        }

        static DateTime TimeOrMin(string text)
        {
            DateTime t;
            return TryParseTime(text, out t) ? t : DateTime.MinValue;
        }

        /// <summary>Counts levels with at least one clear or star, the wallet and the hero copies (owned characters without an explicit count weigh 1).</summary>
        public static SaveSummary Summarize(SaveData d)
        {
            var s = new SaveSummary();
            if (d == null) return s;
            if (d.Levels != null)
                foreach (var kv in d.Levels)
                    if (kv.Value != null && (kv.Value.Clears > 0 || kv.Value.Stars > 0)) s.LevelsCleared++;
            s.Gems = d.Gems;
            s.Coins = d.Coins;
            int copies = 0;
            if (d.Copies != null)
                foreach (var kv in d.Copies) if (kv.Value > 0) copies += kv.Value;
            if (d.Characters != null)
                foreach (string id in d.Characters) if (d.Copies == null || !d.Copies.ContainsKey(id)) copies++;
            s.HeroCopies = copies;
            s.LastPlayedUtc = d.LastPlayedUtc ?? "";
            return s;
        }

        // ---- bytes ----

        public static byte[] Serialize(SaveData d)
        {
            var env = new SaveEnvelope { SavedUtc = NowText(), SaveVersion = d.Version, Summary = Summarize(d), Data = d };
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(env));
        }

        /// <summary>Parses cloud bytes. Returns null for empty, corrupt or foreign data (never throws).</summary>
        public static SaveEnvelope TryDeserialize(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            try
            {
                SaveEnvelope env = JsonConvert.DeserializeObject<SaveEnvelope>(Encoding.UTF8.GetString(bytes));
                if (env == null || env.Data == null || env.Format < 1 || env.Data.Levels == null || env.Data.UnlockedRegions == null) return null;
                if (env.Summary == null) env.Summary = Summarize(env.Data);
                if (string.IsNullOrEmpty(env.Summary.LastPlayedUtc)) env.Summary.LastPlayedUtc = env.Data.LastPlayedUtc ?? "";
                return env;
            }
            catch (Exception) { return null; }
        }

        /// <summary>True when this app can load the cloud save (a save written by a newer app version is never touched).</summary>
        public static bool IsReadable(SaveEnvelope env)
        {
            return env != null && env.Format <= SaveEnvelope.CurrentFormat && env.Data != null && env.Data.Version <= SaveData.CurrentVersion;
        }

        // ---- rules ----

        /// <summary>A device save with no progress at all (nothing cleared, empty wallet, no heroes beyond the start).</summary>
        public static bool IsFresh(SaveData d)
        {
            return Summarize(d).IsEmpty;
        }

        /// <summary>
        /// What to do when the player is signed in to <paramref name="accountId"/> and the account's save is <paramref name="cloud"/> (null = none).
        /// First link (the device never synced with this account): no cloud save uploads the device save; a fresh device takes the cloud save;
        /// equal progress changes nothing; anything else asks. Later launches: the side that changed since the last sync wins; both changed and
        /// different asks.
        /// </summary>
        public static SyncDecision Decide(SaveData local, SaveEnvelope cloud, string accountId)
        {
            if (cloud == null) return IsFresh(local) ? SyncDecision.Nothing : SyncDecision.UploadLocal;
            if (!IsReadable(cloud)) return SyncDecision.Nothing;
            SaveSummary ls = Summarize(local), cs = cloud.Summary ?? Summarize(cloud.Data);
            if (ls.SameProgress(cs)) return SyncDecision.Nothing;
            if (ls.IsEmpty) return SyncDecision.UseCloud;
            if (cs.IsEmpty) return SyncDecision.UploadLocal;

            bool linked = local != null && local.AccountMode == ModeGoogle && !string.IsNullOrEmpty(accountId) && local.AccountId == accountId;
            if (!linked) return SyncDecision.AskPlayer;   // first link, or another Google account than before: never overwrite either side silently

            DateTime lastSync = TimeOrMin(local.LastCloudSyncUtc);
            DateTime localChanged = TimeOrMin(local.LastPlayedUtc);
            DateTime cloudChanged = TimeOrMin(cloud.SavedUtc);
            bool localNew = localChanged > lastSync;
            bool cloudNew = cloudChanged > lastSync;
            if (localNew && cloudNew) return SyncDecision.AskPlayer;
            if (cloudNew) return SyncDecision.UseCloud;
            if (localNew) return SyncDecision.UploadLocal;
            return SyncDecision.Nothing;
        }

        // ---- account fields ----

        /// <summary>Keeps the login state when progress is erased (Settings, reset): the player stays signed in and is not asked again.</summary>
        public static void CarryAccount(SaveData from, SaveData to)
        {
            if (from == null || to == null) return;
            to.AccountMode = from.AccountMode;
            to.AccountId = from.AccountId;
            to.LastCloudSyncUtc = from.LastCloudSyncUtc;
            to.LoginChosen = from.LoginChosen;
        }

        /// <summary>The cloud save becomes the device save: it takes over this device's account fields.</summary>
        public static SaveData PrepareCloudForDevice(SaveEnvelope cloud, string accountId)
        {
            SaveData d = cloud.Data;
            d.AccountMode = ModeGoogle;
            d.AccountId = accountId ?? "";
            d.LoginChosen = true;
            d.LastCloudSyncUtc = NowText();
            return d;
        }

        public static void MarkGuest(SaveData d)
        {
            d.AccountMode = ModeGuest;
            d.AccountId = "";
            d.LoginChosen = true;
        }

        public static void MarkGoogle(SaveData d, string accountId)
        {
            d.AccountMode = ModeGoogle;
            d.AccountId = accountId ?? "";
            d.LoginChosen = true;
        }

        /// <summary>The login screen is shown until the player has chosen Google or guest once (an old save with progress is asked once too).</summary>
        public static bool NeedsLogin(SaveData d)
        {
            if (d == null) return false;
            if (d.LoginChosen) return false;
            return true;
        }

        /// <summary>Just the date, for example "3 Nov 2026" ("no date" when unknown).</summary>
        public static string LastPlayedShort(SaveSummary s)
        {
            DateTime t;
            if (s == null || !TryParseTime(s.LastPlayedUtc, out t)) return "no date";
            return t.ToLocalTime().ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        }

        /// <summary>Short text for the popup cards, for example "Played 3 Nov 2026".</summary>
        public static string LastPlayedText(SaveSummary s)
        {
            DateTime t;
            if (s == null || !TryParseTime(s.LastPlayedUtc, out t)) return "Played: unknown";
            return "Played " + t.ToLocalTime().ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        }
    }
}
