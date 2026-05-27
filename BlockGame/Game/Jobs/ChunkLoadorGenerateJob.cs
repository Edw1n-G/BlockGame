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
            ChunkProvider.LoadedChunks.TryAdd(ChunkCoord, loadedChunk!);
            return;
        }
        
        ChunkData newChunk = context.TerrainGen.GenerateChunk(ChunkCoord);
        ChunkProvider.Chunkdata.TryAdd(ChunkCoord, newChunk);
        
        OnChunkDataGenerated(ChunkCoord);
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
    
    public void OnChunkDataGenerated(ChunkCoord coord)
    {

        // chunk selbst prüfen
        TryQueueForMeshing(coord);

        // nachbar Chunks prüfen
        TryQueueForMeshing(new ChunkCoord(coord.X + 1, coord.Y, coord.Z, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X - 1, coord.Y, coord.Z, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X, coord.Y + 1, coord.Z, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X, coord.Y - 1, coord.Z, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X, coord.Y, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X, coord.Y, coord.Z - 1, coord.LodLevel));
        // Diagonale
        TryQueueForMeshing(new ChunkCoord(coord.X, coord.Y + 1, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X, coord.Y + 1, coord.Z - 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X, coord.Y - 1, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X, coord.Y - 1, coord.Z - 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X + 1, coord.Y, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X + 1, coord.Y, coord.Z - 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X - 1, coord.Y, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X - 1, coord.Y, coord.Z - 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X + 1, coord.Y + 1, coord.Z, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X + 1, coord.Y - 1, coord.Z, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X - 1, coord.Y + 1, coord.Z, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X - 1, coord.Y - 1, coord.Z, coord.LodLevel));
        // Ecken
        TryQueueForMeshing(new ChunkCoord(coord.X + 1, coord.Y + 1, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X + 1, coord.Y + 1, coord.Z - 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X + 1, coord.Y - 1, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X + 1, coord.Y - 1, coord.Z - 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X - 1, coord.Y + 1, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X - 1, coord.Y + 1, coord.Z - 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X - 1, coord.Y - 1, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X - 1, coord.Y - 1, coord.Z - 1, coord.LodLevel));
    }
    
    private void TryQueueForMeshing(ChunkCoord coord)
    {
        //Selbstcheck um existenzkrisen zu vermeiden
        if (!ChunkProvider.Chunkdata.ContainsKey(coord)) return;
        
        //Sind direkte Nachbarn im Dictionary?
        if (!HasAllNeighbors(coord)) return;
        
        if (ChunkProvider.QueuedForMeshing.TryAdd(coord, 1))
        {
            ChunkProvider.PendingMeshRequests.Enqueue(coord);
        }
    }
    
    private bool HasAllNeighbors(ChunkCoord c)
    {
        // 6 direkte Nachbarn
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y, c.Z, c.LodLevel))) return false;
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y, c.Z, c.LodLevel))) return false;
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y + 1, c.Z, c.LodLevel))) return false;
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y - 1, c.Z, c.LodLevel))) return false;
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y, c.Z + 1, c.LodLevel))) return false;
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y, c.Z - 1, c.LodLevel))) return false;

        // 12 Kanten-Nachbarn
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y + 1, c.Z + 1, c.LodLevel))) return false;
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y + 1, c.Z - 1, c.LodLevel))) return false;
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y - 1, c.Z + 1, c.LodLevel))) return false;
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y - 1, c.Z - 1, c.LodLevel))) return false;
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y, c.Z + 1, c.LodLevel))) return false;
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y, c.Z - 1, c.LodLevel))) return false;
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y, c.Z + 1, c.LodLevel))) return false;
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y, c.Z - 1, c.LodLevel))) return false;
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y + 1, c.Z, c.LodLevel))) return false;
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y - 1, c.Z, c.LodLevel))) return false;
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y + 1, c.Z, c.LodLevel))) return false;
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y - 1, c.Z, c.LodLevel))) return false;

        // 8 Eck-Nachbarn
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y + 1, c.Z + 1, c.LodLevel))) return false;
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y + 1, c.Z - 1, c.LodLevel))) return false;
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y - 1, c.Z + 1, c.LodLevel))) return false;
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y - 1, c.Z - 1, c.LodLevel))) return false;
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y + 1, c.Z + 1, c.LodLevel))) return false;
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y + 1, c.Z - 1, c.LodLevel))) return false;
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y - 1, c.Z + 1, c.LodLevel))) return false;
        if (!ChunkProvider.Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y - 1, c.Z - 1, c.LodLevel))) return false;

        return true;
    }
}
