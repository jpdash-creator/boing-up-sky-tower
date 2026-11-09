# Economy module (T8): save, wallet, rewards, unlocks

Assembly `BoingUpSkyTower.Economy` (references Simulation, Content, Newtonsoft). Plain C# except nothing engine-specific; tested in Unity EditMode (`Assets/_Game/Tests/EditMode/EconomyTests.cs`, 28 tests).

## API
```
var repo = ContentRepository.LoadFromResources();
var svc  = new ProgressService(repo, new FileSaveStore(Path.Combine(Application.persistentDataPath, "save.json")), playableRegionLimit: 5);

svc.CanEnterRegion(region), svc.IsLevelUnlocked(level), svc.GemSkipPrice(region), svc.TrySkipToRegion(region) -> SkipResult
svc.ApplyRun(levelId, new RunOutcome { Completed, StarMask, Score, CoinsCollected, GemsCollected }) -> RewardResult
svc.Coins / Gems, AddCoins/AddGems, TrySpendCoins/TrySpendGems, TotalStars(), StarsInRegion(region), Changed event
```
Build `RunOutcome` from the finished `SimState`: `StarMask = StarEvaluator.Earned(spec, s, revivesUsed, ticks)`, `CoinsCollected = s.Coins`, `GemsCollected = s.Gems`, `Completed = GoalEvaluator.IsComplete(spec, s)`.

## Save
`SaveData` (version 1): coins, gems, per-level `LevelProgress {Stars, StarMask, BestScore, Clears}`, unlocked region ids (region 1 implicit). `FileSaveStore` writes `path.tmp`, keeps the last good file as `path.bak`, swaps, and loads in the order main, tmp, backup, fresh. Corrupt files never throw; a save from a newer app version (version greater than 1) is ignored, not guessed at. `MemorySaveStore` is for tests.

## Rules implemented (doc 01 FR-P5/P6, doc 08 section 7)
- Region 1 is always unlocked; clearing level 15 unlocks the next region if it is playable.
- Gem skip: pay `unlock.gemSkip` (region 2 = 100 ... region 20 = 400) to unlock the NEXT locked region without clearing the previous one.
- Teaser regions can never be entered. `playableRegionLimit` (5 for this milestone) restricts live regions in the build; content still marks regions 6-20 live.
- Coins: end-of-level payout = base * region coinFactor, plus a star bonus, reduced to the replay percent (50, `EconomyRules.ReplayPercent`; `remote_config.json` `economy.replayCoinPercent`) once the level has 3 stars. In-run pickups always pay in full.
- Gems: normal levels only from in-run gem pickups. Milestones: L5 and L10 roll the content chance (25 percent for 1-3 gems) on first clear, L10 guarantees 1; L15 first clear gives 5 plus a star bonus (+2 for two stars, +5 for three), replays 50 percent for 2. Rolls use the Simulation `Rng` seeded from the level seed and the clear count, so a server can recompute them.
- Level N unlocks when level N-1 of the same region is cleared. Best stars use the union of earned star bits; score keeps the maximum.

## Endless mode (T13, owner decision 2026-10-20)
- Save (additive, no version bump, old saves load with defaults): `endlessBest` (region id -> best floor), `endlessDay` (UTC `yyyyMMdd` the counter belongs to), `endlessRunsToday`.
- `ProgressService.ApplyEndlessRun(regionId, floors, dayKey)` records the best floor, pays the coins and saves immediately (CUX-8). The region must be enterable. No revive in endless, so the payout is final when the player falls. A run abandoned from the pause menu records nothing.
- **Coin formula** (`EndlessRewards`, doc 08 section 7): `raw = floors / 2 + 50 * (floors / 100)` (integer division), `full = round_half_up(raw x region.coinFactor)`. Examples at factor 1.0: 40 floors = 20 coins, 100 = 100, 250 = 225, 500 = 500.
- **Daily cap** (remote config `economy.endlessCoinDailyFullRateRuns` 3, `endlessCoinOverCapMultiplier` 0.5): per UTC day (`EndlessRewards.DayKey(DateTime.UtcNow)`) the first 3 paying runs pay `full`, later runs pay `round_half_up(full x 0.5)`. A run with `full == 0` (under 2 floors) does not count against the cap. The counter resets when the day key changes. The cap is global, not per region.
- Not done yet: endless gem drops at floors 250/500/1000 (once per account per day, doc 08 D2).

