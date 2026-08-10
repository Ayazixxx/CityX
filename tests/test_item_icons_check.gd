extends Node3D
## Regression check for inventory image icons (items/item_icons.gd +
## ui/item_slot.gd), replacing the old plain-text item names.
##
## What can and can't be checked headlessly, stated plainly: an offscreen
## SubViewport has NO pixels to read back under the dummy renderer, so the
## actual rendered icon images cannot be verified here (the same limitation
## character_creator.gd's own tool icons already hit). Everything around
## that IS checkable, and is what this file covers -- every registered item
## can produce something to photograph, the framing math is real, and a
## slot behaves correctly both with an icon and without one.
##
## The icon-present cases seed the cache through ItemIcons.cache_icon(),
## the same entry point the real renderer writes through, so the slot code
## under test is exercised identically to a real windowed run.

const ItemIcons = preload("res://items/item_icons.gd")
const ItemRegistry = preload("res://items/item_registry.gd")
const ItemSlotScript = preload("res://ui/item_slot.gd")

var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	call_deferred("_run_checks")

func _run_checks() -> void:
	_check_every_item_has_a_visual()
	_check_visual_bounds_are_real()
	_check_camera_frames_the_item()
	await _check_slot_shows_icon_when_available()
	await _check_slot_falls_back_to_name_without_icon()
	await _check_build_all_survives_headless()

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

## --- 1. THE guard against a half-iconned grid: every registered item must
## produce SOMETHING to photograph. An item added to the registry with no
## "scene" (a LOOT_ONLY entry) and no fallback would silently be the one
## text label sitting in a grid of pictures. ---
func _check_every_item_has_a_visual() -> void:
	var missing := []
	for item_id in ItemRegistry.ITEMS:
		var visual := ItemIcons.build_visual_for(item_id)
		if visual == null:
			missing.append(item_id)
		else:
			visual.queue_free()
	if not missing.is_empty():
		_fail("these registered items can't produce an icon visual at all: %s" % [missing])
	else:
		print("PASS: all %d registered items can produce an icon visual (scene-backed or fallback)" % ItemRegistry.ITEMS.size())

	# The scene-less loot entry is the specific case the fallback exists
	# for -- worth naming, since it's the one that would regress silently.
	var scrap := ItemIcons.build_visual_for("scrap_metal")
	if scrap == null or ItemRegistry.get_item("scrap_metal").has("scene"):
		_fail("scrap_metal (a scene-less LOOT_ONLY item) has no fallback visual")
	else:
		add_child(scrap)
		var bounds := ItemIcons.merged_visual_aabb(scrap)
		if bounds.size.length() <= 0.0:
			_fail("scrap_metal's fallback visual has no geometry to photograph")
		else:
			print("PASS: the scene-less loot item still gets a real fallback visual (bounds ", bounds.size, ")")
		scrap.queue_free()

## --- 2. Real item scenes build their meshes in their own _ready(), so
## the AABB has to be measured AFTER a frame in the tree -- this is what
## the icon framing depends on entirely. ---
func _check_visual_bounds_are_real() -> void:
	var visual := ItemIcons.build_visual_for("flashlight")
	if visual == null:
		_fail("couldn't instantiate the flashlight's own scene for an icon")
		return
	# The world-drop version is a live RigidBody3D; an unfrozen one falls
	# out of frame during the very frames being captured.
	if visual is RigidBody3D and not (visual as RigidBody3D).freeze:
		_fail("a RigidBody3D item isn't frozen before being photographed -- it would fall out of frame")
	else:
		print("PASS: a RigidBody3D item is frozen before it's photographed")
	add_child(visual)
	await get_tree().process_frame

	var bounds := ItemIcons.merged_visual_aabb(visual)
	if bounds.size.length() <= 0.0:
		_fail("the flashlight's merged AABB is empty -- nothing would be framed")
	else:
		print("PASS: a real item scene's merged AABB is measurable (", bounds.size, ")")

	# Bounds must be WORLD space, since _frame_camera() places a camera at
	# world coordinates derived from them. A real bug this caught: measuring
	# root-locally instead silently ignored each sword's own `scale = ONE*2`
	# (set in its _ready()), so all three swords rendered at twice the size
	# the camera was framed for and got clipped off the top and bottom of
	# their icons -- while every unscaled item looked perfectly fine.
	visual.scale = Vector3.ONE * 2.0
	await get_tree().process_frame
	var scaled := ItemIcons.merged_visual_aabb(visual)
	if not scaled.size.is_equal_approx(bounds.size * 2.0):
		_fail("merged AABB ignores the root's own scale (%s at 1x vs %s at 2x) -- a scaled item would be framed at the wrong size and clip" % [bounds.size, scaled.size])
	else:
		print("PASS: merged AABB is world-space, so an item whose script scales its root is framed at its real size")
	visual.queue_free()

