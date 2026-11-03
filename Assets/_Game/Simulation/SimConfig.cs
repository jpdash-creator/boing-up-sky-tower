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

        /// <summary>A copy of every tunable (the run applies power-up boosts to a copy, never to the shared default config).</summary>
        public SimConfig Clone()
        {
            var c = (SimConfig)MemberwiseClone();
            c.GemValues = (int[])GemValues.Clone();
            c.GemRarityPercent = (int[])GemRarityPercent.Clone();
            c.ClassCoinPercent = (int[])ClassCoinPercent.Clone();
            return c;
        }

        public Fx ScreenWidth = Fx.FromInt(10);
        public Fx PlayerHalfWidth = Fx.Half;
        /// <summary>Extra horizontal tolerance when landing on a platform edge.</summary>
        public Fx LandingMargin = Fx.FromRatio(1, 4);

        public Fx Gravity = Fx.FromRatio(40, 3600);          // 40 u/s^2
        public Fx Accel = Fx.FromRatio(30, 3600);            // 30 u/s^2
        public Fx AirControl = Fx.FromRatio(3, 5);           // air acceleration share
        /// <summary>Ground braking with no input = Accel x BrakeFactor (45 u/s2 for Momo; docs/29). Replaced the fixed Friction value.</summary>
        public Fx BrakeFactor = Fx.FromRatio(3, 2);
        public Fx MaxSpeed = Fx.FromRatio(9, 60);            // 9 u/s
        /// <summary>
        /// Standing jump launch speed. 19 u/s peaks at 4.51 u = 1.3 median row gaps (3.48 u); with NormalGapMax 4.2 a single standing
        /// jump always reaches the next platform. Full speed (19 + 0.41 x 9 = 22.69 u/s) peaks at 6.44 u = 1.85 median gaps.
        /// History: 14 u/s until 2026-10-20 (2.45 u), 21 u/s with factor 0.85 the same day (too high), 19 u/s with factor 0.58 (owner list 7), then factor 0.41.
        /// </summary>
        public Fx JumpBase = Fx.FromRatio(19, 60);           // 19 u/s
        /// <summary>
        /// Launch speed the level layouts are sized for (the pre-2026-10-20 value, 14 u/s). The generator and builder size gaps and reach
        /// from this, so raising JumpBase made the player stronger without stretching the levels (see LayoutMaxJumpHeight, LayoutMaxReachDx).
        /// </summary>
        /// <summary>Largest vertical gap the generator makes between neighbouring rows (every kind). Below the standing peak (4.51 u) by 0.3 u.</summary>
        public Fx NormalGapMax = Fx.FromRatio(42, 10);
        public Fx LayoutJumpBase = Fx.FromRatio(14, 60);
        /// <summary>Speed factor of the layout reference jump (the pre-2026-10-20 0.85). Layout full-speed launch is 21.65 u/s, below the real 22.69 u/s, so every generated dx is covered.</summary>
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
        /// <summary>A vanishing platform blinks for this many ticks before it disappears (CUX-7: nothing hurts without a warning).</summary>
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
        /// <summary>Chance, in per mille, that a gem spot (one per row of a campaign level) holds a gem. See <see cref="DifficultyGemPermille"/>.</summary>
        public int GemSpotPermille = 27;
        /// <summary>
        /// GemSpotPermille per difficulty (Relaxed, Easy, Normal, Hard, Insane). The owner asked for 2.5, 2.5, 3.5, 4.5, 5 percent (issue list 24 point 5).
        /// Measured with one spot per row: 2.5 percent places only about 62 gems per region (target 70 to 110) and 4.5 / 5 percent place 112 / 130,
        /// and a 2x spread cannot fit the 1.57x target band, so the shares are tuned to 3.2, 3.2, 3.5, 3.8 and 4.1 percent
        /// (78 to 103 placed gems per region on every difficulty). Rarity weights are unchanged.
        /// Owner issue list 26 (2026-10-31): 30 percent fewer gems in game, so the shares are x0.7 rounded: 2.2, 2.2, 2.5, 2.7 and 2.9 percent
        /// (about 55 to 75 placed gems per region on every difficulty).
        /// Doc 25 (owner list 27: levels of 60 to 150 rows, no gems on the combo levels 5 and 10, double gems on levels 8 and 15) re-tuned the shares, not the rarity
        /// weights, to 2.7, 2.7, 2.9, 3.0 and 3.2 percent: 56 to 73 placed gem value per region on every difficulty (target 55 to 75).
        /// </summary>
        public static readonly int[] DifficultyGemPermille = { 27, 27, 29, 30, 32 };
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

        // ---------------------------------------------------------------------------------------
        // Region 1 special platforms (doc 24, owner issue list 26). Ticks are 60 per second, speeds in u per tick.
        // ---------------------------------------------------------------------------------------

        /// <summary>Wind Petal: sideways push while standing on it (about 2.5 u/s), flips every WindFlipTicks (2.5 s); the cue shows for the last WindCueTicks (0.5 s) before a flip.</summary>
        public Fx WindPush = Fx.FromRatio(25, 600);
        public int WindFlipTicks = 150;
        public int WindCueTicks = 30;
        /// <summary>Wind Petals are at least this wide so the gust never blows a landed player straight off (u).</summary>
        public Fx WindMinWidth = Fx.FromInt(3);

        /// <summary>Bee Hive Lift: half amplitude of the vertical bob (peak to peak 2.4 u = about 0.8 of a 3 u row) and the full period (3 s).</summary>
        public Fx HiveBobAmplitude = Fx.FromRatio(12, 10);
        public int HivePeriodTicks = 180;
        /// <summary>Rows next to a hive keep their vertical gap in this range (u): at least HiveGapMin so the bobbing hive never touches the neighbour, at most NormalGapMax - amplitude so every bob height stays within a jump.</summary>
        public Fx HiveGapMin = Fx.FromRatio(26, 10);

        /// <summary>Snap Daisy: closes SnapCloseTicks (1.5 s) after landing, stays shut SnapReopenTicks (2 s), then opens again. The 3-step countdown ticks every SnapCloseTicks / 3.</summary>
        public int SnapCloseTicks = 90;
        public int SnapReopenTicks = 120;

        /// <summary>Dandelion Puff: launch speed is the normal jump speed times this (jump height x1.3) and the launch carries DandelionVx sideways (about 4 u over the flight).</summary>
        public Fx DandelionLaunch = Fx.FromRatio(114, 100);
        public Fx DandelionVx = Fx.FromRatio(72, 1000);
        /// <summary>Distance range (u, centre to centre) of the platform placed after a dandelion in the direction of its arrow.</summary>
        public Fx DandelionNextDxMin = Fx.FromRatio(22, 10);
        public Fx DandelionNextDxMax = Fx.FromRatio(38, 10);

        /// <summary>Thorn Bush: a landing (or step) on the thorny half, beyond ThornDeadZone from the centre, hops the player away from the thorns. Never damages, never ends the run.</summary>
        public Fx ThornKnockVx = Fx.FromRatio(13, 100);
        public Fx ThornKnockVy = Fx.FromRatio(12, 100);
        public Fx ThornDeadZone = Fx.FromRatio(15, 100);
        /// <summary>Thorn Bushes are at least this wide so the safe half is a fair landing spot (u).</summary>
        public Fx ThornMinWidth = Fx.FromInt(4);

        /// <summary>Ladybug Rider: runs along a short path; while the player stands on it the path advances LadybugBoostPercent of the normal speed (150 = 1.5x).</summary>
        public int LadybugBoostPercent = 150;
        public Fx LadybugAmplitudeMin = Fx.FromRatio(8, 10);
        public Fx LadybugAmplitudeMax = Fx.FromRatio(14, 10);
        public Fx LadybugMaxWidth = Fx.FromInt(4);

        /// <summary>Seesaw Leaf: slides the player toward the tilted edge (1.8 u/s); landings within SeesawDeadZone of the centre tilt toward the direction of travel.</summary>
        public Fx SeesawSlide = Fx.FromRatio(18, 600);
        public Fx SeesawDeadZone = Fx.FromRatio(2, 10);
        public Fx SeesawMinWidth = Fx.FromInt(3);

        // ---- Region 2 (Mushroom Grove) special platforms (doc 30 section 3) ----
        /// <summary>Squish Cap: sinks SquishDepth over SquishTicks (0.8 s) while stood on; a jump from a fully squashed cap is x1.45 (SquishJumpBonusPercent 45), a jump at once is a normal jump.</summary>
        public Fx SquishDepth = Fx.FromRatio(35, 100);
        public int SquishTicks = 48;
        public int SquishJumpBonusPercent = 45;
        /// <summary>Sprout Lift: rises SproutRise over SproutRiseTicks (1.2 s) while stood on, sinks back at SproutSinkPercent of the rise speed once empty (1.6 u/s at 120).</summary>
        public Fx SproutRise = Fx.FromRatio(16, 10);
        public int SproutRiseTicks = 72;
        public int SproutSinkPercent = 120;
        /// <summary>Puff Float: after a jump from it gravity is PuffGravityPercent while falling and the fall speed is at most PuffMaxFall, for PuffFloatTicks (1.2 s) of falling.</summary>
        public int PuffFloatTicks = 72;
        public int PuffGravityPercent = 45;
        public Fx PuffMaxFall = Fx.FromRatio(5, 60);
        /// <summary>Fairy Ring: half width of the hole (Platform.Amplitude) is drawn in this range; the ring is always the widest painted piece (FairyRingWidth).</summary>
        public Fx FairyHoleMin = Fx.FromRatio(7, 10);   // the painted hole is exactly 1.4 u wide (bake_region2_new_platforms.py)
        public Fx FairyHoleMax = Fx.FromRatio(7, 10);
        public Fx FairyRingWidth = Fx.FromInt(6);
        /// <summary>Orbit Cap: sideways reach range (Platform.Amplitude), vertical reach and the period of one lap (4.2 s).</summary>
        public Fx OrbitAmplitudeMin = Fx.FromRatio(7, 10);
        public Fx OrbitAmplitudeMax = Fx.FromRatio(9, 10);
        public Fx OrbitAmplitudeY = Fx.FromRatio(5, 10);
        public int OrbitPeriodTicks = 252;
        /// <summary>Rows next to an Orbit Cap keep their vertical gap in OrbitGapMin to NormalGapMax - OrbitAmplitudeY (u).</summary>
        public Fx OrbitGapMin = Fx.FromRatio(20, 10);
        /// <summary>Dizzy Spores: left/right input is ignored for this long after landing (1.0 s). Never inverted.</summary>
        public int DizzyLockTicks = 60;
        /// <summary>Spore Pop: swells for SporePopTicks (1.2 s) after the landing, then flings the player (2.0 u up, about 3 u toward Platform.Dir) if still on it; flat for SporePopFlatTicks (2.0 s).</summary>
        public int SporePopTicks = 72;
        public int SporePopFlatTicks = 120;
        public Fx SporePopVy = Fx.FromRatio(21, 100);
        public Fx SporePopVx = Fx.FromRatio(75, 1000);

        // ---- Region 3 (Sunny Beach) special platforms (doc 30 section 4) ----
        /// <summary>Surf Boost Wave: extra acceleration along Platform.Dir while stood on (45 u/s2; the player's own run is Accel = 30), capped at the normal top speed. At least SurfMinWidth wide.</summary>
        public Fx SurfAccel = Fx.FromRatio(45, 3600);
        public Fx SurfMinWidth = Fx.FromInt(4);
        /// <summary>Sandcastle Step: the raised half is SandcastleRise higher (Platform.Amplitude; 0.16 u = the painted riser, owner picked 0.9 u but the art is a low step, doc 30 section 4); walking down a step is a slide of SandcastleDropSpeed per tick (the player stays grounded). At least SandcastleMinWidth wide (two halves of 2 u).</summary>
        public Fx SandcastleRise = Fx.FromRatio(16, 100);
        public Fx SandcastleDropSpeed = Fx.FromRatio(1, 10);
        public Fx SandcastleMinWidth = Fx.FromInt(4);
        /// <summary>Clam Shell Pad: global clock, open for ClamOpenTicks of every ClamCycleTicks (open 1.2 s, shut 1.2 s); a landing while open launches x1.4 (ClamLaunch).</summary>
        public int ClamCycleTicks = 144;
        public int ClamOpenTicks = 72;
        public Fx ClamLaunch = Fx.FromRatio(14, 10);
        /// <summary>Sinking Raft: sinks RaftSinkDepth over RaftSinkTicks (1.5 s) while stood on, springs back at RaftSpringPercent of the sink speed once empty (300 = 2 u/s).</summary>
        public Fx RaftSinkDepth = Fx.One;
        public int RaftSinkTicks = 90;
        public int RaftSpringPercent = 300;
        /// <summary>Turtle Shuttle: one stop-and-go period (pause, glide, pause, glide back); a glide is TurtleGlideTicks long and covers 2 x Platform.Amplitude (2.4 u at the full 1.2 u).</summary>
        public int TurtlePeriodTicks = 264;
        public int TurtleGlideTicks = 60;
        public Fx TurtleAmplitude = Fx.FromRatio(12, 10);
        /// <summary>Kelp Tangle: no jump for KelpLockTicks after the landing; the jump input is kept (buffer) and fires the moment the lock ends.</summary>
        public int KelpLockTicks = 36;
        /// <summary>Pufferfish Pad: global cycle of PufferCycleTicks (calm until PufferSwellStart, swelling until PufferPuffStart, puffed after); landing or standing while puffed hops the player away.</summary>
        public int PufferCycleTicks = 180;
        public int PufferSwellStart = 96;
        public int PufferPuffStart = 132;
        public Fx PufferHopVy = Fx.FromRatio(12, 100);
        public Fx PufferHopVx = Fx.FromRatio(10, 100);
        public Fx PufferMinWidth = Fx.FromInt(3);

        // ---- Generator: how many rows carry a new special platform (percent of free rows), by campaign level ----
        /// <summary>Share of rows with a new special platform: SpecialShareLevel3Percent at level 3 rising linearly to SpecialShareLevel15Percent at level 15.</summary>
        public int SpecialShareLevel3Percent = 15;
        public int SpecialShareLevel15Percent = 55;
        /// <summary>
        /// Doc 25 (owner list 27): share of rows with a special platform, ramping INSIDE each level from the low value at the first rows to the high value at the
        /// last rows. Index = level 1..15 (index 0 unused): L1 10 to 30, L2 to 5 30 to 50, L6 to 14 40 to 60, L15 50 to 70 percent.
        /// </summary>
        public int[] RampLowPercent = { 0, 10, 30, 30, 30, 30, 40, 40, 40, 40, 40, 40, 40, 40, 40, 50 };
        public int[] RampHighPercent = { 0, 30, 50, 50, 50, 50, 60, 60, 60, 60, 60, 60, 60, 60, 60, 70 };
        /// <summary>Doc 25: platforms per row (percent): 130 platforms per 100 rows, the spine platform of each row plus planned row mates.</summary>
        public int PlatformsPerRowPercent = 130;
        /// <summary>Doc 25: levels with gems (8 and 15) make this percent of the gems double gems (worth DoubleGemValue). Tag 4 of a Gem pickup.</summary>
        public int DoubleGemPercent = 25;
        public int DoubleGemValue = 2;
        /// <summary>Gem pickup Tag of a double gem (tiers 0 to 3 are the rarities 1/2/5/10).</summary>
        public const int DoubleGemTag = 4;
        /// <summary>Level 8 only: the hidden bonus gem is a double gem with this percent chance.</summary>
        public int HiddenBonusDoublePercent = 30;
        /// <summary>Hazard-type specials (Thorn Bush, Snap Daisy) are rare early: at most one per this many rows up to level HazardEarlyLastLevel (owner decision).</summary>
        public int HazardEarlyRowsPerHazard = 6;
        public int HazardEarlyLastLevel = 6;
        /// <summary>Endless: the special share rises from level-3 to level-15 values over this many floors.</summary>
        public int EndlessSpecialRampFloors = 150;
        /// <summary>Endless: the region's new kinds phase in one by one, one more every this many floors (the signature platform and Crumble are there from the start).</summary>
        public int EndlessKindStepFloors = 10;

        // ---- Difficulty ramp (all regions): from level 3 the layout difficulty never falls below a floor that rises from level 3 to 15 ----
        public int RampFloorLevel3Permille = 300;
        public int RampFloorLevel15Permille = 1000;

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

        // Rework 2026-10-31 (owner): every tap ADDS the base time onto the remaining time (no maximum); every owned type is usable in a run.
        /// <summary>Flight: 10 s of flight per tap, rising at 8 u/s with normal air steering; gongs cannot hurt; 1 s of slow fall after it.</summary>
        public int FlightTicks = 600;
        public Fx FlightRise = Fx.FromRatio(8, 60);
        public int FlightGraceTicks = 60;
        /// <summary>Rocket: per tap an auto-climb of RocketFloors rows in RocketTicks (4 s), no steering; gongs cannot hurt; 1 s of slow fall after it.</summary>
        public int RocketTicks = 240;
        public int RocketFloors = 20;
        public int RocketSlowFallTicks = 60;
        /// <summary>Flight and Rocket stop this many rows under the top row of a campaign level, so they can never skip the finish.</summary>
        public int PowerFlightRowsBelowTop = 3;
        /// <summary>Slow fall after Flight/Rocket: gravity is scaled to this percent and the fall speed is capped.</summary>
        public int SlowFallGravityPercent = 35;
        public Fx SlowFallMaxSpeed = Fx.FromRatio(6, 60);
        /// <summary>Double Jump (timed): 30 s per tap in which every air phase has one extra standing-jump-strength air jump.</summary>
        public int DoubleJumpWindowTicks = 1800;
        /// <summary>Wall Stick: one tap enables sticking for 30 s; each stick holds up to 1.5 s; a wall jump launches away from the wall.</summary>
        public int WallStickWindowTicks = 1800;
        public int WallHoldTicks = 90;
        public Fx WallJumpVx = Fx.FromRatio(6, 60);
        /// <summary>Ticks after leaving a wall before it can hold again.</summary>
        public int WallStickCooldownTicks = 15;
        /// <summary>Shield: 60 s per tap during which gongs cannot hurt at all (no knockback, no hit).</summary>
        public int ShieldTicks = 3600;
        /// <summary>Magnet: 30 s per tap; every pickup counts as this much larger (a star then collects from about 3 u).</summary>
        public int MagnetTicks = 1800;
        public Fx MagnetExtraRadius = Fx.FromInt(2);
        /// <summary>Slow Time: 30 s per tap with the auto-scroll (kill line) exactly this percent slower.</summary>
        public int SlowTimeTicks = 1800;
        public int SlowTimePercent = 50;

        /// <summary>HERO POWERS (owner 2026-11-02): a hero power is the same effect as the bought power-up of its type for this percent of the duration (HeroPowerDurationFactor = 0.6). Integer percent, so the simulation stays deterministic.</summary>
        public int HeroPowerDurationPercent = 60;
        /// <summary>The same factor as a float, for docs, UI text and tests (equal to HeroPowerDurationPercent / 100).</summary>
        public const float HeroPowerDurationFactor = 0.6f;

        /// <summary>Ticks of a hero power for a bought duration: the percent of it, rounded half up, at least 1.</summary>
        public int HeroTicks(int baseTicks)
        {
            long t = ((long)baseTicks * HeroPowerDurationPercent + 50) / 100;
            return t < 1 ? 1 : (t > int.MaxValue ? int.MaxValue : (int)t);
        }

        /// <summary>Floors of a hero Rocket (same climb speed as the bought one): the percent of RocketFloors, at least 1.</summary>
        public int HeroRocketFloors { get { int f = (RocketFloors * HeroPowerDurationPercent + 50) / 100; return f < 1 ? 1 : f; } }

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
