shader_type canvas_item;
uniform vec4 color_left : source_color;
uniform vec4 color_right : source_color;
uniform float progress = 0.0;
uniform float gradient_width = 0.05;

uniform float speed = 20.0;
uniform float stripe_width = 12.0;
uniform float slant = -0.26795;
uniform vec2 rect_size = vec2(100.0, 15.0);

void fragment() {
  float safe_progress = max(progress, 0.0001);
  
  vec2 local_pos = UV * rect_size;
  float x_shifted = local_pos.x - local_pos.y * slant;
  x_shifted -= TIME * speed;
  float mod_val = mod(x_shifted, stripe_width * 2.0);
  
  vec4 stripe_c;
  if (mod_val < stripe_width) {
    stripe_c = color_left;
  } else {
    stripe_c = color_right;
  }
  
  if (UV.x > progress) {
    COLOR = vec4(0.0);
  } else {
    COLOR = stripe_c;
  }
}
