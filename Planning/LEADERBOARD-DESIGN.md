# Weekly crown leaderboard: design (owner decisions 2026-11-12)

Owner answers 2026-11-12 are built in: Q1 campaign slots count COMPLETED levels only; Q2 ranks 1-3 above the limit but below 1.5x get the 4th-10th prize; Q3 server-issued single-use seed tickets (section 5.5) and the cap of 8 bought power-ups per type; Q4 one-time consent popup (section 5.7); Q5 server receive time decides the week; Q6 prizes are item counts; Q7 the leaderboard screen is the 4th bottom-bar icon (Shop, Leaderboard, Heroes, Daily Rewards) on world map and level screen, prizes are claimed in Rewards.

Status: SERVICE DEPLOYED 2026-11-12 (Cloud Run `fluffy-leaderboard`, see the deploy section in 12 and `Planning/LEADERBOARD-DEPLOY-LOG.md`); the app flag stays OFF. Earlier text below was written before the deploy. Firebase project `fluffy-climbers-tower-jump`, Firestore (production mode) and Cloud Run in `asia-south1` (Mumbai), players sign in with Google Play Games and get a Firebase Auth uid.

Files delivered with this document:

| File | What |
|---|---|
| `firestore.rules`, `firestore.indexes.json`, `firebase.json` (repo root) | Security rules, composite indexes + TTL policies, Firebase config |
| `Tools/leaderboard_service/` | .NET 10 ASP.NET minimal API for Cloud Run: token check, replay, slot/total updates, my-week, prize claim |
| `Tests/LeaderboardService/` | 30 xunit tests (run `dotnet test Tests/LeaderboardService`), including replays of runs recorded by the REAL `RunSession` |
| `POLICY.md`, `TERMS.md` | Updated to 2026-11-12 (section 12) |
| `Assets/_Game` (2 small edits) | `RunSession` records revives, `RunRecord.ReviveAt` (section 5.3) |

## 1. The rules being implemented

Three weekly boards, all measuring **crowns**: `overall`, `levels` (campaign, regions 1 to 5), `endless` (Endless + Time Attack, regions 1 to 5).

- A **slot** is the unit whose best crown of the week counts: campaign `(level id x Gameplay setting 0..4)` = 125 levels x 5 = **625 slots**; endless `(region x setting x {endless, timeattack})` = 5 x 5 x 2 = **50 slots**. Slot ids: `c.<levelId>.<mode>`, `e.<regionId>.<mode>`, `t.<regionId>.<mode>`.
- A player's weekly total on a board = sum over its slots of the best crown set this week in that slot. `overall = levels + endless`. A lower or equal run never changes a slot.
- **Campaign slots count a run only if the level was COMPLETED** (goal reached in the replay). Endless and Time Attack count the run when it ends. An uncompleted level run answers `200 {accepted:true, counted:false, reason:"level_not_completed"}` and writes nothing.
- Crown of a run = `CrownScore.FromScore(state.Score, mode)` = `(floors x 10 + combo^2 x 5) x (100 + settingPercent) / 100`, `settingPercent` = {10, 20, 25, 40, 50}, integer, rounded half up (`Assets/_Game/Economy/CrownScore.cs`, written by the other worker; the service calls only that one function). Everything counts: revived runs, power-ups, the player's own rolled hero stats.
- Week = Monday 00:00 India time (Sunday 18:30 UTC). Week key = ISO date of that Monday in IST (`2026-11-09`). The week of a run is the week of the server clock when it is received.
- Entry limits (prizes only, remote-config values in Firestore `config/leaderboard`, not in the rules): overall 200,000, levels 150,000, endless 50,000. Ranks 1 to 3 also need 1.5 x the limit. Top 10 of each board are paid.
- Prizes (claimed once per week from the Rewards screen): Overall 1st Bundle + Gold chest, 2nd Bundle + Iron chest, 3rd Gold chest, 4th-10th Iron chest + 2 power-ups. Levels and Endless: 1st Gold chest + 3 power-ups, 2nd Gold chest, 3rd Iron chest + 2 power-ups, 4th-10th 3000 coins + 1 power-up (`Logic/LeaderboardRules.cs`, test `PrizeTablesAndEligibilityFollowTheOwnerDecision`).

## 2. Data model (Firestore, Native mode, asia-south1)

Doc ids and field names are the ones in `Store/Docs.cs`. Times are Firestore timestamps (UTC).

