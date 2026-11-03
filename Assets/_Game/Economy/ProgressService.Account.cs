namespace BoingUpSkyTower.Economy
{
    /// <summary>Account support (docs/35): the progress can be replaced by a cloud save and the login choice is stored in the save.</summary>
    public sealed partial class ProgressService
    {
        /// <summary>Replaces the whole save (the player chose the Google account save), repairs missing fields, saves at once and raises Changed.</summary>
        public void ReplaceData(SaveData data)
        {
            if (data == null) return;
            Data = data;
            EnsureEndlessFields();
            EnsurePowerUpFields();
            EnsureChestFields();
            Commit();
        }

        /// <summary>Records the login choice (Settings and the login screen). Saves at once.</summary>
        public void SetAccount(string mode, string accountId)
        {
            if (mode == SaveSync.ModeGoogle) SaveSync.MarkGoogle(Data, accountId);
            else SaveSync.MarkGuest(Data);
            Commit();
        }

        /// <summary>Stamps the moment the Google connect pop-up was shown (B1.c). Saves the file but is not a game change (no Changed event, no play-time stamp).</summary>
        public void MarkGooglePromptShown(System.DateTime utcNow)
        {
            Data.LastGooglePromptUtc = utcNow.ToUniversalTime().ToString("o", System.Globalization.CultureInfo.InvariantCulture);
            store.Save(Data);
        }

        /// <summary>Writes the save to this device at once (Settings > Save Progress for guests). No change event, the data itself is untouched.</summary>
        public void SaveLocalNow()
        {
            store.Save(Data);
        }

        /// <summary>Remembers when the cloud save was last in step with this device. Saves the file but does not raise Changed or stamp the play time (it is not a game change).</summary>
        public void MarkCloudSynced()
        {
            Data.LastCloudSyncUtc = SaveSync.NowText();
            store.Save(Data);
        }
    }
}
