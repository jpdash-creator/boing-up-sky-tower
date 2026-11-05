# 34. Character merge (2026-11-02)

**Source of truth: the code.** Where this file and the code differ, the code wins and this file is updated in the same change. Owner decisions of list 33 items 9 and 10 plus the duplicates plan. Phase 1 (this change) is the rules, the Uncommon content, the save, the Merge screen and the entry. Reward sources that hand out copies are phase 2 and are NOT built.

Code: `Economy/MergeRules.cs` (pure rules and stat-gain text), `Economy/MergeTierDefs.cs` (tier data and builder), `Economy/ProgressService.Merge.cs` (copies, merge, migration), `Economy/SaveData.cs` (`copies`), `Economy/Characters.cs` (`CharacterSpec.Line / ArtId / FallbackArtId / IsMergeTier`), `Gameplay/MergeScreen.cs` (screen, `MergeArt`), `Gameplay/CharacterRig.cs` (placeholder art, `CharacterCatalog.Copies / DebugAddCopies`), `Gameplay/MetaController.ShowMerge`. Tests: `Tests/EditMode/MergeTests.cs`, `CriticalUxTests.MergeScreen*`.

## 1. Rarity ladder and the rule

Common -> Uncommon -> Rare -> Elite -> Legendary (the fourth step is **Elite** in every UI text and table: `CharacterDefs.RarityNames` says "Elite"; the code enum value is still `CharacterRarity.Epic` so saves and code stay unchanged).

- 3 copies of the SAME character (same hero, same tier) merge into 1 copy of the next tier of the same hero. The next tier is a NEW character: own id, own name, own art (armour and clothing improve with the tier).
- Copies are counted per character id. A tier hero has its own id, so a count is per (hero, tier).
- A merge consumes 3 copies and adds 1 copy of the next tier. The lower tier stays owned only while copies are left (0 copies = not owned; Momo can still always be chosen as the default hero).
- A character with no next tier defined (Legendary, or a tier that is not made yet) cannot merge: result `MaxTier`.
- A stack is capped at 99 copies.
- A merged tier hero is a normal hero: it appears in the 5-column hero grid as its own tile in its rarity colour and can be chosen and played.

## 2. Content, phase 1: the five Uncommon heroes

| Line (Common) | Uncommon id | Name |
|---|---|---|
| momo (Momo) | momo_u | Cloudguard Momo |
| forestgirl (Capi) | forestgirl_u | Thornveil Capi |
| beachotter (Otto) | beachotter_u | Tidecrest Otto |
| candygirl (Bonbon) | candygirl_u | Sugarplate Bonbon |
| monk (Bao) | monk_u | Ironbamboo Bao |

Stats, powers and run bonus follow the tier rule in section 2b. The five Uncommon rows are fixed constants in `MergeTierDefs.Rows` (drawn once with `TierRules.Generate`, seed 20261104 + line index 0..4); the numbers are in docs/29 section 6b. They are not in any chest pool and belong to no region.