```
players/{uid}                                  profile + per-player service state
  displayName, avatarUrl, updatedAt            CLIENT-WRITABLE (only these three, see rules)
  nameLocked                                   moderator flag (service/console)
  pid                                          public id = hex(HMAC-SHA256(salt, uid))[0..8]; boards never show the uid
  heroVariants {heroId: variantIndex}          first variant used per hero (locked, see 5.6)
  lastSubmitAt, dayKey, ticksToday             durable rate limit state
  createdAt

weeks/{weekKey}/slots/{uid}_{slotId}           best run of the week in one slot (<= 675 per player per week)
  uid, slotId, board(levels|endless), kind(level|endless|timeattack), levelId|regionId, mode,
  crown, floors, ticks, score, revives, heroId, variant, seed, simVersion, gameVersion, runId,
  at, expireAt (TTL: week end + 90 days)

weeks/{weekKey}/totals/{uid}                   the three weekly totals of a player
  uid, pid, name, avatar (copy of the profile at the last improvement),
  overall, overallAt, levels, levelsAt, endless, endlessAt   (*At = when that total last grew: tie-break, earlier wins)
  slotCount, updatedAt, expireAt

tickets/{ticketId}                             one doc per USED run ticket (uid, at, expireAt = ticket expiry + 1 day, TTL); written by the service only

weeks/{weekKey}/boards/{board}                 materialised top 100 (board = overall|levels|endless), 1 doc per board per week
  entries[{rank, pid, name, avatar, crowns}] (<= 100, about 15 KB), cutoff (crowns of rank 100 when full, else 0),
  rebuiltAt, final (true once the week is closed), limitUsed, topThreeFactorPercent, expireAt

prizeClaims/{weekKey}_{board}_{uid}            one per player, board and week; created by the claim transaction
  uid, weekKey, board, rank, crowns, bundles, goldChests, ironChests, powerUps, coins,
  state(issued|granted), createdAt, ackedAt, expireAt

config/leaderboard                             limits (see Limits in LeaderboardRules.cs); written by the owner
```

Why a document per slot: a slot update is one small transactional write, totals never need a scan, and the TTL deletes a week without any cron. A player has at most 675 slot docs a week. `weeks/{weekKey}` itself is never written (only its sub-collections), which is fine in Firestore.

### Indexes (`firestore.indexes.json`)

- `totals` composite, scope COLLECTION: `(overall DESC, overallAt ASC)`, `(levels DESC, levelsAt ASC)`, `(endless DESC, endlessAt ASC)` serve the top-100 query (order by total desc, then earlier timestamp).
- `totals` composite `(overall ASC, overallAt ASC)` and the same for `levels` and `endless` serve the "equal crowns reached earlier" part of the rank count (equality on the total, range on the timestamp). The "more crowns" part uses the automatic single-field index.
- Slots by `uid` use the automatic single-field index (`where uid == me`).
- TTL policies (`fieldOverrides ... ttl: true`) on `expireAt` of `slots`, `totals`, `boards`, `tickets`, `prizeClaims`: weekly rows disappear 90 days after the week ends (Firestore deletes within about 24 h of expiry). `players` has no TTL (kept until deletion on request).

## 3. Authentication (exact validation, `Auth/TokenVerifier.cs`)

The client sends `Authorization: Bearer <Firebase ID token>`. The service accepts the call only if ALL hold:

1. The token has three parts, header `alg` is exactly `RS256` (anything else, `none` included, is refused) and a `kid`.
2. The `kid` matches one of Google's current public certificates from `https://www.googleapis.com/robot/v1/metadata/x509/securetoken@system.gserviceaccount.com` (cached until the `Cache-Control` max-age, at most one forced refresh per minute for an unknown `kid`), and the RS256 signature verifies against it.
3. `iss` = `https://securetoken.google.com/fluffy-climbers-tower-jump` and `aud` = `fluffy-climbers-tower-jump`.
4. `exp` is in the future and `iat` is not in the future (clock skew 60 s). Tokens live one hour.
5. `sub` is a non-empty string of at most 128 characters; it is the Firebase uid and the ONLY identity used (never a uid from a body or query).
6. `auth_time`, when present, is not in the future.

Test: `TokenTests` (valid; wrong issuer, audience, expired, future iat, empty/long sub, foreign key, unknown kid, RS512, alg none, junk). No per-request revocation lookup (tokens are short lived; accounts are removed on request, section 10).

## 4. Security rules (`firestore.rules`), rationale

Principle: **clients can read what they need and write only their display profile; every score, total, rank, claim and config write is made by the service account, which bypasses the rules.** The rules contain no limits or prize logic, so changing a limit never needs a rules deploy.

| Path | Client read | Client write |
|---|---|---|
| `players/{uid}` | get own only | create / update own, ONLY `displayName` (1 to 32 chars), `avatarUrl` (https or empty, at most 512), `updatedAt == request.time`; blocked when `nameLocked`; no delete |
| `weeks/*/boards/{board}` | get, any signed-in player (the public top 100) | never |
| `weeks/*/totals/{uid}` | get own only | never |
| `weeks/*/slots/*` | get / list only where `uid == auth.uid` (queries must filter by uid) | never |
| `prizeClaims/*` | get / list only own (`uid == auth.uid`) | never |
| `config/*` | get, signed-in | never |
| everything else | denied | denied |

