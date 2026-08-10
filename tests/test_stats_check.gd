extends Node3D
## Regression check for the health/stamina/fall-damage/death/respawn system
## (see player.gd's own take_damage/_die/respawn and character.gd's
## ragdoll/reset_ragdoll): stamina must drain while actually sprinting and
## regenerate otherwise, gating sprint speed once exhausted; a long fall
## must deal proportional (Minecraft-style) damage and a long enough one
## must be lethal; death must trigger the character's ragdoll; and respawn
## must fully reset health, stamina, position, and the ragdoll pose. Runs
## headless: pure physics + state-machine checks, no rendering needed.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

var _player: CharacterBody3D
var _character: Node3D
var _frame := 0
var _phase := 0
var _ok := true
var _phase_start_stamina := 0.0
var _fall_start_health := 0.0

func _ready() -> void:
	var ground := StaticBody3D.new()
	ground.collision_layer = 1
	var gcol := CollisionShape3D.new()
	var gshape := BoxShape3D.new()
	gshape.size = Vector3(200, 1.0, 200)
	gcol.shape = gshape
	gcol.position = Vector3(0, -0.5, 0)
	ground.add_child(gcol)
	add_child(ground)

	var spawn := Marker3D.new()
	spawn.name = "SpawnPoint"
	spawn.position = Vector3(50, 0, 0)
	add_child(spawn)

	_player = PlayerScene.instantiate()
	add_child(_player)
	_character = CharacterScene.instantiate()
	_character.set_paintable(false)
	_player.add_child(_character)

