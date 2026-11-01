"""Level sweep (stream T1): plays the levels of one region on the phone through AltTester with the game's own Autopilot, checks a
per-level checklist and writes a results table (markdown + JSON).

    python Tools/android_test/alttester/level_sweep.py --levels 1-15 --region 1 [--install] [--no-launch] [--skill 0.5] [--speed 1]
    python Tools/android_test/alttester/run_all.py   --levels 1-15 --region 1      (same thing, through run_all.py)

For every level the script (all through real UI objects, nothing is started behind the UI's back):
  1. opens the level screen of the region, taps the level circle (scrolling the grid when needed), reads the level popup, screenshot;
  2. checks the popup (goal text, the italic wall rule on level 7, no gem badge on combo levels), taps PLAY;
  3. starts the level twice (pause, Restart) and compares the layout hash of the two attempts (the random layout must differ);
  4. checks the run (selected character, power-up buttons, no gem pill on combo levels), attaches the Autopilot, watches the run
     (screenshot in the middle), waits for the result overlay (screenshot) and reads the result from the game (LevelSweepApi);
  5. leaves with 'Level select'.
The game side is Assets/_Game/Gameplay/LevelSweepApi.cs (static methods called with AltDriver.call_static_method). Needs a development build
with the ALTTESTER symbol (README.md); SYSTEM_TEST must be on (Autopilot) and GAME_JUMP_TEST on (all levels open): the script sets both.
Results are written after every level, so a crash keeps what was done; a new run for other levels is merged into the same files (--fresh to start over).
Exit code: 0 = every level PASS or SKIP and every checklist item ok, 1 = a FAIL, 2 = no device.
"""
import argparse
import datetime
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import altbt  # noqa: E402
from altbt import TestFailure, By  # noqa: E402

BRIDGE_TYPE = "BoingUpSkyTower.Gameplay.LevelSweepApi"
BRIDGE_ASM = "BoingUpSkyTower.Gameplay"
OUT_MD = "E:/Unity/logs/alttester_levels.md"
OUT_JSON = "E:/Unity/logs/alttester_levels.json"
POWER_NAMES = ["Flight", "Rocket", "DoubleJump", "WallStick", "Shield", "Magnet", "SlowTime"]   # PowerUpBar: object "Power<Type>" per owned type
RULE_LEVELS = (7,)            # doc 25: level 7 = no wall touch, the start popup shows the small italic line
GEM_LEVELS = (8, 15)          # doc 25: gems are placed along the route
SKIP_GOALS_DEFAULT = "SurviveWave,BossChase"   # goal types the Autopilot does not play (removed from the campaign by doc 25)
COMBO_GOAL = "ComboChain"
SERIAL_DEFAULT = "96355505"   # the owner's phone; an emulator may be attached too (adb reads ANDROID_SERIAL)


class Clock:
    """time.time / time.sleep behind an object, so the tests can run a whole level in zero real seconds."""

    def time(self):
        return time.time()

    def sleep(self, seconds):
        time.sleep(seconds)


# ---------------------------------------------------------------------------------------------- pure helpers (unit tested)

def parse_levels(text):
    levels = []
    for part in str(text).split(","):
        part = part.strip()
        if "-" in part:
            a, b = part.split("-", 1)
            levels.extend(range(int(a), int(b) + 1))
        elif part:
            levels.append(int(part))
    seen, out = set(), []
    for n in levels:
        if n not in seen:
            seen.add(n)
            out.append(n)
    return out


def unwrap(raw):
    """AltTester returns the JSON string of the method's return value; the bridge returns a JSON string, so it may be encoded twice."""
    value = raw
    for _ in range(3):
        if isinstance(value, bytes):
            value = value.decode("utf-8", "replace")
        if isinstance(value, str):
            text = value.strip()
            if not text:
                return {"ok": False, "err": "empty reply"}
            try:
                value = json.loads(text)
            except ValueError:
                return {"ok": False, "err": "unparsable reply: " + text[:120]}
        else:
            break
    return value if isinstance(value, dict) else {"ok": False, "err": "unexpected reply: %r" % (value,)}


