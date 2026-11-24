# THE WOOD PLANK'S TEXTURE, DRAWN (owner, 2026-11-23: "find a free wood texture online that would work well for 1 wood plank"; of the photo found,
# Poly Haven's CC0 "Hinoki Planks": "make your own version of that same design i dont want ours to be too realistic i want it kinda cartoony to
# fit the artstyle"; then, with a drawing of a cartoon plank: "make it textured more like this (without the face)"; docs/WOOD_PLANK.md).
#
# The drawing's look: the broad faces a bright yellow wood covered in short streaks of orange and paler yellow running along the board, a few
# dark cracks, the edges a little darker; the narrow sides a reddish brown with darker streaks. Two boards are drawn, stacked: the upper half for
# the plank's broad faces, the lower half for its narrow sides, ends and chip. Both are seamless left to right (the streaks wrap), so they tile
# along the plank.
#
# "C:/Program Files/Blender Foundation/Blender 4.2/blender.exe" --background --factory-startup --python CityX/SourceArt/WoodPlank/plank_texture.py --
#     CityX/Assets/CityX/Weapons/WoodPlank.png
import os
import sys

import bpy
import numpy as np

OUT = sys.argv[sys.argv.index("--") + 1]

WIDTH = 1024
BOARD = 64

FACE = np.array([0.94, 0.80, 0.40])
FACE_STREAKS = (np.array([0.86, 0.60, 0.24]), np.array([0.90, 0.68, 0.30]), np.array([0.99, 0.90, 0.55]))
FACE_EDGE = np.array([0.80, 0.56, 0.24])
SIDE = np.array([0.62, 0.40, 0.19])
SIDE_STREAKS = (np.array([0.48, 0.29, 0.13]), np.array([0.70, 0.47, 0.24]))
CRACK = np.array([0.30, 0.16, 0.13])

STREAKS = 190           # per board
STREAK_LENGTH = (14, 90)
CRACKS = 4              # on a broad face's board
CRACK_LENGTH = (22, 46)
EDGE_ROWS = 2           # the darker rows along a broad face's edges


def stroke(image, rng, colour, x0, y0, length, half_width):
    """A streak along the board, thickest in its middle and tapering to nothing at both ends, wrapped round left to right."""
    for i in range(int(length)):
        t = i / max(1.0, length - 1.0)
        half = half_width * np.sin(np.pi * t)
        x = int(x0 + i) % WIDTH
        for dy in range(-int(np.ceil(half)), int(np.ceil(half)) + 1):
            y = int(round(y0)) + dy
            if 0 <= y < BOARD and abs(dy) <= half + 0.35:
                image[y, x] = colour


def board(seed, base, streaks, cracks, edge):
    rng = np.random.default_rng(seed)
    image = np.empty((BOARD, WIDTH, 3))
    image[:] = base
    for _ in range(STREAKS):
        colour = streaks[rng.integers(len(streaks))]
        stroke(image, rng, colour, rng.uniform(0, WIDTH), rng.uniform(0, BOARD - 1), rng.uniform(*STREAK_LENGTH), rng.choice([0.6, 0.6, 0.6, 1.0]))
    for _ in range(cracks):
        x0, y0, length = rng.uniform(0, WIDTH), rng.uniform(8, BOARD - 8), rng.uniform(*CRACK_LENGTH)
        stroke(image, rng, CRACK, x0, y0, length, 1.7)
        stroke(image, rng, CRACK, x0 - length * 0.3, y0, length * 1.6, 0.6)   # running on thin past both its ends
    if edge is not None:
        image[:EDGE_ROWS] = edge
        image[-EDGE_ROWS:] = edge
    return image


sheet = np.concatenate([board(7, FACE, FACE_STREAKS, CRACKS, FACE_EDGE), board(23, SIDE, SIDE_STREAKS, 0, None)], axis=0)   # row 0 at the top
pixels = np.ones((sheet.shape[0], WIDTH, 4))
pixels[:, :, :3] = sheet[::-1]                           # Blender's rows run from the bottom
img = bpy.data.images.new("WoodPlank", WIDTH, sheet.shape[0], alpha=False)
img.pixels.foreach_set(pixels.astype(np.float32).ravel())
img.filepath_raw = os.path.abspath(OUT)
img.file_format = "PNG"
img.save()
print("WROTE", os.path.abspath(OUT))
