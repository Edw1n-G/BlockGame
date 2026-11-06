using Basics.Game.Utilities;
using System.Collections.Concurrent;

namespace Basics.Game.Logic.TerrainManaging;

internal static class ChunkNeighbors
{
    public static bool HasAll(ConcurrentDictionary<ChunkCoord, ChunkData> chunks, ChunkCoord c)
    {
        // 6 direkte Nachbarn
        if (!chunks.ContainsKey(new ChunkCoord(c.X + 1, c.Y, c.Z, c.LodLevel))) return false;
        if (!chunks.ContainsKey(new ChunkCoord(c.X - 1, c.Y, c.Z, c.LodLevel))) return false;
        if (!chunks.ContainsKey(new ChunkCoord(c.X, c.Y + 1, c.Z, c.LodLevel))) return false;
        if (!chunks.ContainsKey(new ChunkCoord(c.X, c.Y - 1, c.Z, c.LodLevel))) return false;
        if (!chunks.ContainsKey(new ChunkCoord(c.X, c.Y, c.Z + 1, c.LodLevel))) return false;
        if (!chunks.ContainsKey(new ChunkCoord(c.X, c.Y, c.Z - 1, c.LodLevel))) return false;

        // 12 Kanten-Nachbarn
        if (!chunks.ContainsKey(new ChunkCoord(c.X, c.Y + 1, c.Z + 1, c.LodLevel))) return false;
        if (!chunks.ContainsKey(new ChunkCoord(c.X, c.Y + 1, c.Z - 1, c.LodLevel))) return false;
        if (!chunks.ContainsKey(new ChunkCoord(c.X, c.Y - 1, c.Z + 1, c.LodLevel))) return false;
        if (!chunks.ContainsKey(new ChunkCoord(c.X, c.Y - 1, c.Z - 1, c.LodLevel))) return false;
        if (!chunks.ContainsKey(new ChunkCoord(c.X + 1, c.Y, c.Z + 1, c.LodLevel))) return false;
        if (!chunks.ContainsKey(new ChunkCoord(c.X + 1, c.Y, c.Z - 1, c.LodLevel))) return false;
        if (!chunks.ContainsKey(new ChunkCoord(c.X - 1, c.Y, c.Z + 1, c.LodLevel))) return false;
        if (!chunks.ContainsKey(new ChunkCoord(c.X - 1, c.Y, c.Z - 1, c.LodLevel))) return false;
        if (!chunks.ContainsKey(new ChunkCoord(c.X + 1, c.Y + 1, c.Z, c.LodLevel))) return false;
        if (!chunks.ContainsKey(new ChunkCoord(c.X + 1, c.Y - 1, c.Z, c.LodLevel))) return false;
        if (!chunks.ContainsKey(new ChunkCoord(c.X - 1, c.Y + 1, c.Z, c.LodLevel))) return false;
        if (!chunks.ContainsKey(new ChunkCoord(c.X - 1, c.Y - 1, c.Z, c.LodLevel))) return false;

        // 8 Eck-Nachbarn
        if (!chunks.ContainsKey(new ChunkCoord(c.X + 1, c.Y + 1, c.Z + 1, c.LodLevel))) return false;
        if (!chunks.ContainsKey(new ChunkCoord(c.X + 1, c.Y + 1, c.Z - 1, c.LodLevel))) return false;
        if (!chunks.ContainsKey(new ChunkCoord(c.X + 1, c.Y - 1, c.Z + 1, c.LodLevel))) return false;
        if (!chunks.ContainsKey(new ChunkCoord(c.X + 1, c.Y - 1, c.Z - 1, c.LodLevel))) return false;
        if (!chunks.ContainsKey(new ChunkCoord(c.X - 1, c.Y + 1, c.Z + 1, c.LodLevel))) return false;
        if (!chunks.ContainsKey(new ChunkCoord(c.X - 1, c.Y + 1, c.Z - 1, c.LodLevel))) return false;
        if (!chunks.ContainsKey(new ChunkCoord(c.X - 1, c.Y - 1, c.Z + 1, c.LodLevel))) return false;
        if (!chunks.ContainsKey(new ChunkCoord(c.X - 1, c.Y - 1, c.Z - 1, c.LodLevel))) return false;

        return true;
    }

}
