# 09 - Decisions Needed (v2.1 review)

Questions and gaps found when cross-checking docs 00-08. **Status: all recommendations ACCEPTED by the owner and applied to the owning docs** (01, 02, 03, 04, 05, 07, 08, README). The "Recommendation" column is now the decision. Prototype-tuned values (D4, D5 score formula, D16) are expected to change after Phase 1; revise them in the owning doc, not here. Doc 08 remains authoritative for rules and numbers.

Choices made where the recommendation left room: D8 key colour = `#808080`; D3 daily endless coin cap = about 3 runs at full rate, then half (Remote Config); D10 battle pass = $4.99 at launch.

> **Update 2026-10-20:** section F at the end of this document lists the owner decisions taken during the device-test rounds (2026-10-19/20) in a "Decided" list with dates, and the items that are still open. The rules themselves live in doc 08.

## A. Balance and economy

| ID | Question | Why it matters | Recommendation |
|---|---|---|---|
| D1 | Difficulty cap flattens late regions. With offset +0.0..+0.25 and d capped at 1.0, L14 equals L15 from region 18, L13 from region 27, and L15 never gets harder. Region 20 L1 is d = 0.21, not "like region 1 L10" as doc 02 once claimed. | Late regions feel the same at the top end | Scale instead of clamp: use `d_eff = d + offset * (1 - d)` so the offset compresses toward 1.0 and L15 can differ via other levers (speed, hazard tempo, gap jitter). |
| D2 | Endless gem drops (floors 250/500/1000, "first time per day"): per account or per region? Per region could mean up to 90 gems/day. Not counted in the doc 08 free-income model. | Breaks gem scarcity | Per account, once per day total; add to the income model (about +1-3 gems/day for skilled players). |
| D3 | Endless coin rate beats campaign replays (a 500-floor run is about 950 coins vs 360 for an L15 replay). Does the "+1 coin per new platform" rule also apply in endless? | Coin farming, makes campaign replay pointless | Endless: drop the per-landing coin, keep +1 per 2 floors and the 100-floor bonus; cap endless coins per day at roughly 3 runs' worth at full rate. |
| D10 | Battle pass premium price: $4.99 or $9.99? | Pass tier rewards depend on it | $4.99 at launch, test $9.99 with a richer track. |
| D11 | Launch scope: README says 20 live regions confirmed; doc 07 recommends 8-10 and says 20 adds about 3 months. | Schedule and soft-launch timing | Keep 20 as the target, but treat 8-10 as the fallback decision at the Phase 4 checkpoint. |

## B. Rules and design gaps

| ID | Question | Recommendation |
|---|---|---|
| D4 | **Physics constants are undefined** (gravity, accel, maxSpeed, jumpBase, jumpSpeedFactor, wallBoost). The generator rule "gap < 0.9 x max jump height" and the solvability bot cannot be built without them. | First coding task: choose baselines in a spreadsheet, tune in the prototype, then freeze for the generator. (Done: the values live in `SimConfig`; hero physics and `JumpSpeedFactor`: see docs/44-power-mechanics-bible.md in the boing-up-sky-tower docs folder section 3.) |
| D5 | **Units, floors and score.** Screen width in world units is undefined (easy platforms are 6-8 units wide, player = 1). "Floor" is undefined (platform row?). There is no score formula, yet boards rank by "score, then time". | Floor = platform row index. Screen width about 10 units portrait. Score = floors x 10 + combo bonus (combo^2 x 5) + coins x 1; revisit after the prototype. |
| D6 | Can campaign runs use consumables? Global campaign boards say "only Ranked counts". | Campaign is Casual by default (consumables allowed, no board credit on the 3 milestone global boards unless the run was Ranked). Per-level friends boards accept both, flagged by mode. |
| D12 | Generator special platforms (sticky, slippery, crumble, vanishing, bounce) overlap region signature mechanics (regions 2, 4, 6, 8, 11). Are specials global or region-weighted? | Region-weighted through `platformWeights` in RegionDef: a region's signature platform appears at its full share, others at 0 until the region's difficulty tier allows them. |
| D13 | Combo definition: doc 01 says "combo timer", doc 02 says "jump passing 2+ floors, ends on a low landing". | Use doc 02 (no timer); update FR-G3 when confirmed. |
| D14 | Trail: doc 01 has a trail slot, doc 02 says each character has a unique trail, doc 08 has a light ribbon coloured by the equipped trail. | **Updated 2026-11-01 (no gear):** there is no equip slot for trails. A character's unique trail is part of the character itself (its signature ribbon), not an item. |
| D15 | Level goal types under doc 08 (the class table gives d and rewards but not goals); plus roughly 60 authored chunk sets for L5/L10/L15 of 20 live regions. | Keep the doc 02 goal vocabulary; build a shared chunk library of about 12 chunks per class and reuse across regions with re-skinning. |
| D16 | Endless: at which floor does d reach 1.0? Speed cap is at about floor 940. | d = min(1, floor / 400); speed ramp unchanged. |
| D17 | Run-length pillar says 30-90 s, but the ramp cap is at about floor 940. | Treat the cap as a mastery ceiling for rare long runs; no change needed. |

