using System.Numerics;
using System.Runtime.CompilerServices;
using Basics.Configurations;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Utilities;

namespace Basics.Game.Logic.TerrainManaging.Meshing;

public class Lod0Mesher : BaseMesher
{
    private ushort[] _blockData;
    private ushort[][]? _neighborCache = new ushort[27][]; //Is Block wird verdammt oft aufgerufen, pointer auf die Nachbarn während des ersten mes
    
    // AO Level (0–3) wird als int an den Shader gesendet, Konvertierung in float passiert dort
    
    public Lod0Mesher(ChunkCoord position, ChunkData chunkData)
    {
        this.ChunkPosition = position;
        if (chunkData.Blocks == null) return;
        this._blockData = chunkData.Blocks;
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
                        ChunkPosition.Z + dz,
                        0
                    );

                    
                    if (ChunkProvider.Chunkdata.TryGetValue(neighborCoord, out ChunkData neighborChunk))
                    {
                        _neighborCache[cacheIndex] = neighborChunk.Blocks;
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
        
        ushort[] data = _blockData;
        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 16; y++)
            {
                for (int z = 0; z < 16; z++)
                {
                    int idx = x * 256 + y * 16 + z;
            
                    // Leere überspringen
                    if (data[idx] == 0) continue; 
                    
                    // innere Blöcke 
                    if (x > 0 && x < 15 && y > 0 && y < 15 && z > 0 && z < 15)
                    {
                        if (data[idx + 16] == 0)   CreateCubeFace(x, y, z, BlockTextures.Top);     // y+1
                        if (data[idx - 16] == 0)   CreateCubeFace(x, y, z, BlockTextures.Bottom);  // y-1
                        if (data[idx + 1] == 0)    CreateCubeFace(x, y, z, BlockTextures.Front);   // z+1
                        if (data[idx - 1] == 0)    CreateCubeFace(x, y, z, BlockTextures.Back);    // z-1
                        if (data[idx - 256] == 0)  CreateCubeFace(x, y, z, BlockTextures.Left);    // x-1
                        if (data[idx + 256] == 0)  CreateCubeFace(x, y, z, BlockTextures.Right);   // x+1
                    }
                    // äußere Blöcke Nachbarchunkcheck nötig 
                    else
                    {
                        if (!IsBlock(x, y + 1, z)) CreateCubeFace(x, y, z, BlockTextures.Top);
                        if (!IsBlock(x, y - 1, z)) CreateCubeFace(x, y, z, BlockTextures.Bottom);
                        if (!IsBlock(x, y, z + 1)) CreateCubeFace(x, y, z, BlockTextures.Front);
                        if (!IsBlock(x, y, z - 1)) CreateCubeFace(x, y, z, BlockTextures.Back);
                        if (!IsBlock(x - 1, y, z)) CreateCubeFace(x, y, z, BlockTextures.Left);
                        if (!IsBlock(x + 1, y, z)) CreateCubeFace(x, y, z, BlockTextures.Right);
                    }
                }
            }
        }
        
        _indicesCount = (uint)_indices.Count;
        
        // Model Matrix initialisieren (basierend auf Chunk Position)
        model = Matrix4x4.CreateTranslation(new Vector3(ChunkPosition.X * 16, ChunkPosition.Y * 16, ChunkPosition.Z * 16));
        // Der Nachbar Cache ist jetzt nicht mehr nötig
        _neighborCache = null;
    }
    
    private void CreateCubeFace(int x, int y, int z, int face)
    {
        int id = _blockData[x * 256 + y * 16 + z];
        ushort textureLayer = BlockTextures.Get(id, face);
        
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
        // Der "uint" Trick: Prüft (>= 0 UND < 16)
        if ((uint)x < 16u && (uint)y < 16u && (uint)z < 16u)
        {
            // Blitzschneller Array-Zugriff. 
            return _blockData[x * 256 + y * 16 + z] != 0;
        }

        // Wenn der Block AUßERHALB liegt (x=-1, z=16 etc.), gehe in den langsameren Pfad
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
        if (x < 0)       { cx = 0; x += 16; }
        else if (x > 15) { cx = 2; x -= 16; }

        if (y < 0)       { cy = 0; y += 16; }
        else if (y > 15) { cy = 2; y -= 16; }

        if (z < 0)       { cz = 0; z += 16; }
        else if (z > 15) { cz = 2; z -= 16; }

        // Cache Index (0 bis 26) berechnen
        ushort[] neighborData = _neighborCache[cx * 9 + cy * 3 + cz];

        if (neighborData != null)
        {
            return neighborData[x * 256 + y * 16 + z] != 0;
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