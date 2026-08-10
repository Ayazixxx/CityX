extends CanvasLayer
## Development-only debug console: "`" toggles it, type a command, Enter to
## run it. Gated on OS.is_debug_build() at the call site (see player.gd's
## own _ready()) -- never added to the tree at all in a real release
## export, so there's no way to reach it outside development.
##
## Lives as a direct child of the player it belongs to (see player.gd's own
## _ready()), purely so get_parent() is a free, direct reference to "the
## player" -- same reasoning HUD/PauseMenu already use their own placement
## for. get_parent().get_parent() is that player's own parent in the scene
## tree, which is always the city (see main.gd's own _city.add_child(player)
## call sites) -- that's where a spawned command target actually belongs,
## not under the player itself.
##
## While open, the game is fully paused (same get_tree().paused = true /
## mouse-release pattern pause_menu.gd already uses, see its own comment)
## and this node's own process_mode is ALWAYS so it keeps receiving input
## to notice "`" again while everything else is frozen. Uses _input (not
## _unhandled_input) specifically so the toggle key still works even while
## the InputField has keyboard focus -- otherwise a focused LineEdit would
## just consume "`" as a literal typed character instead of ever reaching a
## toggle check.
##
## No guard against also having the pause menu open at the same time --
## a real, known gap, left alone since this is a dev-only tool and doubling
## up on paused-state juggling isn't worth the complexity it'd add.

const GreatswordScene := preload("res://weapons/greatsword.tscn")
const MartyrBladeScene := preload("res://weapons/martyr_blade.tscn")
const RunebladeScene := preload("res://weapons/runeblade.tscn")
const PistolScene := preload("res://weapons/pistol.tscn")
const WolfScene := preload("res://npc/wolf.tscn")
const SlothScene := preload("res://npc/sloth.tscn")
const MustangScene := preload("res://vehicles/mustang.tscn")
const TopHatScene := preload("res://wearables/top_hat.tscn")
const HockeyMaskScene := preload("res://wearables/hockey_mask.tscn")
const GlassBottleScene := preload("res://weapons/glass_bottle.tscn")
const TShirtScene := preload("res://wearables/t_shirt.tscn")
const HoodieScene := preload("res://wearables/hoodie.tscn")
const HoodedCloakScene := preload("res://wearables/hooded_cloak.tscn")
const SkelliJeansScene := preload("res://wearables/skelli_jeans.tscn")
const CrimsonClawMonsterScene := preload("res://npc/crimson_claw_monster.tscn")
const MutantScene := preload("res://npc/mutant.tscn")
const GunmanScene := preload("res://npc/gunman.tscn")
const RadioScene := preload("res://radio/radio.tscn")
const MirrorScene := preload("res://mirror/mirror.tscn")
const FlashlightScene := preload("res://items/flashlight.tscn")
const GrenadeScene := preload("res://weapons/grenade.tscn")
const GrapplingHookScene := preload("res://items/grappling_hook.tscn")
const ScratcherScene := preload("res://npc/scratcher.tscn")
const CivilianScene := preload("res://npc/civilian.tscn")

@onready var _panel: Control = $Panel
@onready var _output: RichTextLabel = $Panel/VBox/Output
@onready var _input_field: LineEdit = $Panel/VBox/InputField

var _was_mouse_captured := false

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	_panel.visible = false
	_input_field.text_submitted.connect(_on_command_submitted)
	_log("Dev console ready. Try: /spawn sword, /spawn martyrblade, /spawn runeblade, /spawn pistol, /spawn wolf, /spawn sloth, /spawn crimsonclaw, /spawn mutant, /spawn gunman, /spawn scratcher, /spawn civilian, /spawn mustang, /spawn tophat, /spawn hockeymask, /spawn bottle, /spawn grenade, /spawn grapple, /spawn shirt, /spawn hoodie, /spawn cloak, /spawn jeans, /spawn radio, /sword unlock, /sword pose, /gun unlock, /gun pose, /gun shootpose, /gun unfreeze, /car pose, /wearable unlock, /wearable pose, /bottle unlock, /bottle pose, /crimsonclaw pose, /camera unlock, /camera pose, /xp <amount>, /fly, /infinitehealth, /infinitesprint, /weather <rain|clear> (host/singleplayer only), /time <sunrise|noon|sunset|night>, /gfx <high|low>")

func _input(event: InputEvent) -> void:
	if event.is_action_pressed("toggle_console") and not event.is_echo():
		get_viewport().set_input_as_handled()
		if _panel.visible:
			_close()
		else:
			_open()

## Mirrors ui/chat_box.gd's own is_open() -- lets a sibling (see
## chat_box.gd's own _input()) check this without caring how it's tracked
## internally.
func is_open() -> bool:
	return _panel.visible

func _open() -> void:
	_was_mouse_captured = Input.mouse_mode == Input.MOUSE_MODE_CAPTURED
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	get_tree().paused = true
	_panel.visible = true
	_input_field.grab_focus()

func _close() -> void:
	_panel.visible = false
	get_tree().paused = false
	if _was_mouse_captured:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	_input_field.release_focus()

func _log(text: String) -> void:
	_output.append_text(text + "\n")

func _on_command_submitted(text: String) -> void:
	_input_field.text = ""
	var trimmed := text.strip_edges()
	if trimmed.is_empty():
		return
	_log("[color=gray]> " + trimmed + "[/color]")
	_run_command(trimmed)

