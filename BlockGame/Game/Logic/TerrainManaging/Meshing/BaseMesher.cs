using System.Numerics;
using System.Runtime.InteropServices;
using Basics.Game.Graphics;
using Basics.Configurations;
using Basics.Game.Configurations;
using Basics.Game.Utilities;
using Basics.Graphics;
using Silk.NET.OpenGL;

namespace Basics.Game.Logic.TerrainManaging.Meshing;

/// <summary>
/// Basisklasse für alle Meshing-Implementierungen.
/// Stellt gemeinsame Funktionalität für Vertex- und Index-Verwaltung sowie GPU-Upload bereit.
/// </summary>
public class BaseMesher : IDisposable
{
    /// <summary>
    /// Layout eines Vertices: 3 floats (12 Bytes) + ushort (2 Bytes für Texture-Layer) + 1 byte (AO) + 1 byte (Padding) = 16 Bytes pro Vertex.
    /// </summary>
    protected const int VertexStride = 16;
    /// <summary>
    /// Geschätzte Anzahl sichtbarer Flächen pro Chunk (maximale Schätzung für Speicherallokation).
    /// </summary>
    protected static readonly int EstimatedSurfaceFaces = 6 * 16 * 16;
    /// <summary>
    /// Geschätzte Gesamtbytes für Vertices (bei maximaler Flächenanzahl).
    /// </summary>
    protected static readonly int EstimatedVertexBytes = EstimatedSurfaceFaces * 4 * VertexStride;
    /// <summary>
    /// Geschätzte Anzahl von Indices (6 indices pro Fläche).
    /// </summary>
    protected static readonly int EstimatedIndices = EstimatedSurfaceFaces * 6;
    
    /// <summary>
    /// Position des Chunks in Chunk-Koordinaten (z.B. 0/0, 1/0, -1/0, etc.).
    /// </summary>
    public ChunkCoord ChunkPosition;
    
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
    /// Aktuelle Anzahl der Indices im Mesh.
    /// </summary>
    protected uint _indicesCount;
    
    /// <summary>
    /// Model-Matrix für die World-Transformation dieses Chunks.
    /// </summary>
    protected Matrix4x4 model;
    
    /// <summary>
    /// Die OpenGL-Instanz für diesen spezifischen Chunk.
    /// </summary>
    private GL? _gl;
    
