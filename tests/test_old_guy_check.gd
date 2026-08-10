extends Node3D
## Regression check for npc/unique_npc.gd + ui/dialogue_box.gd + npc/
## npc_design.gd -- the hand-authored talking NPC pipeline, first used by
## Old Guy sitting near the spawn point.
##
## The interesting failures here are all quiet ones:
##
##  - A design applied at the WRONG TIME silently does nothing.
##    character.gd's own _ready() rebuilds a blank white atlas, so applying
##    paint before tree entry leaves a plain white NPC and no error at all
##    -- the exact bug main.gd's own _apply_pending_paint documents. Checked
##    by baking a real design in-memory, applying it through the real code
##    path, and reading the pixels back off the live character.
##  - A MISSING design must degrade to plain white, not crash -- that's the
##    state the project actually ships in until a character is baked.
##  - The dialogue box has to close EXACTLY once per conversation. A
##    finished signal that fires twice (or never) is what would strand a
##    caller that used it to un-gate anything.

const UniqueNpcScene := preload("res://npc/old_guy.tscn")
const DialogueBoxScene := preload("res://ui/dialogue_box.tscn")
const NpcDesignScript = preload("res://npc/npc_design.gd")
const CharacterScene := preload("res://character/character.tscn")
const PlayerScene := preload("res://player/player.tscn")
const MainScript = preload("res://main.gd")
const CityChunk = preload("res://city/city_chunk.gd")

## Matches city.gd's own CHUNK_SIZE and WORLD_SEED hash, so a chunk built
## here is byte-identical to what the real city would generate there.
const CHUNK_SIZE := 24.0

var _ok := true
var _finished_count := 0
var _chosen_id := ""

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	call_deferred("_run_checks")

func _run_checks() -> void:
	_build_ground()
	await _check_old_guy_says_his_line()
	await _check_holds_a_floor_pose()
	await _check_plain_white_without_a_design()
	await _check_a_design_actually_lands_on_the_body()
	await _check_dialogue_advances_and_finishes_once()
	await _check_walking_away_closes_the_box()
	await _check_punching_him_does_nothing()
	await _check_he_stays_out_of_the_intro_cutscene()
	await _check_the_tutorial_branches()
	await _check_a_far_claim_stages_him_in_close()
	await _check_he_asks_to_move_in()
	await _check_saying_no_sends_him_wandering()
	await _check_saying_yes_moves_him_in()
	await _check_he_spawns_wearing_the_cloak()

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