## C. Technical and scope

| ID | Question | Recommendation |
|---|---|---|
| D7 | **Signature modules.** Regions 1-30 need about 28 distinct modules. Locked regions 21-30 must ship their modules in the v1.0 binary or each unlock needs an app update (doc 04: new hazard types need an update). | Build all 30 modules before v1.0 if the schedule allows; otherwise ship only 20 and plan an app update before region 21. Adjust the README claim: "no store update" applies from region 31 onward and only when reusing existing modules. |
| D8 | **Chroma-key colour** for AI generation. Neon glow spills onto a `#00FF00` key and doc 08 dropped the explicit colour. | Use a key colour absent from all palettes (for example flat mid-grey or `#FF00FF` magenta only if no palette uses it) and key with a matte pass; generate glow in a separate layer where possible. |
| D9 | **Determinism.** The sim and generator must match across IL2CPP (ARM) and server .NET (x86). | Fixed-point or integer math in the sim and generator (applied in doc 03). Float only for rendering. |
| D18 | **Leaderboard design vs PlayFab limits.** About 180 boards (60 endless, 90 milestone, plus region, total, overall, daily). Friends boards via other players' data may be blocked by PlayFab read permissions and are unvalidated. | Verify limits (flagged in doc 08); fall back to a single keyed-score table; friends boards use server-validated stats, not client-side compares. |
| D19 | **Accessibility vs neon FX.** Flashes, pulses and colour-shifting platforms need a reduced-motion/photosensitivity path and non-colour platform cues. | Applied as a rule in doc 08 section 3; confirm thresholds during prototype QA. |

## D. Housekeeping done in this review (v2.1)

- Doc 08: Bronze chest USD value (0.55), VIP row (25/day), coins per region (about 4,300) and 20-region total (about 110,000), notes linking to D1-D3, D8, accessibility guard.
- Doc 02: gem packs, level difficulty table (d from doc 08), consumable prices, economy sinks/faucets, Epic character 700, optional-hazards note.
- Doc 03: region 21 examples (gemSkip 416, difficultyOffset 0.17), determinism rule.
- Doc 04: STYLE LOCK v2 and halo rule.
- Doc 05: VIP 25 gems/day, battle pass price pointer.
- README: paths, deliverable status, index entry for this doc.

## E. Decisions applied (second pass)

D1, D2, D3, D6, D7, D12, D15, D16, D18 in doc 08; D4, D5, D14 in doc 02; D6, D13, D14 in doc 01; D18 in doc 03; D7, D8 in doc 04; D10 in doc 05; D4, D7, D11, D15 in doc 07; D7 in README. D9 and D19 were applied earlier; D17 needs no change.

## F. Device-test rounds, 2026-10-19 and 2026-10-20

Source: the game repo `.claude/pendingdecisions/device-feedback-2026-10-19.md` (lists 1 to 10 and "Lists 6 to 8"). Owner device: OnePlus 13.

