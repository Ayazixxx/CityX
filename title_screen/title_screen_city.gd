extends Node3D
## Real in-game 3D backdrop for the title screen -- a hand-placed skyline
## silhouette built from the SAME procedural building generator the actual
## streamed city uses (city_chunk.gd's own _build_building/build_materials),
## not a simplified fake, so the title screen reads as an authentic preview
## of the game world. Composited behind the existing 2D rain/vignette/eye
## layers via a SubViewport (see title_screen.tscn) -- this scene owns
## nothing about that compositing itself, it just renders a self-contained
## 3D shot.
##
## The buildings are fixed/hand-placed (not seeded-random like the real
## city) so the skyline's silhouette reliably matches the reference art's
## staggered-height composition every time the title screen loads.

const CityChunk = preload("res://city/city_chunk.gd")

const SKY_COLOR := Color(0.106, 0.114, 0.220)
const HORIZON_COLOR := Color(0.216, 0.235, 0.373)
const BUILDING_BODY_COLOR := Color(0.086, 0.094, 0.153)
const WINDOW_LIGHT_COLOR := Color(0.988, 0.796, 0.443)

# How far the whole skyline (and its matching ground plane) sinks below
# world y=0 -- without this the buildings' own ground-relative base (see
# _build_building's own "ground-relative" comment, y=0 always) sits well
# inside the camera's frame with visible empty sky beneath it, reading as
# "floating" (a real reported bug). Sinking both the buildings AND a flat
# ground plane by the same amount keeps their base flush with the plane
# regardless of exact camera-angle math.
const GROUND_Y := -18.0

const LogoTexture := preload("res://title_screen/assets/logo_cityx.png")
# Matches logo_cityx.png's own 942x714 aspect (1.319), NOT a square: that
# file is the hand-drawn CITY X art with its white background flood-filled
# to transparent and then cropped to the artwork's real bounds (see
# tests/scratch_logo_alpha.gd, which prints exactly this height). Cropping
# is why the aspect has to be stated here rather than left at 1:1 -- the
# source canvas had wide empty margins that would otherwise have shrunk the
# visible logo well inside its own quad. Width stays 16m so the logo keeps
# the same apparent scale against the skyline as the art it replaced.
const LOGO_SIZE := Vector2(16.0, 12.13)
# Centered horizontally (world x=0, matching the camera's own centered
# framing), well above the tallest SKYLINE_BUILDINGS entry's own post-
# GROUND_Y-shift roofline (38 - 18 = 20) so it reads in the open sky above
# the skyline rather than threading between rooftops, and pushed further
# back in Z than any building (see SKYLINE_BUILDINGS' own z offsets, all
# > -12) so it never gets clipped by a near building's silhouette.
const LOGO_POSITION := Vector3(0.0, 27.0, -12.0)

# {size, offset, roof} -- a staggered skyline silhouette loosely mimicking
# the reference art's varied-height buildings along the bottom of frame.
# Sizes/offsets are in meters, offset.y always 0 (buildings sit on the
# ground plane, see GROUND_Y for where that plane actually sits);
# _build_building measures height from size.y itself.
const SKYLINE_BUILDINGS := [
	{"size": Vector3(9, 22, 9), "offset": Vector3(-30, 0, 4), "roof": ""},
	{"size": Vector3(11, 30, 10), "offset": Vector3(-19, 0, 1), "roof": "parapet"},
	{"size": Vector3(8, 17, 8), "offset": Vector3(-8, 0, 6), "roof": ""},
	{"size": Vector3(12, 38, 11), "offset": Vector3(4, 0, -2), "roof": "water_tower"},
	{"size": Vector3(9, 25, 9), "offset": Vector3(17, 0, 3), "roof": "antenna"},
	{"size": Vector3(10, 19, 10), "offset": Vector3(29, 0, 5), "roof": "shed"},
]

var _logo_mat: StandardMaterial3D
var _logo_root: Node3D

func _ready() -> void:
	_build_environment()
	_build_lighting()
	_build_skyline()
	_build_logo()
	_build_camera()

func _build_environment() -> void:
	var env := Environment.new()
	env.background_mode = Environment.BG_SKY
	var sky_mat := ProceduralSkyMaterial.new()
	sky_mat.sky_top_color = SKY_COLOR
	sky_mat.sky_horizon_color = HORIZON_COLOR
	sky_mat.ground_bottom_color = SKY_COLOR
	sky_mat.ground_horizon_color = HORIZON_COLOR
	sky_mat.sun_angle_max = 2.0
	var sky := Sky.new()
	sky.sky_material = sky_mat
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.ambient_light_energy = 0.6
	env.fog_enabled = true
	env.fog_light_color = HORIZON_COLOR
	env.fog_density = 0.012
	env.fog_aerial_perspective = 0.4
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC

	var world_env := WorldEnvironment.new()
	world_env.name = "WorldEnvironment"
	world_env.environment = env
	add_child(world_env)

