extends RigidBody3D
## GTA-style arcade vehicle controller -- Stage 1 of the approved vehicle
## plan (see the plan's own "Core physics algorithm" section for the exact
## formulas this implements). One rigid body, raycast-only wheels (no
## simulated suspension springs), velocity decomposition + partial lateral-
## velocity cancellation ("grip") as the core handling/drift mechanic --
## deliberately NOT a tire-simulation racer, per the user's own spec.
##
## One script for every vehicle (not a per-vehicle subclass tree like the
## swords) -- a second vehicle later would only ever differ by which
## visual_scene it loads and which @export tuning numbers its own .tscn
## sets, both plain data, so there's nothing to actually subclass. See
## two_handed_sword.gd's own class comment for the same reasoning applied to
## swords, where a second real case (martyr_blade.gd) DID justify a shared
## base -- no such divergent behavior exists here.
##
## Stage 1 scope only: bare physics + wheel raycasts + visual mesh, driven
## directly by raw Input reads (no enter/exit possession system exists yet
## -- that's Stage 4). No camera, no drift-specific polish beyond the grip
## mechanic itself, no collision damage/damping beyond RigidBody3D's own
## built-in solver -- see the plan's own staged verification sequence.

const MustangVisualScene := preload("res://vehicles/models/mustang/mustang.glb")

# Real-world Mustang length, nose to tail (4.8m), scaled up by 1.3x -- the
# GLB's own raw units aren't meters (confirmed during Stage 0's asset prep),
# so this normalizes whatever scale the source mesh came in at, same
# runtime-measured-AABB trick weapons/pistol.gd's own _build_visual()
# already uses for its real Beretta OBJ. The 1.3x factor is a pure gameplay-
# feel bump (real-world-accurate size looked a little small standing next
# to the player) -- same "true scale is just a starting point" reasoning
# weapons/pistol.gd's own MODEL_SCALE comment already explains.
const TARGET_LENGTH_METERS := 4.8 * 1.3

@export var visual_scene: PackedScene = MustangVisualScene

# --- Tunables (spec's own naming) -- Stage 2 hand-tunes these against real
# ground via /spawn mustang; these are just reasonable starting points, not
# measured against anything yet. ---
@export var maxSpeed := 40.0
@export var acceleration := 20.0
@export var brakingForce := 35.0
@export var reverseSpeed := 14.0
@export var dragCoefficient := 0.02
@export var rollingResistance := 2.5
@export var turnRate := 32.0 # degrees, max wheel angle
@export var turnSpeed := 200.0 # degrees/sec, how fast the wheel angle itself moves
@export var gripFront := 0.95
@export var gripRear := 0.88
@export var handbrakeGripRear := 0.12
@export var suspensionStiffness := 6.0 # lerp rate for cosmetic lean only

# --- Implementation-level constants (not exposed -- these shape HOW the
# tunables above get applied, not what "feels right," so there's no reason
# for Stage 2 hand-tuning to touch them). ---
const REAR_GRIP_WEIGHT := 0.95 # how much rear vs front grip weighs into one effective grip scalar (single rigid body, no separate per-wheel simulation) -- weighted heavily toward rear so the handbrake's own low handbrakeGripRear actually dominates instead of getting diluted back up by gripFront staying high, which otherwise defeats the whole drift mechanic
const GRIP_BLEND_SPEED := 6.0 # exponential blend rate _effective_grip chases its target at -- this ramp IS the "recovery isn't instant" behavior
const LATERAL_CANCEL_RATE := 8.0 # converts the 0..1 grip amount into a per-second lateral-velocity cancellation rate -- see _apply_grip's own comment
const DRIFT_GRIP_RECOVERY_FRACTION := 0.85 # _effective_grip must recover to this fraction of gripRear before is_drifting clears
const DRIFT_MIN_SPEED := 3.0
const NORMAL_ANGULAR_LERP_SPEED := 10.0
const DRIFT_ANGULAR_LERP_SPEED := 2.0
const DRIFT_TURN_RATE_BOOST := 1.15
const HIGH_SPEED_STEER_REDUCTION := 0.6
const MIN_STEER_FRACTION := 0.35
const MIN_POWER_FRACTION := 0.05
const ANGULAR_RISE_POINT := 0.35 # speed_fraction where angular response peaks
const ANGULAR_TOP_SPEED_REDUCTION := 0.3 # how much angular response falls off again by top speed
# Scales the (small, radian) wheel-steer angle into a meaningful
# angular_velocity.y. Sign verified empirically (not just derived) by
# tests/test_car_physics_check.gd's own steering-direction check -- see its
# own comment on why Godot's rotation-direction convention isn't safe to
# assume.
const ANGULAR_GAIN := 3.0
const ANGULAR_Y_SIGN := -1.0

