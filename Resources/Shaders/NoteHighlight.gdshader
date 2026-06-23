shader_type canvas_item;
render_mode unshaded;

uniform bool is_highlighted = false;
uniform float glow_intensity : hint_range(0.0, 5.0) = 1.5;
uniform float glow_radius : hint_range(0.0, 20.0) = 6.0;

uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

// Precomputed cos/sin for 8 directions (0, 45, 90, 135, 180, 225, 270, 315 deg).
// Avoids runtime trig per fragment.
const vec2 TAP_DIRS[8] = vec2[8](
  vec2( 1.0,       0.0      ),
  vec2( 0.707107,  0.707107 ),
  vec2( 0.0,       1.0      ),
  vec2(-0.707107,  0.707107 ),
  vec2(-1.0,       0.0      ),
  vec2(-0.707107, -0.707107 ),
  vec2( 0.0,      -1.0      ),
  vec2( 0.707107, -0.707107 )
);

void fragment() {
  vec4 base = textureLod(screen_texture, SCREEN_UV, 0.0);

  // Un-premultiply once; guard avoids division by near-zero
  float inv_base_a = base.a > 0.0001 ? 1.0 / base.a : 0.0;
  base.rgb *= inv_base_a;

  if (is_highlighted) {
    vec2 px = SCREEN_PIXEL_SIZE * glow_radius;
    vec2 px_half = px * 0.5;

    float acc_alpha = 0.0;
    vec3  acc_rgb   = vec3(0.0);

    // 8 directions × 2 distances = 16 taps.
    // acc_alpha serves both glow_alpha and color weight — no dual accumulation.
    for (int i = 0; i < 8; i++) {
      vec2 dir = TAP_DIRS[i];
      vec4 s1 = textureLod(screen_texture, SCREEN_UV + dir * px,      0.0);
      vec4 s2 = textureLod(screen_texture, SCREEN_UV + dir * px_half, 0.0);
      acc_alpha += s1.a + s2.a;
      acc_rgb   += s1.rgb + s2.rgb;
    }

    // Normalize: 8 directions × 2 = 16 taps
    acc_alpha *= 0.0625; // 1/16

    // Recover dominant neighbor color from premultiplied sum
    float inv_acc = (acc_alpha * 16.0) > 0.0001 ? 1.0 / (acc_alpha * 16.0) : 0.0;
    vec3 glow_color = acc_alpha > 0.0001 ? acc_rgb * inv_acc : base.rgb;

    float glow_out = smoothstep(0.0, 0.5, acc_alpha) * glow_intensity;

    COLOR = vec4(
      mix(glow_color, base.rgb, base.a),
      max(base.a, clamp(glow_out, 0.0, 1.0))
    );
  } else {
    COLOR = base;
  }
}