func _build_lighting() -> void:
	var moon := DirectionalLight3D.new()
	moon.name = "DirectionalLight3D"
	moon.light_color = Color(0.71, 0.75, 0.98)
	moon.light_energy = 0.55
	moon.shadow_enabled = true
	moon.rotation_degrees = Vector3(-35.0, -35.0, 0.0)
	add_child(moon)

func _build_skyline() -> void:
	var materials := CityChunk.build_materials()
	var root := Node3D.new()
	root.name = "Skyline"
	root.position.y = GROUND_Y
	add_child(root)
	for entry: Dictionary in SKYLINE_BUILDINGS:
		CityChunk._build_building(root, entry["size"], entry["offset"], entry["roof"], materials, null, BUILDING_BODY_COLOR, null)
	_light_up_windows(root)
	_build_ground_plane()

## A big flat plane flush with the (sunk) buildings' own base -- see
## GROUND_Y's own comment for why this exists: guarantees zero visible gap
## under the skyline regardless of exactly how the camera's angle/distance
## math works out.
func _build_ground_plane() -> void:
	var plane_mesh := PlaneMesh.new()
	plane_mesh.size = Vector2(400.0, 400.0)
	var mat := StandardMaterial3D.new()
	mat.albedo_color = BUILDING_BODY_COLOR
	mat.roughness = 1.0
	mat.metallic_specular = 0.0
	plane_mesh.material = mat

	var inst := MeshInstance3D.new()
	inst.name = "Ground"
	inst.mesh = plane_mesh
	inst.position = Vector3(0.0, GROUND_Y, 0.0)
	add_child(inst)

## The reference art's skyline reads as a lit-up city at night (bright
## window squares against a dark silhouette) -- city_chunk.gd's own glass
## is a dim, mostly-transparent pane by design (meant to be seen THROUGH
## during real gameplay), so here we swap every glass surface for a plain
## emissive material instead of touching city_chunk.gd itself, keeping this
## purely a title-screen presentation choice.
func _light_up_windows(root: Node3D) -> void:
	var lit_mat := StandardMaterial3D.new()
	lit_mat.albedo_color = WINDOW_LIGHT_COLOR
	lit_mat.emission_enabled = true
	lit_mat.emission = WINDOW_LIGHT_COLOR
	lit_mat.emission_energy_multiplier = 1.4
	_replace_glass_materials(root, lit_mat)

func _replace_glass_materials(node: Node, lit_mat: StandardMaterial3D) -> void:
	if node is MeshInstance3D:
		var mesh_inst: MeshInstance3D = node
		var mat := mesh_inst.material_override
		if mat != null and mat is BaseMaterial3D and (mat as BaseMaterial3D).transparency != BaseMaterial3D.TRANSPARENCY_DISABLED:
			mesh_inst.material_override = lit_mat
	for child in node.get_children():
		_replace_glass_materials(child, lit_mat)

## The real "CITY X" reference art itself (LogoTexture), not a procedural
## recreation -- an earlier version of this hand-built chunky-letters-plus-
## red-X-slash out of TextMesh/BoxMesh geometry to APPROXIMATE the reference
## art; a direct, transparent-background export of that same art replaced
## it here, so this is now just a single unlit-emissive textured quad,
## billboard-style (always facing the fixed title-screen camera, so no
## per-frame rotation logic is needed the way a real in-world billboard
## would need).
func _build_logo() -> void:
	var logo_root := Node3D.new()
	logo_root.name = "Logo3D"
	logo_root.position = LOGO_POSITION
	add_child(logo_root)
	_logo_root = logo_root

	var mat := StandardMaterial3D.new()
	mat.albedo_texture = LogoTexture
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.emission_enabled = true
	mat.emission_texture = LogoTexture
	mat.emission_energy_multiplier = 0.5
	_logo_mat = mat

	var quad := QuadMesh.new()
	quad.size = LOGO_SIZE
	quad.material = mat

	var inst := MeshInstance3D.new()
	inst.mesh = quad
	logo_root.add_child(inst)

## Called every frame by title_screen.gd's own flicker logic (redirected
## from the old 2D Logo TextureRect's modulate.a) -- drives the same
## flickering-sign look via emission strength instead of alpha, since a 3D
## emissive mesh has no simple "opacity" the way a TextureRect does.
func set_logo_flicker(opacity: float) -> void:
	if _logo_mat != null:
		_logo_mat.emission_energy_multiplier = lerpf(0.15, 1.4, opacity)
		_logo_mat.albedo_color.a = opacity

## Companion to set_logo_flicker() -- title_screen.gd's own hide_foreground_ui()/
## reset_to_initial() toggle the 3D logo's visibility the same way they used
## to toggle the old 2D Logo TextureRect's own `visible`.
func set_logo_visible(v: bool) -> void:
	if _logo_root != null:
		_logo_root.visible = v

func _build_camera() -> void:
	var camera := Camera3D.new()
	camera.name = "Camera3D"
	camera.position = Vector3(0.0, 16.0, 46.0)
	camera.rotation_degrees = Vector3(-8.0, 0.0, 0.0)
	camera.fov = 55.0
	camera.current = true
	add_child(camera)
