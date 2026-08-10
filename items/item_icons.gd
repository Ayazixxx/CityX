extends RefCounted
## Inventory icons, rendered from each item's OWN real 3D scene rather than
## drawn or imported as flat art.
##
## Every icon is a photograph: the item's actual .tscn (the same one that
## spawns in the world and the same one equip_from_slot() instantiates) is
## dropped into a small offscreen SubViewport, framed by its own geometry,
## and captured to an ImageTexture. So a Beretta's icon is a Beretta, and an
## item whose mesh changes later gets a new icon for free with no art to
## re-export.
##
## Same offscreen-SubViewport capture technique character_creator.gd's own
## _build_tool_icon() already established, with two differences it needed:
## the viewport gets its OWN World3D (own_world_3d) so a photographed
## RigidBody3D can't fall through, collide with, or be lit by the live game
## world, and the camera is ORTHOGONAL and framed off the item's own merged
## AABB, so a greatsword and a top hat both fill the same icon box instead
## of one being a speck and the other overflowing.
##
## Icons are cached in a STATIC dictionary, so the whole set is rendered
## once per process no matter how many times a panel is rebuilt (respawn,
## rejoin, a second local player). Building is async and spread one item per
## few frames -- never a blocking loop -- because load-time stalls are this
## project's own standing #1 complaint.
##
## Headless-safe by design: an offscreen viewport has no pixels to give back
## under the dummy renderer (confirmed -- character_creator.gd's own tool
## icons already fail exactly that way), so every capture is null-checked
## and simply leaves that item uncached. ui/item_slot.gd falls back to the
## item's display name whenever get_icon() returns null, which is what keeps
## the tests (and any software-rendered run) showing something readable.

const ItemRegistry = preload("res://items/item_registry.gd")

const ICON_SIZE := 128
# A 3/4 view -- straight-on renders a sword as a featureless vertical line
# and a hat as a flat disc.
const ICON_VIEW_DIRECTION := Vector3(0.62, 0.45, 1.0)
# Breathing room around the item's own bounds so nothing touches the icon's
# own edges.
const ICON_FRAME_PADDING := 1.12
const KEY_LIGHT_ENERGY := 1.6
const KEY_LIGHT_ROTATION := Vector3(-35.0, -40.0, 0.0)
const AMBIENT_ENERGY := 0.6
const BACKDROP_COLOR := Color(0, 0, 0, 0)

# item_id -> ImageTexture. Static: rendered once per process, shared by
# every panel that ever asks.
static var _icons := {}
static var _built := false
static var _building := false

static func get_icon(item_id: String) -> ImageTexture:
	return _icons.get(item_id, null)

## Also the builder's own way in, so a test can seed a known texture through
## exactly the path the real renderer uses.
static func cache_icon(item_id: String, texture: ImageTexture) -> void:
	if texture != null:
		_icons[item_id] = texture

static func is_built() -> bool:
	return _built

