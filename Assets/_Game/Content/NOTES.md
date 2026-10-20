# BoingUpSkyTower.Content (T2: runtime content loading)

Assembly `BoingUpSkyTower.Content` (references BoingUpSkyTower.Simulation + Unity.Nuget.Newtonsoft-Json, package `com.unity.nuget.newtonsoft-json` 3.2.2). Namespace `BoingUpSkyTower.Content`.

## Loading
- Offline fallback: `ContentRepository.LoadFromResources()` reads `Assets/Resources/Content/content_manifest.json` (TextAsset, path `Content/content_manifest`).
- Remote/other text: `ContentRepository.LoadFromJson(string manifestJson)` (same format, validated identically).
- Regenerate the manifest after any change under `Content/`: menu `Tools/Boing Up: Sky Tower/Build Content Manifest`, or headless
  `Unity.exe -batchmode -nographics -projectPath <repo> -executeMethod BoingUpSkyTower.Editor.ContentSync.ContentManifestBuilder.BuildFromCommandLine -logFile <log>` (no `-quit`; exits 0/1).
  It also runs automatically before every player build (`ContentManifestBuildStep`, IPreprocessBuildWithReport).
- The EditMode test `ResourcesManifestIsInSyncWithSourceContent` fails when the manifest is stale.

## Manifest format
`{"version":1,"contentHash":"<sha256 of all inputs>","regions":[..],"levels":[..],"items":[..],"gemPacks":[..],"coinPacks":[..],"modules":[..],"remoteConfig":{..}}`.
Indent 2, LF, no timestamps. Regions ordered by index, levels by id (ordinal); items, gemPacks, modules keep their source-file order. The merge logic is `ContentManifestMerger.Build(contentRoot)` (plain C#, shared by the Editor tool and tests).

## Public API
- `ContentRepository`: `Regions` (by index), `LiveRegions`, `Modules`, `GemPacks`, `CoinPacks` (optional key, docs/14), `Items`, `RemoteConfig`, `ContentHash`, `LevelCount`; `GetRegion(id)`, `GetRegionByIndex(n)`, `GetLevel(id)`, `LevelsOf(regionId)` (ordered by index, new list per call).
- Static helpers: `ContentRepository.EffectiveDifficulty(LevelDef, RegionDef)` returns `Fx` (base d rounded to 1/1000, region offset to 1/10000, then `LevelGenerator.EffectiveDifficulty`); `ContentRepository.CoinPayoutFor(LevelDef, RegionDef)` = coinPayout * coinFactor, half up, integer math (factor in 1/10000 steps).
- Models (`Models.cs`): RegionDef, UnlockDef, ThemeDef, RegionRulesDef, LevelDef (`LevelClass` = JSON `class`; constants `ClassSimple/ClassComplex/ClassHighlyComplex`), GoalDef, StarDef, LevelOverridesDef, RewardsDef, GemRewardDef, ModuleDef, GemPackDef, ItemDef, RemoteConfigDef (+ Ramp/Generator/Score/Economy sections). Content layer uses `double`; convert to `Fx` only at the Simulation boundary.
- Errors: `ContentException` (`ContentId`, message prefixed `[id]`).

## Validation on load (no silent defaults)
Manifest version == 1; contentHash present; every required field present (Newtonsoft `Required.Always`); unknown JSON members rejected (`MissingMemberHandling.Error`); exactly 30 regions with indexes 1..30; 15 levels each; every referenced level exists, belongs to its region and its `index` equals its position; no duplicate ids; no unlisted (orphan) levels; `unlock.requires` references an existing region; status is live or teaser.

## Assumptions
- Nullable by design (not required): `unlock.requires/gemSkip` (null for region 1), `theme.music`, `availableFrom/To`, level `overrides`, `rewards.gems` and its optional fields, goal `value/count/timeSec`, star `value`, item `tags`.
- If the JSON schemas later add fields, the models must be extended (strict unknown-member check will throw with the offending id).
- Mapping LevelDef -> Simulation LevelSpec is intentionally not done here.
- `IsRegionPlayable(region, progress)` is not implemented yet.
- `LoadFromResources` uses `UnityEngine.Resources`; everything else is engine-free.