func _run_command(command: String) -> void:
	var parts := command.split(" ", false)
	if parts.is_empty():
		return
	var verb := parts[0].to_lower().trim_prefix("/")
	match verb:
		"spawn":
			if parts.size() < 2:
				_log("usage: /spawn <name>")
			else:
				_spawn(parts[1].to_lower())
		"sword":
			if parts.size() < 2:
				_log("usage: /sword <unlock|lock|pose>")
			else:
				_sword_command(parts[1].to_lower())
		"arms":
			if parts.size() < 4:
				_log("usage: /arms <forward> <twist> <spread>   degrees, e.g. /arms 0 0 12")
			else:
				_arms_command(parts[1], parts[2], parts[3])
		"fly":
			_toggle_fly()
		"infinitehealth":
			_toggle_infinite_health()
		"infinitesprint":
			_toggle_infinite_sprint()
		"gun":
			if parts.size() < 2:
				_log("usage: /gun <unlock|lock|pose>")
			else:
				_gun_command(parts[1].to_lower())
		"car":
			if parts.size() < 2:
				_log("usage: /car pose")
			else:
				_car_command(parts[1].to_lower())
		"wearable":
			if parts.size() < 2:
				_log("usage: /wearable <unlock|lock|pose>")
			else:
				_wearable_command(parts[1].to_lower())
		"bottle":
			if parts.size() < 2:
				_log("usage: /bottle <unlock|lock|pose>")
			else:
				_bottle_command(parts[1].to_lower())
		"crimsonclaw":
			if parts.size() < 2:
				_log("usage: /crimsonclaw pose")
			else:
				_crimson_claw_command(parts[1].to_lower())
		"camera":
			if parts.size() < 2:
				_log("usage: /camera <unlock|lock|pose>")
			else:
				_camera_command(parts[1].to_lower())
		"xp":
			if parts.size() < 2:
				_log("usage: /xp <amount>")
			else:
				_xp_command(parts[1])
		"weather":
			if parts.size() < 2:
				_log("usage: /weather <rain|clear>")
			else:
				_weather_command(parts[1].to_lower())
		"time":
			if parts.size() < 2:
				_log("usage: /time <sunrise|morning|noon|sunset|night|midnight|0.0-1.0>")
			else:
				_time_command(parts[1].to_lower())
		"gfx":
			if parts.size() < 2:
				_log("usage: /gfx <high|low> -- toggles the expensive effects (SSIL, SSR, volumetric fog)")
			else:
				_gfx_command(parts[1].to_lower())
		_:
			_log("unknown command: " + verb)

## Jumps the day/night cycle straight to a time of day. The full cycle is 34
## real minutes (see city/day_night_cycle.gd), so without this, checking what
## a sunset actually looks like means waiting up to 20 minutes for one.
##
## Named times are given as a fraction of the whole cycle, derived from that
## file's own DAY_SECONDS/NIGHT_SECONDS split rather than hardcoded: the sun
## crosses the horizon at t=0 (sunrise) and again at exactly DAY_SECONDS
## (sunset), so those two are where the warm light actually lives.
func _time_command(which: String) -> void:
	var cycle: Node = _day_night_cycle()
	if cycle == null:
		_log("no day/night cycle in this scene")
		return
	var day: float = cycle.DAY_SECONDS
	var total: float = cycle.CYCLE_SECONDS
	var seconds := -1.0
	match which:
		"sunrise", "dawn":
			seconds = 0.0
		"morning":
			seconds = day * 0.25
		"noon", "midday", "day":
			seconds = day * 0.5
		"afternoon":
			seconds = day * 0.75
		"sunset", "dusk", "golden":
			seconds = day
		"night", "evening":
			seconds = day + (total - day) * 0.25
		"midnight":
			seconds = day + (total - day) * 0.5
		_:
			if not which.is_valid_float():
				_log("usage: /time <sunrise|morning|noon|sunset|night|midnight|0.0-1.0>")
				return
			seconds = clampf(which.to_float(), 0.0, 1.0) * total
	cycle._time = seconds
	# Applied immediately rather than waiting for the next frame's own
	# _process, so the console reports a state that's already on screen.
	cycle._process(0.0)
	_log("time set to %s (%.1f min into the %.0f min cycle)" % [which, seconds / 60.0, total / 60.0])

## Turns the expensive screen-space effects on and off together, so their
## real framerate cost can be measured by flipping between them rather than
## guessed at. Everything else the lighting pass added (the tonemap curve,
## the ambient balance, the sun's own colour) is free and stays on in both
## modes -- those are what actually changed the look, and bundling them in
## here would make this read as "turn the good lighting off".
##
## SSIL is deliberately NOT in here: it was removed for QUALITY, not cost
## (its indirect-light estimate was a source of the reported shadow grain),
## so a "high" setting has no business switching it back on.
func _gfx_command(level: String) -> void:
	var cycle: Node = _day_night_cycle()
	if cycle == null:
		_log("no day/night cycle in this scene")
		return
	var env: Environment = cycle.get_parent().get_node("WorldEnvironment").environment
	var high := level != "low"
	env.ssr_enabled = high
	env.volumetric_fog_enabled = high
	_log("gfx %s -- reflections/volumetric fog %s (tonemap and lighting unchanged)"
		% ["high" if high else "low", "on" if high else "off"])

func _day_night_cycle() -> Node:
	for node in get_tree().get_nodes_in_group("day_night_cycle"):
		return node
	# Not grouped: found by walking from the player up to the city, which is
	# where city.tscn puts it.
	var player: Node = get_parent()
	var world: Node = player.get_parent() if player != null else null
	return world.get_node_or_null("DayNightCycle") if world != null else null

