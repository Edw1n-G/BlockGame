using Basics.Common;
using Basics.Terrain.Meshing;
using Basics.Terrain;
using Basics.Terrain.Chunks;

namespace Basics.Jobs;

/// <summary>
/// Job zum Laden oder Generieren eines Chunks.
/// Prüft zuerst, ob der Chunk von der Festplatte geladen werden kann,
/// andernfalls wird er mit dem <see cref="TerrainGenerator"/> generiert.
/// </summary>
public class ChunkLoadorGenerateJob : IJob
{
    /// <summary>Die Koordinaten des zu ladenden/generierenden Chunks.</summary>
    private ChunkCoord ChunkCoord;
    private readonly long _generation;
    
    /// <summary>
    /// Initialisiert eine neue Instanz von <see cref="ChunkLoadorGenerateJob"/>.
    /// </summary>
    /// <param name="chunkCoord">Die Koordinaten des zu ladenden/generierenden Chunks.</param>
    public ChunkLoadorGenerateJob(ChunkCoord chunkCoord, long generation)
    {
        this.ChunkCoord = chunkCoord;
        _generation = generation;
    }
    
    /// <summary>
    /// Führt den Chunk-Lade- oder Generierungs-Job aus.
    /// </summary>
    /// <param name="context">Der <see cref="JobContext"/> mit den notwendigen Ressourcen.</param>
    public void Execute(JobContext context)
    {
        try
        {
            // Versuch von Festplatte zu laden (Placeholder)
            if (TryLoadFromDisk(ChunkCoord, out BaseMesher? loadedChunk))
            {
                if (loadedChunk?.SourceChunkData is { } source &&
                    context.Provider.TryPublishChunkData(ChunkCoord, _generation, source))
                    context.Provider.UploadQueue.Enqueue(loadedChunk);
                else loadedChunk?.Dispose();
                return;
            }

            ChunkData newChunk = context.TerrainGen.GenerateChunk(ChunkCoord);
            if (!context.Provider.TryPublishChunkData(ChunkCoord, _generation, newChunk)) return;

            OnChunkDataGenerated(context.Provider, ChunkCoord);
        }
        finally { context.Provider.CompleteChunkRequest(ChunkCoord, _generation); }
    }

    public void Discard(JobContext? context) => context?.Provider.CompleteChunkRequest(ChunkCoord, _generation);
    
    /// <summary>
    /// Versucht, einen Chunk von der Festplatte zu laden (derzeit nicht implementiert).
    /// </summary>
    /// <param name="chunkCoord">Die Koordinaten des zu ladenden Chunks.</param>
    /// <param name="chunk">Der geladene Chunk oder null.</param>
    /// <returns>Immer <c>false</c>, da die Festplatten-Integration noch nicht implementiert ist.</returns>
    private bool TryLoadFromDisk(ChunkCoord chunkCoord, out BaseMesher? chunk)
    {
        // TODO: Implementierung für Chunk-Laden von der Festplatte
        chunk = null;
        return false;
    }
    
    /// <summary>
    /// Wird nach der Chunk-Datengenerierung aufgerufen.
    /// Prüft, ob der Chunk und seine Nachbarn für das Meshing vorbereitet werden können.
    /// </summary>
    /// <param name="provider">Der <see cref="ChunkProvider"/> für die Meshing-Anfragen.</param>
    /// <param name="coord">Die Koordinaten des gerade generierten Chunks.</param>
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
    
    /// <summary>
    /// Prüft, ob ein Chunk für das Meshing in die Warteschlange eingereiht werden soll.
    /// </summary>
    /// <param name="provider">Der <see cref="ChunkProvider"/> für die Meshing-Anfragen.</param>
    /// <param name="coord">Die Koordinaten des zu prüfenden Chunks.</param>
    private void TryQueueForMeshing(ChunkProvider provider, ChunkCoord coord) => provider.RemeshChunk(coord);
}
