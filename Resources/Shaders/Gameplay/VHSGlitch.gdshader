shader_type canvas_item;


uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

uniform float Intensity = 0.2;
uniform float JitterSpeed = 2.0;


float rand(vec2 co) {
  return fract(sin(dot(co, vec2(12.9898, 78.233))) * 43758.5453);
}

void fragment() {
  vec2 uv = SCREEN_UV;

  // Tracking line jitter
  float lineY = fract(TIME * JitterSpeed * 0.1);
  float lineDist = abs(uv.y - lineY);
  float lineEffect = smoothstep(0.02, 0.0, lineDist) * Intensity;
  uv.x += lineEffect * (rand(vec2(uv.y, floor(TIME * 30.0))) - 0.5);

  // Tape wobble
  float wobble = sin(uv.y * 50.0 + TIME * JitterSpeed * 10.0) * Intensity * 0.003;
  uv.x += wobble;

  // Chromatic aberration split
  float aberration = Intensity * 0.01;
  float r = texture(screen_texture, uv + vec2(aberration, 0.0)).r;
  float g = texture(screen_texture, uv).g;
  float b = texture(screen_texture, uv - vec2(aberration, 0.0)).b;

  vec3 color = vec3(r, g, b);

  // Noise grain
  float noise = rand(uv * (1.0 / SCREEN_PIXEL_SIZE) + vec2(TIME * 77.0)) * Intensity * 0.3;
  color += noise;

  COLOR = vec4(color, texture(screen_texture, SCREEN_UV).a);
}
