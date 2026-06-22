shader_type canvas_item;
render_mode unshaded; // Disable 2D lighting for UI

uniform bool is_highlighted = false;
uniform float glow_intensity : hint_range(0.0, 5.0) = 1.5;
uniform float glow_radius : hint_range(0.0, 20.0) = 6.0;

// Use Screen Texture to get CanvasGroup's merged output
uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

void fragment() {
  // 1. Get the merged texture of the CanvasGroup
  vec4 base_color = textureLod(screen_texture, SCREEN_UV, 0.0);

  // 2. Un-premultiply alpha to restore real RGB values
  if (base_color.a > 0.0001) {
    base_color.rgb /= base_color.a;
  }

  if (is_highlighted) {
    float glow_alpha = 0.0;
    vec3 acc_color = vec3(0.0);
    float acc_alpha = 0.0;
    
    // Scale offset by SCREEN_PIXEL_SIZE
    vec2 px = SCREEN_PIXEL_SIZE * glow_radius;

    // Scan 8 directions to detect edges and collect colors
    const int GLOW_TAPS = 8;
    for (int i = 0; i < GLOW_TAPS; i++) {
      float angle = float(i) * 0.78539816; // 2 * PI / 8
      vec2 offset = vec2(cos(angle), sin(angle));

      vec4 s1 = textureLod(screen_texture, SCREEN_UV + offset * px, 0.0);
      vec4 s2 = textureLod(screen_texture, SCREEN_UV + offset * px * 0.5, 0.0);

      glow_alpha += s1.a + s2.a;

      // Accumulate premultiplied RGB to find the local dominant color
      acc_color += s1.rgb + s2.rgb;
      acc_alpha += s1.a + s2.a;
    }
    glow_alpha /= float(GLOW_TAPS * 2);

    // Calculate the dominant color of the glow
    vec3 glow_color = base_color.rgb; // Fallback to base color
    if (acc_alpha > 0.0001) {
      glow_color = acc_color / acc_alpha; // Un-premultiply the averaged color
    }

    // Smooth and boost the glow outline
    glow_alpha = smoothstep(0.0, 0.5, glow_alpha) * glow_intensity;

    // Mix colors: Original texture on top, extracted glow color underneath
    vec3 final_rgb = mix(glow_color, base_color.rgb, base_color.a);
    float final_alpha = max(base_color.a, clamp(glow_alpha, 0.0, 1.0));

    COLOR = vec4(final_rgb, final_alpha);
  } else {
    COLOR = base_color;
  }
}