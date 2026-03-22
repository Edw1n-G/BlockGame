using System.Diagnostics;
using System.Drawing;
using Basics.Game.TerrainManaging;
using Basics.Game.TerrainManaging.Meshing;
using Basics.Graphics;
using Silk.NET.Maths;
using Silk.NET.OpenGL;

namespace Basics.Game.Graphics;

public class Renderer
{
    private static GL _gl = null!;

    private static ShaderManager _terrainshader = null!;
    private static TextureArray _terrainTexture = null!;
    private static Camera _camera = null!;
    
    public static ChunkProvider ChunkProvider = null!;
    
    /**
     * Setup Methode, alles was man fürs Rendern braucht.
     * Window, Camera, Shader und test chunks
     */
    public unsafe void Setup(Camera camera, GL gl)
    {
        _gl = gl;
        _gl.ClearColor(Color.CornflowerBlue);
        
        _camera = camera;
        _terrainshader = new ShaderManager(_gl, "shader.vert", "shader.frag");
        _terrainTexture = new TextureArray(_gl, "Game/texture/example.png");
    }

    public void SetCamera(Camera camera)
    {
        _camera = camera;
    }


    
    private int totalchunks;
    private int shownchunks;
    
    ///<summary>
    /// Abstraktion für rendern
    /// jeder Chunk aus dem ChunkProvider wird durchgegangen und auf IsUploaded geprüft
    /// damit der Main-Thread die Daten an die GPU bringen kann,
    /// wenn PCIe Uploads zu lange dauern wird der nächste Frame gerendert und der Upload wird im nächsten Frame fortgesetzt
    /// da ich gerade 16.6ms insgesamt habe benutze ich 5ms aber für schnelle gpus braucht man ein smarten upload timer
    ///</summary>
    public unsafe void Render()
    {
        long startTimestamp = Stopwatch.GetTimestamp();
        long maxTicks = (long)(5.0 / 1000.0 * Stopwatch.Frequency);
        
        
        Frustum frustum = _terrainshader.Use(_gl, _camera);
        _terrainshader.BindTexture(_terrainTexture);

        while (ChunkProvider.UploadQueue.Reader.TryRead(out BaseMesher? chunk))
        {
            // Wenn der Chunk nicht mehr in Chunk data ist, wurde er schon entladen
            if (!ChunkProvider.Chunkdata.ContainsKey(chunk.ChunkPosition))
            {
                continue;
            }
            
            // Alles außer Luft hochladen
            if (!chunk.IsEmpty)
            {
                chunk.UploadToGpu(_gl);
            }

            // Wenn der Chunk schon geladen ist ersetzen, sonst hinzufügen
            if (ChunkProvider.LoadedChunks.TryGetValue(chunk.ChunkPosition, out BaseMesher? oldMesh))
            {
                // Neues Mesh an alte Position setzen
                ChunkProvider.LoadedChunks[chunk.ChunkPosition] = chunk;
            }
            else
            {
                ChunkProvider.LoadedChunks.TryAdd(chunk.ChunkPosition, chunk);
            }
            
            if (Stopwatch.GetTimestamp() - startTimestamp >= maxTicks)
            {
                break;
            }
        }

        foreach (var kvp in ChunkProvider.LoadedChunks) 
        {
            BaseMesher chunk = kvp.Value;
            
            if (chunk.IsEmpty) continue;
            if (!frustum.isInFrustum(chunk.ChunkPosition)) continue;
            chunk.Render(_terrainshader);
        }
        
        while (ChunkProvider.UnloadQueue.TryDequeue(out BaseMesher? chunk))
        {
            chunk.Dispose();
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