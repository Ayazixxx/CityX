# ANOTHER COLOUR OF THE FUR HOODED PARKA (exec after the other parka_*.py files, in the owner's Blender): Meshy's model retextured in a new
# colour is the same mesh with the same texture coordinates, so its map goes onto our parka through the same steps as the green one's.
#   pixels = load_map(path)                   Meshy's 4096 colour map, rows from the bottom, RGBA
#   pixels = clean_map(pixels)                its stray pale spots on the cloth and stray cloth colour on the fur repainted
#   small = colour_parka(pixels, out_path)    painted into the parka's atlas, carried onto the game's layout, saved as a 1024 JPEG
# NS holds what this needs from the green one: which texels Meshy's cloth and fur faces cover ("owners", fur then cloth), the texels any
# face uses grown a little ("used_grown"), the sleeve readings, and the parka's places on the old atlas ("parka_old_uv", from export()).
PALE_CLOTH = 0.26          # a cloth texel whose darkest channel is above this is a pale spot: fur colour or white, never the cloth's own
STRAY_ON_FUR = 0.06        # a fur texel this much redder than its other channels has the cloth's colour on it
DARK_ON_FUR = 0.6          # and one this dark too (the fur's own shading stays lighter: its darkest 1 in 100 is 0.56 on the green)
SPOT_RIM = 2               # texels round a spot repainted with it: its soft rim
FILL_RADII = (3, 8, 20, 48, 120, 300)
PAD_RADII = (2, 8, 32, 128)


def load_map(path):
    image = bpy.data.images.load(path, check_existing=True)
    pixels = np.empty(image.size[0] * image.size[1] * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    return pixels.reshape(image.size[1], image.size[0], 4)


def fill_from_around(rgb, region, holes):
    """The holes' texels painted with the mean of the sound texels of region round them, the nearest that has some."""
    sound = (region & ~holes).astype(np.float32)
    out = rgb.copy()
    left = holes.copy()
    for radius in FILL_RADII:
        weight = box_blur(sound, radius)
        total = box_blur(rgb * sound[..., None], radius)
        reached = left & (weight > 0.02)
        out[reached] = total[reached] / weight[reached][:, None]
        left &= ~reached
        if not left.any():
            break
    out[left] = np.median(rgb[region & ~holes], axis=0)
    return out


def clean_map(pixels):
    """Meshy's map with its stray spots repainted (owner, 2026-11-21, of the maroon one: "fix all these blotchy white spots make it a smooth
    red all over", and "a blotch of the tan from the fur appearing on whats supposed to be a maroon part of the jacket"): on the texels only
    the cloth's faces cover, every pale spot (white specks, the fur's tan) takes the cloth round it; on the fur's, any cloth colour takes
    the fur round it."""
    fur, cloth = NS["owners"]
    cloth_only, fur_only = cloth & ~fur, fur & ~cloth
    rgb = pixels[..., :3]

    def with_rim(spots, region):
        return region & (box_blur(spots.astype(np.float32), SPOT_RIM) > 0.0)

    pale = with_rim(cloth_only & (rgb.min(-1) > PALE_CLOTH), cloth_only)
    redder = rgb[..., 0] - np.maximum(rgb[..., 1], rgb[..., 2])
    stray = with_rim(fur_only & ((redder > STRAY_ON_FUR) | (rgb.mean(-1) < DARK_ON_FUR)), fur_only)
    out = pixels.copy()
    out[..., :3] = fill_from_around(rgb, cloth_only, pale)
    out[..., :3] = np.where(stray[..., None], fill_from_around(rgb, fur_only, stray), out[..., :3])
    print("map cleaned: %d cloth texels were pale spots, %d fur texels had the cloth's colour" % (pale.sum(), stray.sum()))
    return out


def pad_map(pixels):
    """Texels no face uses take the colour of the used ones round them, so nothing else bleeds into a face's edge."""
    used = NS["used_grown"]
    out = pixels.copy()
    left = ~used
    for radius in PAD_RADII:
        weight = box_blur(used.astype(np.float32), radius)
        total = box_blur(pixels[..., :3] * used[..., None], radius)
        reached = left & (weight > 0.02)
        out[..., :3][reached] = total[reached] / weight[reached][:, None]
        left &= ~reached
    return out


def colour_parka(pixels, out_path):
    """The map painted into the parka's atlas (the sleeves' patches, the lining and the ribbing from its own cloth), carried onto the game's
    layout of the finished parka, and saved as the colour's 1024 JPEG. Returns the 1024 texels."""
    readings = NS["readings"]
    atlas = paint_atlas(pad_map(pixels), readings, Caster(bpy.data.objects["Coat_hi"]), bpy.data.objects["Coat_hi"])
    small = np.ones((GAME_MAP, GAME_MAP, 4), dtype=np.float32)
    small[..., :3] = carry_colour(bpy.data.objects["Parka"], NS["parka_old_uv"], atlas)
    name = os.path.splitext(os.path.basename(out_path))[0]
    image = bpy.data.images.get(name) or bpy.data.images.new(name, GAME_MAP, GAME_MAP, alpha=False)
    image.pixels.foreach_set(small.ravel())
    image.update()
    image.filepath_raw = out_path
    image.file_format = "JPEG"
    image.save()
    print("WROTE %s" % out_path)
    return small