def is_italic(value):
    """UnityEngine.FontStyle: Normal 0, Bold 1, Italic 2, BoldAndItalic 3 (AltTester may return the number or the name). None = unknown."""
    if value is None:
        return None
    if isinstance(value, str):
        v = value.strip().strip('"').lower()
        if v in ("italic", "boldanditalic", "2", "3"):
            return True
        if v in ("normal", "bold", "0", "1"):
            return False
        return None
    if isinstance(value, (int, float)):
        return int(value) in (2, 3)
    return None


def check(name, ok, detail="", na=False):
    """One checklist item: status 'ok', 'FAIL' or 'n/a'."""
    return {"name": name, "status": "n/a" if na else ("ok" if ok else "FAIL"), "detail": detail}


def evaluate_popup(n, pre, popup):
    """Checks on the level popup. pre = LevelPreview reply, popup = {'goal': text, 'rule': text or None, 'rule_italic': True/False/None, 'gem_badge': bool}."""
    out = []
    goal = (popup.get("goal") or "").strip()
    expected = pre.get("goalText", "")
    out.append(check("popup goal text", goal != "" and goal == expected, "popup '%s', game says '%s'" % (goal, expected)))
    rule = popup.get("rule")
    if n in RULE_LEVELS:
        out.append(check("popup italic wall rule", rule is not None and popup.get("rule_italic") is not False,
                         "rule '%s', italic=%s" % (rule, popup.get("rule_italic")) if rule is not None else "level %d must show the rule line, none found" % n))
    elif rule is not None:
        out.append(check("popup rule only on level 7", False, "unexpected rule line '%s' on level %d" % (rule, n)))
    elif pre.get("rule"):
        out.append(check("popup rule only on level 7", False, "the game has the rule '%s' for level %d but the popup shows none" % (pre.get("rule"), n)))
    else:
        out.append(check("popup rule only on level 7", True, "", na=True))
    if pre.get("goalType") == COMBO_GOAL:
        out.append(check("combo level: no gem badge", not popup.get("gem_badge"), "GemBadge on the level circle" if popup.get("gem_badge") else "",))
    if pre.get("rule") and rule is not None and rule != pre.get("rule"):
        out.append(check("popup rule text", False, "popup '%s', game says '%s'" % (rule, pre.get("rule"))))
    return out


def evaluate_run(n, pre, run, power_objects, gem_pill):
    """Checks on the running level (after the intro). power_objects = number of Power<Type> UI objects found, gem_pill = HUD object GemPill is on screen."""
    out = []
    out.append(check("run uses selected character", bool(run.get("character")) and run.get("character") == run.get("selected"),
                     "run '%s', selected '%s'" % (run.get("character"), run.get("selected"))))
    out.append(check("power-up buttons count", run.get("powerButtons") == run.get("powerOwned") == power_objects,
                     "owned %s, HUD buttons %s, UI objects %s" % (run.get("powerOwned"), run.get("powerButtons"), power_objects)))
    out.append(check("run goal matches popup", run.get("goalText") == pre.get("goalText") and run.get("target") == pre.get("target"),
                     "run '%s' (%s), popup '%s' (%s)" % (run.get("goalText"), run.get("target"), pre.get("goalText"), pre.get("target"))))
    if pre.get("goalType") == COMBO_GOAL:
        out.append(check("combo level: no gem pill / gems", not gem_pill and run.get("gemsPlaced", 0) == 0,
                         "GemPill on screen=%s, gems placed=%s" % (gem_pill, run.get("gemsPlaced"))))
    if n in GEM_LEVELS:
        out.append(check("gem level has gems", run.get("gemsPlaced", 0) > 0, "gems placed=%s" % run.get("gemsPlaced")))
    return out


