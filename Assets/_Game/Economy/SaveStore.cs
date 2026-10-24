using System;
using System.IO;
using Newtonsoft.Json;

namespace BoingUpSkyTower.Economy
{
    public interface ISaveStore
    {
        SaveData Load();
        void Save(SaveData data);
    }

    /// <summary>In-memory store for tests and previews.</summary>
    public sealed class MemorySaveStore : ISaveStore
    {
        string json;

        public SaveData Load()
        {
            return json == null ? new SaveData() : JsonConvert.DeserializeObject<SaveData>(json);
        }

        public void Save(SaveData data)
        {
            json = JsonConvert.SerializeObject(data);
        }
    }

    /// <summary>
    /// Crash-safe file save. Writes path.tmp first, keeps the previous good file as path.bak, then swaps.
    /// Load order: path, path.tmp (an interrupted swap), path.bak, then a fresh SaveData. Corrupt files never throw.
    /// </summary>
    public sealed class FileSaveStore : ISaveStore
    {
        readonly string path;

        /// <summary>Set by Load(): which file the data came from ("none" for a fresh save).</summary>
        public string LastLoadSource { get; private set; }

        public FileSaveStore(string path)
        {
            this.path = path;
            LastLoadSource = "none";
        }

        string Tmp { get { return path + ".tmp"; } }
        string Bak { get { return path + ".bak"; } }

        public SaveData Load()
        {
            foreach (string candidate in new[] { path, Tmp, Bak })
            {
                SaveData data = TryRead(candidate);
                if (data != null)
                {
                    LastLoadSource = candidate == path ? "main" : (candidate == Tmp ? "tmp" : "backup");
                    return data;
                }
            }
            LastLoadSource = "none";
            return new SaveData();
        }

        static SaveData TryRead(string file)
        {
            try
            {
                if (!File.Exists(file)) return null;
                SaveData data = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(file));
                if (data == null || data.Levels == null || data.UnlockedRegions == null) return null;
                if (data.Version > SaveData.CurrentVersion) return null; // written by a newer app: do not guess
                return data;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void Save(SaveData data)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(Tmp, JsonConvert.SerializeObject(data, Formatting.Indented));
            if (File.Exists(path))
            {
                if (TryRead(path) != null) File.Copy(path, Bak, true); // never overwrite a good backup with a corrupt file
                File.Delete(path);
            }
            File.Move(Tmp, path);
        }
    }
}
