extends RigidBody3D
## A world radio prop: starts OFF, press E (interact) while nearby to power
## it on/off. Cycles through PLAYLIST below, advancing automatically once
## each track finishes, and pauses/resumes the CURRENT track's own position
## rather than restarting it every time it's toggled.
##
## Same "own Area3D + poll interact in _process()" pattern
## city/building_door.gd already establishes for a static world
## interactable -- see its own class comment. There's no player-side
## "nearby interactable" registry in this project; every interactable owns
## its own proximity detection independently. Unlike the door, this is a
## real .tscn (not set_script-attached), so _process() just works without
## needing the door's own set_process(true) workaround.
##
## Audio is real 3D positional sound (AudioStreamPlayer3D), not a hand-
## rolled distance fade -- Godot's own built-in inverse-distance
## attenuation (the engine default) already makes it loud up close and
## fade out smoothly with distance, silent past max_distance.
##
## RigidBody3D, not the StaticBody3D this started as -- a real reported gap
## ("it wasnt letting me carry the radio"): player.gd's own grab/carry
## system only ever targets RigidBody3D in the "grabbable" group. Stays on
## collision_layer 1 (the RigidBody3D/StaticBody3D shared default, never
## changed) rather than moving to the bottle's own dedicated layer 16 --
## that's what keeps it a normal solid obstacle to ordinary player movement
## (unchanged from before) AND already inside player.gd's own grab raycast
## mask (1 | 8 | 16) with zero changes needed there.
##
## Local-only: on/off state, track position, AND now carry state aren't
## networked, same known gap building_door.gd's own open/closed state
## already has for this project's world interactables in general -- every
## peer in a multiplayer session hears/sees their own copy independently.

const INTERACT_RANGE := 2.5
const PLAYLIST: Array[AudioStream] = [
	preload("res://radio/assets/audio/ooga_booga.mp3"),
	preload("res://radio/assets/audio/agriculture.mp3"),
]
# Only really audible within a few meters, fully silent past ~18m -- a
# "certain proximity" radius, not something that carries across a whole
# city block.
const AUDIO_UNIT_SIZE := 4.0
const AUDIO_MAX_DISTANCE := 18.0

# Godot's own OBJ importer resolves this specific file straight to an
# ArrayMesh (same confirmed behavior weapons/pistol.gd's own ModelRes
# already relies on for Beretta_M9.obj), not a PackedScene.
const RadioMesh := preload("res://radio/models/crimson_boombox/Meshy_AI_Crimson_Boombox_0805062355_texture.obj")
const RadioTexture := preload("res://radio/models/crimson_boombox/Meshy_AI_Crimson_Boombox_0805062355_texture.png")
# Meshy's own export isn't in real-world units (raw AABB's largest
# dimension is exactly 1.0) -- normalized the same
# AABB-longest-dimension-to-a-target-length way pistol.gd's own
# _build_visual() already does for its imported .obj, to a plausible
# boombox width.
const TARGET_LENGTH_METERS := 0.55

var _nearby_player: Node3D = null
var _powered_on := false
var _track_index := 0
var _player: AudioStreamPlayer3D

func _ready() -> void:
	collision_layer = 1
	# A portable boombox, not a heavy appliance -- light enough to carry
	# comfortably (see player.gd's own grab/carry system) but noticeably
	# heavier than the glass bottle (mass 0.4), since it's a visibly bigger
	# object. Same shared bounce/friction feel as every other grabbable
	# object in this project (see character.gd's own RAGDOLL_PHYSICS_
	# MATERIAL comment) -- a real reported gap, not previously grabbable at
	# all as a StaticBody3D.
	mass = 3.0
	var bounce_mat := PhysicsMaterial.new()
	bounce_mat.bounce = 0.25
	bounce_mat.friction = 0.7
	physics_material_override = bounce_mat
	add_to_group("grabbable")
	_build_visual()
	_build_audio()
	_build_interact_area()

## Re-centers on the mesh's own AABB middle (X/Z) and lifts it so its own
## lowest point sits at local y=0 (so the whole prop rests ON the ground
## once placed, rather than being buried to its own vertical center) --
## same centering approach pistol.gd's own _build_visual() uses, just also
## keeping the bottom grounded instead of centering vertically too, since
## a placed world prop needs to actually stand on the floor.
func _build_visual() -> void:
	var mesh_inst := MeshInstance3D.new()
	mesh_inst.mesh = RadioMesh
	add_child(mesh_inst)

	var aabb: AABB = mesh_inst.get_aabb()
	var largest_dim: float = maxf(aabb.size.x, maxf(aabb.size.y, aabb.size.z))
	var scale_factor: float = TARGET_LENGTH_METERS / largest_dim if largest_dim > 0.0001 else 1.0
	mesh_inst.scale = Vector3.ONE * scale_factor
	mesh_inst.position = Vector3(-aabb.get_center().x, -aabb.position.y, -aabb.get_center().z) * scale_factor

	var mat := StandardMaterial3D.new()
	mat.albedo_texture = RadioTexture
	mesh_inst.material_override = mat

	var scaled_size: Vector3 = aabb.size * scale_factor
	var collision := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = scaled_size
	collision.shape = box
	collision.position = Vector3(0, scaled_size.y / 2.0, 0)
	add_child(collision)

func _build_audio() -> void:
	_player = AudioStreamPlayer3D.new()
	_player.bus = "Music"
	_player.unit_size = AUDIO_UNIT_SIZE
	_player.max_distance = AUDIO_MAX_DISTANCE
	_player.autoplay = false
	# Roughly speaker height on the boombox's own body, not floor level.
	_player.position = Vector3(0, TARGET_LENGTH_METERS * 0.4, 0)
	add_child(_player)
	_player.finished.connect(_on_track_finished)

func _build_interact_area() -> void:
	var interact_area := Area3D.new()
	interact_area.collision_layer = 0
	interact_area.collision_mask = 4
	var interact_shape := CollisionShape3D.new()
	var interact_sphere := SphereShape3D.new()
	interact_sphere.radius = INTERACT_RANGE
	interact_shape.shape = interact_sphere
	interact_area.add_child(interact_shape)
	add_child(interact_area)
	interact_area.body_entered.connect(_on_interact_area_body_entered)
	interact_area.body_exited.connect(_on_interact_area_body_exited)

func _on_interact_area_body_entered(body: Node3D) -> void:
	if body is CharacterBody3D and body.has_method("respawn"):
		_nearby_player = body

func _on_interact_area_body_exited(body: Node3D) -> void:
	if body == _nearby_player:
		_nearby_player = null

func _process(_delta: float) -> void:
	if _nearby_player == null:
		return
	if not Input.is_action_just_pressed("interact"):
		return
	_toggle()

func _toggle() -> void:
	_powered_on = not _powered_on
	if _powered_on:
		if _player.playing:
			_player.stream_paused = false
		else:
			_play_current_track()
	else:
		_player.stream_paused = true

func _play_current_track() -> void:
	_player.stream = PLAYLIST[_track_index]
	_player.stream_paused = false
	_player.play()

## Only fires while actually playing (a paused stream never reaches its own
## end) -- advances the playlist and keeps going as long as the radio is
## still powered on.
func _on_track_finished() -> void:
	_track_index = (_track_index + 1) % PLAYLIST.size()
	if _powered_on:
		_play_current_track()

func is_powered_on() -> bool:
	return _powered_on
