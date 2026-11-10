# 33 - Product ids, ad placements, payments and ads SDK setup

Owner list 33, item 5. Code: `Assets/_Game/Services` (assembly `FluffyClimbers.Services`, namespace `FluffyClimbers.Monetization`). Choice: **Unity IAP** + **Google AdMob** (owner decision 2026-11-09, replaces Unity LevelPlay).

## 1. Rules for ids

- Format: `com.jdlabs.fluffyclimbers.<category>.<name>`, lowercase letters, digits, underscore (valid in Google Play and App Store Connect).
- Ids are **permanent**: a store product id can never be reused or renamed after creation. The single source in code is `ProductCatalog` (`Assets/_Game/Services/Payments/ProductCatalog.cs`).
- Only **gem packs and the two subscriptions** are sold for money. Chests, bundles, coin packs, power-ups and characters cost gems/coins, so they have no store ids. Remove Ads, VIP, battle pass: not planned (doc 13).
- Launch promo prices: a store product has one price, so each gem pack that has a promo price gets an optional second product `<id>_launch` (`ProductDef.PromoId`). Alternative that needs no extra products: run the promo with Play Console promotions / price change. Decide before creating the products. Subscriptions have no promo.

## 2. Product id table

| Constant | Product id | Type | Name | USD | Promo USD (`_launch`) | Contents |
|---|---|---|---|---|---|---|
| Gems50 | com.jdlabs.fluffyclimbers.gems.pack_50 | Consumable | 50 Gems | 0.79 | 0.49 | 50 gems |
| Gems100 | ...gems.pack_100 | Consumable | 100 Gems | 1.49 | 0.79 | 100 gems |
| Gems200 | ...gems.pack_200 | Consumable | 200 Gems | 2.49 | 1.29 | 200 gems |
| Gems500 | ...gems.pack_500 | Consumable | 500 Gems | 5.99 | 2.99 | 500 gems |
| Gems1000 | ...gems.pack_1000 | Consumable | 1,000 Gems | 10.99 | 5.49 | 1,000 gems |
| Gems2000 | ...gems.pack_2000 | Consumable | 2,000 Gems | 20.99 | 10.49 | 2,000 gems |
| Gems5000 | ...gems.pack_5000 | Consumable | 5,000 Gems | 49.99 | 24.99 | 5,000 gems |
| Gems10000 | ...gems.pack_10000 | Consumable | 10,000 Gems | 89.99 | 44.99 | 10,000 gems |
| Gems15000 | ...gems.pack_15000 | Consumable | 15,000 Gems | 119.99 | 59.99 | 15,000 gems |
| SubWeekly | ...sub.weekly | Subscription (1 week) | Weekly Subscription | 1.49 | - | 100 gems now; daily 10 gems + 500 coins, 7 days |
| SubMonthly | ...sub.monthly | Subscription (1 month) | Monthly Subscription | 4.99 | - | 200 gems now; daily 10 gems + 600 coins, 30 days; power-up every 5th claim |
| StarterPack | ...bundle.starter | Consumable | Starter Pack | 0.99 | - | 100 gems + 2,000 coins. **PLANNED, `Live=false`: do not create yet** (doc 22 point 7) |

"..." stands for the prefix `com.jdlabs.fluffyclimbers`. INR prices (doc 22 4.1) are set in the store per country, not in code. The first-purchase double (packs up to 1,000 gems) is granted by the game, not by the store.

## 3. Rewarded ad placements (`AdPlacements`)

| Constant | Name (= key in `ads_config.json` `rewardedAdUnitIds`) | Where | State |
|---|---|---|---|
| Revive | `revive` | Failed overlay, free first revive | **Built** |
| DoubleReward | `double_reward` | Results after a win, x2 coins | Planned |
| DailyPowerUp | `daily_powerup` | Rewards/home, 1 free power-up a day | Planned |
| ShopFreeGems | `shop_free_gems` | Shop, small capped gem reward | Planned, optional |

No interstitials or banners at launch (doc 13 4.3).

## 4. Code overview

