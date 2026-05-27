using System.Numerics;
using System.Runtime.CompilerServices;
using Basics.Configurations;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Utilities;

namespace Basics.Game.Logic.TerrainManaging.Meshing;

public class Lod1Mesher : BaseMesher
{
    private ushort[] _blockData = null!;
    private ushort[][] _neighborCache = new ushort[27][];
    
    public Lod1Mesher(ChunkCoord position, ChunkData chunkData)
    {
        if (chunkData == null || chunkData.Blocks == null) return;
        this.ChunkPosition = position;
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
                        1
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
        
        _vertices.Clear();
        _indices.Clear();
        _vertexCount = 0;
        _vertices.Capacity = EstimatedVertexBytes;
        _indices.Capacity = EstimatedIndices;
        
        ushort[] data = _blockData;

        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 16; y++)
            {
                for (int z = 0; z < 16; z++)
                {
                    int idx = x * 256 + y * 16 + z;

                    // Kein Block, keine Flächen
                    if (data[idx] == 0) continue;

                    // Koordinaten direkt übergeben, IsBlock wurde optimiert
                    if (!IsBlock(x, y + 1, z)) CreateCubeFace(x, y, z, BlockLoader.Top);
                    if (!IsBlock(x, y - 1, z)) CreateCubeFace(x, y, z, BlockLoader.Bottom);
                    if (!IsBlock(x, y, z + 1)) CreateCubeFace(x, y, z, BlockLoader.Front);
                    if (!IsBlock(x, y, z - 1)) CreateCubeFace(x, y, z, BlockLoader.Back);
                    if (!IsBlock(x - 1, y, z)) CreateCubeFace(x, y, z, BlockLoader.Left);
                    if (!IsBlock(x + 1, y, z)) CreateCubeFace(x, y, z, BlockLoader.Right);
                }
            }
        }
        
        _indicesCount = (uint)_indices.Count;
        _vertices.TrimExcess();
        _indices.TrimExcess();
        // Model Matrix: Zuerst skalieren (Blöcke 2x so groß), dann an die richtige Weltposition verschieben
        model = Matrix4x4.CreateScale(2f) * Matrix4x4.CreateTranslation(
            new Vector3(ChunkPosition.X * 16 * 2, ChunkPosition.Y * 16 * 2, ChunkPosition.Z * 16 * 2));
        // brauchen Daten nicht mehr im RAM, sind in der ChunkProvider.Chunkdata gespeichert
        _neighborCache = null;
        _blockData = null;
    }
    
    private void CreateCubeFace(int x, int y, int z, int face)
    {
        int id = _blockData[x * 256 + y * 16 + z];
        ushort textureLayer = BlockLoader.Get(id, face);
        
        switch (face)
        {
            case BlockLoader.Top:
                AddVertex(x,     y + 1, z + 1, textureLayer);
                AddVertex(x + 1, y + 1, z + 1, textureLayer);
                AddVertex(x + 1, y + 1, z,     textureLayer);
                AddVertex(x,     y + 1, z,     textureLayer);
                break;
            case BlockLoader.Bottom:
                AddVertex(x,     y, z,     textureLayer);
                AddVertex(x + 1, y, z,     textureLayer);
                AddVertex(x + 1, y, z + 1, textureLayer);
                AddVertex(x,     y, z + 1, textureLayer);
                break;
            case BlockLoader.Front:
                AddVertex(x,     y,     z + 1, textureLayer);
                AddVertex(x + 1, y,     z + 1, textureLayer);
                AddVertex(x + 1, y + 1, z + 1, textureLayer);
                AddVertex(x,     y + 1, z + 1, textureLayer);
                break;
            case BlockLoader.Back:
                AddVertex(x + 1, y,     z, textureLayer);
                AddVertex(x,     y,     z, textureLayer);
                AddVertex(x,     y + 1, z, textureLayer);
                AddVertex(x + 1, y + 1, z, textureLayer);
                break;
            case BlockLoader.Left:
                AddVertex(x, y,     z,     textureLayer);
                AddVertex(x, y,     z + 1, textureLayer);
                AddVertex(x, y + 1, z + 1, textureLayer);
                AddVertex(x, y + 1, z,     textureLayer);
                break;
            case BlockLoader.Right:
                AddVertex(x + 1, y,     z + 1, textureLayer);
                AddVertex(x + 1, y,     z,     textureLayer);
                AddVertex(x + 1, y + 1, z,     textureLayer);
                AddVertex(x + 1, y + 1, z + 1, textureLayer);
                break;
        }
        
        // Indices direkt hinzufügen, keine Array-Allokation
        uint baseIdx = (uint)(_vertexCount - 4);
        
        _indices.Add(baseIdx);     _indices.Add(baseIdx + 1); _indices.Add(baseIdx + 2);
        _indices.Add(baseIdx);     _indices.Add(baseIdx + 2); _indices.Add(baseIdx + 3);
        
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
}
