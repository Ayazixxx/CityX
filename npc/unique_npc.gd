extends CharacterBody3D
## A named, hand-authored NPC: wears a character drawn in the game's own
## creator (see npc/npc_design.gd), holds a fixed pose, talks when you press
## E next to it, and can be sent to walk somewhere (see walk_to).
##
## Everything that makes one of these specific -- who they look like, what
## pose they hold, what they say -- is @export data, so a second one is a
## new .tscn with different values rather than a new script. npc/old_guy.tscn
## is the first.
##
## Not a combatant. take_damage() is deliberately a no-op (see its own
## comment): these are story characters, and "I punched the guy who was
## going to tell me something and now he's a ragdoll" is a worse outcome
## than not being able to hit him.
##
## The interact trigger is radio.gd's own established pattern -- a proximity
## Area3D masking layer 4 (characters), plus polling the "interact" action
## in _process while someone is inside it. Polling rather than
## _unhandled_input because that's what every other world interactable here
## already does, and it keeps the whole interaction owned by this node
## instead of needing a hook in player.gd.
##
## PLAYER INPUT IS GATED for the whole conversation, so you can't wander
## off mid-sentence or throw a punch while choosing a reply.
##
## That was NOT safe when conversations advanced on E. The gate makes
## is_input_blocked() true, which is the same predicate this node polls to
## decide whether to listen for E at all -- so the key that advanced the box
## was the key the box had just disabled. Advancing moved to ENTER, handled
## as a real input event inside ui/dialogue_box.gd rather than by polling
## here, and the deadlock went with it. E now only ever STARTS a
## conversation, which is exactly when the gate is still open.

const GRAVITY := 9.8
const FLOOR_MAX_ANGLE_DEGREES := 80.0
const INTERACT_RANGE := 2.5 # same as every other 2.5m interactable in this project

const CharacterScene := preload("res://character/character.tscn")
const DialogueBoxScene := preload("res://ui/dialogue_box.tscn")
const NpcSteering = preload("res://npc/npc_steering.gd")

## Unhurried, and deliberately below the player's own walk speed (4.0) --
## a story NPC crossing a street should read as somebody walking over, not
## as somebody racing you there.
const WALK_SPEED := 2.4
## Close enough to count as standing at the destination.
const WALK_ARRIVE_DISTANCE := 0.8
## A walk that somehow can't finish -- a destination sealed behind geometry
## no amount of steering rounds -- gives up rather than walking forever.
## Generous: this is a safety net, not a schedule.
const WALK_TIMEOUT_SECONDS := 120.0

## Emitted when walk_to() finishes, whether by arriving or by timing out.
## Whoever asked for the walk decides what that means (see npc/old_guy.gd's
## own _on_arrived_at_building).
signal walk_finished

## The baked creator design this NPC wears (see npc/npc_design.gd). Left
## null the body stays plain white -- which is exactly what character.gd's
## own _build_atlas() already produces, so an unbaked NPC is a visible
## placeholder rather than an error.
@export var design: Resource = null

## Overrides the design's own display_name as the dialogue speaker. Only
## needed when they should be introduced as something other than the name
## the character was saved under.
@export var speaker_name_override: String = ""

## What a SIMPLE NPC says, one box at a time. Plenty for someone with a
## fixed thing to say; an NPC with branching conversation or state
## overrides _dialogue_nodes() instead (see npc/old_guy.gd).
@export var lines: PackedStringArray = PackedStringArray()

## The clip this NPC stands/sits in. With freeze_pose off (the default)
## it simply plays -- "idle" is the player's own breathing idle, so an NPC
## left alone looks alive rather than like a mannequin.
@export var pose_animation: String = "idle"

## Freezes pose_animation on a single frame instead of letting it play.
## Only useful for clips that aren't a loop and whose END or START is the
## interesting shape -- "getting_up" at 0.0 is flat on the floor,
## "standing_to_crouch" at 1.0 is a squat. A looping idle should never be
## frozen; that's a statue.
@export var freeze_pose: bool = false

## Where in the clip to freeze, as a RATIO rather than a time in seconds so
## re-baking a clip at a different length can't silently move the pose --
## 1.0 is the last frame, 0.0 the first. Ignored unless freeze_pose is on.
@export_range(0.0, 1.0) var pose_time_ratio: float = 0.0

## Clothing this NPC is authored ALREADY WEARING, as the garment's own scene
## (e.g. res://wearables/hooded_cloak.tscn). NOT a pickup: nothing is
## dropped in the world, no inventory is involved, and the player can't take
## it off them. The scene is instantiated purely to reach the garment's own
## script and freed the moment the garment is on -- see _apply_worn_top().
##
## Only garments that expose wear_on() can be worn this way, which today
## means the SKINNED ones (wearables/hooded_cloak.gd). The rigid multi-piece
## tops (t_shirt/hoodie) still go on through wearables/clothing.gd's own
## _equip(), which needs a real ground pickup to reparent pieces out of.
@export var worn_top: PackedScene = null