## Finds whichever two-handed sword is currently equipped (a child of the
## player's own Character hand attachment, see character.gd's own
## get_hand_node()) -- greatsword.gd or martyr_blade.gd, whichever's script
## path actually matches -- returns null if nothing's equipped yet, rather
## than assuming a /spawn command was ever used.
func _find_equipped_sword() -> Node3D:
	var player: Node3D = get_parent()
	var character: Node = player.get_node_or_null("Character")
	if character == null or not character.has_method("get_hand_node"):
		return null
	var hand: Node3D = character.get_hand_node()
	if hand == null:
		return null
	for child in hand.get_children():
		if child.get_script() == null:
			continue
		var script_path: String = str(child.get_script().resource_path)
		if script_path.contains("greatsword") or script_path.contains("martyr_blade") or script_path.contains("runeblade"):
			return child
	return null

## Same lookup as _find_equipped_sword(), but for guns (see weapons/pistol.gd)
## -- also parented under get_hand_node() once equipped, just matched by its
## own script path instead.
func _find_equipped_gun() -> Node3D:
	var player: Node3D = get_parent()
	var character: Node = player.get_node_or_null("Character")
	if character == null or not character.has_method("get_hand_node"):
		return null
	var hand: Node3D = character.get_hand_node()
	if hand == null:
		return null
	for child in hand.get_children():
		if child.get_script() == null:
			continue
		if str(child.get_script().resource_path).contains("pistol"):
			return child
	return null

## /gun unlock/lock/pose -- same tuning workflow as /sword (see its own
## comment), just for weapons/pistol.gd's own GRIP_HAND_ROTATION/
## GRIP_HAND_POSITION/GRIP_SCALE instead, which started as a rough
## placeholder guess rather than something measured against a real holding
## animation. shootpose/unfreeze are the same idea applied to the gun_shoot
## recoil animation specifically (see their own comment below) -- tuning
## position/rotation/size while the character is just standing there in the
## default idle pose doesn't tell you how the grip actually looks the
## instant it fires, since the hand's own bone pose during recoil isn't the
## same as idle.
## Finds the car currently relevant to the player -- whichever car they're
## seated in (player.gd's own _seated_car, set by vehicles/car.gd's own
## _begin_enter) if any, otherwise the nearest spawned car sibling in the
## world (matched by script path, same pattern _find_equipped_sword/gun
## already use). Falls back to searching siblings so /car pose still works
## before actually getting in -- useful for eyeballing DriverSeat/
## PassengerSeat placement from outside the car too, not just from the seat
## itself.
func _find_car() -> Node:
	var player: Node = get_parent()
	var seated: Node = player.get("_seated_car") if player.has_method("begin_car_transition") else null
	if seated != null:
		return seated
	var world: Node = player.get_parent()
	if world == null:
		return null
	for child in world.get_children():
		if child.get_script() != null and str(child.get_script().resource_path).contains("vehicles/car"):
			return child
	return null

## DriverSeat/PassengerSeat aren't re-driven every frame the way a held
## weapon's own grip pose is (see car.gd's own _build_seats -- set once in
## _ready() and never touched again), so there's no "unlock" needed to stop
## something else from fighting a manual edit: just drag the DriverSeat/
## PassengerSeat node's own Transform directly via the Debugger panel's
## Remote tab (Player -> ... -> Mustang -> DriverSeat, while seated the
## Player itself is reparented right underneath it, so you'll see them move
## together live) and /car pose to print wherever you left it, to copy into
## car.gd's own SEAT_X/Y/Z_FRACTION constants by hand.
func _car_command(sub: String) -> void:
	var car := _find_car()
	if car == null:
		_log("no car nearby -- spawn one with /spawn mustang first")
		return
	match sub:
		"pose":
			var driver_seat: Node3D = car.get_node("DriverSeat")
			var passenger_seat: Node3D = car.get_node("PassengerSeat")
			print("DriverSeat position = ", driver_seat.position)
			print("DriverSeat rotation (degrees) = ", driver_seat.rotation * 180.0 / PI)
			print("PassengerSeat position = ", passenger_seat.position)
			print("PassengerSeat rotation (degrees) = ", passenger_seat.rotation * 180.0 / PI)
			_log("printed DriverSeat/PassengerSeat position+rotation to the log (see the terminal, not this panel) -- drag them directly via the Debugger panel's Remote tab (.../Mustang/DriverSeat)")
		_:
			_log("usage: /car pose")

## Every currently-worn wearable (see wearables/wearable.gd) -- unlike a
## sword/gun there's no single "the equipped one," since a hat and a mask
## can be worn together (different slots, see get_wearable_slot()), so
## /wearable unlock/lock/pose operates on ALL of them at once rather than
## needing a slot argument.
func _find_equipped_wearables() -> Array:
	var player: Node3D = get_parent()
	var character: Node = player.get_node_or_null("Character")
	if character == null or not character.has_method("get_head_node"):
		return []
	var head: Node3D = character.get_head_node()
	if head == null:
		return []
	var result := []
	for child in head.get_children():
		if child.has_method("get_wearable_slot"):
			result.append(child)
	return result

## /wearable unlock/lock/pose -- same tuning workflow as /sword (see its own
## comment), just for wearables/wearable.gd's own HEAD_POSITION/
## HEAD_ROTATION instead, applied to every currently-worn item at once.
func _wearable_command(sub: String) -> void:
	var worn := _find_equipped_wearables()
	if worn.is_empty():
		_log("nothing currently worn -- pick up a hat/mask first")
		return
	match sub:
		"unlock":
			for w in worn:
				w.set_manual_override(true)
			_log("wearable pose unlocked -- edit its Transform via the Debugger panel's Remote tab (Player/Character/BodySkeleton/HeadPivot/<item>)")
		"lock":
			for w in worn:
				w.set_manual_override(false)
			_log("wearable pose locked")
		"pose":
			for w in worn:
				w.print_current_pose()
			_log("printed current position/rotation to the log (see the terminal, not this panel)")
		_:
			_log("usage: /wearable <unlock|lock|pose>")

