using Silk.NET.OpenGL; //Für die OpenGL Funktionen
using Basics.Setup; //Für die Color Klasse
using System.Drawing;
using System.Diagnostics;// Für Upload Limits
using Basics.Game;
using Basics.Game.TerrainManaging;
using Silk.NET.Maths;

namespace Basics.Graphics;

public class Renderer
{
    private static GL _gl = null!;

    private static ShaderManager _terrainshader = null!;
    private static TextureArray _terrainTexture = null!;
    private static Camera _camera = null!;
    
    public static ChunkProvider ChunkProvider = null!; // Referenz auf den Chunk-Verwalter
    
    /**
     * Setup Methode, alles was man fürs Rendern braucht.
     * Window, Camera, Shader und testchunks
     */
    public unsafe void Setup(Camera camera)
    {
        _gl = WindowSetup.Window.CreateOpenGL();
        _gl.ClearColor(Color.CornflowerBlue);
        
        _camera = camera;
        
        _terrainshader = new ShaderManager(_gl, "shader.vert", "shader.frag");
        _terrainTexture = new TextureArray(_gl, "texture/example.png");
    }


    private const double MaxUploadTimeMs = 3.0;
    private readonly Stopwatch _uploadTimer = Stopwatch.StartNew();
    
    ///<summary>
    /// Abstraktion für rendern
    /// jeder Chunk aus dem ChunkProvider wird durchgegangen und auf IsUploaded geprüft
    /// damit der Main-Thread die Daten an die GPU bringen kann,
    /// wenn PCIe Uploads zu lange dauern wird der nächste Frame gerendert und der Upload wird im nächsten Frame fortgesetzt
    /// da ich gerade 16.6ms insgesammt habe benutze ich 5ms aber für schnelle gpus braucht man ein smarten upload timer
    ///</summary>
    public unsafe void Render()
    {
        Frustum frustum = _terrainshader.Use(_gl, _camera);
        _terrainshader.BindTexture(_terrainTexture);
        
        _uploadTimer.Restart();
        foreach (ChunkMesher chunk in ChunkProvider.GetLoadedChunks())
        {
            if (_uploadTimer.Elapsed.TotalMilliseconds > MaxUploadTimeMs)
            {
                // Upload-Zeit überschritten, nächsten Frame rendern und Upload fortsetzen
                break;
            }
            
            // GPU-Upload auf dem Main-Thread falls noch nicht geschehen
            if (!chunk.IsUploaded)
                chunk.UploadToGpu(_gl);
            
            if(!frustum.isInFrustum(chunk.ChunkPosition, frustum)) continue;
            
            chunk.Render(_terrainshader);
        }
        _uploadTimer.Stop();
    }
    
    /**
     * Color und Depth Buffer löschen, damit der vorherige Frame nicht mehr sichtbar ist.
     */
    public void Clear()
    {
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    }
    
    /**
     * Jeden Shader und Chunk gefolgt vom GL Kontext entfernen
     */
    public void Dispose()
    {
        _terrainshader.Dispose();
        _terrainTexture.Dispose();
        ChunkProvider.Dispose();
        _gl.Dispose();
    }
    
    /**
     * Fenstergröße weitergeben damit die Viewportgröße angepasst werden kann.
     */
    public void FramebufferResize(Vector2D<int> size)
    {
        _gl.Viewport(size);
        _camera.AspectRatio = (float)size.X / size.Y;
    }
}