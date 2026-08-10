extends StaticBody3D
## A real mirror: shows the player's actual reflection, not an environment
## probe's approximation of one.
##
## Technique is planar reflection -- a second Camera3D, mirrored across this
## surface's own plane, renders the real scene into a SubViewport that the
## glass then samples in screen space (see mirror.gdshader for why sampling
## by SCREEN_UV is all the projection math this needs). Godot's own
## ReflectionProbe was NOT used: it bakes a cubemap for rough/metallic
## environment reflections and can't give a sharp, correct, per-frame
## reflection of a moving character standing in front of it, which is the
## entire point here.
##
## Two things this has to actively avoid:
##
## 1. FEEDBACK. The glass is on its own visual layer (GLASS_VISUAL_LAYER)
##    that the reflection camera's cull_mask excludes -- without that, the
##    reflection camera renders the glass, which samples the reflection
##    texture, which contains the glass, and the surface degenerates into
##    the usual infinite-corridor smear. The main camera still sees it
##    normally (its own cull_mask covers every layer).
##
## 2. COST. This renders the whole visible scene a SECOND time, every frame
##    it's active -- easily the most expensive single prop in the game, in a
##    project where per-frame and streaming cost is already the thing that
##    gets reported (see city.gd's own STREAM_CHUNKS_PER_FRAME). So it only
##    renders at all when a player is actually within ACTIVE_DISTANCE AND in
##    front of the glass, and it renders at RESOLUTION_SCALE of the window
##    rather than full size. A mirror is far more forgiving of a soft
##    reflection than the framerate is of a second full-resolution scene
##    pass.

const MIRROR_WIDTH := 1.3
const MIRROR_HEIGHT := 2.1
const FRAME_BORDER := 0.07
const FRAME_DEPTH := 0.09
const GLASS_INSET := 0.01 # keeps the glass off the frame's own front plane, no z-fighting

# Deliberately NOT character.gd's own PORTRAIT_VISUAL_LAYER (1 << 19) --
# that layer exists so the HUD/inventory portrait cameras can see ONLY a
# character; this one exists so the reflection camera can see everything
# EXCEPT this glass. Different jobs, so they get different bits.
const GLASS_VISUAL_LAYER := 1 << 18

const ACTIVE_DISTANCE := 22.0
const RESOLUTION_SCALE := 0.5

const FRAME_COLOR := Color(0.13, 0.13, 0.145)

var _viewport: SubViewport
var _reflect_cam: Camera3D
var _shader_mat: ShaderMaterial
var _player_cam: Camera3D = null

func _ready() -> void:
	_build_visual()
	_build_reflection()

func _build_visual() -> void:
	var frame_mat := StandardMaterial3D.new()
	frame_mat.albedo_color = FRAME_COLOR
	frame_mat.roughness = 0.75
	frame_mat.metallic_specular = 0.1

	# Frame drawn as 4 border slabs around the opening rather than one box
	# behind the glass -- the glass is a real reflection, so anything left
	# sitting behind it would never be seen anyway, and 4 thin slabs is less
	# geometry than a full backing panel.
	var half_w := MIRROR_WIDTH / 2.0
	var border := FRAME_BORDER
	var pieces := [
		[Vector3(MIRROR_WIDTH, border, FRAME_DEPTH), Vector3(0, MIRROR_HEIGHT - border / 2.0, 0)],
		[Vector3(MIRROR_WIDTH, border, FRAME_DEPTH), Vector3(0, border / 2.0, 0)],
		[Vector3(border, MIRROR_HEIGHT, FRAME_DEPTH), Vector3(-half_w + border / 2.0, MIRROR_HEIGHT / 2.0, 0)],
		[Vector3(border, MIRROR_HEIGHT, FRAME_DEPTH), Vector3(half_w - border / 2.0, MIRROR_HEIGHT / 2.0, 0)],
	]
	for piece in pieces:
		var mesh_inst := MeshInstance3D.new()
		var bm := BoxMesh.new()
		bm.size = piece[0]
		mesh_inst.mesh = bm
		mesh_inst.material_override = frame_mat
		mesh_inst.position = piece[1]
		add_child(mesh_inst)

	var collision := CollisionShape3D.new()
	var box_shape := BoxShape3D.new()
	box_shape.size = Vector3(MIRROR_WIDTH, MIRROR_HEIGHT, FRAME_DEPTH)
	collision.shape = box_shape
	collision.position = Vector3(0, MIRROR_HEIGHT / 2.0, 0)
	add_child(collision)

	_shader_mat = ShaderMaterial.new()
	_shader_mat.shader = load("res://mirror/mirror.gdshader")

	var glass := MeshInstance3D.new()
	var quad := QuadMesh.new()
	quad.size = Vector2(MIRROR_WIDTH - border * 2.0, MIRROR_HEIGHT - border * 2.0)
	glass.mesh = quad
	glass.material_override = _shader_mat
	glass.position = Vector3(0, MIRROR_HEIGHT / 2.0, FRAME_DEPTH / 2.0 + GLASS_INSET)
	# See this file's own class comment (point 1): the reflection camera must
	# not be able to see the very surface that displays its output.
	glass.layers = GLASS_VISUAL_LAYER
	add_child(glass)

