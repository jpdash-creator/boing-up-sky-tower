"""Unit tests for level_sweep.py with a mock driver (no phone, no AltTester Desktop needed):

    python -m unittest Tools/android_test/alttester/test_level_sweep.py        (or:  python Tools/android_test/alttester/test_level_sweep.py)

Covers the pure helpers (reply unwrapping, level list, verdicts, checklist, table and JSON writing, merging for resume) and plays two whole mock
levels (one cleared, one fell) through play_level / sweep with a fake game, a fake bridge and a fake clock, so the control flow is exercised
in zero real seconds. What it cannot cover: the real UI object names, AltTester timing and the C# side (LevelSweepApi.cs)."""
import json
import os
import sys
import tempfile
import types
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import level_sweep as ls  # noqa: E402
from altbt import TestFailure  # noqa: E402


class FakeClock:
    def __init__(self):
        self.t = 0.0

    def time(self):
        return self.t

    def sleep(self, s):
        self.t += s


class FakeObj:
    def __init__(self, world, name):
        self.world, self.name = world, name
        self.mobileY, self.x = 500, 300

    def get_text(self):
        return self.world.texts.get(self.name, "")

    def tap(self):
        self.world.tap(self.name)

    def get_component_property(self, comp, prop, asm):
        return self.world.rule_style

    def find_object_from_object(self, by, name):
        if name == "GemBadge" and self.world.gem_badge:
            return object()
        raise LookupError(name)


class FakeWorld:
    """One scripted level: grid -> popup -> run (intro 3.2 s, then 2 rows per second) -> result overlay."""

    def __init__(self, clk, n=1, goal_type="ReachFloor", target=20, fail_at_row=None, rule=None, same_layout=False, gem_badge=False, stall=False):
        self.clk, self.n, self.goal_type, self.target = clk, n, goal_type, target
        self.fail_at_row, self.rule, self.same_layout, self.gem_badge, self.stall = fail_at_row, rule, same_layout, gem_badge, stall
        self.rule_style = 2
        self.phase = "grid"            # grid, popup, run, over
        self.paused = False
        self.attempt = 0
        self.run_start = 0.0
        self.attached = False
        self.shots, self.calls = [], []
        self.texts = {"Goal": "Reach floor %d" % target, "Rule": rule or ""}

    # ---- the game's objects
    def present(self, name):
        if name == "LevelGrid" or name == "Play":
            return self.phase == "grid"
        if name == "Level%d" % self.n:
            return self.phase == "grid"
        if name in ("PLAY", "Goal"):
            return self.phase == "popup"
        if name == "Rule":
            return self.phase == "popup" and self.rule is not None
        if name == "Pause":
            return self.phase == "run" and not self.paused
        if name == "Restart":
            return self.phase == "run" and self.paused
        if name == "Level select":
            return self.phase == "over" or (self.phase == "run" and self.paused)
        if name == "GemPill":
            return self.phase == "run" and self.goal_type != "ComboChain"
        return False

    def tap(self, name):
        if name == "Level%d" % self.n:
            self.phase = "popup"
        elif name == "PLAY":
            self.phase, self.attempt, self.run_start, self.paused = "run", 1, self.clk.t, False
        elif name == "Pause":
            self.paused = True
        elif name == "Restart":
            self.attempt, self.run_start, self.paused = self.attempt + 1, self.clk.t, False
        elif name == "Level select":
            self.phase, self.paused = "grid", False

    # ---- RunState as the C# side would report it
    def run_state(self):
        if self.phase not in ("run", "over"):
            return {"ok": True, "run": False}
        sec = max(0.0, self.clk.t - self.run_start)
        intro = sec < 3.2
        row = 0 if intro else (3 if self.stall else int((sec - 3.2) * 2))
        over, reached, fail = False, False, ""
        if self.attached and not self.stall:
            if self.fail_at_row is not None and row >= self.fail_at_row:
                over, fail = True, "You fell!"
            elif row >= self.target:
                over, reached = True, True
        if over:
            self.phase = "over"
        seed = 1000 if self.same_layout else 1000 + self.attempt
        return {"ok": True, "run": True, "level": "lvl_001_%02d" % self.n, "n": self.n, "goalType": self.goal_type, "goalText": "Reach floor %d" % self.target,
                "target": self.target, "rule": self.rule or "", "over": over or self.phase == "over", "reached": reached, "fail": fail, "paused": False, "intro": intro,
                "overlay": self.paused or self.phase == "over", "row": min(row, self.target), "sec": sec, "coins": row // 2, "gems": 1 if reached else 0, "hidden": 0,
                "stars": 3 if reached else 0, "score": row * 10, "ground": "M" if row % 4 == 0 else "N", "layout": "lvl_001_%02d attempt %d" % (self.n, self.attempt),
                "attemptSeed": seed, "layoutHash": "h%d" % seed, "platforms": 26, "special": 6, "kinds": "N:20,M:6", "gemsPlaced": 0 if self.goal_type == "ComboChain" else 4,
                "coinsPlaced": 30, "character": "momo", "selected": "momo", "powerButtons": 0, "powerOwned": 0}


