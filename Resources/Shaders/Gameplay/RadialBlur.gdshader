shader_type canvas_item;


uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

uniform vec2 Center = vec2(0.5, 0.5);
uniform float Strength = 0.05;
uniform float Samples = 10.0;


void fragment() {
  vec2 uv = SCREEN_UV;
  
  // Vector from center to current pixel
  vec2 dir = uv - Center;
  
  float sampleCount = max(floor(Samples), 1.0);
  vec4 sum = vec4(0.0);
  
  for(float i = 0.0; i < 32.0; i += 1.0) {
    if(i >= sampleCount) break;
    
    // Smooth stepping from 0.0 to 1.0
    float t = i / max(sampleCount - 1.0, 1.0); 
    
    // Pull samples inwards to the center relative to Strength
    vec2 offset = dir * Strength * t;
    sum += texture(screen_texture, uv - offset);
  }
  
  COLOR = sum / sampleCount;
}