var _character: Node3D
var _nearby_player: Node3D = null
var _dialogue: CanvasLayer = null
# WHO this NPC currently holds the input gate on, not merely whether it
# holds one. Releasing has to work even once _nearby_player has been
# cleared -- _on_interact_area_body_exited nulls that BEFORE closing the
# conversation, so a gate released through _nearby_player would find
# nothing to release and leave the player permanently unable to move.
var _gated_player: Node3D = null

# Where walk_to() is currently headed, or INF for "standing still". Only the
# XZ of it is used -- gravity owns the vertical, same as every other NPC.
var _walk_target := Vector3.INF
# Which way to turn once he gets there, or INF to keep whatever heading the
# last stride left him on.
var _walk_facing := Vector3.INF
var _walk_time_remaining := 0.0
## Rounds buildings on the way rather than grinding into them -- the same
## component every hostile NPC uses (see npc/npc_steering.gd).
var _steering := NpcSteering.new()

func _ready() -> void:
	floor_max_angle = deg_to_rad(FLOOR_MAX_ANGLE_DEGREES)
	collision_layer = 4 # same "NPC body" layer every other NPC here uses
	collision_mask = 1 | 4
	# The collider comes from the .tscn, so it already exists here.
	_steering.configure_from(self)
	# Steps round PEOPLE too, not just buildings -- unlike the combat NPCs,
	# which only avoid world geometry. A story NPC walking somewhere will
	# sooner or later have the player standing squarely in his path, and
	# walking head-on into a body gives move_and_slide no side to slide
	# toward, so he'd grind there instead of going round.
	_steering.obstacle_mask = NpcSteering.WORLD_AND_CHARACTERS_MASK

	_character = CharacterScene.instantiate()
	_character.set_paintable(false)
	add_child(_character)
	# Same flip main.gd's own _place_character_on applies to the player's own
	# Character child -- the model's local +Z ("face") has to point where
	# this body's own forward (-Z) does.
	_character.rotation = Vector3(0, PI, 0)

	# AFTER add_child, never before: character.gd's own _ready() rebuilds a
	# blank atlas the instant it runs and would wipe the design (see
	# npc_design.gd's own apply_to comment for the bug this avoids).
	if design != null:
		design.apply_to(_character)

	_build_interact_area()
	# Deferred for the same reason gunman.gd defers its own equip: the
	# skeleton/AnimationPlayer the pose and the garment need only finish
	# building during the Character's own _ready().
	call_deferred("_apply_pose")
	call_deferred("_apply_worn_top")

## Starts this NPC's pose, and re-starts it after a walk (see _finish_walk).
## Between those two, nothing touches this AnimationPlayer: character.gd has
## no per-frame animation driver of its own, so whatever is started here is
## what stays until a walk takes it over with set_walking().
##
## Frozen case: play() then seek() then pause(). seek with update=true is
## what actually applies the pose, and pause() is what stops it advancing
## off that frame.
func _apply_pose() -> void:
	if _character == null or not is_instance_valid(_character):
		return
	var anim_player: AnimationPlayer = _character.anim_player
	if anim_player == null or not anim_player.has_animation(pose_animation):
		push_warning("unique_npc: no animation named '%s' -- standing in the default pose" % pose_animation)
		return
	anim_player.play(pose_animation)
	if not freeze_pose:
		return
	var length: float = anim_player.get_animation(pose_animation).length
	anim_player.seek(clampf(pose_time_ratio, 0.0, 1.0) * length, true)
	anim_player.pause()

## Dresses this NPC in `worn_top`, if one was authored. The instantiated
## garment is never added to the tree -- so its own _ready() never runs and
## no ground item, interact area or dropped-cloak visual is ever built -- and
## it's freed as soon as it has handed its mesh to the character. free()
## rather than queue_free() because it isn't in the tree to be queued out of.
##
## Runs AFTER design.apply_to() (deferred from _ready(), which applies the
## design inline): a covering garment swaps the body for stubs, and the
## design has to have painted the real body before that swap happens.
func _apply_worn_top() -> void:
	if worn_top == null:
		return
	if _character == null or not is_instance_valid(_character):
		return
	var garment: Node = worn_top.instantiate()
	if not garment.has_method("wear_on"):
		push_warning("unique_npc: %s can't be worn at spawn -- no wear_on()"
			% worn_top.resource_path)
		garment.free()
		return
	garment.wear_on(_character)
	garment.free()