func _gun_command(sub: String) -> void:
	var gun := _find_equipped_gun()
	if gun == null:
		_log("no gun currently equipped -- pick one up first")
		return
	match sub:
		"unlock":
			gun.set_manual_override(true)
			_log("gun pose unlocked -- edit its Transform (Position/Rotation/Scale) via the Debugger panel's Remote tab (Player/Character/BodySkeleton/HandRPivot/Pistol)")
		"lock":
			gun.set_manual_override(false)
			_log("gun pose locked -- back to the normal automatic grip rotation")
		"pose":
			gun.print_current_pose()
			_log("printed current position/rotation/scale to the log (see the terminal, not this panel)")
		"shootpose":
			_gun_shootpose(gun)
		"unfreeze":
			_gun_unfreeze()
		_:
			_log("usage: /gun <unlock|lock|pose|shootpose|unfreeze>")

## Triggers the gun_shoot recoil animation and then immediately freezes the
## character's own AnimationPlayer partway through it (GunShootAnim is
## trimmed to a brisk 0.3s, see character.gd's own shoot()/GunShootAnim
## comment -- far too quick to eyeball or nudge a Transform against in the
## Remote debugger before it's already back to idle). speed_scale=0 holds
## whatever pose seek() just landed on indefinitely, same as pausing a
## video -- .seek()/.speed_scale both apply immediately regardless of the
## engine's own pause state, so this works fine with the console (and so
## the whole game) paused, the same way /gun unlock's own Remote-debugger
## workflow already relies on. Also unlocks the gun's own manual override
## in the same step, since posing against a frozen recoil frame is the
## whole point -- no reason to make this a separate command too.
func _gun_shootpose(gun: Node3D) -> void:
	var player: Node3D = get_parent()
	var character: Node = player.get_node_or_null("Character")
	if character == null or not character.has_method("shoot"):
		_log("no character to pose against")
		return
	character.shoot()
	character.anim_player.seek(0.12, true)
	character.anim_player.speed_scale = 0.0
	gun.set_manual_override(true)
	_log("shoot animation frozen mid-recoil, gun pose unlocked -- edit its Transform via the Debugger panel's Remote tab (Player/Character/BodySkeleton/HandRPivot/Pistol), /gun pose to print it, /gun unfreeze when done")

## Resumes normal animation playback after /gun shootpose froze it.
func _gun_unfreeze() -> void:
	var player: Node3D = get_parent()
	var character: Node = player.get_node_or_null("Character")
	if character == null or not character.has_method("shoot"):
		_log("no character to unfreeze")
		return
	character.anim_player.speed_scale = 1.0
	_log("shoot animation unfrozen -- back to normal playback")

## /sword unlock -- freezes the greatsword's own auto pose-update (see
## greatsword.gd's own set_manual_override()) so its Transform can be
## dragged/typed by hand via Godot's own Remote Scene Tree debugger
## (Debugger panel -> Remote tab, while the game is actually running --
## navigate to this player's own Character/BodySkeleton/HandRPivot/
## Greatsword and edit Position/Rotation directly) instead of blindly
## guessing numbers. /sword pose then prints whatever that ends up at, to
## copy into GRIP_HAND_ROTATION/HEAVY_ATTACK_GRIP_ROTATION by hand. /sword
## lock hands control back to the normal auto pose-update.
func _sword_command(sub: String) -> void:
	var sword := _find_equipped_sword()
	if sword == null:
		_log("no sword currently equipped -- pick one up first")
		return
	match sub:
		"unlock":
			sword.set_manual_override(true)
			_log("sword pose unlocked -- edit its Transform via the Debugger panel's Remote tab (Player/Character/BodySkeleton/HandRPivot/Greatsword)")
		"lock":
			sword.set_manual_override(false)
			_log("sword pose locked -- back to the normal automatic grip rotation")
		"pose":
			sword.print_current_pose()
			_log("printed current position/rotation to the log (see the terminal, not this panel)")
		_:
			_log("usage: /sword <unlock|lock|pose>")

## /camera unlock/lock/pose -- same manual-tuning workflow as /sword/
## /gun/etc. (see _sword_command's own comment), but for the player's own
## over-the-shoulder camera offset (see player.gd's own SHOULDER_OFFSET_X)
## AND the ADS zoom/"scope" FOV (see ADS_FOV_DEGREES) -- unlock freezes BOTH
## per-frame lerps at once (see player.gd's own _camera_manual_override
## comment), not just the shoulder offset. Player itself owns
## set_camera_manual_override/print_camera_pose (not a per-weapon node),
## since the camera isn't something that gets equipped/unequipped.
func _camera_command(sub: String) -> void:
	var player: Node3D = get_parent()
	match sub:
		"unlock":
			player.set_camera_manual_override(true)
			_log("camera pose unlocked -- edit SpringArm3D's own Position (POV/shoulder offset) via the Debugger panel's Remote tab (Player/CameraPivot/SpringArm3D), or Camera3D one level deeper for a rotation/angle tweak AND its own Fov (the aim 'scope' zoom level -- hold right-click with a gun equipped to preview it while you drag Fov)")
		"lock":
			player.set_camera_manual_override(false)
			_log("camera pose locked -- back to the normal automatic shoulder offset + ADS zoom")
		"pose":
			player.print_camera_pose()
			_log("printed current position/rotation/fov to the log (see the terminal, not this panel)")
		_:
			_log("usage: /camera <unlock|lock|pose>")

