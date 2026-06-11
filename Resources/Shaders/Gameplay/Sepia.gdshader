shader_type canvas_item;


uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

uniform float Amount = 1.0;


void fragment() {
  vec4 color = texture(screen_texture, SCREEN_UV);
  float luma = dot(color.rgb, vec3(0.299, 0.587, 0.114));
  vec3 sepia = vec3(luma * 1.2, luma * 1.0, luma * 0.8);
  color.rgb = mix(color.rgb, sepia, Amount);
  COLOR = color;
}
