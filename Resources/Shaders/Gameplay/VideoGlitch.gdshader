shader_type canvas_item;


uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

uniform float Intensity = 0.2;
uniform float BlockScale = 20.0;
uniform float Speed = 20.0;
uniform float Rate = 0.3;


float rand(vec2 co) {
  return fract(sin(dot(co, vec2(12.9898, 78.233))) * 43758.5453);
}

void fragment() {
  vec2 uv = SCREEN_UV;
  
  float timeStep = floor(TIME * Speed); 

  vec2 blockUv = floor(uv * BlockScale) / BlockScale;
  
  float corruptProb = rand(blockUv + timeStep);
  float isCorrupt = step(1.0 - Rate, corruptProb);
  
  vec2 motionVector = vec2(
    rand(blockUv + timeStep * 1.5) - 0.5,
    rand(blockUv + timeStep * 2.5) - 0.5
  ) * Intensity * 0.2;
  
  vec2 displacedUV = clamp(uv + motionVector * isCorrupt, 0.0, 1.0);
  
  vec4 color = texture(screen_texture, displacedUV);
  
  float quantizeSteps = clamp(20.0 - Intensity * 15.0, 2.0, 20.0);
  vec3 quantColor = floor(color.rgb * quantizeSteps) / quantizeSteps;
  
  float extremeCorrupt = step(1.0 - (Intensity * 0.25), corruptProb);
  float r = texture(screen_texture, clamp(displacedUV + motionVector * 3.0, 0.0, 1.0)).r;
  float b = texture(screen_texture, clamp(displacedUV - motionVector * 3.0, 0.0, 1.0)).b;
  
  vec3 finalColor = mix(color.rgb, quantColor, isCorrupt * 0.5);
  finalColor.r = mix(finalColor.r, r, extremeCorrupt);
  finalColor.b = mix(finalColor.b, b, extremeCorrupt);
  
  COLOR = vec4(finalColor, color.a);
}