## Dev-only shortcut for testing the level/stat-point system (see player.gd's
## own add_xp()) without needing to actually grind wolf kills -- real
## gameplay only ever grants XP through npc/wolf.gd's own _die(). Accepts a
## plain number (int or float, e.g. "/xp 250"); anything that doesn't parse
## logs a usage message instead of silently doing nothing.
func _xp_command(amount_str: String) -> void:
	if not amount_str.is_valid_float():
		_log("usage: /xp <amount>")
		return
	var player: Node3D = get_parent()
	var amount := amount_str.to_float()
	var level_before: int = player.level
	player.add_xp(amount)
	_log("granted %s xp (level %d -> %d, %d stat point(s)/%d skill point(s) available)" % [amount_str, level_before, player.level, player.stat_points, player.skill_points])

## Live arm-pose tuning, the counterpart to /gun pose and /sword pose -- a
## free rotation of both arms about their own shoulders, so a garment's
## sleeve fit can be dialled in while wearing it instead of
## edit-rebake-relaunch.
##
## Three axes in degrees: forward (x), twist (y), spread (z). Mirrored per
## side, so one set of numbers keeps both arms symmetric.
##
## This is a POSE, not the rig: it doesn't touch fused_body.gd's own
## SHOULDER_X/HAND_X and needs no body re-bake. It also moves the BODY only,
## never the garment -- see character.gd's own set_worn_arm_pose(). Once a
## triple looks right it goes into the garment that should apply it
## (wearables/hooded_cloak.gd's own SLEEVE_ARM_ROTATION_DEGREES).
func _arms_command(x_str: String, y_str: String, z_str: String) -> void:
	if not x_str.is_valid_float() or not y_str.is_valid_float() or not z_str.is_valid_float():
		_log("usage: /arms <forward> <twist> <spread>")
		return
	var player: Node3D = get_parent()
	var character: Node = player.get_node_or_null("Character")
	if character == null or not character.has_method("set_worn_arm_pose"):
		_log("no character to pose")
		return
	var degrees := Vector3(x_str.to_float(), y_str.to_float(), z_str.to_float())
	character.set_worn_arm_pose(degrees)
	_log("arms: forward=%.1f twist=%.1f spread=%.1f -- paste those three back when it looks right"
		% [degrees.x, degrees.y, degrees.z])

## Same lookup as _find_equipped_sword/_find_equipped_gun, but for a
## currently-equipped weapons/glass_bottle.gd -- matched by its own script
## path. Only ever finds an EQUIPPED bottle (parented under get_hand_node()),
## never a thrown one -- weapons/thrown_bottle.gd is a separate script/scene
## entirely, spawned fresh into the world, not equipped.
func _find_equipped_bottle() -> Node3D:
	var player: Node3D = get_parent()
	var character: Node = player.get_node_or_null("Character")
	if character == null or not character.has_method("get_hand_node"):
		return null
	var hand: Node3D = character.get_hand_node()
	if hand == null:
		return null
	for child in hand.get_children():
		if child.get_script() == null:
			continue
		if str(child.get_script().resource_path).contains("glass_bottle"):
			return child
	return null

## /bottle unlock/lock/pose -- same tuning workflow as /sword (see its own
## comment), just for weapons/glass_bottle.gd's own GRIP_HAND_ROTATION/
## GRIP_HAND_POSITION instead, which started as a rough placeholder guess
## (no dedicated bottle-holding animation to measure against) rather than
## something measured.
func _bottle_command(sub: String) -> void:
	var bottle := _find_equipped_bottle()
	if bottle == null:
		_log("no bottle currently equipped -- pick one up first")
		return
	match sub:
		"unlock":
			bottle.set_manual_override(true)
			_log("bottle pose unlocked -- edit its Position/Rotation via the Debugger panel's Remote tab (Player/Character/BodySkeleton/HandRPivot/GlassBottle); for SIZE, drill into that same node's own GlassBottle/VisualRoot child and edit ITS Scale instead (nothing re-locks that one, it's already free to edit any time)")
		"lock":
			bottle.set_manual_override(false)
			_log("bottle pose locked -- back to the normal automatic grip rotation")
		"pose":
			bottle.print_current_pose()
			_log("printed current position/rotation/scale to the log (see the terminal, not this panel)")
		_:
			_log("usage: /bottle <unlock|lock|pose>")

## Nearest spawned npc/crimson_claw_monster.gd sibling in the world (same
## script-path-matching fallback _find_car() already uses, since a monster
## is never "equipped" the way a weapon is) -- returns null if none has been
## spawned yet via /spawn crimsonclaw.
func _find_crimson_claw() -> Node:
	var player: Node = get_parent()
	var world: Node = player.get_parent()
	if world == null:
		return null
	for child in world.get_children():
		if child.get_script() != null and str(child.get_script().resource_path).contains("crimson_claw_monster"):
			return child
	return null

