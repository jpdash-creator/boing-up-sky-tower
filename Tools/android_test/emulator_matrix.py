"""Emulator matrix: boots each Android emulator (AVD) one at a time, installs a development APK, runs a screenshot tour of every
screen, collects logcat problems and BTUI layout-rule violations, kills the emulator and writes a markdown report.

    python Tools/android_test/emulator_matrix.py                       # the 5 emu_* AVDs, default APK
    python Tools/android_test/emulator_matrix.py --avd emu_720x1280_9x16 --apk E:/Unity/builds/x.apk
    python Tools/android_test/emulator_matrix.py --skip-boot-wait      # an emulator for the AVD is already running on 5554

Disk-safe: AVDs are created on demand (one at a time, >= 8 GB free on E: required) and deleted after their test.
    python Tools/android_test/emulator_matrix.py --keep-first   # delete all but emu_720x1280_9x16   (--keep keeps all)
    python Tools/android_test/emulator_matrix.py --dry-run      # print create/delete commands only

Output: E:/Unity/device_shots/emulators/<avd>/*.png (+ _tour.log, _logcat.txt) and E:/Unity/qa/emulator_report_<timestamp>.md.
Needs the development build (TestBridge prints BTUI/BTSTATE) built with x86_64. Never talks to a real phone: only an emulator-NNNN serial is used.
"""
import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bt  # noqa: E402
from bt import Device, TestFailure, PKG  # noqa: E402

SDK = os.environ.get("ANDROID_SDK_ROOT", "E:/dev-tools/android-sdk")
AVD_HOME = os.environ.get("ANDROID_AVD_HOME", "E:/dev-tools/avd")
EMULATOR = os.path.join(SDK, "emulator", "emulator.exe")
ADB = os.path.join(SDK, "platform-tools", "adb.exe")
JAVA_HOME = os.environ.get("JAVA_HOME") or "E:/Unity/Hub/Editor/6000.3.26f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK"
AVDMANAGER = os.path.join(SDK, "cmdline-tools", "latest", "bin", "avdmanager.bat")
SYSTEM_IMAGE = "system-images;android-34;google_apis;x86_64"
SAFE_AVD_ROOT = "E:/dev-tools/avd"   # the delete code refuses anything outside this folder
MIN_FREE_GB = 8                      # each AVD is ~5 GB; stop before creating one with less free space on E:
# name -> (width, height, dpi)
PROFILES = {
    "emu_720x1280_9x16": (720, 1280, 320),
    "emu_1080x1920_9x16": (1080, 1920, 420),
    "emu_1080x2400_9x20": (1080, 2400, 420),
    "emu_1080x2520_9x21": (1080, 2520, 420),
    "emu_1536x2048_3x4": (1536, 2048, 320),
}
DEFAULT_AVD = "emu_720x1280_9x16"
DEFAULT_AVDS = ["emu_720x1280_9x16", "emu_1080x1920_9x16", "emu_1080x2400_9x20", "emu_1080x2520_9x21", "emu_1536x2048_3x4"]
DEFAULT_APK = "E:/Unity/builds/boingupskytower_v0.42.0.48_x86.apk"
OUT_ROOT = "E:/Unity/device_shots/emulators"
QA_DIR = "E:/Unity/qa"
PORT = 5554
SERIAL = "emulator-%d" % PORT
PHONE_SERIAL = "96355505"
MAX_SECONDS = 12 * 60   # budget per emulator


def _norm(p):
    return os.path.normcase(os.path.normpath(os.path.abspath(p)))


def tool_env():
    return dict(os.environ, ANDROID_SDK_ROOT=SDK, ANDROID_AVD_HOME=AVD_HOME, JAVA_HOME=JAVA_HOME)


def run_cmd(cmd, dry, **kw):
    """Run (or with --dry-run only print) a command; returns the CompletedProcess or None."""
    print("CMD%s: %s" % (" (dry-run, not executed)" if dry else "", " ".join('"%s"' % c if " " in c else c for c in cmd)), flush=True)
    if dry:
        return None
    return subprocess.run(cmd, env=tool_env(), capture_output=True, timeout=300, **kw)


def free_gb(path="E:/"):
    return shutil.disk_usage(path).free / (1024 ** 3)


