"""
Let it Go - hand tracker core (shared by every camera script).

Camera scripts:
    hand_tracker_webcam.py   normal USB / laptop webcam (OpenCV)
    hand_tracker_orbbec.py   Orbbec Gemini (pyorbbecsdk2, OpenCV OBSENSOR fallback)
    hand_tracker.py          entry Unity starts; --source auto|orbbec|webcam

Reads a camera frame, finds hands with the MediaPipe Gesture Recognizer (Tasks API),
keeps only the first hand seen (see HandLock), and streams the result to Unity
over UDP as one line per frame:

    detected,x,y,openness,gesture[,x0,y0,...,x20,y20]

    detected  1 when a hand is found, else 0
    x, y      palm centre, 0..1, already mirrored (x=0 left of screen, y=0 top)
    openness  how open the hand is. ~1.9+ for an open palm, ~1.0 or less for a fist.
              Unity calibrates the fist/open threshold from this value.
    gesture   MediaPipe gesture name of that hand, for example Closed_Fist,
              Open_Palm, None. Unity uses Closed_Fist as the grab signal.
    x0..y20   the 21 hand landmarks (image units, mirrored), only when detected.
              Unity draws them as the skeleton view in Gameplay.

Unity starts this script automatically (see HandTrackingClient.cs).
Unity sends "ping" to the listen port every second. If no ping arrives for
HEARTBEAT_TIMEOUT seconds, the script exits so no orphan process keeps the webcam.

Run by hand for testing:
    HandTrackerEnv\\Scripts\\python hand_tracker_orbbec.py --preview --no-heartbeat
"""

import argparse
import math
import os
import socket
import sys
import threading
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
MAX_HANDS = 1  # one hand is about twice as fast; MediaPipe keeps tracking the hand it already follows, HandLock guards the rest
# Lower thresholds = hand found sooner and kept while it moves fast or makes a fist.
DETECTION_CONFIDENCE = 0.35
PRESENCE_CONFIDENCE = 0.3
TRACKING_CONFIDENCE = 0.3

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
RED = (40, 40, 230)  # BGR, bones while the hand is a fist
WHITE = (255, 255, 255)
HEARTBEAT_TIMEOUT = 5.0
PREVIEW_EVERY = 3  # draw the preview window on every 3rd frame only (it is slow)

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


def dist3(a, b):
    return math.sqrt((a.x - b.x) ** 2 + (a.y - b.y) ** 2 + (a.z - b.z) ** 2)


def openness(lm, world=None):
    """Average fingertip distance from the wrist, divided by palm size.
    Independent of how far the hand is from the camera.

    With world landmarks (3D, metres) the value does not change when the hand
    turns or tilts: a fist seen from the side or with the knuckles to the camera
    gives the same value as a fist seen from the front. The 2D image landmarks
    are only a fallback (they shrink when the hand tilts away from the camera)."""
    if world:
        d = dist3
        pts = world
    else:
        d = dist
        pts = lm
    palm = d(pts[WRIST], pts[MIDDLE_MCP]) + 1e-6
    tips = sum(d(pts[WRIST], pts[t]) for t in FINGER_TIPS) / len(FINGER_TIPS)
    return tips / palm


def finger_curl(world):
    """Number of curled fingers (index..pinky) from 3D joint angles.
    A finger counts as curled when the tip points back toward the wrist
    (angle MCP->PIP vs PIP->TIP over ~90 degrees). Rotation independent."""
    curled = 0
    for mcp, pip, tip in ((5, 6, 8), (9, 10, 12), (13, 14, 16), (17, 18, 20)):
        a, b, c = world[mcp], world[pip], world[tip]
        v1 = (b.x - a.x, b.y - a.y, b.z - a.z)
        v2 = (c.x - b.x, c.y - b.y, c.z - b.z)
        n1 = math.sqrt(sum(x * x for x in v1)) + 1e-9
        n2 = math.sqrt(sum(x * x for x in v2)) + 1e-9
        cos = sum(x * y for x, y in zip(v1, v2)) / (n1 * n2)
        if cos < 0.1:  # bent more than ~85 degrees
            curled += 1
    return curled


# 3D fist rule. Used when the Gesture Recognizer misses Closed_Fist at an odd angle.
FIST_OPENNESS_3D = 1.35   # openness below this = fist
FIST_MIN_CURLED = 3       # or at least this many curled fingers with openness below OPEN_OPENNESS_3D
OPEN_OPENNESS_3D = 1.6


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