const REAR_BIAS_FRACTION := 0.12 # fraction of body length the center of mass shifts toward the rear (+local Z, since forward is -Z -- see _spawn_weapon's own convention throughout this codebase)
const WHEEL_TRACK_FRACTION := 0.85 # wheel raycasts sit slightly inboard of the full measured body width
const WHEEL_BASE_FRACTION := 0.75 # ...and inboard of the full nose-to-tail length
const WHEEL_RAY_LENGTH := 0.9
const RAY_START_CLEARANCE := 0.2 # see _build_wheel_rays' own comment
const MIN_GROUNDED_RAYS := 2 # of 4 -- fewer than this suppresses engine force/grip/steering-driven rotation entirely

const LEAN_PITCH_MAX := 0.07 # radians, cosmetic-only VisualRoot pitch under throttle
const LEAN_ROLL_MAX := 0.1 # radians, cosmetic-only VisualRoot roll under lateral slide
const LEAN_ROLL_PER_MPS := 0.02

# --- Seats / enter-exit (pulled forward from Stage 4 -- see _begin_enter's
# own comment for why). ---
const INTERACT_RANGE := 4.0 # bigger than a handheld weapon's 2.5m (pistol.gd) -- a car is a much bigger target
# Arbitrary but consistent left/right convention -- nothing in the source
# model distinguishes a modeled steering wheel side, so DriverSeat is just
# always the LEFT (-X) seat, PassengerSeat always the RIGHT (+X) one. Easy
# to flip (swap the two position signs in _build_seats) if that ever reads
# backwards once actually seen in-game.
# A real reported bug: the first-pass Y/Z guesses below put the player up
# on the HOOD near the windshield, not inside the cabin -- the character's
# own local origin sits at roughly foot/floor level (same convention
# standing normally already uses), so the seat needs to sit down near the
# cabin FLOOR, not just "a little below the car's own vertical center."
# These are still just a starting point -- since _build_seats only ever
# runs once in _ready() (nothing re-drives DriverSeat/PassengerSeat's own
# Transform every frame, unlike a held weapon's grip pose), they can be
# dragged directly via the Debugger panel's Remote tab while the game is
# running (.../Mustang/DriverSeat) and read back with the dev console's own
# /car pose -- see ui/dev_console.gd's own _car_command.
const SEAT_X_FRACTION := 0.32 # fraction of half-width, each side of center
const SEAT_Y_FRACTION := -0.32 # fraction of body height below center -- down near the cabin floor, not the car's own vertical middle
const SEAT_Z_FRACTION := -0.04 # fraction of half-length forward of center -- front seats, not the trunk
const EXIT_SIDE_MARGIN := 1.0 # extra clearance beyond the car's own half-width when stepping out

var _current_wheel_angle := 0.0
var _effective_grip := 0.0
var _is_drifting := false
var _is_grounded := false
# The player currently in DriverSeat -- ONLY this one's input gets read (see
# _integrate_forces). null whenever nobody's driving, including while a
# seat-transition animation is still playing (see _transitioning).
var _driver: CharacterBody3D = null
# The player currently in PassengerSeat -- rides along, no control at all.
var _passenger: CharacterBody3D = null
var _nearby_player: CharacterBody3D = null
# True while ANY seat's enter/exit animation is playing -- blocks a second
# interact press (on either seat) and freezes driving input, matching how a
# real car door/seatbelt sequence isn't instant either. One shared flag for
# the whole car (not per-seat) -- simplest correct thing for singleplayer,
# where only one local player can ever be pressing E at a time; a genuinely
# simultaneous two-player entry is a multiplayer concern entirely out of
# scope for this pass (see the approved vehicle plan's own "networking
# deferred" note).
var _transitioning := false
# Saved collision_layer/mask per occupant (see character.gd's own
# _ragdoll_player_collision_layer/_mask for the exact same save/restore
# pattern) -- keyed by player since the driver AND passenger can both be
# mid-occupancy at once, unlike the ragdoll's own single-occupant case.
var _saved_collision: Dictionary = {}
var _body_size := Vector3.ONE

