using System.Collections.Concurrent;
using System.Threading.Channels;
using Basics.Game.Logic.TerrainManaging.Generation;
using Basics.Game.Logic.TerrainManaging.Meshing;
using Basics.Game.Utilities;

namespace Basics.Game.Logic.TerrainManaging;

/// <summary>
/// Verwaltet den Chunk-Lebenszyklus: Laden von Disk (Placeholder), Generieren, Speichern.
/// Zentraler Speicher für alle geladenen Chunks.
///
/// Für Multiplayer Client Server funktionalität muss generieren und meshen der chunks gesplitted werden
/// </summary>
public class ChunkProvider
{
    // Chunk Daten und Meshing Anfragen
    public static ConcurrentDictionary<ChunkCoord, ChunkData> Chunkdata = new();//Die Blockdaten
    public static ConcurrentDictionary<ChunkCoord, byte> QueuedForMeshing = new();// Nur damit nicht mehrere Threads denselben Chunk meshen. byte als dummy da der datentyp vorteile hat
    
    // Buffer für Meshing
    public static readonly ConcurrentQueue<PooledMeshBuffer> VramPool = new();
    public static readonly ConcurrentQueue<List<byte>> VertexListPool = new();
    public static readonly ConcurrentQueue<List<uint>> IndexListPool = new();
    
    // Meshing Anfragen
    public static ConcurrentQueue<ChunkCoord> PendingMeshRequests = new();

    // fertig zum Upload bereite meshes 
    public ConcurrentQueue<BaseMesher> UploadQueue = new();
    
    //hochgeladene meshes
    public static readonly ConcurrentDictionary<ChunkCoord, BaseMesher> LoadedChunks = new();
    
    // meshes die entladen werden müssen
    public ConcurrentQueue<BaseMesher> UnloadQueue = new(); // Chunks die wieder aus der GPU rausmüssen
    
    //Referenzen
    private readonly TerrainGenerator _terrainGenerator;
    private readonly JobScheduler _jobScheduler;
    
    private bool _isRunning = true;
    
    public ChunkProvider(JobScheduler jobScheduler)
    {
        _jobScheduler = jobScheduler;
    }

    /// <summary>
    /// Fordert einen Chunk an. Prüft zuerst, ob er schon geladen ist,
    /// dann ob er von der Festplatte geladen werden kann,
    /// und generiert ihn ansonsten neu.
    /// </summary>
    public void RequestChunk(ChunkCoord coord)
    {
        // Bereits geladen oder Daten bereits generiert? → nichts tun
        if (LoadedChunks.ContainsKey(coord))
            return;
        
        if (Chunkdata.ContainsKey(coord))
            return;
        _jobScheduler.EnqueueLow(new ChunkLoadorGenerateJob(coord));
    }
    
    /// <summary>
    /// Wenn paar tausend meshes im ram liegen und warten bis der main thread es ins vram shiebt
    /// Ehhh Ram kabumm
    /// </summary>
    public void RequestMeshes()
    {
        int maxMeshesInRam = GameSettings.MaxChunkMeshesInRam;

       //gucken ob noch platz im ram und rausziehen zum Meshen
        while (UploadQueue.Count < maxMeshesInRam && 
               PendingMeshRequests.TryDequeue(out ChunkCoord coord))
        {
            // WorkerThreads
            _jobScheduler.EnqueueLow(new MeshgenerateJob(coord));
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
    /// Ändert einen Block an einer Weltposition und löst Re-Meshing aus.
    /// Gibt true zurück wenn der Block erfolgreich geändert wurde.
    /// </summary>
    public bool ModifyBlock(int worldX, int worldY, int worldZ, ushort newBlockId)
    {
        // Weltkoordinaten → ChunkCoord + lokale Koordinaten
        ChunkCoord chunkCoord = ChunkData.WorldToChunkCoord(worldX, worldY, worldZ);
        var (localX, localY, localZ) = ChunkData.WorldToLocal(worldX, worldY, worldZ);

        if (!Chunkdata.TryGetValue(chunkCoord, out ChunkData? chunkData))
            return false; // Chunk nicht geladen

        // Block ändern
        chunkData.SetBlock(localX, localY, localZ, newBlockId);

        // Diesen Chunk neu meshen
        RemeshChunk(chunkCoord);

        // Wenn der Block am Rand des Chunks liegt, müssen auch Nachbar-Chunks neu gemesht werden
        if (localX == 0)  RemeshChunk(new ChunkCoord(chunkCoord.X - 1, chunkCoord.Y, chunkCoord.Z, chunkCoord.LodLevel));
        if (localX == 15) RemeshChunk(new ChunkCoord(chunkCoord.X + 1, chunkCoord.Y, chunkCoord.Z, chunkCoord.LodLevel));
        if (localY == 0)  RemeshChunk(new ChunkCoord(chunkCoord.X, chunkCoord.Y - 1, chunkCoord.Z, chunkCoord.LodLevel));
        if (localY == 15) RemeshChunk(new ChunkCoord(chunkCoord.X, chunkCoord.Y + 1, chunkCoord.Z, chunkCoord.LodLevel));
        if (localZ == 0)  RemeshChunk(new ChunkCoord(chunkCoord.X, chunkCoord.Y, chunkCoord.Z - 1, chunkCoord.LodLevel));
        if (localZ == 15) RemeshChunk(new ChunkCoord(chunkCoord.X, chunkCoord.Y, chunkCoord.Z + 1, chunkCoord.LodLevel));

        return true;
    }

    /// <summary>
    /// Schickt einen Chunk zum Re-Meshing. Das alte Mesh wird erst entladen, wenn das neue fertig ist
    /// </summary>
    public void RemeshChunk(ChunkCoord coord)
    {
        if (!Chunkdata.ContainsKey(coord)) return;
        if (!HasAllNeighbors(coord)) return;

        if (QueuedForMeshing.TryAdd(coord, 1))
        {
            PendingMeshRequests.Enqueue(coord);
        }
    }

    /// <summary>
    /// Gibt die ChunkData an einer Weltposition zurück oder null, wenn nicht geladen.
    /// </summary>
    public ChunkData? GetChunkData(ChunkCoord coord)
    {
        Chunkdata.TryGetValue(coord, out ChunkData? data);
        return data;
    }

    /// <summary>
    /// Gibt die Block-ID an einer Weltposition zurück (0 = Luft/nicht geladen).
    /// </summary>
    public ushort GetBlockAt(int worldX, int worldY, int worldZ)
    {
        ChunkCoord chunkCoord = ChunkData.WorldToChunkCoord(worldX, worldY, worldZ);
        if (!Chunkdata.TryGetValue(chunkCoord, out ChunkData? chunkData))
            return 0;
        var (localX, localY, localZ) = ChunkData.WorldToLocal(worldX, worldY, worldZ);
        return chunkData.GetBlock(localX, localY, localZ);
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
        QueuedForMeshing.TryRemove(coord , out _);
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