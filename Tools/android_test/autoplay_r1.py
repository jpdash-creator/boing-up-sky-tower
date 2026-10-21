"""Region 1 as a real player (adb only, no AltTester): the development build plays its own levels with the Autopilot.

    python Tools/android_test/autoplay_r1.py [--skill 0.5] [--difficulty 0] [--levels 1-15] [--seeds 1] [--stop-on-fail]
                                             [--speed 1] [--install] [--where auto|internal|external|both] [--timeout 3600]

What it does:
  1. writes autoplay.json into the app's persistentDataPath on the phone (run-as for the internal files dir of the debuggable build,
     adb push for the external files dir; the build logs where it looks, 'auto' pushes to both when no save is found),
  2. launches the game and follows logcat for the 'BTAUTO ' lines (JSON, same chunking idea as BTUI),
  3. takes a screenshot at every fall (E:/Unity/device_shots/autoplay_<level>_<n>.png),
  4. prints a PASS/FAIL table, writes E:/Unity/qa/autoplay_report.md and copies the CSV the game wrote (autoplay_results.csv).
The player's real save is not touched by the game (the suite plays on an in-memory save). Needs a development build (Tools/android_test/README
in the repo, build via the normal Android build) and the phone connected with the screen on.
Exit code: 0 all levels cleared, 1 a level failed or the run broke off, 2 no device.
"""
import argparse
import datetime
import json
import os
import re
import subprocess
import sys
import threading
import time

PKG = "com.jdlabs.boingupskytower"
APK = "E:/Unity/builds/boingupskytower.apk"
SHOTS = "E:/Unity/device_shots"
QA = "E:/Unity/qa"
FLAG = "autoplay.json"
CSV = "autoplay_results.csv"
EXT_DIR = "/sdcard/Android/data/%s/files" % PKG


SERIAL = os.environ.get("ANDROID_SERIAL", "96355505")   # the owner's phone: an emulator may be attached too, plain adb would then fail


def adb(*args, check=False, timeout=120, data=None):
    r = subprocess.run(["adb", "-s", SERIAL] + list(args), capture_output=True, timeout=timeout, input=data)
    if check and r.returncode != 0:
        raise RuntimeError("adb %s failed: %s" % (" ".join(args), r.stderr.decode(errors="replace")[:300]))
    return r.stdout.decode(errors="replace")


def parse_levels(text):
    levels = []
    for part in text.split(","):
        part = part.strip()
        if "-" in part:
            a, b = part.split("-", 1)
            levels.extend(range(int(a), int(b) + 1))
        elif part:
            levels.append(int(part))
    return levels


# ---------------------------------------------------------------- flag file on the phone

def push_internal(text):
    adb("shell", "run-as %s mkdir -p files" % PKG)
    adb("shell", "run-as %s sh -c 'cat > files/%s'" % (PKG, FLAG), data=text.encode())
    return FLAG in adb("shell", "run-as %s ls files" % PKG)


def push_external(text, tmp):
    with open(tmp, "w", encoding="utf-8") as f:
        f.write(text)
    adb("shell", "mkdir -p %s" % EXT_DIR)
    adb("push", tmp, "%s/%s" % (EXT_DIR, FLAG))
    return FLAG in adb("shell", "ls %s" % EXT_DIR)


def remove_flags():
    adb("shell", "run-as %s rm -f files/%s" % (PKG, FLAG))
    adb("shell", "rm -f %s/%s" % (EXT_DIR, FLAG))


def has_save_internal():
    return "save.json" in adb("shell", "run-as %s ls files" % PKG)


def has_save_external():
    return "save.json" in adb("shell", "ls %s" % EXT_DIR)


def place_flag(text, where, tmp):
    ok = []
    if where == "auto":
        if has_save_internal():
            where = "internal"
        elif has_save_external():
            where = "external"
        else:
            where = "both"
    if where in ("internal", "both") and push_internal(text):
        ok.append("internal")
    if where in ("external", "both") and push_external(text, tmp):
        ok.append("external")
    return ok


def pull_csv(dest):
    out = adb("exec-out", "run-as %s cat files/%s" % (PKG, CSV))
    if not out.strip():
        out = adb("exec-out", "cat %s/%s" % (EXT_DIR, CSV))
    if out.strip():
        with open(dest, "w", encoding="utf-8", newline="") as f:
            f.write(out)
        return True
    return False


# ---------------------------------------------------------------- logcat

