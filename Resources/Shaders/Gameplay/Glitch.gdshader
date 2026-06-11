shader_type canvas_item;


uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

uniform float Power = 0.1;
uniform float Rate = 0.2;
uniform float Speed = 15.0;
uniform float BlockCount = 30.0;
uniform float ColorRate = 0.02;


float rand(vec2 co) {
  return fract(sin(dot(co, vec2(12.9898, 78.233))) * 43758.5453);
}

void fragment() {
  vec2 uv = SCREEN_UV;

  float blockY = floor(uv.y * BlockCount);
  float timeSeed = floor(TIME * Speed);
  float noise = rand(vec2(blockY, timeSeed));
  float glitchStrength = step(1.0 - Rate, noise);

  float offset = (rand(vec2(blockY, timeSeed + 10.0)) - 0.5) * 2.0 * Power * glitchStrength;

  vec2 displaced = vec2(uv.x + offset, uv.y);

  float chromaticOff = ColorRate * glitchStrength;
  float r = texture(screen_texture, displaced + vec2(chromaticOff, 0.0)).r;
  float g = texture(screen_texture, displaced).g;
  float b = texture(screen_texture, displaced - vec2(chromaticOff, 0.0)).b;

  COLOR = vec4(r, g, b, texture(screen_texture, uv).a);
}
