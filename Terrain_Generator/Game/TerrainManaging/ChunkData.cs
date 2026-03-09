using System.Runtime.CompilerServices;
using Basics.Utilities;

namespace Basics.Game.TerrainManaging;

/// <summary>
/// Speichert die Blockdaten eines einzelnen Chunks.
/// Bietet Hilfsfunktionen für lokale Koordinaten und Blockzugriff.
/// </summary>
public class ChunkData
{
    public const int ChunkSize = 32;
    public const int BlockCount = ChunkSize * ChunkSize * ChunkSize;
    
    
    public readonly byte[] Blocks;
    
    public readonly ChunkCoord Coord;
    
    /// <summary>
    /// Wird true wenn Blöcke geändert wurden.
    /// </summary>
    public bool IsDirty { get; set; }

    public ChunkData(ChunkCoord coord, byte[] blocks)
    {
        Coord = coord;
        Blocks = blocks ?? new byte[BlockCount];
    }
    
    /// <summary>
    /// Erstellt leere Chunkdaten (nur Luft).
    /// </summary>
    public ChunkData(ChunkCoord coord)
    {
        Coord = coord;
        Blocks = new byte[BlockCount];
    }

    /// <summary>
    /// Rechnet lokale Koordinaten (0-31) in den Array-Index um.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ToIndex(int x, int y, int z)
    {
        return x * 1024 + y * 32 + z;
    }

    /// <summary>
    /// Gibt die Block-ID an der lokalen Position zurück.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte GetBlock(int x, int y, int z)
    {
        return Blocks[x * 1024 + y * 32 + z];
    }

    /// <summary>
    /// Setzt die Block-ID an der lokalen Position.
    /// Prüft NICHT ob die Koordinaten gültig sind (Performance)!
    
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBlock(int x, int y, int z, byte blockId)
    {
        Blocks[x * 1024 + y * 32 + z] = blockId;
        IsDirty = true;
    }

    /// <summary>
    /// Gibt die Block-ID zurück, mit Bounds-Check.
    /// Gibt 0 zurück wenn die Koordinaten außerhalb liegen.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte GetBlockSafe(int x, int y, int z)
    {
        if ((uint)x >= ChunkSize || (uint)y >= ChunkSize || (uint)z >= ChunkSize)
            return 0;
        return Blocks[x * 1024 + y * 32 + z];
    }

    /// <summary>
    /// Setzt einen Block mit Bounds-Check.
    /// Gibt false zurück wenn die Koordinaten außerhalb liegen.
    /// </summary>
    public bool SetBlockSafe(int x, int y, int z, byte blockId)
    {
        if ((uint)x >= ChunkSize || (uint)y >= ChunkSize || (uint)z >= ChunkSize)
            return false;
        Blocks[x * 1024 + y * 32 + z] = blockId;
        IsDirty = true;
        return true;
    }

    /// <summary>
    /// Prüft ob an der lokalen Position ein solider Block ist (nicht Luft).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsBlock(int x, int y, int z)
    {
        if ((uint)x >= ChunkSize || (uint)y >= ChunkSize || (uint)z >= ChunkSize)
            return false;
        return Blocks[x * 1024 + y * 32 + z] != 0;
    }

    /// <summary>
    /// Rechnet eine Weltposition in lokale Chunk-Koordinaten um.
    /// </summary>
    public static (int localX, int localY, int localZ) WorldToLocal(int worldX, int worldY, int worldZ)
    {
        // Modulo das auch für negative Werte funktioniert
        int localX = ((worldX % ChunkSize) + ChunkSize) % ChunkSize;
        int localY = ((worldY % ChunkSize) + ChunkSize) % ChunkSize;
        int localZ = ((worldZ % ChunkSize) + ChunkSize) % ChunkSize;
        return (localX, localY, localZ);
    }

    /// <summary>
    /// Rechnet eine Weltposition in die zugehörige ChunkCoord um.
    /// </summary>
    public static ChunkCoord WorldToChunkCoord(int worldX, int worldY, int worldZ, byte lodLevel = 0)
    {
        // Floor-Division für negative Koordinaten
        int chunkX = worldX >= 0 ? worldX / ChunkSize : (worldX - ChunkSize + 1) / ChunkSize;
        int chunkY = worldY >= 0 ? worldY / ChunkSize : (worldY - ChunkSize + 1) / ChunkSize;
        int chunkZ = worldZ >= 0 ? worldZ / ChunkSize : (worldZ - ChunkSize + 1) / ChunkSize;
        return new ChunkCoord(chunkX, chunkY, chunkZ, lodLevel);
    }

    /// <summary>
    /// Prüft ob der Chunk komplett leer ist (nur Luft).
    /// </summary>
    public bool IsEmpty()
    {
        for (int i = 0; i < BlockCount; i++)
        {
            if (Blocks[i] != 0) return false;
        }
        return true;
    }
}

