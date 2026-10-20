"""AltTester tests for Boing Up: Sky Tower (roadmap T26). Each test is a function test_*(g) taking the Game helper; run_all.py runs them in order.
Object names come from the UI code: Play, Settings, Back, Region<n>, Level<n>, LevelGrid, Endless, PLAY (level popup), Close, Pad*,
Try again / Level select (run overlays), <label>Toggle (settings) with a child 'Label' holding ON or OFF."""
import os
import sys
import time

from altbt import TestFailure, Skip, By
from alttester.exceptions import NotFoundException

TOGGLES = ["Sound effectsToggle", "MusicToggle", "HapticsToggle", "Reduced motionToggle", "Left-handedToggle"]
DEV_TOGGLE = "Test: all levels openToggle"


def open_settings(g):
    g.to_menu()
    g.tap("Settings")
    g.find(TOGGLES[0])
    time.sleep(0.8)


def open_map(g):
    g.to_menu()
    g.tap("Play")
    g.find("Region1", 15)
    time.sleep(1.0)


def open_region(g, n):
    open_map(g)
    g.tap("Region%d" % n)
    g.find("LevelGrid", 10)
    time.sleep(1.0)


def toggle_state(g, name):
    return g.text_of(name)


def test_menu_map_play_fail(g):
    """Menu -> map -> region 1 -> level 1 -> PLAY -> jump taps -> fail overlay."""
    open_region(g, 1)
    g.tap("Level1")
    g.tap("PLAY")
    g.wait_scene("Run", 30)
    g.find("PadJump", 20)
    time.sleep(3.0)                                   # the intro camera move
    jump = g.find("PadJump")
    for _ in range(6):
        jump.tap()
        time.sleep(0.5)
    # From here only sideways taps: the ball leaves the platforms and falls, so the failed overlay must appear.
    deadline = time.time() + 90
    while time.time() < deadline:
        if g.exists("Try again"):
            break
        for pad in ("PadRight", "PadLeft"):
            try:
                g.d.find_object(By.NAME, pad).tap()
            except NotFoundException:
                pass
        time.sleep(1.0)
    else:
        g.screenshot("fail_no_overlay")
        raise TestFailure("no end overlay (Try again) within 90 s of play")
    g.screenshot("fail_overlay")
    if not g.exists("Level select"):
        raise TestFailure("fail overlay has no 'Level select' button")
    g.tap("Level select")
    g.wait_scene("Meta", 20)


def test_settings_toggles_persist(g):
    """Every settings toggle flips, and the new value is still there after leaving Settings and coming back."""
    open_settings(g)
    before = {n: toggle_state(g, n) for n in TOGGLES}
    for n in TOGGLES:
        g.tap(n)
    flipped = {n: toggle_state(g, n) for n in TOGGLES}
    for n in TOGGLES:
        if flipped[n] == before[n]:
            raise TestFailure("%s did not flip (%s)" % (n, before[n]))
    g.tap("Back")
    g.find("Play")
    open_settings(g)                                  # the screen is rebuilt from the saved settings
    try:
        for n in TOGGLES:
            now = toggle_state(g, n)
            if now != flipped[n]:
                raise TestFailure("%s did not persist: expected %s, got %s" % (n, flipped[n], now))
    finally:
        for n in TOGGLES:                             # put the settings back as they were
            if toggle_state(g, n) != before[n]:
                g.tap(n)
        g.tap("Back")