## Assumptions to confirm (also in `.claude/pendingdecisions/open-items.md`)
1. Star bonus is +10 percent of the payout per star beyond the first (doc 08 says "base + star bonus" without a number). Constant: `EconomyRules.StarBonusPercentPerExtraStar`.
2. A failed run pays nothing and records nothing.
3. Pickups pay in full on replays (only the end payout is reduced to 40 percent).
4. Gem skip only unlocks the next locked region, not any region.
5. Levels inside a region unlock in order.
6. Hidden gems pay no wallet gems yet (Simulation counts them separately).
7. Playable region limit is code, not content: flip regions 6-20 to `teaser` in `gen_content.py` (and the validator rule) when the launch scope is final.
8. Local only: the server-authoritative wallet (PlayFab) replaces this later; the save format is the conflict-resolution unit (server wins for currencies, max wins for progress).

## Shop (docs/14-shop-requirements.md, first slice)
- `ShopCatalog` (gem packs + coin packs from the manifest; built-in copy of the owner tables when the manifest still has the old format).
- `ProgressService.LaunchPromoActive` (default on; `GameSettings.LaunchPromo` feeds it): `CoinPackPrice`, `TryBuyCoinPack` (debit gems and credit coins in one save), `GrantTestGemPurchase` (development builds only, no billing).
- UI: `Gameplay/ShopScreen.cs`, opened by `MetaController.ShowShop` (main menu "Shop" button). Release builds show "Not available yet" on gem packs.

## Power-ups (docs/15, 2026-10-20)
- `PowerUpCatalog` (`PowerUpDef`: id, `PowerUp` type, name, description, duration, uses, carry cap, max stack 99, coin and gem price, unlock region, implemented flag). Runtime source is the built-in table; `Content/economy/powerups.json` is the editable source and an EditMode test keeps them equal. It is **not** part of the content manifest (adding it would make `Resources/Content/content_manifest.json` stale until Unity regenerates it).
- Save (additive, no version bump): `inventory.powerups` (id to count, 1..99) and `loadout` (3 ids, "" = empty). `EnsurePowerUpFields` repairs null, short or over-long data and drops unknown ids.
- `ProgressService`: `PowerUpDefs`, `PowerUpCount`, `GrantPowerUp` (returns units added, capped), `TryConsumePowerUp` (all or nothing, empties loadout slots at zero), `TryBuyPowerUp(id, PowerUpCurrency, quantity)` -> `PowerUpBuyResult` (Ok, NotEnoughCoins, NotEnoughGems, StackFull, Locked, Invalid; atomic, one save), `IsLoadoutCandidate`, `GetLoadout`, `SetLoadoutSlot`, `SetLoadout`, `CarryUnits`, `BuildRunLoadout` -> `LoadoutSlot[3]`.
- `PowerUpCatalog.PriceFor`: unit price x quantity, 5 units -10 percent, 10 or more -20 percent, half up.
- Prices (coins / gems): Flight 2,400 / 20, Rocket 2,000 / 16, Double Jump 900 / 8, Wall Stick 800 / 8, Shield 1,000 / 10, Magnet 900 / 8, Slow Time 1,200 / 10. A 3-star level pays about 150 coins, so a Flight costs about 16 levels (docs/15 conflicts X-2, X-3 still open).
- Tests: `Assets/_Game/Tests/EditMode/PowerUpTests.cs` (class `PowerUpEconomyTests`).

