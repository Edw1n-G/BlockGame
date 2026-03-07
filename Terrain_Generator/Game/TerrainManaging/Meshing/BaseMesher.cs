using System;
using System.Collections.Generic;
using System.Numerics;
using Basics.Graphics;
using Basics.Utilities;
using Silk.NET.OpenGL;

namespace Basics.Game.TerrainManaging.Meshing;

public class BaseMesher : IDisposable
{
    // Vertex Layout: 3 floats (12 Bytes) + 1 byte (Layer) + 1 byte (AO) = 14 Bytes pro Vertex
    protected const int VertexStride = 14;
    
    public ChunkCoord ChunkPosition; // Position des Chunks in Chunk-Koordinaten (z.B. 0/0, 1/0, -1/0, etc.)
    protected List<uint> _indices = new List<uint>();
    protected List<byte> _vertices = new List<byte>();
    protected int _vertexCount; // Anzahl der Vertices (nicht Bytes!)
    protected Matrix4x4 model;
    protected uint _indicesCount;

    // Die OpenGL Handles für diesen spezifischen Chunk
    private BufferObject<byte> _vbo;
    private BufferObject<uint> _ebo;
    private VertexArrayObject<byte, uint> _vao;
    private GL _gl;
    
    private bool _uploaded = false;
    public bool IsEmpty => _indicesCount == 0;
    
    // Für Chunks die keine AO benutzten
    protected void AddIndices(uint baseIndex)
    {
        _indices.Add(baseIndex);     _indices.Add(baseIndex + 1); _indices.Add(baseIndex + 2);
        _indices.Add(baseIndex);     _indices.Add(baseIndex + 2); _indices.Add(baseIndex + 3);
    }
    
    // Für Chunks die AO benutzten
    protected void AddVertex(float x, float y, float z, byte layer, byte aoLevel)
    {
        // Position (3 floats = 12 Bytes)
        _vertices.AddRange(BitConverter.GetBytes(x));
        _vertices.AddRange(BitConverter.GetBytes(y));
        _vertices.AddRange(BitConverter.GetBytes(z));
        
        // Layer (1 byte)
        _vertices.Add(layer);
        
        // AO Level (1 byte)
        _vertices.Add(aoLevel);
        
        _vertexCount++;
    }
    
    // Für Chunks die keine AO benutzten
    protected void AddVertex(float x, float y, float z, byte layer)
    {
        // Position (3 floats = 12 Bytes)
        _vertices.AddRange(BitConverter.GetBytes(x));
        _vertices.AddRange(BitConverter.GetBytes(y));
        _vertices.AddRange(BitConverter.GetBytes(z));
        
        // Layer (1 byte)
        _vertices.Add(layer);
        
        // AO Level (1 byte)
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
            this._vertices = null;
            this._indices = null;
            _uploaded = true;
            return; 
        }
        
        // Buffer erstellen 
        _ebo = new BufferObject<uint>(_gl, _indices.ToArray(), BufferTargetARB.ElementArrayBuffer);
        _vbo = new BufferObject<byte>(_gl, _vertices.ToArray(), BufferTargetARB.ArrayBuffer);
        _vao = new VertexArrayObject<byte, uint>(_gl, _vbo, _ebo);

        // Layout: Stride = 14 Bytes (3 floats + 1 byte layer + 1 byte ao)
        // aPos: 3 floats ab Offset 0
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, VertexStride, (void*)0);
        _gl.EnableVertexAttribArray(0);
        
        // aLayer: 1 byte (int) ab Offset 12
        _gl.VertexAttribIPointer(1, 1, VertexAttribIType.UnsignedByte, VertexStride, (void*)12);
        _gl.EnableVertexAttribArray(1);
        
        // aAoLevel: 1 byte (int) ab Offset 13
        _gl.VertexAttribIPointer(2, 1, VertexAttribIType.UnsignedByte, VertexStride, (void*)13);
        _gl.EnableVertexAttribArray(2);
        
        this._vertices.Clear();
        this._indices.Clear();
        this._vertices = null;
        this._indices = null;
        _uploaded = true;
    }
    
    public unsafe void Render(ShaderManager shaderManager)
    {
        if (!_uploaded) return; // Noch nicht auf der GPU
        
        //Dem Shader sagen, wo dieser Chunk liegt
        shaderManager.SetModelMatrix(model);
        
        _vao.Bind();
        _ebo.Bind();
        
        _gl.DrawElements(PrimitiveType.Triangles, _indicesCount, DrawElementsType.UnsignedInt, (void*)0);
    }
    
    // Unloading
    public void Dispose()
    {
        _vbo.Dispose();
        _ebo.Dispose();
        _vao.Dispose();
    }
}