func _build_interact_area() -> void:
	var interact_area := Area3D.new()
	interact_area.collision_layer = 0
	interact_area.collision_mask = 4 # characters only, same as radio.gd's own trigger
	var interact_shape := CollisionShape3D.new()
	var interact_sphere := SphereShape3D.new()
	interact_sphere.radius = INTERACT_RANGE
	interact_shape.shape = interact_sphere
	interact_area.add_child(interact_shape)
	add_child(interact_area)
	interact_area.body_entered.connect(_on_interact_area_body_entered)
	interact_area.body_exited.connect(_on_interact_area_body_exited)

## Duck-typed on respawn(), the same "is this actually a Player" test
## radio.gd uses -- civilians and other NPCs are on the same collision layer
## and would otherwise register as someone to talk to.
func _on_interact_area_body_entered(body: Node3D) -> void:
	if body is CharacterBody3D and body.has_method("respawn"):
		_nearby_player = body

func _on_interact_area_body_exited(body: Node3D) -> void:
	if body != _nearby_player:
		return
	_nearby_player = null
	# Walking off mid-sentence ends the conversation rather than leaving a
	# box on screen for an NPC no longer in front of you.
	if _dialogue != null and _dialogue.is_open():
		_dialogue.close()

func _physics_process(delta: float) -> void:
	# A real CharacterBody3D, so it settles onto whatever ground it was
	# dropped above rather than hovering if the spawn point was slightly off
	# -- and so a walk (see walk_to) can climb a kerb on the way.
	if not is_on_floor():
		velocity.y -= GRAVITY * delta
	else:
		velocity.y = 0.0
	_drive_walk(delta)
	move_and_slide()

## Sends this NPC walking to `destination` under its own power, rounding
## anything in the way. `face_direction` is which way to turn on arrival
## (INF to keep whatever heading the last stride left). Emits walk_finished
## when it gets there or gives up.
##
## Only the XZ of `destination` matters -- gravity owns the vertical, so a
## destination given at any sane height lands him on the ground under it.
func walk_to(destination: Vector3, face_direction: Vector3 = Vector3.INF) -> void:
	_walk_target = destination
	_walk_facing = face_direction
	_walk_time_remaining = WALK_TIMEOUT_SECONDS
	# Cleared so a side committed to on some earlier walk can't steer the
	# first strides of this one.
	_steering.reset()

func is_walking_somewhere() -> bool:
	return _walk_target != Vector3.INF

## Stops a walk where it stands, with no walk_finished. For a caller that
## needs to call the whole thing off rather than let it complete.
func cancel_walk() -> void:
	if _walk_target == Vector3.INF:
		return
	_walk_target = Vector3.INF
	_walk_facing = Vector3.INF
	_stop_walking()

func _drive_walk(delta: float) -> void:
	if _walk_target == Vector3.INF:
		return
	var to_target: Vector3 = _walk_target - global_position
	to_target.y = 0.0
	var distance := to_target.length()
	_walk_time_remaining -= delta
	if distance <= WALK_ARRIVE_DISTANCE or _walk_time_remaining <= 0.0:
		_finish_walk()
		return

	var direction := _steering.steer(self, to_target / distance, delta)
	velocity.x = direction.x * WALK_SPEED
	velocity.z = direction.z * WALK_SPEED
	# Faced along where he's actually going, not at the destination -- while
	# rounding a building those are different, and the second one walks him
	# sideways with his head turned.
	_face_along(direction)
	if _character != null and is_instance_valid(_character) and _character.has_method("set_walking"):
		_character.set_walking(true)

func _finish_walk() -> void:
	_walk_target = Vector3.INF
	_stop_walking()
	if _walk_facing != Vector3.INF:
		_face_along(_walk_facing)
		_walk_facing = Vector3.INF
	# Back to whatever pose this NPC holds when it isn't going anywhere.
	_apply_pose()
	walk_finished.emit()

func _stop_walking() -> void:
	velocity.x = 0.0
	velocity.z = 0.0
	_steering.reset()
	if _character != null and is_instance_valid(_character) and _character.has_method("set_walking"):
		_character.set_walking(false)

## Turns the body to face along a horizontal direction. look_at aims the
## body's own -Z, which is where the face points once the PI flip in _ready
## is applied -- the same mechanism npc/old_guy.gd's own arrival facing uses.
func _face_along(direction: Vector3) -> void:
	var flat := Vector3(direction.x, 0.0, direction.z)
	if flat.length_squared() < 0.000001:
		return
	look_at(global_position + flat, Vector3.UP)
	rotation.x = 0.0
	rotation.z = 0.0

