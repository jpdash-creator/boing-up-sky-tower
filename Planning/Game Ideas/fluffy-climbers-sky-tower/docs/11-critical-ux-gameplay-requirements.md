# 11 - Critical UX, Gameplay and Accessibility Requirements (MANDATORY)

**Owner requirement, 2026-10-19:** user experience and accessibility are critical. Gameplay must be seamless and never choppy. Screen items must never overlap improperly. These requirements apply to every doc, every screen and every release, and override convenience and schedule. They extend FR/NFR in doc 01 (performance, accessibility) and are enforced by automated tests (category `CriticalUX`) in the game repo.

The authoritative, always-loaded copy lives in the game repo: `E:\Code\fluffy-climbers-sky-tower\.claude\rules\ux-gameplay-critical.md`. Keep both in sync.

| ID | Requirement | Verified by |
|---|---|---|
| CUX-1 | Seamless play: 60 fps target (30 fps floor), fixed 60 Hz simulation with interpolated rendering, explicit target frame rate, game code at most 2 ms average / 6 ms worst per frame at level 15, zero managed allocations per frame in steady-state play, no sync loading or hitch during a run, time-based camera smoothing | Performance tests (frame time, GC allocations), device profiling |
| CUX-2 | Input latency at most 1 simulation tick; jump taps latched; feedback in the same frame | Input tests, device test |
| CUX-3 | No improper overlap or clipping: HUD, buttons, text and popups never overlap each other, never leave the safe area, never cover the player, next platforms or hazards; works on 9:16, 9:19.5, 9:21 and 3:4 | Automated layout test on every screen and aspect, screenshots |
| CUX-4 | Touch targets at least 48 dp with at least 8 dp spacing; primary actions in the lower thumb zone; left-handed mirror | Layout test, device test |
| CUX-5 | Body text at least 16 sp, contrast at least 4.5:1 (3:1 for large text), text over art has a backing, numbers never clipped, 130 percent text scale | Layout/contrast test |
| CUX-6 | Accessibility: no colour-only cues, reduced motion, haptics/sound toggles, photosensitivity (max 3 flashes per second), audio/visual equivalents, pause anywhere and on interruption, accessibility labels | Tests plus manual accessibility review |
| CUX-7 | Fair and predictable: no unavoidable damage (owner decision 2026-11-01: platform behaviour is discovered by the player, no code-drawn cues on platforms), visible danger zone, one-tap retry, bot-proven solvable levels | Simulation/bot tests |
| CUX-8 | Clear feedback and state; immediate save; locked content explains why | Tests, review |
| CUX-9 | Art coherence: clean sprites, no halos or clipped objects, consistent scale/pivot, readable at drawn size | `qa_art.py` plus visual sheet review |
| CUX-10 | Robustness: portrait lock, safe area, pause/resume, low memory, missing assets never crash | Tests, device test |

Process rules: a change that touches rendering, UI, input, art import or scenes must run the CriticalUX tests and be checked in screenshots (9:16 and 9:21). A failing CUX test blocks the commit. Any exception needs the owner's explicit written approval.

## CUX-9 addendum (owner, 2026-10-23): no stretched images
- No sprite or UI image may be scaled non-uniformly, and no platform piece may have its middle stretched; only flat-colour 9-slice middles may stretch.
- Platforms use a fixed set of real painted widths per region (normal 1.5, 2, 3, 4, 6, 8; crumble, moving and bounce in 2 widths); level widths snap to that set.
- Acceptance: owner-approved contact sheet of every platform width per region, plus an automatic CriticalUX test failing on any non-uniform scale. See 16-ui-polish-and-platform-overhaul.md.