def test_map_scroll_to_far_region(g):
    """Swipe the world map until the highest region pin is on screen, tap it, and the level grid opens."""
    open_map(g)
    w, h = g.screen_size()
    target = None
    for n in (5, 4, 3, 2):                            # the highest pin that exists
        if g.exists("Region%d" % n):
            target = "Region%d" % n
            break
    if target is None:
        raise TestFailure("no Region pins besides Region1 on the map")
    for _ in range(25):
        obj = g.d.find_object(By.NAME, target)
        y = obj.mobileY
        if 0.2 * h < y < 0.8 * h and 0 < obj.x < w:
            break
        if y > h / 2:                                  # pin is lower than the middle: drag the map up
            g.d.swipe((int(w / 2), int(h * 0.72)), (int(w / 2), int(h * 0.34)), duration=0.35)
        else:
            g.d.swipe((int(w / 2), int(h * 0.34)), (int(w / 2), int(h * 0.72)), duration=0.35)
        time.sleep(0.6)
    else:
        g.screenshot("map_scroll_failed")
        raise TestFailure("%s did not come into view after 25 swipes" % target)
    g.screenshot("map_far_region")
    g.tap(target)
    if not g.exists("LevelGrid", 6):
        # A locked region shows a popup instead (no test toggle): not a failure of the scroll, but say so.
        if g.exists("Close", 2):
            g.tap("Close")
            raise Skip("%s is locked here; turn on 'Test: all levels open' to open it" % target)
        raise TestFailure("tapping %s opened nothing" % target)
    g.tap("Back")


def test_level_grid_has_15_nodes(g):
    """The level screen of region 1 shows exactly the 15 level circles Level1..Level15."""
    open_region(g, 1)
    missing = [i for i in range(1, 16) if not g.exists("Level%d" % i)]
    if missing:
        raise TestFailure("level circles missing: %s" % missing)
    if g.exists("Level16"):
        raise TestFailure("unexpected Level16 on the grid")
    g.tap("Back")


def test_all_levels_open_toggle(g):
    """'Test: all levels open' (development builds only) opens region 2: its level 15 opens the level popup instead of 'Locked'."""
    open_settings(g)
    if not g.exists(DEV_TOGGLE, 3):
        g.tap("Back")
        raise Skip("no 'Test: all levels open' toggle (not a development build)")
    was = toggle_state(g, DEV_TOGGLE)
    if was != "ON":
        g.tap(DEV_TOGGLE)
    g.tap("Back")
    try:
        open_region(g, 2)
        g.tap("Level15")
        if not g.exists("PLAY", 6):
            g.screenshot("all_open_failed")
            raise TestFailure("region 2 level 15 did not open its level popup with all levels open")
        g.tap("Close")
        g.tap("Back")
    finally:
        if was != "ON":
            open_settings(g)
            if toggle_state(g, DEV_TOGGLE) == "ON":
                g.tap(DEV_TOGGLE)
            g.tap("Back")


def test_endless_button_present(g):
    """The level screen has an Endless button (endless feature); skipped when the build does not have it."""
    open_region(g, 1)
    if not g.exists("Endless"):
        g.tap("Back")
        raise Skip("object 'Endless' is missing (build without the endless feature)")
    label = g.text_of("Endless")
    if "Endless" not in label:
        raise TestFailure("Endless button label is '%s'" % label)
    w, h = g.screen_size()
    obj = g.d.find_object(By.NAME, "Endless")
    if not (0 < obj.x < w and 0 < obj.mobileY < h):
        raise TestFailure("Endless button is off screen at %s,%s" % (obj.x, obj.mobileY))
    g.tap("Back")


