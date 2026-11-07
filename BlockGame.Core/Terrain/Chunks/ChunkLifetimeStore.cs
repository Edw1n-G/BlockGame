using Basics.Common;
using System.Collections.Concurrent;

namespace Basics.Terrain.Chunks;

/// <summary>Owns chunk data and generation tokens. Gate also protects mesh residency transitions.</summary>
internal sealed class ChunkLifetimeStore
{
    internal object Gate { get; } = new();
    public ConcurrentDictionary<ChunkCoord, ChunkData> Data { get; } = new();
    private readonly Dictionary<ChunkCoord, long> _pending = new();
    private long _generation;

    public long? Begin(ChunkCoord coord, Func<bool> isMeshLoaded)
    {
        lock (Gate)
        {
            if (isMeshLoaded() || Data.ContainsKey(coord) || _pending.ContainsKey(coord)) return null;
            long generation = ++_generation;
            _pending.Add(coord, generation);
            return generation;
        }
    }

    public bool Publish(ChunkCoord coord, long generation, ChunkData data)
    {
        lock (Gate)
        {
            if (!_pending.TryGetValue(coord, out long current) || current != generation) return false;
            _pending.Remove(coord);
            return Data.TryAdd(coord, data);
        }
    }

    public void Complete(ChunkCoord coord, long generation)
    {
        lock (Gate)
            if (_pending.TryGetValue(coord, out long current) && current == generation) _pending.Remove(coord);
    }

    // Call under Gate as part of an atomic unload or shutdown.
    public void Cancel(ChunkCoord coord) => _pending.Remove(coord);
    public void ClearPending() => _pending.Clear();
}