def evaluate_layout(first, second):
    """Two attempts of the same level must have a different random layout (hash over all platforms) and a different attempt seed."""
    if first is None or second is None:
        return check("layout differs between attempts", True, "not compared", na=True)
    same_hash = first.get("layoutHash") == second.get("layoutHash")
    same_seed = first.get("attemptSeed") == second.get("attemptSeed")
    return check("layout differs between attempts", not same_hash and not same_seed,
                 "hash %s / %s, attempt seed %s / %s" % (first.get("layoutHash"), second.get("layoutHash"), first.get("attemptSeed"), second.get("attemptSeed")))


def verdict_for(row, time_limit):
    """PASS = the autopilot cleared the goal within the time limit. SKIP = the level type is not played. FAIL = anything else."""
    if row.get("result") == "skip":
        return "SKIP"
    if row.get("result") == "cleared" and row.get("time", 0) <= time_limit:
        return "PASS"
    return "FAIL"


def new_row(n, region):
    return {"level": n, "region": region, "id": "", "goal_type": "", "goal": "", "rows": 0, "target": 0, "platforms": 0, "special": 0,
            "kinds_layout": "", "kinds_seen": "", "result": "", "time": 0.0, "real_sec": 0.0, "gems": 0, "gems_placed": 0, "coins": 0, "coins_placed": 0,
            "stars": 0, "score": 0, "fail_reason": "", "layout": "", "layout_hash": "", "layout_hash2": "", "character": "", "power_buttons": 0,
            "checks": [], "shots": [], "verdict": "", "note": ""}


def check_summary(row):
    bad = [c for c in row.get("checks", []) if c.get("status") == "FAIL"]
    if not bad:
        return "ok"
    return "%d FAIL: %s" % (len(bad), "; ".join(c["name"] for c in bad))


def _cell(text):
    return str(text).replace("|", "/").replace("\n", " ")


def render_markdown(meta, rows):
    """The results table (docs-friendly markdown). rows are level rows as built by play_level, sorted by level."""
    rows = sorted(rows, key=lambda r: (r.get("region", 1), r.get("level", 0)))
    counts = {"PASS": 0, "FAIL": 0, "SKIP": 0}
    check_fails = 0
    for r in rows:
        counts[r.get("verdict", "FAIL")] = counts.get(r.get("verdict", "FAIL"), 0) + 1
        check_fails += sum(1 for c in r.get("checks", []) if c.get("status") == "FAIL")
    lines = ["# AltTester level sweep", "",
             "- Date: %s" % meta.get("date", ""),
             "- Build: %s (flags SYSTEM_TEST=%s, GAME_JUMP_TEST=%s)" % (meta.get("version", "?"), meta.get("system", "?"), meta.get("jump", "?")),
             "- Region %s, autopilot skill %s, depth %s, game speed %s, time limit %s s, character %s" % (
                 meta.get("region", "?"), meta.get("skill", "?"), meta.get("depth", "?"), meta.get("speed", "?"), meta.get("time_limit", "?"), meta.get("character", "selected")),
             "- Result: %d PASS, %d FAIL, %d SKIP, %d checklist item(s) failed" % (counts["PASS"], counts["FAIL"], counts["SKIP"], check_fails), "",
             "| Level | Goal | Rows | Platforms | Special seen | Result | Time (s) | Gems | Coins | Stars | Fail reason | Verdict | Checks |",
             "|---|---|---|---|---|---|---|---|---|---|---|---|---|"]
    for r in rows:
        special = r.get("kinds_seen") or "-"
        if r.get("kinds_layout"):
            special += " of " + r["kinds_layout"]
        lines.append("| %s | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s | **%s** | %s |" % (
            r["level"], _cell(r.get("goal", "")), "%s/%s" % (r.get("rows", 0), r.get("target", 0)) if r.get("result") != "skip" else "-",
            r.get("platforms") or "-", _cell(special), r.get("result", ""), "%.1f" % r.get("time", 0.0), "%s/%s" % (r.get("gems", 0), r.get("gems_placed", 0)),
            "%s/%s" % (r.get("coins", 0), r.get("coins_placed", 0)), r.get("stars", 0), _cell(r.get("fail_reason") or r.get("note") or "-"), r.get("verdict", ""),
            _cell(check_summary(r))))
    lines += ["", "Columns: Rows = rows climbed / target row; Platforms = platforms in the level; Special seen = platform kinds the player stood on (sampled twice a second) of the kinds placed "
              "(N normal, M moving, C crumble, B bounce, V vanishing, S sticky, L slippery, P bounce cap, W wind petal, H hive lift, D snap daisy, U dandelion, T thorn bush, G ladybug, E seesaw); "
              "Gems and Coins = taken/placed; Verdict PASS = cleared within the time limit.", ""]
    failed = [r for r in rows if any(c.get("status") == "FAIL" for c in r.get("checks", []))]
    if failed:
        lines += ["## Checklist failures", ""]
        for r in failed:
            for c in r["checks"]:
                if c["status"] == "FAIL":
                    lines.append("- Level %s: **%s** - %s" % (r["level"], c["name"], c.get("detail", "")))
        lines.append("")
    lines += ["## Layouts", "", "| Level | Layout of the played attempt | Hash attempt 1 | Hash attempt 2 | Character | Power-up buttons |", "|---|---|---|---|---|---|"]
    for r in rows:
        if r.get("result") == "skip":
            continue
        lines.append("| %s | %s | %s | %s | %s | %s |" % (r["level"], _cell(r.get("layout", "")), r.get("layout_hash", ""), r.get("layout_hash2", "") or "-",
                                                         r.get("character", ""), r.get("power_buttons", 0)))
    lines += ["", "Screenshots: E:/Unity/device_shots/alttester/level_NN_popup.png, level_NN_midrun.png, level_NN_result.png", ""]
    return "\n".join(lines)


