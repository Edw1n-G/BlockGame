#version 460 core
            //Werte die pro fragment übergeben werden aus dem Vertex Shader
            in vec2 fragTexCoords;
            in float fragbrightness;
            
//Globale Feste Werte
uniform sampler2D uTexture;


out vec4 outColor;

void main()
{   
    
    //outColor = vec4(fragTexCoords.x,fragTexCoords.y, 0.0, 1.0);
    vec4 texColor = texture(uTexture, fragTexCoords);
    outColor = vec4(texColor.rgb * fragbrightness, texColor.a);
    
}