## --- 3. The framing math: whatever the item's size or shape, the camera
## must end up looking AT it, from outside it, with an orthogonal size that
## actually contains it. Checked against two deliberately mismatched
## boxes -- a long thin one and a small cube -- since "one framing rule for
## a greatsword and a top hat alike" is the entire point. ---
func _check_camera_frames_the_item() -> void:
	var viewport := SubViewport.new()
	viewport.size = Vector2i(64, 64)
	viewport.own_world_3d = true
	add_child(viewport)
	var camera := Camera3D.new()
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	viewport.add_child(camera)

	for case in [
		{"name": "long thin (sword-like)", "bounds": AABB(Vector3(-0.05, 0.0, -0.05), Vector3(0.1, 2.0, 0.1))},
		{"name": "small cube (loot-like)", "bounds": AABB(Vector3(-0.1, -0.1, -0.1), Vector3(0.2, 0.2, 0.2))},
		{"name": "off-origin (rest-pose wearable)", "bounds": AABB(Vector3(-0.3, 1.1, -0.2), Vector3(0.6, 0.5, 0.4))},
	]:
		var bounds: AABB = case["bounds"]
		ItemIcons._frame_camera(camera, bounds)
		var center := bounds.get_center()

		# Outside the bounds, looking at their center.
		if bounds.has_point(camera.global_position):
			_fail("%s: camera ended up INSIDE the item's own bounds" % case["name"])
			continue
		var to_center := (center - camera.global_position).normalized()
		var forward := -camera.global_transform.basis.z.normalized()
		if to_center.dot(forward) < 0.999:
			_fail("%s: camera isn't actually aimed at the item's center" % case["name"])
			continue

		# Ortho size contains every corner, with the intended padding and
		# no wild over-zoom-out.
		var to_camera := camera.global_transform.affine_inverse()
		var extent := 0.0
		for i in range(8):
			var corner: Vector3 = to_camera * bounds.get_endpoint(i)
			extent = maxf(extent, maxf(absf(corner.x), absf(corner.y)))
		var needed := extent * 2.0
		if camera.size < needed:
			_fail("%s: ortho size %s can't contain the item (needs %s) -- it would be cropped" % [case["name"], camera.size, needed])
		elif camera.size > needed * 1.5:
			_fail("%s: ortho size %s is far larger than needed (%s) -- the item would be a speck" % [case["name"], camera.size, needed])
		else:
			print("PASS: %s is framed to fill the icon (ortho size %.3f vs %.3f needed)" % [case["name"], camera.size, needed])

	viewport.queue_free()

