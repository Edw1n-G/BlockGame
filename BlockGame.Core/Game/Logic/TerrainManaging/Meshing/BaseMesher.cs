using System.Numerics;
using System.Runtime.InteropServices;
using Basics.Configurations;
using Basics.Game.Configurations;
using Basics.Game.Utilities;

namespace Basics.Game.Logic.TerrainManaging.Meshing;

/// <summary>
/// Basisklasse für alle Meshing-Implementierungen.
/// Stellt gemeinsame Funktionalität für Vertex- und Index-Verwaltung sowie GPU-Upload bereit.
/// </summary>
public class BaseMesher : IDisposable
{
    /// <summary>
    /// Geschätzte Anzahl sichtbarer Flächen pro Chunk (maximale Schätzung für Speicherallokation).
    /// </summary>
    protected static readonly int EstimatedSurfaceFaces = 6 * 16 * 16;
    /// <summary>
    /// Geschätzte Gesamtbytes für Vertices (bei maximaler Flächenanzahl).
    /// </summary>
    protected static readonly int EstimatedVertexBytes = EstimatedSurfaceFaces * 4 * ChunkVertexFormat.VertexStride;
    /// <summary>
    /// Geschätzte Anzahl von Indices (6 indices pro Fläche).
    /// </summary>
    protected static readonly int EstimatedIndices = EstimatedSurfaceFaces * 6;
    
    /// <summary>
    /// Position des Chunks in Chunk-Koordinaten (z.B. 0/0, 1/0, -1/0, etc.).
    /// </summary>
    public ChunkCoord ChunkPosition;
    /// <summary>The exact chunk data instance from which this mesh was built.</summary>
    public ChunkData? SourceChunkData { get; protected set; }
    
    /// <summary>
    /// Listen für Vertex- und Index-Daten, wachsen mit der Geometriekomplexität.
    /// </summary>
    protected List<uint> _indices;
    protected List<byte> _vertices;
    
    /// <summary>
    /// Aktuelle Anzahl der Vertices im Mesh.
    /// </summary>
    protected int _vertexCount;
    /// <summary>
    /// Aktuelle Anzahl der Indices im Mesh für den renderer, nachdem die index listen geleert worden sind.
    /// </summary>
    protected uint _indicesCount;
    
    /// <summary>
    /// Model-Matrix für die World-Transformation dieses Chunks.
    /// </summary>
    protected Matrix4x4 model;
    
    internal IChunkGpuMesh? GpuMesh { get; private set; }
    internal Matrix4x4 Model => model;
    internal uint IndexCount => _indicesCount;

    /// <summary>
    /// Gibt an, ob die Mesh-Daten bereits auf die GPU hochgeladen wurden.
    /// </summary>
    private bool _uploaded = false;
    /// <summary>
    /// Gibt an, ob dieser Mesher verworfen wurde.
    /// </summary>
    private bool _disposed = false;
    /// <summary>
    /// Gibt an, ob dieses Mesh leer ist (keine Indices).
    /// </summary>
    public bool IsEmpty => _indicesCount == 0;

    // Borrowed CPU views: valid only until upload or disposal; never retain across pool reuse.
    internal ReadOnlySpan<byte> CpuVertices => !_disposed && !_uploaded
        ? CollectionsMarshal.AsSpan(_vertices) : throw new InvalidOperationException("CPU mesh buffers no longer owned.");
    internal ReadOnlySpan<uint> CpuIndices => !_disposed && !_uploaded
        ? CollectionsMarshal.AsSpan(_indices) : throw new InvalidOperationException("CPU mesh buffers no longer owned.");
    
    /// <summary>
    /// Der ChunkProvider für diesen Mesher.
    /// </summary>
    protected readonly ChunkProvider _provider;
    /// <summary>
    /// Der BlockRegistry für diesen Mesher.
    /// </summary>
    protected readonly BlockRegistry _blockRegistry;
    /// <summary>
    /// Der ChunkProvider für diesen Mesher.
    /// </summary>
    public ChunkProvider Provider => _provider;

