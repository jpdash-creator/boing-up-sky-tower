using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SkyHopper.Simulation.Tests
{
    /// <summary>One shipped level as the Simulation sees it.</summary>
    public sealed class LoadedLevel
    {
        public string Id;
        public int Region;
        public string ClassName;
        public double BaseD;
        public LevelSpec Spec;
    }

    /// <summary>
    /// Test-side mapping from the content JSON (Content/levels, Content/regions) to LevelSpec.
    /// The Simulation assembly itself has no JSON dependency; the Unity side will do the same mapping.
    /// </summary>
    public static class ContentLoader
    {
        public static string FindRoot()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null && !Directory.Exists(Path.Combine(dir, "Content", "levels"))) dir = Path.GetDirectoryName(dir);
            return dir;
        }

        public static int ClassFromName(string name)
        {
            switch (name)
            {
                case "simple": return 0;
                case "medium": return 1;
                case "hard": return 2;
                case "complex": return 3;
                case "highly_complex": return 4;
                default: return 0;
            }
        }

        private static int Int(JsonElement e, string name, int fallback)
        {
            JsonElement v;
            return e.TryGetProperty(name, out v) ? v.GetInt32() : fallback;
        }

        public static List<LoadedLevel> LoadRegions(string root, int firstRegion, int lastRegion)
        {
            List<LoadedLevel> result = new List<LoadedLevel>();
            for (int region = firstRegion; region <= lastRegion; region++)
            {
                string prefix = "region_" + region.ToString("D3") + "_";
                string[] regionFiles = Directory.GetFiles(Path.Combine(root, "Content", "regions"), prefix + "*.json");
                string[] levelFiles = Directory.GetFiles(Path.Combine(root, "Content", "levels"), prefix + "*_levels.json");
                if (regionFiles.Length != 1 || levelFiles.Length != 1) throw new InvalidOperationException("missing content for region " + region);

                double offset;
                SignatureKind signature;
                using (JsonDocument rd = JsonDocument.Parse(File.ReadAllText(regionFiles[0])))
                {
                    JsonElement rules = rd.RootElement.GetProperty("rules");
                    offset = rules.GetProperty("difficultyOffset").GetDouble();
                    signature = SignatureKinds.FromModuleId(rules.GetProperty("signatureModule").GetString());
                }
                Fx regionOffset = Fx.FromRatio((int)Math.Round(offset * 10000), 10000);

                using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(levelFiles[0])))
                {
                    foreach (JsonElement lv in doc.RootElement.EnumerateArray())
                    {
                        LoadedLevel ll = new LoadedLevel();
                        ll.Id = lv.GetProperty("id").GetString();
                        ll.Region = region;
                        ll.ClassName = lv.GetProperty("class").GetString();
                        ll.BaseD = lv.GetProperty("difficulty").GetDouble();

                        LevelSpec spec = new LevelSpec();
                        spec.Seed = lv.GetProperty("seed").GetInt32();
                        Fx baseD = Fx.FromRatio((int)Math.Round(ll.BaseD * 1000), 1000);
                        spec.Difficulty = LevelGenerator.EffectiveDifficulty(baseD, regionOffset);
                        spec.LevelIndex = lv.GetProperty("index").GetInt32();
                        spec.Class = ClassFromName(ll.ClassName);
                        spec.Signature = signature;

                        JsonElement goal = lv.GetProperty("goal");
                        GoalSpec g = new GoalSpec();
                        g.Type = GoalTypes.FromId(goal.GetProperty("type").GetString());
                        g.Value = Int(goal, "value", 0);
                        g.Count = Int(goal, "count", 0);
                        g.TimeSec = Int(goal, "timeSec", 0);
                        spec.Goal = g;

                        List<StarSpec> stars = new List<StarSpec>();
                        foreach (JsonElement st in lv.GetProperty("stars").EnumerateArray())
                        {
                            StarSpec ss = new StarSpec();
                            ss.Kind = StarSpec.KindFromId(st.GetProperty("type").GetString());
                            ss.Value = Int(st, "value", 0);
                            stars.Add(ss);
                        }
                        spec.Stars = stars.ToArray();

                        JsonElement ov;
                        if (lv.TryGetProperty("overrides", out ov))
                        {
                            JsonElement chunks;
                            if (ov.TryGetProperty("fixedChunks", out chunks))
                            {
                                List<string> ids = new List<string>();
                                foreach (JsonElement c in chunks.EnumerateArray()) ids.Add(c.GetString());
                                spec.FixedChunks = ids.ToArray();
                            }
                            JsonElement ramp;
                            if (ov.TryGetProperty("ramp", out ramp)) spec.Ramp = ramp.GetBoolean();
                        }
                        ll.Spec = spec;
                        result.Add(ll);
                    }
                }
            }
            return result;
        }
    }
}
