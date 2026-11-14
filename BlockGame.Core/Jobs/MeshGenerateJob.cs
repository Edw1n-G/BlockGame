using Basics.Terrain.Meshing;
using Basics.Terrain.Chunks;

namespace Basics.Jobs;

/// <summary>Builds a CPU mesh for one uniquely identified request.</summary>
public class MeshGenerateJob : IJob
{
    private readonly MeshRequest _request;
    private readonly ChunkProvider _provider;

    public MeshGenerateJob(MeshRequest request, ChunkProvider provider)
    {
        _request = request;
        _provider = provider;
    }

    public void Execute(JobContext context)
    {
        try
        {
            if (!_provider.QueuedForMeshing.TryGetValue(_request.Coord, out long generation) ||
                generation != _request.Generation || !_provider.Chunkdata.TryGetValue(_request.Coord, out var data)) return;

            BaseMesher mesh = _request.Coord.LodLevel switch
            {
                0 => new Lod0Mesher(_request.Coord, data, _provider, context.BlockRegistry),
                1 => new Lod1Mesher(_request.Coord, data, _provider, context.BlockRegistry),
                2 => new Lod2Mesher(_request.Coord, data, _provider, context.BlockRegistry),
                _ => throw new InvalidOperationException($"Invalid LOD {_request.Coord.LodLevel}")
            };
            _provider.Metrics.Meshed();
            // Only the request that still owns the marker can publish its result.
            if (_provider.CompleteMeshRequest(_request)) _provider.UploadQueue.Enqueue(mesh);
            else mesh.Dispose();
        }
        finally { _provider.CompleteMeshRequest(_request); }
    }

    public void Discard(JobContext? context) => _provider.CompleteMeshRequest(_request);
}
