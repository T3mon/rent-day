shader_type canvas_item;

// Paints one splat into the goo canvas. The canvas stores what the goo is, not its colour:
// red = brightness, green and blue = the splat's two colour seeds, alpha = how much goo. The clip mask has one texel
// per floor cell around the splat (1 = floor it can reach from where it landed), so it
// never spills through a wall into the next room.
uniform sampler2D clip : filter_nearest;
uniform vec2 clip_origin;     // world position of the mask's top-left corner
uniform vec2 clip_size;       // the mask's size in world pixels; zero = no walls nearby, no clipping

varying vec2 world;
varying vec4 tint;            // the stamp's modulate: green and blue carry the seeds, alpha the strength

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	tint = COLOR;
}

void fragment() {
	vec4 t = texture(TEXTURE, UV);
	float keep = 1.0;
	if (clip_size.x > 0.0) {
		vec2 uv = (world - clip_origin) / clip_size;
		keep = any(lessThan(uv, vec2(0.0))) || any(greaterThan(uv, vec2(1.0))) ? 0.0 : texture(clip, uv).r;
	}
	COLOR = vec4(t.r, tint.g, tint.b, t.a * tint.a * keep);
}
