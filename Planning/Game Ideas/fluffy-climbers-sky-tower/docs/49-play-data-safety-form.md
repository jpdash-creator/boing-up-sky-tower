# 49 - Google Play Console: Data safety form (answers, step by step)

Owner request 2026-11-10. Play Console > App content > Data safety. The answers below are derived from what the build actually ships
(`Packages/manifest.json`: Google Mobile Ads Unity plugin 11.6.0 with the UMP consent form, Play Games Services plugin 2.3.0 with Saved Games,
Unity In-App Purchasing 5.4.4; Unity Analytics / Crash Reporting / Performance Reporting off; no server of ours; no notifications) and from the
three vendor disclosure pages (links at the end). `POLICY.md` section 18 must say the same thing as the form; see "Policy follow-up" below.

Facts the answers rest on:
- Ads: `Services` calls `AdMobAdService.Begin()` at boot. After the UMP consent step the SDK is initialised and the revive rewarded ad is
  **pre-loaded immediately**, so the ad SDK sends its data at app start, not only when the player taps an ad button. Ad content rating T,
  child-directed tag unspecified, consent form shown where the law requires it (EEA, UK, Switzerland; US state privacy messages).
- Purchases: Unity IAP is initialised lazily (`ShopScreen`, `SubscriptionScreen`, `StoreLink`, Restore in Settings), so Unity's IAP telemetry
  (transaction data, installation id, device info, crash logs, diagnostics, country from IP) flows only when the player opens the shop or buys.
  Unity's own Play disclosure for IAP 5.4+ marks these as collected, not shared, required, purposes App functionality + Analytics.
- Sign-in: optional. The game receives the Play Games player id and display name; the player id goes into the save, and the save (progress,
  wallet, items, purchase records) is uploaded to Google's Saved Games when signed in. The display name never leaves the device through the game.

## Step 1 - Overview
Read, click **Next**.