## --- 9. The tutorial itself: both branches, and the payoff. Accepting
## sends you off to claim a building and brings him to it; declining ends
## the conversation but doesn't lock you out of changing your mind. ---
func _check_the_tutorial_branches() -> void:
	# --- Declining ---
	var old_guy: CharacterBody3D = UniqueNpcScene.instantiate()
	add_child(old_guy)
	await get_tree().process_frame

	old_guy._on_dialogue_option_chosen(old_guy.OPTION_DECLINE)
	await get_tree().process_frame
	var declined: Array = old_guy._dialogue_nodes()
	if old_guy.tutorial_stage() != old_guy.Stage.DECLINED:
		_fail("declining didn't move him to the DECLINED stage")
	elif declined.size() != 1 or str(declined[0].get("text", "")) != "I can teach you the ropes around here if you'd like":
		_fail("after declining, talking again doesn't re-offer -- it says '%s'" % (str(declined[0].get("text", "")) if not declined.is_empty() else "<nothing>"))
	else:
		print("PASS: declining ends the conversation but leaves the offer open to change your mind")
	old_guy.queue_free()
	await get_tree().process_frame

	# --- Accepting ---
	var player: CharacterBody3D = PlayerScene.instantiate()
	add_child(player)
	player.global_position = Vector3(0, 0.1, 0)
	player.add_to_group("players")

	old_guy = UniqueNpcScene.instantiate()
	add_child(old_guy)
	old_guy.global_position = Vector3(0, 0.1, 1.2) # inside INTERACT_RANGE
	for _i in range(6):
		await get_tree().physics_frame

	if old_guy._nearby_player != player:
		_fail("the player didn't register as nearby, so accepting can't wire up the claim watch")
		player.queue_free()
		old_guy.queue_free()
		return

	old_guy._on_dialogue_option_chosen(old_guy.OPTION_ACCEPT)
	await get_tree().process_frame

	if old_guy.tutorial_stage() != old_guy.Stage.AWAITING_CLAIM:
		_fail("accepting didn't move him to AWAITING_CLAIM")
	elif not player.building_claimed.is_connected(old_guy._on_player_claimed_building):
		_fail("accepting didn't start watching for the player's first claim")
	else:
		print("PASS: accepting sends him to AWAITING_CLAIM and watches for your claim")

	# While waiting, talking to him repeats the objective rather than the
	# whole speech.
	var waiting: Array = old_guy._dialogue_nodes()
	if waiting.size() != 1 or not str(waiting[0].get("text", "")).contains("/claim"):
		_fail("while waiting he doesn't remind you how to claim")
	else:
		print("PASS: while waiting, talking to him repeats the /claim instruction")

	# --- The payoff: claim a building close by, and he WALKS over to it.
	# 40m out, comfortably inside MAX_WALK_SECONDS at WALK_SPEED, so this is
	# the no-staging path: he sets off from exactly where he was standing. ---
	var was_at := old_guy.global_position
	var center := Vector3(0.0, 0.0, -40.0)
	var size := Vector3(14.0, 30.0, 16.0)
	player.building_claimed.emit(center, size)
	await get_tree().process_frame

	# The whole point of the change: no jump. He is still where he was, and
	# is now on his way rather than already standing there.
	if old_guy.tutorial_stage() != old_guy.Stage.TRAVELLING:
		_fail("claiming a nearby building didn't set him TRAVELLING (stage=%d)" % old_guy.tutorial_stage())
	elif old_guy.global_position.distance_to(was_at) > 2.0:
		_fail("he jumped %.1fm the instant the building was claimed -- he's meant to walk it"
			% old_guy.global_position.distance_to(was_at))
	elif not old_guy.is_walking_somewhere():
		_fail("he isn't walking anywhere after the claim")
	else:
		print("PASS: claiming a nearby building sets him walking from where he stood, not teleporting")

	# Run it out for real. Generous cap: ~30m at WALK_SPEED is about 750
	# physics frames, so this fails on a stuck walk rather than hanging.
	var frames := 0
	while old_guy.tutorial_stage() != old_guy.Stage.ARRIVED and frames < 2400:
		await get_tree().physics_frame
		frames += 1

	var moved: Vector3 = old_guy.global_position
	if old_guy.tutorial_stage() != old_guy.Stage.ARRIVED:
		_fail("he never finished the walk (still %.1fm from the door after %d frames)"
			% [Vector2(moved.x - center.x, moved.z - center.z).length(), frames])
	elif moved.distance_to(was_at) < 1.0:
		_fail("he never actually moved")
	elif Vector2(moved.x - center.x, moved.z - center.z).length() > size.z * 0.5 + 8.0:
		_fail("he ended up %.1fm from the claimed building" % Vector2(moved.x - center.x, moved.z - center.z).length())
	elif moved.z <= center.z + size.z * 0.5:
		_fail("he ended up inside/behind the building instead of outside its front (z=%.1f)" % moved.z)
	else:
		print("PASS: he walks the whole way and ends up outside the front door (%d frames)" % frames)

	# ONE_SHOT: a second claim doesn't drag him around the city.
	var after_first := old_guy.global_position
	player.building_claimed.emit(Vector3(-300.0, 0.0, 300.0), size)
	await get_tree().process_frame
	if old_guy.global_position.distance_to(after_first) > 1.0:
		_fail("a second claim moved him again -- he should only come to the first one")
	else:
		print("PASS: he only turns up for the first building you claim")

	player.queue_free()
	old_guy.queue_free()
	await get_tree().process_frame

## --- 10. He spawns already wearing the hooded cloak, through
## unique_npc.gd's own `worn_top` export.
##
## The failures worth catching here are all silent ones:
##
##  - The garment scene going in as a PICKUP by mistake -- a tipped-over
##    cloak lying inside him, an interact area stealing E from his dialogue.
##    _apply_worn_top() never adds it to the tree, so nothing of the ground
##    item should exist afterwards.
##  - The mesh landing on him but not DEFORMING. A MeshInstance3D built in
##    code has an empty `skeleton` path by default (see hooded_cloak.gd's own
##    comment) -- the cloak would hang in its rest pose while he breathes
##    inside it, with no error anywhere.
##  - The body not being swapped for stubs, which is what stops his arms and
##    chest poking through the cloth. ---
func _check_he_spawns_wearing_the_cloak() -> void:
	var old_guy: CharacterBody3D = UniqueNpcScene.instantiate()
	add_child(old_guy)
	# Two frames: worn_top is applied from a call_deferred out of _ready(),
	# which itself waits on the Character's own _ready() building the skeleton.
	await get_tree().process_frame
	await get_tree().process_frame

	if old_guy.worn_top == null:
		_fail("Old Guy isn't authored wearing anything -- the .tscn's worn_top is unset")
		old_guy.queue_free()
		await get_tree().process_frame
		return

	var character: Node = old_guy.get_node_or_null("Character")
	var parts: Array = character._worn_top_parts if character != null else []
	if parts.is_empty():
		_fail("Old Guy spawned with nothing on -- worn_top never reached the character")
		old_guy.queue_free()
		await get_tree().process_frame
		return

	var cloak: MeshInstance3D = parts[0] as MeshInstance3D
	if cloak == null or cloak.mesh == null:
		_fail("the worn top isn't a mesh")
	elif cloak.get_parent() is not Skeleton3D:
		_fail("the cloak hangs off a %s instead of the Skeleton3D -- it can't deform" % cloak.get_parent().get_class())
	elif cloak.skeleton.is_empty():
		_fail("the cloak's skeleton path is empty -- it would stand frozen in its rest pose while he moves")
	elif cloak.skin == null or cloak.skin.get_bind_count() == 0:
		_fail("the cloak arrived with no skin binds")
	else:
		print("PASS: Old Guy spawns wearing the cloak, skinned to his own skeleton (%d binds)"
			% cloak.skin.get_bind_count())

	if not character.is_body_stubs_only():
		_fail("his full body is still drawn under the cloak -- limbs will poke through it")
	else:
		print("PASS: the body underneath is swapped for stubs, same as when the player wears it")

	# Nothing of the PICKUP may survive: no ground cloak lying inside him, and
	# no second interact area competing with his own for E.
	for child in old_guy.get_children():
		if child.get_script() != null and str(child.get_script().resource_path).ends_with("hooded_cloak.gd"):
			_fail("the garment's own pickup node was left parented to him -- there's a cloak lying inside the NPC")
			break

	old_guy.queue_free()
	await get_tree().process_frame

