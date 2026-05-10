using System.Runtime.CompilerServices;
using Basics.Game.Utilities;

namespace Basics.Game.Logic.TerrainManaging;

/// <summary>
/// Speichert die Blockdaten eines einzelnen Chunks.
/// Bietet Hilfsfunktionen für lokale Koordinaten und Blockzugriff.
/// </summary>
public class ChunkData
{
    public const int ChunkSize = 16;
    public const int ChunkArea = 256;
    public const int BlockCount = 4096;
    
    public ushort[]? Blocks;
    
    public readonly ChunkCoord Coord;
    
    /// <summary>
    /// Wird true wenn Blöcke geändert wurden.
    /// </summary>
    public bool IsDirty { get; set; }

    public ChunkData(ChunkCoord coord, ushort[] blocks)
    {
        Coord = coord;
        Blocks = blocks;
    }
    
    /// <summary>
    /// Erstellt leere Chunkdaten (nur Luft).
    /// </summary>
    public ChunkData(ChunkCoord coord)
    {
        Coord = coord;
        Blocks = null;
    }

    /// <summary>
    /// Rechnet lokale Koordinaten (0-15) in den Array-Index um.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ToIndex(int x, int y, int z)
    {
        return x * 256 + y * 16 + z;
    }

    /// <summary>
    /// Gibt die Block-ID an der lokalen Position zurück.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort GetBlock(int x, int y, int z)
    {
        if (Blocks == null) return 0;
        return Blocks[x * 256 + y * 16 + z];
    }

    /// <summary>
    /// Setzt die Block-ID an der lokalen Position.
    /// Prüft NICHT, ob die Koordinaten gültig sind (Performance)!
    
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBlock(int x, int y, int z, ushort blockId)
    {
        if (Blocks == null)
        {
            if (blockId == 0) return; // Falls ein genie luft in luft setzten will
            Blocks =  new ushort[BlockCount];
        }
        Blocks[x * 256 + y * 16 + z] = blockId;
        IsDirty = true;
    }

    /// <summary>
    /// Gibt die Block-ID zurück, mit Bounds-Check.
    /// Gibt 0 zurück, wenn die Koordinaten außerhalb liegen.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort GetBlockSafe(int x, int y, int z)
    {
        if ((uint)x >= 16u || (uint)y >= 16u || (uint)z >= 16u)
            return 0;
        if (Blocks == null) return 0;
        return Blocks[x * 256 + y * 16 + z];
    }

    /// <summary>
    /// Setzt einen Block mit Bounds-Check.
    /// Gibt false zurück, wenn die Koordinaten außerhalb liegen.
    /// </summary>
    public bool SetBlockSafe(int x, int y, int z, ushort blockId)
    {
        if ((uint)x >= 16u || (uint)y >= 16u || (uint)z >= 16u)
            return false;
        if (Blocks == null)
        {
            if (blockId == 0) return true;
            Blocks = new ushort[BlockCount];
        }
        Blocks[x * 256 + y * 16 + z] = blockId;
        IsDirty = true;
        return true;
    }

    /// <summary>
    /// Prüft ob an der lokalen Position ein solider Block ist (nicht Luft).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsBlock(int x, int y, int z)
    {
        if ((uint)x >= 16u || (uint)y >= 16u || (uint)z >= 16u)
            return false;
        if (Blocks == null) return false;
        return Blocks[x * 256 + y * 16 + z] != 0;
    }

    /// <summary>
    /// Rechnet eine Weltposition in lokale Chunk-Koordinaten um.
    /// </summary>
    public static (int localX, int localY, int localZ) WorldToLocal(int worldX, int worldY, int worldZ)
    {
        // Modulo das auch für negative Werte funktioniert
        int localX = ((worldX % 16) + 16) % 16;
        int localY = ((worldY % 16) + 16) % 16;
        int localZ = ((worldZ % 16) + 16) % 16;
        return (localX, localY, localZ);
    }

    /// <summary>
    /// Rechnet eine Weltposition in die zugehörige ChunkCoord um.
    /// </summary>
    public static ChunkCoord WorldToChunkCoord(int worldX, int worldY, int worldZ, byte lodLevel = 0)
    {
        // Floor-Division für negative Koordinaten
        int chunkX = worldX >= 0 ? worldX / 16 : (worldX - 16 + 1) / 16;
        int chunkY = worldY >= 0 ? worldY / 16 : (worldY - 16 + 1) / 16;
        int chunkZ = worldZ >= 0 ? worldZ / 16 : (worldZ - 16 + 1) / 16;
        return new ChunkCoord(chunkX, chunkY, chunkZ, lodLevel);
    }

    /// <summary>
    /// Prüft ob der Chunk komplett leer ist (nur Luft).
    /// </summary>
    public bool IsEmpty()
    {
        if (Blocks == null) return true;
        for (int i = 0; i < BlockCount; i++)
        {
            if (Blocks[i] != 0) return false;
        }
        return true;
    }
}
