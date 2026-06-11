shader_type canvas_item;


uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

uniform float Amount = 1.0;


void fragment() {
  vec4 color = texture(screen_texture, SCREEN_UV);
  color.rgb = mix(color.rgb, 1.0 - color.rgb, Amount);
  COLOR = color;
}