def merge_rows(old_rows, new_rows):
    """A new run for some levels replaces those rows and keeps the rest of an earlier run (same region)."""
    by_key = {(r.get("region", 1), r["level"]): r for r in old_rows}
    for r in new_rows:
        by_key[(r.get("region", 1), r["level"])] = r
    return [by_key[k] for k in sorted(by_key)]


def write_results(meta, rows, md_path=OUT_MD, json_path=OUT_JSON):
    os.makedirs(os.path.dirname(md_path), exist_ok=True)
    os.makedirs(os.path.dirname(json_path), exist_ok=True)
    with open(md_path, "w", encoding="utf-8") as f:
        f.write(render_markdown(meta, rows))
    with open(json_path, "w", encoding="utf-8") as f:
        json.dump({"meta": meta, "levels": sorted(rows, key=lambda r: (r.get("region", 1), r["level"]))}, f, indent=1)


def load_rows(json_path=OUT_JSON):
    try:
        with open(json_path, encoding="utf-8") as f:
            return json.load(f).get("levels", [])
    except (OSError, ValueError):
        return []


# ---------------------------------------------------------------------------------------------- the bridge to the game

class Bridge:
    """The static methods of LevelSweepApi.cs, each reply parsed into a dict ({'ok': False, 'err': ...} on failure)."""

    def __init__(self, driver):
        self.d = driver

    def call(self, method, *params):
        last = None
        for attempt in range(3):
            try:
                raw = self.d.call_static_method(BRIDGE_TYPE, method, BRIDGE_ASM, parameters=list(params))
                return unwrap(raw)
            except Exception as e:  # noqa: BLE001 - scene loads make AltTester commands fail now and then; retry
                last = e
                time.sleep(0.6)
        return {"ok": False, "err": "%s: %s" % (type(last).__name__, last)}

    def state(self):
        r = self.call("RunState")
        return r if r.get("ok") else {"ok": False, "run": False, "err": r.get("err")}


def short(d):
    return json.dumps(d)[:300] if d is not None else "None"


