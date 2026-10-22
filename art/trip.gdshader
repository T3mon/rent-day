shader_type canvas_item;

// Full-screen finish: the corners darken. No warping and no colour changes, so the
// picture stays sharp and steady. Black, more opaque toward the corners: the same as
// darkening the picture, without copying the screen to read it back every frame.
uniform float vignette = 0.45;  // how dark the corners get (0 = off)

void fragment() {
	vec2 d = SCREEN_UV - 0.5;
	COLOR = vec4(0.0, 0.0, 0.0, vignette * dot(d, d) * 2.0);
}
