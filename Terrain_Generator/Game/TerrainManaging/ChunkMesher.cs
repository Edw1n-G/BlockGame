using System.Numerics;
using Basics.Configurations;
using Basics.Graphics;
using Basics.Utilities;
using Silk.NET.OpenGL;

//Liste

namespace Basics.Game.TerrainManaging;

/**
 * Chunk Objekt 32x32x32 Blöcke, das in der Welt platziert wird.
 * Jeder Chunk hat seine eigene Geometrie (VBO, EBO, VAO)
 * 
 */
public class ChunkMesher : IDisposable
{
    public ChunkCoord ChunkPosition; // Position des Chunks in Chunk-Koordinaten (z.B. 0/0, 1/0, -1/0, etc.)
    private List<uint> _indices = new List<uint>();
    private List<float> _vertices = new List<float>();

    // Die OpenGL Handles für diesen spezifischen Chunk
    private BufferObject<float> _vbo;
    private BufferObject<uint> _ebo;
    private VertexArrayObject<float, uint> _vao;
    private GL _gl;
    private uint _indicesCount;
    private Matrix4x4 model; // Model Matrix für diesen Chunk
    private int[,,] _blockData; // 3D Array für die Blocktypen im Chunk (z.B. 0 = Luft, 1 = Erde, etc.)
    private bool _uploaded = false; // Ob die Daten bereits auf die GPU hochgeladen wurden
    
    /// <summary>
    /// Konstruktor: Berechnet nur die Mesh-Daten (Vertices/Indices).
    /// Kann auf jedem Thread aufgerufen werden - keine OpenGL-Calls!
    /// </summary>
    public ChunkMesher(ChunkCoord position, int[,,] blockData)
    {
        this.ChunkPosition = position;
        this._blockData = blockData;
        BuildMeshData(blockData);
    }

    /// <summary>
    /// Berechnet Vertices und Indices - reine CPU-Arbeit, kein OpenGL.
    /// </summary>
    private void BuildMeshData(int[,,] blockData)
    {
        _blockData = blockData;
        _vertices.Clear(); // Sicherstellen, dass Listen leer sind
        _indices.Clear();
        
        for (int x = 0; x < 32; x++)
        {
            for (int y = 0; y < 32; y++)
            {
                for (int z = 0; z < 32; z++)
                {
                    if (_blockData[x, y, z] == 0) continue; // Nur Blöcke rendern, die nicht Luft sind
                    // Top Face
                    
                    if (!IsBlock(x,y+1,z)) // Nur rendern, wenn oben Luft ist
                    {
                        CreateCubeFace(x, y, z, 0);
                    }
                        
                    // Bottom Face
                    if (!IsBlock(x,y-1,z)) // Nur rendern, wenn unten Luft ist
                    {
                        CreateCubeFace(x, y, z, 1);
                    }
                        
                    // Front Face (+Z)
                    if (!IsBlock(x,y,z+1))
                    {
                        CreateCubeFace(x, y, z, 2);
                    }

                    // Back Face (-Z)
                    if (!IsBlock(x,y,z-1))
                    {
                        CreateCubeFace(x, y, z, 3);
                    }
                        
                    // Left Face (-X)
                    if (!IsBlock(x-1,y,z))
                    {
                        CreateCubeFace(x, y, z, 4);
                    }

                    // Right Face (+X)
                    if (!IsBlock(x+1,y,z))
                    {
                        CreateCubeFace(x, y, z, 5);
                    }
                }
            }
        }
        
        _indicesCount = (uint)_indices.Count;
        
        // Model Matrix initialisieren (basierend auf Chunk Position)
        model = Matrix4x4.CreateTranslation(new Vector3(ChunkPosition.X*32, ChunkPosition.Y*32, ChunkPosition.Z*32));
    }

