"""
Let it Go - hand tracker for an Orbbec Gemini camera (tested: Gemini 2 L).

Reads the colour stream with the official Orbbec Python SDK (pip package
"pyorbbecsdk2"). If the SDK is missing or fails, it falls back to the OpenCV
Orbbec backend (cv2.CAP_OBSENSOR).

    HandTrackerEnv\\Scripts\\python -m pip install pyorbbecsdk2
    HandTrackerEnv\\Scripts\\python hand_tracker_orbbec.py --preview --no-heartbeat

Close Orbbec Viewer first: only one program can use the camera.
Use a USB 3 port and cable.

Hand data and UDP protocol: see hand_tracker_core.py.
"""

import sys
import time

import cv2
import numpy as np

import hand_tracker_core as core


# ---------------------------------------------------------------- Orbbec SDK
class OrbbecSdkCamera:
    def __init__(self, ob, ctx, pipeline):
        # Keep the Context alive: the pipeline breaks if it is garbage-collected.
        self.ob, self.ctx, self.pipeline = ob, ctx, pipeline

    def read_bgr(self):
        frames = self.pipeline.wait_for_frames(100)
        if frames is None:
            return False, None
        f = frames.get_color_frame()
        if f is None:
            return False, None
        return True, self._to_bgr(f)

    def _to_bgr(self, f):
        F = self.ob.OBFormat
        w, h, fmt = f.get_width(), f.get_height(), f.get_format()
        data = np.asanyarray(f.get_data())
        if fmt == F.RGB:
            return cv2.cvtColor(data.reshape(h, w, 3), cv2.COLOR_RGB2BGR)
        if fmt == F.BGR:
            return data.reshape(h, w, 3).copy()
        if fmt == F.MJPG:
            return cv2.imdecode(data, cv2.IMREAD_COLOR)
        if fmt == F.YUYV:
            return cv2.cvtColor(data.reshape(h, w, 2), cv2.COLOR_YUV2BGR_YUYV)
        if fmt == F.UYVY:
            return cv2.cvtColor(data.reshape(h, w, 2), cv2.COLOR_YUV2BGR_UYVY)
        if fmt == F.NV12:
            return cv2.cvtColor(data.reshape(h * 3 // 2, w), cv2.COLOR_YUV2BGR_NV12)
        if fmt == F.I420:
            return cv2.cvtColor(data.reshape(h * 3 // 2, w), cv2.COLOR_YUV2BGR_I420)
        return None

    def release(self):
        try:
            self.pipeline.stop()
        except Exception:
            pass


def _pick_profile(ob, profiles, width, height, fps):
    """Prefer RGB (no decode cost), then MJPG, YUYV; fall back to the default profile."""
    for fmt in (ob.OBFormat.RGB, ob.OBFormat.MJPG, ob.OBFormat.YUYV):
        for w, h in ((width, height), (width, 0)):
            try:
                return profiles.get_video_stream_profile(w, h, fmt, fps)
            except Exception:
                pass
    return profiles.get_default_video_stream_profile()


def open_orbbec_sdk(width, height, fps):
    try:
        import pyorbbecsdk as ob
    except ImportError:
        print("[orbbec] pyorbbecsdk2 not installed "
              "(HandTrackerEnv\\Scripts\\python -m pip install pyorbbecsdk2)", flush=True)
        return None
    try:
        try:
            ob.Context.set_logger_level(ob.OBLogLevel.ERROR)
        except Exception:
            pass
        ctx = ob.Context()
        devices = ctx.query_devices()
        if devices.get_count() == 0:
            print("[orbbec] SDK: no Orbbec device found", flush=True)
            return None
        device = devices.get_device_by_index(0)
        name = device.get_device_info().get_name()

        pipeline = ob.Pipeline(device)
        profiles = pipeline.get_stream_profile_list(ob.OBSensorType.COLOR_SENSOR)
        profile = _pick_profile(ob, profiles, width, height, fps)
        config = ob.Config()
        config.enable_stream(profile)
        pipeline.start(config)
        cam = OrbbecSdkCamera(ob, ctx, pipeline)

        deadline = time.time() + 5
        while time.time() < deadline:  # wait for the first real frame
            ok, frame = cam.read_bgr()
            if ok and frame is not None and frame.size:
                vp = profile.as_video_stream_profile() if hasattr(profile, "as_video_stream_profile") else profile
                print(f"[orbbec] {name} opened with SDK: {vp.get_width()}x{vp.get_height()} "
                      f"{vp.get_format()} {vp.get_fps()}fps", flush=True)
                return cam
        cam.release()
        print("[orbbec] SDK: stream started but no colour frames", flush=True)
    except Exception as e:
        print(f"[orbbec] SDK error: {e}", flush=True)
    return None


# ------------------------------------------------------ OpenCV OBSENSOR backend
class OrbbecCvCamera:
    def __init__(self, cap):
        self.cap = cap

    def read_bgr(self):
        if not self.cap.grab():
            return False, None
        return self.cap.retrieve(None, cv2.CAP_OBSENSOR_BGR_IMAGE)

    def release(self):
        self.cap.release()


def open_orbbec_opencv(index):
    if not hasattr(cv2, "CAP_OBSENSOR"):
        return None
    for i in [index] + [i for i in range(4) if i != index]:
        cap = cv2.VideoCapture(i, cv2.CAP_OBSENSOR)
        if not cap.isOpened():
            cap.release()
            continue
        cam = OrbbecCvCamera(cap)
        for _ in range(60):
            ok, frame = cam.read_bgr()
            if ok and frame is not None and frame.size:
                print(f"[orbbec] camera {i} opened with OpenCV OBSENSOR", flush=True)
                return cam
            time.sleep(0.05)
        cam.release()
    print("[orbbec] OpenCV OBSENSOR: no frames", flush=True)
    return None


def open_orbbec(index=0, width=640, height=400, fps=30):
    return open_orbbec_sdk(width, height, fps) or open_orbbec_opencv(index)


def main():
    ap = core.make_parser("Let it Go hand tracker - Orbbec Gemini")
    ap.add_argument("--fps", type=int, default=30)
    # Gemini 2 L colour modes are 16:10 / 16:9; 640x400 is the smallest, fastest one.
    ap.set_defaults(width=640, height=400)
    args = ap.parse_args()
    return core.run(lambda: open_orbbec(args.camera, args.width, args.height, args.fps), args)


if __name__ == "__main__":
    sys.exit(main())
