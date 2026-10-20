using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BoingTower.Content
{
    /// <summary>
    /// Merges the source files under Content/ into one deterministic manifest text.
    /// Plain C# (System.IO only) so Editor tooling and EditMode tests share the same code.
    /// Ordering: regions by index, levels by id, items/gemPacks/coinPacks/modules keep the order of their source file.
    /// </summary>
    public static class ContentManifestMerger
    {
        public const int ManifestVersion = 1;

        public static readonly string[] SingleFiles =
        {
            "items/items.json", "economy/gem_packs.json", "economy/coin_packs.json", "config/modules.json", "config/remote_config.json",
        };

        /// <summary>Returns the manifest text (indent 2, LF, no timestamps) for the Content folder at contentRoot.</summary>
        public static string Build(string contentRoot)
        {
            var inputs = new List<KeyValuePair<string, string>>(); // relative path -> normalized text, sorted by path
            foreach (string f in Directory.GetFiles(Path.Combine(contentRoot, "regions"), "*.json"))
                inputs.Add(Read(contentRoot, f));
            foreach (string f in Directory.GetFiles(Path.Combine(contentRoot, "levels"), "*.json"))
                inputs.Add(Read(contentRoot, f));
            foreach (string rel in SingleFiles)
                inputs.Add(Read(contentRoot, Path.Combine(contentRoot, rel.Replace('/', Path.DirectorySeparatorChar))));
            inputs.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));

            var regions = new List<JObject>();
            var levels = new List<JObject>();
            JArray items = null, gemPacks = null, coinPacks = null, modules = null;
            JObject remote = null;
            foreach (var kv in inputs)
            {
                JToken tok = Parse(kv.Key, kv.Value);
                if (kv.Key.StartsWith("regions/", StringComparison.Ordinal)) regions.Add((JObject)tok);
                else if (kv.Key.StartsWith("levels/", StringComparison.Ordinal)) levels.AddRange(((JArray)tok).Cast<JObject>());
                else if (kv.Key == "items/items.json") items = (JArray)tok;
                else if (kv.Key == "economy/gem_packs.json") gemPacks = (JArray)tok;
                else if (kv.Key == "economy/coin_packs.json") coinPacks = (JArray)tok;
                else if (kv.Key == "config/modules.json") modules = (JArray)tok;
                else if (kv.Key == "config/remote_config.json") remote = (JObject)tok;
            }

            regions = regions.OrderBy(r => (int)r["index"]).ToList();
            levels = levels.OrderBy(l => (string)l["id"], StringComparer.Ordinal).ToList();

            var root = new JObject
            {
                ["version"] = ManifestVersion,
                ["contentHash"] = Hash(inputs),
                ["regions"] = new JArray(regions),
                ["levels"] = new JArray(levels),
                ["items"] = items,
                ["gemPacks"] = gemPacks,
                ["coinPacks"] = coinPacks,
                ["modules"] = modules,
                ["remoteConfig"] = remote,
            };

            var sb = new StringBuilder();
            using (var sw = new StringWriter(sb))
            {
                sw.NewLine = "\n";
                using (var jw = new JsonTextWriter(sw) { Formatting = Formatting.Indented, Indentation = 2, IndentChar = ' ' })
                    root.WriteTo(jw);
            }
            return sb.ToString().Replace("\r\n", "\n") + "\n";
        }

        static KeyValuePair<string, string> Read(string root, string file)
        {
            string rel = Path.GetFullPath(file).Substring(Path.GetFullPath(root).TrimEnd('\\', '/').Length + 1).Replace('\\', '/');
            string text = File.ReadAllText(file, Encoding.UTF8).Replace("\r\n", "\n");
            if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);
            return new KeyValuePair<string, string>(rel, text);
        }

        static JToken Parse(string rel, string text)
        {
            try
            {
                using (var r = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None })
                    return JToken.Load(r);
            }
            catch (JsonException e) { throw new ContentException(rel, "invalid JSON: " + e.Message, e); }
        }

        static string Hash(List<KeyValuePair<string, string>> inputs)
        {
            var sb = new StringBuilder();
            foreach (var kv in inputs) sb.Append(kv.Key).Append('\n').Append(kv.Value).Append('\0');
            using (var sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(new UTF8Encoding(false).GetBytes(sb.ToString()));
                var hex = new StringBuilder(64);
                foreach (byte b in h) hex.Append(b.ToString("x2"));
                return hex.ToString();
            }
        }
    }
}