## --- The other half of the walk: claim a building far enough away that a
## real walk would be minutes of waiting, and he's staged in closer FIRST.
##
## What's asserted is the BUDGET, not the exact spot -- the staging point is
## picked from whichever of a ring of candidates has ground, a clear run and
## isn't on screen, so pinning its coordinates would just re-implement the
## picker. What has to hold is that he ends up within a walk the player will
## actually sit through, and still walks the last stretch rather than
## appearing at the door. ---
func _check_a_far_claim_stages_him_in_close() -> void:
	var player: CharacterBody3D = PlayerScene.instantiate()
	add_child(player)
	player.global_position = Vector3(0, 0.1, 0)
	player.add_to_group("players")

	var old_guy: CharacterBody3D = UniqueNpcScene.instantiate()
	add_child(old_guy)
	old_guy.global_position = Vector3(0, 0.1, 1.2)
	for _i in range(6):
		await get_tree().physics_frame

	old_guy._on_dialogue_option_chosen(old_guy.OPTION_ACCEPT)
	await get_tree().process_frame

	# 150m out: past MAX_WALK_SECONDS (40s) at WALK_SPEED (2.4 m/s = 96m),
	# so this is the staging path rather than a straight walk.
	var center := Vector3(0.0, 0.0, -150.0)
	var size := Vector3(14.0, 30.0, 16.0)
	var outside := center + Vector3(0.0, 0.0, size.z * 0.5 + old_guy.ARRIVAL_DOOR_CLEARANCE)
	player.building_claimed.emit(center, size)
	await get_tree().process_frame

	var staged: Vector3 = old_guy.global_position
	var to_door: float = Vector2(staged.x - outside.x, staged.z - outside.z).length()
	var budget: float = old_guy.MAX_WALK_SECONDS * old_guy.WALK_SPEED

	if old_guy.tutorial_stage() != old_guy.Stage.TRAVELLING:
		_fail("a far claim didn't set him TRAVELLING")
	elif to_door > budget:
		_fail("he was left %.0fm from the door -- a %.0fs walk, past the %.0fs budget"
			% [to_door, to_door / old_guy.WALK_SPEED, old_guy.MAX_WALK_SECONDS])
	elif to_door < 5.0:
		_fail("he was staged %.1fm from the door -- that's on the doorstep, not a walk over" % to_door)
	else:
		print("PASS: a far claim stages him %.0fm out (a %.0fs walk) instead of leaving him %.0fm away"
			% [to_door, to_door / old_guy.WALK_SPEED, Vector2(0.0, 1.2 - outside.z).length()])

	# And he still has to WALK that last stretch -- staging must not have
	# quietly put him at the destination.
	if not old_guy.is_walking_somewhere():
		_fail("he was staged but isn't walking anywhere")
	else:
		print("PASS: he walks the last stretch in rather than being placed at the door")

	player.queue_free()
	old_guy.queue_free()
	await get_tree().process_frame

## --- Once he's outside your building, he has something to say. Guards the
## .tscn-free authored content the same way the opening lines are guarded:
## a typo'd line is still perfectly valid GDScript. ---
func _check_he_asks_to_move_in() -> void:
	var old_guy: CharacterBody3D = UniqueNpcScene.instantiate()
	add_child(old_guy)
	await get_tree().process_frame
	old_guy._stage = old_guy.Stage.ARRIVED

	var nodes: Array = old_guy._dialogue_nodes()
	if nodes.size() != 2:
		_fail("outside your building he has %d lines, expected 2" % nodes.size())
	elif str(nodes[0].get("text", "")) != "Ok good choice I like this building":
		_fail("his approval line reads '%s'" % str(nodes[0].get("text", "")))
	elif str(nodes[1].get("text", "")) != "By the way i'm homeless is it cool if I crash at your place for a bit":
		_fail("his ask reads '%s'" % str(nodes[1].get("text", "")))
	else:
		var options: Array = nodes[1].get("options", [])
		if options.size() != 2:
			_fail("the ask carries %d replies, expected Yes and No" % options.size())
		elif str(options[0].get("text", "")) != "Yes" or str(options[1].get("text", "")) != "No":
			_fail("the replies read '%s' / '%s'" % [str(options[0].get("text", "")), str(options[1].get("text", ""))])
		else:
			print("PASS: outside your building he approves of it and asks to move in, with Yes/No")

	old_guy.queue_free()
	await get_tree().process_frame

