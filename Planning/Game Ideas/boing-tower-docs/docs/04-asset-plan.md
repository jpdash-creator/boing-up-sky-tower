# 04 - Asset Plan (extensible, AI-assisted)

Goal: every asset class has a **template, a prompt recipe, a naming rule, a validation check and a manifest entry**, so adding a new region or item is a repeatable factory job.

## 1. Art direction ("style bible")
- **Updated by doc 08 section 3: add neon rim light and glow, saturated gradients and glossy sparkle accents on top of the style below.**
- Cute chibi cartoon: 2.5-3 head-tall characters, thick consistent outline (3 px at 512 px), soft cel shading with 1 highlight + 1 shadow tone, saturated candy palette, rounded shapes, no photoreal textures.
- Light direction: top-left. Outline colour: darkened hue of the fill, never pure black.
- Each region has a 5-colour palette plus 1 accent, defined in `palettes.json`.
- Produce a one-page **style bible image + text** first; use it as the reference image for every generation (style consistency is the main risk with AI art).

## 2. Asset taxonomy

| Class | Examples | Format | Size | Count at launch |
|---|---|---|---|---|
| Characters (paper-doll body) | Momo + 15 more: head, body, arms, legs, tail layers | PNG layers / PSB | 512 px tall master | 16 |
| Trails/VFX | Momo's default light ribbon, other VFX sheets | PNG sheet | 128 frames | 1 trail + VFX |
| Region tilesets | platforms (normal, narrow, moving, crumble, bounce, ice), walls, edges | PNG atlas | 2048 | 20 + 10 teasers |
| Region backgrounds | 4-6 parallax layers, 1 sky gradient, day-night variants | PNG | 1080x2400 tileable | 20 |
| Hazards/props | per region 4-6 | PNG / sheets | 256 | ~100 |
| Pickups | coin, gem, power-up orbs | PNG / sheets | 128 | 20 |
| Power-up icons | 10 consumables | PNG | 256 | 10 |
| Currency art | coin, gem (3 sizes), gem piles for packs | PNG | 512 | 12 |
| Real-money pack art | 6 gem packs (pouch to treasury vault), starter pack, VIP, battle pass banner | PNG | 1024 | 15 |
| UI kit | buttons, panels, frames per rarity, icons | 9-slice PNG | variable | ~200 |
| Fonts | Lilita One (display) and Nunito Bold (body), free OFL fonts, imported as Unity font assets; not generated with OpenArt (owner decision 2026-10-20) | TTF | - | 2 |
| Region map | **Updated 2026-10-20:** one scrollable world map made of 10 chained OpenArt segments (3 regions each, road-free), stitched with a procedural cloud band; pins and slim name tags are drawn in code | PNG chunks (1080 wide, about 15,764 px tall in total) | 1080 x 3840 | 5 chunks + `world_map.json` |
| Store/marketing | app icon, feature graphic, screenshots, promo video, social cards | PNG/MP4 | store specs | per release |
| Audio | music loops per region, sfx set, jingles. **Placeholder set (2026-10-20):** 14 CC0 Kenney sound effects + one CC0 music loop, see doc 12 and `Content/assets/audio_credits.md` | OGG/WAV | - | 20 loops, ~80 sfx |

## 3. Character system: the paper-doll rig (critical design decision)

All characters share **one standardized skeleton and pose set** so character layers are consistent.
- Skeleton: head, torso, 2 upper arms, 2 lower arms (or mitten hands), 2 legs, 2 feet, optional tail/ears.
- Sprite layer order (back to front): tail, legs, torso, arms, head, face, trail (behind).
- Each character layer is drawn **on the same canvas template** (transparent PNG with the same pivot/bones) so it aligns automatically.
- **No gear (owner, 2026-11-01):** outfits, boots, hats and accessories are not made.
- Animations (idle, run, jump up, peak, fall, land, wall-bounce, combo, win, fail) are bone animations authored **once** on the shared skeleton, via Unity 2D Animation (or Spine). New characters need only new body/head art.
- Rare "special" characters (Legendary) may use a unique rig, but they are the exception.

## 4. AI generation pipeline (repeatable factory)

```
Style bible + reference sheet
   -> Prompt recipe (per asset class, see section 5)
   -> Generate 4-8 candidates (OpenArt / image model via MCP; seed + reference image locked)
   -> Human pick (you) + cleanup (Krita/Photoshop/Photopea: outline, stray pixels, alpha)
   -> Background removal + trim + pivot (script)
   -> Template alignment check (script overlays on the rig guide)
   -> Slice/atlas (Unity Sprite Atlas / TexturePacker)
   -> Manifest entry (JSON) + Addressable group by label
   -> Preview scene screenshot (automated) -> approve -> publish to CDN
```

