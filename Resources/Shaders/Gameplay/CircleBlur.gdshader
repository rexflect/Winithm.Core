shader_type canvas_item;


uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

uniform float Radius = 3.0;


void fragment() {
  vec2 texel = 1.0 / (1.0 / SCREEN_PIXEL_SIZE);
  vec4 sum = vec4(0.0);
  float total = 0.0;
  float rad = floor(Radius);

  for (float y = -16.0; y <= 16.0; y += 1.0) {
    if (abs(y) > rad) continue;
    for (float x = -16.0; x <= 16.0; x += 1.0) {
      if (abs(x) > rad) continue;
      float weight = 1.0 - length(vec2(x, y)) / (rad + 1.0);
      weight = max(weight, 0.0);
      sum += texture(screen_texture, SCREEN_UV + vec2(x, y) * texel) * weight;
      total += weight;
    }
  }

  COLOR = sum / total;
}
