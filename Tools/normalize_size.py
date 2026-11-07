"""Make all round icons the same size.

Each PNG is cropped tightly to its visible pixels (the circle), centered in a
square, and resized so the circle fills a SIZE x SIZE image. After this, every
icon has the same pixel size and the same circle size in Unity.

Usage:
    python normalize_size.py                       # default folders, 1024 px
    python normalize_size.py --size 512
    python normalize_size.py <folder> [<folder> ...] --padding 4

Run after crop_circle.py. Requires: pip install pillow numpy
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


def normalize(img: Image.Image, size: int, padding: int) -> Image.Image:
    rgba = img.convert("RGBA")
    alpha = np.asarray(rgba)[..., 3]
    ys, xs = np.nonzero(alpha > 8)
    if len(xs) == 0:
        raise RuntimeError("image is fully transparent")

    # Square box around the visible circle, centered on its bounding box.
    cx, cy = (xs.min() + xs.max() + 1) / 2, (ys.min() + ys.max() + 1) / 2
    half = max(xs.max() + 1 - xs.min(), ys.max() + 1 - ys.min()) / 2
    box = (cx - half, cy - half, cx + half, cy + half)

    inner = size - 2 * padding
    # Pillow resizes RGBA with premultiplied alpha, so edges stay clean.
    circle = rgba.resize((inner, inner), Image.LANCZOS, box=box)
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.paste(circle, (padding, padding))
    return out


def process_folder(folder: str, size: int, padding: int) -> int:
    if not os.path.isdir(folder):
        print(f"[skip] folder not found: {folder}")
        return 0
    count = 0
    for name in sorted(os.listdir(folder)):
        if not name.lower().endswith(".png"):
            continue
        path = os.path.join(folder, name)
        with Image.open(path) as img:
            before = img.size
            try:
                result = normalize(img, size, padding)
            except RuntimeError as e:
                print(f"[fail] {name}: {e}")
                continue
        result.save(path, "PNG", optimize=True)
        print(f"[ok] {name}: {before[0]}x{before[1]} -> {size}x{size}")
        count += 1
    return count


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("folders", nargs="*", default=DEFAULT_FOLDERS, help="folders that contain .png icons")
    parser.add_argument("--size", type=int, default=1024, help="output width and height in pixels (default 1024)")
    parser.add_argument("--padding", type=int, default=2,
                        help="transparent pixels between circle and image edge (default 2)")
    args = parser.parse_args()

    total = sum(process_folder(f, args.size, args.padding) for f in args.folders)
    print(f"Done: {total} icon(s) resized.")
    if total == 0:
        sys.exit(1)


if __name__ == "__main__":
    main()
