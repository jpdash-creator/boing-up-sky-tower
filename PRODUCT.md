# Product

<!-- impeccable:product-schema 1 -->

## Platform

android

## Stack

Unity 6000.3 (C#), uGUI built in code (no prefabs), Android first (IL2CPP ARM64), portrait only. Not a native Material app: the design language is the game's own, with Material's touch and accessibility rules (48 dp targets, system Back, safe areas).

## Users

Casual players of all ages (owner decision 2026-10-20: "all of them"), on a phone, in short one-handed sessions (commute, breaks), mostly portrait with the right thumb on the lower half. The job: climb as high as possible, feel the bounce, collect stars and gems, unlock the next region, show a friend.

## Product Purpose

Sky Hopper (renamed from the working title Boing Tower on 2026-10-20) is a 2D vertical jumper. The player steers a small chibi character up an endless tower of platforms through 30 themed regions (5 playable now) while the screen scrolls up and the bottom edge is the danger. Success: smooth 60 fps play, instant input, clear feedback, a progression of regions, and a shop that never blocks play.

## Positioning

Cute, glossy, neon-rimmed chibi world art (OpenArt generated, locked style bible) with a precise, fair, deterministic jump (a standing jump always reaches the next platform, every level bot-proven solvable) and a long map of 30 regions on one continuous painted world map.

## Operating Context

One thumb on a 6 to 6.8 inch phone, often outdoors or in transit, sound often off, interrupted by calls and notifications (the game must pause). Screens: main menu, world map (scrollable, 30 region pins), level select (15 levels, stars), level popup, run HUD, pause/fail/results overlays, settings, shop (4 tabs), later character and inventory screens.

## Capabilities and Constraints

- Critical UX rules are binding (.claude/rules/ux-gameplay-critical.md): no overlap, nothing clipped, safe areas, 48 dp targets with 8 dp gaps, body text at least 16 sp, 4.5:1 contrast, text survives 130 percent, never colour alone, left-handed mirror, reduced motion, photosensitivity, zero per-frame allocations in a run.
- The OpenArt art that the owner picked is good and stays (backgrounds, map, level nodes, map pins, currency icons, character, popup frame, close icon, platform sets). The redesign is of the UI chrome around it: surfaces, buttons, tabs, typography, spacing, motion.
- Fonts: Lilita One (display) and Nunito Bold (body), already in the game.
- OpenArt credits are never spent without the owner's explicit approval per batch.
- Shop and monetisation follow docs/14 and docs/15; the audience includes children, so no manipulative patterns, clear prices, confirmations for gem spends.

## Brand Commitments

Name: Sky Hopper (no mention of the old name anywhere). Mascot: Momo (fluffy white chibi with a pink scarf). Art world: cute chibi cartoon, neon rim light, glossy sparkle accents, saturated gradients, daylight sky and meadow as home base.

## Evidence on Hand

Phone screenshots in E:/Unity/device_shots; PlayMode screenshots in E:/Unity/shots; art in Assets/Art; decision log .claude/pendingdecisions/device-feedback-2026-10-19.md (lists 1 to 12). No analytics or store data yet.

## Product Principles

1. The world art is the hero; the interface is quiet, crisp and gets out of the way.
2. One thumb, one glance: every screen has one clear primary action in the lower half.
3. Fair and predictable: state, cost and consequence are always visible before a tap (prices, locks, goals).
4. Joyful but never noisy: motion is short and purposeful, always optional (reduced motion).
5. Readable everywhere: contrast, size and spacing are rules, not taste.

## Accessibility & Inclusion

Binding rules in .claude/rules/ux-gameplay-critical.md (CUX-1 to CUX-10): colour-blind safe, reduced motion, haptics and sound toggles, left-handed layout, text scale to 130 percent, accessibility labels, pause on interruption.