def wait_state(br, clk, pred, timeout, what, poll=0.4):
    end = clk.time() + timeout
    last = None
    while clk.time() < end:
        last = br.state()
        if pred(last):
            return last
        clk.sleep(poll)
    raise TestFailure("timed out after %ds waiting for %s; last state %s" % (timeout, what, short(last)))


# ---------------------------------------------------------------------------------------------- navigation

def open_level_grid(g, region, clk):
    if g.exists("LevelGrid"):
        return
    g.to_menu()
    g.tap("Play")
    g.find("Region%d" % region, 15)
    clk.sleep(1.0)
    g.tap("Region%d" % region)
    g.find("LevelGrid", 10)
    clk.sleep(1.0)


def tap_level(g, n, clk):
    """Taps the level circle LevelN; scrolls the grid first when the circle is not on screen. Leaves the level popup open (PLAY visible)."""
    name = "Level%d" % n
    w, h = g.screen_size()
    for _ in range(10):
        obj = g.find(name, 10)
        y = obj.mobileY
        if 0.14 * h < y < 0.86 * h and 0 < obj.x < w:
            obj.tap()
            clk.sleep(0.6)
            if g.exists("PLAY", 3):
                return
            if g.exists("OK", 1):                      # the 'Locked' message
                g.tap("OK")
                raise TestFailure("%s is locked (is GAME_JUMP_TEST on?)" % name)
        elif y > h / 2:
            g.d.swipe((int(w / 2), int(h * 0.72)), (int(w / 2), int(h * 0.34)), duration=0.35)
            clk.sleep(0.6)
        else:
            g.d.swipe((int(w / 2), int(h * 0.34)), (int(w / 2), int(h * 0.72)), duration=0.35)
            clk.sleep(0.6)
    raise TestFailure("could not open the popup of %s" % name)


def child_exists(obj, name):
    try:
        obj.find_object_from_object(By.NAME, name)
        return True
    except Exception:  # noqa: BLE001 - NotFoundException and driver errors both mean 'not there'
        return False


def read_popup(g, node):
    popup = {"goal": g.find("Goal", 5).get_text(), "rule": None, "rule_italic": None, "gem_badge": child_exists(node, "GemBadge")}
    if g.exists("Rule"):
        rule = g.find("Rule")
        popup["rule"] = rule.get_text()
        try:
            popup["rule_italic"] = is_italic(rule.get_component_property("UnityEngine.UI.Text", "fontStyle", "UnityEngine.UI"))
        except Exception:  # noqa: BLE001 - property access can be refused; the italic check then stays 'unknown'
            popup["rule_italic"] = None
    return popup


def recover(g, br, clk):
    """Back to a known place after an error: close any overlay or leave the run, then the main menu."""
    try:
        br.call("Resume")
        if g.exists("Level select"):
            g.tap("Level select")
            clk.sleep(1.5)
        elif g.exists("Pause"):
            g.tap("Pause")
            g.tap("Level select")
            clk.sleep(1.5)
    except Exception:  # noqa: BLE001
        pass
    try:
        g.to_menu()
    except Exception:  # noqa: BLE001
        pass


# ---------------------------------------------------------------------------------------------- one level

