using System.Numerics;
using System.Runtime.CompilerServices;
using Basics.Configurations;
using Basics.Game.Utilities;

namespace Basics.Game.Logic.TerrainManaging.Meshing;

public class Lod2Mesher : BaseMesher
{
    private ushort[] _blockData = null!;
    private ushort[][] _neighborCache = new ushort[27][];
    
    public Lod2Mesher(ChunkCoord position, ChunkData chunkData)
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
                        2
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
        _vertices.Capacity = 20_000;
        _indices.Capacity = 5_000;
        
        ushort[] data = _blockData; 

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
        // Model Matrix: Zuerst skalieren (Blöcke 4x so groß), dann an die richtige Weltposition verschieben
        model = Matrix4x4.CreateScale(4f) * Matrix4x4.CreateTranslation(new Vector3(ChunkPosition.X * 32 * 4, ChunkPosition.Y * 32 * 4, ChunkPosition.Z * 32 * 4));
        // brauchen Daten nicht mehr im RAM, sind in der ChunkProvider.Chunkdata gespeichert
        _neighborCache = null;
        _blockData = null;
    }
    
    private void CreateCubeFace(int x, int y, int z, int face)
    {
        int id = _blockData[x * 1024 + y * 32 + z];
        ushort textureLayer = BlockTextures.Get(id, face);
        
        switch (face)
        {
            case BlockTextures.Top:
                AddVertex(x,     y + 1, z + 1, textureLayer);
                AddVertex(x + 1, y + 1, z + 1, textureLayer);
                AddVertex(x + 1, y + 1, z,     textureLayer);
                AddVertex(x,     y + 1, z,     textureLayer);
                break;
            case BlockTextures.Bottom:
                AddVertex(x,     y, z,     textureLayer);
                AddVertex(x + 1, y, z,     textureLayer);
                AddVertex(x + 1, y, z + 1, textureLayer);
                AddVertex(x,     y, z + 1, textureLayer);
                break;
            case BlockTextures.Front:
                AddVertex(x,     y,     z + 1, textureLayer);
                AddVertex(x + 1, y,     z + 1, textureLayer);
                AddVertex(x + 1, y + 1, z + 1, textureLayer);
                AddVertex(x,     y + 1, z + 1, textureLayer);
                break;
            case BlockTextures.Back:
                AddVertex(x + 1, y,     z, textureLayer);
                AddVertex(x,     y,     z, textureLayer);
                AddVertex(x,     y + 1, z, textureLayer);
                AddVertex(x + 1, y + 1, z, textureLayer);
                break;
            case BlockTextures.Left:
                AddVertex(x, y,     z,     textureLayer);
                AddVertex(x, y,     z + 1, textureLayer);
                AddVertex(x, y + 1, z + 1, textureLayer);
                AddVertex(x, y + 1, z,     textureLayer);
                break;
            case BlockTextures.Right:
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
        ushort[] neighborData = _neighborCache[cx * 9 + cy * 3 + cz];

        if (neighborData != null)
        {
            return neighborData[x * 1024 + y * 32 + z] != 0;
        }

        return false; // Nachbar nicht geladen
    }
}