shader_type canvas_item;

uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform vec4 text_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float width = 100.0;
uniform bool is_bg = false;

varying float local_x;

void vertex() {
  // Pass local X coordinate to fragment shader to avoid per-glyph UV issues on Labels
  local_x = VERTEX.x;
}

void fragment() {
  // Calculate normalized X based on the actual width
  float normalized_x = local_x / max(width, 0.001);
  float mask = step(normalized_x, progress);
  
  if (is_bg) {
    // Background fill mode
    COLOR = mix(vec4(0.0), text_color, mask);
  } else {
    // Text invert mode
    float tex_alpha = texture(TEXTURE, UV).a;
    vec4 filled_text_color = vec4(1.0 - text_color.rgb, 1.0);
    vec4 txt = mix(text_color, filled_text_color, mask);
    COLOR = vec4(txt.rgb, tex_alpha);
  }
}
