using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkyHopper.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SkyHopper.Content
{
    /// <summary>
    /// Read-only, validated view of all shipped content, loaded from the merged manifest
    /// (Resources/Content/content_manifest.json is the offline fallback; remote manifests use LoadFromJson).
    /// </summary>
    public sealed class ContentRepository
    {
        public const string ResourcePath = "Content/content_manifest";
        public const int ExpectedRegions = 30;
        public const int ExpectedLevelsPerRegion = 15;

        readonly Dictionary<string, RegionDef> _regionsById = new Dictionary<string, RegionDef>();
        readonly Dictionary<int, RegionDef> _regionsByIndex = new Dictionary<int, RegionDef>();
        readonly Dictionary<string, LevelDef> _levelsById = new Dictionary<string, LevelDef>();
        readonly List<RegionDef> _regions = new List<RegionDef>();
        readonly List<RegionDef> _liveRegions = new List<RegionDef>();

        public int ManifestVersion { get; private set; }
        public string ContentHash { get; private set; }
        public IReadOnlyList<RegionDef> Regions { get { return _regions; } }       // ordered by index
        public IReadOnlyList<RegionDef> LiveRegions { get { return _liveRegions; } }
        public IReadOnlyList<ModuleDef> Modules { get; private set; }
        public IReadOnlyList<GemPackDef> GemPacks { get; private set; }
        public IReadOnlyList<CoinPackDef> CoinPacks { get; private set; }
        public IReadOnlyList<ItemDef> Items { get; private set; }
        public RemoteConfigDef RemoteConfig { get; private set; }
        public int LevelCount { get { return _levelsById.Count; } }

        ContentRepository() { }

        /// <summary>Loads the built-in fallback manifest from Resources.</summary>
        public static ContentRepository LoadFromResources()
        {
            var asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>(ResourcePath);
            if (asset == null) throw new ContentException(ResourcePath, "fallback manifest not found in Resources");
            return LoadFromJson(asset.text);
        }

        public static ContentRepository LoadFromJson(string manifestJson)
        {
            if (string.IsNullOrEmpty(manifestJson)) throw new ContentException("manifest", "manifest text is empty");
            JObject root;
            try
            {
                using (var r = new JsonTextReader(new StringReader(manifestJson)) { DateParseHandling = DateParseHandling.None })
                    root = JObject.Load(r);
            }
            catch (JsonException e) { throw new ContentException("manifest", "invalid JSON: " + e.Message, e); }

            var repo = new ContentRepository();
            var ser = JsonSerializer.Create(new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });

            JToken ver = root["version"];
            if (ver == null) throw new ContentException("manifest", "missing 'version'");
            repo.ManifestVersion = (int)ver;
            if (repo.ManifestVersion != ContentManifestMerger.ManifestVersion)
                throw new ContentException("manifest", "unsupported manifest version " + repo.ManifestVersion);
            repo.ContentHash = (string)root["contentHash"];
            if (repo.ContentHash == null) throw new ContentException("manifest", "missing 'contentHash'");

            var regions = ReadList<RegionDef>(root, "regions", ser, "region");
            var levels = ReadList<LevelDef>(root, "levels", ser, "level");
            repo.Items = ReadList<ItemDef>(root, "items", ser, "item");
            repo.GemPacks = ReadList<GemPackDef>(root, "gemPacks", ser, "gemPack");
            // Optional: a manifest built before the shop (docs/14) has no coinPacks; ShopCatalog then falls back to its built-in table.
            repo.CoinPacks = root["coinPacks"] != null ? ReadList<CoinPackDef>(root, "coinPacks", ser, "coinPack") : new List<CoinPackDef>();
            repo.Modules = ReadList<ModuleDef>(root, "modules", ser, "module");

            JToken rc = root["remoteConfig"];
            if (rc == null || rc.Type != JTokenType.Object) throw new ContentException("remoteConfig", "missing 'remoteConfig' object");
            try { repo.RemoteConfig = rc.ToObject<RemoteConfigDef>(ser); }
            catch (JsonException e) { throw new ContentException("remoteConfig", "malformed: " + e.Message, e); }

            foreach (LevelDef l in levels)
            {
                if (repo._levelsById.ContainsKey(l.Id)) throw new ContentException(l.Id, "duplicate level id");
                repo._levelsById[l.Id] = l;
            }
            foreach (RegionDef r in regions.OrderBy(x => x.Index))
            {
                if (repo._regionsById.ContainsKey(r.Id)) throw new ContentException(r.Id, "duplicate region id");
                if (repo._regionsByIndex.ContainsKey(r.Index)) throw new ContentException(r.Id, "duplicate region index " + r.Index);
                repo._regionsById[r.Id] = r;
                repo._regionsByIndex[r.Index] = r;
                repo._regions.Add(r);
                if (r.IsLive) repo._liveRegions.Add(r);
            }
            repo.Validate();
            return repo;
        }

        static List<T> ReadList<T>(JObject root, string key, JsonSerializer ser, string kind)
        {
            JToken tok = root[key];
            if (tok == null || tok.Type != JTokenType.Array) throw new ContentException(key, "missing '" + key + "' array");
            var list = new List<T>();
            int n = 0;
            foreach (JToken el in tok)
            {
                string id = el is JObject o && o["id"] != null ? (string)o["id"] : kind + "#" + n;
                try { list.Add(el.ToObject<T>(ser)); }
                catch (JsonException e) { throw new ContentException(id, "malformed " + kind + ": " + e.Message, e); }
                n++;
            }
            return list;
        }

        void Validate()
        {
            if (_regions.Count != ExpectedRegions)
                throw new ContentException("regions", "expected " + ExpectedRegions + " regions, found " + _regions.Count);
            for (int i = 0; i < _regions.Count; i++)
                if (_regions[i].Index != i + 1) throw new ContentException(_regions[i].Id, "region indexes must be 1..30 without gaps, got " + _regions[i].Index);

            var referenced = new HashSet<string>();
            foreach (RegionDef r in _regions)
            {
                if (r.Status != "live" && r.Status != "teaser") throw new ContentException(r.Id, "unknown region status '" + r.Status + "'");
                if (r.Levels.Count != ExpectedLevelsPerRegion)
                    throw new ContentException(r.Id, "expected " + ExpectedLevelsPerRegion + " levels, found " + r.Levels.Count);
                if (r.Unlock.Requires != null && !_regionsById.ContainsKey(r.Unlock.Requires))
                    throw new ContentException(r.Id, "unlock.requires references unknown region '" + r.Unlock.Requires + "'");
                for (int i = 0; i < r.Levels.Count; i++)
                {
                    string lid = r.Levels[i];
                    LevelDef l;
                    if (!_levelsById.TryGetValue(lid, out l)) throw new ContentException(lid, "level referenced by " + r.Id + " does not exist");
                    if (l.Region != r.Id) throw new ContentException(lid, "level belongs to '" + l.Region + "' but is listed by " + r.Id);
                    if (l.Index != i + 1) throw new ContentException(lid, "level index " + l.Index + " does not match its position " + (i + 1) + " in " + r.Id);
                    referenced.Add(lid);
                }
            }
            foreach (string lid in _levelsById.Keys)
                if (!referenced.Contains(lid)) throw new ContentException(lid, "level is not listed by any region");
        }

        // ---- lookups ----

        public RegionDef GetRegion(string id)
        {
            RegionDef r;
            if (!_regionsById.TryGetValue(id ?? "", out r)) throw new ContentException(id ?? "<null>", "unknown region id");
            return r;
        }

        public RegionDef GetRegionByIndex(int index)
        {
            RegionDef r;
            if (!_regionsByIndex.TryGetValue(index, out r)) throw new ContentException("region index " + index, "unknown region index");
            return r;
        }

        /// <summary>Region by index, or null when there is no such region (for example the one after the last).</summary>
        public RegionDef TryGetRegionByIndex(int index)
        {
            RegionDef r;
            return _regionsByIndex.TryGetValue(index, out r) ? r : null;
        }

        public LevelDef GetLevel(string id)
        {
            LevelDef l;
            if (!_levelsById.TryGetValue(id ?? "", out l)) throw new ContentException(id ?? "<null>", "unknown level id");
            return l;
        }

        /// <summary>The region's levels ordered by index (15). Allocates a new list.</summary>
        public List<LevelDef> LevelsOf(string regionId)
        {
            RegionDef r = GetRegion(regionId);
            var list = new List<LevelDef>(r.Levels.Count);
            foreach (string lid in r.Levels) list.Add(_levelsById[lid]);
            list.Sort((a, b) => a.Index.CompareTo(b.Index));
            return list;
        }

        // ---- helpers (integer / fixed-point math) ----

        /// <summary>d_eff = d + offset * (1 - d) in Fx; base d rounded to 1/1000, offset to 1/10000 (same as the shipped-level solvability test).</summary>
        public static Fx EffectiveDifficulty(LevelDef level, RegionDef region)
        {
            return LevelGenerator.EffectiveDifficulty(
                Fx.FromRatio((int)Math.Round(level.Difficulty * 1000), 1000),
                Fx.FromRatio((int)Math.Round(region.Rules.DifficultyOffset * 10000), 10000));
        }

        /// <summary>coinPayout * coinFactor, rounded half up (doc 08 section 7). coinFactor is taken in 1/10000 steps.</summary>
        public static int CoinPayoutFor(LevelDef level, RegionDef region)
        {
            long factor = (long)Math.Round(region.CoinFactor * 10000);
            return (int)(((long)level.Rewards.CoinPayout * factor + 5000) / 10000);
        }
    }
}
