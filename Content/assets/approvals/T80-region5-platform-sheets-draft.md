# T80 DRAFT: Region 5 (Bamboo Temple) new special platforms, sheet texts (3 + 2 + 2 kinds)

Nothing generated. Method as in T74: text2image, 3:4, six rows, no reference, three calls A, B, C. Region 5 kind list (owner asked for 7 unique incl. hazards): helpful Bamboo Flick, Lantern Lift, Koi Stone; hard Swing Plank, Pagoda Ridge; hazard Mallet Smash, Rolling Log. (Lantern Lift and Koi Stone replace Muffle Drum and Climbing Pole from docs/30, which overlap the shop power-ups Shield and Wall Stick.)

Substitutions in the T74 common prefix and suffix: region 'Bamboo Temple' (a misty temple garden with pagoda roofs, torii gates, bamboo and a koi pond); colours jade green, lacquer red, gold, paper-lantern orange, stone grey; suffix palette line "palette: jade green, lacquer red, gold, lantern orange, stone grey, bamboo green". Exceptions to the flat-top rule are stated per sheet.

SHEET A text (helpful): " Six rows in three groups of two rows. Group 1 (rows 1 and 2), Bamboo Flick, two poses of the same bent green bamboo stalk with a flat wooden seat on top, tied with a red cord: row 1 rest pose, the stalk bent down and loaded, 6 times as long as it is thick; row 2 sprung pose, the same stalk released and swung upright to the right so the seat is tilted slightly up, 6 times as long as it is thick, same left margin and same base line. Group 2 (rows 3 and 4), Lantern Lift: a red paper-lantern slab with golden ribs and gold tassels hanging under both ends, a warm glow inside, flat top; the first row is 6 times as long as it is thick and the second row 10 times. Group 3 (rows 5 and 6), Koi Stone: a flat grey river-stone slab with a jade moss patch and an orange-and-white koi carp pattern carved along its side; the first row is 8 times as long as it is thick and the second row 14 times."

SHEET B text (hard): " Six rows in two groups of three rows. Group 1 (rows 1 to 3), Swing Plank: a wooden plank slab hung from two thick rope loops that rise up and out of the slab (leave an empty band above rows 1 to 3 for the ropes), a red knot at each rope end; the three rows are 6 times, 9 times and 12 times as long as they are thick measured over the plank. Group 2 (rows 4 to 6), Pagoda Ridge: a glazed jade-green pagoda roof ridge slab, a flat narrow gold ridge cap on top in the middle with the roof tiles sloping down on both sides to a lower curved eave tip at each end; the tiles slope down from the ridge so only the gold ridge cap is flat; the three rows are 5 times, 7 times and 9 times as long as they are thick measured at the eaves."

SHEET C text (hazard): " Six rows in two groups. Group 1 (rows 1 to 4), Mallet Smash, two poses of the same temple platform with a big wooden mallet at its right end: a stone-and-bamboo slab with a red-banded wooden mallet on a hinged post; row 1 raised pose, the mallet held high above the slab, 9 times as long as it is thick measured over the slab; row 2 smashed pose, the mallet down on the slab end with small dust puffs, the same slab length; row 3 raised pose and row 4 smashed pose of the same platform 12 times as long as it is thick; raised and smashed rows keep the same flat bottom edge and the same left margin (leave an empty band above the raised rows). Group 2 (rows 5 and 6), Rolling Log: row 5 a long stone-and-bamboo track slab 10 times as long as it is thick with a small carved stop block at each end and nothing on it; row 6 the rolling log alone on the same scale, a round bamboo log about 1.2 slab-thicknesses tall and 3 slab-thicknesses long, striped bands, ends showing growth rings, standing on the same base line."

Per-row plan (for baking): A 1 Flick rest 6x, 2 Flick sprung 6x, 3 Lift 6x, 4 Lift 10x, 5 Koi 8x, 6 Koi 14x; B 1-3 Swing 6x/9x/12x, 4-6 Ridge 5x/7x/9x; C 1-2 Mallet 9x raised/smashed, 3-4 Mallet 12x raised/smashed, 5 Log track 10x, 6 the log object.

## Code bindings of the sheets (added with the region 5 code, 2026-11-04; the sheet texts above are unchanged)

The code is done and binds art by named slots (docs/30 section 15.2): `Assets/Art/Regions/region_005_bamboo_temple/tiles/<kind>_widths/platform_set.json` with `"kind"` = lower-case enum name (`bambooflick`, `lanternlift`, `koistone`, `swingplank`, `pagodaridge`, `malletsmash`, `rollinglog`), `"dir"` = pose number, `"width"` = walkable width in u (the view takes the nearest piece within 1 u, one uniform scale, nothing stretched). Until a folder exists the platform is the normal region platform tinted per kind and no cue is drawn.

| Sheet row | Kind and pose | Painted length (x thickness, 2 N for N u) | Game width (u) | `dir` | Notes for the bake |
|---|---|---|---|---|---|
| A1 | Bamboo Flick, rest | 6x | 3 (fixed) | -1 | art looks toward +1, the view mirrors to `Platform.Dir` |
| A2 | Bamboo Flick, sprung | 6x | 3 | 1 | same base line and left margin as A1 |
| A3 | Lantern Lift | 6x | 3 | 0 | swaying and rising are Sim motion of the whole piece |
| A4 | Lantern Lift | 10x | 5 | 0 | |
| A5 | Koi Stone | 8x | 5 (4 u piece, middle repeated to 5 u; the code never makes a Koi Stone under 5 u) | 0 | |
| A6 | Koi Stone | 14x | 7 | 0 | game widths are 5 and 7 u |
| B1 | Swing Plank | 6x | 3 | 0 | ropes are part of the piece (empty band above the plank) |
| B2 | Swing Plank | 9x | 4.5 | 0 | |
| B3 | Swing Plank | 12x | 6 | 0 | |
| B4 | Pagoda Ridge | 5x | 2.5 | 0 | flat gold ridge cap is the walkable middle (1.0 u) |
| B5 | Pagoda Ridge | 7x | 3.5 | 0 | |
| B6 | Pagoda Ridge | 9x | 4.5 | 0 | |
| C1 | Mallet Smash, raised | 9x | 4.5 | -1 | mallet at the RIGHT end in the art; the view mirrors to `Platform.Dir` (mallet stands at the end toward the nearest wall) |
| C2 | Mallet Smash, smashed | 9x | 4.5 | 1 | same base line |
| C3 | Mallet Smash, raised | 12x | 6 | -1 | |
| C4 | Mallet Smash, smashed | 12x | 6 | 1 | |
| C5 | Rolling Log track | 10x | 5 (fixed) | 0 | nothing on it; the log travels 3.8 u of it |
| C6 | the rolling log alone | about 3 thick x 1.2 thick | log 1.2 u wide | n/a | `platform_set.json` of `rollinglog` gets `"log": {"file": "log.png"}`; drawn with its bottom on the walking surface at the track's scale |

Pose rules: Flick is sprung for 30 ticks after it flicked the player, else rest. Mallet is smashed from tick 60 to 109 of its 150-tick cycle (the smash itself is ticks 60 to 69), else raised. Both poses are state, so reduced motion shows them too.