class HandLock:
    """Plays with the first hand seen and ignores other hands.

    The locked hand is followed by palm position from frame to frame. A hand that
    appears far from the locked one is a different hand and is ignored. The lock
    is released when the locked hand is gone for LOST_TIME seconds; then the next
    hand seen becomes the player hand."""

    LOST_TIME = 0.6   # seconds without the locked hand before the lock is released
    # Largest palm move (image units) between two sightings that is still the same hand
    # when it is the only hand on screen. Large, because a fast swipe moves the palm a lot
    # and MediaPipe often skips a frame or two while the hand moves.
    MAX_JUMP = 0.5

    def __init__(self):
        self.pos = None
        self.last_seen = 0.0

    @property
    def locked(self):
        return self.pos is not None

    def pick(self, result, now, allowed=None):
        """allowed: hand indices that may be used (for example only hands in the
        play distance). None = all hands."""
        hands = result.hand_landmarks or []
        if self.pos is not None and now - self.last_seen > self.LOST_TIME:
            self.pos = None
        if allowed is None:
            allowed = list(range(len(hands)))
        if not allowed:
            return -1
        centres = {i: palm_center(hands[i]) for i in allowed}
        if self.pos is None:
            idx = allowed[0]
        else:
            # Two or more hands: the one nearest the last player position is the player.
            idx = min(allowed, key=lambda i: math.hypot(centres[i][0] - self.pos[0], centres[i][1] - self.pos[1]))
            c = centres[idx]
            if len(hands) == 1 and math.hypot(c[0] - self.pos[0], c[1] - self.pos[1]) > self.MAX_JUMP:
                return -1  # only a hand far away is visible: maybe another person; wait LOST_TIME
        self.pos = centres[idx]
        self.last_seen = now
        return idx


# Play distance. The camera scripts read only the colour image (no depth), so the
# distance is estimated from hand size: MediaPipe world landmarks give the real palm
# size in metres, the image landmarks give it in pixels, and the camera focal length
# turns the ratio into a distance. Several palm segments are measured and the least
# foreshortened one is used, so a tilted hand does not look farther away.
DIST_KEEP_MARGIN = 0.15  # metres of extra range for the hand that is already playing
PALM_SEGMENTS = ((WRIST, INDEX_MCP), (WRIST, PINKY_MCP), (INDEX_MCP, PINKY_MCP), (WRIST, MIDDLE_MCP))


def hand_distance(lm, world, frame_w, frame_h, hfov_deg):
    """Estimated camera-to-hand distance in metres, or None without world landmarks."""
    if not world:
        return None
    f_px = (frame_w / 2.0) / math.tan(math.radians(hfov_deg) / 2.0)
    best = 0.0
    for a, b in PALM_SEGMENTS:
        real = dist3(world[a], world[b])
        if real < 1e-4:
            continue
        px = math.hypot((lm[a].x - lm[b].x) * frame_w, (lm[a].y - lm[b].y) * frame_h)
        best = max(best, px / real)
    if best <= 0:
        return None
    return f_px / best


# MediaPipe labels handedness for the camera view; after our mirror flip the
# label is the opposite of the player's real hand, so swap it for display.
SIDE_SWAP = {"Left": "Right", "Right": "Left"}


def draw_label(frame, text, org, scale=0.6, thick=1):
    (tw, th), base = cv2.getTextSize(text, cv2.FONT_HERSHEY_SIMPLEX, scale, thick)
    x, y = org
    cv2.rectangle(frame, (x - 6, y - th - 8), (x + tw + 6, y + base + 4), WHITE, -1)
    cv2.putText(frame, text, (x, y), cv2.FONT_HERSHEY_SIMPLEX, scale, (30, 30, 30), thick, cv2.LINE_AA)


def draw_hand(frame, lm, color=BLUE):
    h, w = frame.shape[:2]
    pts = [(int(p.x * w), int(p.y * h)) for p in lm]
    for a, b in HAND_CONNECTIONS:
        cv2.line(frame, pts[a], pts[b], color, 3, cv2.LINE_AA)
    for p in pts:
        cv2.circle(frame, p, 4, WHITE, -1, cv2.LINE_AA)
        cv2.circle(frame, p, 4, color, 1, cv2.LINE_AA)


