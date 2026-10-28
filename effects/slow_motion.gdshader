shader_type canvas_item;

// Slow motion over the level (Main lays it above the walls; you, bullets and afterimages draw over it):
// the colours washed a little toward blue, soft dark edges, faint scanlines, red and cyan pulled apart at
// the edges, like a worn tape. `amount` fades it in and out. Nothing comes out green: that's the crosshair's.
uniform sampler2D screen : hint_screen_texture, filter_nearest;
uniform float amount : hint_range(0.0, 1.0) = 0.0;
uniform vec2 game_pixel = vec2(0.0025, 0.00444); // one game pixel, in screen UV (Main sets it)

const vec3 NAVY = vec3(0.039, 0.055, 0.157);
const vec3 BLUE = vec3(0.157, 0.353, 0.745);
const vec3 PALE = vec3(0.784, 0.961, 1.0);
const vec3 DARK = vec3(0.016, 0.031, 0.102);

// 0 in the middle of the screen, rising toward the edges past `start`.
float edge(vec2 uv, float start, float strength) {
	vec2 d = (uv - 0.5) * 2.0;
	return clamp((d.x * d.x * 0.8 + d.y * d.y - start) * strength, 0.0, 1.0);
}

void fragment() {
	vec2 uv = SCREEN_UV;
	vec3 c = texture(screen, uv).rgb;
	vec2 shift = vec2(game_pixel.x, 0.0);
	vec3 split = vec3(texture(screen, uv - shift).r, texture(screen, uv + shift).gb);
	c = mix(c, split, edge(uv, 0.65, 1.0) * amount);
	float lum = dot(c, vec3(0.299, 0.587, 0.114));
	vec3 tape = lum < 0.5 ? mix(NAVY, BLUE, lum * 2.0) : mix(BLUE, PALE, lum * 2.0 - 1.0);
	c = mix(c, tape, 0.3 * amount);
	c = mix(c, DARK, edge(uv, 0.6, 0.7) * amount);
	if (mod(floor(uv.y / game_pixel.y), 2.0) > 0.5)
		c *= 1.0 - 0.08 * amount;
	c.g = mix(c.g, min(c.g, max(c.r, c.b)), amount);
	COLOR = vec4(c, 1.0);
}
