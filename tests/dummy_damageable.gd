extends CharacterBody3D
## Minimal stand-in for "anything punchable" (see player.gd's own _punch()),
## used only by test_punch_check.gd -- exposes just enough (take_damage,
## health) to exercise the real duck-typed hit-detection path without
## needing a full second Player (HUD, camera, replication, etc.) along for
## the ride.

var health := 100.0
var damage_taken := 0.0
# Recorded (not acted on -- this dummy has no hitstun/ragdoll mechanic of its
# own) purely so combo tests can assert exactly what player.gd's own _punch()
# actually computed and passed for a given swing (see combo_data.gd).
var last_hitstun_seconds := 0.0
var last_is_finisher := false

func take_damage(amount: float, attacker: Node = null, hitstun_seconds: float = 0.0, is_finisher: bool = false) -> void:
	health -= amount
	damage_taken += amount
	last_hitstun_seconds = hitstun_seconds
	last_is_finisher = is_finisher
