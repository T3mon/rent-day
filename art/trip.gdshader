shader_type canvas_item;

// Full-screen finish: the corners darken. No warping and no colour changes, so the
// picture stays sharp and steady.
uniform sampler2D screen_texture : hint_screen_texture, filter_nearest;
uniform float vignette = 0.45;  // how dark the corners get (0 = off)

void fragment() {
	vec3 col = texture(screen_texture, SCREEN_UV).rgb;
	vec2 d = SCREEN_UV - 0.5;
	col *= 1.0 - vignette * dot(d, d) * 2.0;
	COLOR = vec4(col, 1.0);
}
