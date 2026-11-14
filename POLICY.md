# Fluffy Climbers: Tower Jump Privacy Policy

Last updated: 2026-11-13

> **In short**
>
> - Fluffy Climbers: Tower Jump ("the Game") has no account system of its own, no server of its own except the optional weekly leaderboard service that runs only if you sign in with Google Play Games (section 4a), no analytics or crash-reporting software of its own (the advertising and purchasing libraries report their own diagnostics to Google and Unity, sections 5 to 7), no chat and no push notifications.
> - Your progress (levels, stars, coins, gems, heroes, power-ups, records and settings) is stored on your device. If you choose to sign in with Google Play Games, a copy of your progress is also stored in your Google account through Google's Saved Games service. Guests keep their progress on the device only.
> - If you sign in with Google Play Games and play the optional weekly leaderboards, your Google Play Games display name, a link to your Google Play Games profile picture, and your weekly scores are stored on Google Cloud servers in Mumbai, India (Firebase), and other players can see your display name, picture and score on the boards. Guests are never on a leaderboard (section 4a).
> - Purchases are processed by Google Play (and, where the Game is offered there, the Apple App Store). We never receive card or bank details.
> - The Game shows optional rewarded video ads, and only after you tap an ad button. The advertising software built into the Game (the Google Mobile Ads SDK, which serves Google AdMob ads) starts with the Game and loads the next ad in advance, so from the first launch it uses your device's advertising ID and shares device, app and ad-interaction data with Google, whether or not you watch an ad. Where the law requires it, Google's consent form (the User Messaging Platform) asks for your choices before ads are requested, and you can change them at any time under "Privacy choices" in Settings.
> - You can play as a guest, unlink your Google account, reset all progress, change your ad choices in Settings, and reset or delete your advertising ID at any time.
> - The Game is not directed to children under 13. If you are under 18, a parent or guardian must agree to this policy and to our Terms of Use for you. See section 15.

## Contents

