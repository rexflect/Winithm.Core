shader_type canvas_item;

uniform bool is_highlighted = false;
uniform float glow_intensity : hint_range(0.0, 5.0) = 1.5;
uniform float glow_radius : hint_range(0.0, 20.0) = 6.0;

void fragment() {
  vec4 base_color = texture(TEXTURE, UV);

  if (is_highlighted) {
    // Calculate average color from a sparse grid sample of the texture
    // This provides a representative glow color from the actual note texture
    vec3 avg = vec3(0.0);
    float total_weight = 0.0;

    const int SAMPLES = 5;
    for (int x = 0; x < SAMPLES; x++) {
      for (int y = 0; y < SAMPLES; y++) {
        vec2 sample_uv = vec2(
          (float(x) + 0.5) / float(SAMPLES),
          (float(y) + 0.5) / float(SAMPLES)
        );
        vec4 s = texture(TEXTURE, sample_uv);
        float w = s.a; // Weight by alpha so transparent areas don't dilute color
        avg += s.rgb * w;
        total_weight += w;
      }
    }

    if (total_weight > 0.0) {
      avg /= total_weight;
    }

    // Multi-pass radial glow using alpha expansion
    float glow_alpha = 0.0;
    vec2 px = TEXTURE_PIXEL_SIZE * glow_radius;

    // 16-tap radial blur for smooth circular glow
    const int GLOW_TAPS = 8;
    for (int i = 0; i < GLOW_TAPS; i++) {
      float angle = float(i) * (3.14159265 * 2.0 / float(GLOW_TAPS));
      vec2 offset = vec2(cos(angle), sin(angle));

      glow_alpha += texture(TEXTURE, UV + offset * px).a;
      glow_alpha += texture(TEXTURE, UV + offset * px * 0.5).a;
    }
    glow_alpha /= float(GLOW_TAPS * 2);

    // Smooth falloff for soft edges
    glow_alpha = smoothstep(0.0, 0.5, glow_alpha) * glow_intensity;

    // Composite: glow behind, base on top (premultiplied alpha blend)
    vec3 glow_color = avg * glow_alpha;
    vec3 final_rgb = base_color.rgb * base_color.a + glow_color * (1.0 - base_color.a);
    float final_alpha = clamp(base_color.a + glow_alpha * (1.0 - base_color.a), 0.0, 1.0);

    COLOR = vec4(final_rgb, final_alpha);
  } else {
    COLOR = base_color;
  }
}