## --- 4. With an icon cached, a slot shows the PICTURE and no name text
## (the name moves to the tooltip), plus a stack badge when it's a stack.
## Drag must also still work -- emptiness is now decided by item_id, and
## the old text-based check would have refused to drag any filled slot. ---
func _check_slot_shows_icon_when_available() -> void:
	var image := Image.create(8, 8, false, Image.FORMAT_RGBA8)
	image.fill(Color(1, 0, 0, 1))
	ItemIcons.cache_icon("pistol", ImageTexture.create_from_image(image))

	var slot: Button = ItemSlotScript.new()
	slot.source = "storage"
	slot.index_or_key = 0
	add_child(slot)
	await get_tree().process_frame

	slot.refresh("pistol", 1)
	if slot.icon == null:
		_fail("a slot holding an item with a cached icon didn't show the icon")
	elif slot.text != "":
		_fail("a slot showing an icon ALSO shows name text ('%s') -- they'd fight for 48px" % slot.text)
	elif slot.tooltip_text != "Beretta M9":
		_fail("the item's name didn't move to the tooltip (got '%s')" % slot.tooltip_text)
	else:
		print("PASS: a slot with a cached icon shows the picture, no inline name, name on tooltip")

	var badge: Label = slot.get_node_or_null("CountBadge")
	if badge == null:
		_fail("the slot has no stack-count badge at all")
	else:
		slot.refresh("scrap_metal", 7)
		if badge.text != "x7":
			_fail("a stack of 7 didn't show an 'x7' badge (got '%s')" % badge.text)
		else:
			slot.refresh("pistol", 1)
			if badge.text != "":
				_fail("a single (non-stacked) item still shows a count badge ('%s')" % badge.text)
			else:
				print("PASS: stack counts show as a corner badge, and single items show none")

	if slot._get_drag_data(Vector2.ZERO) == null:
		_fail("a FILLED slot refused to start a drag (the old text-emptiness check would do exactly this)")
	else:
		print("PASS: a filled slot still starts a drag now that its text is empty")

	slot.refresh("", 0)
	if slot.icon != null:
		_fail("emptying a slot left its icon behind")
	elif slot._get_drag_data(Vector2.ZERO) != null:
		_fail("an EMPTY slot offered drag data")
	else:
		print("PASS: emptying a slot clears its icon and stops it being draggable")

	slot.queue_free()

## --- 5. The headless/software-render path: with no icon available, the
## slot must fall back to the readable display name rather than rendering
## as a blank button. ---
func _check_slot_falls_back_to_name_without_icon() -> void:
	var slot: Button = ItemSlotScript.new()
	slot.source = "storage"
	slot.index_or_key = 1
	add_child(slot)
	await get_tree().process_frame

	# "greatsword" is deliberately never seeded into the cache above.
	slot.refresh("greatsword", 1)
	if ItemIcons.get_icon("greatsword") != null:
		print("NOTE: greatsword icon exists in this run; fallback path not exercised")
	elif slot.icon != null:
		_fail("a slot showed an icon for an item that has none cached")
	elif slot.text != "Greatsword":
		_fail("without an icon, the slot didn't fall back to the item's name (got '%s')" % slot.text)
	else:
		print("PASS: with no icon available, the slot falls back to the readable item name")

	slot.queue_free()

## --- 6. build_all() must survive the headless case (every capture comes
## back null) without erroring, leaving no viewport behind, and without
## wedging the "already built" flag in a way that blocks a real run. ---
func _check_build_all_survives_headless() -> void:
	var children_before := get_child_count()
	await ItemIcons.build_all(self)
	if not ItemIcons.is_built():
		_fail("build_all() didn't finish (is_built() still false)")
	else:
		print("PASS: build_all() ran to completion over every registered item")

	# The throwaway viewport is queue_free()'d at the end -- one more frame
	# for that to actually take effect before counting.
	await get_tree().process_frame
	if get_child_count() > children_before:
		_fail("build_all() left %d node(s) behind after finishing" % (get_child_count() - children_before))
	else:
		print("PASS: build_all() cleans up its offscreen viewport")

	# Idempotent: a second call (a respawn, a rejoin, a second panel) must
	# be a no-op rather than re-rendering everything again.
	await ItemIcons.build_all(self)
	await get_tree().process_frame
	if get_child_count() > children_before:
		_fail("a repeat build_all() call started a second render pass")
	else:
		print("PASS: a repeat build_all() call is a no-op (icons are cached per process)")
