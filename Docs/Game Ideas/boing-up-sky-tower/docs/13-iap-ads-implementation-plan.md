# 13 - In-app purchases and ads: implementation plan

> **2026-11-01 update (re-checked against the code, build v0.38.0.40):** the products, prices and ad placements in this plan now follow the shipped content and docs 22, 26 and 27. Product list: **9 gem packs** (`gem_packs.json`, SKUs `boingupskytower.gems.<amount>`, 50 to 15,000 gems, $0.79 to $119.99, INR prices, optional `.launch` promo variants, first-purchase double on the first five) and **2 subscriptions** (`subscriptions.json`: Weekly $1.49 / Rs 149 for 7 days, Monthly $4.99 / Rs 449 for 30 days). Coin packs, chests, bundles and power-ups are bought with gems, not money. Not in the product list any more: the old 6-pack ladder (80 to 10,500 gems), Remove Ads, VIP, battle pass, starter pack (planned later, doc 22 point 7). 1 gem = 20 coins (fixed); there is no gear. Billing stack, validation, restore, ads and phasing below stay valid.

Status (2026-11-01): the shop screen exists (five tabs Bundles, Chests, Power-ups, Coins, Gems plus a Subscription button, doc 26). Gem packs and subscriptions are **test purchases in development builds** (`GrantTestGemPurchase`, SYSTEM_TEST switch); release builds show "Not available yet". There is no store SDK, no server and no analytics layer yet. Ads: Unity LevelPlay is integrated with an instant `TestRewardedAds` in dev builds and the real SDK on device; `AdConfig` still holds placeholder keys, so release builds have no ads. The only rewarded placement built is the **revive ad** (the free first revive of a run).

Sources: doc 01 (FR-S1..S6, FR-A1..A3), doc 03 sections 1 and 6, doc 05, doc 08 section 8 (wins on prices), doc 11 / `.claude/rules/ux-gameplay-critical.md` (CUX), `Content/economy/gem_packs.json`, `subscriptions.json`.
Everything marked **(verify)** depends on store policy, SDK versions or fees that change: check the official page the week you start that task.

**Short answer to "are there IAP requirements?"** Yes: FR-S1..S6 (9 gem packs and 2 subscriptions, store-localized prices, validated purchases, restore/refund handling) plus FR-A1..A3 (rewarded ads, consent). The test-purchase UI is built; real billing and consent are not. This plan splits them into a small first release and later phases.

---

## 1. Scope for the first store release

Recommendation: **Android / Google Play only at v1.0.** Today's builds are Android only, and every extra store doubles the SDK, test and review work.

| Item | Launch (P0) | Later | Notes |
|---|---|---|---|
| Gem packs (9 SKUs, consumable) | P0 | | 50 / 100 / 200 / 500 / 1,000 / 2,000 / 5,000 / 10,000 / 15,000 gems at $0.79 / 1.49 / 2.49 / 5.99 / 10.99 / 20.99 / 49.99 / 89.99 / 119.99 (promo prices about half, off). Final price points: open question Q4. |
| Weekly / Monthly Subscription (auto-renewing) | P0 if the owner wants it at launch, else P1 | | $1.49 and $4.99; needs server or store entitlement, grace period and restore. Instant gems + daily claim are already built against a test purchase. |
| Rewarded ad: revive | **Built** (test ads) | | The first revive of a run is free by ad; the gem revive (5 then 10 gems, max 2 per run) stays. Never replaces one-tap retry (CUX-7). Max 1 ad revive per run. |
| Rewarded ad: double run reward | Planned, not built | | Results screen, opt-in. |
| Rewarded ad: daily power-up | Planned, not built | | Doc 22 point 8 (one random power-up a day, about 60.7 gems of value). |
| Remove Ads, starter pack, battle pass, VIP tiers | **Not planned / not built** | | Remove Ads and the battle pass are not in docs 20 to 26; VIP became the Subscription; the starter pack is described in doc 22 point 7 ($0.99 = 100 gems + 2,000 coins). |
| Interstitial ads | **Not at launch** | P2 | Doc 01 FR-A2: optional. Skipping them keeps CUX-1/CUX-7 simple. |
| Chests, bundles, coin packs, power-ups | Built | | Paid with gems or coins, so no billing; chests need odds disclosure before characters are switched on (doc 05 section 2). |
| Restore button | Settings row, simple | | Required on iOS only; on Android purchases are queried at launch. |

