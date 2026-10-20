"""Build version of Sky Hopper: vMAJOR.MINOR.HOTFIX.BUILD (see .claude/rules/versioning.md).

    python Tools/version.py show
    python Tools/version.py bump minor      # MINOR+1, HOTFIX back to 0 (BUILD is never reset)
    python Tools/version.py bump hotfix     # HOTFIX+1
    python Tools/version.py bump major      # MAJOR+1, MINOR and HOTFIX back to 0
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
    return "v%d.%d.%d.%d" % (v["major"], v["minor"], v["hotfix"], v["build"])


def main():
    v = load()
    if len(sys.argv) >= 2 and sys.argv[1] == "bump":
        part = sys.argv[2]
        if part == "major":
            v["major"] += 1; v["minor"] = 0; v["hotfix"] = 0
        elif part == "minor":
            v["minor"] += 1; v["hotfix"] = 0
        elif part == "hotfix":
            v["hotfix"] += 1
        else:
            raise SystemExit("bump major|minor|hotfix")
        save(v)
    print(name(v))


if __name__ == "__main__":
    main()
