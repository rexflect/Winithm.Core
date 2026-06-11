shader_type canvas_item;


uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

uniform float Strength = 0.01;
uniform float Angle = 0.0;
uniform float Samples = 8.0;


void fragment() {
  float rad = Angle * 3.14159265 / 180.0;
  vec2 dir = vec2(cos(rad), sin(rad)) * Strength;
  float sampleCount = max(floor(Samples), 1.0);

  vec4 sum = vec4(0.0);
  for (float i = 0.0; i < 32.0; i += 1.0) {
    if (i >= sampleCount) break;
    float t = (i / (sampleCount - 1.0)) - 0.5;
    sum += texture(screen_texture, SCREEN_UV + dir * t);
  }

  COLOR = sum / sampleCount;
}