def avd_paths(name):
    return os.path.join(AVD_HOME, name + ".avd"), os.path.join(AVD_HOME, name + ".ini")


def set_config_values(text, values):
    """Replace key=value lines in an AVD config.ini (append missing keys)."""
    lines, seen = [], set()
    for l in text.splitlines():
        k = l.split("=", 1)[0].strip()
        if "=" in l and k in values:
            lines.append("%s=%s" % (k, values[k]))
            seen.add(k)
        else:
            lines.append(l)
    lines += ["%s=%s" % (k, v) for k, v in values.items() if k not in seen]
    return "\n".join(lines) + "\n"


def create_avd(name, dry):
    """Create the AVD for a profile. avdmanager prints a devices.xml error but still creates the .avd folder, so the
    return code is ignored and the folder is what counts; then config.ini is rewritten (lcd size/density, ram, no frame)."""
    w, h, dpi = PROFILES[name]
    avd_dir, _ = avd_paths(name)
    if os.path.isdir(avd_dir):
        print("AVD %s already exists, reusing" % name)
        return
    free = free_gb()
    if free < MIN_FREE_GB:
        raise SystemExit("STOP: only %.1f GB free on E: (need >= %d GB to create %s). Free disk space and run again." % (free, MIN_FREE_GB, name))
    r = run_cmd([AVDMANAGER, "create", "avd", "-n", name, "-k", SYSTEM_IMAGE, "-d", "pixel", "--force"], dry, input=b"no\n")
    values = {"hw.lcd.width": w, "hw.lcd.height": h, "hw.lcd.density": dpi, "hw.ramSize": 3072, "showDeviceFrame": "no"}
    if dry:
        print("CONFIG (dry-run): would rewrite %s with %s" % (os.path.join(avd_dir, "config.ini"), values))
        return
    if not os.path.isdir(avd_dir):   # the devices.xml error is harmless, a missing folder is not
        raise RuntimeError("avdmanager did not create %s: %s" % (avd_dir, (r.stdout + r.stderr).decode(errors="replace")[-400:]))
    cfg = os.path.join(avd_dir, "config.ini")
    text = open(cfg, encoding="utf-8").read() if os.path.exists(cfg) else ""
    open(cfg, "w", encoding="utf-8", newline="\n").write(set_config_values(text, values))
    print("AVD %s created and config.ini set (%dx%d @%ddpi)" % (name, w, h, dpi))


def delete_avd(name, dry):
    """Delete exactly <AVD_HOME>/<name>.avd and <name>.ini. Exact full-name match only (the folder name includes '.avd'),
    only known profile names, only inside E:/dev-tools/avd."""
    if name not in PROFILES:
        raise RuntimeError("refusing to delete %r: not a known test profile" % name)
    if _norm(AVD_HOME) != _norm(SAFE_AVD_ROOT):
        raise RuntimeError("refusing to delete: AVD home %s is not %s" % (AVD_HOME, SAFE_AVD_ROOT))
    avd_dir, ini = avd_paths(name)
    root = _norm(SAFE_AVD_ROOT)
    for p, want in ((avd_dir, name + ".avd"), (ini, name + ".ini")):
        if os.path.basename(p) != want or os.path.dirname(_norm(p)) != root or os.path.islink(p):
            raise RuntimeError("refusing to delete %s: not exactly %s inside %s" % (p, want, SAFE_AVD_ROOT))
    print("DELETE%s: %s and %s" % (" (dry-run, not executed)" if dry else "", avd_dir, ini), flush=True)
    if dry:
        return
    if os.path.isdir(avd_dir):
        shutil.rmtree(avd_dir)
    if os.path.isfile(ini):
        os.remove(ini)


def adb(*args, timeout=120):
    assert PHONE_SERIAL not in " ".join(args)
    r = subprocess.run([ADB, "-s", SERIAL] + list(args), capture_output=True, timeout=timeout)
    return r.stdout.decode(errors="replace")


