shader_type canvas_item;

// Under the death screen (ui/DeathScreen.cs): the floor zoomed in on your body and lit neon, keeping its own
// lights and darks but recoloured cyan around you, out to magenta and violet, and a little darker. On the hit,
// a white flash. `amount` brings the zoom, the colour and the dark in. Nothing comes out green: that's the crosshair's.
uniform sampler2D screen : hint_screen_texture, filter_nearest;
uniform float amount : hint_range(0.0, 1.0) = 0.0;
uniform float flash : hint_range(0.0, 1.0) = 0.0;
uniform vec2 body = vec2(0.5);           // your body, in screen UV
uniform vec2 size = vec2(400.0, 225.0);  // the screen, in game pixels (DeathScreen sets both)

const vec3 CYAN = vec3(0.176, 0.886, 0.902);
const vec3 MAGENTA = vec3(1.0, 0.169, 0.839);
const vec3 VIOLET = vec3(0.416, 0.122, 0.816);
const vec3 INK = vec3(0.027, 0.012, 0.059);

float lum(vec3 c) {
	return dot(c, vec3(0.3, 0.59, 0.11));
}

// The colour `c` at the brightness `l` (a paint program's "color" blend mode).
vec3 at_lum(vec3 c, float l) {
	c = c + vec3(l - lum(c));
	float m = lum(c);
	float lo = min(min(c.r, c.g), c.b);
	float hi = max(max(c.r, c.g), c.b);
	if (lo < 0.0)
		c = m + (c - m) * m / (m - lo);
	if (hi > 1.0)
		c = m + (c - m) * (1.0 - m) / (hi - m);
	return c;
}

void fragment() {
	vec2 at = body + (SCREEN_UV - body) / (1.0 + 0.14 * amount); // the zoom closes in on your body
	vec3 c = texture(screen, at).rgb;
	float t = clamp((length((at - body) * size) - 8.0) / 272.0, 0.0, 1.0); // 8 to 280 game pixels away
	vec3 glow = t < 0.3 ? mix(CYAN, MAGENTA, t / 0.3) : mix(MAGENTA, VIOLET, (t - 0.3) / 0.7);
	c = mix(c, at_lum(glow, lum(c)), 0.92 * amount);
	c = mix(c, INK, 0.3 * amount);
	COLOR = vec4(mix(c, vec3(1.0), flash), 1.0);
}
