# Privacy policy and terms: where they live, how to publish them, what still blocks release

The public legal documents of Fluffy Climbers: Tower Jump are two files at the repository root:

- `POLICY.md`: the privacy policy (Google Play "Privacy policy URL", Data safety answers, App Store privacy labels).
- `TERMS.md`: the terms of use (licence, virtual items, chest odds, purchases, subscriptions, refunds, fair play, liability).

`index.md` and `_config.yml` at the root make the repository publishable as a GitHub Pages site with the same layout as the Fastline app repo; `LICENSE` is the all-rights-reserved notice for the code.

Both documents were written from the code and docs as of build v0.54.73 (2026-11-09) and updated on 2026-11-10 for Google AdMob rewarded ads (Google Mobile Ads SDK, Advertising ID, Google UMP consent form, "Privacy choices" entry in Settings, ad content rating T). They describe: Google Play Games sign-in and Saved Games cloud save (docs/35), Unity IAP gem packs and the two subscriptions (docs/33, `ProductCatalog`), Google AdMob rewarded ads with the `AD_ID` permission (docs/13, docs/32; the earlier LevelPlay wording was replaced), no own server, no analytics, no notifications, Android backup off (`AndroidNoBackup`), Reset progress keeping purchase records (docs/38), chest odds (CLAUDE.md, `ChestCards`). **Have both reviewed by a qualified lawyer before the first store submission.**

## 1. Publishing (both stores require a public URL)

GitHub Pages is free only for **public** repositories. Two options:

| Option | When | Result |
|---|---|---|
| A. Separate public repo (recommended) | The game repo stays private | Create a public repo, for example `fluffyclimbers-legal`, and copy `POLICY.md`, `TERMS.md`, `index.md` and `_config.yml` into its root (drop the Unity folders from the `exclude` list). Settings > Pages > Deploy from branch `main` / `(root)`. URLs: `https://jpdash-creator.github.io/fluffyclimbers-legal/POLICY` and `.../TERMS`. Re-copy the two files after every edit here (the root copies in this repo stay the source of truth). |
| B. This repo, made public | Only if the whole game source may be public | Settings > Pages > Deploy from branch `main` / `(root)`. URLs: `https://jpdash-creator.github.io/fluffy-climbers-sky-tower/POLICY` and `.../TERMS`. The `_config.yml` already excludes the Unity folders from the site. |

**The website in `docs/` (added 2026-11-10).** The designed site (`index.html`, `policy.html`, `terms.html`, `style.css`, `img/`, `fonts/`, `.nojekyll`) lives in the `docs/` folder, which is the only folder GitHub Pages can publish besides the root. Everything in `docs/` becomes public, so the private planning documents were moved out of the old `Docs/` folder into `Planning/` and must never go back into `docs/`. Settings > Pages > Deploy from branch `main` / `/docs` (this needs a public repository, or a paid plan). The pages are built from the two markdown files: `python Tools/legal_html.py` (policy and terms) and `python Tools/site_assets.py` (images and fonts from the game art). `index.html` and `style.css` are hand-designed. URLs: `https://jpdash-creator.github.io/<repo>/policy.html` and `.../terms.html` (lowercase, because GitHub Pages is case sensitive). **The app already uses these** (changed 2026-11-10): `legal_config.json` and the defaults in `LegalLinks.cs` point to `https://jpdash-creator.github.io/fluffy-climbers-sky-tower/policy.html` and `.../terms.html`. If the repository is renamed, or the site moves to another repo or a custom domain, change those two URLs in both places and rebuild.

Open each URL in a private browser window before entering it anywhere.

**The app opens these URLs itself.** Settings has Privacy and Terms buttons (added 2026-11-09, `Gameplay/LegalLinks.cs`, `MetaController.Settings.cs`) that read `Assets/Resources/Services/legal_config.json` (`privacyUrl`, `termsUrl`, `supportEmail`). The file ships with the Option A URLs; if you choose Option B, change both URLs there before the build. Only https URLs are accepted; a missing or malformed value falls back to the Option A defaults compiled into `LegalLinks`, so the buttons always work.

## 2. Store console entries that depend on these documents

