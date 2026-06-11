shader_type canvas_item;


uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

uniform float Speed = 10.0;
uniform float Strength = 0.1;
uniform float Thickness = 0.2;
uniform float WaveW = 30.0;
uniform float Frequency = 5.0;
uniform float Degree = 0.0;


void fragment() {
  vec2 uv = SCREEN_UV;

  float timeSeed = TIME * Speed;

  // Calculate rotation trigonometry
  float rad = Degree * 3.14159265 / 180.0;
  float s = sin(rad);
  float c = cos(rad);

  // Rotate UV around center to find the local Axis (local Y = scrolling direction)
  vec2 centered = uv - vec2(0.5);
  float localY = -centered.x * s + centered.y * c + 0.5;

  // Calculate base scrolling position for shocklines based on rotated Y
  float wavePhase = localY * Frequency - timeSeed;
  float linePulse = sin(wavePhase) * 0.5 + 0.5; 
  
  // Thickness determines how wide the line is
  float lineMask = smoothstep(1.0 - Thickness * 0.4, 1.0, linePulse);

  // WaveW controls how jagged the interior is, based on rotated Y
  float waveOffset = sin(localY * WaveW + timeSeed * 2.0) * Strength * lineMask;
  
  // Displace along the local X axis (perpendicular to scrolling direction)
  vec2 dir = vec2(c, s);
  uv += dir * waveOffset;

  // Render texture and add flash glow based on mask
  vec4 color = texture(screen_texture, uv);
  color.rgb += lineMask * Strength * 0.8;

  COLOR = color;
}
