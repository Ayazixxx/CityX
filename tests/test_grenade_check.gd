extends Node3D
## Regression check for the throwable grenade (weapons/grenade.gd ->
## weapons/thrown_grenade.gd -> weapons/explosion.gd) and its inventory
## consumption.
##
## The explosion's LOOK can't be verified headlessly (no renderer), so what's
## checked here is everything the look sits on top of: that the fuse burns
## and detonates rather than exploding on contact, that the blast damages
## with real distance falloff and physically throws rigid bodies, that a
## replica of a networked throw does no damage, and that throwing actually
## spends a grenade from the stack and re-arms from what's left.

const GrenadeScene := preload("res://weapons/grenade.tscn")
const ThrownGrenadeScene := preload("res://weapons/thrown_grenade.tscn")
const Explosion = preload("res://weapons/explosion.gd")
const PlayerScene := preload("res://player/player.tscn")
const ItemRegistry = preload("res://items/item_registry.gd")

var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	call_deferred("_run_checks")

func _run_checks() -> void:
	_check_registry_entry()
	await _check_fuse_detonates_on_time()
	await _check_blast_damage_falloff()
	await _check_blast_throws_rigid_bodies()
	await _check_replica_does_no_damage()
	await _check_throw_consumes_from_stack()

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

## --- 1. Registered as a real, STACKABLE inventory item -- it's ammunition,
## and a stack_size of 1 would make carrying grenades pointless. ---
func _check_registry_entry() -> void:
	var item := ItemRegistry.get_item("grenade")
	if item.is_empty():
		_fail("'grenade' isn't in the item registry -- it could never be picked up or equipped")
	elif int(item.get("stack_size", 1)) <= 1:
		_fail("grenades don't stack (stack_size=%s) -- you could only ever carry one" % item.get("stack_size"))
	elif item.get("equip_slot", "") != "hand_r":
		_fail("the grenade doesn't equip to a hand slot (got '%s')" % item.get("equip_slot", ""))
	else:
		print("PASS: the grenade is a registered, stackable (x%d) hand item" % int(item["stack_size"]))

## --- 2. THE defining behaviour: it detonates on its FUSE, not on impact.
## A grenade that blows on contact is just a slow bullet. ---
func _check_fuse_detonates_on_time() -> void:
	var grenade: RigidBody3D = ThrownGrenadeScene.instantiate()
	grenade.freeze = true # keep it still; this check is about the clock, not physics
	add_child(grenade)
	await get_tree().process_frame

	var fuse: float = grenade.FUSE_SECONDS
	# Well past a contact-fuse's window, well short of the real one.
	var elapsed := 0.0
	while elapsed < fuse * 0.5:
		await get_tree().process_frame
		elapsed += get_process_delta_time()
	if not is_instance_valid(grenade) or grenade._has_exploded:
		_fail("the grenade detonated less than halfway through its own fuse")
	else:
		print("PASS: the grenade is still live halfway through its fuse (%.1fs)" % fuse)

	while elapsed < fuse + 0.4 and is_instance_valid(grenade) and not grenade._has_exploded:
		await get_tree().process_frame
		elapsed += get_process_delta_time()
	if is_instance_valid(grenade) and not grenade._has_exploded:
		_fail("the grenade never detonated -- its fuse (%.1fs) ran out %.1fs ago" % [fuse, elapsed - fuse])
	else:
		print("PASS: the grenade detonated when its fuse ran out (~%.1fs)" % elapsed)

## --- 3. Blast damage falls off with distance, and the falloff is real
## rather than all-or-nothing at the radius edge. ---
func _check_blast_damage_falloff() -> void:
	var near := _DamageProbe.new()
	near.position = Vector3(1.0, 0, 0)
	add_child(near)
	var far := _DamageProbe.new()
	far.position = Vector3(5.0, 0, 0)
	add_child(far)
	var outside := _DamageProbe.new()
	outside.position = Vector3(30.0, 0, 0)
	add_child(outside)
	await get_tree().physics_frame
	await get_tree().physics_frame

	Explosion.spawn(Vector3.ZERO, get_tree(), null, 6.0, 100.0)

	if near.total <= 0.0:
		_fail("a target 1m from the blast took no damage at all")
	elif far.total <= 0.0:
		_fail("a target 5m from a 6m blast took no damage")
	elif near.total <= far.total:
		_fail("blast damage doesn't fall off with distance (1m took %.1f, 5m took %.1f)" % [near.total, far.total])
	elif outside.total > 0.0:
		_fail("a target 30m outside a 6m blast still took %.1f damage" % outside.total)
	else:
		print("PASS: blast damage falls off with distance (1m: %.0f, 5m: %.0f, 30m: none)" % [near.total, far.total])

	near.queue_free()
	far.queue_free()
	outside.queue_free()

