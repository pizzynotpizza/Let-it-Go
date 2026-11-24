"""
Let it Go - copy a portable Python with MediaPipe into a Windows build.

    py Tools/bundle_hand_tracker.py "Builds/Windows"

Result inside the build folder:
    HandTracker/python/        Python 3.13 + mediapipe, opencv, numpy (no install needed)
    Start Hand Tracker.bat     starts the tracker (the player then opens the game)

The base Python comes from HandTrackerEnv/pyvenv.cfg ("home"), the packages from
HandTrackerEnv/Lib/site-packages. Run "Start Hand Tracker.bat" in the project once
first, so that HandTrackerEnv exists. Unity runs this script after every Windows
build (Assets/Scripts/Editor/HandTrackerBuildBundle.cs).
"""

import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(HERE)
VENV = os.path.join(PROJECT, "HandTrackerEnv")

# Parts of the base Python the tracker does not need.
SKIP_TOP = {"Doc", "include", "libs", "Scripts", "tcl", "__install__.json"}
SKIP_LIB = {"site-packages", "test", "idlelib", "tkinter", "turtledemo", "ensurepip"}
SKIP_PACKAGES = {"pip"}

LAUNCHER = r"""@echo off
rem Let it Go - start the hand tracker. Open "Let it Go.exe" yourself after the camera window shows.
rem Keep the camera window open while you play. Esc in the camera window stops the tracker.
chcp 65001 >nul
cd /d "%~dp0"
title Let it Go - Hand Tracker
if not exist "HandTracker\python\python.exe" (
    echo HandTracker\python is missing. Copy the whole build folder.
    pause
    exit /b 1
)
echo Starting the hand tracker...
echo When the camera window shows, open "Let it Go.exe" to play.
echo Keep this window and the camera window open while you play.
cd /d "%~dp0Let it Go_Data\StreamingAssets\HandTracker"
"%~dp0HandTracker\python\python.exe" -E -s hand_tracker.py --no-heartbeat --preview %*
if errorlevel 1 pause
"""


def ignore_pycache(_dir, names):
    return [n for n in names if n == "__pycache__"]


def base_python_home():
    cfg = os.path.join(VENV, "pyvenv.cfg")
    with open(cfg, encoding="utf-8") as f:
        for line in f:
            key, _, value = line.partition("=")
            if key.strip() == "home":
                return value.strip()
    raise SystemExit("pyvenv.cfg has no 'home' line: " + cfg)


def main():
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)
    build = os.path.abspath(sys.argv[1])
    home = base_python_home()
    site = os.path.join(VENV, "Lib", "site-packages")
    for path in (home, site):
        if not os.path.isdir(path):
            raise SystemExit("Missing: " + path + " (run 'Start Hand Tracker.bat' in the project first)")

    dest = os.path.join(build, "HandTracker", "python")
    if os.path.isdir(dest):
        shutil.rmtree(dest)
    os.makedirs(dest)

    for name in os.listdir(home):
        if name in SKIP_TOP:
            continue
        src = os.path.join(home, name)
        if name == "Lib":
            os.makedirs(os.path.join(dest, "Lib"))
            for lib in os.listdir(src):
                if lib in SKIP_LIB or lib == "__pycache__":
                    continue
                s = os.path.join(src, lib)
                d = os.path.join(dest, "Lib", lib)
                shutil.copytree(s, d, ignore=ignore_pycache) if os.path.isdir(s) else shutil.copy2(s, d)
        elif os.path.isdir(src):
            shutil.copytree(src, os.path.join(dest, name), ignore=ignore_pycache)
        else:
            shutil.copy2(src, os.path.join(dest, name))

    dest_site = os.path.join(dest, "Lib", "site-packages")
    os.makedirs(dest_site)
    for name in os.listdir(site):
        if name == "__pycache__" or name in SKIP_PACKAGES or name.startswith("pip-"):
            continue
        s = os.path.join(site, name)
        d = os.path.join(dest_site, name)
        shutil.copytree(s, d, ignore=ignore_pycache) if os.path.isdir(s) else shutil.copy2(s, d)

    with open(os.path.join(build, "Start Hand Tracker.bat"), "w", encoding="utf-8", newline="\r\n") as f:
        f.write(LAUNCHER)
    readme = os.path.join(PROJECT, "README.md")
    if os.path.exists(readme):
        shutil.copy2(readme, os.path.join(build, "README.md"))
    print("[bundle_hand_tracker] Portable Python copied to " + dest)


if __name__ == "__main__":
    main()
