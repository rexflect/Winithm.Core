shader_type canvas_item;


uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

uniform float Splits = 2.0;
uniform float Axis = 0.0;


void fragment() {
  vec2 uv = SCREEN_UV;
  float s = max(Splits, 1.0);

  // Axis: 0.0 = X split, 1.0 = Y split, anything between = blend
  float xCoord = mix(fract(uv.x * s), uv.x, step(0.5, Axis));
  float yCoord = mix(uv.y, fract(uv.y * s), step(0.5, Axis));

  COLOR = texture(screen_texture, vec2(xCoord, yCoord));
}
