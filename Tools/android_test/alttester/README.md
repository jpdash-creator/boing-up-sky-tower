# AltTester tests for Boing Up: Sky Tower (roadmap T26)

AltTester drives the real game objects (by name) on the phone: find a button, tap it, read its text, swipe the map. It is an addition to
`Tools/android_test` (`bt.py`, `smoke_test.py`), not a replacement.

## How it works (from the installed SDK 2.3.2 source)

- The SDK is the Unity package `com.alttester.sdk` (OpenUPM scoped registry in `Packages/manifest.json`). The in-game side is the
  `AltTesterPrefab` (an `AltRunner` plus the small connection dialog `AltDialog`, input mock `NewInputSystem`).
- The game is the **client**: `AltDialog` connects out to `host:port` (default `127.0.0.1:13000`) of the **AltTester Desktop** server
  (the "server" the Python driver also connects to, `AltDriver(host, port, app_name)`). Desktop pairs the driver and the app by app name.
- On the phone `127.0.0.1` is the phone itself, so the harness runs `adb reverse tcp:13000 tcp:13000`: phone port 13000 goes to the PC,
  where Desktop listens. (This is the "reverse port forward"; the SDK also has `AltReversePortForwarding` for the same job when the
  Editor starts the run. We use plain adb.)
- The runner only works when the scripting symbol `ALTTESTER` is defined and the build is a Development build (`RunOnlyInDebugMode`).
  Without the symbol the runner logs an error and destroys itself.
- Editor-only parts: `Editor/Scripts/AltTesterEditor/*` (the AltTester window, `AltBuilder`, which inserts the prefab into the first scene,
  saves the scene and sets the define, plus the prefab generator). None of it ships in a player. The Runtime scripts (`AltRunner`, `AltDialog`,
  commands) compile into every player, but nothing instantiates them unless the prefab is in a scene.
- Our build hook does **not** use `AltBuilder` (it saves the scene file and edits player settings). `AltTesterBuildHook` defines `ALTTESTER` for
  that one build with `BuildPlayerOptions.extraScriptingDefines` and adds the prefab to the Boot scene in memory (`IProcessSceneWithReport`),
  so the scene files and project settings stay untouched. Port 13000, host 127.0.0.1, app name `BoingUpSkyTower`, popup hidden.

## Steps for the owner

1. **Build the alttester APK.** Close nothing else; Unity must not be running another build. From a terminal in the project folder:

   ```bash
   BT_ALTTESTER=1 "<Unity 6000.3.26f1 editor>/Unity.exe" -batchmode -quit -projectPath . -executeMethod BoingUpSkyTower.Editor.Build.AndroidBuild.BuildFromCommandLine -logFile Logs/build_alttester.log
   ```

   (or add `-altTester` to the command line instead of the environment variable). Look for `[ALTTESTER] on:` and `[ALTTESTER] runner added to scene`
   in the log. Result: `E:/Unity/builds/boingupskytower_v<version>_alttester.apk` and a copy `E:/Unity/builds/boingupskytower_alttester.apk`.
   The clean `boingupskytower.apk` is not touched. The build number goes up as for any build (`.claude/rules/versioning.md`); tell the owner the version.
   A normal build (no variable, no flag) contains no AltTester runner.
2. **Install** (same package name as the normal build, so it replaces it; the next normal build replaces it back):

   ```bash
   adb install -r E:/Unity/builds/boingupskytower_alttester.apk
   ```
3. **Start AltTester Desktop** on the PC: `E:/tools/alttester_desktop/AltTesterDesktopPackageWindows__2_4_0/AltTesterDesktop__v2.4.0.exe`.
   Leave it on the default port 13000.
4. **Run** (phone connected, screen on and unlocked):

   ```bash
   python Tools/android_test/alttester/run_all.py --install
   ```

   `--install` installs the alttester APK first; leave it out when it is already installed. The script runs `adb reverse`, launches the game with cleared
   data, waits until the game shows up in Desktop, then runs the tests. In the Desktop window the game should appear as app `BoingUpSkyTower` before
   the driver connects. Options: `--no-launch` (use the game that is running), `--only <part of a test name>`.
