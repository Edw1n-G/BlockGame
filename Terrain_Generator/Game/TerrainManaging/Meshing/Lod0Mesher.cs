using System.Numerics;
using Basics.Configurations;
using Basics.Game.TerrainManaging.Meshing;
using Basics.Utilities;
using System.Runtime.CompilerServices;

namespace Basics.Game.TerrainManaging;

public class Lod0Mesher : BaseMesher
{
    private byte[] _blockData;
    private byte[][] _neighborCache = new byte[27][]; //Is Block wird verdammt oft aufgerufen, pointer auf die Nachbarn während des ersten mes
    
    // AO Level (0–3) wird als int an den Shader gesendet, Konvertierung in float passiert dort
    
    public Lod0Mesher(ChunkCoord position, byte[] blockData)
    {
        if (blockData == null) return;
        this.ChunkPosition = position;
        this._blockData = blockData;
        CreateNeighborCache();
        BuildMeshData();
    }
    
    private void CreateNeighborCache()
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    // Index im 1D-Array berechnen dafür ist KI gut
                    int cacheIndex = (dx + 1) * 9 + (dy + 1) * 3 + (dz + 1);

                    // Wenn es der eigene Chunk ist den Cache direkt auf die Blockdaten setzen
                    // Dann muss man nicht hin und her wechseln
                    if (dx == 0 && dy == 0 && dz == 0)
                    {
                        _neighborCache[cacheIndex] = _blockData;
                        continue;
                    }

