# 30. Regions 2 to 5: 28 new region platforms (design proposal, 2026-11-01)

Status: PROPOSAL for the owner. **Region 2 (section 3) is IMPLEMENTED (2026-11-01, owner list 30; see section 12 for what the code does differently from this proposal); regions 3 to 5 are not.** No OpenArt tool used, no credits spent. Follows doc 24 (region 1 set), doc 25 (level structure and share ramps) and the real code (`Assets/_Game/Simulation/`: `Platform.cs`, `SimConfig.cs`, `Sim.cs`, `SpecialPlatforms.cs`, `LevelSpec.cs` `RegionPlatformSets`, `LevelBuilder.cs`, `EndlessTower.cs`, `LevelBot.cs`). Effort numbers are my estimates, not measurements.

## 1. Ground rules

Owner decision 2026-11-01: there are NO code-drawn telegraph cues any more (no drawn arrows, rings, warning icons, speed lines). Consequences for every kind below:
- The behaviour must be readable from (a) the painted shape of the platform (static art), (b) the platform's own motion (the view reads the Sim position, as it already does for Ladybug and Hive) and (c) painted pose frames chosen by the Sim state (for example open or shut, calm or puffed). Pose frames are art, not code cues, but each costs an extra painted piece; the table says which kinds need them.
- Every kind must stay fair: it never kills (the only death stays the kill line), no unavoidable damage (a player who reacts to what is visible can always avoid the cost), the bot (`LevelBot`) solves every random layout, the Sim stays integer and deterministic.
- Placement rules that already exist and apply unchanged (`SpecialPlatforms.cs`): level 1 has no hazard types; level 2 gets 2 safe + 1 hazard special; no two hazard-type kinds in adjacent rows; no hazard right after a launcher; hazards at least 6 rows apart up to level 6; no launcher after a launcher; first special row is row 3 (level 1) or 4 (later levels), so the first 2 rows are always plain; the last 3 rows before the goal and gem row +-2 take no launcher and no hazard; the special share ramps inside each level per doc 25 (10-30 percent at level 1 up to 50-70 percent at level 15).
- New rules proposed here (section 8): a "demanding" class for the hard specials so two of them never touch, and the same last-3-rows exclusion for them.

Row types. "Helpful" = makes a jump easier or safer; "hard" = needs timing or precision but costs nothing besides a missed jump; "hazard" = costs the player something small if used badly (control, time, position), never a death, always avoidable by what is visible. Hazards go into `PlatformKinds.IsHazardType` so all hazard spacing rules apply to them automatically.

Art sheet grouping per region (OpenArt, one approval packet per region later): sheet 1 = the 3 helpful kinds, sheet 2 = the 2 hard kinds, sheet 3 = the 2 hazard kinds (3 + 2 + 2, as the owner asked).

## 2. How the 7 region 1 kinds were built (to estimate effort)

Commit e679a37 (v0.37) added the 7 kinds plus ramp, endless and art in one change (149 files, about 10,100 lines, most of it art pieces, Sim.cs +500 and the 727-line test file). Files a new kind touches:

| # | File | What changes for a new kind | Needed by |
|---|---|---|---|
| 1 | `Simulation/Platform.cs` | enum value; add to `IsHazardType` / `IsLaunch` / `IsTimeDependent` | every kind |
| 2 | `Simulation/SimConfig.cs` | the numbers (ticks, u, u per tick) | every kind |
| 3 | `Simulation/Sim.cs` | `LandOnNewPlatform` (landing effect), `CarryOnNewPlatform` (per-tick effect while grounded), `PlatformX/PlatformY` (own motion), `IsSolid`, jump hook (line ~535, where Sticky scales `v0`), gravity block (line ~560), `SimState` fields | every kind |
| 4 | `Simulation/SpecialPlatforms.cs` | `ConstrainWidth`, `ConstrainGap`, `Params` (Dir, Phase, Amplitude, Period draw), `Plan` rules | every kind |
| 5 | `Simulation/LevelSpec.cs` | `RegionPlatformSets.For` (the kind list per signature) | per region, once |
| 6 | `Simulation/LevelBuilder.cs` and `EndlessTower.cs` | only where the kind has a geometry rule (next platform position after a Dandelion, hive gap, amplitude edge) | some kinds |
| 7 | `Simulation/LevelBot.cs`, `Gameplay/AutopilotPlanner.cs`, `AutopilotReport.cs` | time-dependent keying, waits, launcher handling, report letter | time-based and launch kinds |
| 8 | `Gameplay/RunView.cs` (+ `SpecialPlatformFx.cs`, which the owner decision removes: the view then only reads Sim positions and picks pose frames) | per-frame position or pose from the Sim | moving, posed kinds |
| 9 | `Gameplay/ArtCatalog.cs`, `Editor/ArtCatalog/ArtCatalogBuilder.cs`, `Resources/ArtCatalog.asset` | bind the baked pieces to the kind | every kind |
| 10 | `Tools/art_pipeline/bake_region<N>_new_platforms.py` (copy of `bake_region1_new_platforms.py`) | cut the sheet rows into baked widths | per region (+ per kind rows) |
| 11 | `Tests/Simulation/Region<N>PlatformTests.cs` (copy of `Region1PlatformTests.cs`) | one test per kind plus placement rules, determinism, share ramp | every kind |
| 12 | goldens | `Content/golden/level_hashes_r1-5.txt` (the real file name; it is ONE file for all five regions, not one per region) and the literal hashes in `GoldenHashTests` / `EndlessTests` | per region |

