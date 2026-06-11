shader_type canvas_item;


uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

uniform vec2 Center = vec2(0.5, 0.5);
uniform float Radius = 0.3;
uniform float Feather = 0.1;
uniform float Darken = 0.8;


void fragment() {
  vec2 uv = SCREEN_UV;
  float aspect = (1.0 / SCREEN_PIXEL_SIZE).x / (1.0 / SCREEN_PIXEL_SIZE).y;
  vec2 delta = (uv - Center) * vec2(aspect, 1.0);
  float dist = length(delta);

  float circle = smoothstep(Radius, Radius + Feather, dist);

  vec4 color = texture(screen_texture, uv);
  color.rgb *= 1.0 - circle * Darken;

  COLOR = color;
}