@onready var _visual_root: Node3D = $VisualRoot
@onready var _collision_shape: CollisionShape3D = $CollisionShape3D
@onready var _wheel_ray_fl: RayCast3D = $WheelRayFL
@onready var _wheel_ray_fr: RayCast3D = $WheelRayFR
@onready var _wheel_ray_rl: RayCast3D = $WheelRayRL
@onready var _wheel_ray_rr: RayCast3D = $WheelRayRR
@onready var _interact_area: Area3D = $InteractArea
@onready var _driver_seat: Node3D = $DriverSeat
@onready var _passenger_seat: Node3D = $PassengerSeat
var _wheel_rays: Array[RayCast3D] = []

func _ready() -> void:
	_wheel_rays = [_wheel_ray_fl, _wheel_ray_fr, _wheel_ray_rl, _wheel_ray_rr]
	_build_visual()
	_build_collision()
	_build_wheel_rays()
	_build_seats()
	_build_interact_area()

	can_sleep = false
	contact_monitor = true
	max_contacts_reported = 8
	continuous_cd = true
	collision_layer = 2
	collision_mask = 1 | 2 | 4 # world + other vehicles + characters -- see the approved plan's own corrected layer note (layer 8 is ragdoll, not free)
	center_of_mass_mode = RigidBody3D.CENTER_OF_MASS_MODE_CUSTOM
	center_of_mass = Vector3(0.0, 0.0, _body_size.z * REAR_BIAS_FRACTION)

	var mat := PhysicsMaterial.new()
	mat.bounce = 0.2
	mat.friction = 0.6
	physics_material_override = mat

	_effective_grip = gripRear

func _integrate_forces(state: PhysicsDirectBodyState3D) -> void:
	var basis := state.transform.basis
	var forward_flat := Vector3(-basis.z.x, 0.0, -basis.z.z).normalized()
	var right_flat := Vector3(basis.x.x, 0.0, basis.x.z).normalized()
	var vel_flat := Vector3(state.linear_velocity.x, 0.0, state.linear_velocity.z)
	var forward_speed := vel_flat.dot(forward_flat)
	var lateral_speed := vel_flat.dot(right_flat)
	var speed_fraction: float = clampf(absf(forward_speed) / maxSpeed, 0.0, 1.0)

	_update_grounded()

	var steer_input := 0.0
	var throttle_input := 0.0
	var handbrake_input := false
	# Only the seated DRIVER's input ever drives the car, and only once
	# they're actually fully seated (not still mid-animation) -- see
	# _complete_enter/_begin_exit for when _driver/_transitioning actually
	# flip. Before this, any spawned car would read raw Input unconditionally
	# (a Stage 1 stopgap so /spawn mustang was drivable by hand at all before
	# a possession system existed) -- a real reported bug ("works like an RC
	# car"), fixed by gating on actual possession like every other
	# interactable in this project already does.
	if _driver != null and not _transitioning:
		steer_input = Input.get_axis("move_left", "move_right")
		throttle_input = Input.get_axis("move_back", "move_forward")
		handbrake_input = Input.is_action_pressed("jump")

	# One-frame-stale _effective_grip is fine here (imperceptible) and lets
	# is_drifting be computed once, up front, instead of needing _apply_grip
	# to run before steering/angular can use it.
	var recovered: bool = _effective_grip >= gripRear * DRIFT_GRIP_RECOVERY_FRACTION
	_is_drifting = handbrake_input or (not recovered and absf(forward_speed) > DRIFT_MIN_SPEED)

	_update_steering(steer_input, speed_fraction, state.step)
	_update_driver_hands()

	if _is_grounded:
		_apply_engine_force(state, throttle_input, forward_flat, forward_speed)
		_apply_drag(state, vel_flat)
		_apply_grip(state, right_flat, lateral_speed, handbrake_input, state.step)
		_apply_angular(state, speed_fraction)

	_update_visual_lean(throttle_input, lateral_speed, state.step)