5. Output is `PASS`, `FAIL` or `SKIP` per test and `RESULT: PASSED` or `FAILED` (exit code 1 on a failure). Screenshots of failures and a few
   milestones go to `E:/Unity/device_shots/alttester/`.

If the driver cannot connect: Desktop not running, `adb reverse --list` empty, the plain (not alttester) APK installed, or the phone screen off.

## What each test covers (`tests.py`)

| Test | Covers |
|---|---|
| `test_level_grid_has_15_nodes` | Menu, map, region 1: the objects `Level1` to `Level15` exist and there is no `Level16` |
| `test_endless_button_present` | The level screen has the `Endless` button, its label says Endless, it is on screen. SKIP when the object is missing |
| `test_settings_toggles_persist` | Flips the five settings toggles, leaves and re-enters Settings, checks the values stuck, then restores them |
| `test_all_levels_open_toggle` | Dev toggle 'Test: all levels open' ON: region 2 opens and its level 15 shows the level popup (`PLAY`) instead of 'Locked'. SKIP outside development builds |
| `test_map_scroll_to_far_region` | Swipes the world map until the highest region pin is on screen, taps it, level grid opens. SKIP if that region is locked |
| `test_region1_all_levels_autopilot` | LONG (only with `--long` or `--only region1`, up to 45 min). Writes `autoplay.json` so the game's own Autopilot plays region 1, levels 1 to 15; AltTester only observes the Run scene (pad objects) and takes a screenshot at every death; the results come from the `BTAUTO` logcat lines. FAIL when a level is not cleared. See the next section |
| `test_menu_map_play_fail` | Menu, map, region 1, level 1, PLAY, jump taps on `PadJump`, drift until the fail overlay (`Try again`, `Level select`), back to the map |

Object names come from the UI code (`MetaController.cs`, `RunHud.cs`, `ControlPad.cs`, `UiKit.AddButton`): a button's object name is the
name passed to `AddButton`; toggles are `<label>Toggle` with a child `Label` holding ON or OFF. If a name changes in the game, update `tests.py`.

## Why AltTester, and when to use bt.py instead

- AltTester reads and acts on the live object tree: exact names, enabled state, text, positions, component properties, waits with timeouts,
  swipes and multi-touch, scene queries. Tests are written like normal Python and are less flaky than tapping pixel coordinates.
- Use **AltTester** for feature and flow tests on a special build (menus, settings, progression, map, popups, anything you need to read from an object).
- Use **`bt.py` / `smoke_test.py`** for the checks that must run on the *shipping-like* dev build: no extra SDK, screenshots and BTUI layout rules
  (inside screen, touch targets, overlap), frame time and `BTSTATE` numbers, crash detection from logcat. It needs nothing on the PC but adb.
- Both use the same phone. They do not run at the same time; the alttester build is a separate APK, a normal build replaces it.
- The alttester build is development-only and never a release build; the hook refuses non-development builds.

## Not verified yet

Written without Unity or a device: the C# hook is not compiled, the Python tests have not run against the game. Expect to adjust waits and a few
names on the first real run (for example whether the long-press pads respond to AltTester taps, and how far one swipe moves the map).

## Region 1 as a real player

The game contains a development-only **Autopilot** (`Assets/_Game/Gameplay/Autopilot*.cs`). It plays through the same input path as the on-screen
pad (one input word per simulation tick), plans each hop with the real simulation (like the LevelBot) and can imitate a human: `skill` 0 is a perfect
player, 1 has a 150 ms reaction lag and timing jitter. A fall is a result, nothing is restarted. It only exists in development builds on a device
(`GameSettings.DevBuild`); the player's save is never touched (the suite plays on an in-memory save and the real save is reloaded at the end).