def boot(avd, skip_wait, gpu):
    env = dict(os.environ, ANDROID_SDK_ROOT=SDK, ANDROID_AVD_HOME=AVD_HOME)
    proc = None
    if not skip_wait:
        proc = subprocess.Popen([EMULATOR, "-avd", avd, "-port", str(PORT), "-no-snapshot", "-no-audio", "-no-boot-anim",
                                 "-gpu", gpu], env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    t0 = time.time()
    subprocess.run([ADB, "-s", SERIAL, "wait-for-device"], timeout=240)
    while time.time() - t0 < 300:
        if adb("shell", "getprop", "sys.boot_completed").strip() == "1":
            break
        time.sleep(3)
    else:
        raise RuntimeError("boot timeout")
    adb("shell", "settings", "put", "secure", "immersive_mode_confirmations", "confirmed")   # no "Viewing full screen" hint
    adb("shell", "settings", "put", "global", "hide_error_dialogs", "1")   # a busy PC makes the launcher ANR; the dialog would cover the game
    adb("shell", "input", "keyevent", "82")
    time.sleep(25)   # let the system settle after boot (launcher, gms) before the game starts
    return proc, time.time() - t0


def kill(proc):
    subprocess.run([ADB, "-s", SERIAL, "emu", "kill"], capture_output=True, timeout=30)
    if proc:
        try:
            proc.wait(timeout=40)
        except subprocess.TimeoutExpired:
            proc.kill()
    time.sleep(3)


class Tour:
    def __init__(self, avd):
        self.avd = avd
        bt.SHOTS = os.path.join(OUT_ROOT, avd)
        os.makedirs(bt.SHOTS, exist_ok=True)
        self.d = Device(SERIAL)
        self.shots, self.violations, self.fails = [], [], []
        self.log = open(os.path.join(bt.SHOTS, "_tour.log"), "w", encoding="utf-8")

    def P(self, *a):
        s = " ".join(str(x) for x in a)
        print(s, flush=True)
        self.log.write(s + "\n")
        self.log.flush()

    def check(self, name):
        d = self.d
        time.sleep(1.5)
        d.screenshot(name)
        self.shots.append(name + ".png")
        t = d.ui_tree
        self.P("SHOT", name, "screen=", d.screen(), "fps=", d.state.get("fps"), "items=", len(t["items"]))
        for fn in (d.assert_ui_inside_screen, d.assert_touch_targets, d.assert_no_overlap):
            try:
                fn()
            except TestFailure as e:
                msg = "%s %s: %s" % (name, fn.__name__, str(e)[:400])
                self.violations.append(msg)
                self.P("  BTUI-VIOLATION", msg)

    def step(self, label, fn):
        try:
            fn()
            self.P("ok", label)
        except Exception as e:  # keep touring: later steps recover through back_to_menu
            self.fails.append("%s: %s %s" % (label, type(e).__name__, str(e)[:160]))
            self.P("STEPFAIL", self.fails[-1])
            try:
                self.d.screenshot("FAIL_" + label)
                self.shots.append("FAIL_%s.png" % label)
            except Exception:
                pass

    def tap(self, **k):
        self.d.tap(**k)
        time.sleep(1.2)

    def close_popup(self):
        for n in ("CloseX", "Close"):
            if self.d.find(name=n):
                self.d.tap(name=n)
                time.sleep(1)
                return

    def back_to_menu(self):
        for _ in range(6):
            if self.d.screen() == "Menu":
                return
            self.close_popup()
            if self.d.find(name="Back"):
                self.d.tap(name="Back")
                time.sleep(1.5)

    def goto_map(self):
        self.back_to_menu()
        self.tap(name="TabPlay")
        self.d.wait_screen("Map", 10)

    def run(self):
        d = self.d
        d.launch(clear=True)
        time.sleep(5)
        d.screenshot("01_boot")
        self.shots.append("01_boot.png")
        d.wait_screen("Menu", 240)
        time.sleep(3)
        self.check("02_home")

        def settings():
            self.tap(name="Settings"); d.wait_screen("Settings", 10); self.check("03_settings"); self.back_to_menu()

        def shop():
            self.tap(name="TabShop"); d.wait_screen("Shop", 10); self.check("04_shop_default")
            for i, (n, f) in enumerate([("TabBundles", "bundles"), ("TabChests", "chests"), ("TabPower Ups", "powerups"), ("TabCoins", "coins"), ("TabGems", "gems")]):
                self.step("shoptab_" + f, lambda n=n, f=f, i=i: (self.tap(name=n), self.check("05_shop_%d_%s" % (i + 1, f))))
            self.close_popup()

        def vip():
            self.back_to_menu(); self.tap(name="TabVIP"); d.wait_screen("Subscription", 10); self.check("06_vip")

        def heroes():
            self.back_to_menu(); self.tap(name="TabCharacters"); d.wait_screen("Characters", 10); self.check("07_heroes")
            self.tap(name="HeroInfo"); self.check("07b_hero_details")

        def mapscr():
            self.goto_map(); self.check("08_map")

        def rewards():
            self.goto_map()
            self.tap(name="Rewards"); d.wait_screen("Rewards", 10); self.check("09_rewards_main")
            self.tap(name="TabDaily"); self.check("09_rewards_daily")
            self.tap(name="TabWeekly"); self.check("09_rewards_weekly")

        def levels():
            self.goto_map()
            self.tap(name="Region1"); d.wait_screen("Levels", 10); self.check("10_levels")
            self.tap(name="Level1"); self.check("11_level_popup")

        def run_level():
            self.tap(text="PLAY")
            d.wait(lambda: d.state.get("scene") == "Run", 60, "Run scene")
            d.wait(lambda: not d.state.get("intro", True), 30, "intro end")
            time.sleep(0.5)
            d.screenshot("12_run_start"); self.shots.append("12_run_start.png")
            self.P("RUNSTATE", json.dumps(d.state))
            for _ in range(2):
                for pad in ("PadRight", "PadLeft"):
                    if d.state.get("over"):
                        break   # fell: the end overlay is captured below
                    d.multitouch_hold_and_tap(pad, "PadJump", hold_ms=1400, taps=3)
            self.check("13_run_playing")
            self.P("RUNSTATE", json.dumps(d.state))
            if d.state.get("over") or d.state.get("overlay"):
                time.sleep(1); self.check("14_run_end_overlay")
            else:
                self.tap(name="Pause"); time.sleep(1); self.check("14_run_pause")

        for label, fn in (("settings", settings), ("shop", shop), ("vip", vip), ("heroes", heroes), ("map", mapscr),
                          ("rewards", rewards), ("levels", levels), ("run", run_level)):
            self.step(label, fn)
        self.crashes = list(d.crashes)
        d.close()


def logcat_issues(avd):
    raw = subprocess.run([ADB, "-s", SERIAL, "logcat", "-d", "-v", "time"], capture_output=True, timeout=120).stdout.decode(errors="replace")
    open(os.path.join(OUT_ROOT, avd, "_logcat.txt"), "w", encoding="utf-8").write(raw)
    pat = re.compile(r"FATAL EXCEPTION|ANR in |Fatal signal|NullReferenceException|IndexOutOfRange|Unity\s*:.*(Exception|error CS)|Unity\s*:\s*ERROR|Unity\s*:\s*\[?Error")
    seen, out = set(), []
    for l in raw.splitlines():
        if "boingup" not in l.lower() and "Unity" not in l:
            continue
        if pat.search(l):
            k = re.sub(r"^\S+ \S+ ", "", l)[:120]
            if k not in seen:
                seen.add(k)
                out.append(l[:260])
    return out[:25]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--avd", action="append", help="AVD name (repeatable); default: the 5 emu_* AVDs")
    ap.add_argument("--apk", default=DEFAULT_APK)
    ap.add_argument("--gpu", default="swiftshader_indirect", help="emulator -gpu mode (host is faster if the PC GPU driver works)")
    ap.add_argument("--retries", type=int, default=1, help="extra attempts for an AVD whose tour fails to reach the menu")
    ap.add_argument("--skip-boot-wait", action="store_true", help="do not start/stop an emulator; use the one already running on %s" % SERIAL)
    ap.add_argument("--keep", action="store_true", help="do not delete AVDs after testing (default: each AVD is deleted before the next is created)")
    ap.add_argument("--keep-first", action="store_true", help="delete every AVD after its test except %s, which stays on disk" % DEFAULT_AVD)
    ap.add_argument("--dry-run", action="store_true", help="print the create/delete commands for the chosen AVDs; execute nothing, boot nothing")
    a = ap.parse_args()
    avds = a.avd or DEFAULT_AVDS
    if a.dry_run:
        for avd in avds:
            if avd in PROFILES and not a.skip_boot_wait:
                create_avd(avd, True)
                print("TEST (dry-run): boot %s, install, tour, kill emulator" % avd)
                if not a.keep and not (a.keep_first and avd == DEFAULT_AVD):
                    delete_avd(avd, True)
        return 0
    stamp = time.strftime("%Y%m%d_%H%M%S")
    rep = ["# Emulator matrix report %s" % stamp, "", "APK: `%s`" % a.apk, "Command: `%s`" % " ".join(sys.argv), ""]
    summary = []
    for avd in avds:
        t0 = time.time()
        res = {"avd": avd, "result": "OK", "notes": []}
        proc = None
        tour = None
        try:
            if avd in PROFILES and not a.skip_boot_wait:
                create_avd(avd, False)   # SystemExit if E: has < 8 GB free
            proc, bsec = boot(avd, a.skip_boot_wait, a.gpu)
            res["boot_s"] = int(bsec)
            if adb("install", "-r", a.apk, timeout=300).find("Success") < 0:
                raise RuntimeError("install failed")
            for attempt in range(a.retries + 1):
                tour = Tour(avd)
                try:
                    tour.run()
                    break
                except Exception as e:   # no menu: save evidence, then retry
                    logcat_issues(avd)
                    if attempt == a.retries:
                        raise
                    res["notes"].append("attempt %d failed: %s" % (attempt + 1, str(e)[:120]))
                    adb("shell", "am", "force-stop", PKG)
            res["logcat"] = logcat_issues(avd)
            res["crashes"] = tour.crashes
            if tour.crashes:
                res["result"] = "CRASH"
            elif tour.fails:
                res["result"] = "STEPS FAILED"
            elif tour.violations:
                res["result"] = "LAYOUT VIOLATIONS"
        except Exception as e:
            try:
                logcat_issues(avd)
            except Exception:
                pass
            res["result"] = "ERROR"
            res["notes"].append("%s: %s" % (type(e).__name__, e))
        finally:
            try:
                if not a.skip_boot_wait:
                    kill(proc)
            except Exception:
                pass
            if avd in PROFILES and not a.skip_boot_wait and not a.keep and not (a.keep_first and avd == DEFAULT_AVD):
                try:
                    delete_avd(avd, False)   # free the 5 GB before the next AVD is created
                except Exception as e:
                    print("WARNING: AVD delete failed: %s" % e, flush=True)
        res["secs"] = int(time.time() - t0)
        res["shots"] = tour.shots if tour else []
        res["violations"] = tour.violations if tour else []
        res["fails"] = tour.fails if tour else []
        summary.append(res)
    rep.append("| AVD | result | shots | BTUI violations | failed steps | logcat issues | secs |")
    rep.append("|---|---|---|---|---|---|---|")
    for r in summary:
        rep.append("| %s | %s | %d | %d | %d | %d | %d |" % (r["avd"], r["result"], len(r["shots"]), len(r["violations"]), len(r["fails"]), len(r.get("logcat", [])), r["secs"]))
    for r in summary:
        rep += ["", "## %s - %s" % (r["avd"], r["result"]), "", "Screenshots: `%s/%s/` (%s)" % (OUT_ROOT, r["avd"], ", ".join(r["shots"]))]
        for title, key in (("Errors", "notes"), ("Failed steps", "fails"), ("BTUI rule violations", "violations"), ("Crashes", "crashes"), ("Logcat issues", "logcat")):
            if r.get(key):
                rep += ["", "**%s**" % title] + ["- `%s`" % x.replace("`", "'") for x in r[key]]
    rep += ["", "Visual review (clipping, overlap, safe area) is not automated: open the screenshots."]
    os.makedirs(QA_DIR, exist_ok=True)
    path = os.path.join(QA_DIR, "emulator_report_%s.md" % stamp)
    open(path, "w", encoding="utf-8").write("\n".join(rep) + "\n")
    print("REPORT", path)
    return 0 if all(r["result"] in ("OK", "LAYOUT VIOLATIONS") for r in summary) else 1


if __name__ == "__main__":
    sys.exit(main())