### Decided
| Date | Decision | Applied in |
|---|---|---|
| 2026-10-19 | Three always-visible buttons (Left, Right, Jump); touch zones removed; left-handed swaps sides | docs 01, 02, 03, 08 |
| 2026-10-19 | Platforms at real aspect ratio (variant by width), feet aligned to the surface line | doc 04, 08 |
| 2026-10-19 | Screen-by-screen polish pass on device screenshots; Android test kit (adb harness + UI dump) | doc 03, 06, 07 |
| 2026-10-19 | Auto-scroll after the first jump; bottom edge kills; no hazard from below; no timer, no time star, Speedrun levels become reach-floor goals | doc 01, 02, 08 |
| 2026-10-19 | Revive 2 gems the first time, 5 the second, max 2 per run | doc 02, 05, 08 |
| 2026-10-19 | Free OFL game fonts: Lilita One (display) + Nunito Bold (body); OpenArt not used for fonts | doc 00, 04, 06 |
| 2026-10-19 | Galaxy portal at the top is the goal; longer levels with patterns and dense patches of smaller platforms (some crumble); coins never inside a platform | doc 02, 08 |
| 2026-10-19 | Five scroll difficulty levels in Settings; map names as small tags instead of boxes; new popup frame; text-vs-image overlap tests | doc 08 |
| 2026-10-20 | Economy v2: 3 collectible stars per level, gem rarity tiers 1/2/5/10 (70/22/7/1 percent), coins paid at the end (30 + 40 per star + 5 per combo step), 3-star replays pay 50 percent and drop no gems; CollectCoins goal and old star types retired | doc 02, 05, 08 |
| 2026-10-20 | Narrow platforms get smaller gaps so one jump always works | doc 08 |
| 2026-11-01 | **No gear anywhere** (hats, shirts, pants, boots, accessories, gear chests, gear rarity frames, upgrades, shards, wardrobe layering). Tab 1 of the shop is Chests only (Power-up Chests sold, Coin Chests earn-only). Characters, power-ups and VIP stay | docs 05, 08, 09, 13, 14 (done); 02, 04, 10, 15, game repo (pending) |
| 2026-11-01 | **Fixed rate 1 gem = 20 coins, never changed**; coin packs 5/10/25/50/100/250/500 gems for 100/200/500/1,000/2,000/5,000/10,000 coins; coin price of any item = 20 x gem price; launch promo applies to gem packs in USD only | docs 08, 14 (done); `coin_packs.json`, `ProgressService`, `remote_config.json` `coinsPerGem` (game fork) |
| 2026-10-20 | World map v2: 10 chained OpenArt segments of 3 regions, scrollable, thin cloud stream between segments (150 px, procedural), translucent top bar, hand-placed pins, "Coming soon" for regions 6 to 30 | doc 04, 08 |
| 2026-10-20 | Scroll steps 20/40/60/80/100 percent per 5 s on a 120 percent base, cap +400 percent, "SPEED UP +N%" cue | doc 02, 08 |
| 2026-10-20 | Jump retune: JumpBase 19, normal gap cap 4.2 u (current `JumpSpeedFactor` and jump peaks: docs/44-power-mechanics-bible.md in the boing-up-sky-tower docs folder, section 3) | doc 02, 08 |
| 2026-11-01 | Docs aligned to the code: JumpSpeedFactor is 0.41 (not 0.58); full-speed jump 22.69 u/s launch, peak 6.44 u = 1.85 gaps, half speed 5.43 u. Owner: "go with what code has" | doc 02, 08 |
| 2026-10-20 | Star pickup fix (draw at simulation position, swept test); pickup effects at the pickup position | doc 08 |
| 2026-10-20 | Endless mode is entered with an Endless button on each region's level screen | doc 01, 02, 08 (implementation in progress) |
| 2026-10-20 | Build versioning vMAJOR.MINOR.HOTFIX.BUILD | doc 00, 03, 08 |
| 2026-10-20 | Playable scope for the milestone: 5 regions x 15 levels; dev-build switch "Test: all levels open" (default on) | doc 00, 07, 08 |
| 2026-10-20 | Placeholder audio from CC0 Kenney packs plus one CC0 music loop (download approved) | doc 12 |
| 2026-10-20 | AltTester SDK install approved (SDK 2.3.2, Desktop under E:/tools); the lightweight adb kit stays | doc 03, 06 |

### Still open (owner)
1. **Gem scarcity vs. gem sales.** Economy v2 yields about 10 gems per level (140 to 220 per region) against the old assumption of 7 to 12 per region (docs 02, 05, 08 section 8). The economy check (`Tests/Simulation/ECONOMY_REPORT.md`) calls the inflow adequate but flags: skip prices (100 to 166) outgrowing income, gem farming by replaying 2-star levels (pickup gems pay in full until 3 stars), very uneven gem counts per level (0 to 35), failed runs losing their gems, and the question whether coins may ever buy gems. Needs an owner decision before gem packs are priced for launch.
2. **Replay rate in data.** The rule is 50 percent for 3-star replays, but `remote_config.json` and the level JSON still carry 40; harmless until Remote Config is wired.
3. **Stitched world map look** (cloud separators) and the order of the later regions on the map: owner review; content order and golden hashes follow.
4. **Art picks to verify:** platform sets for regions 2 to 5, popup frame, map segments. Sunny Beach and Bamboo Temple tilesets (T15) wait for an approval packet.
5. **Endless rules:** final numbers wait for the endless work stream (doc 08 will be updated).
6. **Dev APK size.** The build log shows about 1,077 MB per development APK against the 150 MB install target (doc 01); expected for dev builds with uncompressed art, but a size pass is needed before a store build.
7. Housekeeping from earlier rounds (Unity license check, hibernation file, temp files on C:, second Unity Editor): `.claude/pendingdecisions/open-items.md`.
