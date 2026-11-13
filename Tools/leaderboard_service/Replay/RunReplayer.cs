using System;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Simulation;

namespace FluffyClimbers.LeaderboardService
{
    /// <summary>One run as the client uploads it. Everything the server needs to rebuild the run with the deterministic simulation; the claimed numbers are only checked, never trusted.</summary>
    public sealed class RunSubmission
    {
        /// <summary>Rules version of the simulation the client ran (LeaderboardRules / Limits.SimVersions). A client on an unsupported version is told to update.</summary>
        public int SimVersion { get; set; }
        public string GameVersion { get; set; }
        /// <summary>"level", "endless" or "timeattack".</summary>
        public string Kind { get; set; }
        public string LevelId { get; set; }
        public string RegionId { get; set; }
        /// <summary>Gameplay setting 0..4 (Relaxed, Easy, Normal, Hard, Insane).</summary>
        public int Mode { get; set; }
        /// <summary>Campaign: attempt seed of the layout. Endless / Time Attack: the tower seed.</summary>
        public int Seed { get; set; }
        public string HeroId { get; set; }
        /// <summary>Index of the player's rolled variant of the hero (0 = the table row). Checked against the hero's pool and locked per account on first use.</summary>
        public int HeroVariant { get; set; }
        /// <summary>RunRecord.Inputs (InputCodec run-length bytes), base64.</summary>
        public string InputsB64 { get; set; }
        /// <summary>RunRecord.ReviveAt: for every revive the number of recorded ticks before it (new recorder field, see LEADERBOARD-DESIGN.md section 5).</summary>
        public int[] ReviveAt { get; set; }
        /// <summary>Client run id (GUID), only for audit and log correlation.</summary>
        public string RunId { get; set; }
        /// <summary>Ticket from POST /start-run: carries the server-chosen seed, bound to uid, level or region and setting, single use, short expiry.</summary>
        public string Ticket { get; set; }
        public ClaimedResult Claimed { get; set; }
    }

    public sealed class ClaimedResult
    {
        public int Floors { get; set; }
        public int Ticks { get; set; }
        public int Score { get; set; }
        public long Crown { get; set; }
    }

    public sealed class ReplayResult
    {
        public bool Ok;
        /// <summary>Machine readable reason when not Ok: bad_request, unsupported_version, unknown_level, unknown_hero, bad_variant, bad_inputs, too_long, bad_revive, inputs_after_end, power_budget, mismatch.</summary>
        public string Error;
        public string Detail;
        public string SlotId;
        public string Board;
        public int Floors, Ticks, Score, Revives, TicksStepped;
        public long Crown;
        public bool GoalReached, Dead;
        public int[] BoughtUnits = new int[PowerUps.MaxType + 1];

        public static ReplayResult Fail(string error, string detail) { return new ReplayResult { Ok = false, Error = error, Detail = detail }; }
    }

    /// <summary>
    /// Server side replay: builds the same world the game built (hero physics from the per-account roll, Gameplay setting, layout seed), feeds the recorded per-tick inputs to the
    /// deterministic <see cref="Sim"/> and puts the revives back at the recorded tick. This mirrors RunSession.Step line by line (see the comments); the recorded input is the input that
    /// REACHED the simulation (power-up bits are already filtered by stock and cooldown, hero power bits already turned into the bought bit), so a replay needs no stock and no cooldown to be exact.
    /// The cooldown is re-run here only to count how many activations were NOT free hero powers (bought units), which is capped by Limits.MaxBoughtUnitsPerTypePerRun.
    /// </summary>
    public sealed class RunReplayer
    {
        readonly ContentRepository repo;

        public RunReplayer(ContentRepository repo) { this.repo = repo; }

        /// <summary>Input bits a legitimate recording can hold: Left, Right, Jump and the seven bought power-up edges. Hero bits never reach the recorder (RunSession converts them).</summary>
        public const int ValidInputMask = SimInput.Left | SimInput.Right | SimInput.Jump | SimInput.PowerMask;

        /// <summary>The simulation config of a Gameplay setting: the shared defaults plus what GameSession.ApplyDifficulty sets (scroll step percent, gem chance, top speed-up of the setting). Keep in sync with GameSession.ApplyDifficulty; a test pins the source lines.</summary>
        public static SimConfig ConfigFor(int mode)
        {
            SimConfig cfg = new SimConfig();
            cfg.ScrollStepPercent = SimConfig.DifficultyStepPercent[mode];
            cfg.GemSpotPermille = SimConfig.DifficultyGemPermille[mode];
            cfg.ScrollStepCapPercent = SimConfig.TopSpeedUpPercent(mode);
            return cfg;
        }

