"""
Let it Go - hand tracker entry point (Unity starts this file, see HandTrackingClient.cs).

    --source auto     Orbbec first, then webcam (default)
    --source orbbec   only Orbbec Gemini   (same as hand_tracker_orbbec.py)
    --source webcam   only normal webcam   (same as hand_tracker_webcam.py)
"""

import sys

import hand_tracker_core as core
from hand_tracker_orbbec import open_orbbec
from hand_tracker_webcam import open_webcam


def main():
    ap = core.make_parser("Let it Go hand tracker")
    ap.add_argument("--source", choices=("auto", "orbbec", "webcam"), default="auto")
    ap.add_argument("--fps", type=int, default=30, help="Orbbec fps")
    args = ap.parse_args()

    def open_camera():
        cam = None
        if args.source in ("auto", "orbbec"):
            # Orbbec colour modes are 16:10. 1280x800 RGB gives a sharp picture and still
            # 30 fps (measured: same hand rate as 640x400, ~0.3 more CPU core).
            w, h = (1280, 800) if (args.width, args.height) == (640, 480) else (args.width, args.height)
            cam = open_orbbec(args.camera or 0, w, h, args.fps)
        if cam is None and args.source in ("auto", "webcam"):
            cam = open_webcam(args.camera, args.width, args.height)
        return cam

    return core.run(open_camera, args)


if __name__ == "__main__":
    sys.exit(main())