class Follower:
    """Reads logcat in a thread; collects BTAUTO events (re-joining chunked ones) and crash lines."""

    def __init__(self):
        self.events = []
        self.done = False
        self.crash = None
        self.last_line = time.time()
        self._chunks = {}
        self._lock = threading.Lock()
        self._proc = subprocess.Popen(["adb", "-s", SERIAL, "logcat", "-v", "brief"], stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
        threading.Thread(target=self._read, daemon=True).start()

    def _read(self):
        for raw in iter(self._proc.stdout.readline, b""):
            line = raw.decode(errors="replace").rstrip()
            m = re.search(r"BTAUTO (.*)$", line)
            if m:
                self._take(m.group(1).strip())
            elif ("FATAL EXCEPTION" in line and PKG in line) or ("ANR in " + PKG) in line or ("Fatal signal" in line and PKG in line):
                self.crash = line[:300]

    def _take(self, text):
        self.last_line = time.time()
        if text == "done":
            self.done = True
            return
        try:
            ev = json.loads(text)
        except ValueError:
            return
        if "chunk" in ev:
            parts = self._chunks.setdefault(ev["seq"], {})
            parts[ev["chunk"]] = ev["d"]
            if len(parts) == ev["of"]:
                text = "".join(parts[i] for i in range(ev["of"]))
                del self._chunks[ev["seq"]]
                try:
                    ev = json.loads(text)
                except ValueError:
                    return
            else:
                return
        with self._lock:
            self.events.append(ev)

    def take_events(self):
        with self._lock:
            out, self.events = self.events, []
        return out

    def close(self):
        self._proc.kill()


def screenshot(path):
    data = subprocess.run(["adb", "-s", SERIAL, "exec-out", "screencap", "-p"], capture_output=True, timeout=60).stdout
    with open(path, "wb") as f:
        f.write(data)


# ---------------------------------------------------------------- report

def verdict(r):
    return "PASS" if r.get("res") == "cleared" else "FAIL"


def table(results):
    head = "%-3s %-11s %-7s %-9s %7s %6s %6s %-5s %-6s %5s %5s" % ("Lvl", "Level", "Result", "Floors", "Time", "Tick", "X", "Kind", "Stack", "Scr%", "Plans") + "  Stars Gems Coins"
    lines = [head, "-" * len(head)]
    for r in results:
        lines.append("%-3s %-11s %-7s %-9s %6.1fs %6d %6.2f %-5s %-6s %5d %5d  %s" % (
            r.get("n"), r.get("lvl"), r.get("res"), "%s/%s" % (r.get("floors"), r.get("target")), r.get("sec", 0), r.get("tick", 0),
            r.get("x", 0), r.get("gk", ""), ("%d%s" % (r.get("stack", 0), "!" if r.get("dense") else "")), r.get("scr", 0), r.get("plans", 0), verdict(r)) + "  %s/%s %s/%s %s/%s" % (
            r.get("stars", "?"), r.get("starsOf", "?"), r.get("gems", "?"), r.get("gemsOf", "?"), r.get("coins", "?"), r.get("coinsOf", "?")))
    return "\n".join(lines)


def write_report(results, shots, opts, meta, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    passed = sum(1 for r in results if verdict(r) == "PASS")
    out = ["# Region 1 autoplay report", "",
           "- Date: %s" % datetime.datetime.now().strftime("%Y-%m-%d %H:%M"),
           "- Game version: %s" % meta.get("version", "?"),
           "- Device: %s" % meta.get("device", "?"),
           "- Run id: %s" % meta.get("run", "?"),
           "- Autopilot skill: %s (0 perfect, 1 human), difficulty %s (+%s%% scroll step per 5 s), seeds %s, speed %s" % (
               opts["skill"], opts["difficulty"], meta.get("step", "?"), opts["seeds"], opts["speed"]),
           "- Result: **%d of %d runs cleared**" % (passed, len(results)), "",
           "| Level | Run | Result | Floors | Time | Death tick | X | Ground | Near (kind) | Scroll % | Stack in 3 u | Dense | Shot |",
           "|---|---|---|---|---|---|---|---|---|---|---|---|---|"]
    for r in results:
        shot = shots.get((r.get("n"), r.get("att")))
        out.append("| %s | %s | %s %s | %s/%s | %.1f s | %s | %.2f | %s (#%s) | %s | %s | %s | %s | %s |" % (
            r.get("lvl"), r.get("att"), verdict(r), r.get("res"), r.get("floors"), r.get("target"), r.get("sec", 0), r.get("tick"), r.get("x", 0),
            r.get("gk"), r.get("gid"), ",".join(str(i) for i in r.get("near", [])) + " " + r.get("nk", ""), r.get("scr"), r.get("stack"),
            "**yes**" if r.get("dense") else "no", ("[png](file:///%s)" % shot.replace("\\", "/")) if shot else ""))
    dense = [r for r in results if r.get("dense") and verdict(r) == "FAIL"]
    out += ["", "## Findings", ""]
    if not results:
        out.append("No level result was received.")
    if dense:
        out.append("Deaths inside a dense platform stack (%d or more platforms within 3 u of the player):" % 6)
        for r in dense:
            out.append("- %s: died at floor %s, x=%.2f, %s platforms near (%s), ground platform #%s kind %s" % (
                r.get("lvl"), r.get("floors"), r.get("x", 0), r.get("stack"), r.get("nk"), r.get("gid"), r.get("gk")))
    other = [r for r in results if verdict(r) == "FAIL" and not r.get("dense")]
    if other:
        out.append("Other failures:")
        for r in other:
            out.append("- %s: %s at floor %s of %s after %.1f s, ground kind %s, scroll %s%%, kill-zone gap %.2f" % (
                r.get("lvl"), r.get("res"), r.get("floors"), r.get("target"), r.get("sec", 0), r.get("gk"), r.get("scr"), r.get("gap", 0)))
    if not dense and not other and results:
        out.append("Every run was cleared.")
    out += ["", "A fall at skill 0 means the autopilot (a perfect player, planning with the real simulation) could not find a safe line: that points at an unfair "
            "or unsolvable spot. At higher skill a fall may just be the reaction lag; compare the same level at skill 0.", ""]
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(out))


# ---------------------------------------------------------------- main

def merge_csv_rows(results, path, run):
    """The live logcat feed can drop (the game killed, a USB hiccup); the game also writes every finished level to a CSV on the phone.
    Adds the rows of this run that the live feed missed, so the table and the report are complete for every level that was played."""
    import csv
    have = set((str(r.get("lvl")), str(r.get("att"))) for r in results)
    try:
        with open(path, newline="", encoding="utf-8") as f:
            rows = list(csv.DictReader(f))
    except OSError:
        return
    for row in rows:
        if run and row.get("run") != run:
            continue
        key = (row.get("level"), row.get("attempt"))
        if key in have:
            continue
        def num(k, cast=float, default=0):
            try:
                return cast(row.get(k, default))
            except (TypeError, ValueError):
                return default
        results.append({"ev": "level", "lvl": row.get("level"), "n": num("n", int), "att": num("attempt", int), "diff": num("difficulty", int),
                        "step": num("stepPercent", int), "skill": num("skill"), "goal": row.get("goal"), "res": row.get("result"),
                        "floors": num("floors", int), "target": num("targetRow", int), "sec": num("seconds"), "tick": num("tick", int),
                        "x": num("x"), "gk": row.get("groundKind"), "stack": num("stackCount", int), "scr": num("scrollPercent", int),
                        "plans": num("plans", int), "stars": num("stars", int), "starsOf": num("starsOf", int), "gems": num("gems", int),
                        "gemsOf": num("gemsOf", int), "coins": num("coins", int), "coinsOf": num("coinsOf", int), "from_csv": True})
        have.add(key)
    results.sort(key=lambda r: (r.get("n", 0), r.get("att", 0)))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--skill", type=float, default=0.5)
    ap.add_argument("--difficulty", type=int, default=0)
    ap.add_argument("--levels", default="1-15")
    ap.add_argument("--region", type=int, default=1)
    ap.add_argument("--seeds", type=int, default=1)
    ap.add_argument("--speed", type=float, default=1.0)
    ap.add_argument("--stop-on-fail", action="store_true")
    ap.add_argument("--install", action="store_true", help="install %s first" % APK)
    ap.add_argument("--where", default="auto", choices=["auto", "internal", "external", "both"])
    ap.add_argument("--timeout", type=int, default=3600, help="seconds for the whole run")
    ap.add_argument("--report", default=os.path.join(QA, "autoplay_report.md"))
    ap.add_argument("--dry-run", action="store_true", help="print autoplay.json and stop")
    a = ap.parse_args()

    opts = {"region": a.region, "levels": parse_levels(a.levels), "skill": a.skill, "difficulty": a.difficulty, "stopOnFail": a.stop_on_fail,
            "seeds": a.seeds, "speed": a.speed}
    text = json.dumps(opts)
    if a.dry_run:
        print(text)
        return 0
    if not any(l.strip().endswith("device") for l in adb("devices").splitlines()[1:]):
        print("no device connected (adb devices)")
        return 2
    os.makedirs(SHOTS, exist_ok=True)
    os.makedirs(QA, exist_ok=True)
    meta = {"device": adb("shell", "getprop ro.product.model").strip()}
    if a.install:
        out = adb("install", "-r", APK, timeout=300)
        if "Success" not in out:
            print("install failed: " + out[:200])
            return 1

    adb("shell", "am force-stop " + PKG)
    remove_flags()
    placed = place_flag(text, a.where, os.path.join(QA, "autoplay_flag_tmp.json"))
    if not placed:
        print("could not write %s: run-as needs a development (debuggable) build, and the external folder must be writable. "
              "Try --where internal / external." % FLAG)
        return 1
    print("autoplay.json placed (%s): %s" % (",".join(placed), text))

    adb("logcat", "-c")
    log = Follower()
    adb("shell", "monkey -p %s -c android.intent.category.LAUNCHER 1" % PKG)
    started = time.time()
    results, shots = [], {}
    shot_threads = []
    printed_header = False
    exit_code = 0
    try:
        while time.time() - started < a.timeout:
            for ev in log.take_events():
                kind = ev.get("ev")
                if kind == "probe":
                    print("persistentDataPath on the phone: %s (flag seen: %s)" % (ev.get("path"), ev.get("exists")))
                elif kind == "start":
                    meta.update(version=ev.get("v"), run=ev.get("run"), step=ev.get("step"))
                    print("run %s started, version %s, %s runs planned" % (ev.get("run"), ev.get("v"), ev.get("jobs")))
                elif kind == "error":
                    print("ERROR from the game: %s" % ev.get("msg"))
                    exit_code = 1
                elif kind == "level":
                    results.append(ev)
                    meta.setdefault("version", ev.get("v"))
                    if not printed_header:
                        print("\n" + table([])); printed_header = True
                    print(table([ev]).split("\n")[2])
                    if ev.get("res") != "cleared":
                        shot = os.path.join(SHOTS, "autoplay_%02d_%s.png" % (int(ev.get("n", 0)), ev.get("att", 1)))
                        t = threading.Thread(target=lambda p=shot: screenshot(p))
                        t.start()
                        shot_threads.append(t)
                        shots[(ev.get("n"), ev.get("att"))] = shot
                elif kind == "summary":
                    print("\nsummary: %s cleared, %s fell, %s timeout of %s runs" % (ev.get("cleared"), ev.get("fell"), ev.get("timeout"), ev.get("total")))
            if log.done:
                break
            if log.crash:
                print("CRASH in logcat: " + log.crash)
                exit_code = 1
                break
            if not results and time.time() - started > 90 and time.time() - log.last_line > 90:
                print("no BTAUTO output within 90 s: is this a development build (Debug.isDebugBuild) and did the flag land in persistentDataPath? "
                      "Look for the 'probe' line above or try --where both.")
                exit_code = 1
                break
            if results and time.time() - log.last_line > 400:
                print("no BTAUTO output for 400 s: the game stopped reporting")
                exit_code = 1
                break
            time.sleep(0.5)
        else:
            print("timeout after %d s" % a.timeout)
            exit_code = 1
    finally:
        for t in shot_threads:
            t.join(timeout=30)
        log.close()

    csv_dest = os.path.join(QA, "autoplay_results_%s.csv" % meta.get("run", "last"))
    got_csv = pull_csv(csv_dest)
    if got_csv:
        merge_csv_rows(results, csv_dest, meta.get("run"))
    remove_flags()
    if exit_code:
        adb("shell", "am force-stop " + PKG)

    print("\n" + table(results))
    write_report(results, shots, opts, meta, a.report)
    print("\nreport: %s" % a.report)
    if got_csv:
        print("csv:    %s" % csv_dest)
    failed = [r for r in results if verdict(r) == "FAIL"]
    expected = len(opts["levels"]) * opts["seeds"]
    if not log.done and not failed:
        exit_code = exit_code or 1
    print("RESULT: %s (%d of %d runs cleared)" % ("PASSED" if not failed and not exit_code and len(results) >= expected else "FAILED",
                                                  len(results) - len(failed), expected))
    return 1 if (failed or exit_code or len(results) < expected) else 0


if __name__ == "__main__":
    sys.exit(main())
