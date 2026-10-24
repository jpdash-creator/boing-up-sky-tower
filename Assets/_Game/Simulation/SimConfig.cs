namespace BoingUpSkyTower.Simulation
{
    /// <summary>
    /// Physics and rules constants (doc 09 D4: prototype baselines, to be tuned then frozen).
    /// Units: world units, 60 ticks per second. Velocities are units per tick, accelerations per tick squared.
    /// Player width is 1 unit, screen width 10 units (doc 09 D5). Production values will come from
    /// Remote Config; these defaults are the offline fallback.
    /// </summary>
    public sealed class SimConfig
    {
        public const int TicksPerSecond = 60;

        public Fx ScreenWidth = Fx.FromInt(10);
        public Fx PlayerHalfWidth = Fx.Half;
        /// <summary>Extra horizontal tolerance when landing on a platform edge.</summary>
        public Fx LandingMargin = Fx.FromRatio(1, 4);

        public Fx Gravity = Fx.FromRatio(40, 3600);          // 40 u/s^2
        public Fx Accel = Fx.FromRatio(30, 3600);            // 30 u/s^2
        public Fx AirControl = Fx.FromRatio(3, 5);           // air acceleration share
        public Fx Friction = Fx.FromRatio(45, 3600);         // ground deceleration with no input
        public Fx MaxSpeed = Fx.FromRatio(9, 60);            // 9 u/s
        /// <summary>
        /// Standing jump launch speed. 19 u/s peaks at 4.51 u = 1.3 median row gaps (3.48 u); with NormalGapMax 4.2 a single standing
        /// jump always reaches the next platform. Full speed (19 + 0.58 x 9 = 24.2 u/s) peaks at 7.33 u = 2.1 median gaps.
        /// History: 14 u/s until 2026-10-20 (2.45 u), 21 u/s with factor 0.85 the same day (too high), 19 u/s with factor 0.58 (owner list 7).
        /// </summary>
        public Fx JumpBase = Fx.FromRatio(19, 60);           // 19 u/s
        /// <summary>
        /// Launch speed the level layouts are sized for (the pre-2026-10-20 value, 14 u/s). The generator and builder size gaps and reach
        /// from this, so raising JumpBase made the player stronger without stretching the levels (see LayoutMaxJumpHeight, LayoutMaxReachDx).
        /// </summary>
        /// <summary>Largest vertical gap the generator makes between neighbouring rows (every kind). Below the standing peak (4.51 u) by 0.3 u.</summary>
        public Fx NormalGapMax = Fx.FromRatio(42, 10);
        public Fx LayoutJumpBase = Fx.FromRatio(14, 60);
        /// <summary>Speed factor of the layout reference jump (the pre-2026-10-20 0.85). Layout full-speed launch is 21.65 u/s, below the real 24.2 u/s, so every generated dx is covered.</summary>
        public Fx LayoutJumpSpeedFactor = Fx.FromRatio(85, 100);
        public Fx JumpSpeedFactor = Fx.FromRatio(41, 100);   // extra jump speed per unit of horizontal speed
        public Fx WallBoost = Fx.FromRatio(77, 100);         // speed multiplier on wall bounce
        public int CoyoteTicks = 5;                          // 80 ms
        public int JumpBufferTicks = 6;                      // 100 ms

        // Platform behaviours
        public Fx BounceMultiplier = Fx.FromRatio(14, 10);
        public Fx StickyJumpMultiplier = Fx.FromRatio(7, 10);
        public Fx SlipperyFriction = Fx.FromRatio(15, 100);
        public Fx SlipperyAccel = Fx.FromRatio(6, 10);
        public int CrumbleTicks = 30;
        public int VanishVisibleTicks = 150;
        public int VanishHiddenTicks = 60;
        /// <summary>The platform blinks for this many ticks before it vanishes (Sim.VanishWarning).</summary>
        public int VanishWarnTicks = 45;

        // Scroll pressure (kill zone). Units per second at d = 0 and d = 1.
        public Fx ScrollSpeedEasy = Fx.FromRatio(1, 2);
        public Fx ScrollSpeedHard = Fx.FromRatio(2, 1);
        public int ScrollStartRow = 3;
        /// <summary>Kill zone never trails further than this below the highest point reached.</summary>
        public Fx KillDistance = Fx.FromInt(10);
        /// <summary>Distance below the start platform of the bottom edge (danger line) before the first jump.</summary>
        public Fx StartKillDistance = Fx.FromInt(5);
        /// <summary>Scroll speed steps up by ScrollStepPercent every ScrollStepSeconds after the first jump, up to ScrollStepCapPercent.</summary>
        /// <summary>Base scroll speed as a percent of the difficulty-blended speed (owner, 2026-10-19: +20 percent).</summary>
        public int ScrollBasePercent = 120;
        /// <summary>Chance, in percent, that a coin spot is a gem instead (difficulty setting: 5, 5, 7, 9, 10).</summary>
        public int GemInsteadOfCoinPercent = 5;
        public int ScrollStepSeconds = 5;
        public int ScrollStepPercent = 4;
        public int ScrollStepCapPercent = 50;
        /// <summary>
        /// Number of speed steps that are applied; later steps are skipped, so the speed flattens after ScrollStepMaxSteps x ScrollStepSeconds
        /// (30 s). Owner list 2026-10-20: the 20/40/60/80/100 percent steps stay clearly visible for the first 30 s. Without a limit the
        /// screen outruns the best possible climb (about 6.6 to 8 u/s for the bot) after 30 to 40 s and every level ends in a game over.
        /// </summary>
        public int ScrollStepMaxSteps = 6;
        /// <summary>
        /// Hard ceiling of the campaign scroll speed in u/s (not applied to endless). 4.0 u/s is about 60 percent of the fastest bot climb
        /// on the hardest region 1 level (6.6 u/s), the most a good human can sustain; the base speed of the hardest level is 2.4 u/s.
        /// </summary>
        public Fx ScrollMaxSpeed = Fx.FromInt(4);

        // Endless speed ramp (doc 08 section 2)
        public int RampFloors = 25;
        public int RampPercent = 4;
        public int RampCap = 150;
        /// <summary>Percent of the ramp applied to the player's max run speed (doc 08: half rate).</summary>
        public int PlayerSpeedSharePercent = 50;

        // Scoring (doc 09 D5)
        public int FloorPoints = 10;
        public int ComboPointsMultiplier = 5;
        public int ComboMinFloors = 2;

        // ---------------------------------------------------------------------------------------
        // T3 additions. Units: world units (u), ticks (60 per second), percent where named so.
        // ---------------------------------------------------------------------------------------

        // Player body
        /// <summary>Height of the player centre above the feet (s.Y), u. Pickups and hazards test against the centre.</summary>
        public Fx PlayerCenterY = Fx.Half;
        /// <summary>Player collision radius against hazards, u.</summary>
        public Fx PlayerHitRadius = Fx.FromRatio(45, 100);

        // Signature platforms
        /// <summary>BounceCap launch multiplier on the jump speed (Bounce is BounceMultiplier = 1.4; caps are higher).</summary>
        public Fx BounceCapMultiplier = Fx.FromRatio(18, 10);
        /// <summary>Drifting platform (region 3 Moving) swing half-amplitude range, u (plain Moving uses 0.5 to 1.0).</summary>
        public Fx DriftAmplitudeMin = Fx.FromRatio(3, 2);
        public Fx DriftAmplitudeMax = Fx.FromInt(3);
        /// <summary>Drifting platforms are narrowed to at most this width so the sway has room, u.</summary>
        public Fx DriftMaxWidth = Fx.FromInt(4);
        /// <summary>Largest gap generated right above a Sticky platform, u (sticky jumps reach only about 0.49 of the normal height).</summary>
        public Fx StickyNextGapMax = Fx.FromRatio(18, 10);
        /// <summary>
        /// After a Bounce or BounceCap platform the landing speed is not chosen by the player, so the generator keeps the next
        /// row close: gap at most BounceNextGapMax and edge-to-edge horizontal distance at most BounceNextDxMax (u).
        /// </summary>
        public Fx BounceNextGapMax = Fx.FromRatio(42, 10);
        public Fx BounceNextDxMax = Fx.FromInt(3);

        // Level template (doc 08 section 6): share of signature platforms among spine platforms, percent.
        /// <summary>Level 3 (signature introduced gently).</summary>
        public int SignatureIntroPercent = 10;
        /// <summary>Level 4 and up at d = 0; grows linearly to SignatureMaxPercent at d = 1.</summary>
        public int SignatureBasePercent = 15;
        public int SignatureMaxPercent = 40;

        // Coins (doc 08 section 7)
        // Economy v2: coins are not picked up. The only in-run coin source is the combo bonus, paid at the end of the level.
        public int LandingCoins = 0;
        /// <summary>Total coins for a landing that continues a combo (replaces, does not add to, LandingCoins).</summary>
        public int ComboLandingCoins = 5;
        /// <summary>Per extra floor of a multi-floor jump (floors gained beyond 1).</summary>
        public int ExtraFloorCoins = 0;
        public Fx CoinRadius = Fx.FromRatio(9, 10);
        public Fx GemRadius = Fx.FromRatio(9, 10);
        public Fx StarRadius = Fx.FromRatio(10, 10);
        /// <summary>Gems paid per rarity tier (common, rare, epic, legendary) and the chance of each tier in percent (sums to 100).</summary>
        public int[] GemValues = { 1, 2, 5, 10 };
        public int[] GemRarityPercent = { 70, 22, 7, 1 };
        public Fx HiddenGemRadius = Fx.FromRatio(8, 10);
        /// <summary>Path-coin chance per row (percent, 100 = one coin per row) at d = 0 and d = 1.</summary>
        public int PathCoinPercentEasy = 100;
        public int PathCoinPercentHard = 60;
        /// <summary>Chance per row of a risky side-route coin (percent) at d = 0 and d = 1. Half of the hits place a pair.</summary>
        public int RiskyCoinPercentEasy = 10;
        public int RiskyCoinPercentHard = 90;
        /// <summary>Coin density multiplier per level class (simple, medium, hard, complex, highly complex), percent.</summary>
        public int[] ClassCoinPercent = new int[] { 100, 100, 100, 160, 120 };
        /// <summary>Chance per level of one rare 1-gem pickup, percent (doc 08: 3).</summary>
        public int GemChancePercent = 3;

        // Hazards: swinging gongs (region 5; also the SurviveWave section on any region)
        /// <summary>Horizontal impulse away from the gong, u/tick (0.12 = 7.2 u/s; MaxSpeed is 0.15).</summary>
        public Fx GongKnockbackVx = Fx.FromRatio(12, 100);
        /// <summary>Vertical impulse, u/tick, up when the player centre is above the gong centre, down otherwise (airborne only).</summary>
        public Fx GongKnockbackVy = Fx.FromRatio(5, 100);
        /// <summary>Invulnerability after a gong hit, ticks (1.5 s), so a gong cannot chain-hit.</summary>
        public int GongInvulnTicks = 90;
        /// <summary>The arc rises by this share of the swing amplitude at the extremes (pendulum), percent.</summary>
        public int GongArcRisePercent = 25;
        public Fx GongRadiusEasy = Fx.FromRatio(6, 10);
        public Fx GongRadiusHard = Fx.FromRatio(9, 10);
        public Fx GongAmplitudeMin = Fx.FromRatio(3, 2);
        public Fx GongAmplitudeMax = Fx.FromInt(3);
        /// <summary>Chance per row of a gong in a gong region (level 4 and up; level 3 uses half), percent, at d = 0 and d = 1.</summary>
        public int GongDensityEasyPercent = 6;
        public int GongDensityHardPercent = 30;
        /// <summary>Chance per row inside the SurviveWave section, percent (a second gong is added at the same chance / 2).</summary>
        public int WaveDensityPercent = 85;
        /// <summary>SurviveWave section: first and last row as percent of the level height.</summary>
        public int WaveStartPercent = 35;
        public int WaveEndPercent = 65;

        // Campaign scroll ramp (BossChase, LevelSpec.Ramp): same step formula as the endless ramp, clamped.
        public int CampaignRampCapPercent = 60;

        // Builder shaping
        /// <summary>Combo ladder (ComboChain goals): height of one goal-sized jump, u. Rung gap = span / Goal.Value.</summary>
        public Fx ComboLadderSpan = Fx.FromRatio(36, 10);
        public Fx ComboLadderWidth = Fx.FromInt(6);
        /// <summary>Ladders whose rung gap would be below this (comboChain value 4+) use rungs of LadderMinRung in two alternating columns of LadderZigWidth, LadderColumnGap apart edge to edge (stack rule, 2026-10-20).</summary>
        public Fx LadderMinStackGap = Fx.FromRatio(12, 10);
        public Fx LadderMinRung = Fx.One;
        public Fx LadderZigWidth = Fx.FromRatio(30, 10);
        public Fx LadderColumnGap = Fx.FromRatio(22, 10);
        /// <summary>First authored chunk row and spacing (rows between chunks) when LevelSpec.FixedChunks is set.</summary>
        public int ChunkStartRow = 10;
        public int ChunkInterval = 14;
        /// <summary>Authored moving platforms: half-amplitude (u) and period (ticks).</summary>
        public Fx ChunkMovingAmplitude = Fx.FromInt(1);
        public int ChunkMovingPeriod = 240;
        /// <summary>FindHiddenGem side platform width, u, and gem height above its surface, u.</summary>
        public Fx HiddenGemPlatformWidth = Fx.FromRatio(24, 10);
        public Fx HiddenGemHeight = Fx.One;

        // Revive (minimal): see Sim.Revive
        public Fx ReviveKillDistance = Fx.FromInt(6);
        public int ReviveInvulnTicks = 120;

        // ---------------------------------------------------------------------------------------
        // Power-ups (docs/15 FR-PWR; owner decisions 2026-10-20). Player effects only: they never change the layout.
        // Durations are in ticks (60 per second), speeds in u per tick.
        // ---------------------------------------------------------------------------------------

        /// <summary>Flight: 4 s of flight, rising at 8 u/s with normal air steering; hazards cannot hit; 1 s of slow fall after it.</summary>
        public int FlightTicks = 240;
        public Fx FlightRise = Fx.FromRatio(8, 60);
        public int FlightGraceTicks = 60;
        /// <summary>Rocket: 2 s straight up at 14 u/s (28 u, about 8 rows), no steering; 1 s of slow fall after it.</summary>
        public int RocketTicks = 120;
        public Fx RocketRise = Fx.FromRatio(14, 60);
        public int RocketSlowFallTicks = 60;
        /// <summary>Flight and Rocket stop this many rows under the top row of a campaign level, so they can never skip the finish.</summary>
        public int PowerFlightRowsBelowTop = 3;
        /// <summary>Slow fall after Flight/Rocket: gravity is scaled to this percent and the fall speed is capped.</summary>
        public int SlowFallGravityPercent = 35;
        public Fx SlowFallMaxSpeed = Fx.FromRatio(6, 60);
        /// <summary>Double Jump: one unit arms this many extra air jumps (standing-jump strength) for DoubleJumpWindowTicks.</summary>
        public int DoubleJumpCount = 2;
        public int DoubleJumpWindowTicks = 1800;
        /// <summary>Wall Stick: one unit enables sticking for 10 s; each stick holds up to 1.5 s; a wall jump launches away from the wall.</summary>
        public int WallStickWindowTicks = 600;
        public int WallHoldTicks = 90;
        public Fx WallJumpVx = Fx.FromRatio(6, 60);
        /// <summary>Ticks after leaving a wall before it can hold again.</summary>
        public int WallStickCooldownTicks = 15;
        /// <summary>Shield: armed for up to 30 s; absorbs the next gong hit, or the next fall under the kill line (rescue to a platform, once).</summary>
        public int ShieldTicks = 1800;
        /// <summary>The rescue platform is the lowest solid one at least this far above the kill line.</summary>
        public Fx ShieldRescueMargin = Fx.FromInt(3);
        public int ShieldInvulnTicks = 90;
        /// <summary>Magnet: 10 s; every pickup counts as this much larger (a star then collects from about 3 u).</summary>
        public int MagnetTicks = 600;
        public Fx MagnetExtraRadius = Fx.FromInt(2);
        /// <summary>Slow Time: 6 s with the auto-scroll reduced by this percent.</summary>
        public int SlowTimeTicks = 360;
        public int SlowTimePercent = 40;

        /// <summary>Jump launch speed (per tick) for a given horizontal speed magnitude.</summary>
        public Fx JumpSpeed(Fx absVx)
        {
            return JumpBase + JumpSpeedFactor * absVx;
        }

        /// <summary>Analytic maximum jump height at full speed (the real physics).</summary>
        public Fx MaxJumpHeight()
        {
            Fx v0 = JumpSpeed(MaxSpeed);
            return v0 * v0 / (Gravity * 2);
        }

        /// <summary>Maximum jump height the layouts are sized for (full speed at LayoutJumpBase); the generator applies its 0.9 safety factor to this.</summary>
        public Fx LayoutMaxJumpHeight()
        {
            Fx v0 = LayoutJumpBase + LayoutJumpSpeedFactor * MaxSpeed;
            return v0 * v0 / (Gravity * 2);
        }

        /// <summary>
        /// Analytic maximum horizontal distance covered while a full-speed jump rises above and
        /// then comes back down to a platform <paramref name="gap"/> units higher.
        /// Returns zero when the gap is unreachable.
        /// </summary>
        public Fx MaxReachDx(Fx gap)
        {
            return MaxReachDx(gap, Fx.One);
        }

        /// <summary>As MaxReachDx(gap) but for a jump whose launch speed is scaled (for example StickyJumpMultiplier).</summary>
        public Fx MaxReachDx(Fx gap, Fx jumpMultiplier)
        {
            return ReachDx(JumpSpeed(MaxSpeed), gap, jumpMultiplier);
        }

        /// <summary>Layout version of MaxReachDx (launch speed from LayoutJumpBase): the horizontal spread the generator allows between rows.</summary>
        public Fx LayoutMaxReachDx(Fx gap, Fx jumpMultiplier)
        {
            return ReachDx(LayoutJumpBase + LayoutJumpSpeedFactor * MaxSpeed, gap, jumpMultiplier);
        }

        private Fx ReachDx(Fx baseSpeed, Fx gap, Fx jumpMultiplier)
        {
            Fx v0 = baseSpeed * jumpMultiplier;
            Fx disc = v0 * v0 - Gravity * 2 * gap;
            if (disc.Raw < 0) return Fx.Zero;
            Fx t = (v0 + Fx.Sqrt(disc)) / Gravity;
            return MaxSpeed * t;
        }

        /// <summary>Endless ramp percent for a floor: +RampPercent per RampFloors, capped.</summary>
        public int RampPercentAt(int row)
        {
            int p = (row / RampFloors) * RampPercent;
            return p > RampCap ? RampCap : p;
        }
    }
}
