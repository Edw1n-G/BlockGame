using System.Runtime.CompilerServices;
using Basics.Game.Utilities;

namespace Basics.Game.Logic.TerrainManaging;

/// <summary>
/// Speichert die Blockdaten eines einzelnen Chunks.
/// Bietet Hilfsfunktionen für lokale Koordinaten und Blockzugriff.
/// </summary>
/// <remarks>
/// Ein Chunk hat eine feste Größe von 16×16×16 Blöcken (4096 Blöcke insgesamt).
/// Die Daten werden in einem eindimensionalen Array gespeichert mit der Formel: <c>x * 256 + y * 16 + z</c>.
/// </remarks>
public class ChunkData
{
    /// <summary>Die Größe eines Chunks in Blöcken (16).<summary>
    public const int ChunkSize = 16;
    /// <summary>Die Fläche eines Chunk-Bodens in Blöcken (256).<summary>
    public const int ChunkArea = 256;
    /// <summary>Die Gesamtanzahl der Blöcke in einem Chunk (4096).<summary>
    public const int BlockCount = 4096;
    
    /// <summary>Das Array mit den Block-IDs dieses Chunks.</summary>
    public ushort[]? Blocks;
    
    /// <summary>Die Chunk-Koordinaten dieses Chunks im World-Grid.</summary>
    public readonly ChunkCoord Coord;
    
    /// <summary>
    /// Gibt an, ob Blöcke in diesem Chunk geändert wurden, aber noch nicht im Renderer aktualisiert wurden.
    /// </summary>
    public bool IsDirty { get; set; }

    /// <summary>
    /// Initialisiert eine neue Instanz von <see cref="ChunkData"/> mit den angegebenen Blockdaten.
    /// </summary>
    /// <param name="coord">Die Chunk-Koordinaten.</param>
    /// <param name="blocks">Das Array mit den Block-IDs.</param>
    public ChunkData(ChunkCoord coord, ushort[] blocks)
    {
        Coord = coord;
        Blocks = blocks;
    }
    
    /// <summary>
    /// Initialisiert eine neue Instanz von <see cref="ChunkData"/> mit leeren Daten (nur Luft).
    /// </summary>
    /// <param name="coord">Die Chunk-Koordinaten.</param>
    public ChunkData(ChunkCoord coord)
    {
        Coord = coord;
        Blocks = null;
    }

    /// <summary>
    /// Rechnet lokale Koordinaten (0-15) in den Array-Index um.
    /// </summary>
    /// <param name="x">Die X-Koordinate (0-15).</param>
    /// <param name="y">Die Y-Koordinate (0-15).</param>
    /// <param name="z">Die Z-Koordinate (0-15).</param>
    /// <returns>Der Array-Index für die gegebene Position.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ToIndex(int x, int y, int z)
    {
        return x * 256 + y * 16 + z;
    }

    /// <summary>
    /// Gibt die Block-ID an der lokalen Position zurück.
    /// Keine Bounds-Prüfung für maximale Performance.
    /// </summary>
    /// <param name="x">Die X-Koordinate (0-15).</param>
    /// <param name="y">Die Y-Koordinate (0-15).</param>
    /// <param name="z">Die Z-Koordinate (0-15).</param>
    /// <returns>Die Block-ID an der Position oder 0, wenn der Block nicht existiert.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort GetBlock(int x, int y, int z)
    {
        if (Blocks == null) return 0;
        return Blocks[x * 256 + y * 16 + z];
    }

    /// <summary>
    /// Setzt die Block-ID an der lokalen Position.
    /// Keine Bounds-Prüfung für maximale Performance.
    /// </summary>
    /// <param name="x">Die X-Koordinate (0-15).</param>
    /// <param name="y">Die Y-Koordinate (0-15).</param>
    /// <param name="z">Die Z-Koordinate (0-15).</param>
    /// <param name="blockId">Die neue Block-ID.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBlock(int x, int y, int z, ushort blockId)
    {
        if (Blocks == null)
        {
            if (blockId == 0) return;
            Blocks =  new ushort[BlockCount];
        }
        Blocks[x * 256 + y * 16 + z] = blockId;
        IsDirty = true;
    }