Practical rules:
- **Fix one model + settings per asset class.** Do not switch models mid-set.
- Generate on a flat solid colour background, then key it out. Prefer models that output transparent PNG natively.
- Keep a **seed/prompt log** with every asset (`assets_log.csv`, in the game repo `Content/assets/`: one row per OpenArt job with date, batch, model, mode, settings, creation id, credits, approval packet and result) so you can regenerate variants.
- **Art rules from device testing (2026-10-19/20):** platforms are drawn at their real aspect ratio (variant chosen by width), never stretched; characters and pickups stand on the walkable surface line (top of the grass, not the neon rim) using a measured per-sprite surface offset; popups use a 9-sliced frame (clean frame, no ribbon, round X icon) and never stretched art; map region names are small tags beside a pin, never translucent black boxes over the art.
- AI struggles with: exact symmetry, consistent hands, text in images, and left/right boot pairs. Mitigation: generate one boot and mirror it; add text in Unity UI, not in art.
- Train/use a style **LoRA or reference-image locking** if the tool allows it (verify the capabilities of your chosen model).
- Legal: verify the commercial-use terms of the generator and keep records. Apple/Google do not forbid AI art, but you must own or license the output rights. Check trademark: no generated art that resembles any existing game's character or branding. Never use another game's name as a prompt or in metadata.
- Animated frames are better produced by rig animation than by AI-generated frames.

## 5. Prompt recipes (templates)

Replace `{...}` values. Always append the **STYLE LOCK** block.

STYLE LOCK (v2, per doc 08 section 3):
> cute chibi cartoon with neon rim light and glow, thick consistent dark-hue outline, saturated gradient colors, glossy sparkle accents, top-left light, clean vector-like edges, solid flat #808080 mid-grey background, no text, no watermark

Key colour (D8): flat mid-grey `#808080`, used by no region palette, because neon glow spills onto a green key. Remove it with a matte pass, and generate the glow as a separate layer where possible, keeping the base sprite glow-free.

**Character body**
> Full-body front view of {name}, a {species} mascot, 3-head-tall chibi proportions, neutral standing pose, arms slightly away from body, {signature colors}, tiny {signature detail}. {STYLE LOCK}

**Region platform tileset**
> Seamless 2D platformer platform tiles for "{region}", {theme materials}, set of: wide, narrow, moving, crumbling, bouncy; wall tile left/right; side view, top lit; {palette}. {STYLE LOCK}

**Parallax background**
> Vertical mobile-game background for "{region}", {environment}, layer {far|mid|near}, horizontally tileable, transparent sky where possible, {palette}, simple readable shapes so gameplay stays visible. {STYLE LOCK minus background colour}

**Power-up icon**
> Glossy round game icon of "{power-up}": {symbol}, bright rarity ring, centered, readable at 64 px. {STYLE LOCK}

**Gem / coin / pack art**
> Game currency illustration: {a single faceted blue-pink gem | a pile of N gems | a treasure chest overflowing with gems and coins}, glossy highlights, sparkle accents, scale tier {1-6}. {STYLE LOCK}

**Real-money pack art ladder** (consistency across tiers): Handful (small pouch + 3 gems) -> Pouch (bag) -> Sack -> Chest -> Vault (open vault door, glowing) -> Treasury (castle of gems with crown). Each step visually bigger and richer than the last.

**Locked teaser region**
> Dark silhouette mood version of "{region}" with a glowing padlock, mysterious purple tint, hints of the theme shapes.

## 6. Folder & naming convention
```
Art/
  Characters/char_momo/{body,head,tail}_*.png
  Items/trail/<id>_<layer>.png
  Regions/region_021_dragon_peak/{tiles,bg_far,bg_mid,bg_near,props,hazards,music}
  Pickups/ PowerUps/ Currency/ Packs/ UI/ Marketing/
```
Naming: `<class>_<id>_<variant>_<size>.png`, lowercase snake_case. IDs are permanent and match the JSON catalog. Version via git, never rename shipped IDs.

## 7. Extensibility: how to add a new X

| To add | Steps | Needs app update? |
|---|---|---|
| **Region** | Generate tileset + 4-6 bg layers + props + music; create `RegionDef` + 15 `LevelDef`; run bot validation; build Addressables bundle; upload to CDN; set `availableFrom` | No (if it uses existing hazard types) |
| **Level** | New `LevelDef` JSON (+ optional authored chunk) | No |
| **Character** | Body + head layers on template; add `ItemDef`; shop slot | No |
| **Power-up** (new effect) | Icon + `PowerUpDef`; if behaviour is new, a code module is needed | Yes (once), then data only |
| **New hazard type** | Code behaviour | Yes (all 30 regions' signature modules are built before v1.0 where possible, see doc 08 section 5) |
| **Gem pack / offer** | Store SKU created in consoles + `GemPackDef`/`OfferDef` | No (SKU must exist in store) |
| **Season / battle pass** | `BattlePassDef` with 50 tiers + bundle | No |

Rule: **behaviour = code (rare updates), content = data + bundles (frequent)**. Never embed content in scenes.

## 8. Asset QA checklist (automated where possible)
Dimensions are power-of-two friendly, alpha clean, no key-colour halo (intentional neon glow is allowed and checked separately; low-end tiers use baked glow sprites), pivot present, palette within region range, file size cap, atlas fits, template overlap passes, name matches catalog, license log entry exists, preview screenshot generated.

## 9. Budget lens (lean)
Hero art (Momo, app icon, store key art, UI kit) deserves hand polish or a freelancer. Bulk items (tiles, backgrounds, icons) use the automated pipeline. Estimated launch asset volume: 20 characters, 20 region sets, 15 pack/currency images, ~200 UI elements.