class FakeBridge:
    def __init__(self, world):
        self.w = world

    def call(self, method, *params):
        self.w.calls.append(method)
        w = self.w
        if method == "LevelPreview":
            return {"ok": True, "id": "lvl_001_%02d" % w.n, "n": w.n, "goalType": w.goal_type, "goalText": "Reach floor %d" % w.target, "rule": w.rule or "",
                    "target": w.target, "rows": w.target, "platforms": 26, "special": 6, "kinds": "N:20,M:6", "gemsPlaced": 0 if w.goal_type == "ComboChain" else 4, "coinsPlaced": 30}
        if method == "AttachAutopilot":
            w.attached = True
            return {"ok": True}
        if method == "SetFlags":
            return {"ok": True, "system": True, "jump": params[1], "unlockAll": params[1], "autopilot": True}
        if method == "Ping":
            return {"ok": True, "v": "0.9.9.9"}
        return {"ok": True}

    def state(self):
        return self.w.run_state()


class FakeGame:
    def __init__(self, world, clk):
        self.w, self.clk = world, clk
        self.d = types.SimpleNamespace(swipe=lambda *a, **k: None)

    def exists(self, name, timeout=0.0):
        return self.w.present(name)

    def find(self, name, timeout=10):
        if not self.w.present(name):
            raise TestFailure("object '%s' not found" % name)
        return FakeObj(self.w, name)

    def tap(self, name, timeout=10):
        self.find(name, timeout)
        self.w.tap(name)

    def screen_size(self):
        return 1080, 1920

    def wait_scene(self, scene, timeout=30):
        pass

    def screenshot(self, name):
        self.w.shots.append(name)

    def to_menu(self):
        self.w.phase = "grid"


def make_args(**kw):
    argv = ["--levels", "1", "--time-limit", "100", "--stall", "30"]
    for k, v in kw.items():
        argv += ["--" + k.replace("_", "-"), str(v)]
    return ls.parse_args(argv)


def play(world_kwargs=None, n=1, **arg_kw):
    clk = FakeClock()
    world = FakeWorld(clk, n=n, **(world_kwargs or {}))
    row = ls.play_level(FakeGame(world, clk), FakeBridge(world), n, make_args(**arg_kw), clk)
    return row, world


