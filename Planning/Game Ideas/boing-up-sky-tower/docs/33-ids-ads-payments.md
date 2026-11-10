# 33 - Product ids, ad placements, payments and ads SDK setup

Owner list 33, item 5. Code: `Assets/_Game/Services` (assembly `FluffyClimbers.Services`, namespace `FluffyClimbers.Monetization`). Choice: **Unity IAP** + **Unity LevelPlay**.

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

| Constant | Name (= LevelPlay placement name) | Where | State |
|---|---|---|---|
| Revive | `revive` | Failed overlay, free first revive | **Built** |
| DoubleReward | `double_reward` | Results after a win, x2 coins | Planned |
| DailyPowerUp | `daily_powerup` | Rewards/home, 1 free power-up a day | Planned |
| ShopFreeGems | `shop_free_gems` | Shop, small capped gem reward | Planned, optional |

No interstitials or banners at launch (doc 13 4.3).

## 4. Code overview

- `IAdService { IsReady(placement); ShowRewarded(placement, Action<bool> onDone); }`. `StubAdService` (always ready, true after 1 s) and `LevelPlayAdService` (`#if FLUFFY_LEVELPLAY`, own assembly `FluffyClimbers.Services.LevelPlay`, only compiled when the define is set).
- `IPaymentService { Initialize(); GetLocalizedPrice(id); Purchase(id, done); Restore(done); }`. `StubPaymentService` (success at once) and `UnityIapPaymentService` (`#if FLUFFY_UNITY_IAP`, assembly `FluffyClimbers.Services.UnityIap`, Unity IAP v5 `StoreController`).
- `Services.Ads`, `Services.Payments`, `Services.ConsentGiven`: the locator. Real services are used on device only when the define is on, else stubs. The editor always uses stubs.
- Purchase contract: grant and **save inside** the `Purchase` callback; the store transaction is confirmed right after the callback returns. A purchase recovered at start-up (app killed mid-purchase) is raised by `UnityIapPaymentService.RecoveredPurchase`; subscribe before `Initialize()`; with no subscriber it stays unconfirmed and is re-delivered next launch.
- Display prices: keep `ShopCatalog.FormatUsd` as fallback and prefer `Services.Payments.GetLocalizedPrice(productId)` when not null.

## 5. Enable the SDKs

Packages in `Packages/manifest.json`: `com.unity.services.levelplay` 9.5.1 (already installed and in use), `com.unity.purchasing` 5.4.4 (new; the Editor resolves it on next open; pulls `com.unity.services.core` 1.18.0).

1. Open Unity once so the package resolves. Check the Console for errors.
2. Player Settings > Other Settings > Scripting Define Symbols, **Android**: add `FLUFFY_LEVELPLAY` and `FLUFFY_UNITY_IAP` (semicolon separated, keep `TMP_PRESENT`). Do this only after the packages resolved. Without the defines the project compiles with stubs.
3. Build with Gradle as today. Unity IAP adds the Google Play Billing library (resolved by the IAP resolver); LevelPlay already resolves its Android dependencies. `AD_ID` permission is merged by LevelPlay; if the Play Console Data safety / Advertising ID form says otherwise, adjust there.
4. Retire the older `Assets/_Game/Gameplay/Ads` path (`AdService`, `AdConfig`, `LevelPlay/LevelPlayRewardedAds`) once the revive code uses `Services.Ads`: running both would initialise LevelPlay twice.

### LevelPlay (ads) owner steps

1. Create a Unity account / LevelPlay account at unity.com (Unity Dashboard > Monetize > LevelPlay) and accept the terms.
2. Create an **Android app**: package `com.jdlabs.fluffyclimbers`, Google Play store URL (can be added once the listing exists; use "not yet published" while testing).
3. Copy the **App Key** (looks like `1a2b3c4d5`).
4. Create one **Rewarded** ad unit/instance; copy its **Ad Unit ID**. Create the placements from section 3 as Rewarded placements with exactly those names (`revive` first).
5. Enable at least one network for testing (Unity Ads / LevelPlay test network), turn on **Test mode** and register the test device.
6. Put the two values in `Assets/Resources/Services/ads_config.json` (shipped with `REPLACE_WITH_LEVELPLAY_APP_KEY` / `REPLACE_WITH_REWARDED_AD_UNIT_ID` placeholders and `"testMode": true`). With placeholders the build shows no ads (only the gem revive). Before release, set up the production networks and switch the dashboard out of test mode.
7. After the first run on the phone, use `LevelPlay.ValidateIntegration()` (logcat) to verify the integration.

### Google Play (payments) owner steps

