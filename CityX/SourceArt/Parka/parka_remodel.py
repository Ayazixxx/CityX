# THE PARKA REMODELLED, in the owner's live Blender (exec after the other parka_*.py files, through the Blender MCP): Meshy's coat, cleaned and
# cut to 250,000 faces ("Coat_mid", with the full-detail "Coat_hi" to read from and the fur's shell "Fur_low"), becomes "Parka", fitted to
# this game's body, and CityX/SourceArt/Parka/Parka.glb.
#   state = build()      the fitted coat in full detail: sleeves cut loose and fitted, left front re-hung
#   finish(state)        cut down to the game's detail, underarms redrawn, faces and map put in order, cuffs added
#   export()             joined with the fur at the owner's placement, laid out afresh on the game's map, written out
# NS holds what is kept between calls: the readings, the panels' traces ("traces", from parka_front.panel_traces on Coat_hi's points), the
# painted atlas ("atlas", from parka_texture.paint_atlas) and the owner's placement of the coat ("owner_place").
TRIANGLES = 10500          # the coat without its fur (the fur's shell is 4,482): 15,000 together, the owner's number
KNIT_TRIANGLES = 2 * 3 * KNIT_SEGMENTS * 2
SPARE_TRIANGLES = 400      # left for what is added after cutting down: the underarms' webs, the hems' undersides
UNDERARM_GRID = 0.03
WORK = "Parka work"        # the collection the working pieces are kept in, hidden


def body_points():
    """The body's points as the coat meets them: the owner's placement of the coat taken off them."""
    body = bpy.data.objects["Body"]
    co = np.empty(len(body.data.vertices) * 3, dtype=np.float32)
    body.data.vertices.foreach_get("co", co)
    m = np.array(NS["owner_place"].inverted() @ body.matrix_world)
    return co.reshape(-1, 3) @ m[:3, :3].T + m[:3, 3]


def build():
    caster = Caster(bpy.data.objects["Coat_hi"])
    NS["readings"] = {side: Reading(side, caster) for side in (1, -1)}
    body = body_points()
    for side, reading in NS["readings"].items():
        reading.set_band(mirrored(side, body))
    state = start_work("Coat_mid")
    bm, world = state["bm"], state["world"]
    for collection in list(state["obj"].users_collection):
        collection.objects.unlink(state["obj"])
    bpy.data.collections[WORK].objects.link(state["obj"])
    drop_loose(bm, world)
    for side in (1, -1):
        cut_sleeve(state, NS["readings"][side])
    drop_loose(bm, world)
    for side in (1, -1):
        end_at_hem(state, NS["readings"][side])
        cut_band(state, NS["readings"][side])
    fit_sleeves(state, NS["readings"])
    for side in (1, -1):
        join_band(state, NS["readings"][side])
        relax_side(state, side)
    verts = [v for v in bm.verts if v[state["part"]] == 0]
    pts = np.array([world @ v.co for v in verts])
    inverse = world.inverted()
    for v, p in zip(verts, straighten(pts, *NS["traces"])):
        v.co = inverse @ Vector(p)
    heal(state)
    show(state)
    return state


def patch_faces(bm, uv, which, side):
    """The faces of one of the new patches, by their chart on the atlas: a sleeve's inner side ("sleeve") or the coat's side wall ("wall")."""
    plain = np.sort(np.array(PLAIN_UV), 0)
    out = []
    for f in bm.faces:
        at = np.array([corner[uv].uv[:] for corner in f.loops])
        if at[:, 0].min() < ATLAS_OLD - 1e-4 or (len(at) == 3 and np.abs(np.sort(at, 0) - plain).max() < 1e-4):
            continue
        u, v = at[:, 0], at[:, 1]
        if which == "sleeve" and v.max() < 0.6 and (v.max() < 0.3 + 1e-4 if side > 0 else v.min() > 0.3 - 1e-4):
            out.append(f)
        if which == "wall" and v.min() > 0.6 - 1e-4 and v.max() < 0.8 + 1e-4 and (u.max() < 0.9 + 1e-4 if side > 0 else u.min() > 0.9 - 1e-4):
            out.append(f)
    return out


