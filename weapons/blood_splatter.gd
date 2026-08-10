extends GPUParticles3D
## One-shot blood-spurt effect spawned at a landed hit's own impact point
## (see weapons/pistol.gd's own fire(), and weapons/thrown_bottle.gd's own
## impact handling) -- everything about how it looks (color, spread,
## droplet size) lives declaratively on the .tscn itself; this script's
## only job is freeing the node once the burst finishes playing, matching
## this project's own "temporary effect cleans up after itself" convention
## (see e.g. player.gd's own muzzle-flash timer).

## Small, quick red spray at a landed hit's own impact point -- oriented so
## the burst sprays away from whatever surface (a body) it hit, using the
## caller's own hit normal. A standalone scene (not a child of the shooter
## or the target) since it needs to persist and finish playing in a fixed
## WORLD position regardless of what either does next -- added directly to
## the current scene rather than under self, which gets freed/reparented on
## drop/despawn (no such mechanic exists yet, but this doesn't need to
## assume it never will).
##
## A static function (not an instance method on pistol.gd) specifically so
## more than one weapon can call it without duplicating this same particle-
## spawn logic -- same "static-function module" pattern character_save.gd
## already establishes. tree is passed explicitly (a static func has no
## self/get_tree() of its own to call).
##
## Uses load(), not preload(), for its own scene -- this script IS
## blood_splatter.tscn's own attached script, so a top-level preload() of
## that same scene (preload() resolves at script-PARSE time, before this
## script has even finished loading) would be a real circular load. A
## runtime load() call inside the function body has no such problem, since
## by the time spawn() actually executes this script is already fully
## loaded.
static func spawn(hit_position: Vector3, hit_normal: Vector3, tree: SceneTree) -> void:
	var splatter: GPUParticles3D = load("res://weapons/blood_splatter.tscn").instantiate()
	tree.current_scene.add_child(splatter)
	splatter.global_position = hit_position
	# Same "aim an arbitrary axis along a direction, picking a non-parallel
	# up hint" trick pistol.gd's own _apply_spread helper vector already
	# uses -- built so the particle system's own local +Z (see
	# blood_splatter.tscn's own ParticleProcessMaterial direction) ends up
	# pointing along hit_normal, i.e. away from the surface that got hit.
	var up_hint := Vector3.UP if absf(hit_normal.dot(Vector3.UP)) < 0.95 else Vector3.RIGHT
	splatter.global_basis = Basis.looking_at(-hit_normal, up_hint)

func _ready() -> void:
	finished.connect(queue_free)
