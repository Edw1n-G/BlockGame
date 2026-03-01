using Silk.NET.OpenGL; //Für die OpenGL Funktionen
using Basics.Setup; //Für die Color Klasse
using System.Drawing;
using System.Diagnostics;// Für Upload Limits
using Basics.Game;
using Silk.NET.Maths;

// Für die Kamera

namespace Basics.Graphics;

public class Renderer
{
    public static GL gl;
    
    public static ShaderManager terrainshader;
    public static TextureArray terrainTexture;
    private static Camera Camera;
    
    public static ChunkProvider ChunkProvider; // Referenz auf den Chunk-Verwalter
    
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
        terrainTexture = new TextureArray(gl, "texture/example.png");
    }

    ///<summary>
    /// Abstraktion für rendern
    /// jeder Chunk aus dem ChunkProvider wird durchgegangen und auf IsUploaded geprüft
    /// damit der Main-Thread die Daten an die GPU bringen kann,
    /// wenn PCIe Uploads zu lange dauern wird der nächste Frame gerendert und der Upload wird im nächsten Frame fortgesetzt
    /// Da ich gerade 16.6ms insgesammt habe benutze ich 5ms aber für schnelle gpus braucht man ein smarten upload timer
    ///<\summary>
    double maxUploadTimeMs = 5.0;
    Stopwatch _uploadTimer = Stopwatch.StartNew();
    
    public unsafe void Render()
    {
        Frustum frustum = terrainshader.Use(gl, Camera);
        terrainshader.BindTexture(terrainTexture);
        
        _uploadTimer.Restart();
        foreach (var chunk in ChunkProvider.GetLoadedChunks())
        {
            if (_uploadTimer.Elapsed.TotalMilliseconds > maxUploadTimeMs)
            {
                // Upload-Zeit überschritten, nächsten Frame rendern und Upload fortsetzen
                break;
            }
            
            // GPU-Upload auf dem Main-Thread falls noch nicht geschehen
            if (!chunk.IsUploaded)
                chunk.UploadToGpu(gl);
            
            if(!frustum.isInFrustum(chunk.ChunkPosition, frustum)) continue;
            
            chunk.Render(terrainshader);
        }
        _uploadTimer.Stop();
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
        ChunkProvider.Dispose();
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