Why the player profile is client-written: the Play Games display name and avatar URL are only known on the device; the service copies them into `totals` and the boards. A client can lie about a name, which is why `nameLocked`, length/URL checks, the Terms (section 12a) and moderation exist; it cannot touch anything score related. Boards show `pid`, never the uid.

**Rule tests.** The Firebase emulator is not installed on this machine (no jar in `~/.cache/firebase`, no `@firebase/rules-unit-testing`), so the rules were NOT run. Manual matrix to run once with `firebase emulators:exec --only firestore` (or in the Rules Playground) before deploy:

1. unauthenticated: every read and write denied.
2. user A reads `players/B`, `weeks/W/totals/B`, a slot with `uid == B`, `prizeClaims/W_overall_B`: denied; the same for A: allowed.
3. user A lists `weeks/W/slots` without `where uid == A`: denied; with it: allowed.
4. user A writes `weeks/W/totals/A`, `weeks/W/slots/*`, `weeks/W/boards/*`, `prizeClaims/*`, `config/*`: denied.
5. user A creates `players/A` with `{displayName:'Momo', avatarUrl:'', updatedAt: serverTimestamp}`: allowed; with an extra field (`pid`, `nameLocked`, `heroVariants`): denied; displayName of 33 chars or `avatarUrl:'http://x'`: denied; `updatedAt` a client time: denied.
6. user A updates only `displayName` + `updatedAt`: allowed; updates `lastSubmitAt`: denied; updates while `nameLocked == true`: denied; deletes `players/A`: denied.
7. any signed-in user gets `weeks/W/boards/overall`: allowed; lists the boards collection: denied.

## 5. The verify-then-write flow

### 5.1 What the client uploads (`POST /submit-run`, `RunSubmission`)

```json
{ "simVersion": 1, "gameVersion": "0.59.1", "kind": "level|endless|timeattack",
  "levelId": "lvl_001_02", "regionId": null, "mode": 2, "seed": 4242,
  "heroId": "momo", "heroVariant": 0,
  "inputsB64": "<RunRecord.Inputs, InputCodec run-length bytes>", "reviveAt": [1850],
  "runId": "<guid>", "ticket": "<from /start-run>", "claimed": { "floors": 61, "ticks": 3559, "score": 1020, "crown": 1275 } }
```

`seed` = campaign attempt seed (`RunSession.AttemptSeed`) or the tower seed of Endless / Time Attack (`RunSession.Seed`), and it MUST be the seed from `POST /start-run`; `ticket` is the signed ticket from that call (section 5.5). Everything is already in `RunSession.BuildRunRecord` (`Inputs`, `ReviveAt`, `LayoutSeed`/`AttemptSeed`, `HeroId`, `Mode`, `GameVersion`); the client adds `heroVariant` (`ProgressService.HeroVariant(heroId)`), `simVersion` (new constant) and the claimed numbers. Size: about 1 to 4 KB for a level, a long endless run a few tens of KB; body limit 512 KB, inputs limit 256 KB.

### 5.2 What the server does (`RunReplayer`, `LeaderboardApi.SubmitRunAsync`)

1. Token -> uid. In-process throttle (cheap, before any CPU). Ticket check: valid signature, not expired (410 `ticket_expired`), bound to this uid, kind, level or region, setting and seed (403 `bad_ticket` / `ticket_mismatch`).
2. Reject unsupported `simVersion` (HTTP 426: update the app), bad mode/kind, unknown hero, a variant that is not a real entry of the hero's pool (`HeroRolls.IsValidVariant`; Common heroes are variant 0 only), unknown level or a region above `maxRegion` (5).
3. Decode the inputs (length checked from the run-length data BEFORE allocating), cap at `maxTicks` (216,000 = 1 h), reject any input bit outside Left/Right/Jump/the seven bought power-up bits (a legitimate recording never holds hero bits, see 5.4).
4. Rebuild the world exactly as the game does: `SimConfig` (defaults + `GameSession.ApplyDifficulty`: scroll step percent, gem chance, top speed-up of the setting; mirrored in `RunReplayer.ConfigFor`, pinned by the test `TheServerConfigMatchesWhatGameSessionDoes` that reads `GameSession.cs`), hero physics `HeroRolls.StatsFor(hero, variant).ApplyTo(cfg)`, level spec `LevelSpecFactory.Build(level, region, seed)` + `LevelBuilder.Build` + `Sim`, or `LevelSpecFactory.BuildEndless` + `EndlessWorld`. All of these are the game's own source files, linked into the service project from `Assets/_Game` (no copy).
5. Step the recorded inputs (`Sim.Step`, `EndlessWorld.Grow`, goal evaluation, Time Attack clock: the same order as `RunSession.Step`), put every revive back at its recorded tick (`Sim.Revive`), stop if the run ends before the inputs do (reject: `inputs_after_end`).
6. Compare the claimed floors, ticks, score and crown with the replay. Any difference: HTTP 422 `mismatch`, nothing is written, the ticket stays usable. The crown that is stored is always the replayed one. Then burn the ticket (409 `ticket_used` if it was used), and for a campaign level that was not completed answer `counted:false` without writing.
7. Durable checks and the write in ONE Firestore transaction (`SlotMath.Apply`): per-player minimum interval (2 s) and daily replay-tick budget, plausibility cap on one run's crown, hero-variant lock; then read slot + totals, and only when the new crown is **strictly greater** than the stored slot crown write the slot and add the difference to `levels` or `endless` and to `overall`. A lower or equal run writes nothing (except the player's rate-limit fields).
8. If the player's new total can be in a board's top 100 (above `cutoff`, or the board is not full) and the board was last rebuilt more than `boardRebuildSeconds` ago, rebuild that board's top-100 document (100 reads). Cloud Scheduler also calls `POST /admin/tick` once a minute (header `X-Admin-Key`, secret in Secret Manager) as a safety net and to freeze the previous week.

