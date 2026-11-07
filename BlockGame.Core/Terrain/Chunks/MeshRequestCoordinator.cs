using Basics.Common;
using Basics.Jobs;
using Basics.Terrain.Meshing;
using System.Collections.Concurrent;

namespace Basics.Terrain.Chunks;

/// <summary>Tracks ownership of mesh requests and dispatches them under the upload limit.</summary>
internal sealed class MeshRequestCoordinator
{
    private long _generation;
    public ConcurrentDictionary<ChunkCoord, long> Queued { get; } = new();
    public ConcurrentQueue<MeshRequest> Pending { get; } = new();

    public void Queue(ChunkCoord coord)
    {
        long generation = Interlocked.Increment(ref _generation);
        if (Queued.TryAdd(coord, generation)) Pending.Enqueue(new(coord, generation));
    }

    public bool Complete(MeshRequest request) =>
        Queued.TryRemove(new KeyValuePair<ChunkCoord, long>(request.Coord, request.Generation));

    public void Dispatch(int limit, ConcurrentQueue<BaseMesher> uploads, JobScheduler? scheduler, ChunkProvider provider)
    {
        while (uploads.Count < limit && Pending.TryDequeue(out MeshRequest request))
        {
            if (!Queued.TryGetValue(request.Coord, out long current) || current != request.Generation) continue;
            var job = new MeshGenerateJob(request, provider);
            if (scheduler is null || !scheduler.EnqueueLow(job)) job.Discard(null);
        }
    }
}
