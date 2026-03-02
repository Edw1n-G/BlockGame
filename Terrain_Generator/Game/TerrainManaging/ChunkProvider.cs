using System.Collections.Concurrent;
using Basics.Game.TerrainManaging.Generation;
using Basics.Game.TerrainManaging.Meshing;
using Basics.Utilities;


//=============================================
// THIS CODE IS BASED ON AI - USE WITH CAUTION
//=============================================
namespace Basics.Game.TerrainManaging;

/// <summary>
/// Verwaltet den Chunk-Lebenszyklus: Laden von Disk (Placeholder), Generieren, Speichern.
/// Zentraler Speicher für alle geladenen Chunks.
/// </summary>
public class ChunkProvider
{
    public static readonly Dictionary<ChunkCoord, ChunkMesher> LoadedChunks = new();
    public static ConcurrentDictionary<ChunkCoord, int[]> Chunkdata = new();//Die Blockdaten
    private ConcurrentDictionary<ChunkCoord, byte> _queuedForMeshing = new();// Nur damit nicht mehrere Threads den selben Chunk meshen
    public ConcurrentQueue<ChunkCoord> MeshingQueue = new(); // Chunks die bereit für das Meshing sind (haben alle Nachbarn und ihre Blockdaten)
    public ConcurrentQueue<ChunkMesher> UploadQueue = new(); // Chunks die fertig gemesht sind und auf die GPU sollen
    private readonly TerrainGenerator _terrainGenerator;
    
    
    private bool _isRunning = true;

    public ChunkProvider(TerrainGenerator terrainGenerator, int meshingThreads)
    {
        _terrainGenerator = terrainGenerator;
        for (int i = 0; i < meshingThreads; i++)
        {
            Task.Run(MeshingWorkerLoop);
        }
    }

    /// <summary>
    /// Fordert einen Chunk an. Prüft zuerst, ob er schon geladen ist,
    /// dann ob er von der Festplatte geladen werden kann,
    /// und generiert ihn ansonsten neu.
    /// Ein Check um zu gucken, ob der Chunk von einem anderen Thread geladen wird fehlt
    /// </summary>
    public void RequestChunk(ChunkCoord coord)
    {
        // Bereits geladen? → nichts tun
        if (LoadedChunks.ContainsKey(coord))
            return;

        // Versuch von Festplatte zu laden (Placeholder)
        if (TryLoadFromDisk(coord, out ChunkMesher? loadedChunk))
        {
            LoadedChunks.TryAdd(coord, loadedChunk!);
            return;
        }

        int[] chunkBlocks = _terrainGenerator.GenerateChunk(coord);
        OnChunkDataGenerated(coord, chunkBlocks);
    }
    
    public void OnChunkDataGenerated(ChunkCoord coord, int[] data)
    {
        // Daten im Dictionary ablegen
        Chunkdata.TryAdd(coord, data);

        // chunk selbst prüfen
        TryQueueForMeshing(coord);

        // nachbar Chunks prüfen
        TryQueueForMeshing(new ChunkCoord(coord.X + 1, coord.Y, coord.Z));
        TryQueueForMeshing(new ChunkCoord(coord.X - 1, coord.Y, coord.Z));
        TryQueueForMeshing(new ChunkCoord(coord.X, coord.Y + 1, coord.Z));
        TryQueueForMeshing(new ChunkCoord(coord.X, coord.Y - 1, coord.Z));
        TryQueueForMeshing(new ChunkCoord(coord.X, coord.Y, coord.Z + 1));
        TryQueueForMeshing(new ChunkCoord(coord.X, coord.Y, coord.Z - 1));
    }
    
    private void TryQueueForMeshing(ChunkCoord coord)
    {
        // A: Gibt es meine eigenen Block-Daten überhaupt schon?
        if (!Chunkdata.ContainsKey(coord)) return;

        // B: Bin ich vielleicht schon längst in der Meshing-Queue oder fertig?
        if (_queuedForMeshing.ContainsKey(coord)) return;

        // C: Sind alle meine direkten Nachbarn im Dictionary?
        if (!HasAllNeighbors(coord)) return;

        // WENN WIR HIER SIND: Jackpot! Der Chunk ist zu 100% bereit für das Meshing.
    
        // Thread-sicher markieren, dass wir ihn jetzt meshen (verhindert Doppel-Jobs)
        if (_queuedForMeshing.TryAdd(coord, 0))
        {
            // Ab in den Meshing-Threadpool! 
            // (Dein Mesher kann sich die int[] Daten jetzt gefahrlos aus _chunkDataDict holen)
            MeshingQueue.Enqueue(coord); 
        }
    }
    
    private void MeshingWorkerLoop()
    {
        while (_isRunning)
        {
            if (MeshingQueue.TryDequeue(out ChunkCoord coord))
            {
                if (Chunkdata.TryGetValue(coord, out int[] BlockData))
                {
                    ChunkMesher newMesh = new ChunkMesher(coord, BlockData);
                    
                    UploadQueue.Enqueue(newMesh);
                    
                    _queuedForMeshing.TryRemove(coord, out _);
                }
            }
            else
            {
                // Wenn die Queue leer ist, schläft der Thread kurz, um die CPU nicht zu verbrennen
                Thread.Sleep(5);
            }
        }
    }

    private bool HasAllNeighbors(ChunkCoord c)
    {
        // Gucken Ob Nachbarn da sind
        return Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y, c.Z)) &&
               Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y, c.Z)) &&
               //Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y + 1, c.Z)) &&
               //Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y - 1, c.Z)) &&
               Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y, c.Z + 1)) &&
               Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y, c.Z - 1));
    }

    /// <summary>
    /// Entfernt einen Chunk aus dem Speicher und gibt seine Ressourcen frei.
    /// </summary>
    public void UnloadChunk(ChunkCoord coord)
    {
        if (LoadedChunks.Remove(coord, out ChunkMesher? chunk))
        {
            // TODO: Chunk auf Festplatte speichern bevor er entladen wird
            // SaveToDisk(coord, chunk);

            chunk.Dispose();
        }
    }

    /// <summary>
    /// Gibt alle aktuell geladenen Chunks zurück (für den Renderer).
    /// </summary>
    public IEnumerable<ChunkMesher> GetLoadedChunks()
    {
        return LoadedChunks.Values;
    }

    /// <summary>
    /// Prüft ob ein Chunk bereits geladen ist.
    /// </summary>
    public bool IsChunkLoaded(ChunkCoord coord)
    {
        return LoadedChunks.ContainsKey(coord);
    }

    /// <summary>
    /// Placeholder: Versucht einen Chunk von der Festplatte zu laden.
    /// Gibt vorerst immer false zurück.
    /// </summary>
    private bool TryLoadFromDisk(ChunkCoord coord, out ChunkMesher? chunk)
    {
        // TODO: Implementierung für Chunk-Laden von der Festplatte
        // z.B. aus einer Datei wie "chunks/{coord.X}_{coord.Y}_{coord.Z}.chunk"
        chunk = null;
        return false;
    }

    /// <summary>
    /// Gibt alle Ressourcen frei.
    /// </summary>
    public void Dispose()
    {
        foreach (ChunkMesher chunk in LoadedChunks.Values)
        {
            chunk.Dispose();
        }
        LoadedChunks.Clear();
    }
}