def play_level(g, br, n, args, clk):
    row = new_row(n, args.region)
    shot = "level_%02d_" % n
    t_start = clk.time()
    pre = br.call("LevelPreview", args.region, n)
    if not pre.get("ok"):
        raise TestFailure("LevelPreview failed: %s" % pre.get("err"))
    row.update(id=pre["id"], goal_type=pre["goalType"], goal=pre["goalText"], target=pre["target"], platforms=pre["platforms"], special=pre["special"],
               kinds_layout=pre["kinds"], gems_placed=pre["gemsPlaced"], coins_placed=pre["coinsPlaced"])
    skip_goals = [x.strip() for x in args.skip_goals.split(",") if x.strip()]
    if pre["goalType"] in skip_goals:
        row.update(result="skip", note="autopilot cannot play goal type %s" % pre["goalType"])
        row["verdict"] = verdict_for(row, args.time_limit)
        return row

    # 1. the level popup
    open_level_grid(g, args.region, clk)
    node = g.find("Level%d" % n, 10)
    tap_level(g, n, clk)
    popup = read_popup(g, node)
    g.screenshot(shot + "popup")
    row["shots"].append(shot + "popup.png")
    row["checks"] += evaluate_popup(n, pre, popup)

    # 2. start, restart once for the layout comparison
    g.tap("PLAY")
    g.wait_scene("Run", 30)
    first = wait_state(br, clk, lambda s: s.get("run") and s.get("level") == pre["id"], 40, "the Run scene to start")
    second = None
    if not args.no_relayout:
        g.tap("Pause", 10)
        g.tap("Restart", 10)
        second = wait_state(br, clk, lambda s: s.get("run") and not s.get("overlay"), 30, "the restarted attempt (pause overlay gone)")
    row["checks"].append(evaluate_layout(first, second))
    row["layout_hash"] = first.get("layoutHash", "")
    row["layout_hash2"] = second.get("layoutHash", "") if second else ""

    # 3. the run after the intro
    run = wait_state(br, clk, lambda s: s.get("run") and not s.get("intro") and not s.get("overlay"), 40, "the level intro to end")
    power_objects = sum(1 for name in POWER_NAMES if g.exists("Power" + name))
    gem_pill = g.exists("GemPill")
    row["checks"] += evaluate_run(n, pre, run, power_objects, gem_pill)
    row["character"] = run.get("character", "")
    row["power_buttons"] = run.get("powerButtons", 0)
    row["rows"] = run.get("row", 0)
    row["layout"] = run.get("layout", "")
    att = br.call("AttachAutopilot", float(args.skill), int(args.seed + n * 131 + 7919), int(args.depth))
    if not att.get("ok"):
        raise TestFailure("autopilot not attached: %s" % att.get("err"))

    # 4. watch the run
    real_limit = args.time_limit / max(1.0, args.speed) * 1.6 + 30
    stall_limit = args.stall / max(1.0, args.speed)
    t_run = clk.time()
    best_row, t_best, seen, midshot, result, last = -1, clk.time(), set(), False, "", run
    while True:
        s = br.state()
        if not s.get("run"):
            raise TestFailure("the Run scene was lost during the level; last state %s" % short(last))
        last = s
        if s.get("paused"):
            br.call("Resume")
        if s.get("ground"):
            seen.add(s["ground"])
        if s.get("over"):
            break
        if s.get("row", 0) > best_row:
            best_row, t_best = s["row"], clk.time()
        if not midshot and (s.get("row", 0) * 2 >= max(1, s.get("target", 1)) or s.get("sec", 0) >= 40):
            g.screenshot(shot + "midrun")
            row["shots"].append(shot + "midrun.png")
            midshot = True
        now = clk.time()
        if s.get("sec", 0) > args.time_limit or now - t_run > real_limit:
            result = "timeout"
            row["fail_reason"] = "time limit %s s reached at row %s of %s" % (args.time_limit, s.get("row"), s.get("target"))
            break
        if now - t_best > stall_limit:
            result = "timeout"
            row["fail_reason"] = "no progress for %d s at row %s of %s" % (args.stall, s.get("row"), s.get("target"))
            break
        clk.sleep(args.poll)
    if not midshot:
        g.screenshot(shot + "midrun")
        row["shots"].append(shot + "midrun.png")

    # 5. the result overlay
    if last.get("over"):
        final = wait_state(br, clk, lambda s: s.get("run") and s.get("over") and s.get("overlay"), 25, "the result overlay", poll=0.5)
        last = final
        result = "cleared" if final.get("reached") else "fell"
        row["fail_reason"] = final.get("fail", "") if result == "fell" else ""
    g.screenshot(shot + "result")
    row["shots"].append(shot + "result.png")
    seen.discard(None)
    row.update(result=result, time=float(last.get("sec", 0.0)), real_sec=round(clk.time() - t_start, 1), rows=last.get("row", 0), gems=last.get("gems", 0) + last.get("hidden", 0),
               coins=last.get("coins", 0), stars=last.get("stars", 0), score=last.get("score", 0), platforms=last.get("platforms", row["platforms"]),
               special=last.get("special", row["special"]), kinds_layout=last.get("kinds", row["kinds_layout"]), gems_placed=last.get("gemsPlaced", row["gems_placed"]),
               coins_placed=last.get("coinsPlaced", row["coins_placed"]), kinds_seen=",".join(sorted(k for k in seen if k != "N")), layout=last.get("layout", row["layout"]))
    row["verdict"] = verdict_for(row, args.time_limit)
    # 6. back to the level screen (a run that timed out has no result overlay: open the pause overlay, which has the same button)
    if not last.get("over"):
        g.tap("Pause", 10)
    if g.exists("Level select", 5):
        g.tap("Level select")
        g.wait_scene("Meta", 20)
        clk.sleep(1.0)
    return row


