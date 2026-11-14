# Economy review 2026-11-13 (step by step, NOTHING IMPLEMENTED YET; owner chose "collect all steps, implement once")

Goal (owner): free engaged player (30 / 60 min a day) must feel gem scarcity; first 100-gem item in about 2 to 3 weeks; campaign + steady power-ups stays free; balanced (about 2 to 3 percent payers).

## Step 1 decisions: levels, Endless, Time Attack (DECIDED)
- Level coins: cut 20 percent (today 70 + 60 per star + 5 per combo step, region factor 1.00 to 1.12). Exact new constants to be set at implementation (about 56 + 48 per star, combo step 4; check rounding and the tests that pin 70/60/5).
- 3-star replay pays 25 percent coins (today 50), no gems.
- Endless and Time Attack: unchanged (coins as is, no gems).
- Gems in play ONLY on levels 5, 10, 15, 20, 25 of every region (all 5 regions), first clear only, no gems on replay. Fixed amounts 1, 1, 2, 2, 3 (9 per region, 45 over the campaign; today about 260).
- Remove every other in-level gem: gem pickups on rows, level 8 hidden gem. L15 keeps the find-the-hidden-gem goal and its hidden gem is the "double gem" (2 gems).
- Show a gem icon with the amount on the level tiles and in the level pop-up for levels 5/10/15/20/25 (tick when claimed).
- Side effects to handle at implementation: golden level hashes change (generator), redeploy leaderboard Cloud Run service (needs owner approval), tests pinning gem pickups and milestone gems, docs 22/27/39/46 and Economy/NOTES.md.

## Items that were parked and are now RESOLVED (kept for the record)
- Hero cards bought with coins: resolved, see "Shop decisions" (Common 2,000, Uncommon 5,000, Rare 12,000, Elite 32,000, Legendary 80,000 coins).
- Free gems per week target for 30 and 60 min a day: resolved by the faucet decisions below (Daily 0 gems, Weekly 50, Main 50 per region, milestones 1,1,2,2,3); the final income check will confirm the pace.
- Daily 200-point Common hero gift: resolved, 50 percent hero else 1,000 coins.

## Review progress
Step 1 levels / Endless / Time Attack: DONE. Hero milestone bonus: DONE. Daily: DONE. Weekly: DONE. Main: DONE. Shop (hero cards, chests, bundles, power-ups, coin and gem packs, revive, region skip): DONE. Subscriptions (4 plans): DONE. Crown display bug (per-setting crowns, 0 when unplayed): DONE IN CODE (not built yet, v0.60.103 still on the phone). NEXT: the final 30 / 60 minute income check (owner chose this next), then ONE implementation pass of everything below.

## Hero milestone bonus (owner, 2026-11-13, DECIDED, not implemented)
Bonus paid by the hero SELECTED for the run, ONLY on the 5 milestone levels (L5/10/15/20/25), FIRST CLEAR only (no replay), added on top of the base milestone gems (1,1,2,2,3):
| Rarity | Coins | Gems | Extra gem |
|---|---|---|---|
| Common | +50 | 0 | none |
| Uncommon | +100 | +1 | 50 percent chance of a 2nd |
| Rare | +200 | +2 | 50 percent chance of a 3rd |
| Elite (Epic) | +300 | +3 | 50 percent chance of a 4th |
| Legendary | +500 | +5 | 50 percent chance of a 6th |
Owner confirmed: a Legendary adds about 5.5 gems per milestone (about 27 per region, 137 over five regions) on top of 45 base gems, intended. This is hero stats, not pricing (card prices are decided in the Shop section). Open for implementation: chance roll must be deterministic/seeded like MilestoneGems; whether hero coins get the region factor / the 20 percent cut (proposal: no scaling, fixed numbers); show the hero bonus on the level pop-up and result screen.

