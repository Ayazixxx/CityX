extends RefCounted
## Per-moveset attack chains -- shared data table so character.gd (animation
## state) and player.gd (damage/hitstun numbers) don't have to re-derive each
## other's numbers. Each moveset key maps to an ARRAY of stage Dictionaries,
## played in order as punch() keeps landing within each stage's own "window"
## (character.gd's own _combo_window_timer). Only "unarmed" is populated for
## now -- sword keeps its existing separate _sword_combo_ready/
## _sword_combo_timer 2-hit mechanic (see character.gd's own punch()), a
## deliberate scope decision: migrating sword onto this same table (its own
## CHAINS entry, deleting the old boolean state, repointing punch()'s sword
## branch) is a described follow-up, not part of this pass, since 3 existing
## tests poke _sword_combo_ready/_sword_combo_timer directly today.
##
## damage_scale/hitstun_scale are multipliers against player.gd's own
## PUNCH_DAMAGE/HITSTUN_SECONDS (not literal duplicated numbers), matching
## this codebase's existing "derive, don't duplicate" convention (see
## HEAVY_PUNCH_DAMAGE := PUNCH_DAMAGE * 1.5). window is a flat literal
## (seconds after this stage during which another punch input advances to
## the next stage rather than restarting the chain) -- there's no natural
## "base" to scale it against, same as SWORD_COMBO_WINDOW_SECONDS being a
## flat literal today. finisher is only ever true on a chain's last stage --
## an explicit flag (not just "last index") so a future chain could
## theoretically end early without renumbering logic.
const CHAINS := {
	"unarmed": [
		{"anim": "punch_right", "damage_scale": 1.0, "hitstun_scale": 1.0, "window": 0.9, "finisher": false},
		{"anim": "punch_left",  "damage_scale": 1.0, "hitstun_scale": 1.0, "window": 0.9, "finisher": false},
		{"anim": "punch_right", "damage_scale": 1.0, "hitstun_scale": 1.0, "window": 0.9, "finisher": false},
		{"anim": "punch_left",  "damage_scale": 1.0, "hitstun_scale": 1.1, "window": 0.9, "finisher": false},
		{"anim": "punch_heavy", "damage_scale": 1.5, "hitstun_scale": 0.0, "window": 0.0, "finisher": true},
	],
}

static func chain_for(moveset_key: String) -> Array:
	return CHAINS.get(moveset_key, [])
