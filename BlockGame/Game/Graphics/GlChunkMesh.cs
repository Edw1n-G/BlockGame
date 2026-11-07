using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Logic.TerrainManaging.Meshing;
using Silk.NET.OpenGL;

namespace Basics.Game.Graphics;

internal sealed class GlChunkMesh(GL gl) : IChunkGpuMesh
{
    private readonly GL _gl = gl;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;
    private nuint _vboCapacity;
    private nuint _eboCapacity;

    public bool CanReuse => _vao != 0 && _vbo != 0 && _ebo != 0;

    internal unsafe void Create(ReadOnlySpan<byte> vertexSpan, ReadOnlySpan<uint> indexSpan)
    {
        nuint neededVboSize = (nuint)vertexSpan.Length;
        nuint neededEboSize = (nuint)(indexSpan.Length * sizeof(uint));

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

        BaseMesher.ConfigureVertexLayout(_gl);
    }

    internal unsafe void Update(ReadOnlySpan<byte> vertexSpan, ReadOnlySpan<uint> indexSpan)
    {
        nuint neededVboSize = (nuint)vertexSpan.Length;
        nuint neededEboSize = (nuint)(indexSpan.Length * sizeof(uint));

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

    internal unsafe void Render(ShaderManager shaderManager, System.Numerics.Matrix4x4 model, uint indicesCount)
    {
        if (_vao == 0) return;

        shaderManager.SetModelMatrix(model);
        _gl.BindVertexArray(_vao);
        _gl.DrawElements(PrimitiveType.Triangles, indicesCount, DrawElementsType.UnsignedInt, (void*)0);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
    }
}
