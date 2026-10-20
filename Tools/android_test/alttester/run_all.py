"""Runs the AltTester tests on the phone. Prints PASS / FAIL / SKIP per test and RESULT like smoke_test.py (exit 0 = no failure).

    python Tools/android_test/alttester/run_all.py [--install] [--no-launch] [--only name_part] [--long] [--skip-if-offline]

Prerequisites (README.md): AltTester Desktop is running on the PC, the phone is connected through adb.
--install  installs E:/Unity/builds/skyhopper_alttester.apk first.  --no-launch  uses the game that is already running.
--long  also runs the long tests (region 1 autopilot, up to 45 min); --only region1 runs just that one.
--skip-if-offline  prints SKIP and exits 0 when AltTester Desktop / the alttester build cannot be reached (instead of FAIL)."""
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import altbt  # noqa: E402
from altbt import TestFailure, Skip  # noqa: E402
import tests  # noqa: E402


def main():
    if not altbt.device_connected():
        print("no device connected (adb devices)")
        return 2
    if "--install" in sys.argv:
        altbt.install()
    if "--no-launch" not in sys.argv:
        altbt.launch(clear=True)
    else:
        altbt.forward_ports()
    only = sys.argv[sys.argv.index("--only") + 1] if "--only" in sys.argv else None
    try:
        g = altbt.connect()
        g.find("Play", 40)                  # the menu is up
    except TestFailure as e:
        if "--skip-if-offline" in sys.argv:
            print("SKIP all  AltTester not reachable (%s). For region 1 without AltTester use Tools/android_test/autoplay_r1.py" % str(e)[:120])
            return 0
        print("FAIL connect  %s" % e)
        print("RESULT: FAILED")
        return 1
    failed = skipped = passed = 0
    for fn in tests.TESTS:
        if only and only not in fn.__name__:
            continue
        if getattr(fn, "long", False) and not only and "--long" not in sys.argv:
            print("SKIP %-34s long test, use --long or --only %s" % (fn.__name__, fn.__name__[5:13]))
            skipped += 1
            continue
        t = time.time()
        try:
            fn(g)
            passed += 1
            print("PASS %-34s %.1fs" % (fn.__name__, time.time() - t))
        except Skip as e:
            skipped += 1
            print("SKIP %-34s %s" % (fn.__name__, e))
        except Exception as e:  # noqa: BLE001 - TestFailure or any driver error is a failed test
            failed += 1
            print("FAIL %-34s %s: %s" % (fn.__name__, type(e).__name__, e))
            g.screenshot("FAIL_" + fn.__name__)
            try:
                g.to_menu()                 # keep going with the next test from a known place
            except Exception:  # noqa: BLE001
                print("     (could not get back to the menu; later tests may fail)")
    try:
        g.stop()
    except Exception:  # noqa: BLE001
        pass
    print("passed %d, failed %d, skipped %d" % (passed, failed, skipped))
    print("RESULT:", "PASSED" if not failed else "FAILED")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
