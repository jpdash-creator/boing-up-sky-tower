# Build versioning: vMAJOR.MINOR.BUILD (owner requirement 2026-10-20, simplified 2026-11-09 from vMAJOR.MINOR.HOTFIX.BUILD)

Every build (APK) carries a version and is told apart by it. The version file is `Tools/version.json` (keys `major`, `minor`, `build`); `python Tools/version.py show` prints it. Example: v0.52.67 is the last build of the old scheme (was v0.52.0.67), the next build is v0.52.68.

| Part | Meaning | Who changes it |
|---|---|---|
| MAJOR | 0 = pre-release (not in a store). 1 = first public release. 2+ = a redesign of content or economy | by hand, owner decision (`python Tools/version.py bump major`, MINOR back to 0) |
| MINOR | The feature step: a feature or content drop, or any change of game rules | `python Tools/version.py bump minor` |
| BUILD | Running counter of produced APKs. Always counts up, never reset (also not by a MAJOR or MINOR bump), so a build number is unique for the whole project | automatic: `AndroidBuild.Build()` adds 1 before every build |

The old HOTFIX part is gone. Fix-only builds just raise BUILD.

How it is used
- Bump MINOR BEFORE building when the change set of the next build is a new feature or rule change. Several builds of the same change set only raise BUILD.
- The build writes: app version name `M.m.b` (`Application.version`, `PlayerSettings.bundleVersion`, a valid Android versionName), Android version code = BUILD (always increasing), the file `E:/Unity/builds/boingupskytower_v<M.m.b>.apk` (kept) and a copy `boingupskytower.apk` (latest, used by the device test kit), and one line in `Content/builds/build_log.csv` (rows before v0.52.67 keep the old 4-part format).
- The game shows the version on the main menu (bottom) and logs it in the first field of `BTSTATE`.
- Always tell the owner the version of the build you installed ("v0.52.68 is on your phone") and name the version in the report and in the commit message of a build ("Build v0.52.68: ...").
- Never ship or install an APK that is not produced by the build script (it would have no new version).