1. Pay the one-time Play developer registration, create the app `com.jdlabs.fluffyclimbers`, set up the **merchant/payments profile**.
2. Upload a signed build (internal testing track at least) - products can only be created once a build with the BILLING permission is uploaded.
3. Play Console > Monetize > Products > **In-app products**: create each **Consumable** row of section 2 (Live only): Product ID exactly as in the table, name, description, price (USD default, then country prices). Activate it.
4. Monetize > **Subscriptions**: create `...sub.weekly` (base plan weekly, auto-renewing) and `...sub.monthly` (base plan monthly); add a base plan in each, set prices, activate. In code the product id is the **subscription id**; Unity IAP uses the base plan automatically (single base plan per subscription recommended).
5. Add license testers (Setup > License testing) to buy without being charged; install the internal-test build from the Play link (sideloaded APKs cannot buy).
6. Optional promo variants `<id>_launch` (section 1) - or use Play promotions.
7. Receipt validation (release blocker): Unity IAP > Receipt Validation Obfuscator to generate `GooglePlayTangle` with the Play licence key, then validate in `UnityIapPaymentService.OnPending` before granting (TODO in the code), or validate on a server with the Google Play Developer API.

### App Store (later)

Create the same ids in App Store Connect (In-App Purchases: consumables; Subscriptions: one subscription group "Fluffy Climbers Subscription" with weekly and monthly). Ids are identical on both stores, so no `StoreSpecificIds` are needed. Restore Purchases button is mandatory on iOS (`Services.Payments.Restore`). Add `FLUFFY_UNITY_IAP` / `FLUFFY_LEVELPLAY` to the iOS defines then, plus ATT prompt and `NSUserTrackingUsageDescription`.

## 6. Consent / privacy (open)

`Services.ConsentGiven` is a stub (default false = non-personalised). Ads are never requested before the flag is set, because the privacy call precedes `LevelPlay.Init`. Still to build: Google UMP consent form at first start (EEA/UK), "Privacy choices" and "Restore purchases" entries in Settings, age handling (doc 13 4.4, doc 22 open item 10).

## 7. Open TODOs

- Receipt validation (section 5, step 7).
- Wire `ShopScreen`/`SubscriptionScreen` to `Services.Payments.Purchase(ProductCatalog.*)` instead of `GrantTestGemPurchase`; call `Services.Payments.Initialize()` at boot.
- Switch the revive code from `AdService` to `Services.Ads.ShowRewarded(AdPlacements.Revive, ...)`.
- Server/entitlement check for subscriptions (renewal, grace period); today the plan is granted by the test path.

## 8. Magic strings still in code (for the lead)

| Place | String | Replace with |
|---|---|---|
| `Economy/ShopCatalog.cs` BuiltInGemPacks | `Sku = "fluffyclimbers.gems." + gems` | `ProductCatalog.ForGemPack(gems).Id` (Economy cannot reference Services yet: add the assembly reference to `FluffyClimbers.Economy.asmdef`) |
| `Content/economy/gem_packs.json`, `Resources/Content/content_manifest.json` | `"sku": "fluffyclimbers.gems.50"` ... `.15000` (9 rows, field is `Required.Always`) | the new ids from section 2 (regenerate with the content tool); a test then can compare `Sku` to `ProductCatalog` |
| `Economy/SubscriptionCatalog.cs` | plan ids `"weekly"`, `"monthly"` (not store ids) | map with `ProductCatalog.ForSubscription(planId)` at purchase time |
| `Economy/ProgressService.Subscription.cs`, `ProgressService.Shop.cs` `GrantTestGemPurchase` | no store id | pass `ProductCatalog` id in the ledger key (`iap:<productId>:<transactionId>`) |
| `Gameplay/RunController.cs`, `Gameplay/Ads/AdService.cs` | no placement name | `AdPlacements.Revive` |
| `Gameplay/Ads/AdConfig.cs` | `Resources/AdsConfig.json` + placeholders | replaced by `AdsConfig` (`Resources/Services/ads_config.json`) |

## 9. Verification and risks

- `com.unity.purchasing` 5.4.4 verified in the Unity registry (versions list 5.4.0 to 5.4.4; requires Unity 2022.3+; assembly name `Unity.Purchasing`, API checked against the 5.4.4 package source: `UnityIAPServices.StoreController()`, `Connect`, `FetchProducts`, `PurchaseProduct`, `OnPurchasePending`, `ConfirmPurchase`, `RestoreTransactions`). `com.unity.services.levelplay` 9.5.1 is the installed version (API checked against the local package).
- Not compiled by the author (no Unity run): the `#if FLUFFY_*` files are unverified until the defines are enabled in Unity.
- Android: IAP's Gradle dependency resolution and LevelPlay's can conflict on `androidx` versions; if the Android build fails, check `mainTemplate`/EDM resolver output. `com.unity.services.core` is added by IAP; no UGS project link is required for IAP.

## Rename note (2026-11-09)
The game is now "Fluffy Climbers: Tower Jump" and the package / product id prefix is `com.jdlabs.fluffyclimbers` (old: `com.jdlabs.boingupskytower`, never uploaded, so no migration). AdMob, LevelPlay (Unity LevelPlay) and Firebase apps must be registered with the NEW package name `com.jdlabs.fluffyclimbers`; the Play Console / App Store Connect entries and all product ids above must be created for the new id.