## /crimsonclaw pose -- EyeMeshL/EyeMeshR/EyeLightL/EyeLightR (see
## crimson_claw_monster.gd's own _add_eye) are built once and never
## re-touched afterward, so unlike a weapon's own grip there's nothing to
## unlock: just drag them directly via the Debugger panel's own Remote tab
## (spawned as a SIBLING of the player, not held/parented under it -- see
## _spawn_crimson_claw_monster() -- so it's directly under the world root:
## .../CrimsonClawMonster/Model/FacingFlip/EyeMeshL etc., while the game is
## running FROM the editor) and use this to print back wherever they were
## left, to copy into EYE_L/EYE_R/EYE_L_SCALE/EYE_R_SCALE/EYE_L_TILT_DEGREES/
## EYE_R_TILT_DEGREES by hand -- same
## "print the final numbers" role /car pose already fills for
## DriverSeat/PassengerSeat.
func _crimson_claw_command(sub: String) -> void:
	var monster := _find_crimson_claw()
	if monster == null:
		_log("no crimson claw nearby -- spawn one with /spawn crimsonclaw first")
		return
	match sub:
		"pose":
			monster.print_eye_pose()
			_log("printed EyeMeshL/EyeMeshR/EyeLightL/EyeLightR position/scale/tilt to the log (see the terminal, not this panel) -- drag them directly via the Debugger panel's Remote tab (find CrimsonClawMonster/Model/FacingFlip/EyeMeshL under the world root, a sibling of the Player, not nested under it)")
		_:
			_log("usage: /crimsonclaw pose")

## Toggles player.gd's own dev-only fly mode (gravity off, jump=up/
## crouch=down at FLY_SPEED) -- queries the player's own current state via
## is_flying() rather than tracking a separate copy here, so this stays
## correct even if fly mode ever gets toggled some other way.
func _toggle_fly() -> void:
	var player: Node = get_parent()
	if player == null or not player.has_method("set_fly_mode"):
		_log("can't toggle fly -- no player to fly")
		return
	var now_flying: bool = not player.is_flying()
	player.set_fly_mode(now_flying)
	_log("fly mode " + ("ON -- jump=up, crouch=down" if now_flying else "off"))

## "Admin" isn't a preexisting concept anywhere else in this codebase --
## defined here, narrowly, as "host or singleplayer" (see net/network.gd's
## own is_networked()/is_server()): a plain client connected to someone
## else's game is never admin, since granting it there would let any joined
## client silently cheat against a server that isn't expecting it. In
## singleplayer is_networked() is false (no real multiplayer peer at all,
## see network.gd's own comment), so is_server() alone would wrongly read
## false there too -- hence the explicit "not networked" branch, not just
## Net.is_server().
func _is_admin() -> bool:
	return not Net.is_networked() or Net.is_server()

## Dev-only /infinitehealth -- see player.gd's own _infinite_health comment
## for what it actually changes (take_damage()'s own early return). Same
## admin gate and query/flip/set/log toggle shape as _toggle_fly() above,
## just admin-gated first since this is a much more blatant cheat to hand a
## random joined client than flying is.
func _toggle_infinite_health() -> void:
	if not _is_admin():
		_log("admin only -- you're not the host")
		return
	var player: Node = get_parent()
	if player == null or not player.has_method("set_infinite_health"):
		_log("can't toggle infinite health -- no player")
		return
	var now_infinite: bool = not player.is_infinite_health()
	player.set_infinite_health(now_infinite)
	_log("infinite health " + ("ON" if now_infinite else "off"))

## Dev-only /infinitesprint -- see player.gd's own _infinite_stamina comment
## for what it actually changes (_update_stamina()'s own drain branch, plus
## _process_climbing()'s own). Same admin gate/shape as
## _toggle_infinite_health() above.
func _toggle_infinite_sprint() -> void:
	if not _is_admin():
		_log("admin only -- you're not the host")
		return
	var player: Node = get_parent()
	if player == null or not player.has_method("set_infinite_stamina"):
		_log("can't toggle infinite sprint -- no player")
		return
	var now_infinite: bool = not player.is_infinite_stamina()
	player.set_infinite_stamina(now_infinite)
	_log("infinite sprint " + ("ON" if now_infinite else "off"))

## Dev-only /weather rain|clear -- see player/rain_effect.gd's own class
## comment for what this actually turns on/off. Admin-gated (same reasoning
## as _toggle_infinite_health's own comment -- a world-affecting toggle, not
## something a random joined client should get to hand themselves). Unlike
## /fly's own single-verb toggle, this takes an explicit rain|clear argument
## (matching how this kind of weather command already reads in other games,
## e.g. Minecraft's own "/weather clear|rain|thunder") since "start/stop" is
## clearer here than a bare toggle would be.
func _weather_command(sub: String) -> void:
	if not _is_admin():
		_log("admin only -- you're not the host")
		return
	var player: Node = get_parent()
	if player == null or not player.has_method("set_rain_active"):
		_log("can't change the weather -- no player")
		return
	match sub:
		"rain":
			player.set_rain_active(true)
			_log("weather: rain")
		"clear":
			player.set_rain_active(false)
			_log("weather: clear")
		_:
			_log("usage: /weather <rain|clear>")