    /// <summary>
    /// Lädt die berechneten Mesh-Daten auf die GPU hoch.
    /// MUSS auf dem Main-Thread (OpenGL-Kontext) aufgerufen werden
    /// </summary>
    public void UploadToGpu(GL gl)
    {
        if (_uploaded) return;
        _gl = gl;

        // Buffer erstellen (OpenGL-Calls - nur auf dem Main-Thread!)
        _ebo = new BufferObject<uint>(_gl, _indices.ToArray(), BufferTargetARB.ElementArrayBuffer);
        _vbo = new BufferObject<float>(_gl, _vertices.ToArray(), BufferTargetARB.ArrayBuffer);
        _vao = new VertexArrayObject<float, uint>(_gl, _vbo, _ebo);

        // Layout (Position=3 + Layer= + Brightness=1) => Stride = 6
        _vao.VertexAttributePointer(0, 3, VertexAttribPointerType.Float, 5, 0); // aPos (x,y,z)
        _vao.VertexAttributePointer(1, 1, VertexAttribPointerType.Float, 5, 3); // layer
        _vao.VertexAttributePointer(2, 1, VertexAttribPointerType.Float, 5, 4); // brightness

        this._vertices = null;
        this._indices = null;
        _uploaded = true;
    }
    
    /// <summary>
    /// Ob die Daten bereits auf die GPU hochgeladen wurden.
    /// </summary>
    public bool IsUploaded => _uploaded;

    
    
    /// <summary>
    /// AO basierend darauf wie viele blöcke daneben stehen
    /// </summary>
    private void CreateCubeFace(int x, int y, int z, int face)
    {
        int id = _blockData[x, y, z];
        byte textureLayer  = BlockTextures.Get(id, face);
        float[] ao = CalcVertexBrightness(x, y, z, face);
        
        switch (face)
        {
            // Top Face (+Y): Nachbarn auf y+1-Ebene in ±X und ±Z
            case BlockTextures.Top:
            {

                _vertices.AddRange(new float[]
                {
                    x,     y + 1, z + 1, textureLayer, ao[0],
                    x + 1, y + 1, z + 1, textureLayer, ao[1],
                    x + 1, y + 1, z,     textureLayer, ao[2],
                    x,     y + 1, z,     textureLayer, ao[3]
                });
                break;
            }

            // Bottom Face (-Y): Nachbarn auf y-Ebene in ±X und ±Z
            case BlockTextures.Bottom:
            {

                _vertices.AddRange(new float[]
                {
                    x,     y, z,     textureLayer, ao[0],
                    x + 1, y, z,     textureLayer, ao[1],
                    x + 1, y, z + 1, textureLayer, ao[2],
                    x,     y, z + 1, textureLayer, ao[3]
                });
                break;
            }

            // Front Face (+Z): Nachbarn auf z+1-Ebene in ±X und ±Y
            case BlockTextures.Front:
            {

                _vertices.AddRange(new float[]
                {
                    x,     y,     z + 1, textureLayer, ao[0],
                    x + 1, y,     z + 1, textureLayer, ao[1],
                    x + 1, y + 1, z + 1, textureLayer, ao[2],
                    x,     y + 1, z + 1, textureLayer, ao[3]
                });
                break;
            }

            // Back Face (-Z): Nachbarn auf z-Ebene in ±X und ±Y
            case BlockTextures.Back:
            {


                _vertices.AddRange(new float[]
                {
                    x + 1, y,     z, textureLayer, ao[0],
                    x,     y,     z, textureLayer, ao[1],
                    x,     y + 1, z, textureLayer, ao[2],
                    x + 1, y + 1, z, textureLayer, ao[3]
                });
                break;
            }

            // Left Face (-X): Nachbarn auf x-Ebene in ±Z und ±Y
            case BlockTextures.Left:
            {

                _vertices.AddRange(new float[]
                {
                    x, y,     z,      textureLayer, ao[0],
                    x, y,     z + 1,  textureLayer, ao[1],
                    x, y + 1, z + 1,  textureLayer, ao[2],
                    x, y + 1, z,      textureLayer, ao[3]
                });
                break;
            }

            // Right Face (+X): Nachbarn auf x+1-Ebene in ±Z und ±Y
            case BlockTextures.Right:
            {

                _vertices.AddRange(new float[]
                {
                    x + 1, y,     z + 1, textureLayer, ao[0],
                    x + 1, y,     z,     textureLayer, ao[1],
                    x + 1, y + 1, z,     textureLayer, ao[2],
                    x + 1, y + 1, z + 1, textureLayer, ao[3]
                });
                break;
            }
        }
        AddIndices((uint)_vertices.Count / 5 - 4, ao);
    }
    
