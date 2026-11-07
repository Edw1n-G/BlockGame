using Basics.Common;
using Basics.Terrain.Selection;

namespace BlockGame.Benchmarks.Workloads;

/// <summary>The same closed paths as the original, with retained shapes, lists and active-set capacity.</summary>
public sealed class SelectionDeltaWorkload
{
    private readonly ChunkSelectionPlan _plan = new();
    private readonly HashSet<ChunkCoord> _active = new();
    private readonly SelectionTarget[] _path;
    private ChunkSelectionRegion? _region;
    public SelectionDeltaWorkload(string scenario)
    {
        _path = SelectionMovementWorkload.Path(scenario);
        Step(SelectionMovementWorkload.Start);
    }
    private long Step(SelectionTarget target)
    {
        if (!_plan.TryBuild(_region,target.Center,target.Horizontal,target.Vertical)) throw new InvalidOperationException("Unexpected cancellation");
        foreach (var coord in _plan.Added) _active.Add(coord);
        foreach (var coord in _plan.Removed) _active.Remove(coord);
        _region = _plan.Target;
        return _plan.Added.Count + _plan.Removed.Count + _active.Count;
    }
    public long Run()
    {
        long summary = 0;
        foreach (var target in _path) summary += Step(target);
        return summary;
    }
    public HashSet<ChunkCoord> Snapshot() => new(_active);
}
