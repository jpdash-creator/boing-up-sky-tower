# 32 Revive and ads (owner list 2026-11-02, item 4)

Supersedes the "5 then 10 gems, max 2 revives, one free ad" rule of doc 22 point 9.

## Schedule (per run, resets on every new run; code: `RevivePolicy`)

| Event in the run | Ad option | Gem option |
|---|---|---|
| 1st fall | AD 30s (placement `revive_30`) | 5 gems |
| after an ad revive | AD 1m (`revive_60`) | still 5 gems (an ad never makes gems dearer) |
| after a gem revive | next ad as above | 10 gems |
| gem revives after the first | | 10 gems, stays 10 (no cap) |
| after 2 ad revives | none (button hidden) | gem price as above |

Ads and gems are counted separately. Endless runs and wall-touch losses (GoalLost) have no revive. Charges happen only after the revive succeeded; a watched ad whose revive failed is kept (next tap revives for free).

## Fell / game over pop-up

Compact: title, result pills, progress bar, then ONE line with the ad button (`AD 30s` / `AD 1m`, play icon) left and the gem button (gem icon + `5` or `10`) right, then Retry / Shop / Home. When no ad is left or ready, the gem button is centred and wider. Retry stays one tap away and is never blocked. Buttons are 380 x max(130, 48 dp) reference px with a 20 px gap inside the 860 px panel (fits 9:16, 9:19.5, 9:21, 3:4); label text shrinks to fit at 130 percent text.

## Ad service

`AdService.ShowRewarded(string placement, Action<bool> onDone)` (Gameplay/Ads) forwards to the active `IRewardedAds` provider and records `AdService.LastPlacement`. The 30 s / 60 s length is a display and placement value; the ad network decides the real ad.

## Tests

`RevivePolicyTests` (schedule), `ReviveTests` and `GameplayTests` (session behaviour), `CriticalUxTests` run-failed screens (layout).