    // Relative Offsets für AO Checks
    private static readonly int[,,,] AoOffsets = new int[6, 4, 3, 3]
    {
        // Face 0: Top (+Y) — check on y+1 plane, tangent axes are X and Z
        // Vertices: v0=(x,y+1,z+1), v1=(x+1,y+1,z+1), v2=(x+1,y+1,z), v3=(x,y+1,z)
        {
            // v0: corner at (-X, +Z) → side1=(-1,+1,0), side2=(0,+1,+1), corner=(-1,+1,+1)
            { { -1, 1, 0 }, { 0, 1, 1 }, { -1, 1, 1 } },
            // v1: corner at (+X, +Z) → side1=(+1,+1,0), side2=(0,+1,+1), corner=(+1,+1,+1)
            { { 1, 1, 0 }, { 0, 1, 1 }, { 1, 1, 1 } },
            // v2: corner at (+X, -Z) → side1=(+1,+1,0), side2=(0,+1,-1), corner=(+1,+1,-1)
            { { 1, 1, 0 }, { 0, 1, -1 }, { 1, 1, -1 } },
            // v3: corner at (-X, -Z) → side1=(-1,+1,0), side2=(0,+1,-1), corner=(-1,+1,-1)
            { { -1, 1, 0 }, { 0, 1, -1 }, { -1, 1, -1 } },
        },
        // Face 1: Bottom (-Y) — check on y-1 plane, tangent axes are X and Z
        // Vertices: v0=(x,y,z), v1=(x+1,y,z), v2=(x+1,y,z+1), v3=(x,y,z+1)
        {
            // v0: corner at (-X, -Z) → side1=(-1,-1,0), side2=(0,-1,-1), corner=(-1,-1,-1)
            { { -1, -1, 0 }, { 0, -1, -1 }, { -1, -1, -1 } },
            // v1: corner at (+X, -Z) → side1=(+1,-1,0), side2=(0,-1,-1), corner=(+1,-1,-1)
            { { 1, -1, 0 }, { 0, -1, -1 }, { 1, -1, -1 } },
            // v2: corner at (+X, +Z) → side1=(+1,-1,0), side2=(0,-1,+1), corner=(+1,-1,+1)
            { { 1, -1, 0 }, { 0, -1, 1 }, { 1, -1, 1 } },
            // v3: corner at (-X, +Z) → side1=(-1,-1,0), side2=(0,-1,+1), corner=(-1,-1,+1)
            { { -1, -1, 0 }, { 0, -1, 1 }, { -1, -1, 1 } },
        },
        // Face 2: Front (+Z) — check on z+1 plane, tangent axes are X and Y
        // Vertices: v0=(x,y,z+1), v1=(x+1,y,z+1), v2=(x+1,y+1,z+1), v3=(x,y+1,z+1)
        {
            // v0: corner at (-X, -Y) → side1=(-1,0,+1), side2=(0,-1,+1), corner=(-1,-1,+1)
            { { -1, 0, 1 }, { 0, -1, 1 }, { -1, -1, 1 } },
            // v1: corner at (+X, -Y) → side1=(+1,0,+1), side2=(0,-1,+1), corner=(+1,-1,+1)
            { { 1, 0, 1 }, { 0, -1, 1 }, { 1, -1, 1 } },
            // v2: corner at (+X, +Y) → side1=(+1,0,+1), side2=(0,+1,+1), corner=(+1,+1,+1)
            { { 1, 0, 1 }, { 0, 1, 1 }, { 1, 1, 1 } },
            // v3: corner at (-X, +Y) → side1=(-1,0,+1), side2=(0,+1,+1), corner=(-1,+1,+1)
            { { -1, 0, 1 }, { 0, 1, 1 }, { -1, 1, 1 } },
        },
        // Face 3: Back (-Z) — check on z-1 plane, tangent axes are X and Y
        // Vertices: v0=(x+1,y,z), v1=(x,y,z), v2=(x,y+1,z), v3=(x+1,y+1,z)
        {
            // v0: corner at (+X, -Y) → side1=(+1,0,-1), side2=(0,-1,-1), corner=(+1,-1,-1)
            { { 1, 0, -1 }, { 0, -1, -1 }, { 1, -1, -1 } },
            // v1: corner at (-X, -Y) → side1=(-1,0,-1), side2=(0,-1,-1), corner=(-1,-1,-1)
            { { -1, 0, -1 }, { 0, -1, -1 }, { -1, -1, -1 } },
            // v2: corner at (-X, +Y) → side1=(-1,0,-1), side2=(0,+1,-1), corner=(-1,+1,-1)
            { { -1, 0, -1 }, { 0, 1, -1 }, { -1, 1, -1 } },
            // v3: corner at (+X, +Y) → side1=(+1,0,-1), side2=(0,+1,-1), corner=(+1,+1,-1)
            { { 1, 0, -1 }, { 0, 1, -1 }, { 1, 1, -1 } },
        },
        // Face 4: Left (-X) — check on x-1 plane, tangent axes are Z and Y
        // Vertices: v0=(x,y,z), v1=(x,y,z+1), v2=(x,y+1,z+1), v3=(x,y+1,z)
        {
            // v0: corner at (-Z, -Y) → side1=(-1,0,-1), side2=(-1,-1,0), corner=(-1,-1,-1)
            { { -1, 0, -1 }, { -1, -1, 0 }, { -1, -1, -1 } },
            // v1: corner at (+Z, -Y) → side1=(-1,0,+1), side2=(-1,-1,0), corner=(-1,-1,+1)
            { { -1, 0, 1 }, { -1, -1, 0 }, { -1, -1, 1 } },
            // v2: corner at (+Z, +Y) → side1=(-1,0,+1), side2=(-1,+1,0), corner=(-1,+1,+1)
            { { -1, 0, 1 }, { -1, 1, 0 }, { -1, 1, 1 } },
            // v3: corner at (-Z, +Y) → side1=(-1,0,-1), side2=(-1,+1,0), corner=(-1,+1,-1)
            { { -1, 0, -1 }, { -1, 1, 0 }, { -1, 1, -1 } },
        },
        // Face 5: Right (+X) — check on x+1 plane, tangent axes are Z and Y
        // Vertices: v0=(x+1,y,z+1), v1=(x+1,y,z), v2=(x+1,y+1,z), v3=(x+1,y+1,z+1)
        {
            // v0: corner at (+Z, -Y) → side1=(+1,0,+1), side2=(+1,-1,0), corner=(+1,-1,+1)
            { { 1, 0, 1 }, { 1, -1, 0 }, { 1, -1, 1 } },
            // v1: corner at (-Z, -Y) → side1=(+1,0,-1), side2=(+1,-1,0), corner=(+1,-1,-1)
            { { 1, 0, -1 }, { 1, -1, 0 }, { 1, -1, -1 } },
            // v2: corner at (-Z, +Y) → side1=(+1,0,-1), side2=(+1,+1,0), corner=(+1,+1,-1)
            { { 1, 0, -1 }, { 1, 1, 0 }, { 1, 1, -1 } },
            // v3: corner at (+Z, +Y) → side1=(+1,0,+1), side2=(+1,+1,0), corner=(+1,+1,+1)
            { { 1, 0, 1 }, { 1, 1, 0 }, { 1, 1, 1 } },
        },
    };
    /// <summary>
    /// AO basierend darauf wie viele blöcke daneben stehen
    /// Nimmt die Koordinaten und das Face und checkt ob in der Richtung Blöcke sind, je mehr Blöcke desto dunkler
    /// Die Werte werden an CreateCubeFace übergeben und als zusätzliche Vertex-Attribute gespeichert, damit der Shader sie nutzen kann
    /// </summary>
    /// 
    private float[] CalcVertexBrightness(int x, int y, int z, int face)
    {
        float[] ao = new float[4]; // AO Werte für die 4 Ecken des Faces
        // Iteriere über die 4 Eckpunkte (Vertices) des Faces
        for (int v = 0; v < 4; v++)
        {
            int dxS1 = AoOffsets[face, v, 0, 0];
            int dyS1 = AoOffsets[face, v, 0, 1];
            int dzS1 = AoOffsets[face, v, 0, 2];

            int dxS2 = AoOffsets[face, v, 1, 0];
            int dyS2 = AoOffsets[face, v, 1, 1];
            int dzS2 = AoOffsets[face, v, 1, 2];

            int dxC = AoOffsets[face, v, 2, 0];
            int dyC = AoOffsets[face, v, 2, 1];
            int dzC = AoOffsets[face, v, 2, 2];

            // 2. Checke die 3 Blöcke im Array
            bool side1 = IsBlock(x + dxS1, y + dyS1, z + dzS1);
            bool side2 = IsBlock(x + dxS2, y + dyS2, z + dzS2);
            bool corner = IsBlock(x + dxC, y + dyC, z + dzC);

            // 3. Wende die AO-Logik an
            int aoLevel;
            if (side1 && side2)
            {
                aoLevel = 3; // Maximaler Schatten (verhindert Light-Bleeding)
            }
            else
            {
                int s1Val = side1 ? 1 : 0;
                int s2Val = side2 ? 1 : 0;
                int cVal = corner ? 1 : 0;

                aoLevel = s1Val + s2Val + cVal;
            }
            
            // aoLevel 0 = keine Nachbarn = hell (1.0), aoLevel 3 = max Schatten (0.4)
            ao[v] = 1.0f - aoLevel * 0.2f;
        }

        // Gebe die 4 Werte als Tupel zurück
        return ao;
    }
    