func _update_grounded() -> void:
	var grounded_count := 0
	for ray in _wheel_rays:
		ray.force_raycast_update()
		if ray.is_colliding():
			grounded_count += 1
	_is_grounded = grounded_count >= MIN_GROUNDED_RAYS

func _update_steering(steer_input: float, speed_fraction: float, delta: float) -> void:
	var steer_reduction: float = maxf(MIN_STEER_FRACTION, 1.0 - speed_fraction * HIGH_SPEED_STEER_REDUCTION)
	var effective_turn_rate: float = turnRate * (DRIFT_TURN_RATE_BOOST if _is_drifting else 1.0)
	var target_angle: float = steer_input * deg_to_rad(effective_turn_rate) * steer_reduction
	_current_wheel_angle = move_toward(_current_wheel_angle, target_angle, deg_to_rad(turnSpeed) * delta)

## Drives character.gd's own set_driving_wheel_angle() every tick so the
## seated driver's hands visibly turn with the wheel (a real, explicit ask
## -- "his arms move on the steering wheel as you turn like he is actually
## driving") -- max_angle uses the BASE turnRate (not the drift-boosted
## effective_turn_rate _update_steering computes) so the hand pose always
## maps against the same fixed range regardless of whether a drift happens
## to be boosting the wheel's own mechanical limit at this exact instant.
func _update_driver_hands() -> void:
	if _driver == null or _transitioning:
		return
	var character: Node = _driver.get_node_or_null("Character")
	if character != null and character.has_method("set_driving_wheel_angle"):
		character.set_driving_wheel_angle(_current_wheel_angle, deg_to_rad(turnRate))

func _apply_engine_force(state: PhysicsDirectBodyState3D, throttle_input: float, forward_flat: Vector3, forward_speed: float) -> void:
	if absf(throttle_input) < 0.01:
		return
	if throttle_input > 0.0:
		if forward_speed < -0.5:
			state.apply_central_force(forward_flat * brakingForce * mass)
		else:
			var speed_fraction: float = clampf(forward_speed / maxSpeed, 0.0, 1.0)
			var power_curve: float = maxf(MIN_POWER_FRACTION, 1.0 - speed_fraction)
			state.apply_central_force(forward_flat * throttle_input * acceleration * power_curve * mass)
	else:
		if forward_speed > 0.5:
			state.apply_central_force(-forward_flat * brakingForce * mass)
		else:
			var speed_fraction: float = clampf(-forward_speed / reverseSpeed, 0.0, 1.0)
			var power_curve: float = maxf(MIN_POWER_FRACTION, 1.0 - speed_fraction)
			state.apply_central_force(forward_flat * throttle_input * acceleration * power_curve * mass)

func _apply_drag(state: PhysicsDirectBodyState3D, vel_flat: Vector3) -> void:
	var speed := vel_flat.length()
	if speed < 0.01:
		return
	var drag_force: Vector3 = -vel_flat.normalized() * (dragCoefficient * speed * speed + rollingResistance)
	state.apply_central_force(drag_force * mass)

## _effective_grip (0..1) is a GRIP AMOUNT, not a fraction to instantly wipe
## off the lateral velocity every single tick -- that would make the actual
## cancellation rate scale with the physics tick rate instead of real time
## (and at 60 ticks/sec, even a modest grip value compounds to "erases a
## slide in a couple of frames" regardless of how low handbrakeGripRear is).
## LATERAL_CANCEL_RATE converts it into a per-second cancellation rate
## instead, scaled by delta like everything else in here -- this is what
## actually makes normal grip snap a slide back in a fraction of a second
## while a low handbrake grip lets it persist for a real, feelable drift.
func _apply_grip(state: PhysicsDirectBodyState3D, right_flat: Vector3, lateral_speed: float, handbrake_input: bool, delta: float) -> void:
	var rear_grip: float = handbrakeGripRear if handbrake_input else gripRear
	var target_grip: float = lerpf(gripFront, rear_grip, REAR_GRIP_WEIGHT)
	_effective_grip = lerpf(_effective_grip, target_grip, clampf(GRIP_BLEND_SPEED * delta, 0.0, 1.0))
	var cancel_fraction: float = clampf(_effective_grip * LATERAL_CANCEL_RATE * delta, 0.0, 1.0)
	state.linear_velocity -= right_flat * lateral_speed * cancel_fraction

