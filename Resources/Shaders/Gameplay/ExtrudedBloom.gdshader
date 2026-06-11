shader_type canvas_item;


uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

uniform float LayerCount = 10.0;
uniform float Spread = 0.018;
uniform float BloomRadius = 0.012;
uniform float LumaThreshold = 0.12;
uniform float PerspStrength = 0.3;


const vec3 LUMA = vec3(0.299, 0.587, 0.114);

void fragment() {
  vec2 uv = SCREEN_UV;
  vec2 norm = normalize(vec2(0.5) - uv);
  float aspect = (1.0 / SCREEN_PIXEL_SIZE).x / (1.0 / SCREEN_PIXEL_SIZE).y;

  vec4 base = texture(screen_texture, uv);
  vec3 col = base.rgb;

  float layers = max(1.0, floor(LayerCount));
  float lumaLo = LumaThreshold - 0.04;
  float lumaHi = LumaThreshold + 0.10;

  for (int i = 1; i < 24; i++) {
    float fi = float(i);
    float active = step(fi, layers);
    float t = fi / layers;
    vec2 sampleUV = uv + norm * Spread * t * (1.0 + PerspStrength * t * 0.4);
    vec3 s = texture(screen_texture, sampleUV).rgb;
    float objectness = smoothstep(lumaLo, lumaHi, dot(s, LUMA));
    col += s * ((1.0 - t) * 0.7 * objectness * active);
  }

  vec3 bloom = vec3(0.0);
  vec2 br = vec2(BloomRadius / aspect, BloomRadius);
  bloom += texture(screen_texture, uv + br * vec2( 1.0,  0.0)).rgb;
  bloom += texture(screen_texture, uv + br * vec2(-1.0,  0.0)).rgb;
  bloom += texture(screen_texture, uv + br * vec2( 0.0,  1.0)).rgb;
  bloom += texture(screen_texture, uv + br * vec2( 0.0, -1.0)).rgb;
  bloom += texture(screen_texture, uv + br * vec2( 0.707,  0.707)).rgb;
  bloom += texture(screen_texture, uv + br * vec2(-0.707,  0.707)).rgb;
  bloom += texture(screen_texture, uv + br * vec2( 0.707, -0.707)).rgb;
  bloom += texture(screen_texture, uv + br * vec2(-0.707, -0.707)).rgb;
  bloom *= 0.125;

  float baseLuma = dot(base.rgb, LUMA);
  float bloomLuma = dot(bloom, LUMA);
  float bw = max(smoothstep(0.25, 0.75, baseLuma), smoothstep(LumaThreshold, 0.5, bloomLuma)) * 0.45;
  col += bloom * bw;

  COLOR = vec4(col, base.a);
}
