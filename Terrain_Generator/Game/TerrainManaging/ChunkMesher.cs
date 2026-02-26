using System.Numerics;
using Basics.Graphics;
using Silk.NET.OpenGL;
using System.Collections.Generic;
using Basics.Utilities; //Liste

namespace Basics.Game;

/**
 * Chunk Objekt 32x32x32 Blöcke, das in der Welt platziert wird.
 * Jeder Chunk hat seine eigene Geometrie (VBO, EBO, VAO)
 * 
 */
public class ChunkMesher : IDisposable
{
    public ChunkCoord ChunkPosition; // Weltposition des Chunks
    private List<uint> indices = new List<uint>();
    private List<float> vertices = new List<float>();

    // Die OpenGL Handles für diesen spezifischen Chunk
    private BufferObject<float> _vbo;
    private BufferObject<uint> _ebo;
    private VertexArrayObject<float, uint> _vao;
    private GL _gl;
    private uint _indicesCount;
    private Matrix4x4 model; // Model Matrix für diesen Chunk
    private int[,,] _blockData; // 3D Array für die Blocktypen im Chunk (z.B. 0 = Luft, 1 = Erde, etc.)
    private float texturestep = 1.0f / 4.0f; // Top, Bottom, Sides

    public ChunkMesher(GL gl, ChunkCoord position, int[,,] blockData)
    {
        _gl = gl;
        this.ChunkPosition = position;
        this._blockData = blockData;
        InitializeGeometry(blockData);
    }

    private unsafe void InitializeGeometry(int[,,] blockData)
    {
        _blockData = blockData;
        vertices.Clear(); // Sicherstellen, dass Listen leer sind
        indices.Clear();
        
        for (int x = 0; x < 32; x++)
        {
            for (int y = 0; y < 32; y++)
            {
                for (int z = 0; z < 32; z++)
                {
                    if (_blockData[x, y, z] != 0) // Nur Blöcke rendern, die nicht Luft sind
                    {
                        // Top Face
                        if (y == 31 || _blockData[x, y + 1, z] == 0) // Nur rendern, wenn oben Luft ist
                        {
                            vertices.AddRange(new float[]
                            {
                                x, y + 1, z + 1, 0.0f, 0.0f,        // Bottom-Left
                                x + 1, y + 1, z + 1, texturestep, 0.0f, // Bottom-Right
                                x + 1, y + 1, z, texturestep, 1.0f, // Top-Right
                                x, y + 1, z, 0.0f, 1.0f           // Top-Left
                            });
                            AddIndices((uint)vertices.Count / 5 - 4);
                        }
                        
                        // Bottom Face
                        if (y == 0 || _blockData[x, y - 1, z] == 0) // Nur rendern, wenn unten Luft ist
                        {
                            vertices.AddRange(new float[]
                            {
                                x + 1, y, z + 1, texturestep*2, 0.0f, // Bottom-Right
                                x, y, z + 1, texturestep, 0.0f,       // Bottom-Left 
                                x, y, z, texturestep, 1.0f,          // Top-Left
                                x + 1, y, z, texturestep*2, 1.0f       // Top-Right
                            });

                            AddIndices((uint)vertices.Count / 5 - 4);
                        }
                        
                        // Left Face (-X)
                        if (x == 0 || _blockData[x - 1, y, z] == 0)
                        {
                            vertices.AddRange(new float[]
                            {
                                x, y, z, texturestep*2, 1.0f,       // Bottom-Left
                                x, y, z + 1, texturestep*3, 1.0f,            // Bottom-Right
                                x, y + 1, z + 1, texturestep*3, 0.0f,             // Top-Right
                                x, y + 1, z, texturestep*2, 0.0f        // Top-Left
                            });
                            AddIndices((uint)vertices.Count / 5 - 4);
                        }

                        // Right Face (+X)
                        if (x == 31 || _blockData[x + 1, y, z] == 0)
                        {
                            vertices.AddRange(new float[]
                            {
                                x + 1, y, z + 1, texturestep*2, 1.0f,        // Bottom-Left
                                x + 1, y, z, texturestep*3, 1.0f,               // Bottom-Right
                                x + 1, y + 1, z, texturestep*3, 0.0f,           // Top-Right
                                x + 1, y + 1, z + 1, texturestep*2, 0.0f         // Top-Left
                            });
                            AddIndices((uint)vertices.Count / 5 - 4);
                        }

                        // Front Face (+Z)
                        if (z == 31 || _blockData[x, y, z + 1] == 0)
                        {
                             vertices.AddRange(new float[]
                             {
                                 x, y, z + 1, texturestep*2, 1.0f,            // Bottom-Left
                                 x + 1, y, z + 1, texturestep*3, 1.0f,             // Bottom-Right
                                 x + 1, y + 1, z + 1, texturestep*3, 0.0f,               // Top-Right
                                 x, y + 1, z + 1, texturestep*2, 0.0f          // Top-Left
                             });
                             AddIndices((uint)vertices.Count / 5 - 4);
                        }

                        // Back Face (-Z)
                        if (z == 0 || _blockData[x, y, z - 1] == 0)
                        {
                            vertices.AddRange(new float[]
                            {
                                x + 1, y, z,texturestep*2, 1.0f,         // Bottom-Left
                                x, y, z, 0.0f + texturestep*3, 1.0f,            // Bottom-Right
                                x, y + 1, z, 0.0f + texturestep*3, 0.0f,              // Top-Right
                                x + 1, y + 1, z, texturestep*2, 0.0f          // Top-Left
                            });
                            AddIndices((uint)vertices.Count / 5 - 4);
                        }
                    }
                }
            }
        }
        
            

        _indicesCount = (uint)indices.Count;

        // Buffer erstellen
        _ebo = new BufferObject<uint>(_gl, indices.ToArray(), BufferTargetARB.ElementArrayBuffer);
        _vbo = new BufferObject<float>(_gl, vertices.ToArray(), BufferTargetARB.ArrayBuffer);
        _vao = new VertexArrayObject<float, uint>(_gl, _vbo, _ebo);

        // Layout (Position + UV)
        _vao.VertexAttributePointer(0, 3, VertexAttribPointerType.Float, 5, 0);
        _vao.VertexAttributePointer(1, 2, VertexAttribPointerType.Float, 5, 3);
        
        // Model Matrix initialisieren (basierend auf Chunk Position)
        model = Matrix4x4.CreateTranslation(new Vector3(ChunkPosition.X, ChunkPosition.Y, ChunkPosition.Z));
    }

    // Helper für saubereren Code
    private void AddIndices(uint baseIndex)
    {
        indices.AddRange(new uint[]
        {
            baseIndex, baseIndex + 1, baseIndex + 2,
            baseIndex, baseIndex + 2, baseIndex + 3
        });
    }

    public unsafe void Render(ShaderManager shaderManager)
    {
        // 1. Dem Shader sagen, wo dieser Chunk liegt
        shaderManager.SetModelMatrix(model);

        // 2. VAO binden und zeichnen
        _vao.Bind();
        
        // 3. EBO binden und draw call
        _ebo.Bind();
        
        _gl.DrawElements(PrimitiveType.Triangles, _indicesCount, DrawElementsType.UnsignedInt, (void*)0);
    }

    public void Dispose()
    {
        _vbo.Dispose();
        _ebo.Dispose();
        _vao.Dispose();
    }
}
