"""Store listing capture on the phone (dev build only): one scene = one hero/level/powers setup, results pulled to a folder.

    python Tools/android_test/store_capture.py shot  <name> --region 1 --level 3 --hero momo --rows 30-40 [--out DIR]
    python Tools/android_test/store_capture.py video <name> --region 1 --level 5 --hero momo_r --seconds 30 --powers 3 [--out DIR]

Uses autoplay.json (AutopilotBootstrap) with the capture fields (StoreCapture.cs). The phone must have System Test on (Settings, dev rows).
Screenshots are written by the game at the screen resolution into files/shots; video uses adb screenrecord (30 s per call).
"""
import argparse, json, os, subprocess, sys, time

PKG = "com.jdlabs.fluffyclimbers"
SERIAL = os.environ.get("ANDROID_SERIAL", "96355505")
FLAG = "autoplay.json"
EXT = "/sdcard/Android/data/%s/files" % PKG


def adb(*args, data=None, timeout=300):
    r = subprocess.run(["adb", "-s", SERIAL] + list(args), capture_output=True, timeout=timeout, input=data)
    return r.stdout.decode(errors="replace")


def push_flag(opts):
    tmp = os.path.join(os.environ.get("TEMP", "."), FLAG)
    open(tmp, "w", encoding="utf-8").write(json.dumps(opts))
    adb("shell", "mkdir -p " + EXT)
    adb("push", tmp, EXT + "/" + FLAG)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("mode", choices=["shot", "video"])
    ap.add_argument("name")
    ap.add_argument("--region", type=int, default=1)
    ap.add_argument("--level", type=int, required=True)
    ap.add_argument("--hero", default="")
    ap.add_argument("--rows", default="")
    ap.add_argument("--seconds", type=float, default=0)
    ap.add_argument("--powers", type=int, default=0)
    ap.add_argument("--use-powers", action="store_true")
    ap.add_argument("--power-every", type=float, default=2.6)
    ap.add_argument("--skill", type=float, default=0.25)
    ap.add_argument("--difficulty", type=int, default=2)
    ap.add_argument("--coins", type=int, default=12850)
    ap.add_argument("--gems", type=int, default=465)
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--out", default="E:/Unity/store_capture")
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    opts = {"region": a.region, "levels": [a.level], "skill": a.skill, "difficulty": a.difficulty, "seedBase": a.seed,
            "holdFail": 1.0, "holdClear": 1.0, "maxSeconds": 400,
            "capture": True, "hero": a.hero, "powerUnits": a.powers, "usePowers": a.use_powers or a.mode == "video",
            "powerEverySec": a.power_every, "frames": a.mode == "video", "shotEverySec": 0.8, "walletCoins": a.coins, "walletGems": a.gems, "shotPrefix": a.name}
    if a.rows:
        lo, hi = a.rows.split("-")
        opts["shotRowFrom"], opts["shotRowTo"] = int(lo), int(hi)
    if a.seconds:
        opts["runSeconds"] = a.seconds
    adb("shell", "rm -rf %s/shots %s/%s" % (EXT, EXT, FLAG))
    adb("shell", "am force-stop " + PKG)
    adb("logcat", "-c")
    push_flag(opts)
    adb("shell", "monkey -p %s -c android.intent.category.LAUNCHER 1" % PKG)
    rec = None
    t0 = time.time()
    log = subprocess.Popen(["adb", "-s", SERIAL, "logcat", "-s", "Unity:I"], stdout=subprocess.PIPE, text=True, errors="replace")
    lines = []
    started = False
    while time.time() - t0 < 600:
        line = log.stdout.readline()
        if not line:
            break
        if "BTAUTO" in line:
            lines.append(line.strip())
            print(line.strip()[-200:])
            if '"ev":"cap_shot"' in line:
                import re
                m = re.search(r'"row":(\d+),"gems":(\d+),"stars":(\d+)', line)
                if m:
                    fn = os.path.join(a.out, "%s_%03d_r%s_g%s_s%s.png" % (a.name, len(lines), m.group(1), m.group(2), m.group(3)))
                    data = subprocess.run(["adb", "-s", SERIAL, "exec-out", "screencap", "-p"], capture_output=True).stdout
                    open(fn, "wb").write(data)
            if "BTAUTO done" in line:
                break
    log.terminate()
    if a.mode == "video":
        fdir = os.path.join(a.out, a.name + "_frames")
        os.makedirs(fdir, exist_ok=True)
        subprocess.run(["adb", "-s", SERIAL, "pull", EXT + "/frames/.", fdir], capture_output=True)
        import imageio_ffmpeg
        subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(), "-y", "-framerate", "30", "-i", os.path.join(fdir, "f%05d.jpg"), "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "18", "-preset", "medium", os.path.join(a.out, a.name + ".mp4")], capture_output=True)
    shots = adb("shell", "ls %s/shots" % EXT).split(); print("SHOTS", shots[:3], len(shots))
    for f in shots:
        print(adb("pull", "%s/shots/%s" % (EXT, f), os.path.join(a.out, f)).strip()[:120])
    open(os.path.join(a.out, a.name + "_log.txt"), "w", encoding="utf-8").write("\n".join(lines))
    print("saved", len(shots), "shots to", a.out)


if __name__ == "__main__":
    main()
