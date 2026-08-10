extends RefCounted
## Local obstacle avoidance for anything in npc/ that walks toward a point.
##
## Every NPC in this project chases or wanders the same way: work out
## `direction` toward the goal, then `velocity.x/z = direction * SPEED`,
## then move_and_slide(). That is a STRAIGHT LINE, and move_and_slide's own
## sliding is the only thing that happens when a building is in the way --
## so an NPC whose target is on the far side of a block walks into the wall
## and grinds along it forever, which is the reported bug this file exists
## to fix.
##
## This does NOT do pathfinding. There is no navmesh anywhere in this
## project, and baking one per chunk would land squarely on the city's own
## streaming budget -- the thing that has already had to be rescued twice
## (see city.gd's own chunk-streaming comments). Instead this is pure local
## steering, which is enough here for a specific geometric reason: every
## obstacle a city chunk generates is a CONVEX box (see city_chunk.gd's own
## _add_building_box / _build_wing_massing). Wall-following provably escapes
## a convex obstacle -- you round it and resume -- so the cheap answer is
## also the correct one for this world.
##
## WHAT IT DOESN'T DO, stated plainly rather than left to be discovered:
## it will not lead an NPC through a doorway and up a staircase after a
## player who has gone indoors. Building interiors are genuinely concave
## (partition walls, stairwells) and local steering gets trapped in
## concave geometry. Chasing you inside needs real pathfinding; that is a
## separate piece of work, not a tuning tweak to this one.
##
## Held by each NPC as a plain member (see npc/wolf.gd's own `_steering`) --
## a RefCounted, not a Node, because it has no per-frame work of its own and
## nothing to parent: it is called from the NPC's own _physics_process and
## reads everything it needs off the body handed to it.

## Reaching the goal is worth more than hugging the wall exactly: the
## returned direction is the wall tangent blended this far back toward the
## goal, which is what makes an NPC cut the corner and peel off the moment
## the building stops being in the way, rather than tracing its whole
## perimeter.
const GOAL_BIAS := 0.35

## How long a chosen side is held before it can be reconsidered. THE most
## important number here. Without it, an NPC standing square on to a flat
## wall re-picks left/right every frame as its own tiny position changes
## flip which tangent scores better, and it jitters in place instead of
## going anywhere -- the classic local-steering failure. Long enough to
## actually clear a corner, short enough that a wrong guess is cheap.
const COMMIT_SECONDS := 1.1

## The commitment is dropped EARLY, before COMMIT_SECONDS is up, as soon as
## the straight line to the goal is clear again -- that check is what turns
## "follow this wall" back into "walk at the target". This is only the
## ceiling for the case where it never clears.

## Probe height above the body's own origin. Deliberately up at chest level:
## a ray along the floor would hit the kerb/road overlay every chunk has
## (see city_chunk.gd) and read a 6cm step as a wall. Scaled per NPC (see
## configure) since these range from a wolf to a 7.7m sloth.
const PROBE_HEIGHT_FRACTION := 0.45

## How far ahead to look, as a multiple of the body's own radius, plus a
## flat margin so a small NPC still looks far enough ahead to turn in time.
const LOOK_AHEAD_RADIUS_MULTIPLE := 2.5
const LOOK_AHEAD_MARGIN := 1.6

## World geometry: buildings, ground and kerbs are all collision_layer 1
## (see city_chunk.gd). The default for a COMBAT NPC, which deliberately
## does not avoid layer 4 (players and other NPCs) -- crowding each other is
## move_and_slide's problem, and treating a packmate as a wall would make a
## group chasing you scatter instead of converging.
const WORLD_ONLY_MASK := 1
## Characters as well. What something WALKING SOMEWHERE wants (see
## npc/unique_npc.gd's own walk_to): a story NPC crossing town has no reason
## to shove through whoever is in the road, and a head-on stall against a
## player standing in his path is a real deadlock -- move_and_slide's own
## sliding has no preferred direction when you walk squarely into something,
## so he grinds there indefinitely rather than stepping round.
const WORLD_AND_CHARACTERS_MASK := 1 | 4

