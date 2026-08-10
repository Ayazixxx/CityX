extends Node
## Regression check for the /dance and /breakdance chat emotes: player.gd's
## own _on_chat_message_sent intercepting a literal "/dance"/"/breakdance"
## chat message (case-insensitively, whitespace-tolerant) before it ever
## becomes a speech bubble, and character.gd's own dance()/breakdance()
## (both just forward to the shared _play_emote(), which loops the matching
## clip, cancelled by movement/jumping/combat the same defensive way every
## other one-shot action in this file already is).

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

var _ok := true
var _player: CharacterBody3D
var _character: Node3D

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

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

	_player = PlayerScene.instantiate()
	add_child(_player)
	_character = CharacterScene.instantiate()
	_character.set_paintable(false)
	_player.add_child(_character)

	call_deferred("_run_checks")

func _current() -> String:
	return _character.anim_player.current_animation

func _run_checks() -> void:
	for i in range(3):
		_player._physics_process(1.0 / 60.0)

	# 1. Typing "/dance" plays the emote, not a speech bubble.
	_character._speech_bubble.visible = false
	_character._speech_bubble.text = ""
	_player._on_chat_message_sent("/dance")
	if _current() == "dance":
		print("PASS: '/dance' plays the dance animation")
	else:
		_fail("'/dance' played '%s', expected 'dance'" % _current())
	if _character._is_dancing:
		print("PASS: '/dance' sets _is_dancing")
	else:
		_fail("'/dance' didn't set _is_dancing")
	if not _character._speech_bubble.visible:
		print("PASS: '/dance' does NOT show a speech bubble")
	else:
		_fail("'/dance' incorrectly showed a speech bubble ('%s')" % _character._speech_bubble.text)

	# 2. Case/whitespace tolerant.
	_character._is_dancing = false
	_character.anim_player.play("idle")
	_player._on_chat_message_sent("  /DANCE  ")
	if _current() == "dance" and _character._is_dancing:
		print("PASS: '/dance' matching is case-insensitive and whitespace-tolerant")
	else:
		_fail("'  /DANCE  ' didn't trigger the dance (current='%s' is_dancing=%s)" % [_current(), _character._is_dancing])

	# 2b. "/breakdance" plays breakdance, not "/dance" -- confirms the two
	# commands aren't getting confused with each other.
	_character._is_dancing = false
	_character.anim_player.play("idle")
	_character._speech_bubble.visible = false
	_player._on_chat_message_sent("/breakdance")
	if _current() == "breakdance" and _character._is_dancing:
		print("PASS: '/breakdance' plays the breakdance animation")
	else:
		_fail("'/breakdance' played '%s' (is_dancing=%s), expected 'breakdance'" % [_current(), _character._is_dancing])
	if not _character._speech_bubble.visible:
		print("PASS: '/breakdance' does NOT show a speech bubble")
	else:
		_fail("'/breakdance' incorrectly showed a speech bubble")

	_character._is_dancing = false
	_character.anim_player.play("idle")
	_player._on_chat_message_sent("  /BreakDance  ")
	if _current() == "breakdance" and _character._is_dancing:
		print("PASS: '/breakdance' matching is case-insensitive and whitespace-tolerant")
	else:
		_fail("'  /BreakDance  ' didn't trigger breakdance (current='%s' is_dancing=%s)" % [_current(), _character._is_dancing])

	# 3. A normal message still shows a real speech bubble (no regression).
	_player._on_chat_message_sent("gg")
	if _character._speech_bubble.visible and _character._speech_bubble.text == "gg":
		print("PASS: an ordinary chat message still shows a real speech bubble")
	else:
		_fail("ordinary chat message didn't show correctly (visible=%s text='%s')" % [_character._speech_bubble.visible, _character._speech_bubble.text])

	# 4. Movement cancels the dance.
	_player._on_chat_message_sent("/dance")
	Input.action_press("move_forward")
	_player._physics_process(1.0 / 60.0)
	Input.action_release("move_forward")
	if not _character._is_dancing and _current() != "dance":
		print("PASS: movement cancels the dance and takes back over")
	else:
		_fail("dance was still active after moving (is_dancing=%s current='%s')" % [_character._is_dancing, _current()])

	# 5. Jumping cancels the dance too (defensive clear, same as every other
	# one-shot action).
	_player._on_chat_message_sent("/dance")
	_character.jump(false, false)
	if not _character._is_dancing and _current() == "jump":
		print("PASS: jumping cancels the dance")
	else:
		_fail("dance was still active after jumping (is_dancing=%s current='%s')" % [_character._is_dancing, _current()])
	_character.land()

	# 6. dance() is a no-op while airborne.
	_character.anim_player.play("idle")
	_character._is_jumping = true
	_character.dance()
	if not _character._is_dancing and _current() == "idle":
		print("PASS: dance() is a no-op while airborne")
	else:
		_fail("dance() had an effect while airborne (is_dancing=%s current='%s')" % [_character._is_dancing, _current()])
	_character._is_jumping = false

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
