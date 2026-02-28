using Silk.NET.OpenGL; //Für die OpenGL Funktionen
using Basics.Setup; //Für die Color Klasse
using System.Drawing;
using StbImageSharp;
using System.IO;
using System.Numerics;
using Basics.Game;
using Basics.Utilities;
using Silk.NET.Maths;

// Für die Kamera

namespace Basics.Graphics;

public class Renderer
{
    public static GL gl;
    
    public static ShaderManager terrainshader;
    public static Texture terrainTexture;
    private static Camera Camera;
    
    public static ChunkProvidor ChunkProvidor; // Referenz auf den Chunk-Verwalter
    
    /**
     * Setup Methode, alles was man fürs Rendern braucht.
     * Window, Camera, Shader und testchunks
     */
    public unsafe void Setup(Camera camera)
    {
        gl = WindowSetup.window.CreateOpenGL();
        gl.ClearColor(Color.CornflowerBlue);
        
        Camera = camera;
        
        terrainshader = new ShaderManager(gl, "shader.vert", "shader.frag");
        terrainTexture = new Texture(gl, "texture/example.png");
    }

    /**
     * Abstraktion für rendern
     * jeder Chunk in und der Welt wird gerendert.
     * Chunks, die noch nicht auf der GPU sind, werden hier hochgeladen
     * damit der Main-Thread die Daten an die GPU bringen kann
     */
    public unsafe void Render()
    {
        Frustum frustum = terrainshader.Use(gl, Camera);
        terrainshader.BindTexture(terrainTexture);
        
        foreach (var chunk in ChunkProvidor.GetLoadedChunks())
        {
            // GPU-Upload auf dem Main-Thread falls noch nicht geschehen
            if (!chunk.IsUploaded)
                chunk.UploadToGpu(gl);
            
            if(!frustum.isInFrustum(chunk.ChunkPosition, frustum)) continue;
            
            chunk.Render(terrainshader);
        }
    }
    
    /**
     * Color und Depth Buffer löschen, damit der vorherige Frame nicht mehr sichtbar ist.
     */
    public void Clear()
    {
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    }
    
    /**
     * Jeden Shader und Chunk gefolgt vom GL Kontext entfernen
     */
    public void Dispose()
    {
        terrainshader.Dispose();
        terrainTexture.Dispose();
        ChunkProvidor.Dispose();
        gl.Dispose();
    }
    
    /**
     * Fenstergröße weitergeben damit die Viewportgröße angepasst werden kann.
     */
    public void FramebufferResize(Vector2D<int> size)
    {
        gl.Viewport(size);
        Camera.AspectRatio = (float)size.X / size.Y;
    }
}