# 48 Google Play Console product list (ready to paste)

Game: **Fluffy Climbers: Tower Jump**, package `com.jdlabs.fluffyclimbers`. Sources: `Assets/_Game/Services/Payments/ProductCatalog.cs` (ids, Live flags), `Assets/_Game/Economy/ShopCatalog.cs` (gem packs), `SubscriptionCatalog.cs`, docs 13, 22, 26, 33, 35. Doc 33 stays the reference for code behaviour. No secrets are in this file.

Rules: product ids are **permanent and lowercase** (letters, digits, underscores, dots); a deleted or renamed id can never be reused. Change prices later in the console, never the id. The game shows the store's localized price at run time, so the USD/INR values below are the defaults to enter.

## 1. One-time in-app products

Play Console > your app > **Monetize > Products > In-app products > Create product**. Status: **Active**. Set the default price in USD, then edit the country prices and set **India = INR** as below. The game consumes these purchases (gems can be bought again).

| # | Product ID (exact) | Name (max 55) | Description (max 80) | USD | INR |
|---|---|---|---|---|---|
| 1 | `com.jdlabs.fluffyclimbers.gems.pack_50` | 50 Gems | 50 gems for chests, power-ups and coins in Fluffy Climbers. | 0.79 | 75 |
| 2 | `com.jdlabs.fluffyclimbers.gems.pack_100` | 100 Gems | 100 gems for chests, power-ups and coins in Fluffy Climbers. | 1.49 | 142 |
| 3 | `com.jdlabs.fluffyclimbers.gems.pack_200` | 200 Gems | 200 gems for chests, power-ups and coins in Fluffy Climbers. | 2.49 | 237 |
| 4 | `com.jdlabs.fluffyclimbers.gems.pack_500` | 500 Gems | 500 gems for chests, power-ups and coins in Fluffy Climbers. | 5.99 | 569 |
| 5 | `com.jdlabs.fluffyclimbers.gems.pack_1000` | 1,000 Gems | 1,000 gems for chests, power-ups and coins in Fluffy Climbers. | 10.99 | 1,044 |
| 6 | `com.jdlabs.fluffyclimbers.gems.pack_2000` | 2,000 Gems | 2,000 gems for chests, power-ups and coins in Fluffy Climbers. | 20.99 | 1,994 |
| 7 | `com.jdlabs.fluffyclimbers.gems.pack_5000` | 5,000 Gems | 5,000 gems for chests, power-ups and coins in Fluffy Climbers. | 49.99 | 4,749 |
| 8 | `com.jdlabs.fluffyclimbers.gems.pack_10000` | 10,000 Gems | 10,000 gems for chests, power-ups and coins in Fluffy Climbers. | 89.99 | 8,549 |
| 9 | `com.jdlabs.fluffyclimbers.gems.pack_15000` | 15,000 Gems | 15,000 gems for chests, power-ups and coins in Fluffy Climbers. | 119.99 | 11,399 |

All descriptions are at most 80 characters. The first-purchase double (packs up to 1,000 gems) is granted by the game, not by the store: do not mention it in the product text.

**Do NOT create:** `com.jdlabs.fluffyclimbers.bundle.starter` (StarterPack, `Live=false`, planned: $0.99 = 100 gems + 2,000 coins). Its id exists in code only.

## 2. Subscriptions

Play Console > **Monetize > Products > Subscriptions > Create subscription**. The **product ID is the subscription id** the game asks for. One base plan per subscription.

| Field | Weekly | Monthly |
|---|---|---|
| Product ID (exact) | `com.jdlabs.fluffyclimbers.sub.weekly` | `com.jdlabs.fluffyclimbers.sub.monthly` |
| Name | Weekly Subscription | Monthly Subscription |
| Base plan ID (suggested) | `weekly-autorenew` | `monthly-autorenew` |
| Base plan type | Auto-renewing, billing period 1 week | Auto-renewing, billing period 1 month |
| Price USD / INR | 1.49 / 149 | 4.99 / 449 |
| Benefits (up to 4 lines, max 40 chars each) | 100 gems right away; 10 gems + 500 coins daily; Daily rewards for 7 days | 200 gems right away; 10 gems + 600 coins daily; Daily rewards for 30 days; Free power-up every 5th claim |

The in-game screen is called VIP; the store names above match `SubscriptionCatalog`.

**Which base plan id does the game expect? None.** `UnityIapPaymentService` and `ProductCatalog.ForSubscription` use only the subscription product id; Unity IAP picks the base plan from the store. Base plan ids are free to choose, so use the suggested ones. Keep **one base plan per subscription** (doc 33): a second base plan or offer can make Unity IAP pick a different one. No free trial or intro offer is modelled; do not add one.