## --- "No": he says his piece and goes off wandering, and crucially does
## NOT walk into the building. ---
func _check_saying_no_sends_him_wandering() -> void:
	var old_guy: CharacterBody3D = UniqueNpcScene.instantiate()
	add_child(old_guy)
	old_guy.global_position = Vector3(0, 0.1, 0)
	await get_tree().process_frame
	old_guy._stage = old_guy.Stage.ARRIVED
	old_guy._home_center = Vector3(0, 0, -30)
	old_guy._home_size = Vector3(14, 30, 16)

	old_guy._on_dialogue_option_chosen(old_guy.OPTION_TURN_HIM_AWAY)
	await get_tree().process_frame

	if old_guy.tutorial_stage() != old_guy.Stage.WANDERING:
		_fail("saying No didn't send him WANDERING (stage=%d)" % old_guy.tutorial_stage())
	else:
		print("PASS: saying No sends him off to wander")

	# He wanders NEAR WHERE HE WAS, not into the house -- the failure that
	# would matter is him moving in anyway.
	var start := old_guy.global_position
	for _i in range(600):
		await get_tree().physics_frame
	var ended: Vector3 = old_guy.global_position
	var into_building: bool = absf(ended.x - old_guy._home_center.x) < old_guy._home_size.x * 0.5 \
		and absf(ended.z - old_guy._home_center.z) < old_guy._home_size.z * 0.5
	if into_building:
		_fail("he was turned away but walked into the building anyway (%s)" % str(ended))
	elif ended.distance_to(start) < 0.5:
		_fail("he never actually wandered anywhere (moved %.2fm in 600 frames)" % ended.distance_to(start))
	else:
		print("PASS: after being turned away he wanders the street and stays out of the building (%.1fm from where he stood)"
			% ended.distance_to(start))

	old_guy.queue_free()
	await get_tree().process_frame

## --- "Yes": he walks IN. Driven against a real generated chunk so there's
## a real building with a real door in it -- the entry walk aims at the
## doorway found through building_door.gd's own group, and a stub box would
## prove none of that resolves. ---
func _check_saying_yes_moves_him_in() -> void:
	var built := await _build_a_real_building()
	if built.is_empty():
		_fail("couldn't generate a chunk with a building, so the move-in check can't run")
		return
	var center: Vector3 = built["center"]
	var size: Vector3 = built["size"]

	var old_guy: CharacterBody3D = UniqueNpcScene.instantiate()
	add_child(old_guy)
	# Standing outside the front door, exactly where the arrival walk leaves him.
	old_guy.global_position = center + Vector3(0.0, 0.4, size.z * 0.5 + old_guy.ARRIVAL_DOOR_CLEARANCE)
	await get_tree().process_frame
	old_guy._stage = old_guy.Stage.ARRIVED
	old_guy._home_center = center
	old_guy._home_size = size

	var door: Node3D = old_guy._door_near(center + Vector3(0.0, 0.0, size.z * 0.5))
	if door == null:
		_fail("no door found for the generated building -- building_door.gd's group registration is broken")
		built["chunk"].queue_free()
		old_guy.queue_free()
		return
	if door.is_open():
		_fail("test assumption broken: the door started open")

	old_guy._on_dialogue_option_chosen(old_guy.OPTION_LET_HIM_STAY)
	await get_tree().process_frame

	if old_guy.tutorial_stage() != old_guy.Stage.MOVING_IN:
		_fail("saying Yes didn't put him in MOVING_IN")
	elif not door.is_open():
		_fail("he let himself in without opening the closed door")
	else:
		print("PASS: saying Yes opens the front door and starts him walking in")

	var frames := 0
	while old_guy.tutorial_stage() != old_guy.Stage.LIVING_INSIDE and frames < 2400:
		await get_tree().physics_frame
		frames += 1

	var ended: Vector3 = old_guy.global_position
	var inside: bool = absf(ended.x - center.x) < size.x * 0.5 and absf(ended.z - center.z) < size.z * 0.5
	if old_guy.tutorial_stage() != old_guy.Stage.LIVING_INSIDE:
		_fail("he never finished moving in (%d frames, ended at %s)" % [frames, str(ended)])
	elif not inside:
		_fail("he finished 'moving in' but ended up outside the building's own footprint (%s)" % str(ended))
	else:
		print("PASS: he walks through the door and ends up inside the building (%d frames)" % frames)

	# And then he lives there -- mostly still, sometimes strolling, but
	# never out through the wall.
	#
	# Measured as DRIFT FROM WHERE HE LANDED, not as per-frame movement: at
	# WALK_SPEED a single physics frame covers 0.04m, so any per-frame
	# threshold big enough to ignore jitter is also big enough to miss him
	# walking. Run long enough to cover more than one LOITER_MAX_SECONDS
	# beat, so this doesn't hinge on which delay he happened to roll.
	var settled_at: Vector3 = old_guy.global_position
	var furthest := 0.0
	var left_the_building := false
	for _i in range(1800):
		await get_tree().physics_frame
		var at: Vector3 = old_guy.global_position
		furthest = maxf(furthest, at.distance_to(settled_at))
		if absf(at.x - center.x) > size.x * 0.5 or absf(at.z - center.z) > size.z * 0.5:
			left_the_building = true
			break

	if left_the_building:
		_fail("he wandered out of the building he moved into")
	elif furthest < 0.5:
		_fail("he moved in and then never moved again (drifted %.2fm in 30s) -- he's meant to walk around sometimes" % furthest)
	else:
		print("PASS: once moved in he potters about inside (%.1fm around) and stays indoors" % furthest)

	built["chunk"].queue_free()
	old_guy.queue_free()
	await get_tree().process_frame

