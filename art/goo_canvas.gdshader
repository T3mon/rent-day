shader_type canvas_item;

// Shows the level's goo canvas. The canvas only says how much goo lies where (alpha), how
// bright it is (red) and the two colour seeds of the splat it came from (green, blue); this
// turns that into colour. Every splat gets its own fixed mix of two colours. Nothing cycles.
uniform bool all_red = false;  // dev setting: plain red blood instead
uniform float saturation = 0.85;

vec3 hsv_to_rgb(vec3 c) {
	vec3 p = abs(fract(c.xxx + vec3(1.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0);
	return c.z * mix(vec3(1.0), clamp(p - 1.0, 0.0, 1.0), c.y);
}

void fragment() {
	vec4 t = texture(TEXTURE, UV);
	if (t.a < 0.01)
		discard;
	vec3 goo = t.rgb / t.a; // painting faded brightness and seeds by coverage; undo that
	vec3 col;
	if (all_red) {
		col = mix(vec3(0.3, 0.0, 0.02), vec3(0.8, 0.04, 0.06), goo.r);
	} else {
		// The second colour sits within a third of the colour wheel of the first, so pairs stay friendly.
		vec3 a = hsv_to_rgb(vec3(goo.g, saturation, 0.95));
		vec3 b = hsv_to_rgb(vec3(fract(goo.g + (goo.b - 0.5) * 0.7), saturation, 0.8));
		col = mix(b, a, smoothstep(0.25, 0.9, goo.r)) * (0.55 + 0.45 * goo.r);
	}
	// The brightest spots catch the light, like wet goo.
	col = mix(col, vec3(1.0), smoothstep(0.92, 1.0, goo.r) * 0.35);
	COLOR = vec4(col, t.a);
}
