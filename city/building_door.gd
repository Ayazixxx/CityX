extends Node3D
## An openable door filling a building's own front entranceway -- press E
## (interact) while nearby to swing it open/closed. Attached via set_script
## (not a .tscn), matching city/building_glass.gd's own "everything built
## procedurally, driven by an explicit setup() call, no _ready() reliance"
## pattern -- city_chunk.gd's OWN building system has no .tscn anywhere
## either (every wall/floor/stair is SurfaceTool-built code), so a
## companion .tscn here would be the only exception rather than matching
## the established convention.
##
## Swings around its own LEFT edge (the door opening's own u0, see
## city_chunk.gd's own DOOR_WIDTH/door dict) inward into the building, a
## real door's own natural motion -- outward would swing out over the
## sidewalk, reading wrong and risking shoving a player standing just
## outside it.
##
## The panel is an AnimatableBody3D, not a plain StaticBody3D -- Godot's own
## docs call this out specifically for a kinematically-ANIMATED static-role
## body (a moving platform, an opening door): a plain StaticBody3D's own
## collision shape is treated by the physics server as not moving between
## frames unless told otherwise, which risks a player standing right at the
## threshold getting tunneled through or shoved incorrectly as the panel
## swings past them.
##
## Local-only: open/closed state isn't networked -- same known, already-
## documented gap city/building_glass.gd's own breakable-glass state has
## (see its own class comment) for this project's city interactables in
## general. Every peer in a multiplayer session opens/closes their own copy
## independently.

const CityChunk = preload("res://city/city_chunk.gd")

const DOOR_THICKNESS := 0.08
const DOOR_COLOR := Color(0.42, 0.28, 0.16) # warm wood brown -- a fixed prop accent, not a FACADE_VARIANTS entry, same precedent as _add_water_tower's own tank color
const HANDLE_COLOR := Color(0.72, 0.68, 0.55)
const OPEN_ANGLE_DEGREES := 100.0 # a hair past 90 -- reads as "pushed open", not just precisely perpendicular
const OPEN_SECONDS := 0.5
const INTERACT_RANGE := 2.5

## Every door joins this, so anything that needs one can find it by position
## instead of walking the chunk tree. Doors are top-level children of the
## chunk root rather than of the building body (see city_chunk.gd's own
## comment on that placement), so there's no building node to ask for its
## own door -- see npc/old_guy.gd's own _door_near().
const DOOR_GROUP := "building_doors"

var _body: AnimatableBody3D
var _is_open := false
var _nearby_player: CharacterBody3D = null
var _tween: Tween

