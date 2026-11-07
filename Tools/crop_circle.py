"""Crop round icons exactly to their circle outline.

Everything outside the circle's outer edge (glow, sparkles, leaves, white
background) becomes transparent. The circle outline itself is kept.

Usage:
    python crop_circle.py                       # default folders (Work, Happiness)
    python crop_circle.py <folder> [<folder> ...]
    python crop_circle.py --inset 1.5 --keep-jpg

Works on .png and .jpg/.jpeg. A semi-transparent PNG is first composited onto
white, so it is safe to run on output of remove_bg.py. Results are saved as
<name>.png. For a .jpg, its .meta is renamed to .png.meta (keeps the Unity
GUID) and the .jpg is deleted unless --keep-jpg is given.

How the circle is found:
1. Rays are cast from the image center. On each ray, the outermost thin dark
   line (darker than both sides) is taken as a candidate outline point.
2. A circle is fitted to the candidates; points far from the fit (leaves,
   sparkles, objects poking out) are rejected and the fit is repeated.
3. The outer edge of the outline is measured on every ray, and the median
   becomes the crop radius. The mask edge is anti-aliased.

Requires: pip install pillow numpy
"""

import argparse
import os
import sys

import numpy as np
from PIL import Image

ICON_DIR = os.path.join(
    os.path.dirname(os.path.abspath(__file__)), "..", "Let it Go", "Assets", "Icon"
)
DEFAULT_FOLDERS = [os.path.join(ICON_DIR, "Work"), os.path.join(ICON_DIR, "Happiness")]
EXTENSIONS = (".png", ".jpg", ".jpeg")
RAYS = 720


def load_on_white(path: str) -> np.ndarray:
    """Load an image as float RGB, compositing any transparency onto white."""
    with Image.open(path) as img:
        rgba = np.asarray(img.convert("RGBA")).astype(np.float32)
    a = rgba[..., 3:4] / 255.0
    return rgba[..., :3] * a + 255.0 * (1.0 - a)


def sample(lum: np.ndarray, cx: float, cy: float, angles: np.ndarray, radii: np.ndarray) -> np.ndarray:
    """Luminance along rays. Returns array [len(angles), len(radii)]."""
    h, w = lum.shape
    xs = np.clip(np.round(cx + np.cos(angles)[:, None] * radii[None, :]).astype(int), 0, w - 1)
    ys = np.clip(np.round(cy + np.sin(angles)[:, None] * radii[None, :]).astype(int), 0, h - 1)
    return lum[ys, xs]


def fit_circle(xs: np.ndarray, ys: np.ndarray):
    """Algebraic least-squares circle fit."""
    a = np.column_stack([xs, ys, np.ones_like(xs)])
    b = xs ** 2 + ys ** 2
    (c0, c1, c2), *_ = np.linalg.lstsq(a, b, rcond=None)
    cx, cy = c0 / 2, c1 / 2
    return cx, cy, np.sqrt(c2 + cx ** 2 + cy ** 2)


def find_circle(rgb: np.ndarray):
    h, w, _ = rgb.shape
    lum = rgb @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
    size = min(h, w)
    cx, cy = w / 2, h / 2
    angles = np.linspace(0, 2 * np.pi, RAYS, endpoint=False)
    radii = np.arange(int(size * 0.20), int(size * 0.499))
    prof = sample(lum, cx, cy, angles, radii)

    # Outermost thin dark line on each ray.
    win = max(6, size // 160)
    px, py = [], []
    for i, p in enumerate(prof):
        for j in range(len(radii) - win - 1, win, -1):
            if p[j] < p[j + win] - 25 and p[j] < p[j - win] - 10:
                # Move to the darkest pixel nearby.
                k = j - win + int(np.argmin(p[j - win:j + 1]))
                px.append(cx + np.cos(angles[i]) * radii[k])
                py.append(cy + np.sin(angles[i]) * radii[k])
                break
    px, py = np.array(px), np.array(py)
    if len(px) < 20:
        raise RuntimeError("circle outline not found")

    keep = np.ones(len(px), bool)
    for tol in (40, 15, 6):
        cx, cy, r = fit_circle(px[keep], py[keep])
        resid = np.abs(np.hypot(px - cx, py - cy) - r)
        keep = resid < tol

    # Outer edge of the outline: where luminance recovers halfway to the outside value.
    radii = np.arange(int(r - win * 2), int(r + win * 6))
    prof = sample(lum, cx, cy, angles, radii)
    edges = []
    for p in prof:
        j_min = int(np.argmin(p[: win * 4]))
        outside = p[-win:].mean()
        target = p[j_min] + 0.5 * (outside - p[j_min])
        rise = np.nonzero(p[j_min:] >= target)[0]
        if len(rise):
            edges.append(radii[j_min + rise[0]])
    r_outer = float(np.median(edges)) if edges else r + win
    return cx, cy, r_outer, keep.mean()


def crop_to_circle(rgb: np.ndarray, inset: float):
    h, w, _ = rgb.shape
    cx, cy, r_outer, inliers = find_circle(rgb)
    r_cut = r_outer - inset
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    dist = np.hypot(xx + 0.5 - cx, yy + 0.5 - cy)
    alpha = np.clip(r_cut - dist + 0.5, 0.0, 1.0)  # 1px anti-aliased edge
    rgba = np.dstack([rgb * (alpha[..., None] > 0), alpha * 255.0]).round().astype(np.uint8)
    return Image.fromarray(rgba, "RGBA"), (cx, cy, r_cut, inliers)


def process_folder(folder: str, inset: float, keep_jpg: bool) -> int:
    if not os.path.isdir(folder):
        print(f"[skip] folder not found: {folder}")
        return 0
    count = 0
    for name in sorted(os.listdir(folder)):
        base, ext = os.path.splitext(name)
        if ext.lower() not in EXTENSIONS:
            continue
        src = os.path.join(folder, name)
        dst = os.path.join(folder, base + ".png")
        if ext.lower() != ".png" and os.path.exists(dst):
            print(f"[skip] {name}: {base}.png already exists")
            continue
        try:
            result, (cx, cy, r, inl) = crop_to_circle(load_on_white(src), inset)
        except RuntimeError as e:
            print(f"[fail] {name}: {e}")
            continue
        result.save(dst, "PNG", optimize=True)

        if ext.lower() != ".png":
            src_meta, dst_meta = src + ".meta", dst + ".meta"
            if not keep_jpg:
                if os.path.exists(src_meta) and not os.path.exists(dst_meta):
                    os.replace(src_meta, dst_meta)
                os.remove(src)

        print(f"[ok] {name} -> {base}.png  center=({cx:.0f},{cy:.0f}) radius={r:.0f} inliers={inl:.0%}")
        count += 1
    return count


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("folders", nargs="*", default=DEFAULT_FOLDERS, help="folders that contain icon images")
    parser.add_argument("--inset", type=float, default=1.0,
                        help="pixels to cut inside the outline's outer edge, removes light fringe (default 1)")
    parser.add_argument("--keep-jpg", action="store_true", help="keep the original .jpg files")
    args = parser.parse_args()

    total = sum(process_folder(f, args.inset, args.keep_jpg) for f in args.folders)
    print(f"Done: {total} image(s) cropped.")
    if total == 0:
        sys.exit(1)


if __name__ == "__main__":
    main()
