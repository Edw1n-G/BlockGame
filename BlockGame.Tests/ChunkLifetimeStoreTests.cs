using Basics.Common;
using Basics.Terrain;
using Basics.Terrain.Chunks;

namespace BlockGame.Tests;

public class ChunkLifetimeStoreTests
{
    [Fact]
    public void UnloadedGenerationCannotPublishOrCompleteItsReplacement()
    {
        var store = new ChunkLifetimeStore();
        var coord = new ChunkCoord(0, 0, 0, 0);
        long old = store.Begin(coord, () => false)!.Value;
        lock (store.Gate) store.Cancel(coord);
        long current = store.Begin(coord, () => false)!.Value;
        Assert.False(store.Publish(coord, old, new ChunkData(coord)));
        store.Complete(coord, old);
        var data = new ChunkData(coord);
        Assert.True(store.Publish(coord, current, data));
        Assert.Same(data, store.Data[coord]);
    }
}