    /// <summary>
    /// Vertex Buffer Object Handle für diesen Chunk.
    /// </summary>
    private uint _vbo;
    /// <summary>
    /// Element Buffer Object Handle für diesen Chunk.
    /// </summary>
    private uint _ebo;
    /// <summary>
    /// Vertex Array Object Handle für diesen Chunk.
    /// </summary>
    private uint _vao;
    /// <summary>
    /// Aktuelle Kapazität des VBO in Bytes.
    /// </summary>
    private nuint _vboCapacity;
    /// <summary>
    /// Aktuelle Kapazität des EBO in Bytes.
    /// </summary>
    private nuint _eboCapacity;
    
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
        _blockRegistry = blockRegistry ?? EngineContext.Current?.BlockRegistry ?? BlockLoader.Current;
        
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
    
    
    // Für Chunks die keine AO benutzten
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
    /// MUS auf dem Main-Thread (OpenGL-Kontext) aufgerufen werden.
    /// </summary>
    /// <param name="gl">Die OpenGL-Instanz für den GPU-Upload.</param>
    public unsafe void UploadToGpu(GL gl)
    {
        if (_uploaded) return;
        _gl = gl;
        
        if (IsEmpty)
        {
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
        
        // Anstatt mit ToArray() die ganze liste in ein array zu kopieren
        // nimmt man das array der Liste. listen sind ja eh arrays 
        // idk wer den typen der ToArray() schrieb verarscht hat
        Span<byte> vertexSpan = CollectionsMarshal.AsSpan(_vertices);
        Span<uint> indexSpan = CollectionsMarshal.AsSpan(_indices);
        nuint neededVboSize = (nuint)vertexSpan.Length;
        nuint neededEboSize = (nuint)(indexSpan.Length * sizeof(uint));
        
        // Buffer für die grafikkarten arrays leihen
        if (_provider.VramPool.TryDequeue(out PooledMeshBuffer pool))
        {
            _vao = pool.Vao;
            _vbo = pool.Vbo;
            _ebo = pool.Ebo;
            _vboCapacity = pool.VboCapacity;
            _eboCapacity = pool.EboCapacity;

            _gl.BindVertexArray(_vao);

            // VBO
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            fixed (byte* vPtr = vertexSpan)
            {
                if (neededVboSize <= _vboCapacity)
                {
                    //Wenn der buffer viel zu groß ist einmal verkleinern um nicht zu viel speicher zu belegen
                    if (_vboCapacity > neededVboSize * 2 && _vboCapacity > 100_000)
                    {
                        _gl.BufferData(BufferTargetARB.ArrayBuffer, neededVboSize, vPtr, BufferUsageARB.DynamicDraw);
                        _vboCapacity = neededVboSize; // Kapazität nach unten korrigieren
                    }
                    else
                    {
                        // Buffer passt
                        _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, neededVboSize, vPtr);
                    }
                }
                else
                {
                    // Chunk ist größer als recycelter Puffer, vergrößern
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, neededVboSize, vPtr, BufferUsageARB.DynamicDraw);
                    _vboCapacity = neededVboSize;
                }
            }

            // EBO
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
            fixed (uint* iPtr = indexSpan)
            {
                if (neededEboSize <= _eboCapacity)
                {
                    // Gleiche Logik für die Indizes
                    if (_eboCapacity > neededEboSize * 2 && _eboCapacity > 20_000)
                    {
                        _gl.BufferData(BufferTargetARB.ElementArrayBuffer, neededEboSize, iPtr,
                            BufferUsageARB.DynamicDraw);
                        _eboCapacity = neededEboSize;
                    }
                    else
                    {
                        _gl.BufferSubData(BufferTargetARB.ElementArrayBuffer, 0, neededEboSize, iPtr);
                    }
                }
                else
                {
                    _gl.BufferData(BufferTargetARB.ElementArrayBuffer, neededEboSize, iPtr, BufferUsageARB.DynamicDraw);
                    _eboCapacity = neededEboSize;
                }
            }
        }
        else
        {
            // Keine Buffer vorhanden
            _vao = _gl.GenVertexArray();
            _gl.BindVertexArray(_vao);
            
            // VBO
            _vbo = _gl.GenBuffer();
            _vboCapacity = neededVboSize;
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            
            fixed (byte* vPtr = vertexSpan) {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, _vboCapacity, vPtr, BufferUsageARB.DynamicDraw);
            }
            
            // EBO
            _ebo = _gl.GenBuffer();
            _eboCapacity = neededEboSize;
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
            
            fixed (uint* iPtr = indexSpan) {
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer, _eboCapacity, iPtr, BufferUsageARB.DynamicDraw);
            }
            
            // Vertex Attribute initialisieren
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, VertexStride, (void*)0);
            _gl.EnableVertexAttribArray(0);
            
            _gl.VertexAttribIPointer(1, 1, VertexAttribIType.UnsignedShort, VertexStride, (void*)12);
            _gl.EnableVertexAttribArray(1);
            
            _gl.VertexAttribIPointer(2, 1, VertexAttribIType.UnsignedByte, VertexStride, (void*)14);
            _gl.EnableVertexAttribArray(2);
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
    /// Rendert den Mesh mit dem angegebenen Shader-Manager.
    /// </summary>
    /// <param name="shaderManager">Der ShaderManager für das Rendern.</param>
    public unsafe void Render(ShaderManager shaderManager)
    
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
        if (_vao != 0 && _vbo != 0 && _ebo != 0)
        {
            _provider.VramPool.Enqueue(new PooledMeshBuffer 
            {
                Vao = _vao,
                Vbo = _vbo,
                Ebo = _ebo,
                VboCapacity = _vboCapacity,
                EboCapacity = _eboCapacity
            });
        }
        
        _disposed = true;
    }
}