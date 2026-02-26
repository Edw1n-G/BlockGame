using Silk.NET.OpenGL; //Für die OpenGL Funktionen
using Basics.Setup; //Für die Color Klasse
using System.Drawing;
using StbImageSharp;
using System.IO;
using System.Numerics;
using Basics.Game;
using Silk.NET.Maths;

// Für die Kamera

namespace Basics.Graphics;

public class Renderer
{
    public static GL gl;
    
    public static ShaderManager terrainshader;
    public static Texture terrainTexture;
    public static Camera PlayerCamera;
    public static ChunkProvidor ChunkProvidor; // Referenz auf den Chunk-Verwalter
    
    /**
     * Setup Methode, alles was man fürs Rendern braucht.
     * Window, Camera, Shader und testchunks
     */
    public static unsafe void Setup()
    {
        gl = WindowSetup.window.CreateOpenGL();
        gl.ClearColor(Color.CornflowerBlue);
        
        PlayerCamera = new Camera(new Vector3(0.0f, 33.0f, 0.0f));
        
        terrainshader = new ShaderManager(gl, "shader.vert", "shader.frag");
        terrainTexture = new Texture(gl, "texture/example.png");
    }

    /**
     * Abstraktion für Rendern
     * Jeder Chunk in und der Welt wird gerendert.
     */
    public static unsafe void Render()
    {
        terrainshader.Use(gl, PlayerCamera);
        terrainshader.BindTexture(terrainTexture);

        foreach (var chunk in ChunkProvidor.GetLoadedChunks())
        {
            chunk.Render(terrainshader);
        }
    }
    
    /**
     * Color und Depth Buffer löschen, damit der vorherige Frame nicht mehr sichtbar ist.
     */
    public static void Clear()
    {
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    }
    
    /**
     * Jeden Shader und Chunk gefolgt vom GL Kontext entfernen
     */
    public static void Dispose()
    {
        terrainshader.Dispose();
        terrainTexture.Dispose();
        ChunkProvidor.Dispose();
        gl.Dispose();
    }
    
    /**
     * Fenstergröße weitergeben damit die Viewportgröße angepasst werden kann.
     */
    public static void FramebufferResize(Vector2D<int> size)
    {
        gl.Viewport(size);
    }
}