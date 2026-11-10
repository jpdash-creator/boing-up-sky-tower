using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Simulation;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// One attempt at one level: owns the deterministic Sim and its state, advances it one tick at a time and decides when
    /// the run is over (owner decision: a run ends at goal completion, or when the player falls). Plain C#, no Unity types,
    /// so it is unit-testable and the same code a server could replay.
    /// </summary>
    public sealed class RunSession
    {
        /// <summary>Owner decision 2026-10-25 (doc 22, point 9): 5 gems for the first revive of a run, 10 for the second, never more than two. The first can be free through a rewarded ad.</summary>
        public const int FirstReviveGems = RevivePolicy.FirstGemCost, SecondReviveGems = RevivePolicy.LaterGemCost;
        /// <summary>Revive schedule of this run (owner 2026-11-02, new per run): ads 30 s then 60 s then none; gems 5 then 10 and stays 10. See <see cref="RevivePolicy"/>.</summary>
        public readonly RevivePolicy Revive = new RevivePolicy();
        public static int ReviveCostFor(int gemRevivesUsed) { return RevivePolicy.GemCostFor(gemRevivesUsed); }
        public int NextReviveCost { get { return Revive.NextGemCost; } }
        /// <summary>True once an ad revive was taken in this run.</summary>
        public bool AdReviveUsed { get { return Revive.AdsUsed > 0; } }
        /// <summary>An ad was watched to the end but the revive did not happen yet; the next try revives for free without another ad.</summary>
        public bool AdRewardPending { get; private set; }
        /// <summary>The ad option is offered while the run has an ad left (2 per run).</summary>
        public bool AdReviveOffered { get { return CanRevive && Revive.AdAvailable; } }
        /// <summary>Gems still missing for the next gem revive (0 when affordable).</summary>
        public int ReviveShortfall(ProgressService progress) { return progress == null ? NextReviveCost : System.Math.Max(0, NextReviveCost - progress.Gems); }
        /// <summary>Endless runs have no revive: the result (best floor, coins) is saved the moment the player falls (CUX-8), so a revive would double pay.</summary>
        public bool CanRevive { get { return !IsEndless && !GoalLost; } }

        public readonly SimConfig Cfg;
        /// <summary>The campaign level, or null in endless mode.</summary>
        public readonly LevelDef Level;
        public readonly RegionDef Region;
        public readonly LevelSpec Spec;
        /// <summary>Current world. In endless mode the tower grows in chunks: Data and Sim are replaced (same prefix) when WorldVersion changes.</summary>
        public LevelData Data { get; private set; }
        public Sim Sim { get; private set; }
        public SimState State;

        /// <summary>Endless mode (owner decision 2026-10-20): an ever growing tower of the region, the run ends only when the player falls.</summary>
        public readonly bool IsEndless;
        /// <summary>Seed of the endless tower; stored with the run so it can be rebuilt (replay, server check).</summary>
        public readonly int Seed;
        /// <summary>Campaign: the attempt seed of this run's layout (0 = base layout). Endless: always 0 (its tower seed is <see cref="Seed"/>).</summary>
        public readonly int AttemptSeed;
        /// <summary>One-line description of the layout for logs and bug reports: level, attempt seed and the resulting layout seed.</summary>
        public string LayoutInfo
        {
            get { return IsTimeAttack ? "timeattack " + Region.Id + " seed " + Seed : IsEndless ? "endless " + Region.Id + " seed " + Seed : Level.Id + " attempt " + AttemptSeed + " layoutSeed " + Spec.Seed; }
        }
        /// <summary>Endless tower, null for campaign levels.</summary>
        public readonly EndlessTower Tower;
        /// <summary>Changes every time the endless tower grew (the view then extends its world). Always 0 for campaign levels.</summary>
        public int WorldVersion { get; private set; }
        /// <summary>Endless world (tower, Sim, re-base bookkeeping), null for campaign levels. The tower re-bases its HEIGHTS about every 500 floors so the run can go on to floor 100000 and beyond; floors stay absolute.</summary>
        public readonly EndlessWorld World;
        /// <summary>Endless re-base (see EndlessWorld): changes exactly when all window heights and platform/gong ids were shifted. The view re-bases its caches when it sees a new value. 0 for campaign levels.</summary>
        public int RebaseCount { get { return World == null ? 0 : World.RebaseCount; } }
        /// <summary>Height (raw Q16.16) removed from the window so far: absolute height = this + window height. Platform / gong ids dropped so far. 0 for campaign levels.</summary>
        public long OriginYRaw { get { return World == null ? 0L : World.OriginYRaw; } }
        public int DroppedPlatforms { get { return World == null ? 0 : World.DroppedPlatforms; } }
        public int DroppedHazards { get { return World == null ? 0 : World.DroppedHazards; } }

        public bool GoalReached { get; private set; }
        /// <summary>The goal became impossible while alive (NoWallBounce after a wall bounce): the run ends, a revive cannot undo it.</summary>
        public bool GoalLost { get; private set; }
        public bool Failed { get { return (State.Dead || GoalLost) && !GoalReached; } }
        public bool Over { get { return GoalReached || State.Dead || GoalLost || TimeUp; } }

        /// <summary>
        /// Time Attack variant of endless (doc 46 section 7): the same endless tower with a 3:00 countdown from the first jump, no revive, the same hero and
        /// powers. The score is the floors reached when the clock ends (or the player falls first). Open from the start.
        /// </summary>
        public readonly bool IsTimeAttack;
        /// <summary>The 3:00 of a Time Attack run are used up (the run ended on the tick the clock hit 10800). Always false outside Time Attack.</summary>
        public bool TimeUp { get; private set; }
        /// <summary>Ticks left of the Time Attack countdown (the full 10800 before the first jump); 0 outside Time Attack.</summary>
        public int TimeAttackTicksLeft { get { return IsTimeAttack ? TimeRecords.TimeAttackTicksLeft(RunTicks) : 0; } }

        /// <summary>
        /// Campaign attempt. <paramref name="attemptSeed"/> picks the layout: 0 = the level's base layout (tests), any other value a different layout of the
        /// same difficulty. The same (level, attemptSeed) always rebuilds the same layout, so a run can be reproduced from <see cref="LayoutInfo"/>.
        /// </summary>
        public RunSession(ContentRepository repo, SimConfig cfg, string levelId, bool stripGems = false, int attemptSeed = 0)
        {
            Cfg = cfg;
            Level = repo.GetLevel(levelId);
            Region = repo.GetRegion(Level.Region);
            AttemptSeed = attemptSeed;
            Spec = LevelSpecFactory.Build(Level, Region, attemptSeed);
            Data = LevelBuilder.Build(cfg, Spec);
            if (stripGems) StripGems(Data);   // owner 2026-10-24: a level that was cleared before shows no gems again
            Sim = new Sim(cfg, Data, Spec);
            State = Sim.NewState();
        }

        /// <summary>Removes the gem pickups (the hidden gem of a FindHiddenGem goal stays: it is the goal, and pays nothing on a replay).</summary>
        static void StripGems(LevelData data)
        {
            var keep = new System.Collections.Generic.List<Pickup>(data.Pickups.Length);
            for (int i = 0; i < data.Pickups.Length; i++) if (data.Pickups[i].Kind != PickupKind.Gem) keep.Add(data.Pickups[i]);
            data.Pickups = keep.ToArray();
        }

        /// <summary>Endless run in a region. <paramref name="seed"/> picks the tower; the same seed always gives the same tower.</summary>
        public RunSession(ContentRepository repo, SimConfig cfg, RegionDef region, int seed, bool timeAttack = false)
        {
            Cfg = cfg;
            Level = null;
            Region = region;
            IsEndless = true;
            IsTimeAttack = timeAttack;
            Seed = seed;
            Spec = LevelSpecFactory.BuildEndless(region, seed, repo.RemoteConfig.Generator.EndlessDifficultyFloors, timeAttack);
            World = new EndlessWorld(cfg, Spec);
            Tower = World.Tower;
            Data = World.Data;
            Sim = World.Sim;
            State = Sim.NewState();
            WorldVersion = World.Version;
        }

        /// <summary>Advances one 1/60 s tick. Does nothing once the run is over.</summary>
        public void Step(int input)
        {
            if (Over) return;
            if ((input & SimInput.PowerMask) != 0) input = TakePowerUnits(input);
            if ((input & SimInput.HeroMask) != 0) input = TakeHeroPowers(input);
            bool wasGroundedForJumps = State.Grounded;
            inputRec.Add(input);   // timed records (doc 46): the input that really reaches the simulation, run-length coded, no allocation per tick
            Sim.Step(ref State, input);
            RecordClock();
            if (wasGroundedForJumps && !State.Grounded && State.Vy.Raw > 0) jumpsMade++;   // region statistics (Main goals): a take-off from a platform is a jump
            if (Hero != null) Hero.Tick();   // hero cooldowns run in simulation ticks
            if (State.Combo >= 3 && lastCombo < 3) comboChains++;   // reward statistics: a chain reached 3 landings
            lastCombo = State.Combo;
            if (State.BestCombo > bestComboSeen) bestComboSeen = State.BestCombo;
            if (IsTimeAttack && !TimeUp && TimeRecords.TimeAttackExpired(RunTicks)) TimeUp = true;   // the clock is the simulation: exactly 10800 ticks after the first jump
            if (IsEndless)
            {
                // Keep the tower 60 rows above the player. Growth happens once per 40 rows (about every 15 s), never per frame.
                // (EndlessWorld.Grow also re-bases the heights and shifts State when the window top passes 2500 u, see EndlessWorld.)
                if (World.Grow(ref State))
                {
                    Data = World.Data;
                    Sim = World.Sim;
                    WorldVersion = World.Version;
                }
                return;
            }
            if (GoalEvaluator.IsComplete(Spec, State)) GoalReached = true;
            else if (GoalEvaluator.IsFailed(Spec, State)) GoalLost = true;
        }

        /// <summary>Pays the revive price from the wallet and puts the player back on the highest landed platform.</summary>
        public bool TryRevive(ProgressService progress)
        {
            if (progress == null || !State.Dead || GoalReached || !CanRevive) return false;
            int cost = NextReviveCost;
            if (progress.Gems < cost) return false;
            Sim.Revive(ref State);
            // The wallet is charged only after the revive happened. The run's own gems stay in the state (a revive keeps them).
            if (!progress.TrySpendGems(cost)) { State.RevivesUsed--; return false; }
            Revive.RecordGem();
            return true;
        }

        /// <summary>The rewarded ad was watched to the end: remember it so the free revive can be granted again if it did not go through.</summary>
        public void MarkAdWatched()
        {
            if (AdReviveOffered) AdRewardPending = true;
        }

        /// <summary>Free revive after a completed rewarded ad (first revive only, once per run). Costs nothing; keeps gems and effects.</summary>
        public bool TryReviveFree()
        {
            if (!AdRewardPending || !State.Dead || GoalReached || !AdReviveOffered) return false;
            Sim.Revive(ref State);
            AdRewardPending = false;
            Revive.RecordAd();
            return true;
        }

        // ---- power-ups (docs/15, reworked 2026-10-31): every owned type is available (no loadout, no carry cap, no stock cap).
        // The owned units are counted here, outside the simulation state; a tap whose type has no unit left is dropped before it reaches the
        // simulation, every accepted tap takes one unit. Used units are taken from the saved stock when the run pauses or ends. ----

        readonly int[] stock = new int[PowerUps.MaxType + 1];
        readonly int[] settledStock = new int[PowerUps.MaxType + 1];
        readonly bool[] ownedAtStart = new bool[PowerUps.MaxType + 1];

        /// <summary>Puts the owned units into the fresh run (units[i] for PowerUp id i, from ProgressService.BuildRunStock).</summary>
        public void ApplyStock(int[] units)
        {
            if (units == null) return;
            for (int i = 1; i <= PowerUps.MaxType; i++)
            {
                int n = i < units.Length && PowerUps.IsImplemented((PowerUp)i) ? System.Math.Max(0, units[i]) : 0;
                stock[i] = n;
                settledStock[i] = n;
                ownedAtStart[i] = n > 0;
            }
        }

        /// <summary>Units of the type still owned in this run.</summary>
        public int PowerStock(PowerUp type)
        {
            int i = (int)type;
            return i > 0 && i < stock.Length ? stock[i] : 0;
        }

        /// <summary>True when the player owned this type when the run started (the HUD shows one button per owned type and keeps it while the run lasts).</summary>
        public bool OwnedAtStart(PowerUp type)
        {
            int i = (int)type;
            return i > 0 && i < ownedAtStart.Length && ownedAtStart[i];
        }

        /// <summary>True when the player owned at least one power-up when the run started.</summary>
        public bool HasPowerUps
        {
            get
            {
                for (int i = 1; i <= PowerUps.MaxType; i++) if (ownedAtStart[i]) return true;
                return false;
            }
        }

        /// <summary>
        /// Drops the activation bits of types with no unit left (or that cannot start now) and takes one unit for every accepted bit.
        /// </summary>
        int TakePowerUnits(int input)
        {
            int result = input & ~SimInput.PowerMask;
            for (int i = (int)PowerUp.Flight; i <= (int)PowerUp.SlowTime; i++)
            {
                PowerUp p = (PowerUp)i;
                int bit = PowerUps.InputBit(p);
                if ((input & bit) == 0 || !PowerUps.CanActivate(in State, p)) continue;
                // USING ORDER (Bug List 6): FREE first, then bought. The merged button of a type uses the free hero power when it is ready and its effect is not running
                // (no unit taken, its rarity cooldown starts); otherwise it takes one bought unit (a tap on a running effect stacks a bought unit). Same effect and duration either way.
                int heroIdx = Hero != null ? Hero.IndexOf(p) : -1;
                bool free = heroIdx >= 0 && Hero.IsReady(heroIdx) && PowerUps.CanActivateHero(in State, p);
                if (!free && stock[i] <= 0) continue;
                // Flight and Rocket exclude each other inside one tick as well (the lower id wins, like the simulation)
                if (p == PowerUp.Rocket && (result & PowerUps.InputBit(PowerUp.Flight)) != 0) continue;
                if (free) Hero.TryUse(heroIdx);
                else { stock[i]--; powerUpsUsed++; }
                result |= bit;
            }
            return result;
        }

        // ---- hero powers (owner 2026-11-02, docs/34): the chosen hero's Powers are free buttons, READY at the start of a run, each with its own cooldown.
        // A use is a normal power-up activation (Bug List 6: the SAME effect, strength and duration as the bought one, no 60 percent) that takes NO inventory unit.
        // The HUD merges the free power into the button of its type (PowerUpBar); a button press goes through TakePowerUnits (free first, then bought). ----

        /// <summary>The hero's powers and their cooldowns, or null when the hero has none (Common heroes).</summary>
        public HeroPowerCooldown Hero { get; private set; }

        /// <summary>Gives the run the powers of the chosen hero. Null or a hero without powers = no hero buttons. Call before the HUD is built.</summary>
        public void ApplyHeroPowers(CharacterSpec hero)
        {
            Hero = null;
            if (hero == null || hero.Powers == null || hero.Powers.Length == 0) return;
            int r = (int)hero.Rarity;
            var h = new HeroPowerCooldown(hero.Powers, r >= 0 && r < CharacterDefs.CooldownSeconds.Length ? CharacterDefs.CooldownSeconds[r] : 0);
            if (h.Count > 0) Hero = h;
        }

        /// <summary>
        /// Keeps the hero activation bits that may start now (the hero has the power, its cooldown is over, the effect is not running and can start) and starts
        /// the cooldown of each accepted one. Every other hero bit is dropped, so nothing outside the hero's own powers ever reaches the simulation for free.
        /// </summary>
        int TakeHeroPowers(int input)
        {
            int result = input & ~SimInput.HeroMask;
            if (Hero == null) return result;
            for (int k = 0; k < Hero.Count; k++)
            {
                PowerUp p = Hero.Type(k);
                int bit = PowerUps.HeroInputBit(p);
                if ((input & bit) == 0 || !Hero.IsReady(k) || !PowerUps.CanActivateHero(in State, p)) continue;
                if ((result & PowerUps.InputBit(p)) != 0) continue;   // a bought tap of the same type in this tick runs first (it would make this one a no-op)
                // Flight and Rocket exclude each other inside one tick as well (the lower id wins, like the simulation)
                if (p == PowerUp.Rocket && ((result & (PowerUps.InputBit(PowerUp.Flight) | PowerUps.HeroInputBit(PowerUp.Flight))) != 0)) continue;
                if (p == PowerUp.Flight && (result & PowerUps.InputBit(PowerUp.Rocket)) != 0) continue;
                Hero.TryUse(k);
                result |= PowerUps.InputBit(p);   // Bug List 6: a free hero power is the bought effect at full strength and duration (no 60 percent), only without a unit
            }
            return result;
        }

        /// <summary>
        /// Takes the units used since the last call out of the owned stock and saves (used units are gone, also on a failed run; unused ones
        /// stay in the stock). Called when the run pauses, ends or is left, never per tick (a save is a file write).
        /// </summary>
        public int SettlePowerUps(ProgressService progress)
        {
            int used = 0;
            for (int i = 1; i <= PowerUps.MaxType; i++)
            {
                if (stock[i] >= settledStock[i]) continue;
                int n = settledStock[i] - stock[i];
                settledStock[i] = stock[i];
                if (progress != null) progress.TryConsumePowerUp(PowerUps.IdOf((PowerUp)i), n);
                used += n;
            }
            return used;
        }

        // ---- timed records (doc 46): the clock is the simulation itself, so a pause, an app switch or an interruption can never add time ----

        readonly InputRecorder inputRec = new InputRecorder();
        readonly GrowingInts curveRec = new GrowingInts();
        readonly GrowingInts splitRec = new GrowingInts();

        /// <summary>Gameplay mode of this run, 0..4 (GameSettings.Difficulty); set by the RunController when the run begins.</summary>
        public int Mode;
        /// <summary>Id of the hero that plays this run (verification only); set by the RunController.</summary>
        public string HeroId = "";

        /// <summary>Run clock in ticks: from the first jump (SimState.StartTick, set by the simulation) to now, or to the end once the run is over. 0 before the first jump.</summary>
        public int RunTicks { get { return State.StartTick < 0 ? 0 : System.Math.Max(0, State.Tick - State.StartTick); } }

        /// <summary>Floors reached so far: the highest platform row landed on (never decreases).</summary>
        public int RunFloors { get { return State.Row; } }

        /// <summary>True once a revive was used in this run: it can never set a best Time or Pace.</summary>
        public bool RunRevived { get { return State.RevivesUsed > 0; } }

        /// <summary>Per tick after the simulation step: the floors at the end of every second of the clock and (endless) the time to reach floor 100, 200 ... Allocates only when its lists double.</summary>
        void RecordClock()
        {
            int start = State.StartTick;
            if (start < 0) return;
            int elapsed = State.Tick - start;
            if (elapsed >= (curveRec.Count + 1) * TimeRecords.TicksPerSecond) curveRec.Add(State.Row);
            if (IsEndless)
                while ((splitRec.Count + 1) * TimeRecords.SplitFloors <= State.Row) splitRec.Add(elapsed);
        }

        /// <summary>
        /// The finished (or running) run as a record for ProgressService.RecordRun: floors, clock ticks, seeds, hero, mode, version, revive flag, the coded
        /// inputs, the floor-by-time curve and the endless splits. Call once at the end of the run (it allocates the arrays).
        /// </summary>
        public RunRecord BuildRunRecord(string gameVersion)
        {
            return new RunRecord
            {
                Kind = IsTimeAttack ? RunKind.TimeAttack : IsEndless ? RunKind.Endless : RunKind.Level,
                LevelId = Level != null ? Level.Id : null,
                RegionId = Region.Id,
                Floors = State.Row,
                Ticks = RunTicks,
                LayoutSeed = IsEndless ? Seed : Spec.Seed,
                AttemptSeed = AttemptSeed,
                HeroId = HeroId ?? "",
                Mode = Mode,
                GameVersion = gameVersion ?? "",
                Revived = RunRevived,
                Inputs = inputRec.ToBytes(),
                Curve = curveRec.ToArray(),
                Splits = splitRec.ToArray(),
                Coins = State.Coins,
                Gems = State.Gems
            };
        }

        public RunOutcome BuildOutcome()
        {
            return new RunOutcome
            {
                Completed = GoalReached,
                StarMask = GoalReached ? StarEvaluator.Earned(Spec, State, State.RevivesUsed, State.Tick) : 0,
                Score = State.Score,
                CoinsCollected = State.Coins,
                GemsCollected = State.Gems,
                HiddenGems = State.HiddenGems,
                GemPickupsTotal = CountGemPickups(false),
                GemPickupsTaken = CountGemPickups(true),
                ComboChains = comboChains - reportedChains,
                BestCombo = bestComboSeen,
                PowerUpsUsed = powerUpsUsed - reportedPowerUps,
                Region = Region.Index,
                Jumps = jumpsMade - reportedJumps
            };
        }

        // ---- reward statistics (docs/26 section 4): combo chains reaching 3 landings, best combo, power-up units used ----

        int lastCombo, comboChains, bestComboSeen, powerUpsUsed, reportedChains, reportedPowerUps;
        int jumpsMade, reportedJumps;   // region statistics (Main goals, docs/39)

        /// <summary>Marks the combo chains and power-up units counted so far as reported, so the next outcome only carries new ones (a revive keeps the session).</summary>
        public void MarkStatsReported()
        {
            reportedChains = comboChains;
            reportedJumps = jumpsMade;
            reportedPowerUps = powerUpsUsed;
        }

        /// <summary>Gem pickups the level holds (or, with <paramref name="takenOnly"/>, the ones collected). Called at the end of a run only.</summary>
        int CountGemPickups(bool takenOnly)
        {
            int n = 0;
            if (Data == null || Data.Pickups == null) return 0;
            for (int i = 0; i < Data.Pickups.Length; i++)
            {
                if (Data.Pickups[i].Kind != PickupKind.Gem) continue;
                if (takenOnly && (State.PickupTaken == null || i >= State.PickupTaken.Length || !State.PickupTaken[i])) continue;
                n++;
            }
            return n;
        }

        /// <summary>The extra rule line shown under the goal before the level starts ("Don't touch the walls."), or null when the level has no such rule.</summary>
        public static string GoalRule(LevelSpec spec)
        {
            return !spec.Endless && spec.Goal.Type == GoalType.NoWallBounce ? "Don't touch the walls." : null;
        }

        /// <summary>Short goal text for the HUD and the level info popup.</summary>
        public static string DescribeGoal(LevelSpec spec)
        {
            if (spec.Endless) return "Climb as high as you can";
            GoalSpec g = spec.Goal;
            switch (g.Type)
            {
                case GoalType.ReachFloor: return GemLevels.IsGemLevel(spec.LevelIndex, spec.Goal.Type) || spec.IsTopLevel ? "Reach floor " + g.Value + " and grab the gems" : "Reach floor " + g.Value;
                case GoalType.CollectCoins: return "Collect " + g.Count + " coins";
                case GoalType.ComboChain: return "Chain " + g.Count + " jump" + (g.Count == 1 ? "" : "s") + " of " + g.Value + "+ floors";
                case GoalType.NoWallBounce: return "Reach floor " + g.Value;
                case GoalType.FindHiddenGem: return "Find the hidden gem";
                case GoalType.Speedrun: return "Reach floor " + g.Value + " in " + g.TimeSec + " s";
                case GoalType.SurviveWave: return "Survive the wave and reach the top";
                case GoalType.BossChase: return "Outclimb the chase: floor " + g.Value;
                default: return "Reach the top";
            }
        }
    }
}