**What Android-only removes:** StoreKit 2, App Store Connect setup, Apple restore-button review rule, ATT prompt and privacy labels, Sign in with Apple, TestFlight, sandbox accounts, Apple server notifications, and Apple's loot-box rule (3.1.1). Cost: iOS revenue is delayed; the Unity IAP / mediation choices below keep iOS open, so adding it later is mostly store setup plus a retest.

---

## 2. Google Play requirements checklist (owner actions)

Policies change often: re-read the official page for each row **(verify)**.

| # | Requirement | Owner action | Blocks |
|---|---|---|---|
| 1 | Play developer account (one-time registration fee, amount (verify)); identity verification; personal vs organization account decides the testing rules | Register, verify identity | everything |
| 2 | Merchant (payments) profile linked to the account; bank account | Create in Play Console | any paid item |
| 3 | Tax forms and country tax info (US W-8/W-9 style forms, VAT where asked) (verify) | Fill in | payouts |
| 4 | App created, package name final (`com.<owner>.boingupskytower` or the chosen id: cannot change later) | Decide id | upload |
| 5 | App signing: Play App Signing (recommended) and an upload keystore kept safe outside the repo | Create keystore, back it up twice | release build |
| 6 | Upload an AAB (not APK) to a testing track; Billing needs a Play-signed build on a track to test real purchases | Claude prepares the AAB (T-IAP-9) | purchase test |
| 7 | Closed testing track + license testers (Gmail accounts added in Play Console, Settings > License testing). New personal accounts may need a minimum number of testers for a minimum number of days before production (verify) | Provide tester emails; start the test clock early | production |
| 8 | In-app products created: 9 gem packs (managed products) and 2 subscriptions (Weekly, Monthly base plans); product ids = `sku` in `gem_packs.json` (`boingupskytower.gems.50` ... `.15000`) and the subscription ids, immutable after creation | Create, price, activate | purchase test |
| 9 | Privacy policy URL (public page; must name ads SDK, analytics, data collected) | Host page, give URL | submission |
| 10 | Data safety form (declare purchase history, device ids, ad id, crash and analytics data per the SDKs actually in the build) | Fill in using the SDK list we produce (T-IAP-14) | submission |
| 11 | Content rating questionnaire (doc 01 target: teens and general, 12+; in-app purchases and ads declared) | Fill in | submission |
| 12 | Target audience / Families policy: the game is **not** child-directed (doc 00). Choosing a target age under 13 pulls in Families policy (restricted ad SDKs, no personalized ads). Keep 13+ and the neutral age behaviour | Answer consistently in Console | submission |
| 13 | Ads declaration ("contains ads") and advertising ID declaration | Tick in Console | submission |
| 14 | Target API level at the current requirement of the Play deadline (verify); Unity 6000.3 normally meets it, check at upload | Claude checks in T-IAP-9 | upload |
| 15 | Account deletion / data deletion declaration (only matters once a server account exists) | Later | server phase |
| 16 | Loot-box odds disclosure: the Iron and Gold chests are random paid items (power-ups and coins in ranges). The (i) popup lists the contents; add odds per type and the country switch before release. Characters in chests stay off (verify) | Before release | chests |
| 17 | Store listing: icon, feature graphic, screenshots, short/long text, no competitor names | Owner and Claude | submission |
| 18 | Play Integrity API enabled (optional at launch, needed for the server phase) (verify) | Later | server phase |

---

## 3. Technical design

