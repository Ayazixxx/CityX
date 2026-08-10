shader_type spatial;
render_mode unshaded;

// Planar reflection: mirror.gd renders the scene a second time from a
// camera mirrored across this surface's own plane, into reflection_tex.
// Because that camera shares the main camera's FOV and its viewport shares
// the main viewport's aspect, the reflected render already lines up with
// the screen -- so a fragment of the glass samples the reflection at its
// OWN screen position rather than needing any UV projection math here.
uniform sampler2D reflection_tex : filter_linear, source_color;
// Reflecting the camera's basis across the plane flips its handedness,
// which mirror.gd deliberately undoes by rebuilding the basis with look_at
// (a left-handed basis renders inside out). That leaves the image
// un-mirrored, so the flip a real mirror has is reapplied here. Exposed as
// a uniform rather than hardcoded because which way round this needs to go
// is exactly the kind of thing that can't be confirmed without looking at
// it, and flipping it is a one-line change either way.
uniform bool flip_horizontal = true;
// Real glass is never a perfect 1:1 return -- a slight tint/dimming stops
// the reflection reading brighter than the room it's reflecting.
uniform vec3 tint : source_color = vec3(0.86, 0.88, 0.9);

void fragment() {
	vec2 uv = SCREEN_UV;
	if (flip_horizontal) {
		uv.x = 1.0 - uv.x;
	}
	ALBEDO = texture(reflection_tex, uv).rgb * tint;
	SPECULAR = 0.0;
}