    /// <summary>
    /// Gibt true zurück, wenn da keine Luft ist.
    /// </summary>
    private bool IsBlock(int x, int y, int z)
    {
        // Wenn alle Koordinaten im lokalen Bereich liegen → direkt prüfen
        if (x >= 0 && x < 32 && y >= 0 && y < 32 && z >= 0 && z < 32)
        {
            return _blockData[x, y, z] != 0;
        }

        // Nachbar-Chunk-Offset berechnen
        int cx = ChunkPosition.X;
        int cy = ChunkPosition.Y;
        int cz = ChunkPosition.Z;

        // X-Achse normalisieren
        if (x < 0)      { cx--; x += 32; }
        else if (x > 31) { cx++; x -= 32; }

        // Y-Achse normalisieren
        if (y < 0)      { cy--; y += 32; }
        else if (y > 31) { cy++; y -= 32; }

        // Z-Achse normalisieren
        if (z < 0)      { cz--; z += 32; }
        else if (z > 31) { cz++; z -= 32; }

        ChunkCoord neighborCoord = new ChunkCoord(cx, cy, cz);
        if (ChunkProvider.Chunkdata.TryGetValue(neighborCoord, out int[,,]? neighborData))
        {
            return neighborData[x, y, z] != 0;
        }

        // Kein Nachbar-Chunk geladen
        // Sollte nicht passieren können wegen de meshing queue
        return false;
    }

    
    private void AddIndices(uint baseIndex, float[] ao)
    {
        // Quad flippen damit es keine komischen Dreiecke gibt, abhängig davon wie die AO Werte verteilt sind
        if (ao[0] + ao[2] > ao[1] + ao[3])
        {
            _indices.AddRange(new uint[]
            {
                baseIndex + 1, baseIndex + 2, baseIndex + 3,
                baseIndex + 1, baseIndex + 3, baseIndex
            });
        }
        else
        {
            _indices.AddRange(new uint[]
            {
                baseIndex, baseIndex + 1, baseIndex + 2,
                baseIndex, baseIndex + 2, baseIndex + 3
            });
        }
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