## door_height: this building's own actual door height (varies per building,
## see city_chunk.gd's own DOOR_HEIGHT_FLOORS comment) -- width is always
## CityChunk.DOOR_WIDTH, a fixed constant shared by every building.
func setup(door_height: float) -> void:
	add_to_group(DOOR_GROUP)
	var width: float = CityChunk.DOOR_WIDTH

	# The swing is animated by rotating _body's OWN rotation directly, NOT a
	# separate non-physics parent Node3D wrapping it -- confirmed directly
	# (a real regression test) that a plain Node3D pivot's rotation doesn't
	# reliably propagate to an AnimatableBody3D CHILD's own collision in
	# time for move_and_slide() to see it; AnimatableBody3D's own
	# sync_to_physics handling is built around ITS OWN transform changing,
	# not a parent's. _body therefore sits directly at this node's own
	# origin (the hinge point, already positioned at the opening's left
	# edge by city_chunk.gd's own caller) -- every visual/collision piece
	# below is offset from THERE instead.
	_body = AnimatableBody3D.new()
	_body.collision_layer = 1
	add_child(_body)
	var body := _body

	var mat := StandardMaterial3D.new()
	mat.albedo_color = DOOR_COLOR
	mat.roughness = 0.85
	mat.metallic_specular = 0.0

	var mesh_inst := MeshInstance3D.new()
	var bm := BoxMesh.new()
	bm.size = Vector3(width, door_height, DOOR_THICKNESS)
	mesh_inst.mesh = bm
	mesh_inst.material_override = mat
	# Real reported bug: since _body itself sits AT the hinge (this node's
	# own origin, no offset -- see _body's own comment above), every child
	# position here is measured directly from the hinge, not from some
	# already-offset center the way the earlier (pivot-based) version's own
	# children were. This X offset (half the panel's own width) is what
	# actually pushes the panel's rendered/collided center out from the
	# hinge edge to where a real door leaf's center belongs -- an earlier
	# version dropped it while restructuring away from the separate pivot
	# node, leaving the panel centered ON the hinge instead of swinging
	# around it (reported directly: "the door hinge as attached to the
	# middle of the door instead of the edge").
	mesh_inst.position = Vector3(width / 2.0, door_height / 2.0, 0)
	body.add_child(mesh_inst)

	var handle_mat := StandardMaterial3D.new()
	handle_mat.albedo_color = HANDLE_COLOR
	handle_mat.roughness = 0.4
	handle_mat.metallic_specular = 0.5
	var handle := MeshInstance3D.new()
	var handle_mesh := BoxMesh.new()
	handle_mesh.size = Vector3(0.03, 0.03, 0.05)
	handle.mesh = handle_mesh
	handle.material_override = handle_mat
	# Near the door's own free (non-hinge) edge, roughly waist height --
	# purely cosmetic, no collision of its own (folded into the panel's own
	# box below), matching _build_label()-style small flat-accent props
	# elsewhere in this project. Measured from the hinge (x=0) same as
	# mesh_inst above, not from the panel's own center -- 0.9*width sits
	# near the free edge (x=width), matching this file's own original intent.
	handle.position = Vector3(width * 0.9, door_height * 0.45, DOOR_THICKNESS * 0.6)
	body.add_child(handle)

	var collision := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3(width, door_height, DOOR_THICKNESS)
	collision.shape = box
	collision.position = Vector3(width / 2.0, door_height / 2.0, 0)
	body.add_child(collision)

	# Same proximity-only InteractArea pattern vehicles/car.gd's own
	# entering/exiting already establishes (see its own class comment on why
	# a big, unmissable world object doesn't also need weapons' own
	## look-dot-threshold check) -- centered on the doorway itself, not the
	# panel's own hinge-offset center, so it's equally reachable whether the
	# door is currently open or closed.
	var interact_area := Area3D.new()
	interact_area.collision_layer = 0
	interact_area.collision_mask = 4
	var interact_shape := CollisionShape3D.new()
	var interact_sphere := SphereShape3D.new()
	interact_sphere.radius = INTERACT_RANGE
	interact_shape.shape = interact_sphere
	interact_area.add_child(interact_shape)
	interact_area.position = Vector3(width / 2.0, door_height / 2.0, 0)
	add_child(interact_area)
	interact_area.body_entered.connect(_on_interact_area_body_entered)
	interact_area.body_exited.connect(_on_interact_area_body_exited)

	# Explicit, not relied-upon-implicitly: this node is added to the tree
	# (by city_chunk.gd's own caller) BEFORE set_script() attaches this
	# script, so Godot's own "does this node want _process() calls" check
	# already ran once against a scriptless node by the time set_script()
	# happens -- attaching a script afterward doesn't retroactively flip
	# that on by itself (confirmed directly: without this line, _process()
	# never fired at all, so pressing E silently did nothing). Every OTHER
	# script in this project with a _process() override is declared
	# straight in a .tscn instead, present from the node's very first
	# frame, so this ordering gotcha never came up before city_chunk.gd's
	# own fully-code-built pattern (see this file's own class comment)
	# combined with a real per-frame need for the first time.
	set_process(true)

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
	_is_open = not _is_open
	var target_degrees := OPEN_ANGLE_DEGREES if _is_open else 0.0
	if _tween != null and _tween.is_valid():
		_tween.kill()
	_tween = create_tween()
	# Physics-synced, not the default idle-process tween -- _body is a real
	# physics body (see its own comment above), so its own rotation should
	# advance in lockstep with the physics step move_and_slide() actually
	# resolves against, not once-per-rendered-frame idle processing.
	_tween.set_process_mode(Tween.TWEEN_PROCESS_PHYSICS)
	_tween.tween_property(_body, "rotation_degrees:y", target_degrees, OPEN_SECONDS).set_trans(Tween.TRANS_SINE)

func is_open() -> bool:
	return _is_open

## Swings this door open if it isn't already, for something other than a
## player pressing E -- npc/old_guy.gd letting himself in. A no-op when it's
## already open, so a caller doesn't have to check first and can't
## accidentally close it by calling this twice.
func open() -> void:
	if _is_open:
		return
	_toggle()

