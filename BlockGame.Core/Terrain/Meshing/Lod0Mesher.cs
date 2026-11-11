using Basics.Blocks;
using Basics.Common;
using Basics.Terrain;
using Basics.Terrain.Chunks;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Buffers.Binary;

namespace Basics.Terrain.Meshing;

/// <summary>
/// Meshing-Implementierung für LOD-Level 0 (feine Auflösung).
/// Jeder Block repräsentiert 1x1x1 Weltblock.
/// Berechnet Ambient Occlusion (AO) für realistische Schattierung.
/// </summary>
public class Lod0Mesher : BaseMesher
{
    private ushort[] _blockData;
    private ushort[][]? _neighborCache = new ushort[27][]; //Is Block wird verdammt oft aufgerufen, pointer auf die Nachbarn während des ersten mes
    
    // AO Level (0–3) wird als int an den Shader gesendet, Konvertierung in float passiert dort
    
    public Lod0Mesher(ChunkCoord position, ChunkData chunkData, ChunkProvider? provider = null, BlockRegistry? blockRegistry = null)
    : base(provider, blockRegistry)
    {
        this.ChunkPosition = position;
        SourceChunkData = chunkData;
        if (chunkData.Blocks == null) return;
        this._blockData = chunkData.Blocks;
        CreateNeighborCache();
        BuildMeshData();
    }
    
