extends Node
## Regression check for a real reported bug: "world stopped generating
## while driving." city.gd's own _physics_process tracked the player's
## chunk via _player.position (LOCAL, relative to whatever currently
## parents them) instead of _player.global_position -- while walking, the
## player is always a direct child of City so the two are identical, but
## while seated in a car (see vehicles/car.gd's own _complete_enter, which
## reparents the whole Player node onto a seat and resets its LOCAL
## position to Vector3.ZERO) .position stays pinned at that same local
## offset forever regardless of where the car actually drives, so
## _update_chunks() never fires again once you get in and drive away from
## the loaded area. This test reproduces the seated case directly (a plain
## Node3D standing in for a car's own seat, reparented and moved the same
## way car.gd does) without needing the full car/animation machinery, since
## the bug is purely about which position source city.gd reads.

const CityScene := preload("res://city/city.tscn")
const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

var _city: Node3D
var _player: CharacterBody3D
var _seat: Node3D
var _ok := true
var _frame := 0

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	_city = CityScene.instantiate()
	add_child(_city)
	_city.initial_load_ready.connect(_on_initial_load_ready)

func _on_initial_load_ready() -> void:
	_player = PlayerScene.instantiate()
	var character := CharacterScene.instantiate()
	character.set_paintable(false)
	_player.add_child(character)
	_city.add_child(_player)
	_city.set_player(_player)

	# Mimic vehicles/car.gd's own _complete_enter(): gate off the player's
	# own normal movement/gravity (see player.gd's own _seated_car check --
	# without this, the player's own _physics_process would fight this test,
	# trying to fall/move relative to its new parent same as if it had just
	# spawned there on foot), reparent onto a "seat" (a plain Node3D
	# standing in for DriverSeat), and reset its LOCAL position to zero,
	# exactly like a real seat transition does.
	_player.begin_car_transition(self)
	_seat = Node3D.new()
	_city.add_child(_seat)
	_seat.global_position = Vector3(0, 0, 0)
	_player.get_parent().remove_child(_player)
	_seat.add_child(_player)
	_player.position = Vector3.ZERO

	call_deferred("_start_phase_1")

# The SET of chunk coords loaded before the seat moved, not just how many
# there were. A count comparison ("more chunks than before") used to stand
# in for "new chunks loaded" and became wrong once chunk generation was
# spread across frames to fix the streaming hitch: unloading the 27 chunks
# behind you is instant, while loading the ones ahead is throttled, so five
# frames after a 240m teleport the TOTAL has dropped (27 -> 4) even though
# loading is working perfectly. What actually matters is that chunks which
# weren't loaded before now are.
var _initial_loaded_chunks: Array = []
# _physics_process runs from the moment this node enters the tree (Godot
# auto-enables it for any script that defines the callback) -- WAY before
# _on_initial_load_ready ever fires (city's own async initial load takes
# dozens of frames, see city.gd's own INITIAL_LOAD_CHUNKS_PER_FRAME), so
# _frame was already deep into the hundreds by the time the seat actually
# moved on a first attempt at this test, giving _update_chunks() far less
# real time to react than the "5 frames" it looked like. Gating _frame's
# own count on _started (only set true once the seat has actually moved)
# fixes that -- was a bug in the TEST itself, not vehicles/car.gd or
# city.gd, caught by a stray null-access error on _player before setup
# had even run.
var _started := false

func _start_phase_1() -> void:
	_initial_loaded_chunks = _city._loaded_chunks.keys()
	# Chunk (10, 0) -- world x=240 -- is well outside the initial LOAD_RADIUS
	# (4 chunks = 96m) area already generated around the origin, so new
	# chunks are REQUIRED to load if (and only if) city.gd is correctly
	# tracking the player's real world position.
	_seat.global_position = Vector3(240, 0, 0)
	_started = true

func _physics_process(_delta: float) -> void:
	if not _started:
		return
	_frame += 1
	if _frame < 5:
		return
	var expected_chunk := Vector2i(10, 0)
	if _city._last_player_chunk == expected_chunk:
		print("PASS: city correctly tracked the seated player's real (global) position across a chunk boundary")
	else:
		_fail("city didn't track the seated player's position (_last_player_chunk=%s, expected %s)" % [_city._last_player_chunk, expected_chunk])

	var newly_loaded: Array = []
	for coord in _city._loaded_chunks.keys():
		if not _initial_loaded_chunks.has(coord):
			newly_loaded.append(coord)
	if not newly_loaded.is_empty():
		print("PASS: %d chunk(s) that weren't loaded before are loaded around the car's real position" % newly_loaded.size())
	else:
		_fail("no NEW chunks loaded even though the seated player moved 240m away (had %d, now %d)" % [
			_initial_loaded_chunks.size(), _city._loaded_chunks.size(),
		])

	if _city._loaded_chunks.has(expected_chunk):
		print("PASS: the exact chunk under the car's new position is loaded")
	else:
		_fail("the chunk directly under the car's new position was never loaded")

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
