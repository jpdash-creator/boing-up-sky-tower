using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace BoingUpSkyTower.Economy
{
    /// <summary>
    /// The medal target data file (doc 46 section 6): Content/config/medal_targets.json, written by the calibration test
    /// (Tests/Simulation/MedalCalibrationTests.cs) and shipped as Resources/Content/medal_targets.json.
    /// Format: { "version": 1, "percents": [55, 70, 85], "targets": { "lvl_001_01": [m0, m1, m2, m3, m4], ... } } where m0..m4 are the
    /// Pace (milli plat/s, 830 = 0.83) the solvability bot reaches in the Gameplay modes Relaxed..Insane, averaged over its layouts; 0 = no data (no medal).
    /// The three medal lines are <see cref="TimeRecords.MedalPercents"/> of that Pace (the table the owner can change); the file also stores them so a reader can see what it was written for.
    /// </summary>
    public sealed class FileMedalTargets : IMedalTargetSource
    {
        public const int Version = 1;

        readonly Dictionary<string, int[]> map = new Dictionary<string, int[]>();

        /// <summary>Levels that have any data.</summary>
        public int LevelCount { get { return map.Count; } }

        public int TargetPaceMilli(string levelId, int mode)
        {
            int[] row;
            if (levelId == null || !TimeRecords.ValidMode(mode) || !map.TryGetValue(levelId, out row) || row == null || mode >= row.Length) return 0;
            return row[mode] > 0 ? row[mode] : 0;
        }

        public bool Has(string levelId) { return levelId != null && map.ContainsKey(levelId); }

        /// <summary>Parses the file text. Throws a clear exception for a wrong version or shape (a broken target file must be noticed, not silently ignored).</summary>
        public static FileMedalTargets FromJson(string json)
        {
            JObject root = JObject.Parse(json);
            int version = (int?)root["version"] ?? 0;
            if (version != Version) throw new FormatException("medal_targets.json: version " + version + " (expected " + Version + ")");
            var t = new FileMedalTargets();
            JObject targets = root["targets"] as JObject;
            if (targets == null) throw new FormatException("medal_targets.json: no targets object");
            foreach (KeyValuePair<string, JToken> kv in targets)
            {
                JArray arr = kv.Value as JArray;
                if (arr == null) throw new FormatException("medal_targets.json: " + kv.Key + " is not an array");
                var row = new int[TimeRecords.ModeCount];
                for (int m = 0; m < row.Length && m < arr.Count; m++) row[m] = Math.Max(0, (int)arr[m]);
                t.map[kv.Key] = row;
            }
            return t;
        }

        /// <summary>The file text for a table (levels sorted by id, one line per level, LF, final newline) so the file is deterministic and diffs cleanly.</summary>
        public static string ToJson(IDictionary<string, int[]> targets, int layoutsPerMode, string note)
        {
            var ids = new List<string>(targets.Keys);
            ids.Sort(StringComparer.Ordinal);
            var sb = new System.Text.StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"version\": ").Append(Version).Append(",\n");
            sb.Append("  \"_doc\": ").Append(Newtonsoft.Json.JsonConvert.ToString(note ?? "")).Append(",\n");
            sb.Append("  \"percents\": [").Append(TimeRecords.MedalPercents[0]).Append(", ").Append(TimeRecords.MedalPercents[1]).Append(", ").Append(TimeRecords.MedalPercents[2]).Append("],\n");
            sb.Append("  \"layoutsPerMode\": ").Append(layoutsPerMode).Append(",\n");
            sb.Append("  \"targets\": {\n");
            for (int i = 0; i < ids.Count; i++)
            {
                int[] row = targets[ids[i]];
                sb.Append("    \"").Append(ids[i]).Append("\": [");
                for (int m = 0; m < TimeRecords.ModeCount; m++)
                {
                    if (m > 0) sb.Append(", ");
                    sb.Append(m < row.Length ? row[m] : 0);
                }
                sb.Append(i + 1 < ids.Count ? "],\n" : "]\n");
            }
            sb.Append("  }\n}\n");
            return sb.ToString();
        }
    }
}
