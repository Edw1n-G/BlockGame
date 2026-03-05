using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
    public static readonly ConcurrentDictionary<ChunkCoord, BaseMesher> LoadedChunks = new();
    public static ConcurrentDictionary<ChunkCoord, byte[]> Chunkdata = new();//Die Blockdaten
    private ConcurrentDictionary<ChunkCoord, byte> _queuedForMeshing = new();// Nur damit nicht mehrere Threads den selben Chunk meshen
    public BlockingCollection<ChunkCoord> MeshingQueue = new(); // Chunks die bereit für das Meshing sind (haben alle Nachbarn und ihre Blockdaten)
    public BlockingCollection<BaseMesher> UploadQueue = new(100); // Chunks die fertig gemesht sind und auf die GPU sollen
    public ConcurrentQueue<BaseMesher> UnloadQueue = new(); // Chunks die wieder aus der GPU raus müssen
    private readonly TerrainGenerator _terrainGenerator;
    
    
    private bool _isRunning = true;

    public ChunkProvider(TerrainGenerator terrainGenerator, int meshingThreads)
    {
        _terrainGenerator = terrainGenerator;
        for (int i = 0; i < meshingThreads; i++)
        {
            // Threads machen die nicht aus dem Algemeienen Threadpool kommen
            Thread t = new Thread(MeshingWorkerLoop);
            t.IsBackground = true;
            t.Name = $"MeshingThread_{i}";
            t.Start();
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
        // Bereits geladen oder Daten bereits generiert? → nichts tun
        if (LoadedChunks.ContainsKey(coord))
            return;
        
        if (Chunkdata.ContainsKey(coord))
            return;

        // Versuch von Festplatte zu laden (Placeholder)
        if (TryLoadFromDisk(coord, out BaseMesher? loadedChunk))
        {
            LoadedChunks.TryAdd(coord, loadedChunk!);
            return;
        }
        
        // Wenn der Chunkgenerator null zurückgibt ist der Chunk nur Luft oder 
        // Nicht in der Welt 
        if (_terrainGenerator.GenerateChunk(coord) == null)
        {
            return;
        }
        byte[] chunkBlocks = _terrainGenerator.GenerateChunk(coord);
        OnChunkDataGenerated(coord, chunkBlocks);
    }
    
    public void OnChunkDataGenerated(ChunkCoord coord, byte[] data)
    {
        // Daten im Dictionary ablegen
        Chunkdata.TryAdd(coord, data);

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
            MeshingQueue.Add(coord); 
        }
    }
    
    private void MeshingWorkerLoop()
    {
        // GetConsumingEnumerable() blockiert automatisch wenn nichts drinne ist und wird selbst aktiv wenn man add aufruft
        foreach (ChunkCoord coord in MeshingQueue.GetConsumingEnumerable())
        {
            if (!_isRunning) break;

            if (Chunkdata.TryGetValue(coord, out byte[] BlockData))
            {
                BaseMesher newMesh = new Lod0Mesher(coord, BlockData);
                UploadQueue.Add(newMesh);
                _queuedForMeshing.TryRemove(coord, out _);
            }
        }
    }

    private bool HasAllNeighbors(ChunkCoord c)
    {
        // 6 direkte Nachbarn
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y, c.Z, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y, c.Z, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y + 1, c.Z, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y - 1, c.Z, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y, c.Z + 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y, c.Z - 1, c.LodLevel))) return false;

        // 12 Kanten-Nachbarn
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y + 1, c.Z + 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y + 1, c.Z - 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y - 1, c.Z + 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y - 1, c.Z - 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y, c.Z + 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y, c.Z - 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y, c.Z + 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y, c.Z - 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y + 1, c.Z, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y - 1, c.Z, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y + 1, c.Z, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y - 1, c.Z, c.LodLevel))) return false;

        // 8 Eck-Nachbarn
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y + 1, c.Z + 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y + 1, c.Z - 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y - 1, c.Z + 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y - 1, c.Z - 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y + 1, c.Z + 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y + 1, c.Z - 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y - 1, c.Z + 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y - 1, c.Z - 1, c.LodLevel))) return false;

        return true;
    }

    /// <summary>
    /// Entfernt einen Chunk aus dem Speicher und gibt seine Ressourcen frei.
    /// </summary>
    public void UnloadChunk(ChunkCoord coord)
    {
        if (LoadedChunks.TryRemove(coord, out BaseMesher? chunk))
        {
            // TODO: Chunk auf Festplatte speichern bevor er entladen wird
            // SaveToDisk(coord, chunk);
            
            UnloadQueue.Enqueue(chunk);
        }
        
        //Chunks könnten auch ohne Mesh entladen werden
        Chunkdata.TryRemove(coord, out _);
        _queuedForMeshing.TryRemove(coord, out _);
    }

    /// <summary>
    /// Gibt alle aktuell geladenen Chunks zurück (für den Renderer).
    /// </summary>
    public IEnumerable<BaseMesher> GetLoadedChunks()
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
    private bool TryLoadFromDisk(ChunkCoord coord, out BaseMesher? chunk)
    {
        // TODO: Implementierung für Chunk-Laden von der Festplatte
        chunk = null;
        return false;
    }

    /// <summary>
    /// Gibt alle Ressourcen frei.
    /// </summary>
    public void Dispose()
    {
        foreach (BaseMesher chunk in LoadedChunks.Values)
        {
            chunk.Dispose();
        }
        LoadedChunks.Clear();
    }
}