func _spawn(target_name: String) -> void:
	match target_name:
		"sword":
			_spawn_weapon(GreatswordScene, "a greatsword")
		"martyrblade":
			_spawn_weapon(MartyrBladeScene, "the Blade of the First Martyr")
		"runeblade":
			_spawn_weapon(RunebladeScene, "a runeblade")
		"pistol":
			_spawn_weapon(PistolScene, "a pistol")
		"wolf":
			_spawn_wolf()
		"sloth":
			_spawn_sloth()
		"mustang":
			_spawn_mustang()
		"tophat":
			_spawn_wearable(TopHatScene, "a top hat")
		"hockeymask":
			_spawn_wearable(HockeyMaskScene, "a hockey mask")
		"bottle":
			# _spawn_wearable's own upright/no-rotation ground placement, NOT
			# _spawn_weapon's blade-down tilt -- a bottle's own local origin
			# is its base (see glass_bottle.gd's own geometry, base at
			# local y=0), so it already stands upright with zero rotation;
			# the blade-down flip every sword spawn uses would tip it onto
			# its neck instead, visually wrong for a bottle.
			_spawn_wearable(GlassBottleScene, "a glass bottle")
		"grapple":
			# Upright ground placement, same as the bottle/grenade -- the
			# launcher's own origin is its body centre, so it lands lying
			# on the ground with no rotation applied.
			_spawn_wearable(GrapplingHookScene, "a grappling hook")
		"grenade":
			# Same upright ground placement a bottle uses (see above) -- a
			# grenade's own capsule stands on end with zero rotation, and
			# the sword spawn's blade-down flip would just bury it.
			_spawn_wearable(GrenadeScene, "a frag grenade")
		"shirt":
			_spawn_wearable(TShirtScene, "a t-shirt")
		"hoodie":
			_spawn_wearable(HoodieScene, "a hoodie")
		"cloak":
			# Same upright ground placement every other wearable uses -- the
			# cloak's own origin is its bounding-box centre (set during
			# extraction, see wearables/hooded_cloak.gd), so it lies on the
			# ground like the rest rather than needing its own placement.
			_spawn_wearable(HoodedCloakScene, "a hooded cloak")
		"jeans", "skellijeans":
			_spawn_wearable(SkelliJeansScene, "a pair of Skelli Jeans")
		"crimsonclaw":
			_spawn_crimson_claw_monster()
		"mutant":
			_spawn_mutant()
		"gunman":
			_spawn_gunman()
		"scratcher":
			_spawn_scratcher()
		"civilian":
			_spawn_civilian()
		"radio":
			# Same upright/no-rotation ground placement as "bottle" -- the
			# radio's own local origin is already its base (see radio.gd's
			# own _build_visual(), which lifts the mesh so its lowest point
			# sits at local y=0), so it already stands right-side-up with
			# zero rotation.
			_spawn_wearable(RadioScene, "a radio")
		"mirror":
			# Same upright ground placement every other prop uses -- the
			# mirror's own local origin is its base (see mirror.gd's own
			# _build_visual, which builds upward from y=0), so it stands
			# correctly with no rotation. It faces its own local +Z, which
			# _spawn_wearable's placement points back toward the player.
			_spawn_wearable(MirrorScene, "a mirror")
		"flashlight":
			_spawn_wearable(FlashlightScene, "a flashlight")
		_:
			_log("unknown spawn target: " + target_name)

## Placed 2m in front of wherever the player is CURRENTLY standing/facing
## (not tied to SpawnPoint the way main.gd's own placement is) -- planted
## blade-down the same way, see main.gd's own comment on that rotation.
## Shared by every /spawn <sword> variant -- both weapons use the identical
## placement/rotation convention (see martyr_blade.gd's own class comment on
## why it copies greatsword.gd's grip constants instead of sharing a base).
func _spawn_weapon(scene: PackedScene, log_name: String) -> void:
	var player: Node3D = get_parent()
	var world: Node = player.get_parent()
	if world == null:
		_log("can't spawn -- player isn't in a scene yet")
		return
	var weapon := scene.instantiate()
	world.add_child(weapon)
	var forward: Vector3 = -player.global_transform.basis.z
	weapon.global_position = player.global_position + forward * 2.0 + Vector3(0, 0.93, 0)
	weapon.rotation = Vector3(PI, 0.0, deg_to_rad(12))
	_log("spawned " + log_name + " in front of you")

## Same "in front of wherever the player currently is" placement as
## _spawn_weapon, just resting flat on the ground (no blade-down rotation --
## a hat/mask isn't planted like a sword) with a small clearance so it
## doesn't spawn embedded in the ground plane.
func _spawn_wearable(scene: PackedScene, log_name: String) -> void:
	var player: Node3D = get_parent()
	var world: Node = player.get_parent()
	if world == null:
		_log("can't spawn -- player isn't in a scene yet")
		return
	var wearable := scene.instantiate()
	world.add_child(wearable)
	var forward: Vector3 = -player.global_transform.basis.z
	wearable.global_position = player.global_position + forward * 2.0 + Vector3(0, 0.05, 0)
	_log("spawned " + log_name + " on the ground in front of you")

## Same "in front of wherever the player currently is" placement as
## _spawn_weapon, just further out (4m, not 2m) and standing upright on the
## ground rather than planted blade-down -- a wolf spawned right on top of
## the player would start biting before there's even room to see it coming.
func _spawn_wolf() -> void:
	var player: Node3D = get_parent()
	var world: Node = player.get_parent()
	if world == null:
		_log("can't spawn -- player isn't in a scene yet")
		return
	var wolf := WolfScene.instantiate()
	world.add_child(wolf)
	var forward: Vector3 = -player.global_transform.basis.z
	wolf.global_position = player.global_position + forward * 4.0
	_log("spawned a hostile wolf in front of you -- it's already coming")

## Out at 16m -- a Scratcher runs at the player's own sprint speed (see
## npc/scratcher.gd's own CHASE_SPEED), so spawning it at wolf distance
## would put it on top of you before you could turn around, and there'd be
## no way to see it move.
func _spawn_scratcher() -> void:
	var player: Node3D = get_parent()
	var world: Node = player.get_parent()
	if world == null:
		_log("can't spawn -- player isn't in a scene yet")
		return
	var scratcher := ScratcherScene.instantiate()
	world.add_child(scratcher)
	var forward: Vector3 = -player.global_transform.basis.z
	scratcher.global_position = player.global_position + forward * 16.0
	_log("spawned a Scratcher 16m ahead -- it runs as fast as you sprint, so you can't outrun it")