def test_region1_all_levels_autopilot(g):
    """Region 1 as a real player: the game's own Autopilot plays levels 1..15 (it needs the autoplay.json flag file, written here), and this test
    only OBSERVES through AltTester (the Run scene and its pad objects exist while the autopilot plays) and takes a screenshot at every death.
    Results come from the BTAUTO lines in logcat. Settings through the environment: AUTOPLAY_SKILL (0.5), AUTOPLAY_LEVELS ('1-15'),
    AUTOPLAY_DIFFICULTY (0), AUTOPLAY_SEEDS (1), AUTOPLAY_MINUTES (45). SKIP outside development builds."""
    sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
    import autoplay_r1 as ap
    import json

    if not g.exists("Settings", 5):
        g.to_menu()
    opts = {"region": 1, "levels": ap.parse_levels(os.environ.get("AUTOPLAY_LEVELS", "1-15")), "skill": float(os.environ.get("AUTOPLAY_SKILL", "0.5")),
            "difficulty": int(os.environ.get("AUTOPLAY_DIFFICULTY", "0")), "stopOnFail": False, "seeds": int(os.environ.get("AUTOPLAY_SEEDS", "1")),
            "speed": 1.0}
    expected = len(opts["levels"]) * opts["seeds"]
    ap.remove_flags()
    log = ap.Follower()                     # logcat reader for the BTAUTO lines (adb only, runs next to AltTester)
    try:
        placed = ap.place_flag(json.dumps(opts), "both", os.path.join(ap.QA, "autoplay_flag_tmp.json"))
        if not placed:
            raise Skip("could not write autoplay.json (needs a development build with run-as or a writable external files folder)")
        # The game polls for the file while the menu is up and then loads the Run scene by itself.
        results, shots, saw_run, meta = [], {}, False, {}
        deadline = time.time() + 60 * float(os.environ.get("AUTOPLAY_MINUTES", "45"))
        started = time.time()
        while time.time() < deadline and not log.done:
            for ev in log.take_events():
                if ev.get("ev") == "start":
                    meta.update(version=ev.get("v"), run=ev.get("run"), step=ev.get("step"))
                elif ev.get("ev") == "error":
                    raise TestFailure("autopilot error: %s" % ev.get("msg"))
                elif ev.get("ev") == "level":
                    results.append(ev)
                    print("     %s %s %s floors %s/%s %.1fs" % (ap.verdict(ev), ev.get("lvl"), ev.get("res"), ev.get("floors"), ev.get("target"), ev.get("sec", 0)))
                    if ev.get("res") != "cleared":
                        name = "autopilot_%02d_%s_death" % (int(ev.get("n", 0)), ev.get("att", 1))
                        g.screenshot(name)  # the game keeps the death on screen for a few seconds
                        shots[(ev.get("n"), ev.get("att"))] = os.path.join(altbt_shots(), name + ".png")
            if g.scene() == "Run" and not saw_run:
                saw_run = g.exists("PadJump", 5) or g.exists("Pause", 1)
            if log.crash:
                raise TestFailure("crash in logcat: " + log.crash)
            if not results and not saw_run and time.time() - started > 120:
                raise TestFailure("the autopilot did not start within 120 s (is this a development build, is the flag in persistentDataPath?)")
            time.sleep(0.5)
        if not log.done:
            raise TestFailure("autoplay did not finish within the time limit; %d of %d runs reported" % (len(results), expected))
        if not saw_run:
            raise TestFailure("never saw the Run scene or its pad objects through AltTester while the autopilot played")
        ap.write_report(results, shots, opts, meta, os.path.join(ap.QA, "autoplay_report_alttester.md"))
        print(ap.table(results))
        failed = [r for r in results if ap.verdict(r) == "FAIL"]
        if len(results) < expected:
            raise TestFailure("only %d of %d runs reported" % (len(results), expected))
        if failed:
            raise TestFailure("%d run(s) not cleared: %s (report: E:/Unity/qa/autoplay_report_alttester.md)" % (
                len(failed), ", ".join("%s %s@%s" % (r.get("lvl"), r.get("res"), r.get("floors")) for r in failed)))
    finally:
        log.close()
        ap.remove_flags()


def altbt_shots():
    import altbt
    return altbt.SHOTS


test_region1_all_levels_autopilot.long = True   # run_all.py runs 'long' tests only with --only or --long


TESTS = [
    test_level_grid_has_15_nodes,
    test_endless_button_present,
    test_settings_toggles_persist,
    test_all_levels_open_toggle,
    test_map_scroll_to_far_region,
    test_menu_map_play_fail,
    test_region1_all_levels_autopilot,   # long (up to 45 min): run alone with --only region1
]
