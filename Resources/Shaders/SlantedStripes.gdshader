shader_type canvas_item;

uniform vec4 stripe_color : source_color = vec4(0.1, 0.1, 0.1, 1.0);
uniform vec4 bg_color : source_color = vec4(0.0, 0.0, 0.0, 1.0);

uniform float speed = 10.0;
uniform float stripe_width = 15.0;
uniform float stripe_gap = 5.0;
uniform float slant = -1.0;

void fragment() {
  vec2 local_pos = UV / fwidth(UV);

  float x_shifted = local_pos.x - local_pos.y * slant;
  x_shifted -= TIME * speed;

  float cycle = stripe_width + stripe_gap;
  float mod_val = mod(x_shifted, cycle);

  COLOR = mod_val < stripe_width ? stripe_color : bg_color;
}