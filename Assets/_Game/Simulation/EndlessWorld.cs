namespace BoingUpSkyTower.Simulation
{
    /// <summary>
    /// The world of a PLAYED endless run (owner 2026-11-05, "Fix the limit now": endless can be played to floor 100000 and beyond).
    /// Owns the growing <see cref="EndlessTower"/> (Stream mode), the current <see cref="Sim"/> and the bookkeeping of the RE-BASE.
    ///
    /// Why a re-base: heights are Q16.16 (about 32000 u) and a floor is up to 4.2 u, so a tower that is never shifted overflows at floor 7000.
    /// The tower therefore keeps only a window of rows and, when its top passes <see cref="RebaseAtU"/> (2500 u, about every 500 floors), drops the rows
    /// far below the player and moves EVERY height down by the same exact amount. This class makes the running <see cref="SimState"/> follow
    /// (<see cref="Sim.ShiftState"/>): Y, KillY, MaxY, the rocket target, the platform ids and the per-platform clocks. The Sim only compares heights with
    /// heights, so a re-base is invisible to the game logic (tests: EndlessRebaseTests, state traces are identical bit for bit).
    ///
    /// What is NOT re-based: floors. Platform.Row, SimState.Row, JumpStartRow, RocketTargetRow, the score, EndlessTower.LevelForFloor and the generator's row index are
    /// ABSOLUTE floor numbers all the time. What is re-based is only the height origin: absolute height = <see cref="OriginYRaw"/> + height in the window (the view
    /// uses it to keep its background and wall patterns continuous). The tower is generated lazily in chunks of 40 rows, 60 rows above the player; nothing of
    /// the 100000 floors exists up front, and the window stays at about 200 to 800 rows.
    /// </summary>
    public sealed class EndlessWorld
    {
        /// <summary>The game re-bases when the top of the stored tower passes this height (u). Window of at most about 900 rows, about 1100 platforms.</summary>
        public const int RebaseAtU = 2500;
        /// <summary>Rows below the player's platform that are never dropped (the player can only be a few rows below the platform he last stood on).</summary>
        public const int SafetyRowsBelowPlayer = 30;

        public readonly SimConfig Cfg;
        public readonly EndlessTower Tower;
        public LevelData Data { get; private set; }
        public Sim Sim { get; private set; }
        /// <summary>Changes whenever the tower grew (Data and Sim were replaced). Equals <see cref="EndlessTower.Version"/>.</summary>
        public int Version { get { return Tower.Version; } }

        // ---- re-base bookkeeping: cumulative since the start of the run, so a consumer that missed a step can still catch up from its own copy ----
        /// <summary>Platforms and gongs dropped from the window so far (a window id is the original generation index minus this).</summary>
        public int DroppedPlatforms { get; private set; }
        public int DroppedHazards { get; private set; }
        /// <summary>Height (raw Q16.16) removed from the window so far: absolute height = OriginYRaw + window height.</summary>
        public long OriginYRaw { get; private set; }
        /// <summary>Number of re-bases applied to the state so far; it changes exactly when a re-base happened.</summary>
        public int RebaseCount { get; private set; }

        public EndlessWorld(SimConfig cfg, LevelSpec spec) : this(cfg, spec, true)
        {
        }

        /// <param name="stream">False builds a flat tower (cap 7000 rows, never re-bases): the reference for the determinism tests.</param>
        public EndlessWorld(SimConfig cfg, LevelSpec spec, bool stream)
        {
            Cfg = cfg;
            Tower = new EndlessTower(cfg, spec);
            Tower.Stream = stream;
            Tower.RebaseAtYRaw = Fx.FromInt(RebaseAtU).Raw;
            Data = Tower.Data;
            Sim = Tower.CreateSim();
        }

        public SimState NewState()
        {
            return Sim.NewState();
        }

        /// <summary>
        /// Keeps the tower 60 rows above the player (once per 40 rows, never per tick). When it grew, Data and Sim are new (same rows, same ids unless a re-base happened)
        /// and <paramref name="s"/> was extended (and shifted on a re-base). True when the world changed.
        /// </summary>
        public bool Grow(ref SimState s)
        {
            if (Tower.Stream)
            {
                // the player's platform and the best one he landed on always survive a re-base
                int g = s.GroundId >= 0 && s.GroundId < Sim.Platforms.Length ? Sim.Platforms[s.GroundId].Row : 0;
                int b = s.BestGroundId >= 0 && s.BestGroundId < Sim.Platforms.Length ? Sim.Platforms[s.BestGroundId].Row : 0;
                Tower.ProtectRow = (g < b ? g : b) - SafetyRowsBelowPlayer;
            }
            if (!Tower.EnsureAhead(s.Row)) return false;
            ApplyGrowth(ref s);
            return true;
        }

        /// <summary>
        /// Dev / test hook: generates the tower up to <paramref name="row"/> + the lookahead BEFORE any state exists (tests start a run in the middle of a tower).
        /// Re-bases that happen on the way are absorbed (no state to shift yet); the caller creates its state from <see cref="Sim"/> afterwards.
        /// </summary>
        public void PrepareTo(int row)
        {
            Tower.GenerateTo(row + EndlessTower.LookaheadRows);
            DroppedPlatforms = Tower.DroppedPlatforms;
            DroppedHazards = Tower.DroppedHazards;
            OriginYRaw = Tower.OriginYRaw;
            Data = Tower.Data;
            Sim = Tower.CreateSim();
        }

        /// <summary>Test hook: forces a re-base now and publishes it (the state is shifted). Stream worlds only.</summary>
        public void RebaseNow(ref SimState s)
        {
            if (!Tower.Stream) return;
            int g = Sim.Platforms[s.GroundId].Row, b = Sim.Platforms[s.BestGroundId].Row;
            Tower.ProtectRow = (g < b ? g : b) - SafetyRowsBelowPlayer;
            Tower.RebaseNow();
            Tower.GenerateTo(Tower.TopRow + 1);   // publishes a snapshot (one more row)
            ApplyGrowth(ref s);
        }

        private void ApplyGrowth(ref SimState s)
        {
            int dropP = Tower.DroppedPlatforms - DroppedPlatforms;
            int dropH = Tower.DroppedHazards - DroppedHazards;
            long dy = Tower.OriginYRaw - OriginYRaw;
            if (dropP != 0 || dy != 0)
            {
                Sim.ShiftState(ref s, dropP, Fx.FromRaw((int)dy));
                DroppedPlatforms = Tower.DroppedPlatforms;
                DroppedHazards = Tower.DroppedHazards;
                OriginYRaw = Tower.OriginYRaw;
                RebaseCount++;
            }
            Data = Tower.Data;
            Sim = Tower.CreateSim();
            Sim.GrowState(ref s);
        }
    }
}
