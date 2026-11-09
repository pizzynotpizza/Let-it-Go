#!/usr/bin/env python3
"""
Let it Go - start the MediaPipe hand tracker in its own window.

Run this BEFORE you open the Unity project (or before you press Play):

    py run_hand_tracker.py            (or double-click "Start Hand Tracker.bat")

What it does:
  1. Creates the venv "HandTrackerEnv" next to this file if it is missing
     (needs Python 3.13, MediaPipe has no wheels for newer Python yet).
  2. Installs / checks mediapipe + opencv-python in that venv.
  3. Starts Assets/StreamingAssets/HandTracker/hand_tracker.py with a camera
     preview window. Hand data goes to Unity on UDP port 5065.

The tracker keeps running when you stop Play mode or close Unity.
Close it with Esc in the camera window, or Ctrl+C in this console.
HandTrackingClient.cs sees that this tracker is running and does not start
a second copy.

Options:
  --update        upgrade mediapipe + opencv-python to the latest version
  --camera N      camera index (default 0)
  --source S      auto (Orbbec, then webcam), orbbec, or webcam
  --no-preview    no camera window (console only)
  Other options go straight to hand_tracker.py (for example --port 5065).
"""

import argparse
import os
import shutil
import socket
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
VENV_DIR = os.path.join(HERE, "HandTrackerEnv")
if os.name == "nt":
    VENV_PY = os.path.join(VENV_DIR, "Scripts", "python.exe")
else:
    VENV_PY = os.path.join(VENV_DIR, "bin", "python")
TRACKER = os.path.join(HERE, "Assets", "StreamingAssets", "HandTracker", "hand_tracker.py")

PYTHON_VERSION = "3.13"
PACKAGES = ["mediapipe", "opencv-python", "pyorbbecsdk2"]  # pyorbbecsdk2 = Orbbec Gemini SDK
HEARTBEAT_PORT = 5066  # hand_tracker.py binds this port, so it shows if a tracker already runs


def log(msg):
    print(f"[run_hand_tracker] {msg}", flush=True)


def run(cmd):
    log("> " + " ".join(f'"{c}"' if " " in c else c for c in cmd))
    return subprocess.call(cmd)


def base_python():
    """Python used to create the venv. Prefers the Windows 'py' launcher with 3.13."""
    if os.name == "nt" and shutil.which("py"):
        probe = subprocess.call(["py", f"-{PYTHON_VERSION}", "-c", "pass"],
                                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        if probe == 0:
            return ["py", f"-{PYTHON_VERSION}"]
    if sys.version_info[:2] == tuple(int(v) for v in PYTHON_VERSION.split(".")):
        return [sys.executable]
    return None


def install_packages():
    run([VENV_PY, "-m", "pip", "install", "--upgrade", "pip"])
    return run([VENV_PY, "-m", "pip", "install", "--upgrade", *PACKAGES]) == 0


def packages_ok():
    return subprocess.call([VENV_PY, "-c", "import mediapipe, cv2"],
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL) == 0


def mediapipe_version():
    try:
        out = subprocess.check_output(
            [VENV_PY, "-c", "import mediapipe, cv2; print(mediapipe.__version__, cv2.__version__)"],
            text=True, stderr=subprocess.DEVNULL)
        mp_ver, cv_ver = out.split()
        return f"mediapipe {mp_ver}, opencv {cv_ver}"
    except Exception:
        return "unknown"


def tracker_already_running(port):
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        s.bind(("127.0.0.1", port))
        return False
    except OSError:
        return True
    finally:
        s.close()


def setup(update):
    if not os.path.exists(VENV_PY):
        base = base_python()
        if base is None:
            log(f"ERROR: Python {PYTHON_VERSION} not found. Install it from python.org and run again.")
            return False
        log(f"Creating venv in {VENV_DIR}")
        if run(base + ["-m", "venv", VENV_DIR]) != 0:
            log("ERROR: cannot create venv.")
            return False
        return install_packages()
    if update or not packages_ok():
        log("Installing / upgrading packages...")
        return install_packages()
    return True


def main():
    ap = argparse.ArgumentParser(description="Start the Let it Go hand tracker in its own window.")
    ap.add_argument("--update", action="store_true", help="upgrade mediapipe to the latest version")
    ap.add_argument("--camera", type=int, default=0, help="webcam index")
    ap.add_argument("--no-preview", action="store_true", help="do not show the camera window")
    args, extra = ap.parse_known_args()

    if not os.path.exists(TRACKER):
        log(f"ERROR: tracker script missing: {TRACKER}")
        return 1

    if tracker_already_running(HEARTBEAT_PORT):
        log(f"A hand tracker already runs (port {HEARTBEAT_PORT} in use). Close it first.")
        return 1

    if not setup(args.update):
        return 1

    log(f"Using {mediapipe_version()}")
    log("Starting tracker. Esc in the camera window or Ctrl+C here to stop.")

    cmd = [VENV_PY, TRACKER, "--no-heartbeat", "--camera", str(args.camera), *extra]
    if not args.no_preview:
        cmd.append("--preview")

    try:
        code = subprocess.call(cmd, cwd=os.path.dirname(TRACKER))
    except KeyboardInterrupt:
        code = 0
    log(f"Tracker stopped (exit code {code}).")
    return code


if __name__ == "__main__":
    rc = main()
    if rc != 0 and sys.stdin and sys.stdin.isatty():
        input("Press Enter to close...")
    sys.exit(rc)