func _apply_angular(state: PhysicsDirectBodyState3D, speed_fraction: float) -> void:
	var response_curve := _angular_response_curve(speed_fraction)
	var target_angular_y: float = _current_wheel_angle * ANGULAR_Y_SIGN * ANGULAR_GAIN * response_curve
	var lerp_speed: float = DRIFT_ANGULAR_LERP_SPEED if _is_drifting else NORMAL_ANGULAR_LERP_SPEED
	var av := state.angular_velocity
	av.y = lerpf(av.y, target_angular_y, clampf(lerp_speed * state.step, 0.0, 1.0))
	state.angular_velocity = av

## No rotation at zero speed, peaks around ANGULAR_RISE_POINT, slightly
## reduced again by top speed -- matches the plan's own "no rotation at 0
## speed, max around mid-speed, slightly reduced at top speed" spec.
func _angular_response_curve(speed_fraction: float) -> float:
	if speed_fraction <= 0.001:
		return 0.0
	var rise: float = clampf(speed_fraction / ANGULAR_RISE_POINT, 0.0, 1.0)
	var fall: float = 1.0 - clampf((speed_fraction - ANGULAR_RISE_POINT) / (1.0 - ANGULAR_RISE_POINT), 0.0, 1.0) * ANGULAR_TOP_SPEED_REDUCTION
	return rise * fall

func _update_visual_lean(throttle_input: float, lateral_speed: float, delta: float) -> void:
	var target_pitch: float = -throttle_input * LEAN_PITCH_MAX
	var target_roll: float = clampf(-lateral_speed * LEAN_ROLL_PER_MPS, -LEAN_ROLL_MAX, LEAN_ROLL_MAX)
	var rot := _visual_root.rotation
	var t: float = clampf(suspensionStiffness * delta, 0.0, 1.0)
	rot.x = lerpf(rot.x, target_pitch, t)
	rot.z = lerpf(rot.z, target_roll, t)
	_visual_root.rotation = rot

## Instances the real Mustang GLB (see MustangVisualScene's own comment) and
## normalizes its scale so its own largest HORIZONTAL dimension (nose-to-
## tail or door-to-door, whichever the raw model's own axes happen to line
## up with) matches TARGET_LENGTH_METERS -- same self-measuring approach
## weapons/pistol.gd's own _build_visual() uses for its real Beretta OBJ,
## necessary here too since Stage 0 confirmed the source mesh's raw units
## aren't meters. Re-centers on the mesh's own AABB center so this node's
## local origin (where the collision box/wheel rays/center of mass all get
## built around, below) already sits at the car's true geometric middle.
func _build_visual() -> void:
	var visual: Node3D = visual_scene.instantiate()
	_visual_root.add_child(visual)
	var aabb := _compute_combined_aabb(visual)
	var largest_horizontal: float = maxf(aabb.size.x, aabb.size.z)
	var normalize_scale: float = TARGET_LENGTH_METERS / largest_horizontal if largest_horizontal > 0.0001 else 1.0
	visual.scale = Vector3.ONE * normalize_scale
	visual.position = -aabb.get_center() * normalize_scale
	_body_size = aabb.size * normalize_scale

## GLB scenes carry the mesh split across several MeshInstance3D nodes (not
## one ArrayMesh the way weapons/pistol.gd's plain OBJ import is), so this
## walks the whole instanced subtree and merges each mesh's own AABB
## (transformed into `root`'s local space) into one combined bounding box.
func _compute_combined_aabb(root: Node3D) -> AABB:
	var combined := AABB()
	var found := false
	var stack: Array = [[root, Transform3D.IDENTITY]]
	while not stack.is_empty():
		var entry: Array = stack.pop_back()
		var node: Node = entry[0]
		var xform: Transform3D = entry[1]
		if node is MeshInstance3D and node.mesh != null:
			var world_aabb: AABB = xform * node.mesh.get_aabb()
			if not found:
				combined = world_aabb
				found = true
			else:
				combined = combined.merge(world_aabb)
		for child in node.get_children():
			var child_xform: Transform3D = xform
			if child is Node3D:
				child_xform = xform * child.transform
			stack.append([child, child_xform])
	return combined