## Daily rewards points track (owner, 2026-11-13, DECIDED, not implemented)
ZERO gems in Daily. The 20 tasks / 200 points stay; the five gifts become:
| Points | Gift (today) | New |
|---|---|---|
| 40 | 500 coins | 200 coins |
| 80 | 5 gems | 400 coins |
| 120 | 1,000 coins | 600 coins |
| 160 | 10 gems | 800 coins |
| 200 | random Common hero | 50 percent a random Common hero, otherwise 1,000 coins |
Coins from the four coin gifts: 2,000 (today 1,500); a full day is 2,000 coins + (hero or 1,000 coins). Daily gems fall from 15 a day to 0. Open for implementation: the 50 percent roll must be deterministic/seeded per day (like MilestoneGems) and survive save/reload; the Daily gift cell for the 200-point gift shows hero-or-1,000-coins; update rewards.json, RewardCatalog.DailyMilestoneRows, RewardsTests, docs 39/27/22. This also settles the "Daily 200-point hero gift" parked item: it is now a 50 percent chance.

## Weekly rewards (owner, 2026-11-13, DECIDED in shape; per-task split is my proposal, not implemented)
Owner choices: perfect week = about 50 gems (today 120), gems ONLY on hard skill tasks, coins cut to about 6,000 (today 10,500), keep 5 power-ups, the two hard-clear Common hero gifts become 50 percent hero else 1,000 coins each. The 25 tasks and ids stay.
Proposed table (gems / coins / items):
| Task | Gems | Coins | Items |
|---|---|---|---|
| w_login5 / w_login7 | 0 / 0 | 150 / 150 | |
| w_play100 / w_play250 | 0 / 0 | 250 / 300 | |
| w_jumps8000 | 0 | 150 | |
| w_powerups10 / w_powerups20 | 0 / 0 | 150 / 150 | 2 PU / 1 PU |
| w_stars20 / w_stars40 | 0 / 4 | 250 / 200 | |
| w_levels20 / w_levels40 | 0 / 0 | 250 / 300 | |
| w_nowall2 | 0 | 200 | |
| w_combos150 / w_combos300 | 0 / 5 | 300 / 250 | |
| w_endless100 / 150 / 200 | 0 / 4 / 6 | 200 / 200 / 250 | |
| w_endlessruns20 | 5 | 250 | |
| w_pace3 | 4 | 200 | |
| w_medals8 | 0 | 250 | 1 PU |
| w_gold5 / w_gold10 | 5 / 7 | 250 / 250 | 1 PU on gold5 |
| w_ta100 | 0 | 200 | |
| w_hard5 / w_hard10 | 4 / 6 | 200 / 200 | each also 50 percent Common hero, else 1,000 coins |
Totals: 50 gems, 5,500 task coins (plus up to 2,000 coins from the two hero-or-coins gifts), 5 power-ups. Estimated: 60-min a day player about 40 to 45 gems a week, 30-min about 15 to 20. Note: with Daily at 0 gems, Weekly is the only recurring gem faucet. The two hero rolls must be seeded (per week and task) and survive save/reload.

## Main goals (owner, 2026-11-13, DECIDED in shape; per-goal split is my proposal, not implemented)
Today (RegionGoalTable, 49 goals per region): 110 gems per region (74 core + 13 gold medals + 23 Endless), 550 over 5 regions; coins about 4,100 to 4,590 core + 7,300 to 8,170 Endless per region; 25 core + Endless power-ups; 6 Common hero gifts per region (stars 75, levels 10/15/20/25, gold 25), 30 in all.
Owner choices:
- Gems: about 50 per region, 250 total, on MILESTONE goals only (all other goals pay no gems).
- Heroes: 2 Common hero gifts per region, on "Clear all 25 levels" and "Gold medals on 25 levels"; the other hero tiers (levels 10/15/20, stars 75) pay coins instead.
- Coins (the one-time Claim bonus, NOT gameplay coins): cut 20 percent, same as level coins (core about 3,300, Endless about 5,800 per region).
Proposed 50 gems per region: levels_5 (clear all 25) 10, stars3_3 (3 stars on 25) 10, gold_5 (gold on 25) 10, stars_5 (75 stars) 8, hard_3 (6 hard levels) 4, endless_3 (floor 500) 3, endless_4 (floor 1000) 3, nowall_2 (no-wall 3 times) 2. All other goals 0 gems (coins and power-ups only).
Impact: Main falls from 550 to 250 gems; the campaign gem total becomes milestones 45 + Main 250 + hero bonus (Common 0) = about 295 over about 6.5 hours of first-clear play plus the weeks of Main chasing.

