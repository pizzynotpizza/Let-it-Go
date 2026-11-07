"""Remove the solid background from icon images, save them as PNG, and make
them the same size as the other icons.

Usage:
    python remove_bg.py                     # default folders (Work, Happiness)
    python remove_bg.py <file-or-folder> [<file-or-folder> ...]
    python remove_bg.py --tolerance 40 --size 1024 --keep-jpg

How it works:
1. The background color is detected from the image border (median color).
   It works for white, black, or any other solid color.
2. Pixels close to that color (within --tolerance) and connected to the image
   border are treated as background (flood fill from the edges). Similar
   colors inside the icon's outline are kept because they are not connected
   to the border.
3. The background becomes fully transparent. A thin ring around it (--grow
   pixels) is "un-mixed" from the background color (color-to-alpha), so
   anti-aliased edges fade out smoothly instead of leaving a halo.
4. The visible icon is cropped, centered and resized to --size x --size
   (default 1024, the same size as the other icons). Use --size 0 to skip.
5. The result is saved as <name>.png. The old <name>.jpg.meta is renamed to
   <name>.png.meta so Unity keeps the same GUID, and the .jpg is deleted
   (use --keep-jpg to keep it).

Requires: pip install pillow numpy
"""

import argparse
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

from normalize_size import normalize

ICON_DIR = os.path.join(
    os.path.dirname(os.path.abspath(__file__)), "..", "Let it Go", "Assets", "Icon"
)
DEFAULT_FOLDERS = [os.path.join(ICON_DIR, "Work"), os.path.join(ICON_DIR, "Happiness")]
EXTENSIONS = (".jpg", ".jpeg")


def border_color(rgb: np.ndarray) -> np.ndarray:
    """Median color of the outermost pixels."""
    edge = np.concatenate([rgb[0], rgb[-1], rgb[:, 0], rgb[:, -1]])
    return np.median(edge, axis=0)


def background_mask(rgb: np.ndarray, bg: np.ndarray, tolerance: int) -> np.ndarray:
    """Return a bool mask of the background-colored region connected to the border."""
    h, w, _ = rgb.shape
    near = np.abs(rgb - bg).max(axis=2) <= tolerance
    mask_img = Image.fromarray(np.where(near, 255, 0).astype(np.uint8), "L").copy()  # copy: own pixel buffer, else floodfill may not write

    # Flood fill from every background pixel on the border.
    border = (
        [(x, 0) for x in range(w)]
        + [(x, h - 1) for x in range(w)]
        + [(0, y) for y in range(h)]
        + [(w - 1, y) for y in range(h)]
    )
    for x, y in border:
        if mask_img.getpixel((x, y)) == 255:
            ImageDraw.floodfill(mask_img, (x, y), 128)
    return np.array(mask_img) == 128


def grow_mask(mask: np.ndarray, grow: int) -> np.ndarray:
    if grow <= 0:
        return mask
    img = Image.fromarray(np.where(mask, 255, 0).astype(np.uint8), "L")
    return np.array(img.filter(ImageFilter.MaxFilter(grow * 2 + 1))) == 255


def color_to_alpha(rgb: np.ndarray, bg: np.ndarray):
    """Un-mix the background color. Returns (alpha 0..1, un-mixed rgb)."""
    diff = rgb - bg
    room = np.where(diff >= 0, 255.0 - bg, bg)  # max distance possible per channel
    alpha = (np.abs(diff) / np.maximum(room, 1e-6)).max(axis=2)
    safe = np.maximum(alpha, 1e-6)[..., None]
    unmixed = np.clip(bg + diff / safe, 0, 255)
    return alpha, unmixed


def remove_background(img: Image.Image, tolerance: int, grow: int) -> Image.Image:
    rgb = np.asarray(img.convert("RGB")).astype(np.float32)
    bg = border_color(rgb)
    core = background_mask(rgb, bg, tolerance)
    ring = grow_mask(core, grow) & ~core

    alpha, unmixed = color_to_alpha(rgb, bg)
    out_a = np.ones(alpha.shape, np.float32)
    out_a[ring] = alpha[ring]
    out_a[core] = 0.0
    out_rgb = np.where(ring[..., None], unmixed, rgb)
    out_rgb[out_a == 0] = 0

    rgba = np.dstack([out_rgb, out_a * 255.0]).round().astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


def process_file(src: str, args) -> None:
    folder, name = os.path.split(src)
    base = os.path.splitext(name)[0]
    dst = os.path.join(folder, base + ".png")

    with Image.open(src) as img:
        before = img.size
        result = remove_background(img, args.tolerance, args.grow)
    if args.size > 0:
        result = normalize(result, args.size, args.padding)
    result.save(dst, "PNG", optimize=True)

    # Keep the Unity GUID by moving the .meta file to the new name.
    src_meta, dst_meta = src + ".meta", dst + ".meta"
    if os.path.exists(src_meta) and not os.path.exists(dst_meta):
        if args.keep_jpg:
            print(f"  note: kept {name}.meta, Unity will create a new .meta for the PNG")
        else:
            os.replace(src_meta, dst_meta)
    if not args.keep_jpg:
        if os.path.exists(src_meta):
            os.remove(src_meta)
        os.remove(src)

    print(f"[ok] {name} ({before[0]}x{before[1]}) -> {base}.png ({result.size[0]}x{result.size[1]})")


def process_path(path: str, args) -> int:
    """Convert one .jpg file, or every .jpg in a folder. Returns the count."""
    if os.path.isfile(path):
        if os.path.splitext(path)[1].lower() not in EXTENSIONS:
            print(f"[skip] not a .jpg/.jpeg file: {path}")
            return 0
        process_file(path, args)
        return 1
    if not os.path.isdir(path):
        print(f"[skip] not found: {path}")
        return 0
    count = 0
    for name in sorted(os.listdir(path)):
        if os.path.splitext(name)[1].lower() in EXTENSIONS:
            process_file(os.path.join(path, name), args)
            count += 1
    return count


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("paths", nargs="*", default=DEFAULT_FOLDERS, help=".jpg files, or folders that contain .jpg images")
    parser.add_argument("--tolerance", type=int, default=40,
                        help="max per-channel difference from the background color (default 40)")
    parser.add_argument("--grow", type=int, default=2,
                        help="pixels around the background to un-mix for smooth edges (default 2)")
    parser.add_argument("--size", type=int, default=1024,
                        help="output width and height in pixels, 0 = keep original (default 1024)")
    parser.add_argument("--padding", type=int, default=2,
                        help="transparent pixels between icon and image edge (default 2)")
    parser.add_argument("--keep-jpg", action="store_true", help="keep the original .jpg files")
    args = parser.parse_args()

    total = sum(process_path(p, args) for p in args.paths)
    print(f"Done: {total} image(s) converted.")
    if total == 0:
        sys.exit(1)


if __name__ == "__main__":
    main()
