using System.Collections.Generic;
using Newtonsoft.Json;

namespace BoingUpSkyTower.Content
{
    // Property names match Content/*.json exactly. Unknown JSON members are an error (see ContentRepository).

    public sealed class UnlockDef
    {
        [JsonProperty("requires")] public string Requires { get; set; }   // null for region 1
        [JsonProperty("gemSkip")] public int? GemSkip { get; set; }       // null for region 1
    }

    public sealed class ThemeDef
    {
        [JsonProperty("bundle", Required = Required.Always)] public string Bundle { get; set; }
        [JsonProperty("palette", Required = Required.Always)] public List<string> Palette { get; set; }
        [JsonProperty("music")] public string Music { get; set; }
    }

    public sealed class RegionRulesDef
    {
        [JsonProperty("signatureModule", Required = Required.Always)] public string SignatureModule { get; set; }
        [JsonProperty("difficultyOffset", Required = Required.Always)] public double DifficultyOffset { get; set; }
        [JsonProperty("platformWeights", Required = Required.Always)] public Dictionary<string, double> PlatformWeights { get; set; }
        [JsonProperty("hazards", Required = Required.Always)] public List<string> Hazards { get; set; }
    }

    public sealed class RegionDef
    {
        [JsonProperty("id", Required = Required.Always)] public string Id { get; set; }
        [JsonProperty("index", Required = Required.Always)] public int Index { get; set; }
        [JsonProperty("nameKey", Required = Required.Always)] public string NameKey { get; set; }
        [JsonProperty("name", Required = Required.Always)] public string Name { get; set; }
        [JsonProperty("status", Required = Required.Always)] public string Status { get; set; }   // live | teaser
        [JsonProperty("unlock", Required = Required.Always)] public UnlockDef Unlock { get; set; }
        [JsonProperty("theme", Required = Required.Always)] public ThemeDef Theme { get; set; }
        [JsonProperty("rules", Required = Required.Always)] public RegionRulesDef Rules { get; set; }
        [JsonProperty("coinFactor", Required = Required.Always)] public double CoinFactor { get; set; }
        [JsonProperty("levels", Required = Required.Always)] public List<string> Levels { get; set; }
        [JsonProperty("availableFrom")] public string AvailableFrom { get; set; }

        public bool IsLive { get { return Status == "live"; } }
    }

    public sealed class GoalDef
    {
        [JsonProperty("type", Required = Required.Always)] public string Type { get; set; }
        [JsonProperty("value")] public int? Value { get; set; }
        [JsonProperty("count")] public int? Count { get; set; }
        [JsonProperty("timeSec")] public int? TimeSec { get; set; }
    }

    public sealed class StarDef
    {
        [JsonProperty("type", Required = Required.Always)] public string Type { get; set; }
        [JsonProperty("value")] public int? Value { get; set; }
    }

    public sealed class LevelOverridesDef
    {
        [JsonProperty("fixedChunks")] public List<string> FixedChunks { get; set; }
        [JsonProperty("ramp")] public bool? Ramp { get; set; }
    }

    public sealed class GemRewardDef
    {
        [JsonProperty("chance", Required = Required.Always)] public double Chance { get; set; }
        [JsonProperty("min", Required = Required.Always)] public int Min { get; set; }
        [JsonProperty("max", Required = Required.Always)] public int Max { get; set; }
        [JsonProperty("firstClearGuaranteed")] public int? FirstClearGuaranteed { get; set; }
        [JsonProperty("starBonusMax")] public int? StarBonusMax { get; set; }
        [JsonProperty("replayChance")] public double? ReplayChance { get; set; }
        [JsonProperty("replayAmount")] public int? ReplayAmount { get; set; }
    }

    public sealed class RewardsDef
    {
        [JsonProperty("coinPayout", Required = Required.Always)] public int CoinPayout { get; set; }
        [JsonProperty("replayCoinPercent", Required = Required.Always)] public int ReplayCoinPercent { get; set; }
        [JsonProperty("gems")] public GemRewardDef Gems { get; set; }
    }

    public sealed class LevelDef
    {
        public const string ClassSimple = "simple";
        public const string ClassComplex = "complex";
        public const string ClassHighlyComplex = "highly_complex";

        [JsonProperty("id", Required = Required.Always)] public string Id { get; set; }
        [JsonProperty("region", Required = Required.Always)] public string Region { get; set; }
        [JsonProperty("index", Required = Required.Always)] public int Index { get; set; }
        [JsonProperty("seed", Required = Required.Always)] public int Seed { get; set; }
        [JsonProperty("class", Required = Required.Always)] public string LevelClass { get; set; }
        [JsonProperty("difficulty", Required = Required.Always)] public double Difficulty { get; set; }
        [JsonProperty("goal", Required = Required.Always)] public GoalDef Goal { get; set; }
        [JsonProperty("stars", Required = Required.Always)] public List<StarDef> Stars { get; set; }
        [JsonProperty("overrides")] public LevelOverridesDef Overrides { get; set; }
        [JsonProperty("rewards", Required = Required.Always)] public RewardsDef Rewards { get; set; }
    }

    public sealed class ModuleDef
    {
        [JsonProperty("id", Required = Required.Always)] public string Id { get; set; }
        [JsonProperty("name", Required = Required.Always)] public string Name { get; set; }
        [JsonProperty("description", Required = Required.Always)] public string Description { get; set; }
        [JsonProperty("regions", Required = Required.Always)] public List<int> Regions { get; set; }
        [JsonProperty("buildStatus", Required = Required.Always)] public string BuildStatus { get; set; }
    }

