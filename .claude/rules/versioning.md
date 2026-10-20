# Build versioning: vMAJOR.MINOR.HOTFIX.BUILD (owner requirement, 2026-10-20)

Every build (APK) carries a semantic version and is told apart by it. The version file is `Tools/version.json`; `python Tools/version.py show` prints it.

| Part | Meaning | Who changes it |
|---|---|---|
| MAJOR | 0 = pre-release (not in a store). 1 = first public release. 2+ = a redesign of content or economy | by hand, owner decision |
| MINOR | A feature or content drop, or any change of game rules: a new screen or system, new regions, physics or economy tuning, the world map | `python Tools/version.py bump minor` (HOTFIX back to 0) |
| HOTFIX | Fix-only change on the same feature set: bug fixes, layout and art polish, text, no rule changes | `python Tools/version.py bump hotfix` |
| BUILD | Counter of produced APKs. Never reset, so a build number is unique for the whole project | automatic: `AndroidBuild.Build()` adds 1 before every build |

Start: v0.9.0.x = the five-region milestone candidate (pre-release). v1.0.0.x is the first store build.

How it is used
- Bump MINOR or HOTFIX BEFORE building when the change set of the next build needs it (new feature or rule change = minor, fixes only = hotfix). Several builds of the same change set only raise BUILD.
- The build writes: app version name `M.m.h.b` (`Application.version`, `PlayerSettings.bundleVersion`), Android version code = BUILD, the file `E:/Unity/builds/boingupskytower_v<M.m.h.b>.apk` (kept) and a copy `boingupskytower.apk` (latest, used by the device test kit), and one line in `Content/builds/build_log.csv`.
- The game shows the version on the main menu (bottom) and logs it in the first field of `BTSTATE`.
- Always tell the owner the version of the build you installed ("v0.9.0.7 is on your phone") and name the version in the report and in the commit message of a build ("Build v0.9.0.7: ...").
- Never ship or install an APK that is not produced by the build script (it would have no new version).
