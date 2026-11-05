# 37 Bundles with a hidden hero card (owner Issue 34 item 4, 2026-11-03; reworked for Bug List 1 section 4a, 2026-11-03)

The whole previous pool (540 to 3,000 gems, dollar worth) is gone: data, generator logic, UI text and tests. The new pool is **2,000 bundles**, ids `bnd_0001` to `bnd_2000`
(new seed, new prices, new names; the same id now means a different bundle, so the save keeps a pool version and an older save redraws its offers once).
Every bundle holds coins, 1 to 4 power-up types and **exactly one hidden hero card**.

## Rules (owner Bug List 1 B4.a.i to vi)
- **Price:** every bundle costs 200 to 500 gems (steps of 10), nothing else (Bug List 2 C2.b.i; the v0.46 pool was 200 to 600).
- **Worth:** 1.5x to 2.5x the gems paid (Bug List 2 C2.b.ii; v0.46 was 2.0 to 3.5), **shown as a multiplier only**: the text "Worth x2.1" (format "Worth x" + one decimal) at the TOP RIGHT of the card, plain **italic**, in the **same font as the bundle name**, **no star and no pill/background**. Size = name size x 0.7 but never below the 16 sp minimum: the card name is 42 px, which IS the minimum (16 sp), so the worth text is as large as the name (0.7 x 42 = 29 px would be below the minimum). **No dollars anywhere** (data, generator, schema, UI text, tests, this doc). Worth is measured in gems of value: coins at the shop rate (coins / 20), power-ups at their gem prices (50 or 75), the hero card at its expected gem value (below).
- **Hidden card:** one per bundle, drawn when the bundle is BOUGHT. Odds (weights = percent): Uncommon 70, Rare 20, Epic 10 ("Elite" in the UI). Only characters of regions the player has OPEN. Common is never a card.
- **Icons over text:** the card is shown as the `HeroCardIcon` image with a big white "?" on it, no words.

## The hidden card
- Pool: the characters of the drawn rarity whose region is open. A merge tier (`momo_u`, ...) belongs to the region of the base hero of its line (`BundleCards.RegionOf`); Momo and the Common line are region 1 (always open); other regions count when `ProgressService.IsRegionUnlocked` is true. A hero with no region (loose test data) counts as open.
- Then the hero is picked **uniformly** (sorted by id, so a draw does not change when the list order changes). A new Rare or Epic row is picked up automatically.
- **Fallback:** a tier with no character in an open region uses the highest lower tier that has one (Epic with nothing open at Epic or Rare gives Uncommon). If nothing exists at or below, the lowest tier above. At least Uncommon must exist; if no Uncommon-or-better hero exists in an open region at all, no card is given and the purchase still works. Today only the 5 Uncommon heroes exist (`momo_u` region 1, `forestgirl_u` 2, `beachotter_u` 3, `candygirl_u` 4, `monk_u` 5), so a new account always gets `momo_u`, and every Rare/Epic draw falls back to Uncommon until higher tiers get characters.
- The card is added with `ProgressService.AddCopies(id, 1)` (capped at `MergeRules.MaxCopies`); the wallet, contents and card go out in **one save**. The draw is repeatable from the per-account bundle seed plus `BundleSaveData.CardDraws` (no `UnityEngine.Random`); a failed purchase draws nothing. The tier roll and the character roll are two separate rolls.
- Code: `Economy/BundleCards.cs` (pure: odds, tier gems, region rule, pools, fallback), `ProgressService.Bundles.cs` (`TryBuyBundle(def, out card)`, `TryBuyTimerOffer(now, out card)`, `BundleOpenRegionCount`, `BundleCardPool`), `BundlesScreen.ShowReveal`.

## Worth: the ONE table (`BundleCards.TierGems`)
Gem value of one hero card per tier. Change it here only (and in `Tools/content_cli/gen_bundles.py`; a content test compares both):

