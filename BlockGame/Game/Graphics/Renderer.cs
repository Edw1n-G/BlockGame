using System.Diagnostics;
using System.Drawing;
using Basics.Configurations;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Logic.TerrainManaging.Meshing;
using Basics.Game.texture;
using Silk.NET.Maths;
using Silk.NET.OpenGL;

namespace Basics.Game.Graphics;

/// <summary>
/// Verwaltet das Rendern der Welt und aller Chunks.
/// Stellt Methoden zum Einrichten, Rendern und Aufräumen der Grafikressourcen bereit.
/// Alle GPU-Interaktionen laufen über den Main-Thread.
/// </summary>
public class Renderer
{
    private static GL _gl = null!;

    private static ShaderManager _terrainshader = null!;
    private static TextureArray _terrainTexture = null!;
    private static Camera _camera = null!;
    
    /// <summary>
    /// Stellt den ChunkProvider bereit, der die Chunks verwaltet und den Upload-Queue bereitstellt.
    /// </summary>
    public ChunkProvider ChunkProvider { get; set; }

    /// <summary>
    /// Initialisiert eine neue Instanz des <see cref="Renderer"/>-Klassens mit einem optionalen ChunkProvider.
    /// </summary>
    /// <param name="chunkProvider">Der zu verwendende ChunkProvider. Wenn nicht angegeben, wird der aktuelle Context oder der Standard-Provider verwendet.</param>
    public Renderer(ChunkProvider? chunkProvider = null)
    {
        ChunkProvider = chunkProvider ?? EngineContext.Current?.ChunkProvider ?? ChunkProvider.Current;
    }
    
    /// <summary>
    /// Richtet alle erforderlichen Ressourcen für das Rendern ein.
    /// Initialisiert Shader, Texturen und aktiviert Multisampling.
    /// </summary>
    /// <param name="camera">Die Kamera, die für das Rendern verwendet werden soll.</param>
    /// <param name="gl">Die OpenGL-Instanz.</param>
    /// <param name="chunkProvider">Optional ein neuer ChunkProvider, der verwendet werden soll.</param>
    public unsafe void Setup(Camera camera, GL gl, ChunkProvider? chunkProvider = null)
    {
        if (chunkProvider != null)
        {
            ChunkProvider = chunkProvider;
        }

        _gl = gl;
        ChunkProvider.AttachGraphicsContext(gl);
        _gl.ClearColor(Color.CornflowerBlue);
        
        _camera = camera;
        _terrainshader = new ShaderManager(_gl, "shader.vert", "shader.frag");
        _terrainTexture = BlockLoader.TerrainTexture;
        
        _gl.Enable(EnableCap.Multisample);
    }

    /// <summary>
    /// Legt die Kamera für das Rendern fest.
    /// </summary>
    /// <param name="camera">Die neue Kamera, die verwendet werden soll.</param>
    public void SetCamera(Camera camera)
    {
        _camera = camera;
    }

    private int totalchunks;
    private int shownchunks;
    
    /// <summary>
    /// Führt den Rendervorgang durch. Lädt Chunks aus der Upload-Queue in die GPU,
    /// rendert alle sichtbaren Chunks innerhalb der Zeitbeschränkung von ca. 5ms pro Frame
    /// und entlädt Chunks aus der Unload-Queue.
    /// </summary>
    /// <remarks>
    /// Diese Methode ist zeitlich begrenzt auf etwa 5ms pro Frame, um eine gleichmäßige Frame-Rate sicherzustellen.
    /// Wenn die Upload-Operationen zu lange dauern, wird der nächste Frame gerendert und die Uploads im nächsten Frame fortgesetzt.
    /// </remarks>
    public unsafe void Render()
    {
        long startTimestamp = Stopwatch.GetTimestamp();
        long maxTicks = (long)(5.0 / 1000.0 * Stopwatch.Frequency);
        
        Frustum frustum = _terrainshader.Use(_gl, _camera);
        _terrainshader.BindTexture(_terrainTexture);

        while (ChunkProvider.UploadQueue.TryDequeue(out BaseMesher? chunk))
        {
            if (!ChunkProvider.Chunkdata.ContainsKey(chunk.ChunkPosition))
            {
                chunk.Dispose();
                continue;
            }
            
            chunk.UploadToGpu(_gl);

            if (ChunkProvider.LoadedChunks.TryGetValue(chunk.ChunkPosition, out BaseMesher? oldMesh))
            {
                ChunkProvider.LoadedChunks[chunk.ChunkPosition] = chunk;
                ChunkProvider.UnloadQueue.Enqueue(oldMesh);
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
            if (!frustum.IsInFrustum(chunk.ChunkPosition)) continue;
            chunk.Render(_terrainshader);
        }
        
        while (ChunkProvider.UnloadQueue.TryDequeue(out BaseMesher? chunk))
        {
            chunk.Dispose();
        }
    }
    
    /// <summary>
    /// Löscht den Farb- und Tiefenpuffer, damit der vorherige Frame nicht mehr sichtbar ist.
    /// </summary>
    public void Clear()
    {
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    }
    
    /// <summary>
    /// Gibt alle vom Renderer verwendeten Ressourcen frei.
    /// Entlädt alle Shader, Texturen, den ChunkProvider und die OpenGL-Instanz.
    /// </summary>
    public void Dispose()
    {
        _terrainshader.Dispose();
        _terrainTexture.Dispose();
        ChunkProvider.Dispose();
        _gl.Dispose();
    }
    
    /// <summary>
    /// Benachrichtigt den Renderer über eine Größenänderung des Framebuffers.
    /// Passt die Viewport-Größe an und aktualisiert das AspectRatio der Kamera.
    /// </summary>
    /// <param name="size">Die neue Größe des Framebuffers in Pixeln.</param>
    public void FramebufferResize(Vector2D<int> size)
    {
        _gl.Viewport(size);
        _camera.AspectRatio = (float)size.X / size.Y;
    }
}