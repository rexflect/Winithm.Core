shader_type canvas_item;

uniform float grayscale_amount : hint_range(0.0, 1.0) = 0.0;

void fragment() {
    vec4 tex_color = texture(TEXTURE, UV) * COLOR;
    float luma = dot(tex_color.rgb, vec3(0.299, 0.587, 0.114));
    COLOR = vec4(mix(tex_color.rgb, vec3(luma), grayscale_amount), tex_color.a);
}