Replay cost measured here (test `ReplayCostIsMeasured`): a 3,559-tick level run replays in about 12 ms including building the level, a 60-floor endless run in about 8 ms; the 1-hour cap is about 0.7 s of CPU. A semaphore limits concurrent replays to the core count.

### 5.3 Can the recorded stream reproduce revives, power-ups and hero powers? Findings

I read `RunSession`, `Sim`, `HeroPowerCooldown`, `InputRecorder`/`InputCodec`.

| Event | In the current stream? | Result |
|---|---|---|
| Left / Right / Jump | Yes | replays exactly |
| **Bought power-up** | Yes. `RunSession.Step` records the input AFTER `TakePowerUnits`, so only accepted activations (stock available, `CanActivate`) are in the stream, as `PowerUps.InputBit(type)` bits | replays exactly; no stock needed on the server |
| **Hero power** (free, cooldown) | Yes, as the SAME bit: `TakeHeroPowers` turns an accepted hero tap into `InputBit(type)` (Bug List 6: same strength and duration as a bought one) before the recorder sees it | replays exactly; the server re-runs `HeroPowerCooldown` only to tell free uses from bought units |
| **Revive** (gems or free ad) | **No.** `Sim.Revive` is called between two `Step`s, outside the input stream. Without it the replay would see a dead player followed by more inputs | **Fixed (minimal, allocation-free):** `RunSession` records `inputRec.Ticks` at each revive into a `GrowingInts` (`reviveRec`) and `RunRecord.ReviveAt` carries them (default empty). Replay applies `Sim.Revive` before tick `ReviveAt[i]` and requires the player to be dead there |
| Player's rolled hero stats | Not in the stream (they come from the per-account roll) | upload `heroId` + `heroVariant`; the server rebuilds the physics from the pool (`HeroRolls`) |
| First-clear gem stripping (`stripGems`) | Not in the stream | harmless: gems never change the climb or `Score`; test `ARealBotRunReplaysToTheSameFloorsTicksAndCrown` (third case) proves it |
| Pause / resume | Not ticks | no effect, as intended |

Proof, in `ReplayTests`, using the real `RunSession` linked into the test project: bot runs on 3 levels (and `stripGems`), a gem revive and a free-ad revive in the middle of a level followed by the bot finishing it, bought Shield / Slow Time / Flight, a free hero power, Endless and Time Attack; in every case the server replay gives the same floors, ticks, score, crown and revive count as the session. Negative tests: tampered inputs, forged claim, wrong seed or setting, no `ReviveAt` (-> `inputs_after_end`), revive while alive (-> `bad_revive`), power-up spam above the cap (-> `power_budget`), hero bits in the stream, broken run-length data, over-long input, wrong sim version.

**Client changes still needed (not done here, Unity side):** after a run, build the `RunSubmission` from `BuildRunRecord` (+ `heroVariant`, `simVersion`) and queue it for `POST /submit-run` (retry while offline); the recorded `Inputs` are currently dropped for records above a size cap in `TimeRecordData` (`MaxInputBytes`), the leaderboard upload must use the fresh `RunRecord` of the run, not the stored best record; `RunRecord.Counts` (no revive) must NOT gate the upload (revived runs count).

### 5.4 Residual risks (cannot be closed by replay alone)

