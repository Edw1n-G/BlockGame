#version 330 core
            in vec2 fragTexCoords;

uniform sampler2D uTexture;

out vec4 outColor;

void main()
{
    //outColor = vec4(fragTexCoords.x,fragTexCoords.y, 0.0, 1.0);
    outColor = texture(uTexture, fragTexCoords);
}