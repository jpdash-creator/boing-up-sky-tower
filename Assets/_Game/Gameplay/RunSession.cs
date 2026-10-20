using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Simulation;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// One attempt at one level: owns the deterministic Sim and its state, advances it one tick at a time and decides when
    /// the run is over (owner decision: a run ends at goal completion, or when the player falls). Plain C#, no Unity types,
    /// so it is unit-testable and the same code a server could replay.
    /// </summary>
    public sealed class RunSession
    {
        /// <summary>Owner decision 2026-10-19: 2 gems for the first revive of a run, 5 for the second, never more than two.</summary>
        public const int MaxRevives = 2;
        public static int ReviveCostFor(int revivesUsed) { return revivesUsed <= 0 ? 2 : 5; }
        public int NextReviveCost { get { return ReviveCostFor(State.RevivesUsed); } }
        /// <summary>Endless runs have no revive: the result (best floor, coins) is saved the moment the player falls (CUX-8), so a revive would double pay.</summary>
        public bool CanRevive { get { return !IsEndless && State.RevivesUsed < MaxRevives; } }

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
        /// <summary>Endless tower, null for campaign levels.</summary>
        public readonly EndlessTower Tower;
        /// <summary>Changes every time the endless tower grew (the view then extends its world). Always 0 for campaign levels.</summary>
        public int WorldVersion { get; private set; }

        public bool GoalReached { get; private set; }
        public bool Failed { get { return State.Dead && !GoalReached; } }
        public bool Over { get { return GoalReached || State.Dead; } }

        public RunSession(ContentRepository repo, SimConfig cfg, string levelId)
        {
            Cfg = cfg;
            Level = repo.GetLevel(levelId);
            Region = repo.GetRegion(Level.Region);
            Spec = LevelSpecFactory.Build(Level, Region);
            Data = LevelBuilder.Build(cfg, Spec);
            Sim = new Sim(cfg, Data, Spec);
            State = Sim.NewState();
        }

        /// <summary>Endless run in a region. <paramref name="seed"/> picks the tower; the same seed always gives the same tower.</summary>
        public RunSession(ContentRepository repo, SimConfig cfg, RegionDef region, int seed)
        {
            Cfg = cfg;
            Level = null;
            Region = region;
            IsEndless = true;
            Seed = seed;
            Spec = LevelSpecFactory.BuildEndless(region, seed, repo.RemoteConfig.Generator.EndlessDifficultyFloors);
            Tower = new EndlessTower(cfg, Spec);
            Data = Tower.Data;
            Sim = Tower.CreateSim();
            State = Sim.NewState();
            WorldVersion = Tower.Version;
        }

        /// <summary>Advances one 1/60 s tick. Does nothing once the run is over.</summary>
        public void Step(int input)
        {
            if (Over) return;
            Sim.Step(ref State, input);
            if (IsEndless)
            {
                // Keep the tower 60 rows above the player. Growth happens once per 40 rows (about every 15 s), never per frame.
                if (Tower.EnsureAhead(State.Row))
                {
                    Data = Tower.Data;
                    Sim = Tower.CreateSim();
                    Sim.GrowState(ref State);
                    WorldVersion = Tower.Version;
                }
                return;
            }
            if (GoalEvaluator.IsComplete(Spec, State)) GoalReached = true;
        }

        /// <summary>Pays the revive price from the wallet and puts the player back on the highest landed platform.</summary>
        public bool TryRevive(ProgressService progress)
        {
            if (!State.Dead || GoalReached || !CanRevive) return false;
            if (!progress.TrySpendGems(NextReviveCost)) return false;
            Sim.Revive(ref State);
            return true;
        }

        // ---- power-ups (docs/15): the loadout is put into the state; used units are taken from the stock when the run pauses or ends ----

        readonly int[] settledUnits = new int[PowerUps.SlotCount];

        /// <summary>Puts the player's loadout (units already limited by stock and carry cap) into the fresh run state.</summary>
        public void ApplyLoadout(LoadoutSlot[] slots)
        {
            if (slots == null || slots.Length < PowerUps.SlotCount) return;
            Sim.SetLoadout(ref State, slots[0].Type, slots[0].Units, slots[1].Type, slots[1].Units, slots[2].Type, slots[2].Units);
            for (int i = 0; i < settledUnits.Length; i++) settledUnits[i] = State.SlotUnits(i);
        }

        /// <summary>True when the loadout holds at least one power-up (the HUD then shows the buttons).</summary>
        public bool HasLoadout
        {
            get
            {
                for (int i = 0; i < PowerUps.SlotCount; i++) if (State.SlotType(i) != PowerUp.None) return true;
                return false;
            }
        }

        /// <summary>
        /// Takes the units used since the last call out of the owned stock and saves (used units are gone, also on a failed run; unused ones
        /// stay in the stock). Called when the run pauses, ends or is left, never per tick (a save is a file write).
        /// </summary>
        public int SettlePowerUps(ProgressService progress)
        {
            int used = 0;
            for (int i = 0; i < settledUnits.Length; i++)
            {
                int now = State.SlotUnits(i);
                if (now >= settledUnits[i]) continue;
                int n = settledUnits[i] - now;
                settledUnits[i] = now;
                if (progress != null) progress.TryConsumePowerUp(PowerUps.IdOf(State.SlotType(i)), n);
                used += n;
            }
            return used;
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
                HiddenGems = State.HiddenGems
            };
        }

        /// <summary>Short goal text for the HUD and the level info popup.</summary>
        public static string DescribeGoal(LevelSpec spec)
        {
            if (spec.Endless) return "Climb as high as you can";
            GoalSpec g = spec.Goal;
            switch (g.Type)
            {
                case GoalType.ReachFloor: return "Reach floor " + g.Value;
                case GoalType.CollectCoins: return "Collect " + g.Count + " coins";
                case GoalType.ComboChain: return "Chain " + g.Count + " jump" + (g.Count == 1 ? "" : "s") + " of " + g.Value + "+ floors";
                case GoalType.NoWallBounce: return "Reach floor " + g.Value + " without a wall bounce";
                case GoalType.FindHiddenGem: return "Find the hidden gem";
                case GoalType.Speedrun: return "Reach floor " + g.Value + " in " + g.TimeSec + " s";
                case GoalType.SurviveWave: return "Survive the wave and reach the top";
                case GoalType.BossChase: return "Outclimb the chase: floor " + g.Value;
                default: return "Reach the top";
            }
        }

        public static string DescribeStar(StarSpec star)
        {
            switch (star.Kind)
            {
                case StarSpec.KindClear: return "Clear the level";
                case StarSpec.KindCoins: return "Collect " + star.Value + " coins";
                case StarSpec.KindNoRevive: return "Do not revive";
                case StarSpec.KindTime: return "Finish in " + star.Value + " s";
                default: return "";
            }
        }
    }
}