## Renders every registered item's icon, one item at a time, yielding
## between each. `host` only supplies a SceneTree to wait on and a parent
## for the throwaway viewport -- it's never modified. Safe to call
## repeatedly: already-built or mid-build calls return immediately (see
## ui/inventory_panel.gd, which re-refreshes on every open, so a panel that
## opened mid-build picks up the finished icons by itself).
static func build_all(host: Node) -> void:
	if _built or _building or host == null or not host.is_inside_tree():
		return
	_building = true
	var tree := host.get_tree()

	var viewport := SubViewport.new()
	viewport.size = Vector2i(ICON_SIZE, ICON_SIZE)
	viewport.transparent_bg = true
	# Its own world, so nothing photographed here can touch (or be lit by,
	# or fall through) the real city the player is standing in.
	viewport.own_world_3d = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	# The panel that hosts this pauses the tree when it opens; icons must
	# keep rendering regardless of who paused what.
	viewport.process_mode = Node.PROCESS_MODE_ALWAYS
	host.add_child(viewport)

	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = BACKDROP_COLOR
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color.WHITE
	environment.ambient_light_energy = AMBIENT_ENERGY

	var camera := Camera3D.new()
	camera.environment = environment
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	viewport.add_child(camera)

	# Ambient alone renders every item as a flat silhouette -- a key light
	# is what makes the shapes actually readable at 128px.
	var key_light := DirectionalLight3D.new()
	key_light.light_energy = KEY_LIGHT_ENERGY
	key_light.rotation = Vector3(
		deg_to_rad(KEY_LIGHT_ROTATION.x),
		deg_to_rad(KEY_LIGHT_ROTATION.y),
		deg_to_rad(KEY_LIGHT_ROTATION.z)
	)
	viewport.add_child(key_light)

	for item_id in ItemRegistry.ITEMS:
		if not is_instance_valid(viewport) or tree == null:
			break
		await _build_one(viewport, camera, tree, item_id)

	if is_instance_valid(viewport):
		viewport.queue_free()
	_building = false
	_built = true

static func _build_one(viewport: SubViewport, camera: Camera3D, tree: SceneTree, item_id: String) -> void:
	var visual := build_visual_for(item_id)
	if visual == null:
		return
	viewport.add_child(visual)
	# One frame for the item's own _ready() to actually build its meshes --
	# nearly every item in this project generates its geometry in code
	# rather than storing it in the .tscn, so there is nothing to measure or
	# photograph before this.
	await tree.process_frame
	if not is_instance_valid(visual):
		return
	# Only NOW, after _ready() has run: stops each item's own _process from
	# polling input or proximity for the rest of its short life as a photo
	# subject.
	visual.process_mode = Node.PROCESS_MODE_DISABLED

	var bounds := merged_visual_aabb(visual)
	if bounds.size.length() > 0.0:
		_frame_camera(camera, bounds)
		# Two more frames: one for the re-aimed camera to take effect, one
		# for the viewport to actually render through it.
		await tree.process_frame
		await tree.process_frame
		if is_instance_valid(viewport):
			var texture := viewport.get_texture()
			if texture != null:
				var image := texture.get_image()
				# Null under the dummy renderer -- see the class comment.
				if image != null:
					cache_icon(item_id, ImageTexture.create_from_image(image))

	if is_instance_valid(visual):
		visual.queue_free()

## The node to photograph for one item: its own registered scene, or a
## stand-in for the LOOT_ONLY entries that have no scene at all (see
## items/item_registry.gd) -- every registered item gets SOMETHING, so no
## item can silently end up as the one text label in a grid of icons.
static func build_visual_for(item_id: String) -> Node3D:
	var data := ItemRegistry.get_item(item_id)
	if data.is_empty():
		return null
	if data.has("scene"):
		var instance = data["scene"].instantiate()
		if instance is Node3D:
			# Set BEFORE it enters the tree: a world-drop item is a live
			# RigidBody3D, and an unfrozen one starts falling out of frame
			# during the very frames being photographed.
			if instance is RigidBody3D:
				(instance as RigidBody3D).freeze = true
			return instance as Node3D
		instance.queue_free()
		return null
	return _build_loot_visual(item_id)

