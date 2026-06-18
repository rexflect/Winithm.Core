shader_type canvas_item;

uniform float speed = 10.0;
uniform float stripe_width = 10.0;
uniform float slant = -1.0;

void fragment() {
  vec2 local_pos = UV / fwidth(UV);

  float x_shifted = local_pos.x - local_pos.y * slant;
  x_shifted -= TIME * speed;
  float mod_val = mod(x_shifted, stripe_width * 2.0);

  COLOR = mod_val < stripe_width ? COLOR : vec4(0.0);
}