    /// <summary>
    /// Gem pack sold for real money (docs/14). The player pays UsdPromo while the launch promotion is on (UsdList is shown crossed out),
    /// UsdList otherwise. Badge is "", "popular" or "best_value".
    /// </summary>
    public sealed class GemPackDef
    {
        [JsonProperty("id", Required = Required.Always)] public string Id { get; set; }
        [JsonProperty("gems", Required = Required.Always)] public int Gems { get; set; }
        [JsonProperty("usdList")] public double UsdList { get; set; }
        [JsonProperty("usdPromo")] public double UsdPromo { get; set; }
        [JsonProperty("sku", Required = Required.Always)] public string Sku { get; set; }
        [JsonProperty("badge")] public string Badge { get; set; }
        /// <summary>Legacy fields of the pre-docs/14 manifest (kept so a stale manifest still loads until it is regenerated).</summary>
        [JsonProperty("usd")] public double LegacyUsd { get; set; }
        [JsonProperty("nameKey")] public string LegacyNameKey { get; set; }
    }

    /// <summary>Coin pack bought with gems (docs/14). The price is GemsPromo while the launch promotion is on, GemsList otherwise.</summary>
    public sealed class CoinPackDef
    {
        [JsonProperty("id", Required = Required.Always)] public string Id { get; set; }
        [JsonProperty("coins", Required = Required.Always)] public int Coins { get; set; }
        [JsonProperty("gemsList", Required = Required.Always)] public int GemsList { get; set; }
        [JsonProperty("gemsPromo", Required = Required.Always)] public int GemsPromo { get; set; }
    }

    public sealed class ItemSpritesDef
    {
        [JsonProperty("left", Required = Required.Always)] public string Left { get; set; }
        [JsonProperty("right", Required = Required.Always)] public string Right { get; set; }
    }

    public sealed class ItemPriceDef
    {
        [JsonProperty("gems")] public int? Gems { get; set; }
        [JsonProperty("coins")] public int? Coins { get; set; }
    }

    public sealed class ItemDef
    {
        [JsonProperty("id", Required = Required.Always)] public string Id { get; set; }
        [JsonProperty("slot", Required = Required.Always)] public string Slot { get; set; }
        [JsonProperty("rarity", Required = Required.Always)] public string Rarity { get; set; }
        [JsonProperty("bundle", Required = Required.Always)] public string Bundle { get; set; }
        [JsonProperty("sprites", Required = Required.Always)] public ItemSpritesDef Sprites { get; set; }
        [JsonProperty("price", Required = Required.Always)] public ItemPriceDef Price { get; set; }
        [JsonProperty("tags")] public List<string> Tags { get; set; }
        [JsonProperty("availableFrom")] public string AvailableFrom { get; set; }
        [JsonProperty("availableTo")] public string AvailableTo { get; set; }
    }

    public sealed class RampConfigDef
    {
        [JsonProperty("rampFloors", Required = Required.Always)] public int RampFloors { get; set; }
        [JsonProperty("rampPercent", Required = Required.Always)] public int RampPercent { get; set; }
        [JsonProperty("rampCap", Required = Required.Always)] public int RampCap { get; set; }
        [JsonProperty("playerSpeedShare", Required = Required.Always)] public double PlayerSpeedShare { get; set; }
    }

    public sealed class GeneratorConfigDef
    {
        [JsonProperty("endlessDifficultyFloors", Required = Required.Always)] public int EndlessDifficultyFloors { get; set; }
        [JsonProperty("screenWidthUnits", Required = Required.Always)] public int ScreenWidthUnits { get; set; }
    }

    public sealed class ScoreConfigDef
    {
        [JsonProperty("floorPoints", Required = Required.Always)] public int FloorPoints { get; set; }
        [JsonProperty("comboPointsMultiplier", Required = Required.Always)] public int ComboPointsMultiplier { get; set; }
        [JsonProperty("coinPoints", Required = Required.Always)] public int CoinPoints { get; set; }
    }

    public sealed class EconomyConfigDef
    {
        [JsonProperty("coinsPerGem", Required = Required.Always)] public int CoinsPerGem { get; set; }
        [JsonProperty("replayCoinPercent", Required = Required.Always)] public int ReplayCoinPercent { get; set; }
        [JsonProperty("regionCoinScalePercent", Required = Required.Always)] public int RegionCoinScalePercent { get; set; }
        [JsonProperty("vipGemsPerDay", Required = Required.Always)] public int VipGemsPerDay { get; set; }
        [JsonProperty("endlessCoinDailyFullRateRuns", Required = Required.Always)] public int EndlessCoinDailyFullRateRuns { get; set; }
        [JsonProperty("endlessCoinOverCapMultiplier", Required = Required.Always)] public double EndlessCoinOverCapMultiplier { get; set; }
    }

    public sealed class RemoteConfigDef
    {
        [JsonProperty("ramp", Required = Required.Always)] public RampConfigDef Ramp { get; set; }
        [JsonProperty("generator", Required = Required.Always)] public GeneratorConfigDef Generator { get; set; }
        [JsonProperty("score", Required = Required.Always)] public ScoreConfigDef Score { get; set; }
        [JsonProperty("economy", Required = Required.Always)] public EconomyConfigDef Economy { get; set; }
    }
}
