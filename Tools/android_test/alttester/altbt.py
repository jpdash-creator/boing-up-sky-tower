"""AltTester helper for Boing Up: Sky Tower (roadmap T26): connects the Python driver to the alttester build on the phone.

Needs: AltTester Desktop running on the PC (it is the server on port 13000), the alttester APK installed (see README.md).
The game connects OUT to 127.0.0.1:13000 on the phone; "adb reverse" maps that to the PC, where Desktop listens.
The driver connects to the same Desktop on the PC.
"""
import os
import subprocess
import sys
import time

sys.path.insert(0, "E:/pylibs")
from alttester import AltDriver, By  # noqa: E402
from alttester.exceptions import NotFoundException, WaitTimeOutException  # noqa: E402

PKG = "com.jdlabs.boingupskytower"
APK = "E:/Unity/builds/boingupskytower_alttester.apk"
PORT = 13000
APP_NAME = "BoingUpSkyTower"
SHOTS = "E:/Unity/device_shots/alttester"


class TestFailure(AssertionError):
    pass


class Skip(Exception):
    """Raise from a test to mark it skipped (printed as SKIP, not a failure)."""


def adb(*args, check=True, timeout=120):
    r = subprocess.run(["adb"] + list(args), capture_output=True, timeout=timeout)
    if check and r.returncode != 0:
        raise TestFailure("adb %s failed: %s" % (" ".join(args), r.stderr.decode(errors="replace")[:300]))
    return r.stdout.decode(errors="replace")


def device_connected():
    return any(l.strip().endswith("device") for l in adb("devices", check=False).splitlines()[1:])


def install(apk=APK):
    out = adb("install", "-r", apk, timeout=300)
    if "Success" not in out:
        raise TestFailure("install failed: " + out[:200])


def forward_ports():
    """Phone 127.0.0.1:13000 -> PC 127.0.0.1:13000 (AltTester Desktop)."""
    adb("reverse", "tcp:%d" % PORT, "tcp:%d" % PORT)


def launch(clear=False):
    adb("shell", "am", "force-stop", PKG, check=False)
    if clear:
        adb("shell", "pm", "clear", PKG, check=False)
    forward_ports()
    adb("shell", "monkey", "-p", PKG, "-c", "android.intent.category.LAUNCHER", "1")


def connect(timeout=90):
    """Retries until the game has registered with Desktop (the build needs a few seconds after launch)."""
    end = time.time() + timeout
    last = None
    while time.time() < end:
        try:
            return Game(AltDriver(host="127.0.0.1", port=PORT, app_name=APP_NAME, timeout=10))
        except Exception as e:  # noqa: BLE001 - the driver raises several connection errors while the app is still starting
            last = e
            time.sleep(2)
    raise TestFailure("could not connect to the game through AltTester Desktop within %ds: %s. "
                      "Is Desktop running, is the alttester APK installed, did you launch the game?" % (timeout, last))


class Game:
    """Thin wrapper with waiting finders. Objects are found by the names the UI code gives them (see README.md)."""

    def __init__(self, driver):
        self.d = driver
        os.makedirs(SHOTS, exist_ok=True)

    def stop(self):
        self.d.stop()

    def find(self, name, timeout=10):
        try:
            return self.d.wait_for_object(By.NAME, name, timeout=timeout, interval=0.4)
        except WaitTimeOutException:
            raise TestFailure("object '%s' not found within %ss" % (name, timeout))

    def exists(self, name, timeout=0.0):
        if timeout <= 0:
            try:
                self.d.find_object(By.NAME, name)
                return True
            except NotFoundException:
                return False
        try:
            self.d.wait_for_object(By.NAME, name, timeout=timeout, interval=0.4)
            return True
        except WaitTimeOutException:
            return False

    def tap(self, name, timeout=10):
        self.find(name, timeout).tap()
        time.sleep(0.4)

    def text_of(self, name):
        return self.find(name).find_object_from_object(By.NAME, "Label").get_text()

    def scene(self):
        return self.d.get_current_scene()

    def wait_scene(self, scene, timeout=30):
        try:
            self.d.wait_for_current_scene_to_be(scene, timeout=timeout, interval=0.5)
        except WaitTimeOutException:
            raise TestFailure("scene '%s' not reached (now '%s')" % (scene, self.scene()))

    def screen_size(self):
        w, h = self.d.get_application_screensize()
        return w, h

    def screenshot(self, name):
        try:
            self.d.get_png_screenshot(os.path.join(SHOTS, name + ".png"))
        except Exception:  # noqa: BLE001 - a missing screenshot must never hide the real failure
            pass

    def to_menu(self):
        """Gets back to the main menu from wherever a test left the game (popups, Back arrows, the Run overlay)."""
        for _ in range(8):
            if self.exists("Play"):
                return
            if self.exists("Level select"):
                self.tap("Level select")
                self.wait_scene("Meta", 20)
                time.sleep(1.0)
                continue
            if self.exists("Close"):
                self.tap("Close")
                continue
            if self.exists("Back"):
                self.tap("Back")
                continue
            time.sleep(1.0)
        raise TestFailure("could not get back to the main menu")