## Bounding-box collider sized from the visual mesh's own measured (and
## already-normalized) extents, per the plan's explicit "bounding box/
## capsule collider" call -- not the decimated visual mesh itself.
func _build_collision() -> void:
	var box := BoxShape3D.new()
	box.size = _body_size
	_collision_shape.shape = box

## Straight-down raycasts near the four corners of the measured body, used
## for exactly two things (see the plan's own wheel-raycast section): (1)
## airborne detection -- fewer than MIN_GROUNDED_RAYS of 4 hitting suppresses
## engine force/grip/steering-driven rotation, and (2) nothing cosmetic yet
## in Stage 1 (ground-normal visual alignment is a later polish pass, not
## required for a driveable bare controller).
func _build_wheel_rays() -> void:
	var half_x: float = _body_size.x * 0.5 * WHEEL_TRACK_FRACTION
	var half_z: float = _body_size.z * 0.5 * WHEEL_BASE_FRACTION
	# Starting the ray exactly at the car's geometric underside (-half_y)
	# means that the instant the car is actually resting on the ground --
	# the normal, common case, not an edge case -- the ray's own start point
	# sits AT (or a hair inside) the ground surface. A ray that starts
	# already inside a shape doesn't report a hit against that shape at all
	# (confirmed directly: wheel rays read is_colliding()==false forever on
	# a car resting motionless on flat ground). Starting RAY_START_CLEARANCE
	# above the true underside keeps the start point comfortably outside the
	# ground even at rest, then the ray travels back down through the
	# underside and beyond it by WHEEL_RAY_LENGTH to actually find it.
	var wheel_y: float = -_body_size.y * 0.5 + RAY_START_CLEARANCE
	# Forward is -Z (see _spawn_weapon's own convention) -- FL/FR sit toward
	# the nose (-Z), RL/RR toward the tail (+Z).
	_wheel_ray_fl.position = Vector3(-half_x, wheel_y, -half_z)
	_wheel_ray_fr.position = Vector3(half_x, wheel_y, -half_z)
	_wheel_ray_rl.position = Vector3(-half_x, wheel_y, half_z)
	_wheel_ray_rr.position = Vector3(half_x, wheel_y, half_z)
	for ray in _wheel_rays:
		ray.target_position = Vector3(0.0, -(RAY_START_CLEARANCE + WHEEL_RAY_LENGTH), 0.0)
		ray.collision_mask = 1
		ray.enabled = true

## Positions DriverSeat/PassengerSeat from the measured body -- see
## SEAT_X/Y/Z_FRACTION's own comments for the (rough, first-pass) placement
## reasoning. Purely a local Transform each seated player gets reparented
## onto (see _complete_enter) -- no visible geometry of its own.
func _build_seats() -> void:
	var seat_x: float = _body_size.x * 0.5 * SEAT_X_FRACTION
	var seat_y: float = _body_size.y * SEAT_Y_FRACTION
	var seat_z: float = _body_size.z * 0.5 * SEAT_Z_FRACTION
	_driver_seat.position = Vector3(-seat_x, seat_y, seat_z)
	_passenger_seat.position = Vector3(seat_x, seat_y, seat_z)

## Same InteractArea pattern every holdable prop already uses (see
## weapons/pistol.gd's own InteractArea comment on collision_layer=0/
## collision_mask=4), just a bigger sphere (INTERACT_RANGE) since a car is a
## much bigger, unmissable target -- unlike picking up a gun, this doesn't
## also require a look-dot-threshold check (see _process), just proximity.
func _build_interact_area() -> void:
	_interact_area.collision_layer = 0
	_interact_area.collision_mask = 4
	var shape := CollisionShape3D.new()
	var sphere := SphereShape3D.new()
	sphere.radius = INTERACT_RANGE
	shape.shape = sphere
	_interact_area.add_child(shape)
	_interact_area.body_entered.connect(_on_interact_area_body_entered)
	_interact_area.body_exited.connect(_on_interact_area_body_exited)

func _on_interact_area_body_entered(body: Node3D) -> void:
	if body is CharacterBody3D and body.has_method("respawn"):
		_nearby_player = body

func _on_interact_area_body_exited(body: Node3D) -> void:
	if body == _nearby_player:
		_nearby_player = null