## Generates real chunks until one has a building, and returns it. Same
## approach tests/test_claim_building_check.gd uses -- a real interior with a
## real door is the whole point.
func _build_a_real_building() -> Dictionary:
	var materials: Dictionary = CityChunk.build_materials()
	var coord := Vector2i(1, 1)
	for _attempt in range(30):
		if not CityChunk.is_street_chunk(coord):
			var seed_value: int = hash(Vector3i(coord.x, coord.y, 1337))
			var params: Dictionary = CityChunk.building_params_for_seed(seed_value, coord, CHUNK_SIZE)
			if params["has_building"]:
				var chunk: Node3D = CityChunk.generate_chunk(seed_value, coord, CHUNK_SIZE, materials)
				chunk.position = Vector3(coord.x * CHUNK_SIZE, 0.0, coord.y * CHUNK_SIZE)
				add_child(chunk)
				await get_tree().physics_frame
				await get_tree().physics_frame
				var offset: Vector3 = params["offset"]
				return {
					"chunk": chunk,
					"center": chunk.position + offset,
					"size": params["size"],
				}
		coord.x += 1
		coord.y += 1
	return {}

func _build_ground() -> void:
	var ground := StaticBody3D.new()
	var shape := CollisionShape3D.new()
	var box := BoxShape3D.new()
	# Big enough for the far-claim staging check to have real ground 150m
	# out AND a full ring of candidates around it.
	box.size = Vector3(500, 1, 500)
	shape.shape = box
	shape.position = Vector3(0, -0.5, 0)
	ground.add_child(shape)
	ground.collision_layer = 1
	add_child(ground)

## --- 1. The actual content: he's called Old Guy and he says the one line.
## Guards the .tscn's own exported data, which nothing else would catch --
## a typo'd line is still perfectly valid GDScript. ---
func _check_old_guy_says_his_line() -> void:
	var old_guy: CharacterBody3D = UniqueNpcScene.instantiate()
	add_child(old_guy)
	await get_tree().process_frame

	var nodes: Array = old_guy._dialogue_nodes()
	if old_guy.speaker_name() != "Old Guy":
		_fail("the spawn NPC introduces himself as '%s', not 'Old Guy'" % old_guy.speaker_name())
	elif nodes.size() != 2:
		_fail("Old Guy opens with %d lines, expected 2 (the greeting and the offer)" % nodes.size())
	elif str(nodes[0].get("text", "")) != "Dude I just saw you fall from the sky":
		_fail("Old Guy's opening line is '%s'" % str(nodes[0].get("text", "")))
	elif str(nodes[1].get("text", "")) != "I can teach you the ropes around here if you'd like":
		_fail("Old Guy's offer line is '%s'" % str(nodes[1].get("text", "")))
	else:
		var options: Array = nodes[1].get("options", [])
		if options.size() != 2:
			_fail("the offer carries %d replies, expected 2" % options.size())
		elif str(options[0].get("text", "")) != "Sure" or str(options[1].get("text", "")) != "I'll figure things out myself":
			_fail("the replies read '%s' / '%s'" % [str(options[0].get("text", "")), str(options[1].get("text", ""))])
		else:
			print("PASS: Old Guy greets you, offers to teach you, and gives both replies verbatim")

	old_guy.queue_free()
	await get_tree().process_frame