    /// <summary>
    /// Gibt die Block-ID an der lokalen Position zurück mit Bounds-Prüfung.
    /// </summary>
    /// <param name="x">Die X-Koordinate (0-15).</param>
    /// <param name="y">Die Y-Koordinate (0-15).</param>
    /// <param name="z">Die Z-Koordinate (0-15).</param>
    /// <returns>Die Block-ID an der Position oder 0, wenn die Koordinaten außerhalb liegen oder der Block nicht existiert.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort GetBlockSafe(int x, int y, int z)
    {
        if ((uint)x >= 16u || (uint)y >= 16u || (uint)z >= 16u)
            return 0;
        if (Blocks == null) return 0;
        return Blocks[x * 256 + y * 16 + z];
    }

    /// <summary>
    /// Setzt einen Block mit Bounds-Prüfung.
    /// </summary>
    /// <param name="x">Die X-Koordinate (0-15).</param>
    /// <param name="y">Die Y-Koordinate (0-15).</param>
    /// <param name="z">Die Z-Koordinate (0-15).</param>
    /// <param name="blockId">Die neue Block-ID.</param>
    /// <returns><c>true</c>, wenn der Block erfolgreich gesetzt wurde, <c>false</c>, wenn die Koordinaten außerhalb liegen.</returns>
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
    /// Prüft, ob an der lokalen Position ein solider Block (nicht Luft) ist.
    /// </summary>
    /// <param name="x">Die X-Koordinate (0-15).</param>
    /// <param name="y">Die Y-Koordinate (0-15).</param>
    /// <param name="z">Die Z-Koordinate (0-15).</param>
    /// <returns><c>true</c>, wenn ein solider Block vorhanden ist, andernfalls <c>false</c>.</returns>
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
    /// <param name="worldX">Die X-Koordinate in Weltkoordinaten.</param>
    /// <param name="worldY">Die Y-Koordinate in Weltkoordinaten.</param>
    /// <param name="worldZ">Die Z-Koordinate in Weltkoordinaten.</param>
    /// <returns>Ein Tupel mit den lokalen Koordinaten (localX, localY, localZ) im Bereich 0-15.</returns>
    public static (int localX, int localY, int localZ) WorldToLocal(int worldX, int worldY, int worldZ)
    {
        // Modulo das auch für negative Werte funktioniert
        int localX = ((worldX % 16) + 16) % 16;
        int localY = ((worldY % 16) + 16) % 16;
        int localZ = ((worldZ % 16) + 16) % 16;
        return (localX, localY, localZ);
    }

    /// <summary>
    /// Rechnet eine Weltposition in die zugehörige <see cref="ChunkCoord"/> um.
    /// </summary>
    /// <param name="worldX">Die X-Koordinate in Weltkoordinaten.</param>
    /// <param name="worldY">Die Y-Koordinate in Weltkoordinaten.</param>
    /// <param name="worldZ">Die Z-Koordinate in Weltkoordinaten.</param>
    /// <param name="lodLevel">Der LOD-Level (Standard: 0).</param>
    /// <returns>Die <see cref="ChunkCoord"/> für die gegebene Weltposition.</returns>
    public static ChunkCoord WorldToChunkCoord(int worldX, int worldY, int worldZ, byte lodLevel = 0)
    {
        // Floor-Division für negative Koordinaten
        int chunkX = worldX >= 0 ? worldX / 16 : (worldX - 16 + 1) / 16;
        int chunkY = worldY >= 0 ? worldY / 16 : (worldY - 16 + 1) / 16;
        int chunkZ = worldZ >= 0 ? worldZ / 16 : (worldZ - 16 + 1) / 16;
        return new ChunkCoord(chunkX, chunkY, chunkZ, lodLevel);
    }

    /// <summary>
    /// Prüft, ob der Chunk komplett leer ist (nur Luft).
    /// </summary>
    /// <returns><c>true</c>, wenn der Chunk leer ist, andernfalls <c>false</c>.</returns>
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