| Tier | Weight (percent) | Card value in gems |
|---|---|---|
| Uncommon | 70 | 100 |
| Rare | 20 | 250 |
| Elite (code `Epic`) | 10 | 600 |

Expected card value = (70 x 100 + 20 x 250 + 10 x 600) / 100 = **180 gems**, the same for every bundle, so a lucky draw never changes the stated worth.
Worth (gems) = coins / 20 + power-ups at their gem prices + 180. **Multiplier = Worth / gems paid** (`BundleWorth.Multiplier`, text `BundleWorth.Text` = "Worth x2.1", info line `BundleWorth.Line` = "Worth x2.1 for 340 gems"). `TierGems` (100 / 250 / 600) needed no retuning for 200 to 500 gems and 1.5 to 2.5: the card is 180 gems of the 300 to 1,250 gems of worth, and every price still leaves room for at least 1,000 coins plus one power-up. It stays the one table..
The generator draws a target multiplier per bundle, uniformly between 1.52 and 2.48, and sizes the coins (steps of 50) so the real ratio lands within 0.02 of it, always inside 1.5 to 2.5. The multiplier is therefore varied, not constant, and a cheap bundle is not systematically better or worse value than a dear one.

Numbers of the shipped set (2,000 bundles):
- **Price (v0.47 pool):** 200 to 500 gems, average 365.6; per hundred: 200-299 = 451, 300-399 = 738, 400-499 = 771, exactly 500 = 40.
- **Worth multiplier:** 1.518 to 2.480, average 2.05; 1.5-1.75 = 351, 1.75-2.0 = 456, 2.0-2.25 = 613, 2.25-2.5 = 580 (11 different one-decimal values shown).
- Coins 2,450 to 29,100; power-up types per bundle 1/2/3/4 = 690/734/388/188; bag size by price (under 300, under 450, above) = 477/757/766; 2,000 distinct names (all at most 24 characters) and 2,000 distinct contents.
- The **timer offer** (30 percent off for 5 minutes) is a sale shown only by the cheaper gem price: it displays the worth at the LIST price (the same "Worth x2.1" as the normal card, always 1.5 to 2.5). The info popup opened from it reads "Worth x2.1 for 238 gems" (list worth, gems really paid).

## Chests share this logic (Bug List 5 F3)
The Treasure Chests draw their hero card the same way (`Economy/ChestCards.cs`, details in docs/26): same open-regions predicate, same pool (`BundleCards.Pool` for Uncommon and up, plus the base hero of each open region for Common), same fallback to the next lower tier, same card gem values (`BundleCards.TierGems`; `ChestRolls.CommonCardGems = 40` is the only addition). Iron: 70 Common / 20 Uncommon / 10 Rare, worth x1.8. Gold: 50 Uncommon / 40 Rare / 10 Elite, worth x1.9. A duplicate card is another copy, in bundles and chests alike. Bundles still never give a Common.

## Generator and data
`python Tools/content_cli/gen_bundles.py` (splitmix64, fixed seeds, no wall clock) writes `Content/economy/bundles.json` and the identical game copy `Assets/Resources/Content/bundles.json`. Fields: `id, name, tier, priceGems, coins, powerups[], hiddenCard: true`; no hero is ever named in the file and no money value is stored. Schema `Content/schemas/bundle.schema.json` (price 200 to 500, `joke` required, 10 to 140 characters). New seeds: contents `20261108`, names `8112026`. Names are "Adjective Noun" from 56 adjectives and 40 nouns (the v0.46 words are reused on purpose, the owner allowed it; 2,240 combinations, 2,000 used in a new shuffled order, so the ids map to different bundles).
- **Jokes (info popup only):** `joke` = the noun joke + " " + the adjective joke (`NOUN_JOKES` 40 + `ADJ_JOKES` 56 = 96 jokes in `gen_bundles.py`), e.g. "Syrup not included, regrets neither. Cozy, pairs well with a blanket and a sigh." 2,000 different names give 2,000 different lines (longest 100 characters). Family friendly, no dollars, no competitor names.
Save compatibility: `BundleSaveData.PoolVersion` (`BundleCatalog.PoolVersion = 3`); a save with another version drops its 6 offers and its timer offer once and draws again. The per-account seed and card counters are kept.

