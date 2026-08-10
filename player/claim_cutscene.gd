extends Node
## The flourish when a building is claimed: the view pulls back off the
## player and flies one full circle around the building's exterior, then
## hands back. Started by player.gd's own /claim handling (see
## start_claim_cutscene).
##
## Instantiated with .new() and freed when done, same "plain script
## extending a built-in class, no .tscn" shape player/intro_cutscene.gd
## already uses.
##
## USES ITS OWN CAMERA, unlike intro_cutscene.gd which re-poses the player's
## own spring arm. That's forced by what's being framed: the player's camera
## hangs off a pivot attached to the PLAYER, so orbiting it circles the
## player, not the building. Claiming has to circle a building the player is
## standing inside -- a completely different centre -- so a free camera
## placed on a circle each frame is both simpler and exactly right, and it
## leaves the player's own rig untouched to restore afterwards.
##
## FRAMING IS DERIVED, NOT FIXED. Buildings here range from 8m to 96m tall
## (see city_chunk.gd's MIN_HEIGHT/MAX_HEIGHT) with footprints from 9m to
## 18m, so any single hardcoded radius would either clip through a tower or
## leave a low building as a speck. See _frame_radius.

signal finished

## Full turn, once. Slow enough to read as a reveal rather than a spin.
const ORBIT_SECONDS := 6.0
## How far above the building's own centre the camera rides, as an angle.
## A modest downward tilt reads as surveying the building rather than as a
## flat architectural elevation, without going so high it becomes a roof
## shot of a 96m tower.
const ORBIT_ELEVATION_DEGREES := 14.0
## Extra breathing room past the minimum distance that fits the building in
## frame, so it never quite touches the edges.
const FRAME_MARGIN := 1.18
const FRAME_MIN_DISTANCE := 16.0
## Godot's own default 3D camera FOV, and VERTICAL by default
## (keep_aspect defaults to KEEP_HEIGHT), which is what the framing maths
## below treats it as. On a landscape window the horizontal FOV is wider
## still, so fitting vertically fits horizontally for free.
const CAMERA_FOV_DEGREES := 75.0

var player: CharacterBody3D
## Building footprint centre in world space, at ground level.
var building_center := Vector3.ZERO
## Full building extents (x/z footprint, y height) -- straight off the
## indoor trigger's own box shape, so it's the real thing rather than an
## estimate.
var building_size := Vector3.ONE

var _camera: Camera3D
var _elapsed := 0.0
## Distance from the building's bounding-sphere centre (NOT a horizontal
## radius -- see _place_camera).
var _distance := FRAME_MIN_DISTANCE
## Bounding-sphere centre: the building's own middle, which is what the
## camera both orbits and points at.
var _focus := Vector3.ZERO
var _start_angle := 0.0
var _player_camera: Camera3D = null

func begin() -> void:
	_focus = building_center + Vector3(0.0, building_size.y * 0.5, 0.0)
	_distance = _frame_distance()
	# Starts on the side the player is already looking from, so the orbit
	# opens on a continuation of their own view rather than cutting to the
	# far side of the building.
	var from_player := player.global_position - building_center
	_start_angle = atan2(from_player.z, from_player.x) if from_player.length_squared() > 0.001 else 0.0

	_camera = Camera3D.new()
	_camera.fov = CAMERA_FOV_DEGREES
	# Added to the WORLD, not to the player -- a camera parented to a body
	# that is still being simulated would inherit its motion.
	player.get_parent().add_child(_camera)
	_place_camera(0.0)

	_player_camera = player.camera
	_camera.current = true

func _process(delta: float) -> void:
	if _camera == null or not is_instance_valid(_camera):
		return
	_elapsed += delta
	var t: float = clampf(_elapsed / ORBIT_SECONDS, 0.0, 1.0)
	# Smoothstep rather than linear so the circle eases out of and back into
	# rest instead of starting and stopping at full speed.
	_place_camera(smoothstep(0.0, 1.0, t) * TAU)
	if t >= 1.0:
		_finish()

## Puts the camera on its orbit at `angle` radians past the start, always
## pointed back at the building's middle.
##
## The orbit is a circle on a CONE around the focus point, not a circle at
## some fixed world height: the camera keeps a constant distance from the
## bounding-sphere centre while riding ORBIT_ELEVATION_DEGREES above it.
## That's what makes the framing hold -- a fixed height with a fixed
## horizontal radius changes the real camera-to-building distance as the
## building gets taller, which is exactly how the first version cropped the
## top off a 48m tower.
func _place_camera(angle: float) -> void:
	var a := _start_angle + angle
	var elevation := deg_to_rad(ORBIT_ELEVATION_DEGREES)
	var offset := Vector3(cos(a) * cos(elevation), sin(elevation), sin(a) * cos(elevation)) * _distance
	_camera.global_position = _focus + offset
	_camera.look_at(_focus, Vector3.UP)

## How far the camera must sit from the building's centre for all of it to
## fit in frame, solved as a BOUNDING SPHERE rather than per-axis.
##
## A sphere's required distance is r / sin(half_fov) and is the same from
## every direction -- which is the point, because the orbit views the
## building from all sides AND from above. The per-axis version this
## replaced only held for a camera level with the building's middle; the
## moment it rode higher and tilted down, the real angular span grew and the
## top of a tall building fell outside the frustum (seen directly: a 48m
## tower cropped at the roof).
##
## The sphere encloses the full box: half the footprint diagonal across, and
## half the height up.
func _frame_distance() -> float:
	var half_fov := deg_to_rad(CAMERA_FOV_DEGREES) * 0.5
	var half_diagonal := Vector2(building_size.x, building_size.z).length() * 0.5
	var radius := Vector2(half_diagonal, building_size.y * 0.5).length()
	return maxf(FRAME_MIN_DISTANCE, radius / maxf(sin(half_fov), 0.001) * FRAME_MARGIN)

func _finish() -> void:
	# Restored BEFORE the camera is freed -- freeing the current camera with
	# nothing else marked current leaves the viewport with no active camera
	# for a frame, which renders as a black flash.
	if _player_camera != null and is_instance_valid(_player_camera):
		_player_camera.current = true
	if _camera != null and is_instance_valid(_camera):
		_camera.queue_free()
		_camera = null
	finished.emit()

## Cleans up if the cutscene is torn down mid-orbit (the player dying, the
## world being freed) -- without this the free camera would outlive it and
## the player would never get their view back.
func _exit_tree() -> void:
	if _camera != null and is_instance_valid(_camera):
		if _player_camera != null and is_instance_valid(_player_camera):
			_player_camera.current = true
		_camera.queue_free()
		_camera = null