class LatestFrame:
    """Reads the camera on its own thread and keeps only the newest frame.
    The tracker never works on an old, queued frame, so latency stays at one frame."""

    def __init__(self, cam):
        self.cam = cam
        self.cond = threading.Condition()
        self.frame = None
        self.seq = 0
        self.running = True
        self.thread = threading.Thread(target=self._loop, daemon=True)
        self.thread.start()

    def _loop(self):
        while self.running:
            try:
                ok, frame = self.cam.read_bgr()
            except Exception as e:
                print(f"[hand_tracker] camera read error: {e}", flush=True)
                ok, frame = False, None
            if not ok or frame is None:
                time.sleep(0.005)
                continue
            with self.cond:
                self.frame = frame
                self.seq += 1
                self.cond.notify()

    def read(self, last_seq, timeout=0.2):
        """Newest frame after last_seq, or (last_seq, None) on timeout."""
        with self.cond:
            self.cond.wait_for(lambda: self.seq != last_seq, timeout)
            if self.seq == last_seq:
                return last_seq, None
            return self.seq, self.frame

    def release(self):
        self.running = False
        self.thread.join(1.0)
        self.cam.release()


def make_parser(description):
    """Options shared by every camera script."""
    ap = argparse.ArgumentParser(description=description)
    ap.add_argument("--host", default="127.0.0.1")
    ap.add_argument("--port", type=int, default=5065, help="Unity receive port")
    ap.add_argument("--listen", type=int, default=5066, help="heartbeat port")
    ap.add_argument("--camera", type=int, default=0)
    ap.add_argument("--width", type=int, default=640)
    ap.add_argument("--height", type=int, default=480)
    ap.add_argument("--preview", action="store_true", help="show camera window")
    ap.add_argument("--no-heartbeat", action="store_true", help="never auto-exit")
    ap.add_argument("--min-dist", type=float, default=0.3, help="closest play distance, metres")
    ap.add_argument("--max-dist", type=float, default=1.0, help="farthest play distance, metres")
    ap.add_argument("--hfov", type=float, default=None,
                    help="camera horizontal field of view in degrees (distance estimate). "
                         "Default: 86 for Orbbec, 65 for a webcam. Raise it if the shown cm is too far, lower it if too close.")
    return ap


