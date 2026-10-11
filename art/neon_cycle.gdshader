shader_type canvas_item;

// Slowly cycles colours around the rainbow, with a ripple across the world,
// so neon things (walls, goo) look like they're breathing.
uniform float speed = 0.12;    // full colour cycles per second
uniform float ripple = 0.003;  // colour shift per pixel of distance across the level

varying vec2 world_pos;

vec3 hue_rotate(vec3 c, float a) {
	const vec3 k = vec3(0.57735);
	float ca = cos(a);
	return c * ca + cross(k, c) * sin(a) + k * dot(k, c) * (1.0 - ca);
}

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

void fragment() {
	vec4 c = texture(TEXTURE, UV) * COLOR;
	float angle = TAU * (TIME * speed + (world_pos.x + world_pos.y) * ripple);
	COLOR = vec4(hue_rotate(c.rgb, angle), c.a);
}