func _physics_process(_delta: float) -> void:
	_frame += 1
	match _phase:
		0: # let everything settle onto the ground for a few frames
			if _frame >= 5:
				_phase_start_stamina = _player.stamina
				Input.action_press("move_forward")
				Input.action_press("sprint")
				_phase = 1
				_frame = 0
		1: # sprint for 1 second (60 frames) -- stamina should drain
			if _frame >= 60:
				var drained: float = _phase_start_stamina - _player.stamina
				var expected: float = _player.STAMINA_DRAIN_PER_SEC * (60.0 / 60.0)
				if absf(drained - expected) < 3.0 and _player.stamina < _phase_start_stamina:
					print("PASS: stamina drained while sprinting (~", drained, ", expected ~", expected, ")")
				else:
					print("FAIL: stamina drain mismatch (drained ", drained, ", expected ~", expected, ")")
					_ok = false
				Input.action_release("sprint")
				Input.action_release("move_forward")
				_phase_start_stamina = _player.stamina
				_phase = 2
				_frame = 0
		2: # stand still for 1 second -- stamina should regenerate
			if _frame >= 60:
				if _player.stamina > _phase_start_stamina:
					print("PASS: stamina regenerated while not sprinting (", _phase_start_stamina, " -> ", _player.stamina, ")")
				else:
					print("FAIL: stamina did not regenerate (", _phase_start_stamina, " -> ", _player.stamina, ")")
					_ok = false
				_phase = 3
				_frame = 0
		3: # drain stamina all the way to 0 by holding sprint for a long time
			Input.action_press("move_forward")
			Input.action_press("sprint")
			if _player.stamina <= 0.0 or _frame >= 600:
				if _player.stamina <= 0.0 and _player._stamina_exhausted:
					print("PASS: stamina hit 0 and exhaustion flag set")
				else:
					print("FAIL: stamina never hit 0 / exhaustion flag not set (stamina=", _player.stamina, ")")
					_ok = false
				# While exhausted, actually-applied speed should be walking
				# speed, not sprint speed, even though sprint is still held.
				var horiz_speed := Vector2(_player.velocity.x, _player.velocity.z).length()
				if horiz_speed < _player.SPRINT_SPEED - 0.5:
					print("PASS: exhausted stamina caps speed below sprint speed (", horiz_speed, ")")
				else:
					print("FAIL: still moving at sprint speed while exhausted (", horiz_speed, ")")
					_ok = false
				Input.action_release("sprint")
				Input.action_release("move_forward")
				_player.velocity = Vector3.ZERO
				_phase = 4
				_frame = 0
		4: # teleport up high and let gravity carry it into a lethal fall
			if _frame == 1:
				_player.velocity = Vector3.ZERO
				_player.global_position = Vector3(0, 50, 0)
				_fall_start_health = _player.health
			if _frame >= 300 or _player.is_dead:
				_phase = 5
				_frame = 0
		5: # verify the fall actually hurt (or killed) the player, and that
			# a lethal fall triggered ragdoll + the died signal's effects.
			# Compares health directly (not "started full and still alive",
			# which silently passed for the wrong reason as long as the
			# configured fall height happened to be lethal -- a real
			# survivable-but-damaging fall would have a health drop with
			# is_dead still false, which that check couldn't tell apart
			# from no damage at all).
			if _player.health >= _fall_start_health and not _player.is_dead:
				print("FAIL: falling from 50m dealt no damage at all")
				_ok = false
			else:
				print("PASS: falling from 50m dealt damage (health ", _fall_start_health, " -> ", _player.health, ", is_dead=", _player.is_dead, ")")
			if not _player.is_dead:
				# Force a clean death for the rest of the checks regardless
				# of exactly how much the fall itself dealt.
				_player.take_damage(_player.MAX_HEALTH)
			if _player.is_dead and _character._is_ragdolled:
				print("PASS: death triggered ragdoll (character._is_ragdolled)")
			else:
				print("FAIL: death did not trigger ragdoll")
				_ok = false
			_phase = 6
			_frame = 0
		6: # let the ragdoll tween play out, then respawn
			if _frame >= 40:
				_player.respawn()
				_phase = 7
				_frame = 0
		7:
			if _frame >= 3:
				var spawn_pos: Vector3 = get_node("SpawnPoint").position
				var pos_ok := _player.position.distance_to(spawn_pos) < 0.5
				if not _player.is_dead and _player.health == _player.MAX_HEALTH and _player.stamina == _player.MAX_STAMINA and pos_ok and not _character._is_ragdolled:
					print("PASS: respawn reset health/stamina/position and un-ragdolled")
				else:
					print("FAIL: respawn state wrong (is_dead=", _player.is_dead, " health=", _player.health, " stamina=", _player.stamina, " pos=", _player.position, " ragdolled=", _character._is_ragdolled, ")")
					_ok = false
				_phase = 8
				_frame = 0
		8: # a fall of exactly ONE story (half of SAFE_FALL_DISTANCE, which is
			# defined as two stories -- see player.gd's own comment) must deal
			# NO damage at all, e.g. jumping off the top of a single flight of
			# stairs in one of the generated buildings.
			if _frame == 1:
				_player.velocity = Vector3.ZERO
				_player.global_position = Vector3(0, _player.SAFE_FALL_DISTANCE / 2.0, 0)
				_fall_start_health = _player.health
			if _frame >= 200 or (_player.is_on_floor() and _frame > 5):
				if _player.health >= _fall_start_health:
					print("PASS: a one-story fall (", _player.SAFE_FALL_DISTANCE / 2.0, "m) dealt no damage")
				else:
					print("FAIL: a one-story fall dealt damage it shouldn't have (health ", _fall_start_health, " -> ", _player.health, ")")
					_ok = false
				_phase = 9
				_frame = 0
		9: # a fall just past TWO stories (SAFE_FALL_DISTANCE itself) must
			# start dealing real damage.
			if _frame == 1:
				_player.velocity = Vector3.ZERO
				_player.global_position = Vector3(0, _player.SAFE_FALL_DISTANCE + 1.0, 0)
				_fall_start_health = _player.health
			if _frame >= 200 or (_player.is_on_floor() and _frame > 5):
				if _player.health < _fall_start_health:
					print("PASS: a just-over-two-stories fall (", _player.SAFE_FALL_DISTANCE + 1.0, "m) dealt damage (", _fall_start_health, " -> ", _player.health, ")")
				else:
					print("FAIL: a just-over-two-stories fall dealt no damage (stayed at ", _player.health, ")")
					_ok = false
				_player.respawn()
				_phase = 10
				_frame = 0
		10: # jumping off the roof of the SHORTEST building city_chunk.gd
			# would still call "4 stories" (num_floors = floor(height /
			# story_height), so the low edge of that bracket is exactly 4
			# story-heights up) must be lethal -- a fresh full-health
			# respawn first so this checks the fall alone, not leftover
			# damage from phase 9.
			var story_height: float = _player.SAFE_FALL_DISTANCE / 2.0
			if _frame == 1:
				_player.velocity = Vector3.ZERO
				_player.global_position = Vector3(0, story_height * 4.0, 0)
				_fall_start_health = _player.health
			if _frame >= 300 or _player.is_dead:
				if _player.is_dead:
					print("PASS: jumping off a 4-story building (", story_height * 4.0, "m) is lethal")
				else:
					print("FAIL: a 4-story fall was survivable (health ", _fall_start_health, " -> ", _player.health, ")")
					_ok = false
				print("OVERALL: ", "PASS" if _ok else "FAIL")
				get_tree().quit(0 if _ok else 1)
