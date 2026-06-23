shader_type canvas_item;
render_mode unshaded; // Disable 2D lighting for UI

uniform bool is_highlighted = false;
uniform float glow_intensity : hint_range(0.0, 5.0) = 1.5;
uniform float glow_radius : hint_range(0.0, 20.0) = 6.0;

// Use Screen Texture to get CanvasGroup's merged output
uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

// Pre-calculate the 8 directional vectors. 
// This completely removes the expensive sin() and cos() calculations from the fragment shader.
const vec2 OFFSETS[8] = vec2[8](
  vec2(1.0, 0.0),
  vec2(0.707106, 0.707106),
  vec2(0.0, 1.0),
  vec2(-0.707106, 0.707106),
  vec2(-1.0, 0.0),
  vec2(-0.707106, -0.707106),
  vec2(0.0, -1.0),
  vec2(0.707106, -0.707106)
);

void fragment() {
  // 1. Get the merged texture of the CanvasGroup
  vec4 base_color = textureLod(screen_texture, SCREEN_UV, 0.0);

  // Early exit using a flipped condition. If not highlighted, skip all heavy calculations.
  if (!is_highlighted) {
    COLOR = base_color;
  } else {
    // 2. Branchless un-premultiply alpha. 
    // max() prevents division by zero without needing an expensive 'if' statement.
    base_color.rgb /= max(base_color.a, 0.00001);

    // Vectorized accumulator: rgb = color, a = alpha.
    // Calculating them together uses the GPU's SIMD architecture much more efficiently.
    vec4 acc = vec4(0.0);
    
    vec2 px = SCREEN_PIXEL_SIZE * glow_radius;
    vec2 half_px = px * 0.5;

    // Because the loop size is fixed (8) and reads from a constant array, 
    // the shader compiler will easily "unroll" this loop for maximum speed.
    for (int i = 0; i < 8; i++) {
      vec2 offset = OFFSETS[i];
      
      vec4 s1 = textureLod(screen_texture, SCREEN_UV + offset * px, 0.0);
      vec4 s2 = textureLod(screen_texture, SCREEN_UV + offset * half_px, 0.0);

      // Add both colors and alphas simultaneously
      acc += s1 + s2;
    }

    // Multiply is significantly faster than division ( dividing by 16 is multiplying by 0.0625 )
    float glow_alpha = acc.a * 0.0625;

    // Branchless fallback logic:
    // Extract the glow color. If acc.a is 0, this results in black.
    vec3 extracted_glow = acc.rgb / max(acc.a, 0.00001);
    
    // step() returns 0.0 if acc.a < 0.00001, and 1.0 otherwise. 
    // We use this to mix the fallback base_color branchlessly.
    float has_glow = step(0.00001, acc.a);
    vec3 glow_color = mix(base_color.rgb, extracted_glow, has_glow);

    // Smooth and boost the glow outline
    glow_alpha = smoothstep(0.0, 0.5, glow_alpha) * glow_intensity;

    // Mix colors: Original texture on top, extracted glow color underneath
    vec3 final_rgb = mix(glow_color, base_color.rgb, base_color.a);
    float final_alpha = max(base_color.a, clamp(glow_alpha, 0.0, 1.0));

    COLOR = vec4(final_rgb, final_alpha);
  }
}