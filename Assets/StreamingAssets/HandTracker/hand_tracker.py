"""
Let it Go - webcam hand tracker.

Reads the webcam, finds hands with the MediaPipe Gesture Recognizer (Tasks API),
keeps only the nearest hand (largest on screen),
and streams the result to Unity over UDP as one line per frame:

    detected,x,y,openness,gesture

    detected  1 when a hand is found, else 0
    x, y      palm centre, 0..1, already mirrored (x=0 left of screen, y=0 top)
    openness  how open the hand is. ~1.9+ for an open palm, ~1.0 or less for a fist.
              Unity calibrates the fist/open threshold from this value.
    gesture   MediaPipe gesture name of that hand, for example Closed_Fist,
              Open_Palm, None. Unity uses Closed_Fist as the grab signal.

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
    "https://storage.googleapis.com/mediapipe-models/gesture_recognizer/"
    "gesture_recognizer/float16/latest/gesture_recognizer.task"
)
MODEL_FILE = "gesture_recognizer.task"
MAX_HANDS = 4  # detect several, then keep only the nearest one

HAND_CONNECTIONS = (
    (0, 1), (1, 2), (2, 3), (3, 4),
    (0, 5), (5, 6), (6, 7), (7, 8),
    (5, 9), (9, 10), (10, 11), (11, 12),
    (9, 13), (13, 14), (14, 15), (15, 16),
    (13, 17), (0, 17), (17, 18), (18, 19), (19, 20),
)
GESTURE_NAMES = {
    "None": "None", "Closed_Fist": "Closed Fist", "Open_Palm": "Open Palm",
    "Pointing_Up": "Pointing Up", "Thumb_Down": "Thumb Down", "Thumb_Up": "Thumb Up",
    "Victory": "Victory", "ILoveYou": "I Love You",
}
BLUE = (235, 120, 30)  # BGR
WHITE = (255, 255, 255)
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


def hand_size(lm):
    """Bounding-box diagonal in image units. Bigger = closer to the camera."""
    xs = [p.x for p in lm]
    ys = [p.y for p in lm]
    return math.hypot(max(xs) - min(xs), max(ys) - min(ys))


def nearest_hand(result):
    """Index of the largest (nearest) hand, or -1."""
    if not result.hand_landmarks:
        return -1
    return max(range(len(result.hand_landmarks)), key=lambda i: hand_size(result.hand_landmarks[i]))


def draw_label(frame, text, org, scale=0.6, thick=1):
    (tw, th), base = cv2.getTextSize(text, cv2.FONT_HERSHEY_SIMPLEX, scale, thick)
    x, y = org
    cv2.rectangle(frame, (x - 6, y - th - 8), (x + tw + 6, y + base + 4), WHITE, -1)
    cv2.putText(frame, text, (x, y), cv2.FONT_HERSHEY_SIMPLEX, scale, (30, 30, 30), thick, cv2.LINE_AA)


def draw_hand(frame, lm):
    h, w = frame.shape[:2]
    pts = [(int(p.x * w), int(p.y * h)) for p in lm]
    for a, b in HAND_CONNECTIONS:
        cv2.line(frame, pts[a], pts[b], BLUE, 2, cv2.LINE_AA)
    for p in pts:
        cv2.circle(frame, p, 4, WHITE, -1, cv2.LINE_AA)
        cv2.circle(frame, p, 4, BLUE, 1, cv2.LINE_AA)


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
    model_path = os.path.join(here, MODEL_FILE)
    ensure_model(model_path)

    # Pass the model as bytes: MediaPipe's native loader fails on non-ASCII
    # paths (this project lives under a Thai folder name).
    with open(model_path, "rb") as f:
        model_bytes = f.read()

    options = vision.GestureRecognizerOptions(
        base_options=mp_python.BaseOptions(model_asset_buffer=model_bytes),
        running_mode=vision.RunningMode.VIDEO,
        num_hands=MAX_HANDS,
        min_hand_detection_confidence=0.5,
        # Lower presence/tracking thresholds keep a closed fist tracked (a fist is harder to see).
        min_hand_presence_confidence=0.4,
        min_tracking_confidence=0.4,
    )
    landmarker = vision.GestureRecognizer.create_from_options(options)

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
            result = landmarker.recognize_for_video(image, ts)
            idx = nearest_hand(result)

            if idx >= 0:
                lm = result.hand_landmarks[idx]
                x, y = palm_center(lm)
                op = openness(lm)
                gname = "None"
                if result.gestures and result.gestures[idx]:
                    gname = result.gestures[idx][0].category_name or "None"
                msg = f"1,{x:.4f},{y:.4f},{op:.4f},{gname}"
                if args.preview:
                    side = result.handedness[idx][0].category_name if result.handedness else "?"
                    if result.gestures and result.gestures[idx]:
                        g = result.gestures[idx][0]
                        name = GESTURE_NAMES.get(g.category_name, g.category_name or "None")
                        label = f"{side} - {name} {g.score * 100:.0f}%"
                    else:
                        label = f"{side} - None"
                    draw_hand(frame, lm)
                    draw_label(frame, label, (16, 32), 0.7, 2)
            else:
                msg = "0,0,0,0,None"

            out.sendto(msg.encode("ascii"), target)

            if args.preview:
                if idx < 0:
                    draw_label(frame, "No hand", (16, 32), 0.7, 2)
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
