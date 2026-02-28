#version 460 core

layout (location = 0) in vec3 aPos;
layout (location = 1) in float aLayer;
layout (location = 2) in float brightness;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;

out vec3 fragTexCoords;
out float fragbrightness;

void main()
{
    // Die UV-Koordinaten für die vier Ecken eines Quads
    vec2 quadUVs[4] = vec2[4](
    vec2(0.0, 0.0), // Vertex 0: Unten Links
    vec2(1.0, 0.0), // Vertex 1: Unten Rechts
    vec2(1.0, 1.0), // Vertex 2: Oben Rechts
    vec2(0.0, 1.0)  // Vertex 3: Oben Links
    );
    
    int cornerIndex = gl_VertexID % 4;
    
    vec2 uv = quadUVs[cornerIndex];
    
    gl_Position = uProjection * uView * uModel* vec4(aPos, 1.0);
    fragTexCoords = vec3(uv.x, uv.y, aLayer);
    fragbrightness = brightness;
}