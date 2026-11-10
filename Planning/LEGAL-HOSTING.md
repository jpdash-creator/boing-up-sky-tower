# Privacy policy and terms: where they live, how to publish them, what still blocks release

The public legal documents of Fluffy Climbers: Tower Jump are two files at the repository root:

- `POLICY.md`: the privacy policy (Google Play "Privacy policy URL", Data safety answers, App Store privacy labels).
- `TERMS.md`: the terms of use (licence, virtual items, chest odds, purchases, subscriptions, refunds, fair play, liability).

`index.md` and `_config.yml` at the root make the repository publishable as a GitHub Pages site with the same layout as the Fastline app repo; `LICENSE` is the all-rights-reserved notice for the code.

Both documents were written from the code and docs as of build v0.54.73 (2026-11-09). They describe: Google Play Games sign-in and Saved Games cloud save (docs/35), Unity IAP gem packs and the two subscriptions (docs/33, `ProductCatalog`), Unity LevelPlay rewarded ads with the `AD_ID` permission (docs/13, docs/32, `LevelPlayAndroidDependency`), no own server, no analytics, no notifications, Android backup off (`AndroidNoBackup`), Reset progress keeping purchase records (docs/38), chest odds (CLAUDE.md, `ChestCards`). **Have both reviewed by a qualified lawyer before the first store submission.**

## 1. Publishing (both stores require a public URL)

GitHub Pages is free only for **public** repositories. Two options:

| Option | When | Result |
|---|---|---|
| A. Separate public repo (recommended) | The game repo stays private | Create a public repo, for example `fluffyclimbers-legal`, and copy `POLICY.md`, `TERMS.md`, `index.md` and `_config.yml` into its root (drop the Unity folders from the `exclude` list). Settings > Pages > Deploy from branch `main` / `(root)`. URLs: `https://jpdash-creator.github.io/fluffyclimbers-legal/POLICY` and `.../TERMS`. Re-copy the two files after every edit here (the root copies in this repo stay the source of truth). |
| B. This repo, made public | Only if the whole game source may be public | Settings > Pages > Deploy from branch `main` / `(root)`. URLs: `https://jpdash-creator.github.io/boing-up-sky-tower/POLICY` and `.../TERMS`. The `_config.yml` already excludes the Unity folders from the site. |

Open each URL in a private browser window before entering it anywhere.

## 2. Store console entries that depend on these documents

| Where | Entry | Source |
|---|---|---|
| Play Console > Store listing | Privacy policy URL | the POLICY URL above |
| Play Console > App content > Data safety | Collected and shared data | `POLICY.md` section 18 (purchase history; advertising ID; approximate location; app diagnostics; Play Games id and display name; app activity (save) when signed in). Payment data is handled by Google, not collected by the app. |
| Play Console > App content > Ads | "Contains ads": yes (rewarded only) | `POLICY.md` section 6 |
| Play Console > App content > Advertising ID | Uses the advertising ID: yes (ads, fraud prevention) | `LevelPlayAndroidDependency` declares `AD_ID` |
| Play Console > App content > Target audience | 13 and over; **not** designed for children | `POLICY.md` section 15, `TERMS.md` section 3, docs/13 row 12 |
| Play Console > App content > Content rating | In-app purchases and ads: yes; randomised paid items (chests): yes | `TERMS.md` section 5 |
| App Store Connect (later) | Privacy labels, EULA link (custom EULA = TERMS URL), odds disclosure | `POLICY.md` section 18, `TERMS.md` sections 5 and 24 |

## 3. Decisions and code gaps the documents assume (owner)

1. **Audience.** The documents say "not directed to children under 13, minimum age 13, parents consent for under-18s" (the Play Console plan in docs/13 row 12). `PRODUCT.md` says "casual players of all ages". If the Play target audience is ever set to include under-13s, the Families policy applies (certified ad SDKs, no personalised ads, no AD_ID) and both documents must be rewritten first.
2. **In-app links.** Google Play's User Data policy requires the privacy policy to be linked **inside the app** as well as on the listing. Docs/38 currently says Settings has no privacy or terms links; add two small link rows (open the POLICY and TERMS URLs) before submission.
3. **Consent prompt.** `POLICY.md` section 6 promises a consent prompt before the first ad where the law requires one (EEA, UK, Switzerland). `Services.ConsentGiven` is still a stub defaulting to non-personalised; the Google UMP flow (docs/33 section 6, docs/13 section 4.4) must be built before ads go live in those regions.
4. **Chest odds in the UI.** `TERMS.md` section 5 says the in-game information panel shows contents and odds and prevails. The chest (i) popup must show the odds per rarity (docs/13 row 16) before release.
5. **Receipt validation.** `TERMS.md` section 6 says purchases are verified before granting. `UnityIapPaymentService.OnPending` still has the validation TODO (docs/33 section 7).
6. **Admin code.** Change `adminCode` in `Assets/Resources/Services/admin_config.json` from the documented default before a public build; the terms forbid players from using admin and test tools, which only helps if the code is secret.
7. **Publisher identity.** The documents name J. P. Dash, Bengaluru, Karnataka, India, with jpdash.creator@gmail.com, the same as the Fastline app. If the Play Console developer name is different (for example a "JD Labs" trade name), add it to section 1 of both documents.
8. **Ad networks.** Only the LevelPlay (ironSource / Unity Ads) network is named. Adding AdMob or any other network means a new row in `POLICY.md` sections 6 and 7 before the build ships.

## 4. Keeping them current

- Any change to data handling (new SDK, permission, analytics, server, notifications, ad network, product type, cloud feature, age gate) updates `POLICY.md` and/or `TERMS.md` and the "Last updated" date **before** the build ships, and is announced in the release notes when material.
- Keep `.claude/rules` and CLAUDE.md pointing at these files so that the rule is applied in every session.