- `IAdService { IsReady(placement); ShowRewarded(placement, Action<bool> onDone); }`. `StubAdService` (always ready, true after 1 s; editor and tests) and `AdMobAdService` (own assembly `FluffyClimbers.Services.AdMob`, define constraint `FLUFFY_ADMOB`, device only). The revive flow calls `Services.Ads.ShowRewarded(AdPlacements.Revive, ...)`; the old `Gameplay/Ads` path and all LevelPlay code are deleted.
- `IPaymentService { Initialize(); GetLocalizedPrice(id); Purchase(id, done); Restore(done); }`. `StubPaymentService` (success at once; editor and development builds only), `UnavailablePaymentService` (a release build without a working store refuses every purchase, nothing is ever granted) and `UnityIapPaymentService` (`#if FLUFFY_UNITY_IAP`, assembly `FluffyClimbers.Services.UnityIap`, Unity IAP v5 `StoreController`). The interface also has two events, `RecoveredPurchase` and `ActiveSubscriptionFound`.
- `Services.Ads`, `Services.Payments`, `Services.ConsentGiven`, `Services.PrivacyOptionsRequired`, `Services.ShowPrivacyOptions(Action onClosed)`: the locator. `PrivacyOptionsRequired` is true only when UMP says a privacy options entry must be offered (Settings shows the button then); stubs and the editor return false and call `onClosed` at once. Real services are used on device only when the define is on, else stubs. The editor always uses stubs.
- Purchase contract: grant and **save inside** the `Purchase` callback; the store transaction is confirmed right after the callback returns, unless the callback sets `PurchaseResult.Reject` (then it stays unconfirmed and Google refunds it after about 3 days). The receipt is validated BEFORE the callback runs (section 5, step 7). A purchase recovered at start-up (app killed mid-purchase, pending purchase approved later) is raised by `IPaymentService.RecoveredPurchase`; `Gameplay/StoreLink.Start` (a boot step in `BootLoader`) subscribes before `Initialize()`; with no subscriber it stays unconfirmed and is re-delivered next launch.
- **Grant and ledger** (`Economy/ProgressService.StorePurchases.cs`): `ApplyStorePurchase(productId, transactionId, now)` adds the gems (first-purchase double included) or starts the subscription period and saves ONCE together with the purchase record. The ledger key is `iap:<productId>:<transactionId>` (stored as the record's product id and transaction id; the transaction id is the Google orderId, else the purchase token), so the same purchase delivered twice (retry, restore, recovery) returns `Duplicate` and adds nothing. Outcomes: `Granted`, `Duplicate`, `UnknownProduct` (not a live ProductCatalog id), `NotGranted` (for example the other subscription plan is running: Reject, so Play refunds).
- **Where it is used**: `ShopScreen.TapGemPack` and `SubscriptionScreen.TapBuy` buy through `Services.Payments.Purchase(ProductCatalog...)` whenever the test purchases are off (always in release builds). The test-grant paths (`GrantTestGemPurchase`, `GrantTestSubscriptionPurchase`) stay behind `ShopScreen.TestPurchasesEnabled` = `GameSettings.ShopTest`, which needs `GameSettings.DevBuild`, which is compiled to false unless `AdminAccess.ToolsCompiledIn` (editor or development build). Store prices (`GetLocalizedPrice`) replace the USD text on the cards once the store has answered (`StoreLink.StorePrice`); the USD price is the fallback.
- **Subscriptions** (no own server): a purchase starts or extends the plan by its days (weekly 7, monthly 30) and pays the instant gems; renewals while the app was closed are seen through `ActiveSubscriptionFound` (the store lists the subscription as active at start-up and after Restore): when the local time has run out, `ApplyActiveSubscription` starts one new period WITHOUT instant gems (one record per product and local day). Limits: grace period, pause and account-hold states are not modelled; a server check (Google Play Developer API) is the stronger option.
- **Restore purchases** (Settings > Restore): `Services.Payments.Restore`, then `ProgressService.RestoreFromRecords` re-grants subscriptions the saved purchase records still entitle (gems are never paid twice); a result popup says how many were restored.
- Display prices: keep `ShopCatalog.FormatUsd` as fallback and prefer `Services.Payments.GetLocalizedPrice(productId)` when not null.

## 5. Enable the SDKs

Packages in `Packages/manifest.json`: `com.google.ads.mobile` 11.6.0 (Google Mobile Ads Unity plugin, via OpenUPM; pulls the External Dependency Manager), `com.unity.purchasing` 5.4.4. `com.unity.services.levelplay` was removed.

1. Open Unity once so the packages resolve. Check the Console for errors.
2. Player Settings > Other Settings > Scripting Define Symbols, **Android**: `TMP_PRESENT;FLUFFY_ADMOB;FLUFFY_GPGS;FLUFFY_UNITY_IAP`. Without the defines the project compiles with stubs.
3. Build with Gradle as today. The Google Mobile Ads plugin resolves `play-services-ads` and UMP through the External Dependency Manager (do not set `UNITY_EDM_DISABLED`). Manifest: `INTERNET`, `ACCESS_NETWORK_STATE`, `com.google.android.gms.permission.AD_ID` are ensured once by `Assets/Editor/Build/AdMobAndroidConfig.cs` (hook order 40, after short label 20 and no-backup 30; it only inserts elements). The AdMob **app id** is written into the plugin settings asset (`GoogleMobileAdsSettings`) before the plugin's own pre-build step, from `ads_config.json`.
4. App id rules: development builds with placeholders (or `testMode: true`) use Google's sample app id `ca-app-pub-3940256099942544~3347511713` and sample rewarded unit `ca-app-pub-3940256099942544/5224354917`. A **release build with a placeholder `androidAppId` fails** (`AndroidReleaseBuild` preflight and the pre-build hook). A release build with a placeholder unit id (but a real app id) builds and shows no ads (one warning in the log; revive falls back to gems). Sample ids ship in release only with `"testMode": true`.

### `Resources/Services/ads_config.json`

| Field | Meaning |
|---|---|
| `androidAppId` | AdMob App ID `ca-app-pub-XXXXXXXXXXXXXXXX~YYYYYYYYYY` (placeholder `REPLACE_WITH_ADMOB_APP_ID`) |
| `rewardedAdUnitIds` | `default`, `revive`, `double_reward`, `daily_powerup`, `shop_free_gems`: `ca-app-pub-XXXXXXXXXXXXXXXX/ZZZZZZZZZZ`; a placement left as `REPLACE_WITH_REWARDED_UNIT_ID` uses `default`. One unit for all is fine at launch |
| `testMode` | true = always Google sample ids, also in release (default false) |
| `tagForChildDirectedTreatment` | `unspecified` (default), `true` = child treatment. The game is NOT child-directed (13+) |
| `maxAdContentRating` | `G`, `PG`, `T` (default), `MA` |
| `debugGeography` | `disabled` (default), `eea`, `not_eea`: UMP test geography, **development builds only** |
| `testDeviceIds` | list of hashed test device ids (also used for UMP debug); logcat prints your id |

Flow (`AdMobAdService`): request configuration, then UMP `ConsentInformation.Update` and `ConsentForm.LoadAndShowConsentFormIfRequired`, `Services.ConsentGiven = CanRequestAds()`, and only then `MobileAds.Initialize` and the first rewarded load. After each show the next ad loads; failed loads retry after 5 s, doubling up to 120 s. The callback of `ShowRewarded` gets `true` only when the reward was earned.

### AdMob owner steps (console)

1. Sign in at admob.google.com, accept the terms, set up payments info and address (needed before payouts, not for testing).
2. Apps > Add app > Android, "app is published on Google Play": No (until the listing exists; link it later). Name "Fluffy Climbers: Tower Jump". Copy the **App ID** (`ca-app-pub-...~...`).
3. In the app: Ad units > Add ad unit > **Rewarded**. Name `revive` (reward: any, e.g. 1 "revive"; the game ignores the amount). Copy the **Ad unit ID** (`ca-app-pub-.../...`). Optionally create further units for the planned placements; one unit is enough at launch.
4. Put both values in `Assets/Resources/Services/ads_config.json` (`androidAppId`, `rewardedAdUnitIds.default`).
5. **app-ads.txt**: AdMob > Apps > the app > "Set up app-ads.txt". Host the file at the root of the developer website domain that is listed on the Google Play store listing (`https://<domain>/app-ads.txt`), with the line AdMob gives you (`google.com, pub-XXXXXXXXXXXXXXXX, DIRECT, f08c47fec0942fa0`). AdMob shows the check status after the app is published.
6. **Privacy & messaging** (AdMob left menu): create a **GDPR message** (EEA/UK) and a **US state regulations** message if offered, attach it to the app, publish it. Enable the "Privacy options" entry point. Without a published message UMP shows nothing.
7. **Test devices**: AdMob > Settings > Test devices > add the phone (device id from logcat or the Ad Inspector). Add the same id to `testDeviceIds`.
8. **Google Play Console**: App content > Ads: "Yes, my app contains ads". Data safety: declare Device or other IDs (Advertising ID) collected and shared for advertising, plus the data AdMob collects (see its Play data disclosure guide). App content > Advertising ID: "Yes" (the app declares `AD_ID`). Target audience 13+ (not for children), consistent with `tagForChildDirectedTreatment: unspecified`.
9. POLICY.md / TERMS.md must name AdMob and the consent choice (kept up to date by the legal-docs owner).

### Google Play (payments) owner steps

1. Pay the one-time Play developer registration, create the app `com.jdlabs.fluffyclimbers`, set up the **merchant/payments profile**.
2. Upload a signed build (internal testing track at least) - products can only be created once a build with the BILLING permission is uploaded.
3. Play Console > Monetize > Products > **In-app products**: create each **Consumable** row of section 2 (Live only): Product ID exactly as in the table, name, description, price (USD default, then country prices). Activate it.
4. Monetize > **Subscriptions**: create `...sub.weekly` (base plan weekly, auto-renewing) and `...sub.monthly` (base plan monthly); add a base plan in each, set prices, activate. In code the product id is the **subscription id**; Unity IAP uses the base plan automatically (single base plan per subscription recommended).
5. Add license testers (Setup > License testing) to buy without being charged; install the internal-test build from the Play link (sideloaded APKs cannot buy).
6. Optional promo variants `<id>_launch` (section 1) - or use Play promotions.
7. **Receipt validation** (done in code, key needed): Play Console > Monetize > **Monetization setup** > "Licensing" shows the base64 **licence key** (a public key). Paste it into `Assets/Resources/Services/iap_config.json` as `playLicenseKey` (shipped placeholder `REPLACE_WITH_PLAY_LICENSE_KEY`). `UnityIapPaymentService` decodes it and checks every purchase with `UnityEngine.Purchasing.Security.CrossPlatformValidator` (RSA signature, package name `com.jdlabs.fluffyclimbers`, product id, state Purchased) before the grant. No key: development builds grant with a logged warning; **release builds refuse the purchase** (Failed, logged, never granted) and `AndroidReleaseBuild` fails its preflight while the key is a placeholder. The package's own tangle generator (`GooglePlayTangle`) is not used: the key is a public key, so a config file is enough and easier to replace. Local validation only stops casual tampering and replayed receipts of other apps; **validating on a server with the Google Play Developer API (purchases.products / purchases.subscriptionsv2) is the stronger option** and the way to see cancellations, refunds and renewals.

### App Store (later)

Create the same ids in App Store Connect (In-App Purchases: consumables; Subscriptions: one subscription group "Fluffy Climbers Subscription" with weekly and monthly). Ids are identical on both stores, so no `StoreSpecificIds` are needed. Restore Purchases button is mandatory on iOS (`Services.Payments.Restore`). Add `FLUFFY_UNITY_IAP` / `FLUFFY_ADMOB` to the iOS defines then (the AdMob iOS app id and SKAdNetwork list live in the plugin settings and are not wired by this project yet), plus ATT prompt and `NSUserTrackingUsageDescription`.

## 6. Consent / privacy

Built: Google UMP before any ad request (section 5 flow). `Services.ConsentGiven` follows `ConsentInformation.CanRequestAds()`. Settings shows a "Privacy options" button when `Services.PrivacyOptionsRequired` is true; tapping calls `Services.ShowPrivacyOptions(onClosed)`. Age: the game is 13+, not child-directed; `maxAdContentRating` T. Players in a consent region who decline get no ads (revive offers gems only). Still open: "Restore purchases" entry in Settings, doc 13 4.4 / doc 22 item 10 wording.

## 7. Open TODOs

- Receipt validation: done locally (section 5, step 7); owner supplies the licence key. A server check stays optional.
- ShopScreen/SubscriptionScreen use `Services.Payments.Purchase(ProductCatalog.*)`, `Initialize()` runs at boot (`StoreLink.Start`): done. Not tested against a real store (needs a Play build and a licence tester).
- Other placements (`double_reward`, `daily_powerup`, `shop_free_gems`) have unit-id slots in the config but no call sites yet (TODO; only revive and the Admin test button call `Services.Ads`).
- Server/entitlement check for subscriptions (cancellation, refund, grace period, account hold). Today: purchase and start-up renewal detection only (section 4).

## 8. Magic strings still in code (for the lead)

| Place | String | Replace with |
|---|---|---|
| `Economy/ShopCatalog.cs` BuiltInGemPacks | (done) | `Sku = ProductCatalog.Prefix + "gems.pack_" + gems`; `FluffyClimbers.Economy.asmdef` now references `FluffyClimbers.Services` (no cycle: Services references nothing) |
| `Content/economy/gem_packs.json`, `Content/schemas/gem_pack.schema.json`, `Resources/Content/content_manifest.json` | (done) | `"sku": "com.jdlabs.fluffyclimbers.gems.pack_<gems>"`, schema pattern updated, manifest edited by hand in the same way (the lead rebuilds it in Unity); `StorePurchaseTests` compares every `Sku` to `ProductCatalog` |
| `Economy/SubscriptionCatalog.cs` | plan ids `"weekly"`, `"monthly"` (not store ids) | (done) mapped with `ProductCatalog.ForSubscription(planId)` in `SubscriptionScreen` |
| `Economy/ProgressService.Subscription.cs`, `ProgressService.Shop.cs` `GrantTestGemPurchase` | (done) | records use `ProductCatalog` ids; the real path is `ApplyStorePurchase` (ledger key `iap:<productId>:<transactionId>`) |
| `Gameplay/RunController.cs` | (done) | uses `AdPlacements.Revive`; the old `Gameplay/Ads` folder is deleted |

## 9. Verification and risks

- `com.unity.purchasing` 5.4.4 verified in the Unity registry (versions list 5.4.0 to 5.4.4; requires Unity 2022.3+; assembly name `Unity.Purchasing`, API checked against the 5.4.4 package source: `UnityIAPServices.StoreController()`, `Connect`, `FetchProducts`, `PurchaseProduct`, `OnPurchasePending`, `ConfirmPurchase`, `RestoreTransactions`). `com.google.ads.mobile` 11.6.0: API checked against the package samples (`RewardedAd.Load`, `Show`, `CanShowAd`, full-screen events, `ConsentInformation`, `ConsentForm`, `RequestConfiguration`); the DLLs have no source, so the enum names (`AgeRestrictedTreatment`, `MaxAdContentRating`, `DebugGeography`) are unverified until the first compile.
- Not compiled by the author (no Unity run): the `#if FLUFFY_*` files are unverified until the defines are enabled in Unity.
- Android: IAP's Gradle dependency resolution and AdMob's (`play-services-ads`) can conflict on `androidx` versions; if the Android build fails, check `mainTemplate`/EDM resolver output. `com.unity.services.core` is added by IAP; no UGS project link is required for IAP.

## Rename note (2026-11-09)
The game is now "Fluffy Climbers: Tower Jump" and the package / product id prefix is `com.jdlabs.fluffyclimbers` (old: `com.jdlabs.boingupskytower`, never uploaded, so no migration). AdMob and Firebase apps must be registered with the NEW package name `com.jdlabs.fluffyclimbers`; the Play Console / App Store Connect entries and all product ids above must be created for the new id.

## 10. AdMob test checklist (phone, development build)

1. Clean install (uninstall first). With placeholders the dev build uses Google sample ids (the build log prints `[AdMob] Android app id ... SAMPLE`).
2. First start: no crash. To test consent, set `debugGeography` to `eea` in a dev build: the consent form appears before any ad; accept or decline, the menu opens.
3. Fail a level: the failed overlay offers the ad revive. With no network it offers gems only; with network the test rewarded ad plays.
4. Watch to the end: revive happens, no gems spent. Close early: no revive, the offer shows again. A second ad in the same run works (next ad was preloaded).
5. Airplane mode, fail a level: only the gem option, no hang. Network back on: within about 2 minutes (backoff) the ad offer returns.
6. Settings shows "Privacy options" only in a consent region (`eea` debug geography); it opens the form and returns.
7. Admin menu "Rewarded ad" shows an ad and reports reward ok / no reward.
8. Release candidate: real ids in `ads_config.json`, `testMode` false, `debugGeography` disabled. Build Release AAB (it fails while the app id is a placeholder). Install the internal-test build, confirm an ad with the phone listed in AdMob Test devices (never tap your own live ads).
9. After publishing: check app-ads.txt status in AdMob.
