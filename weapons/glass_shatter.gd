extends GPUParticles3D
## One-shot glass-shard burst spawned wherever a thrown bottle impacts
## anything (see weapons/thrown_bottle.gd's own _on_body_entered) --
## everything about how it looks lives declaratively on the .tscn itself,
## same "temporary effect cleans up after itself" convention
## weapons/blood_splatter.gd already establishes. Omnidirectional (unlike
## blood_splatter's own away-from-the-surface spray) since a real
## shattering bottle bursts outward in every direction at once, not just
## away from whatever it hit -- no orientation basis needed at the spawn
## site, unlike blood_splatter.gd's own Basis.looking_at(-hit_normal, ...).

func _ready() -> void:
	finished.connect(queue_free)