## --- 2. He stands there, alive. Old Guy plays the ordinary breathing idle
## rather than freezing on a frame, so the check is that the clip is really
## RUNNING -- a frozen NPC reads as a mannequin, and a silently-missing clip
## would leave him in the rig's bare default pose with no error. The
## freeze path (used by other poses) is covered right after. ---
func _check_holds_a_floor_pose() -> void:
	var old_guy: CharacterBody3D = UniqueNpcScene.instantiate()
	add_child(old_guy)
	await get_tree().process_frame
	await get_tree().process_frame

	var character: Node = old_guy.get_node_or_null("Character")
	if character == null:
		_fail("the NPC has no Character child")
		old_guy.queue_free()
		return
	var anim_player: AnimationPlayer = character.anim_player
	if anim_player == null:
		_fail("the NPC's Character has no AnimationPlayer")
		old_guy.queue_free()
		return

	if old_guy.freeze_pose:
		_fail("Old Guy is set to freeze on a frame -- he should be standing there breathing, not a statue")
	elif not anim_player.has_animation(old_guy.pose_animation):
		_fail("Old Guy's pose clip '%s' doesn't exist -- he'd stand in the rig's bare default pose" % old_guy.pose_animation)
	elif anim_player.current_animation != old_guy.pose_animation:
		_fail("Old Guy is playing '%s', expected '%s'" % [anim_player.current_animation, old_guy.pose_animation])
	else:
		var before := anim_player.current_animation_position
		for _i in range(20):
			await get_tree().process_frame
		if absf(anim_player.current_animation_position - before) < 0.001:
			_fail("Old Guy's '%s' isn't advancing -- he's frozen mid-idle" % old_guy.pose_animation)
		else:
			print("PASS: Old Guy stands there playing '%s', and it's really running" % old_guy.pose_animation)

	old_guy.queue_free()
	await get_tree().process_frame

	# The other half of the same feature: a pose that IS meant to hold on
	# one frame has to actually stay there. Nothing ships using this yet
	# (Old Guy stands), but it's the mechanism any future sitting/lying NPC
	# depends on, and a resumed AnimationPlayer would slide off the pose.
	var frozen: CharacterBody3D = UniqueNpcScene.instantiate()
	frozen.pose_animation = "getting_up"
	frozen.freeze_pose = true
	frozen.pose_time_ratio = 0.0
	add_child(frozen)
	await get_tree().process_frame
	await get_tree().process_frame

	var frozen_player: AnimationPlayer = frozen.get_node("Character").anim_player
	var held_at := frozen_player.current_animation_position
	for _i in range(30):
		await get_tree().process_frame
	if absf(frozen_player.current_animation_position - held_at) > 0.01:
		_fail("a frozen pose drifted from %.2fs to %.2fs -- something is still driving the AnimationPlayer" % [held_at, frozen_player.current_animation_position])
	else:
		print("PASS: a frozen pose really holds its single frame (%.2fs into 'getting_up')" % held_at)

	frozen.queue_free()
	await get_tree().process_frame

## --- 3. With no design baked yet (the state this ships in until a
## character is drawn and baked), he must come up plain white rather than
## erroring or vanishing. ---
func _check_plain_white_without_a_design() -> void:
	var old_guy: CharacterBody3D = UniqueNpcScene.instantiate()
	old_guy.design = null
	add_child(old_guy)
	await get_tree().process_frame

	var character: Node = old_guy.get_node_or_null("Character")
	if character == null or character.atlas_image == null:
		_fail("an NPC with no design failed to build a body at all")
	elif not character.atlas_image.get_pixel(1, 1).is_equal_approx(Color.WHITE):
		_fail("an NPC with no design came up already painted")
	else:
		print("PASS: an NPC with no design baked yet is a plain white placeholder")

	old_guy.queue_free()
	await get_tree().process_frame

## --- 4. THE one that matters for the whole drawn-NPC pipeline: a design
## really lands on the live body. Applied through the real
## NpcDesign.apply_to path, then read back off the character's own atlas --
## which is what catches the "applied before tree entry, silently wiped by
## _ready()" ordering bug. ---
func _check_a_design_actually_lands_on_the_body() -> void:
	# A real design built the same way the bake script builds one: take a
	# genuine character atlas, paint it a colour nothing else would produce,
	# and round-trip it through PNG bytes exactly as characters.cfg does.
	var source: Node3D = CharacterScene.instantiate()
	add_child(source)
	await get_tree().process_frame

	var marker := Color(0.2, 0.45, 0.85)
	var painted: Image = source.atlas_image.duplicate()
	painted.fill(marker)

	var design: Resource = NpcDesignScript.new()
	design.display_name = "Test Subject"
	design.atlas_png = painted.save_png_to_buffer()
	design.head_shape = source.get_head_shape()
	design.hair_style = "none"
	design.height_scale = 1.0
	source.queue_free()

	var old_guy: CharacterBody3D = UniqueNpcScene.instantiate()
	old_guy.design = design
	add_child(old_guy)
	await get_tree().process_frame

	var character: Node = old_guy.get_node_or_null("Character")
	if character == null or character.atlas_image == null:
		_fail("an NPC with a design failed to build a body")
	elif not _colors_match(character.atlas_image.get_pixel(1, 1), marker):
		_fail("the baked design never reached the NPC's body -- atlas pixel is %s, expected %s (the _ready() atlas wipe is back)" % [str(character.atlas_image.get_pixel(1, 1)), str(marker)])
	else:
		print("PASS: a baked creator design really lands on the NPC's live body")

	# And the design's own saved name is what the dialogue box announces,
	# with no second copy of that string to drift.
	old_guy.speaker_name_override = ""
	if old_guy.speaker_name() != "Test Subject":
		_fail("the speaker name fell back to '%s' instead of the design's own display_name" % old_guy.speaker_name())
	else:
		print("PASS: the dialogue speaker name comes from the design itself")

	old_guy.queue_free()
	await get_tree().process_frame