                    ChunkCoord neighborCoord = new ChunkCoord(
                        ChunkPosition.X + dx, 
                        ChunkPosition.Y + dy, 
                        ChunkPosition.Z + dz
                    );

                    
                    if (ChunkProvider.Chunkdata.TryGetValue(neighborCoord, out byte[] neighborData))
                    {
                        _neighborCache[cacheIndex] = neighborData;
                    }
                    else
                    {
                        _neighborCache[cacheIndex] = null; // Nachbar ist (noch) nicht geladen
                    }
                }
            }
        }
    }
    
    private void BuildMeshData()
    {
        // Worst-case Schätzung: Ein Chunk mit vielen Oberflächen hat ca. 30% sichtbare Flächen
        // Pro Face: 4 Vertices × 14 Bytes = 56 Bytes, 6 indices
        // Realistisch: ~8000 Faces → 448k Bytes, 48k indices
        _vertices.Clear();
        _indices.Clear();
        _vertexCount = 0;
        _vertices.Capacity = 450_000;
        _indices.Capacity = 48_000;
        
        byte[] data = _blockData; 

        for (int x = 0; x < 32; x++)
        {
            for (int y = 0; y < 32; y++)
            {
                for (int z = 0; z < 32; z++)
                {
                    // Kein Block, keine Flächen
                    if (data[x * 1024 + y * 32 + z] == 0) continue;

                    // Koordinaten direkt übergeben, IsBlock wurde optimiert
                    if (!IsBlock(x, y + 1, z)) CreateCubeFace(x, y, z, BlockTextures.Top);
                    if (!IsBlock(x, y - 1, z)) CreateCubeFace(x, y, z, BlockTextures.Bottom);
                    if (!IsBlock(x, y, z + 1)) CreateCubeFace(x, y, z, BlockTextures.Front);
                    if (!IsBlock(x, y, z - 1)) CreateCubeFace(x, y, z, BlockTextures.Back);
                    if (!IsBlock(x - 1, y, z)) CreateCubeFace(x, y, z, BlockTextures.Left);
                    if (!IsBlock(x + 1, y, z)) CreateCubeFace(x, y, z, BlockTextures.Right);
                }
            }
        }
        
        _indicesCount = (uint)_indices.Count;
        _vertices.TrimExcess();
        _indices.TrimExcess();
        // Model Matrix initialisieren (basierend auf Chunk Position)
        model = Matrix4x4.CreateTranslation(new Vector3(ChunkPosition.X*32, ChunkPosition.Y*32, ChunkPosition.Z*32));
        // brauchen Daten nicht mehr im RAM, sind in der ChunkProvider.Chunkdata gespeichert
        _neighborCache = null;
        _blockData = null;
    }
    
    private void CreateCubeFace(int x, int y, int z, int face)
    {
        int id = _blockData[x * 1024 + y * 32 + z];
        byte textureLayer = BlockTextures.Get(id, face);
        
        // AO direkt als int berechnen (0–3)
        byte ao0 = CalcAoLevel(x, y, z, face, 0);
        byte ao1 = CalcAoLevel(x, y, z, face, 1);
        byte ao2 = CalcAoLevel(x, y, z, face, 2);
        byte ao3 = CalcAoLevel(x, y, z, face, 3);
        
        switch (face)
        {
            case BlockTextures.Top:
                AddVertex(x,     y + 1, z + 1, textureLayer, ao0);
                AddVertex(x + 1, y + 1, z + 1, textureLayer, ao1);
                AddVertex(x + 1, y + 1, z,     textureLayer, ao2);
                AddVertex(x,     y + 1, z,     textureLayer, ao3);
                break;
            case BlockTextures.Bottom:
                AddVertex(x,     y, z,     textureLayer, ao0);
                AddVertex(x + 1, y, z,     textureLayer, ao1);
                AddVertex(x + 1, y, z + 1, textureLayer, ao2);
                AddVertex(x,     y, z + 1, textureLayer, ao3);
                break;
            case BlockTextures.Front:
                AddVertex(x,     y,     z + 1, textureLayer, ao0);
                AddVertex(x + 1, y,     z + 1, textureLayer, ao1);
                AddVertex(x + 1, y + 1, z + 1, textureLayer, ao2);
                AddVertex(x,     y + 1, z + 1, textureLayer, ao3);
                break;
            case BlockTextures.Back:
                AddVertex(x + 1, y,     z, textureLayer, ao0);
                AddVertex(x,     y,     z, textureLayer, ao1);
                AddVertex(x,     y + 1, z, textureLayer, ao2);
                AddVertex(x + 1, y + 1, z, textureLayer, ao3);
                break;
            case BlockTextures.Left:
                AddVertex(x, y,     z,     textureLayer, ao0);
                AddVertex(x, y,     z + 1, textureLayer, ao1);
                AddVertex(x, y + 1, z + 1, textureLayer, ao2);
                AddVertex(x, y + 1, z,     textureLayer, ao3);
                break;
            case BlockTextures.Right:
                AddVertex(x + 1, y,     z + 1, textureLayer, ao0);
                AddVertex(x + 1, y,     z,     textureLayer, ao1);
                AddVertex(x + 1, y + 1, z,     textureLayer, ao2);
                AddVertex(x + 1, y + 1, z + 1, textureLayer, ao3);
                break;
        }
        
        // Indices direkt hinzufügen, keine Array-Allokation
        uint baseIdx = (uint)(_vertexCount - 4);
        if (ao0 + ao2 > ao1 + ao3)
        {
            _indices.Add(baseIdx + 1); _indices.Add(baseIdx + 2); _indices.Add(baseIdx + 3);
            _indices.Add(baseIdx + 1); _indices.Add(baseIdx + 3); _indices.Add(baseIdx);
        }
        else
        {
            _indices.Add(baseIdx);     _indices.Add(baseIdx + 1); _indices.Add(baseIdx + 2);
            _indices.Add(baseIdx);     _indices.Add(baseIdx + 2); _indices.Add(baseIdx + 3);
        }
    }
    /// <summary>
    /// Gibt true zurück, wenn da keine Luft ist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)] // Black magic 
    private bool IsBlock(int x, int y, int z)
    {
        // Der "uint" Trick: Prüft (>= 0 UND < 32)
        if ((uint)x < 32u && (uint)y < 32u && (uint)z < 32u)
        {
            // Blitzschneller Array-Zugriff. 
            // 1024 ist 32*32 vorab ausgerechnet, das spart Multiplikationen!
            return _blockData[x * 1024 + y * 32 + z] != 0; 
        }

        // Wenn der Block AUßERHALB liegt (x=-1, z=32 etc.), gehe in den langsameren Pfad
        return IsBlockNeighbor(x, y, z);
    }

    // Diese Methode wird nur aufgerufen, wenn wir WIRKLICH über die Chunk-Grenze gucken
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool IsBlockNeighbor(int x, int y, int z)
    {
        int cx = 1;
        int cy = 1;
        int cz = 1;

        // Koordinaten "wrappen" und Nachbar-Index berechnen
        if (x < 0)       { cx = 0; x += 32; }
        else if (x > 31) { cx = 2; x -= 32; }

        if (y < 0)       { cy = 0; y += 32; }
        else if (y > 31) { cy = 2; y -= 32; }

        if (z < 0)       { cz = 0; z += 32; }
        else if (z > 31) { cz = 2; z -= 32; }

        // Cache Index (0 bis 26) berechnen
        byte[] neighborData = _neighborCache[cx * 9 + cy * 3 + cz];

        if (neighborData != null)
        {
            return neighborData[x * 1024 + y * 32 + z] != 0;
        }

        return false; // Nachbar nicht geladen
    }
    
    // sbyte statt int: Werte sind nur -1, 0, +1
    private static readonly sbyte[] AoOffsets = new sbyte[]
    {
        // Face 0: Top (+Y)
        
             -1, 1, 0,  0, 1, 1 ,  -1, 1, 1 ,
             1, 1, 0,   0, 1, 1 ,  1, 1, 1 ,
             1, 1, 0,   0, 1, -1 ,  1, 1, -1 ,
             -1, 1, 0,  0, 1, -1 ,  -1, 1, -1 ,
        
        // Face 1: Bottom (-Y)
        
             -1, -1, 0 ,  0, -1, -1 ,  -1, -1, -1 ,
             1, -1, 0 ,  0, -1, -1 ,  1, -1, -1 ,
             1, -1, 0 ,  0, -1, 1 ,  1, -1, 1 ,
             -1, -1, 0 ,  0, -1, 1 ,  -1, -1, 1 ,
        
        // Face 2: Front (+Z)
        
             -1, 0, 1 ,  0, -1, 1 ,  -1, -1, 1 ,
             1, 0, 1 ,  0, -1, 1 ,  1, -1, 1 ,
             1, 0, 1 ,  0, 1, 1 ,  1, 1, 1 ,
             -1, 0, 1 ,  0, 1, 1 ,  -1, 1, 1 ,
        
        // Face 3: Back (-Z)
        
             1, 0, -1 ,  0, -1, -1 ,  1, -1, -1 ,
             -1, 0, -1 ,  0, -1, -1 ,  -1, -1, -1 ,
             -1, 0, -1 ,  0, 1, -1 ,  -1, 1, -1 ,
             1, 0, -1 ,  0, 1, -1 ,  1, 1, -1 ,
        
        // Face 4: Left (-X)
        
             -1, 0, -1 ,  -1, -1, 0 ,  -1, -1, -1 ,
             -1, 0, 1 ,  -1, -1, 0 ,  -1, -1, 1 ,
             -1, 0, 1 ,  -1, 1, 0 ,  -1, 1, 1 ,
             -1, 0, -1 ,  -1, 1, 0 ,  -1, 1, -1 ,
        
        // Face 5: Right (+X)
        
             1, 0, 1 ,  1, -1, 0 ,  1, -1, 1 ,
             1, 0, -1 ,  1, -1, 0,  1, -1, -1 ,
             1, 0, -1 ,  1, 1, 0 ,  1, 1, -1 ,
             1, 0, 1 ,  1, 1, 0 ,  1, 1, 1 ,
    };
    
    /// <summary>
    /// Berechnet den AO-Level (0–3) für einen einzelnen Vertex.
    /// Gibt direkt einen int zurück, keine Heap-Allokation.
    /// </summary>
    private byte CalcAoLevel(int x, int y, int z, int face, int vertex)
    {
        int offsetIndex = (face * 4 + vertex) * 9;
        bool side1 = IsBlock(x + AoOffsets[offsetIndex], y + AoOffsets[offsetIndex+1], z + AoOffsets[offsetIndex+2]);
        bool side2 = IsBlock(x + AoOffsets[offsetIndex+3], y + AoOffsets[offsetIndex+4], z + AoOffsets[offsetIndex+5]);
        
        if (side1 && side2) return 3;
        
        bool corner = IsBlock(x + AoOffsets[offsetIndex+6], y + AoOffsets[offsetIndex+7], z + AoOffsets[offsetIndex+8]);
        
        return (byte)((side1 ? 1 : 0) + (side2 ? 1 : 0) + (corner ? 1 : 0));
    }
}