shader_type canvas_item;


uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

uniform float Smoothness = 0.4;
uniform float Radius = 0.8;
uniform vec4 Color : source_color = vec4(0.0, 0.0, 0.0, 1.0);


void fragment() {
  vec4 tex = texture(screen_texture, SCREEN_UV);
  vec2 uv = SCREEN_UV;
  float dist = length(uv - vec2(0.5));

  float vig = smoothstep(Radius, Radius - Smoothness, dist);
  vec3 result = mix(Color.rgb, tex.rgb, vig);

  COLOR = vec4(result, tex.a);
}
