"""
Let it Go - hand tracker for a normal webcam (USB or laptop camera).

Uses OpenCV VideoCapture. On Windows it tries DirectShow first, then Media Foundation.
If the requested index gives no frames, it scans the other indexes.

    HandTrackerEnv\\Scripts\\python hand_tracker_webcam.py --preview --no-heartbeat
    HandTrackerEnv\\Scripts\\python hand_tracker_webcam.py --camera 1 --preview

Hand data and UDP protocol: see hand_tracker_core.py.
"""

import sys
import threading
import time

import cv2

import hand_tracker_core as core


class WebcamCamera:
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


def open_webcam(index=0, width=640, height=480, scan=True):
    if sys.platform == "win32":
        backends = [("DSHOW", cv2.CAP_DSHOW), ("MSMF", cv2.CAP_MSMF)]
    else:
        backends = [("ANY", cv2.CAP_ANY)]
    indexes = [index] + ([i for i in range(6) if i != index] if scan else [])
    for i in indexes:
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
    args = ap.parse_args()
    return core.run(lambda: open_webcam(args.camera, args.width, args.height, not args.no_scan), args)


if __name__ == "__main__":
    sys.exit(main())
