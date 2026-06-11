shader_type canvas_item;


uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

uniform float Amount = 1.0;


void fragment() {
  vec4 color = texture(screen_texture, SCREEN_UV);
  float luma = dot(color.rgb, vec3(0.299, 0.587, 0.114));
  color.rgb = mix(color.rgb, vec3(luma), Amount);
  COLOR = color;
}
