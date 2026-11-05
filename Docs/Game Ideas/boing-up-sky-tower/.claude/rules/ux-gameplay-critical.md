# CRITICAL REQUIREMENTS: seamless gameplay, no improper overlap, accessibility

**Owner requirement (2026-10-19): user experience and accessibility are critical. Gameplay must be seamless and never choppy. Screen items must never overlap improperly.**
These rules are MANDATORY. They override convenience and speed. Before any change to rendering, UI, input, art import or scenes, check it against this list.
If a change cannot meet a rule, stop and tell the owner instead of shipping it. Every rule below has an ID (CUX-n); automated tests carry the same ID in their name or category `CriticalUX`, and the whole suite must be green before a commit that touches these areas.

## CUX-1 Seamless play (never choppy)
- Target 60 fps on mid-range devices, 30 fps floor on the low tier. The simulation runs at a fixed 60 Hz and the view interpolates, so any display rate looks smooth.
- `Application.targetFrameRate` is set explicitly on boot (60); mobile default is 30 and must never be relied on. vSync/refresh handled in one place (`PerformanceSettings`).
- Per-frame game code budget: average at most 2 ms, worst frame at most 6 ms on the reference machine, at 150 platform rows (level 15).
- ZERO managed allocations per frame in steady-state play (Run scene: Update, input, view, HUD). No string building per frame (cache, update only on change), no LINQ, no `new` in hot paths, no `foreach` over non-struct enumerators.
- No synchronous loading, shader compile or import hitch during a run. Scene loads are async with a progress indicator. Warm up first-use assets before the run starts.
- Camera and effects use frame-rate independent smoothing (time-based), never per-frame lerp constants.

## CUX-2 Input is instant and never lost
- Input is sampled every rendered frame and applied on the very next simulation tick (at most 1 tick = 16.7 ms latency). A jump tap is latched until consumed. Coyote time and jump buffer stay on.
- Every input gets feedback within the same frame (squash/stretch, sound, haptic if enabled).

## CUX-3 No improper overlap, nothing clipped
- HUD, buttons, texts, popups and labels never overlap each other, never extend outside the safe area (notches, rounded corners) and never cover the player, the next platforms or any hazard.
- Child elements stay inside their panel; popups fit completely on every supported aspect: 9:16, 9:19.5, 9:21, 3:4.
- Decorative art may sit behind UI only if the UI has a readable backing.
- Verified by the automated layout test on every screen (Boot, Menu, Map, Levels, Settings, popups, Run HUD, pause, failed, results).

## CUX-4 Touch targets
- Interactive elements are at least 48 x 48 dp (about 9 mm) with at least 8 dp between neighbours. Primary actions sit in the thumb-reachable lower half. One-handed play must work; offer a left-handed mirror option.

## CUX-5 Legible text
- Body text at least 16 sp (about 36 px on the 1080 x 1920 reference), headings larger. Contrast ratio at least 4.5:1 against the immediate background (3:1 for text of 24 sp or more). Text over busy art always has a backing panel or outline.
- Numbers (coins, floors, scores) are never clipped or wrapped. Text must survive 130 percent scale.

## CUX-6 Accessibility
- Never rely on colour alone: platform kinds, hazards, stars, button states and the danger zone also differ by shape, icon, pattern or motion. Colour-blind-safe palette checked.
- Settings: reduced motion (no screen shake, no flashes, no parallax scroll, no pulsing), haptics on/off, sound/music on/off, left-handed layout.
- Photosensitivity: nothing flashes more than 3 times per second; no full-screen flashes.
- Audio cues have visual equivalents and the reverse. The game can be paused at any moment and pauses automatically on interruption (call, notification shade, app switch).
- Buttons carry accessibility labels where the platform supports them.

## CUX-7 Fair and predictable gameplay
- Hazards telegraph before they hurt (a warning state with a shape/motion cue). No unavoidable damage. Every level is bot-proven solvable (already enforced by the Simulation tests).
- The danger zone is always visible. Failing and retrying takes one tap. Revive offer never blocks the retry.

## CUX-8 Clear feedback and state
- Success, failure, pickups, combos and unlocks are unmistakable. Progress is saved immediately after every result. Locked content explains why it is locked.

## CUX-9 Art coherence
- Sliced sprites are clean: no clipped objects, no grey key-colour halos, consistent scale and pivot, readable at the size they are drawn. Every sprite passes the automated art QA (`Tools/art_pipeline/qa_art.py`) and is reviewed visually per sheet before it is used.

## CUX-10 Robustness
- Portrait lock, safe-area handling, app pause/resume, low-memory behaviour. Missing art or content never crashes the game (visible fallback instead).

How to apply: when you add or change a screen, run the layout/accessibility/performance tests (category `CriticalUX`) and look at screenshots at 9:16 and 9:21. A failing CUX test blocks the commit. Keep this file in sync with `docs/11-critical-ux-gameplay-requirements.md` in the docs folder.
