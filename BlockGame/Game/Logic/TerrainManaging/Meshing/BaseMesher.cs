using System.Numerics;
using System.Runtime.InteropServices;
using Basics.Game.Graphics;
using Basics.Game.Utilities;
using Basics.Graphics;
using Silk.NET.OpenGL;

namespace Basics.Game.Logic.TerrainManaging.Meshing;

public class BaseMesher : IDisposable
{
    // Vertex Layout: 3 floats (12 Bytes) + ushort (2 Bytes for Layer) + 1 byte (AO) + 1 byte (padding) = 16 Bytes per Vertex
    protected const int VertexStride = 16;
    
    public ChunkCoord ChunkPosition; // Position des Chunks in Chunk-Koordinaten (z.B. 0/0, 1/0, -1/0, etc.)
    
    //Listen um mit der geometriy komplexität zu wachsen
    protected List<uint> _indices;
    protected List<byte> _vertices;
    
    protected int _vertexCount;
    protected uint _indicesCount;
    
    protected Matrix4x4 model;
    
    // Die OpenGL Handles für diesen spezifischen Chunk
    private GL? _gl;
    
    private uint _vbo;
    private uint _ebo;
    private uint _vao;
    private nuint _vboCapacity;
    private nuint _eboCapacity;
    
    private bool _uploaded = false;
    private bool _disposed = false;
    public bool IsEmpty => _indicesCount == 0;
    
    public BaseMesher()
    {
        // checken ob eine Liste im Pool frei ist
        if (!ChunkProvider.VertexListPool.TryDequeue(out _vertices))
        {
            //Wenn der pool, leer ist, liste mit bestimmter größe erstellen um reallocation zu vermeiden
            _vertices = new List<byte>(60_000);
        }
        _vertices.Clear(); //gebrauchte liste leeren

        // Das gleiche für indexe
        if (!ChunkProvider.IndexListPool.TryDequeue(out _indices))
        {
            _indices = new List<uint>(10_000);
        }
        _indices.Clear();
    }
    
    
    // Für Chunks die keine AO benutzten
    protected void AddIndices(uint baseIndex)
    {
        _indices.Add(baseIndex);     _indices.Add(baseIndex + 1); _indices.Add(baseIndex + 2);
        _indices.Add(baseIndex);     _indices.Add(baseIndex + 2); _indices.Add(baseIndex + 3);
    }
    
    // Für Chunks die AO benutzten
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
    
    // Für Chunks die keine AO benutzten
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
    /// MUSS auf dem Main-Thread (OpenGL-Kontext) aufgerufen werden
    /// </summary>
    public unsafe void UploadToGpu(GL gl)
    {
        if (_uploaded) return;
        _gl = gl;
        
        if (IsEmpty)
        {
            this._vertices?.Clear();
            this._indices?.Clear();
            
            //geliehende Listen wieder zum pool geben
            if (this._vertices != null) ChunkProvider.VertexListPool.Enqueue(this._vertices);
            if (this._indices != null) ChunkProvider.IndexListPool.Enqueue(this._indices);
            
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
        if (ChunkProvider.VramPool.TryDequeue(out PooledMeshBuffer pool))
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
        
        // Alles auf der GPU geometrie daten löschen und buffer zum pool geben
        this._vertices.Clear();
        this._indices.Clear();
        
        ChunkProvider.VertexListPool.Enqueue(_vertices);
        ChunkProvider.IndexListPool.Enqueue(_indices);
        
        this._vertices = null!;
        this._indices = null!;
        _uploaded = true;
    }
    
    public unsafe void Render(ShaderManager shaderManager)
    {
        if (!_uploaded || _disposed || _vao == 0 || _gl is null) return;
        
        // Dem Shader die Weltposition geben
        shaderManager.SetModelMatrix(model);
        
        _gl.BindVertexArray(_vao);
        // Das EBO ist im VAO gebunden, wir können direkt zeichnen
        _gl.DrawElements(PrimitiveType.Triangles, _indicesCount, DrawElementsType.UnsignedInt, (void*)0);
    }
    
    // Unloading
    public void Dispose()
    {
        if (_disposed) return;
        
        // Return lists to pool if never uploaded
        if (!_uploaded)
        {
            if (_vertices != null)
            {
                _vertices.Clear();
                ChunkProvider.VertexListPool.Enqueue(_vertices);
                _vertices = null!;
            }
            if (_indices != null)
            {
                _indices.Clear();
                ChunkProvider.IndexListPool.Enqueue(_indices);
                _indices = null!;
            }
        }
        
        // Buffer in den pool nach dem GPU-Upload
        if (_vao != 0 && _vbo != 0 && _ebo != 0)
        {
            ChunkProvider.VramPool.Enqueue(new PooledMeshBuffer 
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