## Bundles (docs/26 section 3, 2026-10-31)
- Data: `Content/economy/bundles.json` (2,000 bundles, ids `bnd_0001`..`bnd_2000`, each `hiddenCard: true`, docs/37) is made by `python Tools/content_cli/gen_bundles.py`, which also writes the identical copy `Assets/Resources/Content/bundles.json`. The game loads that copy through `Resources` (`BundleCatalog.LoadFromResources`, path `Content/bundles`), NOT through the content manifest (so `content_manifest.json` does not go stale and no Unity regeneration is needed). A missing file gives an empty catalog and the page shows a notice. Schema: `Content/schemas/bundle.schema.json`; tests: `Tests/content/test_bundles.py` (python) and `Assets/_Game/Tests/EditMode/BundleTests.cs`.
- Save (additive, no version bump): `SaveData.Bundles` (`BundleSaveData`: the 6 offer ids, seed, draw counter, timer state: next offer in play seconds, current id, expiry UTC ticks, last id, count).
- `ProgressService.Bundles.cs`: `Bundles`, `BundleClock`, `CurrentBundleOffers`, `RefreshBundleOffers(payWithCoins)` (100 coins or 5 gems), `TryBuyBundle(def, out card)` (atomic, one save, no stock cap; draws the hidden hero card from `BundleCards` and adds 1 copy through `AddCopies`), `CurrentTimerOffer(now)`, `TimerOfferPrice` (30 percent off, rounded up), `TimerOfferSecondsLeft`, `TryBuyTimerOffer`, `DismissTimerOffer`, `TickPlayTime(seconds, inRun)`.
- UI: `Gameplay/BundlesScreen.cs` (the page and the chip/card helpers), `Gameplay/BundleTimerButton.cs` (corner button and popup on the home and map screens).

## Rewards (Bug List 5 F1, docs/39)
Main tab = per-region goals: `RegionGoals.cs` (`RegionGoalTable`, the one table to retune, pure data), `RewardCatalog.cs` (Daily/Weekly rows + `RegionGoals(region)`), `ProgressService.RegionGoals.cs` (progress, ready/claimed, `TryClaimRegionGoal`, `ClaimAllRegion`), per-region counters `RegionStats` in `RewardsData.Regions` (additive save fields). Regions 6 to 30 have no goals (Coming Soon). Tests: `Tests/Simulation/RewardTablesTests.cs`, `Tests/EditMode/RegionGoalTests.cs`.

## Active heroes only (owner Bug List 6, 2026-11-09)
Only 5 hero lines are ACTIVE (momo, forestgirl, beachotter, candygirl, monk) = 25 heroes (`HeroTable.ActiveLines`, generated from `ACTIVE_LINES` in `Tools/hero_table_gen.py`). The other 20 lines (100 heroes) are inactive ALWAYS: a compile-time rule, no setting. Their 125-row physics table, `MergeTierDefs.Rows` and `TierRules.Lines/Pools` stay as DATA (`HeroRow.Active`, `CharacterDefs.AllIncludingInactive`, `CharacterDefs.GetIncludingInactive`) for the generator and the bot proof; `CharacterDefs.All` and everything built from it (inventory, All Heroes, merge lines, chest/bundle/Hero-shop pools, rewards, saves) hold the 25 active heroes only, and `CharacterDefs.Get` returns null for an inactive id (saves drop it, the catalog falls back to Momo). Rolls pick the rarity with the usual odds, then an ACTIVE hero of that rarity. The four extra region goals (clear 6/9/12 levels, 45 stars) give `RewardEntry.RandomOpenRegionHero` (one random active Common of an open region). Art of the 100 inactive heroes lives in `Art/_inactive_heroes/<id>/` (outside Assets, so it is not built); move a folder (and its .meta) back to `Assets/Resources/Characters/` to bring a hero back, after adding its line to `ACTIVE_LINES` and rerunning the generator.
