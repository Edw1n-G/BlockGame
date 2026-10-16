using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Logic.TerrainManaging.Meshing;

namespace Basics.Game.Utilities;

public class ChunkLoadorGenerateJob : IJob
{
    private ChunkCoord ChunkCoord;
    
    public ChunkLoadorGenerateJob(ChunkCoord chunkCoord)
    {
        this.ChunkCoord = chunkCoord;
    }
    
    public void Execute(JobContext context)
    {
        // Versuch von Festplatte zu laden (Placeholder)
        if (TryLoadFromDisk(ChunkCoord, out BaseMesher? loadedChunk))
        {
            context.Provider.LoadedChunks.TryAdd(ChunkCoord, loadedChunk!);
            return;
        }
        
        ChunkData newChunk = context.TerrainGen.GenerateChunk(ChunkCoord);
        context.Provider.Chunkdata.TryAdd(ChunkCoord, newChunk);
        
        OnChunkDataGenerated(context.Provider, ChunkCoord);
    }
    
    /// <summary>
    /// Placeholder: Versucht einen Chunk von der Festplatte zu laden.
    /// Gibt vorerst immer false zurück.
    /// </summary>
    private bool TryLoadFromDisk(ChunkCoord chunkCoord, out BaseMesher? chunk)
    {
        // TODO: Implementierung für Chunk-Laden von der Festplatte
        chunk = null;
        return false;
    }
    
    public void OnChunkDataGenerated(ChunkProvider provider, ChunkCoord coord)
    {
        // chunk selbst prüfen
        TryQueueForMeshing(provider, coord);

        // nachbar Chunks prüfen
        TryQueueForMeshing(provider, new ChunkCoord(coord.X + 1, coord.Y, coord.Z, coord.LodLevel));
        TryQueueForMeshing(provider, new ChunkCoord(coord.X - 1, coord.Y, coord.Z, coord.LodLevel));
        TryQueueForMeshing(provider, new ChunkCoord(coord.X, coord.Y + 1, coord.Z, coord.LodLevel));
        TryQueueForMeshing(provider, new ChunkCoord(coord.X, coord.Y - 1, coord.Z, coord.LodLevel));
        TryQueueForMeshing(provider, new ChunkCoord(coord.X, coord.Y, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(provider, new ChunkCoord(coord.X, coord.Y, coord.Z - 1, coord.LodLevel));
        // Diagonale
        TryQueueForMeshing(provider, new ChunkCoord(coord.X, coord.Y + 1, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(provider, new ChunkCoord(coord.X, coord.Y + 1, coord.Z - 1, coord.LodLevel));
        TryQueueForMeshing(provider, new ChunkCoord(coord.X, coord.Y - 1, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(provider, new ChunkCoord(coord.X, coord.Y - 1, coord.Z - 1, coord.LodLevel));
        TryQueueForMeshing(provider, new ChunkCoord(coord.X + 1, coord.Y, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(provider, new ChunkCoord(coord.X + 1, coord.Y, coord.Z - 1, coord.LodLevel));
        TryQueueForMeshing(provider, new ChunkCoord(coord.X - 1, coord.Y, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(provider, new ChunkCoord(coord.X - 1, coord.Y, coord.Z - 1, coord.LodLevel));
        TryQueueForMeshing(provider, new ChunkCoord(coord.X + 1, coord.Y + 1, coord.Z, coord.LodLevel));
        TryQueueForMeshing(provider, new ChunkCoord(coord.X + 1, coord.Y - 1, coord.Z, coord.LodLevel));
        TryQueueForMeshing(provider, new ChunkCoord(coord.X - 1, coord.Y + 1, coord.Z, coord.LodLevel));
        TryQueueForMeshing(provider, new ChunkCoord(coord.X - 1, coord.Y - 1, coord.Z, coord.LodLevel));
        // Ecken
        TryQueueForMeshing(provider, new ChunkCoord(coord.X + 1, coord.Y + 1, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(provider, new ChunkCoord(coord.X + 1, coord.Y + 1, coord.Z - 1, coord.LodLevel));
        TryQueueForMeshing(provider, new ChunkCoord(coord.X + 1, coord.Y - 1, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(provider, new ChunkCoord(coord.X + 1, coord.Y - 1, coord.Z - 1, coord.LodLevel));
        TryQueueForMeshing(provider, new ChunkCoord(coord.X - 1, coord.Y + 1, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(provider, new ChunkCoord(coord.X - 1, coord.Y + 1, coord.Z - 1, coord.LodLevel));
        TryQueueForMeshing(provider, new ChunkCoord(coord.X - 1, coord.Y - 1, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(provider, new ChunkCoord(coord.X - 1, coord.Y - 1, coord.Z - 1, coord.LodLevel));
    }
    
    private void TryQueueForMeshing(ChunkProvider provider, ChunkCoord coord)
    {
        // Selbstcheck um existenzkrisen zu vermeiden
        if (!provider.Chunkdata.ContainsKey(coord)) return;
        
        // Sind direkte Nachbarn im Dictionary?
        if (!HasAllNeighbors(provider, coord)) return;
        
        if (provider.QueuedForMeshing.TryAdd(coord, 1))
        {
            provider.PendingMeshRequests.Enqueue(coord);
        }
    }
    
    private bool HasAllNeighbors(ChunkProvider provider, ChunkCoord c)
    {
        // 6 direkte Nachbarn
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y, c.Z, c.LodLevel))) return false;
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y, c.Z, c.LodLevel))) return false;
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y + 1, c.Z, c.LodLevel))) return false;
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y - 1, c.Z, c.LodLevel))) return false;
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y, c.Z + 1, c.LodLevel))) return false;
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y, c.Z - 1, c.LodLevel))) return false;

        // 12 Kanten-Nachbarn
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y + 1, c.Z + 1, c.LodLevel))) return false;
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y + 1, c.Z - 1, c.LodLevel))) return false;
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y - 1, c.Z + 1, c.LodLevel))) return false;
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y - 1, c.Z - 1, c.LodLevel))) return false;
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y, c.Z + 1, c.LodLevel))) return false;
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y, c.Z - 1, c.LodLevel))) return false;
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y, c.Z + 1, c.LodLevel))) return false;
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y, c.Z - 1, c.LodLevel))) return false;
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y + 1, c.Z, c.LodLevel))) return false;
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y - 1, c.Z, c.LodLevel))) return false;
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y + 1, c.Z, c.LodLevel))) return false;
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y - 1, c.Z, c.LodLevel))) return false;

        // 8 Eck-Nachbarn
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y + 1, c.Z + 1, c.LodLevel))) return false;
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y + 1, c.Z - 1, c.LodLevel))) return false;
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y - 1, c.Z + 1, c.LodLevel))) return false;
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y - 1, c.Z - 1, c.LodLevel))) return false;
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y + 1, c.Z + 1, c.LodLevel))) return false;
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y + 1, c.Z - 1, c.LodLevel))) return false;
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y - 1, c.Z + 1, c.LodLevel))) return false;
        if (!provider.Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y - 1, c.Z - 1, c.LodLevel))) return false;

        return true;
    }
}
