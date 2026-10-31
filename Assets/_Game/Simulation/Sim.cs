namespace BoingUpSkyTower.Simulation
{
    /// <summary>Input bit flags for one tick. Jump is an edge: set it only on the tick of the tap.</summary>
    public static class SimInput
    {
        public const int Left = 1;
        public const int Right = 2;
        public const int Jump = 4;
        /// <summary>
        /// Power-up activation edges (docs/15 FR-REGION-3, reworked 2026-10-31): one bit per power-up TYPE, set only on the tick of the tap.
        /// Bit = PowerFirst shifted left by (type id - 1): Flight 8, Rocket 16, Double Jump 32, Wall Stick 64, Shield 128, Magnet 256, Slow Time 512
        /// (use PowerUps.InputBit). There are no loadout slots any more: every owned type can be used. Bits above 4 were unused before,
        /// so old replays and the bot (which never set them) are unaffected.
        /// </summary>
        public const int PowerFirst = 8;
        public const int PowerMask = 8 | 16 | 32 | 64 | 128 | 256 | 512;
    }

    /// <summary>All mutable simulation state. Copy with Clone() (the CrumbleTick and PickupTaken arrays are the only reference members).</summary>
    public struct SimState
    {
        public int Tick;
        public Fx X, Y, Vx, Vy;
        public bool Grounded;
        /// <summary>Platform last stood on (kept while in coyote time and while airborne).</summary>
        public int GroundId;
        public int Coyote;
        public int JumpBuffer;
        public int JumpStartRow;
        /// <summary>Highest platform row landed on.</summary>
        public int Row;
        public int Combo, BestCombo, Score, WallBounces;
        public Fx KillY, MaxY;
        /// <summary>Tick of the first jump of the run, or -1 before it. The screen starts scrolling then (owner, 2026-10-19).</summary>
        public int StartTick;
        public bool Dead;
        /// <summary>Per platform: tick of first landing on a crumble platform, or -1.</summary>
        public int[] CrumbleTick;

        // ---- T3 run tracking (plain value fields except the PickupTaken array) ----
        /// <summary>Coins collected this run (landing rules plus pickups). Not part of Score.</summary>
        public int Coins;
        /// <summary>Bits of the collectible stars taken (bit i = star i) and how many.</summary>
        public int StarMask, Stars;
        public int Gems;
        /// <summary>Hidden gems collected (FindHiddenGem goal). Counted separately from Gems.</summary>
        public int HiddenGems;
        /// <summary>Gong hits taken.</summary>
        public int Hits;
        /// <summary>Remaining ticks of hit/revive invulnerability.</summary>
        public int Invuln;
        public int RevivesUsed;
        /// <summary>Platform id of the highest row landed on (revive point).</summary>
        public int BestGroundId;
        /// <summary>Floors gained by the most recent landing, measured from the platform jumped from.</summary>
        public int LastJumpFloors, BestJumpFloors;
        /// <summary>
        /// Histogram of landing jumps by floors gained: 8 buckets of 8 bits (bucket k-1 = jumps that gained k floors,
        /// the last bucket also counts 8 or more; saturates at 255). Read it with GoalEvaluator.JumpsWithFloors.
        /// </summary>
        public ulong JumpHist;
        /// <summary>Per pickup: collected flag (index = pickup id).</summary>
        public bool[] PickupTaken;

        // ---- Region 1 special platforms (doc 24). Three small fields; per-platform dynamic state lives in CrumbleTick (Snap Daisy: landing tick, Ladybug: boost ticks). ----
        /// <summary>Tick of the latest landing (the view eases the seesaw tilt and the snap daisy countdown from it).</summary>
        public int LandTick;
        /// <summary>Seesaw Leaf under the player: -1 tilted left, +1 tilted right (set at the landing), 0 none.</summary>
        public int TiltDir;
        /// <summary>Times a thorn bush hopped the player away (it never damages or ends the run).</summary>
        public int ThornKnocks;

        // ---- Power-ups. Plain ints for the stacked timers (no maximum, additions saturate at int.MaxValue); the OWNED STOCK lives outside the
        // state (RunSession counts it and only passes an activation bit while units are left), so the bot, which copies SimState on every
        // recursion level, stays small. Read and write the fields through the int properties. ----
        private int pwUsed, flightTicks, rocketTicks, magnetTicks, slowTicks, shieldTicks, wallStickTicks, doubleJumpTicks, rocketTargetRow;
        private Fx rocketTargetY;
        private ushort pwUsedMask, slowFall, wallHold, wallCool;
        private byte shieldSaves;
        private bool assisted, airJumpUsed;

        /// <summary>Units used this run and a bit mask of the types used (bit = PowerUp id).</summary>
        public int PwUsed { readonly get { return pwUsed; } set { pwUsed = value; } }
        public int PwUsedMask { readonly get { return pwUsedMask; } set { pwUsedMask = (ushort)value; } }
        /// <summary>Remaining ticks: Flight, Rocket, slow fall after them, Magnet, Slow Time, Shield, Wall Stick, Double Jump. A tap adds its base time onto the remaining time.</summary>
        public int FlightTicks { readonly get { return flightTicks; } set { flightTicks = value; } }
        public int RocketTicks { readonly get { return rocketTicks; } set { rocketTicks = value; } }
        public int SlowFall { readonly get { return slowFall; } set { slowFall = (ushort)value; } }
        public int MagnetTicks { readonly get { return magnetTicks; } set { magnetTicks = value; } }
        public int SlowTicks { readonly get { return slowTicks; } set { slowTicks = value; } }
        public int ShieldTicks { readonly get { return shieldTicks; } set { shieldTicks = value; } }
        public int WallStickTicks { readonly get { return wallStickTicks; } set { wallStickTicks = value; } }
        /// <summary>Double Jump: ticks left of the window in which one extra air jump per air phase is available.</summary>
        public int DoubleJumpTicks { readonly get { return doubleJumpTicks; } set { doubleJumpTicks = value; } }
        /// <summary>The extra air jump of the current air phase was used (cleared on landing).</summary>
        public bool AirJumpUsed { readonly get { return airJumpUsed; } set { airJumpUsed = value; } }
        /// <summary>Rocket: the row and height the burn is climbing to; every stacked rocket adds SimConfig.RocketFloors rows.</summary>
        public int RocketTargetRow { readonly get { return rocketTargetRow; } set { rocketTargetRow = value; } }
        public Fx RocketTargetY { readonly get { return rocketTargetY; } set { rocketTargetY = value; } }
        /// <summary>Wall Stick: ticks left of the current hold (0 = not holding) and the cooldown before the next hold.</summary>
        public int WallHold { readonly get { return wallHold; } set { wallHold = (ushort)value; } }
        public int WallCool { readonly get { return wallCool; } set { wallCool = (ushort)value; } }
        /// <summary>The current air phase was started by Flight/Rocket: its landing does not count for combos, jump buckets or landing coins.</summary>
        public bool Assisted { readonly get { return assisted; } set { assisted = value; } }
        /// <summary>Kept for old callers; the Shield is a timed block now and does not count saves (always 0).</summary>
        public int ShieldSaves { readonly get { return shieldSaves; } set { shieldSaves = (byte)value; } }

        public SimState Clone()
        {
            SimState c = this;
            if (CrumbleTick != null) c.CrumbleTick = (int[])CrumbleTick.Clone();
            if (PickupTaken != null) c.PickupTaken = (bool[])PickupTaken.Clone();
            return c;
        }
    }

    /// <summary>
    /// Deterministic fixed-step simulation of one run. Stateless apart from the level: all mutable
    /// state lives in SimState so replays, bots and the server can branch and re-simulate cheaply.
    /// Platforms must be sorted by Y ascending (the generator guarantees this); pickups and hazards too.
    /// Step never allocates.
    /// </summary>
    public sealed class Sim
    {
        public readonly SimConfig Cfg;
        public readonly LevelData Level;
        public readonly Platform[] Platforms;
        public readonly Hazard[] Hazards;
        public readonly Pickup[] Pickups;
        public readonly bool Endless;
        /// <summary>Campaign scroll ramp (BossChase): endless step formula clamped to SimConfig.CampaignRampCapPercent.</summary>
        public readonly bool CampaignRamp;
        private readonly Fx scrollPerTick;
        private readonly Fx maxPickupRadius;
        private readonly Fx powerCapY;
        /// <summary>Vertical reach of bobbing platforms around their base Y (zero when the level has no Bee Hive Lift): widens the landing search.</summary>
        private readonly Fx bobMargin;

        /// <param name="difficulty">0..1 level difficulty; scales the scroll pressure.</param>
        public Sim(SimConfig cfg, Platform[] platforms, Fx difficulty, bool endless)
            : this(cfg, LevelData.FromPlatforms(platforms), difficulty, endless, false)
        {
        }

        /// <summary>Campaign constructor: level from LevelBuilder, difficulty and ramp taken from the spec.</summary>
        public Sim(SimConfig cfg, LevelData level, LevelSpec spec)
            : this(cfg, level, spec.EffectiveDifficulty(cfg), false, spec.Ramp, spec.TargetRow())
        {
        }

        public Sim(SimConfig cfg, LevelData level, Fx difficulty, bool endless, bool campaignRamp)
            : this(cfg, level, difficulty, endless, campaignRamp, int.MaxValue)
        {
        }

        /// <param name="targetRow">The row the goal checks (campaign): Flight and Rocket stop PowerFlightRowsBelowTop rows below the lower of it and the top row.</param>
        private Sim(SimConfig cfg, LevelData level, Fx difficulty, bool endless, bool campaignRamp, int targetRow)
        {
            Cfg = cfg;
            Level = level;
            Platforms = level.Platforms;
            Hazards = level.Hazards;
            Pickups = level.Pickups;
            Endless = endless;
            CampaignRamp = campaignRamp;
            scrollPerTick = Fx.Lerp(cfg.ScrollSpeedEasy, cfg.ScrollSpeedHard, difficulty) * cfg.ScrollBasePercent / 100 / SimConfig.TicksPerSecond;
            Fx maxR = Fx.Zero;
            for (int i = 0; i < Pickups.Length; i++) if (Pickups[i].Radius > maxR) maxR = Pickups[i].Radius;
            maxPickupRadius = maxR;
            Fx bob = Fx.Zero;
            for (int i = 0; i < Platforms.Length; i++) if (Platforms[i].Kind == PlatformKind.HiveLift) { bob = cfg.HiveBobAmplitude; break; }
            bobMargin = bob;

            // Flight and Rocket stop PowerFlightRowsBelowTop rows under the top row of a campaign level (never in endless).
            Fx cap = Fx.FromInt(30000);
            if (!endless && Platforms.Length > 0)
            {
                int limitRow = (level.Rows < targetRow ? level.Rows : targetRow) - cfg.PowerFlightRowsBelowTop;
                int idx = Platforms.Length - 1;
                while (idx > 0 && Platforms[idx].Row > limitRow) idx--;
                cap = Platforms[idx].Y;
            }
            powerCapY = cap;
        }

        public SimState NewState()
        {
            Platform start = Platforms[0];
            SimState s = new SimState();
            s.X = start.X;
            s.Y = start.Y;
            s.Grounded = true;
            s.GroundId = 0;
            s.BestGroundId = 0;
            s.Coyote = Cfg.CoyoteTicks;
            s.MaxY = start.Y;
            s.StartTick = -1;
            s.KillY = start.Y - Cfg.StartKillDistance;
            s.CrumbleTick = new int[Platforms.Length];
            for (int i = 0; i < s.CrumbleTick.Length; i++) s.CrumbleTick[i] = -1;
            s.PickupTaken = new bool[Pickups.Length];
            return s;
        }

        /// <summary>
        /// Endless mode: after the tower grew (a new Sim was built from the longer level, same prefix), extends the per-platform and
        /// per-pickup arrays of a state taken from the shorter one. Allocates, so call it once per growth step, never per tick.
        /// </summary>
        public void GrowState(ref SimState s)
        {
            if (s.CrumbleTick == null || s.CrumbleTick.Length < Platforms.Length)
            {
                int[] c = new int[Platforms.Length];
                int have = s.CrumbleTick == null ? 0 : s.CrumbleTick.Length;
                for (int i = 0; i < c.Length; i++) c[i] = i < have ? s.CrumbleTick[i] : -1;
                s.CrumbleTick = c;
            }
            if (s.PickupTaken == null || s.PickupTaken.Length < Pickups.Length)
            {
                bool[] p = new bool[Pickups.Length];
                if (s.PickupTaken != null) System.Array.Copy(s.PickupTaken, p, s.PickupTaken.Length);
                s.PickupTaken = p;
            }
        }

        /// <summary>
        /// Minimal revive: if dead, puts the player back on the highest platform landed on, clears speed,
        /// grants a short invulnerability and counts the revive. UI and payment are the caller's job.
        /// </summary>
        public void Revive(ref SimState s)
        {
            if (!s.Dead) return;
            Platform p = Platforms[s.BestGroundId];
            p.Y = PlatformY(s.BestGroundId, s.Tick);   // a Bee Hive Lift is somewhere on its bob
            s.TiltDir = 0;
            s.Dead = false;
            s.GroundId = s.BestGroundId;
            s.X = PlatformX(s.BestGroundId, s.Tick);
            s.Y = p.Y;
            s.Vx = Fx.Zero;
            s.Vy = Fx.Zero;
            s.Grounded = true;
            s.Coyote = Cfg.CoyoteTicks;
            s.JumpBuffer = 0;
            s.JumpStartRow = p.Row;
            s.MaxY = p.Y;
            s.KillY = p.Y - Cfg.ReviveKillDistance;
            if (s.StartTick < 0) s.StartTick = s.Tick;
            s.Invuln = Cfg.ReviveInvulnTicks;
            s.WallHold = 0;
            s.Assisted = false;
            s.Combo = 0;
            s.RevivesUsed++;
        }

        /// <summary>Position of a hazard at a tick (triangle wave on X, small pendulum rise at the extremes).</summary>
        public void HazardPosition(int id, int tick, out Fx x, out Fx y)
        {
            Hazard h = Hazards[id];
            int half = h.Period / 2;
            int phase = (tick + h.Phase) % h.Period;
            int tri = phase < half ? phase : h.Period - phase;
            Fx sway = Fx.FromRatio(tri, half) * 2 - Fx.One;
            x = h.X + sway * h.Amplitude;
            y = h.Y + Fx.Abs(sway) * h.Amplitude * Cfg.GongArcRisePercent / 100;
        }

        /// <summary>
        /// Centre X of a platform at a tick. A Ladybug Rider is returned on its plain (unboosted) path here; use <see cref="PlatformXAt"/>
        /// (or the overload with the state array) when the player may have been standing on it.
        /// </summary>
        public Fx PlatformX(int id, int tick)
        {
            return PlatformX(id, tick, null);
        }

        /// <summary>Centre X of a platform at a tick; <paramref name="aux"/> is SimState.CrumbleTick (the Ladybug Rider's boost ticks live there), may be null.</summary>
        public Fx PlatformX(int id, int tick, int[] aux)
        {
            Platform p = Platforms[id];
            int shift = 0;
            if (p.Kind == PlatformKind.Ladybug) { if (aux != null && aux[id] > 0) shift = aux[id]; }
            else if (p.Kind != PlatformKind.Moving) return p.X;
            int half = p.Period / 2;
            int phase = (tick + p.Phase + shift) % p.Period;
            int tri = phase < half ? phase : p.Period - phase;
            Fx t = Fx.FromRatio(tri, half);
            return p.X + (t * 2 - Fx.One) * p.Amplitude;
        }

        /// <summary>Centre X of a platform as the run in <paramref name="s"/> sees it at the state's tick (includes the Ladybug boost).</summary>
        public Fx PlatformXAt(in SimState s, int id)
        {
            return PlatformX(id, s.Tick, s.CrumbleTick);
        }

        /// <summary>Surface height of a platform at a tick: the base Y, bobbing for a Bee Hive Lift (triangle wave of HivePeriodTicks, +-HiveBobAmplitude).</summary>
        public Fx PlatformY(int id, int tick)
        {
            Platform p = Platforms[id];
            if (p.Kind != PlatformKind.HiveLift || p.Period <= 0) return p.Y;
            int half = p.Period / 2;
            int phase = (tick + p.Phase) % p.Period;
            int tri = phase < half ? phase : p.Period - phase;
            return p.Y + (Fx.FromRatio(tri, half) * 2 - Fx.One) * Cfg.HiveBobAmplitude;
        }

        /// <summary>Wind Petal: direction (-1 left, +1 right) of the gust at a tick; it flips every WindFlipTicks.</summary>
        public int WindDirection(int id, int tick)
        {
            Platform p = Platforms[id];
            if (p.Kind != PlatformKind.WindPetal) return 0;
            int flips = (tick + p.Phase) / Cfg.WindFlipTicks;
            int d = p.Dir == 0 ? 1 : p.Dir;
            return (flips & 1) == 0 ? d : -d;
        }

        /// <summary>Wind Petal: true during the last WindCueTicks before the gust flips (the arrows start to flip).</summary>
        public bool WindFlipCue(int id, int tick)
        {
            Platform p = Platforms[id];
            if (p.Kind != PlatformKind.WindPetal) return false;
            return (tick + p.Phase) % Cfg.WindFlipTicks >= Cfg.WindFlipTicks - Cfg.WindCueTicks;
        }

        /// <summary>
        /// Snap Daisy phase at a tick: 0 open and idle, 1 counting down to close (SnapCountdown steps 3, 2, 1), 2 closed. Uses SimState.CrumbleTick (landing tick).
        /// </summary>
        public int SnapPhase(int id, int tick, int[] crumbleTick)
        {
            if (Platforms[id].Kind != PlatformKind.SnapDaisy) return 0;
            int landed = crumbleTick[id];
            if (landed < 0) return 0;
            int e = tick - landed;
            if (e < Cfg.SnapCloseTicks) return 1;
            return e < Cfg.SnapCloseTicks + Cfg.SnapReopenTicks ? 2 : 0;
        }

        /// <summary>Snap Daisy countdown step 3, 2, 1 while it is closing (0 otherwise): one tick of the ring every SnapCloseTicks / 3.</summary>
        public int SnapCountdown(int id, int tick, int[] crumbleTick)
        {
            if (SnapPhase(id, tick, crumbleTick) != 1) return 0;
            int e = tick - crumbleTick[id];
            int step = Cfg.SnapCloseTicks / 3;
            if (step < 1) step = 1;
            int k = e / step;
            return 3 - (k > 2 ? 2 : k);
        }

        /// <summary>Seesaw Leaf: tilt of a platform for the run in <paramref name="s"/> (-1 left, +1 right) while the player stands on it, else 0.</summary>
        public int SeesawTilt(in SimState s, int id)
        {
            return s.Grounded && s.GroundId == id && Platforms[id].Kind == PlatformKind.Seesaw ? s.TiltDir : 0;
        }

        /// <summary>Ladybug Rider: true while the player stands on it (it runs LadybugBoostPercent as fast).</summary>
        public bool LadybugBoosted(in SimState s, int id)
        {
            return s.Grounded && s.GroundId == id && Platforms[id].Kind == PlatformKind.Ladybug;
        }

        /// <summary>Thorn Bush: true when x is on the thorny half (beyond ThornDeadZone from the centre, on the side of Platform.Dir).</summary>
        public bool OnThorns(int id, Fx x, int tick)
        {
            Platform p = Platforms[id];
            if (p.Kind != PlatformKind.ThornBush || p.Dir == 0) return false;
            return (x - p.X) * p.Dir > Cfg.ThornDeadZone;
        }

        public bool IsSolid(int id, int tick, int[] crumbleTick)
        {
            Platform p = Platforms[id];
            if (p.Kind == PlatformKind.SnapDaisy) return SnapPhase(id, tick, crumbleTick) != 2;
            if (p.Kind == PlatformKind.Vanishing)
            {
                return (tick + p.Phase) % (Cfg.VanishVisibleTicks + Cfg.VanishHiddenTicks) < Cfg.VanishVisibleTicks;
            }
            if (p.Kind == PlatformKind.Crumble)
            {
                int landed = crumbleTick[id];
                return landed < 0 || tick - landed < Cfg.CrumbleTicks;
            }
            return true;
        }

        /// <summary>True while a vanishing platform is still solid but disappears within VanishWarnTicks (the view blinks it).</summary>
        public bool VanishWarning(int id, int tick)
        {
            Platform p = Platforms[id];
            if (p.Kind != PlatformKind.Vanishing) return false;
            int phase = (tick + p.Phase) % (Cfg.VanishVisibleTicks + Cfg.VanishHiddenTicks);
            return phase < Cfg.VanishVisibleTicks && phase >= Cfg.VanishVisibleTicks - Cfg.VanishWarnTicks;
        }

        /// <summary>Index of the first platform whose Y is at or above y.</summary>
        private int LowerBound(Fx y)
        {
            int lo = 0, hi = Platforms.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (Platforms[mid].Y < y) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        private Fx CurrentMaxSpeed(int row)
        {
            Fx max = Cfg.MaxSpeed;
            if (Endless)
            {
                int pct = Cfg.RampPercentAt(row) * Cfg.PlayerSpeedSharePercent / 100;
                max = max * (100 + pct) / 100;
            }
            return max;
        }

        /// <summary>
        /// Scroll speed (units per tick) of the danger line at <paramref name="tick"/> before the Slow Time power-up: base speed, stepped speed-up,
        /// row ramp (endless / boss) and the campaign ceiling. The HUD percent is derived from this so screen and game cannot drift apart.
        /// </summary>
        public Fx ScrollPerTickAt(in SimState s, int tick)
        {
            Fx scroll = scrollPerTick;
            int steps = (tick - s.StartTick) / (Cfg.ScrollStepSeconds * SimConfig.TicksPerSecond);
            if (steps > Cfg.ScrollStepMaxSteps) steps = Cfg.ScrollStepMaxSteps;
            int stepPct = CampaignRamp ? 0 : steps * Cfg.ScrollStepPercent;   // boss levels already ramp with the row
            if (stepPct > Cfg.ScrollStepCapPercent) stepPct = Cfg.ScrollStepCapPercent;
            scroll = scroll * (100 + stepPct) / 100;
            if (Endless)
            {
                scroll = scroll * (100 + Cfg.RampPercentAt(s.Row)) / 100;
            }
            else if (CampaignRamp)
            {
                int pct = Cfg.RampPercentAt(s.Row);
                if (pct > Cfg.CampaignRampCapPercent) pct = Cfg.CampaignRampCapPercent;
                scroll = scroll * (100 + pct) / 100;
            }
            if (!Endless)
            {
                Fx ceiling = Cfg.ScrollMaxSpeed / SimConfig.TicksPerSecond;
                if (scroll > ceiling) scroll = ceiling;
            }
            return scroll;
        }

        /// <summary>The scroll speed of the run's starting speed (units per second), the 0 percent reference of the HUD.</summary>
        public float BaseScrollPerSecond { get { return scrollPerTick.Raw * (float)SimConfig.TicksPerSecond / Fx.OneRaw; } }

        /// <summary>
        /// Speed-up shown to the player: (current scroll speed / starting scroll speed - 1) in whole percent, rounded.
        /// Slow Time is a temporary power-up and is not part of it. 0 before the first jump.
        /// </summary>
        public int ScrollSpeedUpPercent(in SimState s)
        {
            if (s.StartTick < 0 || scrollPerTick.Raw <= 0) return 0;
            long cur = ScrollPerTickAt(in s, s.Tick).Raw;
            long bas = scrollPerTick.Raw;
            return (int)((cur * 100 + bas / 2) / bas) - 100;
        }

        public void Step(ref SimState s, int input)
        {
            if (s.Dead) return;
            int tick = s.Tick + 1;
            int dir = ((input & SimInput.Right) != 0 ? 1 : 0) - ((input & SimInput.Left) != 0 ? 1 : 0);
            Fx maxSpeed = CurrentMaxSpeed(s.Row);

            // --- power-ups: activation edges, then the effect flags of this tick ---
            if ((input & SimInput.PowerMask) != 0) ActivatePowerUps(ref s, input, tick);
            bool rocket = s.RocketTicks > 0;
            bool flying = rocket || s.FlightTicks > 0;
            if (s.Grounded) s.AirJumpUsed = false;
            if (flying)
            {
                s.Grounded = false;
                s.Coyote = 0;
                s.Assisted = true;
                s.WallHold = 0;
                s.AirJumpUsed = false;
            }
            if (rocket) dir = 0;
            // Wall Stick: steering away from the wall lets go (jumping is the wall jump, below).
            if (s.WallHold > 0 && dir != 0 && dir == WallAwayDir(s))
            {
                s.WallHold = 0;
                s.WallCool = Cfg.WallStickCooldownTicks;
            }
            bool holding = s.WallHold > 0;

            // --- horizontal acceleration ---
            PlatformKind surface = Platforms[s.GroundId].Kind;
            Fx accel = Cfg.Accel;
            Fx friction = Cfg.Friction;
            if (!s.Grounded)
            {
                accel = accel * Cfg.AirControl;
            }
            else if (surface == PlatformKind.Slippery)
            {
                accel = accel * Cfg.SlipperyAccel;
                friction = friction * Cfg.SlipperyFriction;
            }
            if (dir != 0)
            {
                Fx a = accel;
                if (s.Vx.Sign() != 0 && s.Vx.Sign() != dir) a = a * 2;
                s.Vx = s.Vx + a * dir;
            }
            else if (s.Grounded)
            {
                if (s.Vx > friction) s.Vx = s.Vx - friction;
                else if (s.Vx < -friction) s.Vx = s.Vx + friction;
                else s.Vx = Fx.Zero;
            }
            s.Vx = Fx.Clamp(s.Vx, -maxSpeed, maxSpeed);
            if (rocket || holding) s.Vx = Fx.Zero;

            // --- jump ---
            if ((input & SimInput.Jump) != 0) s.JumpBuffer = Cfg.JumpBufferTicks;
            if (holding && s.JumpBuffer > 0)
            {
                // wall jump: standing-jump strength, pushed away from the wall
                s.Vy = Cfg.JumpBase;
                s.Vx = Cfg.WallJumpVx * WallAwayDir(s);
                s.WallHold = 0;
                s.WallCool = Cfg.WallStickCooldownTicks;
                s.JumpBuffer = 0;
                holding = false;
            }
            else if (s.JumpBuffer > 0 && (s.Grounded || s.Coyote > 0))
            {
                Fx v0 = Cfg.JumpSpeed(Fx.Abs(s.Vx));
                if (surface == PlatformKind.Sticky) v0 = v0 * Cfg.StickyJumpMultiplier;
                s.Vy = v0;
                s.Grounded = false;
                s.Coyote = 0;
                s.JumpBuffer = 0;
                s.JumpStartRow = Platforms[s.GroundId].Row;
                if (s.StartTick < 0) s.StartTick = tick;
            }
            else if (s.JumpBuffer > 0 && s.DoubleJumpTicks > 0 && !s.AirJumpUsed && !flying && !holding && !s.Grounded && s.Coyote == 0 && s.Vy <= Fx.Zero)
            {
                // Double Jump (timed): while the window runs, every air phase has one extra standing-jump launch in mid-air (no sticky or bounce multiplier)
                s.Vy = Cfg.JumpBase;
                s.AirJumpUsed = true;
                s.JumpBuffer = 0;
            }

            // --- movement ---
            Fx prevY = s.Y;
            Fx prevX = s.X;
            if (s.Grounded)
            {
                if (Platforms[s.GroundId].Kind == PlatformKind.Moving)
                {
                    s.X = s.X + PlatformX(s.GroundId, tick) - PlatformX(s.GroundId, s.Tick);
                }
                else if (Platforms[s.GroundId].Kind >= PlatformKind.WindPetal) CarryOnNewPlatform(ref s, tick);
                s.X = s.X + s.Vx;
            }
            else
            {
                if (flying)
                {
                    if (rocket)
                    {
                        // auto-climb: close the gap to the target row evenly over the remaining burn ticks (arrives exactly on the last tick)
                        Fx gap = s.RocketTargetY - s.Y;
                        s.Vy = gap > Fx.Zero ? gap / (s.RocketTicks > 0 ? s.RocketTicks : 1) : Fx.Zero;
                    }
                    else
                    {
                        s.Vy = Cfg.FlightRise;
                    }
                }
                else if (holding)
                {
                    s.Vy = Fx.Zero;
                }
                else if (s.SlowFall > 0)
                {
                    s.Vy = s.Vy - Cfg.Gravity * Cfg.SlowFallGravityPercent / 100;
                    if (s.Vy < -Cfg.SlowFallMaxSpeed) s.Vy = -Cfg.SlowFallMaxSpeed;
                }
                else
                {
                    s.Vy = s.Vy - Cfg.Gravity;
                }
                s.Y = s.Y + s.Vy;
                s.X = s.X + s.Vx;
                if (flying && s.Y >= powerCapY)
                {
                    // never skip the finish: stop under the top rows, then fall slowly
                    s.Y = powerCapY;
                    s.Vy = Fx.Zero;
                    s.SlowFall = rocket ? Cfg.RocketSlowFallTicks : Cfg.FlightGraceTicks;
                    s.RocketTicks = 0;
                    s.FlightTicks = 0;
                    flying = false;
                    rocket = false;
                }
            }

            // --- walls ---
            Fx minX = Cfg.PlayerHalfWidth;
            Fx maxX = Cfg.ScreenWidth - Cfg.PlayerHalfWidth;
            if (s.X < minX)
            {
                s.X = minX;
                if (s.Vx < Fx.Zero)
                {
                    if (CanWallStick(in s, flying)) StartWallHold(ref s);
                    else { s.Vx = Fx.Clamp(-s.Vx * Cfg.WallBoost, -maxSpeed, maxSpeed); if (!s.Grounded) s.WallBounces++; }
                }
            }
            else if (s.X > maxX)
            {
                s.X = maxX;
                if (s.Vx > Fx.Zero)
                {
                    if (CanWallStick(in s, flying)) StartWallHold(ref s);
                    else { s.Vx = Fx.Clamp(-s.Vx * Cfg.WallBoost, -maxSpeed, maxSpeed); if (!s.Grounded) s.WallBounces++; }
                }
            }

            // --- ground / landing ---
            if (s.Grounded)
            {
                Platform g = Platforms[s.GroundId];
                bool solid = IsSolid(s.GroundId, tick, s.CrumbleTick);
                if (g.Kind == PlatformKind.ThornBush && OnThorns(s.GroundId, s.X, tick))
                {
                    KnockFromThorns(ref s, in g);   // stepping onto the thorny half hops the player away like a landing there
                }
                else if (!solid || Fx.Abs(s.X - PlatformX(s.GroundId, tick, s.CrumbleTick)) > g.Width / 2 + Cfg.LandingMargin)
                {
                    s.Grounded = false;
                    s.Vy = Fx.Zero;
                    s.Coyote = Cfg.CoyoteTicks;
                    s.JumpStartRow = g.Row;
                }
            }
            else
            {
                if (s.Coyote > 0) s.Coyote--;
                if (s.Vy < Fx.Zero)
                {
                    int best = -1;
                    Fx bestY = Fx.Zero;
                    for (int i = LowerBound(s.Y - bobMargin); i < Platforms.Length && Platforms[i].Y <= prevY + bobMargin; i++)
                    {
                        Platform p = Platforms[i];
                        Fx surfaceY = p.Y;
                        if (p.Kind == PlatformKind.HiveLift)
                        {
                            // a bobbing surface is crossed when the feet were above it last tick and are at or below it now
                            surfaceY = PlatformY(i, tick);
                            if (surfaceY < s.Y || PlatformY(i, s.Tick) > prevY) continue;
                        }
                        else if (p.Y < s.Y || p.Y > prevY) continue;   // outside the fall of this tick (only possible with the widened window)
                        if (!IsSolid(i, tick, s.CrumbleTick)) continue;
                        if (Fx.Abs(s.X - PlatformX(i, tick, s.CrumbleTick)) > p.Width / 2 + Cfg.LandingMargin) continue;
                        if (best < 0 || surfaceY > bestY) { best = i; bestY = surfaceY; }
                    }
                    if (best >= 0)
                    {
                        if (Platforms[best].Kind == PlatformKind.ThornBush && OnThorns(best, s.X, tick))
                        {
                            Platform tp = Platforms[best];
                            s.Y = tp.Y;
                            KnockFromThorns(ref s, in tp);   // a landing on the thorns does not count as a landing
                        }
                        else Land(ref s, best, tick);
                    }
                }
            }

            // --- pickups and hazards ---
            if (Pickups.Length > 0) CollectPickups(ref s, prevX, prevY + Cfg.PlayerCenterY, s.MagnetTicks > 0 ? Cfg.MagnetExtraRadius : Fx.Zero);
            if (Hazards.Length > 0 && !flying && s.SlowFall == 0 && s.ShieldTicks == 0) HitHazards(ref s, tick);   // Shield, Flight and Rocket: gongs cannot hurt

            // --- scroll pressure and death ---
            if (s.Y > s.MaxY) s.MaxY = s.Y;
            if (s.StartTick >= 0)
            {
                Fx scroll = ScrollPerTickAt(in s, tick);   // one source of truth, also read by the HUD speed cue
                if (s.SlowTicks > 0) scroll = scroll * (100 - Cfg.SlowTimePercent) / 100;
                s.KillY = s.KillY + scroll;
            }
            Fx follow = s.MaxY - Cfg.KillDistance;
            if (follow > s.KillY) s.KillY = follow;
            if (s.Y < s.KillY) s.Dead = true;

            if (s.JumpBuffer > 0) s.JumpBuffer--;
            if (s.Invuln > 0) s.Invuln--;
            if (s.PwUsed > 0) TickPowerUps(ref s);
            s.Tick = tick;
        }

        // ------------------------------------------------------------------------------------------
        // Power-ups. Everything below is skipped (or cheap) for a run without a loadout.
        // ------------------------------------------------------------------------------------------

        private void ActivatePowerUps(ref SimState s, int input, int tick)
        {
            // One bit per type (SimInput.PowerFirst << (id - 1)); every tap uses one owned unit and ADDS the full base time onto the remaining time.
            for (int i = (int)PowerUp.Flight; i <= (int)PowerUp.SlowTime; i++)
            {
                PowerUp p = (PowerUp)i;
                if ((input & PowerUps.InputBit(p)) == 0) continue;
                if (!PowerUps.CanActivate(in s, p)) continue;
                s.PwUsed = PowerUps.SatAdd(s.PwUsed, 1);
                s.PwUsedMask |= 1 << i;
                switch (p)
                {
                    case PowerUp.Flight:
                    case PowerUp.Rocket:
                        bool already = p == PowerUp.Flight ? s.FlightTicks > 0 : s.RocketTicks > 0;
                        if (p == PowerUp.Flight)
                        {
                            s.FlightTicks = PowerUps.SatAdd(s.FlightTicks, Cfg.FlightTicks);
                        }
                        else
                        {
                            // Rocket: each tap adds RocketFloors rows and RocketTicks ticks; the climb is measured from the platform stood on last
                            s.RocketTicks = PowerUps.SatAdd(s.RocketTicks, Cfg.RocketTicks);
                            int baseRow = already ? s.RocketTargetRow : Platforms[s.GroundId].Row;
                            s.RocketTargetRow = baseRow + Cfg.RocketFloors;
                            s.RocketTargetY = RowHeight(s.RocketTargetRow);
                        }
                        if (already) break;   // stacking onto a running burn: nothing else changes
                        s.SlowFall = 0;
                        s.Grounded = false;
                        s.Coyote = 0;
                        s.WallHold = 0;
                        s.Assisted = true;
                        s.JumpStartRow = Platforms[s.GroundId].Row;
                        if (p == PowerUp.Rocket) s.Vx = Fx.Zero;
                        if (s.StartTick < 0) s.StartTick = tick;
                        break;
                    case PowerUp.DoubleJump: s.DoubleJumpTicks = PowerUps.SatAdd(s.DoubleJumpTicks, Cfg.DoubleJumpWindowTicks); break;
                    case PowerUp.WallStick: s.WallStickTicks = PowerUps.SatAdd(s.WallStickTicks, Cfg.WallStickWindowTicks); break;
                    case PowerUp.Shield: s.ShieldTicks = PowerUps.SatAdd(s.ShieldTicks, Cfg.ShieldTicks); break;
                    case PowerUp.Magnet: s.MagnetTicks = PowerUps.SatAdd(s.MagnetTicks, Cfg.MagnetTicks); break;
                    case PowerUp.SlowTime: s.SlowTicks = PowerUps.SatAdd(s.SlowTicks, Cfg.SlowTimeTicks); break;
                }
            }
        }

        /// <summary>Height of the first platform of the given row (the last platform when the level has fewer rows), capped by the finish limit for Flight and Rocket.</summary>
        private Fx RowHeight(int row)
        {
            int idx = 0;
            while (idx < Platforms.Length - 1 && Platforms[idx].Row < row) idx++;
            Fx y = Platforms[idx].Y;
            return y > powerCapY ? powerCapY : y;
        }

        /// <summary>Direction (+1 right, -1 left) that leads away from the wall the player is held on.</summary>
        private int WallAwayDir(in SimState s)
        {
            return s.X < Cfg.ScreenWidth / 2 ? 1 : -1;
        }

        private static bool CanWallStick(in SimState s, bool flying)
        {
            return s.WallStickTicks > 0 && s.WallCool == 0 && s.WallHold == 0 && !s.Grounded && !flying;
        }

        private void StartWallHold(ref SimState s)
        {
            s.WallHold = Cfg.WallHoldTicks;
            s.Vx = Fx.Zero;
            s.Vy = Fx.Zero;
            s.WallBounces++;
        }

        /// <summary>End-of-tick countdown of the timed effects (only runs once something was activated).</summary>
        private void TickPowerUps(ref SimState s)
        {
            if (s.SlowFall > 0) s.SlowFall--;
            if (s.FlightTicks > 0 && --s.FlightTicks == 0) { s.SlowFall = Cfg.FlightGraceTicks; s.Vy = Fx.Zero; }
            if (s.RocketTicks > 0 && --s.RocketTicks == 0) { s.SlowFall = Cfg.RocketSlowFallTicks; s.Vy = Fx.Zero; s.RocketTargetRow = 0; }
            if (s.MagnetTicks > 0) s.MagnetTicks--;
            if (s.SlowTicks > 0) s.SlowTicks--;
            if (s.ShieldTicks > 0) s.ShieldTicks--;
            if (s.WallStickTicks > 0) s.WallStickTicks--;
            if (s.WallCool > 0) s.WallCool--;
            if (s.WallHold > 0 && --s.WallHold == 0) s.WallCool = Cfg.WallStickCooldownTicks;
            if (s.DoubleJumpTicks > 0) s.DoubleJumpTicks--;
        }

        private static long Dist2(Fx ax, Fx ay, Fx bx, Fx by)
        {
            long dx = (long)ax.Raw - bx.Raw;
            long dy = (long)ay.Raw - by.Raw;
            return dx * dx + dy * dy;
        }

        /// <summary>
        /// Squared distance from point (px, py) to the segment a-b, in raw Q16.16 units squared. The foot parameter is computed in Q16 integer
        /// math, so the result is exact on every platform.
        /// </summary>
        private static long SegmentDist2(Fx ax, Fx ay, Fx bx, Fx by, Fx px, Fx py)
        {
            long dx = (long)bx.Raw - ax.Raw, dy = (long)by.Raw - ay.Raw;
            long dd = dx * dx + dy * dy;
            if (dd == 0) return Dist2(ax, ay, px, py);
            long dot = ((long)px.Raw - ax.Raw) * dx + ((long)py.Raw - ay.Raw) * dy;
            if (dot <= 0) return Dist2(ax, ay, px, py);
            if (dot >= dd) return Dist2(bx, by, px, py);
            long t = (dot << 16) / dd;   // 0..65536
            long fx = ax.Raw + ((dx * t) >> 16), fy = ay.Raw + ((dy * t) >> 16);
            long ex = fx - px.Raw, ey = fy - py.Raw;
            return ex * ex + ey * ey;
        }

        /// <summary>
        /// Collects every pickup the player centre touched during this tick: the path from the previous tick position
        /// (prevCx, prevCy) to the current one is tested against the pickup circle, so a fast pass cannot jump over a star between ticks.
        /// </summary>
        private void CollectPickups(ref SimState s, Fx prevCx, Fx prevCy, Fx extraRadius)
        {
            Fx cx = s.X;
            Fx cy = s.Y + Cfg.PlayerCenterY;
            int lo = 0, hi = Pickups.Length;
            Fx lowY = Fx.Min(cy, prevCy), highY = Fx.Max(cy, prevCy);
            Fx from = lowY - maxPickupRadius - extraRadius;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (Pickups[mid].Y < from) lo = mid + 1; else hi = mid;
            }
            Fx to = highY + maxPickupRadius + extraRadius;
            for (int i = lo; i < Pickups.Length && Pickups[i].Y <= to; i++)
            {
                if (s.PickupTaken[i]) continue;
                long r = (long)Pickups[i].Radius.Raw + extraRadius.Raw;
                if (SegmentDist2(prevCx, prevCy, cx, cy, Pickups[i].X, Pickups[i].Y) > r * r) continue;
                s.PickupTaken[i] = true;
                switch (Pickups[i].Kind)
                {
                    case PickupKind.Coin: s.Coins++; break;
                    case PickupKind.Star: s.StarMask |= 1 << Pickups[i].Tag; s.Stars++; break;
                    case PickupKind.Gem:
                        {
                            if (Pickups[i].Tag == SimConfig.DoubleGemTag) { s.Gems += Cfg.DoubleGemValue; break; }   // doc 25: a double gem is worth 2
                            int tier = Pickups[i].Tag < 0 ? 0 : (Pickups[i].Tag >= Cfg.GemValues.Length ? Cfg.GemValues.Length - 1 : Pickups[i].Tag);
                            s.Gems += Cfg.GemValues[tier];
                            break;
                        }
                    default: s.HiddenGems += Pickups[i].Tag == 1 ? Cfg.DoubleGemValue : 1; break;   // Tag 1 = a double bonus gem (level 8)
                }
            }
        }

        /// <summary>Largest vertical distance between a hazard anchor and any point the hazard or the player can occupy while overlapping.</summary>
        private static readonly Fx HazardQueryMargin = Fx.FromInt(4);

        private void HitHazards(ref SimState s, int tick)
        {
            if (s.Invuln > 0 || s.ShieldTicks > 0) return;   // Shield: gongs cannot hurt at all (no knockback, no hit) while it runs
            Fx cx = s.X;
            Fx cy = s.Y + Cfg.PlayerCenterY;
            int lo = 0, hi = Hazards.Length;
            Fx from = cy - HazardQueryMargin;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (Hazards[mid].Y < from) lo = mid + 1; else hi = mid;
            }
            Fx to = cy + HazardQueryMargin;
            for (int i = lo; i < Hazards.Length && Hazards[i].Y <= to; i++)
            {
                Fx gx, gy;
                HazardPosition(i, tick, out gx, out gy);
                long r = (long)Hazards[i].Radius.Raw + Cfg.PlayerHitRadius.Raw;
                if (Dist2(cx, cy, gx, gy) > r * r) continue;
                s.Vx = cx >= gx ? Cfg.GongKnockbackVx : -Cfg.GongKnockbackVx;
                if (!s.Grounded) s.Vy = cy >= gy ? Cfg.GongKnockbackVy : -Cfg.GongKnockbackVy;
                s.Invuln = Cfg.GongInvulnTicks;
                s.Hits++;
                return;
            }
        }

        private void Land(ref SimState s, int id, int tick)
        {
            Platform p = Platforms[id];
            s.AirJumpUsed = false;   // Double Jump: every landing (also on a bounce platform) gives the next air phase its extra jump back
            p.Y = PlatformY(id, tick);   // the surface of a Bee Hive Lift is somewhere on its bob
            s.Y = p.Y;
            s.Vy = Fx.Zero;
            s.Grounded = true;
            s.GroundId = id;
            s.Coyote = Cfg.CoyoteTicks;
            s.LandTick = tick;

            int oldRow = s.Row;
            int gained = s.Assisted ? 0 : p.Row - s.JumpStartRow;   // Flight/Rocket landings never count as a multi-floor jump
            s.Assisted = false;
            bool newRow = p.Row > oldRow;
            bool qualifies = gained >= Cfg.ComboMinFloors;
            bool continuesCombo = qualifies && s.Combo > 0;

            if (newRow)
            {
                s.Score += (p.Row - oldRow) * Cfg.FloorPoints;
                s.Row = p.Row;
                s.BestGroundId = id;
            }
            if (qualifies)
            {
                s.Combo++;
                if (s.Combo > s.BestCombo) s.BestCombo = s.Combo;
                s.Score += s.Combo * s.Combo * Cfg.ComboPointsMultiplier;
            }
            else
            {
                s.Combo = 0;
            }

            // Doc 08 section 7 coin rules (campaign only; endless has its own rule, D3).
            if (newRow && !Endless)
            {
                s.Coins += continuesCombo ? Cfg.ComboLandingCoins : Cfg.LandingCoins;
                int newFloors = p.Row - oldRow;
                int extra = (gained < newFloors ? gained : newFloors) - 1;
                if (extra > 0) s.Coins += extra * Cfg.ExtraFloorCoins;
            }

            // Endless (owner 2026-10-24): no gems, but every combo landing pays its combo coins like the campaign (settled with the floor coins at the end).
            if (newRow && Endless && continuesCombo) s.Coins += Cfg.ComboLandingCoins;

            s.LastJumpFloors = gained;
            if (gained > s.BestJumpFloors) s.BestJumpFloors = gained;
            if (gained >= 1)
            {
                int shift = (gained > 8 ? 7 : gained - 1) * 8;
                if (((s.JumpHist >> shift) & 0xFFUL) < 255UL) s.JumpHist += 1UL << shift;
            }

            if (p.Kind == PlatformKind.Crumble && s.CrumbleTick[id] < 0) s.CrumbleTick[id] = tick;
            if (p.Kind == PlatformKind.Bounce || p.Kind == PlatformKind.BounceCap)
            {
                Fx mul = p.Kind == PlatformKind.Bounce ? Cfg.BounceMultiplier : Cfg.BounceCapMultiplier;
                s.Vy = Cfg.JumpSpeed(Fx.Abs(s.Vx)) * mul;
                s.Grounded = false;
                s.Coyote = 0;
                s.JumpStartRow = p.Row;
            }
            if (p.Kind >= PlatformKind.WindPetal) LandOnNewPlatform(ref s, in p, id, tick);
        }

        // ------------------------------------------------------------------------------------------
        // Region 1 special platforms (doc 24). Every effect is deterministic and none of them can end the run by itself.
        // ------------------------------------------------------------------------------------------

        private void LandOnNewPlatform(ref SimState s, in Platform p, int id, int tick)
        {
            switch (p.Kind)
            {
                case PlatformKind.Seesaw:
                    {
                        // tilts toward the side landed on (a landing near the centre tilts the way the player was moving)
                        Fx dx = s.X - p.X;
                        if (Fx.Abs(dx) > Cfg.SeesawDeadZone) s.TiltDir = dx.Sign();
                        else s.TiltDir = s.Vx.Sign() != 0 ? s.Vx.Sign() : 1;
                        break;
                    }
                case PlatformKind.SnapDaisy:
                    {
                        // a new countdown starts unless the daisy is already counting down or shut
                        int landed = s.CrumbleTick[id];
                        if (landed < 0 || tick - landed >= Cfg.SnapCloseTicks + Cfg.SnapReopenTicks) s.CrumbleTick[id] = tick;
                        break;
                    }
                case PlatformKind.Dandelion:
                    {
                        // diagonal launch: x1.3 jump height, sideways in the direction of the arrow
                        s.Vy = Cfg.JumpSpeed(Fx.Abs(s.Vx)) * Cfg.DandelionLaunch;
                        s.Vx = Cfg.DandelionVx * (p.Dir == 0 ? 1 : p.Dir);
                        s.Grounded = false;
                        s.Coyote = 0;
                        s.JumpStartRow = p.Row;
                        break;
                    }
            }
        }

        /// <summary>Per-tick effect of the platform the player stands on: Ladybug boost and carry, Bee Hive bob, Wind Petal gust, Seesaw slide.</summary>
        private void CarryOnNewPlatform(ref SimState s, int tick)
        {
            int id = s.GroundId;
            switch (Platforms[id].Kind)
            {
                case PlatformKind.Ladybug:
                    {
                        Fx before = PlatformX(id, s.Tick, s.CrumbleTick);
                        int extra = Cfg.LadybugBoostPercent - 100;
                        int add = tick * extra / 100 - (tick - 1) * extra / 100;   // 1.5x speed: the path clock gains one tick every second tick
                        if (add > 0) s.CrumbleTick[id] = (s.CrumbleTick[id] > 0 ? s.CrumbleTick[id] : 0) + add;
                        s.X = s.X + PlatformX(id, tick, s.CrumbleTick) - before;
                        break;
                    }
                case PlatformKind.HiveLift:
                    s.Y = PlatformY(id, tick);
                    break;
                case PlatformKind.WindPetal:
                    s.X = s.X + Cfg.WindPush * WindDirection(id, tick);
                    break;
                case PlatformKind.Seesaw:
                    s.X = s.X + Cfg.SeesawSlide * s.TiltDir;
                    break;
            }
        }

        /// <summary>Thorn Bush: hops the player away from the thorny half. Never damages, never counts as a landing, never ends the run.</summary>
        private void KnockFromThorns(ref SimState s, in Platform p)
        {
            s.Grounded = false;
            s.Vx = Cfg.ThornKnockVx * -p.Dir;
            s.Vy = Cfg.ThornKnockVy;
            s.Coyote = 0;
            s.JumpStartRow = p.Row;
            s.ThornKnocks++;
        }
    }
}