    private void CreateNeighborCache()
    {
        for (int idx = 0; idx < 27; idx++)
        {
            int dx = idx / 9 - 1;
            int dy = (idx / 3) % 3 - 1;
            int dz = idx % 3 - 1;

            int cacheIndex = idx;

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

            if (_provider.Chunkdata.TryGetValue(neighborCoord, out ChunkData neighborChunk))
            {
                _neighborCache[cacheIndex] = neighborChunk.Blocks;
            }
            else
            {
                _neighborCache[cacheIndex] = null; // Nachbar ist (noch) nicht geladen
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
        for (int idx = 0; idx < 16 * 16 * 16; idx++)
        {
            if (data[idx] == 0) continue;

            int x = idx >> 8;         // idx / 256
            int y = (idx >> 4) & 0xF; // (idx / 16) % 16
            int z = idx & 0xF;        // idx % 16

            // innere Blöcke 
            if (x > 0 && x < 15 && y > 0 && y < 15 && z > 0 && z < 15)
            {
                if (data[idx + 16] == 0)   CreateCubeFace(x, y, z, BlockDefinitionLoader.Top);     // y+1
                if (data[idx - 16] == 0)   CreateCubeFace(x, y, z, BlockDefinitionLoader.Bottom);  // y-1
                if (data[idx + 1] == 0)    CreateCubeFace(x, y, z, BlockDefinitionLoader.Front);   // z+1
                if (data[idx - 1] == 0)    CreateCubeFace(x, y, z, BlockDefinitionLoader.Back);    // z-1
                if (data[idx - 256] == 0)  CreateCubeFace(x, y, z, BlockDefinitionLoader.Left);    // x-1
                if (data[idx + 256] == 0)  CreateCubeFace(x, y, z, BlockDefinitionLoader.Right);   // x+1
            }
            // äußere Blöcke Nachbarchunkcheck nötig 
            else
            {
                if (!IsBlock(x, y + 1, z)) CreateCubeFace(x, y, z, BlockDefinitionLoader.Top);
                if (!IsBlock(x, y - 1, z)) CreateCubeFace(x, y, z, BlockDefinitionLoader.Bottom);
                if (!IsBlock(x, y, z + 1)) CreateCubeFace(x, y, z, BlockDefinitionLoader.Front);
                if (!IsBlock(x, y, z - 1)) CreateCubeFace(x, y, z, BlockDefinitionLoader.Back);
                if (!IsBlock(x - 1, y, z)) CreateCubeFace(x, y, z, BlockDefinitionLoader.Left);
                if (!IsBlock(x + 1, y, z)) CreateCubeFace(x, y, z, BlockDefinitionLoader.Right);
            }
        }
        
        _indicesCount = (uint)_indices.Count;
        
        // Model Matrix initialisieren (basierend auf Chunk Position)
        model = Matrix4x4.CreateTranslation(new Vector3(ChunkPosition.X * 16, ChunkPosition.Y * 16, ChunkPosition.Z * 16));
        // Der Nachbar Cache ist jetzt nicht mehr nötig
        _neighborCache = null;
    }
    
    // Reserve one complete vertex in the pooled list before taking a borrowed span.
    // Explicit little-endian writes preserve the existing GPU byte layout and zero padding.
    private new void AddVertex(float x, float y, float z, ushort layer, byte aoLevel)
    {
        int offset = _vertices.Count;
        CollectionsMarshal.SetCount(_vertices, checked(offset + 16));
        Span<byte> vertex = CollectionsMarshal.AsSpan(_vertices).Slice(offset, 16);
        BinaryPrimitives.WriteSingleLittleEndian(vertex, x);
        BinaryPrimitives.WriteSingleLittleEndian(vertex.Slice(4), y);
        BinaryPrimitives.WriteSingleLittleEndian(vertex.Slice(8), z);
        BinaryPrimitives.WriteUInt16LittleEndian(vertex.Slice(12), layer);
        vertex[14] = aoLevel;
        vertex[15] = 0;
        _vertexCount++;
    }

    private void CreateCubeFace(int x, int y, int z, int face)
    {
        int id = _blockData[x * 256 + y * 16 + z];
        ushort textureLayer = _blockRegistry.Get(id, face);
        
        // AO direkt als int berechnen (0–3)
        byte ao0 = CalcAoLevel(x, y, z, face, 0);
        byte ao1 = CalcAoLevel(x, y, z, face, 1);
        byte ao2 = CalcAoLevel(x, y, z, face, 2);
        byte ao3 = CalcAoLevel(x, y, z, face, 3);
        
        switch (face)
        {
            case BlockDefinitionLoader.Top:
                AddVertex(x,     y + 1, z + 1, textureLayer, ao0);
                AddVertex(x + 1, y + 1, z + 1, textureLayer, ao1);
                AddVertex(x + 1, y + 1, z,     textureLayer, ao2);
                AddVertex(x,     y + 1, z,     textureLayer, ao3);
                break;
            case BlockDefinitionLoader.Bottom:
                AddVertex(x,     y, z,     textureLayer, ao0);
                AddVertex(x + 1, y, z,     textureLayer, ao1);
                AddVertex(x + 1, y, z + 1, textureLayer, ao2);
                AddVertex(x,     y, z + 1, textureLayer, ao3);
                break;
            case BlockDefinitionLoader.Front:
                AddVertex(x,     y,     z + 1, textureLayer, ao0);
                AddVertex(x + 1, y,     z + 1, textureLayer, ao1);
                AddVertex(x + 1, y + 1, z + 1, textureLayer, ao2);
                AddVertex(x,     y + 1, z + 1, textureLayer, ao3);
                break;
            case BlockDefinitionLoader.Back:
                AddVertex(x + 1, y,     z, textureLayer, ao0);
                AddVertex(x,     y,     z, textureLayer, ao1);
                AddVertex(x,     y + 1, z, textureLayer, ao2);
                AddVertex(x + 1, y + 1, z, textureLayer, ao3);
                break;
            case BlockDefinitionLoader.Left:
                AddVertex(x, y,     z,     textureLayer, ao0);
                AddVertex(x, y,     z + 1, textureLayer, ao1);
                AddVertex(x, y + 1, z + 1, textureLayer, ao2);
                AddVertex(x, y + 1, z,     textureLayer, ao3);
                break;
            case BlockDefinitionLoader.Right:
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
        // All coordinates are in 0..15 exactly when their combined high bits are zero.
        // A negative coordinate retains its sign bit and fails the unsigned comparison.
        if ((uint)(x | y | z) < 16u)
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
        // Face/AO queries are at most one block outside the chunk (-1..16).
        // Arithmetic shifts map these to neighbor offsets -1/0/1, including -1.
        int cx = (x >> 4) + 1;
        int cy = (y >> 4) + 1;
        int cz = (z >> 4) + 1;

        // Cache Index (0 bis 26) berechnen
        ushort[] neighborData = _neighborCache[cx * 9 + cy * 3 + cz];

        if (neighborData != null)
        {
            return neighborData[(x & 15) * 256 + (y & 15) * 16 + (z & 15)] != 0;
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