### 3.1 Store SDK choice

| | Unity IAP (com.unity.purchasing, v5 line) **(verify current major and Billing Library level)** | RevenueCat SDK (Unity plugin) **(verify)** |
|---|---|---|
| What it is | Unity's official wrapper over Google Play Billing (and StoreKit) | Hosted entitlement/receipt service plus SDK |
| Server validation | Not included: you build it (section 3.3) | Included in the service |
| Subscriptions (Weekly, Monthly) | Possible, you manage entitlement and expiry | Strong fit |
| Cost | Free SDK | Free tier then a percentage of revenue above a threshold **(verify)** |
| Extra moving parts | None (already in Unity ecosystem) | Third-party account, SDK, webhooks, privacy disclosure |
| Fit for gem packs offline-first | Good | Good, but adds a server dependency we then can't remove |
| **Recommendation** | **Use Unity IAP at launch** for gem packs and the two subscriptions. Revisit RevenueCat only when the Subscription (or any later pass) needs server entitlements and we have not yet built our own backend. | |

Keep the SDK behind our own interface so the choice is reversible:

```csharp
interface IStoreService   // Assets/_Game/Monetization/Store
{
    Task InitializeAsync();                       // fetch product metadata (localized price strings)
    IReadOnlyList<StoreProduct> Products { get; } // id, title, localized price, currency code
    Task<PurchaseOutcome> PurchaseAsync(string sku);
    Task<int> RestoreAsync();                     // re-query owned non-consumables, pending items
    event Action<PurchaseUpdate> OnUpdate;        // out-of-band updates (pending finished, refund)
}
```

### 3.2 Purchase flow state machine

```
Idle -> Initiating -> StorePending (Google "pending", e.g. cash payment: may take days)
                   -> Purchased  (store says paid, not yet ours)
                        -> Validating (optional server or local check)
                        -> Granting   (write ledger entry + gems, SAVE to disk)
                        -> Consuming  (consumable) / Acknowledging (non-consumable) within the store window
                        -> Done
Failed / Cancelled -> Idle      (no grant, no charge)
Refunded / Revoked (found at launch or by notification) -> Reversing (see 3.5) -> Done
```

Rules:
1. **Grant before consume, save before consume.** Order: validate, append ledger entry (idempotency key = store order id / purchase token), `Data.Gems += n`, save, then consume. If the app dies after the save but before consume, on next start the purchase reappears as unconsumed, the ledger already contains the key, so we only consume and do not grant twice.
2. If the app dies before the save, the unconsumed purchase is delivered again at start and granted then. Unacknowledged purchases are refunded by Google after the acknowledgement window **(verify the window, currently days)**, so acknowledge promptly.
3. Handle `Pending` explicitly: show "Waiting for payment" in the shop, do not grant, do not block play.
4. All of this runs off the gameplay thread/frames; no allocation in Run scene (CUX-1). The shop is a separate scene.

### 3.3 Receipt validation options

| Option | Cost (money) | Cost (build effort) | Protects against | Weakness |
|---|---|---|---|---|
| A. Client-only (store SDK checks signature locally, optional local signature check with the Play public key) | none | low (about 1 task) | casual fake purchases | rooted devices / patched APKs can forge grants; no refund detection except on next launch query |
| B. Own minimal server function: Firebase Cloud Function (or PlayFab CloudScript) that calls the Google Play Developer API to verify the purchase token, returns a signed grant | Cloud Functions need the pay-as-you-go plan (small volume is near free, verify plan terms) ; PlayFab free tier **(verify)** | medium (function, service account, client call, tests) about 3 tasks | fake receipts, double use, refund detection with Real-time developer notifications (RTDN, needs Pub/Sub) | needs online at purchase (already required); you operate a server |
| C. RevenueCat | percentage fee **(verify)** | low-medium | like B, hosted | third-party dependency |
| **Recommendation** | **Soft launch: A with the ledger and idempotency design, plus the grant interface (`IGrantAuthority`) already shaped for B.** Move to B before money is meaningful (see Q5) or when any gem-gated competitive feature (leaderboard, cosmetics trading) exists. | | | |