1. [Who we are](#1-who-we-are)
2. [Scope and how to read this policy](#2-scope-and-how-to-read-this-policy)
3. [Data stored on your device](#3-data-stored-on-your-device)
4. [Google Play Games sign-in and cloud save (optional)](#4-google-play-games-sign-in-and-cloud-save-optional)
    - 4a. [Weekly leaderboard (optional, only when signed in)](#4a-weekly-leaderboard-optional-only-when-signed-in)
5. [Purchases and payments](#5-purchases-and-payments)
6. [Advertising](#6-advertising)
7. [Third-party software built into the Game](#7-third-party-software-built-into-the-game)
8. [Data we do not collect](#8-data-we-do-not-collect)
9. [Device permissions](#9-device-permissions)
10. [Why we process data and our legal bases](#10-why-we-process-data-and-our-legal-bases)
11. [Who receives data](#11-who-receives-data)
12. [International transfers](#12-international-transfers)
13. [Retention and deletion](#13-retention-and-deletion)
14. [Your choices](#14-your-choices)
15. [Children and age](#15-children-and-age)
16. [Your rights (GDPR, UK GDPR, CCPA/CPRA, India DPDP Act, LGPD and others)](#16-your-rights-gdpr-uk-gdpr-ccpacpra-india-dpdp-act-lgpd-and-others)
17. [Security](#17-security)
18. [Store data safety summary](#18-store-data-safety-summary)
19. [Changes to this policy](#19-changes-to-this-policy)
20. [Contact](#20-contact)

## 1. Who we are

Fluffy Climbers: Tower Jump ("the Game", "we", "us", "our") is a 2D vertical-jumping arcade game for mobile devices published by J. P. Dash, an individual developer based in Bengaluru, Karnataka, India. The Game is distributed on Google Play under the application id `com.jdlabs.fluffyclimbers` and may later be distributed on the Apple App Store.

For privacy questions, requests and complaints, contact jpdash.creator@gmail.com. J. P. Dash is the controller (under the GDPR and the UK GDPR), the business (under the CCPA/CPRA), and the data fiduciary and grievance officer (under India's Digital Personal Data Protection Act, 2023) for the limited personal data described in this policy.

## 2. Scope and how to read this policy

This policy explains what data the Game handles, what stays on your device, what third parties receive through the software built into the Game, and the choices and rights you have. It applies to the Game on every platform where we offer it, to its testing and pre-release builds, and to e-mail you send us. It does not cover the services of Google (including AdMob), Apple or Unity; their own policies are linked where they apply. Your use of the Game is governed by our Terms of Use, published next to this policy. Both documents can be opened at any time from the Game's Settings screen (the Privacy and Terms buttons) and from the store listing.

Where this policy says "we receive" or "we collect", it means that data reaches the developer. Most of the data described below is processed only on your device, or only by the third-party service named, and never reaches us. We do not operate a server for normal play. The only exception is the optional weekly leaderboard (section 4a), which runs on Google Cloud services that we configure and control and that keep a small database of the players who use it.

## 3. Data stored on your device

The Game keeps the following in its private app storage on your device:

- **Progress:** levels cleared, stars earned, best scores, clear counts, timed records and medals per level and per gameplay mode, best floor and records in endless and time-attack modes, unlocked regions, and which hero introductions you have seen.
- **Wallet and items:** coins, gems, owned power-ups, owned heroes and their copies, chest-opening counter, bundle offers and their timers, reward claims and daily or weekly counters, and subscription claim state (days left, last claim day).
- **Purchase records:** for each real-money purchase, the product id, the kind of product (gem pack or subscription), the store transaction id and the time. These records let the Game restore your subscription days and prevent a one-time bonus from being granted twice. They contain no payment details.
- **Account fields:** whether you chose guest or Google play, the Google Play Games player id of a linked account, the time of the last cloud sync, the time of the last change, and when the "connect Google" reminder was last shown.
- **Settings:** sound, music, haptics, reduced motion, left-handed layout, text size, gameplay difficulty, pace ghost, and promotional flags. Development-only test switches exist in development builds and have no effect in release builds.

This data is written as plain (unencrypted) files in the app's private folder, which other apps cannot read on an unmodified device. The Game is configured to opt out of Android device backup, so the save is not copied into Google device backups or device-to-device transfers; the only off-device copy is the optional cloud save described in section 4. Nothing in this section is sent to us. Uninstalling the Game deletes it.

## 4. Google Play Games sign-in and cloud save (optional)

On first launch you choose **Sign in with Google Play** or **Play as guest**. You can change your choice later in Settings. Signing in is never required to play.

**What the Game accesses.** When you sign in, the Game uses the Google Play Games Services software on your device and receives two things from it: your Play Games **player id** (an identifier Google creates for you for this Game only) and your Play Games **display name**. The display name is shown only to you, in the Settings screen of your own device, unless you use the weekly leaderboards (section 4a). The player id is stored in your save so that the Game can recognise the linked account. The Game does not receive your Google e-mail address, real name, contacts, friends list or photo files. (If you use the weekly leaderboards, section 4a, the Game also reads the link to your Play Games profile picture.)

**Cloud save.** While you are signed in, the Game stores one save file (the data listed in section 3, as plain JSON, together with a short summary and a time stamp) in Google's **Saved Games** service, which keeps it in the hidden app-data area of your Google Drive. Google processes that file under the [Google Privacy Policy](https://policies.google.com/privacy) and the [Google Play Games Services terms](https://developers.google.com/games/services/terms). We have no server and cannot read your cloud save; only the Game running on a device signed in to your Google account reads and writes it. The Game uploads a few seconds after a change and when you pause or close the app. When your device save and your cloud save differ, the Game shows you both and lets you choose; the one you do not pick is replaced.

**Google's own features.** Google Play Games may create a gamer profile for you and offer its own features (achievements, friends, profile visibility). Those features, and what other people can see of your Play Games profile, are controlled in the Google Play Games app and your Google account settings, not by us. Apart from the optional weekly leaderboard (section 4a), the Game publishes nothing about you.

**Unlinking.** "Unlink Google Account" in Settings makes the Game stop using the account and stops uploads. Your progress stays on the device. The cloud save remains in your Google account until you delete it (Google Play Games settings > Delete Play Games data for this Game, or Google Drive > Settings > Manage apps > hidden app data), or until you use Reset progress while signed in, which uploads an empty save in its place.

Google Play Games sign-in and cloud save are available on Android. If we offer a comparable feature on another platform, we will describe it here first.

## 4a. Weekly leaderboard (optional, only when signed in)

The Game has three weekly leaderboards: Overall, Levels and Endless. They rank the crown points you earn in a week. A week runs from Monday 00:00 to the next Monday 00:00 India time. The leaderboards need a Google Play Games sign-in; a guest is never on a leaderboard and nothing in this section applies to a guest.

**What is sent and stored.** When you sign in and finish a run, the Game sends the run to our leaderboard service so that it can check the score: the level or tower and gameplay setting, the hero you used and its stat variant, the power-ups you used, the recorded inputs of the run (button presses by game tick), the score you reached, the game version and a random run id. The service signs you in to Firebase Authentication with your Google Play Games account, which gives it an anonymous-looking Firebase user id, and keeps for each week: your best crown score in each level, setting and mode, your weekly totals, your display name (copied from Play Games, or the name you chose yourself, see "Your own display name" below) and the link to your Google Play Games profile picture (copied from Play Games), a short public player id that is derived from your Firebase user id so that boards do not show it, the time of your latest submissions, and any prize you claimed. Your display name and Google Play Games profile picture are shown to every other player on the boards (the top 100 of each board for the week). Your own rank is shown to you. We do not receive your e-mail address, real name, contacts, location or advertising ID through this feature.

**Your own display name.** Once you are signed in with Google, Settings lets you choose a display name for the leaderboards instead of your Google Play Games name (3 to 16 letters, numbers, spaces or underscores; a built-in filter refuses rude words). The name is your own choice: do not use your real name or any personal detail if you do not want other players to see it. It is saved in your game save (and in your cloud save) and, once you have confirmed the leaderboards, sent with your other leaderboard data and shown to other players. It is not checked for uniqueness. You can change it at any time, and clearing it brings your Google Play Games name back. We may hide or change a name that is offensive or impersonates someone.

**Your confirmation.** Before the Game first sends anything to the leaderboard it asks you once to confirm that your leaderboard name (the display name you chose, or your Google Play Games name) and profile picture will be shown to other players and that your runs are sent to our server for checking. Nothing is sent until you confirm, and you can leave the leaderboards at any time in Settings. The Game also asks our server for a one-time ticket with a random level seed before a run you want to submit; the ticket contains your Firebase user id, the level or tower, the setting and an expiry, and is stored in used form for a few days to stop it being reused.

**Who processes it.** The data is processed for us by Google as our processor, using Firebase Authentication, Cloud Firestore and Google Cloud Run, in Google's data centre region in Mumbai, India (asia-south1). Google's terms and privacy information for these services apply (the Google Cloud and Firebase privacy documentation).

**How long it is kept.** Weekly rows (your slot scores, your weekly totals and the weekly boards) are deleted automatically 90 days after the week ends. Prize claim records are deleted on the same schedule. Your profile row (display name, picture link, public player id, the hero variants first used, and rate-limit counters) is kept while you use the leaderboard and until you ask us to delete it.

**Your choices and deletion.** Signing out of Google Play Games, or choosing to play as a guest, stops new submissions. To have your leaderboard data deleted at once (profile, weekly rows and claims), e-mail jpdash.creator@gmail.com from the address you want us to reply to and tell us your Google Play Games player name; we delete it within one month and your name and picture disappear from the boards when the next board refresh runs. Deleting the Game's Play Games data in your Google account does not by itself delete the leaderboard rows.

**Prizes.** The top players of a finished week can claim a prize (section 5 of the Terms of Use). Claiming one stores a record that you claimed it, so that it cannot be claimed twice.

## 5. Purchases and payments

The Game sells gem packs and four subscription plans (two weekly and two monthly plans) through the store's in-app purchase system: Google Play Billing on Android (through the Unity In-App Purchasing library on your device) and, where the Game is offered there, Apple's in-app purchase system.

- **Payment data.** Payment is processed entirely by Google or Apple. We never receive card, bank, billing-address or payment-account details. Subscriptions renew automatically through Google Play until you cancel them in the Play Store (see the Terms of Use, section 7).
- **What the Game receives on your device.** For a completed purchase the store software gives the Game the product id, the store order or transaction id, and a purchase token or receipt that the Game uses to verify the purchase before it grants the gems or subscription. The Game stores the product id, kind, transaction id and time in your save (section 3). The receipt itself is not kept by the Game beyond the purchase. The Game also reads the store's localised price text so that prices can be shown in your currency.
- **Restoring purchases.** "Restore" in Settings asks the store software on your device for your purchase history for this Game and re-grants active subscription days. This reads data from your store account on your device; it is not sent to us.
- **What we receive from the store.** Google Play and Apple provide developers with sales and subscription reports and order details (for example order id, country, amount, currency, tax and refund status) under their own policies. We use these reports only for accounting, tax, refund handling and fraud prevention, and keep them as long as tax and accounting laws require. We cannot identify you from them unless you contact us about an order.
- **Unity.** The Unity In-App Purchasing library is made by Unity Technologies and runs the purchase flow on your device. It is started only when you open the shop, restore purchases or buy something; a guest who never opens the shop sends nothing to Unity. Once started, the library sends to Unity's purchasing service: the product, price and transaction details the store returns (product id, order id, purchase token), a Unity installation identifier and session identifier, device information (model, operating system version, language and locale), its own diagnostic, performance and crash data, and the country derived from your IP address. Unity uses this to run the purchasing service, to de-duplicate transactions and to give us sales reports; it keeps transaction data as long as the service needs it and its service logs for 30 days, and acts as our processor under the [Unity Privacy Policy](https://unity.com/legal/privacy-policy). Unity never receives payment details.

Purchases, refunds and subscription management are governed by the [Google Play Terms of Service](https://play.google.com/about/play-terms/) and the [Google Privacy Policy](https://policies.google.com/privacy), or by Apple's media services terms and the [Apple Privacy Policy](https://www.apple.com/legal/privacy/).

## 6. Advertising

The Game is free to download and is partly funded by **rewarded video ads**. An ad plays only after you tap an ad button (for example "watch an ad to revive"), and you can always decline. There are no banner ads and no ads that interrupt play uninvited.

The ad software itself, however, starts together with the Game: once your consent choices are known (see "Consent and your ad choices" below) it initialises and loads the next rewarded ad in advance, so that the ad is ready when you ask for it. The data listed below is therefore sent from the first launch of each play session, whether or not you ever watch an ad. Where the law requires consent, nothing is requested until you have answered the consent form.

**Who serves the ads.** Ads are delivered by **Google AdMob**, an advertising service of Google LLC, through the **Google Mobile Ads SDK** built into the Game. Google acts as an independent controller for the data it handles when it serves and measures ads. If we add further ad networks we will list them here before they go live. The Game is not directed to children and its ad content rating is set to "T" (Teen), so AdMob is asked to show ads suitable for that rating.

**Data the ad software processes.** To request, show and measure an ad, the Google Mobile Ads SDK on your device and Google's advertising systems may collect and process:

- your device's **advertising ID** (Google Advertising ID on Android; the Game declares the `AD_ID` permission for this purpose), and, where applicable, other device or app identifiers;
- your **IP address**, from which an approximate location (country or city level) can be derived;
- **device and app information:** device model, operating system and version, language, screen size, network type, app id and version, and SDK version;
- **ad interaction data:** which placement requested an ad, impressions, clicks, video completion, and the reward signal that tells the Game you watched the ad to the end;
- **diagnostic and performance data** about ad loading and display errors.

This data is used to deliver ads, to limit how often you see the same ad, to detect fraud and invalid traffic, to measure performance, to pay the Game for completed views and, where you have consented or the law allows it, to personalise the ads you see.

**Consent and your ad choices.** Where the law requires consent before ads are requested or personalised (for example the European Economic Area, the United Kingdom and Switzerland), or requires a privacy message (for example in some US states), the Game shows Google's consent form (the Google User Messaging Platform) before the first ad and passes your answer to the Google Mobile Ads SDK. You can review and change your answer at any time under **Privacy choices** in the Game's Settings, where that entry is available to you. If you decline personalised ads, Google may still use the advertising ID and device data for frequency capping, fraud prevention and measurement, and you will see non-personalised ads. On Android you can also reset your advertising ID, delete it, or opt out of ads personalisation at any time (Settings > Google > Ads, or Settings > Privacy > Ads); if you delete it, the ad software receives a string of zeros instead.

**Ad content.** The ads themselves come from third-party advertisers. Tapping an ad can open a third-party website or store page outside the Game, where that party's privacy policy applies.

The processing by Google is described in the [Google Privacy Policy](https://policies.google.com/privacy) and in [How Google uses information from sites or apps that use its services](https://policies.google.com/technologies/partner-sites). AdMob's terms are at [admob.google.com/home/terms](https://admob.google.com/home/terms/).

## 7. Third-party software built into the Game

| Component | Provider | Purpose in the Game | Data it may process | Policy |
|---|---|---|---|---|
| Unity game engine | Unity Technologies | Runs the Game | Device and operating-system information needed to run the Game. Unity Analytics, Unity Crash Reporting, Unity Performance Reporting and Unity Ads (as engine services) are turned off in the Game and no Unity cloud project is linked. | [Unity Privacy Policy](https://unity.com/legal/privacy-policy) |
| Unity In-App Purchasing | Unity Technologies | Talks to Google Play Billing / Apple in-app purchase | Product, price and transaction details, Unity installation and session identifiers, device information, the library's own diagnostics, performance data and crash logs, country derived from your IP address (section 5) | [Unity Privacy Policy](https://unity.com/legal/privacy-policy) |
| Google Mobile Ads SDK (Google AdMob) | Google LLC | Rewarded video ads | Advertising ID, IP address, device and app information, ad interaction data, diagnostics (section 6) | [Google Privacy Policy](https://policies.google.com/privacy), [AdMob terms](https://admob.google.com/home/terms/) |
| Google User Messaging Platform (consent form) | Google LLC | Shows the ad consent / privacy choices form and stores your choice | Your consent choices, device and app identifiers needed to apply them | [Google Privacy Policy](https://policies.google.com/privacy) |
| Google Play Billing | Google LLC | Payments | Payment and order data (never seen by us) | [Google Privacy Policy](https://policies.google.com/privacy) |
| Google Play Games Services | Google LLC | Optional sign-in and cloud save | Play Games player id, display name, saved game file (section 4) | [Google Privacy Policy](https://policies.google.com/privacy) |
| Firebase Authentication | Google LLC (as our processor) | Signs your Google Play Games account in to the leaderboard service (only when you use the weekly leaderboards, section 4a) | Firebase user id, the Play Games sign-in token, time and approximate network information of the sign-in | [Firebase privacy information](https://firebase.google.com/support/privacy) |
| Cloud Firestore | Google LLC (as our processor) | Stores your weekly leaderboard rows (section 4a) in Mumbai, India (asia-south1) | Display name, profile picture link, weekly scores and totals, public player id, prize claims | [Firebase privacy information](https://firebase.google.com/support/privacy) |
| Google Cloud Run | Google LLC (as our processor) | Runs the leaderboard service that checks and records your runs (section 4a), in Mumbai, India (asia-south1) | The runs you submit (section 4a), your Firebase user id, IP address and request logs | [Google Cloud privacy notice](https://cloud.google.com/terms/cloud-privacy-notice) |
| Google Play services | Google LLC | Required by the components above on Android | Device identifiers and diagnostics under Google's policy | [Google Privacy Policy](https://policies.google.com/privacy) |
| Newtonsoft Json.NET, Unity Addressables, fonts (Lilita One, Luckiest Guy, Nunito) | Various | Reading content, art and text on the device | None. These run entirely on your device and make no network requests. | Their open-source licences |

All regions, levels, art and sounds ship inside the app package. The Game downloads no content from a server of ours, so no content-delivery network of ours sees your IP address.

## 8. Data we do not collect

- No account, sign-in, name, e-mail address, phone number or password is required or collected by the Game. Google sign-in is optional and handled by Google. If you use the weekly leaderboards (section 4a) the leaderboard service receives your display name (your Play Games name, or the name you chose yourself) and profile picture link, but never your e-mail address, real name or password.
- No analytics, usage-tracking or crash-reporting software of ours is included, and the Game sends no usage events to us. The only diagnostics that leave your device are those the Google Mobile Ads SDK, Google Play services and the Unity In-App Purchasing library report to their own makers (sections 5 to 7).
- No precise location, contacts, calendar, photos, files, camera or microphone access.
- No chat, messages, friend lists or user-generated content. The only thing other players can see is your display name (your Play Games name, or the name you chose in Settings), profile picture and weekly score on the leaderboards, and only if you sign in and play them (section 4a).
- No push or remote notifications; the Game has no notification feature at all.
- No server of ours for normal play: the Game does not connect to a computer operated by us, except to the weekly leaderboard service (section 4a) when you are signed in. All other network activity is performed by the Google (including AdMob), Apple and Unity software described above.
- No sale of personal data, and no cross-app tracking by us.

The weekly leaderboards (section 4a) were added on 2026-11-12. If the Game gains another feature that changes any of this (for example events, notifications or analytics), we will update this policy before the feature is released and tell you about it in the Game or in the store release notes.

## 9. Device permissions

| Permission (Android) | Why | Added by |
|---|---|---|
| Internet and network state | Ads, purchases, Google sign-in and cloud save | Unity engine, Google Mobile Ads SDK, Unity In-App Purchasing |
| `com.android.vending.BILLING` | In-app purchases through Google Play | Unity In-App Purchasing |
| `com.google.android.gms.permission.AD_ID` | Lets the ad software read the advertising ID (Android 13 and later) | Google Mobile Ads SDK |
| Vibrate | Haptic feedback when you turn Haptics on in Settings | Unity engine |

The Game requests no runtime permissions: it never asks for camera, microphone, location, contacts, storage, photos or notification access. Google Play Games sign-in uses the Google account picker provided by Google Play services and needs no separate permission from the Game.

## 10. Why we process data and our legal bases

| Purpose | Data | Legal basis (GDPR / UK GDPR) |
|---|---|---|
| Running the Game and saving your progress on your device | Section 3 | Performance of our contract with you (the Terms of Use) |
| Optional Google sign-in and cloud save | Section 4 | Performance of the contract, at your request |
| Processing, delivering, restoring and verifying purchases and subscriptions | Section 5 | Performance of the contract; legal obligation (tax and accounting) |
| Showing rewarded ads to fund a free game, measuring them and preventing ad fraud | Section 6 | Legitimate interests (funding the Game, fraud prevention), balanced against your interests; or your consent where the law requires consent for any advertising storage or access |
| Personalised ads | Section 6 | Your consent, which you can withdraw at any time under Privacy choices in Settings |
| Weekly leaderboards: checking submitted runs, ranking players, showing the boards and giving out prizes | Section 4a | Performance of the contract, at your request (you choose to sign in and play the boards); legitimate interests (preventing cheating) |
| Security, fraud prevention and enforcing the Terms (for example refund abuse or tampered purchases) | Purchase records and store reports | Legitimate interests |
| Answering your e-mail and handling rights requests | What you send us | Performance of the contract; legal obligation; legitimate interests |
| Complying with law and lawful requests | Any of the above | Legal obligation |

Under India's Digital Personal Data Protection Act, 2023, we process personal data for the purposes above with your consent or for the legitimate uses the Act allows, including performing the contract you entered with us and complying with law.

## 11. Who receives data

- **Google LLC** (Google Play, Google Play Billing, Google Play services, Google Play Games Services) receives the data described in sections 4 and 5 directly from your device, as an independent controller under its own policy.
- **Apple Inc.** receives payment and store data if you use the Game through the App Store.
- **Google LLC (Google AdMob)** receives the advertising ID, device and app identifiers, IP address and ad interaction data described in section 6 directly from your device, and may share them with advertisers and advertising partners as described in Google's policies. Google acts as an independent controller for ad delivery and measurement.
- **Unity Technologies** receives, as our processor, the purchasing data described in section 5 (transaction details, installation and session identifiers, device information, diagnostics and crash logs of the purchasing library, and country) when you use the shop. The Unity engine itself sends nothing, because its cloud services are turned off (section 7).
- **Google LLC as our processor** (Firebase Authentication, Cloud Firestore, Google Cloud Run) receives and stores the leaderboard data of section 4a for us, in Mumbai, India.
- **Other players** see your Play Games display name, profile picture and weekly score on the leaderboards if you sign in and play them.
- **Nobody else.** We have no analytics vendors, no other hosting provider for player data, no marketing partners and no data brokers. We do not sell personal data and we do not share it for money.
- **Legal requirements.** We may disclose information we hold (in practice, order records from the store) if the law requires it or to protect our rights, your safety or the safety of others.
- **Business transfer.** If the Game or the developer's business is transferred to another publisher, the store listing, this policy and the limited records we hold would pass to the new publisher under the same commitments. Your device data and cloud save are not affected, because we do not hold them.

## 12. International transfers

We do not hold your personal data on any server of our own. The only data we store on servers is the leaderboard data of section 4a, which is stored and processed by Google as our processor in Mumbai, India (asia-south1); it can be read by other players in any country because the boards are public in the Game. Google, Apple and Unity process data globally, including in the United States and the European Union, under their own safeguards (such as standard contractual clauses or adequacy decisions). The links in section 7 describe them.

## 13. Retention and deletion

| Data | Where it lives | How long | How to delete |
|---|---|---|---|
| Progress, wallet, items, settings | Your device | Until you reset or uninstall | Settings > Reset (progress) or uninstall the Game |
| Purchase records in the save | Your device and, if signed in, your cloud save | Kept through a Reset so that subscriptions can be restored; removed when you uninstall and delete the cloud save | Uninstall, then delete the Play Games data for the Game |
| Cloud save, Play Games player id and display name | Your Google account (Saved Games, Play Games profile) | Until you delete it or overwrite it | Reset while signed in (uploads an empty save), or delete the Game's Play Games data in Google Play Games settings or Google Drive. We hold no copy on any server of ours, so there is nothing for us to delete on your behalf |
| Weekly leaderboard data (section 4a): slot scores, weekly totals, boards, prize claims | Cloud Firestore, Mumbai, India (asia-south1) | Deleted automatically 90 days after the week ends | E-mail us to delete it earlier |
| Leaderboard profile (display name, picture link, public player id, hero variants, rate-limit counters) | Cloud Firestore, Mumbai, India (asia-south1) | While you use the leaderboards, until you ask us to delete it | E-mail jpdash.creator@gmail.com; we delete it within one month |
| Advertising ID and ad interaction data | Google AdMob systems | Per Google's policies | Reset or delete the advertising ID in Android settings; change or withdraw ad consent under Privacy choices in Settings |
| Store order and sales reports | Google or Apple, and our accounting records | As long as tax and accounting laws require (several years), then deleted | Refunds and order questions go through the store; e-mail us for anything else |
| Unity purchasing service records (transaction details, identifiers, diagnostics, crash logs) | Unity Technologies | Transaction data as long as the service needs it; service logs 30 days | E-mail us; Unity offers no self-service deletion, so we relay your request to Unity as its customer |
| E-mail you send us | Our mailbox | As long as needed to resolve your request and to meet legal obligations, then deleted | Ask us to delete it |

Uninstalling the Game or resetting progress does not cancel a subscription. Cancel it in your Google Play or App Store subscription settings.

## 14. Your choices

- **Play as a guest.** Nothing leaves your device except what sections 5 and 6 describe.
- **Sign in or unlink Google** at any time in Settings. Play as a guest and you are on no leaderboard.
- **Reset progress** in Settings deletes all progress, wallet and items on your device (and, if you are signed in, uploads the empty save to your Google account). Purchase records are kept so that subscriptions can be restored.
- **Ads.** Do not tap ad buttons and no ad is shown (the ad software still loads one in the background, section 6). Open **Privacy choices** in Settings at any time to review, change or withdraw your ad consent (this reopens Google's consent form). Reset or delete your advertising ID, or opt out of ads personalisation, in Android settings.
- **Purchases.** Manage subscriptions, payment methods and purchase approvals (for example "require authentication for purchases" and Family Link approvals) in your Google Play or Apple account.
- **Haptics, sound, music, reduced motion, text size and left-handed layout** are in Settings and stay on your device.

## 15. Children and age

The Game is a general-audience game. It is **not directed to children under 13**, and we do not knowingly collect personal data from children under 13, or under the higher age at which a person can consent to data processing in their country (for example 16 in parts of the European Union). If you are below that age, do not play the Game, or ask a parent or guardian to set it up and supervise your play through their own account.

If you are under 18, you may use the Game only if a parent or guardian has read this policy and our Terms of Use and agrees to them for you. Under India's Digital Personal Data Protection Act, 2023, a person under 18 is a child and their parent or lawful guardian must give verifiable consent; we do not knowingly track or monitor children's behaviour or serve them targeted advertising. Personalised ads depend on the consent choices described in section 6, and we do not knowingly request personalised ads for anyone under the age at which they can give that consent.

Parents and guardians: you can block or require approval for purchases in Google Play (Family Link, purchase authentication) and in Apple's Screen Time and Ask to Buy. If you believe a child has provided personal data to us or to Google AdMob through the Game, contact jpdash.creator@gmail.com and we will delete anything we hold and help you contact Google.

## 16. Your rights (GDPR, UK GDPR, CCPA/CPRA, India DPDP Act, LGPD and others)

Apart from the leaderboard data of section 4a, we hold no server-side data about players, so most rights are exercised directly on your device or with Google (including AdMob) or Apple. For the leaderboard data we are the controller and Google is our processor, and you can exercise your rights by e-mail. We will help with any request. In practice:

- **Access and portability:** your progress is on your device and, if signed in, in your Google account (Google Takeout exports Play Games data). We can provide a copy of any order or e-mail record we hold about you.
- **Correction:** edit progress and settings in the Game; order data is corrected through the store.
- **Erasure:** Reset progress, uninstall, and delete the Game's Play Games data (section 13). We will delete e-mail and order records we hold unless tax or accounting law requires us to keep them.
- **Restriction and objection:** you can object to processing based on legitimate interests (section 10) by contacting us; you can limit ad processing by withdrawing consent under Privacy choices, deleting your advertising ID and not tapping ad buttons.
- **Withdraw consent:** at any time, under Privacy choices in Settings, or by e-mail; withdrawal does not affect processing that already happened.
- **Automated decisions:** we do not profile you or make automated decisions with legal or similar effects about you.
- **European Economic Area and United Kingdom:** you may complain to your local data protection authority (in the UK, the Information Commissioner's Office).
- **California and other US states (CCPA/CPRA and similar laws):** we do not sell personal information. Personalised advertising by ad networks may count as "sharing" for cross-context behavioural advertising; you can opt out through the privacy message and Privacy choices in Settings, where Google's consent form offers that choice for your location, and by resetting or deleting your advertising ID or opting out of ads personalisation in Android settings, which also acts as your "Do Not Sell or Share" request for the Game. We do not knowingly sell or share personal information of consumers under 16. You have the right to know, delete and correct, and not to be discriminated against for exercising your rights. An authorised agent may act for you with proof of authority.
- **India (Digital Personal Data Protection Act, 2023):** you have the right to access a summary of personal data processed, to correction and erasure, to grievance redressal, and to nominate another person to exercise your rights. Our grievance officer is J. P. Dash, jpdash.creator@gmail.com. If you are not satisfied with our response you may approach the Data Protection Board of India.
- **Brazil (LGPD):** you have the rights of confirmation, access, correction, anonymisation, portability, deletion, information about sharing, and revocation of consent, and may complain to the Autoridade Nacional de Proteção de Dados.
- **Other countries:** you have whatever rights your local law gives you, and we will honour them.

To exercise a right, e-mail jpdash.creator@gmail.com from the address you want us to reply to and describe your request. We may ask for information that lets us verify you (for example a store order number) before acting on it. We respond within one month, or within the shorter period your local law requires, and we do not charge for requests unless the law allows it for excessive or repeated requests. Data held by Google, Apple or Unity is controlled by them; contact them to exercise rights over it, and we will point you to the right place if you ask.

## 17. Security

Your progress is protected by your device's own security (app sandboxing, lock screen and device encryption). The Game's network traffic is performed by Google and Unity software over connections those providers secure. Apart from the leaderboard data of section 4a we hold no player data on servers. The leaderboard database accepts no writes from the app itself (a leaderboard service we run checks every run on the server and writes the scores), and the app can read only the public boards and your own rows. No method of storage is perfectly secure, so keep your device updated and protected, do not install modified copies of the Game, and report any security concern to jpdash.creator@gmail.com.

## 18. Store data safety summary

This table mirrors what we declare in the Google Play Data safety section and the App Store privacy labels. "Required" means the data is sent whenever the Game runs, because the ad software starts with the Game (section 6); "optional" means it is sent only if you choose to sign in, open the shop or buy something.

| Data type | Collected by | Purpose | Shared with | Required or optional |
|---|---|---|---|---|
| Approximate location (derived from your IP address) | Google Mobile Ads SDK at every ad request; Unity In-App Purchasing (country only) | App functionality, analytics, advertising, fraud prevention | Google (advertising) | Required for ads; optional for purchases |
| User IDs: Google Play Games player id; Firebase user id (only if you use the weekly leaderboards, section 4a); Unity installation and player identifiers for purchasing | Google Play Games Services (the Game stores the player id on your device and in your cloud save); Firebase Authentication (our leaderboard service); Unity In-App Purchasing | Account management, app functionality, analytics | The Firebase user id and the leaderboard data go to Google as our service provider (Firebase, Cloud Run); never sold | Optional: only when you sign in or use the shop |
| Purchase history (product, price, order or transaction id, time) | Unity In-App Purchasing (sent to Unity's purchasing service); the Game stores product and transaction ids on your device and in your cloud save | App functionality (delivering and restoring purchases), analytics (sales reports), fraud prevention | Not shared; Google or Apple process the payment itself under their own policies | Optional: only if you buy something |
| App interactions: ad interactions (app launch, taps, impressions, video completion) | Google Mobile Ads SDK (AdMob) | Advertising, analytics, fraud prevention, paying for ad views | Google (advertising) | Required |
| Name and photos: your Google Play Games display name and a link to your profile picture | Firebase Authentication and Cloud Firestore, only when you use the weekly leaderboards (section 4a) | App functionality (leaderboards), account management | Shown publicly to other players on the leaderboards; processed for us by Google | Optional: only when signed in and playing the boards |
| App interactions: your weekly leaderboard scores, the runs you submit (inputs by game tick, hero, power-ups used) and prize claims | Cloud Firestore and Google Cloud Run, only when you use the weekly leaderboards (section 4a) | App functionality (leaderboards), fraud prevention | Scores and display name are shown publicly to other players; processed for us by Google | Optional: only when signed in and playing the boards |
| Other actions: your save file (progress, wallet, items, records) | Google Saved Games, only while signed in | App functionality, account management (cloud save) | Not shared; stored in your own Google account | Optional: only when signed in |
| Crash logs | Unity In-App Purchasing (crash data of the purchasing library) | Analytics | Not shared | Optional: only when the shop is used |
| Diagnostics | Google Mobile Ads SDK, Unity In-App Purchasing, Google Play Games Services | App functionality, analytics, advertising reliability, fraud prevention | Google (advertising) | Required |
| Other app performance data | Unity In-App Purchasing | App functionality, analytics | Not shared | Optional: only when the shop is used |
| Device or other IDs: advertising ID, app set ID, other device and app identifiers, Unity installation identifier | Google Mobile Ads SDK (AdMob), Google Play services, Unity In-App Purchasing | App functionality, analytics, advertising, fraud prevention | Google (advertising) | Required; the advertising ID can be reset or deleted in Android settings |

All of this data is encrypted in transit by the software that sends it. None of it is processed only ephemerally. How to delete it: section 13.

Not collected: name (your Play Games display name is sent only if you use the weekly leaderboards, section 4a; otherwise it is shown only on your own device), e-mail address, phone number, address, precise location, contacts, photos, videos, audio, files, health or fitness data, financial account details (payment is handled entirely by the store), messages, search history, browsing history, installed apps, calendar, or any user-generated content (the leaderboards show only your Play Games display name, profile picture and score).

## 19. Changes to this policy

We may update this policy, for example when the Game adds a feature that handles data differently, when we add or remove an ad network, or when the law changes. We will change the "Last updated" date above and publish the new version at the same address. If a change is material, we will also tell you in the Game or in the store release notes before it takes effect, and where consent is required we will ask for it again.

## 20. Contact

J. P. Dash
Bengaluru, Karnataka, India

Privacy, data protection and grievance officer (India DPDP Act): jpdash.creator@gmail.com

Please include "Fluffy Climbers" in the subject line so that your e-mail is found quickly.