1. **Power-up stock is client-owned.** The game has no server inventory, so the server cannot know how many units a player owns. Mitigation implemented: free hero uses are verified by cooldown, and bought units per type per run are capped (`maxBoughtUnitsPerTypePerRun`, default 8). A cheater with an edited save can use up to that cap in every run. Real fix: server-side inventory (a later phase), or lower the cap.
2. **Seeds are chosen by the server (built, section 5.5).** What remains: a player can request many tickets (0.5 s minimum between `start-run` calls per player) and keep only the layout that suits them, simply by not submitting the rest; a ticket can also be solved offline by a bot before it expires (120 min).
3. **Bots and tool-assisted play** produce legal inputs and pass the replay. The leaderboard fights this with the prize limits, plausibility caps, report/moderation and `nameLocked`. A superhuman-input heuristic (inputs per second, perfect timing variance) can be added from the stored inputs later.
4. **Hero variant shopping.** A cheater could try different variant indexes of a hero. All variants of a hero are equal strength by construction (Forgiveness Index gate) and the first variant used with a hero is locked per account (`variant_locked`), so the gain is nil to small. Stricter: upload the account seed and the roll counter and let the server recompute `HeroRolls.Pick`; the seed is a private save field, so this is optional.
5. **Sim version drift.** The service compiles the game's Simulation sources. After ANY change to the simulation, level generator, hero tables, content manifest or `GameSession.ApplyDifficulty`, redeploy the service from the same commit as the release, bump `simVersion` (client constant + `Limits.SimVersions`) when old clients can no longer replay, and keep old versions in `simVersions` only while the service still carries their code. A run from a client the server cannot replay is rejected with 426, never accepted blind. `Content/golden/level_hashes_r1-5.txt` already pins level layouts.
6. **The week is the server's receive time.** A run played on Sunday night and uploaded after Monday 00:00 IST counts for the new week. Harmless; documented.

### 5.5 Run tickets (`POST /start-run`, `Logic/TicketService.cs`)

1. The client asks `POST /start-run {kind, levelId | regionId, mode}` when the player starts a run. The server checks the target exists and is playable and picks a random non-zero seed (`RandomNumberGenerator`).
2. It returns `{ticket, seed, expiresAt}`. The ticket is `base64url(payload).base64url(HMAC-SHA256(key, payload))`, payload `v1|uid|kind|target|mode|seed|expiryUnix|nonce`, key derived from the server secret (`LB_PID_SALT`, rotate to invalidate all tickets), life `ticketMinutes` (120, a run is capped at 1 h).
3. The client MUST use that seed (campaign `attemptSeed`, endless / Time Attack tower seed) for the run. Offline play without a ticket is still possible but never reaches a leaderboard.
4. `submit-run` accepts only a ticket that verifies, is unexpired, and matches uid, kind, level or region, setting and seed exactly. Single use: after the replay matches, the nonce is stored in `tickets/{nonce}` by a create-if-absent transaction; a second submit gets 409 `ticket_used`. A run that fails to replay, mismatches or is refused earlier does NOT burn the ticket (the client may resend); once burned, a ticket cannot be reused with other inputs.
5. Tests: `ARunNeedsAValidSingleUseTicketBoundToUidTargetSettingAndSeed` (no ticket, wrong uid, level, setting, seed, forged signature, reuse), `AnExpiredTicketIsRefused`, `EndlessTicketsAreBoundToTheRegionAndKind`.

Client work: call `start-run` on PLAY (campaign, Endless, Time Attack) when signed in and online; use the returned seed instead of `GameSession.NewAttemptSeed`/`NewEndlessSeed`; keep the ticket with the run; if the call fails the run is played with a local seed and is not uploaded.

### 5.6 Hero variant lock

(See risk 4 above.) The first variant a hero is used with on the leaderboard is locked per account.

### 5.7 Consent popup (Q4, client text and flag)

Shown once, the first time a signed-in player opens the leaderboard screen or would upload a run (never for guests):

- Title: "Join the weekly leaderboards?"
- Body: "Your Google Play Games name and profile picture will be shown to other players on the leaderboards. Your runs are sent to our server so it can check your scores. You can stop any time by playing as a guest."
- Buttons: "Join leaderboards" (primary) and "Not now". A link "Privacy Policy" opens POLICY section 4a.
- Stored flag in the save (additive field, device-only, plus cloud save): `leaderboardConsent: { "v": 1, "at": <unix seconds> }`. No run is queued or sent, no profile written and no `start-run` called until it exists. "Not now" stores nothing and asks again at the next leaderboard open (at most once per day). Bump `v` when the data shown or sent changes, which asks again. Signing out of Play Games keeps the flag but stops uploads; "Leave leaderboards" in Settings clears it.

### 5.8 Own display name (owner 2026-11-13, Main Bug List 1 item 2e)

