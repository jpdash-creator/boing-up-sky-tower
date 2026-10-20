using System.Collections.Generic;
using BoingUpSkyTower.Simulation;

namespace BoingUpSkyTower.Content
{
    /// <summary>
    /// Maps a content LevelDef (+ its RegionDef) to the Simulation's engine-agnostic LevelSpec.
    /// Mirrors the dotnet test loader (Tests/Simulation/ContentLoader.cs); the golden level hashes pin both to the same result.
    /// </summary>
    public static class LevelSpecFactory
    {
        public static int ClassFromName(string name)
        {
            switch (name)
            {
                case "simple": return 0;
                case "medium": return 1;
                case "hard": return 2;
                case "complex": return 3;
                case "highly_complex": return 4;
                default: throw new ContentException(name ?? "(null)", "Unknown level class '" + name + "'.");
            }
        }

        public static LevelSpec Build(LevelDef level, RegionDef region)
        {
            if (level == null) throw new ContentException("(null)", "LevelDef is null.");
            if (region == null) throw new ContentException(level.Id, "RegionDef is null.");

            var spec = new LevelSpec();
            spec.Seed = level.Seed;
            spec.Difficulty = ContentRepository.EffectiveDifficulty(level, region);
            spec.LevelIndex = level.Index;
            spec.Class = ClassFromName(level.LevelClass);
            spec.Signature = SignatureKinds.FromModuleId(region.Rules.SignatureModule);

            var goal = new GoalSpec();
            goal.Type = GoalTypes.FromId(level.Goal.Type);
            goal.Value = level.Goal.Value ?? 0;
            goal.Count = level.Goal.Count ?? 0;
            goal.TimeSec = level.Goal.TimeSec ?? 0;
            spec.Goal = goal;

            var stars = new StarSpec[level.Stars.Count];
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i] = new StarSpec { Kind = StarSpec.KindFromId(level.Stars[i].Type), Value = level.Stars[i].Value ?? 0 };
            }
            spec.Stars = stars;

            if (level.Overrides != null)
            {
                if (level.Overrides.FixedChunks != null) spec.FixedChunks = level.Overrides.FixedChunks.ToArray();
                if (level.Overrides.Ramp.HasValue) spec.Ramp = level.Overrides.Ramp.Value;
            }
            return spec;
        }

        /// <summary>
        /// Endless mode spec of a region: its signature and difficulty offset, the run's seed (kept with the run so a retry or a replay can
        /// rebuild the same tower) and the floor where the layout difficulty reaches 1 (remote config generator.endlessDifficultyFloors).
        /// </summary>
        public static LevelSpec BuildEndless(RegionDef region, int seed, int difficultyFloors)
        {
            if (region == null) throw new ContentException("(null)", "RegionDef is null.");
            Fx offset = Fx.FromRatio((int)System.Math.Round(region.Rules.DifficultyOffset * 10000), 10000);
            return EndlessTower.MakeSpec(seed, offset, SignatureKinds.FromModuleId(region.Rules.SignatureModule), difficultyFloors);
        }

        /// <summary>Convenience: spec for a level id using a loaded repository.</summary>
        public static LevelSpec Build(ContentRepository repo, string levelId)
        {
            LevelDef level = repo.GetLevel(levelId);
            return Build(level, repo.GetRegion(level.Region));
        }
    }
}