## Shop decisions (owner, 2026-11-13, DECIDED, not implemented)
- Hero card coin prices (replaces 3,000/6,000/14,000/30,000; "use this everywhere", also as the value of hero rewards): Common 2,000, Uncommon 5,000, Rare 12,000, Elite 32,000, Legendary 80,000 coins. Commons become BUYABLE for coins (HeroPurchase.CoinPrice used to return 0 for Common). Note for implementation: direct buy undercuts the 3-copies-merge ladder (3 Commons = 6,000 > 5,000 Uncommon; 3 Uncommon = 15,000 > 12,000 Rare; 3 Rare = 36,000 > 32,000 Elite; 3 Elite = 96,000 > 80,000 Legendary), so buying direct is always cheaper than merging copies. Owner chose this knowingly.
- Reward rule stays: 50 percent Common hero, otherwise 1,000 coins (Daily 200-point gift, Weekly hard-clear tasks).
- Chest odds: Iron = 50 percent Common, 30 percent Uncommon, 20 percent Rare. Gold = 60 percent Rare, 30 percent Elite, 10 percent Legendary. Prices (100 and 300 gems) and other contents (Iron 1 PU + 800-1,200 coins; Gold 3 PU + 3,000-4,200 coins) unchanged. Owner accepts richer chests: worth about x3.6 (Iron) and x5.3 (Gold) at the new card prices; raise the ChestTests worth cap (was 1.5 to 2.5) and Tests/Simulation/ChestCardTests.cs accordingly.
- Bundles: cut the COIN part about 30 percent; prices (200 to 500 gems) unchanged. Re-check bundle worth (was 1.4 to 1.8, BundleCards, gen_bundles.py) after the cut and after the new card prices.
- Subscription: SUPERSEDED by the four-plan design in the Subscriptions section below (the old two plans, Weekly 100 + 10 gems + 500 coins and Monthly 200 + 10 gems + 600 coins, become Starter and Standard).
- Power-up prices, coin packs, gem packs, revive and region skip: reviewed, see the next section.

