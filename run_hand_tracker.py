#!/usr/bin/env python3
"""
Let it Go - start the MediaPipe hand tracker in its own window.

Run this BEFORE you open the Unity project (or before you press Play):

    py run_hand_tracker.py            (or double-click "Start Hand Tracker.bat")

What it does:
  1. Creates the venv "HandTrackerEnv" next to this file if it is missing
     (needs Python 3.13, 3.12 or 3.11; MediaPipe has no wheels for newer Python).
     A venv copied from another computer does not work; it is rebuilt.
  2. Installs the tested versions from requirements.txt (Orbbec SDK optional).
  3. Starts Assets/StreamingAssets/HandTracker/hand_tracker.py with a camera
     preview window. Hand data goes to Unity on UDP port 5065.

The tracker keeps running when you stop Play mode or close Unity.
Close it with Esc in the camera window, or Ctrl+C in this console.
HandTrackingClient.cs sees that this tracker is running and does not start
a second copy.

Options:
  --update        upgrade mediapipe + opencv-python to the latest version
  --camera N      camera index (default: the USB webcam, found by name)
  --source S      auto (Orbbec, then webcam), orbbec, or webcam
  --no-preview    no camera window (console only)
  --setup-only    only create / repair HandTrackerEnv (first run on a new computer)
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

# MediaPipe wheels exist only for some Python versions. First one found is used.
PYTHON_VERSIONS = ("3.13", "3.12", "3.11")
REQUIREMENTS = os.path.join(HERE, "Assets", "StreamingAssets", "HandTracker", "requirements.txt")
PACKAGES = ["mediapipe", "opencv-python", "numpy"]       # fallback when the pinned versions fail
OPTIONAL_PACKAGES = ["pyorbbecsdk2"]                      # Orbbec Gemini SDK; webcam works without it
HEARTBEAT_PORT = 5066  # hand_tracker.py binds this port, so it shows if a tracker already runs


def log(msg):
    print(f"[run_hand_tracker] {msg}", flush=True)


def run(cmd):
    log("> " + " ".join(f'"{c}"' if " " in c else c for c in cmd))
    return subprocess.call(cmd)


def base_python():
    """Python used to create the venv: the Windows 'py' launcher with a supported
    version, else python3.X on PATH, else the Python running this script."""
    for ver in PYTHON_VERSIONS:
        if os.name == "nt" and shutil.which("py"):
            if subprocess.call(["py", f"-{ver}", "-c", "pass"],
                               stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL) == 0:
                return ["py", f"-{ver}"]
        exe = shutil.which(f"python{ver}")
        if exe:
            return [exe]
    if f"{sys.version_info[0]}.{sys.version_info[1]}" in PYTHON_VERSIONS:
        return [sys.executable]
    return None


def venv_works():
    """A venv copied from another computer points at that computer's Python
    (pyvenv.cfg 'home'), so its python.exe exists but does not start."""
    if not os.path.exists(VENV_PY):
        return False
    try:
        return subprocess.call([VENV_PY, "-c", "pass"], stdout=subprocess.DEVNULL,
                               stderr=subprocess.DEVNULL, timeout=60) == 0
    except Exception:
        return False


def install_packages():
    run([VENV_PY, "-m", "pip", "install", "--upgrade", "pip"])
    ok = os.path.exists(REQUIREMENTS) and run([VENV_PY, "-m", "pip", "install", "-r", REQUIREMENTS]) == 0
    if not ok:
        log("Tested versions did not install here; installing the newest versions instead.")
        ok = run([VENV_PY, "-m", "pip", "install", "--upgrade", *PACKAGES]) == 0
    if ok:
        for pkg in OPTIONAL_PACKAGES:
            if run([VENV_PY, "-m", "pip", "install", pkg]) != 0:
                log(f"Optional package {pkg} did not install (only needed for an Orbbec camera).")
    return ok and packages_ok()


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


def kill_child_with_me(proc):
    """Windows: put the tracker in a Job Object that is closed when this launcher
    exits, so closing the console window also ends the tracker (it used to keep
    running and hold the camera)."""
    if os.name != "nt":
        return None
    try:
        import ctypes
        from ctypes import wintypes
        k32 = ctypes.WinDLL("kernel32", use_last_error=True)
        k32.CreateJobObjectW.restype = wintypes.HANDLE
        k32.OpenProcess.restype = wintypes.HANDLE
        job = k32.CreateJobObjectW(None, None)

        class BASIC(ctypes.Structure):
            _fields_ = [("PerProcessUserTimeLimit", ctypes.c_int64), ("PerJobUserTimeLimit", ctypes.c_int64),
                        ("LimitFlags", wintypes.DWORD), ("MinimumWorkingSetSize", ctypes.c_size_t),
                        ("MaximumWorkingSetSize", ctypes.c_size_t), ("ActiveProcessLimit", wintypes.DWORD),
                        ("Affinity", ctypes.c_size_t), ("PriorityClass", wintypes.DWORD),
                        ("SchedulingClass", wintypes.DWORD)]

        class IO(ctypes.Structure):
            _fields_ = [(n, ctypes.c_uint64) for n in ("r", "w", "o", "rb", "wb", "ob")]

        class EXT(ctypes.Structure):
            _fields_ = [("Basic", BASIC), ("Io", IO), ("ProcessMemoryLimit", ctypes.c_size_t),
                        ("JobMemoryLimit", ctypes.c_size_t), ("PeakProcessMemoryUsed", ctypes.c_size_t),
                        ("PeakJobMemoryUsed", ctypes.c_size_t)]

        info = EXT()
        info.Basic.LimitFlags = 0x2000  # JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        k32.SetInformationJobObject(job, 9, ctypes.byref(info), ctypes.sizeof(info))  # 9 = ExtendedLimitInformation
        h = k32.OpenProcess(0x0001 | 0x0100, False, proc.pid)  # PROCESS_TERMINATE | PROCESS_SET_QUOTA
        k32.AssignProcessToJobObject(job, h)
        k32.CloseHandle(h)
        return job  # keep the handle open; Windows closes it (and the tracker) when we exit
    except Exception as e:
        log(f"(could not tie the tracker to this window: {e})")
        return None


def stop_old_trackers():
    """Stop hand_tracker.py processes left over from an earlier run (they keep the
    camera busy, so the new tracker cannot open it)."""
    if os.name != "nt":
        return
    ps = ("Get-CimInstance Win32_Process -Filter \"Name='python.exe'\" | "
          "Where-Object { $_.CommandLine -like '*HandTracker*hand_tracker*.py*' -and $_.ProcessId -ne %d } | "
          "ForEach-Object { Stop-Process -Id $_.ProcessId -Force; $_.ProcessId }" % os.getpid())
    try:
        out = subprocess.run(["powershell", "-NoProfile", "-Command", ps],
                             capture_output=True, text=True, timeout=20).stdout.split()
        if out:
            log("Stopped old tracker process(es) that held the camera: " + ", ".join(out))
    except Exception as e:
        log(f"(could not check for old trackers: {e})")


def setup(update):
    if os.path.exists(VENV_DIR) and not venv_works():
        log("HandTrackerEnv does not work on this computer (copied from another one?). Rebuilding it.")
        shutil.rmtree(VENV_DIR, ignore_errors=True)
    if not os.path.exists(VENV_PY):
        base = base_python()
        if base is None:
            log("ERROR: no supported Python found (" + ", ".join(PYTHON_VERSIONS) + "). "
                "Install Python 3.13 from python.org (tick 'Add python.exe to PATH') and run again.")
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
    ap.add_argument("--camera", type=int, default=None,
                    help="webcam index (default: the USB webcam, found by name)")
    ap.add_argument("--no-preview", action="store_true", help="do not show the camera window")
    ap.add_argument("--setup-only", action="store_true",
                    help="only create / repair HandTrackerEnv, then exit (Unity then starts the tracker itself)")
    args, extra = ap.parse_known_args()

    if args.setup_only:
        ok = setup(args.update)
        log("Setup " + ("done: " + mediapipe_version() if ok else "FAILED"))
        return 0 if ok else 1

    if not os.path.exists(TRACKER):
        log(f"ERROR: tracker script missing: {TRACKER}")
        return 1

    if tracker_already_running(HEARTBEAT_PORT):
        log(f"An old hand tracker still runs (port {HEARTBEAT_PORT} in use). Stopping it...")
        stop_old_trackers()
        import time
        for _ in range(20):
            if not tracker_already_running(HEARTBEAT_PORT):
                break
            time.sleep(0.25)
        else:
            log("The old tracker did not stop. Close it in Task Manager (python.exe) and try again.")
            return 1

    if not setup(args.update):
        return 1

    log(f"Using {mediapipe_version()}")
    log("Starting tracker. Esc in the camera window or Ctrl+C here to stop.")

    cmd = [VENV_PY, TRACKER, "--no-heartbeat", *extra]
    if args.camera is not None:
        cmd += ["--camera", str(args.camera)]
    if not args.no_preview:
        cmd.append("--preview")

    proc = subprocess.Popen(cmd, cwd=os.path.dirname(TRACKER))
    job = kill_child_with_me(proc)
    try:
        code = proc.wait()
    except KeyboardInterrupt:
        try:
            proc.wait(5)
        except Exception:
            proc.kill()
        code = 0
    log(f"Tracker stopped (exit code {code}).")
    return code


if __name__ == "__main__":
    rc = main()
    if rc != 0 and sys.stdin and sys.stdin.isatty():
        input("Press Enter to close...")
    sys.exit(rc)