## Watches "interact" (E) every rendered frame -- proximity-based only (see
## _build_interact_area's own comment), no look-dot-threshold the way
## picking up a weapon needs. Handles BOTH directions: a nearby player not
## already seated anywhere enters whichever of this car's own two seats is
## closest to THEM (the literal ask -- "sitting in the seat of the car he is
## pressing E closest to"), while pressing E with either seat of THIS car
## already occupied exits it instead. _transitioning blocks a second press
## from stacking on top of an animation already in progress either way.
##
## Checking _driver/_passenger first (rather than _nearby_player) for the
## exit case isn't just an ordering choice -- it's the only option: a seated
## occupant's own collision is zeroed the instant they're seated (see
## _complete_enter), so they can never trigger _on_interact_area_body_
## entered again and can never be _nearby_player while seated. This also
## means Input.is_action_just_pressed("interact") isn't attributed to any
## particular player here -- fine for this singleplayer-scoped pass (see
## the approved vehicle plan's own "networking deferred" note), where
## there's only ever one real local player who could be pressing it.
func _process(_delta: float) -> void:
	if _transitioning:
		return
	if not Input.is_action_just_pressed("interact"):
		return
	if _driver != null:
		_begin_exit(_driver, _driver_seat)
		return
	if _passenger != null:
		_begin_exit(_passenger, _passenger_seat)
		return
	if _nearby_player == null:
		return
	var seat := _nearest_free_seat(_nearby_player)
	if seat != null:
		_begin_enter(_nearby_player, seat)

## Whichever of DriverSeat/PassengerSeat is closer to player AND not already
## occupied -- null if both are taken. Distance compared in world space
## (both seats are already positioned via _build_seats), matching the
## literal ask ("the seat... he is pressing E closest to").
func _nearest_free_seat(player: CharacterBody3D) -> Node3D:
	var driver_free: bool = _driver == null
	var passenger_free: bool = _passenger == null
	if not driver_free and not passenger_free:
		return null
	if driver_free and not passenger_free:
		return _driver_seat
	if passenger_free and not driver_free:
		return _passenger_seat
	var to_driver: float = player.global_position.distance_to(_driver_seat.global_position)
	var to_passenger: float = player.global_position.distance_to(_passenger_seat.global_position)
	return _driver_seat if to_driver <= to_passenger else _passenger_seat

## Starts the walk-up-and-sit animation (see character.gd's own
## play_enter_car()) BEFORE any actual possession handoff -- the player is
## still standing in their own original position/parent while this plays,
## only reparented into the seat once it finishes (see _complete_enter).
## Pulled forward from the plan's own Stage 4 (originally a plain instant
## teleport-style enter with no animation at all) after the user explicitly
## asked for the real Entering Car.fbx/Exiting Car.fbx clips instead.
func _begin_enter(player: CharacterBody3D, seat: Node3D) -> void:
	var character: Node = player.get_node_or_null("Character")
	if character == null or not character.has_method("play_enter_car"):
		return
	_transitioning = true
	if player.has_method("begin_car_transition"):
		player.begin_car_transition(self)
	# Face the same direction the car itself is facing -- a reasonable
	# "standing alongside the car, about to get in" orientation given the
	# animation's own baked turn (see scratch_mixamo_retarget.gd's own
	# include_root_rotation) then carries it the rest of the way into a
	# seated facing, without needing an exact per-door angle computed here.
	player.rotation.y = rotation.y
	character.play_enter_car()
	character.anim_player.animation_finished.connect(_on_enter_finished.bind(player, seat), CONNECT_ONE_SHOT)

func _on_enter_finished(anim_name: StringName, player: CharacterBody3D, seat: Node3D) -> void:
	if anim_name != "entering_car":
		return
	_complete_enter(player, seat)