Why A is acceptable first: gems buy power-ups, chests, bundles, coins, revives and region skips (there is no gear); ranked runs disallow consumables, so the exposure is lost revenue, not unfair ranked play. Doc 01 FR-S4 (server-authoritative) stays the target.

### 3.4 Offline-first consistency

The game is offline-first; **purchases require a connection** (the store says so). Rules:
- Shop buttons are disabled with an explanation when the store is unavailable ("Store not reachable. Check your connection"), never a silent no-op (CUX-8).
- Spending gems (revive, region skip, items) stays local and instant, as now.
- **Grant queue:** a purchase that is validated server-side (option B) but cannot reach the server yet is stored as `pending` in the ledger with its idempotency key; gems are granted when the server answers. The store transaction is **not consumed** while pending, so nothing is lost on reinstall.
- Idempotency key per grant: `iap:<purchaseToken-hash>`; for ad rewards `ad:<placement>:<run-id>`.
- When a server wallet arrives later (doc 03), the local save becomes a cache: server wins for currencies, per `SaveData` comment. The ledger converts into server grants without double counting because keys are reused.

### 3.5 Save-file changes (`SaveData`, `Assets/_Game/Economy/SaveData.cs`)

Add fields (old saves load with defaults; `CurrentVersion` bump to 2 only if migrating renamed data):

| Field | Type | Purpose |
|---|---|---|
| `purchaseLedger` | `List<PurchaseRecord>` | `{key, sku, gems, utc, state: pending/granted/reversed}`; capped (keep last 200 granted, all pending) |
| `adsRemoved` | bool | entitlement cache; re-derived from the store at each launch |
| `lastTxIds` | `List<string>` (last 20) | duplicate guard if the ledger is trimmed |
| `adDay`, `adCounts` | string, `Dictionary<string,int>` | per-day rewarded ad counters (frequency cap, same pattern as `endlessDay`) |
| `consentState` | enum/int | last UMP result (so Settings shows it without a network call) |
| `iapDisabledUntil` | optional | kill switch from Remote Config |

New `ProgressService` methods (small, each saves immediately like the others): `GrantPurchase(record)`, `ReversePurchase(key)` (can drive gems to a floor of 0; never negative), `SetAdsRemoved(bool)`. Revoking more gems than owned: take what exists, record the shortfall in the ledger.

### 3.6 Code layout

`Assets/_Game/Monetization/` (its own asmdef, referenced by UI; Simulation never references it):
`Store/` (IStoreService, UnityIapStore, FakeStore), `Ads/` (IAdService, MediationAdService, FakeAdService), `Consent/` (UMP wrapper), `PurchaseCoordinator` (state machine + ledger), `AdPolicy` (caps, placements), `MonetizationConfig` (from `Content/config/`). Shop UI goes in `Assets/_Game/UI`. Fakes make every flow testable in EditMode with no SDK.

### 3.7 Testing approach

| Layer | How |
|---|---|
| Logic | EditMode tests on `PurchaseCoordinator` with `FakeStore`: grant once on duplicate delivery, crash between grant and consume (simulated), pending, cancel, refund reversal, floor at 0, ledger trim |
| CriticalUX | Layout test for the new Shop, Consent and rewarded-ad prompts at 9:16, 9:19.5, 9:21, 3:4; 48 dp targets; 130% text scale |
| Device | Extend `Tools/android_test/smoke_test.py`: shop opens, price strings present, purchase button state; use the in-game TestBridge to inject `FakeStore` results in **development builds only** (the bridge never ships) |
| Real store | Internal / closed track build + Play license testers (test cards that do not charge) ; test purchase, cancel, pending, consume-after-kill (adb force-stop between steps), refund in Console |
| Ads | Mediation test mode and test device ids only; never click live ads during development (account suspension risk) |

