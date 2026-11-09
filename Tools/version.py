"""Build version of Boing Up: Sky Tower: vMAJOR.MINOR.BUILD, example v0.52.68 (see .claude/rules/versioning.md).

    python Tools/version.py show
    python Tools/version.py bump minor      # MINOR+1 (BUILD is never reset)
    python Tools/version.py bump major      # MAJOR+1, MINOR back to 0 (BUILD is never reset)
BUILD is incremented by the Android build itself (Assets/Editor/Build/AndroidBuild.cs), once per produced APK, and never reset.
"""
import json
import os
import sys

PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "version.json")


def load():
    with open(PATH, encoding="utf-8") as f:
        return json.load(f)


def save(v):
    with open(PATH, "w", encoding="utf-8", newline="\n") as f:
        json.dump(v, f, indent=2)
        f.write("\n")


def name(v):
    return "v%d.%d.%d" % (v["major"], v["minor"], v["build"])


def main():
    v = load()
    v.pop("hotfix", None)   # the old vX.Y.Z.B scheme had a hotfix part; it is dropped
    if len(sys.argv) >= 2 and sys.argv[1] == "bump":
        part = sys.argv[2]
        if part == "major":
            v["major"] += 1; v["minor"] = 0
        elif part == "minor":
            v["minor"] += 1
        else:
            raise SystemExit("bump major|minor")
        v.pop("hotfix", None)
        save(v)
    print(name(v))


if __name__ == "__main__":
    main()
