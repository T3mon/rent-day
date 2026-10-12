shader_type canvas_item;

// Full-screen colour grade: the palette slowly drifts around the colour wheel
// and the corners darken. No warping, so the picture stays sharp and steady.
uniform sampler2D screen_texture : hint_screen_texture, filter_nearest;
uniform float drift = 0.04;     // palette drift around the colour wheel (0..1, 0 = off)
uniform float vignette = 0.45;  // how dark the corners get (0 = off)

vec3 hue_rotate(vec3 c, float a) {
	const vec3 k = vec3(0.57735);
	float ca = cos(a);
	return c * ca + cross(k, c) * sin(a) + k * dot(k, c) * (1.0 - ca);
}

void fragment() {
	vec3 col = texture(screen_texture, SCREEN_UV).rgb;
	col = hue_rotate(col, sin(TIME * 0.4) * drift * TAU);
	vec2 d = SCREEN_UV - 0.5;
	col *= 1.0 - vignette * dot(d, d) * 2.0;
	COLOR = vec4(col, 1.0);
}
