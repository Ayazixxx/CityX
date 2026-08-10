shader_type canvas_item;
// Masks the node's own rect down to its inscribed circle, with a solid-
// color ring drawn right at the boundary -- baking the border into the
// same shader pass (rather than layering a separate circular background
// panel behind an inset, separately-masked image) guarantees the ring is
// pixel-perfectly concentric with the mask itself, with no separate
// layer's own size/position to get slightly out of sync with the other.

uniform vec4 border_color : source_color = vec4(0.75, 0.6, 0.2, 1.0);
uniform float border_thickness : hint_range(0.0, 0.5) = 0.06;

void fragment() {
	vec2 centered = UV - vec2(0.5);
	float dist = length(centered);
	if (dist > 0.5) {
		COLOR = vec4(0.0);
	} else if (dist > 0.5 - border_thickness) {
		COLOR = border_color;
	} else {
		COLOR = texture(TEXTURE, UV);
	}
}
