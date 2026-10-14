# A LOOK AT THE REFERENCE (owner, 2026-10-14: "use this file attached as a guide but remake it yourself"): imports the free AK-47 reload FBX into an
# empty scene, prints what is in it (meshes with their sizes, the armature's bones, the actions and their frame ranges) and renders frames of the
# reload from the side and from above, so the model's proportions and the reload's phases can be STUDIED. Nothing from it is kept or exported.
#   blender --background --python ak47_reference_probe.py -- <fbx path> <out dir>
import bpy
import math
import os
import sys
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
FBX, OUT = args[0], args[1]
os.makedirs(OUT, exist_ok=True)

for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)

bpy.ops.import_scene.fbx(filepath=FBX, use_anim=True)
scene = bpy.context.scene
print("=== OBJECTS ===")
for o in bpy.data.objects:
    bb = [o.matrix_world @ Vector(c) for c in o.bound_box]
    lo = Vector((min(v.x for v in bb), min(v.y for v in bb), min(v.z for v in bb)))
    hi = Vector((max(v.x for v in bb), max(v.y for v in bb), max(v.z for v in bb)))
    print("%-30s %-9s parent=%-20s size=(%.3f %.3f %.3f) min=(%.3f %.3f %.3f) scale=(%.3f %.3f %.3f)" % (
        o.name, o.type, o.parent.name if o.parent else "-", *(hi - lo), *lo, *o.scale))
    if o.type == 'MESH':
        print("      verts %d, materials %s, vertex groups %s" % (len(o.data.vertices), [m.name for m in o.data.materials if m],
                                                                  [g.name for g in o.vertex_groups][:40]))
    if o.type == 'ARMATURE':
        for b in o.data.bones:
            print("      bone %-28s parent=%-22s head=(%.3f %.3f %.3f) len=%.3f" % (b.name, b.parent.name if b.parent else "-", *b.head_local, b.length))
print("=== ACTIONS ===")
for a in bpy.data.actions:
    print("%-30s frames %s  fcurves %d" % (a.name, a.frame_range, len(a.fcurves)))
    groups = sorted(set(fc.group.name for fc in a.fcurves if fc.group))
    print("      groups", groups[:60])
print("fps", scene.render.fps, "range", scene.frame_start, scene.frame_end)

# Renders: Workbench, from the right side, the left side and above, at a spread of frames.
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'MATERIAL'
scene.display.shading.show_shadows = True
scene.render.resolution_x, scene.render.resolution_y = 900, 600
scene.render.image_settings.file_format = 'PNG'
cam = bpy.data.objects.new("ProbeCam", bpy.data.cameras.new("ProbeCam"))
scene.collection.objects.link(cam)
scene.camera = cam
cam.data.clip_start = 0.001
cam.data.clip_end = 10000.0

# the whole scene's bounds, at the first frame
scene.frame_set(scene.frame_start)
bpy.context.view_layer.update()
pts = []
for o in bpy.data.objects:
    if o.type == 'MESH':
        pts += [o.matrix_world @ Vector(c) for c in o.bound_box]
lo = Vector((min(v.x for v in pts), min(v.y for v in pts), min(v.z for v in pts)))
hi = Vector((max(v.x for v in pts), max(v.y for v in pts), max(v.z for v in pts)))
centre = (lo + hi) * 0.5
extent = max(hi - lo) * 1.4
print("scene bounds min", tuple(lo), "max", tuple(hi), "centre", tuple(centre), "extent", extent)


def look(position, target):
    cam.location = position
    cam.rotation_euler = (Vector(target) - Vector(position)).to_track_quat('-Z', 'Y').to_euler()
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = extent


# The action's own range, not the scene's (the scene runs on past the last key).
ranges = [a.frame_range for a in bpy.data.actions]
first = int(min(r[0] for r in ranges)) if ranges else int(scene.frame_start)
last = int(max(r[1] for r in ranges)) if ranges else int(scene.frame_end)
count = int(args[2]) if len(args) > 2 else 10
frames = sorted(set(int(round(first + (last - first) * i / (count - 1))) for i in range(count)))
for f in frames:
    scene.frame_set(f)
    bpy.context.view_layer.update()
    for name, offset in (("x", Vector((extent * 3, 0, 0))), ("negy", Vector((0, -extent * 3, 0))), ("z", Vector((0, 0, extent * 3)))):
        look(centre + offset, centre)
        scene.render.filepath = os.path.join(OUT, "ref_%03d_%s.png" % (f, name))
        bpy.ops.render.render(write_still=True)
print("rendered frames", frames)
