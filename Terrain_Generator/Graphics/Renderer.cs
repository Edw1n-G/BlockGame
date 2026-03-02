using Silk.NET.OpenGL; //Für die OpenGL Funktionen
using Basics.Setup; //Für die Color Klasse
using System.Drawing;
using System.Diagnostics;// Für Upload Limits
using Basics.Game;
using Basics.Game.TerrainManaging;
using Basics.Game.TerrainManaging.Meshing;
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


    long startTimestamp = Stopwatch.GetTimestamp();
    long maxTicks = (long)(2.0 / 1000.0 * Stopwatch.Frequency);
    
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

        while (ChunkProvider.UploadQueue.TryDequeue(out ChunkMesher? chunk))
        {
            chunk.UploadToGpu(_gl);

            ChunkProvider.LoadedChunks.TryAdd(chunk.ChunkPosition, chunk);
            
            if (Stopwatch.GetTimestamp() - startTimestamp >= maxTicks)
            {
                break;
            }
        }

        foreach (ChunkMesher chunk in ChunkProvider.LoadedChunks.Values)
        {
            if (!frustum.isInFrustum(chunk.ChunkPosition, frustum)) continue;

            chunk.Render(_terrainshader);
        }

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