## UI
- **Card** (a gold row): left column = the **"i" button** (48 dp, top left, object `BundleInfo`) over the bag; middle = the name and compact chips (coin, each power-up with amount, the `HeroCardIcon` with "?"); right column = the **italic "Worth x2.1" text** (top right, object `Worth`, no star, no pill) over the green gem-price button. It is right aligned inside the strip between the name and the right edge of the card, so it never meets the "i" target, the name, the bag or the price button; the row grows with text scale.
- **"i" popup** (`BundlesScreen.ShowInfo`, object `BundleInfoPopup`): title = bundle name; the contents as icon chips with amounts (coins, power-ups, "?" card); a line "HeroCardIcon + Heroes from your open regions (N)"; three columns "Uncommon 70%", "Rare 20%", "Elite 10%" in their rarity colour; the pill "Worth x2.1 for 340 gems" and, below it, the **joke** in small italic text (object `Joke`, 16 sp and up, wrapped, panel height measured). Text 16 sp and up, built for 130 percent; the same "i" sits at the top left of the timer-offer popup (it opens the info popup and OK/X returns to the offer).
- **Hero card icon:** object name `HeroCardIcon`; sprite `Resources/Art/Ui/hero_card_icon.png` when it exists, else `icon_heroes` (existing). To use an OpenArt icon, drop it there under that name; no code change. The white "?" is a text child (`Mark`).
- **Confirm popup** ("Buy X for N gems?", gems you have, "Worth x2.1", "Includes 1 hidden hero card (?)"), **reveal popup** (hero portrait, "You got: name (rarity)" in the rarity colour, the worth pill "Worth x2.1", the rest of the bundle as icon chips) and the **timer offer** (bag, "i", italic worth text, chips with the "?" card, was/ends in, Buy, Hide) all use the same helpers.
- Refresh (5 gems or 100 coins), 6 offers at a time, the 45 minute timer offer and the per-account seed draw are unchanged; every price (200+ gems) shows the confirm popup (threshold 50 gems).
- CUX: layout audit screens `bundles-page-*`, `bundles-info`, `bundles-confirm-popup`, `bundles-card-reveal`, `bundles-card-reveal-long-name`, `bundles-card-reveal-no-hero-fallback`, `timer-offer-popup`, `timer-offer-info` at 100 and 130 percent text.

## Tests
- `Tests/content/test_bundles.py`: count 2000, schema, ids and names unique, every price 200 to 500, every worth ratio 1.5 to 2.5 and varied, 2,000 different jokes built from the joke tables, card table equals the C# source (`Weights`, `TierGems`), no dollars in data/generator/schema, new names, determinism against both files.
- `Tests/Simulation/BundleCardTests.cs`: odds over 100,000 draws within 1 point of 70:20:10, uniform pick, fallback, region rule (only open regions, fallback when a tier has nobody open, new account = Momo's line), the shipped file (price, worth, no `$`).
- `Assets/_Game/Tests/EditMode/BundleTests.cs`: 2,000 bundles 200-500 and 1.5-2.5 via `BundleWorth`, no dollar strings in the bundle sources, 2,000 jokes, pool version 3, purchase adds exactly 1 copy in one save, card pool grows with unlocked regions, old pool version redraws, timer offer card, repeatable draws, save round trip.

- **Hero card mark (C2.b.v):** icon and "?" only, no words. The white "?" now sits on a dark rounded disc (`MarkBacking`, near black violet at 88 percent) so it reads at 4.5:1 or better on any icon art.
