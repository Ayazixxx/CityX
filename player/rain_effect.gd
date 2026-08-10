extends Node3D
## Local, purely visual+audio rain effect -- toggled by ui/dev_console.gd's
## own "/weather rain"/"/weather clear" (admin-gated, see its own
## _is_admin()) via set_active(). Purely a per-peer local effect, not
## networked -- matching every other dev console toggle in this project
## (/fly, /infinitehealth, /infinitesprint, /xp), none of which sync to
## other peers either.
##
## Parented directly to the player's own root (see player.gd's own _ready())
## rather than the camera, so camera bob/aim never drags the emission volume
## around -- GPUParticles3D's own default local_coords=false already means
## individual drops, once emitted, stay fixed in WORLD space regardless of
## the player subsequently moving/turning; only the EMISSION BOX itself (new
## drops keep spawning wherever the player currently is) follows the
## player's own translation, for free, via inherited transform.

const PARTICLE_COUNT := 500
const EMISSION_BOX_SIZE := Vector3(40.0, 2.0, 40.0)
const EMISSION_HEIGHT_ABOVE_PLAYER := 18.0
const FALL_SPEED := 22.0
const DROP_HEIGHT := 0.9
const DROP_WIDTH := 0.03
# Same drop color the title screen's own 2D rain (rain_layer.gd, a
# completely separate screen-space effect for that menu, not reused here
# directly) already established for this project -- a pale blue-white.
const DROP_COLOR := Color(200.0 / 255.0, 210.0 / 255.0, 255.0 / 255.0, 0.55)
const RainSound := preload("res://title_screen/assets/audio/rain_noises.mp3")

var _particles: GPUParticles3D
var _rain_player: AudioStreamPlayer

func _ready() -> void:
	_particles = _build_particles()
	add_child(_particles)
	_rain_player = AudioStreamPlayer.new()
	_rain_player.stream = RainSound
	_rain_player.bus = &"Music"
	add_child(_rain_player)
	set_active(false)

func _build_particles() -> GPUParticles3D:
	var particles := GPUParticles3D.new()
	particles.amount = PARTICLE_COUNT
	particles.lifetime = EMISSION_HEIGHT_ABOVE_PLAYER / FALL_SPEED
	# Pre-simulates this many seconds before the first visible frame -- turning
	# rain on should look like it's already mid-storm, not an empty box that
	# visibly fills up with drops over the next second.
	particles.preprocess = particles.lifetime
	particles.position = Vector3(0, EMISSION_HEIGHT_ABOVE_PLAYER, 0)
	particles.emitting = false

	var mat := ParticleProcessMaterial.new()
	mat.direction = Vector3(0, -1, 0)
	mat.spread = 2.0
	mat.gravity = Vector3.ZERO # constant fall speed, not accelerating -- terminal-velocity rain, not a dropped object
	mat.initial_velocity_min = FALL_SPEED
	mat.initial_velocity_max = FALL_SPEED * 1.1
	mat.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_BOX
	mat.emission_box_extents = EMISSION_BOX_SIZE / 2.0
	particles.process_material = mat

	var mesh := QuadMesh.new()
	mesh.size = Vector2(DROP_WIDTH, DROP_HEIGHT)
	var visual_mat := StandardMaterial3D.new()
	visual_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	visual_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	visual_mat.albedo_color = DROP_COLOR
	# FIXED_Y (not full ENABLED) keeps each drop's own quad upright (aligned
	# to world-up) while only rotating around Y to face the camera -- reads
	# as a real falling streak from any horizontal viewing angle, rather
	# than a flat blob that goes edge-on and disappears from some angles.
	visual_mat.billboard_mode = BaseMaterial3D.BILLBOARD_FIXED_Y
	mesh.material = visual_mat
	particles.draw_pass_1 = mesh

	return particles

## True while rain is actively falling/audible.
func is_active() -> bool:
	return _particles.emitting

func set_active(active: bool) -> void:
	_particles.emitting = active
	_particles.visible = active
	if active:
		if not _rain_player.playing:
			# AudioStreamMP3 loop, same convention title_screen.gd's own
			# rain_player already establishes for this exact same asset.
			_rain_player.stream.loop = true
			_rain_player.play()
	else:
		_rain_player.stop()