        public ReplayResult Replay(RunSubmission s, Limits lim)
        {
            if (s == null || s.Claimed == null) return ReplayResult.Fail("bad_request", "empty submission");
            if (!lim.SimVersionAllowed(s.SimVersion)) return ReplayResult.Fail("unsupported_version", "sim version " + s.SimVersion);
            if (s.Mode < 0 || s.Mode >= TimeRecords.ModeCount) return ReplayResult.Fail("bad_request", "mode");
            if (s.Kind != Kinds.Level && s.Kind != Kinds.Endless && s.Kind != Kinds.TimeAttack) return ReplayResult.Fail("bad_request", "kind");

            // ---- hero: the id must exist and the variant must be a real entry of its pool ----
            CharacterSpec hero = s.HeroId == null ? null : CharacterDefs.Get(s.HeroId);
            if (hero == null) return ReplayResult.Fail("unknown_hero", s.HeroId);
            if (s.HeroVariant != 0 && !HeroRolls.IsValidVariant(s.HeroId, s.HeroVariant)) return ReplayResult.Fail("bad_variant", s.HeroId + "#" + s.HeroVariant);

            // ---- inputs ----
            byte[] raw;
            try { raw = string.IsNullOrEmpty(s.InputsB64) ? new byte[0] : Convert.FromBase64String(s.InputsB64); }
            catch (FormatException) { return ReplayResult.Fail("bad_inputs", "base64"); }
            if (raw.Length > lim.MaxInputBytes) return ReplayResult.Fail("too_long", "input bytes " + raw.Length);
            int len = InputCodec.DecodedLength(raw);   // no allocation of the big array before the length is checked
            if (len < 0) return ReplayResult.Fail("bad_inputs", "damaged run-length data");
            if (len == 0) return ReplayResult.Fail("bad_inputs", "no inputs");
            if (len > lim.MaxTicks) return ReplayResult.Fail("too_long", "ticks " + len);
            int[] inputs = InputCodec.Decode(raw);
            if (inputs == null) return ReplayResult.Fail("bad_inputs", "damaged run-length data");
            for (int i = 0; i < inputs.Length; i++)
                if ((inputs[i] & ~ValidInputMask) != 0) return ReplayResult.Fail("bad_inputs", "illegal input bits at tick " + i);

            int[] revives = s.ReviveAt ?? new int[0];
            if (revives.Length > lim.MaxRevives) return ReplayResult.Fail("bad_revive", "too many revives");
            if (revives.Length > 0 && s.Kind != Kinds.Level) return ReplayResult.Fail("bad_revive", "revive in endless or time attack");
            for (int i = 0; i < revives.Length; i++)
                if (revives[i] < 0 || revives[i] > len || (i > 0 && revives[i] < revives[i - 1])) return ReplayResult.Fail("bad_revive", "order");

            // ---- world (same construction as RunSession's two constructors) ----
            SimConfig cfg = ConfigFor(s.Mode);
            CharacterStats stats = HeroRolls.StatsFor(hero, s.HeroVariant);   // the player's own rolled copy, capped at the safety limits
            stats.ApplyTo(cfg);

            LevelSpec spec;
            Sim sim;
            EndlessWorld world = null;
            RegionDef region;
            string slotId;
            if (s.Kind == Kinds.Level)
            {
                LevelDef level = s.LevelId == null ? null : SafeLevel(s.LevelId);
                if (level == null) return ReplayResult.Fail("unknown_level", s.LevelId);
                region = repo.GetRegion(level.Region);
                if (region.Index < 1 || region.Index > lim.MaxRegion) return ReplayResult.Fail("unknown_level", "region closed");
                spec = LevelSpecFactory.Build(level, region, s.Seed);
                LevelData data = LevelBuilder.Build(cfg, spec);   // a first clear strips gems on the client; gems never change the climb (pinned by a test)
                sim = new Sim(cfg, data, spec);
                slotId = SlotIds.Level(level.Id, s.Mode);
            }
            else
            {
                region = s.RegionId == null ? null : SafeRegion(s.RegionId);
                if (region == null) return ReplayResult.Fail("unknown_level", s.RegionId);
                if (region.Index < 1 || region.Index > lim.MaxRegion) return ReplayResult.Fail("unknown_level", "region closed");
                bool ta = s.Kind == Kinds.TimeAttack;
                spec = LevelSpecFactory.BuildEndless(region, s.Seed, repo.RemoteConfig.Generator.EndlessDifficultyFloors, ta);
                world = new EndlessWorld(cfg, spec);
                sim = world.Sim;
                slotId = ta ? SlotIds.TimeAttack(region.Id, s.Mode) : SlotIds.Endless(region.Id, s.Mode);
            }
            SimState state = sim.NewState();

            // hero cooldown clock, only to tell free hero activations from bought units
            HeroPowerCooldown cd = null;
            if (hero.Powers != null && hero.Powers.Length > 0)
            {
                int r = (int)hero.Rarity;
                var h = new HeroPowerCooldown(hero.Powers, r >= 0 && r < CharacterDefs.CooldownSeconds.Length ? CharacterDefs.CooldownSeconds[r] : 0);
                if (h.Count > 0) cd = h;
            }

            var res = new ReplayResult { SlotId = slotId, Board = SlotIds.BoardOf(s.Kind) };
            bool goalReached = false, goalLost = false, timeUp = false;
            int ri = 0;

            for (int i = 0; i <= inputs.Length; i++)
            {
                // a revive recorded at i happened between tick i-1 and tick i, while the player was dead (the run was over for Step)
                while (ri < revives.Length && revives[ri] == i)
                {
                    if (!state.Dead || goalReached || goalLost) return ReplayResult.Fail("bad_revive", "revive while alive at tick " + i);
                    sim.Revive(ref state);
                    ri++;
                }
                if (i == inputs.Length) break;
                bool over = goalReached || state.Dead || goalLost || timeUp;
                if (over) return ReplayResult.Fail("inputs_after_end", "tick " + i);

                int input = inputs[i];
                if ((input & SimInput.PowerMask) != 0 && !CountBought(res, cd, in state, input, lim)) return ReplayResult.Fail("power_budget", "too many bought power-up units");
                sim.Step(ref state, input);                       // RunSession.Step: Sim.Step(ref State, input)
                if (cd != null) cd.Tick();                        // hero cooldowns run in simulation ticks
                int runTicks = state.StartTick < 0 ? 0 : Math.Max(0, state.Tick - state.StartTick);
                if (s.Kind == Kinds.TimeAttack && !timeUp && TimeRecords.TimeAttackExpired(runTicks)) timeUp = true;
                if (world != null)
                {
                    if (world.Grow(ref state)) sim = world.Sim;   // endless tower grows in chunks; Grow also re-bases heights (RunSession.Step does the same)
                }
                else
                {
                    if (GoalEvaluator.IsComplete(spec, state)) goalReached = true;
                    else if (GoalEvaluator.IsFailed(spec, state)) goalLost = true;
                }
                res.TicksStepped++;
            }

            res.Ok = true;
            res.Floors = state.Row;
            res.Ticks = state.StartTick < 0 ? 0 : Math.Max(0, state.Tick - state.StartTick);
            res.Score = state.Score;
            res.Crown = CrownScore.FromScore(state.Score, s.Mode);
            res.Revives = state.RevivesUsed;
            res.GoalReached = goalReached;
            res.Dead = state.Dead;
            return res;
        }