def finish(state):
    for name in ("Coat_fitted", "Coat_low"):
        if name in bpy.data.objects:
            bpy.data.objects.remove(bpy.data.objects[name])
    source, work = state["obj"], bpy.data.collections[WORK]
    pieces = []
    for name in ("Coat_fitted", "Coat_low"):
        piece = source.copy()
        piece.data = source.data.copy()
        piece.name = name
        work.objects.link(piece)
        piece.data.materials.clear()
        piece.data.materials.append(bpy.data.materials["ParkaMat"])
        pieces.append(piece)
    fitted, low = pieces
    low.hide_set(False)
    bpy.ops.object.select_all(action="DESELECT")
    low.select_set(True)
    bpy.context.view_layer.objects.active = low
    low.modifiers.new("t", "TRIANGULATE")
    bpy.ops.object.modifier_apply(modifier="t")
    low.modifiers.new("d", "DECIMATE").ratio = (TRIANGLES - KNIT_TRIANGLES - SPARE_TRIANGLES) / len(low.data.polygons)
    bpy.ops.object.modifier_apply(modifier="d")
    bm = bmesh.new()
    bm.from_mesh(low.data)
    uv, part, world = bm.loops.layers.uv.active, bm.verts.layers.int.get("part"), low.matrix_world.copy()
    for side in (1, -1):
        for which in ("sleeve", "wall"):
            new = remesh_patch(bm, patch_faces(bm, uv, which, side), world, uv, UNDERARM_GRID)
            if new is None:
                print("   %s patch, side %d: left as it is (its edge is not one loop)" % (which, side))
            elif which == "sleeve":
                for f in new:
                    for v in f.verts:
                        v[part] = 1 if side > 0 else 2
    close_slits(bm, world)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3])
    settle_faces(bm)
    close_specks(bm, world)
    body = bpy.data.objects["Body"]
    lift_over_body(bm, world, BVHTree.FromPolygons([Vector(p) for p in body_points()], [list(f.vertices) for f in body.data.polygons]))
    mend_map(bm, world, uv, NS["atlas"])
    low_state = {"bm": bm, "world": world, "uv": uv, "part": part}
    for side in (1, -1):
        close_under_hem(low_state, NS["readings"][side])
        knit_cuff(low_state, NS["readings"][side])
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3])
    print("cut down: %d triangles; %d edges with three or more faces, %d open edges" % (
        len(bm.faces), sum(1 for e in bm.edges if len(e.link_faces) > 2), sum(1 for e in bm.edges if e.is_boundary)))
    bm.to_mesh(low.data)
    bm.free()
    low.data.update()
    fitted.hide_set(True)
    source.hide_set(True)
    return low


def export():
    """The finished parka as the one object "Parka" in the owner's scene, at their placement, and Parka.glb beside this file: the coat and
    its fur, laid out afresh on one 1024 map (parka_bake.py), and in a second set of texture coordinates which points are a sleeve's
    (u = 1), for the skinning."""
    folder = os.path.join(os.environ.get("CITYX_REPO", r"C:\Users\Lenovo\Downloads\the game"), "CityX", "SourceArt", "Parka")
    for o in list(bpy.data.objects):
        if o.type == "MESH" and (o.name == "Parka" or o.name.startswith("Parka.") or o.name == "Parka_fitted"):
            bpy.data.objects.remove(o)
    parts = []
    for name in ("Coat_low", "Fur_low"):
        piece = bpy.data.objects[name].copy()
        piece.data = piece.data.copy()
        bpy.context.scene.collection.objects.link(piece)
        piece.hide_set(False)
        parts.append(piece)
    bpy.ops.object.select_all(action="DESELECT")
    for piece in parts:
        piece.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    parka = bpy.context.view_layer.objects.active
    parka.name = parka.data.name = "Parka"
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    mesh = parka.data
    parka.matrix_world = NS["owner_place"].copy()     # the mesh is built where Meshy's coat stood; the object carries the owner's placement
    bpy.context.view_layer.update()
    old = lay_out(parka, [bpy.data.objects["Body"], bpy.data.objects["Head"]])
    NS["parka_old_uv"] = old           # where each corner was on the atlas: every other colour is carried over through it (parka_colourway.py)
    small = np.ones((GAME_MAP, GAME_MAP, 4), dtype=np.float32)
    small[..., :3] = carry_colour(parka, old, NS["atlas"])
    image = bpy.data.images.get("parka_color_1024") or bpy.data.images.new("parka_color_1024", GAME_MAP, GAME_MAP, alpha=False)
    image.pixels.foreach_set(small.ravel())
    image.update()
    image.filepath_raw = os.path.join(folder, "parka_color.jpg")
    image.file_format = "JPEG"
    image.save()
    image.pack()
    mesh.materials.clear()
    mesh.materials.append(bpy.data.materials["ParkaGame"])
    part = np.zeros(len(mesh.vertices), dtype=np.int32)
    mesh.attributes["part"].data.foreach_get("value", part)
    corner = np.empty(len(mesh.loops), dtype=np.int32)
    mesh.loops.foreach_get("vertex_index", corner)
    flag = np.zeros((len(mesh.loops), 2), dtype=np.float32)
    flag[:, 0] = part[corner] > 0
    (mesh.uv_layers.get("Sleeve") or mesh.uv_layers.new(name="Sleeve")).data.foreach_set("uv", flag.ravel())
    mesh.uv_layers.active = mesh.uv_layers[0]
    mesh.uv_layers[0].active_render = True
    for attribute in [a for a in mesh.attributes if a.name in ("part", "mark", "splice", "owner")]:
        mesh.attributes.remove(attribute)
    bpy.ops.object.select_all(action="DESELECT")
    parka.select_set(True)
    bpy.context.view_layer.objects.active = parka
    bpy.ops.export_scene.gltf(filepath=os.path.join(folder, "Parka.glb"), use_selection=True, export_format="GLB",
                              export_image_format="JPEG", export_jpeg_quality=90, export_apply=True, export_yup=True)
    for name in ("Coat_low", "Fur_low"):
        bpy.data.objects[name].hide_set(True)
    print("Parka: %d triangles, %d of its points a sleeve's" % (len(mesh.polygons), (part > 0).sum()))
    return parka
