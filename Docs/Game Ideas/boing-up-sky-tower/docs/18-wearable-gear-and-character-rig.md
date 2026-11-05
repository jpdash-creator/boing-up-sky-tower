> **SUPERSEDED by doc 20; gear removed (owner 2026-10-24 and 2026-11-01).** The wearable-gear approach (mannequin layers, gear fitting, gear art run) was dropped with all gear. Kept only as a short historical note.

# 18. Wearable gear and the character rig (history, owner issue list 21, 2026-10-24)

The document described a shared template (T1) and a parts template (P1) so that gear could be worn by every character. Gear no longer exists, so all gear sections (mannequin layers, gear fit, the 240-piece art run, the gear test batch, gear drawing in the run and on the character screen) are removed.

What survives from it, because characters still use it (see docs 19 and 20):

- Every character is painted on the same **1365 x 1649** canvas as Momo.
- A character is a set of separate **part pictures** (transparent PNG, full canvas) plus a `rig.json` with the draw `order`, back to front. Momo has nine parts; the Sporeling uses the polygon parts.
- A new character is one set of part pictures; no other change is needed.
- In the run, the selected character is drawn from its parts at the player sprite's own bounds, so the run, the home screen and the character screen show the same picture.
