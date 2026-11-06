using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Utilities;

namespace BlockGame.Tests;

public class ChunkSelectionTests
{
    [Fact]
    public void SelectsEllipsoidAroundNegativePosition()
    {
        var center = new ChunkCoord(-4, -2, 3, 0);
        var result = ChunkSelection.Calculate(center, 1, 1);
        Assert.Equal(7, result.Count);
        Assert.Contains(center, result);
        Assert.Contains(new ChunkCoord(-5, -2, 3, 0), result);
        Assert.DoesNotContain(new ChunkCoord(-5, -1, 3, 0), result);
    }

    [Fact]
    public void ZeroDistancesSelectOnlyTheRemainingAxes()
    {
        var center = new ChunkCoord(0, 0, 0, 0);
        Assert.Single(ChunkSelection.Calculate(center, 0, 0));
        Assert.Equal(3, ChunkSelection.Calculate(center, 0, 1).Count);
        Assert.Equal(5, ChunkSelection.Calculate(center, 1, 0).Count);
    }

    [Fact]
    public void OldMeshCompletionCannotRemoveNewRequest()
    {
        var requests = new MeshRequestCoordinator();
        var coord = new ChunkCoord(0, 0, 0, 0);
        requests.Queue(coord);
        Assert.True(requests.Pending.TryDequeue(out var old));
        Assert.True(requests.Complete(old));
        requests.Queue(coord);
        Assert.False(requests.Complete(old));
        Assert.True(requests.Queued.ContainsKey(coord));
    }
}