class PureHelperTests(unittest.TestCase):
    def test_parse_levels(self):
        self.assertEqual(ls.parse_levels("1-3,7,3"), [1, 2, 3, 7])
        self.assertEqual(ls.parse_levels("15"), [15])

    def test_unwrap_handles_double_encoding(self):
        self.assertEqual(ls.unwrap('{"ok":true,"a":1}'), {"ok": True, "a": 1})
        self.assertEqual(ls.unwrap(json.dumps('{"ok":true,"a":1}')), {"ok": True, "a": 1})
        self.assertFalse(ls.unwrap("not json")["ok"])
        self.assertFalse(ls.unwrap("")["ok"])
        self.assertFalse(ls.unwrap("[1,2]")["ok"])

    def test_is_italic(self):
        self.assertTrue(ls.is_italic(2))
        self.assertTrue(ls.is_italic("Italic"))
        self.assertFalse(ls.is_italic(0))
        self.assertIsNone(ls.is_italic(None))

    def test_verdicts(self):
        self.assertEqual(ls.verdict_for({"result": "cleared", "time": 99.0}, 100), "PASS")
        self.assertEqual(ls.verdict_for({"result": "cleared", "time": 101.0}, 100), "FAIL")
        self.assertEqual(ls.verdict_for({"result": "fell", "time": 5.0}, 100), "FAIL")
        self.assertEqual(ls.verdict_for({"result": "timeout", "time": 5.0}, 100), "FAIL")
        self.assertEqual(ls.verdict_for({"result": "skip"}, 100), "SKIP")
        self.assertEqual(ls.verdict_for({"result": "error"}, 100), "FAIL")

    def test_popup_checks(self):
        pre = {"goalText": "Reach floor 90", "goalType": "NoWallBounce", "rule": "Don't touch the walls."}
        ok = ls.evaluate_popup(7, pre, {"goal": "Reach floor 90", "rule": "Don't touch the walls.", "rule_italic": True, "gem_badge": False})
        self.assertTrue(all(c["status"] != "FAIL" for c in ok))
        no_rule = ls.evaluate_popup(7, pre, {"goal": "Reach floor 90", "rule": None, "rule_italic": None, "gem_badge": False})
        self.assertIn("popup italic wall rule", [c["name"] for c in no_rule if c["status"] == "FAIL"])
        not_italic = ls.evaluate_popup(7, pre, {"goal": "Reach floor 90", "rule": "Don't touch the walls.", "rule_italic": False, "gem_badge": False})
        self.assertIn("popup italic wall rule", [c["name"] for c in not_italic if c["status"] == "FAIL"])
        stray = ls.evaluate_popup(3, {"goalText": "x", "goalType": "ReachFloor", "rule": ""}, {"goal": "x", "rule": "Don't touch the walls.", "rule_italic": True, "gem_badge": False})
        self.assertIn("popup rule only on level 7", [c["name"] for c in stray if c["status"] == "FAIL"])
        combo = ls.evaluate_popup(5, {"goalText": "x", "goalType": "ComboChain", "rule": ""}, {"goal": "x", "rule": None, "rule_italic": None, "gem_badge": True})
        self.assertIn("combo level: no gem badge", [c["name"] for c in combo if c["status"] == "FAIL"])

    def test_run_checks(self):
        pre = {"goalText": "g", "target": 90, "goalType": "ComboChain"}
        run = {"character": "momo", "selected": "momo", "powerButtons": 2, "powerOwned": 2, "goalText": "g", "target": 90, "gemsPlaced": 0}
        self.assertTrue(all(c["status"] != "FAIL" for c in ls.evaluate_run(5, pre, run, 2, False)))
        bad = ls.evaluate_run(5, pre, dict(run, character="sporeling"), 1, True)
        self.assertEqual(sorted(c["name"] for c in bad if c["status"] == "FAIL"),
                         ["combo level: no gem pill / gems", "power-up buttons count", "run uses selected character"])

    def test_layout_check(self):
        a, b = {"layoutHash": "1", "attemptSeed": 5}, {"layoutHash": "2", "attemptSeed": 6}
        self.assertEqual(ls.evaluate_layout(a, b)["status"], "ok")
        self.assertEqual(ls.evaluate_layout(a, dict(b, layoutHash="1"))["status"], "FAIL")
        self.assertEqual(ls.evaluate_layout(a, None)["status"], "n/a")

    def test_table_and_merge(self):
        passed = ls.new_row(1, 1)
        passed.update(goal="Reach floor 60", rows=60, target=60, platforms=80, kinds_seen="M,C", kinds_layout="N:60,M:12", result="cleared", time=61.5, gems=1, gems_placed=2,
                      coins=30, coins_placed=40, stars=3, verdict="PASS")
        failed = ls.new_row(2, 1)
        failed.update(goal="Reach floor 90", rows=40, target=90, result="fell", fail_reason="Wall touched!", time=30.0, verdict="FAIL",
                      checks=[ls.check("popup goal text", False, "mismatch")])
        skipped = ls.new_row(3, 1)
        skipped.update(goal="Survive", result="skip", note="autopilot cannot play goal type SurviveWave", verdict="SKIP")
        md = ls.render_markdown({"date": "d", "version": "1.2.3.4", "region": 1}, [failed, skipped, passed])
        self.assertIn("1 PASS, 1 FAIL, 1 SKIP, 1 checklist item(s) failed", md)
        self.assertIn("| 1 | Reach floor 60 | 60/60 | 80 | M,C of N:60,M:12 | cleared | 61.5 | 1/2 | 30/40 | 3 | - | **PASS** | ok |", md)
        self.assertIn("Wall touched!", md)
        self.assertIn("**SKIP**", md)
        self.assertLess(md.index("| 1 |"), md.index("| 2 |"))     # sorted by level
        merged = ls.merge_rows([dict(passed, verdict="FAIL"), skipped], [passed])
        self.assertEqual([r["level"] for r in merged], [1, 3])
        self.assertEqual(merged[0]["verdict"], "PASS")

    def test_write_and_reload(self):
        with tempfile.TemporaryDirectory() as d:
            md, js = os.path.join(d, "a", "l.md"), os.path.join(d, "a", "l.json")
            row = ls.new_row(4, 1)
            row.update(result="cleared", verdict="PASS", time=10.0)
            ls.write_results({"date": "d"}, [row], md, js)
            self.assertTrue(os.path.getsize(md) > 0)
            self.assertEqual(ls.load_rows(js)[0]["level"], 4)
            self.assertEqual(ls.load_rows(os.path.join(d, "missing.json")), [])