## --- 5. The box walks its lines and signals completion exactly once --
## not zero times (a caller waiting on it would hang) and not twice. ---
func _check_dialogue_advances_and_finishes_once() -> void:
	var box: CanvasLayer = DialogueBoxScene.instantiate()
	add_child(box)
	await get_tree().process_frame

	_finished_count = 0
	box.finished.connect(_on_dialogue_finished)

	if box.is_open():
		_fail("the dialogue box started out already open")

	box.open("Old Guy", [{"text": "first"}, {"text": "second"}])
	if not box.is_open():
		_fail("the dialogue box didn't open")

	# Driven with real ENTER key events, not by calling a method: the whole
	# point of this change is that Enter advances, and that path (an
	# InputEvent reaching _input, being consumed) is what could break.
	_press_enter(box)
	if not box.is_open():
		_fail("Enter closed the box after the first of two lines")
	_press_enter(box)
	if box.is_open():
		_fail("the dialogue box stayed open past its last line")
	elif _finished_count != 1:
		_fail("finished fired %d times for one conversation, expected exactly 1" % _finished_count)
	else:
		print("PASS: Enter walks every line and the conversation finishes exactly once")

	# A stray Enter/close after the end must be a no-op, not a second
	# finished.
	_press_enter(box)
	box.close()
	if _finished_count != 1:
		_fail("interacting after the conversation ended fired finished again (%d total)" % _finished_count)
	else:
		print("PASS: pressing Enter again after a conversation ends is a no-op")

	# An empty node list closes out immediately rather than showing a blank box.
	box.open("Nobody", [])
	if box.is_open():
		_fail("opening a dialogue with no lines showed an empty box")
	else:
		print("PASS: a dialogue with no lines never shows an empty box")

	# --- Replies: navigable by arrows AND A/D, confirmed with Enter. ---
	_chosen_id = ""
	box.option_chosen.connect(_on_option_chosen)
	box.open("Old Guy", [{"text": "pick one", "options": [
		{"text": "Sure", "id": "accept"},
		{"text": "No thanks", "id": "decline"},
	]}])
	if not box.is_choosing():
		_fail("a line carrying replies didn't put the box into a choice")
	elif box._selected != 0:
		_fail("the first reply wasn't selected by default (selected=%d)" % box._selected)
	else:
		print("PASS: a line with replies waits on a choice, starting on the first")

	_press_key(box, KEY_D)
	if box._selected != 1:
		_fail("D didn't move the selection (selected=%d)" % box._selected)
	else:
		_press_key(box, KEY_UP)
		if box._selected != 0:
			_fail("the arrow keys didn't move the selection back (selected=%d)" % box._selected)
		else:
			print("PASS: A/D and the arrow keys both move between replies")

	# Past the end wraps rather than sticking.
	_press_key(box, KEY_LEFT)
	if box._selected != 1:
		_fail("moving past the first reply didn't wrap round (selected=%d)" % box._selected)
	else:
		print("PASS: moving past either end wraps round")

	_press_enter(box)
	if _chosen_id != "decline":
		_fail("Enter picked '%s', expected the highlighted 'decline'" % _chosen_id)
	elif box.is_open():
		_fail("the box stayed open after a reply was picked")
	else:
		print("PASS: Enter picks the highlighted reply and reports its id")

	box.queue_free()
	await get_tree().process_frame

## Feeds a real key press into the box's own _input, the same path a player
## keypress takes.
func _press_key(box: CanvasLayer, keycode: Key) -> void:
	var event := InputEventKey.new()
	event.keycode = keycode
	event.pressed = true
	box._input(event)

func _press_enter(box: CanvasLayer) -> void:
	_press_key(box, KEY_ENTER)

func _on_option_chosen(id: String) -> void:
	_chosen_id = id

func _on_dialogue_finished() -> void:
	_finished_count += 1

