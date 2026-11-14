namespace Basics.Diagnostics;

public readonly record struct PipelineCounts(long Requested, long Generated, long Meshed, long Uploaded, long SelectionCommits = 0, long SelectionRollbacks = 0)
{
    public static PipelineCounts operator -(PipelineCounts a, PipelineCounts b) =>
        new(a.Requested-b.Requested, a.Generated-b.Generated, a.Meshed-b.Meshed, a.Uploaded-b.Uploaded, a.SelectionCommits-b.SelectionCommits, a.SelectionRollbacks-b.SelectionRollbacks);
}

/// <summary>Per-provider counters. Counts completed work even when its result becomes stale.</summary>
public sealed class ChunkPipelineMetrics
{
    private long _requested, _generated, _meshed, _uploaded, _selectionCommits, _selectionRollbacks;
    public void SelectionCommitted() => Interlocked.Increment(ref _selectionCommits);
    public void SelectionRolledBack() => Interlocked.Increment(ref _selectionRollbacks);
    public void Requested() => Interlocked.Increment(ref _requested);
    public void Generated() => Interlocked.Increment(ref _generated);
    public void Meshed() => Interlocked.Increment(ref _meshed);
    public void Uploaded() => Interlocked.Increment(ref _uploaded);
    public PipelineCounts Snapshot() => new(Interlocked.Read(ref _requested), Interlocked.Read(ref _generated),
        Interlocked.Read(ref _meshed), Interlocked.Read(ref _uploaded), Interlocked.Read(ref _selectionCommits), Interlocked.Read(ref _selectionRollbacks));
}