func _build_reflection() -> void:
	_viewport = SubViewport.new()
	# Inherits the main viewport's World3D (own_world_3d stays false) -- it
	# has to render THIS scene, not an empty one of its own.
	_viewport.own_world_3d = false
	_viewport.transparent_bg = false
	_viewport.render_target_update_mode = SubViewport.UPDATE_DISABLED
	add_child(_viewport)

	_reflect_cam = Camera3D.new()
	_reflect_cam.current = true # current WITHIN this SubViewport only, never the main view
	_reflect_cam.cull_mask = 0xFFFFF & ~GLASS_VISUAL_LAYER
	_viewport.add_child(_reflect_cam)

	_shader_mat.set_shader_parameter("reflection_tex", _viewport.get_texture())

## The local player's camera. Re-resolved whenever it goes invalid rather
## than cached once -- players respawn and (in multiplayer) come and go, and
## a mirror that latched onto a freed camera would simply stop working with
## no obvious cause.
func _find_player_camera() -> Camera3D:
	if _player_cam != null and is_instance_valid(_player_cam):
		return _player_cam
	for player in get_tree().get_nodes_in_group("players"):
		if not player.is_multiplayer_authority():
			continue
		var cam: Camera3D = player.get("camera")
		if cam != null and is_instance_valid(cam):
			_player_cam = cam
			return _player_cam
	return null

func _process(_delta: float) -> void:
	var cam := _find_player_camera()
	if cam == null:
		_viewport.render_target_update_mode = SubViewport.UPDATE_DISABLED
		return

	var normal := global_transform.basis.z.normalized()
	var plane_point := global_position + Vector3(0, MIRROR_HEIGHT / 2.0, 0)
	var to_cam := cam.global_position - plane_point
	var distance_along_normal := to_cam.dot(normal)

	# Nothing to draw when the viewer is too far to make it out, or standing
	# BEHIND the glass (a mirror has no image from its own back face).
	if distance_along_normal <= 0.0 or to_cam.length() > ACTIVE_DISTANCE:
		_viewport.render_target_update_mode = SubViewport.UPDATE_DISABLED
		return
	_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS

	var window_size: Vector2 = get_viewport().get_visible_rect().size
	var target_size := Vector2i(maxi(1, int(window_size.x * RESOLUTION_SCALE)), maxi(1, int(window_size.y * RESOLUTION_SCALE)))
	if _viewport.size != target_size:
		_viewport.size = target_size

	# Reflect the camera's POSITION across the mirror plane, then rebuild a
	# proper right-handed basis via look_at from the reflected forward/up.
	# Assigning a directly-reflected basis instead would be left-handed,
	# which renders the whole scene inside out -- the flip a real mirror
	# actually shows is reapplied in the shader (see its own comment).
	var reflected_origin := cam.global_position - 2.0 * normal * distance_along_normal
	var forward := -cam.global_transform.basis.z
	var up := cam.global_transform.basis.y
	var r_forward := forward - 2.0 * normal * forward.dot(normal)
	var r_up := up - 2.0 * normal * up.dot(normal)
	# Degenerate only if the reflected up and forward line up exactly (the
	# viewer looking straight down the mirror's own normal from directly
	# above/below) -- world up is a safe fallback there.
	if absf(r_forward.normalized().dot(r_up.normalized())) > 0.999:
		r_up = Vector3.UP

	_reflect_cam.global_position = reflected_origin
	_reflect_cam.look_at(reflected_origin + r_forward, r_up)
	_reflect_cam.fov = cam.fov
	_reflect_cam.near = cam.near
	_reflect_cam.far = cam.far
