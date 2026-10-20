"""Android test kit for Boing Up: Sky Tower (roadmap T19): a small, Playwright-like driver on top of adb.

The development build contains TestBridge, which logs the visible UI tree with screen rectangles (BTUI) and the game state (BTSTATE) to
logcat. This module reads those lines, finds elements by their text, and drives the phone with adb taps, holds and screenshots.

    from bt import Device
    d = Device(); d.install(); d.launch(clear=True)
    d.wait_screen("Menu"); d.tap("PLAY"); d.wait_screen("Map")
"""
import json
import os
import re
import subprocess
import threading
import time

PKG = "com.jdlabs.boingupskytower"
APK = "E:/Unity/builds/boingupskytower.apk"
SHOTS = "E:/Unity/device_shots"


class TestFailure(AssertionError):
    pass


class Device:
    def __init__(self, serial=None):
        self.serial = serial
        self.state = {}
        self.ui_tree = {"w": 0, "h": 0, "items": []}
        self.state_log = []
        self.crashes = []
        self._proc = None
        self._lock = threading.Lock()
        self._seq = -1
        self._parts = {}
        os.makedirs(SHOTS, exist_ok=True)

    def _base(self):
        return ["adb"] + (["-s", self.serial] if self.serial else [])

    # ---- adb ----
    def adb(self, *args, timeout=120, check=True):
        r = subprocess.run(self._base() + list(args), capture_output=True, timeout=timeout)
        if check and r.returncode != 0:
            raise TestFailure("adb %s failed: %s" % (" ".join(args), r.stderr.decode(errors="replace")[:300]))
        return r.stdout.decode(errors="replace")

    def connected(self):
        out = self.adb("devices", check=False)
        return any(l.strip().endswith("device") for l in out.splitlines()[1:])

    def install(self, apk=APK):
        out = self.adb("install", "-r", apk, timeout=300)
        if "Success" not in out:
            raise TestFailure("install failed: " + out[:200])

    def uninstall(self):
        self.adb("uninstall", PKG, check=False)

    def launch(self, clear=False):
        self.stop()
        if clear:
            self.adb("shell", "pm", "clear", PKG, check=False)
        self.adb("logcat", "-c")
        self._start_log()
        self.adb("shell", "monkey", "-p", PKG, "-c", "android.intent.category.LAUNCHER", "1")

    def stop(self):
        self.adb("shell", "am", "force-stop", PKG, check=False)

    # ---- log reader ----
    def _start_log(self):
        if self._proc:
            self._proc.kill()
        self._proc = subprocess.Popen(self._base() + ["logcat", "-v", "brief"], stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
        threading.Thread(target=self._read_log, daemon=True).start()

    def _read_log(self):
        for raw in iter(self._proc.stdout.readline, b""):
            line = raw.decode(errors="replace")
            m = re.search(r"BTSTATE (\{.*\})", line)
            if m:
                try:
                    s = json.loads(m.group(1))
                    with self._lock:
                        self.state = s
                        self.state_log.append((time.time(), s))
                except ValueError:
                    pass
                continue
            m = re.search(r"BTUI (\{.*\})", line)
            if m:
                try:
                    part = json.loads(m.group(1))
                except ValueError:
                    continue
                with self._lock:
                    if part["seq"] != self._seq:
                        self._seq, self._parts = part["seq"], {}
                    self._parts[part["part"]] = part
                    if len(self._parts) == part["parts"]:
                        items = []
                        for k in sorted(self._parts):
                            items.extend(self._parts[k]["items"])
                        self.ui_tree = {"w": part["w"], "h": part["h"], "items": items}
                continue
            if "FATAL EXCEPTION" in line and PKG in line or ("ANR in " + PKG) in line or ("Fatal signal" in line and PKG in line):
                self.crashes.append(line.strip()[:300])

    # ---- queries ----
    def screen(self):
        with self._lock:
            s = dict(self.state)
        return s.get("screen") if s.get("scene") == "Meta" else s.get("scene")

    def wait(self, predicate, timeout=20.0, what="condition"):
        end = time.time() + timeout
        while time.time() < end:
            if predicate():
                return True
            time.sleep(0.2)
        raise TestFailure("timeout waiting for " + what + "; state=" + json.dumps(self.state)[:300])

    def wait_screen(self, name, timeout=20.0):
        self.wait(lambda: self.screen() == name, timeout, "screen " + name)

    def find(self, text=None, name=None, kind=None):
        with self._lock:
            items = list(self.ui_tree.get("items", []))
        for it in items:
            if kind and it["k"] != kind:
                continue
            if text is not None and text.lower() not in it["t"].lower():
                continue
            if name is not None and it["n"] != name:
                continue
            return it
        return None

    def wait_element(self, text=None, name=None, kind=None, timeout=15.0):
        found = []

        def ok():
            it = self.find(text, name, kind)
            if it:
                found.append(it)
            return bool(it)

        self.wait(ok, timeout, "element %r/%r" % (text, name))
        return found[-1]

    # ---- actions ----
    def tap_xy(self, x, y):
        self.adb("shell", "input", "tap", str(int(x)), str(int(y)))

    def tap(self, text=None, name=None, timeout=15.0):
        it = self.wait_element(text, name, "button", timeout)
        self.tap_xy(it["x"] + it["w"] / 2, it["y"] + it["h"] / 2)
        return it

    def hold(self, text=None, name=None, ms=1000):
        it = self.wait_element(text, name, "button", 5)
        x, y = it["x"] + it["w"] / 2, it["y"] + it["h"] / 2
        self.adb("shell", "input", "swipe", str(int(x)), str(int(y)), str(int(x)), str(int(y)), str(int(ms)))

    def multitouch_hold_and_tap(self, hold_name, tap_name, hold_ms=1500, taps=3):
        """Holds one button while tapping another (a held move button plus Jump taps), using two adb shells in parallel."""
        h = self.wait_element(name=hold_name, kind="button", timeout=5)
        t = self.wait_element(name=tap_name, kind="button", timeout=5)
        hx, hy = h["x"] + h["w"] / 2, h["y"] + h["h"] / 2
        tx, ty = t["x"] + t["w"] / 2, t["y"] + t["h"] / 2
        p = subprocess.Popen(self._base() + ["shell", "input", "swipe", str(int(hx)), str(int(hy)), str(int(hx)), str(int(hy)), str(hold_ms)])
        for _ in range(taps):
            time.sleep(hold_ms / 1000.0 / (taps + 1))
            self.tap_xy(tx, ty)
        p.wait(timeout=30)

    def key(self, code):
        self.adb("shell", "input", "keyevent", str(code))

    def screenshot(self, name):
        path = os.path.join(SHOTS, name + ".png")
        data = subprocess.run(self._base() + ["exec-out", "screencap", "-p"], capture_output=True, timeout=60).stdout
        with open(path, "wb") as f:
            f.write(data)
        return path

    # ---- assertions ----
    def assert_no_crash(self):
        if self.crashes:
            raise TestFailure("crash in logcat: " + self.crashes[0])

    def assert_ui_inside_screen(self):
        with self._lock:
            tree = dict(self.ui_tree)
        w, h = tree.get("w", 0), tree.get("h", 0)
        bad = [i for i in tree.get("items", []) if i["x"] < -2 or i["y"] < -2 or i["x"] + i["w"] > w + 2 or i["y"] + i["h"] > h + 2]
        if bad:
            raise TestFailure("UI outside the screen: " + json.dumps(bad[:3]))

    def assert_touch_targets(self):
        with self._lock:
            tree = dict(self.ui_tree)
        w = tree.get("w", 1080) or 1080
        need = 48 * (w / 411.0) - 2     # a typical phone is about 411 dp wide
        small = [i for i in tree.get("items", []) if i["k"] == "button" and (i["w"] < need or i["h"] < need)]
        if small:
            raise TestFailure("touch targets below 48 dp: " + json.dumps(small[:3]))

    def assert_no_overlap(self):
        with self._lock:
            items = [i for i in self.ui_tree.get("items", []) if i["w"] > 0]

        def inter(a, b):
            ox = min(a["x"] + a["w"], b["x"] + b["w"]) - max(a["x"], b["x"])
            oy = min(a["y"] + a["h"], b["y"] + b["h"]) - max(a["y"], b["y"])
            return ox, oy

        problems = []
        for i in range(len(items)):
            for j in range(i + 1, len(items)):
                a, b = items[i], items[j]
                if a["k"] == b["k"]:
                    ox, oy = inter(a, b)
                    if ox > 14 and oy > 14:
                        problems.append((a["t"], b["t"], int(ox), int(oy)))
        if problems:
            raise TestFailure("overlapping UI: " + json.dumps(problems[:4]))

    def close(self):
        if self._proc:
            self._proc.kill()