For every level the game logs one line `BTAUTO {json}` to logcat and appends a row to `autoplay_results.csv` in its `persistentDataPath`:
version, level, difficulty and step-table value, skill, result (`cleared`, `fell`, `timeout`), floors reached, time, tick of death, row, X and Y,
ground platform and the platform ids and kinds within 3 units, scroll speed percent at death, kill-zone gap, number of platforms in the 3 unit window
and the `dense` flag (6 or more, the 'stacked platforms' case). At the end it writes `BTAUTO done` and deletes `autoplay.json`.

### Option A: adb only (no AltTester Desktop, no alttester APK)

1. Install a **normal development build** (the usual Android build; `Debug.isDebugBuild` must be true). Phone connected, screen on and unlocked.
2. Run:

   ```bash
   python Tools/android_test/autoplay_r1.py --skill 0.5
   ```

   Useful options: `--skill 0` (perfect player: a fall then points at an unfair level), `--levels 8` or `--levels 1-15`, `--difficulty 0..4`,
   `--seeds 3` (three runs per level with different jitter), `--speed 2` (game speed, 1 is real time), `--stop-on-fail`, `--install`
   (installs `E:/Unity/builds/boingupskytower.apk` first), `--where internal|external|both` (where `autoplay.json` is written; `auto` is right on most phones).
3. The script pushes `autoplay.json` (run-as for the internal files folder, adb push for `/sdcard/Android/data/com.jdlabs.boingupskytower/files`), launches the
   game, follows the `BTAUTO` lines, prints a PASS/FAIL table, takes a screenshot at every fall and writes the report. Do not touch the phone while it plays;
   the whole region takes 10 to 20 minutes in real time.
4. Results: `E:/Unity/qa/autoplay_report.md`, `E:/Unity/qa/autoplay_results_<run>.csv` (copy of the file on the phone),
   screenshots `E:/Unity/device_shots/autoplay_<level>_<n>.png` (level number, attempt). Exit code 0 only when every run was cleared.

### Option B: through AltTester (needs the Desktop server and the alttester APK)

1. Build and install the alttester APK and start AltTester Desktop exactly as in 'Steps for the owner' above (Desktop must be running on port 13000).
2. Run the long test on its own (it is not part of the normal run):

   ```bash
   python Tools/android_test/alttester/run_all.py --install --only region1
   ```

   Environment variables: `AUTOPLAY_SKILL` (0.5), `AUTOPLAY_LEVELS` (`1-15`), `AUTOPLAY_DIFFICULTY` (0), `AUTOPLAY_SEEDS` (1), `AUTOPLAY_MINUTES` (45).
   `--skip-if-offline` makes the script print SKIP and exit 0 when Desktop or the alttester build cannot be reached.
3. The test writes the flag file while the menu is up (the game polls for it once a second), then observes with AltTester: the Run scene and its pad
   objects must appear; at every death it saves `E:/Unity/device_shots/alttester/autopilot_<level>_<n>_death.png`. Report: `E:/Unity/qa/autoplay_report_alttester.md`.
   The same test also needs adb to read logcat, which AltTester does not replace.

### Reading the result

- A level that falls at skill 0 means that a perfect player, which searches the real simulation, found no safe line: look at `dense`, `nk` (platform kinds
  near the death) and the screenshot; that is the signal for an unfair or broken spot (for example the stacked platforms).
- A fall only at higher skill is the reaction lag: compare the same level at skill 0 and 0.5.
- `timeout` means the run neither ended nor made progress for 90 s (or lasted 300 s of game time).

### Not verified yet

Written without Unity, a compiler or a device. The planner (`AutopilotPlanner`) is new code modelled on `LevelBot`, so the first run may show levels that
the LevelBot proves solvable but the autopilot does not clear at skill 0 (goals such as FindHiddenGem and ComboChain are only roughly handled). Check
first that `lvl_001_01` is cleared at skill 0 (EditMode test `PerfectAutopilotClearsRegionOneLevelOne`, PlayMode test `AutopilotClearsRegionOneLevelOneAtSkillZero`).
