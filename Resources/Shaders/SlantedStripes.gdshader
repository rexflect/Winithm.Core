shader_type canvas_item;

uniform vec4 bg_color : source_color = vec4(0.5, 0.5, 0.5, 1.0);
uniform vec4 stripe_color : source_color = vec4(0.0, 0.0, 0.0, 0.0);
uniform float speed = 10.0;
uniform float stripe_width = 10.0;
uniform float slant = -1.0;

void fragment() {
  vec2 pixel_size = 1.0 / SCREEN_PIXEL_SIZE;
  
  vec2 local_pos = UV / fwidth(UV); 
  
  float x_shifted = local_pos.x - local_pos.y * slant;
  x_shifted -= TIME * speed;
  float mod_val = mod(x_shifted, stripe_width * 2.0);
  
  if (mod_val < stripe_width) {
    COLOR = bg_color;
  } else {
    COLOR = stripe_color;
  }
}