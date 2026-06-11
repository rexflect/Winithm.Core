shader_type canvas_item;


uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

uniform float Strength = 0.5;


void fragment() {
  vec2 uv = SCREEN_UV;
  vec2 center = vec2(0.5);
  vec2 delta = uv - center;
  float dist = length(delta);

  float power = 1.0 + Strength * 2.0;
  float bind = 0.5;

  vec2 warped = center + normalize(delta) * bind * pow(dist / bind, power);
  warped = mix(uv, warped, step(0.001, dist));

  COLOR = texture(screen_texture, warped);
}
