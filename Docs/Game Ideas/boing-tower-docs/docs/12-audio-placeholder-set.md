# 12 - Placeholder audio set (T10)

Placeholder sound effects and one music loop, all CC0. Code: `Assets/_Game/Gameplay/AudioManager.cs`
(static access, created on first use, survives scene loads). Clips live in `Assets/Resources/Audio/{sfx,music}`
(loaded through Resources, so a missing clip is silent, never a crash). Import settings: `Assets/Editor/ProjectBootstrap/AudioImportPostprocessor.cs`
(Vorbis, SFX mono + decompress on load, music streamed, preload on; looping is set on the music AudioSource).
Total imported audio about 1.9 MB (music 1.77 MB, 14 SFX about 128 KB). Checked against `Assets/Resources/Audio` on 2026-10-20: the 14 files and the loop below are exactly the files present, and the `Sfx` enum in `AudioManager.cs` has the same 14 ids. Provenance and licences (all CC0, downloaded 2026-10-20 with the owner's approval): `Content/assets/audio_credits.md` in the game repo.

## Clips
| Sfx id | File (Resources/Audio/...) | Source pack | Original file | Triggered from |
| --- | --- | --- | --- | --- |
| Tap | sfx/tap.ogg | Interface Sounds | click_002.ogg | every UiKit.AddButton click |
| Jump | sfx/jump.ogg | Digital Audio | phaseJump1.ogg | RunView.DeriveAudio (upward kick) |
| Land | sfx/land.ogg | Impact Sounds | impactSoft_medium_000.ogg | RunView.DeriveAudio (grounded edge) |
| Bounce | sfx/bounce.ogg | Digital Audio | pepSound1.ogg | RunView.DeriveAudio (kick off a bounce platform) |
| Coin | sfx/coin.ogg | Interface Sounds | select_002.ogg | RunView.SpawnPickupEffects (fallback for any other pickup kind; since economy v2 no coins are picked up in a level, so it is rarely heard) |
| Gem | sfx/gem.ogg | Impact Sounds | impactGlass_light_000.ogg | RunView.SpawnPickupEffects (gem, hidden gem) |
| Star | sfx/star.ogg | Digital Audio | powerUp8.ogg | RunView.SpawnPickupEffects |
| Combo | sfx/combo.ogg | Digital Audio | phaserUp3.ogg | RunHud.Refresh (combo goes up, x2+) |
| SpeedUp | sfx/speedup.ogg | Digital Audio | zapTwoTone.ogg | RunHud.Refresh (SPEED UP text) |
| Fall | sfx/fall.ogg | Digital Audio | phaserDown2.ogg | RunView.DeriveAudio (dead, or hit by a hazard) |
| Win | sfx/win.ogg | Music Jingles (8-Bit) | jingles_NES00.ogg | RunHud.ShowResults |
| Fail | sfx/fail.ogg | Digital Audio | lowThreeTone.ogg | RunHud.ShowFailed |
| Back | sfx/back.ogg | Interface Sounds | back_002.ogg | popup close X (BuildPopupFrame) |
| Popup | sfx/popup.ogg | Interface Sounds | open_001.ogg | UiKit.BuildPopupFrame |
| Music | music/loop.ogg | OpenGameArt, by yd | Insistent.ogg | meta screens (volume 0.45) and runs (0.28) |

Clips were picked by name and category, not by ear: audition them in the editor and swap files freely (same file name = no code change).
To use a separate run track, drop `Resources/Audio/music/run.ogg`; without it the run reuses the loop quieter.

## Behaviour
- Settings: Sound effects and Music toggles apply at once (GameSettings setters call `AudioManager.Refresh()`); music off stops instantly, on fades in.
- Music cross-fades (0.8 s) between the meta and run beds; the same recording just changes level.
- 8 pooled AudioSources for SFX, no allocation per play; the same cue cannot retrigger within 40 ms.
- `AudioListener.pause` follows app pause and focus loss.
- The simulation does not know about audio: events are derived in the view layer from state changes. Every cue has a visual twin (squash and dust, pickup pops,
  COMBO text, SPEED UP text, result and failed popups); keep it that way (CUX-6).
- Not covered by a visual twin on its own: hazard hit (plays Fall). Check the hit flash is visible.
