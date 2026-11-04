# THE KICK HAIRSTYLE, cut down (owner, 2026-11-03: "lower the face count on this hairstyle to a reasonable amount while maintaining the design"). The file came with
# 164,057 triangles and two specks a metre or more below the head. Keeps the biggest piece, then decimates by collapsing edges, which spends its triangles
# where the strands curve and takes them from the flat runs; the silhouette and each lock stay.
#   blender --background --factory-startup --python kickhair_reduce.py -- <source.glb> <out.glb> <triangles>
import sys

import bmesh
import bpy

args = sys.argv[sys.argv.index("--") + 1:]
source, out, target = args[0], args[1], int(args[2])

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=source)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
bpy.ops.object.select_all(action="DESELECT")
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
hair = bpy.context.view_layer.objects.active
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

mesh = bmesh.new()
mesh.from_mesh(hair.data)
seen = set()
pieces = []
for start in mesh.verts:
    if start in seen:
        continue
    piece, stack = [], [start]
    seen.add(start)
    while stack:
        v = stack.pop()
        piece.append(v)
        for e in v.link_edges:
            w = e.other_vert(v)
            if w not in seen:
                seen.add(w)
                stack.append(w)
    pieces.append(piece)
pieces.sort(key=len, reverse=True)
for piece in pieces[1:]:
    bmesh.ops.delete(mesh, geom=piece, context="VERTS")
mesh.to_mesh(hair.data)
mesh.free()
hair.data.update()

triangles = sum(len(p.vertices) - 2 for p in hair.data.polygons)
print("kept the biggest of %d pieces: %d triangles" % (len(pieces), triangles))
if target < triangles:
    decimate = hair.modifiers.new("Decimate", "DECIMATE")
    decimate.decimate_type = "COLLAPSE"
    decimate.ratio = target / triangles
    decimate.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=decimate.name)
for polygon in hair.data.polygons:
    polygon.use_smooth = True
print("wrote %d triangles" % sum(len(p.vertices) - 2 for p in hair.data.polygons))
bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_materials="NONE", export_texcoords=False, export_normals=True, use_selection=False)
