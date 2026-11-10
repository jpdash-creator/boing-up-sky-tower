# 36. Character inventory and hero inventory (owner Issue 34, items 2 and 3)

## Definitions
- **Character inventory** (`CharacterInventory`, `Economy/CharacterInventory.cs`): the hidden, static, read-only master list of every character that exists
  (Common heroes plus every merge tier): id, hero line, tier, region, rarity, unlock rules. It is the source for chest and bundle pools and for rules.
  It holds nothing about a player and is **never shown to players as a list**. `All` returns a copy; no member takes or returns a save.
  Order: by hero line (list order of the Common heroes), then tier (Common, Uncommon, ...).
- **Hero inventory** (`HeroInventory`, `Economy/HeroInventory.cs`): the heroes one account or guest owns, with a copy count per character id.
  It is what the Heroes screen lists. Owned = copies of 1 or more. Get it with `progress.HeroInventory`. Each profile/save has its own.

## Data model
The hero inventory is a view over `SaveData.Copies` (JSON `"copies"`, `Dictionary<string,int>` keyed by character id, introduced in save v3 by the merge work).
No format change, no version bump. A missing key means "not counted yet": a character owned by the old rules (bought or received, the Common hero of an unlocked region,
Momo) counts as 1 copy. 0 means all copies were merged away. Fields added by the login work (`accountMode`, `accountId`) are untouched.

## API (`progress.HeroInventory`)
| Call | Meaning |
|---|---|
| `Copies(id)` | copies held (0 if unknown or not owned) |
| `Owns(id)` | true when owned (copies >= 1; SYSTEM_TEST owns all) |
| `AddCopies(id, n)` | adds n (cap 99), saves once, returns the new count; unknown id or n < 1 changes nothing |
| `Owned()` | ordered list (hero then tier) of the heroes the player may see; a region hero whose reveal is pending is not listed yet |

`ProgressService.Copies / AddCopies / OwnsCharacter / TryMerge` stay as thin entry points (Copies and AddCopies call the hero inventory). Bundles, chests and rewards
call `progress.AddCopies(id, n)` or `progress.HeroInventory.AddCopies(id, n)`. Region heroes are still granted (copy 1) when their region unlocks.
`CharacterCatalog.Available(p)` is the screen list (same order); `CharacterCatalog.OwnedCards(p)` the cards (one per copy).

## Heroes screen
(Bug List 5 F2, 2026-11-05) No counts are shown any more. The screen lists one card per owned copy (`HeroCards.Owned`), see docs/34 section 10. Adding copies of a character the player does not have adds its card.

## Migration
Saves older than v3 get an explicit 1 for every owned character on load (`EnsureCopyFields`). Unknown ids and negative counts are dropped. Nothing else changes.

## Tests
EditMode `HeroInventoryTests` (separation, API, new card, one card per copy, merge, old save, per-profile); dotnet `CharacterInventoryTests` (complete, ordered, copy);
PlayMode CriticalUX `characters-duplicates` and `characters-many` screens (no overlap, 130 percent fit).
