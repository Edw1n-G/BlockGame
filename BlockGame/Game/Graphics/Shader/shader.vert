#version 460 core

layout (location = 0) in vec3 aPos;
layout (location = 1) in uint aLayer;   // Textur-Layer als uint16
layout (location = 2) in int aAoLevel;  // AO Level 0-3 als byte (int8)

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;

out vec3 fragTexCoords;
out float fragbrightness;

// AO Lookup: Index 0=hell, 3=dunkel
const float aoLookup[4] = float[4](1.0, 0.8, 0.6, 0.4);

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
    fragTexCoords = vec3(uv.x, uv.y, float(aLayer));
    fragbrightness = aoLookup[aAoLevel];
}