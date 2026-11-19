"""
Let it Go - hand tracker for a normal webcam (USB or laptop camera).

Uses OpenCV VideoCapture. On Windows it tries DirectShow first, then Media Foundation.
If the requested index gives no frames, it scans the other indexes.

    HandTrackerEnv\\Scripts\\python hand_tracker_webcam.py --preview --no-heartbeat
    HandTrackerEnv\\Scripts\\python hand_tracker_webcam.py --camera 1 --preview

Hand data and UDP protocol: see hand_tracker_core.py.
"""

import re
import shutil
import subprocess
import sys
import threading
import time

import cv2

import hand_tracker_core as core


class WebcamCamera:
    HFOV = 70.0  # Logitech C920 (typical USB webcam 60-78)
    def __init__(self, cap):
        self.cap = cap

    def read_bgr(self):
        return self.cap.read()

    def release(self):
        self.cap.release()


def _try_open(index, backend, width, height):
    cap = cv2.VideoCapture(index, backend)
    if not cap.isOpened():
        cap.release()
        return None
    # MJPG lets USB 2 webcams run 30 fps at 640x480; buffer 1 = no stale frames.
    cap.set(cv2.CAP_PROP_FOURCC, cv2.VideoWriter_fourcc(*"MJPG"))
    cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, width)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, height)
    # DirectShow may reset the format when the size changes: ask MJPG and 30 fps again.
    cap.set(cv2.CAP_PROP_FOURCC, cv2.VideoWriter_fourcc(*"MJPG"))
    cap.set(cv2.CAP_PROP_FPS, 30)
    for _ in range(30):
        ok, frame = cap.read()
        if ok and frame is not None and frame.size:
            return WebcamCamera(cap)
        time.sleep(0.05)
    cap.release()
    return None


def _try_open_timeout(index, backend, width, height, timeout=6.0):
    """Some drivers (Media Foundation with depth cameras) hang forever on open.
    Run the probe in a thread and give up after `timeout` seconds."""
    box = []
    t = threading.Thread(target=lambda: box.append(_try_open(index, backend, width, height)), daemon=True)
    t.start()
    t.join(timeout)
    if t.is_alive():
        print(f"[webcam] camera {index}: open timed out", flush=True)
        return None
    return box[0] if box else None


# The external USB webcam is used first. Its DirectShow index is looked up by name,
# because Windows numbers cameras in its own order (the laptop camera is often 0).
PREFERRED_CAMERA = "C920"
# Other external cameras, tried in this order when PREFERRED_CAMERA is not plugged in.
FALLBACK_CAMERAS = ("Gemini 2 L RGB", "RGB Camera", "Logi")
# Virtual cameras give frames but no real picture; never pick them in the scan.
SKIP_NAMES = ("OBS", "VTube", "Virtual", "IR Camera", "Depth Camera")


def list_dshow_cameras():
    """DirectShow video device names in index order. Needs ffmpeg on PATH; returns [] without it."""
    exe = shutil.which("ffmpeg")
    if sys.platform != "win32" or not exe:
        return []
    try:
        out = subprocess.run([exe, "-hide_banner", "-list_devices", "true", "-f", "dshow", "-i", "dummy"],
                             capture_output=True, text=True, timeout=8,
                             creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0)).stderr
    except Exception:
        return []
    return re.findall(r'"([^"]+)" \(video\)', out)


def find_camera_index(name):
    """Index of the first camera whose name contains `name`, or None."""
    for i, n in enumerate(list_dshow_cameras()):
        if name.lower() in n.lower():
            print(f"[webcam] found '{n}' at index {i}", flush=True)
            return i
    return None


def open_webcam(index=None, width=640, height=480, scan=True, name=PREFERRED_CAMERA):
    if index is None:
        found = None
        for n in ((name,) if name else ()) + FALLBACK_CAMERAS:
            found = find_camera_index(n)
            if found is not None:
                break
        # Without ffmpeg: index 1 is the USB camera on a laptop that has a built-in one.
        index = found if found is not None else 1
    names = list_dshow_cameras()
    if sys.platform == "win32":
        backends = [("DSHOW", cv2.CAP_DSHOW), ("MSMF", cv2.CAP_MSMF)]
    else:
        backends = [("ANY", cv2.CAP_ANY)]
    indexes = [index] + ([i for i in range(6) if i != index] if scan else [])
    for i in indexes:
        if i != index and i < len(names) and any(k.lower() in names[i].lower() for k in SKIP_NAMES):
            continue
        for name, be in backends:
            cam = _try_open_timeout(i, be, width, height)
            if cam is not None:
                print(f"[webcam] camera {i} opened with {name}", flush=True)
                return cam
            print(f"[webcam] camera {i} / {name}: no frames", flush=True)
    return None


def main():
    ap = core.make_parser("Let it Go hand tracker - webcam")
    ap.add_argument("--no-scan", action="store_true", help="use only --camera, do not scan other indexes")
    ap.add_argument("--camera-name", default=PREFERRED_CAMERA, help="use the camera whose name contains this text")
    args = ap.parse_args()
    return core.run(lambda: open_webcam(args.camera, args.width, args.height, not args.no_scan, args.camera_name), args)


if __name__ == "__main__":
    sys.exit(main())
