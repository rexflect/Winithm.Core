shader_type canvas_item;

// The angle of the hinge in radians
uniform float rotate_x : hint_range(-1.57, 1.57) = 0.0; 
// Controls the depth of the 3D effect
uniform float perspective : hint_range(0.0001, 0.01) = 0.005; 

void vertex() {
	float y_offset = VERTEX.y; 
	float z = y_offset * sin(rotate_x);
	float w = 1.0 / (1.0 - z * perspective);
	
	VERTEX.x *= w;
	VERTEX.y = y_offset * cos(rotate_x) * w;
}

