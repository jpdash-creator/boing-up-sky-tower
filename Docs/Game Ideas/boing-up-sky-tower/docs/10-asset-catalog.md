# 10 - Asset Catalog and Production Plan (OpenArt)

The complete, exhaustive asset request list is generated in the game repo and kept in sync with docs 02, 04, 08 and 09:

- `E:\Code\boing-up-sky-tower\Content\assets\CATALOG.md` - totals, credit scenarios and the ordered approval batches
- `E:\Code\boing-up-sky-tower\Content\assets\catalog\<class>.md` - every asset with its exact prompt, settings, references, save path, post-processing and acceptance checks
- `E:\Code\boing-up-sky-tower\Content\assets\manifest.json` - the same data, machine-readable
- `E:\Code\boing-up-sky-tower\Content\assets\README.md` - the production workflow and roles

## Summary (launch scope)
| Class | Count | Notes |
|---|---|---|
| Foundation (style bible, Momo, logo, icon, expressions, rig guide) | 7 | rig guide is manual |
| Characters | 15 (+ Momo) | later characters; only Momo and the Sporeling exist (doc 20) |
| Trails | 12 | a character's light ribbon only |
| Live regions (20) | 6 assets each: tileset, 3 background layers, props, signature art | 120 assets |
| Locked teaser regions (10) | 1 each | full sets only when a region goes live |
| Pickups, power-up icons, currency | 3 / 10 / 12 | |
| Pack, shop and chest art | about 20 | 9 gem-pack piles, 7 coin-pack piles, Iron and Gold chests, bundle bag, subscription tickets (docs 22, 26) |
| UI kit sheets | 25 | text always rendered in Unity |
| Map, VFX sheets, marketing | 1 / 8 / 5 | |
| **Total requests** | **247 in the generated catalog (after the gear classes were removed; was 367)** | 745 images still to generate, including candidates |

**Gear removed (owner 2026-10-24 and 2026-11-01):** the outfit, boots, hat and accessory classes are gone from the generator (`Tools/asset_cli/gen_asset_manifest.py`), the generated manifest and the catalog files (RC 13). The trail class stays (a motion effect, 12 assets). Nothing was generated for the gear classes; their candidate images are archived in `Art/_candidates/_archive_gear`.

Audio (music, SFX) and the rig template, layer splitting, wordmark typography and screenshots are not generated with credits (see the catalog).

## Credit reality
At the locked-model price (Seedream 5 Lite, 15 credits per image) the 745 remaining images cost about **11,175 credits**, roughly 0.9 months of the Plus allowance (12,000). Real cost depends on candidate counts and settings; the catalog has a sensitivity table.

## Guardrail
No generation without the owner's explicit approval of the exact batch, with full details (model, settings, prompts, per-job and total credits, balance after, alternatives). See `.claude/rules/openart-credit-guardrail.md` and the approval packet template at `Tools/asset_cli/APPROVAL_TEMPLATE.md`.
