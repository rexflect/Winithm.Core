shader_type canvas_item;


uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

uniform float SampleCount = 3.0;
uniform float Strength = 0.01;


void fragment() {
  vec2 uv = SCREEN_UV;
  vec2 center = vec2(0.5, 0.5);
  vec2 dir = uv - center;
  
  float samples = max(1.0, floor(SampleCount));
  float r = 0.0;
  float g = 0.0;
  float b = 0.0;
  
  // Safety check for single sample
  if (samples < 1.1) {
    vec2 off = dir * Strength;
    r = texture(screen_texture, uv - off).r;
    g = texture(screen_texture, uv).g;
    b = texture(screen_texture, uv + off).b;
  } else {
    for (int i = 0; i < 32; i++) {
      if (float(i) >= samples) break;
      
      float t = float(i) / (samples - 1.0);
      float off_factor = t * Strength;
      
      r += texture(screen_texture, uv - dir * off_factor).r;
      g += texture(screen_texture, uv).g;
      b += texture(screen_texture, uv + dir * off_factor).b;
    }
    r /= samples;
    g /= samples;
    b /= samples;
  }
  
  COLOR = vec4(r, g, b, texture(screen_texture, uv).a);
}