After a Google account is linked, Settings shows a "Name" row (only when the leaderboard is enabled or the Admin test switch is on). The popup sets a display name stored in the save (`leaderboardName`, additive, nullable, travels in the cloud save). Rules (`Assets/_Game/Leaderboard/LeaderboardNameRules.cs`, pure, tests in `LeaderboardNameTests`): 3 to 16 characters after trimming; letters of any language (`char.IsLetter`), digits, single spaces between words and underscore; leading, trailing and repeated spaces are removed; a small built-in word filter (leetspeak and separators folded, short words only as whole words, no external service); NO uniqueness check; free to change; empty = back to the Play Games name. The hub writes the profile (`players/{uid}.displayName`) with the own name when set, else the Play Games name, and writes it again when the name changes (`LeaderboardHub.SetDisplayName` then `SyncProfile`; only with consent and a signed-in Firebase user, otherwise the name is only stored and goes out with the next sign-in). The Firestore rules and the server needed no change (the field is the same string, at most 32 characters). POLICY.md section 4a describes it.

## 6. Endpoints (`Program.cs`, `LeaderboardApi.cs`)

| Endpoint | Purpose |
|---|---|
| `POST /start-run` | body `{kind, levelId or regionId, mode}`; 200 `{ticket, seed, expiresAt}`; 400, 401, 429 |
| `POST /submit-run` | body = `RunSubmission`; 200 `{accepted, counted, improved, weekKey, slotId, crown, previousCrown, slotCrown, floors, ticks, totals{overall,levels,endless}}`; 401, 422 (`mismatch`, `bad_inputs`, `too_long`, `bad_revive`, `power_budget`, `unknown_*`, `bad_variant`, `variant_locked`, `implausible`, `inputs_after_end`), 403 (`bad_ticket`, `ticket_mismatch`), 409 `ticket_used`, 410 `ticket_expired`, 426 (update the app), 429 |
| `GET /my-week?slots=1&prizes=1` | `{weekKey, pid, weekEndsAt, totals, ranks[{Board,Crowns,Rank,EntryLimit,ReachedLimit}], slots?, pendingPrizes?}`; `pendingPrizes` freezes and reads the last 4 weeks (claim window) |
| `POST /claim-prize` | `{weekKey, board}` -> prize and `grant`; `{weekKey, board, ack:true}` after the app saved it |
| `POST /admin/tick` | `X-Admin-Key`: rebuild current boards, freeze last week (Cloud Scheduler, every minute) |
| `GET /healthz` | liveness |

Reading a board does not need the service: the app reads `weeks/{weekKey}/boards/{board}` directly with the Firestore SDK (rules allow it): 1 read per board view.

## 7. Ranks

- Sort order everywhere: crowns DESC, then `<board>At` ASC (the earlier time the total was reached wins), then document id (never visible).
- Top 100: one query per board `orderBy(board DESC).orderBy(boardAt ASC).limit(100)` (index above), written as the `boards/{board}` document (rebuild rules in 5.2 step 8). Players with 0 crowns are not listed.
- A player's own rank (`GET /my-week`): `1 + count(total > mine) + count(total == mine and at < myAt)` with two Firestore COUNT aggregations per board (6 per call, each billed as about 1 read per 1,000 index entries scanned).
- At the week end the first `claim`/`my-week?prizes=1` call (or `POST /admin/tick`) writes the board with `final: true` (transactional: the first writer wins) and freezes `limitUsed` and `topThreeFactorPercent`; later config changes never change a finished week.

## 8. Prize eligibility and the claim transaction

Eligible = rank 1 to `paidRanks` (10) in the FROZEN top 100 of the board AND crowns >= the board's entry limit; ranks 1 to 3 also need crowns >= 1.5 x the limit. A rank 1 to 3 player that passed the limit but not 1.5 x gets the 4th-10th prize, no other rank shifts (owner decision Q2; test `ATopThreePlayerBetweenTheLimitAndOnePointFiveTimesGetsTheFourthToTenthPrize`). Claim window: from the week end until `claimWindowDays` (28) later.

`POST /claim-prize {weekKey, board}`:

1. Week must be closed (409 `week_open`) and inside the window (410).
2. Freeze the board (no-op when final), find the caller by `pid` (403 `not_ranked` / `not_eligible`).
3. Firestore transaction: read `prizeClaims/{week}_{board}_{uid}`; if absent `create` it with state `issued` and the prize; return the stored claim. A second call returns the same claim (idempotent, no second document, `Create` fails if it exists).
4. The app grants the prize in its save, then calls `claim-prize` with `ack: true` (state `issued` -> `granted`). While the state is `issued` the response says `grant: true` again, so a crash between server and save never loses the prize; after the ack `grant` is always false. The app also keeps the claim id in its save so a repeated response is never granted twice.

Tests: `PrizesAreClaimedOnceAfterTheWeekClosesWithTheFrozenRankAndLimits`.

## 9. Anti-abuse summary