## The actual possession handoff -- exact same save-collision/reparent
## pattern character.gd's own ragdoll() already uses for the player's body
## (see _ragdoll_player_collision_layer/_mask), just onto a car seat instead
## of leaving the player where they stood. Character stays VISIBLE the
## whole drive (a real, explicit ask -- "show him sitting in the drivers
## seat") -- entering_car (LOOP_NONE) is already frozen on its own
## seated-looking last frame by this point (see character.gd's own
## play_enter_car comment), and resetting position/rotation to zero here
## is the "teleport him into place if needed" the user also explicitly
## asked for -- a hard snap to the seat's own exact local origin regardless
## of exactly where the animation's own root motion happened to end.
func _complete_enter(player: CharacterBody3D, seat: Node3D) -> void:
	_saved_collision[player] = [player.collision_layer, player.collision_mask]
	player.collision_layer = 0
	player.collision_mask = 0
	player.get_parent().remove_child(player)
	seat.add_child(player)
	player.position = Vector3.ZERO
	player.rotation = Vector3.ZERO
	if seat == _driver_seat:
		_driver = player
	else:
		_passenger = player
	_transitioning = false

## Mirrors _begin_enter for the reverse direction -- the player stays
## parented under `seat` (so they visibly ride along with the car, still
## moving if it's still moving) for the WHOLE clip, only reparented back
## into the world at the very end (see _complete_exit) once they're actually
## standing again. _driver/_passenger themselves ALSO stay set until then
## (not cleared here) -- they're still physically in the seat throughout the
## animation, matching _complete_enter's own symmetric "assign on
## completion, not on start." Actual driving input is already blocked the
## instant _transitioning flips true regardless (see _integrate_forces'
## own gate), so this is purely about what the state reflects, not a
## separate gameplay effect.
func _begin_exit(player: CharacterBody3D, seat: Node3D) -> void:
	var character: Node = player.get_node_or_null("Character")
	if character == null or not character.has_method("play_exit_car"):
		return
	_transitioning = true
	character.play_exit_car()
	character.anim_player.animation_finished.connect(_on_exit_finished.bind(player, seat), CONNECT_ONE_SHOT)

func _on_exit_finished(anim_name: StringName, player: CharacterBody3D, seat: Node3D) -> void:
	if anim_name != "exiting_car":
		return
	_complete_exit(player, seat)

## Steps the player out to the side of whichever seat they were in (driver
## exits left, passenger exits right -- matching _build_seats' own
## convention), at the car's own current position/facing so this still
## reads correctly even if the car was mid-drive when they got out.
func _complete_exit(player: CharacterBody3D, seat: Node3D) -> void:
	if seat == _driver_seat:
		_driver = null
	else:
		_passenger = null
	var side_sign: float = -1.0 if seat == _driver_seat else 1.0
	var exit_offset: Vector3 = global_transform.basis.x * side_sign * (_body_size.x * 0.5 + EXIT_SIDE_MARGIN)
	var exit_pos: Vector3 = seat.global_position + exit_offset
	seat.remove_child(player)
	get_parent().add_child(player)
	player.global_position = exit_pos
	# A real reported bug: naively copying just rotation.y (the car's own
	# raw Euler Y component) onto the player left them standing at a
	# visible tilt after exiting. The car is a RigidBody3D that's very
	# rarely PERFECTLY level (small pitch/roll from ordinary driving
	# physics, uneven wheel contact, or a bump right as E was pressed) --
	# once its rotation isn't pure yaw, its own .rotation.x/.rotation.z are
	# nonzero too, and grabbing only .y while the player's own x/z get
	# reset to 0 by the reparent produces an inconsistent Euler triple that
	# doesn't correspond to "upright, facing the car's own direction" at
	# all. Deriving yaw from the car's actual FLATTENED forward vector
	# instead (ignoring whatever pitch/roll it currently has) and building
	# a clean (0, yaw, 0) rotation from that guarantees the player always
	# ends up perfectly upright.
	var forward_flat := Vector3(-global_transform.basis.z.x, 0.0, -global_transform.basis.z.z)
	var yaw: float = Vector3.FORWARD.signed_angle_to(forward_flat, Vector3.UP) if forward_flat.length_squared() > 0.0001 else rotation.y
	player.rotation = Vector3(0.0, yaw, 0.0)
	player.velocity = Vector3.ZERO
	var saved: Array = _saved_collision.get(player, [4, 1])
	player.collision_layer = saved[0]
	player.collision_mask = saved[1]
	_saved_collision.erase(player)
	var character: Node = player.get_node_or_null("Character")
	if character != null and character.has_method("clear_driving_wheel_pose"):
		character.clear_driving_wheel_pose()
	if player.has_method("end_car_transition"):
		player.end_car_transition()
	_transitioning = false