## A few jumbled metal shards, for scrap_metal and any future scene-less
## loot -- deliberately built here rather than given a real .tscn of its
## own, since a pure loot entry has no world presence to justify one.
static func _build_loot_visual(_item_id: String) -> Node3D:
	var root := Node3D.new()
	var material := StandardMaterial3D.new()
	material.albedo_color = Color(0.55, 0.57, 0.6)
	material.metallic = 0.7
	material.roughness = 0.45

	var shards := [
		{"size": Vector3(0.13, 0.03, 0.09), "pos": Vector3(0.0, 0.0, 0.0), "rot": Vector3(6.0, 18.0, -10.0)},
		{"size": Vector3(0.10, 0.025, 0.07), "pos": Vector3(0.05, 0.03, -0.04), "rot": Vector3(-14.0, -35.0, 22.0)},
		{"size": Vector3(0.08, 0.02, 0.06), "pos": Vector3(-0.05, 0.025, 0.04), "rot": Vector3(20.0, 52.0, 8.0)},
	]
	for shard in shards:
		var mesh_instance := MeshInstance3D.new()
		var box := BoxMesh.new()
		box.size = shard["size"]
		mesh_instance.mesh = box
		mesh_instance.material_override = material
		mesh_instance.position = shard["pos"]
		mesh_instance.rotation = Vector3(
			deg_to_rad(shard["rot"].x), deg_to_rad(shard["rot"].y), deg_to_rad(shard["rot"].z)
		)
		root.add_child(mesh_instance)
	return root

## Every MeshInstance3D under `root`, merged into one AABB in WORLD space.
## This is what makes one framing rule work for a 2m greatsword and a 0.2m
## flashlight alike -- and it's measured from the real meshes rather than
## any authored bound, since most of this project's items build their
## geometry procedurally at _ready() time.
##
## World space, NOT root-local, and that distinction is load-bearing:
## _frame_camera() places a camera at real world coordinates derived from
## these bounds. Measuring root-locally instead cost every item whose own
## script scales its root -- all three swords set `scale = ONE * 2` in their
## _ready() -- exactly that scale factor of framing, so their blades
## rendered at double the size the camera had been sized to contain and got
## clipped off the top and bottom of the icon. Caught by rendering the real
## icons and measuring their alpha bounds, since a headless run has no
## pixels to check.
static func merged_visual_aabb(root: Node3D) -> AABB:
	var bounds := AABB()
	var found := false
	for node in _all_descendants(root):
		if not (node is MeshInstance3D):
			continue
		var mesh_instance := node as MeshInstance3D
		if mesh_instance.mesh == null or not mesh_instance.visible:
			continue
		var piece := _transform_aabb(mesh_instance.global_transform, mesh_instance.mesh.get_aabb())
		if found:
			bounds = bounds.merge(piece)
		else:
			bounds = piece
			found = true
	return bounds

static func _all_descendants(node: Node) -> Array:
	var out := [node]
	for child in node.get_children():
		out.append_array(_all_descendants(child))
	return out

## Transforms all 8 corners and re-encapsulates, rather than relying on a
## Transform3D*AABB operator -- an AABB has to stay axis-aligned, so a
## rotated piece's real bound is the box around its rotated corners.
static func _transform_aabb(transform: Transform3D, bounds: AABB) -> AABB:
	var out := AABB(transform * bounds.position, Vector3.ZERO)
	for i in range(8):
		out = out.expand(transform * bounds.get_endpoint(i))
	return out

## Aims the orthogonal camera at the item's own bounds from ICON_VIEW_
## DIRECTION and sizes it to exactly contain them: the 8 corners are
## projected into camera space and the widest one sets `size`, so the item
## fills the icon whatever its shape or aspect ratio.
static func _frame_camera(camera: Camera3D, bounds: AABB) -> void:
	var center := bounds.get_center()
	var radius := maxf(bounds.size.length() * 0.5, 0.001)
	var distance := radius * 3.0
	var direction := ICON_VIEW_DIRECTION.normalized()
	camera.global_position = center + direction * distance
	camera.look_at(center, Vector3.UP)

	var to_camera := camera.global_transform.affine_inverse()
	var extent := 0.0
	for i in range(8):
		var corner: Vector3 = to_camera * bounds.get_endpoint(i)
		extent = maxf(extent, maxf(absf(corner.x), absf(corner.y)))
	camera.size = maxf(extent * 2.0 * ICON_FRAME_PADDING, 0.001)
	camera.near = 0.01
	camera.far = distance + radius * 2.0 + 1.0
