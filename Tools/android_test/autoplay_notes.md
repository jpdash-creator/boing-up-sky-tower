# Autoplay and device notes: stacked platforms (owner report 2026-10-20)

## Where the stack was

- **Region 1, level 11 (`lvl_001_11`, Combo Chain 6x1), platform rows 18 to 25 (height about 67 to 71 u, roughly 10 to 25 s into the run).** A column of 8 wide platforms with only 0.6 u between them (the old combo ladder). This is the "8 to 10 platforms stacked" the owner saw. Level 5 (`lvl_001_05`, Combo Chain 3x2) has the same ladder at rows 18 to 25 with 1.2 u rungs. Regions 2 to 5 have the same ladders in their levels 5 and 11.
- Dense patches (small platforms, up to 4 on one row, some crumbling) reached 5 platforms per 3 u window on lvl_001_04, 06 and 15.
- Touching a platform never kills by itself (one-way platforms, no side or underside collision). The game over comes from the rising kill line: at Insane (+100 percent per 5 s) the screen is already faster than any climb after about 20 to 30 s, and a ladder or dense patch slows the climb at that moment.

## How to recognise it on the phone

- Before the fix: a tall column of 8 platforms one above the other, nearly touching (lvl_001_11) around floor 18, all at the same x.
- After the fix (build with the stack rule): ladders of 4 or more floors alternate left/right in two narrow columns, never more than 4 platforms in one screen-height third; the dense patches are thinner; the speed-up stops after 30 s ("SPEED UP" no longer appears after +600 percent) and the screen never scrolls faster than 4 u/s.

## Autoplay hints (`autoplay_r1.py`)

- Check `lvl_001_11` first, then `lvl_001_05`, `lvl_001_04`, `lvl_001_06`, `lvl_001_15`.
- Use the Insane difficulty to reproduce; Relaxed (+20 percent per 5 s) survives much longer.
- Expected result with the fix: no stack above 4 platforms in a 3 u window (`Tests/Simulation/StackRuleTests.cs`), and the bot survives all 15 region 1 levels at Insane (`BotSurvivesRegion1AtTheHardestStepTable`).