## --- 4. The blast physically THROWS things. This is most of why an
## explosion reads as powerful, and it's also what launches ragdolls. ---
func _check_blast_throws_rigid_bodies() -> void:
	var body := RigidBody3D.new()
	var collision := CollisionShape3D.new()
	var shape := SphereShape3D.new()
	shape.radius = 0.3
	collision.shape = shape
	body.add_child(collision)
	body.gravity_scale = 0.0 # isolate the impulse from gravity
	add_child(body)
	body.global_position = Vector3(2.0, 0, 0)
	await get_tree().physics_frame
	await get_tree().physics_frame

	Explosion.spawn(Vector3.ZERO, get_tree(), null, 6.0, 10.0)
	await get_tree().physics_frame
	await get_tree().physics_frame

	if body.linear_velocity.length() < 0.5:
		_fail("a rigid body 2m from the blast was barely moved (speed %.2f)" % body.linear_velocity.length())
	elif body.linear_velocity.x <= 0.0:
		_fail("the blast pushed a body TOWARD the explosion instead of away (vx %.2f)" % body.linear_velocity.x)
	elif body.linear_velocity.y <= 0.0:
		_fail("the blast has no upward component -- things would skid along the ground instead of being thrown")
	else:
		print("PASS: the blast throws rigid bodies up and away (velocity %s)" % body.linear_velocity)

	body.queue_free()

## --- 5. Networking: a non-authoritative replica plays the explosion but
## must NOT damage anyone, or one grenade would hurt everybody once per
## peer in the session. ---
func _check_replica_does_no_damage() -> void:
	var probe := _DamageProbe.new()
	probe.position = Vector3(1.0, 0, 0)
	add_child(probe)
	await get_tree().physics_frame

	var replica: RigidBody3D = ThrownGrenadeScene.instantiate()
	replica.is_authoritative = false
	replica.freeze = true
	add_child(replica)
	replica.global_position = Vector3.ZERO
	await get_tree().process_frame
	replica._explode()
	await get_tree().physics_frame

	if probe.total > 0.0:
		_fail("a non-authoritative replica dealt %.1f damage -- every peer's copy would damage independently" % probe.total)
	else:
		print("PASS: a replica grenade explodes visually but deals no damage")

	probe.queue_free()

## --- 6. Throwing SPENDS a grenade and immediately re-arms from the rest of
## the stack, so throwing several in a row doesn't mean re-opening the
## inventory between each. ---
func _check_throw_consumes_from_stack() -> void:
	var player: CharacterBody3D = PlayerScene.instantiate()
	add_child(player)
	player.add_to_group("players")
	await get_tree().process_frame

	var character: Node3D = preload("res://character/character.tscn").instantiate()
	player.add_child(character)
	await get_tree().process_frame

	var inv: Node = player.get_node("Inventory")
	inv.add_item("grenade", 3)
	# equip_from_slot(), the real in-game path (clicking the slot) -- NOT
	# equip_item_id(), which doesn't touch storage at all and would make the
	# accounting below meaningless.
	if not inv.equip_from_slot(0, player):
		_fail("couldn't equip a grenade at all")
		player.queue_free()
		return
	await get_tree().process_frame

	var after_equip := _count_grenades(inv)
	# THE stack bug this caught: equip_from_slot() used to wipe the whole
	# slot, so equipping one grenade out of three destroyed the other two.
	if after_equip != 2:
		_fail("equipping 1 grenade from a stack of 3 left %d in storage, expected 2 (the rest were destroyed)" % after_equip)
	else:
		print("PASS: equipping from a stack takes one and leaves the rest")

	if player._equipped_grenade == null:
		_fail("equipping a grenade didn't register it on the player (the throw branch would never run)")
		player.queue_free()
		return
	print("PASS: an equipped grenade registers on the player")

	player._try_throw_grenade()
	await get_tree().process_frame

	# 3 picked up, 1 drawn into the hand on equip (2 left), 1 thrown, and the
	# next one drawn back into the hand -- so 1 left in storage, 1 in hand.
	var remaining := _count_grenades(inv)
	if remaining != 1:
		_fail("after throwing one of a stack of 3, storage holds %d instead of 1" % remaining)
	elif player._equipped_grenade == null:
		_fail("throwing left the player empty-handed even though they had more grenades")
	else:
		print("PASS: throwing spends one grenade and re-arms from the stack (1 left in storage)")

	player.queue_free()

func _count_grenades(inv: Node) -> int:
	var total := 0
	for slot in inv.storage_slots:
		if not slot.is_empty() and slot.get("item_id", "") == "grenade":
			total += int(slot.get("count", 1))
	return total

## A minimal damageable stand-in -- take_damage() is the only thing the
## blast duck-types against, so nothing heavier is needed.
class _DamageProbe extends StaticBody3D:
	var total := 0.0

	func _init() -> void:
		var collision := CollisionShape3D.new()
		var shape := SphereShape3D.new()
		shape.radius = 0.4
		collision.shape = shape
		add_child(collision)

	func take_damage(amount: float, _attacker: Node = null, _hitstun: float = 0.0, _finisher: bool = false) -> void:
		total += amount
