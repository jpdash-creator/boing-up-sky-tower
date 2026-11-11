"""Runs the 8 store screenshot scenes (3 seeds each) through store_capture.py. Output: E:/Unity/store_capture/shots/<scene>/seed<N>/"""
import subprocess, sys, os
SCENES = [
    ("s1_momo_meadow_l3",      1, 3,  "momo",         "30-40"),
    ("s2_cloudknight_l5",      1, 5,  "momo_r",       "30-40"),
    ("s3_sunlord_l25",         1, 25, "momo_l",       "90-100"),
    ("s4_capi_grove_l5",       2, 5,  "forestgirl",   "40-50"),
    ("s5_grovequeen_l21",      2, 21, "forestgirl_l", "70-80"),
    ("s6_otto_beach_l3",       3, 3,  "beachotter",   "30-40"),
    ("s7_bonbon_candy_l6",     4, 6,  "candygirl",    "30-40"),
    ("s8_bao_bamboo_l8",       5, 8,  "monk",         "30-40"),
]
only = sys.argv[1:] 
for name, region, level, hero, rows in SCENES:
    if only and name.split("_")[0] not in only: continue
    for seed in (1, 2, 3):
        out = "E:/Unity/store_capture/shots/%s/seed%d" % (name, seed)
        os.makedirs(out, exist_ok=True)
        print("RUN", name, seed, flush=True)
        subprocess.run([sys.executable, "-u", "Tools/android_test/store_capture.py", "shot", "f", "--region", str(region), "--level", str(level),
                        "--hero", hero, "--rows", rows, "--skill", "0", "--difficulty", "1", "--seed", str(seed), "--out", out],
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
print("BATCH DONE", flush=True)
