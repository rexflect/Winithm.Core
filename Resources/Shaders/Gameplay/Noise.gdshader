shader_type canvas_item;


uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

uniform float Amount = 0.15;


float rand(vec2 co) {
  return fract(sin(dot(co, vec2(12.9898, 78.233))) * 43758.5453);
}

void fragment() {
  vec4 color = texture(screen_texture, SCREEN_UV);
  float grain = rand(SCREEN_UV * (1.0 / SCREEN_PIXEL_SIZE) + vec2(TIME * 100.0)) * 2.0 - 1.0;
  color.rgb += grain * Amount;
  COLOR = color;
}