        /// <summary>Mirror of RunSession.TakePowerUnits seen from the record: every power bit in the stream was an ACCEPTED activation; a free hero use (ready cooldown, power in the hero's list) is not a bought unit, everything else is.</summary>
        static bool CountBought(ReplayResult res, HeroPowerCooldown cd, in SimState state, int input, Limits lim)
        {
            for (int i = (int)PowerUp.Flight; i <= (int)PowerUp.SlowTime; i++)
            {
                PowerUp p = (PowerUp)i;
                if ((input & PowerUps.InputBit(p)) == 0) continue;
                int idx = cd != null ? cd.IndexOf(p) : -1;
                bool free = idx >= 0 && cd.IsReady(idx) && PowerUps.CanActivateHero(in state, p);
                if (free) { cd.TryUse(idx); continue; }
                if (++res.BoughtUnits[i] > lim.MaxBoughtUnitsPerTypePerRun) return false;
            }
            return true;
        }

        /// <summary>True when the level (kind level) or region (endless, timeattack) exists and is playable (region index 1..maxRegion).</summary>
        public bool TargetExists(string kind, string levelId, string regionId, int maxRegion, out string canonicalTarget)
        {
            canonicalTarget = null;
            if (kind == Kinds.Level)
            {
                LevelDef l = levelId == null ? null : SafeLevel(levelId);
                if (l == null) return false;
                RegionDef r = repo.GetRegion(l.Region);
                if (r.Index < 1 || r.Index > maxRegion) return false;
                canonicalTarget = l.Id;
                return true;
            }
            if (kind == Kinds.Endless || kind == Kinds.TimeAttack)
            {
                RegionDef r = regionId == null ? null : SafeRegion(regionId);
                if (r == null || r.Index < 1 || r.Index > maxRegion) return false;
                canonicalTarget = r.Id;
                return true;
            }
            return false;
        }

        LevelDef SafeLevel(string id) { try { return repo.GetLevel(id); } catch (ContentException) { return null; } }
        RegionDef SafeRegion(string id) { try { return repo.GetRegion(id); } catch (ContentException) { return null; } }

        /// <summary>Checks the numbers the client claimed against the replay. Null = identical.</summary>
        public static string CompareClaim(RunSubmission s, ReplayResult r)
        {
            ClaimedResult c = s.Claimed;
            if (c.Floors != r.Floors) return "floors claimed " + c.Floors + " replay " + r.Floors;
            if (c.Ticks != r.Ticks) return "ticks claimed " + c.Ticks + " replay " + r.Ticks;
            if (c.Score != r.Score) return "score claimed " + c.Score + " replay " + r.Score;
            if (c.Crown != r.Crown) return "crown claimed " + c.Crown + " replay " + r.Crown;
            return null;
        }
    }
}
