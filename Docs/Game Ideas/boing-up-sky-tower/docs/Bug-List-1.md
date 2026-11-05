# Bug List 1

Owner list received 2026-11-03 (after build v0.45.0.53). Written down first, then worked. Status column is updated as items land.
Item ids: B1.x = Home, B2.x = Settings, B3.x = Settings Admin, B4.x = Shop, B5.x = World Map.

## 1. Home Screen
| Id | Request | Status |
|---|---|---|
| B1.a | Remove Heroes, Daily Rewards and Shop from the Home screen. Only the Play button stays. Remove the "Play" text from the Play button. The play icon is centred vertically and horizontally and the Play button extends end to end. | Done (code and tests written, not yet run in Unity) |
| B1.b | Settings moves to just above the Play button: a blue button, similar size to the Shop button, in the place of Daily Rewards. | Done (code and tests written, not yet run in Unity) |
| B1.c | Move the Google sign-in ("G" button). Google sign-up is done only in Settings. After the player has connected nothing / after a first test run, also show a pop-up asking the user to connect a Google account; they can do it in Settings as well. | Done (code and tests written, not yet run in Unity) |
| B1.d | Remove all coin, gem and star counters from the Home screen. | Done (code and tests written, not yet run in Unity) |
| B1.e | "Now the home screen is good." (acceptance line) | n/a |

## 2. Settings
| Id | Request | Status |
|---|---|---|
| B2.a | Redo Settings to look like the reference image (SS 21), but only with Google Play integration, nothing else from the reference. Allow saving progress: signed in with Google Play = saves to Google, guest = saves locally on the phone. | Done 2026-11-03 (compact popup per SS 21, Google Play only; Save Progress: guest = phone, Google = Saved Games; doc 38) |
| B2.b | The current Settings UI needs improvement; use SS 21 as the reference. | Done 2026-11-03 (icon cells with green switches, big buttons, doc 38) |

## 3. Settings Admin
| Id | Request | Status |
|---|---|---|
| B3.a | Structure the admin pop-up with System Test at the top. System Test OFF = PROD ready. | Done 2026-11-03 (master switch + PROD READY / TEST MODE pill; OFF clears all test switches) |
| B3.b | Structure the Jump test as GAME TEST (OFF / ON). Then Shop Test, Character Test, etc. | Done 2026-11-03 (Game Test, Shop Test, Character Test, Cloud Test; doc 38) |
| B3.c | The height of Settings and Settings Admin is too much: make both sleek; see if OpenArt icons are needed. | Done 2026-11-03 (both compact and content-sized; existing icons are enough, no OpenArt spent) |

## 4. Shop
| Id | Request | Status |
|---|---|---|
| B4.a.i | Bundles cost only between 200 and 600 gems. | Done in code 2026-11-03 (docs/37; Unity audit pending) |
| B4.a.ii | Worth stays between 2.0x and 3.5x the gems paid. No dollars anywhere (remove). Worth shows at the top right of the card. | Done in code 2026-11-03 (docs/37; Unity audit pending) |
| B4.a.iii | Regenerate bundles with coins, power-ups and a character; the character can only come from open regions. Uncommon 100 percent chance, Rare 50 percent, Epic 25 percent. | Done in code 2026-11-03 (docs/37; Unity audit pending) |
| B4.a.iv | Delete the existing bundles and regenerate 2,000 fresh bundles (names in the same decent style, about 2,000 new names), gems price 200 to 600 only, worth 2x to 3.5x. | Done in code 2026-11-03 (docs/37; Unity audit pending) |
| B4.a.v | The hero card shows an icon and "?" only, no words. | Done in code 2026-11-03 (docs/37; Unity audit pending) |
| B4.a.vi | Like power-ups, bundles get an "i" icon at the top left explaining what is in this bundle, specific to the bundle: chance of the character, the make-up of the contents, how many times worth it is versus the gems paid. | Done in code 2026-11-03 (docs/37; Unity audit pending) |
| B4.b | Crown page: remove the "VIP" header. The yellow banner reads "Subscription"; then a similar yellow banner "Chests", centred like the VIP one, with a slight gap. | Done (code; not yet run in Unity) |
| B4.c.i | Heroes and Power-ups merge into a single bottom icon (new OpenArt icon with Momo and a power-up together). | Placeholder done (ShopIcons.CharacterPower, code-composited); final OpenArt PNG pending approval |
| B4.c.ii | Heroes yellow banner on top, with a small gap below it the "Heroes coming soon" card. | Done (code; not yet run in Unity) |
| B4.c.iii | Under that the Power-ups yellow banner; the power-ups move onto this single page. | Done (code; not yet run in Unity) |
| B4.c.iv | The single power-up bottom icon goes away. | Done (code; not yet run in Unity) |
| B4.d.i | Gems and Coins merge into a single bottom icon (new OpenArt icon with a gem and a coin together). | Placeholder done (ShopIcons.GemsCoins, code-composited); final OpenArt PNG pending approval |
| B4.d.ii | Gems with its yellow banner on top; after a gap Coins with the coins yellow banner and the coin items follow. | Done (code; not yet run in Unity) |
| B4.d.iii | The single coin bottom icon goes away. | Done (code; not yet run in Unity) |
| B4.e | Shop now has 4 bottom icons: Bundles, Crown, Character / Power-ups, Gems / Coins; wider and evenly placed. | Done (code; not yet run in Unity) |

## 5. World Map
| Id | Request | Status |
|---|---|---|
| B5.a | Move Sunny Beach near the beach and water so the text has room. | Done (code, tests not yet run in Unity): Moved to marker x 0.60, y 0.56 (tag right) on the sand above the sea; Meadow-side pin 2 lowered 0.368 to 0.34 to make room. |
| B5.b | Region name tags with the black background: increase transparency by 30 percent. | Done (code, tests not yet run in Unity): Tag backing alpha 0.95 to 0.665 (TagKit.MapBackingAlpha); locked tags darker; audit checks 4.5:1 over white. |
| B5.c | Regions 6 to 30: replace the real name with the placeholder text "Coming Soon". | Done (code, tests not yet run in Unity): MetaController.MapLabel: regions 6+ read "Coming Soon" (tags and a11y), data untouched. |
| B5.d | The crown is now in the Shop: remove the Crown button. The bottom bar is Shop, Character (new Character / Power-up icon), Daily Rewards; all equal width, evenly spaced. | Done (code, tests not yet run in Unity): Map/Level bar = Shop | Character | Daily Rewards (red dot), crown and floating Rewards button removed; Heroes bar Shop | PLAY | Character. |

## Notes
- Bundle prices and worth replace the 540 to 3,000 gem pool from Issue 34 (bundle doc: docs/37-bundles.md).
- Epic is the code name for the tier called Elite in the UI.
- OpenArt icons (2 bottom icons, maybe settings/admin icons) need the usual approval packet before any credits are spent.