    /// <summary>
    /// Initialisiert einen neuen Mesher mit optionalen Provider und Registry.
    /// </summary>
    /// <param name="provider">Der zu verwendende ChunkProvider. Wenn null, wird der aktuelle EngineContext verwendet.</param>
    /// <param name="blockRegistry">Die zu verwendende BlockRegistry. Wenn null, wird der aktuelle EngineContext verwendet.</param>
    public BaseMesher(ChunkProvider? provider = null, BlockRegistry? blockRegistry = null)
    {
        _provider = provider ?? EngineContext.Current?.ChunkProvider ?? ChunkProvider.Current;
        _blockRegistry = blockRegistry ?? EngineContext.Current?.BlockRegistry ?? BlockDefinitionLoader.Current;
        
        if (_blockRegistry == null)
        {
            throw new InvalidOperationException("BlockRegistry konnte nicht gefunden werden.");
        }
        
        // Prüfen, ob eine Liste im Pool frei ist
        if (!_provider.VertexListPool.TryDequeue(out _vertices))
        {
            // Wenn der Pool leer ist, Liste mit bestimmter Größe erstellen, um Reallocation zu vermeiden
            _vertices = new List<byte>(60_000);
        }
        _vertices.Clear(); // Gebrauchte Liste leeren

        // Das gleiche für Indizes
        if (!_provider.IndexListPool.TryDequeue(out _indices))
        {
            _indices = new List<uint>(10_000);
        }
        _indices.Clear();
    }
    
    /// <summary>
    /// Fügt Indices für Quads ohne AO hinzu.
    /// Verwendet für Chunks, die keine Ambient Occlusion benutzen.
    /// </summary>
    /// <param name="baseIndex">Der Basis-Index für die vier Vertices.</param>
    protected void AddIndices(uint baseIndex)
    {
        _indices.Add(baseIndex);     _indices.Add(baseIndex + 1); _indices.Add(baseIndex + 2);
        _indices.Add(baseIndex);     _indices.Add(baseIndex + 2); _indices.Add(baseIndex + 3);
    }
    
    /// <summary>
    /// Fügt einen Vertex mit AO-Level hinzu (für LOD0).
    /// </summary>
    /// <param name="x">X-Koordinate des Vertices.</param>
    /// <param name="y">Y-Koordinate des Vertices.</param>
    /// <param name="z">Z-Koordinate des Vertices.</param>
    /// <param name="layer">Texture-Layer (Index in der Texture2DArray).</param>
    /// <param name="aoLevel">Ambient Occlusion Level (0-3).</param>
    protected void AddVertex(float x, float y, float z, ushort layer, byte aoLevel)
    {
        // X Float in 4 Bytes zerlegen und sofort hinzufügen (keine Arrays, kein AddRange!)
        int ix = BitConverter.SingleToInt32Bits(x);
        _vertices.Add((byte)(ix));
        _vertices.Add((byte)(ix >> 8));
        _vertices.Add((byte)(ix >> 16));
        _vertices.Add((byte)(ix >> 24));
        
        // Y Float
        int iy = BitConverter.SingleToInt32Bits(y);
        _vertices.Add((byte)(iy));
        _vertices.Add((byte)(iy >> 8));
        _vertices.Add((byte)(iy >> 16));
        _vertices.Add((byte)(iy >> 24));
        
        // Z Float
        int iz = BitConverter.SingleToInt32Bits(z);
        _vertices.Add((byte)(iz));
        _vertices.Add((byte)(iz >> 8));
        _vertices.Add((byte)(iz >> 16));
        _vertices.Add((byte)(iz >> 24));
        
        // Layer (ushort), AO (byte), +1 byte padding damit das Vertex 16 Bytes bleibt.
        _vertices.Add((byte)layer);
        _vertices.Add((byte)(layer >> 8));
        _vertices.Add(aoLevel);
        _vertices.Add(0);
        
        _vertexCount++;
    }
    
