shader_type canvas_item;

// Full-screen trip filter: the picture sways, colours split into red/blue fringes,
// and the whole palette slowly drifts. Keep it subtle or aiming gets hard.
uniform sampler2D screen_texture : hint_screen_texture, filter_nearest;
uniform float sway = 0.0015;        // how far the picture wobbles (fraction of the screen)
uniform float color_split = 0.0012; // width of the red/blue fringe
uniform float drift = 0.05;         // how far colours drift around the colour wheel (0..1)

vec3 hue_rotate(vec3 c, float a) {
	const vec3 k = vec3(0.57735);
	float ca = cos(a);
	return c * ca + cross(k, c) * sin(a) + k * dot(k, c) * (1.0 - ca);
}

void fragment() {
	vec2 uv = SCREEN_UV;
	uv.x += sin(uv.y * 20.0 + TIME * 1.6) * sway;
	uv.y += cos(uv.x * 16.0 + TIME * 1.2) * sway;
	float r = texture(screen_texture, uv + vec2(color_split, 0.0)).r;
	float g = texture(screen_texture, uv).g;
	float b = texture(screen_texture, uv - vec2(color_split, 0.0)).b;
	COLOR = vec4(hue_rotate(vec3(r, g, b), sin(TIME * 0.4) * drift * TAU), 1.0);
}
