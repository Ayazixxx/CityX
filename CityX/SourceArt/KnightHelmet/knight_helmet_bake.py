# Run: blender --background --python knight_helmet_bake.py -- <Meshy Silver Sentinel Helm .fbx> <Wearables/Models/KnightHelmet.glb> <its colour .png> <out .png> <out .glb> 512
# (owner's Downloads/Meshy_AI_Silver_Sentinel_Helm_1019020939_texture_fbx.zip, not committed: 40 MB). Writes KnightHelmetSteel.png and the unwrapped KnightHelmet.glb.
# Bakes the Meshy Silver Sentinel Helm's colour map onto the game's 7,390-triangle Knight Helmet: unwraps the low one, then copies the colour
# across surface to surface (Cycles, selected to active). The low one keeps its own placement; the high one is moved onto it.
import bpy, sys, os
args = sys.argv[sys.argv.index("--") + 1:]
fbx, glb, colour_png, out_png, out_glb, size = args[0], args[1], args[2], args[3], args[4], int(args[5])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
hi = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
hi.name = "High"
bpy.ops.import_scene.gltf(filepath=glb)
lo = [o for o in bpy.context.scene.objects if o.type == "MESH" and o != hi][0]
print("low", lo.name, "scale", tuple(lo.scale), "loc", tuple(lo.location))

# The high one into the low one's frame: the owner's own Blender fit (scale 0.49, lifted 2.275 m) of this same model.
hi.matrix_world = lo.matrix_world @ hi.matrix_world

# The high one shows its colour map as emission, so the bake copies exactly the colour (no lighting, no metal darkening).
src = bpy.data.materials.new("SentinelColour"); src.use_nodes = True
nt = src.node_tree; nt.nodes.clear()
tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = bpy.data.images.load(colour_png)
emit = nt.nodes.new("ShaderNodeEmission"); out = nt.nodes.new("ShaderNodeOutputMaterial")
nt.links.new(tex.outputs["Color"], emit.inputs["Color"]); nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
hi.data.materials.clear(); hi.data.materials.append(src)

# The low one unwrapped, with one image to bake into.
bpy.ops.object.select_all(action="DESELECT")
bpy.context.view_layer.objects.active = lo; lo.select_set(True)
bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=1.45, island_margin=0.004, area_weight=1.0, scale_to_bounds=True)
bpy.ops.uv.average_islands_scale()
bpy.ops.uv.pack_islands(rotate=True, margin=0.004)
bpy.ops.object.mode_set(mode="OBJECT")
target = bpy.data.images.new("KnightHelmetSteel", size, size)
dst = bpy.data.materials.new("KnightHelmetBake"); dst.use_nodes = True
node = dst.node_tree.nodes.new("ShaderNodeTexImage"); node.image = target; dst.node_tree.nodes.active = node
lo.data.materials.clear(); lo.data.materials.append(dst)

scene = bpy.context.scene
scene.render.engine = "CYCLES"; scene.cycles.samples = 4; scene.cycles.device = "CPU"
scene.render.bake.use_selected_to_active = True
scene.render.bake.cage_extrusion = 0.02
scene.render.bake.max_ray_distance = 0.06
scene.render.bake.margin = 6
hi.select_set(True); lo.select_set(True); bpy.context.view_layer.objects.active = lo
bpy.ops.object.bake(type="EMIT")
target.filepath_raw = out_png; target.file_format = "PNG"; target.save()

# The low one with its new UVs, for the game (glTF keeps the node's fit; no material or image in it).
bpy.ops.object.select_all(action="DESELECT"); lo.select_set(True)
lo.data.materials.clear()
bpy.ops.export_scene.gltf(filepath=out_glb, use_selection=True, export_format="GLB", export_materials="NONE")
print("BAKED", out_png, out_glb, len(lo.data.vertices), "verts")

# A look at the result: the low one wearing the baked map, from the front and the side.
show = bpy.data.materials.new("Show"); show.use_nodes = True
p = show.node_tree.nodes["Principled BSDF"]
t = show.node_tree.nodes.new("ShaderNodeTexImage"); t.image = target
show.node_tree.links.new(t.outputs["Color"], p.inputs["Base Color"])
lo.data.materials.append(show)
hi.hide_render = True
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"; scene.display.shading.color_type = "TEXTURE"
scene.render.resolution_x = scene.render.resolution_y = 600
from mathutils import Vector
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); scene.collection.objects.link(cam); scene.camera = cam
centre = Vector((0, 0, 2.29))
for name, eye in (("front", (0.4, -2.4, 2.45)), ("side", (2.4, -0.6, 2.4))):
    cam.location = Vector(eye)
    cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = os.path.join(os.path.dirname(out_png), "preview_" + name + ".png")
    bpy.ops.render.render(write_still=True)
print("PREVIEWS done")