# ---------------------------------------------------------------------------------------------- the sweep

def prepare_flags(g, br, args, clk):
    """SYSTEM_TEST on (autopilot, all characters) and GAME_JUMP_TEST on or off, through LevelSweepApi.SetFlags (documented in README.md)."""
    want_jump = args.jump_test == "on"
    r = br.call("SetFlags", True, want_jump)
    if not r.get("ok"):
        raise TestFailure("could not set the test flags: %s (is this the alttester build with LevelSweepApi?)" % r.get("err"))
    if not r.get("autopilot"):
        raise TestFailure("Autopilot is not allowed after SetFlags: %s" % short(r))
    if want_jump and not r.get("unlockAll"):
        raise TestFailure("levels are not all open after SetFlags: %s" % short(r))
    if args.character:
        c = br.call("SelectCharacter", args.character)
        if not c.get("ok") or c.get("selected") != args.character:
            raise TestFailure("character '%s' not selectable: %s" % (args.character, short(c)))
    if args.speed > 1:
        br.call("SetSpeed", float(args.speed))
    g.to_menu()
    return r


def sweep(g, br, args, clk=None, out_md=OUT_MD, out_json=OUT_JSON):
    """Plays args.levels in order, writes the results after every level. Returns (rows of this run, exit code)."""
    clk = clk or Clock()
    flags = prepare_flags(g, br, args, clk)
    ping = br.call("Ping")
    meta = {"date": datetime.datetime.now().strftime("%Y-%m-%d %H:%M"), "version": ping.get("v", "?"), "system": flags.get("system"), "jump": flags.get("jump"),
            "region": args.region, "skill": args.skill, "depth": args.depth, "speed": args.speed, "time_limit": args.time_limit, "character": args.character or "selected"}
    old = [] if args.fresh else load_rows(out_json)
    done = []
    code = 0
    for n in args.levels:
        t = clk.time()
        try:
            row = play_level(g, br, n, args, clk)
        except Exception as e:  # noqa: BLE001 - one broken level must not stop the sweep
            row = new_row(n, args.region)
            row.update(result="error", fail_reason="%s: %s" % (type(e).__name__, str(e)[:300]), verdict="FAIL")
            try:
                g.screenshot("level_%02d_error" % n)
                row["shots"].append("level_%02d_error.png" % n)
            except Exception:  # noqa: BLE001
                pass
            recover(g, br, clk)
        done.append(row)
        failed_checks = [c["name"] for c in row["checks"] if c["status"] == "FAIL"]
        print("%-4s level %2d  %-8s %6.1fs  row %s/%s  gems %s coins %s  %s%s" % (row["verdict"], n, row["result"], row["time"], row["rows"], row["target"], row["gems"], row["coins"],
                                                                                 row["fail_reason"] or row["note"], ("  CHECKS: " + "; ".join(failed_checks)) if failed_checks else ""))
        write_results(meta, merge_rows(old, done), out_md, out_json)
        if row["verdict"] == "FAIL" or failed_checks:
            code = 1
        if args.stop_on_fail and row["verdict"] == "FAIL":
            break
        sys.stdout.flush()
    print("results: %s and %s" % (out_md, out_json))
    return done, code