---

## 4. Rewarded ads design

### 4.1 Mediation

| | AppLovin MAX | Unity LevelPlay |
|---|---|---|
| Notes | Independent mediation, many networks, strong tooling | Unity-integrated, fits Unity packages and dashboard |
| Unity install | Unity package (`.unitypackage`/UPM) | UPM package |
| Consent | Works with UMP | Works with UMP |
| Fees | Free to use, network revenue share **(verify)** | Same model **(verify)** |
| **Recommendation** | Either is fine for one network at launch. Pick **LevelPlay if staying inside Unity services is preferred (less setup), MAX if network breadth matters** (decision Q6). Wrap behind `IAdService` so switching is a small task. | |

### 4.2 Placements

| Placement | Where | Reward | State | Cap (starting values, tune after soft launch) |
|---|---|---|---|---|
| `revive` | Failed overlay, offered with the first revive next to the 5-gem button | One free revive, same effect as the gem revive | **Built** (`RunSession.TryReviveFree`, `AdService.ShowRewarded`) | max 1 ad revive per run; shares the "max 2 revives per run" rule; no ad if one was used |
| `double_reward` | Results screen after a win | x2 coins of that run | Planned | once per result; max N per day (start 20) |
| `daily_powerup` | Rewards or home screen | one random power-up (doc 22 point 8) | Planned | 1 per day |
| `shop_free_gems` | Shop | small gem amount | Planned, optional; any gem reward must stay below the 5 gems a revive ad saves and needs a daily cap | 3 per day, cooldown |

### 4.3 Rules

- **Always opt-in, always skippable.** The big **Retry** button is always visible and one tap (CUX-7). The ad offer is secondary, never a modal that hides Retry, never auto-played.
- An ad is shown only after a tap on its button. If no ad is loaded: the button shows a disabled state with text "No ad available, try later" (CUX-8), and revive via gems still works.
- Preload the next rewarded ad after each show and at Run start (not during), so there is never a load hitch (CUX-1). Ad playback pauses the simulation; on return the game stays paused until the player taps Resume. Audio muted during ad, restored after.
- Reward is granted in the **ad-finished callback**, written to the ledger with key `ad:<placement>:<run-id>` (idempotent), and saved immediately.
- Frequency: no interstitials at launch. Remove Ads is not built; if it is ever added, the revive and x2 buttons stay and pay out directly without an ad (proposed, Q3).
- Test ad units and a debug flag in development builds; the production ids live in a config file that is not committed with real ids if the repo is ever public (ids are not secret but keep them in one place).

### 4.4 Consent and Settings

- Google UMP (User Messaging Platform) flow at first start, before the first ad request: request consent info, show form if required (EEA/UK), then initialize ads. The Boot scene shows a progress indicator; a failed consent request must never block entering the game (CUX-10): ads simply stay off for the session.
- Settings entries (not built): "Privacy choices" (reopens the UMP form when required), "Privacy policy" (opens URL), "Restore purchases", "Ads: status (personalized / limited)".
- Age: neutral age screen behaviour per doc 01; mark requests as not child-directed only if that matches Console answers (checklist row 12).
- Accessibility: ad prompts are plain text buttons with accessibility labels; no flashing UI around the ad button (CUX-6); photosensitivity and reduced-motion settings apply to the ad button animation.

---

## 5. Shop screen UX (must satisfy CUX-3..CUX-8)

