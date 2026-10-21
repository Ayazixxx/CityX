# The folded T-Shirt, as a shirt lies on the ground before it is picked up (docs/TSHIRT.md): the owner's T-ShirtFolded.fbx (a Meshy model,
# 350,000 triangles, no texture coordinates; not kept in the repo) laid flat, front up, at a folded shirt's size, and reduced to a light game
# mesh. Its texture coordinates are not made here: TShirtBuilder gives it the worn shirt's own layout, so whatever is printed on the worn
# shirt shows on the folded one.
#
#   blender --background --factory-startup --python folded_build.py -- <T-ShirtFolded.fbx> <out .glb> [<preview .png>]
#
# Frames: the source stands up, its front toward Blender -Y and its collar up. It is laid front up (+Z) with the collar toward -Y, which
# the glTF export and Unity's import turn into: lying on y = 0, front up, collar toward +Z, centred on x = 0.
import math
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

FOLDED_WIDTH = 0.80          # across the folded shirt, game metres (the owner: twice a real folded tee, so it reads on the ground)
TRIANGLES = 3000             # a small thing on the ground: as light as a helmet (2-5k); at 2,000 its far corner and collar went ragged
VOXEL = 0.004                # the closed surface it is rebuilt from: a 4 mm grid (the folded shirt is 7 cm thick)
SMOOTHING = 5                # passes evening out the voxel grid's steps before the reduction

args = sys.argv[sys.argv.index("--") + 1:]
SOURCE, OUT = args[0], args[1]
PREVIEW = args[2] if len(args) > 2 else None

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SOURCE)
shirt = next(o for o in bpy.data.objects if o.type == "MESH")
bpy.context.view_layer.objects.active = shirt
shirt.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# Laid front up with the collar toward -Y: a quarter turn about X (front -Y to +Z, collar +Z to +Y), then a half turn about Z.
mesh = shirt.data
lay = Matrix.Rotation(math.pi, 4, "Z") @ Matrix.Rotation(-math.pi / 2.0, 4, "X")
mesh.transform(lay)
xs = [v.co.x for v in mesh.vertices]
scale = FOLDED_WIDTH / (max(xs) - min(xs))
mesh.transform(Matrix.Scale(scale, 4))
lo = Vector((min(v.co.x for v in mesh.vertices), min(v.co.y for v in mesh.vertices), min(v.co.z for v in mesh.vertices)))
hi = Vector((max(v.co.x for v in mesh.vertices), max(v.co.y for v in mesh.vertices), max(v.co.z for v in mesh.vertices)))
mesh.transform(Matrix.Translation(Vector((-(lo.x + hi.x) / 2.0, -(lo.y + hi.y) / 2.0, -lo.z))))   # centred, lying on z = 0
mesh.update()

before = len(mesh.polygons)
size = hi - lo
# Made into one closed, even surface first (a voxel remesh: the source's folds are layers inside each other, and its collar overlaps
# itself), then rebuilt from even quads (QuadriFlow) at the wanted size. Collapsed in one step from 350,000, the collar tore apart and the
# flat top came out in jagged facets; shaded from the source's normals, the hidden inner layers turned patches of it dark.
remesh = shirt.modifiers.new("Even", "REMESH")
remesh.mode = "VOXEL"
remesh.voxel_size = VOXEL
bpy.ops.object.modifier_apply(modifier=remesh.name)
# Only the outer surface kept: between the source's layered folds the voxel grid closed off small pockets of its own, which poked through
# the reduced surface in spikes.
bm = bmesh.new()
bm.from_mesh(shirt.data)
pieces, seen = [], set()
for start in bm.verts:
    if start in seen:
        continue
    piece, frontier = [], [start]
    seen.add(start)
    while frontier:
        v = frontier.pop()
        piece.append(v)
        for e in v.link_edges:
            w = e.other_vert(v)
            if w not in seen:
                seen.add(w)
                frontier.append(w)
    pieces.append(piece)
pieces.sort(key=len)
bmesh.ops.delete(bm, geom=[v for piece in pieces[:-1] for v in piece], context="VERTS")
print("voxel surface: %d pieces, kept the largest (%d points)" % (len(pieces), len(pieces[-1])))
bm.to_mesh(shirt.data)
bm.free()
smooth = shirt.modifiers.new("Smooth", "CORRECTIVE_SMOOTH")   # the voxel grid's steps evened out, the shape kept
smooth.iterations = SMOOTHING
smooth.use_only_smooth = True
bpy.ops.object.modifier_apply(modifier=smooth.name)
decimate = shirt.modifiers.new("Reduce", "DECIMATE")
decimate.decimate_type = "COLLAPSE"
decimate.ratio = TRIANGLES / sum(len(p.vertices) - 2 for p in shirt.data.polygons)
decimate.use_collapse_triangulate = True
bpy.ops.object.modifier_apply(modifier=decimate.name)
for p in shirt.data.polygons:
    p.use_smooth = True
print("folded: %d faces -> %d triangles, %.3f x %.3f x %.3f m (across, along, thick)"
      % (before, len(shirt.data.polygons), size.x, size.y, size.z))

shirt.data.materials.clear()
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True, export_texcoords=False, export_normals=True,
                          export_materials="NONE", export_animations=False, export_skins=False)
print("WROTE", OUT)

if PREVIEW:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x, scene.render.resolution_y = 900, 450
    for i, (offset, name) in enumerate(((Vector((0.0, -0.05, 0.9)), "top"), (Vector((0.6, -0.6, 0.35)), "angle"))):
        cam = bpy.data.objects.new(name, bpy.data.cameras.new(name))
        scene.collection.objects.link(cam)
        cam.location = offset
        cam.rotation_euler = (Vector((0.0, 0.0, 0.0)) - offset).to_track_quat("-Z", "Y").to_euler()
        scene.camera = cam
        scene.render.filepath = PREVIEW.replace(".png", "_%s.png" % name)
        bpy.ops.render.render(write_still=True)
