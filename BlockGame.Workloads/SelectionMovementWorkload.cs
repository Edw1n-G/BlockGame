using Basics.Common;

namespace BlockGame.Workloads;

/// <summary>Historical double-precision model, not the actual c2a6ab2 float implementation. Test/control oracle only.</summary>
public static class SelectionReference
{
    public static HashSet<ChunkCoord> Calculate(ChunkCoord center, int horizontal, int vertical)
    {
        horizontal = Math.Max(0, horizontal); vertical = Math.Max(0, vertical);
        var result = new HashSet<ChunkCoord>();
        for (int x = -horizontal; x <= horizontal; x++)
        for (int z = -horizontal; z <= horizontal; z++)
        for (int y = -vertical; y <= vertical; y++)
        {
            double h = horizontal == 0 ? 0 : ((double)x*x + (double)z*z) / ((double)horizontal*horizontal);
            double v = vertical == 0 ? 0 : (double)y*y / ((double)vertical*vertical);
            if (h+v <= 1) result.Add(new ChunkCoord(center.X+x, center.Y+y, center.Z+z, 0));
        }
        return result;
    }
}

public readonly record struct SelectionTarget(ChunkCoord Center, int Horizontal, int Vertical);

public static class SelectionMovementWorkload
{
    public const int Version = 1;
    public static IEnumerable<string> Cases => ["same", "walk-x", "diagonal", "vertical", "teleport", "resize", "burst"];
    public static SelectionTarget Start => new(new ChunkCoord(-4, -2, 3, 0), 20, 15);
    // Every timed path returns to its start, so invocations do equivalent work without iteration setup.
    public static SelectionTarget[] Path(string id)
    {
        var start = Start;
        SelectionTarget At(int x, int y, int z, int h = 20, int v = 15) => new(new ChunkCoord(x,y,z,0),h,v);
        return id switch
        {
            "same" => [start, start],
            "walk-x" => [At(-3,-2,3), start],
            "diagonal" => [At(-3,-1,4), start],
            "vertical" => [At(-4,-1,3), start],
            "teleport" => [At(100,40,-80), start],
            "resize" => [At(-4,-2,3,24,18), start],
            "burst" => Enumerable.Range(1,8).Select(i => At(-4+i,-2+i%3,3+i)).Append(start).ToArray(),
            _ => throw new ArgumentException(id)
        };
    }
}

/// <summary>CPU-only old requestor selection/difference/active-set work, excluding provider/tasks/locks.</summary>
public sealed class SelectionRebuildWorkload
{
    private HashSet<ChunkCoord> _active;
    private readonly SelectionTarget[] _path;
    public SelectionRebuildWorkload(string scenario)
    {
        var s = SelectionMovementWorkload.Start;
        _active = SelectionReference.Calculate(s.Center,s.Horizontal,s.Vertical);
        _path = SelectionMovementWorkload.Path(scenario);
    }
    public long Run()
    {
        long summary = 0;
        foreach (var target in _path)
        {
            var next = SelectionReference.Calculate(target.Center,target.Horizontal,target.Vertical);
            HashSet<ChunkCoord> pending = new();
            foreach (var coord in next) if (!_active.Contains(coord)) pending.Add(coord);
            foreach (var coord in pending) _active.Add(coord);
            int removed = 0;
            foreach (var coord in _active) if (!next.Contains(coord)) removed++;
            _active = next;
            summary += pending.Count + removed + _active.Count;
        }
        return summary;
    }
    public HashSet<ChunkCoord> Snapshot() => new(_active);
}
