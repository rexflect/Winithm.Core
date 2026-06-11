shader_type canvas_item;


uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

uniform vec2 Center = vec2(0.5, 0.5);
uniform float Thickness = 0.05;
uniform float Force = 0.04;
uniform float Progress = 0.0;


void fragment() {
  vec2 uv = SCREEN_UV;
  float aspect = (1.0 / SCREEN_PIXEL_SIZE).x / (1.0 / SCREEN_PIXEL_SIZE).y;
  vec2 scaledUV = (uv - Center) * vec2(aspect, 1.0);
  float dist = length(scaledUV);

  float ring = abs(dist - Progress);
  float mask = smoothstep(Thickness, 0.0, ring);

  vec2 dir = normalize(scaledUV + vec2(0.0001));
  vec2 displaced = uv + dir * mask * Force;

  COLOR = texture(screen_texture, displaced);
}
