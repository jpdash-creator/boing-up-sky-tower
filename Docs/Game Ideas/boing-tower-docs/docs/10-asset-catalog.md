# 10 - Asset Catalog and Production Plan (OpenArt)

The complete, exhaustive asset request list is generated in the game repo and kept in sync with docs 02, 04, 08 and 09:

- `E:\Code\boing-tower-game\Content\assets\CATALOG.md` - totals, credit scenarios and the ordered approval batches
- `E:\Code\boing-tower-game\Content\assets\catalog\<class>.md` - every asset with its exact prompt, settings, references, save path, post-processing and acceptance checks
- `E:\Code\boing-tower-game\Content\assets\manifest.json` - the same data, machine-readable
- `E:\Code\boing-tower-game\Content\assets\README.md` - the production workflow and roles

## Summary (launch scope)
| Class | Count | Notes |
|---|---|---|
| Foundation (style bible, Momo, logo, icon, expressions, rig guide) | 7 | rig guide is manual |
| Characters | 15 (+ Momo) | 16 total per doc 02 |
| Trails | 1 | Momo's default ribbon only; no gear (owner, 2026-11-01) |
| Live regions (20) | 6 assets each: tileset, 3 background layers, props, signature art | 120 assets |
| Locked teaser regions (10) | 1 each | full sets only when a region goes live |
| Pickups, power-up icons, currency | 3 / 10 / 12 | |
| Pack, shop and chest art | 19 | gem ladder, starter, VIP, pass, offers, chests |
| UI kit sheets | 24 | text always rendered in Unity |
| Map, VFX sheets, marketing | 1 / 8 / 5 | |
| **Total requests** | **235** | about 709 images including candidates |

Audio (music, SFX) and the rig template, layer splitting, wordmark typography and screenshots are not generated with credits (see the catalog).

## Credit reality
At the default planning tiers (30 credits for hero art, 15 for standard) the full set is about **10,635 credits** (at 15 per image; was 16,575 before gear was removed), roughly 0.9 months of the Plus allowance (12,000). Real cost depends on the model locked in Gate 0 and on candidate counts; the catalog has a sensitivity table.

## Guardrail
No generation without the owner's explicit approval of the exact batch, with full details (model, settings, prompts, per-job and total credits, balance after, alternatives). See `.claude/rules/openart-credit-guardrail.md` and the approval packet template at `Tools/asset_cli/APPROVAL_TEMPLATE.md`.
