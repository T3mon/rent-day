shader_type canvas_item;

// An afterimage (Afterimage): the sprite as a flat silhouette in its modulate colour.
varying vec4 tint;

void vertex() {
	tint = COLOR;
}

void fragment() {
	COLOR = vec4(tint.rgb, texture(TEXTURE, UV).a * tint.a);
}