The per-kind effort estimates below use: Sim (rows 1-4), Build (rows 4 and 6), View (rows 8-9 and the kind's share of 10), Tests (row 11), Bot (row 7 plus the proof runs over levels 2-15, 20+ seeds). Shared per-region work is listed once in section 7.

## 3. Region 2: Mushroom Grove (signature: bouncy caps)

Palette and theme: purple and teal (content JSON), with glow-cyan highlights, moss green and warm amber lamp spots; a dim forest floor lit from within; fireflies, dew, gills, spore dust. Existing signature: `BounceCap` (launch x1.8, most common kind). Generic kinds reused: Crumble (rotting log) and Slippery (dew moss), one extra option each; Moving, Vanishing, Sticky and plain Bounce are dropped (the cap is the bounce of this region).

Mechanic families used here: charge jump, one-shot rise, slow fall, hole, orbit, steering lock, delayed fling.

| # | Name | Type | What it does (numbers) | How the Sim implements it | Discoverable from |
|---|---|---|---|---|---|
| 2.1 | Squish Cap | helpful | The cap sinks while you stand on it: up to 0.35 u over 0.8 s (48 ticks). Jumping from a fully squashed cap launches x1.45 (about 2.1x the height); jumping at once is a normal jump. Never worse than normal. | New kind. Squash = min(tick - `LandTick`, 48)/48 x `SquishDepth` applied in `CarryOnNewPlatform` (like the Hive bob but one-way); jump hook multiplies `v0` by 1 + 0.45 x squash. No new state beyond `LandTick`. Next gap follows the normal cap (4.2 u); the charge is a bonus, never required. | The cap visibly sinks when you stand; painted rubbery cap |
| 2.2 | Sprout Lift | helpful | A sprout platform that rises 1.6 u in 1.2 s after you land, stays up while you stand, sinks back 1.6 u/s once empty. Gives a free half-row of height. | New kind. Rise clock in `CrumbleTick[id]` (landing tick, as Snap Daisy) and `PlatformY(id, tick, crumbleTick)` (own overload, like Ladybug's X). Grounded player rides it (`s.Y = PlatformY`). Landing search widened by the rise (`bobMargin` already exists for the hive; extend it to max(hive, sprout)). | Painted stem and bud under the cap; it visibly grows once you land |
| 2.3 | Puff Float | helpful | After you jump from it, falling is slowed (gravity 45 percent, fall speed at most 5 u/s) for 1.2 s (72 ticks): more time to steer onto the next platform. Rising is normal, no extra height. | New kind. New `SimState.FloatTicks` set in the jump hook when the ground kind is Puff Float; in the gravity block, if `FloatTicks > 0 && Vy < 0` use the existing slow-fall maths (`SlowFallGravityPercent` style) with its own constants. Counts down in the tick epilogue. | Painted fluffy puffball with spore dust trails above it |
| 2.4 | Fairy Ring | hard | A wide cap (at least 4.4 u) with a 1.2 to 1.6 u hole in the middle. You must land on one of the two halves (each at least 1.4 u wide). Stepping into the hole drops you. Static. | New kind. `Amplitude` = hole half width, `Dir` unused. Landing test: reject when `abs(x - p.X) < Amplitude` (also in the grounded walk-off test). Hole drawn in the art (two caps with a gap, one sprite). | The gap is painted in the platform |
| 2.5 | Orbit Cap | hard | A cap that travels a small ellipse: 0.9 u sideways, 0.5 u up and down, period 4.2 s (252 ticks), always the same direction. | New kind. `PlatformX` = base + `Amplitude` x cos-table(phase), `PlatformY` = base + 0.5 x sin-table(phase) (16-entry integer quarter table, no `System.Math`). Reuses the widened landing search (bob 0.5). Builder treats the base as the reach anchor like Moving (`edge = w/2 + Amplitude`). | Visible circular motion |
| 2.6 | Dizzy Spores | hazard | A spotted toadstool that bursts a purple spore puff when you land: for 1.0 s (60 ticks) left/right input is ignored (momentum and friction still work). You can jump at once; you only lose steering. | New kind (hazard type). New `SimState.SteerLockTicks` set in `LandOnNewPlatform`; in the input stage `dir = 0` while it runs. Shares the field with 4.2 (Floss Glide uses the same counter with a scale). Never inverts controls (accessibility). | Painted swirl spore cloud around the cap |
| 2.7 | Spore Pop | hazard | A puffball that swells for 1.2 s (72 ticks) after you land, then pops and flings you 2.0 u up and about 3 u toward its Dir (the middle of the screen), then stays flat for 2.0 s. Leave before it pops and nothing happens. | New kind (hazard type AND launch-like for placement). Timer in `CrumbleTick` like Snap Daisy; at tick landing+72 and player still grounded on it: `Vy = 0.10`, `Vx = 0.075 x Dir`. `SpecialPlatforms.DandelionNextX` rule reused so the next row is within reach of the fling. 3 painted poses (calm, swollen, flat). | The swelling is painted in 3 poses and chosen by Sim state |

Art lines (usable as sheet text; PREFIX/SUFFIX as in T61, region name 'Mushroom Grove', palette purple, teal, glow cyan):
- Sheet 1: Squish Cap = a glossy purple toadstool cap slab with soft rubbery white spots and a cyan underglow; Sprout Lift = a teal leaf-cap slab on top of a thin curled green sprout stem, small bud at the base; Puff Float = a pale lilac puffball slab, fuzzy on top, a few drifting spore dots.
- Sheet 2: Fairy Ring = two matching purple mushroom caps side by side with a clear empty gap between them, a ring of tiny white mushrooms under the gap; Orbit Cap = a teal cap slab with a round glowing ring (orbit trace) painted behind it.
- Sheet 3: Dizzy Spores = a red-brown toadstool cap with yellow-green swirl spore clouds around it; Spore Pop = a round warm-pink puffball slab in three poses: calm, swollen with a cracked skin, flat and wrinkled.

| # | Sim | Build | View | Tests | Bot | Total h | Notes |
|---|---|---|---|---|---|---|---|
| 2.1 | 3 | 0.5 | 1.5 | 1.5 | 1 | 7.5 | simple |
| 2.2 | 4 | 1.5 | 2 | 2 | 1.5 | 11 | gap rule: clearance to the row above while risen |
| 2.3 | 3 | 0.5 | 0.5 | 1.5 | 1 | 6.5 | |
| 2.4 | 3 | 1.5 | 0.5 | 1.5 | 1 | 7.5 | min width 4.4, next-row dx from the half the player lands on |
| 2.5 | 4 | 2 | 1 | 2 | 2.5 | 11.5 | time dependent: bot keys by clock |
| 2.6 | 2.5 | 0.5 | 0.5 | 1.5 | 1.5 | 6.5 | bot's air steering must wait out the lock |
| 2.7 | 4 | 2 | 2 | 2 | 2.5 | 12.5 | needs 3 pose pieces |
| Sum | | | | | | 63 | + region work 17.5 = about 80.5 h |

Risks R2:
- Spore Pop and BounceCap are both launch-like; `launchBudget` (share of options) must count Spore Pop, otherwise caps plus pops make a quarter of all specials uncontrolled launches. BounceCap already needs `BounceNextGapMax` / `BounceNextDxMax`; Spore Pop needs the same.
- Sprout Lift rising 1.6 u shrinks the gap to the row above to as little as 1.0 u; the gap to the next row must stay at least 2.6 u (hive rule) so the lifted player never passes through the next platform.
- Dizzy Spores steering lock after a BounceCap launch is not possible (a hazard never follows a launcher, existing rule), good.
- Golden: region 2 lines in `level_hashes_r1-5.txt` change completely (new kinds replace the generic mix); the region 2 audit test (`Region2AuditTests`) and `OtherRegionsGetNoRegion1Platforms` need updating.

## 4. Region 3: Sunny Beach (signature: drifting platforms)

Palette and theme: gold and aqua (content JSON), white foam, coral pink, sandy beige, a bright noon sun; driftwood, shells, palm, buoys, tide pools. Existing signature: drifting platform (`Moving`, amplitude 1.5 to 3 u, width at most 4 u). Generic kinds reused: Crumble (dry sand slab) and Vanishing (sea foam); plain Bounce, Sticky and Slippery are dropped. The drifting platforms stay the most common kind.

Mechanic families used here: speed strip, step-up ledge, timed pad, weight sink, stop-and-go mover, jump lock, inflating spikes.

| # | Name | Type | What it does (numbers) | How the Sim implements it | Discoverable from |
|---|---|---|---|---|---|
| 3.1 | Surf Boost Wave | helpful | A wave-curl slab, at least 4 u wide. While you stand on it you are accelerated along its Dir at 45 u/s squared (your own run is 30), so you reach full run speed in about 0.4 s. A full-speed jump is 6.44 u high and reaches far. Run against it to hold still. | New kind. `CarryOnNewPlatform`: `s.Vx` += `SurfAccel x Dir` capped at `MaxSpeed`. Dir points toward the middle of the screen (like the Dandelion). Builder may use the full-speed reach (`LayoutMaxReachDx`) for the next row. | Painted wave curling in one direction |
| 3.2 | Sandcastle Step | helpful | A two-level sand block: one half is 0.9 u higher. Walking from the low half into the high half steps up automatically; you can land on either half. Free height. | New kind. `Amplitude` = riser height (0.9), `Dir` = side of the raised half. `PlatformSurfaceY(id, x)`; landing uses the surface at the player's x; the grounded walk raises `s.Y` when the surface at the new x is higher by at most 0.9 and drops it (becomes airborne with coyote) when lower. Width at least 4.4 so each half is at least 2.2. | The step is painted in the platform |
| 3.3 | Clam Shell Pad | helpful | A giant clam that opens and shuts every 2.4 s (144 ticks): open for 1.2 s, shut for 1.2 s. Landing on an open clam launches x1.4 (like a pad), landing on a shut clam is a plain platform. | New kind (launch type). Global clock `(tick + Phase) % 144 < 72` = open; `LandOnNewPlatform` applies `v0 x 1.4` only when open. Placement as a launcher (no launcher after a launcher, next gap/dx caps as Bounce). 2 painted poses (open, shut) chosen by the Sim. | The hinge opens and shuts, shown in the 2 painted poses |
| 3.4 | Sinking Raft | hard | A driftwood raft that sinks 1.0 u in 1.5 s (0.67 u/s) while you stand on it and springs back 2 u/s once empty. You have to jump within about a second and a half. | New kind. Sink = min(grounded ticks, 90) / 90 x 1.0, grounded ticks from `LandTick`; release clock in `CrumbleTick`. Gap to the next row clamped to 3.2 u (4.2 - 1.0) like the hive rule, and the gap below to at least 2.6 u. | Raft visibly sinks when you stand |
| 3.5 | Turtle Shuttle | hard | A sea turtle shell that swims stop-and-go: pause 1.2 s, glide 2.4 u in 1.0 s, pause 1.2 s, glide back. Differs from smooth drift by its pauses. | New kind. Piecewise path of period 264 ticks in `PlatformX` (4 phases, integer lerp with ease). Same reach anchor rule as Moving. | Visible stop-and-go motion; painted turtle head and flippers |
| 3.6 | Kelp Tangle | hazard | Sticky green kelp: after you land you cannot jump for 0.6 s (36 ticks); the jump input is kept (buffer) and fires the moment it ends. | New kind (hazard type). New `SimState.JumpLockTicks`; the jump block is skipped while it runs and the jump buffer is held (`JumpBufferTicks` extended to 36 for this case). Shares the "control lock" family with Dizzy Spores. | Painted tangled strands that wrap the slab |
| 3.7 | Pufferfish Pad | hazard | A pufferfish slab: calm 1.6 s (96 ticks), swelling 0.6 s (36 ticks), puffed and spiky 0.8 s (48 ticks), cycle 3.0 s. Landing or standing while puffed hops you 1.2 u up and 1.7 u away from its centre (no landing counted, never a hit). Calm and swelling are safe. | New kind (hazard type). Cycle clock `(tick + Phase) % 180`; puffed = ticks 132..179; on landing or while grounded and puffed: `KnockFromThorns`-style hop (`Vy 0.12`, `Vx 0.10 x away`). 3 painted poses (calm, swelling, puffed). | The fish swells in the poses; spikes only show when puffed |

Art lines (palette gold, aqua, white foam, coral):
- Sheet 1: Surf Boost Wave = a long aqua wave slab curling forward at its front tip with white foam streaks along its length, all pointing one way; Sandcastle Step = a sand-coloured block with two levels, left half 0.9 thick higher, tiny shell decoration, flat tops; Clam Shell Pad = a big pink-white clam in two poses (shut slab, open slab with a pearl).
- Sheet 2: Sinking Raft = a lashed driftwood plank raft with rope ties, slightly water-logged at one end; Turtle Shuttle = a green turtle shell slab with a head, front flipper and tail visible at the ends.
- Sheet 3: Kelp Tangle = a slab wrapped in dark green kelp strands and a bubble; Pufferfish Pad = a yellow pufferfish as a slab in three poses: calm, swelling, fully puffed with short spikes.

| # | Sim | Build | View | Tests | Bot | Total h | Notes |
|---|---|---|---|---|---|---|---|
| 3.1 | 2 | 1 | 0.5 | 1.5 | 1.5 | 6.5 | bot must plan the speed it jumps with |
| 3.2 | 5 | 1 | 1 | 2 | 1.5 | 10.5 | surface-by-x is the only new Sim concept |
| 3.3 | 3.5 | 1 | 2 | 1.5 | 2 | 10 | bot waits for open phase |
| 3.4 | 3 | 1.5 | 1 | 2 | 2 | 9.5 | |
| 3.5 | 3 | 1 | 1 | 1.5 | 2 | 8.5 | |
| 3.6 | 2.5 | 0.5 | 1 | 1.5 | 1.5 | 7 | |
| 3.7 | 3.5 | 1 | 2 | 2 | 2.5 | 11 | 3 poses |
| Sum | | | | | | 63 | + region work 17.5 = about 80.5 h |

Risks R3:
- Many mover kinds in one region (drift, Turtle Shuttle, Sinking Raft, Surf Boost): the bot's time-key (`(Tick/20) % 24`) must cover the new period 264; hazard and demanding rule keeps them apart.
- Clam Shell Pad is a timed launcher: if the owner finds the uncontrolled launch unfair when the shell snaps open, make the clam launch only on a jump press while open (becomes a charge pad); costs +2 h.
- Surf Boost Wave next to a wall: the boost can carry a landing player into the wall (wall bounce goal in level 7 `noWallBounce`): never in level 7 (add to the level 7 exclusion list) and Dir always toward the middle.
- Region 3 endless golden (`EndlessTests` region 3, seed 12345) changes.

## 5. Region 4: Candy Canyon (signature: sticky gum)

Palette and theme: pink and mint (content JSON), cream, caramel brown, strawberry red, sprinkles; frosting cliffs, lollipop trees, jelly pools. Existing signature: `Sticky` (jump x0.7, next gap at most 1.8 u). Generic kinds reused: Crumble (sugar crumble, doc 02) and Slippery (icing); Moving, Vanishing and Bounce are dropped. NB the sticky platform shortens the next gap to 1.8 u; every new kind that follows a sticky one inherits that cap (existing rule).

Mechanic families used here: centring bowl, air steering, air zones (updraft and gust), treadmill, width breathing, landing-triggered shake.

| # | Name | Type | What it does (numbers) | How the Sim implements it | Discoverable from |
|---|---|---|---|---|---|
| 4.1 | Candy Bowl | helpful | A dish-shaped slab (4 to 8 u) with raised lips. Outside the centre 55 percent of the width, a gentle pull (1.5 u/s) moves you back toward the middle, so you cannot be carried off a lip by momentum. | New kind. `CarryOnNewPlatform`: if `abs(x - p.X) > 0.55 x width/2`, `s.X` += 1.5 u/s toward centre (1.5/60 per tick). Walk-off test unchanged (you can still leave on purpose by jumping). | Painted concave dish with lips |
| 4.2 | Floss Glide | helpful | After you jump off this cotton-candy cloud your air steering is doubled for 1.5 s (90 ticks). | New kind. `SimState.AirControlTicks`; in the acceleration block `accel = accel * AirControl * 2` while it runs. Same field family as Dizzy Spores (scale 0 versus 2). | Fluffy pink cloud slab with trailing wisps |
| 4.3 | Fizz Vent | helpful | A soda-bubble vent. In the column 1.8 u wide above it, up to 3.2 u high, gravity is cut by 80 percent while you are in the air: a jump from it climbs about 2.5 u higher (7.0 u instead of 4.5 u), a jump through it from below is lifted too. | New kind with an "air zone" (shared infra with 4.7). In the air-movement block: if `abs(x - p.X) < 0.9` and `0 < y - p.Y < 3.2` for any vent in the local window (platform scan already exists for landing), use `Vy -= Gravity x 0.2`. Builder keeps other platforms of the same row out of the column. | Painted soda bubbles rising in a column above the vent (part of the art, tall piece) |
| 4.4 | Licorice Belt | hard | A striped licorice conveyor, at least 4 u wide. While you stand on it you are carried 2.0 u/s along its Dir, which points AWAY from the next platform (so you must run against it or jump at once). | New kind. `CarryOnNewPlatform`: `s.X` += `BeltSpeed x Dir`. Builder sets Dir from the side of the next platform (like Dandelion but opposite). Fixed direction and speed; Wind Petal (region 1) flips and is weaker. | Painted tread pattern in 2 loop frames (belt moves) |
| 4.5 | Taffy Stretch | hard | A taffy slab that breathes: width pulses between 2.4 and 5.2 u with period 4.5 s (270 ticks), edges move 0.6 u/s. Land when it is wide; if it shrinks under your feet you simply walk inward or fall. | New kind. `Platform.Width` becomes a function `WidthAt(id, tick)` used in landing and walk-off tests (a new helper, like `PlatformX`). Reach and builder use the MINIMUM width. View: left cap, tiled middle, right cap (the pipeline's 'never stretch' rule; see risk). | Visible stretching motion |
| 4.6 | Jelly Wobble | hazard | A jelly cube. When you land it wobbles sideways +-0.6 u at 3 Hz, fading over 1.5 s (90 ticks), carrying you with it; a wobble can shake an edge-standing player off. Stand in the middle (width at least 3.5) or jump at once. | New kind (hazard type). Landing tick in `CrumbleTick`; `PlatformX` adds a decaying triangle wave after the landing (like the Ladybug boost state). | The jelly visibly jiggles after you land |
| 4.7 | Pinwheel Gust | hazard | A pinwheel lollipop. Airborne players in a column 2.0 u wide and 3.5 u high above it are pushed 3.0 u/s along its Dir (toward the screen middle). You can steer against it (air accel is 18 u/s squared). | New kind (hazard type), uses the same air zone code as 4.3. | The pinwheel spins (2 painted loop frames) and its swirl lines point one way |

Art lines (palette pink, mint, cream, caramel):
- Sheet 1: Candy Bowl = a pink-and-white striped dish slab with curled-up ends and sprinkles inside; Floss Glide = a puffy pastel pink cotton-candy cloud slab with a swirl wisp at each end; Fizz Vent = a mint soda-bottle-cap slab with a cluster of bubbles above (a tall piece: slab plus a faint column of bubbles).
- Sheet 2: Licorice Belt = a black-and-red licorice conveyor slab with diagonal tread stripes and two end rollers, in two frames; Taffy Stretch = a golden pulled-taffy slab with twisted ends, thin in the middle.
- Sheet 3: Jelly Wobble = a translucent strawberry jelly block slab with a cherry on top, glossy; Pinwheel Gust = a rainbow-swirl lollipop pinwheel on a short stick, slab base of white candy.

| # | Sim | Build | View | Tests | Bot | Total h | Notes |
|---|---|---|---|---|---|---|---|
| 4.1 | 2.5 | 0.5 | 0.5 | 1.5 | 1 | 6 | |
| 4.2 | 2.5 | 0.5 | 0.5 | 1.5 | 1.5 | 6.5 | |
| 4.3 | 6 | 1.5 | 1.5 | 2 | 2 | 13 | builds the air-zone infra |
| 4.4 | 2 | 1.5 | 1 | 1.5 | 1.5 | 7.5 | |
| 4.5 | 3 | 1 | 3 | 2 | 2 | 11 | 3-slice rendering |
| 4.6 | 3 | 1 | 1.5 | 2 | 2 | 9.5 | |
| 4.7 | 3.5 | 1.5 | 1.5 | 2 | 2.5 | 11 | reuses the air zone of 4.3 |
| Sum | | | | | | 64.5 | + region work 17.5 = about 82 h |

Risks R4:
- Sticky signature + Pinwheel Gust + Taffy Stretch all reduce control: keep Sticky and any hazard from adjacent rows (hazard rule does that for Jelly and Pinwheel; add Sticky to the 'demanding' neighbours of Taffy).
- Air zones change trajectories of jumps from OTHER rows passing through the column; the builder must place the next platform within reach of both the plain and the gusted path, or keep neighbouring rows out of the column; the bot proof over 20+ seeds per level is the check.
- Taffy Stretch conflicts with the 'platforms are never stretched' art rule (commit 55bc9df); either a 3-slice renderer (accepted in the estimate) or four baked widths with a cross-fade. Owner question 6.
- Region 4 test `Region4Tests` (sticky gap 1.8) must stay green; its golden hashes change.

## 6. Region 5: Bamboo Temple (signature: swinging gongs)

Palette and theme: jade and red (content JSON), lacquer red, gold, warm paper-lantern orange, stone grey, bamboo green; incense haze, pagoda roofs, torii, koi pond. Existing signature: swinging gongs, which are Hazard OBJECTS in the corridor between rows (`Hazard`/`HazardKind.Gong`, `GongDensity` 6 to 30 percent of rows from level 3), not a platform kind. So this region has NO signature platform kind; the 7 new kinds fill the list together with two generics: Vanishing (paper screen) and Moving (rope bridge); Crumble, Slippery, Sticky and Bounce are dropped.

Mechanic families used here: gong freeze, pole grab, long horizontal flick, pendulum, roof slide-off, timed smash zone, rolling object.

| # | Name | Type | What it does (numbers) | How the Sim implements it | Discoverable from |
|---|---|---|---|---|---|
| 5.1 | Muffle Drum | helpful | Landing on the drum silences every gong for 3.0 s (180 ticks): they hang still where they are (they stay a hazard only if you touch them, as now), then swing on. | New kind. `SimState.GongFreezeTicks`, and a frozen-clock offset `GongLagTicks` subtracted in `HazardPosition(id, tick)` (all callers: `Sim.HitHazards`, `RunView`, `LevelBot`). Does not touch `Invuln` or `ShieldTicks`. | The gongs visibly stop swinging |
| 5.2 | Climbing Pole | helpful | A bamboo pole (2.6 u tall) planted on a small stone base (1.6 u). Touch the pole while airborne AND steering into it: you grab it (hold up to 1.5 s, no gravity); jump to leave straight up with standing-jump strength. Never grabbed by accident. | New kind. Base = normal platform; pole = grab zone at `p.X`. Reuses the Wall Stick state machine (`WallHold`, `StartWallHold`, `WallCool`) with the pole as the wall; condition `dir toward pole`. Same-row platforms stay 1.6 u clear of the pole. | Tall painted pole with a base |
| 5.3 | Bamboo Flick | helpful | A bent bamboo stalk with a flat seat. Landing flicks you 0.8 jump high and 8 u/s sideways toward its Dir (the screen middle), about 6 to 7 u of travel: reaches the far side in one go. | New kind (launch type). `LandOnNewPlatform`: `Vy = JumpSpeed x 0.8`, `Vx = 0.133 x Dir`. Builder puts the next platform 5.5 to 7 u away in the Dir direction (same function as `DandelionNextX`, larger distance). 2 painted poses (bent, snapped). | The stalk is painted bent toward its Dir |
| 5.4 | Swing Plank | hard | A plank hung on two ropes that swings like a pendulum: 3.0 u sideways, rising 0.6 u at the ends, period 4.4 s (264 ticks), slow at the ends and fast in the middle. | New kind. `PlatformX` = anchor + `Amplitude` x sin-table(phase) (integer quarter-wave table), `PlatformY` = anchor + arc (the same table squared). Landing search widened (bob 0.6). Reach anchored on the middle. | Visible pendulum swing, ropes painted |
| 5.5 | Pagoda Ridge | hard | A pagoda roof ridge: only the middle 1.0 u is flat; the sloping sides slide you outward at 2.4 u/s. Land in the middle or jump at once. | New kind. `CarryOnNewPlatform`: if `abs(x - p.X) > 0.5`, `s.X` += 2.4 u/s outward (sign of dx). Width 3 to 6. | Painted roof shape (inverse of the Candy Bowl) |
| 5.6 | Mallet Smash | hazard | A temple mallet at one end of the platform (the side is its Dir): it rises slowly for 1.0 s, smashes down in 0.15 s, rests 1.3 s (cycle 2.5 s, 150 ticks). The smash covers a 1.6 u zone; standing or landing there at the smash moment knocks you 3 u sideways and 0.7 u up. The rest of the platform (at least 2.9 u) is always safe. | New kind (hazard type). Cycle `(tick + Phase) % 150`; smash window ticks 60..69; zone `abs(x - zoneX) < 0.8`; hit: `KnockFromThorns`-style (invulnerable for 60 ticks so it cannot chain). Mallet arm is a separate painted sprite rotated by the Sim phase (motion, not a code cue). | The mallet arm visibly lifts and falls |
| 5.7 | Rolling Log | hazard | A log rolls back and forth along the platform (width at least 5 u) at 2.4 u/s, 1.2 u wide. Touching it from the side bumps you 1.0 u up and 2 u forward; jumping over it is safe. | New kind (hazard type). Log x = triangle wave over the platform width (period from width); contact when grounded and `abs(x - logX) < 0.7`, or when the player's feet are lower than 0.6 u above the platform. Invulnerable 60 ticks after a bump. | Visible rolling log with painted grain, platform with a groove |

Art lines (palette jade, red, gold, stone):
- Sheet 1: Muffle Drum = a round red-lacquer drum slab with gold studs and a soft felt mallet resting on it; Climbing Pole = a thick green bamboo pole standing on a small stone base slab, tall piece; Bamboo Flick = a bamboo stalk bent over like a spring with a flat seat at the tip, drawn in two poses (bent, flung).
- Sheet 2: Swing Plank = a wooden plank hanging from two red ropes tied to a short crossbar (rope height as a tall piece); Pagoda Ridge = a grey-tiled pagoda roof slab with an upturned ridge and curved eaves.
- Sheet 3: Mallet Smash = a stone platform with a wooden mallet on a hinged arm at one end (arm as a separate piece, two poses); Rolling Log = a stone platform with a shallow groove and a round bamboo log with rings on it.

| # | Sim | Build | View | Tests | Bot | Total h | Notes |
|---|---|---|---|---|---|---|---|
| 5.1 | 3 | 0.5 | 1 | 2 | 2 | 8.5 | `HazardPosition` signature change touches the view and bot |
| 5.2 | 6 | 2 | 2 | 2.5 | 2 | 14.5 | highest risk of the 28; optional use only |
| 5.3 | 3 | 2 | 2 | 2 | 2 | 11 | |
| 5.4 | 4 | 2 | 1 | 2 | 2.5 | 11.5 | |
| 5.5 | 2.5 | 1 | 0.5 | 1.5 | 1.5 | 7 | |
| 5.6 | 4.5 | 1.5 | 2.5 | 2 | 2.5 | 13 | |
| 5.7 | 4.5 | 1.5 | 2 | 2 | 2.5 | 12.5 | |
| Sum | | | | | | 78 | + region work 18.5 = about 96.5 h |

Risks R5:
- Gongs already exist as hazards between rows. The hazard rule 'no two hazards in adjacent rows' must now count gong objects too: no gong in the corridor beside a Mallet Smash or Rolling Log row (builder change in the gong placement, +1 h counted in the region work). Without it two knockbacks can chain.
- Muffle Drum overlaps the Shield power-up (60 s, no gong hurts) and Climbing Pole overlaps Wall Stick (sold power-up). Both are shorter or conditional than the power-ups, but the owner should confirm they do not cut shop value (open question 4).
- Climbing Pole is the only kind that needs a new input rule (steer into the pole) and a bot action; fallback: ship it last, or replace it with a second plain helpful kind.
- Region 5 has `GongDensity` rising to 30 percent: with 25 percent hazard-type specials plus gongs the late levels can get dense; the existing `StackRuleTests` and bot proofs decide; may need a cap on combined hazard density.

## 7. Shared work per region (counted once per region, about 17.5 h; region 5 18.5 h)

| Item | h |
|---|---|
| `RegionPlatformSets.For` case, generic-kind mix (see below), classifier updates (`IsHazardType`, `IsLaunch`, `IsTimeDependent`, new `IsDemanding`) | 2 |
| Art catalog wiring, `ArtCatalogBuilder`, `bake_region<N>_new_platforms.py` (3 sheets, 7 kinds, pose frames) | 6 |
| `Region<N>PlatformTests` harness: placement rules, determinism, even kind mix, share ramp, hazard budget, copy of the region 1 harness | 3 |
| Endless mirror (`EndlessTower` geometry rules per kind: they are duplicated from `LevelBuilder`) | 2 |
| Bot proofs: levels 2-15 x 24 seeds plus `RandomLayoutTests` (all levels x 20 seeds), fixes | 2 |
| Golden regeneration (`UPDATE_GOLDEN=1`), `DIFFICULTY_REPORT`, gem value report (`GemCoinEconomyTests`), `CoinStarTargetTests` | 1.5 |
| NOTES.md and doc sync | 1 |

Code change needed once (not per kind): today `SpecialPlatforms.Options` drops the generic kinds when a region has a new set. To reuse generics, add a per-signature generic list (about 1 h, listed under item 1): R2 Crumble + Slippery, R3 Crumble + Vanishing, R4 Crumble + Slippery, R5 Vanishing + Moving. Each generic appears once among 9 or 10 options, so the new kinds stay the stars.

Overall: about 63 + 63 + 64.5 + 78 = 268.5 h of per-kind work plus about 72 h of shared work = about 340 h for the four regions (about 80 to 96 h per region, close to what region 1 took). The art cost, from the T61 packet (3 sheets, 2 options, 15 credits per image) is about 90 credits per region, 360 credits for the four; no credits are spent by this document.

## 8. Fairness and solvability rules for the new sets

1. All 8 hazard kinds (Dizzy Spores, Spore Pop, Kelp Tangle, Pufferfish Pad, Jelly Wobble, Pinwheel Gust, Mallet Smash, Rolling Log) go into `IsHazardType`: automatic no-adjacent-rows, no hazard after a launcher, spacing 6 rows up to level 6, none in level 1, last 3 rows and gem row +-2 free, hazard share limited to its share of the options (2 of 9 = about 22 percent).
2. Launch-like kinds (BounceCap, Spore Pop, Clam Shell Pad, Bamboo Flick) go into `IsLaunch`: never two in a row, the next row within the existing `BounceNextGapMax` 4.2 / `BounceNextDxMax` 3 u, or the larger distance for the Flick.
3. New `IsDemanding` class (hard specials that depend on time or position: Orbit Cap, Fairy Ring, Sinking Raft, Turtle Shuttle, Licorice Belt, Taffy Stretch, Swing Plank, Pagoda Ridge): never adjacent to each other or to a hazard, and not in the last 3 rows. The bot proof must still pass with them at the maximum share.
4. Control locks (Dizzy Spores, Kelp Tangle) are at most 1.0 s, the lock never hides a required landing: the next row is always reachable from the lock-free standing jump after the lock ends, and the kill line never makes it unavoidable because hazards are 6 rows apart early and never adjacent later.
5. Air zones (Fizz Vent, Pinwheel Gust): the next platform stays reachable with and without the zone effect (builder uses reach computed with the zone off and the bot proves the gusted path); neighbours of the same row keep out of the column.
6. Dir always points toward the middle of the screen (or away, where the kind says so) so a push, fling or flick never lands in a wall.
7. Every new kind: a unit test with hand-built worlds (numbers above), a determinism test, a 'never ends the run' test (random landings 500 times, like `ThornBush_ManyRandomLandingsNeverEndTheRun`), and the placement test generalised from `Region1PlatformTests.PlacementRulesHoldOnEveryRandomRegion1Layout` to take the region as a parameter.
8. Level 7 (`noWallBounce`): exclude Surf Boost Wave, Bamboo Flick, Spore Pop and Pinwheel Gust from the plan (they push toward walls); level 5/10 combo ladders and the rows beside them keep the existing 'no hive, sticky or launcher' rule, extended to Sprout Lift, Orbit Cap, Swing Plank, Sinking Raft and the air-zone kinds.

## 9. Golden hashes, tests and bot

- Region 1 hashes stay untouched as long as the shared RNG streams of `SpecialPlatforms.Plan` are not changed and `ComputeHash` is not extended (the new kinds only use `Amplitude`, `Period`, `Phase`, `Dir`, all already hashed; `Dir` only when non-zero). Regions 2 to 5 change completely when their set is switched on, because the kind mix replaces the generic mix.
- The golden file is ONE file: `Content/golden/level_hashes_r1-5.txt` (not `level_hashes_r2..r5`); regenerate once per region (or once for all) with `UPDATE_GOLDEN=1 dotnet test Tests/Simulation`; the literals in `GoldenHashTests` and the Unity test `LevelSpecFactoryTests`, and the endless golden (region 3, seed 12345, 100 rows) change too. Report files that rewrite on run: `DIFFICULTY_REPORT.md`, `SOLVABILITY_REPORT.md`, `ECONOMY_TABLES.md`.
- Tests that need a region-aware generalisation: `OtherRegionsGetNoRegion1Platforms`, `Region2AuditTests` to `Region5AuditTests`, `StackRuleTests` (hazard and zone kinds), `PlatformSeparationTests`, `PickupOverlapTests` (Orbit Cap, Swing Plank, Rolling Log, Mallet arm can sweep over pickups), `WallSafetyTests`, `GemCoinEconomyTests` (gem value band 55-75), `CoinStarTargetTests`.
- Bot: time-dependent keying `(Tick/20) % 24` has to cover the new periods; `IsLaunch`, waits and `MeasureOptions` extend as in region 1; Climbing Pole and Muffle Drum are treated as optional by the bot (it must solve every level without using them).
- Build order suggestion: shared infra first (control lock + air control field, air zone, surface-by-x, cycle clock helper), then the cheap kinds, then the risky ones (Climbing Pole, Taffy Stretch) last.

## 10. Summary table: all 28 new kinds (plus region 1 for context)

Enum values would be 15 to 21 (R2), 22 to 28 (R3), 29 to 35 (R4), 36 to 42 (R5) so existing values and hashes stay unchanged.

| Region | # | Name | Type | One-line mechanic | Family (distinct from) | Hours |
|---|---|---|---|---|---|---|
| 1 | - | Wind Petal, Bee Hive Lift, Snap Daisy, Dandelion Puff, Thorn Bush, Ladybug Rider, Seesaw Leaf | mixed | built (doc 24) | push, bob, timed close, diagonal launch, half knock, speed-up mover, tilt slide | done |
| 2 | 2.1 | Squish Cap | helpful | stand to charge, jump x1.45 | charge jump | 7.5 |
| 2 | 2.2 | Sprout Lift | helpful | rises 1.6 u once landed | one-shot rise | 11 |
| 2 | 2.3 | Puff Float | helpful | slow fall 1.2 s after jumping off | slow fall | 6.5 |
| 2 | 2.4 | Fairy Ring | hard | hole in the middle | hole | 7.5 |
| 2 | 2.5 | Orbit Cap | hard | small elliptical orbit | orbit path | 11.5 |
| 2 | 2.6 | Dizzy Spores | hazard | steering locked 1.0 s | control lock | 6.5 |
| 2 | 2.7 | Spore Pop | hazard | swells then flings you toward Dir | delayed fling | 12.5 |
| 3 | 3.1 | Surf Boost Wave | helpful | pushes you to full speed along Dir | speed strip | 6.5 |
| 3 | 3.2 | Sandcastle Step | helpful | two levels, auto step-up 0.9 u | step ledge | 10.5 |
| 3 | 3.3 | Clam Shell Pad | helpful | opens/shuts, launches x1.4 when open | timed pad | 10 |
| 3 | 3.4 | Sinking Raft | hard | sinks 1.0 u in 1.5 s while stood on | weight sink | 9.5 |
| 3 | 3.5 | Turtle Shuttle | hard | stop-and-go glide | stop-go mover | 8.5 |
| 3 | 3.6 | Kelp Tangle | hazard | no jump for 0.6 s after landing | jump lock | 7 |
| 3 | 3.7 | Pufferfish Pad | hazard | swells and spikes on a 3 s cycle, hops you off | inflating spikes | 11 |
| 4 | 4.1 | Candy Bowl | helpful | pulls you back to the middle | centring bowl | 6 |
| 4 | 4.2 | Floss Glide | helpful | double air steering for 1.5 s | air steering | 6.5 |
| 4 | 4.3 | Fizz Vent | helpful | low-gravity column above it | air zone (lift) | 13 |
| 4 | 4.4 | Licorice Belt | hard | conveyor against the next platform | treadmill | 7.5 |
| 4 | 4.5 | Taffy Stretch | hard | width breathes 2.4 to 5.2 u | width pulse | 11 |
| 4 | 4.6 | Jelly Wobble | hazard | shakes sideways after landing | landing shake | 9.5 |
| 4 | 4.7 | Pinwheel Gust | hazard | sideways air push in a column | air zone (push) | 11 |
| 5 | 5.1 | Muffle Drum | helpful | freezes all gongs 3 s | gong freeze | 8.5 |
| 5 | 5.2 | Climbing Pole | helpful | grab the pole, leave with a jump | pole grab | 14.5 |
| 5 | 5.3 | Bamboo Flick | helpful | flicks you 6 to 7 u sideways | long flick | 11 |
| 5 | 5.4 | Swing Plank | hard | pendulum swing with an arc | pendulum | 11.5 |
| 5 | 5.5 | Pagoda Ridge | hard | flat ridge, slopes slide you off | slide-off roof | 7 |
| 5 | 5.6 | Mallet Smash | hazard | timed smash zone at one end | timed smash | 13 |
| 5 | 5.7 | Rolling Log | hazard | log rolls end to end, bump up | rolling object | 12.5 |

Closest pairs (worth a look): Orbit Cap and Swing Plank (both curved paths; circle versus slowing pendulum with arc), Pufferfish Pad and Thorn Bush (spiky knock; one timed and upward, one static and sideways), Spore Pop and Snap Daisy and Mallet Smash (all timed; effects are fling, drop, smash zone), Dizzy Spores and Kelp Tangle (control locks; steering versus jump), Licorice Belt and Wind Petal (ground push; constant versus flipping).

## 11. Open questions for the owner

1. Scope per region: are 7 kinds plus the signature plus 2 generics too many per level mix? Early levels (3 to 6) could unlock the kinds gradually (for example 3 or 4 kinds at level 3, all 7 from level 7). Doc 24 said 'all available from level 3 for region 1'; same for regions 2 to 5?
2. Are the hazard consequences acceptable? Steering locked 1.0 s (Dizzy Spores), no jump for 0.6 s (Kelp Tangle), uncontrolled fling (Spore Pop) and knock-ups (Pufferfish, Mallet, Rolling Log). A control lock is the only effect that touches input; I never invert controls (accessibility). Keep, or drop the two locks for knock-only hazards?
3. Launchers: Clam Shell Pad and Bamboo Flick make 4 launch-like kinds across regions 2 to 5 (plus the caps). OK, or should the Clam launch only on a jump press while open?
4. Do Muffle Drum (overlaps the Shield power-up) and Climbing Pole (overlaps Wall Stick) cut the value of the shop power-ups? If yes, replace them with plain helpful kinds (for example a wide rest platform or a second lift) and keep the power-ups unique.
5. Region 5 gongs: allow gong objects in the rows beside Mallet Smash / Rolling Log, or ban them (my proposal: ban; costs a builder change)?
6. Taffy Stretch needs a 3-slice platform renderer (left cap, tiled middle, right cap) because platforms are never stretched. Accept the +3 h, or replace Taffy Stretch with a static kind?
7. Hazard share: the current rule gives hazards their share of the options (2 of 9 = about 22 percent of special rows) and level 2 gets 1 hazard. Keep, or lower it for region 2 (the first region after the tutorial region)?
8. Generic mix: confirm which generic kinds each region keeps (proposal: R2 Crumble + Slippery, R3 Crumble + Vanishing, R4 Crumble + Slippery, R5 Vanishing + Moving). Plain Bounce and Sticky disappear outside regions 1 and 4 (Sticky stays in its own region).
9. Art: OK to plan 3 sheets per region (3 + 2 + 2 kinds, 6 poses extra: Spore Pop 3, Pufferfish 3, Clam 2, Bamboo Flick 2, Licorice Belt 2, Pinwheel 2, Mallet arm 2) at about 90 credits per region (360 total)? Each approval packet is sent separately before any generation.
10. Order of work: region by region as in doc 24 (2, then 3, 4, 5, about 80 to 97 h each), or first the shared infra for all four (control lock, air zone, surface-by-x, cycle clock; about 25 h) so the cheap kinds come fast?
11. Level 7 (`noWallBounce`) and the combo ladders: confirm the exclusion list in section 8 (push and launch kinds out of level 7).

## 12. Region 2 as built (2026-11-01, owner list 30)

Owner answers (list 30): all 7 kinds in the normal ramp from level 3 like region 1 (levels 1 and 2 follow the region 1 rule: the same ramp, level 1 without hazard types), the two control-lock hazards stay (controls never inverted, hazards never kill), the Muffle Drum / Climbing Pole overlaps are accepted (region 5, later). Generic mix: Crumble + Slippery next to the 7 kinds and the BounceCap signature (10 options; `RegionPlatformSets.GenericsFor`).

Code: enum values `SquishCap` 15, `SproutLift` 16, `PuffFloat` 17, `FairyRing` 18, `OrbitCap` 19, `DizzySpores` 20, `SporePop` 21 (`Platform.cs`); numbers in `SimConfig.cs` (Region 2 block); behaviour in `Sim.cs` (`LandOnNewPlatform`, `CarryOnNewPlatform`, `PlatformX/PlatformY`, jump hook, gravity block, landing search); placement in `SpecialPlatforms.cs` (`Allowed`, `ConstrainWidth`, `ConstrainGap`, `Params`, integer `Sin`/`Cos` table) and `LevelSpec.cs` (`RegionPlatformSets`); the builder and `EndlessTower` call the same helpers. Shared infrastructure built: control lock (`SimState.SteerLock`), fall slow-down (`SimState.FloatTicks`), per-platform clocks in `CrumbleTick` (Spore Pop landing tick, Sprout Lift rise clock), integer sine table, `PlatformKinds.IsDemanding`. NOT built (not needed by region 2): air zone, surface-by-x, cycle clock (regions 3 to 5).

What differs from the numbers above:
- **Squish Cap**: the sink is real: the standing player and the cap are drawn `SquishSink` lower (0.35 u over 48 ticks); the jump bonus (x1 to x1.45) only counts for a grounded jump (a coyote jump after walking off is a plain jump).
- **Sprout Lift**: rise clock: while stood on, `CrumbleTick[id]` = virtual start tick of the rise (level = ticks since, max 72); once empty it holds `-(tickLeft * 128 + levelLeft) - 2` and the level falls 1.2 per tick (1.6 u in 60 ticks). Landing on a risen or sinking sprout continues from its current level. Gap rule: both neighbours in [HiveGapMin 2.6, 4.2] u. Not beside ladders/chunks.
- **Puff Float**: the 72-tick window counts only while the player is FALLING (the rise does not eat it), so the whole fall of a normal jump is slowed; fall speed at most 5 u/s, gravity 45 percent; ends at the next landing.
- **Fairy Ring**: always the 6 u painted width (two usable halves of 2.2 u), hole half width 0.6 to 0.8 u (`Platform.Amplitude`); a revive on it puts the player 1 u beside the hole.
- **Orbit Cap**: sideways reach 0.7 to 0.9 u (limited by the wall room), 0.5 u up and down, 252 ticks, counter-clockwise (x = cos, y = sin); neighbour gaps in [2.0, 3.7] u.
- **Dizzy Spores**: 60 ticks, cleared by the next landing anywhere; waits of 62 ticks in the bot and the autopilot.
- **Spore Pop**: swell 72 ticks, fling `Vy` 0.21 u/tick (2.0 u apex; the 0.10 in the table above would only give 0.45 u) and `Vx` 0.075 u/tick toward Dir (middle of the screen), flat 120 ticks (still solid), no new countdown until the flat phase is over. It is a hazard type but NOT a launcher: it only acts when the player lingers, so `IsLaunch`, `BounceNextGapMax` and the launch budget do not apply (a player who leaves in time is never launched).
- **Demanding class** (`IsDemanding`: Fairy Ring, Orbit Cap): never adjacent to each other, to a hazard or to a launcher, and not in the last 3 rows / around the gem row. Sprout Lift and Orbit Cap are also kept out of rows beside combo ladders and authored chunks.
- **Level 7**: no exclusion needed (Spore Pop flings toward the middle only if the player lingers; the bot never does).
- Placeholder art: until the painted pieces of the T73 packet exist the view draws the normal platform tinted per kind (`SpecialPlatformFx.Tint`), no code-drawn cues; the motion that is the mechanic (orbit lap, sprout rise, squash sink) comes from the Sim positions.
- Tests: `Tests/Simulation/Region2PlatformTests.cs` (per-kind hand-built worlds, placement rules on 20 seeds x 15 levels, ramp, endless, bot proofs 24 seeds x 15 levels, endless bot). Golden: only the 15 `lvl_002_*` lines of `Content/golden/level_hashes_r1-5.txt` changed; regions 1, 3, 4, 5 are bit-identical.

## 13. Region 3 art status (2026-11-02, owner picks T74)

Owner picks: A2 (Surf Boost Wave, Sandcastle Step, Clam Shell Pad), B1 rows 1 to 3 (Sinking Raft), B2 rows 4 to 6 (Turtle Shuttle), C2 (Kelp Tangle, Pufferfish Pad). Nothing new was generated (0 credits); the picks are already logged in `Content/assets/assets_log.csv` (T74 `region3_picks`).

Baked (no Unity needed) by `Tools/art_pipeline/bake_region3_new_platforms.py`, same method as region 2 (grey key, drop shadows, the dark sticker halo of the rafts, sparkles, row numbers and divider lines removed; one uniform scale per kind = 0.5 u thick, every width cut / repeated in the middle, nothing stretched). Output: `Assets/Art/Regions/region_003_sunny_beach/tiles/<kind>_widths/reg003_<kind>_*.png` + `platform_set.json` (kind = `PlatformKind` name), 167 px per u:

| Kind | Pieces | Notes |
|---|---|---|
| SurfBoostWave | 4, 5, 6 u x dir +1 (rolls right) and -1 (mirror) | wave curl is never cut |
| SandcastleStep | 4, 5, 6 u x dir -1 (raised half LEFT, as painted) and +1 (mirror) | step exactly in the middle (each half W / 2); `surfaceY` = LOW surface (Platform.Y), `riserPx` / `riserU` in the json |
| ClamShellPad | 2, 3, 4 u x pose open (-1) / shut (+1) | same scale and base line for both poses |
| SinkingRaft | 2, 3, 4, 6 u | each raft row has its own scale (0.5 u thick) |
| TurtleShuttle | 2, 3, 4, 6 u | head and flippers overhang the shell; the view mirrors the art with the swim direction |
| KelpTangle | 2, 3, 4, 6 u | |
| PufferfishPad | 3, 4, 6 u x pose calm (-1) / swelling (+1) / puffed (+2) | glossy highlights painted out of the calm and swelling rows so they do not repeat |

View: `RunView` (posed kinds: Spore Pop, Clam open when `(tick + Phase) % 144 < 72` via `Sim.ClamOpen`, Pufferfish calm / swelling / puffed via `Sim.PufferPhase`; turtle x from `PlatformX`, raft y from `PlatformY`) and `SpecialPlatformFx` (turtle faces its next glide, placeholder tints). Catalog: `ArtCatalogBuilder.LoadKindPieces` already reads every `*_widths` folder; run **Tools/Boing Up: Sky Tower/Build Art Catalog** once so `RegionArt.KindPieces` of region 3 is filled. No code-drawn cues.

Open points for the owner:
- **Sandcastle riser**: the painted step is only 0.16 to 0.23 u high (with the platform 0.5 u thick), but `SimConfig.SandcastleRise` is 0.9 u. Either set `SandcastleRise` to about 0.2 u or ask for a taller-step sheet; at 0.9 u the player would hover above the raised half.
- **Clam**: the open pose has a lid about 1.2 u above the surface at 3 u wide (decoration behind the player), the shut pose is 1 u thick.
- Art QA (`qa_art.analyse` on every piece): 0 clipped, 0 halo.

## 14. Region 4 (Candy Canyon) as built and the art plan (2026-11-04, Bug List 3 item D5)

Status: **code and tests done, art NOT generated** (0 credits). Until the painted pieces exist the view draws the normal platform of the region tinted per kind (`SpecialPlatformFx.Tint`), no code-drawn cues. The OpenArt sheets are drafted in `Content/assets/approvals/T78-region4-platform-sheets-draft.md` (sheet texts only; the approval packet is assembled by the lead and needs an explicit owner yes per batch).

### 14.1 What the code does (differences from section 5 in bold)

Enum values: `CandyBowl` 29, `FlossGlide` 30, `FizzVent` 31, `LicoriceBelt` 32, `TaffyStretch` 33, `JellyWobble` 34, `PinwheelGust` 35 (`Platform.cs`; region 5 continues at 36). Kind list of region 4 (`RegionPlatformSets`, `StickyGum`): these 7 + the Sticky signature (listed once) + the generics Crumble and Slippery = 10 options, so hazard types are 2 of 10 (about 20 percent of the special rows), demanding 2 of 10, no launcher. Numbers live in the "Region 4" block of `SimConfig.cs`. Sim: `SimState.GlideTicks` (new field), `Sim.WidthAt`, `Sim.JellyOffset`, `ReadAirZones` (new, only runs on levels that have a vent or pinwheel), `CarryOnNewPlatform`, `LandOnNewPlatform`.

| # | Kind | Built as | Differences |
|---|---|---|---|
| 4.1 | Candy Bowl (helpful) | outside the centre 55 percent of the width a standing player is pulled 1.5 u/s toward the middle (`BowlPull` 0.025 u per tick); width 4, 6 or 8 u | as designed |
| 4.2 | Floss Glide (helpful) | a jump from it sets `GlideTicks` = 90; while it runs the air steering is x2; cleared by every landing | as designed (a wall jump or double jump from elsewhere never starts it) |
| 4.3 | Fizz Vent (helpful, air zone) | column 0.9 u either side, 3.2 u high above the vent surface; gravity x0.2 while airborne in it; width 2 to 4 u | **a standing jump from the vent peaks at about 6.7 u (the plain jump 4.4 u as the Sim integrates it), not 7.0 u**: the first airborne tick is at the surface and not yet in the zone. Falling players are slowed in the column too |
| 4.4 | Licorice Belt (hard, demanding) | carries a standing player 2.0 u/s along `Dir`; width 4 or 6 u | **`Dir` points toward the nearest wall and the next row is always placed on the middle side of the belt (`SpecialPlatforms.BeltNextX`, like the Dandelion), so the belt always runs away from the next platform and the tower stays prefix stable in endless**; a runner keeps still or advances against it |
| 4.5 | Taffy Stretch (hard, demanding) | `Platform.Width` = the narrow width 2.4 u, `Platform.Amplitude` = how far each edge travels (1.4 u, less next to a wall so the widest 5.2 u stays inside the walls), period 270 ticks, triangle wave, edges move 0.62 u/s; landing and walk-off use `Sim.WidthAt(id, tick)`; the generator, the reach and the bot use the narrow width | the 3-slice renderer is NOT needed: the view swaps between 4 painted widths (14.2) and only the fallback (no art) scales the placeholder. Sticky is not allowed in a row next to a Taffy (`SpecialPlatforms.Allowed`, risk R4) |
| 4.6 | Jelly Wobble (hazard type) | the landing tick is kept in `CrumbleTick`; the cube swings up to 0.6 u at 3 Hz (triangle wave of 20 ticks starting at 0) with a linear fade over 90 ticks; width 4 or 6 u (min 3.5) | **the player is carried 60 percent of the cube's movement (`JellyCarryPercent`), the rest slides under the feet**: with a 100 percent carry nothing could shake an edge-standing player off, which the design wants. Landing again while it still wobbles continues the wobble; a revive resets it |
| 4.7 | Pinwheel Gust (hazard type, air zone) | column 1.0 u either side (2.0 u wide), 3.5 u high; airborne players are moved 3.0 u/s along `Dir` (position push, no speed build-up); width 2 to 4 u; `Dir` toward the middle of the screen | **not offered in the no-wall-bounce level 7** (doc 30 section 8 item 8, like the Surf Boost Wave) |

Placement beyond the generic rules: rows beside combo ladders and authored chunks take no Fizz Vent, Pinwheel Gust, Licorice Belt or Taffy Stretch (section 8 item 8). Demanding kinds (Belt, Taffy) join `IsDemanding`: never adjacent to each other, to a hazard or a launcher, not in the last 3 rows or around the gem row. The Taffy narrow width 2.4 u is a painted width of its own (`LevelBuilder.SnapWidth`); the Taffy and the Jelly keep their swing inside the walls (`SnapPlatformWidths`). Bot: `LevelBot` keys the Taffy by its 270-tick clock (`TimeBucket`); the bot solves levels 1 to 15 x 24 random seeds, the 15 base layouts and 20 endless towers of 60 floors (`Region4PlatformTests`).

Golden hashes: only the 15 `lvl_004_*` lines of `Content/golden/level_hashes_r1-5.txt` changed (regions 1, 2, 3, 5 bit-identical; the endless golden literal of region 3 is unchanged). `EndlessAllRegionsTests` expected set of region 4 updated.

View (no drawn cues): `RunView` poses for Belt (2 tread frames, `(tick / 6) % 2`), Pinwheel (2 spin frames, `(tick / 8) % 2`), Taffy (nearest of 4 painted widths to `Sim.WidthAt`), Jelly (squashed for the first 24 ticks of a wobble, else calm; the jiggle itself is the Sim x from `PlatformX`); reduced motion shows frame A only. `SpecialPlatformFx` mirrors Belt and Pinwheel to `Platform.Dir`. Fizz Vent has no pose.

### 14.2 Region 4 art plan (sheet row plan the prompts are written from)

Rules shared with regions 2 and 3: slab thickness 0.5 u (so a length of N u is 2 N times the thickness), 167 px per u after baking, one uniform scale per kind, nothing stretched (widths are cut or repeated in the middle), grey key background, art bound by kind through `Assets/Art/Regions/region_004_candy_canyon/tiles/<kind>_widths/platform_set.json` (`"kind"` = lower-case enum name, `"dir"` = pose number as below, `"width"` in u) which `ArtCatalogBuilder.LoadKindPieces` already reads; a kind without a folder falls back to the tinted normal platform. A baker script `Tools/art_pipeline/bake_region4_new_platforms.py` (copy of the region 3 one) is written after the owner picks the sheets. Palette: pink, mint, cream, caramel brown, strawberry red, licorice black.

| Sheet | Row | Kind and pose | Painted length (x thickness) | Baked widths (u) and `dir` |
|---|---|---|---|---|
| A (helpful) | 1 | Candy Bowl | 8 (4 u) | 4 u; the 6 and 8 u pieces repeat the middle |
| A | 2 | Candy Bowl | 16 (8 u) | 6, 8 u; walkable floor flat, raised lips at both ends about 0.8 thick, lips never cut |
| A | 3 | Floss Glide | 4 (2 u) | 2 u (wisps at both ends are never cut) |
| A | 4 | Floss Glide | 12 (6 u) | 3, 4, 6 u |
| A | 5 | Fizz Vent | 4 (2 u) | 2, 3 u; bubble cluster above the slab up to 4 thick high, empty band above rows 5 and 6 |
| A | 6 | Fizz Vent | 8 (4 u) | 4 u |
| B (hard) | 1 | Licorice Belt, tread frame A (`dir` -1) | 12 (6 u) | 4, 6 u; art looks toward +1, the view mirrors to `Platform.Dir` |
| B | 2 | Licorice Belt, tread frame B (`dir` +1) | 12 (6 u) | 4, 6 u; same base line and end rollers as frame A, only the stripes shift half a stripe |
| B | 3 | Taffy Stretch, narrow (`dir` -1) | 4.8 (2.4 u) | 2.4 u |
| B | 4 | Taffy Stretch, second (`dir` 1) | 6.6 (3.3 u) | 3.3 u |
| B | 5 | Taffy Stretch, third (`dir` 2) | 8.6 (4.3 u) | 4.3 u |
| B | 6 | Taffy Stretch, wide (`dir` 3) | 10.4 (5.2 u) | 5.2 u; same taffy colour and twisted ends in all four, thinner in the middle the wider it is |
| C (hazard) | 1 | Jelly Wobble, calm (`dir` -1) | 8 (4 u) | 4 u |
| C | 2 | Jelly Wobble, squashed (`dir` 1) | 8 (4 u) | 4 u; a little wider and flatter than calm, cherry slid to one side, same base line |
| C | 3 | Jelly Wobble, calm (`dir` -1) | 12 (6 u) | 6 u |
| C | 4 | Jelly Wobble, squashed (`dir` 1) | 12 (6 u) | 6 u |
| C | 5 | Pinwheel Gust, spin frame A (`dir` -1) | 8 (4 u) | 2, 3, 4 u; the white candy slab base repeats its middle, the pinwheel stays whole in the centre; swirl lines point toward +1, mirrored by `Platform.Dir` |
| C | 6 | Pinwheel Gust, spin frame B (`dir` 1) | 8 (4 u) | 2, 3, 4 u; blades turned by about 45 degrees |

Counts: sheet A 3 kinds / 6 rows, sheet B 2 kinds / 6 rows, sheet C 2 kinds / 6 rows, 3 + 2 + 2 kinds; extra poses: Belt 2, Taffy 4 widths, Jelly 2, Pinwheel 2. Credits: 3 calls x 2 options as in T74 (about 90 credits at 30 per call, re-quoted before the call).

Open points for the owner / the art step:
- **Fizz Vent column**: the zone is 3.2 u high but the painted bubbles only reach about 2 u above the slab (a taller piece does not fit the sheet layout). If wanted, a separate faint bubble column sprite (a looping overlay painted behind the player) can be added later; today the zone beyond the bubbles is learned by play.
- **Taffy Stretch** is drawn as 4 swapped widths (no 3-slice renderer, which saves the 3 h from section 5); the step between widths is at most 1 u, visible as a small pop. A 3-slice renderer (left cap, tiled middle, right cap) is the alternative if the pop is too visible.
- **Sticky Gum** (signature, 5) already has region 4 art; Crumble and Slippery are existing generic pieces.

## 15. Region 5 (Bamboo Temple) as built and the art plan (2026-11-04, Bug List 3 item D5)

Status: **code and tests done, art NOT generated** (0 credits). Until painted pieces exist the view draws the normal platform of the region tinted per kind (`SpecialPlatformFx.Tint`), no code-drawn cues. The sheet texts are in `Content/assets/approvals/T80-region5-platform-sheets-draft.md` (with the code bindings table added).

### 15.1 What the code does (differences from section 6 in bold)

**Replaced kinds:** Muffle Drum and Climbing Pole (5.1, 5.2) are NOT built: they overlap the shop power-ups Shield and Wall Stick (open question 4). They are replaced by **Lantern Lift** and **Koi Stone**. Enum values: `BambooFlick` 36, `LanternLift` 37, `KoiStone` 38, `SwingPlank` 39, `PagodaRidge` 40, `MalletSmash` 41, `RollingLog` 42 (`Platform.cs`, `IsRegion5New`, `IsRegionNew` now ends at 42). Option list of region 5 (`RegionPlatformSets`): the 7 new + generics Moving and Vanishing = 9 options; the swinging gongs stay hazard OBJECTS (no platform option). Hazard types 2 of 9, demanding 2 of 9, launcher 1 of 9. Numbers live in the "Region 5" block of `SimConfig.cs`.

| Kind | Built as |
|---|---|
| Bamboo Flick (helpful, launch type) | a landing sets `Vy` = 0.8 x the standing jump speed (fixed, independent of the landing speed; peaks 2.76 u as integrated) and `Vx` = 8 u/s along `Dir` (toward the middle). Width 3 u. The next platform is 3.6 to 4.6 u away in `Dir` (`SpecialPlatforms.FlickNextX`) and 2.0 to 2.2 u higher (`ConstrainGap`): **the design's 5.5 to 7 u was too far for 8 u/s over the 0.6 s flight to a platform 2 u up (the unsteered flight covers about 4.6 u)**. Poses rest/sprung (sprung 30 ticks after the flick). |
| Lantern Lift (helpful) | rises 2.0 u in 90 ticks (1.5 s) after the landing, stays up while stood on, floats down at 60 percent of the rise speed (150 ticks for the full 2 u) once empty; sways +-0.3 u on a 180-tick sine always (the player rides it). Reuses the Sprout Lift clock (`SproutLevel`) with its own constants. Width 3 or 5 u; rows next to it keep a gap of 3.0 to 4.2 u (a risen lantern leaves 1.0 u of air). Different from the Sprout Lift (1.6 u, 72 ticks, sinks fast, no sway). |
| Koi Stone (helpful) | 5 or 7 u wide, never moves. Standing still (no horizontal speed) for 60 ticks charges it: the next jump from it launches x1.05 (about +10 percent height) and the flight has the full ground steering (no air-control cut) for 60 ticks. `SimState.KoiStill`, `KoiAir`; any movement or landing resets the rest. |
| Swing Plank (hard, demanding) | pendulum: +-1.5 u (3.0 u total) on a 264-tick sine, rising 0.6 u at the ends (`y = 0.6 x sin^2`), carries the player along its arc. Width 3, 4.5 or 6 u; rows next to it keep a gap of 2.0 to 3.6 u. |
| Pagoda Ridge (hard, demanding) | only the middle 1.0 u is flat; beyond 0.5 u of the centre a standing player is moved outward 0.04 u per tick (2.4 u/s); a jump at once, or running inward, is not held. Width 2.5, 3.5 or 4.5 u (**design said 3 to 6**: the painted widths of the sheet). |
| Mallet Smash (hazard type) | global cycle of 150 ticks: the smash is ticks 60 to 69, the pose is "down" ticks 60 to 109, else raised. Zone 1.6 u at the end on side `Dir`; **`Dir` is the end toward the nearest wall** so the hop (3 u inward, 0.7 u up) never goes into a wall. Standing or landing (a landing is no landing) in the zone during the smash hops the player and sets 60 ticks of invulnerability. Width 4.5 or 6 u (safe part at least 2.9 u). |
| Rolling Log (hazard type) | track 5 u wide, the log (1.2 u) rolls 3.8 u end to end at 2.4 u/s (190 ticks there and back, `Sim.LogX`). Contact within 0.7 u of the log centre while grounded, when landing on it, or while less than 0.6 u above the platform just left bumps the player 1.0 u up and 2 u **toward the middle of the platform** (the design said forward; toward the middle can never push into a wall). 60 ticks invulnerable. Jumping over it is safe. |

Gongs: the planned builder ban on gongs beside Mallet/Log rows (risk R5) is **not needed**: every Mallet and Log bump sets `Invuln` (60 ticks) and `HitHazards` ignores a player with `Invuln`, while a gong hit's 90 invulnerable ticks block the Mallet and Log (they only act when `Invuln` is 0). No two knockbacks can chain, and gong counts stay unchanged.

Placement beyond the generic rules: rows beside combo ladders and authored chunks take no Lantern Lift, Swing Plank, Pagoda Ridge or launcher. Swing Plank and Pagoda Ridge join `IsDemanding` (never adjacent to a demanding kind, hazard or launcher, not in the last 3 rows or around the gem row). Flick is a launcher (no launcher after a launcher, not in the last 3 rows or around the gem row). Painted widths per kind are enforced by `LevelBuilder.SnapWidth` (own sets). Bot: waits for the Lantern (0, 30, 95, 130) and Koi (0, 20, 85, 110); `LevelBot` keys Lantern, Plank, Mallet and Log by their clocks. Endless: the Moving rope bridge is there from floor 0, the other 8 kinds phase in every 10 floors (Lantern, Koi, Vanishing, then Flick, Plank, Ridge, then Mallet, Log).

View (no drawn cues): `RunView` poses for Flick (rest/sprung) and Mallet (raised/smashed), positions of Lantern and Plank from the Sim; `SpecialPlatformFx` mirrors Flick and Mallet to `Platform.Dir` and draws the Rolling Log as its own object (sprite slot `RegionArt.RollingLog`, a tinted block until it exists).

### 15.2 Region 5 art plan (named slots)

Rules as in 14.2: slab thickness 0.5 u (length N u = 2 N x thickness), uniform scale, nothing stretched, art bound by `tiles/<kind>_widths/platform_set.json` (`"kind"` lower-case enum name, `"dir"` pose, `"width"` u, optional `"log": {"file": ...}` for `rollinglog`); a kind without a folder falls back to the tinted normal platform. Slots: `bambooflick` (rest -1, sprung 1; 6x = 3 u), `lanternlift` (6x = 3 u, 10x = 5 u), `koistone` (8x = 4 u piece widened to 5 u by repeating its middle, 14x = 7 u), `swingplank` (6x, 9x, 12x = 3, 4.5, 6 u, ropes above inside the piece), `pagodaridge` (5x, 7x, 9x = 2.5, 3.5, 4.5 u), `malletsmash` (9x = 4.5 u and 12x = 6 u, raised -1 / smashed 1), `rollinglog` (track 10x = 5 u plus the log object). Full row table: T80 draft. Baker script `Tools/art_pipeline/bake_region5_new_platforms.py` is to be written after the owner picks the sheets.