class MockLevelTests(unittest.TestCase):
    def test_cleared_level(self):
        row, world = play()
        self.assertEqual(row["result"], "cleared")
        self.assertEqual(row["verdict"], "PASS")
        self.assertEqual(row["stars"], 3)
        self.assertEqual(row["rows"], 20)
        self.assertEqual(row["kinds_seen"], "M")
        self.assertEqual([c["name"] for c in row["checks"] if c["status"] == "FAIL"], [])
        self.assertNotEqual(row["layout_hash"], row["layout_hash2"])
        self.assertEqual(world.shots, ["level_01_popup", "level_01_midrun", "level_01_result"])
        self.assertEqual(world.phase, "grid")                     # left through 'Level select'
        self.assertIn("AttachAutopilot", world.calls)

    def test_fell_level(self):
        row, _ = play({"fail_at_row": 6})
        self.assertEqual((row["result"], row["verdict"], row["fail_reason"]), ("fell", "FAIL", "You fell!"))

    def test_stalled_level_times_out(self):
        row, world = play({"stall": True})
        self.assertEqual((row["result"], row["verdict"]), ("timeout", "FAIL"))
        self.assertIn("no progress", row["fail_reason"])
        self.assertEqual(world.phase, "grid")                     # left through Pause -> Level select

    def test_same_layout_is_flagged(self):
        row, _ = play({"same_layout": True})
        self.assertIn("layout differs between attempts", [c["name"] for c in row["checks"] if c["status"] == "FAIL"])

    def test_skip_goal_type(self):
        row, world = play({"goal_type": "SurviveWave"})
        self.assertEqual((row["result"], row["verdict"]), ("skip", "SKIP"))
        self.assertNotIn("AttachAutopilot", world.calls)

    def test_level_7_rule(self):
        row, _ = play({"goal_type": "NoWallBounce", "rule": "Don't touch the walls."}, n=7)
        self.assertEqual([c["name"] for c in row["checks"] if c["status"] == "FAIL"], [])
        self.assertIn("popup italic wall rule", [c["name"] for c in row["checks"]])

    def test_combo_level_with_gem_badge_fails_check(self):
        row, _ = play({"goal_type": "ComboChain", "gem_badge": True}, n=5)
        self.assertIn("combo level: no gem badge", [c["name"] for c in row["checks"] if c["status"] == "FAIL"])

    def test_sweep_writes_results_and_exit_code(self):
        clk = FakeClock()
        world = FakeWorld(clk)
        with tempfile.TemporaryDirectory() as d:
            md, js = os.path.join(d, "l.md"), os.path.join(d, "l.json")
            args = make_args()
            args.fresh = True
            rows, code = ls.sweep(FakeGame(world, clk), FakeBridge(world), args, clk, md, js)
            self.assertEqual(code, 0)
            self.assertEqual([r["verdict"] for r in rows], ["PASS"])
            with open(md, encoding="utf-8") as f:
                self.assertIn("**PASS**", f.read())
            self.assertEqual(ls.load_rows(js)[0]["level"], 1)


if __name__ == "__main__":
    unittest.main()
