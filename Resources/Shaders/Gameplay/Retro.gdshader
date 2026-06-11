shader_type canvas_item;


uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

uniform float ScanlineIntensity = 0.3;
uniform float Curvature = 0.02;


void fragment() {
  vec2 uv = SCREEN_UV;

  // CRT barrel curvature
  vec2 centered = uv * 2.0 - 1.0;
  centered *= 1.0 + Curvature * dot(centered, centered);
  uv = centered * 0.5 + 0.5;

  // Clamp out-of-bounds to black
  float mask = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);

  vec4 color = texture(screen_texture, uv) * mask;

  // Scanlines
  float scanline = sin((uv.y * (1.0 / SCREEN_PIXEL_SIZE).y + TIME * 60.0) * 3.14159) * 0.5 + 0.5;
  color.rgb *= 1.0 - ScanlineIntensity * (1.0 - scanline);

  COLOR = color;
}