| Where | Entry | Source |
|---|---|---|
| Play Console > Store listing | Privacy policy URL | the POLICY URL above |
| Play Console > App content > Data safety | Collected and shared data | Step-by-step answers in `Planning/Game Ideas/fluffy-climbers-sky-tower/docs/49-play-data-safety-form.md`, mirrored by `POLICY.md` section 18 (2026-11-12: plus the leaderboard and Firebase answers, doc 49 last section: User IDs incl. Firebase uid, Name, Photos, App interactions incl. scores and run inputs): nine types (approximate location; user IDs; purchase history; app interactions; other actions = cloud save; crash logs; diagnostics; other app performance data; device or other IDs). Shared only where AdMob is the source; ad-SDK types are required (the SDK starts with the app), sign-in and shop types optional. Encrypted in transit yes; deletion request yes; account creation no. Payment data is handled by Google, not collected by the app. |
| Play Console > App content > Ads | "Contains ads": yes (rewarded only) | `POLICY.md` section 6 |
| Play Console > App content > Advertising ID | Uses the advertising ID: yes (ads, fraud prevention) | the Google Mobile Ads SDK needs `AD_ID` (confirm it is declared in the built manifest) |
| Play Console > App content > Target audience | 13 and over; **not** designed for children | `POLICY.md` section 15, `TERMS.md` section 3, docs/13 row 12 |
| Play Console > App content > Content rating | In-app purchases and ads: yes; randomised paid items (chests): yes | `TERMS.md` section 5 |
| App Store Connect (later) | Privacy labels, EULA link (custom EULA = TERMS URL), odds disclosure | `POLICY.md` section 18, `TERMS.md` sections 5 and 24 |

## 3. Decisions and code gaps the documents assume (owner)

1. **Audience.** The documents say "not directed to children under 13, minimum age 13, parents consent for under-18s" (the Play Console plan in docs/13 row 12). `PRODUCT.md` says "casual players of all ages". If the Play target audience is ever set to include under-13s, the Families policy applies (certified ad SDKs, no personalised ads, no AD_ID) and both documents must be rewritten first.
2. **In-app links: done 2026-11-09.** Google Play's User Data policy requires the privacy policy to be linked **inside the app** as well as on the listing. Settings now has Privacy and Terms buttons under Reset / Restore (docs/38); make sure `legal_config.json` holds the final URLs once hosting is chosen.
3. **Consent prompt.** `POLICY.md` section 6 states that Google's UMP consent form is shown before ads are requested where the law requires it (EEA, UK, Switzerland, US-state messages) and that Settings has a "Privacy choices" entry that reopens it. Verify in the build that the UMP flow runs before the first ad request and that the Privacy choices entry shows when UMP requires it.
4. **Chest odds in the UI.** `TERMS.md` section 5 says the in-game information panel shows contents and odds and prevails. The chest (i) popup must show the odds per rarity (docs/13 row 16) before release.
5. **Receipt validation.** `TERMS.md` section 6 says purchases are verified before granting. `UnityIapPaymentService.OnPending` still has the validation TODO (docs/33 section 7).
6. **Admin code.** Change `adminCode` in `Assets/Resources/Services/admin_config.json` from the documented default before a public build; the terms forbid players from using admin and test tools, which only helps if the code is secret.
7. **Publisher identity.** The documents name J. P. Dash, Bengaluru, Karnataka, India, with jpdash.creator@gmail.com, the same as the Fastline app. If the Play Console developer name is different (for example a "JD Labs" trade name), add it to section 1 of both documents.
8. **Ad networks.** Only Google AdMob (Google Mobile Ads SDK) is named. Adding any other network or mediation layer means a new row in `POLICY.md` sections 6, 7 and 18 before the build ships. Also confirm that no LevelPlay/ironSource SDK remains in the build.

9. **Leaderboard and Firebase (2026-11-12).** `POLICY.md` section 4a and `TERMS.md` section 12a describe the weekly leaderboard (Firebase Auth, Firestore and Cloud Run in Mumbai, public name, picture and scores, one-time consent popup). Before a build that contains the code is released: (a) enter the leaderboard rows of doc 49 in the Data safety form; (b) regenerate `docs/policy.html` and `docs/terms.html` (`python Tools/legal_html.py`) and publish them; (c) confirm the release manifest has the Firebase Analytics kill-switch flags and NO `BIND_GET_INSTALL_REFERRER_SERVICE` permission (`Planning/RELEASE-AAB-CHECKLIST.md`); (d) the service must be deployed with the same simulation, hero table and manifest as the build (`Planning/LEADERBOARD-DEPLOY-LOG.md`); (e) the in-app flag stays OFF until the owner switches it on.

## 4. Keeping them current

- Any change to data handling (new SDK, permission, analytics, server, notifications, ad network, product type, cloud feature, age gate) updates `POLICY.md` and/or `TERMS.md` and the "Last updated" date **before** the build ships, and is announced in the release notes when material.
- Keep `.claude/rules` and CLAUDE.md pointing at these files so that the rule is applied in every session.
