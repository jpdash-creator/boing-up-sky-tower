namespace FluffyClimbers.Simulation
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
        /// <summary>
        /// HERO power activation edges (owner 2026-11-02): the same seven types, one bit each, 1024 shifted left by (type id - 1) (use PowerUps.HeroInputBit).
        /// A hero power is a normal activation of the type that costs no inventory unit and lasts SimConfig.HeroPowerDurationPercent (60) percent of the bought one.
        /// Runs without a hero power never set these bits, so bot proofs, replays and golden files are unaffected.
        /// </summary>
        public const int HeroFirst = 1024;
        public const int HeroMask = 1024 | 2048 | 4096 | 8192 | 16384 | 32768 | 65536;
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

        // ---- Region 2 special platforms (doc 30). Per-platform dynamic state lives in CrumbleTick (Spore Pop: landing tick, Sprout Lift: rise clock). ----
        /// <summary>Dizzy Spores: ticks left in which left/right input is ignored (cleared by every landing).</summary>
        public int SteerLock;
        /// <summary>Puff Float: ticks left of the slow fall after a jump from it (cleared by every landing).</summary>
        public int FloatTicks;
        /// <summary>Times a Spore Pop flung the player (it never damages or ends the run).</summary>
        public int SporePops;

        // ---- Region 3 special platforms (doc 30 section 4). Per-platform dynamic state lives in CrumbleTick (Sinking Raft: sink clock, same coding as the Sprout Lift rise clock). ----
        /// <summary>Kelp Tangle: ticks left in which a jump is not possible (the jump press is kept in JumpBuffer; cleared by every landing).</summary>
        public int JumpLock;
        /// <summary>Times a Pufferfish Pad hopped the player away (it never damages or ends the run).</summary>
        public int PufferHops;

        // ---- Region 4 special platforms (doc 30 section 5). Per-platform dynamic state lives in CrumbleTick (Jelly Wobble: landing tick). ----
        /// <summary>Floss Glide: ticks left of the doubled air steering after a jump from it (cleared by every landing).</summary>
        public int GlideTicks;

        // ---- Region 5 special platforms (doc 30 section 15). Per-platform dynamic state lives in CrumbleTick (Lantern Lift: rise clock, same coding as the Sprout Lift). ----
        /// <summary>Koi Stone: ticks the player has stood still on it (reset by any movement and every landing); KoiStillTicks charges the next jump.</summary>
        public int KoiStill;
        /// <summary>Koi Stone: ticks left of the full air steering after a charged jump (cleared by every landing).</summary>
        public int KoiAir;
        /// <summary>Times a Mallet Smash or Rolling Log bumped the player (they never damage or end the run).</summary>
        public int Bumps;

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
        /// <summary>Endless: base scroll per tick of campaign level N (index 1..15), the speed of that level (EndlessTower.LevelForFloor); null otherwise.</summary>
        private readonly Fx[] endlessScroll;
        private readonly int endlessLevels;
        private readonly bool endlessTimeAttack;
        private readonly Fx maxPickupRadius;
        private readonly Fx powerCapY;
        /// <summary>Vertical reach of bobbing platforms around their base Y (zero when the level has no Bee Hive Lift): widens the landing search.</summary>
        private readonly Fx bobMargin;
        /// <summary>True when the level has a Fizz Vent or Pinwheel Gust (the air zones are only scanned then).</summary>
        private readonly bool hasAirZones;
        /// <summary>Largest height of an air zone above its platform (scan window below the player).</summary>
        private readonly Fx zoneReach;

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
            if (endless)
            {
                // Endless cycle (owner 2026-11-05): the base speed follows the level of the floor, like a campaign level of that number. Level 1 is the run starting
                // difficulty (the HUD percent is measured against it); the region offset is recovered from it (spec.Difficulty = Effective(0.05, offset)).
                int topLevel = level.EndlessLevels;
                endlessLevels = topLevel;
                endlessTimeAttack = level.EndlessTimeAttack;
                endlessScroll = new Fx[EndlessTower.MaxLevel + 1];
                Fx offset = level.HasEndlessOffset ? level.EndlessOffset : EndlessTower.OffsetFromStartDifficulty(difficulty);
                for (int n = 1; n <= topLevel; n++)
                {
                    Fx dn = n == 1 ? difficulty : LevelSpec.DifficultyForLevel(cfg, n, offset, topLevel);
                    endlessScroll[n] = Fx.Lerp(cfg.ScrollSpeedEasy, cfg.ScrollSpeedHard, dn) * cfg.ScrollBasePercent / 100 / SimConfig.TicksPerSecond;
                }
            }
            Fx maxR = Fx.Zero;
            for (int i = 0; i < Pickups.Length; i++) if (Pickups[i].Radius > maxR) maxR = Pickups[i].Radius;
            maxPickupRadius = maxR;
            Fx bob = Fx.Zero;
            bool zones = false;
            Fx reach = Fx.Zero;
            for (int i = 0; i < Platforms.Length; i++)
            {
                PlatformKind bk = Platforms[i].Kind;
                if (bk == PlatformKind.FizzVent) { zones = true; if (cfg.VentHeight > reach) reach = cfg.VentHeight; }
                else if (bk == PlatformKind.PinwheelGust) { zones = true; if (cfg.PinwheelHeight > reach) reach = cfg.PinwheelHeight; }
                if (bk == PlatformKind.HiveLift && cfg.HiveBobAmplitude > bob) bob = cfg.HiveBobAmplitude;
                else if (bk == PlatformKind.SproutLift && cfg.SproutRise > bob) bob = cfg.SproutRise;
                else if (bk == PlatformKind.LanternLift && cfg.LanternRise > bob) bob = cfg.LanternRise;
                else if (bk == PlatformKind.SwingPlank && cfg.PlankRise > bob) bob = cfg.PlankRise;
                else if (bk == PlatformKind.OrbitCap && cfg.OrbitAmplitudeY > bob) bob = cfg.OrbitAmplitudeY;
                else if (bk == PlatformKind.SinkingRaft && cfg.RaftSinkDepth > bob) bob = cfg.RaftSinkDepth;
                else if (bk == PlatformKind.SandcastleStep && Platforms[i].Amplitude > bob) bob = Platforms[i].Amplitude;
            }
            bobMargin = bob;
            hasAirZones = zones;
            zoneReach = reach;

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
        /// Endless re-base (EndlessTower.Stream): the tower dropped its lowest <paramref name="dropPlatforms"/> platforms and moved every height down by
        /// <paramref name="yShift"/>. Makes the running state follow: every stored absolute height (Y, KillY, MaxY, the rocket target) moves by the same exact amount, the
        /// platform ids (GroundId, BestGroundId, the per-platform CrumbleTick array) move down by the dropped count. Nothing else in a state is a height or an id
        /// (rows are absolute floors and stay), and the Sim only ever compares heights with heights, so the run goes on bit for bit as if nothing happened.
        /// Allocates one array: call it once per re-base (a few hundred floors), never per tick. Call it BEFORE <see cref="GrowState"/> of the new Sim.
        /// </summary>
        public static void ShiftState(ref SimState s, int dropPlatforms, Fx yShift)
        {
            s.Y = s.Y - yShift;
            s.KillY = s.KillY - yShift;
            s.MaxY = s.MaxY - yShift;
            if (s.RocketTicks > 0) s.RocketTargetY = s.RocketTargetY - yShift;   // only a running burn reads it (it is set again at the next tap)
            s.GroundId -= dropPlatforms;
            s.BestGroundId -= dropPlatforms;
            if (dropPlatforms > 0 && s.CrumbleTick != null)
            {
                int keep = s.CrumbleTick.Length - dropPlatforms;
                if (keep < 0) keep = 0;
                int[] c = new int[keep];
                System.Array.Copy(s.CrumbleTick, dropPlatforms, c, 0, keep);
                s.CrumbleTick = c;
            }
        }

        /// <summary>
        /// Minimal revive: if dead, puts the player back on the highest platform landed on, clears speed,
        /// grants a short invulnerability and counts the revive. UI and payment are the caller's job.
        /// </summary>
        public void Revive(ref SimState s)
        {
            if (!s.Dead) return;
            // A paid revive must not drop the player again at once: a hidden or about-to-vanish Vanishing platform is skipped (the next platform
            // down the tower is used), and a Crumble / Snap Daisy / Spore Pop gets its clock reset (open, whole, calm) as if never touched.
            int rid = s.BestGroundId;
            int vanishCycle = Cfg.VanishVisibleTicks + Cfg.VanishHiddenTicks;
            while (rid > 0 && Platforms[rid].Kind == PlatformKind.Vanishing
                   && (s.Tick + Platforms[rid].Phase) % vanishCycle >= Cfg.VanishVisibleTicks - Cfg.VanishWarnTicks)
                rid--;
            s.BestGroundId = rid;
            PlatformKind rk = Platforms[rid].Kind;
            if ((rk == PlatformKind.Crumble || rk == PlatformKind.SnapDaisy || rk == PlatformKind.SporePop || rk == PlatformKind.JellyWobble) && s.CrumbleTick != null) s.CrumbleTick[rid] = -1;
            Platform p = Platforms[s.BestGroundId];
            p.Y = PlatformY(s.BestGroundId, s.Tick, s.CrumbleTick);   // a Bee Hive Lift is somewhere on its bob, a Sprout Lift somewhere on its rise
            s.TiltDir = 0;
            s.SteerLock = 0;
            s.FloatTicks = 0;
            s.JumpLock = 0;
            s.GlideTicks = 0;
            s.KoiStill = 0;
            s.KoiAir = 0;
            s.Dead = false;
            s.GroundId = s.BestGroundId;
            s.X = PlatformX(s.BestGroundId, s.Tick);
            if (p.Kind == PlatformKind.FairyRing) s.X = p.X + p.Amplitude + Fx.One;   // never revive into the hole
            s.LandTick = s.Tick;
            if (p.Kind == PlatformKind.SproutLift || p.Kind == PlatformKind.SinkingRaft || p.Kind == PlatformKind.LanternLift) OccupySprout(ref s, s.BestGroundId, s.Tick);
            s.Y = p.Kind == PlatformKind.SandcastleStep ? SandSurfaceY(in p, s.X) : p.Y;
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
            else if (p.Kind == PlatformKind.OrbitCap)
            {
                if (p.Period <= 0) return p.X;
                return p.X + SpecialPlatforms.Cos((tick + p.Phase) % p.Period, p.Period) * p.Amplitude;   // counter-clockwise lap: x = cos, y = sin
            }
            else if (p.Kind == PlatformKind.TurtleShuttle) return p.X + TurtleOffset(in p, tick);
            else if (p.Kind == PlatformKind.JellyWobble) return p.X + JellyOffset(in p, id, tick, aux);
            else if (p.Kind == PlatformKind.LanternLift || p.Kind == PlatformKind.SwingPlank)
            {
                // region 5: a lantern sways and a plank swings on a sine of its own period (Platform.Amplitude each way)
                if (p.Period <= 0) return p.X;
                return p.X + SpecialPlatforms.Sin((tick + p.Phase) % p.Period, p.Period) * p.Amplitude;
            }
            else if (p.Kind != PlatformKind.Moving) return p.X;
            int half = p.Period / 2;
            int phase = (tick + p.Phase + shift) % p.Period;
            int tri = phase < half ? phase : p.Period - phase;
            Fx t = Fx.FromRatio(tri, half);
            return p.X + (t * 2 - Fx.One) * p.Amplitude;
        }

        /// <summary>
        /// Turtle Shuttle: sideways offset from the base X at a tick, -Amplitude to +Amplitude. Stop-and-go: pause at the left, glide right (eased,
        /// TurtleGlideTicks), pause at the right, glide back; the pauses fill the rest of TurtlePeriodTicks.
        /// </summary>
        public Fx TurtleOffset(in Platform p, int tick)
        {
            int period = p.Period > 0 ? p.Period : Cfg.TurtlePeriodTicks;
            int glide = Cfg.TurtleGlideTicks;
            int pause = (period - 2 * glide) / 2;
            int t = (tick + p.Phase) % period;
            Fx u;   // 0 = left end, 1 = right end
            if (t < pause) u = Fx.Zero;
            else if (t < pause + glide) u = Ease(t - pause, glide);
            else if (t < 2 * pause + glide) u = Fx.One;
            else u = Fx.One - Ease(t - 2 * pause - glide, glide);
            return (u * 2 - Fx.One) * p.Amplitude;
        }

        /// <summary>
        /// Jelly Wobble: sideways offset at a tick. <paramref name="aux"/> is SimState.CrumbleTick (the landing tick lives there; -1 or null = calm). After a landing
        /// the cube swings on a triangle wave of JellyWaveTicks (3 Hz) that starts at 0, with an amplitude that fades linearly to 0 over JellyWobbleTicks.
        /// </summary>
        public Fx JellyOffset(in Platform p, int id, int tick, int[] aux)
        {
            if (aux == null) return Fx.Zero;
            int landed = aux[id];
            if (landed < 0) return Fx.Zero;
            int e = tick - landed;
            if (e < 0 || e >= Cfg.JellyWobbleTicks) return Fx.Zero;
            int wave = Cfg.JellyWaveTicks;
            int q = wave / 4;                       // quarter wave: 0 to +1 in q ticks, back to 0 and on to -1, then to 0
            int ph = e % wave;
            Fx tri;
            if (ph < q) tri = Fx.FromRatio(ph, q);
            else if (ph < 3 * q) tri = Fx.FromRatio(2 * q - ph, q);
            else tri = Fx.FromRatio(ph - wave, q);
            return p.Amplitude * tri * (Cfg.JellyWobbleTicks - e) / Cfg.JellyWobbleTicks;
        }

        /// <summary>Width of a platform at a tick: Platform.Width, except the Taffy Stretch, which breathes between Width and Width + 2 x Amplitude on a triangle wave of its period.</summary>
        public Fx WidthAt(int id, int tick)
        {
            Platform p = Platforms[id];
            if (p.Kind != PlatformKind.TaffyStretch || p.Period <= 0) return p.Width;
            int half = p.Period / 2;
            int phase = (tick + p.Phase) % p.Period;
            int tri = phase < half ? phase : p.Period - phase;
            return p.Width + p.Amplitude * 2 * Fx.FromRatio(tri, half);
        }

        private static Fx Ease(int t, int total)
        {
            Fx e = Fx.FromRatio(t, total);
            return e * e * (Fx.FromInt(3) - e * 2);   // smoothstep
        }

        /// <summary>Centre X of a platform as the run in <paramref name="s"/> sees it at the state's tick (includes the Ladybug boost).</summary>
        public Fx PlatformXAt(in SimState s, int id)
        {
            return PlatformX(id, s.Tick, s.CrumbleTick);
        }

        /// <summary>Surface height of a platform at a tick: the base Y, bobbing for a Bee Hive Lift (triangle wave of HivePeriodTicks, +-HiveBobAmplitude).</summary>
        public Fx PlatformY(int id, int tick)
        {
            return PlatformY(id, tick, null);
        }

        /// <summary>
        /// Surface height of a platform at a tick: Bee Hive Lift bob, Orbit Cap lap (sin), Sprout Lift rise. <paramref name="aux"/> is SimState.CrumbleTick
        /// (the Sprout Lift's rise clock lives there; null = not risen), may be null for every other kind.
        /// </summary>
        public Fx PlatformY(int id, int tick, int[] aux)
        {
            Platform p = Platforms[id];
            switch (p.Kind)
            {
                case PlatformKind.HiveLift:
                    {
                        if (p.Period <= 0) return p.Y;
                        int half = p.Period / 2;
                        int phase = (tick + p.Phase) % p.Period;
                        int tri = phase < half ? phase : p.Period - phase;
                        return p.Y + (Fx.FromRatio(tri, half) * 2 - Fx.One) * Cfg.HiveBobAmplitude;
                    }
                case PlatformKind.OrbitCap:
                    if (p.Period <= 0) return p.Y;
                    return p.Y + SpecialPlatforms.Sin((tick + p.Phase) % p.Period, p.Period) * Cfg.OrbitAmplitudeY;
                case PlatformKind.SproutLift:
                    return p.Y + Cfg.SproutRise * SproutLevel(id, tick, aux) / Cfg.SproutRiseTicks;
                case PlatformKind.SinkingRaft:
                    return p.Y - Cfg.RaftSinkDepth * SproutLevel(id, tick, aux) / Cfg.RaftSinkTicks;
                case PlatformKind.LanternLift:
                    return p.Y + Cfg.LanternRise * SproutLevel(id, tick, aux) / Cfg.LanternRiseTicks;
                case PlatformKind.SwingPlank:
                    {
                        // pendulum: lowest in the middle, PlankRise higher at both ends (y = rise x sin^2)
                        if (p.Period <= 0) return p.Y;
                        Fx sn = SpecialPlatforms.Sin((tick + p.Phase) % p.Period, p.Period);
                        return p.Y + Cfg.PlankRise * sn * sn;
                    }
                default:
                    return p.Y;
            }
        }

        /// <summary>
        /// Sprout Lift rise level at a tick, 0 to SproutRiseTicks. CrumbleTick[id] is -1 (never stood on), the virtual start tick of the rise while the player stands
        /// on it (level = ticks since, capped), or -(tickLeft * 128 + levelLeft) - 2 once it is empty (the level then falls SproutSinkPercent of a tick per tick).
        /// </summary>
        public int SproutLevel(int id, int tick, int[] aux)
        {
            if (aux == null) return 0;
            int v = aux[id];
            if (v == -1) return 0;
            PlatformKind lk = Platforms[id].Kind;
            bool raft = lk == PlatformKind.SinkingRaft;   // the Sinking Raft uses the same clock: its "rise" is the sink, its release is the spring back
            bool lantern = lk == PlatformKind.LanternLift;   // the Lantern Lift (region 5) too: own rise ticks and a slower float down
            int cap = raft ? Cfg.RaftSinkTicks : (lantern ? Cfg.LanternRiseTicks : Cfg.SproutRiseTicks);
            if (v >= 0)
            {
                int l = tick - v;
                return l < 0 ? 0 : (l > cap ? cap : l);
            }
            int packed = -(v + 2);
            int tl = packed >> 7, ll = packed & 127;
            int level = ll - (tick - tl) * (raft ? Cfg.RaftSpringPercent : (lantern ? Cfg.LanternSinkPercent : Cfg.SproutSinkPercent)) / 100;
            return level < 0 ? 0 : level;
        }

        /// <summary>The player starts standing on a Sprout Lift: the rise continues from the level it has now.</summary>
        private void OccupySprout(ref SimState s, int id, int tick)
        {
            int t0 = tick - SproutLevel(id, tick, s.CrumbleTick);
            s.CrumbleTick[id] = t0 < 0 ? 0 : t0;
        }

        /// <summary>The player left a Sprout Lift (jump, walk-off, hop): it starts to sink from its current level.</summary>
        private void ReleaseSprout(ref SimState s, int id, int tick)
        {
            int v = s.CrumbleTick[id];
            if (v < 0) return;
            int level = SproutLevel(id, tick, s.CrumbleTick);
            s.CrumbleTick[id] = -(tick * 128 + level) - 2;
        }

        /// <summary>Sinking Raft: how far the raft has sunk at a tick (0 to RaftSinkDepth); the player on it and the raft are drawn this much lower.</summary>
        public Fx RaftSink(int id, int tick, int[] aux)
        {
            if (Platforms[id].Kind != PlatformKind.SinkingRaft) return Fx.Zero;
            return Platforms[id].Y - PlatformY(id, tick, aux);
        }

        /// <summary>Sandcastle Step: surface height at x (the half on side Platform.Dir is Platform.Amplitude higher than Platform.Y; the centre line belongs to the low half).</summary>
        public static Fx SandSurfaceY(in Platform p, Fx x)
        {
            return IsRaisedHalf(in p, x) ? p.Y + p.Amplitude : p.Y;
        }

        /// <summary>Sandcastle Step: true when x is on the raised half.</summary>
        public static bool IsRaisedHalf(in Platform p, Fx x)
        {
            return p.Kind == PlatformKind.SandcastleStep && p.Dir != 0 && (x - p.X) * p.Dir > Fx.Zero;
        }

        /// <summary>Clam Shell Pad: true while the shell is open (global clock; a landing while open launches).</summary>
        public bool ClamOpen(int id, int tick)
        {
            Platform p = Platforms[id];
            return p.Kind == PlatformKind.ClamShellPad && (tick + p.Phase) % Cfg.ClamCycleTicks < Cfg.ClamOpenTicks;
        }

        /// <summary>Pufferfish Pad phase at a tick: 0 calm, 1 swelling, 2 puffed and spiky (landing or standing hops the player away).</summary>
        public int PufferPhase(int id, int tick)
        {
            Platform p = Platforms[id];
            if (p.Kind != PlatformKind.PufferfishPad) return 0;
            int c = (tick + p.Phase) % Cfg.PufferCycleTicks;
            return c >= Cfg.PufferPuffStart ? 2 : (c >= Cfg.PufferSwellStart ? 1 : 0);
        }

        /// <summary>Mallet Smash: position in the global cycle at a tick, 0 to MalletCycleTicks - 1 (rising until MalletSmashStart, smashing for MalletSmashTicks, then down and lifting again).</summary>
        public int MalletCycle(int id, int tick)
        {
            Platform p = Platforms[id];
            return p.Kind == PlatformKind.MalletSmash ? (tick + p.Phase) % Cfg.MalletCycleTicks : 0;
        }

        /// <summary>Mallet Smash: true during the smash window (ticks MalletSmashStart to MalletSmashStart + MalletSmashTicks - 1 of the cycle): the only time the zone hurts.</summary>
        public bool MalletSmashing(int id, int tick)
        {
            if (Platforms[id].Kind != PlatformKind.MalletSmash) return false;
            int c = (tick + Platforms[id].Phase) % Cfg.MalletCycleTicks;
            return c >= Cfg.MalletSmashStart && c < Cfg.MalletSmashStart + Cfg.MalletSmashTicks;
        }

        /// <summary>Mallet Smash painted pose: true from the smash until MalletDownEnd (the mallet lies on the slab), false while it is raised or rising. Pure presentation of the cycle.</summary>
        public bool MalletDown(int id, int tick)
        {
            if (Platforms[id].Kind != PlatformKind.MalletSmash) return false;
            int c = (tick + Platforms[id].Phase) % Cfg.MalletCycleTicks;
            return c >= Cfg.MalletSmashStart && c < Cfg.MalletDownEnd;
        }

        /// <summary>Mallet Smash: true when x is inside the smash zone (MalletZoneHalf either side of a point MalletZoneHalf inside the end on side Platform.Dir).</summary>
        public bool InMalletZone(in Platform p, Fx x)
        {
            if (p.Kind != PlatformKind.MalletSmash || p.Dir == 0) return false;
            Fx zoneX = p.X + (p.Width / 2 - Cfg.MalletZoneHalf) * p.Dir;
            return Fx.Abs(x - zoneX) < Cfg.MalletZoneHalf;
        }

        /// <summary>Rolling Log: centre x of the log at a tick (triangle wave over the platform: LogWidth in from each end, Platform.Period for there and back at a constant speed).</summary>
        public Fx LogX(int id, int tick)
        {
            Platform p = Platforms[id];
            if (p.Kind != PlatformKind.RollingLog || p.Period <= 1) return p.X;
            int half = p.Period / 2;
            int phase = (tick + p.Phase) % p.Period;
            int tri = phase < half ? phase : p.Period - phase;
            Fx travel = p.Width - Cfg.LogWidth;
            return p.X - travel / 2 + travel * Fx.FromRatio(tri, half);
        }

        /// <summary>Squish Cap: how far the cap has sunk under the player standing on it (0 to SquishDepth); the player and the cap are drawn this much lower.</summary>
        public Fx SquishSink(in SimState s, int id)
        {
            if (!s.Grounded || s.GroundId != id || Platforms[id].Kind != PlatformKind.SquishCap) return Fx.Zero;
            int e = s.Tick - s.LandTick;
            if (e < 0) e = 0;
            if (e > Cfg.SquishTicks) e = Cfg.SquishTicks;
            return Cfg.SquishDepth * e / Cfg.SquishTicks;
        }

        /// <summary>Jump speed factor of a jump from the Squish Cap in <paramref name="s"/>: 1 right after the landing, 1 + SquishJumpBonusPercent / 100 when fully squashed.</summary>
        private Fx SquishJumpMultiplier(in SimState s)
        {
            int e = s.Tick - s.LandTick;
            if (e < 0) e = 0;
            if (e > Cfg.SquishTicks) e = Cfg.SquishTicks;
            return Fx.FromRatio(100 * Cfg.SquishTicks + Cfg.SquishJumpBonusPercent * e, 100 * Cfg.SquishTicks);
        }

        /// <summary>Fairy Ring: true when x is over the hole (the player falls through there).</summary>
        public static bool InHole(in Platform p, Fx x)
        {
            return p.Kind == PlatformKind.FairyRing && Fx.Abs(x - p.X) < p.Amplitude;
        }

        /// <summary>Spore Pop phase at a tick: 0 calm, 1 swelling (the pop comes at its end), 2 flat after the pop. Uses SimState.CrumbleTick (landing tick).</summary>
        public int SporePopPhase(int id, int tick, int[] crumbleTick)
        {
            if (Platforms[id].Kind != PlatformKind.SporePop) return 0;
            int landed = crumbleTick[id];
            if (landed < 0) return 0;
            int e = tick - landed;
            if (e < Cfg.SporePopTicks) return 1;
            return e < Cfg.SporePopTicks + Cfg.SporePopFlatTicks ? 2 : 0;
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
            // Endless (owner 2026-11-05): no player-speed bonus any more; a floor at level N plays like campaign level N, which has none.
            return Cfg.MaxSpeed;
        }

        /// <summary>
        /// Scroll speed (units per tick) of the danger line at <paramref name="tick"/> before the Slow Time power-up: base speed, stepped speed-up,
        /// row ramp (endless / boss) and the campaign ceiling. The HUD percent is derived from this so screen and game cannot drift apart.
        /// </summary>
        public Fx ScrollPerTickAt(in SimState s, int tick)
        {
            Fx scroll = Endless ? endlessScroll[EndlessTower.LevelForFloor(s.Row, endlessLevels, endlessTimeAttack)] : scrollPerTick;
            int steps = (tick - s.StartTick) / (Cfg.ScrollStepSeconds * SimConfig.TicksPerSecond);
            if (steps > Cfg.ScrollStepMaxSteps) steps = Cfg.ScrollStepMaxSteps;
            int stepPct = CampaignRamp ? 0 : steps * Cfg.ScrollStepPercent;   // boss levels already ramp with the row
            if (stepPct > Cfg.ScrollStepCapPercent) stepPct = Cfg.ScrollStepCapPercent;
            scroll = scroll * (100 + stepPct) / 100;
            if (!Endless && CampaignRamp)
            {
                int pct = Cfg.RampPercentAt(s.Row);
                if (pct > Cfg.CampaignRampCapPercent) pct = Cfg.CampaignRampCapPercent;
                scroll = scroll * (100 + pct) / 100;
            }
            // the campaign ceiling holds in endless too: a floor at level N is as fast as campaign level N, never faster
            Fx ceiling = Cfg.ScrollMaxSpeed / SimConfig.TicksPerSecond;
            if (scroll > ceiling) scroll = ceiling;
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
            if (s.SteerLock > 0) dir = 0;   // Dizzy Spores: steering is ignored (never inverted); momentum and friction still work
            PlatformKind groundKind = Platforms[s.GroundId].Kind;
            int sproutId = s.Grounded && (groundKind == PlatformKind.SproutLift || groundKind == PlatformKind.SinkingRaft || groundKind == PlatformKind.LanternLift) ? s.GroundId : -1;
            Fx maxSpeed = CurrentMaxSpeed(s.Row);

            // --- power-ups: activation edges, then the effect flags of this tick ---
            if ((input & (SimInput.PowerMask | SimInput.HeroMask)) != 0) ActivatePowerUps(ref s, input, tick);
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
            Fx friction = Cfg.Accel * Cfg.BrakeFactor;
            if (!s.Grounded)
            {
                accel = accel * Cfg.AirControl;
                if (s.GlideTicks > 0) accel = accel * Cfg.GlideAirMultiplier;   // Floss Glide: the air steering after a jump from it is doubled
                else if (s.KoiAir > 0) accel = Cfg.Accel;                       // Koi Stone: a charged jump keeps the full ground steering in the air
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
            // Kelp Tangle: the press is kept until the lock ends (the buffer outlasts the remaining lock by one tick)
            if ((input & SimInput.Jump) != 0) s.JumpBuffer = s.JumpLock > 0 && s.JumpLock + 1 > Cfg.JumpBufferTicks ? s.JumpLock + 1 : Cfg.JumpBufferTicks;
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
            else if (s.JumpBuffer > 0 && s.JumpLock == 0 && (s.Grounded || s.Coyote > 0))
            {
                Fx v0 = Cfg.JumpSpeed(Fx.Abs(s.Vx));
                if (surface == PlatformKind.Sticky) v0 = v0 * Cfg.StickyJumpMultiplier;
                else if (surface == PlatformKind.SquishCap && s.Grounded) v0 = v0 * SquishJumpMultiplier(in s);   // a charged cap launches up to x1.45
                else if (surface == PlatformKind.PuffFloat) s.FloatTicks = Cfg.PuffFloatTicks;                      // the fall after this jump is slowed
                else if (surface == PlatformKind.FlossGlide) s.GlideTicks = Cfg.GlideAirTicks;                      // the air steering after this jump is doubled
                else if (surface == PlatformKind.KoiStone && s.Grounded && s.KoiStill >= Cfg.KoiStillTicks)
                {
                    v0 = v0 * Cfg.KoiJumpMultiplier;   // a rested player launches about 10 percent higher and keeps the full air steering for a second
                    s.KoiAir = Cfg.KoiAirTicks;
                }
                s.KoiStill = 0;
                s.Vy = v0;
                s.Grounded = false;
                s.Coyote = 0;
                s.JumpBuffer = 0;
                s.JumpStartRow = Platforms[s.GroundId].Row;
                if (s.StartTick < 0) s.StartTick = tick;
            }
            else if (s.JumpBuffer > 0 && s.DoubleJumpTicks > 0 && !s.AirJumpUsed && !flying && !holding && !s.Grounded && s.Coyote == 0 && s.Vy <= Fx.Zero && s.JumpLock == 0)
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
                // Air zones (Fizz Vent: low gravity, Pinwheel Gust: sideways push): read once per tick from the platforms below the player
                Fx gravity = Cfg.Gravity, zonePush = Fx.Zero;
                if (hasAirZones && !flying && !holding) ReadAirZones(in s, out gravity, out zonePush);
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
                else if (s.FloatTicks > 0 && s.Vy < Fx.Zero)
                {
                    s.Vy = s.Vy - Cfg.Gravity * Cfg.PuffGravityPercent / 100;   // Puff Float: rising is normal, falling is slow
                    if (s.Vy < -Cfg.PuffMaxFall) s.Vy = -Cfg.PuffMaxFall;
                }
                else
                {
                    s.Vy = s.Vy - gravity;
                }
                s.Y = s.Y + s.Vy;
                s.X = s.X + s.Vx + zonePush;
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
                else if (!solid || Fx.Abs(s.X - PlatformX(s.GroundId, tick, s.CrumbleTick)) > WidthAt(s.GroundId, tick) / 2 + Cfg.LandingMargin || InHole(in g, s.X))
                {
                    s.Grounded = false;
                    s.Vy = Fx.Zero;
                    s.Coyote = Cfg.CoyoteTicks;
                    s.JumpStartRow = g.Row;
                }
                else if (g.Kind == PlatformKind.SandcastleStep)
                {
                    // two levels: walking into the raised half steps up at once, walking back down slides to the low half (the player stays grounded)
                    Fx surf = SandSurfaceY(in g, s.X);
                    if (surf > s.Y) s.Y = surf;
                    else if (surf < s.Y) s.Y = Fx.Max(surf, s.Y - Cfg.SandcastleDropSpeed);
                }
                else if (g.Kind == PlatformKind.PufferfishPad && PufferPhase(s.GroundId, tick) == 2)
                {
                    HopFromPuffer(ref s, in g);   // standing on a puffed pufferfish hops the player away
                }
                else if (g.Kind == PlatformKind.MalletSmash && s.Invuln == 0 && MalletSmashing(s.GroundId, tick) && InMalletZone(in g, s.X))
                {
                    HopFromMallet(ref s, in g);   // standing in the smash zone at the smash: hopped inward
                }
                else if (g.Kind == PlatformKind.RollingLog && s.Invuln == 0 && Fx.Abs(s.X - LogX(s.GroundId, tick)) < Cfg.LogContactHalf)
                {
                    BumpFromLog(ref s, in g, tick);   // the rolling log runs into the player
                }
            }
            else
            {
                if (s.Coyote > 0) s.Coyote--;
                if (s.Invuln == 0 && Platforms[s.GroundId].Kind == PlatformKind.RollingLog && s.Y >= Platforms[s.GroundId].Y && s.Y - Platforms[s.GroundId].Y < Cfg.LogLowAir
                    && Fx.Abs(s.X - LogX(s.GroundId, tick)) < Cfg.LogContactHalf && Fx.Abs(s.X - Platforms[s.GroundId].X) < Platforms[s.GroundId].Width / 2 + Cfg.LandingMargin)
                {
                    Platform lp = Platforms[s.GroundId];
                    BumpFromLog(ref s, in lp, tick);   // still low over the platform it just left: the log catches the feet (jump over it instead)
                }
                if (s.Vy < Fx.Zero)
                {
                    int best = -1;
                    Fx bestY = Fx.Zero;
                    for (int i = LowerBound(s.Y - bobMargin); i < Platforms.Length && Platforms[i].Y <= prevY + bobMargin; i++)
                    {
                        Platform p = Platforms[i];
                        Fx surfaceY = p.Y;
                        if (p.Kind == PlatformKind.SandcastleStep)
                        {
                            // the surface under the player's x; a player already below the top of the raised half lands on the low surface (no falling through the block)
                            if (Fx.Abs(s.X - p.X) > p.Width / 2 + Cfg.LandingMargin) continue;
                            Fx top = IsRaisedHalf(in p, s.X) ? p.Y + p.Amplitude : p.Y;
                            if (top <= prevY && top >= s.Y) surfaceY = top;
                            else if (p.Y <= prevY && p.Y >= s.Y) surfaceY = p.Y;
                            else continue;
                        }
                        else if (p.Kind == PlatformKind.HiveLift || p.Kind == PlatformKind.OrbitCap || p.Kind == PlatformKind.SproutLift || p.Kind == PlatformKind.SinkingRaft
                            || p.Kind == PlatformKind.LanternLift || p.Kind == PlatformKind.SwingPlank)
                        {
                            // a bobbing, orbiting or rising surface is crossed when the feet were above it last tick and are at or below it now
                            surfaceY = PlatformY(i, tick, s.CrumbleTick);
                            if (surfaceY < s.Y || PlatformY(i, s.Tick, s.CrumbleTick) > prevY) continue;
                        }
                        else if (p.Y < s.Y || p.Y > prevY) continue;   // outside the fall of this tick (only possible with the widened window)
                        if (!IsSolid(i, tick, s.CrumbleTick)) continue;
                        if (Fx.Abs(s.X - PlatformX(i, tick, s.CrumbleTick)) > (p.Kind == PlatformKind.TaffyStretch ? WidthAt(i, tick) : p.Width) / 2 + Cfg.LandingMargin) continue;
                        if (InHole(in p, s.X)) continue;   // Fairy Ring: the hole is no landing spot
                        if (best < 0 || surfaceY > bestY) { best = i; bestY = surfaceY; }
                    }
                    if (best >= 0)
                    {
                        if (Platforms[best].Kind == PlatformKind.PufferfishPad && PufferPhase(best, tick) == 2)
                        {
                            Platform pp = Platforms[best];
                            s.Y = pp.Y;
                            HopFromPuffer(ref s, in pp);   // a landing on a puffed pufferfish is no landing: it hops the player away
                        }
                        else if (Platforms[best].Kind == PlatformKind.ThornBush && OnThorns(best, s.X, tick))
                        {
                            Platform tp = Platforms[best];
                            s.Y = tp.Y;
                            KnockFromThorns(ref s, in tp);   // a landing on the thorns does not count as a landing
                        }
                        else if (Platforms[best].Kind == PlatformKind.MalletSmash && s.Invuln == 0 && MalletSmashing(best, tick) && InMalletZone(in Platforms[best], s.X))
                        {
                            Platform mp = Platforms[best];
                            s.Y = mp.Y;
                            HopFromMallet(ref s, in mp);   // a landing under the smashing mallet is no landing: it hops the player inward
                        }
                        else if (Platforms[best].Kind == PlatformKind.RollingLog && s.Invuln == 0 && Fx.Abs(s.X - LogX(best, tick)) < Cfg.LogContactHalf)
                        {
                            Platform gp = Platforms[best];
                            s.Y = gp.Y;
                            BumpFromLog(ref s, in gp, tick);   // landing on the log bumps the player up and toward the middle
                        }
                        else Land(ref s, best, tick, bestY);
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
            if (s.SteerLock > 0) s.SteerLock--;
            if (s.JumpLock > 0) s.JumpLock--;
            if (s.GlideTicks > 0) s.GlideTicks--;
            if (s.KoiAir > 0) s.KoiAir--;
            if (s.FloatTicks > 0 && s.Vy < Fx.Zero) s.FloatTicks--;   // Puff Float: the window runs while the player falls (the rise does not eat it)
            if (sproutId >= 0 && !(s.Grounded && s.GroundId == sproutId)) ReleaseSprout(ref s, sproutId, tick);   // the Sprout Lift starts to sink once it is empty
            s.Tick = tick;
        }

        // ------------------------------------------------------------------------------------------
        // Power-ups. Everything below is skipped (or cheap) for a run without a loadout.
        // ------------------------------------------------------------------------------------------

        private void ActivatePowerUps(ref SimState s, int input, int tick)
        {
            // One bit per type (SimInput.PowerFirst << (id - 1)); every tap uses one owned unit and ADDS the full base time onto the remaining time.
            // A hero bit (SimInput.HeroFirst << (id - 1)) activates the same effect for free with HeroPowerDurationPercent of the time (no stacking onto itself).
            for (int i = (int)PowerUp.Flight; i <= (int)PowerUp.SlowTime; i++)
            {
                PowerUp p = (PowerUp)i;
                if ((input & PowerUps.InputBit(p)) != 0 && PowerUps.CanActivate(in s, p)) ApplyPowerUp(ref s, p, tick, false);
                if ((input & PowerUps.HeroInputBit(p)) != 0 && PowerUps.CanActivateHero(in s, p)) ApplyPowerUp(ref s, p, tick, true);
            }
        }

        private void ApplyPowerUp(ref SimState s, PowerUp p, int tick, bool hero)
        {
            int i = (int)p;
            s.PwUsed = PowerUps.SatAdd(s.PwUsed, 1);
            s.PwUsedMask |= 1 << i;
            switch (p)
            {
                case PowerUp.Flight:
                case PowerUp.Rocket:
                    bool already = p == PowerUp.Flight ? s.FlightTicks > 0 : s.RocketTicks > 0;
                    if (p == PowerUp.Flight)
                    {
                        s.FlightTicks = PowerUps.SatAdd(s.FlightTicks, hero ? Cfg.HeroTicks(Cfg.FlightTicks) : Cfg.FlightTicks);
                    }
                    else
                    {
                        // Rocket: each tap adds RocketFloors rows and RocketTicks ticks; the climb is measured from the platform stood on last.
                        // A hero Rocket scales both (same climb speed): 12 floors in 2.4 s.
                        s.RocketTicks = PowerUps.SatAdd(s.RocketTicks, hero ? Cfg.HeroTicks(Cfg.RocketTicks) : Cfg.RocketTicks);
                        int baseRow = already ? s.RocketTargetRow : Platforms[s.GroundId].Row;
                        s.RocketTargetRow = baseRow + (hero ? Cfg.HeroRocketFloors : Cfg.RocketFloors);
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
                case PowerUp.DoubleJump: s.DoubleJumpTicks = PowerUps.SatAdd(s.DoubleJumpTicks, hero ? Cfg.HeroTicks(Cfg.DoubleJumpWindowTicks) : Cfg.DoubleJumpWindowTicks); break;
                case PowerUp.WallStick: s.WallStickTicks = PowerUps.SatAdd(s.WallStickTicks, hero ? Cfg.HeroTicks(Cfg.WallStickWindowTicks) : Cfg.WallStickWindowTicks); break;
                case PowerUp.Shield: s.ShieldTicks = PowerUps.SatAdd(s.ShieldTicks, hero ? Cfg.HeroTicks(Cfg.ShieldTicks) : Cfg.ShieldTicks); break;
                case PowerUp.Magnet: s.MagnetTicks = PowerUps.SatAdd(s.MagnetTicks, hero ? Cfg.HeroTicks(Cfg.MagnetTicks) : Cfg.MagnetTicks); break;
                case PowerUp.SlowTime: s.SlowTicks = PowerUps.SatAdd(s.SlowTicks, hero ? Cfg.HeroTicks(Cfg.SlowTimeTicks) : Cfg.SlowTimeTicks); break;
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

        private void Land(ref SimState s, int id, int tick, Fx surfaceY)
        {
            Platform p = Platforms[id];
            s.AirJumpUsed = false;   // Double Jump: every landing (also on a bounce platform) gives the next air phase its extra jump back
            p.Y = p.Kind == PlatformKind.SandcastleStep ? surfaceY : PlatformY(id, tick, s.CrumbleTick);   // the surface of a Bee Hive Lift is somewhere on its bob, an Orbit Cap on its lap, a Sprout Lift on its rise, a raft on its sink, a Sandcastle Step at the player's x
            s.JumpLock = 0;
            s.GlideTicks = 0;   // Floss Glide ends with the next landing
            s.KoiStill = 0;     // Koi Stone: the rest starts over with every landing, the charged flight ends
            s.KoiAir = 0;
            s.SteerLock = 0;    // Dizzy Spores and Puff Float end with the next landing
            s.FloatTicks = 0;
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
                s.Score = PowerUps.SatAdd(s.Score, (p.Row - oldRow) * Cfg.FloorPoints);   // saturating: a very long endless chain (combo^2 points) must never wrap negative
                s.Row = p.Row;
                s.BestGroundId = id;
            }
            if (qualifies)
            {
                s.Combo++;
                if (s.Combo > s.BestCombo) s.BestCombo = s.Combo;
                s.Score = PowerUps.SatAdd(s.Score, (int)System.Math.Min((long)s.Combo * s.Combo * Cfg.ComboPointsMultiplier, int.MaxValue));
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
                case PlatformKind.SproutLift:
                case PlatformKind.SinkingRaft:
                case PlatformKind.LanternLift:
                    OccupySprout(ref s, id, tick);
                    break;
                case PlatformKind.BambooFlick:
                    {
                        // the stalk springs back: a fixed 0.8 jump (not dependent on the run speed) and 8 u/s sideways toward the middle of the screen
                        s.Vy = Cfg.JumpBase * Cfg.FlickLaunch;
                        s.Vx = Cfg.FlickVx * (p.Dir == 0 ? 1 : p.Dir);
                        s.Grounded = false;
                        s.Coyote = 0;
                        s.JumpStartRow = p.Row;
                        break;
                    }
                case PlatformKind.KelpTangle:
                    s.JumpLock = Cfg.KelpLockTicks;
                    break;
                case PlatformKind.ClamShellPad:
                    if (ClamOpen(id, tick))
                    {
                        // an open shell launches like a pad (x1.4)
                        s.Vy = Cfg.JumpSpeed(Fx.Abs(s.Vx)) * Cfg.ClamLaunch;
                        s.Grounded = false;
                        s.Coyote = 0;
                        s.JumpStartRow = p.Row;
                    }
                    break;
                case PlatformKind.DizzySpores:
                    s.SteerLock = Cfg.DizzyLockTicks;
                    break;
                case PlatformKind.JellyWobble:
                    {
                        // a new wobble starts unless the cube is still wobbling from the last landing
                        int landed = s.CrumbleTick[id];
                        if (landed < 0 || tick - landed >= Cfg.JellyWobbleTicks) s.CrumbleTick[id] = tick;
                        break;
                    }
                case PlatformKind.SporePop:
                    {
                        // a new countdown starts unless the puffball is already swelling or flat
                        int landed = s.CrumbleTick[id];
                        if (landed < 0 || tick - landed >= Cfg.SporePopTicks + Cfg.SporePopFlatTicks) s.CrumbleTick[id] = tick;
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
                case PlatformKind.SquishCap:
                    s.Y = Platforms[id].Y - SquishSink(in s, id);   // the cap sinks under the player (the sink of the previous tick; the cap is drawn the same)
                    break;
                case PlatformKind.SproutLift:
                case PlatformKind.SinkingRaft:
                    s.Y = PlatformY(id, tick, s.CrumbleTick);
                    break;
                case PlatformKind.LanternLift:
                    // the lantern sways (x) and rises (y) under the player
                    s.X = s.X + PlatformX(id, tick) - PlatformX(id, s.Tick);
                    s.Y = PlatformY(id, tick, s.CrumbleTick);
                    break;
                case PlatformKind.SwingPlank:
                    // the pendulum carries the player along its arc
                    s.X = s.X + PlatformX(id, tick) - PlatformX(id, s.Tick);
                    s.Y = PlatformY(id, tick);
                    break;
                case PlatformKind.KoiStone:
                    s.KoiStill = s.Vx.Raw == 0 ? s.KoiStill + 1 : 0;   // standing still: no horizontal speed
                    break;
                case PlatformKind.PagodaRidge:
                    {
                        // beyond the flat middle the glazed tiles slope away: a standing player slides outward
                        Fx roff = s.X - Platforms[id].X;
                        if (roff > Cfg.RidgeFlatHalf) s.X = s.X + Cfg.RidgeSlide;
                        else if (roff < -Cfg.RidgeFlatHalf) s.X = s.X - Cfg.RidgeSlide;
                        break;
                    }
                case PlatformKind.SurfBoostWave:
                    {
                        // the wave carries the player along Dir (toward the middle of the screen) up to the top speed
                        int sd = Platforms[id].Dir == 0 ? 1 : Platforms[id].Dir;
                        Fx top = CurrentMaxSpeed(s.Row);
                        s.Vx = Fx.Clamp(s.Vx + Cfg.SurfAccel * sd, -top, top);
                        break;
                    }
                case PlatformKind.TurtleShuttle:
                    s.X = s.X + PlatformX(id, tick) - PlatformX(id, s.Tick);
                    break;
                case PlatformKind.JellyWobble:
                    // the wobbling cube carries the player most of the way (JellyCarryPercent), so the footing slides a little under an edge-standing player
                    s.X = s.X + (PlatformX(id, tick, s.CrumbleTick) - PlatformX(id, s.Tick, s.CrumbleTick)) * Cfg.JellyCarryPercent / 100;
                    break;
                case PlatformKind.CandyBowl:
                    {
                        // outside the centre share of the dish a gentle pull moves the player back toward the middle
                        Platform bp = Platforms[id];
                        Fx off = s.X - bp.X;
                        if (Fx.Abs(off) > bp.Width / 2 * Cfg.BowlCenterPercent / 100) s.X = off > Fx.Zero ? s.X - Cfg.BowlPull : s.X + Cfg.BowlPull;
                        break;
                    }
                case PlatformKind.LicoriceBelt:
                    s.X = s.X + Cfg.BeltSpeed * (Platforms[id].Dir == 0 ? 1 : Platforms[id].Dir);   // the conveyor carries along Dir (away from the next platform)
                    break;
                case PlatformKind.OrbitCap:
                    {
                        Fx before = PlatformX(id, s.Tick);
                        s.X = s.X + PlatformX(id, tick) - before;
                        s.Y = PlatformY(id, tick);
                        break;
                    }
                case PlatformKind.SporePop:
                    {
                        // still standing on it when the swelling ends: it pops and flings the player up and toward the middle of the screen
                        int landed = s.CrumbleTick[id];
                        if (landed >= 0 && tick - landed == Cfg.SporePopTicks)
                        {
                            Platform pp = Platforms[id];
                            s.Grounded = false;
                            s.Vy = Cfg.SporePopVy;
                            s.Vx = Cfg.SporePopVx * (pp.Dir == 0 ? 1 : pp.Dir);
                            s.Coyote = 0;
                            s.JumpStartRow = pp.Row;
                            s.SporePops++;
                        }
                        break;
                    }
            }
        }

        /// <summary>
        /// Air zones above the platforms below the player: a Fizz Vent column cuts gravity to VentGravityPercent, a Pinwheel Gust column pushes along its Dir.
        /// The zone starts at the surface (the player's feet must be above it) and is VentHeight / PinwheelHeight high.
        /// </summary>
        private void ReadAirZones(in SimState s, out Fx gravity, out Fx push)
        {
            gravity = Cfg.Gravity;
            push = Fx.Zero;
            for (int i = LowerBound(s.Y - zoneReach); i < Platforms.Length && Platforms[i].Y < s.Y; i++)
            {
                Platform p = Platforms[i];
                if (p.Kind == PlatformKind.FizzVent)
                {
                    if (Fx.Abs(s.X - p.X) < Cfg.VentHalfWidth && s.Y - p.Y < Cfg.VentHeight) gravity = Cfg.Gravity * Cfg.VentGravityPercent / 100;
                }
                else if (p.Kind == PlatformKind.PinwheelGust)
                {
                    if (Fx.Abs(s.X - p.X) < Cfg.PinwheelHalfWidth && s.Y - p.Y < Cfg.PinwheelHeight) push = push + Cfg.PinwheelPush * (p.Dir == 0 ? 1 : p.Dir);
                }
            }
        }

        /// <summary>Pufferfish Pad: hops the player away from its centre. Never damages, never counts as a landing, never ends the run.</summary>
        private void HopFromPuffer(ref SimState s, in Platform p)
        {
            s.Grounded = false;
            s.Vx = s.X >= p.X ? Cfg.PufferHopVx : -Cfg.PufferHopVx;
            s.Vy = Cfg.PufferHopVy;
            s.Coyote = 0;
            s.JumpStartRow = p.Row;
            s.PufferHops++;
        }

        /// <summary>Mallet Smash: hops the player inward (away from the mallet end on side Platform.Dir). Never damages, never counts as a landing, never ends the run; invulnerable afterwards so nothing chains.</summary>
        private void HopFromMallet(ref SimState s, in Platform p)
        {
            s.Grounded = false;
            s.Vx = Cfg.MalletKnockVx * -p.Dir;
            s.Vy = Cfg.MalletKnockVy;
            s.Coyote = 0;
            s.JumpStartRow = p.Row;
            if (s.Invuln < Cfg.MalletInvulnTicks) s.Invuln = Cfg.MalletInvulnTicks;
            s.Bumps++;
        }

        /// <summary>Rolling Log: bumps the player up and toward the middle of the platform (away from the platform ends, never into a wall). Never damages, never counts as a landing, never ends the run.</summary>
        private void BumpFromLog(ref SimState s, in Platform p, int tick)
        {
            int toward = s.X < p.X ? 1 : (s.X > p.X ? -1 : (LogX(p.Id, tick) < p.X ? 1 : -1));
            s.Grounded = false;
            s.Vx = Cfg.LogBumpVx * toward;
            s.Vy = Cfg.LogBumpVy;
            s.Coyote = 0;
            s.JumpStartRow = p.Row;
            if (s.Invuln < Cfg.LogInvulnTicks) s.Invuln = Cfg.LogInvulnTicks;
            s.Bumps++;
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