Token on every call, uid only from the token; server-issued single-use seed tickets; server replay of every run; claimed numbers must match; input bits whitelist; byte cap 256 KB, tick cap 216,000, body cap 512 KB; revives <= 40, only in campaign; bought power-up units capped per type; sim-version gate; in-process throttle + durable minimum interval (2 s) + daily tick budget (4,000,000 replayed ticks); `maxCrownPerRun`; hero-variant lock; slot only improves; clients cannot write anything but a display name; `nameLocked` for moderation; rows expire after 90 days; the admin endpoint needs a secret. All limits are in `config/leaderboard` (defaults in `Tools/leaderboard_service/config.default.json`).

## 10. Costs at launch scale (estimates, check the current Mumbai price list)

Assumption: 10,000 daily active players, 8 submissions each a day (80,000 submits/day), 5 `my-week` calls and 3 board views per player per day.

| Item | Per day | Notes |
|---|---|---|
| Submit reads | about 360,000 | slot + totals + player + 1 to 2 board docs per improved submit |
| Submit writes | about 145,000 | player doc always, slot + totals when improved (about 40 percent) |
| Board rebuilds | up to about 430,000 reads at the default (a rebuild is 100 reads, at most one per board per `boardRebuildSeconds`; set 60 s) | falls to near zero when boards are quiet |
| `my-week` | about 400,000 reads (1 totals + 6 count aggregations + overhead) | |
| Board views | 30,000 reads | 1 doc per board view |
| Stored rows | slots: players x slots played x 90 days; totals 1 per player-week | TTL removes them; well under 1 GB |

Roughly 1.2 million reads and 0.15 million writes a day. At the usual Firestore list prices (about 0.04 to 0.07 USD per 100,000 reads, 0.12 to 0.21 per 100,000 writes, Mumbai is in that range) that is about 0.7 to 1.2 USD a day for reads and 0.2 to 0.3 for writes: **about 30 USD a month at 10,000 DAU**, a free-tier-sized bill below about 1,000 DAU (50,000 reads and 20,000 writes a day are free). Cloud Run: 80,000 replays x about 15 ms is about 20 CPU-minutes a day, inside the free tier; keep `min-instances 0`, `concurrency 8`, 512 MiB, CPU only during requests. Cloud Scheduler: 1 job, free. Egress is small (boards are 15 KB per view). Biggest lever: `boardRebuildSeconds` and how often the app calls `my-week`.

## 11. POLICY.md and TERMS.md: what had to change (done, both dated 2026-11-12)

Per CLAUDE.md the documents were updated in the same change as the feature design. The exact additions (all in the files now):

POLICY.md
- "Last updated: 2026-11-12". In short: first bullet now says "no server of its own except the optional weekly leaderboard service that runs only if you sign in with Google Play Games (section 4a)"; new bullet: "If you sign in with Google Play Games and play the optional weekly leaderboards, your Google Play Games display name, a link to your Google Play Games profile picture, and your weekly scores are stored on Google Cloud servers in Mumbai, India (Firebase), and other players can see your display name, picture and score on the boards. Guests are never on a leaderboard (section 4a)."
- Section 2: "We do not operate a server for normal play. The only exception is the optional weekly leaderboard (section 4a), which runs on Google Cloud services that we configure and control and that keep a small database of the players who use it."
- Section 4: the display name and photo sentences now point to 4a ("unless you use the weekly leaderboards", "photo files. (If you use the weekly leaderboards ... the Game also reads the link to your Play Games profile picture.)", "Apart from the optional weekly leaderboard (section 4a), the Game publishes nothing about you.").
- NEW section 4a "Weekly leaderboard (optional, only when signed in)": what is sent (level or tower, setting, hero and stat variant, power-ups used, recorded inputs, score, version, run id), what is stored (best crown per level/setting/mode, weekly totals, display name and picture link, a derived public player id, timestamps, prize claims), that name and picture are shown to every other player (top 100 of each board), "processed for us by Google as our processor ... Firebase Authentication, Cloud Firestore and Google Cloud Run, in Mumbai, India (asia-south1)", retention ("deleted automatically 90 days after the week ends", profile kept until deletion on request), deletion by e-mail within one month, prizes.
- Section 7 table: rows for Firebase Authentication, Cloud Firestore, Google Cloud Run (processors, asia-south1).
- Section 8: no-account, no-chat/user-content and no-server bullets reworded (name and picture are shown on boards only if you sign in and play them; leaderboard service is the only server); the "feature that changes any of this" sentence now records that leaderboards were added on 2026-11-12.
- Section 10: new legal-basis row. Section 11: Google as our processor; other players see name, picture and score; "no other hosting provider". Section 12: leaderboard data stored in Mumbai, public in the Game. Section 13: two retention rows (weekly data 90 days; profile until deletion). Section 14, 16, 17: guest is on no board; we are controller for leaderboard data; the database accepts no app writes. Section 18: Data safety rows for "Name and photos" and "Other actions" (leaderboard) and the "Not collected" sentence.