**Grace period / account hold (recommended):** in each base plan, under resubscribe, grace period and account hold: grace period **3 days (weekly)** and **7 days (monthly)**, account hold **30 days**, allow resubscribe **on**. The game does not model grace, hold or pause yet (doc 33, doc 14 FR-SHOP-57): it only sees active subscriptions at start-up and after Restore. The settings protect revenue on the Play side; a Google Play Developer API server check is the later fix. Activate the base plan after saving.

## 3. Step order

1. Pay the one-time developer registration and create the app `com.jdlabs.fluffyclimbers`.
2. Set up the **payments profile** (merchant account, bank, tax). Nothing can be sold without it.
3. Build a **signed release AAB** (upload keystore `E:\Unity\keys\fluffyclimbers-upload.keystore`, see doc 47) and upload it to **Test and release > Testing > Internal testing** first. Products can only be created once a build with the `com.android.vending.BILLING` permission (added by Unity IAP) is uploaded. Roll it out and add the testers' emails.
4. Create the in-app products (section 1) and subscriptions (section 2) and set each **Active**.
5. **Setup > License testing**: add the tester Gmail accounts (they buy without being charged). Install from the Internal-testing opt-in link (sideloaded APKs cannot buy). Test one pack and one subscription, check the grant and Restore.
6. Copy the licensing key into `iap_config.json` (section 5), rebuild, upload a higher version code, and re-test before any production release.
7. Go to production only after the internal tests pass.

## 4. Launch price (promo), doc 33 section 1

Gem packs carry a launch promo USD price in code (0.49, 0.79, 1.29, 2.99, 5.49, 10.49, 24.99, 44.99, 59.99 for packs 50 to 15,000). The promo is OFF by default (a PlayerPrefs switch). A store product has one price, so the code names an optional second product `<id>_launch` (for example `com.jdlabs.fluffyclimbers.gems.pack_50_launch`). Decide BEFORE creating products:

- **Recommended:** create only the 9 list-price products above and run the launch discount with Play Console promotions or a temporary price change. No extra permanent ids.
- Alternative: also create the 9 `_launch` products with the promo prices and activate them only while the promo runs. Those ids are permanent too.
- Subscriptions have no promo; do not create `_launch` variants for them.

## 5. Checklist: Play Console value to project file

Project root: `E:\Code\fluffy-climbers-sky-tower`.

| Play Console / Google value | Where to find it | Project file and key | State |
|---|---|---|---|
| Product IDs and prices | Monetize > Products | `Assets/_Game/Services/Payments/ProductCatalog.cs` (ids live in code; the console must match) | Match sections 1 and 2 |
| Licensing public key (base64) | Monetize > Monetization setup > Licensing | `Assets/Resources/Services/iap_config.json`, key `playLicenseKey` | **Owner still has to paste it** (placeholder `REPLACE_WITH_PLAY_LICENSE_KEY`); release builds refuse purchases and the release preflight fails while it is a placeholder |
| Play Games Project ID (digits) | Play Games Services > Configuration, under the game name | `Assets/Resources/Services/play_games_config.json`, key `projectId` | Currently set (794061115424); confirm it belongs to the `com.jdlabs.fluffyclimbers` game |
| AdMob app id and rewarded unit ids | AdMob console | `Assets/Resources/Services/ads_config.json`, keys `androidAppId`, `rewardedAdUnitIds` | App id and `default`/`revive` set; `double_reward`, `daily_powerup`, `shop_free_gems` are placeholders (they fall back to `default`) |
| Upload key SHA-1 `E3:0D:AE:F1:AF:2A:29:EC:78:D5:40:1E:F4:6D:A9:66:81:11:5F:6A` | Keystore `E:\Unity\keys\fluffyclimbers-upload.keystore` | Play Games Configuration > Credentials > Android credential (doc 35 step 4) | Add |
| Debug key SHA-1 `DE:D2:01:A4:59:B2:16:96:16:59:A9:B0:20:53:74:19:E7:C4:AF:85` | Unity debug keystore | Same credentials list | Add (development builds) |
| Play App Signing key SHA-1 | Test and release > Setup > App signing > "App signing key certificate" (after the FIRST upload) | Same credentials list | Owner copies it; without it sign-in fails for Play-installed builds (`DeveloperError`) |
| Package name | Play Console app | Unity bundle id; `ProductCatalog.Prefix` | `com.jdlabs.fluffyclimbers` |
| Privacy policy / terms | App content > Privacy policy | `POLICY.md`, `TERMS.md` in the project root (doc 47) | Must name AdMob and billing |
| Ads declaration | App content > Ads, Advertising ID, Data safety | console only | Contains ads: Yes; Advertising ID: Yes; 13+ (doc 33 step 8) |

Doc 35: create one Android credential per signing key (all with the package name above), then select each on the Play Games Configuration page and Save.
