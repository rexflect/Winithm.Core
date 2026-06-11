shader_type canvas_item;


uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

uniform float Size = 8.0;


void fragment() {
  vec2 pixels = (1.0 / SCREEN_PIXEL_SIZE) / max(Size, 1.0);
  vec2 uv = floor(SCREEN_UV * pixels) / pixels;
  COLOR = texture(screen_texture, uv);
}