def run(open_camera, args):
    """Main loop. open_camera() returns an object with read_bgr() -> (ok, frame)
    and release(), or None when no camera opens."""

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
        min_hand_detection_confidence=DETECTION_CONFIDENCE,
        min_hand_presence_confidence=PRESENCE_CONFIDENCE,
        min_tracking_confidence=TRACKING_CONFIDENCE,
    )
    landmarker = vision.GestureRecognizer.create_from_options(options)

    cam = open_camera()
    if cam is None:
        print("[hand_tracker] ERROR: cannot open the camera. Close other apps "
              "that use the camera (Orbbec Viewer, Zoom, ...) and check the cable.", flush=True)
        landmarker.close()
        return 1
    if args.hfov is None:
        args.hfov = getattr(cam, "HFOV", 65.0)
    print(f"[hand_tracker] play distance {args.min_dist:.2f}-{args.max_dist:.2f} m, hfov {args.hfov:.0f}", flush=True)
    cap = LatestFrame(cam)

    out = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    target = (args.host, args.port)

    hb = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    hb.bind(("127.0.0.1", args.listen))
    hb.setblocking(False)
    last_ping = time.time()

    print(f"[hand_tracker] mediapipe {mp.__version__} running, sending to {target}", flush=True)
    t0 = time.time()
    last_ts = -1
    seq = 0
    fps, fps_t, fps_n = 0.0, time.time(), 0
    lock = HandLock()

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

            seq, frame = cap.read(seq)
            if frame is None:
                continue
            fps_n += 1
            # The preview window (imshow + waitKey) costs ~10-20 ms on Windows. Doing it
            # every frame halved the hand updates sent to Unity, so draw it every few frames.
            show = args.preview and fps_n % PREVIEW_EVERY == 0
            if time.time() - fps_t >= 1.0:
                fps, fps_t, fps_n = fps_n / (time.time() - fps_t), time.time(), 0

            frame = cv2.flip(frame, 1)  # mirror so moving right = right on screen
            rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb)
            ts = int((time.time() - t0) * 1000)
            if ts <= last_ts:
                ts = last_ts + 1
            last_ts = ts
            result = landmarker.recognize_for_video(image, ts)
            # Keep only hands inside the play distance (min_dist..max_dist).
            fh, fw = frame.shape[:2]
            hands = result.hand_landmarks or []
            worlds = result.hand_world_landmarks or []
            dists = [hand_distance(hands[i], worlds[i] if i < len(worlds) else None, fw, fh, args.hfov)
                     for i in range(len(hands))]
            # Hysteresis: a new hand must be inside min..max to start playing, but the
            # hand already playing is kept in a wider band, so small errors in the
            # distance estimate (it is noisy, more so while the hand moves) never drop it.
            lo, hi = args.min_dist, args.max_dist
            if lock.locked:
                lo, hi = lo - DIST_KEEP_MARGIN, hi + DIST_KEEP_MARGIN
            allowed = [i for i, d in enumerate(dists) if d is None or lo <= d <= hi]
            idx = lock.pick(result, time.time(), allowed)
            range_hint = None
            if idx < 0 and hands:
                known = [d for d in dists if d is not None]
                if known and min(known) < args.min_dist:
                    range_hint = "TooClose"
                elif known and min(known) > args.max_dist:
                    range_hint = "TooFar"

            if idx >= 0:
                lm = result.hand_landmarks[idx]
                x, y = palm_center(lm)
                world = result.hand_world_landmarks[idx] if result.hand_world_landmarks else None
                op = openness(lm, world)
                gname = "None"
                if result.gestures and result.gestures[idx]:
                    gname = result.gestures[idx][0].category_name or "None"
                # Gesture model is trained mostly on front-facing hands. Check the
                # 3D shape too, so a fist still counts when the hand is turned.
                if world and gname != "Closed_Fist":
                    if op < FIST_OPENNESS_3D or (op < OPEN_OPENNESS_3D and finger_curl(world) >= FIST_MIN_CURLED):
                        gname = "Closed_Fist"
                # Unity gets the real gesture name: its fist logic treats only
                # Open_Palm (or a clearly open hand) as "release", so a half-closed
                # hand read as None / Pointing_Up does not drop the grab.
                unity_gesture = gname
                # The preview shows only two states: fist or open hand.
                model_gesture = gname
                if gname != "Closed_Fist":
                    gname = "Open_Palm"
                # 21 landmarks (x, y in image units, mirrored).
                pts = ",".join(f"{p.x:.3f},{p.y:.3f}" for p in lm)
                msg = f"1,{x:.4f},{y:.4f},{op:.4f},{unity_gesture},{pts}"
                if show:
                    side = result.handedness[idx][0].category_name if result.handedness else "?"
                    side = SIDE_SWAP.get(side, side)
                    label = f"{side} - {GESTURE_NAMES[gname]}"
                    g = result.gestures[idx][0] if result.gestures and result.gestures[idx] else None
                    if g is not None and g.category_name == model_gesture == gname:
                        label += f" {g.score * 100:.0f}%"
                    draw_hand(frame, lm, RED if gname == "Closed_Fist" else BLUE)
                    draw_label(frame, label, (16, 32), 0.7, 2)
                    if dists[idx] is not None:
                        draw_label(frame, f"{dists[idx] * 100:.0f} cm", (16, 70), 0.55, 1)
            else:
                # Gesture field carries TooClose / TooFar so Unity can tell the player.
                msg = f"0,0,0,0,{range_hint or 'None'}"

            out.sendto(msg.encode("ascii"), target)

            if show:
                if idx < 0:
                    text = {"TooClose": "Too close - move back",
                            "TooFar": "Too far - move closer"}.get(range_hint, "No hand")
                    draw_label(frame, text, (16, 32), 0.7, 2)
                    for i, lm_other in enumerate(hands):
                        if dists[i] is not None:
                            px = palm_center(lm_other)
                            draw_label(frame, f"{dists[i] * 100:.0f} cm",
                                       (int(px[0] * fw), int(px[1] * fh)), 0.55, 1)
                draw_label(frame, f"{fps:.0f} fps", (16, frame.shape[0] - 16), 0.5, 1)
                cv2.imshow("Let it Go - hand tracker (Esc to quit)", frame)
                if cv2.waitKey(1) & 0xFF == 27:
                    break
    finally:
        cap.release()
        landmarker.close()
        if args.preview:
            cv2.destroyAllWindows()
    return 0
