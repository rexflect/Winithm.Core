shader_type canvas_item;


uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

uniform float SplitX = 2.0;
uniform float SplitY = 2.0;

float applySplit(float coord, float n) {
  return fract((coord - 0.5) * n + 0.5);
}

void fragment() {
  vec2 uv = SCREEN_UV;
  float sx = max(SplitX, 1.0);
  float sy = max(SplitY, 1.0);

  float xCoord = applySplit(uv.x, sx);
  float yCoord = applySplit(uv.y, sy);

  COLOR = texture(screen_texture, vec2(xCoord, yCoord));
}
