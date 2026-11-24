# THE BLACK SNEAKERS' MAP, headless (owner, 2026-11-23: "make an all black version of the sneakers texture and make it a black sneakers items
# equippable", "use the bw sneakers and white ones as a guide", "but you can probably just make the white one darker"):
#   blender --background --factory-startup --python sneaker_black.py -- <Wearables/WhiteSneaker.jpg> <Wearables/BlackSneakers.jpg>
# The white pair's own map darkened: each texel's brightness kept as shading (its creases, stitching and the sole's tread still show) but laid
# into a near-black range, a touch cool, so the shoe reads as black rubber and canvas under the cel shading's black line and not as a flat
# silhouette. Same layout as the white and BW maps, so it goes on the same meshes.
import os
import sys

import bpy
import numpy as np

SOURCE, OUT = (os.path.abspath(a) for a in sys.argv[sys.argv.index("--") + 1:][:2])

DARKEST = np.array([0.022, 0.022, 0.025])    # the white map's darkest becomes this (sRGB)
LIGHTEST = np.array([0.115, 0.115, 0.123])   # and its white this (a tad darker than first made: owner, 2026-11-23)
JPEG_QUALITY = 90

image = bpy.data.images.load(SOURCE)
w, h = image.size
pixels = np.array(image.pixels[:], dtype=np.float32).reshape(h, w, 4)
light = pixels[:, :, :3].mean(axis=2, keepdims=True)
low, high = np.percentile(light, 2), np.percentile(light, 98)
t = np.clip((light - low) / max(1e-4, high - low), 0.0, 1.0)
pixels[:, :, :3] = DARKEST + (LIGHTEST - DARKEST) * t
out = bpy.data.images.new("BlackSneakers", w, h, alpha=False)
out.pixels.foreach_set(pixels.ravel())
out.filepath_raw = OUT
out.file_format = "JPEG"
scene = bpy.context.scene
scene.render.image_settings.file_format = "JPEG"
scene.render.image_settings.quality = JPEG_QUALITY
out.save_render(OUT, scene=scene)
print("WROTE", OUT, w, h)
