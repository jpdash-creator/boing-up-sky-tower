# Pause state (2026-11-06, laptop restart)

Stopped on request: full sim suite (simout57, no result, must be re-run from scratch) and the Momo Rare/Elite/Legendary integration agent (killed mid-work, so the working tree may hold half-finished edits: check git status, then compile and run EditMode before anything else). No OpenArt jobs running.

## Where things stand
- Repo E:\Code\boing-up-sky-tower, branch main, last push commit 22cbeac (v0.49.0.60, on phone 96355505).
- Bug Lists 1-4 done and shipped. Bug List 5 (docs/Bug-List-5.md): F1 Rewards, F2 Hero screens, F3 Chests and endless past floor 7,000 are done in code (EditMode green, layout audit clean) but NOT yet built, pushed or installed. F4 new heroes in progress.
- OpenArt balance: 3,428 credits.

## F4 art status
- Momo Rare (momo_r1), Elite (momo_e3), Legendary (momo_l1) picked, in Art/_candidates. Integration into character data was in progress (may be partial).
- Bumble the bee (a MAGE, no wings on any hero): Common B-3 (bumble_c3.png) and Uncommon U-1 (bumble_u1.png, logged T87) picked.
- NEXT: Bumble Rare (mage, pointed violet hat, two orbs), prompt drafted; owner has NOT approved it. Ask again with the full packet (T88, 30 credits for 2 images, reference = bumble_u1.png uploaded) and wait for a clear yes.
- Then Bumble Elite, Legendary, then Ribbi, Dandy, Pip (Common first), then Regions 2-5.

## After restart
1. git status, compile, EditMode (Tools/unity_tests.ps1 -Platform EditMode), finish the Momo tiers integration.
2. Re-run the full sim suite alone (dotnet test -o E:\Unity\logs\simoutNN; ~70-95 min), then version bump to v0.50.0, APK build alone (not alongside the sim suite), adb install, commit and push with the Co-Authored-By trailer.
3. Unity may need a Unity Hub sign-in after restart. Phone may need reconnecting.
4. Open owner questions: chest coin inflation (about 2x/3x), test-mode Commons for locked regions, hero buy at max copies still charges, hard stop at exactly 100,000 floors, admin code 0000-TEST before release, LevelPlay / Play Games / Play Console accounts.
5. The recurring 3-minute status prompt (one table Task | Status | Est. minutes left + one line) continues.
