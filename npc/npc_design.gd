extends Resource
class_name NpcDesign
## One character drawn in the game's own character creator, baked into the
## project as a real asset so a unique NPC can wear it.
##
## Everything the creator produces is these five fields -- there is no
## separate "NPC art" pipeline and there doesn't need to be one: an NPC body
## IS character.tscn (see npc/civilian.gd, npc/gunman.gd), the same node the
## player uses, so handing it a saved design is exactly what main.gd already
## does for a loaded save (see its own _apply_pending_paint).
##
## WHY A RESOURCE AND NOT A .png ON DISK: atlas_png holds the raw bytes of
## the painted atlas, which have to survive byte-for-byte -- character.gd's
## own apply_atlas_png() rejects anything that isn't exactly the atlas's own
## dimensions, and a .png sitting in the project would be run through
## Godot's texture importer (compression, mipmaps, possible resize) before
## anything could read it back. A Resource is stored verbatim and never
## touched by an importer, the same reason character/fused_body_cache.res
## exists in this project.
##
## Baked by tests/scratch_bake_npc_design.gd, which reads the real
## user://characters.cfg. Nothing at runtime ever reads that save file --
## once baked, a design is a project asset that ships, works on any machine,
## and survives deleting the original character in character select.

## Shown as the speaker name in dialogue (see ui/dialogue_box.gd), and the
## name the character was actually saved under.
@export var display_name: String = ""
## The painted atlas, exactly as character.gd's own get_atlas_png() returned
## it.
@export var atlas_png: PackedByteArray = PackedByteArray()
@export var head_shape: String = "sphere"
@export var hair_style: String = "none"
@export var height_scale: float = 1.0

## Applies this design onto a live character.tscn instance.
##
## MUST be called AFTER the character is already inside the scene tree.
## character.gd's own _ready() unconditionally rebuilds a blank white atlas
## the moment it runs, so anything applied before tree entry is silently
## wiped -- the exact bug main.gd's own _apply_pending_paint documents
## (reported once as "loading an old saved character enters the game as a
## plain white character"). set_head_shape() goes first for the same reason
## it does there: changing head shape rebuilds that part of the atlas.
func apply_to(character: Node) -> void:
	if character == null or not is_instance_valid(character):
		return
	character.set_head_shape(head_shape)
	character.apply_atlas_png(atlas_png)
	character.set_hair_style(hair_style)
	character.set_height_scale(height_scale)