TERMS.md
- "Last updated: 2026-11-12"; NEW section 12a "Weekly leaderboards and prizes": availability (signed-in only, weekly reset Monday 00:00 India time), "Your name and picture are public", "Runs are checked by us" (server replay), cheating and moderation (including name changes, exclusion, no refund), prizes (top 10, entry limits, 1.5 x for ranks 1 to 3, Virtual Items with no cash value, claimed once, expire 28 days after the week), "No guarantee". Section 11 gets a Leaderboards bullet (not part of the save, kept 90 days).

Still to do OUTSIDE the documents before release (same rule in `Planning/LEGAL-HOSTING.md`): re-host both documents, update the Play Console Data safety form (name and photos, "other actions", user IDs now collected, data shared publicly for display name and picture), keep the in-app policy links, add the leaderboard opt-in/consent surface in the UI (a first-time "Your Play Games name and picture will be shown to other players" confirmation is recommended), and add the deletion e-mail route to the store listing's data-deletion URL answer.

## 12. Deployment checklist

**DEPLOYED 2026-11-12 (owner approved).** Service `fluffy-leaderboard`, project `fluffy-climbers-tower-jump`, region `asia-south1`, URL `https://fluffy-leaderboard-763742589585.asia-south1.run.app`, first revision `fluffy-leaderboard-00001-zhn` (100 percent traffic). Public (`--allow-unauthenticated`), max 5 / min 0 instances, 1 CPU, 512 Mi, timeout 60 s, concurrency 40. Runtime service account `fluffy-leaderboard-run@...` with `roles/datastore.user` (project) and `roles/secretmanager.secretAccessor` on secret `lb-pid-salt` only. Built from a Dockerfile (not buildpacks). `LB_ADMIN_KEY` is NOT set, so `/admin/tick` always answers 401 (no Cloud Scheduler yet). Note: use `/healthz/` WITH the trailing slash on run.app, plain `/healthz` is answered 404 by Google's front end. Smoke: `/healthz/` 200, POST `/start-run`, `/my-week`, `/admin/tick` without token 401. Nothing written to Firestore. Commands: `Planning/LEADERBOARD-DEPLOY-LOG.md`. The checklist below is the original plan.

1. Enable APIs (Firestore, Cloud Run, Cloud Build, Artifact Registry, Secret Manager, Cloud Scheduler, Identity Toolkit); create Firestore (production mode, `asia-south1`).
2. `firebase deploy --only firestore:rules,firestore:indexes` (indexes take minutes to build; deploy before the first query). Seed `config/leaderboard` from `config.default.json`.
3. Secrets: `LB_PID_SALT` (random 32 bytes), `LB_ADMIN_KEY`. Service account of the Cloud Run service: `roles/datastore.user` only.
4. Deploy from the repo root so the linked `Assets/_Game` files are in the upload: `gcloud run deploy leaderboard --source . --region asia-south1 --project fluffy-climbers-tower-jump --allow-unauthenticated --set-secrets LB_PID_SALT=...,LB_ADMIN_KEY=... --min-instances 0 --concurrency 8 --memory 512Mi --set-build-env-vars GOOGLE_BUILDABLE=Tools/leaderboard_service/LeaderboardService.csproj` (the .NET buildpack builds that project, no Dockerfile; public because players call it with a Firebase token, the service checks the token itself). Root-level `.gcloudignore` is needed for a root deploy: copy `Tools/leaderboard_service/.gcloudignore` to the repo root (kept here in the service folder so it does not affect anything until then).
5. Cloud Scheduler: `POST /admin/tick` every minute with `X-Admin-Key`.
6. Firebase Auth: enable the Play Games provider (Play Games Services v2: server auth code -> `PlayGamesAuthProvider` credential -> Firebase ID token in the Firebase Unity SDK; adds the Firebase Unity SDK, `google-services.json`: a client change).
7. Smoke test with `LB_STORE=memory LB_DEV_UID=test dotnet run` (header `Authorization: Dev test`), then against the real project with a test account.

## 13. Known gaps in what was built

- `FirestoreStore` has no automated test (no emulator here). The logic it wraps (`SlotMath`, `Prizes`, `WeekMath`, claim idempotency semantics) is tested through `InMemoryStore`, which implements the same interface; review the transaction bodies once with the emulator.
- Hero variants: `HeroVariants.Rows` was still empty while the other worker generates it, so the variant tests (a generated variant replays; wrong variant fails; variant lock) return early until it is filled. Variant 0 of a tier hero is tested now.
- Time Attack's 10,800-tick clock end is handled by the same branch as `RunSession` but no test run is long enough to hit it.
- No client code, no `POST /delete-me`: deletion on request is an admin job (delete `players/{uid}`, `weeks/*/{slots,totals}` rows of that uid, claims; boards refresh within 20 s).
- `RunSession` hero-cooldown and revive edits are covered by the tests above, but the Unity EditMode tests were not run (no Unity here).