**Adding Rare, Elite or Legendary later is data only:** one `TierRow` per hero in `MergeTierDefs.Rows` (line id, new id, name, rarity, art id, percent boosts, run coins and gems added; powers come from the line's pool automatically) and the art folder `Resources/Characters/{id}/` (parts and `rig.json` like the Common hero). No code changes: `MergeRules.NextTier` finds the row by line and rarity + 1.

## 2b. Tier rules (owner 2026-11-03, data in `Economy/TierRules.cs`)

**The boosts are a chain on the numbers the player SEES.** Each tier is +10 to +25 percent over the SHOWN stats of the previous tier of the same hero: Uncommon over the Common hero, Rare over the Uncommon version, Elite over the Rare version, Legendary over the Elite version. The four shown stats are exactly the hero sheet's: Jump (a height, launch squared over 2g), Speed, Control and Wall Bounce (speed times wall share), each printed with ONE decimal (`ShownStats`, `Simulation/ShownStats.cs`). A stored +17 percent prints as +17 percent on the sheet and in the merge text. Because the numbers have one decimal, a percent is only valid when the printed result reads back as that exact percent (`TierRules.ValidateChain`; the generator only draws such percents).

| Tier (code enum) | Stats boosted | Boost per stat (over previous tier, shown) | Run bonus coins (own total on top of the Common hero's bonus, steps of 10) | Run bonus gems (own total) | Hero powers | Power cooldown |
|---|---|---|---|---|---|---|
| Uncommon | exactly 2 of 4 | +10% to +25% | +50 to +100 | +1 to +5 | 1 | 30 s |
| Rare | exactly 3 of 4 | +10% to +25% | +200 to +500 | +5 to +10 | 2 | 22 s |
| Elite (`Epic`) | exactly 3 of 4 | +10% to +25% | +500 to +1000 | +5 to +10 | 3 | 15 s |
| Legendary | all 4 | +10% to +25% | +1000 to +2000 | +5 to +10 | 4 | 10 s |

Unboosted stats stay exactly as they were on the previous tier. The run bonus of a tier is its OWN total added to the Common hero's own bonus, NOT cumulative on lower tiers (a Rare Momo pays Momo's 80 plus 200..500, not plus the Uncommon add). `CharacterDefs.PowerCount` is `{0, 1, 2, 3, 4}`. Only Uncommon rows exist; **Rare, Elite and Legendary heroes are NOT created** (no art yet). Their rules are data in `TierRules.Rules` (percent band, boosted count, `OverPreviousTier`, run ranges, powers, cooldown):

- `TierRules.Validate(TierRow)`: pure check of a row against its rule (stat count, percent band, coins band and step, gems band). `TierRules.ValidateChain(prevShown, row)`: every boosted stat prints as exactly its percent. `TierRules.ValidatePowers(line, rarity, powers)`: the power list.
- `TierRules.Apply(prevShown, commonShown, row)`: the shown stats of a tier from the previous tier. `MergeTierDefs.ShownOf` walks the whole chain of a hero.
- `TierRules.Generate(lineId, id, name, artId, rarity, seed, prevShown)`: draws a row from a seed with a built-in generator (deterministic on every runtime, no `System.Random`). Workflow for a new or re-randomised tier: take the previous tier's shown stats (`ShownStats.From` of the Common hero for Uncommon, `TierRules.Apply` of the Uncommon row for Rare, and so on), call Generate once, paste the numbers into `MergeTierDefs.Rows` as constants. Nothing is random at run time. The five Uncommon rows were drawn with seed **20261104** + line index (momo 0, forestgirl 1, beachotter 2, candygirl 3, monk 4) over the Common hero; the test `TheFiveUncommonRowsAreTheFixedSeedDrawAndNeverChange` pins this and `TheGeneratorIsDeterministicAndEveryTierRowObeysCountBandAndChainExactness` checks generated Rare, Elite and Legendary chains for 5 heroes x 150 seeds.

### Safety: shown stats and effective stats

`CharacterSpec.Shown` holds the shown stats (not limited); the hero sheet (`HeroSheetData.From(..., shown)`) and the merge result card (`MergeRules.ShownGainLines`) read them. `CharacterSpec.Stats` holds the **effective** stats the simulation plays: `ShownStats.ToPhysics` turns the shown numbers into whole-number physics (launch = sqrt(2 g x Jump height), Speed and Control rounded, wall share = Wall Bounce / Speed) and `MergeTierDefs.Cap` clamps the four physics stats to the validated limits of docs/29 section 7 (Jump launch 17..23, Speed 8..12, Control 27..45, Wall share 69..100 hundredths). Only the effective stats reach the simulation, so a shown value past a limit is still shown but plays at the limit, and every level stays solvable and not trivialised. Rounding to whole units can move a played stat by up to half a unit from the shown one (for example a wall share derived from a one-decimal Wall Bounce). The run bonus is meta only (paid after the run, never in the simulation) and is not capped by the physics limits; `CharacterStats.ValidateTier()` allows it up to the Common maximum plus the Legendary add (2100 coins, 15 gems). Levels, bot proofs and golden hashes are built with the baseline Momo config and do not change.

**The chain passes the caps.** Four tiers at +25 percent are 1.25^4 = 2.4x. The cap in shown units (what the sheet number must reach before the physics stops growing) and how many full +25 percent steps that is:

| Hero | Common Jump | Jump cap 6.6 | Common Speed | Speed cap 12 | Common Control | Control cap 45 |
|---|---|---|---|---|---|---|
| Momo | 4.5 | x1.47 (about 2 steps) | 9 | x1.33 (2) | 30 | x1.50 (2) |
| Capi | 4.5 | x1.47 (2) | 10 | x1.20 (1) | 28.9 | x1.56 (2) |
| Otto | 4.1 | x1.61 (3) | 8 | x1.50 (2) | 32 | x1.41 (2) |
| Bonbon | 5.5 | x1.20 (1) | 10 | x1.20 (1) | 31 | x1.45 (2) |
| Bao | 5 | x1.32 (2) | 10 | x1.20 (1) | 31 | x1.45 (2) |

Consequence: Uncommon rows can already touch a cap (Bonbon's Jump 6.8 plays at the 6.6 cap), and Rare, Elite and Legendary rows will usually show numbers above what they play. The sheet always shows the owner's chained number; a hero that is capped still gets better from its other stats, its run bonus and its powers. If the owner wants the sheet to show the played value instead, change `HeroSheetData.From` to ignore `shown` (one line); the caps then become visible as smaller gains.

### Hero power pools (region themed)

Powers use only the power-ups that exist in the simulation (Flight, Rocket, Double Jump, Wall Stick, Shield, Magnet, Slow Time). Each pool is ordered: Uncommon gets power 1, Rare powers 1 to 2, Elite 1 to 3, Legendary 1 to 4 (a higher tier keeps the earlier powers and adds the next). Code: `TierRules.Pools`. A power is free to use, ready at the start of a run, then on the cooldown of the table above. Section 2c describes how they work in a run.

| Hero (line) | Region | 1 Uncommon | 2 Rare | 3 Elite | 4 Legendary | Theme |
|---|---|---|---|---|---|---|
| Momo (`momo`) | Meadow Springs, R1 | Double Jump | Magnet | Shield | Rocket | bouncy hops, easy pickups |
| Capi (`forestgirl`) | Mushroom Grove, R2 | Wall Stick | Shield | Magnet | Slow Time | trunks and walls, safe footing |
| Otto (`beachotter`) | Sunny Beach, R3 | Magnet | Double Jump | Flight | Shield | sea breeze, floating |
| Bonbon (`candygirl`) | Candy Canyon, R4 | Double Jump | Slow Time | Magnet | Rocket | springy sweets, sticky slow-down |
| Bao (`monk`) | Bamboo Temple, R5 | Wall Stick | Slow Time | Shield | Flight | wall mastery, calm focus |

## 2c. Hero powers in a run (owner 2026-11-02, built)

**Triggers.** The chosen hero's `CharacterSpec.Powers` become round buttons in the Run scene (`HeroPowerBar`, built by `RunHud`): 0 for Common, 1 Uncommon, 2 Rare, 3 Elite, 4 Legendary. A tap latches a hero activation bit (`SimInput.HeroFirst` 1024, shifted by the power-up type id, `PowerUps.HeroInputBit`) into the next simulation tick, exactly like the bought power-up buttons. A hero use is a normal power-up activation of the same type: **same effect, 60 percent of the duration**, and it **costs no inventory unit** (`RunSession` never touches the stock and does not count it as a used power-up). Runs without hero powers never set the bits, so the bot proofs, replays and level hashes (golden files) are unchanged (a Simulation test asserts it).

**Cooldowns.** Free to use and READY at the start of every run. A use starts that power's own cooldown (`HeroPowerCooldown`, pure logic counted in simulation ticks, so it pauses with the game and replays identically): Uncommon 30 s, Rare 22 s, Elite 15 s, Legendary 10 s (`CharacterDefs.CooldownSeconds`, the same number for every power of the hero). The cooldown starts at the use, not when the effect ends. Each button has its own cooldown. A hero power cannot be used while its own effect still runs (it never stacks onto itself, so a long Shield cannot be kept up for ever); the button is disabled then. Flight and Rocket still exclude each other. A bought power-up of the same type may still be tapped any time and stacks as before.

**Durations** (`SimConfig.HeroPowerDurationPercent` = 60, `HeroPowerDurationFactor` = 0.6; ticks rounded half up by `SimConfig.HeroTicks`):

| Power | Bought | Hero power (60 percent) |
|---|---|---|
| Flight | 10 s | 6 s |
| Rocket | 20 floors in 4 s | 12 floors in 2.4 s (same climb speed) |
| Double Jump | 30 s | 18 s |
| Wall Stick | 30 s | 18 s |
| Shield | 60 s | 36 s |
| Magnet | 30 s | 18 s |
| Slow Time | 30 s | 18 s |

**Buttons.** Round, 150 px (about 57 dp), 28 px gaps, drawn at 50 percent alpha (`ControlPad.ButtonAlpha`), the same icon as the bought power-up of the type, up to 4 in one column starting at the same height as the bought block (`PowerUpBar.BottomOffset`, above the pad) and growing upward. The column is on the move-pad side (`HeroPowerBar.OnJumpSide` = false; a column beside the bought block would reach 530 px from the edge and hit the centred combo and speed-up banners); the left-handed option mirrors it. States use text and shape, not only colour: ready = icon, "FREE" pill and a full gold ring; cooling down = dimmed icon, seconds left in the pill and a blue ring that refills clockwise; effect running = gold pill with the seconds left at the top. The hero sheet and the merge screen already list the powers and the cooldown ("ready again after N s").

**Tests.** Simulation `HeroPowerTests` (category HeroPowers: 60 percent effect, bits, no self stacking, replay determinism, unchanged runs and layouts, cooldown logic), EditMode `HeroPowerEditTests` (button count per tier, cooldown seconds, free use, factor, layout numbers), CriticalUX `HeroPowerButtonsFitEveryAspect...` (screens `run-heropowers` and `run-heropowers-cooldown`, Legendary hero with 4 powers plus 7 bought power-ups, four aspects, notch, 130 percent text, left-handed).

## 3. Art slots (placeholders until OpenArt art exists)

- Hero art: `Resources/Characters/{id}/` (for example `momo_u`). Until a folder with a `rig.json` exists, the game draws the Common hero's pictures instead (`CharacterDef.FallbackArtId`) with a soft green tint in the UI (`MomoDoll.PlaceholderTint`) so a placeholder is easy to spot. Dropping the real parts and `rig.json` into the folder switches over with no code change (the run view draws the placeholder untinted).
- `MergeBackground`: `Resources/Art/Ui/MergeBackground` (a Sprite). Missing = the code-drawn dark purple-navy gradient with a glowing circle, rings and still sparkles.
- `MergeIcon`: `Resources/Art/Ui/MergeIcon` (a Sprite). Missing = a gold four-point sparkle in a ring (`MergeArt.Icon`, usable by the Heroes screen button). The Merge screen's title bar shows it as the object named `MergeIcon`.

## 4. Save and migration

`SaveData.Copies` (`copies`): character id -> count. Save version 2 -> 3.
- A missing key means "not counted yet": an owned character counts as 1 copy (`ProgressService.Copies`). A key with 0 means all copies were merged away (not owned). A key above 0 means owned (this also lets a future reward give a copy of a locked hero).
- Migration (on load, `EnsureCopyFields`): a save older than version 3 gets an explicit 1 for every character it owns (Momo, unlocked region heroes, anything from a chest or the shop) and is stamped version 3. Unknown ids and negative counts are removed. Every merge saves at once.
- SYSTEM_TEST (`TestAllCharacters`) still owns every hero, but the Merge screen lists only heroes with copies.

## 5. Merge screen (`MergeScreen`, screen name `Merge`) (the hero list and selection below are replaced by section 10, 2026-11-05)

Top to bottom: back button and title `MERGE` (with the icon slot); the big result slot (next-tier hero in a glowing circle, name, `Common > Uncommon`, stat gains); a green arrow; three input slots (filled with the chosen hero for each copy held, up to 3; empty ones dim); the gold `MERGE` button (disabled with the reason below it when it cannot work); the grid of heroes with copies (5 columns, scrolls) with `x2/3` counters (`x5` for a top-tier hero). Tap a tile to choose which hero to merge.

On success: save, sound and haptic, then a card `New hero:` with the new hero's picture, name, rarity change and stat gains and one OK button. Entry: the `Merge` icon on the Heroes screen (above the (i) icon, `CharactersScreen.Host.OpenMerge` -> `MetaController.ShowMerge`); Back returns to the Heroes screen. The system-test build has `Add 2 copies` on the Heroes screen (`CharacterCatalog.DebugAddCopies`).

CUX: touch targets at least 48 dp with 8 dp gaps, text at least 16 sp and sized from the real text scale (survives 130 percent), laid out inside the safe area at 9:16, 9:19.5, 9:21 and 3:4, no per-frame work (the screen is built on change only; no Update, no allocation per frame), no motion at all (reduced motion safe), the MERGE state and the ready stack are also written in words (not colour alone), accessibility labels on every control. Audited by `CriticalUxTests.MergeScreenHasNoOverlapAndFitsEveryAspect` and `MergeScreenSurvives130PercentText` (ready, full grid, not enough copies, top tier, other hero, result card).

## 6. Drop odds placeholders (phase 2, NOT built)

Where copies will come from is open; these are placeholders for the later reward-source work (daily, weekly, chests, bundles, subscription). They are numbers to tune, not code.

| Source | Gives | Placeholder odds |
|---|---|---|
| Daily reward | a Common copy of a hero you own | 1 copy on some days (about 1 in 4) |
| Weekly reward | a Common copy | 1 to 2 copies, Uncommon copy about 1 in 10 |
| Iron chest | Common copies | 15 percent per chest |
| Golden chest | Common or Uncommon copies | Common 25 percent, Uncommon 5 percent |
| Gem bundle | a fixed hero copy | by bundle definition |
| VIP / subscription | a Common copy with each daily claim | 1 per week |

Chests now give a hero card as a copy (Bug List 5 F3); the old duplicate gem refund (25 percent) was removed.

## 7. Open points

- Hero powers work in a run (section 2c). Open: the hero column is on the move-pad side; a thumb on the jump side may prefer it there (flip `HeroPowerBar.OnJumpSide`, then the combo and speed-up banners need to move). Not tried on a device yet.
- The merge result card lists at most 5 gain lines (checked for Uncommon); a Legendary row can need 6 (4 stats + run coins + run gems) and must be checked on a device when that tier is added.
- The hero sheet region line says "Merged" for a merge tier (fixed 2026-11-05, `HeroSheetData.For`).
- Changelog: 2026-11-02 document created with phase 1.
- 2026-11-02: Uncommon rows redone to the owner rule (2 stats, run bonus added, 1 region power), tier rules for Rare / Elite / Legendary as data, power pools, shown versus effective stats, Elite name.

## 8. Installed art (2026-11-02)

Owner-approved OpenArt results, cut and installed (no code change needed; the game switches from the tinted placeholder to the real art because `Resources/Characters/{id}/rig.json` now exists).

- **Hero parts** in `Assets/Resources/Characters/{momo_u, forestgirl_u, beachotter_u, candygirl_u, monk_u}/`: `legL, legR, body, handL, handR, head` (+ `back` for momo_u: cape and cloud tail, and candygirl_u: candy cane) and `rig.json` (same format as the Common polygon heroes; order back to front). Sources: `Art/_candidates/uncommon_heroes/` (momo_2, capi_1, otto_1, bonbon_1, bao_1); cut previews next to them as `{id}_cut_preview.png`.
- **Cutter** `Tools/art_pipeline/cut_polygon_character.py`: Capi, Otto and Bao use the Common zone sets `--zones forest|otter|monk` unchanged (no `--ref`, same ECC as the Commons). New: `--zones candy_u` (Bonbon: Common zones, wider cane polygon and outline reach), `--zones momo_u --outline-key --ref Assets/Resources/Characters/momo` (Cloudguard Momo: the painting has a light halo ring and silver armour, so the grey key is replaced by a flood from the corners that stops at the dark outline; registered onto the Common Momo's silhouette; shoulder guards stay with the arms, chest plate with the body, cape and cloud become `back`). Momo_u uses the 7 polygon-hero part names (Common Momo has 9 parts); the rig is data driven so this is fine.
- **Front sprites for the hero sheet/portrait:** the Common heroes have none (only parts + rig), so none were made.
- **QA:** parts do not overlap (0 px), parts stack to the painting, edge grey pixels at the Common level (0.1 to 3.7 percent of edge pixels). Quality notes: Momo_u has a few light fringe pixels at the ear tips (the source has a glow instead of an outline there).
- **Merge screen slots:** `Assets/Resources/Art/Ui/MergeIcon.png` (512 px, grey keyed, the three white discs replaced by lilac hero-head silhouettes drawn in code; 256 px copy in `Art/_candidates/merge_icon/`) and `Assets/Resources/Art/Ui/MergeBackground.jpg` (1080 x 1920, about 200 KB, centre crop of bg_1). Import settings come from `ArtImportPostprocessor` (Art/Ui = UI kit sprite, Characters = picture); no .meta files were written, Unity creates them.

## 9. Inventories (2026-11-03)
The copies table is the player's hero inventory; the master list is the hidden character inventory. See `36-inventories.md`. (Bug List 5 F2, 2026-11-05: the copy badge is gone; the Heroes screen shows one card per owned copy, see section 10.)
- 2026-11-03: owner correction: boosts are a chain of +10 to +25 percent per tier over the previous tier's SHOWN stats (2/3/3/4 stats), defined on the hero-sheet numbers with one decimal; Uncommon rows redrawn (seed 20261104); shown versus effective stats and the cap table.
- 2026-11-05 (Bug List 4 E2): the Shop sells one random hero per card: Uncommon 150, Rare 300, Elite 700, Legendary 1,500 gems (`HeroPurchase`), from heroes of OPEN regions only; the tier (i) popup reads the tier rule numbers above. See `26-shop-bundles-subscription-rewards.md` section 8c.

## 10. Hero screens (Bug List 5 F2, 2026-11-05)

Code: `Economy/HeroCards.cs` (pure: `HeroCard`, owned and merge card lists, selection rule), `Economy/AllHeroes.cs` (pure: groups, locked state, placeholders, how to get), `Gameplay/CharactersScreen.cs`, `Gameplay/AllHeroesScreen.cs` (new), `Gameplay/MergeScreen.cs`, `MetaController.ShowAllHeroes`, `CharacterCatalog.OwnedCards / MergeCards`, `Glyphs.Lock`. Tests: dotnet `HeroCardsTests`; EditMode `HeroScreensTests`, `HeroInventoryTests`; CriticalUX screens `characters`, `characters-test`, `characters-duplicates`, `characters-many`, `all-heroes*`, `merge-list`, `merge-selected-3`, `merge-wrong-selection`, `merge-after`.

**Storage is unchanged** (`copies`: a count per character id, docs/36). Only the presentation changed: no number is shown on any hero card anywhere (the `xN` badge, the `x2/3` pills and `BadgeCount` are removed).

**Heroes screen.** Stage with the hero and the (i) button as before. Directly under the stage and above the list is the action row (`ActionRow`, at least 150 px and 48 dp high): left `All Heroes` (hero icon plus the label, `AllHeroesButton`), right `Merge` (the painted `MergeIcon` plus the word, the whole cell is the touch target, `MergeButton`). The list shows ONLY heroes the player owns, **one card per owned copy** with the same artwork (2 Common Momo = 2 cards, object names `Char_momo`, `Char_momo_2`). A tap on any card selects that hero TYPE for play; the green tick (`Chosen`) sits on the FIRST card of the chosen type only. A visible hero always has at least one card (Momo stays selectable with no copy left). The test-only `Add 2 copies` button is removed from this screen; the admin Character Test popup (Hero test page, `Pick a hero...`) is the copies tool.

**Character Test** (`ProgressService.TestAllCharacters`): owns every COMMON region hero (and skips the reveal pop-up); Uncommon and above are never given by it: they stay locked and appear only in All Heroes until merged, bought or received.

**All Heroes** (`AllHeroesScreen`, screen name `AllHeroes`). Opened from the action row, Back returns to the Heroes screen. Full-screen scrolling list, 3 cards per row, grouped BY REGION under a banner. The groups come from `AllHeroes.Build`: one group per region index named by a Common hero (region names from the content repository); inside a group each Common hero is followed by its line Common, Uncommon, Rare, Elite, Legendary; a tier with no `CharacterSpec` yet is a locked PLACEHOLDER (id like `momo_rare`, name "Rare Momo", rarity colour, silhouette of the line's Common hero). More heroes per region later are just more rows in `CharacterDefs` / `MergeTierDefs.Rows` and show up automatically. Owned heroes are full colour; every other card is LOCKED: muted rarity colour, dark silhouette, padlock (never owned even in Character Test for Uncommon, Rare, Elite and Legendary). Each card has an `i` button (at least 48 dp) that opens the details popup (`AllHeroesInfo`): name, silhouette or picture, rarity, stats (real heroes: the hero sheet numbers and run bonus; placeholders: "Not made yet" and the tier rule ranges), powers with the cooldown, and for a locked hero "How to get": `Merge 3 <tier below> <Hero>` for tiers, `Unlock <Region> or find it in Bundles / Chests / Rewards` for a Common hero, else `Bundles / Chests / Rewards`. The card itself shows the Common hero's name and the rarity word on a dark pill (white, 16 sp minimum); the full tier name is in the popup (a long name does not fit 3 per row at 130 percent text).

**Merge screen** keeps the result panel, the glowing circle, the 3 slots and the MERGE button; the count grid is replaced:
- The list holds every owned hero type that has a next tier and at least 3 real copies, as one card per copy (`HeroCards.Mergeable`). Types with fewer copies and top-tier heroes are not listed. Example: 3 Momo + 2 Capi lists the 3 Momo; later 4 Momo, 3 Capi, 2 Bonbon lists 4 Momo and 3 Capi.
- A tap on a card selects it (green tick plus a gold ring, `Picked`); a second tap deselects; the 3 slots fill in tap order and a tap on a filled slot removes that card. A full selection ignores more taps (remove one first).
- MERGE is enabled ONLY for exactly 3 selected cards of the same hero type. Otherwise it is disabled and the hint (words, no counts) says `Pick 3 of the same hero`, `Pick more of the same hero`, `Wrong mix: pick the same hero` or `Wrong mix: remove a card`; `No hero to merge yet` when the list is empty.
- The result card previews the next tier of the first selected card.
- A merge consumes exactly 3 copies of that type (the save keeps counts), clears the selection and rebuilds the list from the save: the type vanishes when fewer than 3 copies remain, and the new tier (for example Uncommon Momo) appears on the Heroes screen as another card.

CUX: all new controls at least 48 dp with 8 dp gaps, text at least 16 sp from the real text scale (130 percent), layout from real text heights, scroll areas, no Update and no per-frame work (screens are rebuilt on change only), tick and padlock are shapes, not only colour, accessibility labels on every card and button.
