shader_type canvas_item;


uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

uniform float Flash = 0.0;
uniform float Threshold = 0.8;
uniform float Intensity = 1.0;
uniform vec4 Color : source_color = vec4(1.0, 1.0, 1.0, 1.0);


void fragment() {
  vec4 tex_color = texture(screen_texture, SCREEN_UV);
  
  float luminance = dot(tex_color.rgb, vec3(0.299, 0.587, 0.114));
  
  float contribution = step(Threshold, luminance);
  vec3 bright_areas = tex_color.rgb * contribution;
  
  vec3 final_bloom = bright_areas * Color.rgb * Intensity;
  vec3 flash_effect = vec3(Flash);
  
  COLOR = vec4(tex_color.rgb + final_bloom + flash_effect, tex_color.a);
}