| Rule | Shop design |
|---|---|
| Prices | Show only the **store-provided localized price string**; fall back to "-" plus disabled button when metadata is missing. Never show hardcoded USD in the UI (JSON `usdList` / `usdPromo` / `inrList` are for tools and the test build only) |
| Touch targets | Every pack card and button at least 48 x 48 dp, 8 dp gap; card fully tappable; Buy button in the lower half of each card; close button top corner at least 48 dp |
| Layout | Scrollable grid of 9 packs (three per row, doc 26) with a pinned header (gem balance) and pinned bottom bar (Back, Restore). Safe-area aware; no card clipped at 9:16, 9:19.5, 9:21, 3:4; verified by the layout test |
| Text | Body 16 sp or more, gem amounts never clipped or wrapped, survive 130% scale; contrast 4.5:1 on a backing panel |
| Not color-only | Best-value and most-popular tags use an icon plus text; disabled states differ by pattern/label, not only grey |
| Feedback | Tap gives instant press feedback; purchase shows a blocking-but-cancelable "Processing" state with timeout text; success shows a clear confirmation with the new balance; failure explains the reason ("Payment cancelled", "Store not available", "Pending: we will add your gems when payment completes") |
| Restore / entitlements | Settings "Restore purchases" says plainly what happened ("Subscription restored", "Nothing to restore") |
| Locked content | Anything unavailable says why (e.g. "Your Monthly Subscription is active until ...") |
| Never in the way | The shop is never opened by a popup during a run; offers appear only on menu/result screens and never cover Retry. Pausing and back navigation work at all times |
| Money-safety copy | Real-money items are visually distinct from gem-spend items; no dark patterns (no fake countdowns, no pre-selected upsell) |

---

## 6. Analytics, fraud and refunds

### 6.1 Firebase events (extend doc 05's event list)

| Event | When | Params |
|---|---|---|
| `shop_view` | shop opened | source (menu/failed/results) |
| `iap_view` | pack card shown | sku |
| `iap_start` | Buy tapped | sku |
| `iap_pending` | store reports pending | sku |
| `iap_success` | gems granted | sku, value, currency (store currency code), gems |
| `iap_fail` | error or cancel | sku, reason code |
| `iap_restore` | restore run | restored count |
| `iap_refund` | refund/revoke applied | sku, gems_reversed |
| `ad_offer` | ad button shown | placement |
| `ad_start`, `ad_complete`, `ad_fail` | playback | placement, network (if known) |
| `consent_result` | UMP finished | status |
Rule: no personal data in params; respect consent (analytics collection follows the consent state).

### 6.2 Fraud and refunds

| Topic | Handling |
|---|---|
| Fake/forged purchase (client-only phase) | Local signature check; anomaly counters; accept the residual risk while gems buy only consumables, chests and convenience (no gear exists) |
| Replay of one purchase | Idempotency key in ledger + consume |
| Refund noticed at launch | Query owned purchases; a ledger entry whose purchase is no longer valid is `reversed`: remove the gems (floor at 0), re-lock unlocks bought only with refunded gems is **not** done (decision Q8: keep it simple, just deduct) |
| Refund noticed live (server phase) | Real-time developer notifications to a function that marks the entry reversed |
| Ad farming | Daily caps, ledger keys, server-side later |
| Abuse reports | Remote Config kill switch for each placement and for IAP (`iapDisabledUntil`) |

---

## 7. Task list

Minutes are Claude working minutes, +-40 percent. "Owner" lists what blocks the task.