## Which layers count as something to steer around. See the two masks above.
var obstacle_mask := WORLD_ONLY_MASK

var body_radius := 0.4
var probe_height := 1.0
var look_ahead := 2.6

# The side currently committed to, as a signed multiplier on the left
# vector: -1 right, +1 left, 0 for "not avoiding anything right now".
var _commit_side := 0.0
var _commit_remaining := 0.0

## Sizes the probe by hand. `width` is the body's own half-width across
## (what sets how far apart the shoulder casts sit), `top_y` how far above
## the body's origin its head is (what sets the probe height), and `reach`
## its longest horizontal half-extent (what sets how far ahead it looks).
func configure(width: float, top_y: float, reach: float) -> void:
	body_radius = maxf(width, 0.05)
	probe_height = maxf(top_y * PROBE_HEIGHT_FRACTION, 0.2)
	look_ahead = maxf(reach, width) * LOOK_AHEAD_RADIUS_MULTIPLE + LOOK_AHEAD_MARGIN

## Sizes the probe off the body's OWN CollisionShape3D, so an NPC says
## `_steering.configure_from(self)` and can't drift out of sync with its
## collider the way a hand-copied number would.
##
## Measures the shape THROUGH ITS OWN TRANSFORM rather than reading .height
## off it. That isn't fussiness: wolf.gd and scratcher.gd both lay their
## capsule flat (rotation_degrees = (90,0,0)) because they're quadrupeds,
## so the "height" field is their NOSE-TO-TAIL LENGTH, not how tall they
## are. Taking it at face value aims a wolf's probe at 0.65m when the whole
## animal is 0.56m tall -- casting clean over its own head, at nothing.
## Corners are transformed properly, which gets every case right without
## needing to know which NPC laid its capsule down.
func configure_from(body: CollisionObject3D) -> void:
	for child in body.get_children():
		if not (child is CollisionShape3D):
			continue
		var col := child as CollisionShape3D
		var shape: Shape3D = col.shape
		if shape == null:
			continue
		var basis := col.transform.basis
		var offset := col.transform.origin
		if shape is CapsuleShape3D:
			var capsule := shape as CapsuleShape3D
			# A capsule is its two end-cap centres, each inflated by radius.
			var half_axis: Vector3 = basis.y.normalized() * maxf(capsule.height * 0.5 - capsule.radius, 0.0)
			var a := offset + half_axis
			var b := offset - half_axis
			var top: float = maxf(a.y, b.y) + capsule.radius
			var reach: float = maxf(Vector2(a.x, a.z).length(), Vector2(b.x, b.z).length()) + capsule.radius
			configure(capsule.radius, top, reach)
			return
		if shape is SphereShape3D:
			var r: float = (shape as SphereShape3D).radius
			configure(r, offset.y + r, r)
			return
		if shape is BoxShape3D:
			# Real AABB half-extent of a rotated box: each axis picks up a
			# contribution from all three, so it's the ABSOLUTE basis times
			# the half-size, not the transformed corner vector.
			var half: Vector3 = (shape as BoxShape3D).size * 0.5
			var abs_basis := Basis(basis.x.abs(), basis.y.abs(), basis.z.abs())
			var extents: Vector3 = abs_basis * half
			configure(minf(extents.x, extents.z), offset.y + extents.y,
				maxf(extents.x, extents.z))
			return
		if shape is CylinderShape3D:
			var cyl := shape as CylinderShape3D
			configure(cyl.radius, offset.y + cyl.height * 0.5, cyl.radius)
			return