## Step 2 - Data collection and security
| Question | Answer | Why |
|---|---|---|
| Does your app collect or share any of the required user data types? | **Yes** | AdMob, Unity IAP and Play Games Services all transmit data off the device |
| Is all of the user data collected by your app encrypted in transit? | **Yes** | AdMob: TLS; Play Games Services: HTTPS; Unity IAP: yes on its disclosure; the game itself makes no network calls |
| Do you provide a way for users to request that their data is deleted? | **Yes** | `POLICY.md` sections 13, 14 and 16: Reset progress (uploads an empty save), delete Play Games data for the game, reset/delete the advertising ID, e-mail jpdash.creator@gmail.com (requests to Unity are relayed through Unity's IAP support form) |
| Does your app allow users to create an account? (account deletion section) | **No** | The game creates no account of its own and has no server; sign-in links the existing Google Play Games profile, whose per-game data the user deletes in their Play Games profile. If a reviewer objects, switch to Yes and give `https://jpdash-creator.github.io/fluffy-climbers-sky-tower/policy.html` (section 13) as the deletion-request URL |
| Independent security review (MASA) | **No** / leave unticked | Not done; not required for games |

## Step 3 - Data types (tick exactly these)
- Location > **Approximate location**
- Personal info > **User IDs**, **Name** (leaderboard only, see the 2026-11-12 section below)
- Photos and videos > **Photos** (leaderboard profile picture link only)
- Financial info > **Purchase history**
- App activity > **App interactions** (now also the leaderboard scores and run inputs), **Other actions**
- App info and performance > **Crash logs**, **Diagnostics**, **Other app performance data**
- Device or other IDs > **Device or other IDs**

Everything else stays unticked (no name, e-mail, phone, address, precise location, contacts, photos, audio, files, health, messages,
search or browsing history, installed apps, calendar, financial account details, other user-generated content).

## Step 4 - Data usage and handling (one card per ticked type)
Columns: C = collected, S = shared. "Required" = "Data collection is required (users can't opt out)"; "Optional" = "Users can choose whether this data is collected".

| Data type | C / S | Ephemeral? | Required or optional | Why collected | Why shared | Source |
|---|---|---|---|---|---|---|
| Approximate location | C + S | No | Required | App functionality, Analytics, Advertising or marketing, Fraud prevention, security, and compliance | Advertising or marketing, Analytics, Fraud prevention, security, and compliance | AdMob (IP address at every ad request, from app start); Unity IAP (country from IP) |
| User IDs | C + S | No | Optional | App functionality, Account management, Analytics, Fraud prevention, security, and compliance | App functionality (Google / Firebase as service provider) | Play Games player id (in the cloud save); Firebase uid (leaderboard sign-in); Unity IAP player / installation id |
| Purchase history | C | No | Optional | App functionality, Analytics, Fraud prevention, security, and compliance | - | Unity IAP transaction data to Unity's IAP service; product and transaction ids in the cloud save |
| App interactions | C + S | No | Required | Advertising or marketing, Analytics, Fraud prevention, security, and compliance | Advertising or marketing, Analytics, Fraud prevention, security, and compliance | AdMob (app launch, taps, video views) |
| Other actions | C | No | Optional | App functionality, Account management | - | Game progress, wallet, items and records in the Saved Games file (only when signed in) |
| Crash logs | C | No | Optional | Analytics | - | Unity IAP service telemetry |
| Diagnostics | C + S | No | Required | App functionality, Analytics, Advertising or marketing, Fraud prevention, security, and compliance | Advertising or marketing, Analytics, Fraud prevention, security, and compliance | AdMob (SDK performance), Unity IAP, Play Games Services |
| Other app performance data | C | No | Optional | App functionality, Analytics | - | Unity IAP |
| Device or other IDs | C + S | No | Required | App functionality, Analytics, Advertising or marketing, Fraud prevention, security, and compliance | Advertising or marketing, Analytics, Fraud prevention, security, and compliance | AdMob (advertising ID, app set ID); Unity IAP installation id; Play Games Services |

"Shared" is ticked only for the AdMob-sourced types: Google's disclosure says the plugin "collects and shares" them and Google uses them for
its own advertising. Unity acts as our processor for IAP (its disclosure says not shared) and Google stores the save on the player's own
Drive, so those rows are collected only.

## Step 5 - Preview
Check the summary against the table above, **Save**, then **Submit** (the form is reviewed with the next release). Afterwards use
**Export to CSV** and commit the file as `Planning/play-data-safety.csv` so later changes can be diffed.

## Related App content items (same page)
- **Advertising ID**: Yes, the app uses advertising ID; purposes Advertising or marketing, Analytics. The Google Mobile Ads SDK merges the
  `com.google.android.gms.permission.AD_ID` permission into the manifest.
- **Privacy policy URL**: `https://jpdash-creator.github.io/fluffy-climbers-sky-tower/policy.html` (the same address as `legal_config.json`).
- **Ads**: Yes, the app contains ads (rewarded video only).

## Policy alignment (done 2026-11-10)
`POLICY.md` was aligned with this form on 2026-11-10 (summary bullets, sections 5, 6, 7, 8, 11, 13, 14, 16 and 18; `docs/policy.html`
regenerated with `python Tools/legal_html.py`): (1) ad-SDK data is collected from app start because the ad is pre-loaded, not "only if you
tap an ad button"; (2) Unity IAP 5.4 sends its own diagnostics, crash logs, device / installation identifiers and country to Unity's IAP
service whenever the shop is used, independent of the (disabled) Unity engine services. Section 18 now lists the same nine data types with
the same collected / shared / required answers as Step 4 above. Keep the two in step: a change to one is a change to the other.

## Sources
- Google Mobile Ads Unity plugin, Google Play data disclosure: https://developers.google.com/admob/unity/privacy/play-data-disclosure
- Unity In-App Purchasing 5.4+, Google Play data safety: https://docs.unity.com/en-us/iap/privacy-and-consent/google-play-data-safety
- Unity IAP privacy overview (default data collected): https://docs.unity.com/en-us/iap/privacy-and-consent/overview
- Play Games Services, prepare for Google Play's data disclosure requirements: https://developer.android.com/games/pgs/data-collection
- Google Play Help, provide information for the Data safety section: https://support.google.com/googleplay/android-developer/answer/10787469


## Leaderboard and Firebase: answers to enter (owner decision 2026-11-12)

Added when the weekly leaderboard (`Planning/LEADERBOARD-DESIGN.md`) and Firebase Authentication were built. The feature is OFF in the app until the owner switches it on, but the data safety form must be right BEFORE a build that contains the code is released (the SDKs are in the build either way). Source text: `POLICY.md` section 4a (what is sent), `TERMS.md` section 12a (public name and picture), `POLICY.md` section 18 (the mirror table, aligned 2026-11-12).

Enter or change these items. Everything else in Steps 2 to 4 above stays as it is.

| Play Console item | Data type | Collected | Shared | Ephemeral | Required / optional | Purposes (collected) | Purposes (shared) | Source in the build |
|---|---|---|---|---|---|---|---|---|
| Personal info > User IDs | Google Play Games player id and Firebase uid | Yes | Yes: with a service provider (Google / Firebase; the uid is also shown only as a short public id, never the uid) | No | Optional (only when signed in and the player joins the boards) | App functionality, Account management, Fraud prevention, security, and compliance | App functionality | Play Games Services; Firebase Authentication; Cloud Run token check |
| Personal info > Name | Play Games display name | Yes | Yes: shown publicly to other players on the boards and held by Google as service provider | No | Optional (only if the player accepts the one-time consent popup and plays the boards) | App functionality | App functionality | Cloud Firestore `players/{uid}.displayName`, boards top 100 |
| Photos and videos > Photos | Link to the Play Games profile picture (no file is read or uploaded) | Yes | Yes: shown publicly to other players | No | Optional (same as Name) | App functionality | App functionality | Cloud Firestore `players/{uid}.avatarUrl` |
| App activity > App interactions | Weekly scores (crowns), the recorded run inputs by game tick, hero and power-ups used, prize claims | Yes | Yes: with a service provider (Google / Firebase); scores are public on the boards | No | Optional | App functionality, Fraud prevention, security, and compliance | App functionality | Cloud Run `/submit-run`, Cloud Firestore `weeks/...` |
| Device or other IDs | Firebase installation id (Firebase Authentication) in addition to the ad IDs | Yes | Yes: with a service provider (Google / Firebase) | No | Required once the SDK runs | App functionality, Fraud prevention, security, and compliance | App functionality | Firebase Installations (inside Firebase Auth); already ticked for AdMob |
| Approximate location | IP address in Cloud Run request logs | Yes | Yes: with a service provider (Google) | No | Optional | Fraud prevention, security, and compliance | Fraud prevention, security, and compliance | already ticked for AdMob; no change |

Fixed answers for the same section:
- Data NOT sold: **No** data is sold.
- **Firebase Analytics is disabled.** No analytics events, no advertising ID collection, no SSAID, all consent-mode defaults denied: manifest flags in `Assets/Plugins/Android/FluffyPrivacyDefaults.androidlib` (`firebase_analytics_collection_deactivated=true`, `firebase_analytics_collection_enabled=false`, `google_analytics_adid_collection_enabled=false`, `google_analytics_ssaid_collection_enabled=false`, four `google_analytics_default_allow_*=false`) and the install-referrer permission is removed (`tools:node="remove"`). So Firebase adds NO Analytics purpose and no new "Analytics" tick for itself; do not tick Crash logs or Diagnostics because of Firebase.
- Encrypted in transit: **Yes** (HTTPS to Cloud Run and Firebase).
- Users can request deletion: **Yes**. E-mail jpdash.creator@gmail.com (`POLICY.md` section 4a and 13): profile, weekly rows and claims are deleted within one month; weekly rows also expire 90 days after the week ends (Firestore TTL).
- Account creation question: stays **No** (the game has no own account; the Firebase uid is created silently from the Play Games sign-in). If Play review objects, answer Yes and give the policy URL as the deletion-request URL.
- After saving, export the CSV (`Planning/play-data-safety.csv`) as before.

Notes and mismatches found 2026-11-12:
1. Play's own help says that sending data to a service provider that processes it on your behalf is not "sharing". The owner decision is to declare it as shared with a service provider (the cautious answer; it is also the label the public boards need for Name, Photos and scores). Keep it unless Play review asks to change it.
2. `POLICY.md` section 18 used the label "Other actions" for the leaderboard scores; changed to App interactions to match this table. Its User IDs row now names the Firebase uid.
3. `docs/policy.html` is generated from `POLICY.md` (`python Tools/legal_html.py`) and was NOT regenerated in this documentation pass: regenerate it before publishing.
4. Section 4a says other players see the top 100 of each board; no mismatch with this table. `TERMS.md` section 12a says the same about name and picture.
