extends Node3D
## Scratch end-to-end check: does a REAL city.tscn actually grow a crowd?
## The permanent suite (tests/test_civilian_check.gd) stubs the city out so
## it can sweep placements cheaply; this one stands up the real thing once
## to confirm the wiring between city.gd, its CivilianPopulation child and
## set_player() is genuinely hooked up.

const CITY_SCENE := preload("res://city/city.tscn")

func _ready() -> void:
	call_deferred("_run")

func _run() -> void:
	var city: Node3D = CITY_SCENE.instantiate()
	add_child(city)
	await city.initial_load_ready

	var population: Node = city.get_node_or_null("CivilianPopulation")
	print("population node present: ", population != null)

	# A stand-in for the player -- the population only needs a Node3D whose
	# global_position it can read.
	var fake_player := Node3D.new()
	city.add_child(fake_player)
	fake_player.global_position = Vector3.ZERO
	city.set_player(fake_player)

	for i in range(700):
		await get_tree().physics_frame
		if i % 100 == 0 and population != null:
			print("frame %d: %d civilians" % [i, population.get_child_count()])

	if population != null:
		var indoors := 0
		for c in population.get_children():
			var params := {}
			print("  civilian at ", c.global_position, " wander_radius ", c.wander_radius)
			if c.wander_radius < 5.0:
				indoors += 1
		print("total: %d civilians (%d placed indoors)" % [population.get_child_count(), indoors])

	get_tree().quit(0)
