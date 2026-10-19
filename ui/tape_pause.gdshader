shader_type canvas_item;

// Behind the Esc menu: the frozen floor washed out to grey with a cold purple tint, like a paused tape.
uniform sampler2D screen : hint_screen_texture, filter_nearest;

void fragment() {
	vec3 c = texture(screen, SCREEN_UV).rgb;
	float grey = dot(c, vec3(0.299, 0.587, 0.114));
	COLOR = vec4(grey * vec3(0.478, 0.4, 0.722) * 0.7, 1.0);
}