func _process(_delta: float) -> void:
	if _nearby_player == null or not is_instance_valid(_nearby_player):
		return
	if not Input.is_action_just_pressed("interact"):
		return
	# E only ever STARTS a conversation now -- Enter advances it, handled
	# inside ui/dialogue_box.gd. So there's nothing to do if one is already
	# up, and this also stops E re-opening the box over itself.
	if is_talking():
		return
	# Input.is_action_just_pressed polling doesn't respect a focused chat
	# LineEdit on its own, so without this, typing the letter "e" into chat
	# next to an NPC would start a conversation. It also covers the gate
	# this node itself sets while talking (see player.gd's is_input_blocked).
	if _nearby_player.has_method("is_input_blocked") and _nearby_player.is_input_blocked():
		return
	_begin_talking()

## The conversation to open right now, as the Dictionaries ui/dialogue_box.gd
## documents. The default turns the plain `lines` export into one node per
## line; a subclass with branching or state overrides this (see
## npc/old_guy.gd) and never touches `lines` at all.
func _dialogue_nodes() -> Array:
	var nodes: Array = []
	for line in lines:
		nodes.append({"text": line})
	return nodes

## Called when a conversation ends normally, and when the player picks a
## reply (with its id). Both are no-ops here -- a plain NPC has nothing to
## react to. Subclasses override.
func _on_dialogue_finished() -> void:
	pass

func _on_dialogue_option_chosen(_id: String) -> void:
	pass

func _begin_talking() -> void:
	var nodes := _dialogue_nodes()
	if nodes.is_empty():
		return
	_ensure_dialogue_box()
	_gate_input()
	_dialogue.open(speaker_name(), nodes)

## Opens a follow-on stretch of conversation on the SAME box -- what a
## subclass calls from _on_dialogue_option_chosen to continue after a reply.
func _continue_talking(nodes: Array) -> void:
	if nodes.is_empty():
		return
	_ensure_dialogue_box()
	_gate_input()
	_dialogue.open(speaker_name(), nodes)

## set_ui_capturing moves a COUNTER, so these have to be exactly balanced --
## _gated is what guarantees that regardless of how the conversation ends
## (last line, a reply, walking away, the NPC being freed mid-sentence).
func _gate_input() -> void:
	if _gated_player != null:
		return
	if is_instance_valid(_nearby_player) and _nearby_player.has_method("set_ui_capturing"):
		_nearby_player.set_ui_capturing(true)
		_gated_player = _nearby_player

func _release_input_gate() -> void:
	if _gated_player == null:
		return
	var gated: Node3D = _gated_player
	# Cleared FIRST so a re-entrant call (a subclass opening the next
	# stretch from inside _on_dialogue_option_chosen) can't double-release.
	_gated_player = null
	if is_instance_valid(gated) and gated.has_method("set_ui_capturing"):
		gated.set_ui_capturing(false)

## Frees the gate if this NPC is torn down mid-conversation -- quitting to
## the title, or the whole city being freed. Without it the counter would
## be left raised on a player that outlives this node.
func _exit_tree() -> void:
	_release_input_gate()

func _ensure_dialogue_box() -> void:
	if _dialogue != null and is_instance_valid(_dialogue):
		return
	_dialogue = DialogueBoxScene.instantiate()
	add_child(_dialogue)
	_dialogue.finished.connect(_handle_dialogue_finished)
	_dialogue.option_chosen.connect(_handle_dialogue_option_chosen)

## Player movement/combat is gated for as long as a conversation is up, and
## released the moment it ends either way. Balanced calls -- see player.gd's
## own set_ui_capturing, which moves a counter.
func _handle_dialogue_finished() -> void:
	_release_input_gate()
	_on_dialogue_finished()

func _handle_dialogue_option_chosen(id: String) -> void:
	# Released BEFORE the subclass runs: it will usually open the next
	# stretch straight away, which re-gates. Doing it in this order keeps
	# the counter at one rather than climbing.
	_release_input_gate()
	_on_dialogue_option_chosen(id)

## The design's own saved name unless explicitly overridden -- so the
## speaker label is whatever the character was actually named in the
## creator, with no second place to keep that in sync.
func speaker_name() -> String:
	if speaker_name_override != "":
		return speaker_name_override
	if design != null and design.display_name != "":
		return design.display_name
	return name

func is_talking() -> bool:
	return _dialogue != null and is_instance_valid(_dialogue) and _dialogue.is_open()

## Story NPCs don't take damage. Accepts the same duck-typed 4-arg signature
## every damageable thing in this project uses (see wolf.gd's own comment)
## so player.gd's own _punch() can call it without a special case -- it just
## does nothing. Punching one is a whiff, not a kill.
func take_damage(_amount: float, _attacker: Node = null, _hitstun_seconds: float = 0.0, _is_finisher: bool = false) -> void:
	pass