| ID | Task | Min | Depends on | Owner blocker |
|---|---|---|---|---|
| T-IAP-1 | Interfaces, `FakeStore`, `FakeAdService`, asmdef `Monetization` | 40 | | none |
| T-IAP-2 | Save changes: ledger, `adsRemoved`, ad counters; migration tests | 40 | 1 | none |
| T-IAP-3 | `PurchaseCoordinator` state machine + EditMode tests (duplicate, crash, pending, refund) | 90 | 1, 2 | none |
| T-IAP-4 | Shop UI states for real billing (9 packs, 2 subscriptions, pending/failed/restored; the layout itself exists) + CriticalUX layout tests | 120 | 3 | final price points (Q4) |
| T-IAP-5 | Unity IAP integration (`UnityIapStore`), product metadata, restore | 90 | 3 | Play Console products (checklist 8) |
| T-IAP-6 | Settings entries: Restore, Privacy choices, Privacy policy link | 40 | 4 | privacy URL (checklist 9) |
| T-IAP-7 | Ad service (mediation SDK wrapper), test ads, preload logic | 90 | 1 | ad network account (Q6) |
| T-IAP-8 | UMP consent flow + Boot integration | 60 | 7 | privacy URL, consent message in ad network console |
| T-IAP-9 | Release AAB build target, keystore wiring, target API check, version bump to the milestone | 60 | 5 | keystore and Play app created (checklist 4-5) |
| T-IAP-10 | Rewarded placements: revive (built, test ads), x2 reward (results), daily power-up, caps, UX tests | 100 | 7, 8, 2 | LevelPlay app key and ad unit id (placeholders today) |
| T-IAP-11 | Subscription entitlement from the store (replaces the test purchase; Remove Ads is not planned) | 40 | 5, 10 | products created |
| T-IAP-12 | Firebase events (section 6.1) | 50 | 4, 10 | Firebase project and `google-services` config |
| T-IAP-13 | Device test kit extension (shop, injected fake purchases, ad states) | 70 | 4, 10 | phone |
| T-IAP-14 | Closed-track test pass with license testers; fill SDK list for Data safety | 60 | 9, 11 | testers, Play Console setup (checklist 1-8, 10-13) |
| T-IAP-15 | Server validation function (Firebase or PlayFab) + client `IGrantAuthority` B implementation | 240 | 3, backend choice | cloud project, billing plan (Q5) |
| T-IAP-16 | Refund notifications (RTDN) and reversal | 120 | 15 | Pub/Sub topic |
| T-IAP-17 | P2: starter pack, daily power-up ad, first-purchase double and launch promo switch-on check | 180 | 14 | decisions on P2 scope |

Launch path (no server): 1, 2, 3, 4, 5, 6, 7, 8, 10, 11, 12, 9, 13, 14. About 14 working hours of Claude time (+-40 percent), plus waiting for owner and Google steps (closed-test period can take weeks: start it first).

Dependency order: 1 -> 2 -> 3 -> (4, 5, 7 in parallel) -> 6, 8 -> 10 -> 11 -> 12 -> 13 -> 9 -> 14. 15 and 16 are independent of the launch path and can follow.

### Version milestone

Per `.claude/rules/versioning.md`: new systems and economy changes are a **MINOR bump**. Proposal:
- **v0.10.0.x** = IAP shop + ads (fake store and real test-track builds, pre-release; MAJOR stays 0 until the store build).
- v0.11 = server validation / refunds (if Q5 says server at launch, fold into 0.10).
- **v1.0.0.x** = first public Google Play release (all P0 items, checklist complete).
Bump with `python Tools/version.py bump minor` before the first build of the work; name the version in every report.

---

## 8. Open questions for the owner

1. Android / Google Play only at launch (iOS later)?
2. Ads at launch (rewarded only, no interstitials), or IAP only first?
3. Remove Ads is not in the owner docs 20 to 26: confirm it stays out. (If added, we propose it keeps the revive and double rewards without the video.)
4. Are the nine gem price points final ($0.79 to $119.99, doc 22 4.1) and the two subscription prices ($1.49, $4.99)? Is a $119.99 pack acceptable at launch? Starter pack in v1.0 or later?
5. Server at launch? Recommendation: no (client-only validation with a ledger), server before real revenue scales. Which backend (Firebase or PlayFab)?
6. Mediation: Unity LevelPlay (less setup) or AppLovin MAX (more networks)?
7. Who creates the Play developer account, merchant profile and tax info, and when can the closed-test clock start (it has a minimum duration)?
8. On refund: just deduct the gems (proposed) or also lock items bought with them?
9. The Subscription replaced VIP: is it in v1.0? The battle pass is not planned. Chests already ship with gems only: is their odds disclosure enough for v1.0 (characters stay off)?
10. Privacy policy: who hosts it, and which contact email goes in it?
