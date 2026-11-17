using BlockGame.Benchmarks.Workloads;
using Basics.Common;
using Basics.Terrain.Selection;

namespace BlockGame.Tests;

public class IncrementalSelectionTests
{
    [Fact]
    public void ScanlinesExactlyMatchOriginalAcrossRadiiAndCoordinateSeams()
    {
        ChunkCoord[] centers = [new(0,0,0,0),new(-17,-8,23,0),new(32767,-32768,32767,0)];
        foreach (var center in centers)
        for (int h = 0; h <= 12; h++) for (int v = 0; v <= 10; v++)
            Assert.True(SelectionReference.Calculate(center,h,v).SetEquals(ChunkSelection.Calculate(center,h,v)), $"center={center} h={h} v={v}");
    }

    [Theory]
    [InlineData(20,15)]
    [InlineData(32,24)]
    [InlineData(-1,-3)]
    [InlineData(0,40000)]
    public void ScanlinesPreserveBoundaryAndClampingSemantics(int h, int v)
    {
        var center = new ChunkCoord(32767,32767,-32768,0);
        Assert.True(SelectionReference.Calculate(center,h,v).SetEquals(ChunkSelection.Calculate(center,h,v)));
    }

    [Fact]
    public void DeltaMatchesReferenceForRandomMovesResizesTeleportsAndWraparound()
    {
        var random = new Random(0x5E1EC7);
        var plan = new ChunkSelectionPlan(); ChunkSelectionRegion? previous = null;
        var active = new HashSet<ChunkCoord>();
        for (int step = 0; step < 100; step++)
        {
            var center = (step % 4) switch {
                0 => new ChunkCoord(32767,-32768,32767,0), 1 => new ChunkCoord(-32768,32767,-32768,0),
                _ => new ChunkCoord(random.Next(-20,20),random.Next(-10,10),random.Next(-20,20),0) };
            int h = random.Next(0,10), v = random.Next(0,8);
            var expected = SelectionReference.Calculate(center,h,v);
            Assert.True(plan.TryBuild(previous,center,h,v));
            Assert.Equal(plan.Added.Count,plan.Added.Distinct().Count());
            Assert.Equal(plan.Removed.Count,plan.Removed.Distinct().Count());
            Assert.True(expected.Except(active).ToHashSet().SetEquals(plan.Added));
            Assert.True(active.Except(expected).ToHashSet().SetEquals(plan.Removed));
            active.UnionWith(plan.Added); active.ExceptWith(plan.Removed); previous = plan.Target;
            Assert.True(expected.SetEquals(active));
        }
    }

    [Fact]
    public void VerticalIntervalsHandleLargeWrappedOverlap()
    {
        var plan = new ChunkSelectionPlan(); ChunkSelectionRegion? previous = null;
        var active = new HashSet<ChunkCoord>();
        foreach (var (y,v) in new[] {(32767,30000),(-32768,30000),(0,30000),(0,40000),(32760,5)})
        {
            var center = new ChunkCoord(0,y,0,0); var expected = SelectionReference.Calculate(center,0,v);
            Assert.True(plan.TryBuild(previous,center,0,v));
            Assert.True(expected.Except(active).ToHashSet().SetEquals(plan.Added));
            Assert.True(active.Except(expected).ToHashSet().SetEquals(plan.Removed));
            active.UnionWith(plan.Added); active.ExceptWith(plan.Removed); previous = plan.Target;
            Assert.True(expected.SetEquals(active));
        }
    }

    [Fact]
    public void CanceledPlanningDoesNotMutateCommittedSelectionAndCanBeRetried()
    {
        var plan = new ChunkSelectionPlan(); var center = new ChunkCoord(0,0,0,0);
        Assert.True(plan.TryBuild(null,center,20,15));
        var previous = plan.Target; var active = plan.Added.ToHashSet();
        int polls = 0;
        Assert.False(plan.TryBuild(previous,new ChunkCoord(1,0,0,0),20,15,() => ++polls >= 5));
        Assert.True(active.SetEquals(SelectionReference.Calculate(center,20,15)));
        Assert.True(plan.TryBuild(previous,new ChunkCoord(1,0,0,0),20,15));
        active.UnionWith(plan.Added); active.ExceptWith(plan.Removed);
        Assert.True(active.SetEquals(SelectionReference.Calculate(new(1,0,0,0),20,15)));
    }

    [Fact]
    public void CanceledShapeBuildCanBeRetriedWithoutPoisoningShapeCache()
    {
        var plan = new ChunkSelectionPlan(); var center = new ChunkCoord(-1,0,2,0);
        Assert.True(plan.TryBuild(null,center,2,1)); var previous=plan.Target;
        int polls=0;
        Assert.False(plan.TryBuild(previous,center,24,18,() => ++polls >= 3));
        Assert.True(plan.TryBuild(previous,center,24,18));
        var active=SelectionReference.Calculate(center,2,1);
        active.UnionWith(plan.Added); active.ExceptWith(plan.Removed);
        Assert.True(active.SetEquals(SelectionReference.Calculate(center,24,18)));
    }
}
