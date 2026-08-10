extends Node
## Cheap sanity check on character/combo_data.gd's own data table -- catches
## an accidental miscount/mislabeled finisher flag before it ever reaches a
## real Character (see test_unarmed_combo_check.gd for the end-to-end
## behavioral check). Runs headless: pure data, no scene tree needed at all.

const ComboData = preload("res://character/combo_data.gd")

func _ready() -> void:
	var ok := true
	var chain := ComboData.chain_for("unarmed")

	if chain.size() == 5:
		print("PASS: unarmed chain has exactly 5 stages")
	else:
		print("FAIL: expected 5 stages, got ", chain.size())
		ok = false

	for i in range(chain.size()):
		var stage: Dictionary = chain[i]
		var is_last := i == chain.size() - 1
		var finisher: bool = stage.get("finisher", false)
		if finisher == is_last:
			print("PASS: stage ", i, " finisher flag is ", finisher, " as expected")
		else:
			print("FAIL: stage ", i, " finisher flag is ", finisher, ", expected ", is_last)
			ok = false

	if ComboData.chain_for("nonexistent_moveset").is_empty():
		print("PASS: an unknown moveset key returns an empty chain rather than erroring")
	else:
		print("FAIL: unknown moveset key should return an empty array")
		ok = false

	print("OVERALL: ", "PASS" if ok else "FAIL")
	get_tree().quit(0 if ok else 1)
