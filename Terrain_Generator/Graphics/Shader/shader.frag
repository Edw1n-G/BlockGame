#version 460 core
            //Werte die pro fragment übergeben werden aus dem Vertex Shader
            in vec2 fragTexCoords;
            in float fragbrightness;
            in vec3 WorldPos;
//Globale Feste Werte
uniform sampler2D uTexture;
uniform vec3 u_PlayerCameraPos;
uniform bool u_UseDebugLOD;


out vec4 outColor;

void main()
{   
    // AI helped me, I have no clue about glsl
    if (u_UseDebugLOD)
    {
        // 1. Distanz vom aktuellen Pixel zur SPIELER-Kamera berechnen
        float dist = distance(WorldPos, u_PlayerCameraPos);

        // 2. Distanz in ein Mipmap-Level umrechnen. 
        // Die Werte (z.B. alle 16 Einheiten ein neues Level) musst du an deine Welt anpassen.
        // log2 ist eine gute Annäherung an die echte Hardware-Berechnung.
        float mipmapLevel = max(0.0, log2(dist / 18.0)); // 16.0 ist hier die "Basisgröße" für Level 0, anpassen je nach Texturgröße

        // 3. Textur mit ERZWUNGENEM Mipmap-Level auslesen
        vec4 texColor = textureLod(uTexture, fragTexCoords, mipmapLevel);
        outColor = vec4(texColor.rgb * fragbrightness, texColor.a);

        // OPTIONAL: Um es extrem gut sichtbar zu machen, kannst du die 
        // Mipmap-Level als Farben über die Blöcke legen!
        /*
        if (mipmapLevel < 1.0) FragColor *= vec4(1.0, 1.0, 1.0, 1.0);      // Level 0: Normal
        else if (mipmapLevel < 2.0) FragColor *= vec4(1.0, 0.5, 0.5, 1.0); // Level 1: Rot
        else if (mipmapLevel < 3.0) FragColor *= vec4(0.5, 1.0, 0.5, 1.0); // Level 2: Grün
        else FragColor *= vec4(0.5, 0.5, 1.0, 1.0);                        // Level 3+: Blau
        */
    }
    else{
        //outColor = vec4(fragTexCoords.x,fragTexCoords.y, 0.0, 1.0);
        vec4 texColor = texture(uTexture, fragTexCoords);
        outColor = vec4(texColor.rgb * fragbrightness, texColor.a);
    }
}