def parse_args(argv):
    p = argparse.ArgumentParser(description="AltTester level sweep with the in-game Autopilot")
    p.add_argument("--levels", default="1-15", help="levels of the region, e.g. 1-15 or 8-15 or 3,7 (resume from level N: --levels N-15)")
    p.add_argument("--region", type=int, default=1)
    p.add_argument("--skill", type=float, default=0.5, help="autopilot skill: 0 perfect, 1 human-like (reaction lag and jitter)")
    p.add_argument("--depth", type=int, default=2, help="autopilot look-ahead in landings")
    p.add_argument("--seed", type=int, default=1, help="autopilot jitter seed base")
    p.add_argument("--speed", type=float, default=1.0, help="game speed 1..4 (1 = real time)")
    p.add_argument("--time-limit", type=int, default=300, dest="time_limit", help="game seconds a level may take for a PASS")
    p.add_argument("--stall", type=int, default=90, help="game seconds without a new best row before the level counts as timeout")
    p.add_argument("--poll", type=float, default=0.5)
    p.add_argument("--jump-test", choices=["on", "off"], default="on", dest="jump_test", help="GAME_JUMP_TEST (all levels open); on by default")
    p.add_argument("--character", default="", help="select this character id first (momo, forestgirl); default = whatever is selected")
    p.add_argument("--skip-goals", default=SKIP_GOALS_DEFAULT, dest="skip_goals")
    p.add_argument("--no-relayout", action="store_true", dest="no_relayout", help="do not restart each level once to compare the two layouts")
    p.add_argument("--stop-on-fail", action="store_true", dest="stop_on_fail")
    p.add_argument("--fresh", action="store_true", help="do not merge with the results of an earlier run")
    p.add_argument("--install", action="store_true")
    p.add_argument("--no-launch", action="store_true", dest="no_launch")
    p.add_argument("--keep-data", action="store_true", dest="keep_data", help="launch without clearing the app data")
    p.add_argument("--out-md", default=OUT_MD, dest="out_md")
    p.add_argument("--out-json", default=OUT_JSON, dest="out_json")
    args, _unknown = p.parse_known_args(argv)
    args.levels = parse_levels(args.levels)
    return args


def main(argv=None):
    args = parse_args(sys.argv[1:] if argv is None else argv)
    os.environ.setdefault("ANDROID_SERIAL", SERIAL_DEFAULT)
    if not altbt.device_connected():
        print("no device connected (adb devices)")
        return 2
    if args.install:
        altbt.install()
    if args.no_launch:
        altbt.forward_ports()
    else:
        altbt.launch(clear=not args.keep_data)
    try:
        g = altbt.connect()
        g.find("Play", 40)                                  # the main menu is up
    except TestFailure as e:
        print("FAIL connect  %s" % e)
        print("RESULT: FAILED")
        return 1
    br = Bridge(g.d)
    try:
        rows, code = sweep(g, br, args, out_md=args.out_md, out_json=args.out_json)
    except TestFailure as e:
        print("FAIL setup  %s" % e)
        print("RESULT: FAILED")
        return 1
    finally:
        try:
            g.stop()
        except Exception:  # noqa: BLE001
            pass
    passed = sum(1 for r in rows if r["verdict"] == "PASS")
    failed = sum(1 for r in rows if r["verdict"] == "FAIL")
    skipped = sum(1 for r in rows if r["verdict"] == "SKIP")
    print("passed %d, failed %d, skipped %d" % (passed, failed, skipped))
    print("RESULT:", "PASSED" if code == 0 else "FAILED")
    return code


if __name__ == "__main__":
    sys.exit(main())
