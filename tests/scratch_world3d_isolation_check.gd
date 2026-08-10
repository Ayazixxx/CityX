extends Node
## One-off regression check for a real bug: title_screen_city.gd's
## SubViewport (see title_screen.tscn) didn't set own_world_3d, so by
## default it inherited the ROOT viewport's World3D -- the same one
## character_creator.tscn (and the real game world) renders into directly.
## That meant the title screen's own WorldEnvironment (dark navy fog +
## filmic tonemap) was leaking into character_creator's rendering, which is
## almost certainly why the user saw a corrupted/washed-out character
## creator view. Fixed by adding own_world_3d = true to that SubViewport.
## This check confirms the two scenes now resolve to DIFFERENT World3D
## instances. Not part of the game.

const TitleScreenScene := preload("res://title_screen/title_screen.tscn")
const CharacterCreatorScene := preload("res://character_creator/character_creator.tscn")

func _ready() -> void:
	var ts := TitleScreenScene.instantiate()
	add_child(ts)
	var creator := CharacterCreatorScene.instantiate()
	add_child(creator)
	await get_tree().process_frame
	await get_tree().process_frame

	var title_subviewport: SubViewport = ts.get_node("City3DViewport/SubViewport")
	var title_world := title_subviewport.find_world_3d()
	var creator_world := creator.get_viewport().find_world_3d()

	print("Title screen SubViewport own_world_3d: ", title_subviewport.own_world_3d)
	print("Title world == creator world (should be FALSE): ", title_world == creator_world)
	print("Root viewport world == creator world (should be TRUE, creator uses the real root viewport): ", get_viewport().find_world_3d() == creator_world)

	var ok := title_subviewport.own_world_3d and title_world != creator_world
	print("OVERALL: PASS" if ok else "OVERALL: FAIL")
	get_tree().quit()
