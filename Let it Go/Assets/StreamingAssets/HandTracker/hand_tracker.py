"""
Let it Go - webcam hand tracker.

Reads the webcam, finds one hand with the MediaPipe Hand Landmarker (Tasks API),
and streams the result to Unity over UDP as one line per frame:

    detected,x,y,openness

    detected  1 when a hand is found, else 0
    x, y      palm centre, 0..1, already mirrored (x=0 left of screen, y=0 top)
    openness  how open the hand is. ~1.9+ for an open palm, ~1.0 or less for a fist.
              Unity calibrates the fist/open threshold from this value.

Unity starts this script automatically (see HandTrackingClient.cs).
Unity sends "ping" to the listen port every second. If no ping arrives for
HEARTBEAT_TIMEOUT seconds, the script exits so no orphan process keeps the webcam.

Run by hand for testing:
    HandTrackerEnv\\Scripts\\python hand_tracker.py --preview
"""

import argparse
import math
import os
import socket
import sys
import time
import urllib.request

import cv2
import mediapipe as mp
from mediapipe.tasks import python as mp_python
from mediapipe.tasks.python import vision

MODEL_URL = (
    "https://storage.googleapis.com/mediapipe-models/hand_landmarker/"
    "hand_landmarker/float16/latest/hand_landmarker.task"
)
HEARTBEAT_TIMEOUT = 5.0

# Landmark indices (MediaPipe hand model)
WRIST = 0
INDEX_MCP, MIDDLE_MCP, RING_MCP, PINKY_MCP = 5, 9, 13, 17
FINGER_TIPS = (8, 12, 16, 20)
FINGER_PIPS = (6, 10, 14, 18)


def ensure_model(path):
    if os.path.exists(path):
        return
    print(f"[hand_tracker] Downloading model to {path}", flush=True)
    urllib.request.urlretrieve(MODEL_URL, path)


def dist(a, b):
    return math.hypot(a.x - b.x, a.y - b.y)


def openness(lm):
    """Average fingertip distance from the wrist, divided by palm size.
    Independent of how far the hand is from the camera."""
    palm = dist(lm[WRIST], lm[MIDDLE_MCP]) + 1e-6
    tips = sum(dist(lm[WRIST], lm[t]) for t in FINGER_TIPS) / len(FINGER_TIPS)
    return tips / palm


def palm_center(lm):
    ids = (WRIST, INDEX_MCP, MIDDLE_MCP, RING_MCP, PINKY_MCP)
    x = sum(lm[i].x for i in ids) / len(ids)
    y = sum(lm[i].y for i in ids) / len(ids)
    return x, y


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--host", default="127.0.0.1")
    ap.add_argument("--port", type=int, default=5065, help="Unity receive port")
    ap.add_argument("--listen", type=int, default=5066, help="heartbeat port")
    ap.add_argument("--camera", type=int, default=0)
    ap.add_argument("--width", type=int, default=640)
    ap.add_argument("--height", type=int, default=480)
    ap.add_argument("--preview", action="store_true", help="show camera window")
    ap.add_argument("--no-heartbeat", action="store_true", help="never auto-exit")
    args = ap.parse_args()

    here = os.path.dirname(os.path.abspath(__file__))
    model_path = os.path.join(here, "hand_landmarker.task")
    ensure_model(model_path)

    # Pass the model as bytes: MediaPipe's native loader fails on non-ASCII
    # paths (this project lives under a Thai folder name).
    with open(model_path, "rb") as f:
        model_bytes = f.read()

    options = vision.HandLandmarkerOptions(
        base_options=mp_python.BaseOptions(model_asset_buffer=model_bytes),
        running_mode=vision.RunningMode.VIDEO,
        num_hands=1,
        min_hand_detection_confidence=0.5,
        min_hand_presence_confidence=0.5,
        min_tracking_confidence=0.5,
    )
    landmarker = vision.HandLandmarker.create_from_options(options)

    cap = cv2.VideoCapture(args.camera, cv2.CAP_DSHOW if sys.platform == "win32" else 0)
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, args.width)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, args.height)
    if not cap.isOpened():
        print("[hand_tracker] ERROR: cannot open camera", flush=True)
        sys.exit(1)

    out = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    target = (args.host, args.port)

    hb = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    hb.bind(("127.0.0.1", args.listen))
    hb.setblocking(False)
    last_ping = time.time()

    print(f"[hand_tracker] mediapipe {mp.__version__} running, sending to {target}", flush=True)
    t0 = time.time()
    last_ts = -1

    try:
        while True:
            # Heartbeat from Unity
            try:
                while True:
                    hb.recvfrom(64)
                    last_ping = time.time()
            except (BlockingIOError, ConnectionResetError):
                pass
            if not args.no_heartbeat and time.time() - last_ping > HEARTBEAT_TIMEOUT:
                print("[hand_tracker] Unity heartbeat lost, exiting", flush=True)
                break

            ok, frame = cap.read()
            if not ok:
                time.sleep(0.01)
                continue

            frame = cv2.flip(frame, 1)  # mirror so moving right = right on screen
            rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb)
            ts = int((time.time() - t0) * 1000)
            if ts <= last_ts:
                ts = last_ts + 1
            last_ts = ts
            result = landmarker.detect_for_video(image, ts)

            if result.hand_landmarks:
                lm = result.hand_landmarks[0]
                x, y = palm_center(lm)
                op = openness(lm)
                msg = f"1,{x:.4f},{y:.4f},{op:.4f}"
                if args.preview:
                    h, w = frame.shape[:2]
                    for p in lm:
                        cv2.circle(frame, (int(p.x * w), int(p.y * h)), 3, (0, 255, 0), -1)
                    cv2.putText(frame, f"open={op:.2f}", (10, 30),
                                cv2.FONT_HERSHEY_SIMPLEX, 0.8, (0, 255, 255), 2)
            else:
                msg = "0,0,0,0"

            out.sendto(msg.encode("ascii"), target)

            if args.preview:
                cv2.imshow("Let it Go - hand tracker (Esc to quit)", frame)
                if cv2.waitKey(1) & 0xFF == 27:
                    break
    finally:
        cap.release()
        landmarker.close()
        if args.preview:
            cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
