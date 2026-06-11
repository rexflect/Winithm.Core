shader_type canvas_item;


uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

uniform vec2 Center = vec2(0.5, 0.5);
uniform float Radius = 0.3;
uniform float Strength = 0.5;


void fragment() {
  vec2 uv = SCREEN_UV;
  float aspect = (1.0 / SCREEN_PIXEL_SIZE).x / (1.0 / SCREEN_PIXEL_SIZE).y;
  vec2 delta = (uv - Center) * vec2(aspect, 1.0);
  float dist = length(delta);

  float mask = smoothstep(Radius, 0.0, dist);
  vec2 dir = delta / (dist + 0.0001);

  // Push pixels outward from center (magnify)
  vec2 displaced = uv - dir * mask * Strength * dist;

  COLOR = texture(screen_texture, displaced);
}
