using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Logic.TerrainManaging.Meshing;

namespace Basics.Game.Utilities;

/// <summary>
/// Job zum Laden oder Generieren eines Chunks.
/// Prüft zuerst, ob der Chunk von der Festplatte geladen werden kann,
/// andernfalls wird er mit dem <see cref="TerrainGenerator"/> generiert.
/// </summary>
public class ChunkLoadorGenerateJob : IJob
{
    /// <summary>Die Koordinaten des zu ladenden/generierenden Chunks.</summary>
    private ChunkCoord ChunkCoord;
    
    /// <summary>
    /// Initialisiert eine neue Instanz von <see cref="ChunkLoadorGenerateJob"/>.
    /// </summary>
    /// <param name="chunkCoord">Die Koordinaten des zu ladenden/generierenden Chunks.</param>
    public ChunkLoadorGenerateJob(ChunkCoord chunkCoord)
    {
        this.ChunkCoord = chunkCoord;
    }
    
    /// <summary>
    /// Führt den Chunk-Lade- oder Generierungs-Job aus.
    /// </summary>
    /// <param name="context">Der <see cref="JobContext"/> mit den notwendigen Ressourcen.</param>
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
    
    /// <summary>
    /// Prüft, ob der angegebene Chunk alle notwendigen Nachbarn (26 Nachbarn) geladen hat.
    /// </summary>
    /// <param name="provider">Der <see cref="ChunkProvider"/> für die Chunk-Daten.</param>
    /// <param name="c">Die Koordinaten des zu prüfenden Chunks.</param>
    /// <returns><c>true</c>, wenn alle Nachbarn geladen sind, andernfalls <c>false</c>.</returns>
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
