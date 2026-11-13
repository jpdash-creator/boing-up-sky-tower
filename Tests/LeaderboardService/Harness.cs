using System;
using System.IO;
using System.Linq;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Simulation;

namespace FluffyClimbers.LeaderboardService.Tests
{
    /// <summary>Shared test setup: the shipped content, the REAL RunSession driven like the game does, and the upload built from what it records.</summary>
    public static class Harness
    {
        /// <summary>Repo root: from the compile-time path of this file (the build output may live outside the repo, e.g. with an artifacts path), FLUFFY_ROOT overrides.</summary>
        public static string Root
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("FLUFFY_ROOT");
                if (!string.IsNullOrEmpty(env)) return env;
                return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThisFile()), "..", ".."));
            }
        }

        static string ThisFile([System.Runtime.CompilerServices.CallerFilePath] string p = "") { return p; }

        static ContentRepository repo;
        public static ContentRepository Repo
        {
            get
            {
                if (repo == null) repo = ContentRepository.LoadFromJson(File.ReadAllText(Path.Combine(Root, "Assets", "Resources", "Content", "content_manifest.json")));
                return repo;
            }
        }

        public static RunReplayer Replayer { get { return new RunReplayer(Repo); } }
        public static readonly Limits Lim = new Limits();

        /// <summary>The config the GAME builds for a run: GameSession.ApplyDifficulty on the shared config, cloned, then the hero's stats (GameSession.BuildRunCfg). Written out here independently of RunReplayer.ConfigFor.</summary>
        public static SimConfig ClientCfg(int mode, CharacterStats stats)
        {
            var shared = new SimConfig();
            shared.ScrollStepPercent = SimConfig.DifficultyStepPercent[mode];
            shared.GemSpotPermille = SimConfig.DifficultyGemPermille[mode];
            shared.ScrollStepCapPercent = SimConfig.TopSpeedUpPercent(mode);
            SimConfig run = shared.Clone();
            (stats ?? CharacterStats.Default).ApplyTo(run);
            return run;
        }

        public static CharacterSpec Hero(string id) { return CharacterDefs.Get(id); }

        public static RunSession NewLevelSession(string levelId, int mode, int seed, string heroId = "momo", int variant = 0, bool stripGems = false)
        {
            CharacterSpec h = Hero(heroId);
            var s = new RunSession(Repo, ClientCfg(mode, HeroRolls.StatsFor(h, variant)), levelId, stripGems, seed);
            s.Mode = mode;
            s.HeroId = heroId;
            s.ApplyHeroPowers(h);
            return s;
        }

        public static RunSession NewEndlessSession(string regionId, int mode, int seed, bool timeAttack, string heroId = "momo", int variant = 0)
        {
            CharacterSpec h = Hero(heroId);
            var s = new RunSession(Repo, ClientCfg(mode, HeroRolls.StatsFor(h, variant)), Repo.GetRegion(regionId), seed, timeAttack);
            s.Mode = mode;
            s.HeroId = heroId;
            s.ApplyHeroPowers(h);
            return s;
        }

        /// <summary>The solving inputs of the level bot for a fresh or revived state (a real, goal reaching run line).</summary>
        public static int[] BotTrace(RunSession s, SimState from, int targetRow = 0)
        {
            LevelBot bot = s.IsEndless ? new LevelBot(s.Sim, targetRow, 600000) : new LevelBot(s.Sim, s.Spec, s.Data, 600000);
            bot.RecordInputs = true;
            BotResult r = bot.SolveFrom(from.Clone());
            if (!r.Solved || bot.InputTrace == null) throw new InvalidOperationException("bot did not solve (best row " + r.BestRow + ")");
            return bot.InputTrace;
        }

        public static void Play(RunSession s, int[] trace, int from = 0, int count = -1)
        {
            int end = count < 0 ? trace.Length : Math.Min(trace.Length, from + count);
            for (int i = from; i < end && !s.Over; i++) s.Step(trace[i]);
        }

        /// <summary>The upload of a finished (or running) session, exactly what the client would build from RunRecord.</summary>
        public static RunSubmission Upload(RunSession s, string kind, int seed, int variant = 0)
        {
            RunRecord rec = s.BuildRunRecord("0.59.1");
            return new RunSubmission
            {
                SimVersion = 1, GameVersion = "0.59.1", Kind = kind, LevelId = s.Level != null ? s.Level.Id : null, RegionId = s.IsEndless ? s.Region.Id : null,
                Mode = s.Mode, Seed = seed, HeroId = s.HeroId, HeroVariant = variant,
                InputsB64 = Convert.ToBase64String(rec.Inputs), ReviveAt = rec.ReviveAt, RunId = Guid.NewGuid().ToString("N"),
                Claimed = new ClaimedResult { Floors = rec.Floors, Ticks = rec.Ticks, Score = s.State.Score, Crown = CrownScore.FromScore(s.State.Score, s.Mode) }
            };
        }

        /// <summary>A copy of the submission whose claim is rewritten to what the replay says (to build a different but self consistent run).</summary>
        public static RunSubmission WithHonestClaim(RunSubmission s)
        {
            ReplayResult r = Replayer.Replay(s, Lim);
            if (!r.Ok) throw new InvalidOperationException("replay failed: " + r.Error + " " + r.Detail);
            s.Claimed = new ClaimedResult { Floors = r.Floors, Ticks = r.Ticks, Score = r.Score, Crown = r.Crown };
            return s;
        }

        /// <summary>A tier hero (rolled per player) with at least <paramref name="minPool"/> pool entries (the table row plus generated variants), or null while HeroVariants.Rows is empty.</summary>
        public static string HeroWithPool(int minPool)
        {
            CharacterSpec c = CharacterDefs.All.FirstOrDefault(h => HeroRolls.IsRollable(h.Id) && HeroRolls.PoolSize(h.Id) >= minPool);
            return c == null ? null : c.Id;
        }

        /// <summary>Any tier hero (rolled per player).</summary>
        public static string FirstRollableHero() { return HeroWithPool(1); }
    }
}