## Same "in front of wherever the player currently is" placement as
## _spawn_wolf, but out at 12m -- inside npc/gunman.gd's own DETECTION_RANGE
## (26m) so it engages immediately, while landing in the middle of its own
## PREFERRED_RANGE_MIN/MAX firing band, which is where its behaviour is
## actually worth looking at. Spawned at wolf distance it would just
## immediately backpedal.
func _spawn_gunman() -> void:
	var player: Node3D = get_parent()
	var world: Node = player.get_parent()
	if world == null:
		_log("can't spawn -- player isn't in a scene yet")
		return
	var gunman := GunmanScene.instantiate()
	world.add_child(gunman)
	var forward: Vector3 = -player.global_transform.basis.z
	gunman.global_position = player.global_position + forward * 12.0
	_log("spawned an armed gunman 12m ahead -- he's already drawing on you")

## A single pedestrian (see npc/civilian.gd), 6m ahead -- close enough to
## punch immediately. This is now the ONLY way one ever appears: the ambient
## crowd that used to place them automatically is retired (see
## archive/civilian_population/). home_position is set BEFORE add_child so it
## wanders around where it was dropped rather than around wherever
## civilian.gd's own fallback happened to see first.
func _spawn_civilian() -> void:
	var player: Node3D = get_parent()
	var world: Node = player.get_parent()
	if world == null:
		_log("can't spawn -- player isn't in a scene yet")
		return
	var civilian := CivilianScene.instantiate()
	var forward: Vector3 = -player.global_transform.basis.z
	var spot: Vector3 = player.global_position + forward * 6.0
	civilian.home_position = spot
	world.add_child(civilian)
	civilian.global_position = spot
	_log("spawned a civilian 6m ahead -- harmless until you hit them")

## Same "in front of wherever the player currently is" placement as
## _spawn_wolf, just much further out (14m, not 4m) -- sloth.gd's own model
## is ~9.5m tall with arms reaching nearly to the ground, so spawning it as
## close as a wolf would drop it standing on top of/inside the player.
func _spawn_sloth() -> void:
	var player: Node3D = get_parent()
	var world: Node = player.get_parent()
	if world == null:
		_log("can't spawn -- player isn't in a scene yet")
		return
	var sloth := SlothScene.instantiate()
	world.add_child(sloth)
	var forward: Vector3 = -player.global_transform.basis.z
	sloth.global_position = player.global_position + forward * 14.0
	_log("spawned a giant hostile beast in front of you -- run")

## Same "in front of wherever the player currently is" placement as
## _spawn_wolf, a bit further out (6m, not 4m) -- the monster's own claws
## reach forward past its own body origin, and it hovers in on spawn rather
## than needing to fall/walk into position. global_position.y gets
## overwritten to HOVER_HEIGHT by the monster's own _ready() regardless of
## whatever this sets it to, so no Y handling is needed here.
func _spawn_crimson_claw_monster() -> void:
	var player: Node3D = get_parent()
	var world: Node = player.get_parent()
	if world == null:
		_log("can't spawn -- player isn't in a scene yet")
		return
	var monster := CrimsonClawMonsterScene.instantiate()
	world.add_child(monster)
	var forward: Vector3 = -player.global_transform.basis.z
	monster.global_position = player.global_position + forward * 6.0
	_log("spawned a crimson claw monster in front of you -- its eyes are already on you")

## Same "in front of wherever the player currently is" placement as
## _spawn_wolf, but much further out (14m, matching _spawn_sloth's own
## distance) -- mutant.gd's own MODEL_SCALE (5x) makes this ~8.5m tall with
## a comparably long reach, so spawning it as close as a human-sized enemy
## would drop it standing on top of/inside the player, same reasoning
## _spawn_sloth's own comment already documents for a similarly giant
## model. Dropped 1m above ground rather than placed exactly at it --
## unlike crimson_claw_monster.gd's own hover (which force-sets its own Y
## in _ready() regardless), this one walks on real gravity/is_on_floor(),
## so a small drop lets it settle naturally the same way _spawn_mustang's
## own car does.
func _spawn_mutant() -> void:
	var player: Node3D = get_parent()
	var world: Node = player.get_parent()
	if world == null:
		_log("can't spawn -- player isn't in a scene yet")
		return
	var mutant := MutantScene.instantiate()
	world.add_child(mutant)
	var forward: Vector3 = -player.global_transform.basis.z
	mutant.global_position = player.global_position + forward * 14.0 + Vector3(0, 1.0, 0)
	_log("spawned a hostile mutant in front of you -- it's already coming")

## Same "in front of wherever the player currently is" placement as
## _spawn_wolf/_spawn_sloth, facing the same direction the player's
## currently facing (car.gd has no AI to turn it toward anything, unlike
## the wolf/sloth). Dropped 1.2m above ground rather than placed exactly at
## it -- car.gd's own collision box is only known once the car's _ready()
## has already measured its visual mesh, so rather than depend on reading
## that back out immediately after add_child(), this just lets RigidBody3D
## gravity settle it onto the ground the same way any other dropped rigid
## body naturally would, over the first few physics frames.
func _spawn_mustang() -> void:
	var player: Node3D = get_parent()
	var world: Node = player.get_parent()
	if world == null:
		_log("can't spawn -- player isn't in a scene yet")
		return
	var car := MustangScene.instantiate()
	world.add_child(car)
	var forward: Vector3 = -player.global_transform.basis.z
	car.global_position = player.global_position + forward * 6.0 + Vector3(0, 1.2, 0)
	car.rotation.y = player.rotation.y
	_log("spawned a Mustang in front of you -- WASD to drive, Space to handbrake")