    /// <summary>
    /// Fügt einen Vertex ohne AO hinzu (für LOD1-3).
    /// </summary>
    /// <param name="x">X-Koordinate des Vertices.</param>
    /// <param name="y">Y-Koordinate des Vertices.</param>
    /// <param name="z">Z-Koordinate des Vertices.</param>
    /// <param name="layer">Texture-Layer (Index in der Texture2DArray).</param>
    protected void AddVertex(float x, float y, float z, ushort layer)
    {
        // X Float
        int ix = BitConverter.SingleToInt32Bits(x);
        _vertices.Add((byte)(ix));
        _vertices.Add((byte)(ix >> 8));
        _vertices.Add((byte)(ix >> 16));
        _vertices.Add((byte)(ix >> 24));
        
        // Y Float
        int iy = BitConverter.SingleToInt32Bits(y);
        _vertices.Add((byte)(iy));
        _vertices.Add((byte)(iy >> 8));
        _vertices.Add((byte)(iy >> 16));
        _vertices.Add((byte)(iy >> 24));
        
        // Z Float
        int iz = BitConverter.SingleToInt32Bits(z);
        _vertices.Add((byte)(iz));
        _vertices.Add((byte)(iz >> 8));
        _vertices.Add((byte)(iz >> 16));
        _vertices.Add((byte)(iz >> 24));
        
        // Layer (ushort), AO (byte), +1 byte padding damit das Vertex 16 Bytes bleibt.
        _vertices.Add((byte)layer);
        _vertices.Add((byte)(layer >> 8));
        _vertices.Add(0);
        _vertices.Add(0);
        
        _vertexCount++;
    }
    

    /// <summary>
    /// Ob die Daten bereits auf die GPU hochgeladen wurden.
    /// </summary>
    public bool IsUploaded => _uploaded;
    
    /// <summary>
    /// Lädt die berechneten Mesh-Daten auf die GPU hoch.
    /// MUSS auf dem Main-Thread (OpenGL-Kontext) aufgerufen werden.
    /// </summary>
    public void UploadToGpu()
    {
        if (_uploaded) return;

        if (IsEmpty)
        {
            _indicesCount = 0;
            
            this._vertices?.Clear();
            this._indices?.Clear();
            
            //geliehende Listen wieder zum pool geben
            if (this._vertices != null) _provider.VertexListPool.Enqueue(this._vertices);
            if (this._indices != null) _provider.IndexListPool.Enqueue(this._indices);
            
            this._vertices = null!;
            this._indices = null!;
            _uploaded = true;
            return; 
        }
        
        _indicesCount = (uint)_indices.Count;
        
        var vertices = CollectionsMarshal.AsSpan(_vertices);
        var indices = CollectionsMarshal.AsSpan(_indices);
        var factory = _provider.GpuMeshFactory;
        if (_provider.VramPool.TryDequeue(out var pooled))
        {
            GpuMesh = pooled.Mesh;
            factory.Update(GpuMesh, vertices, indices);
        }
        else
        {
            GpuMesh = factory.Create(vertices, indices);
        }

        // Alles auf der GPU. geometrie daten löschen und buffer zum pool geben
        this._vertices.Clear();
        this._indices.Clear();
        
        _provider.VertexListPool.Enqueue(_vertices);
        _provider.IndexListPool.Enqueue(_indices);
        
        this._vertices = null!;
        this._indices = null!;
        _uploaded = true;
    }

    /// <summary>
    /// Gibt alle Ressourcen frei.
    /// Entweder gibt sie die Listen zum Pool zurück (wenn nicht hochgeladen)
    /// oder die Buffer-Handles (wenn auf der GPU).
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        
        // Return lists to pool if never uploaded
        if (!_uploaded)
        {
            if (_vertices != null)
            {
                _vertices.Clear();
                _provider.VertexListPool.Enqueue(_vertices);
                _vertices = null!;
            }
            if (_indices != null)
            {
                _indices.Clear();
                _provider.IndexListPool.Enqueue(_indices);
                _indices = null!;
            }
        }
        
        // Buffer in den pool nach dem GPU-Upload
        if (GpuMesh is { CanReuse: true } mesh)
            _provider.VramPool.Enqueue(new PooledMeshBuffer(mesh));
        GpuMesh = null;

        _disposed = true;
    }
}