## The direction to actually walk in this frame.
##
## `desired` is the unit direction toward the goal -- exactly what each NPC
## already computed. The return is a unit direction on the XZ plane, and is
## simply `desired` back again whenever nothing is in the way, so an NPC
## crossing open ground moves in precisely the straight line it always did.
func steer(body: CharacterBody3D, desired: Vector3, delta: float) -> Vector3:
	var goal := Vector3(desired.x, 0.0, desired.z)
	if goal.length_squared() < 0.000001:
		return desired
	goal = goal.normalized()

	_commit_remaining = maxf(0.0, _commit_remaining - delta)

	var space := body.get_world_3d().direct_space_state
	var origin := body.global_position + Vector3.UP * probe_height
	var blocked := _probe(space, body, origin, goal)

	if not blocked:
		# The way ahead is clear. Dropping the commitment HERE rather than
		# waiting for it to time out is what makes an NPC peel off a wall
		# the instant it has cleared the corner, instead of tracing the
		# rest of the building.
		_commit_side = 0.0
		_commit_remaining = 0.0
		return goal

	var left := Vector3.UP.cross(goal).normalized()
	if _commit_remaining <= 0.0 or _commit_side == 0.0:
		_commit_side = _choose_side(space, body, origin, goal, left)
		_commit_remaining = COMMIT_SECONDS

	var along := (left * _commit_side).normalized()
	# Blended back toward the goal so this hugs the wall rather than running
	# exactly parallel to it forever -- see GOAL_BIAS.
	var steered := (along + goal * GOAL_BIAS)
	if steered.length_squared() < 0.000001:
		return along
	return steered.normalized()

## Which way to go round. Prefers the side that is ACTUALLY OPEN (a real
## probe down each tangent) over anything derived from the surface normal:
## at an outside corner both tangents look equally good to the normal, and
## only a real cast can tell that one of them walks straight into the
## adjoining wall.
##
## When both sides are open (a flat wall face-on) or both are blocked (an
## alcove), it falls back to whichever tangent points more toward the goal,
## which is the direction that makes progress.
func _choose_side(space: PhysicsDirectSpaceState3D, body: CharacterBody3D,
		origin: Vector3, goal: Vector3, left: Vector3) -> float:
	var left_open := not _cast(space, body, origin, left, look_ahead)
	var right_open := not _cast(space, body, origin, -left, look_ahead)
	if left_open and not right_open:
		return 1.0
	if right_open and not left_open:
		return -1.0
	# Tie. Both tangents are perpendicular to the goal, so neither is
	# "toward" it -- break it on the body's own current heading instead, so
	# an NPC already moving keeps its momentum rather than reversing.
	var heading := Vector3(body.velocity.x, 0.0, body.velocity.z)
	if heading.length_squared() > 0.01:
		return 1.0 if heading.normalized().dot(left) >= 0.0 else -1.0
	return 1.0

## Whether anything solid sits between the body and `look_ahead` metres
## along `direction`. Three casts, not one: a single centre ray lets an NPC
## clip a corner with its shoulder and snag, since the ray passes the edge
## cleanly while the body doesn't. The two offset casts are what give it its
## real width.
func _probe(space: PhysicsDirectSpaceState3D, body: CharacterBody3D,
		origin: Vector3, goal: Vector3) -> bool:
	if _cast(space, body, origin, goal, look_ahead):
		return true
	var left := Vector3.UP.cross(goal).normalized() * body_radius
	if _cast(space, body, origin + left, goal, look_ahead):
		return true
	return _cast(space, body, origin - left, goal, look_ahead)

func _cast(space: PhysicsDirectSpaceState3D, body: CharacterBody3D,
		from: Vector3, direction: Vector3, distance: float) -> bool:
	var query := PhysicsRayQueryParameters3D.create(from, from + direction * distance)
	query.collision_mask = obstacle_mask
	# Its own body excluded so a wide NPC doesn't read itself as the wall.
	query.exclude = [body.get_rid()]
	return not space.intersect_ray(query).is_empty()

## True while actively going around something -- for an NPC that wants to
## keep facing its target while sidestepping, or to suppress an attack that
## shouldn't fire at a wall.
func is_avoiding() -> bool:
	return _commit_side != 0.0

## Clears the committed side. Called when an NPC changes goal outright (a
## new wander target, a new chase target) so a commitment made for the old
## one doesn't steer the first moments of the new one.
func reset() -> void:
	_commit_side = 0.0
	_commit_remaining = 0.0
