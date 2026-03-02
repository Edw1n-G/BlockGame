using System;
using System.Collections.Generic;
using System.Numerics;
using Basics.Graphics;
using Basics.Utilities;
using Silk.NET.OpenGL;

namespace Basics.Game.TerrainManaging.Meshing;

public class BaseMesher : IDisposable
{
    public ChunkCoord ChunkPosition; // Position des Chunks in Chunk-Koordinaten (z.B. 0/0, 1/0, -1/0, etc.)
    protected List<uint> _indices = new List<uint>();
    protected List<float> _vertices = new List<float>();
    protected Matrix4x4 model;
    protected uint _indicesCount;

    // Die OpenGL Handles für diesen spezifischen Chunk
    private BufferObject<float> _vbo;
    private BufferObject<uint> _ebo;
    private VertexArrayObject<float, uint> _vao;
    private GL _gl;
    
    private bool _uploaded = false;
    
    // Für Chunks die keine AO benutzten
    protected void AddIndices(uint baseIndex)
    {
        _indices.Add(baseIndex);     _indices.Add(baseIndex + 1); _indices.Add(baseIndex + 2);
        _indices.Add(baseIndex);     _indices.Add(baseIndex + 2); _indices.Add(baseIndex + 3);
    }

    /// <summary>
    /// Ob die Daten bereits auf die GPU hochgeladen wurden.
    /// </summary>
    public bool IsUploaded => _uploaded;
    
    /// <summary>
    /// Lädt die berechneten Mesh-Daten auf die GPU hoch.
    /// MUSS auf dem Main-Thread (OpenGL-Kontext) aufgerufen werden
    /// </summary>
    public void UploadToGpu(GL gl)
    {
        if (_uploaded) return;
        _gl = gl;

        // Buffer erstellen 
        _ebo = new BufferObject<uint>(_gl, _indices.ToArray(), BufferTargetARB.ElementArrayBuffer);
        _vbo = new BufferObject<float>(_gl, _vertices.ToArray(), BufferTargetARB.ArrayBuffer);
        _vao = new VertexArrayObject<float, uint>(_gl, _vbo, _ebo);

        // Layout (Position=3 + Layer= + Brightness=1) => Stride = 6
        _vao.VertexAttributePointer(0, 3, VertexAttribPointerType.Float, 5, 0); // aPos (x,y,z)
        _vao.VertexAttributePointer(1, 1, VertexAttribPointerType.Float, 5, 3); // layer
        _vao.VertexAttributePointer(2, 1, VertexAttribPointerType.Float, 5, 4); // brightness
        
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