## Rest of the shop (owner, 2026-11-13, DECIDED, not implemented)
- Power-up prices: keep (1,000 / 1,500 coins; gem column 50 / 75 stays data).
- Coin packs: keep as is (20 to 25 coins per gem).
- Revive: unchanged (5 gems, then 10, max 2 per run, first free by rewarded ad).
- Region skip: raise to 500 gems PER REGION (was 100 / 116 / 133 / 150 for regions 2 to 5). Implement in Content/regions/*.json (skip price field) and its tests; check how the skip UI and unlock rules use the price.
- Gem packs: keep prices; launch promo and first-purchase double stay OFF.
Review of every shop and rewards item is now complete; remaining step: the final 30/60-minute income check with all decisions, then ONE implementation pass.

## Subscriptions (owner, 2026-11-13, DECIDED in shape, not implemented)
Four plans, fixed-period passes, ONE plan at a time (as today), no auto-renew change. Prices: Weekly Starter $1.49 / 7 days, Weekly Plus $2.99 / 7 days, Monthly Standard $4.99 / 30 days, Monthly Premium $9.99 / 30 days.
| Plan | Instant gems | Daily | Power-ups | Chests |
|---|---|---|---|---|
| Weekly Starter (id weekly, unchanged) | 100 | 10 gems + 500 coins | 1 on claim 4 | none |
| Weekly Plus (new) | 250 | 20 gems + 1,000 coins | every 2nd claim (3) | 1 Iron chest on the FIRST daily claim |
| Monthly Standard (id monthly, unchanged) | 200 | 10 gems + 600 coins | every 5th claim (6) | none |
| Monthly Premium (new) | 300 | 20 gems + 1,000 coins (owner confirmed 2026-11-13) | every 5th claim (6) | Gold chest on claims 1, 11 and 21 (3 chests) |
Value (gems at $0.011, 1 gem = 20 coins, PU 60.7, Iron chest 356 GE and Gold 1,602 GE at the new odds and card prices): "price measure" (chests counted at their gem price 100/300): Starter x3.0, Plus x3.8, Standard x3.9, Premium x3.4; "worth measure" (chests at their luck-adjusted worth): Plus about x4.7, Premium about x7.7. 3 Gold chests = about 0.3 Legendary a month in expectation.
UI (owner): the BUTTON says Pay + price; the card TEXT lists the contents with how many days each repeats (e.g. "10 gems every day for 30 days", "Gold chest on days 1, 11, 21", totals). Keep the "Worth x" badge. The shop must fit four cards without overlap (CUX-3) on 9:16, 9:19.5, 9:21, 3:4; one card per plan, non-scrolling or scrolling VIP page to be decided at implementation.
SETTLED: Premium daily = 20 gems + 1,000 coins (same as Plus; price measure about x4.0, 600 gems + 30,000 coins over 30 days).
Implementation notes: new ids (e.g. weekly_plus, monthly_premium) in SubscriptionCatalog + Content/economy/subscriptions.json + the equality test; SubscriptionScreen / ShopScreen.Subscription layout for four cards (it hard-codes weekly vs monthly look: icon_ticket_weekly / icon_ticket_monthly, gold flag); SubscriptionDef needs chest grants (kind + claim numbers) and PowerUp-on-claim-N support (Starter claim 4); a chest grant opens the chest through ChestRolls at claim (chests are not stored items); Play Billing: four store product ids (Console setup is the owner's step); POLICY.md/TERMS.md only if the billing wording changes (they currently describe two plans); OtherPlanActive rule stays, so Plus cannot be bought during Starter etc.

## Crown display fix (done in code 2026-11-13, not built or installed yet)
Owner report: the crown calculation pop-up repeated the same numbers across settings (a Relaxed crown showed the base score of an Insane run). Cause: `ProgressService.BestCrown` and `BestCrownRun` treated the mode-less legacy `LevelProgress.BestScore` (best base over ALL settings) as a Relaxed crown. Fix: crowns come only from the per-setting `Crowns[mode]` and `CrownRuns[mode]` stored by real runs; no legacy fallback; an unplayed setting shows 0 on the level pop-up, the Endless pop-up and the calculation pop-up (floors, combo points and crown all 0, formula and bonus still shown). The leaderboard is replay-verified on the server with the run's real setting, so it was never affected. Files: Economy/ProgressService.cs, Economy/SaveData.cs (comments), Gameplay/CrownInfoUi.cs, Gameplay/MetaController.cs, tests CrownEditTests, CrownBreakdownTests, PlayMode CrownInfoUxTests (PlayMode not run yet). Crowns already saved from real runs stay; Relaxed crowns that were only the borrowed score drop to 0.

## All decisions at a glance (owner, 2026-11-13)
| Area | Decision |
|---|---|
| Level coins | -20 percent (about 56 + 48 per star, combo step about 4) |
| 3-star replay | 25 percent coins (was 50), no gems |
| Endless and Time Attack | unchanged, no gems |
| Gems in play | only levels 5/10/15/20/25, first clear, 1,1,2,2,3 per region (45 total); no pickups, no L8 hidden gem; L15 hidden gem = the 2-gem reward; gem icon on the level tiles and level pop-up |
| Hero milestone bonus | selected hero, milestone first clear only: Common +50 coins; Uncommon +100 +1 gem (50% 2nd); Rare +200 +2 (50% 3rd); Elite +300 +3 (50% 4th); Legendary +500 +5 (50% 6th) |
| Daily | zero gems; gifts 200/400/600/800 coins, then 50% Common hero else 1,000 coins |
| Weekly | about 50 gems on 10 hard tasks only, about 5,500 task coins, 5 power-ups, 2 hero-or-1,000-coins gifts |
| Main | about 50 gems per region (250 total) on milestone goals only, 2 Common heroes per region, claim coins -20 percent |
| Hero card coin prices | 2,000 / 5,000 / 12,000 / 32,000 / 80,000 (Common to Legendary), Commons buyable |
| Chests | Iron 50/30/20 Common/Uncommon/Rare; Gold 60/30/10 Rare/Elite/Legendary; prices and other contents unchanged; chest worth test cap raised |
| Bundles | coin part -30 percent, prices unchanged |
| Power-ups, coin packs, gem packs, revive | unchanged |
| Region skip | 500 gems per region (was 100 to 150) |
| Subscriptions | four fixed-period passes: Weekly Starter $1.49, Weekly Plus $2.99 (Iron chest on claim 1), Monthly Standard $4.99, Monthly Premium $9.99 (3 Gold chests on claims 1, 11, 21; 20 gems + 1,000 coins a day); one plan at a time; button = Pay, card text lists each benefit with its days |
| Parked / not decided | nothing open; final 30/60 minute income check is the next step, then implementation |

## Final 30 / 60 minute income check (run 2026-11-13; model: Planning/economy_income_check_2026-11-13.py, all inputs are my assumptions)
Engaged free player who does the tasks. 30 min a day: about 110 Daily points, 18 Weekly gems, about 3 free power-ups a week; 60 min: 170 points, 42 Weekly gems, 5 power-ups. Main gems collected: 70 percent (30 min, over 8 weeks) and 85 percent (60 min, over 5 weeks); milestones 45.
| | 30 min a day | 60 min a day |
|---|---|---|
| Steady gems | 18 a week (2.6 a day) | 42 a week (6 a day) |
| Cumulative gems | wk1 48, wk2 103, wk4 197, wk8 357, wk12 429 | wk1 98, wk2 182, wk4 351, wk8 562, wk12 730 |
| First 100 gems | about week 2.0 | about week 1.0 |
| Iron chest (100) at steady rate | 5.6 weeks | 2.4 weeks |
| Gold chest (300) / bundle (364) / region skip (500) at steady rate | 17 / 20 / 28 weeks | 7 / 9 / 12 weeks |
| Coins a week (steady) | about 13,500 | about 29,900 |
| Spare coins after one power-up a day | about 8,500 | about 27,400 |
| Spare coins buy | Rare 1.4 wk, Elite 3.8 wk, Legendary 9.4 wk | Rare 0.4 wk, Elite 1.2 wk, Legendary 2.9 wk |
| Free hero copies a week | 0.5 | 2.2 |
Subscriber gem value a day: Starter 24, Plus 70, Standard 17, Premium 60 (vs 2.6 and 6 free).
Findings: (1) 30-min matches the target (first 100 gems about week 2, steady 5.6 weeks per Iron chest). (2) 60-min is early at first (about week 1) because Main gems are front-loaded and then about 2.4 weeks steady. (3) Coins are still plentiful for a 60-min player: Elite about every 1.2 weeks and Legendary about every 3 weeks with spare coins, even at 80,000. (4) Campaign + one power-up a day stays free for both, as the owner wanted. (5) Subscriptions give 3 to 25 times the free gem flow. (6) Sensitive guesses: Weekly gem tasks reached, Daily points per minute, power-up use, Main completion; verify in soft launch. Levers if too generous: Main gems per region, Weekly gem column, Legendary and Elite coin prices, replay percent.
