shader_type canvas_item;

// Neon goo. The texture only says how bright and how solid the goo is;
// this shader paints it with rainbow colours that flow across the splat
// and drift over time. Every splat gets its own random seed.
uniform float seed = 0.0;      // set randomly per splat by Goo.cs
uniform float swirl = 0.035;   // colour change per pixel across the splat
uniform float speed = 0.08;    // colour drift per second
uniform float saturation = 0.9;

varying vec2 local_pos;

vec3 hsv_to_rgb(vec3 c) {
	vec3 p = abs(fract(c.xxx + vec3(1.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0);
	return c.z * mix(vec3(1.0), clamp(p - 1.0, 0.0, 1.0), c.y);
}

void vertex() {
	local_pos = VERTEX;
}

void fragment() {
	vec4 t = texture(TEXTURE, UV);
	float hue = fract(seed + TIME * speed
		+ (local_pos.x * 0.8 + local_pos.y * 0.5) * swirl
		+ sin(local_pos.y * 0.35 + TIME * 1.3) * 0.04);
	vec3 col = hsv_to_rgb(vec3(hue, saturation, 1.0)) * (0.35 + t.r);
	// The brightest spots go almost white, like light catching wet goo.
	col = mix(col, vec3(1.0), smoothstep(0.9, 1.0, t.r) * 0.55);
	COLOR = vec4(col, t.a * COLOR.a);
}