## --- 8. He must not appear anywhere in the intro cutscene. That's achieved
## by not spawning him until control hands back, so what's checked is the
## timing itself: nothing in the world during the cutscene, a single Old Guy
## the instant it ends, and no waiting at all for a loaded character who has
## no cutscene to stay out of.
##
## main.gd is driven directly (.new(), never added to the tree, so its own
## _ready() -- title screen, network signals -- doesn't run) with a stub
## city and a stub player. That keeps this a real test of the actual
## scheduling function rather than a 15-second wait on a real fall. ---
func _check_he_stays_out_of_the_intro_cutscene() -> void:
	var main = MainScript.new()
	var city := Node3D.new()
	add_child(city)
	main._city = city

	var stub: CharacterBody3D = _StubPlayer.new()
	add_child(stub)

	# New character: the cutscene is running, so nothing should exist yet.
	main._spawn_old_guy_when_playable(stub, Vector3.ZERO, true)
	await get_tree().process_frame
	if _count_old_guys(city) != 0:
		_fail("Old Guy was spawned during the intro cutscene -- he'd be in the fall/looking-around shots")
	else:
		print("PASS: Old Guy doesn't exist while the intro cutscene is playing")

	stub.intro_cutscene_finished.emit()
	await get_tree().process_frame
	if _count_old_guys(city) != 1:
		_fail("after the cutscene handed over there were %d Old Guys, expected exactly 1" % _count_old_guys(city))
	else:
		print("PASS: Old Guy appears once the cutscene hands control back")

	# A second emit must not produce a twin standing inside the first.
	stub.intro_cutscene_finished.emit()
	await get_tree().process_frame
	if _count_old_guys(city) != 1:
		_fail("a repeated cutscene-finished signal spawned a duplicate Old Guy (%d total)" % _count_old_guys(city))
	else:
		print("PASS: a repeated finish signal doesn't duplicate him")

	# Loaded character: no cutscene, so he's there immediately.
	var city2 := Node3D.new()
	add_child(city2)
	main._city = city2
	var stub2: CharacterBody3D = _StubPlayer.new()
	add_child(stub2)
	main._spawn_old_guy_when_playable(stub2, Vector3.ZERO, false)
	await get_tree().process_frame
	if _count_old_guys(city2) != 1:
		_fail("a loaded character (no cutscene) didn't get Old Guy placed immediately")
	else:
		print("PASS: a loaded character finds Old Guy already standing there")

	# And he really is off to the side and BEHIND the landing point, so
	# handing control back doesn't pop him into view straight ahead.
	var offset: Vector3 = MainScript.OLD_GUY_SPAWN_OFFSET
	if offset.z <= 0.0:
		_fail("Old Guy is placed in front of the player (z=%.1f) -- he'd pop into view at handoff" % offset.z)
	elif absf(offset.x) < 6.0:
		_fail("Old Guy is only %.1fm to the side -- that isn't 'off to the side'" % absf(offset.x))
	else:
		print("PASS: he's placed %.1fm to the side and %.1fm behind the landing point" % [absf(offset.x), offset.z])

	main.free()
	city.queue_free()
	city2.queue_free()
	stub.queue_free()
	stub2.queue_free()
	await get_tree().process_frame

func _count_old_guys(city: Node) -> int:
	var count := 0
	for child in city.get_children():
		if child.name.begins_with("OldGuy"):
			count += 1
	return count

## The only thing main.gd's own scheduling actually needs from a Player.
class _StubPlayer extends CharacterBody3D:
	signal intro_cutscene_finished

## Color comparison with a one-8-bit-step tolerance, NOT is_equal_approx.
## A design round-trips through real PNG bytes (that's the whole point of
## npc_design.gd storing them verbatim), and PNG is 8 bits per channel, so
## 0.45 comes back as 114/255 = 0.4471. is_equal_approx treats that as a
## mismatch and would fail a design that arrived perfectly intact.
func _colors_match(a: Color, b: Color) -> bool:
	var tolerance := 1.5 / 255.0
	return absf(a.r - b.r) <= tolerance and absf(a.g - b.g) <= tolerance and absf(a.b - b.b) <= tolerance

## --- 6. Leaving mid-sentence closes the box. Without this the subtitle
## stays on screen for an NPC who's no longer anywhere near you. ---
func _check_walking_away_closes_the_box() -> void:
	var player: CharacterBody3D = PlayerScene.instantiate()
	add_child(player)
	player.global_position = Vector3(0, 0.1, 0)
	player.add_to_group("players")

	var old_guy: CharacterBody3D = UniqueNpcScene.instantiate()
	add_child(old_guy)
	old_guy.global_position = Vector3(0, 0.1, 1.2) # well inside INTERACT_RANGE
	await get_tree().process_frame
	await get_tree().physics_frame
	await get_tree().physics_frame

	# Driven directly rather than through a synthetic key press: the E poll
	# itself is covered by the checks above, and what's under test here is
	# the proximity exit, not the input path.
	old_guy._begin_talking()
	if not old_guy.is_talking():
		_fail("the NPC didn't start talking with a player standing next to him")
	else:
		player.global_position = Vector3(0, 0.1, 60.0)
		for _i in range(10):
			await get_tree().physics_frame
		if old_guy.is_talking():
			_fail("walking 60m away left the dialogue box still open")
		else:
			print("PASS: walking away from an NPC closes the conversation")

	player.queue_free()
	old_guy.queue_free()
	await get_tree().process_frame

## --- 7. He can't be killed. A story NPC who ragdolls the first time you
## mistime a punch takes his dialogue with him. ---
func _check_punching_him_does_nothing() -> void:
	var old_guy: CharacterBody3D = UniqueNpcScene.instantiate()
	add_child(old_guy)
	await get_tree().process_frame

	old_guy.take_damage(9999.0, self)
	await get_tree().process_frame

	if not is_instance_valid(old_guy):
		_fail("punching the story NPC freed him outright")
	# == true, not bool(): a story NPC has no is_dead property at all (he
	# isn't a combatant), and get() returns null for one that doesn't
	# exist -- which bool() refuses to convert.
	elif old_guy.get("is_dead") == true:
		_fail("the story NPC can be killed -- his dialogue would go with him")
	else:
		print("PASS: the story NPC shrugs off damage instead of dying")

	old_guy.queue_free()
	await get_tree().process_frame
