# THE TITLE MENU'S BUTTONS, from the owner's drawings (singleplayer.webp, multiplayer.webp, settings.webp here), headless:
#   blender --background --factory-startup --python menu_buttons.py
# Owner, 2026-11-11: "I want you to replace the singleplayer, multiplayer, and settings buttons with these new drawings i made", then "when you
# hover over the buttons a red aura starts emanating from them as well as the slight grow".
#
# Each drawing is cut to what is drawn, given room round it for the aura, brought down to at most OUT_WIDTH across, and written beside its
# aura (the same size, so they lie one over the other): the drawing's shape grown by AURA_GROW and blurred soft, in red. The game fades the
# aura in under a word pointed at and sends copies of it out from the word (TitleScreenBuilder, UI/Buttons/DrawnFace).
import os

import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "CityX", "UI", "Textures", "Menu"))
WORDS = {"singleplayer": "MenuSingleplayer", "multiplayer": "MenuMultiplayer", "settings": "MenuSettings"}

OUT_WIDTH = 1024        # pixels across at most, room included
ROOM = 0.06             # room round the drawing for the aura, a share of its width
AURA_GROW = 0.012       # the aura's shape is the drawing's grown by this share of its width...
AURA_SOFT = 0.012       # ...and blurred this soft
AURA_COLOUR = (1.0, 0.10, 0.06)
AURA_STRENGTH = 1.8     # its alpha, before it is capped at 1: the near edge solid, the far one fading


def load(path):
    image = bpy.data.images.load(path)
    w, h = image.size
    px = np.array(image.pixels[:], dtype=np.float32).reshape(h, w, 4)
    return np.flipud(px)   # row 0 at the top


def save(px, path):
    h, w = px.shape[:2]
    image = bpy.data.images.new(os.path.basename(path), w, h, alpha=True)
    image.pixels = np.flipud(px).ravel()
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()


def resize(px, w, h):
    """Box-filtered down to w x h (premultiplied, so the clear edges do not bleed dark)."""
    src_h, src_w = px.shape[:2]
    pre = px.copy()
    pre[..., :3] *= pre[..., 3:4]
    ys = (np.arange(h + 1) * src_h / h).astype(int)
    xs = (np.arange(w + 1) * src_w / w).astype(int)
    rows = np.add.reduceat(pre, ys[:-1], axis=0) / np.diff(ys)[:, None, None]
    out = np.add.reduceat(rows, xs[:-1], axis=1) / np.diff(xs)[None, :, None]
    alpha = out[..., 3:4]
    out[..., :3] = np.where(alpha > 1e-4, out[..., :3] / np.maximum(alpha, 1e-4), 0.0)
    return out


def box_blur(a, r):
    if r < 1:
        return a
    for axis in (0, 1):
        pad = [(0, 0), (0, 0)]
        pad[axis] = (r + 1, r)
        c = np.cumsum(np.pad(a, pad), axis=axis)
        hi = np.take(c, np.arange(2 * r + 1, c.shape[axis]), axis=axis)
        lo = np.take(c, np.arange(0, c.shape[axis] - 2 * r - 1), axis=axis)
        a = (hi - lo) / (2 * r + 1)
    return a


def grow(a, r):
    out = a.copy()
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if dx * dx + dy * dy <= r * r:
                out = np.maximum(out, np.roll(np.roll(a, dy, axis=0), dx, axis=1))
    return out


def build(name, out_name):
    px = load(os.path.join(HERE, name + ".webp"))
    drawn = np.nonzero(px[..., 3] > 0.02)
    top, bottom, left, right = drawn[0].min(), drawn[0].max() + 1, drawn[1].min(), drawn[1].max() + 1
    px = px[top:bottom, left:right]
    room = int(round(ROOM * px.shape[1]))
    px = np.pad(px, [(room, room), (room, room), (0, 0)])
    h, w = px.shape[:2]
    if w > OUT_WIDTH:
        h, w = int(round(h * OUT_WIDTH / w)), OUT_WIDTH
        px = resize(px, w, h)

    shape = px[..., 3]
    soft = max(1, int(round(AURA_SOFT * w)))
    aura_alpha = box_blur(box_blur(grow(shape, max(1, int(round(AURA_GROW * w)))), soft), soft)
    aura = np.zeros_like(px)
    aura[..., :3] = AURA_COLOUR
    aura[..., 3] = np.clip(aura_alpha * AURA_STRENGTH, 0.0, 1.0)

    os.makedirs(OUT, exist_ok=True)
    save(px, os.path.join(OUT, out_name + ".png"))
    save(aura, os.path.join(OUT, out_name + "Aura.png"))
    print("%s: %d x %d" % (out_name, w, h))


bpy.ops.wm.read_factory_settings(use_empty=True)
for source, target in WORDS.items():
    build(source, target)
