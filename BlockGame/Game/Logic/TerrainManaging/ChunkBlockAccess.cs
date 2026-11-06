using Basics.Game.Utilities;
using System.Collections.Concurrent;

namespace Basics.Game.Logic.TerrainManaging;

internal sealed class ChunkBlockAccess(ConcurrentDictionary<ChunkCoord, ChunkData> chunks, Action<ChunkCoord> remesh)
{
    private readonly ConcurrentDictionary<ChunkCoord, ChunkData> _chunks = chunks;
    private readonly Action<ChunkCoord> _remesh = remesh;

    public bool ModifyBlock(int worldX, int worldY, int worldZ, ushort newBlockId)
    {
        ChunkCoord chunkCoord = ChunkData.WorldToChunkCoord(worldX, worldY, worldZ);
        var (localX, localY, localZ) = ChunkData.WorldToLocal(worldX, worldY, worldZ);

        if (!_chunks.TryGetValue(chunkCoord, out ChunkData? chunkData))
            return false;

        chunkData.SetBlock(localX, localY, localZ, newBlockId);
        _remesh(chunkCoord);

        if (localX == 0)  _remesh(new ChunkCoord(chunkCoord.X - 1, chunkCoord.Y, chunkCoord.Z, chunkCoord.LodLevel));
        if (localX == 15) _remesh(new ChunkCoord(chunkCoord.X + 1, chunkCoord.Y, chunkCoord.Z, chunkCoord.LodLevel));
        if (localY == 0)  _remesh(new ChunkCoord(chunkCoord.X, chunkCoord.Y - 1, chunkCoord.Z, chunkCoord.LodLevel));
        if (localY == 15) _remesh(new ChunkCoord(chunkCoord.X, chunkCoord.Y + 1, chunkCoord.Z, chunkCoord.LodLevel));
        if (localZ == 0)  _remesh(new ChunkCoord(chunkCoord.X, chunkCoord.Y, chunkCoord.Z - 1, chunkCoord.LodLevel));
        if (localZ == 15) _remesh(new ChunkCoord(chunkCoord.X, chunkCoord.Y, chunkCoord.Z + 1, chunkCoord.LodLevel));

        return true;
    }


    public ushort GetBlockAt(int worldX, int worldY, int worldZ)
    {
        ChunkCoord chunkCoord = ChunkData.WorldToChunkCoord(worldX, worldY, worldZ);
        if (!_chunks.TryGetValue(chunkCoord, out ChunkData? chunkData))
            return 0;
        var (localX, localY, localZ) = ChunkData.WorldToLocal(worldX, worldY, worldZ);
        return chunkData.GetBlock(localX, localY, localZ);
    }

}
