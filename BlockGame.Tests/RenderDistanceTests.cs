using Basics.Common;
using Basics.Diagnostics;
using Basics.Settings;
using Basics.Terrain;
using Basics.Terrain.Chunks;
using Basics.Terrain.Selection;
using System.Collections.Concurrent;

namespace BlockGame.Tests;

public class RenderDistanceTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(8)]
    public void GameplaySettersEnforceMinimum(int requested)
    {
        int expected = Math.Max(5, requested);
        var settings = new GameSettings { RenderDistance = requested, VerticalRenderDistance = requested };
        settings.SetRenderDistance(requested);
        settings.SetVerticalRenderDistance(requested);
        Assert.Equal(expected, settings.RenderDistance);
        Assert.Equal(expected, settings.VerticalRenderDistance);
    }

    [Fact]
    public void MinimumSelectionProvidesAllMeshingNeighborsAtCenter()
    {
        var settings = new GameSettings();
        settings.SetRenderDistance(1);
        settings.SetVerticalRenderDistance(1);
        var center = new ChunkCoord(0,0,0,0);
        var plan = new ChunkSelectionPlan();
        Assert.True(plan.TryBuild(null, center, settings.RenderDistance, settings.VerticalRenderDistance));
        var chunks = new ConcurrentDictionary<ChunkCoord, ChunkData>();
        foreach (var coord in plan.Added) chunks.TryAdd(coord, new ChunkData(coord));
        Assert.True(ChunkNeighbors.HasAll(chunks, center));
    }

    [Theory]
    [InlineData("--render-distance")]
    [InlineData("--sweep")]
    public void FrameBenchmarkRejectsRadiusBelowMinimum(string option)
    {
        Assert.Throws<ArgumentException>(() => FrameBenchmarkOptions.Parse(["--benchmark", option, "4"]));
    }
}
